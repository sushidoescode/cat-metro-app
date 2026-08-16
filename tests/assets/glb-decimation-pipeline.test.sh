#!/usr/bin/env bash
# Behavioral contract for the offline GLB decimation orchestrator. The fake
# process boundary is validated independently before any production entry point
# runs, so a later RED is attributable to orchestrator behavior.
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)
decimate_script=${DECIMATE_SCRIPT:-$repo/scripts/decimate-assets.py}
fake_blender="$repo/tests/assets/fake_blender.py"
expected_driver=$(cd "$(dirname "$decimate_script")" && pwd -P)/blender_decimate.py
review_section=${GLB_DECIMATION_REVIEW_SECTION:-all}
case "$review_section" in
  all|A|B|C) ;;
  *) die_message="GLB_DECIMATION_REVIEW_SECTION must be all, A, B, or C"
     printf 'glb-decimation pipeline test: %s\n' "$die_message" >&2
     exit 2 ;;
esac
tmp=$(mktemp -d)
marker_name="$(basename "$tmp")-argv-injection-marker"
marker="$repo/$marker_name"
marker_cleanup_armed=0

cleanup() {
  rm -rf -- "$tmp"
  if [ "$marker_cleanup_armed" -eq 1 ]; then
    rm -f -- "$marker"
  fi
}
trap cleanup EXIT

die() {
  printf 'glb-decimation pipeline test: %s\n' "$1" >&2
  exit 1
}

test ! -e "$marker" || die "shell-evaluation marker already exists"
marker_cleanup_armed=1
cd "$repo"

mkdir -p "$tmp/bin"
# shellcheck disable=SC2016 # sentinel variables expand when the generated script runs
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'set -euo pipefail' \
  ': "${CURL_SENTINEL_LOG:?}"' \
  'printf "curl called\\n" >>"$CURL_SENTINEL_LOG"' \
  'exit 91' \
  >"$tmp/bin/curl"
chmod +x "$tmp/bin/curl"
export CURL_SENTINEL_LOG="$tmp/curl-called.log"
export PATH="$tmp/bin:$PATH"

assert_no_external_effects() {
  test ! -e "$CURL_SENTINEL_LOG" || die "curl sentinel was called"
  test ! -e "$marker" || die "a metacharacter path was evaluated by a shell"
}

sha256_file() {
  PYTHONDONTWRITEBYTECODE=1 python3 - "$1" <<'PY'
import hashlib
import sys
from pathlib import Path

print(hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest())
PY
}

magic_hex() {
  PYTHONDONTWRITEBYTECODE=1 python3 - "$1" <<'PY'
import sys
from pathlib import Path

print(Path(sys.argv[1]).read_bytes()[:4].hex())
PY
}

write_fixture() {
  PYTHONDONTWRITEBYTECODE=1 python3 "$repo/tests/assets/glb_fixture.py" "$@"
}

