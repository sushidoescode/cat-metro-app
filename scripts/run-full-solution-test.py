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
ACTIVE_ENV = "CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE"
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
ROOT_BUILD_FILES = (
    "global.json",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "Directory.Packages.targets",
    "Directory.Build.rsp",
    "Directory.Solution.props",
    "Directory.Solution.targets",
    "MSBuild.rsp",
    ".globalconfig",
    ".editorconfig",
    "NuGet.Config",
    "nuget.config",
)
OUTSIDE_BUILD_FILES = ROOT_BUILD_FILES


class Uncacheable(RuntimeError):
    """The real command remains safe, but this snapshot must not be reused."""


@dataclass(frozen=True)
class Snapshot:
    digest: str
    tool_path: str


StatIdentity = tuple[int, int, int, int, int, int]


def _stat_identity(details: os.stat_result) -> StatIdentity:
    return (
        details.st_dev,
        details.st_ino,
        details.st_mode,
        details.st_size,
        details.st_mtime_ns,
        details.st_ctime_ns,
    )


@dataclass
class ObservedInputs:
    """Inputs seen during one snapshot, revalidated together before it may be trusted."""

    files: dict[Path, StatIdentity]
    missing: set[Path]
    git_manifests: list[tuple[Path, tuple[str, ...]]]
    explicit_manifests: list[
        tuple[Path, tuple[tuple[str, ...], tuple[str, ...]]]
    ]
    external_manifests: list[tuple[Path, tuple[str, ...]]]

    @classmethod
    def empty(cls) -> "ObservedInputs":
        return cls({}, set(), [], [], [])

    def present(self, path: Path, details: os.stat_result) -> None:
        identity = _stat_identity(details)
        previous = self.files.setdefault(path, identity)
        if previous != identity or path in self.missing:
            raise Uncacheable("an observed input changed during fingerprinting")

    def absent(self, path: Path) -> None:
        if path in self.files:
            raise Uncacheable("an observed input disappeared during fingerprinting")
        self.missing.add(path)

    def verify(self) -> None:
        # Recheck every early-observed object only after source, toolchain, packages, and
        # configuration have all been fingerprinted. Per-file stability alone permits a late
        # write to an early-sorted file to escape an otherwise equal final snapshot.
        for path in sorted(self.files, key=lambda item: os.fsencode(os.fspath(item))):
            try:
                details = os.lstat(path)
            except OSError as error:
                raise Uncacheable("an observed input vanished before snapshot commit") from error
            if _stat_identity(details) != self.files[path]:
                raise Uncacheable("an observed input changed before snapshot commit")
        for path in sorted(self.missing, key=lambda item: os.fsencode(os.fspath(item))):
            try:
                os.lstat(path)
            except FileNotFoundError:
                continue
            except OSError as error:
                raise Uncacheable("a missing-input marker cannot be revalidated") from error
            raise Uncacheable("a previously missing input appeared before snapshot commit")
        for root, expected in self.git_manifests:
            if _git_paths(root) != expected:
                raise Uncacheable("Git input membership changed before snapshot commit")
        for root, expected in self.explicit_manifests:
            if _explicit_paths(root, None) != expected:
                raise Uncacheable("explicit input membership changed before snapshot commit")
        for root, expected in self.external_manifests:
            if _external_tree_paths(root, None) != expected:
                raise Uncacheable("external input membership changed before snapshot commit")


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


def _explicit_paths(
    root: Path, observed: ObservedInputs | None
) -> tuple[tuple[str, ...], tuple[str, ...]]:
    files: set[str] = set()
    markers: list[str] = []

    for relative_root in EXPLICIT_TREES:
        start = root / relative_root
        try:
            start_stat = os.lstat(start)
        except FileNotFoundError:
            if observed is not None:
                observed.absent(start)
            markers.append("missing:" + relative_root)
            continue
        if not stat.S_ISDIR(start_stat.st_mode) or stat.S_ISLNK(start_stat.st_mode):
            raise Uncacheable("an explicit input root is not a real directory")
        if observed is not None:
            observed.present(start, start_stat)

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
                    if observed is not None:
                        observed.present(Path(entry.path), entry_stat)
                    if entry.name not in WALK_EXCLUDES:
                        stack.append(Path(entry.path))
                elif stat.S_ISREG(entry_stat.st_mode):
                    files.add(relative)
                else:
                    raise Uncacheable("a non-regular input is reachable from an input tree")

    return tuple(sorted(files, key=os.fsencode)), tuple(sorted(markers))


