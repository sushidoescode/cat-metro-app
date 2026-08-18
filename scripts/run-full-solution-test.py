#!/usr/bin/env python3
"""Run Cat Metro's ordinary full-solution test, with optional run-local reuse."""

from __future__ import annotations

import fcntl
import hashlib
import json
import locale
import os
from pathlib import Path, PurePosixPath
import platform
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from typing import Iterable


CACHE_ENV = "CAT_METRO_FULL_SOLUTION_CACHE_DIR"
ARTIFACT_ENV = "CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR"
LOGICAL_ARGS = ("test", "dotnet/CatMetro.sln", "-c", "Release", "--nologo")
SCHEMA = 1

# The Git manifest covers every tracked and ordinary untracked input. These trees are walked as
# well so ignored files that are nevertheless reachable through SDK globs or runtime enumeration
# cannot hide behind .gitignore. Build outputs are deliberately outside the input set.
EXPLICIT_TREES = (
    "dotnet",
    "unity/Assets/Scripts/Domain",
    "unity/Assets/Scripts/Content",
    "unity/Assets/Scripts/Services",
    "unity/Assets/Scripts/Application",
    "unity/Assets/Tests/EditMode/Pure",
    "content",
    "config",
    "docs/plan/data",
    "tests/fixtures",
    "tests/validation/fixtures",
    "tests/contract",
    "tests/taxonomy",
)
WALK_EXCLUDES = frozenset(("bin", "obj", ".git"))
OUTSIDE_BUILD_FILES = (
    "global.json",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "Directory.Packages.targets",
    ".globalconfig",
)


class Uncacheable(RuntimeError):
    """The real command remains safe, but this snapshot must not be reused."""


@dataclass(frozen=True)
class Snapshot:
    digest: str
    tool_path: str


def _frame(hasher: "hashlib._Hash", value: bytes) -> None:
    hasher.update(len(value).to_bytes(8, "big"))
    hasher.update(value)


def _secret_like(relative: str) -> bool:
    return any(part == ".env" or part.startswith(".env.") for part in PurePosixPath(relative).parts)


