#!/usr/bin/env bash
# Behavioral contract for the offline GLB decimation orchestrator. The fake
# process boundary is validated independently before any production entry point
# runs, so the initial RED can only be earned by the absent orchestrator.
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)
decimate_script=${DECIMATE_SCRIPT:-$repo/scripts/decimate-assets.py}
fake_blender="$repo/tests/assets/fake_blender.py"
expected_driver=$(cd "$(dirname "$decimate_script")" && pwd -P)/blender_decimate.py
tmp=$(mktemp -d)
marker_name="$(basename "$tmp")-argv-injection-marker"
marker="$repo/$marker_name"

cleanup() {
  rm -rf -- "$tmp"
  rm -f -- "$marker"
}
trap cleanup EXIT

die() {
  printf 'glb-decimation pipeline test: %s\n' "$1" >&2
  exit 1
}

test ! -e "$marker" || die "shell-evaluation marker already exists"
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
    FAKE_BLENDER_VERSION="${CASE_BLENDER_VERSION:-5.1.2}" \
    FAKE_BLENDER_BUILD_HASH="${CASE_BLENDER_BUILD_HASH:-ec6e62d40fa9}" \
    PIPELINE_TEST_API_KEY="must-not-leak-key-value" \
    PIPELINE_TEST_AUTHORIZATION="must-not-leak-auth-value" \
    PYTHONDONTWRITEBYTECODE=1 \
    python3 "$decimate_script" \
      --manifest "$manifest" \
      --input-dir "$input_dir" \
      --output-dir "$output_dir" \
      --blender "$fake_blender" \
      "$@" \
      >"$stdout" 2>"$stderr"
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

version_output=$(PYTHONDONTWRITEBYTECODE=1 "$fake_blender" --background --version)
test "$version_output" = $'Blender 5.1.2\nbuild hash: ec6e62d40fa9' || \
  die "fake Blender version surface is wrong"
wrong_version_output=$(
  FAKE_BLENDER_VERSION=5.2.0 FAKE_BLENDER_BUILD_HASH=wrong \
    PYTHONDONTWRITEBYTECODE=1 "$fake_blender" --background --version
)
test "$wrong_version_output" = $'Blender 5.2.0\nbuild hash: wrong' || \
  die "fake Blender version overrides are wrong"

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
  "$repo/scripts" "$happy_log" "$input_dir" "$output_dir" "$fake_blender" "$expected_driver" <<'PY'
import hashlib
import json
import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, sys.argv[1])
from glb_metrics import compare_preservation, inspect_glb

log_path, input_dir, output_dir, fake_path, driver_path = map(Path, sys.argv[2:])
records = [json.loads(line) for line in log_path.read_text(encoding="utf-8").splitlines()]
assert len(records) == 2
assert [record["target"] for record in records] == [15000, 10000]
fixed_prefix = [
    "--background", "--factory-startup", "--offline-mode", "--disable-autoexec",
    "--threads", "1", "--python-exit-code", "97", "--python",
]
expected_sources = [input_dir / "cat-source.glb", input_dir / "prop-source.glb"]
expected_source_triangles = ["30000", "20000"]
expected_targets = ["15000", "10000"]
expected_minima = ["13500", "9000"]
expected_maxima = ["15000", "10000"]
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
    assert staged_output.resolve().is_relative_to(output_dir.resolve())
    assert staged_output.suffix == ".glb"
    assert values["--source-triangles"] == expected_source_triangles[index]
    assert values["--target-triangles"] == expected_targets[index]
    assert values["--minimum-triangles"] == expected_minima[index]
    assert values["--maximum-triangles"] == expected_maxima[index]