write_sidecar() {
  local source=$1
  local service=$2
  local prompt=$3
  local tier=${4:-paid}
  local claimed_sha=${5:-}
  if [ -z "$claimed_sha" ]; then
    claimed_sha=$(sha256_file "$source")
  fi
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$source.json" "$service" "$prompt" "$tier" "$claimed_sha" <<'PY'
import json
import re
import sys
from pathlib import Path

path, service, prompt, tier, claimed_sha = sys.argv[1:]
record = {
    "service": service,
    "task_id": f"fixture-{service}-task",
    "timestamp_utc": "2026-08-15T12:34:56Z",
    "plan_tier": tier,
    "prompt": prompt,
    "note": "local paid fixture",
    "sha256": claimed_sha,
}
Path(path).write_text(json.dumps(record, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY
}

write_single_manifest() {
  local path=$1
  local asset_id=$2
  local kind=$3
  local service=$4
  local output=$5
  local prompt=$6
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$path" "$asset_id" "$kind" "$service" "$output" "$prompt" <<'PY'
import json
import sys
from pathlib import Path

path, asset_id, kind, service, output, prompt = sys.argv[1:]
document = {
    "assets": [{
        "id": asset_id,
        "kind": kind,
        "service": service,
        "out": output,
        "prompt": prompt,
    }]
}
Path(path).write_text(json.dumps(document, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY
}

write_happy_manifest() {
  local path=$1
  PYTHONDONTWRITEBYTECODE=1 python3 - "$path" <<'PY'
import json
import sys
from pathlib import Path

document = {
    "assets": [
        {
            "id": "fixture-cat",
            "kind": "cat",
            "service": "meshy",
            "out": "cat-source.glb",
            "prompt": "round fixture cat",
        },
        {
            "id": "fixture-prop",
            "kind": "prop",
            "service": "tripo",
            "out": "prop-source.glb",
            "prompt": "rounded fixture prop",
        },
    ]
}
Path(sys.argv[1]).write_text(
    json.dumps(document, indent=2, sort_keys=True) + "\n",
    encoding="utf-8",
)
PY
}

fingerprint_tree() {
  PYTHONDONTWRITEBYTECODE=1 python3 - "$1" <<'PY'
import hashlib
import json
import os
import sys
from pathlib import Path

root = Path(sys.argv[1])
records = []
for directory, names, filenames in os.walk(root, followlinks=False):
    directory_path = Path(directory)
    for name in sorted(names + filenames):
        path = directory_path / name
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            records.append([relative, "symlink", os.readlink(path)])
        elif path.is_dir():
            records.append([relative, "directory"])
        else:
            records.append([
                relative,
                "file",
                hashlib.sha256(path.read_bytes()).hexdigest(),
            ])
print(json.dumps(records, separators=(",", ":")))
PY
}

line_count() {
  if [ -f "$1" ]; then
    wc -l <"$1" | tr -d ' '
  else
    printf '0\n'
  fi
}

run_decimator() {
  local mode=$1
  local log=$2
  local stdout=$3
  local stderr=$4
  shift 4
  env \
    FAKE_BLENDER_MODE="$mode" \
    FAKE_BLENDER_LOG="$log" \
    FAKE_BLENDER_AUDIT="$log.audit" \
    FAKE_BLENDER_VERSION="${CASE_BLENDER_VERSION:-5.1.2}" \
    FAKE_BLENDER_BUILD_HASH="${CASE_BLENDER_BUILD_HASH:-ec6e62d40fa9}" \
    PIPELINE_SENTINEL_KEY="environment-sentinel-1" \
    PIPELINE_SENTINEL_TOKEN="environment-sentinel-2" \
    PIPELINE_SENTINEL_SECRET="environment-sentinel-3" \
    PIPELINE_SENTINEL_AUTH="environment-sentinel-4" \
    PIPELINE_SENTINEL_CREDENTIAL="environment-sentinel-5" \
    PIPELINE_SENTINEL_BEARER="environment-sentinel-6" \
    PYTHONDONTWRITEBYTECODE=1 \
    python3 "$decimate_script" \
      --manifest "$manifest" \
      --input-dir "$input_dir" \
      --output-dir "$output_dir" \
      --blender "$fake_blender" \
      "$@" \
      >"$stdout" 2>"$stderr"
}

assert_exact_provenance() {
  local source=$1
  local final=$2
  local proof=$3
  local category=$4
  local target=$5
  local minimum=$6
  local service=$7
  local prompt=$8
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$repo/scripts" "$source" "$final" "$proof" \
    "$category" "$target" "$minimum" "$service" "$prompt" <<'PY'
import hashlib
import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import compare_preservation, inspect_glb

source = Path(sys.argv[2])
final = Path(sys.argv[3])
proof_path = Path(sys.argv[4])
category = sys.argv[5]
target = int(sys.argv[6])
minimum = int(sys.argv[7])
service = sys.argv[8]
prompt = sys.argv[9]
source_sidecar_path = Path(f"{source}.json")

source_metrics = inspect_glb(source)
output_metrics = inspect_glb(final)
source_sidecar = json.loads(source_sidecar_path.read_text(encoding="utf-8"))
record = json.loads(proof_path.read_text(encoding="utf-8"))
metric_names = (
    "triangles", "vertices", "primitives", "materials",
    "material_primitives", "images", "embedded_images", "uv_primitives",
    "animations", "cameras", "lights", "skins", "morph_targets",
    "extensions_used", "extensions_required", "world_bounds",
)

assert set(record) == {"schema_version", "source", "derivative", "tool", "geometry"}
assert record["schema_version"] == 1
assert set(record["source"]) == {"filename", "sha256", "sidecar_sha256", "provenance"}
assert record["source"]["filename"] == source.name
assert record["source"]["sha256"] == hashlib.sha256(source.read_bytes()).hexdigest()
assert record["source"]["sidecar_sha256"] == hashlib.sha256(source_sidecar_path.read_bytes()).hexdigest()
assert record["source"]["provenance"] == {
    key: source_sidecar[key]
    for key in sorted({"service", "task_id", "timestamp_utc", "plan_tier", "prompt", "note"})
}
assert source_sidecar["service"] == service
assert source_sidecar["prompt"] == prompt
assert source_sidecar["plan_tier"] == "paid"

assert record["derivative"] == {
    "filename": final.name,
    "sha256": hashlib.sha256(final.read_bytes()).hexdigest(),
}
assert set(record["tool"]) == {"name", "version", "build_hash", "operation", "timestamp_utc"}
assert record["tool"]["name"] == "Blender"
assert record["tool"]["version"] == "5.1.2"
assert record["tool"]["build_hash"] == "ec6e62d40fa9"
assert record["tool"]["operation"] == "collapse-decimate"
assert re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", record["tool"]["timestamp_utc"])

geometry = record["geometry"]
assert set(geometry) == {
    "category", "target_triangles", "accepted_minimum",
    "accepted_maximum", "source", "output",
}
assert geometry["category"] == category
assert geometry["target_triangles"] == target
assert geometry["accepted_minimum"] == minimum
assert geometry["accepted_maximum"] == target
assert geometry["source"] == {name: source_metrics[name] for name in metric_names}
assert geometry["output"] == {name: output_metrics[name] for name in metric_names}
assert minimum <= output_metrics["triangles"] <= target
assert 5000 <= output_metrics["triangles"] <= 20000
assert compare_preservation(source_metrics, output_metrics) == []

forbidden = re.compile(
    r"api[_-]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)
def scan(value):
    if isinstance(value, dict):
        for key, child in value.items():
            assert not forbidden.search(str(key)), key
            scan(child)
    elif isinstance(value, list):
        for child in value:
            scan(child)
    elif isinstance(value, str):
        assert not forbidden.search(value), value

scan(record)
PY
}

# Validate syntax without generating bytecode, then exercise every fake mode
# from a foreign working directory. This proves the fixture import is relative
# to __file__, the version/build surface is exact, and each negative mode is a
# real, distinguishable output before the orchestrator can be blamed.
PYTHONDONTWRITEBYTECODE=1 python3 - "$fake_blender" "$repo/tests/assets/glb_fixture.py" <<'PY'
import sys
from pathlib import Path

for filename in sys.argv[1:]:
    compile(Path(filename).read_bytes(), filename, "exec")
PY
test -x "$fake_blender" || die "fake Blender is not executable"

version_audit="$tmp/version.audit"
version_output=$(
  FAKE_BLENDER_AUDIT="$version_audit" \
    PYTHONDONTWRITEBYTECODE=1 "$fake_blender" --background --version
)
test "$version_output" = $'Blender 5.1.2\nbuild hash: ec6e62d40fa9' || \
  die "fake Blender version surface is wrong"
test "$(cat "$version_audit")" = version || \
  die "fake Blender did not safely audit its version phase"
wrong_version_output=$(
  FAKE_BLENDER_VERSION=5.2.0 FAKE_BLENDER_BUILD_HASH=wrong \
    PYTHONDONTWRITEBYTECODE=1 "$fake_blender" --background --version
)
test "$wrong_version_output" = $'Blender 5.2.0\nbuild hash: wrong' || \
  die "fake Blender version overrides are wrong"

forbidden_audit="$tmp/forbidden-environment.audit"
forbidden_log="$tmp/forbidden-environment.log"
sentinel_names=(
  PIPELINE_SENTINEL_KEY PIPELINE_SENTINEL_TOKEN PIPELINE_SENTINEL_SECRET
  PIPELINE_SENTINEL_AUTH PIPELINE_SENTINEL_CREDENTIAL PIPELINE_SENTINEL_BEARER
)
sentinel_number=0
for sentinel_name in "${sentinel_names[@]}"; do
  sentinel_number=$((sentinel_number + 1))
  sentinel_value="environment-probe-$sentinel_number"
  for phase in version asset; do
    before_audit=$(line_count "$forbidden_audit")
    before_log=$(line_count "$forbidden_log")
    set +e
    if [ "$phase" = version ]; then
      env "$sentinel_name=$sentinel_value" \
        FAKE_BLENDER_AUDIT="$forbidden_audit" \
        FAKE_BLENDER_LOG="$forbidden_log" \
        PYTHONDONTWRITEBYTECODE=1 \
        "$fake_blender" --background --version \
        >"$tmp/forbidden.stdout" 2>"$tmp/forbidden.stderr"
    else
      env "$sentinel_name=$sentinel_value" \
        FAKE_BLENDER_AUDIT="$forbidden_audit" \
        FAKE_BLENDER_LOG="$forbidden_log" \
        PYTHONDONTWRITEBYTECODE=1 \
        "$fake_blender" --background --factory-startup -- \
        >"$tmp/forbidden.stdout" 2>"$tmp/forbidden.stderr"
    fi
    forbidden_rc=$?
    set -e
    test "$forbidden_rc" -eq 86 || \
      die "fake Blender accepted $sentinel_name on its $phase phase"
    rg -q '^fake-blender: forbidden environment sentinel present$' \
      "$tmp/forbidden.stderr" || \
      die "fake Blender lacked its safe environment rejection"
    if rg -Fq "$sentinel_value" "$tmp/forbidden.stdout" "$tmp/forbidden.stderr"; then
      die "fake Blender logged a forbidden environment value"
    fi
    test "$(line_count "$forbidden_audit")" -eq "$before_audit" || \
      die "fake Blender audited a rejected environment"
    test "$(line_count "$forbidden_log")" -eq "$before_log" || \
      die "fake Blender logged a rejected environment"
  done
done

preflight="$tmp/fake-preflight"
mkdir -p "$preflight/caller"
write_fixture "$preflight/source.glb" --triangles 20000
preflight_log="$preflight/fake.log"
fake_modes=(
  success over_budget under_budget malformed_output missing_uv
  missing_material missing_image bounds_drift external_image
  unsupported_extension unexpected_scene_content fail
)
for mode in "${fake_modes[@]}"; do
  output="$preflight/$mode.glb"
  set +e
  (
    cd "$preflight/caller"
    FAKE_BLENDER_MODE="$mode" \
      FAKE_BLENDER_LOG="$preflight_log" \
      PYTHONDONTWRITEBYTECODE=1 \
      "$fake_blender" \
        --background --factory-startup --offline-mode --disable-autoexec \
        --threads 1 --python-exit-code 97 --python "$expected_driver" -- \
        --source "$preflight/source.glb" \
        --output "$output" \
        --source-triangles 20000 \
        --target-triangles 10000 \
        --minimum-triangles 9000 \
        --maximum-triangles 10000
  )
  fake_rc=$?
  set -e
  if [ "$mode" = fail ]; then
    test "$fake_rc" -eq 17 || die "fake Blender fail mode did not exit 17"
  else
    test "$fake_rc" -eq 0 || die "fake Blender mode $mode failed setup"
  fi
done

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts" "$preflight" "$preflight_log" "$fake_blender" "$expected_driver" <<'PY'
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import inspect_glb

root = Path(sys.argv[2])
records = [json.loads(line) for line in Path(sys.argv[3]).read_text(encoding="utf-8").splitlines()]
fake = Path(sys.argv[4]).resolve()
driver = Path(sys.argv[5]).resolve()
modes = [
    "success", "over_budget", "under_budget", "malformed_output",
    "missing_uv", "missing_material", "missing_image", "bounds_drift",
    "external_image", "unsupported_extension", "unexpected_scene_content",
    "fail",
]
assert len(records) == len(modes)
for record, mode in zip(records, modes):
    argv = record["argv"]
    assert Path(argv[0]).resolve() == fake
    assert "--" in argv
    assert Path(argv[argv.index("--python") + 1]).resolve() == driver
    assert record["target"] == 10000
    post = argv[argv.index("--") + 1:]
    assert post[0::2] == [
        "--source", "--output", "--source-triangles", "--target-triangles",
        "--minimum-triangles", "--maximum-triangles",
    ]

assert not (root / "fail.glb").exists()
assert (root / "malformed_output.glb").read_bytes() == b"not glTF"
metrics = {
    mode: inspect_glb(root / f"{mode}.glb")
    for mode in modes
    if mode not in {"fail", "malformed_output"}
}
assert metrics["success"]["triangles"] == 10000
assert metrics["over_budget"]["triangles"] == 10001
assert metrics["under_budget"]["triangles"] == 7999
assert metrics["missing_uv"]["uv_primitives"] == 0
assert metrics["missing_material"]["materials"] == 0
assert metrics["missing_material"]["material_primitives"] == 0
assert metrics["missing_image"]["images"] == 0
assert metrics["missing_image"]["embedded_images"] == 0
assert metrics["bounds_drift"]["world_bounds"] == {
    "min": [99.0, -1.0, -1.0], "max": [101.0, 1.0, 1.0]
}
assert metrics["external_image"]["external_uris"] == ["fixture-external.png"]
assert metrics["unsupported_extension"]["extensions_used"] == ["VENDOR_unreviewed"]
scene = metrics["unexpected_scene_content"]
assert [scene[name] for name in ("animations", "cameras", "lights", "skins", "morph_targets")] == [1, 1, 1, 1, 1]
PY
assert_no_external_effects

# Review regression A: filesystem identity is stronger than path spelling.
# These cases exercise the real orchestration entry point, but require every
# alias to be rejected before even the fake Blender version probe.
if [ "$review_section" = all ] || [ "$review_section" = A ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-identity" "$repo" "$fake_blender" <<'PY'
import contextlib
import hashlib
import importlib.util
import io
import json
import os
import re
import sys
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from glb_fixture import write_glb

spec = importlib.util.spec_from_file_location("decimate_assets_identity_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

errors = []

def check(condition, message):
    if not condition:
        errors.append(message)

def digest_bytes(value):
    return hashlib.sha256(value).hexdigest()

def snapshot(root_path):
    records = []
    for path in sorted(root_path.rglob("*"), key=lambda item: item.as_posix()):
        relative = path.relative_to(root_path).as_posix()
        if path.is_symlink():
            records.append((relative, "symlink", os.readlink(path)))
        elif path.is_dir():
            records.append((relative, "directory"))
        else:
            data = path.read_bytes()
            records.append((relative, "file", digest_bytes(data), data[:4].hex()))
    return records

def write_sidecar(source, service, prompt):
    path = Path(f"{source}.json")
    path.write_text(json.dumps({
        "service": service,
        "task_id": f"fixture-{service}-task",
        "timestamp_utc": "2026-08-15T12:34:56Z",
        "plan_tier": "paid",
        "prompt": prompt,
        "note": "local paid fixture",
        "sha256": digest_bytes(source.read_bytes()),
    }, sort_keys=True) + "\n", encoding="utf-8")
    return path

def write_manifest(path, entries):
    path.write_text(
        json.dumps({"assets": entries}, sort_keys=True) + "\n",
        encoding="utf-8",
    )

def line_count(path):
    if not path.exists():
        return 0
    return len(path.read_text(encoding="utf-8").splitlines())

def run_case(case_root, input_dir, output_dir, manifest, *, force=False):
    fake_log = case_root / "fake.log"
    fake_audit = case_root / "fake.audit"
    environment = {
        "FAKE_BLENDER_MODE": "success",
        "FAKE_BLENDER_LOG": str(fake_log),
        "FAKE_BLENDER_AUDIT": str(fake_audit),
        "PIPELINE_SENTINEL_KEY": "identity-sentinel-1",
        "PIPELINE_SENTINEL_TOKEN": "identity-sentinel-2",
        "PIPELINE_SENTINEL_SECRET": "identity-sentinel-3",
        "PIPELINE_SENTINEL_AUTH": "identity-sentinel-4",
        "PIPELINE_SENTINEL_CREDENTIAL": "identity-sentinel-5",
        "PIPELINE_SENTINEL_BEARER": "identity-sentinel-6",
        "PYTHONDONTWRITEBYTECODE": "1",
    }
    arguments = [
        "--manifest", str(manifest),
        "--input-dir", str(input_dir),
        "--output-dir", str(output_dir),
        "--blender", str(fake_blender),
    ]
    if force:
        arguments.append("--force")
    stdout = io.StringIO()
    stderr = io.StringIO()
    with (
        mock.patch.dict(os.environ, environment, clear=False),
        contextlib.redirect_stdout(stdout),
        contextlib.redirect_stderr(stderr),
    ):
        result = module.main(arguments)
    return result, stdout.getvalue() + stderr.getvalue(), fake_log, fake_audit

def require_pre_fake_rejection(label, result, output, fake_log, fake_audit, pattern):
    check(result != 0, f"{label}: aliased filesystem identity was accepted")
    check(
        re.search(pattern, output, re.IGNORECASE) is not None,
        f"{label}: missing alias diagnostic; output={output!r}",
    )
    check(line_count(fake_log) == 0, f"{label}: fake asset invocation was reached")
    check(line_count(fake_audit) == 0, f"{label}: fake version/asset invocation was reached")

def require_no_transaction_residue(label, tree):
    residue = [
        path.relative_to(tree).as_posix()
        for path in tree.rglob("*")
        if path.name.startswith(".glb-decimation-") or ".backup-" in path.name
    ]
    check(not residue, f"{label}: transaction residue remains: {residue}")

# A1: each forced final member aliases the corresponding source inode.
case_root = root / "force-destination-hardlinks"
input_dir = case_root / "input"
output_dir = case_root / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
source = input_dir / "asset.glb"
write_glb(source, triangles=30000)
sidecar = write_sidecar(source, "meshy", "identity fixture cat")
final_glb = output_dir / source.name
final_json = output_dir / sidecar.name
os.link(source, final_glb)
os.link(sidecar, final_json)
assert os.path.samefile(source, final_glb)
assert os.path.samefile(sidecar, final_json)
manifest = case_root / "manifest.json"
write_manifest(manifest, [{
    "id": "identity-cat", "kind": "cat", "service": "meshy",
    "out": source.name, "prompt": "identity fixture cat",
}])
input_before = snapshot(input_dir)
output_before = snapshot(output_dir)
source_bytes = source.read_bytes()
sidecar_bytes = sidecar.read_bytes()
source_hash = digest_bytes(source_bytes)
sidecar_hash = digest_bytes(sidecar_bytes)
result, output, fake_log, fake_audit = run_case(
    case_root, input_dir, output_dir, manifest, force=True
)
require_pre_fake_rejection(
    "force destination hardlinks", result, output, fake_log, fake_audit,
    r"alias|hard.?link|same (?:file|identity|inode)|filesystem identity",
)
check(snapshot(input_dir) == input_before, "force destination hardlinks: source tree changed")
check(snapshot(output_dir) == output_before, "force destination hardlinks: initial output pair changed")
check(source.read_bytes() == source_bytes, "force destination hardlinks: source GLB bytes changed")
check(sidecar.read_bytes() == sidecar_bytes, "force destination hardlinks: source sidecar bytes changed")
check(digest_bytes(source.read_bytes()) == source_hash, "force destination hardlinks: source GLB hash changed")
check(digest_bytes(sidecar.read_bytes()) == sidecar_hash, "force destination hardlinks: source sidecar hash changed")
check(source.read_bytes()[:4] == b"glTF", "force destination hardlinks: source GLB magic changed")
check(
    final_glb.exists() and os.path.samefile(source, final_glb),
    "force destination hardlinks: GLB alias was detached or removed",
)
check(
    final_json.exists() and os.path.samefile(sidecar, final_json),
    "force destination hardlinks: JSON alias was detached or removed",
)
require_no_transaction_residue("force destination hardlinks", case_root)

# A2: two different manifest leaves name the same source GLB inode.
case_root = root / "manifest-source-hardlinks"
input_dir = case_root / "input"
output_dir = case_root / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
source_a = input_dir / "first.glb"
source_b = input_dir / "second.glb"
write_glb(source_a, triangles=30000)
os.link(source_a, source_b)
assert os.path.samefile(source_a, source_b)
sidecar_a = write_sidecar(source_a, "meshy", "shared identity fixture")
sidecar_b = write_sidecar(source_b, "meshy", "shared identity fixture")
assert not os.path.samefile(sidecar_a, sidecar_b)
manifest = case_root / "manifest.json"
write_manifest(manifest, [
    {"id": "identity-first", "kind": "cat", "service": "meshy", "out": source_a.name, "prompt": "shared identity fixture"},
    {"id": "identity-second", "kind": "cat", "service": "meshy", "out": source_b.name, "prompt": "shared identity fixture"},
])
input_before = snapshot(input_dir)
source_a_bytes = source_a.read_bytes()
source_b_bytes = source_b.read_bytes()
source_a_hash = digest_bytes(source_a_bytes)
source_b_hash = digest_bytes(source_b_bytes)
result, output, fake_log, fake_audit = run_case(case_root, input_dir, output_dir, manifest)
require_pre_fake_rejection(
    "manifest source hardlinks", result, output, fake_log, fake_audit,
    r"source paths alias|hard.?link|duplicate source|same (?:file|identity|inode)|filesystem identity",
)
check(snapshot(input_dir) == input_before, "manifest source hardlinks: source tree changed")
check(source_a.read_bytes() == source_a_bytes, "manifest source hardlinks: first source bytes changed")
check(source_b.read_bytes() == source_b_bytes, "manifest source hardlinks: second source bytes changed")
check(digest_bytes(source_a.read_bytes()) == source_a_hash, "manifest source hardlinks: first source hash changed")
check(digest_bytes(source_b.read_bytes()) == source_b_hash, "manifest source hardlinks: second source hash changed")
check(source_a.read_bytes()[:4] == b"glTF", "manifest source hardlinks: first source magic changed")
check(source_b.read_bytes()[:4] == b"glTF", "manifest source hardlinks: second source magic changed")
check(os.path.samefile(source_a, source_b), "manifest source hardlinks: source identity split")
check(list(output_dir.iterdir()) == [], "manifest source hardlinks: partial output was created")
require_no_transaction_residue("manifest source hardlinks", case_root)

# A3: output names that differ only by case must be duplicate/alias-invalid on
# both case-sensitive and case-insensitive filesystems.
case_root = root / "casefold-output-names"
input_dir = case_root / "input"
output_dir = case_root / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
upper = input_dir / "Case.glb"
lower = input_dir / "case.glb"
write_glb(upper, triangles=30000)
write_sidecar(upper, "meshy", "casefold identity fixture")
if not lower.exists():
    write_glb(lower, triangles=30000)
    write_sidecar(lower, "meshy", "casefold identity fixture")
else:
    assert os.path.samefile(upper, lower)
manifest = case_root / "manifest.json"
write_manifest(manifest, [
    {"id": "case-upper", "kind": "cat", "service": "meshy", "out": "Case.glb", "prompt": "casefold identity fixture"},
    {"id": "case-lower", "kind": "cat", "service": "meshy", "out": "case.glb", "prompt": "casefold identity fixture"},
])
input_before = snapshot(input_dir)
result, output, fake_log, fake_audit = run_case(case_root, input_dir, output_dir, manifest)
require_pre_fake_rejection(
    "case-fold duplicate outputs", result, output, fake_log, fake_audit,
    r"duplicate[^\n]*out|case.?fold|output paths alias",
)
check(snapshot(input_dir) == input_before, "case-fold duplicate outputs: source tree changed")
check(list(output_dir.iterdir()) == [], "case-fold duplicate outputs: partial output was created")
require_no_transaction_residue("case-fold duplicate outputs", case_root)

# A4: on a case-insensitive volume, differently cased root spellings are one
# directory. The capability guard itself proves why a skip is safe elsewhere.
case_root = root / "case-variant-roots"
input_dir = case_root / "InputCase"
output_dir = case_root / "inputcase"
input_dir.mkdir(parents=True)
case_insensitive = output_dir.exists() and os.path.samefile(input_dir, output_dir)
if case_insensitive:
    source = input_dir / "asset.glb"
    write_glb(source, triangles=30000)
    sidecar = write_sidecar(source, "meshy", "case root identity fixture")
    manifest = case_root / "manifest.json"
    write_manifest(manifest, [{
        "id": "case-root-cat", "kind": "cat", "service": "meshy",
        "out": source.name, "prompt": "case root identity fixture",
    }])
    source_bytes = source.read_bytes()
    sidecar_bytes = sidecar.read_bytes()
    source_hash = digest_bytes(source_bytes)
    sidecar_hash = digest_bytes(sidecar_bytes)
    tree_before = snapshot(input_dir)
    result, output, fake_log, fake_audit = run_case(
        case_root, input_dir, output_dir, manifest, force=True
    )
    require_pre_fake_rejection(
        "case-variant samefile roots", result, output, fake_log, fake_audit,
        r"alias|overlap|input.*output|output.*input|same (?:file|directory|identity|inode)",
    )
    check(os.path.samefile(input_dir, output_dir), "case-variant roots stopped aliasing")
    check(snapshot(input_dir) == tree_before, "case-variant samefile roots: shared source tree changed")
    check(source.read_bytes() == source_bytes, "case-variant samefile roots: source GLB changed")
    check(sidecar.read_bytes() == sidecar_bytes, "case-variant samefile roots: source sidecar changed")
    check(digest_bytes(source.read_bytes()) == source_hash, "case-variant samefile roots: source GLB hash changed")
    check(digest_bytes(sidecar.read_bytes()) == sidecar_hash, "case-variant samefile roots: source sidecar hash changed")
    check(source.read_bytes()[:4] == b"glTF", "case-variant samefile roots: GLB magic changed")
    require_no_transaction_residue("case-variant samefile roots", case_root)
else:
    assert not output_dir.exists()
    print("glb-decimation review A: case-variant root skipped; volume proved case-sensitive")

if errors:
    raise AssertionError("filesystem identity regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = A ]; then
  printf 'glb-decimation review A: pass\n'
  exit 0
fi

# Review regression B: every forced promotion phase and rollback branch must
# preserve pair lineage. Persistent restore faults may leave a recoverable old
# pair only when both finals are absent and both complete backups remain.
if [ "$review_section" = all ] || [ "$review_section" = B ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-rollback" <<'PY'
import hashlib
import importlib.util
import os
import sys
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
root.mkdir()
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("decimate_assets_rollback_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

errors = []
forward_order = ["backup_glb", "backup_json", "promote_glb", "promote_json"]

def check(condition, message):
    if not condition:
        errors.append(message)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def backup_files(directory):
    return sorted(
        (path for path in directory.iterdir() if ".backup-" in path.name),
        key=lambda path: path.name,
    )

def exercise(name, primary_failure, restore_failure=None):
    directory = root / name
    directory.mkdir()
    staged_glb = directory / "staged.glb"
    staged_json = directory / "staged.json"
    final_glb = directory / "final.glb"
    final_json = directory / "final.glb.json"
    old_glb = f"old GLB bytes for {name}".encode()
    old_json = f"old JSON bytes for {name}".encode()
    staged_glb.write_bytes(f"new GLB bytes for {name}".encode())
    staged_json.write_bytes(f"new JSON bytes for {name}".encode())
    final_glb.write_bytes(old_glb)
    final_json.write_bytes(old_json)
    old_hashes = (digest(final_glb), digest(final_json))

    real_replace = os.replace
    captured_backups = {}
    calls = []
    primary_reached = False
    restore_reached = False

    def classify(source, destination):
        source_path = Path(source)
        destination_path = Path(destination)
        if source_path == final_glb and destination_path != staged_glb:
            captured_backups["glb"] = destination_path
            return "backup_glb"
        if source_path == final_json and destination_path != staged_json:
            captured_backups["json"] = destination_path
            return "backup_json"
        if source_path == staged_glb and destination_path == final_glb:
            return "promote_glb"
        if source_path == staged_json and destination_path == final_json:
            return "promote_json"
        if source_path == captured_backups.get("glb") and destination_path == final_glb:
            return "restore_glb"
        if source_path == captured_backups.get("json") and destination_path == final_json:
            return "restore_json"
        return f"unexpected:{source_path.name}->{destination_path.name}"

    def replacing(source, destination):
        nonlocal primary_reached, restore_reached
        phase = classify(source, destination)
        calls.append(phase)
        if phase == primary_failure and not primary_reached:
            primary_reached = True
            raise OSError(f"injected primary {primary_failure} failure")
        if (
            restore_failure is not None
            and primary_reached
            and phase == restore_failure
            and not restore_reached
        ):
            restore_reached = True
            raise OSError(f"injected compound {restore_failure} failure")
        return real_replace(source, destination)

    caught = None
    with mock.patch.object(module.os, "replace", side_effect=replacing):
        try:
            module.promote_pair(
                staged_glb, staged_json, final_glb, final_json, True
            )
        except BaseException as exc:
            caught = exc

    label = f"{primary_failure}+{restore_failure or 'single'}"
    check(caught is not None, f"{label}: promotion swallowed injected failure")
    check(primary_reached, f"{label}: primary injection was not reached; calls={calls}")
    expected_prefix = forward_order[: forward_order.index(primary_failure) + 1]
    check(
        calls[: len(expected_prefix)] == expected_prefix,
        f"{label}: forward phases were not reached in order; calls={calls}",
    )
    check(
        not any(phase.startswith("unexpected:") for phase in calls),
        f"{label}: unclassified replace call; calls={calls}",
    )
    if restore_failure is not None:
        check(restore_reached, f"{label}: compound restore injection was not reached; calls={calls}")
        check(restore_failure in calls, f"{label}: restore phase absent; calls={calls}")

    backups = backup_files(directory)
    finals_are_old = (
        final_glb.is_file()
        and final_json.is_file()
        and (digest(final_glb), digest(final_json)) == old_hashes
    )
    finals_absent = not final_glb.exists() and not final_json.exists()
    captured_glb = captured_backups.get("glb")
    captured_json = captured_backups.get("json")
    complete_old_backups = (
        len(backups) == 2
        and captured_glb is not None
        and captured_json is not None
        and set(backups) == {captured_glb, captured_json}
        and captured_glb.is_file()
        and captured_json.is_file()
        and digest(captured_glb) == old_hashes[0]
        and digest(captured_json) == old_hashes[1]
    )

    if restore_failure is None:
        check(finals_are_old, f"{label}: old final pair was not restored exactly")
        check(not backups, f"{label}: backup residue remained after recoverable failure: {backups}")
    else:
        allowed_terminal = (
            (finals_are_old and not backups)
            or (finals_absent and complete_old_backups)
        )
        check(
            allowed_terminal,
            f"{label}: split, partial, or unrecoverable terminal state; "
            f"final_glb={final_glb.exists()} final_json={final_json.exists()} "
            f"backup_hashes={[digest(path) for path in backups]}",
        )
        check(
            not (final_glb.exists() != final_json.exists()),
            f"{label}: exactly one final member remains",
        )
        if finals_absent:
            check(
                complete_old_backups,
                f"{label}: final pair absent without a complete two-file old backup pair",
            )

for phase in forward_order:
    exercise(f"single-{phase}", phase)

for primary, restore in [
    ("backup_json", "restore_glb"),
    ("promote_glb", "restore_glb"),
    ("promote_glb", "restore_json"),
    ("promote_json", "restore_glb"),
    ("promote_json", "restore_json"),
]:
    exercise(f"compound-{primary}-{restore}", primary, restore)

if errors:
    raise AssertionError("promotion rollback regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = B ]; then
  printf 'glb-decimation review B: pass\n'
  exit 0
fi

# Review regression C: the default absent-destination promotion is one atomic
# custody decision. Freeze _promotion_guard(final_glb, final_json, *,
# on_attempt=None, on_acquired=None) as a private context-manager seam around
# the complete existence-check/promotion/rollback transaction. Its callbacks
# report a nonblocking contention decision; _path_exists then proves B rechecks
# both completed finals after the real same-output guard acquires.
if [ "$review_section" = all ] || [ "$review_section" = C ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - \
    "$decimate_script" "$tmp/review-concurrency" <<'PY'
import contextlib
import hashlib
import importlib.util
import os
import sys
import threading
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
root.mkdir()
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("decimate_assets_concurrency_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

final_glb = root / "final.glb"
final_json = root / "final.glb.json"
staged = {
    "A": (root / "a-staged.glb", root / "a-staged.json"),
    "B": (root / "b-staged.glb", root / "b-staged.json"),
}
payloads = {
    "A": (b"complete derivative from A", b"complete provenance from A"),
    "B": (b"complete derivative from B", b"complete provenance from B"),
}
for owner in ("A", "B"):
    staged[owner][0].write_bytes(payloads[owner][0])
    staged[owner][1].write_bytes(payloads[owner][1])

real_replace = os.replace
real_path_exists = module._path_exists
state_mutex = threading.Lock()
guard_state = threading.local()
a_at_first_replace = threading.Event()
b_at_first_replace = threading.Event()
b_progress = threading.Event()
b_guard_attempted = threading.Event()
a_guard_acquired = threading.Event()
b_guard_acquired = threading.Event()
a_pair_completed_in_guard = threading.Event()
allow_a_glb_replace = threading.Event()
a_glb_established = threading.Event()
allow_b_glb_replace = threading.Event()
b_json_failure_reached = threading.Event()
release_a_json = threading.Event()
results = {}
replace_calls = []
path_observations = []
guard_attempts = []
guard_acquisitions = []

@contextlib.contextmanager
def missing_promotion_guard(
    _final_glb, _final_json, *, on_attempt=None, on_acquired=None
):
    if on_attempt is not None:
        on_attempt(False)
    if on_acquired is not None:
        on_acquired(False)
    yield

real_promotion_guard = getattr(
    module, "_promotion_guard", missing_promotion_guard
)
if os.environ.get("GLB_DECIMATION_TEST_GUARD_MUTATION") == "noop":
    real_promotion_guard = missing_promotion_guard
elif os.environ.get("GLB_DECIMATION_TEST_GUARD_MUTATION") not in {None, ""}:
    raise AssertionError("unsupported GLB_DECIMATION_TEST_GUARD_MUTATION")

def thread_owner():
    name = threading.current_thread().name
    if name == "promotion-A":
        return "A"
    if name == "promotion-B":
        return "B"
    raise AssertionError(f"unexpected promotion thread {name!r}")

@contextlib.contextmanager
def controlled_promotion_guard(guard_glb, guard_json):
    assert Path(guard_glb) == final_glb
    assert Path(guard_json) == final_json
    owner = thread_owner()

    def attempted(contended):
        assert isinstance(contended, bool)
        with state_mutex:
            guard_attempts.append((owner, contended))
        if owner == "B":
            b_guard_attempted.set()
            b_progress.set()

    def acquired(contended):
        assert isinstance(contended, bool)
        completed = a_pair_completed_in_guard.is_set()
        with state_mutex:
            guard_acquisitions.append((owner, contended, completed))
        if owner == "A":
            a_guard_acquired.set()
        else:
            b_guard_acquired.set()

    with real_promotion_guard(
        guard_glb,
        guard_json,
        on_attempt=attempted,
        on_acquired=acquired,
    ):
        guard_state.active = True
        try:
            yield
        finally:
            if (
                owner == "A"
                and final_glb.is_file()
                and final_json.is_file()
                and final_glb.read_bytes() == payloads["A"][0]
                and final_json.read_bytes() == payloads["A"][1]
            ):
                a_pair_completed_in_guard.set()
            guard_state.active = False

def observing_path_exists(path):
    candidate = Path(path)
    value = real_path_exists(candidate)
    if candidate in {final_glb, final_json}:
        owner = thread_owner()
        member = "glb" if candidate == final_glb else "json"
        inside_guard = bool(getattr(guard_state, "active", False))
        completed = a_pair_completed_in_guard.is_set()
        with state_mutex:
            path_observations.append(
                (owner, member, value, inside_guard, completed)
            )
    return value

def replacing(source, destination):
    source_path = Path(source)
    destination_path = Path(destination)
    if source_path in staged["A"]:
        owner = "A"
    elif source_path in staged["B"]:
        owner = "B"
    else:
        return real_replace(source_path, destination_path)
    if destination_path == final_glb:
        member = "glb"
    elif destination_path == final_json:
        member = "json"
    else:
        return real_replace(source_path, destination_path)
    with state_mutex:
        replace_calls.append((owner, member))
    if owner == "A" and member == "glb":
        a_at_first_replace.set()
        if not allow_a_glb_replace.wait(5):
            raise AssertionError("timed out releasing A GLB promotion")
        result = real_replace(source_path, destination_path)
        a_glb_established.set()
        if not release_a_json.wait(5):
            raise AssertionError("timed out releasing A JSON promotion")
        return result
    if owner == "B" and member == "glb":
        b_at_first_replace.set()
        b_progress.set()
        if not allow_b_glb_replace.wait(5):
            raise AssertionError("timed out releasing B GLB promotion")
        return real_replace(source_path, destination_path)
    if owner == "B" and member == "json":
        b_json_failure_reached.set()
        raise OSError("injected concurrent B JSON promotion failure")
    return real_replace(source_path, destination_path)

def promote(owner):
    try:
        module.promote_pair(
            staged[owner][0], staged[owner][1], final_glb, final_json, False
        )
    except BaseException as exc:
        result = ("failure", exc)
    else:
        result = ("success", None)
    with state_mutex:
        results[owner] = result

with (
    mock.patch.object(module, "_promotion_guard", new=controlled_promotion_guard, create=True),
    mock.patch.object(module, "_path_exists", new=observing_path_exists),
    mock.patch.object(module.os, "replace", new=replacing),
):
    thread_a = threading.Thread(target=promote, args=("A",), name="promotion-A")
    thread_b = threading.Thread(target=promote, args=("B",), name="promotion-B")
    thread_a.start()
    assert a_at_first_replace.wait(5), "A never reached first GLB promotion"
    thread_b.start()
    assert b_progress.wait(5), "B reached neither the guard-attempt seam nor first replace"
    b_acquired_before_a_release = b_guard_acquired.is_set()

    # Correct code signals the guard attempt and blocks on A's transaction.
    # Current unlocked code instead reaches B's first replace and blocks there.
    allow_a_glb_replace.set()
    assert a_glb_established.wait(5), "A never established the destination GLB"
    allow_b_glb_replace.set()
    if b_at_first_replace.is_set():
        assert b_json_failure_reached.wait(5), "B crossed but did not reach JSON injection"
    release_a_json.set()

    thread_a.join(5)
    thread_b.join(5)
    assert not thread_a.is_alive(), "A promotion thread did not terminate"
    assert not thread_b.is_alive(), "B promotion thread did not terminate"

errors = []
def check(condition, message):
    if not condition:
        errors.append(message)

with state_mutex:
    result_snapshot = dict(results)
    replace_snapshot = list(replace_calls)
    observation_snapshot = list(path_observations)
    attempt_snapshot = list(guard_attempts)
    acquisition_snapshot = list(guard_acquisitions)

check(set(result_snapshot) == {"A", "B"}, f"missing thread result: {result_snapshot}")
successes = [owner for owner, result in result_snapshot.items() if result[0] == "success"]
failures = [owner for owner, result in result_snapshot.items() if result[0] == "failure"]
check(successes == ["A"], f"expected only A success; results={result_snapshot}")
check(failures == ["B"], f"expected only B refusal/failure; results={result_snapshot}")
b_exception = result_snapshot.get("B", (None, None))[1]
check(
    isinstance(b_exception, module.DecimationError)
    and str(b_exception) == "refusing existing derivative without --force",
    f"B lacked exact existing-destination DecimationError: {b_exception!r}",
)
check(a_guard_acquired.is_set(), "A did not acquire the _promotion_guard seam")
check(b_guard_attempted.is_set(), "B did not attempt _promotion_guard before A release")
check(b_guard_acquired.is_set(), "B did not acquire _promotion_guard after A completed")
check(
    not b_acquired_before_a_release,
    "B acquired _promotion_guard before A released its transaction",
)
check(
    attempt_snapshot == [("A", False), ("B", True)],
    f"guard attempts did not prove real B contention: {attempt_snapshot}",
)
check(
    acquisition_snapshot == [("A", False, False), ("B", True, True)],
    "guard acquisitions did not serialize B behind completed A: "
    f"{acquisition_snapshot}",
)
check(a_pair_completed_in_guard.is_set(), "A pair was not completed while its guard was held")
check(not b_at_first_replace.is_set(), f"B replace was reached: {replace_snapshot}")
b_postlock = [
    (member, value, completed)
    for owner, member, value, inside_guard, completed in observation_snapshot
    if owner == "B" and inside_guard
]
check(
    {member for member, _, _ in b_postlock} == {"glb", "json"}
    and all(value and completed for _, value, completed in b_postlock),
    f"B did not recheck both completed A finals inside the guard: {b_postlock}",
)
complete_pair = final_glb.is_file() and final_json.is_file()
check(complete_pair, "successful A final pair is incomplete")
if complete_pair:
    check(final_glb.read_bytes() == payloads["A"][0], "final GLB is not A's successful member")
    check(final_json.read_bytes() == payloads["A"][1], "final JSON is not A's successful member")
    check(
        hashlib.sha256(final_glb.read_bytes()).hexdigest()
        == hashlib.sha256(payloads["A"][0]).hexdigest(),
        "final GLB hash does not belong to successful A",
    )
    check(
        hashlib.sha256(final_json.read_bytes()).hexdigest()
        == hashlib.sha256(payloads["A"][1]).hexdigest(),
        "final JSON hash does not belong to successful A",
    )
expected_entries = {final_glb, final_json, staged["B"][0], staged["B"][1]}
actual_entries = set(root.iterdir())
check(
    actual_entries == expected_entries,
    "concurrent promotion left missing/unexpected transaction entries: "
    f"{sorted(path.name for path in actual_entries)}",
)
if errors:
    raise AssertionError("concurrent promotion regressions:\n- " + "\n- ".join(errors))
PY
  assert_no_external_effects
fi

if [ "$review_section" = C ]; then
  printf 'glb-decimation review C: pass\n'
  exit 0
fi

# Happy path: both selected roots deliberately contain spaces and shell
# metacharacters. If production turns the argument vector into a shell string,
# the input path attempts to create $marker in the repository root.
input_dir="$tmp/input space;\$(touch $marker_name);#"
output_dir="$tmp/output space&[]{}"
manifest="$tmp/manifest happy.json"
mkdir -p "$input_dir" "$output_dir"
write_fixture "$input_dir/cat-source.glb" --triangles 30000
write_fixture "$input_dir/prop-source.glb" --triangles 20000
write_sidecar "$input_dir/cat-source.glb" meshy "round fixture cat" paid
write_sidecar "$input_dir/prop-source.glb" tripo "rounded fixture prop" paid
write_happy_manifest "$manifest"

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts" "$manifest" "$input_dir" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import inspect_glb

manifest = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
input_dir = Path(sys.argv[3])
assert [(entry["kind"], entry["out"]) for entry in manifest["assets"]] == [
    ("cat", "cat-source.glb"), ("prop", "prop-source.glb")
]
for entry, expected_triangles in zip(manifest["assets"], (30000, 20000)):
    source = input_dir / entry["out"]
    metrics = inspect_glb(source)
    assert metrics["triangles"] == expected_triangles
    assert metrics["uv_primitives"] == metrics["material_primitives"] == metrics["primitives"]
    assert metrics["materials"] == metrics["embedded_images"] == 1
    sidecar = json.loads(Path(f"{source}.json").read_text(encoding="utf-8"))
    assert sidecar["service"] == entry["service"]
    assert sidecar["prompt"] == entry["prompt"]
    assert sidecar["plan_tier"] == "paid"
    assert sidecar["sha256"] == hashlib.sha256(source.read_bytes()).hexdigest()
PY

happy_input_before=$(fingerprint_tree "$input_dir")
happy_log="$tmp/happy-fake.log"
happy_stdout="$tmp/happy.stdout"
happy_stderr="$tmp/happy.stderr"
if ! run_decimator success "$happy_log" "$happy_stdout" "$happy_stderr"; then
  sed -n '1,160p' "$happy_stdout" >&2
  sed -n '1,160p' "$happy_stderr" >&2
  die "two-entry happy path failed"
fi
assert_no_external_effects
test "$happy_input_before" = "$(fingerprint_tree "$input_dir")" || \
  die "happy path modified a source or source sidecar"

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts" "$happy_log" "$input_dir" "$output_dir" \
  "$fake_blender" "$expected_driver" "$happy_log.audit" <<'PY'
import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import compare_preservation, inspect_glb

log_path, input_dir, output_dir, fake_path, driver_path, audit_path = map(Path, sys.argv[2:])
records = [json.loads(line) for line in log_path.read_text(encoding="utf-8").splitlines()]
assert len(records) == 2
assert [record["target"] for record in records] == [15000, 10000]
assert audit_path.read_text(encoding="utf-8").splitlines() == ["version", "asset", "asset"]
fixed_prefix = [
    "--background", "--factory-startup", "--offline-mode", "--disable-autoexec",
    "--threads", "1", "--python-exit-code", "97", "--python",
]
expected_sources = [input_dir / "cat-source.glb", input_dir / "prop-source.glb"]
expected_source_triangles = ["30000", "20000"]
expected_targets = ["15000", "10000"]
expected_minima = ["13500", "9000"]
expected_maxima = ["15000", "10000"]
staged_outputs = []
for index, record in enumerate(records):
    argv = record["argv"]
    assert Path(argv[0]).resolve() == fake_path.resolve()
    assert argv[1:1 + len(fixed_prefix)] == fixed_prefix
    driver_index = 1 + len(fixed_prefix)
    assert Path(argv[driver_index]).resolve() == driver_path.resolve()
    assert argv[driver_index + 1] == "--"
    post = argv[driver_index + 2:]
    assert post[0::2] == [
        "--source", "--output", "--source-triangles", "--target-triangles",
        "--minimum-triangles", "--maximum-triangles",
    ]
    values = dict(zip(post[0::2], post[1::2]))
    assert Path(values["--source"]) == expected_sources[index]
    staged_output = Path(values["--output"])
    staged_outputs.append(staged_output)
    assert staged_output.resolve().is_relative_to(output_dir.resolve())
    assert staged_output.suffix == ".glb"
    assert values["--source-triangles"] == expected_source_triangles[index]
    assert values["--target-triangles"] == expected_targets[index]
    assert values["--minimum-triangles"] == expected_minima[index]
    assert values["--maximum-triangles"] == expected_maxima[index]
assert len(set(staged_outputs)) == len(staged_outputs) == 2

for filename, target, minimum in (
    ("cat-source.glb", 15000, 13500),
    ("prop-source.glb", 10000, 9000),
):
    source = input_dir / filename
    final = output_dir / filename
    proof_path = Path(f"{final}.json")
    assert final.is_file() and proof_path.is_file()
    source_metrics = inspect_glb(source)
    output_metrics = inspect_glb(final)
    assert output_metrics["triangles"] == target
    assert minimum <= output_metrics["triangles"] <= target
    assert 5000 <= output_metrics["triangles"] <= 20000
    assert compare_preservation(source_metrics, output_metrics) == []

forbidden = re.compile(
    r"api[_-]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)
def scan(value):
    if isinstance(value, dict):
        for key, child in value.items():
            assert not forbidden.search(str(key)), key
            scan(child)
    elif isinstance(value, list):
        for child in value:
            scan(child)
    elif isinstance(value, str):
        assert not forbidden.search(value), value

for proof_path in output_dir.glob("*.glb.json"):
    scan(json.loads(proof_path.read_text(encoding="utf-8")))
for record in records:
    scan(record)

actual_entries = sorted(
    path.relative_to(output_dir).as_posix()
    for path in output_dir.rglob("*")
)
assert actual_entries == [
    "cat-source.glb", "cat-source.glb.json",
    "prop-source.glb", "prop-source.glb.json",
]
PY

assert_exact_provenance \
  "$input_dir/cat-source.glb" "$output_dir/cat-source.glb" \
  "$output_dir/cat-source.glb.json" cat 15000 13500 meshy "round fixture cat"
assert_exact_provenance \
  "$input_dir/prop-source.glb" "$output_dir/prop-source.glb" \
  "$output_dir/prop-source.glb.json" prop 10000 9000 tripo "rounded fixture prop"

# Each table row receives a fresh input/output tree. The shared runner proves
# the named diagnostic, source custody, final-pair behavior, fake reachability,
# curl abstinence, and absence of shell evaluation.
case_root=
input_dir=
output_dir=
manifest=
final_glb=
final_json=
case_external_referent=

prepare_valid_case() {
  local name=$1
  local triangles=${2:-30000}
  case_root="$tmp/cases/$name"
  input_dir="$case_root/input"
  output_dir="$case_root/output"
  manifest="$case_root/manifest.json"
  final_glb="$output_dir/asset.glb"
  final_json="$output_dir/asset.glb.json"
  case_external_referent=
  mkdir -p "$input_dir" "$output_dir"
  write_fixture "$input_dir/asset.glb" --triangles "$triangles"
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid
  write_single_manifest "$manifest" fixture-cat cat meshy asset.glb "fixture cat"
}

install_existing_pair() {
  write_fixture "$final_glb" --triangles 14000
  local derivative_sha
  derivative_sha=$(sha256_file "$final_glb")
  PYTHONDONTWRITEBYTECODE=1 python3 - "$final_json" "$derivative_sha" <<'PY'
import json
import sys
from pathlib import Path

record = {
    "schema_version": 1,
    "derivative": {"filename": "asset.glb", "sha256": sys.argv[2]},
    "sentinel": "old pair",
}
Path(sys.argv[1]).write_text(json.dumps(record, sort_keys=True) + "\n", encoding="utf-8")
PY
}

run_failure_case() {
  local name=$1
  local mode=$2
  local expected_pattern=$3
  local fake_reached=$4
  local existing=${5:-absent}
  local stdout="$case_root/run.stdout"
  local stderr="$case_root/run.stderr"
  local log="$case_root/fake.log"
  local before_input before_output='' before_lines after_lines
  local old_glb_sha='' old_json_sha=''
  local referent_snapshot='' referent_sha='' referent_magic=''

  before_input=$(fingerprint_tree "$input_dir")
  if [ "$existing" = preserve_tree ]; then
    before_output=$(fingerprint_tree "$output_dir")
  fi
  if [ -n "$case_external_referent" ]; then
    referent_snapshot="$case_root/external-referent.snapshot"
    cp -- "$case_external_referent" "$referent_snapshot"
    referent_sha=$(sha256_file "$case_external_referent")
    referent_magic=$(magic_hex "$case_external_referent")
    test "$referent_magic" = 676c5446 || \
      die "$name external referent was not independently valid GLB input"
  fi
  before_lines=$(line_count "$log")
  if [ "$existing" = preserve ]; then
    old_glb_sha=$(sha256_file "$final_glb")
    old_json_sha=$(sha256_file "$final_json")
  fi

  set +e
  run_decimator "$mode" "$log" "$stdout" "$stderr"
  local rc=$?
  set -e
  test "$rc" -ne 0 || die "$name unexpectedly succeeded"
  if ! rg -q "$expected_pattern" "$stderr"; then
    sed -n '1,120p' "$stderr" >&2
    die "$name lacked diagnostic $expected_pattern"
  fi
  test "$before_input" = "$(fingerprint_tree "$input_dir")" || \
    die "$name modified its source custody tree"
  if [ -n "$case_external_referent" ]; then
    test "$referent_sha" = "$(sha256_file "$case_external_referent")" || \
      die "$name changed its external referent hash"
    test "$referent_magic" = "$(magic_hex "$case_external_referent")" || \
      die "$name changed its external referent magic"
    cmp -s "$referent_snapshot" "$case_external_referent" || \
      die "$name changed its external referent bytes"
  fi

  after_lines=$(line_count "$log")
  if [ "$fake_reached" = yes ]; then
    test "$after_lines" -eq $((before_lines + 1)) || \
      die "$name did not reach fake Blender exactly once"
  else
    test "$after_lines" -eq "$before_lines" || \
      die "$name reached fake Blender's asset surface"
  fi

  if [ "$existing" = preserve ]; then
    test "$old_glb_sha" = "$(sha256_file "$final_glb")" || \
      die "$name changed the existing derivative"
    test "$old_json_sha" = "$(sha256_file "$final_json")" || \
      die "$name changed the existing provenance"
  elif [ "$existing" = preserve_tree ]; then
    test "$before_output" = "$(fingerprint_tree "$output_dir")" || \
      die "$name changed its pre-existing output tree or symlink"
  elif find "$output_dir" -mindepth 1 -print -quit | grep -q .; then
    find "$output_dir" -mindepth 1 -print >&2
    die "$name left a final or staged output"
  fi
  assert_no_external_effects
}

setup_malformed_json() {
  prepare_valid_case malformed-json
  printf '{"assets":[' >"$manifest"
}
setup_malformed_root() {
  prepare_valid_case malformed-root
  printf '[]\n' >"$manifest"
}
setup_malformed_assets() {
  prepare_valid_case malformed-assets
  printf '{"assets":{}}\n' >"$manifest"
}
setup_malformed_entry() {
  prepare_valid_case malformed-entry
  printf '{"assets":["not-an-object"]}\n' >"$manifest"
}
setup_duplicate_id() {
  prepare_valid_case duplicate-id
  PYTHONDONTWRITEBYTECODE=1 python3 - "$manifest" <<'PY'
import json
import sys
from pathlib import Path

entry = {"id": "same", "kind": "cat", "service": "meshy", "out": "asset.glb", "prompt": "fixture cat"}
Path(sys.argv[1]).write_text(json.dumps({"assets": [entry, {**entry, "out": "other.glb"}]}) + "\n", encoding="utf-8")
PY
}
setup_duplicate_out() {
  prepare_valid_case duplicate-out
  PYTHONDONTWRITEBYTECODE=1 python3 - "$manifest" <<'PY'
import json
import sys
from pathlib import Path

entry = {"id": "one", "kind": "cat", "service": "meshy", "out": "asset.glb", "prompt": "fixture cat"}
Path(sys.argv[1]).write_text(json.dumps({"assets": [entry, {**entry, "id": "two"}]}) + "\n", encoding="utf-8")
PY
}
setup_unsupported_kind() {
  prepare_valid_case unsupported-kind
  write_single_manifest "$manifest" fixture-station station meshy asset.glb "fixture cat"
}
setup_missing_source() {
  prepare_valid_case missing-source
  rm -f -- "$input_dir/asset.glb" "$input_dir/asset.glb.json"
}
setup_missing_sidecar() {
  prepare_valid_case missing-sidecar
  rm -f -- "$input_dir/asset.glb.json"
}
setup_bad_magic() {
  prepare_valid_case bad-magic
  printf 'NOTGLTF' >"$input_dir/asset.glb"
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid
}
setup_bad_sha() {
  prepare_valid_case bad-sha
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid \
    0000000000000000000000000000000000000000000000000000000000000000
}
setup_unpaid() {
  prepare_valid_case unpaid
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" unknown
}
setup_path_escape() {
  prepare_valid_case path-escape
  write_single_manifest "$manifest" fixture-cat cat meshy ../escape.glb "fixture cat"
}
setup_input_symlink_escape() {
  prepare_valid_case input-symlink-escape
  case_external_referent="$case_root/outside-input.glb"
  mv "$input_dir/asset.glb" "$case_external_referent"
  rm -f -- "$input_dir/asset.glb.json"
  ln -s "../outside-input.glb" "$input_dir/asset.glb"
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid
}
setup_output_symlink_escape() {
  prepare_valid_case output-symlink-escape
  case_external_referent="$case_root/outside-output.glb"
  write_fixture "$case_external_referent" --triangles 14000
  ln -s "../outside-output.glb" "$final_glb"
}
setup_wrong_version() {
  prepare_valid_case wrong-version
  CASE_BLENDER_VERSION=5.2.0
}
setup_wrong_build() {
  prepare_valid_case wrong-build
  CASE_BLENDER_BUILD_HASH=wrong-build
}
setup_small_source() {
  prepare_valid_case small-source 15000
}
setup_preexisting() {
  prepare_valid_case preexisting
  install_existing_pair
}
setup_fake_mode() {
  prepare_valid_case "$1"
}

setup_malformed_json
run_failure_case "malformed JSON" success 'invalid manifest' no
setup_malformed_root
run_failure_case "manifest root type" success 'invalid manifest' no
setup_malformed_assets
run_failure_case "manifest assets type" success 'invalid manifest' no
setup_malformed_entry
run_failure_case "manifest entry type" success 'invalid manifest' no
setup_duplicate_id
run_failure_case "duplicate manifest id" success 'invalid manifest' no
setup_duplicate_out
run_failure_case "duplicate manifest out" success 'invalid manifest' no
setup_unsupported_kind
run_failure_case "unsupported kind" success 'unsupported kind' no
setup_missing_source
run_failure_case "missing source" success 'missing source' no
setup_missing_sidecar
run_failure_case "missing source sidecar" success 'missing source sidecar' no
setup_bad_magic
run_failure_case "bad source magic" success 'invalid GLB header' no
setup_bad_sha
run_failure_case "bad source SHA" success 'source SHA-256 mismatch' no
setup_unpaid
run_failure_case "unpaid source" success 'plan_tier must be paid' no
setup_path_escape
run_failure_case "manifest path escape" success 'bare \.glb filename' no
test ! -e "$case_root/escape.glb" && test ! -e "$case_root/escape.glb.json" || \
  die "manifest path escape created an escaped output"
setup_input_symlink_escape
run_failure_case "input-leaf symlink escape" success 'path escapes' no
setup_output_symlink_escape
run_failure_case "output-leaf symlink escape" success 'path escapes' no preserve_tree
setup_wrong_version
run_failure_case "wrong Blender version" success 'requires Blender 5\.1\.2' no
unset CASE_BLENDER_VERSION
setup_wrong_build
run_failure_case "wrong Blender build" success 'ec6e62d40fa9' no
unset CASE_BLENDER_BUILD_HASH
setup_small_source
run_failure_case "source already within target" success 'already within budget' no
setup_preexisting
run_failure_case "pre-existing destination" success 'refusing existing derivative' no preserve

setup_fake_mode blender-failure
run_failure_case "Blender failure" fail 'Blender failed' yes
setup_fake_mode malformed-derivative
run_failure_case "malformed derivative" malformed_output 'invalid GLB header' yes
setup_fake_mode above-band
run_failure_case "above category band" over_budget 'triangle band' yes
setup_fake_mode below-band
run_failure_case "below category band" under_budget 'triangle band' yes
setup_fake_mode missing-uv
run_failure_case "missing UV" missing_uv 'lost UV' yes
setup_fake_mode missing-material
run_failure_case "missing material" missing_material 'material count changed' yes
setup_fake_mode missing-image
run_failure_case "missing embedded image" missing_image 'embedded-image count changed' yes
setup_fake_mode bounds-drift
run_failure_case "bounds drift" bounds_drift 'center drift' yes
setup_fake_mode external-image
run_failure_case "external image" external_image 'external URI' yes
setup_fake_mode unsupported-extension
run_failure_case "arbitrary extension" unsupported_extension 'unsupported extension' yes
setup_fake_mode active-scene
run_failure_case "active scene payload" unexpected_scene_content 'animation|camera|light' yes

# Force is pair-safe: default refusal and a rejected candidate retain both old
# hashes, while a fully accepted candidate replaces both and records its hash.
prepare_valid_case force-pair
install_existing_pair
force_old_glb_sha=$(sha256_file "$final_glb")
force_old_json_sha=$(sha256_file "$final_json")
force_input_before=$(fingerprint_tree "$input_dir")
force_log="$case_root/fake.log"

set +e
run_decimator success "$force_log" "$case_root/force-default.stdout" "$case_root/force-default.stderr"
force_default_rc=$?
set -e
test "$force_default_rc" -ne 0 || die "default run replaced an existing pair"
rg -q 'refusing existing derivative' "$case_root/force-default.stderr" || \
  die "default pair refusal lacked its diagnostic"
test "$force_old_glb_sha" = "$(sha256_file "$final_glb")" || \
  die "default refusal changed the old GLB"
test "$force_old_json_sha" = "$(sha256_file "$final_json")" || \
  die "default refusal changed the old JSON"
test "$(line_count "$force_log")" -eq 0 || \
  die "default refusal reached fake Blender's asset surface"

set +e
run_decimator over_budget "$force_log" "$case_root/force-bad.stdout" "$case_root/force-bad.stderr" --force
force_bad_rc=$?
set -e
test "$force_bad_rc" -ne 0 || die "--force promoted an over-budget candidate"
rg -q 'triangle band' "$case_root/force-bad.stderr" || \
  die "--force over-budget failure lacked triangle-band diagnostic"
test "$force_old_glb_sha" = "$(sha256_file "$final_glb")" || \
  die "failed --force changed the old GLB"
test "$force_old_json_sha" = "$(sha256_file "$final_json")" || \
  die "failed --force changed the old JSON"
test "$(line_count "$force_log")" -eq 1 || \
  die "failed --force did not reach fake Blender exactly once"
PYTHONDONTWRITEBYTECODE=1 python3 - "$output_dir" <<'PY'
import sys
from pathlib import Path

root = Path(sys.argv[1])
assert sorted(path.relative_to(root).as_posix() for path in root.rglob("*")) == [
    "asset.glb", "asset.glb.json"
]
PY

run_decimator success "$force_log" "$case_root/force-good.stdout" "$case_root/force-good.stderr" --force || {
  sed -n '1,120p' "$case_root/force-good.stderr" >&2
  die "valid --force replacement failed"
}
test "$(line_count "$force_log")" -eq 2 || \
  die "successful --force did not reach fake Blender exactly once"
test "$force_old_glb_sha" != "$(sha256_file "$final_glb")" || \
  die "successful --force did not replace the old GLB"
test "$force_old_json_sha" != "$(sha256_file "$final_json")" || \
  die "successful --force did not replace the old JSON"
test "$force_input_before" = "$(fingerprint_tree "$input_dir")" || \
  die "force path modified source custody"
PYTHONDONTWRITEBYTECODE=1 python3 - "$repo/scripts" "$final_glb" "$final_json" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import inspect_glb

glb = Path(sys.argv[2])
proof = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8"))
assert inspect_glb(glb)["triangles"] == 15000
assert proof["derivative"]["sha256"] == hashlib.sha256(glb.read_bytes()).hexdigest()
assert sorted(path.name for path in glb.parent.iterdir()) == [
    "asset.glb", "asset.glb.json"
]
PY
assert_exact_provenance \
  "$input_dir/asset.glb" "$final_glb" "$final_json" \
  cat 15000 13500 meshy "fixture cat"
assert_no_external_effects

# Static network boundary: the inspector, Blender driver, and orchestrator may
# not import a networking stack. Parse real syntax rather than grepping prose.
PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$repo/scripts/glb_metrics.py" "$expected_driver" "$decimate_script" <<'PY'
import ast
import sys
from pathlib import Path

for filename in sys.argv[1:]:
    source = Path(filename).read_bytes()
    tree = ast.parse(source, filename=filename)
    forbidden = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            forbidden.extend(alias.name for alias in node.names if alias.name.split(".")[0] in {"socket", "urllib", "http", "requests"})
        elif isinstance(node, ast.ImportFrom) and (node.module or "").split(".")[0] in {"socket", "urllib", "http", "requests"}:
            forbidden.append(node.module or "")
    assert not forbidden, f"{filename} imports network modules: {forbidden}"
PY

# Fault injection at the public promotion boundary. The injected exception is
# tied to the staged-JSON -> final-JSON replace, so both rollback legs prove
# the first promotion really occurred before the second one failed.
PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$decimate_script" "$tmp/helper-faults" "$repo" "$fake_blender" <<'PY'
import contextlib
import hashlib
import importlib.util
import io
import json
import os
import sys
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
repo = Path(sys.argv[3])
fake_blender = Path(sys.argv[4])
root.mkdir()
sys.dont_write_bytecode = True
sys.path.insert(0, str(repo / "tests" / "assets"))
from glb_fixture import write_glb

spec = importlib.util.spec_from_file_location("decimate_assets_under_test", script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def inject_second_promotion(directory, force):
    directory.mkdir()
    staged_glb = directory / "staged.glb"
    staged_json = directory / "staged.json"
    final_glb = directory / "final.glb"
    final_json = directory / "final.glb.json"
    staged_glb.write_bytes(b"new glb")
    staged_json.write_bytes(b"new json")
    old_hashes = None
    if force:
        final_glb.write_bytes(b"old glb")
        final_json.write_bytes(b"old json")
        old_hashes = (digest(final_glb), digest(final_json))

    real_replace = os.replace
    calls = []
    reached_first_promotion = False
    reached_second_promotion = False

    def failing_replace(source, destination):
        nonlocal reached_first_promotion, reached_second_promotion
        source_path = Path(source)
        destination_path = Path(destination)
        calls.append((source_path, destination_path))
        if source_path == staged_glb and destination_path == final_glb:
            reached_first_promotion = True
        if source_path == staged_json and destination_path == final_json:
            reached_second_promotion = True
            raise OSError("injected second promotion failure")
        return real_replace(source, destination)

    with mock.patch.object(module.os, "replace", side_effect=failing_replace):
        try:
            module.promote_pair(
                staged_glb, staged_json, final_glb, final_json, force
            )
        except OSError as exc:
            assert "injected second promotion failure" in str(exc)
        else:
            raise AssertionError("promote_pair swallowed the injected failure")

    assert reached_first_promotion and reached_second_promotion, calls
    if force:
        assert final_glb.is_file() and final_json.is_file()
        assert (digest(final_glb), digest(final_json)) == old_hashes
        assert set(directory.iterdir()) <= {final_glb, final_json, staged_json}
    else:
        assert not final_glb.exists() and not final_json.exists()
        assert set(directory.iterdir()) <= {staged_json}

inject_second_promotion(root / "new-destination", False)
inject_second_promotion(root / "forced-destination", True)

# Freeze main(argv: list[str]) as the import-safe orchestration interface. This
# fault reaches the real one-asset path after version check, fake execution,
# and candidate validation, then fails the staged provenance Path.open before
# promotion can begin.
orchestration = root / "provenance-orchestration"
input_dir = orchestration / "input"
output_dir = orchestration / "output"
input_dir.mkdir(parents=True)
output_dir.mkdir()
source = input_dir / "asset.glb"
source_sidecar = Path(f"{source}.json")
manifest = orchestration / "manifest.json"
final_glb = output_dir / "asset.glb"
final_json = output_dir / "asset.glb.json"
fake_log = orchestration / "fake.log"
fake_audit = orchestration / "fake.audit"
write_glb(source, triangles=30000)
source_sha = digest(source)
source_sidecar.write_text(json.dumps({
    "service": "meshy",
    "task_id": "fixture-meshy-task",
    "timestamp_utc": "2026-08-15T12:34:56Z",
    "plan_tier": "paid",
    "prompt": "fixture cat",
    "note": "local paid fixture",
    "sha256": source_sha,
}, sort_keys=True) + "\n", encoding="utf-8")
manifest.write_text(json.dumps({"assets": [{
    "id": "fixture-cat",
    "kind": "cat",
    "service": "meshy",
    "out": "asset.glb",
    "prompt": "fixture cat",
}]}, sort_keys=True) + "\n", encoding="utf-8")
source_before = source.read_bytes()
sidecar_before = source_sidecar.read_bytes()

real_open = Path.open
promote = mock.Mock()
opened_paths = []

def failing_open(path, *args, **kwargs):
    mode = args[0] if args else kwargs.get("mode", "r")
    candidate = Path(path)
    resolved = candidate.resolve(strict=False)
    if (
        any(flag in mode for flag in "wax")
        and candidate.suffix == ".json"
        and resolved.is_relative_to(output_dir.resolve())
        and candidate != final_json
    ):
        opened_paths.append(candidate)
        raise OSError("injected staged provenance failure")
    return real_open(path, *args, **kwargs)

sentinel_environment = {
    "FAKE_BLENDER_MODE": "success",
    "FAKE_BLENDER_LOG": str(fake_log),
    "FAKE_BLENDER_AUDIT": str(fake_audit),
    "PIPELINE_SENTINEL_KEY": "orchestration-sentinel-1",
    "PIPELINE_SENTINEL_TOKEN": "orchestration-sentinel-2",
    "PIPELINE_SENTINEL_SECRET": "orchestration-sentinel-3",
    "PIPELINE_SENTINEL_AUTH": "orchestration-sentinel-4",
    "PIPELINE_SENTINEL_CREDENTIAL": "orchestration-sentinel-5",
    "PIPELINE_SENTINEL_BEARER": "orchestration-sentinel-6",
    "PYTHONDONTWRITEBYTECODE": "1",
}
arguments = [
    "--manifest", str(manifest),
    "--input-dir", str(input_dir),
    "--output-dir", str(output_dir),
    "--blender", str(fake_blender),
]
stdout = io.StringIO()
stderr = io.StringIO()
assert callable(module.main), "decimate-assets.py must expose main(argv: list[str]) -> int"
with (
    mock.patch.dict(os.environ, sentinel_environment, clear=False),
    mock.patch.object(Path, "open", failing_open),
    mock.patch.object(module, "promote_pair", promote),
    contextlib.redirect_stdout(stdout),
    contextlib.redirect_stderr(stderr),
):
    try:
        main_result = module.main(arguments)
    except OSError as exc:
        assert "injected staged provenance failure" in str(exc)
    except SystemExit as exc:
        assert isinstance(exc.code, int) and exc.code != 0
    else:
        assert isinstance(main_result, int) and main_result != 0

assert len(opened_paths) == 1
assert opened_paths[0] != final_json
assert opened_paths[0].resolve(strict=False).is_relative_to(output_dir.resolve())
promote.assert_not_called()
assert not final_glb.exists() and not final_json.exists()
assert source.read_bytes() == source_before
assert source_sidecar.read_bytes() == sidecar_before
records = [json.loads(line) for line in fake_log.read_text(encoding="utf-8").splitlines()]
assert len(records) == 1 and records[0]["target"] == 15000
assert fake_audit.read_text(encoding="utf-8").splitlines() == ["version", "asset"]
combined_output = stdout.getvalue() + stderr.getvalue()
for value in sentinel_environment.values():
    if value.startswith("orchestration-sentinel-"):
        assert value not in combined_output
PY

assert_no_external_effects
test -z "$(find "$repo/tests/assets" "$repo/scripts" -type d -name __pycache__ -print -quit)" || \
  die "pipeline test left Python bytecode cache residue"

printf 'glb-decimation pipeline test: pass\n'