def _run_git(root: Path, args: Iterable[str]) -> bytes:
    completed = subprocess.run(
        ("git", "-C", os.fspath(root), *args),
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if completed.returncode != 0:
        raise Uncacheable("Git input enumeration failed")
    return completed.stdout


def _git_paths(root: Path) -> tuple[str, ...]:
    raw = _run_git(root, ("ls-files", "-z", "--cached", "--others", "--exclude-standard"))
    if raw and not raw.endswith(b"\0"):
        raise Uncacheable("Git returned a truncated NUL manifest")
    paths: set[str] = set()
    for encoded in raw.split(b"\0"):
        if not encoded:
            continue
        relative = os.fsdecode(encoded)
        pure = PurePosixPath(relative)
        if pure.is_absolute() or ".." in pure.parts or relative in ("", "."):
            raise Uncacheable("Git returned a path outside the worktree")
        paths.add(relative)
    return tuple(sorted(paths, key=os.fsencode))


def _explicit_paths(root: Path) -> tuple[tuple[str, ...], tuple[str, ...]]:
    files: set[str] = set()
    markers: list[str] = []

    for relative_root in EXPLICIT_TREES:
        start = root / relative_root
        try:
            start_stat = os.lstat(start)
        except FileNotFoundError:
            markers.append("missing:" + relative_root)
            continue
        if not stat.S_ISDIR(start_stat.st_mode) or stat.S_ISLNK(start_stat.st_mode):
            raise Uncacheable("an explicit input root is not a real directory")

        stack = [start]
        while stack:
            directory = stack.pop()
            try:
                entries = sorted(os.scandir(directory), key=lambda entry: os.fsencode(entry.name))
            except OSError as error:
                raise Uncacheable("an explicit input tree is unreadable") from error
            for entry in entries:
                relative = entry.path[len(os.fspath(root)) + 1 :].replace(os.sep, "/")
                if _secret_like(relative):
                    raise Uncacheable("a secret-like path is reachable from an input tree")
                try:
                    entry_stat = entry.stat(follow_symlinks=False)
                except OSError as error:
                    raise Uncacheable("an explicit input entry is unreadable") from error
                if stat.S_ISLNK(entry_stat.st_mode):
                    raise Uncacheable("a symlink is reachable from an input tree")
                if stat.S_ISDIR(entry_stat.st_mode):
                    if entry.name not in WALK_EXCLUDES:
                        stack.append(Path(entry.path))
                elif stat.S_ISREG(entry_stat.st_mode):
                    files.add(relative)
                else:
                    raise Uncacheable("a non-regular input is reachable from an input tree")

    return tuple(sorted(files, key=os.fsencode)), tuple(sorted(markers))


def _guard_outside_build_files(root: Path) -> None:
    # MSBuild searches parent directories. An out-of-repo build policy is not owned by this task,
    # so detect it without reading it and fall back to the ordinary command.
    for parent in root.parents:
        for name in OUTSIDE_BUILD_FILES:
            candidate = parent / name
            try:
                candidate_stat = os.lstat(candidate)
            except FileNotFoundError:
                continue
            except OSError as error:
                raise Uncacheable("could not inspect an ancestor build policy") from error
            if not stat.S_ISREG(candidate_stat.st_mode):
                raise Uncacheable("an ancestor build policy is not a regular file")
            raise Uncacheable("an out-of-repo build policy is active")


def _hash_file(root: Path, relative: str, aggregate: "hashlib._Hash") -> None:
    if _secret_like(relative):
        raise Uncacheable("refusing to read a secret-like input")
    path = root / relative
    try:
        before = os.lstat(path)
    except FileNotFoundError:
        _frame(aggregate, b"missing")
        return
    except OSError as error:
        raise Uncacheable("an input cannot be inspected") from error
    if not stat.S_ISREG(before.st_mode) or stat.S_ISLNK(before.st_mode):
        raise Uncacheable("a Git input is not a regular file")

    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(path, flags)
    except OSError as error:
        raise Uncacheable("an input cannot be opened safely") from error

    content = hashlib.sha256()
    try:
        opened = os.fstat(descriptor)
        if not stat.S_ISREG(opened.st_mode):
            raise Uncacheable("an opened input is not regular")
        while True:
            block = os.read(descriptor, 1024 * 1024)
            if not block:
                break
            content.update(block)
        after_read = os.fstat(descriptor)
    finally:
        os.close(descriptor)

    try:
        after = os.lstat(path)
    except OSError as error:
        raise Uncacheable("an input changed while being read") from error
    stable_fields = ("st_dev", "st_ino", "st_mode", "st_size", "st_mtime_ns")
    if any(getattr(before, field) != getattr(opened, field) for field in stable_fields):
        raise Uncacheable("an input changed before it was read")
    if any(getattr(opened, field) != getattr(after_read, field) for field in stable_fields):
        raise Uncacheable("an input changed while it was read")
    if any(getattr(after_read, field) != getattr(after, field) for field in stable_fields):
        raise Uncacheable("an input changed after it was read")

    _frame(aggregate, b"regular")
    _frame(aggregate, str(stat.S_IMODE(before.st_mode)).encode("ascii"))
    _frame(aggregate, content.digest())


def _input_digest(root: Path) -> str:
    _guard_outside_build_files(root)
    git_before = _git_paths(root)
    explicit_before, markers_before = _explicit_paths(root)
    paths = tuple(sorted(set(git_before).union(explicit_before), key=os.fsencode))

    aggregate = hashlib.sha256()
    _frame(aggregate, b"cat-metro-input-v1")
    for marker in markers_before:
        _frame(aggregate, os.fsencode(marker))
    for relative in paths:
        _frame(aggregate, os.fsencode(relative))
        _hash_file(root, relative, aggregate)

    git_after = _git_paths(root)
    explicit_after, markers_after = _explicit_paths(root)
    if git_after != git_before or explicit_after != explicit_before or markers_after != markers_before:
        raise Uncacheable("input membership changed during fingerprinting")
    return aggregate.hexdigest()


def _environment_digest(environment: dict[str, str]) -> str:
    aggregate = hashlib.sha256()
    _frame(aggregate, b"cat-metro-environment-v1")
    for name in sorted(environment, key=os.fsencode):
        _frame(aggregate, os.fsencode(name))
        _frame(aggregate, os.fsencode(environment[name]))
    return aggregate.hexdigest()


def _tool_identity(environment: dict[str, str], root: Path) -> tuple[str, str]:
    located = shutil.which("dotnet", path=environment.get("PATH"))
    if not located:
        raise Uncacheable("dotnet is not on PATH")
    resolved = os.path.realpath(located)
    binary_digest = hashlib.sha256()
    _frame(binary_digest, os.fsencode(resolved))
    _hash_file(Path(resolved).parent, Path(resolved).name, binary_digest)

    completed = subprocess.run(
        (resolved, "--info"),
        cwd=root,
        env=environment,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        raise Uncacheable("dotnet --info failed")
    info = completed.stdout + b"\0stderr\0" + completed.stderr
    text = info.decode("utf-8", "replace")
    version = re.search(r"(?im)^\s*Version:\s*([0-9]+)\.", text)
    if version is None:
        version = re.search(r"fake-([0-9]+)\.", text)
    if version is None or int(version.group(1)) < 8:
        raise Uncacheable("the SDK does not support --artifacts-path")
    _frame(binary_digest, info)
    return resolved, binary_digest.hexdigest()


def _snapshot(root: Path, environment: dict[str, str]) -> Snapshot:
    tool_path, tool_digest = _tool_identity(environment, root)
    source_digest = _input_digest(root)
    system = {
        "cwd": os.fsencode(os.fspath(root)).hex(),
        "locale": locale.setlocale(locale.LC_ALL, None),
        "logical_args": LOGICAL_ARGS,
        "machine": platform.machine(),
        "os": platform.platform(),
        "timezone": (time.tzname, time.timezone, time.daylight),
    }
    material = {
        "environment": _environment_digest(environment),
        "source": source_digest,
        "system": system,
        "tool": tool_digest,
    }
    encoded = json.dumps(material, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return Snapshot(hashlib.sha256(encoded).hexdigest(), tool_path)


def _private_directory(raw: str) -> Path:
    path = Path(raw)
    if not path.is_absolute() or os.path.abspath(path) != os.path.realpath(path):
        raise Uncacheable("the cache directory is not an absolute real path")
    try:
        details = os.lstat(path)
    except OSError as error:
        raise Uncacheable("the cache directory does not exist") from error
    if not stat.S_ISDIR(details.st_mode) or stat.S_ISLNK(details.st_mode):
        raise Uncacheable("the cache path is not a real directory")
    if hasattr(os, "getuid") and details.st_uid != os.getuid():
        raise Uncacheable("the cache directory has a different owner")
    if stat.S_IMODE(details.st_mode) & 0o077:
        raise Uncacheable("the cache directory is not private")
    return path


def _private_child(parent: Path, name: str) -> Path:
    path = parent / name
    try:
        os.mkdir(path, 0o700)
    except FileExistsError:
        pass
    except OSError as error:
        raise Uncacheable("a private cache subdirectory cannot be created") from error
    return _private_directory(os.fspath(path))


def _ensure_real_directory_chain(root: Path, target: Path) -> None:
    try:
        relative = target.relative_to(root)
    except ValueError as error:
        raise Uncacheable("the artifacts path escaped the repository") from error
    current = root
    for part in relative.parts:
        current /= part
        try:
            details = os.lstat(current)
        except FileNotFoundError:
            try:
                os.mkdir(current, 0o700)
            except OSError as error:
                raise Uncacheable("the artifacts directory cannot be created") from error
            details = os.lstat(current)
        except OSError as error:
            raise Uncacheable("the artifacts directory cannot be inspected") from error
        if not stat.S_ISDIR(details.st_mode) or stat.S_ISLNK(details.st_mode):
            raise Uncacheable("an artifacts-path component is not a real directory")


def _artifact_path(root: Path, cache: Path, control: str | None, digest: str) -> Path:
    base = root / "dotnet/CatMetro.Tests/obj/ci-full-solution"
    if control:
        session = Path(control)
        if not session.is_absolute():
            raise Uncacheable("the artifacts session is not absolute")
    else:
        cache_identity = hashlib.sha256(os.fsencode(os.fspath(cache))).hexdigest()[:20]
        session = base / ("adhoc." + cache_identity)
    try:
        relative_session = session.relative_to(base)
    except ValueError as error:
        raise Uncacheable("the artifacts session escaped its ignored root") from error
    if not relative_session.parts:
        raise Uncacheable("the artifacts session is too broad")

    target = session / digest
    try:
        relative_probe = (target / ".ignore-probe").relative_to(root)
    except ValueError as error:
        raise Uncacheable("the artifacts path escaped the worktree") from error
    ignored = subprocess.run(
        ("git", "-C", os.fspath(root), "check-ignore", "-q", "--", os.fspath(relative_probe)),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if ignored.returncode != 0:
        raise Uncacheable("the artifacts path is not ignored")
    _ensure_real_directory_chain(root, target)
    return target


def _command_digest(arguments: tuple[str, ...]) -> str:
    aggregate = hashlib.sha256()
    _frame(aggregate, b"cat-metro-command-v1")
    for argument in arguments:
        _frame(aggregate, os.fsencode(argument))
    return aggregate.hexdigest()


def _record_core(key: str, input_digest: str, command_digest: str) -> dict[str, object]:
    return {
        "command_digest": command_digest,
        "input_digest": input_digest,
        "key": key,
        "schema": SCHEMA,
        "success": True,
    }


def _record_checksum(core: dict[str, object]) -> str:
    encoded = json.dumps(core, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _record_is_valid(path: Path, expected: dict[str, object]) -> bool:
    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(path, flags)
    except OSError:
        return False
    try:
        details = os.fstat(descriptor)
        if not stat.S_ISREG(details.st_mode) or details.st_size > 16 * 1024:
            return False
        raw = b""
        while len(raw) <= 16 * 1024:
            block = os.read(descriptor, 4096)
            if not block:
                break
            raw += block
    finally:
        os.close(descriptor)
    try:
        record = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError):
        return False
    checksum = record.pop("checksum", None) if isinstance(record, dict) else None
    return record == expected and checksum == _record_checksum(expected)


def _publish_record(path: Path, core: dict[str, object]) -> None:
    record = dict(core)
    record["checksum"] = _record_checksum(core)
    payload = (json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    descriptor, temporary = tempfile.mkstemp(prefix=".record.", dir=path.parent)
    try:
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        descriptor = -1
        os.replace(temporary, path)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        try:
            os.unlink(temporary)
        except FileNotFoundError:
            pass


def _open_lock(path: Path) -> int:
    flags = os.O_RDWR | os.O_CREAT
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    details = os.fstat(descriptor)
    if not stat.S_ISREG(details.st_mode) or stat.S_IMODE(details.st_mode) & 0o077:
        os.close(descriptor)
        raise Uncacheable("the producer lock is not private and regular")
    fcntl.flock(descriptor, fcntl.LOCK_EX)
    return descriptor


def _run(arguments: tuple[str, ...], environment: dict[str, str], root: Path) -> int:
    try:
        return subprocess.run(arguments, cwd=root, env=environment, check=False).returncode
    except FileNotFoundError:
        print("full-solution test: dotnet not found", file=sys.stderr)
        return 127
    except KeyboardInterrupt:
        return 130


def _direct(environment: dict[str, str], root: Path) -> int:
    return _run(("dotnet", *LOGICAL_ARGS), environment, root)


def _cached(
    root: Path,
    environment: dict[str, str],
    cache_control: str,
    artifact_control: str | None,
) -> int:
    try:
        cache = _private_directory(cache_control)
        records = _private_child(cache, "records")
        locks = _private_child(cache, "locks")
        first = _snapshot(root, environment)
        artifacts = _artifact_path(root, cache, artifact_control, first.digest)
        actual = (first.tool_path, *LOGICAL_ARGS, "--artifacts-path", os.fspath(artifacts))
        command_digest = _command_digest(actual)
        key_material = (str(SCHEMA) + "\0" + first.digest + "\0" + command_digest).encode("ascii")
        key = hashlib.sha256(key_material).hexdigest()
        core = _record_core(key, first.digest, command_digest)
        record = records / (key + ".json")
        lock_descriptor = _open_lock(locks / (key + ".lock"))
    except (OSError, Uncacheable, ValueError):
        return _direct(environment, root)

    try:
        try:
            before = _snapshot(root, environment)
        except (OSError, Uncacheable, ValueError):
            return _direct(environment, root)
        if before != first:
            return _direct(environment, root)
        if _record_is_valid(record, core):
            return 0

        result = _run(actual, environment, root)
        if result != 0:
            return result
        try:
            after = _snapshot(root, environment)
        except (OSError, Uncacheable, ValueError):
            return 0
        if after == before:
            try:
                _publish_record(record, core)
            except OSError:
                pass
        return 0
    finally:
        try:
            fcntl.flock(lock_descriptor, fcntl.LOCK_UN)
        finally:
            os.close(lock_descriptor)


def main() -> int:
    if len(sys.argv) != 1:
        print("usage: run-full-solution-test.py", file=sys.stderr)
        return 2
    try:
        root_output = subprocess.check_output(
            ("git", "rev-parse", "--show-toplevel"), stderr=subprocess.DEVNULL
        )
        root = Path(os.fsdecode(root_output.rstrip(b"\n"))).resolve(strict=True)
    except (OSError, subprocess.CalledProcessError):
        print("full-solution test: not in a Git worktree", file=sys.stderr)
        return 2

    cache_control = os.environ.get(CACHE_ENV)
    artifact_control = os.environ.get(ARTIFACT_ENV)
    environment = dict(os.environ)
    environment.pop(CACHE_ENV, None)
    environment.pop(ARTIFACT_ENV, None)
    if not cache_control:
        return _direct(environment, root)
    return _cached(root, environment, cache_control, artifact_control)


if __name__ == "__main__":
    raise SystemExit(main())
