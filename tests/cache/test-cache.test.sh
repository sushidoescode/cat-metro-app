#!/usr/bin/env bash
# Self-test for scripts/test_cache.py.
#
# A test-result cache that is ever wrong is worse than no cache: it reports
# green for code that was never run. So this suite is written to try to make
# the cache lie, and asserts it does not.
#
# Isolation by construction: every scenario mints its OWN temp dir, git repo,
# fake toolchain and cache directory via `mktemp -d` with an EXPLICIT template
# under $TMPDIR. Nothing is ever reset or deleted -- scenarios cannot leak into
# each other because they never share a path. (On this macOS a bare `mktemp -d`
# ignores $TMPDIR and lands somewhere the agent sandbox cannot write, so the
# template is not optional.)
#
# No real `dotnet` runs here: a fake one on PATH counts its own invocations,
# which is what lets us assert "the command ran exactly once" -- the only
# assertion that actually proves a cache hit.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

HELPER="$PWD/scripts/test_cache.py"
BASE="${TMPDIR:-/tmp}"
BASE="${BASE%/}"
pass=0
fail=0

ok()  { pass=$((pass + 1)); echo "  ok   — $1"; }
bad() { fail=$((fail + 1)); echo "  FAIL — $1"; }

[ -f "$HELPER" ] || { echo "test-cache: FAIL — $HELPER missing (fail-closed)"; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "test-cache: FAIL — python3 missing"; exit 1; }

# --- scenario factory -------------------------------------------------------
# Mints S (scenario root), REPO, CACHE, RUNS, and a fake `dotnet` on PATH.
new_scenario() {
  S="$(mktemp -d "$BASE/cm-testcache-XXXXXX")" || return 1
  S="$(cd "$S" && pwd -P)"
  REPO="$S/repo"; CACHE="$S/cache"; RUNS="$S/runs"; HOMEDIR="$S/home"
  mkdir -p "$REPO" "$CACHE" "$HOMEDIR" "$S/bin"
  : > "$RUNS"
  printf 'v1\n' > "$S/sdk-version"
  printf 'suite green\nREPLAY_HASH=abc\n' > "$S/stdout"
  printf '0\n' > "$S/rc"

  cat > "$S/bin/dotnet" <<'FAKE'
#!/usr/bin/env bash
case "${1:-}" in
  --version) cat "$FAKE_SDK"; exit 0 ;;
  --info)    echo "SDK Version: $(cat "$FAKE_SDK")"; exit 0 ;;
  workload)  echo "no workloads installed"; exit 0 ;;
  test)      echo run >> "$FAKE_RUNS"
             [ -n "${FAKE_SLOW:-}" ] && sleep 0.3
             cat "$FAKE_STDOUT"
             exit "$(cat "$FAKE_RC")" ;;
esac
exit 0
FAKE
  chmod +x "$S/bin/dotnet"

  # A tiny repo: one staged file (exercises the git index path) and one
  # untracked file (exercises the working-tree overlay path).
  git -C "$REPO" init -q
  mkdir -p "$REPO/src"
  printf 'let x = 1\n' > "$REPO/src/main.cs"
  printf '{"locked":true}\n' > "$REPO/packages.lock.json"
  git -C "$REPO" add -A >/dev/null 2>&1
  printf 'untracked\n' > "$REPO/notes.txt"
}

# Run the helper inside the current scenario. Extra args before `--` pass through.
invoke() {
  ( cd "$REPO" && \
    PATH="$S/bin:$PATH" \
    HOME="$HOMEDIR" \
    FAKE_SDK="$S/sdk-version" \
    FAKE_RUNS="$RUNS" \
    FAKE_STDOUT="$S/stdout" \
    FAKE_RC="$S/rc" \
    CATMETRO_TEST_CACHE_DIR="$CACHE" \
    python3 "$HELPER" "$@" -- dotnet test solution 2>/dev/null )
}

runs() { wc -l < "$RUNS" | tr -d ' '; }

