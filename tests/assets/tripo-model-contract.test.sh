#!/usr/bin/env bash
# PR #93 regression pin: Tripo v3 rejects a create request without `model`, and the
# provenance sidecar must record the exact model sent. Removing the required field,
# ignoring the override, or letting request/provenance resolution drift turns this RED.
# The real script runs end to end; only curl's external HTTP/download boundary is faked.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 1

SRC="${1:-scripts/gen-assets.sh}"
DEFAULT_MODEL="v3.1-20260211"
OVERRIDE_MODEL="v2.5-20250123"
fail=0
umask 077
tmp_root=$(python3 -c '
import os, tempfile
print(tempfile.mkdtemp(prefix="cat-metro-tripo-model.", dir=os.environ.get("TMPDIR") or None))
' 2>/dev/null) || { echo "FAIL: could not create private test directory"; exit 1; }
[ -n "$tmp_root" ] && [ -d "$tmp_root" ] \
  || { echo "FAIL: private test directory is unavailable"; exit 1; }
trap 'rm -rf -- "$tmp_root"' EXIT
mkdir "$tmp_root/bin" || { echo "FAIL: could not create curl-stub directory"; exit 1; }

check_dry_run_model() { # label, expected model; TRIPO_MODEL_VERSION comes from caller
  local label="$1" expected="$2" output rc body actual
  output=$(bash "$SRC" tripo "a test cat" "$label.glb" --dry-run 2>&1); rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "FAIL: $label dry-run exited $rc"
    fail=1
    return
  fi
  body=$(printf '%s\n' "$output" | sed -n 's/^  body: //p' | head -1)
  actual=$(printf '%s' "$body" | python3 -c '
import json, sys
try:
    print(json.load(sys.stdin)["model"])
except Exception:
    raise SystemExit(1)
' 2>/dev/null) || actual="<missing>"
  if [ "$actual" = "$expected" ]; then
    echo "ok: $label request model=$actual"
  else
    echo "FAIL: $label request model=$actual (want $expected)"
    fail=1
  fi
}

unset TRIPO_MODEL_VERSION
check_dry_run_model default "$DEFAULT_MODEL"
TRIPO_MODEL_VERSION="$OVERRIDE_MODEL" check_dry_run_model override "$OVERRIDE_MODEL"
unset TRIPO_MODEL_VERSION

stub_curl="$tmp_root/bin/curl"
if ! printf '%s\n' \
  '#!/bin/sh' \
  'out=""' \
  'want_out=0' \
  'data=""' \
  'want_data=0' \
  'last=""' \
  'for arg in "$@"; do' \
  '  if [ "$want_out" -eq 1 ]; then out="$arg"; want_out=0' \
  '  elif [ "$want_data" -eq 1 ]; then data="$arg"; want_data=0' \
  '  elif [ "$arg" = "-o" ]; then want_out=1' \
  '  elif [ "$arg" = "-d" ]; then want_data=1' \
  '  fi' \
  '  last="$arg"' \
  'done' \
  'if [ -n "$out" ]; then printf glTFstub > "$out"; exit 0; fi' \
  'case "$last" in' \
  '  */generation/text-to-model)' \
  '    [ -n "${TRIPO_STUB_CAPTURE:-}" ] || exit 23' \
  '    printf "%s" "$data" > "$TRIPO_STUB_CAPTURE" || exit 24' \
  '    printf "%s" '\''{"code":0,"data":{"task_id":"task-123"}}'\'' ;;' \
  '  */tasks/task-123) printf "%s" '\''{"code":0,"data":{"status":"success","progress":100,"output":{"model_url":"https://cdn.example/cat.glb"}}}'\'' ;;' \
  '  *) echo "unexpected curl target: $last" >&2; exit 22 ;;' \
  'esac' > "$stub_curl"; then
  echo "FAIL: could not write curl stub"
  exit 1
fi
chmod +x "$stub_curl" || { echo "FAIL: could not make curl stub executable"; exit 1; }
resolved_curl=$(PATH="$tmp_root/bin:$PATH" command -v curl 2>/dev/null) || resolved_curl=""
[ "$resolved_curl" = "$stub_curl" ] \
  || { echo "FAIL: private curl stub is not the resolved curl"; exit 1; }

check_sidecar_model() { # label, expected model; TRIPO_MODEL_VERSION comes from caller
  local label="$1" expected="$2" out_dir="$tmp_root/out-$1" capture output rc sidecar actual request_model
  capture="$tmp_root/request-$label.json"
  output=$(PATH="$tmp_root/bin:$PATH" TRIPO_API_KEY="sentinel-not-a-real-key" \
    TRIPO_STUB_CAPTURE="$capture" \
    GEN_ASSETS_OUT_DIR="$out_dir" GEN_ASSETS_ACCOUNT_TIER=paid \
    bash "$SRC" tripo "a test cat" "$label.glb" 2>&1); rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "FAIL: $label stubbed generation exited $rc: $output"
    fail=1
    return
  fi
  request_model=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["model"])' \
    "$capture" 2>/dev/null) || request_model="<missing>"
  if [ "$request_model" = "$expected" ]; then
    echo "ok: $label live POST model=$request_model"
  else
    echo "FAIL: $label live POST model=$request_model (want $expected)"
    fail=1
  fi
  sidecar="$out_dir/$label.glb.json"
  actual=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["note"])' \
    "$sidecar" 2>/dev/null) || actual="<missing>"
  if [ "$actual" = "tripo model=$expected" ]; then
    echo "ok: $label sidecar records exact request model=$expected"
  else
    echo "FAIL: $label sidecar note=$actual (want tripo model=$expected)"
    fail=1
  fi
}

unset TRIPO_MODEL_VERSION
check_sidecar_model default "$DEFAULT_MODEL"
TRIPO_MODEL_VERSION="$OVERRIDE_MODEL" check_sidecar_model override "$OVERRIDE_MODEL"
unset TRIPO_MODEL_VERSION

[ "$fail" -eq 0 ] && echo "tripo-model-contract: PASS" || echo "tripo-model-contract: FAIL"
exit "$fail"
