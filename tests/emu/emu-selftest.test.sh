#!/usr/bin/env bash
# EMU-RIG criteria 1-3: the emulator self-test helper exists with the proven subcommand
# surface, every adb call is serial-scoped through one wrapper, non-emulator serials are
# refused (the physical Pixel must be untouchable by this tool), no upload/signing
# surface exists, the runbook records the trap ledger, and the committed evidence pack's
# frame hashes match its ARTIFACT.md manifest. Static gates plus one adb-free behavioral
# probe — CI has no emulator, and the probe exits inside the guard before any adb path.
# Hardened per the #89 round-1 review: the refusal is EXECUTED, not grepped (F1); the
# adb-custody gate counts occurrences across the whole stripped file with a boundary
# class that catches $( ), backticks, absolute paths, and assignments (F2); positive
# gates run on a view with full-line AND trailing comments removed (F7 — prose in
# comments must never satisfy a gate); manifest rows tie each sha to its filename on
# the same line (F9). Denylists still run on the RAW source, where a prose
# false-positive is the desirable failure direction.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "emu-selftest.test.sh: FAIL — $*" >&2; exit 1; }

src="scripts/emu-selftest.sh"
runbook="docs/runbooks/emulator-selftest.md"
pack="evals/results/device/emu-gameplay-pass-2026-08-14"

[ -f "$src" ] || fail "helper script is missing"
[ -x "$src" ] || fail "helper script is not executable"

# Behavioral refusal probe (F1): a non-emulator serial must be refused with exit 2
# BEFORE any adb invocation. The deliberately-nonexistent serial means that even a
# future guard regression cannot reach real hardware from this probe.
probe_rc=0
probe_out="$(EMU_SERIAL="pixel-nonexistent-0000" bash "$src" status 2>&1)" || probe_rc=$?
[ "$probe_rc" -eq 2 ] || fail "non-emulator serial was not refused with exit 2 (got $probe_rc)"
grep -q "refusing non-emulator serial" <<<"$probe_out" \
  || fail "refusal did not print the refusal message"

# Guard-before-dispatch (F1-B): the case guard must appear before the subcommand
# dispatch in the source order, so no subcommand path can run ahead of it.
stripped="$(sed -e '/^[[:space:]]*#/d' -e 's/[[:space:]]#.*//' "$src")"
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

# Serial custody (F2): count every occurrence of the token 'adb' across the whole
# stripped file — bounded by non-word/non-path characters so $(adb, `adb, /usr/bin/adb,
# X=adb and same-line repeats are all counted. Exactly one may exist: the wrapper.
adb_occurrences=$(printf ' %s ' "$stripped" | tr '\n' ' ' | grep -oE '[^A-Za-z0-9_]adb[^A-Za-z0-9_]' | wc -l | tr -d ' ')
[ "$adb_occurrences" -eq 1 ] || fail "expected exactly one adb occurrence (the wrapper), found $adb_occurrences"
has 'adb -s "\$EMU_SERIAL"' || fail "the adb wrapper is not serial-scoped"
has 'emulator-\*' || fail "the emulator-only serial allowlist is missing"

# No upload or signing surface, ever (raw source; matches are failures outright).
grep -Eiq 'fastlane|supply|keystore|apksigner|jarsigner|play.*upload' "$src" \
  && fail "the self-test helper must carry no upload or signing surface"

[ -f "$runbook" ] || fail "runbook is missing"
for token in JAVA_HOME swiftshader_indirect force-stop acceleration 2G0YC5ZF7Z056Q 'disk.dataPartition.size'; do
  grep -qi -- "$token" "$runbook" || fail "runbook trap ledger lacks '$token'"
done
# The runbook must document the full subcommand surface (F5/F10): every dispatch
# subcommand by name, and rotation via the helper rather than raw adb.
for sub in boot install launch bounce coldstart frame tap rotate-landscape rotate-portrait status; do
  grep -q -- "$sub" "$runbook" || fail "runbook does not document subcommand '$sub'"
done
grep -q 'emu-selftest.sh rotate-' "$runbook" \
  || fail "runbook does not route rotation through the helper"

[ -f "$pack/ARTIFACT.md" ] || fail "evidence ARTIFACT.md is missing"
png_count=0
for png in "$pack"/*.png; do
  [ -f "$png" ] || fail "evidence pack has no frames"
  png_count=$((png_count + 1))
  sha=$(shasum -a 256 "$png" | cut -d' ' -f1)
  base=$(basename "$png")
  # F9: the sha and ITS filename must share a manifest line — a swapped or misnamed
  # frame must not be certifiable by a sha that merely appears elsewhere.
  grep -E "$sha.*$base|$base.*$sha" "$pack/ARTIFACT.md" >/dev/null \
    || fail "frame $base and sha $sha do not share a manifest line in ARTIFACT.md"
done
[ "$png_count" -ge 9 ] || fail "expected at least 9 evidence frames, found $png_count"

echo "emu-selftest.test.sh: PASS"