# --- 1. identical tree -> hit ----------------------------------------------
new_scenario
a="$(invoke)"; b="$(invoke)"
if [ "$(runs)" = "1" ] && [ "$a" = "$b" ] && [ -n "$a" ]; then
  ok "identical tree: second call is a hit (command ran once, output identical)"
else
  bad "identical tree: expected 1 run, got $(runs) (a='$a' b='$b')"
fi

# --- 2. touched source -> miss ---------------------------------------------
new_scenario
invoke >/dev/null
printf 'let x = 2\n' > "$REPO/src/main.cs"
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "changed tracked source: miss" || bad "changed source: expected 2 runs, got $(runs)"

# --- 3. changed lock file -> miss ------------------------------------------
new_scenario
invoke >/dev/null
printf '{"locked":false}\n' > "$REPO/packages.lock.json"
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "changed packages.lock.json: miss" || bad "changed lock: expected 2 runs, got $(runs)"

# --- 4. changed untracked file -> miss -------------------------------------
new_scenario
invoke >/dev/null
printf 'edited\n' > "$REPO/notes.txt"
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "changed untracked file: miss" || bad "untracked: expected 2 runs, got $(runs)"

# --- 5. changed SDK/toolchain -> miss --------------------------------------
new_scenario
invoke >/dev/null
printf 'v2\n' > "$S/sdk-version"
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "changed SDK version: miss" || bad "SDK bump: expected 2 runs, got $(runs)"

# --- 6. out-of-repo MSBuild policy -> miss (edge case a) --------------------
# Directory.Build.props in an ANCESTOR of the repo root silently changes every
# build. It is invisible to `git status`, so this is the case the old review
# called "unkeyed MSBuild user extensions".
new_scenario
invoke >/dev/null
printf '<Project/>\n' > "$S/Directory.Build.props"
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "out-of-repo Directory.Build.props: miss (edge case a)" \
  || bad "ancestor policy file: expected 2 runs, got $(runs)"

# --- 7. gitignored in-repo policy file -> miss ------------------------------
new_scenario
printf 'Directory.Build.props\n' > "$REPO/.gitignore"
git -C "$REPO" add -A >/dev/null 2>&1
invoke >/dev/null
printf '<Project/>\n' > "$REPO/Directory.Build.props"
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "gitignored Directory.Build.props: miss" \
  || bad "gitignored policy: expected 2 runs, got $(runs)"

# --- 8. empty vs unset workload override -> miss (edge case c) --------------
# `VAR=""` is a different build from `VAR` unset. A truthiness test conflates
# them; the key must not.
new_scenario
invoke >/dev/null
before="$(runs)"
( cd "$REPO" && PATH="$S/bin:$PATH" HOME="$HOMEDIR" FAKE_SDK="$S/sdk-version" \
  FAKE_RUNS="$RUNS" FAKE_STDOUT="$S/stdout" FAKE_RC="$S/rc" \
  CATMETRO_TEST_CACHE_DIR="$CACHE" DOTNETSDK_WORKLOAD_PACK_ROOTS= \
  python3 "$HELPER" -- dotnet test solution >/dev/null 2>&1 )
after="$(runs)"
[ "$before" = "1" ] && [ "$after" = "2" ] \
  && ok "empty-string workload override keys apart from unset (edge case c)" \
  || bad "empty override: expected 1 then 2 runs, got $before then $after"

# --- 9. corrupted cache entry -> miss, not a false hit ---------------------
new_scenario
invoke >/dev/null
entry="$(find "$CACHE" -name '*.json' -type f | head -1)"
if [ -z "$entry" ]; then
  bad "corruption: no cache entry was written"
else
  printf 'this is not json' > "$entry"
  invoke >/dev/null
  [ "$(runs)" = "2" ] && ok "corrupted entry: miss, not a false hit" \
    || bad "corrupted entry: expected 2 runs, got $(runs)"
fi

