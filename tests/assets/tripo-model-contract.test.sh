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
tmp_root="${TMPDIR:-/tmp}/cat-metro-tripo-model.$$"
mkdir -p "$tmp_root/bin"
trap 'rm -rf "$tmp_root"' EXIT

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

printf '%s\n' \
  '#!/bin/sh' \
  'out=""' \
  'want_out=0' \
  'last=""' \
  'for arg in "$@"; do' \
  '  if [ "$want_out" -eq 1 ]; then out="$arg"; want_out=0' \
  '  elif [ "$arg" = "-o" ]; then want_out=1' \
  '  fi' \
  '  last="$arg"' \
  'done' \
  'if [ -n "$out" ]; then printf glTFstub > "$out"; exit 0; fi' \
  'case "$last" in' \
  '  */generation/text-to-model) printf "%s" '\''{"code":0,"data":{"task_id":"task-123"}}'\'' ;;' \
  '  */tasks/task-123) printf "%s" '\''{"code":0,"data":{"status":"success","progress":100,"output":{"model_url":"https://cdn.example/cat.glb"}}}'\'' ;;' \
  '  *) echo "unexpected curl target: $last" >&2; exit 22 ;;' \
  'esac' > "$tmp_root/bin/curl"
chmod +x "$tmp_root/bin/curl"

check_sidecar_model() { # label, expected model; TRIPO_MODEL_VERSION comes from caller
  local label="$1" expected="$2" out_dir="$tmp_root/out-$1" output rc sidecar actual
  output=$(PATH="$tmp_root/bin:$PATH" TRIPO_API_KEY="sentinel-not-a-real-key" \
    GEN_ASSETS_OUT_DIR="$out_dir" GEN_ASSETS_ACCOUNT_TIER=paid \
    bash "$SRC" tripo "a test cat" "$label.glb" 2>&1); rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "FAIL: $label stubbed generation exited $rc: $output"
    fail=1
    return
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