def _guard_outside_build_files(root: Path, observed: ObservedInputs) -> None:
    # MSBuild searches parent directories. An out-of-repo build policy is not owned by this task,
    # so detect it without reading it and fall back to the ordinary command.
    for parent in root.parents:
        for name in OUTSIDE_BUILD_FILES:
            candidate = parent / name
            try:
                candidate_stat = os.lstat(candidate)
            except FileNotFoundError:
                observed.absent(candidate)
                continue
            except OSError as error:
                raise Uncacheable("could not inspect an ancestor build policy") from error
            if not stat.S_ISREG(candidate_stat.st_mode):
                raise Uncacheable("an ancestor build policy is not a regular file")
            raise Uncacheable("an out-of-repo build policy is active")


def _hash_file(
    root: Path,
    relative: str,
    aggregate: "hashlib._Hash",
    observed: ObservedInputs,
) -> None:
    if _secret_like(relative):
        raise Uncacheable("refusing to read a secret-like input")
    path = root / relative
    try:
        before = os.lstat(path)
    except FileNotFoundError:
        observed.absent(path)
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
    stable_fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_size",
        "st_mtime_ns",
        "st_ctime_ns",
    )
    if any(getattr(before, field) != getattr(opened, field) for field in stable_fields):
        raise Uncacheable("an input changed before it was read")
    if any(getattr(opened, field) != getattr(after_read, field) for field in stable_fields):
        raise Uncacheable("an input changed while it was read")
    if any(getattr(after_read, field) != getattr(after, field) for field in stable_fields):
        raise Uncacheable("an input changed after it was read")

    observed.present(path, after)
    _frame(aggregate, b"regular")
    _frame(
        aggregate,
        ":".join(str(value) for value in _stat_identity(after)).encode("ascii"),
    )
    _frame(aggregate, content.digest())


def _external_tree_paths(
    start: Path, observed: ObservedInputs | None
) -> tuple[str, ...]:
    """Enumerate a consumed external tree without following links or special files."""
    if not start.is_absolute() or os.path.abspath(start) != os.path.realpath(start):
        raise Uncacheable("an external input tree is not an absolute real path")
    try:
        details = os.lstat(start)
    except OSError as error:
        raise Uncacheable("an external input tree is missing") from error
    if not stat.S_ISDIR(details.st_mode) or stat.S_ISLNK(details.st_mode):
        raise Uncacheable("an external input tree is not a real directory")
    if observed is not None:
        observed.present(start, details)

    files: list[str] = []
    stack = [start]
    while stack:
        directory = stack.pop()
        try:
            entries = sorted(os.scandir(directory), key=lambda entry: os.fsencode(entry.name))
        except OSError as error:
            raise Uncacheable("an external input tree is unreadable") from error
        for entry in entries:
            try:
                entry_stat = entry.stat(follow_symlinks=False)
            except OSError as error:
                raise Uncacheable("an external input entry is unreadable") from error
            if stat.S_ISLNK(entry_stat.st_mode):
                raise Uncacheable("a symlink is reachable from an external input tree")
            if stat.S_ISDIR(entry_stat.st_mode):
                if observed is not None:
                    observed.present(Path(entry.path), entry_stat)
                stack.append(Path(entry.path))
            elif stat.S_ISREG(entry_stat.st_mode):
                relative = Path(entry.path).relative_to(start).as_posix()
                if _secret_like(relative):
                    raise Uncacheable("a secret-like external input is reachable")
                files.append(relative)
            else:
                raise Uncacheable("a non-regular external input is reachable")
    return tuple(sorted(files, key=os.fsencode))


def _hash_external_tree(
    start: Path,
    label: str,
    aggregate: "hashlib._Hash",
    observed: ObservedInputs,
) -> None:
    before = _external_tree_paths(start, observed)
    observed.external_manifests.append((start, before))
    _frame(aggregate, os.fsencode(label))
    for relative in before:
        _frame(aggregate, os.fsencode(relative))
        _hash_file(start, relative, aggregate, observed)
    if _external_tree_paths(start, None) != before:
        raise Uncacheable("external input membership changed during fingerprinting")


