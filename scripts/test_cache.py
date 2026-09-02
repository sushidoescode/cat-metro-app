#!/usr/bin/env python3
"""Memoise an expensive, deterministic test command.

Why this exists
---------------
`bash scripts/test.sh` runs the *identical* full-solution command

    dotnet test dotnet/CatMetro.sln -c Release --nologo [--logger ...]

nine times across seven wrappers, because seven contracts each independently
assert "the unfiltered suite is green". One run is ~529 s / 857 tests on the
reference machine, so ~4,760 s of a ~6,860 s suite is the same command over
and over. This helper runs it once per distinct (inputs, argv, slot) and
replays the recorded stdout + exit code for the rest.

Usage
-----
    python3 scripts/test_cache.py [--slot N] -- <command> [args...]

Escape hatches
--------------
    CATMETRO_NO_TEST_CACHE=1     bypass entirely; exec the real command
    CATMETRO_TEST_CACHE_DIR=DIR  relocate the store (default ~/.cache/cat-metro/test-cache)

Safety contract
---------------
Every failure to key an input raises `Uncacheable`, which runs the real
command. There is no path from "we could not fingerprint something" to a
green exit. A cache that is ever wrong is worse than no cache.

The `--slot` argument exists so wrappers that deliberately demand *two
independent processes* (tests/domain/determinism.test.sh,
tests/solver/solver.test.sh compare a hash across two runs) keep getting two
genuinely distinct recorded runs rather than one run replayed twice, which
would make their cross-process assertion trivially true.
"""

from __future__ import annotations

import base64
import hashlib
import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
from pathlib import Path

SCHEMA = 1
CACHE_ENTRY_LIMIT = 64 * 1024 * 1024  # refuse absurd records rather than OOM

# Build-policy filenames MSBuild/NuGet pick up implicitly by directory walk.
# In-repo copies are covered by the tree fingerprint, but they may be
# gitignored, so they are hashed by name too; out-of-repo ancestors are the
# "unkeyed MSBuild user extensions" hole and are hashed explicitly.
POLICY_NAMES = (
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Build.rsp",
    "Directory.Packages.props",
    "Directory.Packages.targets",
    "Directory.Solution.props",
    "Directory.Solution.targets",
    "MSBuild.rsp",
    "global.json",
    "NuGet.config",
    "nuget.config",
    "NuGet.Config",
    ".editorconfig",
    ".globalconfig",
)

# Environment variables that participate in an MSBuild/NuGet/dotnet run.
# Captured tri-state: a name that is absent contributes nothing, a name set to
# "" contributes its framed name plus an empty value. That distinction is the
# "empty workload overrides" edge case -- `DOTNETSDK_WORKLOAD_PACK_ROOTS=""`
# is NOT the same build as having it unset, and a truthiness test conflates
# them.
ENV_PREFIXES = ("DOTNET", "NUGET", "MSBUILD", "LC_", "CLR_")
ENV_NAMES = ("PATH", "HOME", "LANG", "XDG_DATA_HOME", "XDG_CONFIG_HOME")

# Our own control variables must never enter the key: they change how the
# helper behaves, not what the command computes.
CONTROL_NAMES = ("CATMETRO_NO_TEST_CACHE", "CATMETRO_TEST_CACHE_DIR")


class Uncacheable(Exception):
    """An input could not be keyed. Always resolves to a real run."""


def _frame(payload: bytes) -> bytes:
    """Length-prefix a field so concatenated digests cannot alias."""
    return len(payload).to_bytes(8, "big") + payload


def _feed(h, *payloads: bytes) -> None:
    for payload in payloads:
        h.update(_frame(payload))


