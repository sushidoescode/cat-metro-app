#!/usr/bin/env bash
# ADR-0011 public-repository custody gate. Licensed derivatives may exist only as
# ignored local inputs; this test never prints their contents.
set -uo pipefail
fail() { echo "polyfork-custody.test.sh: FAIL — $1"; exit 1; }

custody_script_dir="${BASH_SOURCE[0]%/*}"
[ "$custody_script_dir" != "${BASH_SOURCE[0]}" ] || custody_script_dir=.
. "$custody_script_dir/../../scripts/reject-git-redirect-env.sh"
catmetro_reject_git_redirect_env "polyfork-custody.test.sh" || exit 1
catmetro_require_checkout_root "$custody_script_dir/../.." "polyfork-custody.test.sh" || exit 1

model_root="unity/Assets/Art/Polyfork/Models"
custody_profile="${CM_REQUIRE_POLYFORK_LOCAL:-0}"
case "$custody_profile" in
  0|1) ;;
  *) fail "CM_REQUIRE_POLYFORK_LOCAL must be 0 or 1" ;;
esac
export GIT_NO_REPLACE_OBJECTS=1
tracked=$(git ls-files "$model_root/*.fbx" "$model_root/*.fbx.meta")
[ -z "$tracked" ] || fail "licensed FBX derivatives or metadata are tracked"
grep -q 'fetch-depth: 0' .github/workflows/ci.yml \
  || fail "CI must fetch full history before evaluating public custody"
grep -q '^permissions:$' .github/workflows/ci.yml \
  && grep -q '^  contents: read$' .github/workflows/ci.yml \
  || fail "CI must run with read-only repository permissions"
grep -q 'actions/checkout@fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09' \
  .github/workflows/ci.yml \
  || fail "CI checkout must pin the reviewed v5.1.0 commit"
grep -q 'persist-credentials: false' .github/workflows/ci.yml \
  || fail "CI checkout must not persist its GitHub credential"
grep -qx 'unity/.utmp/' .gitignore \
  || fail "Unity's generated .utmp cache must be ignored"

python3 - "$model_root" "$custody_profile" <<'PY'
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys


def fail(message: str) -> None:
    print(f"polyfork-custody.test.sh: FAIL — {message}")
    raise SystemExit(1)


def require_owner_private(path: Path, expected_mode: int, label: str) -> None:
    metadata = path.lstat()
    if metadata.st_uid != os.geteuid():
        fail(f"{label} must be owned by the current licensed user")
    if stat.S_IMODE(metadata.st_mode) != expected_mode:
        fail(f"{label} must have mode {expected_mode:04o}")
    if sys.platform == "darwin":
        acl = subprocess.run(
            ["ls", "-lde", str(path)],
            check=True,
            capture_output=True,
            text=True,
        ).stdout.splitlines()
        if len(acl) != 1 or "+" in acl[0].split()[0]:
            fail(f"{label} must not grant access through an extended ACL")