def _hash_optional_external_file(
    path: Path,
    label: str,
    aggregate: "hashlib._Hash",
    observed: ObservedInputs,
) -> None:
    _frame(aggregate, os.fsencode(label))
    try:
        os.lstat(path)
    except FileNotFoundError:
        observed.absent(path)
        _frame(aggregate, b"missing")
        return
    except OSError as error:
        raise Uncacheable("an external configuration cannot be inspected") from error
    if not path.is_absolute() or os.path.abspath(path) != os.path.realpath(path):
        raise Uncacheable("an external configuration is not an absolute real path")
    _hash_file(path.parent, path.name, aggregate, observed)


def _hash_optional_external_tree(
    path: Path,
    label: str,
    aggregate: "hashlib._Hash",
    observed: ObservedInputs,
) -> None:
    try:
        os.lstat(path)
    except FileNotFoundError:
        observed.absent(path)
        _frame(aggregate, os.fsencode(label))
        _frame(aggregate, b"missing")
        return
    except OSError as error:
        raise Uncacheable("an external configuration tree cannot be inspected") from error
    _hash_external_tree(path, label, aggregate, observed)


def _read_regular_bytes(
    path: Path, observed: ObservedInputs, *, maximum: int = 16 * 1024 * 1024
) -> bytes:
    try:
        before = os.lstat(path)
    except OSError as error:
        raise Uncacheable("a structured input cannot be inspected") from error
    if not stat.S_ISREG(before.st_mode) or stat.S_ISLNK(before.st_mode):
        raise Uncacheable("a structured input is not a regular file")
    if before.st_size > maximum:
        raise Uncacheable("a structured input exceeds its read limit")

    flags = os.O_RDONLY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(path, flags)
    except OSError as error:
        raise Uncacheable("a structured input cannot be opened safely") from error
    try:
        opened = os.fstat(descriptor)
        if not stat.S_ISREG(opened.st_mode) or opened.st_size > maximum:
            raise Uncacheable("an opened structured input is invalid")
        chunks: list[bytes] = []
        size = 0
        while True:
            block = os.read(descriptor, 64 * 1024)
            if not block:
                break
            size += len(block)
            if size > maximum:
                raise Uncacheable("a structured input grew past its read limit")
            chunks.append(block)
        after_read = os.fstat(descriptor)
    finally:
        os.close(descriptor)
    try:
        after = os.lstat(path)
    except OSError as error:
        raise Uncacheable("a structured input vanished while being read") from error
    identity = _stat_identity(before)
    if (
        _stat_identity(opened) != identity
        or _stat_identity(after_read) != identity
        or _stat_identity(after) != identity
    ):
        raise Uncacheable("a structured input changed while being read")
    observed.present(path, after)
    return b"".join(chunks)


def _locked_packages(
    lock_path: Path, observed: ObservedInputs
) -> tuple[tuple[str, str], ...]:
    packages: set[tuple[str, str]] = set()
    try:
        document = json.loads(_read_regular_bytes(lock_path, observed))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise Uncacheable("a NuGet lock file cannot be parsed") from error
    frameworks = document.get("dependencies") if isinstance(document, dict) else None
    if not isinstance(frameworks, dict):
        raise Uncacheable("a NuGet lock file has no dependency map")
    for package_map in frameworks.values():
        if not isinstance(package_map, dict):
            raise Uncacheable("a NuGet lock framework is malformed")
        for package_id, metadata in package_map.items():
            if not isinstance(package_id, str) or not isinstance(metadata, dict):
                raise Uncacheable("a NuGet lock package is malformed")
            resolved = metadata.get("resolved")
            package_type = metadata.get("type")
            if package_type == "Project":
                continue
            if not isinstance(resolved, str) or not resolved:
                raise Uncacheable("a NuGet lock package has no resolved version")
            packages.add((package_id.lower(), resolved.lower()))
    return tuple(sorted(packages, key=lambda item: (os.fsencode(item[0]), os.fsencode(item[1]))))