def _git(root: Path, *args: str) -> bytes:
    try:
        done = subprocess.run(
            ("git", "-C", str(root), *args),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
    except OSError as exc:
        raise Uncacheable(f"git unavailable: {exc}") from exc
    if done.returncode != 0:
        raise Uncacheable(f"git {' '.join(args)} failed: {done.stderr[:200]!r}")
    return done.stdout


def _path_state(path: Path) -> tuple[bytes, bytes]:
    """Return (content_identity, stat_identity) for one working-tree path.

    content_identity is what a compiler would read; it alone goes into the
    cache key, so a fresh clone or a bare `touch` does not spuriously miss.
    stat_identity additionally pins (size, mtime_ns, ctime_ns) and is used
    only for the before/after mutation bracket, where it catches a command
    that rewrites an input and restores the original bytes -- content-only
    comparison would call that unchanged and publish a stale green.
    """
    try:
        info = path.lstat()
    except FileNotFoundError:
        return b"absent", b"absent"
    except OSError as exc:
        raise Uncacheable(f"cannot stat {path}: {exc}") from exc

    if stat.S_ISLNK(info.st_mode):
        try:
            target = os.readlink(path).encode()
        except OSError as exc:
            raise Uncacheable(f"cannot read link {path}: {exc}") from exc
        content = b"symlink:" + target
    elif stat.S_ISREG(info.st_mode):
        digest = hashlib.sha256()
        try:
            with open(path, "rb") as handle:
                for chunk in iter(lambda: handle.read(1 << 20), b""):
                    digest.update(chunk)
        except OSError as exc:
            raise Uncacheable(f"cannot read {path}: {exc}") from exc
        content = b"file:" + digest.digest()
    else:
        # Fifos, sockets, devices: describable but not readable as source.
        content = b"special:" + str(stat.S_IFMT(info.st_mode)).encode()

    marks = f"{info.st_size}:{info.st_mtime_ns}:{info.st_ctime_ns}".encode()
    return content, content + b"|" + marks


def _status_paths(payload: bytes) -> list[str]:
    """Paths git reports as modified, deleted, or untracked (.gitignore honoured)."""
    paths: list[str] = []
    for record in payload.split(b"\0"):
        if len(record) < 4:
            continue
        # porcelain=v1 -z with --no-renames: "XY <path>"
        paths.append(record[3:].decode("utf-8", "surrogateescape"))
    return paths


def _tree_digests(root: Path) -> tuple[bytes, bytes]:
    """Fingerprint the whole working tree: (content_digest, witness_digest).

    Scope is deliberately the ENTIRE repository, not a curated list of source
    globs. The .csproj files glob `../../unity/Assets/Scripts/**/*.cs`, tests
    read `content/levels/`, and fixtures live under `tests/` -- proving a
    narrow input list complete is exactly what sank the previous attempt.
    Hashing everything over-invalidates (a docs edit costs a rebuild) and is
    never wrong. It costs ~0.3 s against a 529 s command.
    """
    content = hashlib.sha256()
    witness = hashlib.sha256()

    # Tracked content, straight from the index (blob SHAs, mode, path).
    index = _git(root, "ls-files", "-s", "-z")
    _feed(content, b"index", index)
    _feed(witness, b"index", index)

    # ...overlaid with anything the working tree says differs from it, plus
    # untracked-but-not-ignored files.
    status = _git(
        root, "status", "--porcelain=v1", "-z", "--untracked-files=all", "--no-renames"
    )
    for rel in sorted(set(_status_paths(status))):
        c, w = _path_state(root / rel)
        _feed(content, rel.encode("utf-8", "surrogateescape"), c)
        _feed(witness, rel.encode("utf-8", "surrogateescape"), w)

    # Build-policy files by name, so a gitignored Directory.Build.props (which
    # `git status` will never list) still moves the key.
    for name in POLICY_NAMES:
        c, w = _path_state(root / name)
        _feed(content, b"policy:" + name.encode(), c)
        _feed(witness, b"policy:" + name.encode(), w)

    return content.digest(), witness.digest()


def _external_policy_digest(root: Path, env: dict) -> bytes:
    """Out-of-repo MSBuild/NuGet policy that the in-repo walk cannot see.

    Covers the first blocked edge case, "unkeyed MSBuild user extensions", in
    two parts: build-policy files in every ancestor directory of the repo
    root, and the user-extension import directories MSBuild auto-imports.
    """
    h = hashlib.sha256()

    for ancestor in root.parents:
        for name in POLICY_NAMES:
            c, _ = _path_state(ancestor / name)
            if c != b"absent":
                _feed(h, str(ancestor / name).encode(), c)

    # User-level NuGet configuration is read regardless of where the repo sits.
    home = env.get("HOME")
    if home:
        for rel in (
            ".nuget/NuGet/NuGet.Config",
            ".config/NuGet/NuGet.Config",
        ):
            c, _ = _path_state(Path(home) / rel)
            _feed(h, rel.encode(), c)

    # $(MSBuildUserExtensionsPath)/**/{ImportBefore,ImportAfter}/* -- a .props
    # dropped here silently changes every build on the machine.
    for base in _user_extension_roots(env):
        _feed(h, b"userext", str(base).encode())
        for path in _walk_files(base):
            c, _ = _path_state(path)
            _feed(h, str(path.relative_to(base)).encode(), c)

    return h.digest()


def _user_extension_roots(env: dict) -> list[Path]:
    override = env.get("MSBuildUserExtensionsPath")
    if override is not None:
        # Present-but-empty is a real, distinct configuration; refuse to guess.
        if not override:
            raise Uncacheable("MSBuildUserExtensionsPath is set but empty")
        return [Path(override)]
    home = env.get("HOME")
    if not home:
        raise Uncacheable("HOME is unset; cannot locate MSBuild user extensions")
    roots = [Path(home) / ".local/share/Microsoft/MSBuild", Path(home) / ".microsoft/msbuild"]
    data_home = env.get("XDG_DATA_HOME")
    if data_home:
        roots.append(Path(data_home) / "Microsoft/MSBuild")
    return roots


def _walk_files(base: Path) -> list[Path]:
    """Every regular file under base, sorted. Missing base is fine; unreadable is not."""
    found: list[Path] = []
    if not base.exists():
        return found
    try:
        for parent, dirs, names in os.walk(base, onerror=_raise_walk_error):
            dirs.sort()
            for name in sorted(names):
                found.append(Path(parent) / name)
    except OSError as exc:
        raise Uncacheable(f"cannot enumerate {base}: {exc}") from exc
    return found


def _raise_walk_error(exc: OSError) -> None:
    raise exc


def _toolchain_digest(root: Path, argv: list[str], env: dict) -> bytes:
    """Identity of the toolchain that will execute the command.

    `dotnet --info` pins SDK version, host version, RID and every installed
    shared runtime; `dotnet workload list` pins workload state (including the
    empty case). The previous attempt additionally content-hashed the entire
    ~1 GB SDK tree three times per lookup, which plausibly cost more than the
    runs it saved. An installed SDK is immutable by version in practice, so
    the version string is the honest stopping point -- see LIMITS below.
    """
    tool = argv[0]
    exe = shutil.which(tool, path=env.get("PATH"))
    if exe is None:
        raise Uncacheable(f"{tool} is not on PATH")
    real = os.path.realpath(exe)

    h = hashlib.sha256()
    _feed(h, b"tool", real.encode())
    c, _ = _path_state(Path(real))
    _feed(h, b"toolbytes", c)

    if os.path.basename(real) == "dotnet" or tool == "dotnet":
        for probe in (("--version",), ("--info",), ("workload", "list")):
            try:
                done = subprocess.run(
                    (real, *probe),
                    cwd=str(root),
                    env=env,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    check=False,
                )
            except OSError as exc:
                raise Uncacheable(f"probe {probe} failed: {exc}") from exc
            if done.returncode != 0:
                raise Uncacheable(f"probe {probe} exited {done.returncode}")
            _feed(h, " ".join(probe).encode(), done.stdout)

    return h.digest()


def _env_digest(env: dict) -> bytes:
    """Tri-state digest of the build-relevant environment.

    An absent name contributes nothing; a name set to "" contributes its
    framed name and an empty value. That is the fix for the "empty workload
    overrides" edge case -- `any(env.get(n) for n in names)` treats
    `DOTNETSDK_WORKLOAD_PACK_ROOTS=""` as absent, which it is not.
    """
    h = hashlib.sha256()
    for name in sorted(env):
        if name in CONTROL_NAMES:
            continue
        upper = name.upper()
        if upper.startswith(ENV_PREFIXES) or name in ENV_NAMES:
            _feed(h, name.encode(), env[name].encode("utf-8", "surrogateescape"))
    return h.digest()


def _compute_key(root: Path, argv: list[str], slot: int, env: dict) -> tuple[str, bytes]:
    """Return (hex cache key, witness digest for the mutation bracket)."""
    content, witness = _tree_digests(root)
    h = hashlib.sha256()
    _feed(
        h,
        str(SCHEMA).encode(),
        b"tree",
        content,
        b"external",
        _external_policy_digest(root, env),
        b"tool",
        _toolchain_digest(root, argv, env),
        b"env",
        _env_digest(env),
        b"root",
        str(root).encode(),
        b"slot",
        str(slot).encode(),
        b"argc",
        str(len(argv)).encode(),
    )
    for arg in argv:
        _feed(h, b"arg", arg.encode("utf-8", "surrogateescape"))
    return h.hexdigest(), witness


def _cache_dir(env: dict) -> Path:
    override = env.get("CATMETRO_TEST_CACHE_DIR")
    if override:
        base = Path(override)
    else:
        home = env.get("HOME")
        if not home:
            raise Uncacheable("HOME is unset; cannot locate the cache")
        base = Path(home) / ".cache/cat-metro/test-cache"
    try:
        base.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        raise Uncacheable(f"cannot create {base}: {exc}") from exc
    return base


def _record_checksum(core: dict) -> str:
    canonical = json.dumps(core, sort_keys=True, separators=(",", ":")).encode()
    return hashlib.sha256(canonical).hexdigest()


def _load(path: Path, key: str) -> tuple[int, bytes] | None:
    """Read a record, or return None for any reason at all.

    Every rejection here is a miss, never a hit: truncated file, bad JSON,
    wrong schema, checksum mismatch, key mismatch, bad base64.
    """
    try:
        if path.stat().st_size > CACHE_ENTRY_LIMIT:
            return None
        raw = path.read_bytes()
    except OSError:
        return None
    try:
        record = json.loads(raw)
    except (ValueError, UnicodeDecodeError):
        return None
    if not isinstance(record, dict):
        return None
    checksum = record.pop("checksum", None)
    if not isinstance(checksum, str) or checksum != _record_checksum(record):
        return None
    if record.get("schema") != SCHEMA or record.get("key") != key:
        return None
    returncode = record.get("returncode")
    if not isinstance(returncode, int):
        return None
    try:
        stdout = base64.b64decode(record.get("stdout", ""), validate=True)
    except (ValueError, TypeError):
        return None
    return returncode, stdout


def _publish(path: Path, key: str, argv: list[str], slot: int, returncode: int, stdout: bytes) -> None:
    """Write a record so no reader can ever observe a partial one.

    A single JSON file installed with os.replace(): the rename is atomic on
    POSIX, so a concurrent reader sees either the old complete file or the new
    complete file. This is the whole answer to the "concurrent-writer window"
    edge case -- there is no lock, because two writers racing on the same key
    simply both run the real command and both publish byte-identical results.
    Duplicated work, never a torn read, and none of the flock/TOCTOU surface
    that the previous attempt was blocked on.
    """
    core = {
        "schema": SCHEMA,
        "key": key,
        "argv": argv,
        "slot": slot,
        "returncode": returncode,
        "stdout": base64.b64encode(stdout).decode("ascii"),
    }
    core["checksum"] = _record_checksum({k: v for k, v in core.items()})
    payload = json.dumps(core, sort_keys=True, separators=(",", ":")).encode()

    handle, temporary = tempfile.mkstemp(prefix=".record.", dir=str(path.parent))
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, 0o600)
        os.replace(temporary, path)
    except OSError:
        # A cache we cannot write is not an error; the run already happened.
        try:
            os.unlink(temporary)
        except OSError:
            pass