# --- 10. truncated (partial) entry -> miss ---------------------------------
new_scenario
invoke >/dev/null
entry="$(find "$CACHE" -name '*.json' -type f | head -1)"
if [ -z "$entry" ]; then
  bad "truncation: no cache entry was written"
else
  head -c 20 "$entry" > "$entry.part" && mv "$entry.part" "$entry"
  invoke >/dev/null
  [ "$(runs)" = "2" ] && ok "truncated entry: miss" || bad "truncated: expected 2 runs, got $(runs)"
fi

# --- 11. tampered payload (valid JSON, wrong checksum) -> miss -------------
# The dangerous corruption is the plausible one: well-formed JSON claiming a
# green exit for output that was never produced.
new_scenario
invoke >/dev/null
entry="$(find "$CACHE" -name '*.json' -type f | head -1)"
if [ -z "$entry" ]; then
  bad "tamper: no cache entry was written"
else
  python3 - "$entry" <<'PY'
import base64, json, sys
p = sys.argv[1]
r = json.load(open(p))
r["stdout"] = base64.b64encode(b"FORGED GREEN\n").decode()
json.dump(r, open(p, "w"))
PY
  out="$(invoke)"
  if [ "$(runs)" = "2" ] && ! printf '%s' "$out" | grep -q FORGED; then
    ok "checksum-tampered entry: miss, forged payload never replayed"
  else
    bad "tampered entry: runs=$(runs), output='$out'"
  fi
fi

# --- 12. concurrent writers -> no torn read --------------------------------
new_scenario
FAKE_SLOW=1
for i in 1 2 3 4 5 6 7 8; do
  ( cd "$REPO" && PATH="$S/bin:$PATH" HOME="$HOMEDIR" FAKE_SDK="$S/sdk-version" \
    FAKE_RUNS="$RUNS" FAKE_STDOUT="$S/stdout" FAKE_RC="$S/rc" FAKE_SLOW=1 \
    CATMETRO_TEST_CACHE_DIR="$CACHE" \
    python3 "$HELPER" -- dotnet test solution > "$S/out.$i" 2>/dev/null ) &
done
wait
torn=0
for i in 1 2 3 4 5 6 7 8; do
  diff -q "$S/out.$i" "$S/stdout" >/dev/null 2>&1 || torn=1
done
entry="$(find "$CACHE" -name '*.json' -type f | head -1)"
parses=1
[ -n "$entry" ] && python3 -c 'import json,sys; json.load(open(sys.argv[1]))' "$entry" 2>/dev/null || parses=0
leftovers="$(find "$CACHE" -name '.record.*' -type f | wc -l | tr -d ' ')"
if [ "$torn" = "0" ] && [ "$parses" = "1" ] && [ "$leftovers" = "0" ]; then
  ok "8 concurrent writers: every output intact, entry parses, no temp residue (edge case d)"
else
  bad "concurrency: torn=$torn parses=$parses leftovers=$leftovers"
fi

# --- 13. slots record independent runs -------------------------------------
# determinism.test.sh and solver.test.sh compare a hash across two INDEPENDENT
# processes. Collapsing them to one replayed run would make that assertion
# trivially true, so slots must not share a record.
new_scenario
invoke --slot 1 >/dev/null
invoke --slot 2 >/dev/null
first="$(runs)"
invoke --slot 1 >/dev/null
invoke --slot 2 >/dev/null
if [ "$first" = "2" ] && [ "$(runs)" = "2" ]; then
  ok "slots: two independent runs recorded, both replayed on the second pass"
else
  bad "slots: expected 2 then 2 runs, got $first then $(runs)"
fi

# --- 14. failures are never cached -----------------------------------------
new_scenario
printf '1\n' > "$S/rc"
invoke >/dev/null; rc1=$?
invoke >/dev/null; rc2=$?
if [ "$rc1" = "1" ] && [ "$rc2" = "1" ] && [ "$(runs)" = "2" ]; then
  ok "red run: never cached, re-runs and stays red"
else
  bad "failure caching: rc1=$rc1 rc2=$rc2 runs=$(runs)"
