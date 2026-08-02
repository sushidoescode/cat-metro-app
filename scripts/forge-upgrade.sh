#!/usr/bin/env bash
# Forge template upgrader: classify against pristine provenance, plan, then apply explicit approval.
set -euo pipefail

command -v python3 >/dev/null 2>&1 || {
  echo "forge-upgrade: python3 is required" >&2
  exit 2
}

exec python3 -I - "$@" <<'PY'
import argparse
import difflib
import hashlib
import json
import os
import re
import secrets
import shutil
import stat
import subprocess
import sys
import tempfile
import time
import unicodedata
from pathlib import Path

STAMP_NAME = ".forge-template.json"
FORMAT = "forge-template-v1"
ALLOWED_MODES = {"100644", "100755"}
HEX64 = re.compile(r"^[0-9a-f]{64}$")
SEMVER = re.compile(
    r"^(?P<major>0|[1-9][0-9]*)\."
    r"(?P<minor>0|[1-9][0-9]*)\."
    r"(?P<patch>0|[1-9][0-9]*)"
    r"(?:-(?P<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
    r"(?:\+(?P<build>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"
)
MAX_DIFF_BYTES = 128 * 1024
HUMAN_BYPASS_ENV = "FORGE_" + "HUMAN_" + "OVERRIDE"


class UpgradeError(Exception):
    pass


def fail(message, code=2):
    print(f"forge-upgrade: REFUSED: {message}", file=sys.stderr)
    raise SystemExit(code)


def run_git(root, *args, text=False, check=True):
    command = [
        "git",
        "-c",
        "core.fsmonitor=false",
        "-c",
        "diff.external=",
        "-c",
        "core.safecrlf=false",
        "-C",
        str(root),
        *args,
    ]
    result = subprocess.run(
        command,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=text,
        env=git_environment(),
    )
    if check and result.returncode != 0:
        stderr = result.stderr.strip() if text else result.stderr.decode("utf-8", "replace").strip()
        raise UpgradeError(
            f"git {' '.join(args)} failed"
            + (f": {escape_controls(stderr)}" if stderr else "")
        )
    return result


def git_environment():
    environment = {}
    for key in ("LANG", "LC_ALL", "LC_CTYPE"):
        if key in os.environ:
            environment[key] = os.environ[key]
    path_parts = ["/usr/bin", "/bin", "/usr/sbin", "/sbin"]
    for extra in ("/usr/local/bin", "/opt/homebrew/bin", str(Path(sys.executable).parent)):
        if extra not in path_parts:
            path_parts.append(extra)
    environment["PATH"] = os.pathsep.join(path_parts)
    environment["GIT_CONFIG_GLOBAL"] = os.devnull
    environment["GIT_CONFIG_NOSYSTEM"] = "1"
    environment["GIT_OPTIONAL_LOCKS"] = "0"
    environment["GIT_TERMINAL_PROMPT"] = "0"
    environment.pop(HUMAN_BYPASS_ENV, None)
    return environment


def escape_controls(value):
    output = []
    for character in str(value):
        number = ord(character)
        if character in "\n\t":
            output.append(character)
        elif number < 32 or number == 127:
            output.append(f"\\x{number:02x}")
        else:
            output.append(character)
    return "".join(output)