def require_time_machine_excluded(path: Path, label: str) -> None:
    tmutil = shutil.which("tmutil")
    if tmutil is None:
        fail("licensed-local custody requires the macOS Time Machine exclusion verifier")
    result = subprocess.run(
        [tmutil, "isexcluded", str(path)],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.lstrip()
    if not result.startswith("[Excluded]"):
        fail(f"{label} must be excluded from Time Machine backups")


root = Path.cwd()
model_root = root / sys.argv[1]
require_local = sys.argv[2] == "1"
if require_local and sys.platform != "darwin":
    fail("licensed-local custody currently requires the ACL-checked macOS owner host")
provenance = root / "unity/Assets/Art/Polyfork/PROVENANCE.md"
authoring = root / "unity/Assets/Art/Polyfork/Editor/CatMetroDioramaAuthoring.cs"
prefab_root = root / "unity/Assets/Prefabs/Diorama"
verifier = root / "unity/Assets/Editor/PolyforkLocalCustody.cs"
unity_test_driver = root / "scripts/run-unity-editmode.sh"
test_harness = root / "scripts/test.sh"
if not unity_test_driver.is_file():
    fail("canonical licensed-local Unity test driver is missing")
unity_test_text = unity_test_driver.read_text(encoding="utf-8")
test_harness_text = test_harness.read_text(encoding="utf-8")
if "umask 077" not in unity_test_text:
    fail("Unity test wrapper must keep owner-local import caches private")
if "CM_REQUIRE_POLYFORK_LOCAL=1" not in unity_test_text or "polyfork-custody.test.sh" not in unity_test_text:
    fail("Unity test wrapper must run strict custody before opening the Editor")
for token in (
    "${CM_UNITY_EDITOR+x}",
    "CM_UNITY_EDITOR overrides are forbidden",
    "verify-unity-editor.sh",
    "licensed-local profile requires the pinned Unity editor",
    "${POLYFORK_KEY+x}",
    "refusing inherited POLYFORK_KEY",
):
    if token not in unity_test_text:
        fail(f"Unity test wrapper lacks fail-closed profile control: {token}")
if "unity/.utmp" not in unity_test_text:
    fail("Unity test wrapper must precreate the private .utmp cache")
for token in (
    "tests/unity/editmode.test.sh",
    "scripts/run-unity-editmode.sh",
    "${POLYFORK_KEY+x}",
    "${CM_UNITY_EDITOR+x}",
    "unity_profile=",
    "unset CM_REQUIRE_POLYFORK_LOCAL",
):
    if token not in test_harness_text:
        fail(f"canonical test harness lacks the custody route: {token}")
if test_harness_text.count("tests/unity/editmode.test.sh") != 1:
    fail("canonical test harness must intercept the immutable Unity verifier exactly once")
if test_harness_text.count("scripts/run-unity-editmode.sh") != 1:
    fail("canonical test harness must dispatch through exactly one custody-aware driver")
for exact in (
    'unity_profile="${CM_REQUIRE_POLYFORK_LOCAL:-0}"',
    "unset CM_REQUIRE_POLYFORK_LOCAL",
    'CM_REQUIRE_POLYFORK_LOCAL="$unity_profile" bash scripts/run-unity-editmode.sh',
):
    if test_harness_text.count(exact) != 1:
        fail(f"canonical test harness must pin strict-profile routing exactly once: {exact}")
if not (
    test_harness_text.index('unity_profile="${CM_REQUIRE_POLYFORK_LOCAL:-0}"')
    < test_harness_text.index("unset CM_REQUIRE_POLYFORK_LOCAL")
    < test_harness_text.index("while IFS= read -r t")
    < test_harness_text.index(
        'CM_REQUIRE_POLYFORK_LOCAL="$unity_profile" bash scripts/run-unity-editmode.sh'
    )
):
    fail("canonical test harness must capture, unset, then forward strict profile only to Unity")
for token in (
    "grep -Fxc '# --- editor half ---'",
    "# --- editor half ---$/,$d",
    "clean-public",
):
    if token not in unity_test_text:
        fail(f"Unity test driver lacks fail-closed clean-public projection: {token}")
editor_verify = unity_test_text.index("verify-unity-editor.sh")
strict_custody = unity_test_text.index("CM_REQUIRE_POLYFORK_LOCAL=1")
cache_create = unity_test_text.index("for local_cache in")
raw_unity = unity_test_text.index('bash "$raw_wrapper"')
if not editor_verify < strict_custody < cache_create < raw_unity:
    fail("Unity test driver must authenticate, verify custody, secure caches, then run Unity")
auto_strict = 'if [ "$custody_profile" = "1" ] || [ -n "$local_pack_entry" ]; then'
if unity_test_text.count(auto_strict) != 1:
    fail("Unity test driver must select strict mode for an explicit profile or any local entry")
for token in ("git merge-base", "git diff --quiet", "immutable verifier differs"):
    if token not in unity_test_text:
        fail(f"Unity test driver does not independently protect the frozen verifier: {token}")
if unity_test_text.index("git diff --quiet") > editor_verify:
    fail("frozen-verifier integrity must be checked before editor authentication")
if unity_test_text.index("${POLYFORK_KEY+x}") > unity_test_text.index(
    "reject-git-redirect-env.sh"
):
    fail("Unity test driver must reject acquisition credentials before its first subprocess")
if test_harness_text.index("${POLYFORK_KEY+x}") > test_harness_text.index(
    "reject-git-redirect-env.sh"
):
    fail("canonical test harness must reject acquisition credentials before test subprocesses")
build_text = (root / "scripts/build.sh").read_text(encoding="utf-8")
if "unity/.utmp" not in build_text:
    fail("Android build wrapper must precreate the private .utmp cache")
editor_settings = (root / "unity/ProjectSettings/EditorSettings.asset").read_text(
    encoding="utf-8"
)
for setting in (
    "m_CacheServerMode: 2",
    "m_CacheServerEnableDownload: 0",
    "m_CacheServerEnableUpload: 0",
):
    if editor_settings.count(setting) != 1:
        fail(f"shared Unity Accelerator must remain disabled: {setting}")

shallow = subprocess.run(
    ["git", "rev-parse", "--is-shallow-repository"],
    cwd=root,
    check=True,
    capture_output=True,
    text=True,
).stdout.strip()
if shallow != "false":
    fail("reachable-history custody scan requires a full clone")
replacement_refs = subprocess.run(
    ["git", "for-each-ref", "--format=%(refname)", "refs/replace"],
    cwd=root,
    check=True,
    capture_output=True,
    text=True,
).stdout.strip()
if replacement_refs:
    fail("reachable-history custody scan refuses local Git replace refs")
grafts_text = subprocess.run(
    ["git", "rev-parse", "--git-path", "info/grafts"],
    cwd=root,
    check=True,
    capture_output=True,
    text=True,
).stdout.strip()
grafts = Path(grafts_text)
if not grafts.is_absolute():
    grafts = root / grafts
if grafts.is_file() and grafts.stat().st_size != 0:
    fail("reachable-history custody scan refuses legacy Git grafts")

risky_suffixes = (
    ".fbx", ".fbx.meta", ".glb", ".gltf", ".obj", ".blend", ".dae",
    ".3ds", ".stl", ".ply", ".unitypackage", ".zip", ".7z", ".tar",
    ".tgz", ".gz", ".bz2", ".xz",
)
standalone_magic = (
    b"Kaydara FBX Binary", b"; FBX", b"glTF", b"BLENDER", b"PK\x03\x04",
    b"7z\xbc\xaf\x27\x1c", b"\x1f\x8b", b"BZh", b"ply\n", b"ply\r\n",
)
lfs_magic = b"version https://git-lfs.github.com/spec/v1"
# Any future public standalone 3D/archive payload needs an independently reviewed content-hash
# allowlist entry. The empty default deliberately fails closed for this public repository.
allowed_public_payload_hashes: set[str] = set()

row_pattern = re.compile(
    r"^\| \[[^]]+\]\([^)]+\) \(`(?P<asset_id>[^`]+)`\)"
    r" \| (?P<tris>[0-9,]+)"
    r" \| `(?P<source>[0-9a-f]{64})`"
    r" \| `(?P<name>[^`]+\.fbx)`"
    r" \| `(?P<derivative>[0-9a-f]{64})`"
    r" \| `(?P<guid>[0-9a-f]{32})`"
    r" \| `(?P<meta>[0-9a-f]{64})` \|$"
)
rows = []
for line in provenance.read_text(encoding="utf-8").splitlines():
    match = row_pattern.match(line)
    if match:
        rows.append(match.groupdict())

if len(rows) != 9:
    fail(f"PROVENANCE must contain exactly 9 complete receipt rows, found {len(rows)}")
for key in ("asset_id", "source", "name", "derivative", "guid", "meta"):
    values = [row[key] for row in rows]
    if len(set(values)) != 9:
        fail(f"PROVENANCE {key} values must be unique")

# Inspect history reachable from the candidate and every fetched remote/tag ref, plus the current
# index. Local-only branches and detached sibling-worktree heads remain private object-store state;
# CI fetches public refs and repeats this scan on the candidate merge.
known_payload_hashes = {
    value
    for row in rows
    for value in (row["source"], row["derivative"], row["meta"])
}
object_paths: dict[str, set[str]] = {}

fetched_refs = subprocess.run(
    [
        "git", "for-each-ref", "--format=%(refname)",
        "refs/remotes", "refs/tags", "refs/pull",
    ],
    cwd=root,
    check=True,
    capture_output=True,
    text=True,
).stdout.splitlines()
public_revisions = ["HEAD", *fetched_refs]

raw_history = subprocess.run(
    [
        "git", "log", "--no-color", "--root", "-m", "--pretty=format:",
        "--raw", "--no-abbrev", "--no-renames", *public_revisions,
    ],
    cwd=root,
    check=True,
    capture_output=True,
    text=True,
).stdout.splitlines()
raw_pattern = re.compile(
    r"^:[0-7]{6} [0-7]{6} (?P<old>[0-9a-f]{40}) (?P<new>[0-9a-f]{40}) [A-Z]+\t(?P<path>.+)$"
)
zero_oid = "0" * 40
for line in raw_history:
    match = raw_pattern.match(line)
    if match is None:
        continue
    path = match.group("path")
    for key in ("old", "new"):
        oid = match.group(key)
        if oid != zero_oid:
            object_paths.setdefault(oid, set()).add(path)

indexed = subprocess.run(
    ["git", "ls-files", "--stage", "-z"],
    cwd=root,
    check=True,
    capture_output=True,
).stdout.split(b"\0")
for entry in indexed:
    if not entry:
        continue
    metadata, encoded_path = entry.split(b"\t", 1)
    _mode, encoded_oid, _stage = metadata.split(b" ", 2)
    oid = encoded_oid.decode("ascii")
    path = encoded_path.decode("utf-8", "surrogateescape")
    object_paths.setdefault(oid, set()).add(path)

reachable_oids = {
    line.split(" ", 1)[0]
    for line in subprocess.run(
        ["git", "rev-list", "--objects", *public_revisions],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.splitlines()
}
candidate_oids = sorted(reachable_oids | set(object_paths))
types = subprocess.run(
    ["git", "cat-file", "--batch-check=%(objectname) %(objecttype)"],
    cwd=root,
    check=True,
    input="".join(f"{oid}\n" for oid in candidate_oids),
    capture_output=True,
    text=True,
).stdout.splitlines()
if len(types) != len(candidate_oids):
    fail("git object type scan returned an incomplete response")
for expected_oid, response in zip(candidate_oids, types):
    fields = response.split(" ")
    if len(fields) != 2 or fields[0] != expected_oid or fields[1] not in {
        "blob", "commit", "tag", "tree"
    }:
        fail(f"git object type scan failed closed for {expected_oid}")
blob_oids = [line.split(" ", 1)[0] for line in types if line.endswith(" blob")]

batch = subprocess.Popen(
    ["git", "cat-file", "--batch"],
    cwd=root,
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
)
if batch.stdin is None or batch.stdout is None:
    fail("could not open git object inspection pipes")
try:
    for oid in blob_oids:
        batch.stdin.write((oid + "\n").encode("ascii"))
        batch.stdin.flush()
        header = batch.stdout.readline().decode("ascii").strip().split(" ")
        if len(header) != 3 or header[1] != "blob":
            fail(f"could not inspect reachable/index object {oid}")
        remaining = int(header[2])
        digest = hashlib.sha256()
        prefix = bytearray()
        while remaining:
            chunk = batch.stdout.read(min(65536, remaining))
            if not chunk:
                fail(f"truncated git object while inspecting {oid}")
            if len(prefix) < 512:
                prefix.extend(chunk[: 512 - len(prefix)])
            digest.update(chunk)
            remaining -= len(chunk)
        if batch.stdout.read(1) != b"\n":
            fail(f"malformed git object boundary while inspecting {oid}")

        paths = object_paths.get(oid, {f"object:{oid}"})
        risky_paths = sorted(path for path in paths if path.casefold().endswith(risky_suffixes))
        label = risky_paths[0] if risky_paths else sorted(paths)[0]
        payload_hash = digest.hexdigest()
        if payload_hash in known_payload_hashes:
            fail(f"reachable history or index contains an exact licensed payload: {label}")

        prefix_bytes = bytes(prefix)
        lfs_match = re.search(rb"^oid sha256:([0-9a-f]{64})$", prefix_bytes, re.MULTILINE)
        is_lfs_pointer = prefix_bytes.startswith(lfs_magic + b"\n") and lfs_match is not None
        risky_named = any(path.casefold().endswith(risky_suffixes) for path in paths)
        standalone_bytes = (
            prefix_bytes.startswith(standalone_magic)
            or (len(prefix_bytes) > 262 and prefix_bytes[257:262] == b"ustar")
            or b"<COLLADA" in prefix_bytes[:512]
        )
        if is_lfs_pointer and payload_hash not in allowed_public_payload_hashes:
            fail(f"reachable history or index contains a forbidden Git LFS pointer: {label}")
        if (risky_named or standalone_bytes) and payload_hash not in allowed_public_payload_hashes:
            fail(f"reachable history or index contains a standalone model/archive payload: {label}")
finally:
    batch.stdin.close()
    batch.stdout.close()
    batch_status = batch.wait()
if batch_status != 0:
    fail("git object content scan exited nonzero")

# A rewritten public branch can leave blobs outside the candidate/fetched-ref history in this
# clone's object store. They are a disclosed local residual; if present, the entire common Git
# database must remain owner-private and backup-excluded.
all_object_types = subprocess.run(
    ["git", "cat-file", "--batch-all-objects", "--batch-check=%(objectname) %(objecttype)"],
    cwd=root,
    check=True,
    capture_output=True,
    text=True,
).stdout.splitlines()
all_blob_oids = []
for response in all_object_types:
    fields = response.split(" ")
    if len(fields) != 2 or fields[1] not in {"blob", "commit", "tag", "tree"}:
        fail("local Git object inventory returned an invalid response")
    if fields[1] == "blob":
        all_blob_oids.append(fields[0])

local_stale_payload = False
local_batch = subprocess.Popen(
    ["git", "cat-file", "--batch"],
    cwd=root,
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
)
if local_batch.stdin is None or local_batch.stdout is None:
    fail("could not open local Git object inspection pipes")
try:
    for oid in all_blob_oids:
        local_batch.stdin.write((oid + "\n").encode("ascii"))
        local_batch.stdin.flush()
        header = local_batch.stdout.readline().decode("ascii").strip().split(" ")
        if len(header) != 3 or header[0] != oid or header[1] != "blob":
            fail(f"could not inspect local Git blob {oid}")
        remaining = int(header[2])
        digest = hashlib.sha256()
        while remaining:
            chunk = local_batch.stdout.read(min(65536, remaining))
            if not chunk:
                fail(f"truncated local Git blob while inspecting {oid}")
            digest.update(chunk)
            remaining -= len(chunk)
        if local_batch.stdout.read(1) != b"\n":
            fail(f"malformed local Git blob boundary while inspecting {oid}")
        if digest.hexdigest() in known_payload_hashes:
            local_stale_payload = True
finally:
    local_batch.stdin.close()
    local_batch.stdout.close()
    local_batch_status = local_batch.wait()
if local_batch_status != 0:
    fail("local Git object content scan exited nonzero")

common_git_text = subprocess.run(
    ["git", "rev-parse", "--git-common-dir"],
    cwd=root,
    check=True,
    capture_output=True,
    text=True,
).stdout.strip()
common_git = Path(common_git_text)
if not common_git.is_absolute():
    common_git = root / common_git
alternates = common_git / "objects/info/alternates"
if alternates.is_file() and alternates.stat().st_size != 0:
    fail("custody scan refuses a Git alternate object store")
if local_stale_payload:
    if common_git.is_symlink() or not common_git.is_dir():
        fail("stale licensed Git objects require a private regular common Git directory")
    require_owner_private(common_git, 0o700, "common Git object database")
    if sys.platform == "darwin":
        require_time_machine_excluded(common_git, "common Git object database")

authoring_text = authoring.read_text(encoding="utf-8")
preflight_token = "PolyforkLocalCustody.RequireExact();"
build_start = authoring_text.index("public static void Build()")
capture_start = authoring_text.index("public static void CaptureOrientationSheet()")
build_text = authoring_text[build_start:capture_start]
capture_text = authoring_text[capture_start:]
first_mutation_token = 'EnsureFolder("Assets/Art", "Materials");'
if preflight_token not in build_text:
    fail("diorama authoring does not invoke the cryptographic custody verifier")
if build_text.index(preflight_token) > build_text.index(first_mutation_token):
    fail("diorama authoring custody verification must run before asset mutation")
if preflight_token not in capture_text:
    fail("orientation capture does not invoke the cryptographic custody verifier")
if capture_text.index(preflight_token) > capture_text.index("EditorSceneManager.NewScene"):
    fail("orientation capture custody verification must run before replacing the scene")
if capture_text.index("if (prefabs[row] == null)") > capture_text.index("EditorSceneManager.NewScene"):
    fail("orientation capture must resolve every prefab before replacing the scene")
if authoring_text.count(preflight_token) != 2:
    fail("the two Polyfork authoring entry points must each invoke custody exactly once")
if "RequirePolyforkLocalCustody" in authoring_text:
    fail("diorama authoring retains the obsolete existence-only custody preflight")
if not verifier.is_file():
    fail("shared cryptographic local-custody verifier is missing")
verifier_text = verifier.read_text(encoding="utf-8")
if "public static void RequireExactAt(string modelRoot)" not in verifier_text:
    fail("compiled custody verifier has no behavioral test seam")
for row in rows:
    if authoring_text.count(f'new Dressing("{row["name"]}"') != 1:
        fail(f"authoring manifest must name {row['name']} exactly once")
    for key in ("name", "derivative", "guid", "meta"):
        value = row[key]
        if verifier_text.count(value) != 1:
            fail(f"compiled custody verifier must pin receipt {key} {value} exactly once")

prefabs = sorted(prefab_root.glob("Polyfork_*.prefab"))
if len(prefabs) != 9:
    fail(f"expected 9 Cat-Metro Polyfork prefabs, found {len(prefabs)}")
prefab_contents = {path: path.read_text(encoding="utf-8") for path in prefabs}
prefab_text = "\n".join(prefab_contents.values())
for forbidden in ("Mesh:", "m_VertexData:", "m_IndexBuffer:", "m_CompressedMesh:", "_typelessdata:"):
    if forbidden in prefab_text:
        fail(f"Cat-Metro prefabs embed mesh payload token {forbidden}")
for row in rows:
    referencing_prefabs = [
        path for path, contents in prefab_contents.items()
        if re.search(rf"guid: {re.escape(row['guid'])}\b", contents)
    ]
    if len(referencing_prefabs) != 1:
        fail(
            f"receipt GUID {row['guid']} must belong to exactly one Cat-Metro prefab, "
            f"found {len(referencing_prefabs)}"
        )

expected_fbx = {model_root / row["name"] for row in rows}
expected_meta = {Path(str(path) + ".meta") for path in expected_fbx}
if model_root.is_symlink():
    fail("local custody directory must not be a symlink")
actual_entries = set(model_root.iterdir()) if model_root.is_dir() else set()
actual_fbx = {path for path in actual_entries if path.name.casefold().endswith(".fbx")}
actual_meta = {path for path in actual_entries if path.name.casefold().endswith(".fbx.meta")}

unexpected = actual_entries - expected_fbx - expected_meta
if unexpected:
    fail("unexpected local entry exists in the custody directory")
present = actual_fbx | actual_meta
expected = expected_fbx | expected_meta
if present and present != expected:
    fail("local custody must contain either zero files or all 9 FBX/meta pairs")
if require_local and present != expected:
    fail("CM_REQUIRE_POLYFORK_LOCAL=1 requires all 9 FBX/meta pairs")
if os.name == "posix":
    cache_paths = [
        (root / "unity/Library", "unity/Library"),
        (root / "unity/Temp", "unity/Temp"),
        (root / "unity/Logs", "unity/Logs"),
        (root / "unity/.utmp", "unity/.utmp"),
    ]
    project_cache_present = any(
        cache.exists() or cache.is_symlink() for cache, _label in cache_paths
    )
    needs_owner_cache_boundary = bool(present) or local_stale_payload or project_cache_present
    if sys.platform == "darwin" and needs_owner_cache_boundary:
        require_time_machine_excluded(root / "unity", "Unity worktree parent")
        require_time_machine_excluded(
            Path.home() / "Library/Caches", "macOS cache parent"
        )
        cache_paths.append((
            Path.home() / "Library/Caches/com.unity3d.UnityEditor",
            "global Unity cache root",
        ))
    for cache, relative_cache in cache_paths:
        if cache.exists() or cache.is_symlink():
            if cache.is_symlink() or not cache.is_dir():
                fail(f"owner-local Unity cache must be a private directory: {relative_cache}")
            require_owner_private(cache, 0o700, relative_cache)
            if sys.platform == "darwin":
                require_time_machine_excluded(cache, relative_cache)
    if present:
        require_owner_private(model_root, 0o700, "local custody directory")
        if sys.platform == "darwin":
            require_time_machine_excluded(model_root, "local custody directory")

for row in rows:
    fbx = model_root / row["name"]
    meta = Path(str(fbx) + ".meta")
    for path in (fbx, meta):
        result = subprocess.run(
            ["git", "check-ignore", "--no-index", "--quiet", str(path.relative_to(root))],
            cwd=root,
            check=False,
        )
        if result.returncode != 0:
            fail(f"{path.relative_to(root)} is not protected by gitignore")
    if not present:
        continue
    if fbx.is_symlink() or meta.is_symlink() or not fbx.is_file() or not meta.is_file():
        fail(f"local custody requires regular, non-symlink files for {row['name']}")
    if os.name == "posix":
        require_owner_private(fbx, 0o600, f"local derivative {row['name']}")
        require_owner_private(meta, 0o600, f"local metadata {row['name']}")
    if hashlib.sha256(fbx.read_bytes()).hexdigest() != row["derivative"]:
        fail(f"local derivative hash mismatch for {row['name']}")
    if hashlib.sha256(meta.read_bytes()).hexdigest() != row["meta"]:
        fail(f"local metadata hash mismatch for {row['name']}")
    guid_match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8"), re.MULTILINE)
    if guid_match is None or guid_match.group(1) != row["guid"]:
        fail(f"local metadata GUID mismatch for {row['name']}")

profile = "hydrated-local" if present else "no-local-pack"
print(f"polyfork-custody.test.sh: OK ({profile}; 9 receipts; no tracked derivatives)")
PY
