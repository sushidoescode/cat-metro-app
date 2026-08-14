# Emulator gameplay pass — 2026-08-14 (EMU-RIG evidence)

Agent claim (not attested): first full self-played gameplay pass on Android. The rig
booted the game, recovered a halted run by tap, flipped the interchange during transit,
delivered cats to matching stations, won L001, advanced via the ResultsPanel Next
button, and won L002's mixed-color cast with timed flips — all verified by eye from
framebuffer captures, on-device, with no editor in the loop.

## Provenance

- Build: dev APK from main @ d10509d (CatMetroCliBuild dev path; Development Build
  watermark visible in frames). The APK is evidence provenance, not committed.
- Device: AVD `catmetro-test` (pixel_7, arm64-v8a, headless SwiftShader), serial
  emulator-5554, portrait framebuffer 1080x2400.
- Input method: `adb shell input tap` at raw framebuffer coordinates; rotation driven
  by the virtual accelerometer. Recipe + trap ledger: docs/runbooks/emulator-selftest.md.

## Frames (sha256 manifest)

| # | frame | what it shows |
|---|-------|----------------|
| 01 | `f9af7a6cb12e8077caa4368014767a52b4bd9e631748745976a0567c1fe8f236` 01-landscape-defect-before.png | THE ROTATION DEFECT: full-landscape render (2400x1080) after an accelerometer sweep — the before-proof ORIENT-LOCK's after-proof must pair with. Sticky until force-stop. |
| 02 | `1212ea9a2335a717399ffbae16aee30b9fb2eebb444076b44c7ac063a02fbf89` 02-boot-to-halt-portrait.png | Cold start lands in L001 already halted: red cat on the blue station, NEW-Q4 guard banner, dev console showing the six known collider errors. |
| 03 | `40df52f2a5bae3c165b89d4d26d2cb79d83769b092396e325d133825a6c79022` 03-halt-clean.png | Halt state with console closed. A player-facing gap: no visible retry affordance (recovery is an undiscoverable tap). |
| 04 | `695475a6b00a3a356b453d73e128d0bd511f874ef9f523d9edbe5a2facf79cc2` 04-retry-live-x2red.png | Tap on the interchange restarts the run: lines brighten to active white, red x2 cat counter appears. |
| 05 | `d313e70592aa9e0e50e2c8e0f130253d26fb33b416fd7e9bdcfd8d1166e37629` 05-red-delivered-at-R.png | After a mid-transit switch flip, the red cat arrives at the red R station — matched delivery, no fault. |
| 06 | `c781c7f948b3d6bf9c88ad33388fb68bd8d6590fcc91ff1303b691c02f44c40b` 06-L001-all-cats-home.png | L001 won: "All cats home!", board blurred, green ResultsPanel with Next (the CM-LOADNEXT flow live on device). |
| 07 | `7c115a887abe4de833827b453fab344ceb410f7dce7d728bc7248188e3b0c657` 07-L002-loaded-blue-x2.png | Next loads L002 (blue x2 cast) — which halts instantly on default routing: first cat spawns with no grace period (game-feel finding). |
| 08 | `41020d4a906d389b48fdf47b150cbb399087f6f2e5fed4dbdab86d460892b748` 08-L002-blue-descending-switch-right.png | L002 mid-play: blue cat descending with the switch already flipped back right after the red lead cat was routed left. |
| 09 | `c6403efedb2f1b0fc1b2ebd34e33caf145cfdca632925f52474f7d1110dfd636` 09-L002-all-cats-home.png | L002 won with timed color-matched flips. |

## Findings for the queue (observations, not this contract's diff)

1. **No retry affordance on halt** — recovery is a bare tap on the interchange with no
   button or hint. Fine for a dev fence; not shippable UX.
2. **L002 spawns instantly** — the level halts before a human could read the board.
   Needs a spawn grace period or a ready gate.
3. **Rotation defect confirmed on device** (frame 01) — including its stickiness
   (landscape survives the sensor returning to portrait until force-stop). ORIENT-LOCK
   (PR #87) is the fix; its post-merge APK owes the paired after-proof.
4. The dev console auto-opens over gameplay because of the six known collider errors
   (art-chain debt) — it occludes the lower board until dismissed.

## Blinded-rigs disclosure

Frames were captured and judged by the same session that drove the inputs. Level
outcomes are corroborated by in-game state transitions visible across frames (halt
banner clearing, counters, ResultsPanel), not by any log the session authored.