def _run(argv: list[str], root: Path, env: dict) -> tuple[int, bytes]:
    try:
        done = subprocess.run(
            argv,
            cwd=str(root),
            env=env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            check=False,
        )
    except OSError as exc:
        sys.stderr.write(f"test_cache: cannot execute {argv[0]}: {exc}\n")
        return 127, b""
    return done.returncode, done.stdout


def _emit(returncode: int, stdout: bytes) -> int:
    sys.stdout.buffer.write(stdout)
    sys.stdout.buffer.flush()
    return returncode


def main(raw: list[str]) -> int:
    slot = 1
    args = list(raw)
    while args and args[0] != "--":
        if args[0] == "--slot":
            if len(args) < 2:
                sys.stderr.write("test_cache: --slot needs a value\n")
                return 2
            try:
                slot = int(args[1])
            except ValueError:
                sys.stderr.write("test_cache: --slot needs an integer\n")
                return 2
            args = args[2:]
        else:
            sys.stderr.write(f"test_cache: unknown option {args[0]}\n")
            return 2
    if not args or args[0] != "--":
        sys.stderr.write("usage: test_cache.py [--slot N] -- <command> [args...]\n")
        return 2
    argv = args[1:]
    if not argv:
        sys.stderr.write("test_cache: empty command\n")
        return 2

    env = dict(os.environ)

    try:
        root = Path(
            subprocess.run(
                ("git", "rev-parse", "--show-toplevel"),
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                check=True,
            )
            .stdout.decode()
            .strip()
        )
    except (OSError, subprocess.CalledProcessError):
        return _emit(*_run(argv, Path.cwd(), env))

    if env.get("CATMETRO_NO_TEST_CACHE"):
        return _emit(*_run(argv, root, env))

    # ---- lookup -------------------------------------------------------
    try:
        key, witness_before = _compute_key(root, argv, slot, env)
        entry = _cache_dir(env) / f"{key}.json"
    except Uncacheable as exc:
        sys.stderr.write(f"test_cache: not cacheable ({exc}); running for real\n")
        return _emit(*_run(argv, root, env))

    hit = _load(entry, key)
    if hit is not None:
        # Re-verify the tree has not moved between fingerprint and replay.
        try:
            recheck, _ = _compute_key(root, argv, slot, env)
        except Uncacheable:
            recheck = None
        if recheck == key:
            sys.stderr.write(f"test_cache: hit {key[:12]} (slot {slot})\n")
            return _emit(*hit)

    # ---- miss ---------------------------------------------------------
    sys.stderr.write(f"test_cache: miss {key[:12]} (slot {slot}); running\n")
    returncode, stdout = _run(argv, root, env)

    # Only successes are recorded. A red run must stay red on every
    # invocation so an iterating developer never replays a stale failure,
    # and a flaky failure never becomes sticky.
    if returncode != 0:
        return _emit(returncode, stdout)

    # Mutation bracket: if the command rewrote any fingerprinted input --
    # `dotnet restore` rewriting packages.lock.json is the live example in
    # this repo -- the result does not belong to the key we computed. Run it,
    # honour it, record nothing.
    try:
        _, witness_after = _tree_digests(root)
    except Uncacheable:
        witness_after = None
    if witness_after == witness_before:
        _publish(entry, key, argv, slot, returncode, stdout)
    else:
        sys.stderr.write("test_cache: inputs changed during the run; not recording\n")

    return _emit(returncode, stdout)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
