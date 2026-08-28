#!/usr/bin/env bash
# Behavioral check for the solution gate's two-run summary wiring.
# A fake dotnet keeps this fast while the real wrapper, cache bypass, parser,
# marker counting, and final success/failure decision all execute unchanged.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

BASE="${TMPDIR:-/tmp}"
BASE="${BASE%/}"
SCENARIO="$(mktemp -d "$BASE/cm-suite-summary-XXXXXX")" || exit 1
SCENARIO="$(cd "$SCENARIO" && pwd -P)"
trap 'rm -rf -- "$SCENARIO"' EXIT
mkdir -p "$SCENARIO/bin"

REAL_PYTHON="$(command -v python3)"
[ -n "$REAL_PYTHON" ] || { echo "solution-suite-summary: FAIL — python3 missing"; exit 1; }

printf '%s\n' \
  '#!/usr/bin/env bash' \
  'if [ "${1:-}" = "tests/suite/dotnet-summary.py" ]; then' \
  '  "$CM_SUITE_REAL_PYTHON" "$@"' \
  '  rc=$?' \
  '  [ "${CM_SUITE_PARSER_WARNING:-0}" = "1" ] && echo "unexpected parser warning"' \
  '  exit "$rc"' \
  'fi' \
  'exec "$CM_SUITE_REAL_PYTHON" "$@"' \
  > "$SCENARIO/bin/python3"
chmod +x "$SCENARIO/bin/python3"

printf '%s\n' \
  '#!/usr/bin/env bash' \
  'run_number="$(wc -l < "$CM_SUITE_FAKE_RUNS" | tr -d " ")"' \
  'printf "run\n" >> "$CM_SUITE_FAKE_RUNS"' \
  'if [ "$run_number" = "0" ]; then' \
  '  printf "%s\n" "$CM_SUITE_FAKE_OUTPUT1"' \
  '  exit "${CM_SUITE_FAKE_RC1:-0}"' \
  'fi' \
  'printf "%s\n" "$CM_SUITE_FAKE_OUTPUT2"' \
  'exit "${CM_SUITE_FAKE_RC2:-0}"' \
  > "$SCENARIO/bin/dotnet"
chmod +x "$SCENARIO/bin/dotnet"

RUNS="$SCENARIO/runs"
hash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
green5="$(printf '%s\n' \
  "REPLAY_HASH=$hash" \
  'SOLVER_LOG=empty' \
  'Test Run Successful.' \
  'Total tests: 5' \
  '     Passed: 5' \
  ' Total time: 0.2 Seconds')"
green4="$(printf '%s\n' \
  "REPLAY_HASH=$hash" \
  'SOLVER_LOG=empty' \
  'Test Run Successful.' \
  'Total tests: 4' \
  '     Passed: 4' \
  ' Total time: 0.2 Seconds')"
zero="$(printf '%s\n' \
  "REPLAY_HASH=$hash" \
  'SOLVER_LOG=empty' \
  'Test Run Successful.' \
  'Total tests: 0' \
  ' Total time: 0.2 Seconds')"

pass=0
fail=0
ok()  { pass=$((pass + 1)); echo "  ok   — $1"; }
bad() { fail=$((fail + 1)); echo "  FAIL — $1"; }

invoke_wrapper() {
  : > "$RUNS"
  WRAPPER_OUT="$(
    PATH="$SCENARIO/bin:$PATH" \
    CM_SUITE_REAL_PYTHON="$REAL_PYTHON" \
    CM_SUITE_PARSER_WARNING="${3:-0}" \
    CM_SUITE_FAKE_RUNS="$RUNS" \
    CM_SUITE_FAKE_OUTPUT1="$1" \
    CM_SUITE_FAKE_OUTPUT2="$2" \
    CATMETRO_NO_TEST_CACHE=1 \
    bash tests/suite/solution-suite.test.sh 2>&1
  )"
  WRAPPER_RC=$?
}

invoke_wrapper "$green5" "$green5"
run_count="$(wc -l < "$RUNS" | tr -d ' ')"
if [ "$WRAPPER_RC" -eq 0 ] \
  && [ "$run_count" = "2" ] \
  && printf '%s\n' "$WRAPPER_OUT" | grep -q '^solution-suite: OK — suite green (5 passed),'; then
  ok "two detailed green artifacts produce one proven green"
else
  bad "detailed green: rc=$WRAPPER_RC runs=$run_count output='$WRAPPER_OUT'"
fi

invoke_wrapper "$green5" "$zero"
if [ "$WRAPPER_RC" -ne 0 ] \
  && printf '%s\n' "$WRAPPER_OUT" | grep -q 'run2: dotnet-summary: FAIL'; then
  ok "a vacuous second run fails closed"
else
  bad "vacuous second run: rc=$WRAPPER_RC output='$WRAPPER_OUT'"
fi

invoke_wrapper "$green5" "$green4"
if [ "$WRAPPER_RC" -ne 0 ] \
  && printf '%s\n' "$WRAPPER_OUT" | grep -q 'test counts differ across independent processes'; then
  ok "different positive counts across runs are rejected"
else
  bad "count mismatch: rc=$WRAPPER_RC output='$WRAPPER_OUT'"
fi

invoke_wrapper "$green5" "$green5" 1
if [ "$WRAPPER_RC" -ne 0 ] \
  && printf '%s\n' "$WRAPPER_OUT" | grep -q 'parser output was not exactly five integer fields'; then
  ok "identical extra parser output fails closed"
else
  bad "parser noise: rc=$WRAPPER_RC output='$WRAPPER_OUT'"
fi

if [ "$fail" -eq 0 ]; then
  echo "solution-suite-summary: OK — $pass wrapper behaviors passed"
fi
exit "$fail"
