# ART-DIORAMA evidence — 2026-08-09

This pack records Lane 1A's rendered and mechanical evidence. It does not claim to resolve
the human TG-1..TG-8 taste gates.

## Reference and editor renders

- Committed Gemini golden reference, `gemini-tabletop-golden.png`, viewed at 1536 x 2752:
  `5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a`
- `editor-diorama-board.png` (inspected):
  `78d241b40292211ffe758b57c9c42e787185309357c3b5f17861f409bdd951ac`
- `editor-diorama-commuter.png` (inspected):
  `57f991cd1a876687cdce595dc134f2d9df4d4069bb167e668f00b38e20fad3b4`

The real `Game` scene is visible in both frames. The alternate frame contains a live red cat
commuter with separated ears, face, tail, contact shadow, line-colour body, and matching circle
tag. The board frame shows the cream/Ink-Navy track, two station symbol plates, depot, thrown
teal/orange switch, desk dressing, imported props, and blob shadows.

**Superseded baseline:** on 2026-08-10 the human TG review rejected this composition. These two
editor frames are retained only as before-state diagnostics. They are not final art evidence and
must be replaced after the camera, commuter/train scale, desk colour, and lighting correction.

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

PENDING RE-CAPTURE AND HUMAN LEG: the prior sheets predate the TG composition correction and are
not eligible for the final rating. New renders must be emitted from the corrected shipped assets
before any CM-R21 protocol is run. No pass is claimed here.

## Test and mutation evidence

- Focused EditMode diorama asset tests: 8/8.
- Focused PlayMode diorama construction tests: 9/9.
- Full Unity EditMode: 829/829, zero failures (268.30 seconds).
- Full Unity PlayMode: 146/146, zero failures (120.00 seconds).
- Final `bash scripts/check.sh`: PASS.
- `bash scripts/test.sh`: PASS, 16/16 discovered shell tests at the player/art evidence head;
  the later editor-only temp-output hardening has exact-head full Unity suites plus focused
  check/build/device/CLI gates green.
- Final `bash scripts/build.sh`: PASS.
- `tests/unity/device-config.test.sh`: PASS.
- `tests/unity/cli-build-shim.test.sh`: PASS.
- Named negative controls and their clean reverts are recorded in `MUTATION-PROOFS.md`.

The Lane 1A shell gate finds zero `GameObject.CreatePrimitive` calls in Board and Cameras.
WavePreviewStrip's remaining primitive is Lane 1B's explicitly owned debt.

## Development APK

- Player/art commit: `4e1af6b` (rebased onto `origin/main` `11a3335`). Final source head adds
  only editor-side authoring-output hardening and evidence bookkeeping, neither shipped in APK.
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

## Superseded Pixel baseline

Captured 2026-08-10 on the human-connected Pixel only. The PICO emulator remained enumerated and
was not targeted.

- ADB serial/model: `48121FDAP006X4` / `Pixel 9 Pro` (`caiman`).
- Device/runtime: Android 17, API 37, physical render size 960x2142 at density 360.
- `adb -s 48121FDAP006X4 install -r <recorded-apk>`: `Success`; existing app data was preserved.
- Explicit cold launch of
  `com.catmetro.game/com.unity3d.player.UnityPlayerGameActivity`: `Status: ok`; the activity was
  both `mCurrentFocus` and `mFocusedApp` before capture.
- `pixel-board-live-red.png` (960x2142, inspected): live round red cat with circle tag at the
  depot, both station symbol plates, and the central teal switch with its orange arm exposed;
  SHA-256 `e6dc2986eda5fd99e4ea7cc625b8557ecdad2ead63efe00478e43b04e53e7ba6`.
- `pixel-board-live-blue.png` (960x2142, inspected): distinct slim blue cat with square tag on
  the same live board; SHA-256
  `d9a7b0d132badee815e3091829542737358fadb0bc2c120091c29d4a06cff027`.

The selected PNGs contain no development-console or results overlay. Console-bearing diagnostic
candidates were retained only in a private temporary directory and were not committed.

The human TG review marked this installed composition **FAIL as shipped**: the camera reads
top-down, the cats are about six times too large for the track gauge and do not ride inside open
cars, Ticket Orange dominates the board, the wooden desk edge/grain and warm contact lighting do
not match the golden, and the preview treatment is not reference-like. These images remain as
explicit before-state evidence only. A newly built APK and newly captured Pixel frames are
required after correction.

Scoped launch logcat was streamed live with only `Unity` and `AndroidRuntime` tags; no global log
buffer was cleared. It reports the expected development IL2CPP/ARM64 build, Pixel 9 Pro/API 37,
Vulkan/Mali-G715, and zero `AndroidRuntime`/fatal/crash events. Exactly two Unity errors occur at
launch (2026-08-10 00:18:25.770 and `.772`): both are `MeshCollider` failures from
`GameObject.CreatePrimitive`, and both stack directly to
`CatMetro.Presentation.Hud.WavePreview.WavePreviewStrip.Create` then `GameRoot.Wire`. There are
zero `BoardView` or `CauseCameraController` frames. This is an owned-scope PASS for Lane 1A and an
explicit whole-app clean-log NON-CLAIM: WavePreviewStrip remains Lane 1B's recorded collider debt.

## External gates

- The trusted-base risk classifier returned `RISKY` because this required evidence touches
  `evals/results`; independent code and security reviews are required.
- The first independent code review returned NOT MERGEABLE. Its concrete runtime, gate,
  camera, rounding, portrait, shader, evidence, and provenance findings were repaired. The same
  independent round reports no remaining code finding through the editor-hardening delta; final
  evidence-only exact-head closure remains pending.
- Independent security reassessment found no leaked secret, runtime importer/networking,
  package drift, or unresolved provenance defect. Its predictable-temp-output LOW was repaired
  with unique/reparse/CreateNew safeguards. It records a low-risk trusted-caller APK-output
  containment debt for a future production workflow. The accepted ADR and Pixel evidence delta
  still require exact-head security closure.
- Draft PR #65's remote head `23b893d` has successful `ci` plus both `forge-policy` runs after the
  human fixed Actions billing. Main then advanced through #64; Lane 1A was rebased locally and the
  ADR/signature/Pixel evidence commits are not yet pushed, so those green checks do not cover the
  eventual new remote head and must rerun after push.
- Polyfork license/source custody is now accepted in ADR-0011: the human signed all seven items
  against proposal `feb78a1`; signature record `33a8d6c` is pinned by its successor. The root
  `.env` custody gate was rechecked without reading contents: regular file, mode `0600`, ignored,
  and untracked.
- The first Pixel install/cold launch/logcat leg is complete only as a rejected baseline. A new
  development APK, install, scoped logcat, and two inspected reference-framed screenshots are
  open.
- CM-R21 is held until the corrected shipped-asset renders exist; no protocol choice or
  legibility pass is claimed.
- Fresh exact-head review closure and post-push CI remain open.
- HC-25 remains closed: no merge may be armed without a fresh in-chat merge word after the
  evidence and review legs are complete.
