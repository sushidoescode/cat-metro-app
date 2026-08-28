#!/usr/bin/env bash
# EMU-RIG criteria 1-3: the emulator self-test helper exists with the proven subcommand
# surface, every adb call is serial-scoped through one wrapper, non-emulator serials are
# refused (the physical Pixel must be untouchable by this tool), no upload/signing
# surface exists, and the runbook records the trap ledger. Static gates plus one adb-free
# behavioral probe — CI has no emulator, and the probe exits inside the guard before any
# adb path. This test does not claim a current emulator run or gameplay render.
# Hardened per the #89 round-1 review: the refusal is EXECUTED, not grepped (F1); the
# adb-custody gate counts occurrences across the whole stripped file with a boundary
# class that catches $( ), backticks, absolute paths, and assignments (F2); positive
# gates run on a view with full-line AND trailing comments removed (F7 — prose in
# comments must never satisfy a gate). Denylists still run on the RAW source,
# where a prose false-positive is the desirable failure direction.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "emu-selftest.test.sh: FAIL — $*" >&2; exit 1; }

src="scripts/emu-selftest.sh"
runbook="docs/runbooks/emulator-selftest.md"

[ -f "$src" ] || fail "helper script is missing"
[ -x "$src" ] || fail "helper script is not executable"

# Behavioral refusal probe (F1, hardened per R2-3): a non-emulator serial must be
# refused with exit 2 BEFORE any PATH-resolved adb invocation — asserted by observation,
# not by source order: a shim adb sits first on PATH and records a sentinel if the script
# reaches a PATH-resolved adb. (An absolute-path adb wrapper would slip this shim, but
# the adb-occurrence counter below still counts it — belt and suspenders.) The
# deliberately-nonexistent serial means even a guard regression cannot reach real hardware.
shimdir="$(mktemp -d "${TMPDIR:-/tmp}/emushim.XXXXXX")"
sentinel="$shimdir/adb-was-invoked"
printf '#!/bin/sh\ntouch "%s"\nexit 0\n' "$sentinel" > "$shimdir/adb"
chmod +x "$shimdir/adb"
# F-A: self-test the shim BEFORE trusting its silence — a noexec TMPDIR (or a chmod that
# no-ops) would leave the shim unrunnable and the probe would fail OPEN, silently losing
# the adb-before-refusal detector. Prove the instrument works (runs AND records), then
# reset it. The explicit if-guard reports the reason instead of letting set -e short it.
if ! "$shimdir/adb" >/dev/null 2>&1 || [ ! -e "$sentinel" ]; then
  fail "probe shim is non-functional — cannot verify adb-before-refusal"
fi
rm -f "$sentinel"
probe_rc=0
probe_out="$(EMU_SERIAL="pixel-nonexistent-0000" ANDROID_SDK_ROOT="$shimdir/no-sdk" \
  PATH="$shimdir:$PATH" bash "$src" status 2>&1)" || probe_rc=$?
[ "$probe_rc" -eq 2 ] || fail "non-emulator serial was not refused with exit 2 (got $probe_rc)"
grep -q "refusing non-emulator serial" <<<"$probe_out" \
  || fail "refusal did not print the refusal message"
[ ! -e "$sentinel" ] || fail "the script invoked adb before refusing the serial"
rm -rf "$shimdir"

# Guard-before-dispatch (F1-B): source-order belt kept alongside the behavioral proof.
# Two views (R2-1): 'stripped' (full-line AND trailing comments removed) serves the
# POSITIVE gates only; the adb-custody counter runs on 'codeview' (full-line comment
# removal ONLY), because the trailing-comment stripper is quote-naive — a quoted '#'
# would hide the rest of a real code line from the counter. On codeview a trailing
# comment mentioning adb costs a false RED, which is the desirable failure direction.
stripped="$(sed -e '/^[[:space:]]*#/d' -e 's/[[:space:]]#.*//' "$src")"
codeview="$(sed -e '/^[[:space:]]*#/d' "$src")"
has()  { grep -q  "$1" <<<"$stripped"; }
hasE() { grep -Eq "$1" <<<"$stripped"; }
line_of() { grep -n "$1" <<<"$stripped" | head -1 | cut -d: -f1; }
guard_line=$(line_of 'refusing non-emulator serial')
dispatch_line=$(line_of '^case "\$cmd" in')
[ -n "$guard_line" ] && [ -n "$dispatch_line" ] || fail "guard or dispatch not found"
[ "$guard_line" -lt "$dispatch_line" ] || fail "serial guard does not precede the dispatch"