for filename, category, target, minimum, source_triangles in (
    ("cat-source.glb", "cat", 15000, 13500, 30000),
    ("prop-source.glb", "prop", 10000, 9000, 20000),
):
    source = input_dir / filename
    source_sidecar_path = Path(f"{source}.json")
    final = output_dir / filename
    proof_path = Path(f"{final}.json")
    assert final.is_file() and proof_path.is_file()
    source_metrics = inspect_glb(source)
    output_metrics = inspect_glb(final)
    assert output_metrics["triangles"] == target
    assert minimum <= output_metrics["triangles"] <= target
    assert 5000 <= output_metrics["triangles"] <= 20000
    assert compare_preservation(source_metrics, output_metrics) == []

    source_sidecar = json.loads(source_sidecar_path.read_text(encoding="utf-8"))
    proof = json.loads(proof_path.read_text(encoding="utf-8"))
    assert proof["schema_version"] == 1
    assert proof["source"]["filename"] == filename
    assert proof["source"]["sha256"] == hashlib.sha256(source.read_bytes()).hexdigest()
    assert proof["source"]["sidecar_sha256"] == hashlib.sha256(source_sidecar_path.read_bytes()).hexdigest()
    assert proof["source"]["provenance"] == {
        key: source_sidecar[key]
        for key in sorted({"service", "task_id", "timestamp_utc", "plan_tier", "prompt", "note"})
    }
    assert proof["derivative"] == {
        "filename": filename,
        "sha256": hashlib.sha256(final.read_bytes()).hexdigest(),
    }
    assert proof["tool"]["name"] == "Blender"
    assert proof["tool"]["version"] == "5.1.2"
    assert proof["tool"]["build_hash"] == "ec6e62d40fa9"
    assert proof["tool"]["operation"] == "collapse-decimate"
    assert re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", proof["tool"]["timestamp_utc"])
    geometry = proof["geometry"]
    assert geometry["category"] == category
    assert geometry["target_triangles"] == target
    assert geometry["accepted_minimum"] == minimum
    assert geometry["accepted_maximum"] == target
    assert geometry["source"]["triangles"] == source_triangles
    assert geometry["output"]["triangles"] == target
    for facts in (geometry["source"], geometry["output"]):
        assert set(facts) == {
            "triangles", "vertices", "primitives", "materials",
            "material_primitives", "images", "embedded_images", "uv_primitives",
            "animations", "cameras", "lights", "skins", "morph_targets",
            "extensions_used", "extensions_required", "world_bounds",
        }

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

# Each table row receives a fresh input/output tree. The shared runner proves
# the named diagnostic, source custody, final-pair behavior, fake reachability,
# curl abstinence, and absence of shell evaluation.
case_root=
input_dir=
output_dir=
manifest=
final_glb=
final_json=

prepare_valid_case() {
  local name=$1
  local triangles=${2:-30000}
  case_root="$tmp/cases/$name"
  input_dir="$case_root/input"
  output_dir="$case_root/output"
  manifest="$case_root/manifest.json"
  final_glb="$output_dir/asset.glb"
  final_json="$output_dir/asset.glb.json"
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
  local before_input before_lines after_lines old_glb_sha='' old_json_sha=''

  before_input=$(fingerprint_tree "$input_dir")
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
setup_symlink_escape() {
  prepare_valid_case symlink-escape
  mv "$input_dir/asset.glb" "$case_root/outside.glb"
  rm -f -- "$input_dir/asset.glb.json"
  ln -s "../outside.glb" "$input_dir/asset.glb"
  write_sidecar "$input_dir/asset.glb" meshy "fixture cat" paid
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
setup_symlink_escape
run_failure_case "source symlink escape" success 'path escapes' no
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
PYTHONDONTWRITEBYTECODE=1 python3 - "$decimate_script" "$tmp/helper-faults" <<'PY'
import hashlib
import importlib.util
import json
import os
import sys
from pathlib import Path
from unittest import mock

script = Path(sys.argv[1])
root = Path(sys.argv[2])
root.mkdir()
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

staged = root / "provenance-failure.json"
record = {"schema_version": 1, "derivative": {"sha256": "0" * 64}}
real_open = Path.open
promote = mock.Mock()

def failing_open(path, *args, **kwargs):
    if path == staged:
        raise OSError("injected staged provenance failure")
    return real_open(path, *args, **kwargs)

with mock.patch.object(Path, "open", failing_open), mock.patch.object(
    module, "promote_pair", promote
):
    try:
        module.write_staged_provenance(staged, record)
    except OSError as exc:
        assert "injected staged provenance failure" in str(exc)
    else:
        raise AssertionError("write_staged_provenance swallowed open failure")
promote.assert_not_called()
assert not staged.exists()
PY

assert_no_external_effects
test -z "$(find "$repo/tests/assets" "$repo/scripts" -type d -name __pycache__ -print -quit)" || \
  die "pipeline test left Python bytecode cache residue"

printf 'glb-decimation pipeline test: pass\n'
