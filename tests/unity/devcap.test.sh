#!/usr/bin/env bash
# CM-C3-DEVCAP harness wrapper (criteria 1-static, 4, 5; criterion 6 is this file's own
# discovery + the untouched-gates evidence in the PR). Static/fixture legs only — the PlayMode
# legs (criteria 1-pure, 2, 3) run through editmode.test.sh's editor half. Fail-closed on every
# missing scan root; every guard pattern is proven to FIRE against a negative fixture (the
# save.test.sh:38-40 precedent).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "devcap.test.sh: FAIL — $1"; exit 1; }
cap="unity/Assets/Scripts/Bootstrap/DevCapture"
badfx="tests/fixtures/devcap-bad"
goodfx="tests/fixtures/devcap"

# --- criterion 1 static: the capture may never grow a clock. This grep IS A-C3-6's enforcement:
# time reaches the capture only as FrameRecord copies, so every clock-shaped token is banned
# under the capture tree (comments included, by design — reword prose, never weaken the check).
CLOCK='Time\.|realtimeSinceStartup|DateTime|DateTimeOffset|Stopwatch|Environment\.TickCount|frameCount'
[ -d "$cap" ] || fail "criterion 1: $cap missing (fail-closed)"
hits=$(grep -rEn --include='*.cs' "$CLOCK" "$cap" 2>/dev/null || true)
[ -z "$hits" ] || fail "criterion 1: clock token under DevCapture: $hits"
[ -f "$badfx/Banned.cs" ] || fail "criterion 1: negative fixture missing (fail-closed)"
grep -Eq "$CLOCK" "$badfx/Banned.cs" \
  || fail "criterion 1: clock pattern failed to fire on the negative fixture"

# --- criterion 4: zero release footprint, proven by a scan that fires ---
# Rule 1: every .cs under the capture dir is WHOLLY wrapped in the dev guard, directives balanced.
# Rule 2: every DevFrameCapture|FrameLogCsv reference elsewhere sits inside such a region.
# Fixture mode passes the same dir as both roots (deliberately: both rules must fire there);
# real mode excludes the capture dir from rule 2 since rule 1 already owns it.
scan() { python3 - "$1" "$2" <<'PYEOF'
import os, re, sys
capdir, refsroot = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
fixture_mode = capdir == refsroot
GUARD = re.compile(r'^#if\s+DEVELOPMENT_BUILD\s*\|\|\s*UNITY_EDITOR\s*$')
SYM = re.compile(r'DevFrameCapture|FrameLogCsv')
viol = 0
def cs_files(root):
    for r, _, fs in os.walk(root):
        for f in sorted(fs):
            if f.endswith('.cs'):
                yield os.path.join(r, f)
# rule 1: whole-file guard over the capture dir
for path in cs_files(capdir):
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    sig = [l.strip() for l in lines if l.strip() and not l.strip().startswith('//')]
    if not sig or not GUARD.match(sig[0]) or sig[-1] != '#endif':
        viol += 1; print('unwrapped: ' + path)
    depth = 0; underflow = False
    for l in lines:
        s = l.strip()
        if s.startswith('#if'): depth += 1
        elif s.startswith('#endif'): depth -= 1
        if depth < 0: underflow = True
    if depth != 0 or underflow:
        viol += 1; print('unbalanced: ' + path)
# rule 2: references outside the capture dir must sit inside a dev-guard region
for path in cs_files(refsroot):
    if not fixture_mode and (path == capdir or path.startswith(capdir + os.sep)):
        continue
    depth = 0; guard_depth = 0
    for n, l in enumerate(open(path, encoding='utf-8', errors='replace'), 1):
        s = l.strip()
        if s.startswith('#if'):
            depth += 1
            if 'DEVELOPMENT_BUILD' in s and 'UNITY_EDITOR' in s and guard_depth == 0:
                guard_depth = depth
        elif s.startswith('#endif'):
            if guard_depth == depth: guard_depth = 0
            depth -= 1
        if SYM.search(l) and guard_depth == 0:
            viol += 1; print('unguarded reference: %s:%d' % (path, n))
print('VIOLATIONS=%d' % viol)
PYEOF
}
[ -d "$badfx" ] || fail "criterion 4: negative-fixture dir missing (fail-closed)"
real_out=$(scan "$cap" "unity/Assets/Scripts") || fail "criterion 4: scanner errored on the real tree"
real_viol=$(echo "$real_out" | sed -n 's/^VIOLATIONS=//p')
[ "$real_viol" = "0" ] || fail "criterion 4: guard violations in the real tree: $real_out"
fx_out=$(scan "$badfx" "$badfx") || fail "criterion 4: scanner errored on the fixture"
fx_viol=$(echo "$fx_out" | sed -n 's/^VIOLATIONS=//p')
[ -n "$fx_viol" ] && [ "$fx_viol" -ge 2 ] \
  || fail "criterion 4: the scan must report >=2 planted violations, got '$fx_viol'"
# monetization tokens: zero under the capture tree, pattern proven live against save-bad
MON='/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds|AnalyticsQueue'
mon=$(grep -rEn --include='*.cs' "$MON" "$cap" 2>/dev/null || true)
[ -z "$mon" ] || fail "criterion 4: monetization/analytics token under DevCapture: $mon"
grep -rEq '/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds' tests/fixtures/save-bad \
  || fail "criterion 4: monetization pattern failed to fire on the save-bad fixture"

# --- criterion 5: the reducer reproduces the editor tables from a pulled CSV ---
[ -f scripts/devcap-report.sh ] || fail "criterion 5: scripts/devcap-report.sh missing (fail-closed)"
[ -f "$goodfx/sample-framelog.csv" ] && [ -f "$goodfx/sample-expected.txt" ] \
  && [ -f "$goodfx/sample-short.csv" ] || fail "criterion 5: golden fixtures missing (fail-closed)"
out=$(bash scripts/devcap-report.sh "$goodfx/sample-framelog.csv") \
  || fail "criterion 5: reducer exited non-zero on the golden fixture"
if ! diff <(printf '%s\n' "$out") "$goodfx/sample-expected.txt" > /dev/null; then
  diff <(printf '%s\n' "$out") "$goodfx/sample-expected.txt" | head -10
  fail "criterion 5: reducer output drifts from the hand-computed golden"
fi
if shortout=$(bash scripts/devcap-report.sh "$goodfx/sample-short.csv" 2>&1); then
  fail "criterion 5: a 19-cycle capture must exit non-zero (p95 is over 20)"
fi
echo "$shortout" | grep -q "19" \
  || fail "criterion 5: the short-capture failure must print the cycle counts"

echo "devcap.test.sh: OK (1-static, 4, 5)"
exit 0
