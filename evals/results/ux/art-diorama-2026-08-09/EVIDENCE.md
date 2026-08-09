# ART-DIORAMA evidence — 2026-08-09

This pack records Lane 1A's rendered and mechanical evidence. It does not claim to resolve
the human TG-1..TG-8 taste gates.

## Reference and editor renders

- Gemini reference viewed at 1536 x 2752:
  `5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a`
- `editor-diorama-board.png` (inspected):
  `78d241b40292211ffe758b57c9c42e787185309357c3b5f17861f409bdd951ac`
- `editor-diorama-commuter.png` (inspected):
  `57f991cd1a876687cdce595dc134f2d9df4d4069bb167e668f00b38e20fad3b4`

The real `Game` scene is visible in both frames. The alternate frame contains a live red cat
commuter with separated ears, face, tail, contact shadow, line-colour body, and matching circle
tag. The board frame shows the cream/Ink-Navy track, two station symbol plates, depot, thrown
teal/orange switch, desk dressing, imported props, and blob shadows.

## Accessibility render pack

All files below were emitted from the shipped commuter construction and inspected. The four
simulations transform the complete rendered palette; the symbol meshes remain independently
readable. These are inputs to, not substitutes for, CM-R21's human rater protocol.

- `golden-frame-five-lines.png`: all five line colours, symbols, and silhouette families;
  `d92c9c01a7b52dcbd697546570f4ead054aa043fb13ed6242fbea5b5b0ecaa71`.
- `golden-frame-deutan.png`:
  `e80f748bdff052f89d6c980fe989f9f1621a86350fd456fea1b72956b3e40d2a`.
- `golden-frame-protan.png`:
  `5b04879be9948365efeeacb86ce079514ca4342b532c2e0dd61863bb133311b1`.
- `golden-frame-tritan.png`:
  `8135cbb21b2d9c58ff4bfdf63c48ba499c0b79dff9908b370df565237be736a7`.
- `golden-frame-grayscale.png`:
  `e101225d9ab56167c9c0327251c89a0c3337bbadd57662e3d357b366c2c6f081`.
- `silhouettes-five-at-64px.png`: five adjacent 64x64 silhouette-only cells, with colour and
  symbols removed; `e0fdf3f6418caed44a0f1b154a805b608759f114d4855f9b56538dc037753c28`.

PENDING HUMAN LEG: five non-author raters, randomized/unprompted, must record at least 23/25
correct trials for both the symbol+silhouette frame and silhouette-only sheet. No pass is
claimed here.

## Test and mutation evidence

- Focused EditMode diorama asset tests: 7/7.
- Focused PlayMode diorama construction tests: 9/9.
- Full Unity EditMode: 828/828, zero failures.
- Full Unity PlayMode: 146/146, zero failures.
- Final `bash scripts/check.sh`: PASS.
- Final `bash scripts/test.sh`: PASS, 16/16 discovered shell tests.
- Final `bash scripts/build.sh`: PASS.
- `tests/unity/device-config.test.sh`: PASS.
- `tests/unity/cli-build-shim.test.sh`: PASS.
- Named negative controls and their clean reverts are recorded in `MUTATION-PROOFS.md`.

The Lane 1A shell gate finds zero `GameObject.CreatePrimitive` calls in Board and Cameras.
WavePreviewStrip's remaining primitive is Lane 1B's explicitly owned debt.

## Development APK

- Code/art commit: `4e1af6b` (rebased onto `origin/main` `11a3335`).
- Artifact (not committed):
  `/Users/sushantsrikrish/Downloads/CatMetro-art-diorama-4e1af6b-dev.apk`.
- SHA-256:
  `b59c820475f8c4135e2581e3df5f17467d313616aa515e678c12e83532aa950c`.
- Size: 72,444,059 bytes; ZIP integrity check reports no errors.
- Unity CLI result: `Succeeded dev=True`, zero errors.
- Package: `com.catmetro.game`, version code `1`, version `0.1.0`.
- ABI: `arm64-v8a`; min SDK 25; target/compile SDK 36.
- `aapt dump badging` reports `application-debuggable` and launch activity
  `com.unity3d.player.UnityPlayerGameActivity`.
- Binary metadata contains `DEVCAP_BOOT_OVERRIDE`, `DEVCAP_BOOT_OVERRIDE_INVALID`,
  `DEVCAP_LEVEL_OVERRIDE`, `DEVCAP_LEVEL_OVERRIDE_INVALID`, and `DEVCAP_WRITTEN`.

## Pixel evidence

PENDING: the Pixel is not yet visible to `adb`. Only a Quest 3 and PICO emulator are currently
enumerated, and the APK has deliberately not been installed on either. Replace this section
with the exact Pixel serial/model, install/launch result, two inspected screenshot hashes, and
the scoped Unity logcat verdict before this lane is merge-ready.

## External gates

- The trusted-base risk classifier returned `RISKY` because this required evidence touches
  `evals/results`; independent code and security reviews are required.
- The first independent code review returned NOT MERGEABLE. Its concrete runtime, gate,
  camera, rounding, portrait, shader, evidence, and provenance findings were repaired. The same
  independent round reports no remaining code finding at `4e1af6b`; final evidence-only
  exact-head closure remains pending.
- Independent security reassessment found no leaked secret, runtime importer/networking,
  package drift, or unresolved provenance defect. It retains the external ADR blocker below and
  records a low-risk trusted-caller APK-output containment debt for a future production workflow.
- The human-approved Polyfork license/source-custody ADR remains an external pre-merge gate.
- HC-25 remains closed: no merge may be armed without a fresh in-chat merge word after the
  evidence and review legs are complete.