def _project_paths(root: Path, observed: ObservedInputs) -> tuple[Path, ...]:
    projects = tuple(
        sorted(
            root.glob("dotnet/*/*.csproj"),
            key=lambda path: os.fsencode(path.relative_to(root).as_posix()),
        )
    )
    if not projects:
        raise Uncacheable("no .NET project files were found")
    for project in projects:
        try:
            details = os.lstat(project)
        except OSError as error:
            raise Uncacheable("a .NET project cannot be inspected") from error
        if not stat.S_ISREG(details.st_mode) or stat.S_ISLNK(details.st_mode):
            raise Uncacheable("a .NET project is not a regular file")
        observed.present(project, details)
    return projects


def _effective_package_locations(
    root: Path,
    tool: str,
    environment: dict[str, str],
    aggregate: "hashlib._Hash",
    observed: ObservedInputs,
) -> tuple[tuple[Path, str, str], ...]:
    locations: set[tuple[Path, str, str]] = set()
    for project in _project_paths(root, observed):
        relative = project.relative_to(root).as_posix()
        arguments = (
            tool,
            "msbuild",
            relative,
            "-nologo",
            "-getProperty:NuGetPackageRoot",
            "-getProperty:RestorePackagesPath",
        )
        completed = subprocess.run(
            arguments,
            cwd=root,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        _frame(aggregate, b"effective-package-root")
        for argument in arguments:
            _frame(aggregate, os.fsencode(argument))
        _frame(aggregate, completed.stdout)
        _frame(aggregate, completed.stderr)
        if completed.returncode != 0:
            raise Uncacheable("MSBuild could not report the effective package root")
        try:
            report = json.loads(completed.stdout.decode("utf-8-sig"))
            properties = report["Properties"]
            raw_root = properties["NuGetPackageRoot"]
        except (KeyError, TypeError, UnicodeDecodeError, json.JSONDecodeError) as error:
            raise Uncacheable("MSBuild returned an invalid package-root report") from error
        if not isinstance(raw_root, str) or not raw_root:
            raise Uncacheable("MSBuild returned an empty effective package root")
        package_root = Path(raw_root)
        if (
            not package_root.is_absolute()
            or os.path.abspath(package_root) != os.path.realpath(package_root)
            or _secret_like(package_root.as_posix())
        ):
            raise Uncacheable("the effective package root is not an absolute real path")
        lock_path = project.with_name("packages.lock.json")
        for package_id, version in _locked_packages(lock_path, observed):
            locations.add((package_root, package_id, version))
    if not locations:
        raise Uncacheable("no effective locked NuGet package locations were found")
    return tuple(
        sorted(
            locations,
            key=lambda item: (
                os.fsencode(os.fspath(item[0])),
                os.fsencode(item[1]),
                os.fsencode(item[2]),
            ),
        )
    )


def _nuget_configuration_paths(environment: dict[str, str]) -> tuple[tuple[Path, str], ...]:
    home_raw = environment.get("HOME")
    if not home_raw:
        raise Uncacheable("HOME is unavailable for NuGet configuration discovery")
    home = Path(home_raw)
    candidates: list[tuple[Path, str]] = [
        (home / ".nuget/NuGet/NuGet.Config", "user-nuget-config"),
        (home / ".config/NuGet/NuGet.Config", "xdg-default-nuget-config"),
        (Path("/etc/nuget.config"), "machine-nuget-config-lower"),
        (Path("/etc/NuGet.Config"), "machine-nuget-config"),
    ]
    xdg_raw = environment.get("XDG_CONFIG_HOME")
    if xdg_raw:
        candidates.append((Path(xdg_raw) / "NuGet/NuGet.Config", "xdg-nuget-config"))
    appdata_raw = environment.get("APPDATA")
    if appdata_raw:
        candidates.append((Path(appdata_raw) / "NuGet/NuGet.Config", "appdata-nuget-config"))
    return tuple(candidates)


def _hash_external_build_inputs(
    root: Path,
    tool: str,
    environment: dict[str, str],
    dotnet_info: str,
    aggregate: "hashlib._Hash",
    observed: ObservedInputs,
) -> None:
    external_overrides = (
        "DOTNET_ADDITIONAL_DEPS",
        "DOTNET_HOST_PATH",
        "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR",
        "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR",
        "DOTNET_SHARED_STORE",
        "DOTNET_STARTUP_HOOKS",
        "MSBUILD_EXE_PATH",
        "MSBuildExtensionsPath",
        "MSBuildExtensionsPath32",
        "MSBuildExtensionsPath64",
        "MSBuildSDKsPath",
        "NUGET_CREDENTIALPROVIDERS_PATH",
        "NUGET_FALLBACK_PACKAGES",
        "NUGET_PLUGIN_PATHS",
    )
    if any(environment.get(name) for name in external_overrides):
        raise Uncacheable("an external .NET/MSBuild override is active")

    base_match = re.search(r"(?im)^\s*Base Path:\s*(.+?)\s*$", dotnet_info)
    if base_match is None:
        raise Uncacheable("dotnet --info did not report the selected SDK base path")
    sdk = Path(base_match.group(1))
    _hash_external_tree(sdk, "selected-dotnet-sdk", aggregate, observed)

    host_match = re.search(
        r"(?m)^Host:[ \t]*\r?\n[ \t]*Version:[ \t]*([^\s]+)", dotnet_info
    )
    if host_match is None:
        raise Uncacheable("dotnet --info did not report the selected host version")
    dotnet_root = sdk.parent.parent
    _hash_external_tree(
        dotnet_root / "host/fxr" / host_match.group(1),
        "selected-dotnet-hostfxr:" + host_match.group(1),
        aggregate,
        observed,
    )
    _hash_external_tree(dotnet_root / "packs", "selected-dotnet-packs", aggregate, observed)
    _hash_optional_external_tree(
        dotnet_root / "sdk-manifests", "dotnet-workload-manifests", aggregate, observed
    )
    _hash_optional_external_tree(
        dotnet_root / "metadata/workloads", "dotnet-workload-metadata", aggregate, observed
    )

    runtime_pattern = re.compile(r"(?m)^\s*(Microsoft\.[^\s]+)\s+([^\s]+)\s+\[(.+?)\]\s*$")
    for runtime_name, runtime_version, runtime_parent in runtime_pattern.findall(dotnet_info):
        runtime = Path(runtime_parent) / runtime_version
        _hash_external_tree(
            runtime,
            "runtime:" + runtime_name + ":" + runtime_version,
            aggregate,
            observed,
        )

    # Hash configuration before asking MSBuild to evaluate each project's effective package
    # root. A configured globalPackagesFolder or RestorePackagesPath may differ from both HOME
    # and NUGET_PACKAGES; the evaluated NuGetPackageRoot is the directory the real build consumes.
    for config, label in _nuget_configuration_paths(environment):
        _hash_optional_external_file(config, label, aggregate, observed)
    _hash_optional_external_tree(
        Path(environment["HOME"]) / ".nuget/NuGet/config",
        "user-nuget-fragments",
        aggregate,
        observed,
    )
    _hash_optional_external_tree(
        Path("/etc/opt/NuGet/Config"), "machine-nuget-fragments", aggregate, observed
    )
    _hash_optional_external_tree(
        Path("/Library/Application Support/NuGet/Config"),
        "macos-machine-nuget-fragments",
        aggregate,
        observed,
    )
    xdg_raw = environment.get("XDG_CONFIG_HOME")
    if xdg_raw:
        _hash_optional_external_tree(
            Path(xdg_raw) / "NuGet/config", "xdg-nuget-fragments", aggregate, observed
        )
    appdata_raw = environment.get("APPDATA")
    if appdata_raw:
        _hash_optional_external_tree(
            Path(appdata_raw) / "NuGet/config", "appdata-nuget-fragments", aggregate, observed
        )

    for package_root, package_id, version in _effective_package_locations(
        root, tool, environment, aggregate, observed
    ):
        _hash_external_tree(
            package_root / package_id / version,
            "nuget-package:"
            + os.fspath(package_root)
            + ":"
            + package_id
            + ":"
            + version,
            aggregate,
            observed,
        )


def _input_digest(root: Path, observed: ObservedInputs) -> str:
    _guard_outside_build_files(root, observed)
    git_before = _git_paths(root)
    observed.git_manifests.append((root, git_before))
    explicit_before, markers_before = _explicit_paths(root, observed)
    observed.explicit_manifests.append((root, (explicit_before, markers_before)))
    paths = tuple(
        sorted(set(git_before).union(explicit_before, ROOT_BUILD_FILES), key=os.fsencode)
    )

    aggregate = hashlib.sha256()
    _frame(aggregate, b"cat-metro-input-v1")
    for marker in markers_before:
        _frame(aggregate, os.fsencode(marker))
    for relative in paths:
        _frame(aggregate, os.fsencode(relative))
        _hash_file(root, relative, aggregate, observed)

    git_after = _git_paths(root)
    explicit_after, markers_after = _explicit_paths(root, None)
    if (
        git_after != git_before
        or explicit_after != explicit_before
        or markers_after != markers_before
    ):
        raise Uncacheable("input membership changed during fingerprinting")
    return aggregate.hexdigest()


def _environment_digest(environment: dict[str, str]) -> str:
    aggregate = hashlib.sha256()
    _frame(aggregate, b"cat-metro-environment-v1")
    for name in sorted(environment, key=os.fsencode):
        _frame(aggregate, os.fsencode(name))
        _frame(aggregate, os.fsencode(environment[name]))
    return aggregate.hexdigest()


def _tool_identity(
    environment: dict[str, str], root: Path, observed: ObservedInputs
) -> tuple[str, str]:
    located = shutil.which("dotnet", path=environment.get("PATH"))
    if not located:
        raise Uncacheable("dotnet is not on PATH")
    resolved = os.path.realpath(located)
    binary_digest = hashlib.sha256()
    _frame(binary_digest, os.fsencode(resolved))
    _hash_file(Path(resolved).parent, Path(resolved).name, binary_digest, observed)

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
    _hash_external_build_inputs(root, resolved, environment, text, binary_digest, observed)
    return resolved, binary_digest.hexdigest()


def _snapshot(root: Path, environment: dict[str, str]) -> Snapshot:
    observed = ObservedInputs.empty()
    # Validate repository inputs before invoking toolchain probes, then retain every observed
    # identity through a final global pass so neither phase can mutate the other invisibly.
    source_digest = _input_digest(root, observed)
    tool_path, tool_digest = _tool_identity(environment, root, observed)
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
    observed.verify()
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
        returncode = subprocess.run(arguments, cwd=root, env=environment, check=False).returncode
        return returncode if returncode >= 0 else 128 - returncode
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
    active_control: str,
    cache_control: str,
    artifact_control: str | None,
) -> int:
    try:
        active = _private_directory(active_control)
        cache = _private_directory(cache_control)
        if cache.parent != active or cache.name != "cache":
            raise Uncacheable("the cache is not bound to the active harness session")
        if artifact_control:
            artifact_candidate = Path(artifact_control)
            if artifact_candidate.parent != active or artifact_candidate.name != "artifacts":
                raise Uncacheable("the artifacts path is not bound to the active harness session")
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
            try:
                hit = _snapshot(root, environment)
            except (OSError, Uncacheable, ValueError):
                return _direct(environment, root)
            return 0 if hit == before else _direct(environment, root)

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