for sub in boot install launch bounce coldstart frame tap rotate-landscape rotate-portrait status; do
  hasE "^[[:space:]]*${sub}\)" || fail "subcommand '$sub' is missing from the dispatch"
done

# Serial custody (F2, view fixed per R2-1): count every occurrence of the token 'adb'
# across the CODE view (quote-safe: only full-line comments removed) — bounded by
# non-word characters so $(adb, `adb, /usr/bin/adb, X=adb and same-line repeats are
# all counted, and a quoted '#' cannot hide a call site. Exactly one may exist: the
# wrapper. (The count message undercounts ADJACENT tokens sharing one boundary char —
# R2-9 — but any extra literal token still pushes the count above 1, which is what
# gates.)
adb_occurrences=$(printf ' %s ' "$codeview" | tr '\n' ' ' | grep -oE '[^A-Za-z0-9_]adb[^A-Za-z0-9_]' | wc -l | tr -d ' ')
[ "$adb_occurrences" -eq 1 ] || fail "expected exactly one adb occurrence (the wrapper), found $adb_occurrences"
has 'adb -s "\$EMU_SERIAL"' || fail "the adb wrapper is not serial-scoped"
has 'emulator-\*' || fail "the emulator-only serial allowlist is missing"

# Condition-6 controls are gated on the CONTROL FORM, not its error string (F-B: a gate
# that greps only the message lets a maintainer keep the message and defang the control).
# Each check pins the exact code shape a defang mutation would have to alter.
has ' -port "\$port" -no-window' || fail "boot does not pass EMU_SERIAL's derived port to the launcher"
hasE 'case "\$port" in \*\[!0-9\]' || fail "boot does not reject an underivable serial's port"
# Both boot waits are bounded by the literal '-le 60' guard; unbounding either (D1/D2 raise
# the number) drops the count below 2.
bound_count=$(grep -cE '\[ "\$tries" -le 60 \]' <<<"$stripped" || true)
[ "$bound_count" -eq 2 ] || fail "boot's two 180s waits are not both bounded at -le 60 (found $bound_count)"
sed -n '/rotate-portrait)/,/;;/p' <<<"$stripped" | grep -q 'accelerometer_rotation 1' \
  || fail "rotate-portrait does not enable accelerometer_rotation"
# The tap guard must validate the ACTUAL args "${1}${2}" (D3 defangs by testing a literal).
hasE 'case "\$\{1\}\$\{2\}" in \*\[!0-9\]' || fail "tap does not validate its own coordinate args"

# No upload or signing surface, ever (raw source; matches are failures outright).
grep -Eiq 'fastlane|supply|keystore|apksigner|jarsigner|play.*upload' "$src" \
  && fail "the self-test helper must carry no upload or signing surface"

[ -f "$runbook" ] || fail "runbook is missing"
for token in JAVA_HOME swiftshader_indirect force-stop acceleration 2G0YC5ZF7Z056Q 'disk.dataPartition.size'; do
  grep -qi -- "$token" "$runbook" || fail "runbook trap ledger lacks '$token'"
done
# The runbook must document the full subcommand surface (F5/F10, tightened per R2-6):
# every dispatch subcommand as a backticked code token — incidental prose ("first
# launch", "boots an Android") no longer satisfies it — and rotation via the helper.
for sub in boot install launch bounce coldstart frame tap rotate-landscape rotate-portrait status; do
  grep -q -- '`'"$sub" "$runbook" || fail "runbook does not document subcommand '$sub' as a code token"
done
grep -q 'emu-selftest.sh rotate-' "$runbook" \
  || fail "runbook does not route rotation through the helper"

echo "emu-selftest.test.sh: PASS"
