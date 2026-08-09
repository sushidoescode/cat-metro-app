# ART-DIORAMA evidence — 2026-08-09

This pack records Lane 1A's rendered and mechanical evidence. It does not claim to resolve
the human TG-1..TG-8 taste gates.

## Reference and editor renders

- Gemini reference viewed at 1536 x 2752:
  `5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a`
- `editor-diorama-board.png` (inspected):
  `03313a81126bdbdd52497ef38c454e2e4ef7eb90b7bfd57b37da50f575c40b6d`
- `editor-diorama-commuter.png` (inspected):
  `efe8a437a8b26176f93291ca6f7eef777da347e1bc8e885fa910d97f4afc64b0`

The real `Game` scene is visible in both frames. The alternate frame contains a live red cat
commuter with separated ears, face, tail, contact shadow, line-colour body, and matching circle
tag. The board frame shows the cream/Ink-Navy track, two station symbol plates, depot, thrown
teal/orange switch, desk dressing, imported props, and blob shadows.

## Accessibility render pack

All files below were emitted from the shipped commuter construction and inspected. The four
simulations transform the complete rendered palette; the symbol meshes remain independently
readable. These are inputs to, not substitutes for, CM-R21's human rater protocol.

- `golden-frame-five-lines.png`: all five line colours, symbols, and silhouette families;
  `dfcce72deabc6fff9c7aa2cb9dfa9e16ca04a4073d0802b75174e60ec10b2f8f`.
- `golden-frame-deutan.png`:
  `c6305f322d2b269c8c187ae44b120eddad39f7c9d3669b78fc5d0420d43a314b`.
- `golden-frame-protan.png`:
  `50d064304379ea44979f73f977936a81981388c20bbd3d103c370d745ed038d5`.
- `golden-frame-tritan.png`:
  `08e484dbab328901a5b31aa41f9c5c51fd7719579ac3a176b46a2a7b17205820`.
- `golden-frame-grayscale.png`:
  `68bbab4ac6544e9e0c739882eadd1bd46eae4bda1d223efdc57d49dfbc266939`.
- `silhouettes-five-at-64px.png`: five adjacent 64x64 silhouette-only cells, with colour and
  symbols removed; `bd85352a6ebcd1e924d3de3d7f979a077949efc14bc0bb4aa1576b7538cf3e9f`.

PENDING HUMAN LEG: five non-author raters, randomized/unprompted, must record at least 23/25
correct trials for both the symbol+silhouette frame and silhouette-only sheet. No pass is
claimed here.

## Test and mutation evidence

- Focused EditMode diorama asset tests: 6/6.
- Focused PlayMode diorama construction tests: 8/8.
- Full Unity EditMode: 827/827, zero failures (266.40 seconds).
- Full Unity PlayMode: 145/145, zero failures (118.22 seconds).
- Final `bash scripts/check.sh`, `bash scripts/test.sh`, and `bash scripts/build.sh`: PENDING
  after the review-fix commit; the pre-review increment passed all three.
- `tests/unity/device-config.test.sh`: PASS.
- `tests/unity/cli-build-shim.test.sh`: PASS.
- Named negative controls and their clean reverts are recorded in `MUTATION-PROOFS.md`.

The Lane 1A shell gate finds zero `GameObject.CreatePrimitive` calls in Board and Cameras.
WavePreviewStrip's remaining primitive is Lane 1B's explicitly owned debt.

## Development APK

PENDING: the earlier `75f0866` development APK was valid for that commit but is superseded by
the review fixes. Build, hash, and inspect a corrected-commit APK before device installation.

## Pixel evidence

PENDING: the Pixel is not yet visible to `adb`. Only a Quest 3 and PICO emulator are currently
enumerated, and the APK has deliberately not been installed on either. Replace this section
with the exact Pixel serial/model, install/launch result, two inspected screenshot hashes, and
the scoped Unity logcat verdict before this lane is merge-ready.

## External gates

- The trusted-base risk classifier returned `RISKY` because this required evidence touches
  `evals/results`; independent code and security reviews are required.
- The first independent code review returned NOT MERGEABLE. Its concrete runtime, gate,
  camera, rounding, shader, evidence, and provenance findings have been repaired; exact-head
  reassessment remains pending.
- The human-approved Polyfork license/source-custody ADR remains an external pre-merge gate.
- HC-25 remains closed: no merge may be armed without a fresh in-chat merge word after the
  evidence and review legs are complete.
