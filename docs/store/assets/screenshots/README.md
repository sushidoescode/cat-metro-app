# Store screenshots — exact 1179×2556, no device frames

Captured 2026-09-11 from MAIN `88ae1ddc5c120e258b4634e0f646aa8069830939` (both the
original-art merge and the Android packaging merge included), with the licensed cat rig and
the licensed prop install admitted in the main checkout.

Shipaton requires **at least one screenshot at exactly 1179px × 2556px without device
frames** ([official rules](https://revenuecat-shipaton-2026.devpost.com/rules)). All five
files here are exactly that size and carry no device frame.

| File | Surface | How it was produced |
|---|---|---|
| `01-board-L009-1179x2556.png` | Gameplay board, three stations, cats mid-route | `BoardLookTests.CaptureEvidence_BoardLook_917x2048_WhenRequested` with `CM_CAPTURE_SIZE=1179x2556 CM_CAPTURE_LEVEL=L009 CM_CAPTURE_TICK=22 CM_CAPTURE_HUD=on` |
| `02-home-1179x2556.png` | Home: carved sign, diorama window, three pills, Daily locked | `UiPhoneCaptureTests` at the same `CM_CAPTURE_SIZE` |
| `03-board-L001-wave-preview-1179x2556.png` | Board with the wave-preview capsule | as above |
| `04-home-daily-unlocked-1179x2556.png` | Home with Daily Line unlocked | as above |
| `05-failure-1179x2556.png` | Failure surface | as above |

`01-board-L009-capture-state.txt` is the deterministic capture receipt for the hero frame
(level, tick, switch routes, train state, outcome). The capture path replays to a fixed tick
rather than sampling wall-clock, so the frame is reproducible.

**Suggested Play order:** 01, 03, 02, 04, 05. **Devpost hero:** 01.

## What these are not

- Not device screenshots. They are render-target captures at the store resolution from the
  editor, which is what the rules ask for (no device frames). Device screenshots from the
  Pixel 9 Pro are still worth taking for the Play listing if time allows; the Pixel was not
  attached when these were made.
- Not final if the board art changes. Re-run the same command and replace the files.
- The failure frame still uses the older flat marker treatment in places; see the win-frame
  note in `docs/release/shipaton-submission-package.md` before cutting video from this build.
