#!/usr/bin/env bash
# EMU-RIG criteria 1-3: the emulator self-test helper exists with the proven subcommand
# surface, every adb call is serial-scoped through one wrapper, non-emulator serials are
# refused (the physical Pixel must be untouchable by this tool), no upload/signing
# surface exists, the runbook records the trap ledger, and the committed evidence pack's
# frame hashes match its ARTIFACT.md manifest. Static gates only — CI has no emulator.
# Positive gates run on a view with full-line comments removed (cli-aab-build.test.sh
# F-1 lesson: prose in comments must never satisfy a gate); denylists run on the RAW
# source, where a prose false-positive is the desirable failure direction.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "emu-selftest.test.sh: FAIL — $*" >&2; exit 1; }

src="scripts/emu-selftest.sh"
runbook="docs/runbooks/emulator-selftest.md"
pack="evals/results/device/emu-gameplay-pass-2026-08-14"

[ -f "$src" ] || fail "helper script is missing"
[ -x "$src" ] || fail "helper script is not executable"

stripped="$(sed '/^[[:space:]]*#/d' "$src")"
has()  { grep -q  "$1" <<<"$stripped"; }
hasE() { grep -Eq "$1" <<<"$stripped"; }

for sub in boot install launch bounce coldstart frame tap rotate-landscape rotate-portrait status; do
  hasE "^[[:space:]]*${sub}\)" || fail "subcommand '$sub' is missing from the dispatch"
done

# Serial custody: exactly one wrapper owns adb, and it scopes by EMU_SERIAL.
adb_lines=$(grep -cE '(^|[;&|[:space:]])adb([[:space:]]|$)' <<<"$stripped" || true)
[ "$adb_lines" -eq 1 ] || fail "expected exactly one adb call site (the wrapper), found $adb_lines"
hasE 'adb -s "\$EMU_SERIAL"' || fail "the adb wrapper is not serial-scoped"
has 'emulator-\*' || fail "the emulator-only serial allowlist is missing"
has 'refusing non-emulator serial' || fail "the non-emulator refusal message is missing"

# No upload or signing surface, ever (raw source; matches are failures outright).
grep -Eiq 'fastlane|supply|keystore|apksigner|jarsigner|play.*upload' "$src" \
  && fail "the self-test helper must carry no upload or signing surface"

[ -f "$runbook" ] || fail "runbook is missing"
for token in JAVA_HOME swiftshader_indirect force-stop acceleration 2G0YC5ZF7Z056Q 'disk.dataPartition.size'; do
  grep -qi -- "$token" "$runbook" || fail "runbook trap ledger lacks '$token'"
done

[ -f "$pack/ARTIFACT.md" ] || fail "evidence ARTIFACT.md is missing"
png_count=0
for png in "$pack"/*.png; do
  [ -f "$png" ] || fail "evidence pack has no frames"
  png_count=$((png_count + 1))
  sha=$(shasum -a 256 "$png" | cut -d' ' -f1)
  grep -q "$sha" "$pack/ARTIFACT.md" \
    || fail "frame $(basename "$png") sha $sha is not in the ARTIFACT.md manifest"
done
[ "$png_count" -ge 9 ] || fail "expected at least 9 evidence frames, found $png_count"

echo "emu-selftest.test.sh: PASS"