def load_json(path, label):
    def reject_duplicate(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise UpgradeError(f"{label} contains duplicate JSON key {key!r}")
            result[key] = value
        return result

    try:
        with open(path, "r", encoding="utf-8") as stream:
            return json.load(stream, object_pairs_hook=reject_duplicate)
    except UpgradeError:
        raise
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise UpgradeError(f"cannot read {label}: {error}") from error


def canonical_bytes(value):
    return json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("utf-8")


def digest(value):
    return hashlib.sha256(canonical_bytes(value)).hexdigest()


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def validate_relpath(path):
    if not isinstance(path, str) or not path:
        raise UpgradeError("template manifest contains an empty or non-string path")
    if path != unicodedata.normalize("NFC", path):
        raise UpgradeError(f"template path is not NFC-normalized: {path!r}")
    if path.startswith("/") or "\\" in path or "\x00" in path:
        raise UpgradeError(f"unsafe template path: {path!r}")
    if any(ord(character) < 32 or ord(character) == 127 for character in path):
        raise UpgradeError(f"control character in template path: {path!r}")
    parts = path.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        raise UpgradeError(f"unsafe template path component: {path!r}")
    if any(part.casefold() == ".git" for part in parts):
        raise UpgradeError(f"template path enters .git: {path!r}")
    if any(":" in part for part in parts):
        raise UpgradeError(f"non-portable ':' in template path: {path!r}")
    return path


def validate_casefold_paths(paths, label):
    seen = {}
    for path in paths:
        folded = unicodedata.normalize("NFC", path).casefold()
        previous = seen.get(folded)
        if previous is not None and previous != path:
            raise UpgradeError(
                f"{label} has case/Unicode-colliding paths: {previous!r}, {path!r}"
            )
        seen[folded] = path


def validate_state(value, label):
    if not isinstance(value, dict) or set(value) != {"mode", "sha256"}:
        raise UpgradeError(f"{label} must contain exactly mode and sha256")
    mode = value["mode"]
    checksum = value["sha256"]
    if mode not in ALLOWED_MODES:
        raise UpgradeError(f"{label} has unsupported mode {mode!r}")
    if not isinstance(checksum, str) or not HEX64.fullmatch(checksum):
        raise UpgradeError(f"{label} has invalid sha256")
    return {"mode": mode, "sha256": checksum}


def validate_files_map(value, label):
    if not isinstance(value, dict):
        raise UpgradeError(f"{label} must be an object")
    result = {}
    for path, state_value in value.items():
        validate_relpath(path)
        result[path] = validate_state(state_value, f"{label}[{path!r}]")
    validate_casefold_paths(result, label)
    return result


def parse_semver(value, label):
    match = SEMVER.fullmatch(value) if isinstance(value, str) else None
    if match is None:
        raise UpgradeError(f"{label} is not a supported semantic version: {value!r}")
    prerelease = match.group("prerelease")
    if prerelease is not None:
        for identifier in prerelease.split("."):
            if (
                identifier.isdigit()
                and len(identifier) > 1
                and identifier.startswith("0")
            ):
                raise UpgradeError(
                    f"{label} is not a supported semantic version: {value!r}"
                )
    numbers = tuple(
        int(match.group(component)) for component in ("major", "minor", "patch")
    )
    return numbers, prerelease


def semver_greater(left, right):
    left_numbers, left_pre = parse_semver(left, "template version")
    right_numbers, right_pre = parse_semver(right, "kit version")
    if left_numbers != right_numbers:
        return left_numbers > right_numbers
    if left_pre is None and right_pre is not None:
        return True
    if left_pre is not None and right_pre is None:
        return False
    left_parts = (left_pre or "").split(".")
    right_parts = (right_pre or "").split(".")
    for left_part, right_part in zip(left_parts, right_parts):
        if left_part == right_part:
            continue
        left_numeric = left_part.isdigit()
        right_numeric = right_part.isdigit()
        if left_numeric and right_numeric:
            return int(left_part) > int(right_part)
        if left_numeric != right_numeric:
            return not left_numeric
        return left_part > right_part
    return len(left_parts) > len(right_parts)


def catalog_id(template_version, release, files):
    return digest(
        {
            "template_version": template_version,
            "release": release,
            "files": files,
        }
    )


def validate_stamp(value, label, require_current=False):
    required = {"format", "kit_version", "current", "catalog", "files"}
    if not isinstance(value, dict) or set(value) != required:
        raise UpgradeError(f"{label} must contain exactly {sorted(required)}")
    if value["format"] != FORMAT:
        raise UpgradeError(f"{label} has unsupported format {value['format']!r}")
    parse_semver(value["kit_version"], f"{label} kit_version")
    if not isinstance(value["current"], str) or not HEX64.fullmatch(value["current"]):
        raise UpgradeError(f"{label} current baseline id is invalid")
    if not isinstance(value["catalog"], dict) or not value["catalog"]:
        raise UpgradeError(f"{label} catalog must be a non-empty object")

    catalog = {}
    for identifier, entry in value["catalog"].items():
        if not isinstance(identifier, str) or not HEX64.fullmatch(identifier):
            raise UpgradeError(f"{label} catalog id is invalid")
        if not isinstance(entry, dict) or set(entry) != {
            "template_version",
            "files",
            "release",
        }:
            raise UpgradeError(
                f"{label} catalog[{identifier}] must contain template_version, release, and files"
            )
        parse_semver(entry["template_version"], f"{label} catalog version")
        release = entry["release"]
        if release is not None:
            expected_release = f"v{entry['template_version']}"
            if not isinstance(release, str) or release != expected_release:
                raise UpgradeError(
                    f"{label} catalog[{identifier}] release tag must equal "
                    f"{expected_release!r}"
                )
        files = validate_files_map(
            entry["files"], f"{label} catalog[{identifier}].files"
        )
        if catalog_id(entry["template_version"], release, files) != identifier:
            raise UpgradeError(f"{label} catalog[{identifier}] digest mismatch")
        catalog[identifier] = {
            "template_version": entry["template_version"],
            "release": release,
            "files": files,
        }
    if value["current"] not in catalog:
        raise UpgradeError(f"{label} current baseline is absent from catalog")
    if (
        catalog[value["current"]]["template_version"] != value["kit_version"]
    ):
        raise UpgradeError(f"{label} current baseline version disagrees with kit_version")

    if not isinstance(value["files"], dict):
        raise UpgradeError(f"{label} files ledger must be an object")
    ledger = {}
    for path, identifier in value["files"].items():
        validate_relpath(path)
        if not isinstance(identifier, str) or identifier not in catalog:
            raise UpgradeError(f"{label} ledger source is unknown for {path!r}")
        if path not in catalog[identifier]["files"]:
            raise UpgradeError(
                f"{label} ledger source does not contain path {path!r}"
            )
        ledger[path] = identifier
    validate_casefold_paths(ledger, f"{label} ledger")

    current_files = catalog[value["current"]]["files"]
    if require_current:
        if ledger != {path: value["current"] for path in current_files}:
            raise UpgradeError(
                f"{label} kit ledger must map every current path to current baseline"
            )
    return {
        "format": FORMAT,
        "kit_version": value["kit_version"],
        "current": value["current"],
        "catalog": catalog,
        "files": ledger,
    }


def filesystem_state(path, include_data=False):
    try:
        details = os.lstat(path)
    except FileNotFoundError:
        return {"kind": "absent"}
    except OSError as error:
        raise UpgradeError(f"cannot inspect {path}: {error}") from error
    if stat.S_ISLNK(details.st_mode):
        target = os.readlink(path).encode("utf-8", "surrogateescape")
        return {
            "kind": "unsafe-symlink",
            "sha256": sha256_bytes(target),
        }
    if not stat.S_ISREG(details.st_mode):
        return {"kind": "unsafe-special"}
    descriptor = None
    try:
        flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
        flags |= getattr(os, "O_NONBLOCK", 0)
        descriptor = os.open(path, flags)
        opened = os.fstat(descriptor)
        if (
            not stat.S_ISREG(opened.st_mode)
            or opened.st_dev != details.st_dev
            or opened.st_ino != details.st_ino
        ):
            raise UpgradeError(f"path changed while opening {path}")
        chunks = []
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
        after = os.fstat(descriptor)
        if (
            opened.st_size != after.st_size
            or opened.st_mtime_ns != after.st_mtime_ns
            or opened.st_ctime_ns != after.st_ctime_ns
        ):
            raise UpgradeError(f"path changed while reading {path}")
        data = b"".join(chunks)
    except UpgradeError:
        raise
    except OSError as error:
        raise UpgradeError(f"cannot read {path}: {error}") from error
    finally:
        if descriptor is not None:
            os.close(descriptor)
    result = {
        "kind": "file",
        "mode": "100755" if opened.st_mode & 0o111 else "100644",
        "sha256": sha256_bytes(data),
    }
    if include_data:
        result["data"] = data
    return result


def manifest_state(value):
    if value is None:
        return {"kind": "absent"}
    return {
        "kind": "file",
        "mode": value["mode"],
        "sha256": value["sha256"],
    }


def comparable_state(value):
    return {key: item for key, item in value.items() if key != "data"}


def states_equal(left, right):
    return comparable_state(left) == comparable_state(right)


def ensure_regular_ancestors(root, relative, allow_missing=False):
    current = root
    parts = relative.split("/")[:-1]
    for part in parts:
        current = current / part
        try:
            details = os.lstat(current)
        except FileNotFoundError:
            if allow_missing:
                continue
            raise UpgradeError(f"missing parent directory for {relative!r}")
        if stat.S_ISLNK(details.st_mode) or not stat.S_ISDIR(details.st_mode):
            raise UpgradeError(f"unsafe parent for template path {relative!r}")


def list_template_worktree(template_root):
    result = {}
    for directory, dirnames, filenames in os.walk(template_root, topdown=True, followlinks=False):
        directory_path = Path(directory)
        safe_directories = []
        for name in sorted(dirnames):
            child = directory_path / name
            details = os.lstat(child)
            if stat.S_ISLNK(details.st_mode):
                raise UpgradeError(f"kit template contains symlink directory {child}")
            if not stat.S_ISDIR(details.st_mode):
                raise UpgradeError(f"kit template contains special directory entry {child}")
            safe_directories.append(name)
        dirnames[:] = safe_directories
        for name in sorted(filenames):
            path = directory_path / name
            relative = path.relative_to(template_root).as_posix()
            if relative == STAMP_NAME:
                continue
            validate_relpath(relative)
            state_value = filesystem_state(path)
            if state_value["kind"] != "file":
                raise UpgradeError(f"kit template path is not a regular file: {relative}")
            result[relative] = {
                "mode": state_value["mode"],
                "sha256": state_value["sha256"],
            }
    validate_casefold_paths(result, "kit template")
    return result


def resolve_git_root(path, label):
    candidate = Path(path).resolve()
    result = run_git(candidate, "rev-parse", "--show-toplevel", text=True)
    root = Path(result.stdout.strip()).resolve()
    if root != candidate:
        raise UpgradeError(f"{label} must name the Git worktree root ({root})")
    bare = run_git(root, "rev-parse", "--is-bare-repository", text=True).stdout.strip()
    if bare != "false":
        raise UpgradeError(f"{label} is a bare repository")
    run_git(root, "rev-parse", "-q", "--verify", "HEAD^{commit}")
    return root


def ensure_not_nested(project, kit):
    project_text = str(project)
    kit_text = str(kit)
    common = os.path.commonpath([project_text, kit_text])
    if common in {project_text, kit_text}:
        raise UpgradeError("project and kit worktrees must not overlap or contain one another")


def ensure_clean(root, label):
    status_result = run_git(
        root,
        "status",
        "--porcelain=v1",
        "-z",
        "--untracked-files=all",
        "--ignore-submodules=none",
    )
    if status_result.stdout:
        raise UpgradeError(f"{label} worktree is dirty; commit or remove all changes first")

    if run_git(root, "ls-files", "-u", "-z").stdout:
        raise UpgradeError(f"{label} index has unmerged entries")

    sparse = run_git(root, "config", "--bool", "core.sparseCheckout", text=True, check=False)
    if sparse.returncode not in {0, 1}:
        raise UpgradeError(f"cannot inspect {label} sparse-checkout configuration")
    if sparse.returncode == 0 and sparse.stdout.strip().lower() == "true":
        raise UpgradeError(f"{label} uses sparse checkout")

    flags = run_git(root, "ls-files", "-v", "-z").stdout.split(b"\0")
    for record in flags:
        if not record:
            continue
        if len(record) < 3 or record[1:2] != b" ":
            raise UpgradeError(f"cannot parse {label} index flags")
        marker = chr(record[0])
        if marker == "S" or marker.islower():
            raise UpgradeError(
                f"{label} has skip-worktree or assume-unchanged index entries"
            )

    git_dir_text = run_git(root, "rev-parse", "--absolute-git-dir", text=True).stdout.strip()
    git_dir = Path(git_dir_text)
    operation_markers = (
        "MERGE_HEAD",
        "CHERRY_PICK_HEAD",
        "REVERT_HEAD",
        "BISECT_LOG",
        "rebase-apply",
        "rebase-merge",
        "sequencer",
    )
    if any((git_dir / marker).exists() for marker in operation_markers):
        raise UpgradeError(f"{label} has a Git operation in progress")


def git_index_entries(root, label):
    records = run_git(root, "ls-files", "--stage", "-z").stdout.split(b"\0")
    result = {}
    for record in records:
        if not record:
            continue
        try:
            metadata, raw_path = record.split(b"\t", 1)
            mode_raw, _object_id, stage_raw = metadata.split(b" ")
        except ValueError as error:
            raise UpgradeError(f"cannot parse {label} tracked entries") from error
        if stage_raw != b"0":
            raise UpgradeError(f"{label} has unmerged tracked entries")
        try:
            path = raw_path.decode("utf-8")
            mode = mode_raw.decode("ascii")
        except UnicodeDecodeError as error:
            raise UpgradeError(f"{label} has a non-UTF-8 tracked path") from error
        if path in result:
            raise UpgradeError(f"{label} repeats tracked path {path!r}")
        result[path] = mode
    validate_casefold_paths(result, f"{label} tracked files")
    return result


def git_tracked_paths(root, label):
    return set(git_index_entries(root, label))


def release_template_files(root, tag):
    reference = f"refs/tags/{tag}"
    try:
        plugin_tree = run_git(
            root,
            "ls-tree",
            "-z",
            reference,
            "--",
            "forge/.claude-plugin/plugin.json",
        ).stdout
        template_root_tree = run_git(
            root,
            "ls-tree",
            "-z",
            reference,
            "--",
            "template",
        ).stdout
        template_tree = run_git(
            root,
            "ls-tree",
            "-r",
            "-z",
            f"{reference}:template",
        ).stdout
    except UpgradeError as error:
        raise UpgradeError(
            f"kit release evidence tag {tag!r} is unavailable"
        ) from error

    plugin_records = [record for record in plugin_tree.split(b"\0") if record]
    if len(plugin_records) != 1:
        raise UpgradeError(
            f"kit release evidence tag {tag!r} has no unique plugin.json"
        )
    try:
        plugin_metadata, plugin_path_raw = plugin_records[0].split(b"\t", 1)
        plugin_mode, plugin_kind, plugin_oid = plugin_metadata.decode("ascii").split()
        plugin_path = plugin_path_raw.decode("utf-8")
    except (UnicodeError, ValueError) as error:
        raise UpgradeError(
            f"kit release evidence tag {tag!r} has invalid plugin tree data"
        ) from error
    if (
        plugin_path != "forge/.claude-plugin/plugin.json"
        or plugin_kind != "blob"
        or plugin_mode not in ALLOWED_MODES
    ):
        raise UpgradeError(
            f"kit release evidence tag {tag!r} plugin.json is not a regular file"
        )
    template_root_records = [
        record for record in template_root_tree.split(b"\0") if record
    ]
    if len(template_root_records) != 1:
        raise UpgradeError(
            f"kit release evidence tag {tag!r} has no unique template tree"
        )
    try:
        root_metadata, root_path_raw = template_root_records[0].split(b"\t", 1)
        root_mode, root_kind, _root_oid = root_metadata.decode("ascii").split()
        root_path = root_path_raw.decode("utf-8")
    except (UnicodeError, ValueError) as error:
        raise UpgradeError(
            f"kit release evidence tag {tag!r} has invalid template root data"
        ) from error
    if (
        root_path != "template"
        or root_kind != "tree"
        or root_mode != "040000"
    ):
        raise UpgradeError(
            f"kit release evidence tag {tag!r} template is not a directory"
        )
    try:
        plugin_raw = run_git(root, "cat-file", "blob", plugin_oid).stdout
        plugin = json.loads(plugin_raw.decode("utf-8"))
    except UpgradeError:
        raise
    except (UnicodeError, json.JSONDecodeError) as error:
        raise UpgradeError(
            f"kit release evidence tag {tag!r} has invalid plugin.json"
        ) from error
    if not isinstance(plugin, dict) or not isinstance(plugin.get("version"), str):
        raise UpgradeError(
            f"kit release evidence tag {tag!r} has no plugin version"
        )

    files = {}
    for record in template_tree.split(b"\0"):
        if not record:
            continue
        try:
            metadata, raw_path = record.split(b"\t", 1)
            mode, kind, object_id = metadata.decode("ascii").split()
            path = raw_path.decode("utf-8")
        except (UnicodeError, ValueError) as error:
            raise UpgradeError(
                f"kit release evidence tag {tag!r} has invalid template tree data"
            ) from error
        validate_relpath(path)
        if kind != "blob" or mode not in ALLOWED_MODES:
            raise UpgradeError(
                f"kit release evidence tag {tag!r} contains "
                f"non-regular template path {path!r}"
            )
        if path == STAMP_NAME:
            continue
        if path in files:
            raise UpgradeError(
                f"kit release evidence tag {tag!r} repeats {path!r}"
            )
        data = run_git(root, "cat-file", "blob", object_id).stdout
        files[path] = {
            "mode": mode,
            "sha256": sha256_bytes(data),
        }
    validate_casefold_paths(files, f"kit release evidence tag {tag!r}")
    return plugin["version"], files


def validate_release_catalog(root, stamp):
    for identifier, entry in stamp["catalog"].items():
        tag = entry["release"]
        if tag is None:
            continue
        version, files = release_template_files(root, tag)
        if version != entry["template_version"]:
            raise UpgradeError(
                f"kit release evidence tag {tag!r} plugin version does not "
                f"match catalog[{identifier}]"
            )
        if files != entry["files"]:
            raise UpgradeError(
                f"kit release evidence tag {tag!r} template does not "
                f"match catalog[{identifier}]"
            )


def path_is_ignored(project, path):
    result = run_git(
        project,
        "check-ignore",
        "--no-index",
        "-q",
        "--",
        path,
        check=False,
    )
    if result.returncode == 0:
        return True
    if result.returncode == 1:
        return False
    raise UpgradeError(f"cannot determine whether project path is ignored: {path!r}")


def index_digest(root):
    return sha256_bytes(run_git(root, "ls-files", "--stage", "-z").stdout)


def git_head_ref(root):
    symbolic = run_git(
        root, "symbolic-ref", "-q", "HEAD", text=True, check=False
    )
    if symbolic.returncode == 0:
        return symbolic.stdout.strip()
    if symbolic.returncode == 1:
        return None
    raise UpgradeError("cannot inspect the current project HEAD reference")


def git_identity(root):
    details = os.stat(root)
    return {
        "root": str(root),
        "device": details.st_dev,
        "inode": details.st_ino,
        "head_ref": git_head_ref(root),
        "head": run_git(root, "rev-parse", "HEAD", text=True).stdout.strip(),
        "head_tree": run_git(root, "rev-parse", "HEAD^{tree}", text=True).stdout.strip(),
        "index": index_digest(root),
    }


def load_kit(root, require_clean=True):
    kit_root = resolve_git_root(root, "kit")
    if require_clean:
        ensure_clean(kit_root, "kit")
    plugin_path = kit_root / "forge" / ".claude-plugin" / "plugin.json"
    stamp_path = kit_root / "template" / STAMP_NAME
    entries = git_index_entries(kit_root, "kit")
    tracked = set(entries) if require_clean else None
    fixed_inputs = {
        "forge/.claude-plugin/plugin.json",
        f"template/{STAMP_NAME}",
    }
    if tracked is not None:
        missing = sorted(fixed_inputs - tracked)
        if missing:
            raise UpgradeError(
                "kit upgrade inputs must be tracked: " + ", ".join(missing)
            )
    for index_path, mode in entries.items():
        prefix = "template/"
        if not index_path.startswith(prefix):
            continue
        relative = index_path[len(prefix):]
        if relative == STAMP_NAME:
            continue
        if mode not in ALLOWED_MODES:
            raise UpgradeError(
                f"kit template path is not a regular file: {relative}"
            )
    for path, label in (
        (plugin_path, "kit plugin.json"),
        (stamp_path, "kit template stamp"),
    ):
        if filesystem_state(path)["kind"] != "file":
            raise UpgradeError(f"{label} must be a regular file")
    plugin = load_json(plugin_path, "kit plugin.json")
    if not isinstance(plugin, dict) or not isinstance(plugin.get("version"), str):
        raise UpgradeError("kit plugin.json has no string version")
    parse_semver(plugin["version"], "kit plugin version")
    stamp = validate_stamp(load_json(stamp_path, "kit template stamp"), "kit template stamp", True)
    if stamp["kit_version"] != plugin["version"]:
        raise UpgradeError(
            "kit template stamp version does not match forge/.claude-plugin/plugin.json"
        )
    validate_release_catalog(kit_root, stamp)
    actual_files = list_template_worktree(kit_root / "template")
    if tracked is not None:
        required_sources = {f"template/{path}" for path in actual_files}
        missing = sorted(required_sources - tracked)
        if missing:
            raise UpgradeError(
                "kit template sources must be tracked: " + ", ".join(missing)
            )
        tracked_sources = {
            path[len("template/"):]
            for path in tracked
            if path.startswith("template/")
            and path != f"template/{STAMP_NAME}"
        }
        omitted = sorted(tracked_sources - set(actual_files))
        if omitted:
            raise UpgradeError(
                "tracked kit template sources are not materialized regular files: "
                + ", ".join(omitted)
            )
    current_files = stamp["catalog"][stamp["current"]]["files"]
    if actual_files != current_files:
        missing = sorted(set(current_files) - set(actual_files))
        extra = sorted(set(actual_files) - set(current_files))
        changed = sorted(
            path
            for path in set(actual_files) & set(current_files)
            if actual_files[path] != current_files[path]
        )
        details = []
        if missing:
            details.append("missing=" + ",".join(missing))
        if extra:
            details.append("extra=" + ",".join(extra))
        if changed:
            details.append("changed=" + ",".join(changed))
        raise UpgradeError(
            "kit template does not match its current manifest"
            + (": " + "; ".join(details) if details else "")
            + " — after editing template/, regenerate the stamp with"
            " `python3 forge/skills/forge-upgrade/tests/generate-stamp.py`"
            " and commit template/.forge-template.json alongside the change"
        )
    return {
        "root": kit_root,
        "identity": git_identity(kit_root),
        "version": plugin["version"],
        "stamp": stamp,
        "stamp_digest": digest(stamp),
        "files": current_files,
    }


def project_stamp(project):
    path = project / STAMP_NAME
    try:
        details = os.lstat(path)
    except FileNotFoundError:
        return None
    if stat.S_ISLNK(details.st_mode) or not stat.S_ISREG(details.st_mode):
        raise UpgradeError(f"{STAMP_NAME} must be a regular file")
    return validate_stamp(load_json(path, "project template stamp"), "project template stamp")


def catalog_candidates(kit_stamp, project, requested_version=None):
    all_paths = sorted(
        {
            path
            for entry in kit_stamp["catalog"].values()
            for path in entry["files"]
        }
    )
    project_states = {}
    for path in all_paths:
        ensure_regular_ancestors(project, path, allow_missing=True)
        project_states[path] = filesystem_state(project / path)

    candidates = []
    for identifier, entry in kit_stamp["catalog"].items():
        version = entry["template_version"]
        if requested_version is not None and version != requested_version:
            continue
        matches = 0
        compatible_absences = 0
        mismatches = 0
        for path in all_paths:
            baseline = manifest_state(entry["files"].get(path))
            project_value = project_states[path]
            if (
                baseline["kind"] == "file"
                and project_value["kind"] == "file"
                and states_equal(project_value, baseline)
            ):
                matches += 1
            elif baseline["kind"] == "absent" and project_value["kind"] == "absent":
                compatible_absences += 1
            else:
                mismatches += 1
        candidates.append(
            (
                matches,
                -mismatches,
                1 if entry["release"] is not None else 0,
                compatible_absences,
                identifier,
                entry,
            )
        )
    if requested_version is not None and not candidates:
        raise UpgradeError(
            f"requested legacy version {requested_version!r} is not in the kit catalog"
        )
    if not candidates:
        raise UpgradeError("kit has no historical baseline candidates")
    if requested_version is not None:
        released = [candidate for candidate in candidates if candidate[2] == 1]
        if not released:
            raise UpgradeError(
                f"requested legacy version {requested_version!r} has no "
                "tagged release baseline in the kit catalog"
            )
        candidates = released
    candidates.sort(
        key=lambda item: (item[0], item[1], item[2], item[3], item[4]),
        reverse=True,
    )
    best = candidates[0]
    if requested_version is None and best[0] < 3:
        raise UpgradeError(
            "legacy template version cannot be inferred safely "
            "(fewer than three pristine file matches)"
        )
    tied = [
        candidate
        for candidate in candidates
        if candidate[:4] == best[:4]
    ]
    if len(tied) > 1:
        unique_manifests = {
            canonical_bytes(candidate[5]["files"]) for candidate in tied
        }
        versions = sorted({candidate[5]["template_version"] for candidate in tied})
        if len(versions) > 1 or len(unique_manifests) > 1:
            raise UpgradeError(
                "legacy template version is ambiguous among "
                + ", ".join(versions)
                + "; rerun with --from-version after human confirmation"
            )
    return best[4], best[5], best[0]


def load_baseline(project, kit, requested_version):
    stamp = project_stamp(project)
    if stamp is None:
        identifier, entry, matches = catalog_candidates(
            kit["stamp"], project, requested_version
        )
        if semver_greater(entry["template_version"], kit["version"]):
            raise UpgradeError("refusing to downgrade from a newer legacy template")
        return {
            "kind": "legacy",
            "display": (
                f"{entry['template_version']} (legacy confirmed)"
                if requested_version is not None
                else f"{entry['template_version']} (legacy inferred)"
            ),
            "ledger": {path: identifier for path in entry["files"]},
            "source_stamp": None,
            "matches": matches,
            "confirmed_version": requested_version,
        }

    if requested_version is not None:
        raise UpgradeError("--from-version is only valid for an unstamped legacy project")

    ledger = {}
    versions = set()
    for path, identifier in stamp["files"].items():
        if identifier not in kit["stamp"]["catalog"]:
            raise UpgradeError(
                f"project baseline {identifier} for {path!r} is not trusted by this kit"
            )
        entry = kit["stamp"]["catalog"][identifier]
        if path not in entry["files"]:
            raise UpgradeError(f"project baseline source lacks {path!r}")
        version = entry["template_version"]
        if semver_greater(version, kit["version"]):
            raise UpgradeError(
                f"refusing to downgrade project path {path!r} from {version}"
            )
        versions.add(version)
        ledger[path] = identifier
    if not versions:
        display = "empty (stamped)"
    elif len(versions) == 1:
        display = f"{next(iter(versions))} (stamped)"
    else:
        display = "mixed stamped: " + ", ".join(sorted(versions))
    return {
        "kind": "stamped",
        "display": display,
        "ledger": ledger,
        "source_stamp": stamp,
        "matches": None,
        "confirmed_version": None,
    }


def immutable_path(path):
    lower = unicodedata.normalize("NFC", path).casefold()
    if lower == "evals/results" or lower.startswith("evals/results/"):
        return lower == "evals/results/attested" or lower.startswith(
            "evals/results/attested/"
        )
    return (
        lower == "tests/contract"
        or lower.startswith("tests/contract/")
        or lower == "docs/constitution.md"
        or lower == ".claude/hooks"
        or lower.startswith(".claude/hooks/")
        or lower == "scripts/git-hooks"
        or lower.startswith("scripts/git-hooks/")
        or lower == "state/mode"
        or lower == "state/trust.json"
        or lower == "evals"
        or lower.startswith("evals/")
        or lower == ".github/workflows"
        or lower.startswith(".github/workflows/")
        or lower in {"codeowners", ".github/codeowners", "docs/codeowners"}
    )


def manual_policy_path(path):
    lower = unicodedata.normalize("NFC", path).casefold()
    return (
        lower == ".gitignore"
        or lower.endswith("/.gitignore")
        or lower in {
        ".claude/settings.json",
        ".gitattributes",
        ".gitmodules",
        "agents.md",
        "claude.md",
        }
    )


def baseline_state(path, baseline, kit_stamp):
    identifier = baseline["ledger"].get(path)
    if identifier is None:
        return {"kind": "absent"}
    entry = kit_stamp["catalog"].get(identifier)
    if entry is None or path not in entry["files"]:
        raise UpgradeError(f"baseline provenance is missing for {path!r}")
    return manifest_state(entry["files"][path])


def classify(base, project, kit):
    if states_equal(project, kit):
        return "identical"
    if base["kind"] == "absent" and project["kind"] == "absent" and kit["kind"] == "file":
        return "new-in-kit"
    if states_equal(project, base):
        return "kit-changed-only"
    if states_equal(kit, base):
        return "locally-modified-only"
    return "both-changed"


def action_for(path, classification, kit_state):
    if classification == "identical":
        return "none"
    if classification in {"new-in-kit", "kit-changed-only"}:
        if immutable_path(path):
            return "manual-immutable"
        if manual_policy_path(path):
            return "manual-policy"
        if kit_state["kind"] == "absent":
            return "auto-delete"
        if classification == "new-in-kit":
            return "auto-add"
        return "auto-update"
    if classification == "locally-modified-only":
        return "skip"
    return "conflict"


def plan(project_arg, kit_arg, requested_version=None):
    project = resolve_git_root(project_arg, "project")
    kit = load_kit(kit_arg, require_clean=True)
    ensure_not_nested(project, kit["root"])
    ensure_clean(project, "project")
    baseline = load_baseline(project, kit, requested_version)

    all_paths = sorted(set(baseline["ledger"]) | set(kit["files"]))
    validate_casefold_paths(all_paths, "managed path union")
    tracked_set = git_tracked_paths(project, "project")
    stamp_ignored = (
        STAMP_NAME not in tracked_set
        and path_is_ignored(project, STAMP_NAME)
    )

    items = []
    for path in all_paths:
        ensure_regular_ancestors(project, path, allow_missing=True)
        base = baseline_state(path, baseline, kit["stamp"])
        project_value = filesystem_state(project / path, include_data=True)
        kit_manifest = kit["files"].get(path)
        if kit_manifest is None:
            kit_value = {"kind": "absent"}
        else:
            kit_value = filesystem_state(
                kit["root"] / "template" / path, include_data=True
            )
            if not states_equal(kit_value, manifest_state(kit_manifest)):
                raise UpgradeError(f"kit source changed while planning: {path}")
        classification = classify(base, project_value, kit_value)
        action = action_for(path, classification, kit_value)
        ignored = path not in tracked_set and path_is_ignored(project, path)
        if ignored and action in {
            "none",
            "auto-add",
            "auto-delete",
            "auto-update",
        }:
            action = "skip-ignored"
        items.append(
            {
                "path": path,
                "base": comparable_state(base),
                "project": comparable_state(project_value),
                "kit": comparable_state(kit_value),
                "project_data": project_value.get("data"),
                "kit_data": kit_value.get("data"),
                "classification": classification,
                "action": action,
                "immutable": immutable_path(path),
                "manual_policy": manual_policy_path(path),
                "ignored": ignored,
            }
        )

    identity = git_identity(project)
    payload_items = [
        {
            key: value
            for key, value in item.items()
            if key not in {"project_data", "kit_data"}
        }
        for item in items
    ]
    payload = {
        "format": "forge-upgrade-plan-v1",
        "project": identity,
        "kit": {
            **kit["identity"],
            "version": kit["version"],
            "current": kit["stamp"]["current"],
            "stamp": kit["stamp_digest"],
        },
        "baseline": {
            "kind": baseline["kind"],
            "ledger": baseline["ledger"],
            "confirmed_version": baseline["confirmed_version"],
        },
        "stamp_ignored": stamp_ignored,
        "items": payload_items,
    }
    return {
        "project": project,
        "kit": kit,
        "baseline": baseline,
        "items": items,
        "stamp_ignored": stamp_ignored,
        "payload": payload,
        "id": digest(payload),
    }


def state_description(value):
    if value["kind"] == "absent":
        return "absent"
    if value["kind"] != "file":
        return value["kind"]
    return f"{value['mode']} sha256:{value['sha256']}"


def diff_lines(item):
    old = item["project_data"]
    new = item["kit_data"]
    if old is None and item["project"]["kind"] != "absent":
        return [
            f"  project: {state_description(item['project'])}",
            f"  kit:     {state_description(item['kit'])}",
        ]
    if new is None and item["kit"]["kind"] != "absent":
        return [
            f"  project: {state_description(item['project'])}",
            f"  kit:     {state_description(item['kit'])}",
        ]
    old_bytes = old or b""
    new_bytes = new or b""
    if len(old_bytes) + len(new_bytes) > MAX_DIFF_BYTES:
        return [
            "  diff omitted: file pair exceeds 128 KiB",
            f"  project: {state_description(item['project'])}",
            f"  kit:     {state_description(item['kit'])}",
        ]
    try:
        old_text = old_bytes.decode("utf-8").splitlines(keepends=True)
        new_text = new_bytes.decode("utf-8").splitlines(keepends=True)
    except UnicodeDecodeError:
        return [
            "  binary diff (hashes only)",
            f"  project: {state_description(item['project'])}",
            f"  kit:     {state_description(item['kit'])}",
        ]
    path = item["path"]
    from_name = f"a/{path}" if old is not None else "/dev/null"
    to_name = f"b/{path}" if new is not None else "/dev/null"
    output = []
    if item["project"].get("mode") != item["kit"].get("mode"):
        output.append(
            f"  mode {item['project'].get('mode', 'absent')} -> "
            f"{item['kit'].get('mode', 'absent')}"
        )
    for line in difflib.unified_diff(
        old_text, new_text, fromfile=from_name, tofile=to_name
    ):
        output.append(escape_controls(line.rstrip("\n")))
    if not output:
        output.append("  content diff unavailable; state differs by type")
    return output


def print_plan(result):
    print("Forge template upgrade plan")
    print(f"project: {result['project']}")
    print(f"project template: {result['baseline']['display']}")
    if result["baseline"]["matches"] is not None:
        print(f"legacy evidence: {result['baseline']['matches']} pristine path matches")
    print(f"kit template: {result['kit']['version']}")
    print()
    for item in result["items"]:
        print(
            f"{item['classification']} | {item['action']} | "
            f"{escape_controls(item['path'])}"
        )
    print()
    changed = [
        item
        for item in result["items"]
        if (
            item["action"]
            in {
                "auto-add",
                "auto-delete",
                "auto-update",
                "manual-immutable",
                "manual-policy",
            }
            or (
                (item["immutable"] or item["manual_policy"])
                and item["classification"] != "identical"
                and not states_equal(item["base"], item["kit"])
            )
        )
    ]
    if changed:
        print("Planned and manual diffs (project -> kit):")
        for item in changed:
            print(f"diff | {item['action']} | {escape_controls(item['path'])}")
            for line in diff_lines(item):
                print(line)
            if item["immutable"] or item["manual_policy"]:
                print(
                    "  MANUAL ONLY: review and apply this change yourself; "
                    "forge-upgrade will never write this path."
                )
    else:
        print("No template content differs.")
    print()
    if result["stamp_ignored"]:
        print(
            f"metadata | blocked-ignored | {STAMP_NAME} "
            "(commit an ignore-rule correction before apply)"
        )
    else:
        print(
            f"metadata | update-on-apply | {STAMP_NAME} "
            "(records only approved or already-identical provenance)"
        )
    print(f"plan-id: {result['id']}")
    print("No files written.")


def open_parent_dir(root_fd, relative, create, created_directories=None):
    parts = relative.split("/")
    current_fd = None
    traversed = []
    created_here = []
    try:
        current_fd = os.dup(root_fd)
        for part in parts[:-1]:
            traversed.append(part)
            flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
            flags |= getattr(os, "O_NOFOLLOW", 0)
            try:
                next_fd = os.open(part, flags, dir_fd=current_fd)
            except FileNotFoundError:
                if not create:
                    raise UpgradeError(f"missing destination parent for {relative!r}")
                next_fd = None
                record_parent_fd = None
                created = False
                try:
                    os.mkdir(part, mode=0o755, dir_fd=current_fd)
                    created = True
                    next_fd = os.open(part, flags, dir_fd=current_fd)
                    record_parent_fd = os.dup(current_fd)
                    created_here.append(
                        {
                            "parent_fd": record_parent_fd,
                            "name": part,
                            "identity": descriptor_identity(
                                os.fstat(next_fd)
                            ),
                        }
                    )
                except BaseException:
                    close_descriptor(record_parent_fd)
                    close_descriptor(next_fd)
                    if created:
                        try:
                            os.rmdir(part, dir_fd=current_fd)
                        except OSError:
                            pass
                    raise
                close_descriptor(current_fd)
                current_fd = next_fd
                if created_directories is not None:
                    created_directories.add("/".join(traversed))
                continue
            except OSError as error:
                raise UpgradeError(f"unsafe destination parent for {relative!r}: {error}") from error
            close_descriptor(current_fd)
            current_fd = next_fd
        for directory in created_here:
            close_descriptor(directory["parent_fd"])
        return current_fd, parts[-1]
    except BaseException:
        close_descriptor(current_fd)
        for directory in reversed(created_here):
            try:
                if path_has_identity(
                    directory["parent_fd"],
                    directory["name"],
                    directory["identity"],
                    "directory",
                ):
                    os.rmdir(
                        directory["name"],
                        dir_fd=directory["parent_fd"],
                    )
            except OSError:
                pass
            close_descriptor(directory["parent_fd"])
        raise


def stage_destination(
    root_fd,
    relative,
    data,
    mode,
    delete=False,
    created_directories=None,
    stage_registry=None,
):
    parent_fd, name = open_parent_dir(
        root_fd,
        relative,
        create=not delete,
        created_directories=created_directories,
    )
    temporary = None
    descriptor = None
    temporary_identity = None
    try:
        temporary = f".forge-upgrade-{os.getpid()}-{secrets.token_hex(8)}"
        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        flags |= getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(temporary, flags, 0o600, dir_fd=parent_fd)
        temporary_identity = descriptor_identity(os.fstat(descriptor))
        if not delete:
            view = memoryview(data)
            while view:
                written = os.write(descriptor, view)
                if written <= 0:
                    raise UpgradeError(f"short write for {relative!r}")
                view = view[written:]
        os.fsync(descriptor)
        os.fchmod(
            descriptor,
            0o600 if delete else (0o755 if mode == "100755" else 0o644),
        )
        os.close(descriptor)
        descriptor = None
        parent_path = relative.rsplit("/", 1)[0] if "/" in relative else ""
        stage = {
            "relative": relative,
            "parent_fd": parent_fd,
            "name": name,
            "temporary": temporary,
            "temporary_path": (
                f"{parent_path}/{temporary}" if parent_path else temporary
            ),
            "delete": delete,
            "committed": False,
        }
        if stage_registry is not None:
            stage_registry.append(stage)
        return stage
    except BaseException:
        if temporary is not None:
            try:
                if temporary_identity is None and descriptor is not None:
                    temporary_identity = descriptor_identity(
                        os.fstat(descriptor)
                    )
                if (
                    temporary_identity is None
                    or path_has_identity(
                        parent_fd,
                        temporary,
                        temporary_identity,
                        "file",
                    )
                ):
                    os.unlink(temporary, dir_fd=parent_fd)
            except OSError:
                pass
        close_descriptor(descriptor)
        close_descriptor(parent_fd)
        raise


def verify_stage_parent(root_fd, stage):
    reopened_fd, _name = open_parent_dir(
        root_fd, stage["relative"], create=False
    )
    try:
        held = os.fstat(stage["parent_fd"])
        reopened = os.fstat(reopened_fd)
        if held.st_dev != reopened.st_dev or held.st_ino != reopened.st_ino:
            raise UpgradeError(
                f"destination parent changed during apply: {stage['relative']}"
            )
    finally:
        os.close(reopened_fd)


def commit_staged(root_fd, stage):
    verify_stage_parent(root_fd, stage)
    if stage["delete"]:
        os.unlink(stage["temporary"], dir_fd=stage["parent_fd"])
        stage["temporary"] = None
        os.unlink(stage["name"], dir_fd=stage["parent_fd"])
    else:
        os.replace(
            stage["temporary"],
            stage["name"],
            src_dir_fd=stage["parent_fd"],
            dst_dir_fd=stage["parent_fd"],
        )
        stage["temporary"] = None
    stage["committed"] = True


def cleanup_stage(stage):
    temporary = stage.get("temporary")
    if temporary is not None:
        try:
            os.unlink(temporary, dir_fd=stage["parent_fd"])
        except OSError:
            pass
    try:
        os.close(stage["parent_fd"])
    except OSError:
        pass


def cleanup_created_directories(root_fd, created_directories):
    for relative in sorted(
        created_directories,
        key=lambda path: (path.count("/"), path),
        reverse=True,
    ):
        try:
            parent_fd, name = open_parent_dir(root_fd, relative, create=False)
        except (OSError, UpgradeError):
            continue
        try:
            os.rmdir(name, dir_fd=parent_fd)
        except OSError:
            pass
        finally:
            os.close(parent_fd)


def ensure_clean_with_stage_files(root, temporary_paths, managed_paths=()):
    status = run_git(
        root,
        "status",
        "--porcelain=v1",
        "-z",
        "--untracked-files=all",
        "--ignore-submodules=none",
    ).stdout
    allowed = {
        path.encode("utf-8")
        for path in set(temporary_paths) | set(managed_paths)
    }
    for record in status.split(b"\0"):
        if not record:
            continue
        if len(record) >= 3 and record[3:] in allowed:
            continue
        raise UpgradeError("project worktree changed during apply")


def ensure_ignore_state_unchanged(result):
    project = result["project"]
    tracked = git_tracked_paths(project, "project")
    stamp_ignored = (
        STAMP_NAME not in tracked
        and path_is_ignored(project, STAMP_NAME)
    )
    if stamp_ignored != result["stamp_ignored"]:
        raise UpgradeError(f"{STAMP_NAME} ignore eligibility changed during apply")
    for item in result["items"]:
        ignored = (
            item["path"] not in tracked
            and path_is_ignored(project, item["path"])
        )
        if ignored != item["ignored"]:
            raise UpgradeError(
                f"ignore eligibility changed during apply: {item['path']}"
            )


def project_stamp_bytes(result, approved):
    ledger = dict(result["baseline"]["ledger"])
    current = result["kit"]["stamp"]["current"]
    for item in result["items"]:
        path = item["path"]
        if item["classification"] == "identical" and not item["ignored"]:
            if item["kit"]["kind"] == "file":
                ledger[path] = current
            else:
                ledger.pop(path, None)
        if path not in approved:
            continue
        if item["kit"]["kind"] == "absent":
            ledger.pop(path, None)
        else:
            ledger[path] = current
    stamp = {
        "format": FORMAT,
        "kit_version": result["kit"]["version"],
        "current": current,
        "catalog": result["kit"]["stamp"]["catalog"],
        "files": ledger,
    }
    return json.dumps(stamp, indent=2, sort_keys=True).encode("utf-8") + b"\n"


def close_descriptor(descriptor):
    if descriptor is None:
        return
    try:
        os.close(descriptor)
    except OSError:
        pass


def descriptor_identity(details):
    return details.st_dev, details.st_ino


def path_has_identity(parent_fd, name, identity, kind):
    try:
        details = os.stat(
            name,
            dir_fd=parent_fd,
            follow_symlinks=False,
        )
    except OSError:
        return False
    if descriptor_identity(details) != identity:
        return False
    return stat.S_ISREG(details.st_mode) if kind == "file" else stat.S_ISDIR(details.st_mode)


def release_apply_locks(state):
    for lock in reversed(state["locks"]):
        try:
            if path_has_identity(
                lock["parent_fd"],
                lock["name"],
                lock["identity"],
                "file",
            ):
                os.unlink(lock["name"], dir_fd=lock["parent_fd"])
        except OSError:
            pass
        close_descriptor(lock["descriptor"])
        close_descriptor(lock["parent_fd"])
    state["locks"].clear()
    for directory in reversed(state["created_directories"]):
        try:
            if path_has_identity(
                directory["parent_fd"],
                directory["name"],
                directory["identity"],
                "directory",
            ):
                os.rmdir(
                    directory["name"],
                    dir_fd=directory["parent_fd"],
                )
        except OSError:
            pass
        close_descriptor(directory["parent_fd"])
    state["created_directories"].clear()


def open_git_ref_lock_parent(common_fd, reference, created_directories):
    parts = reference.split("/")
    if (
        len(parts) < 3
        or parts[:2] != ["refs", "heads"]
        or any(
            part in {"", ".", ".."}
            or "/" in part
            or "\x00" in part
            for part in parts
        )
    ):
        raise UpgradeError("project HEAD has an unsafe symbolic reference")
    directory_flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
    directory_flags |= getattr(os, "O_NOFOLLOW", 0)
    current_fd = os.dup(common_fd)
    try:
        for part in parts[:-1]:
            created = False
            try:
                next_fd = os.open(
                    part,
                    directory_flags,
                    dir_fd=current_fd,
                )
            except FileNotFoundError:
                try:
                    os.mkdir(part, 0o755, dir_fd=current_fd)
                    created = True
                except FileExistsError:
                    pass
                if created:
                    next_fd = None
                    record_parent_fd = None
                    try:
                        next_fd = os.open(
                            part,
                            directory_flags,
                            dir_fd=current_fd,
                        )
                        record_parent_fd = os.dup(current_fd)
                        created_directories.append(
                            {
                                "parent_fd": record_parent_fd,
                                "name": part,
                                "identity": descriptor_identity(
                                    os.fstat(next_fd)
                                ),
                            }
                        )
                    except BaseException:
                        if record_parent_fd is not None:
                            close_descriptor(record_parent_fd)
                        if next_fd is not None:
                            close_descriptor(next_fd)
                        try:
                            os.rmdir(part, dir_fd=current_fd)
                        except OSError:
                            pass
                        raise
                else:
                    next_fd = os.open(
                        part,
                        directory_flags,
                        dir_fd=current_fd,
                    )
            close_descriptor(current_fd)
            current_fd = next_fd
        return current_fd, f"{parts[-1]}.lock"
    except BaseException as error:
        close_descriptor(current_fd)
        if isinstance(error, OSError):
            raise UpgradeError(
                "cannot safely traverse the current project branch reference"
            ) from error
        raise


def require_apply_lock_support():
    required_flags = ("O_DIRECTORY", "O_NOFOLLOW")
    required_dir_fd = (os.open, os.mkdir, os.unlink, os.rmdir, os.stat)
    if (
        any(not hasattr(os, name) for name in required_flags)
        or any(function not in os.supports_dir_fd for function in required_dir_fd)
        or os.stat not in os.supports_follow_symlinks
    ):
        raise UpgradeError(
            "platform lacks safe descriptor-relative Git apply locking"
        )


def require_files_ref_storage(project):
    result = run_git(
        project,
        "config",
        "--get",
        "extensions.refStorage",
        text=True,
        check=False,
    )
    if result.returncode == 1:
        return
    if result.returncode != 0 or result.stdout.strip().lower() != "files":
        raise UpgradeError(
            "project Git reference storage is unsupported for safe apply locking"
        )


def acquire_named_apply_lock(state, parent_fd, name, message):
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW
    try:
        descriptor = os.open(name, flags, 0o600, dir_fd=parent_fd)
    except OSError as error:
        close_descriptor(parent_fd)
        if isinstance(error, FileExistsError):
            raise UpgradeError(message) from error
        raise UpgradeError(f"cannot acquire project apply lock: {error}") from error

    transferred = False
    try:
        record = {
            "parent_fd": parent_fd,
            "name": name,
            "descriptor": descriptor,
            "identity": descriptor_identity(os.fstat(descriptor)),
        }
        state["locks"].append(record)
        transferred = True
        data = f"{os.getpid()}\n".encode("ascii")
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written <= 0:
                raise OSError("short write while acquiring project apply lock")
            view = view[written:]
        os.fsync(descriptor)
    except BaseException as error:
        if not transferred:
            try:
                opened = descriptor_identity(os.fstat(descriptor))
                if path_has_identity(parent_fd, name, opened, "file"):
                    os.unlink(name, dir_fd=parent_fd)
            except OSError:
                pass
            close_descriptor(descriptor)
            close_descriptor(parent_fd)
        if isinstance(error, OSError):
            raise UpgradeError(
                f"cannot acquire project apply lock: {error}"
            ) from error
        raise


def acquire_lock(project, lock_registry=None):
    require_apply_lock_support()
    require_files_ref_storage(project)
    git_dir = Path(
        run_git(project, "rev-parse", "--absolute-git-dir", text=True).stdout.strip()
    ).resolve()
    common_text = run_git(
        project, "rev-parse", "--git-common-dir", text=True
    ).stdout.strip()
    common_dir = Path(common_text)
    if not common_dir.is_absolute():
        common_dir = (project / common_dir).resolve()
    else:
        common_dir = common_dir.resolve()
    directory_flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW
    git_dir_fd = None
    common_fd = None
    try:
        git_dir_fd = os.open(git_dir, directory_flags)
        common_fd = os.open(common_dir, directory_flags)
    except OSError as error:
        for descriptor in (common_fd, git_dir_fd):
            if descriptor is not None:
                try:
                    os.close(descriptor)
                except OSError:
                    pass
        raise UpgradeError(f"cannot open project Git metadata safely: {error}") from error

    state = {"locks": [], "created_directories": []}
    try:
        acquire_named_apply_lock(
            state,
            os.dup(git_dir_fd),
            "forge-upgrade.lock",
            "another forge-upgrade apply appears to be running",
        )
        acquire_named_apply_lock(
            state,
            os.dup(git_dir_fd),
            "index.lock",
            "project Git index is locked by another operation",
        )
        acquire_named_apply_lock(
            state,
            os.dup(git_dir_fd),
            "HEAD.lock",
            "project HEAD is locked by another operation",
        )
        reference = git_head_ref(project)
        if reference is not None:
            parent_fd, name = open_git_ref_lock_parent(
                common_fd,
                reference,
                state["created_directories"],
            )
            acquire_named_apply_lock(
                state,
                parent_fd,
                name,
                "project branch is locked by another operation",
            )
        close_descriptor(git_dir_fd)
        git_dir_fd = None
        close_descriptor(common_fd)
        common_fd = None
        if lock_registry is not None:
            lock_registry.append(state)
        return state
    except BaseException:
        release_apply_locks(state)
        raise
    finally:
        close_descriptor(git_dir_fd)
        close_descriptor(common_fd)


def run_doctor(project):
    doctor = filesystem_state(project / "scripts" / "forge-doctor.sh")
    if doctor["kind"] != "file":
        print("forge-doctor exit: 127")
        print("forge-upgrade: scripts/forge-doctor.sh is not a regular file")
        return 127
    environment = git_environment()
    bash = shutil.which("bash", path=environment["PATH"])
    if not bash:
        print("forge-doctor exit: 127")
        print("forge-upgrade: bash is unavailable")
        return 127
    # The RFC permits one kit fetch and no other upgrade network. A leading gh shim makes the
    # doctor's ruleset probe report UNVERIFIED without contacting GitHub; all local checks still run.
    with tempfile.TemporaryDirectory(
        prefix="forge-upgrade-doctor-", dir="/tmp"
    ) as shim_dir:
        gh_shim = Path(shim_dir) / "gh"
        gh_shim.write_text("#!/bin/sh\nexit 127\n", encoding="utf-8")
        gh_shim.chmod(0o700)
        environment["PATH"] = shim_dir + os.pathsep + environment.get("PATH", "")
        result = subprocess.run(
            [bash, "scripts/forge-doctor.sh"],
            cwd=project,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            env=environment,
        )
    if result.stdout:
        print(result.stdout, end="" if result.stdout.endswith("\n") else "\n")
    if result.stderr:
        print(result.stderr, end="" if result.stderr.endswith("\n") else "\n", file=sys.stderr)
    print(f"forge-doctor exit: {result.returncode}")
    return result.returncode


def apply_plan(args):
    result = plan(args.project, args.kit, args.from_version)
    if result["baseline"]["kind"] == "legacy" and args.from_version is None:
        raise UpgradeError(
            "unstamped legacy apply requires a human-confirmed --from-version; "
            "rerun plan with that version and approve its new plan id"
        )
    if result["stamp_ignored"]:
        raise UpgradeError(
            f"{STAMP_NAME} is ignored and untracked; commit an ignore-rule "
            "correction before apply so provenance remains reviewable"
        )
    if args.plan_id != result["id"]:
        raise UpgradeError(
            "plan id is stale or does not match this project/kit; run plan again"
        )
    approved = args.approve or []
    if not approved and not args.approve_metadata:
        raise UpgradeError(
            "no paths approved; use --approve-metadata only for an explicitly "
            "reviewed provenance-only update"
        )
    if len(set(approved)) != len(approved):
        raise UpgradeError("an approved path was repeated")
    eligible = {
        item["path"]: item
        for item in result["items"]
        if item["action"] in {"auto-add", "auto-delete", "auto-update"}
    }
    for path in approved:
        validate_relpath(path)
        if path not in eligible:
            raise UpgradeError(f"approved path is not eligible in this plan: {path}")

    lock_states = []
    root_fd = None
    stages = []
    created_directories = set()
    applied = []
    stamp_applied = False
    try:
        acquire_lock(result["project"], lock_registry=lock_states)
        hold_marker = os.environ.get("FORGE_UPGRADE_TEST_HOLD_LOCKS")
        if hold_marker:
            # Test-only synchronization point: the upgrade e2e parks the apply here,
            # with every project lock held, so it can probe concurrent Git mutations
            # against a provably held window instead of racing lock release on slow
            # runners. Bounded, read-only, and inert unless the variable is set.
            hold_deadline = time.monotonic() + 30
            while os.path.exists(hold_marker) and time.monotonic() < hold_deadline:
                time.sleep(0.01)
        root_flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
        root_flags |= getattr(os, "O_NOFOLLOW", 0)
        root_fd = os.open(result["project"], root_flags)
        opened_root = os.fstat(root_fd)
        if (
            opened_root.st_dev != result["payload"]["project"]["device"]
            or opened_root.st_ino != result["payload"]["project"]["inode"]
        ):
            raise UpgradeError("project worktree root changed after approval")
        ensure_clean(result["project"], "project")
        if git_identity(result["project"]) != result["payload"]["project"]:
            raise UpgradeError("project HEAD or index changed after approval")
        ensure_clean(result["kit"]["root"], "kit")
        if git_identity(result["kit"]["root"]) != result["kit"]["identity"]:
            raise UpgradeError("kit HEAD or index changed after approval")

        stamp_before = filesystem_state(result["project"] / STAMP_NAME)
        operations = []
        for path in approved:
            if git_identity(result["project"]) != result["payload"]["project"]:
                raise UpgradeError("project HEAD or index changed during apply")
            if git_identity(result["kit"]["root"]) != result["kit"]["identity"]:
                raise UpgradeError("kit HEAD or index changed during apply")
            item = eligible[path]
            ensure_regular_ancestors(result["project"], path, allow_missing=True)
            now = filesystem_state(result["project"] / path)
            if not states_equal(now, item["project"]):
                raise UpgradeError(f"destination changed after approval: {path}")
            if item["kit"]["kind"] == "absent":
                operations.append(
                    {"path": path, "item": item, "data": b"", "delete": True}
                )
            else:
                source_now = filesystem_state(
                    result["kit"]["root"] / "template" / path,
                    include_data=True,
                )
                if not states_equal(source_now, item["kit"]):
                    raise UpgradeError(f"kit source changed after approval: {path}")
                operations.append(
                    {
                        "path": path,
                        "item": item,
                        "data": source_now["data"],
                        "delete": False,
                    }
                )

        # Fully materialize every approved replacement and a deletion permission probe
        # before changing a managed destination. The provenance stamp is staged last.
        for operation in operations:
            operation["stage"] = stage_destination(
                root_fd,
                operation["path"],
                operation["data"],
                operation["item"]["kit"].get("mode", "100644"),
                delete=operation["delete"],
                created_directories=created_directories,
                stage_registry=stages,
            )
        stamp_stage = stage_destination(
            root_fd,
            STAMP_NAME,
            project_stamp_bytes(result, set(approved)),
            "100644",
            created_directories=created_directories,
            stage_registry=stages,
        )

        ensure_clean_with_stage_files(
            result["project"],
            {stage["temporary_path"] for stage in stages},
        )
        if git_identity(result["project"]) != result["payload"]["project"]:
            raise UpgradeError("project HEAD or index changed during apply")
        ensure_clean(result["kit"]["root"], "kit")
        if git_identity(result["kit"]["root"]) != result["kit"]["identity"]:
            raise UpgradeError("kit HEAD or index changed during apply")
        ensure_ignore_state_unchanged(result)
        if not states_equal(
            filesystem_state(result["project"] / STAMP_NAME), stamp_before
        ):
            raise UpgradeError(f"{STAMP_NAME} changed during apply")

        for operation in operations:
            path = operation["path"]
            item = operation["item"]
            if not states_equal(
                filesystem_state(result["project"] / path), item["project"]
            ):
                raise UpgradeError(f"destination changed during apply: {path}")
            if not operation["delete"]:
                source_now = filesystem_state(
                    result["kit"]["root"] / "template" / path,
                    include_data=True,
                )
                if (
                    not states_equal(source_now, item["kit"])
                    or source_now["data"] != operation["data"]
                ):
                    raise UpgradeError(f"kit source changed during apply: {path}")

        for operation in operations:
            if git_identity(result["project"]) != result["payload"]["project"]:
                raise UpgradeError("project HEAD or index changed during apply")
            ensure_ignore_state_unchanged(result)
            if not states_equal(
                filesystem_state(result["project"] / operation["path"]),
                operation["item"]["project"],
            ):
                raise UpgradeError(
                    f"destination changed during apply: {operation['path']}"
                )
            commit_staged(root_fd, operation["stage"])
            applied.append(operation["path"])
            print(f"APPLIED | {operation['path']}", flush=True)
            os.fsync(operation["stage"]["parent_fd"])
        if git_identity(result["project"]) != result["payload"]["project"]:
            raise UpgradeError("project HEAD or index changed during apply")
        ensure_clean_with_stage_files(
            result["project"],
            {stage["temporary_path"] for stage in stages},
            {operation["path"] for operation in operations},
        )
        for operation in operations:
            if not states_equal(
                filesystem_state(result["project"] / operation["path"]),
                operation["item"]["kit"],
            ):
                raise UpgradeError(
                    f"applied destination changed during apply: {operation['path']}"
                )
        ensure_clean(result["kit"]["root"], "kit")
        if git_identity(result["kit"]["root"]) != result["kit"]["identity"]:
            raise UpgradeError("kit HEAD or index changed during apply")
        ensure_ignore_state_unchanged(result)
        if not states_equal(
            filesystem_state(result["project"] / STAMP_NAME), stamp_before
        ):
            raise UpgradeError(f"{STAMP_NAME} changed during apply")
        if git_identity(result["project"]) != result["payload"]["project"]:
            raise UpgradeError("project HEAD or index changed during apply")
        if git_identity(result["kit"]["root"]) != result["kit"]["identity"]:
            raise UpgradeError("kit HEAD or index changed during apply")
        commit_staged(root_fd, stamp_stage)
        stamp_applied = True
        print(f"APPLIED | {STAMP_NAME} (per-path provenance)", flush=True)
        os.fsync(stamp_stage["parent_fd"])
    except (OSError, UpgradeError) as error:
        if applied or stamp_applied:
            completed = ", ".join(
                applied + ([STAMP_NAME] if stamp_applied else [])
            )
            raise UpgradeError(
                f"apply stopped after writing {completed}; review or revert the "
                f"partial worktree before retrying: {error}"
            ) from error
        if isinstance(error, UpgradeError):
            raise
        raise UpgradeError(f"apply preflight failed before managed writes: {error}") from error
    finally:
        try:
            for stage in stages:
                cleanup_stage(stage)
            if root_fd is not None:
                cleanup_created_directories(root_fd, created_directories)
        finally:
            close_descriptor(root_fd)
            for lock_state in reversed(lock_states):
                release_apply_locks(lock_state)

    doctor_status = run_doctor(result["project"])
    if doctor_status != 0:
        manual_paths = [
            f"{escape_controls(item['path'])} ({item['action']})"
            for item in result["items"]
            if item["action"] in {"manual-immutable", "manual-policy"}
        ]
        manual_display = ", ".join(manual_paths) or "none listed by this plan"
        if doctor_status == 127:
            print(
                "forge-upgrade: WRITES SUCCEEDED; forge-doctor could not run "
                "(exit 3). Restore scripts/forge-doctor.sh (its diagnostics are "
                "above), rerun it by hand, and apply these plan paths by hand: "
                f"{manual_display}.",
                flush=True,
            )
        else:
            print(
                "forge-upgrade: WRITES SUCCEEDED; forge-doctor found pending human "
                "work (exit 3). Human must apply these plan paths by hand: "
                f"{manual_display}. Review doctor output above for any additional work.",
                flush=True,
            )
        raise SystemExit(3)


def build_parser():
    parser = argparse.ArgumentParser(
        prog="forge-upgrade.sh",
        description="Plan and apply safe Forge template upgrades.",
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    check = subparsers.add_parser("check-kit", help="validate the kit stamp")
    check.add_argument("--kit", required=True)
    check.add_argument(
        "--development-worktree",
        action="store_true",
        help=argparse.SUPPRESS,
    )

    for name in ("plan", "apply"):
        child = subparsers.add_parser(name)
        child.add_argument("--kit", required=True)
        child.add_argument("--project", default=os.getcwd())
        child.add_argument("--from-version")
        if name == "apply":
            child.add_argument("--plan-id", required=True)
            child.add_argument("--approve", action="append", default=[])
            child.add_argument(
                "--approve-metadata",
                action="store_true",
                help="explicitly approve a provenance-only stamp update",
            )
    return parser


def main():
    parser = build_parser()
    args = parser.parse_args()
    try:
        if args.command == "check-kit":
            kit = load_kit(args.kit, require_clean=not args.development_worktree)
            print(
                f"forge-upgrade: kit manifest OK "
                f"({kit['version']}, {len(kit['files'])} template files)"
            )
            return
        if args.from_version is not None:
            parse_semver(args.from_version, "--from-version")
        if args.command == "plan":
            print_plan(plan(args.project, args.kit, args.from_version))
            return
        apply_plan(args)
    except UpgradeError as error:
        fail(str(error))


if __name__ == "__main__":
    main()
PY