def _repository_root() -> Path:
    try:
        root_output = subprocess.check_output(
            ("git", "rev-parse", "--show-toplevel"), stderr=subprocess.DEVNULL
        )
        return Path(os.fsdecode(root_output.rstrip(b"\n"))).resolve(strict=True)
    except (OSError, subprocess.CalledProcessError) as error:
        raise Uncacheable("not in a Git worktree") from error


def _open_real_directory(path: Path, *, parent_fd: int | None = None) -> int:
    flags = os.O_RDONLY
    if hasattr(os, "O_DIRECTORY"):
        flags |= os.O_DIRECTORY
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    if hasattr(os, "O_CLOEXEC"):
        flags |= os.O_CLOEXEC
    descriptor = os.open(os.fspath(path), flags, dir_fd=parent_fd)
    details = os.fstat(descriptor)
    if not stat.S_ISDIR(details.st_mode):
        os.close(descriptor)
        raise Uncacheable("a cleanup path component is not a directory")
    return descriptor


def _remove_tree_at(
    parent_fd: int,
    name: str,
    expected_identity: tuple[int, int],
    *,
    require_private: bool = False,
) -> None:
    """Remove one real directory tree relative to a held parent fd (Python 3.9 compatible)."""
    descriptor = _open_real_directory(Path(name), parent_fd=parent_fd)
    try:
        opened = os.fstat(descriptor)
        opened_identity = (opened.st_dev, opened.st_ino)
        if opened_identity != expected_identity:
            raise Uncacheable("a cleanup directory was substituted before it was opened")
        if require_private:
            if hasattr(os, "getuid") and opened.st_uid != os.getuid():
                raise Uncacheable("the opened cleanup target has a different owner")
            if stat.S_IMODE(opened.st_mode) & 0o077:
                raise Uncacheable("the opened cleanup target is not private")
        with os.scandir(descriptor) as entries:
            for entry in entries:
                try:
                    details = entry.stat(follow_symlinks=False)
                except OSError as error:
                    raise Uncacheable("a cleanup entry cannot be inspected") from error
                if stat.S_ISDIR(details.st_mode) and not stat.S_ISLNK(details.st_mode):
                    _remove_tree_at(
                        descriptor, entry.name, (details.st_dev, details.st_ino)
                    )
                else:
                    try:
                        os.unlink(entry.name, dir_fd=descriptor)
                    except OSError as error:
                        raise Uncacheable("a cleanup entry cannot be removed safely") from error
        try:
            current = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
            if (
                not stat.S_ISDIR(current.st_mode)
                or stat.S_ISLNK(current.st_mode)
                or (current.st_dev, current.st_ino) != opened_identity
            ):
                raise Uncacheable("a cleanup directory was substituted before removal")
            os.rmdir(name, dir_fd=parent_fd)
        except OSError as error:
            raise Uncacheable("a cleanup directory cannot be removed safely") from error
    finally:
        os.close(descriptor)


