# EMU-RIG — frozen contract

**Task:** Turn the proven Android-emulator self-test capability into repo deliverables: a serial-guarded helper script, a runbook capturing every operational trap learned while building the rig, and the committed evidence pack from the first full self-played gameplay pass (L001 + L002 won by agent input on the emulator).

**Authority:** the human's 2026-08-14 directive (H-1-class, agent-relayed): "research a way for you visually confirm how the app behaves on mobile, and see how you can test the whole thing yourself too before handing it off to me." The rig exists and worked; this contract lands it so any session can reuse it.

**Base:** origin/main @ d10509d. **Branch:** task/EMU-RIG. **Mode:** sprint (proportional ceremony; demo criterion = the shape gate runs green in CI).

## Criteria

1. `scripts/emu-selftest.sh` — subcommand helper wrapping the proven recipe: `boot`, `install <apk>`, `launch`, `bounce`, `coldstart`, `frame <out.png>`, `tap <x> <y>`, `rotate-landscape`, `rotate-portrait`, `status`. Every adb invocation is serial-scoped (`-s "$EMU_SERIAL"`); the script REFUSES any serial that does not start with `emulator-` (the physical Pixel `2G0YC5ZF7Z056Q` must be untouchable by this tool). No Play-upload or signing surface of any kind.
2. `docs/runbooks/emulator-selftest.md` — the full recipe + trap ledger: sdkmanager needs Unity's OpenJDK as JAVA_HOME; arm64-v8a image; AVD disk cap 3G; stale-lock cleanup; headless flags; SwiftShader first-draw ~44s; OS-dialog focus steal → lifecycle bounce; tap coords are raw framebuffer (screencap 1080x2400); rotation must be driven by the virtual accelerometer (settings-only rotation does nothing — the app is the orientation source); the pre-ORIENT-LOCK build's landscape state is STICKY until force-stop; never touch non-emulator serials.
3. Evidence pack `evals/results/device/emu-gameplay-pass-2026-08-14/`: ARTIFACT.md (method, provenance, findings, per-frame sha256 manifest) + the gameplay-pass frames (halt state, retry, switch flip, delivery, L001 win, Next → L002, mixed-color play, L002 win, plus the landscape-defect before-frame). Every committed frame's sha256 must appear in ARTIFACT.md.
4. TDD: `tests/emu/emu-selftest.test.sh` shape gate lands RED-first (script absent at RED commit), then the script turns it GREEN. Static gates only (CI has no emulator): subcommand presence on a comment-stripped view, serial-guard presence, no unscoped adb calls, evidence-manifest sha integrity.

## Out of scope

Rotation AFTER-proof (owed by ORIENT-LOCK follow-up once #87 merges and the APK is rebuilt); any product-code change; wiring the self-test into CI (needs a human cost ruling — recorded as an open question for the queue).

## Assumptions (unlisted assumptions are defects)

- Committing PNG evidence to `evals/results/` is the established pattern (ui-chrome-pass artev did exactly this; agents may write claims under evals/results/ excluding attested/).
- The dev APK used for the pass (built from main d10509d) is disposable evidence provenance, not a shipped artifact; the APK itself is NOT committed.
- ~1.3MB of frames is within repo norms (largest prior artifact pack was comparable).
