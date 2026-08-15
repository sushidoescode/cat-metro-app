#!/usr/bin/env bash
# In-repo regression pin for the LIVE failure of 2026-08-14: meshy_poll wrote progress to
# STDOUT, its own data channel, so the refine stage captured "  meshy task ...%" in front of
# the JSON and every Meshy asset died with "no GLB url on refine task" (7/7). Keyless: the
# network layer is stubbed. cd to the repo root so it runs from anywhere.
cd "$(git rev-parse --show-toplevel)" 2>/dev/null || true
# Proves the meshy_poll stdout-contract fix WITHOUT keys or network: stub api_curl to return
# IN_PROGRESS first and SUCCEEDED second, capture stdout exactly as meshy_generate's refine
# stage does, and assert stdout is PURE JSON with the progress line on stderr.
# NOTE: api_curl runs inside $( ) — a subshell — so the call counter must live in a FILE,
# not a variable (an in-memory counter silently resets every call).
set -uo pipefail
SRC="${1:-scripts/gen-assets.sh}"

MESHY_API_KEY="sentinel-key-not-used"
MESHY_BASE="https://example.invalid"
MESHY_POLL_INTERVAL=0
MESHY_STAGE_TIMEOUT=5
eval "$(sed -n '/^redact()/,/^}/p;/^say()/,/^}/p;/^err()/,/^}/p;/^json_get()/,/^}/p;/^meshy_poll()/,/^}/p' "$SRC")"

CNT="${TMPDIR:-/tmp}/.mp_cnt.$$"; echo 0 > "$CNT"
ERRF="${TMPDIR:-/tmp}/.mp_err.$$"
api_curl() {
  local n; n=$(cat "$CNT"); n=$((n + 1)); echo "$n" > "$CNT"
  if [ "$n" -le 1 ]; then
    printf '%s' '{"status":"IN_PROGRESS","progress":40}'
  else
    printf '%s' '{"status":"SUCCEEDED","progress":100,"model_urls":{"glb":"https://cdn.example/x.glb"}}'
  fi
}
export CNT

fail=0
final=$(meshy_poll "task-123" 2>"$ERRF"); rc=$?
[ "$rc" -eq 0 ] || { echo "FAIL: meshy_poll returned $rc"; fail=1; }

if printf '%s' "$final" | python3 -c 'import json,sys; json.load(sys.stdin)' 2>/dev/null; then
  echo "ok: captured stdout is PURE JSON (data channel clean)"
else
  echo "FAIL: stdout not parseable — progress leaked into the data channel. First line:"
  printf '%s\n' "$final" | head -1
  fail=1
fi

if glb=$(printf '%s' "$final" | json_get model_urls.glb); then
  echo "ok: model_urls.glb extracted -> $glb"
else
  echo "FAIL: could not extract model_urls.glb (this IS the live failure)"; fail=1
fi

if grep -q "meshy task task-123" "$ERRF"; then
  echo "ok: progress line went to STDERR (operator sees it, parser does not)"
else
  echo "FAIL: progress line not on stderr"; fail=1
fi

rm -f "$CNT" "$ERRF"
[ "$fail" -eq 0 ] && echo "meshy-poll-contract: PASS" || echo "meshy-poll-contract: FAIL"
exit "$fail"