fi

# --- 15. escape hatch -------------------------------------------------------
new_scenario
invoke >/dev/null
( cd "$REPO" && PATH="$S/bin:$PATH" HOME="$HOMEDIR" FAKE_SDK="$S/sdk-version" \
  FAKE_RUNS="$RUNS" FAKE_STDOUT="$S/stdout" FAKE_RC="$S/rc" \
  CATMETRO_TEST_CACHE_DIR="$CACHE" CATMETRO_NO_TEST_CACHE=1 \
  python3 "$HELPER" -- dotnet test solution >/dev/null 2>&1 )
[ "$(runs)" = "2" ] && ok "CATMETRO_NO_TEST_CACHE=1 bypasses a warm cache" \
  || bad "escape hatch: expected 2 runs, got $(runs)"

# --- 16. a command that mutates its own inputs is not recorded -------------
# `dotnet restore` rewriting packages.lock.json is the live example in this
# repo. The result does not belong to the key we computed, so it must not be
# published -- even though the run itself was green.
new_scenario
cat > "$S/bin/dotnet" <<'FAKE'
#!/usr/bin/env bash
case "${1:-}" in
  --version) cat "$FAKE_SDK"; exit 0 ;;
  --info)    echo "SDK Version: $(cat "$FAKE_SDK")"; exit 0 ;;
  workload)  echo "no workloads installed"; exit 0 ;;
  test)      echo run >> "$FAKE_RUNS"
             echo "mutated $(date +%s%N)" > packages.lock.json
             cat "$FAKE_STDOUT"; exit 0 ;;
esac
exit 0
FAKE
chmod +x "$S/bin/dotnet"
invoke >/dev/null
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "input-mutating run: not recorded, re-runs next time" \
  || bad "mutating run: expected 2 runs, got $(runs)"

# --- 17. write-then-restore is still not recorded (stat witness) -----------
# The subtle one: the command rewrites an input, runs against the new bytes,
# then restores the original. A content-only comparison calls that unchanged
# and publishes a result computed against bytes that are no longer there.
new_scenario
cat > "$S/bin/dotnet" <<'FAKE'
#!/usr/bin/env bash
case "${1:-}" in
  --version) cat "$FAKE_SDK"; exit 0 ;;
  --info)    echo "SDK Version: $(cat "$FAKE_SDK")"; exit 0 ;;
  workload)  echo "no workloads installed"; exit 0 ;;
  test)      echo run >> "$FAKE_RUNS"
             original="$(cat src/main.cs)"
             echo "temporarily different" > src/main.cs
             printf '%s' "$original" > src/main.cs
             cat "$FAKE_STDOUT"; exit 0 ;;
esac
exit 0
FAKE
chmod +x "$S/bin/dotnet"
invoke >/dev/null
invoke >/dev/null
[ "$(runs)" = "2" ] && ok "write-then-restore: not recorded (stat witness caught it)" \
  || bad "write-then-restore: expected 2 runs, got $(runs)"

# --- 18. a missing toolchain fails closed ----------------------------------
# PATH keeps python3 and git (the helper needs both) but drops the fake dotnet,
# so the failure under test is "cannot key the toolchain", not "cannot start".
new_scenario
out="$( cd "$REPO" && PATH="/usr/bin:/bin" HOME="$HOMEDIR" \
  CATMETRO_TEST_CACHE_DIR="$CACHE" python3 "$HELPER" -- dotnet test solution 2>&1 )"
rc=$?
if [ "$rc" != "0" ] && [ "$(find "$CACHE" -name '*.json' -type f | wc -l | tr -d ' ')" = "0" ]; then
  ok "absent toolchain: fails closed, records nothing"
else
  bad "absent toolchain: rc=$rc, entries=$(find "$CACHE" -name '*.json' -type f | wc -l | tr -d ' ')"
fi

echo "test-cache.test.sh: $pass passed, $fail failed"
[ "$fail" -eq 0 ]