def _cleanup_session(root: Path, raw: str) -> None:
    requested = Path(raw)
    normalized = Path(os.path.abspath(raw))
    base = root / "dotnet/CatMetro.Tests/obj/ci-full-solution"
    if not requested.is_absolute() or requested != normalized:
        raise Uncacheable("cleanup requires an absolute normalized path")
    if requested.parent != base or re.fullmatch(r"session\.[A-Za-z0-9]+", requested.name) is None:
        raise Uncacheable("cleanup target is not one exact harness session")
    relative_probe = (requested / ".ignore-probe").relative_to(root)
    ignored = subprocess.run(
        ("git", "-C", os.fspath(root), "check-ignore", "-q", "--", os.fspath(relative_probe)),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if ignored.returncode != 0:
        raise Uncacheable("cleanup target is not ignored")

    descriptor = _open_real_directory(root)
    try:
        for part in ("dotnet", "CatMetro.Tests", "obj", "ci-full-solution"):
            child = _open_real_directory(Path(part), parent_fd=descriptor)
            os.close(descriptor)
            descriptor = child
        try:
            details = os.stat(requested.name, dir_fd=descriptor, follow_symlinks=False)
        except FileNotFoundError:
            return
        if not stat.S_ISDIR(details.st_mode) or stat.S_ISLNK(details.st_mode):
            raise Uncacheable("cleanup target is not a real directory")
        if hasattr(os, "getuid") and details.st_uid != os.getuid():
            raise Uncacheable("cleanup target has a different owner")
        if stat.S_IMODE(details.st_mode) & 0o077:
            raise Uncacheable("cleanup target is not private")
        _remove_tree_at(
            descriptor,
            requested.name,
            (details.st_dev, details.st_ino),
            require_private=True,
        )
    finally:
        os.close(descriptor)


def main() -> int:
    try:
        root = _repository_root()
    except Uncacheable:
        print("full-solution test: not in a Git worktree", file=sys.stderr)
        return 2

    if len(sys.argv) == 3 and sys.argv[1] == "--cleanup-session":
        try:
            _cleanup_session(root, sys.argv[2])
        except (OSError, Uncacheable, ValueError) as error:
            print("full-solution cleanup: " + str(error), file=sys.stderr)
            return 2
        return 0
    if len(sys.argv) != 1:
        print(
            "usage: run-full-solution-test.py [--cleanup-session ABSOLUTE_SESSION]",
            file=sys.stderr,
        )
        return 2

    cache_control = os.environ.get(CACHE_ENV)
    active_control = os.environ.get(ACTIVE_ENV)
    artifact_control = os.environ.get(ARTIFACT_ENV)
    environment = dict(os.environ)
    environment.pop(CACHE_ENV, None)
    environment.pop(ACTIVE_ENV, None)
    environment.pop(ARTIFACT_ENV, None)
    if not cache_control or not active_control:
        return _direct(environment, root)
    return _cached(root, environment, active_control, cache_control, artifact_control)


if __name__ == "__main__":
    raise SystemExit(main())
