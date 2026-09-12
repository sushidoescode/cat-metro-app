# Store screenshots — exact 1179×2556, no device frames

**Refreshed 2026-09-12 from MAIN `e1a2a28817b93939071354b16c325d1004279fbe`**, which ships the
consist at `CatModelCatalog.ConsistScale = 1.30`. The 2026-09-11 set was cut at 1.00 and is
superseded: at the same level, tick and framing the riders and vehicles are visibly larger — the
licensed rider's rendered head measures 9.92% of frame width on L001/L002 and 8.07% on L009/L015,
against the 5%/4% floors the older set was held to. The licensed cat rig and the licensed prop
install were both admitted in the main checkout.

Shipaton requires **at least one screenshot at exactly 1179px × 2556px without device
frames** ([official rules](https://revenuecat-shipaton-2026.devpost.com/rules)). Every file here
is exactly that size and carries no device frame.

| File | Surface | How it was produced |
|---|---|---|
| `01-board-L009-1179x2556.png` | Gameplay board, three stations, cats mid-route | `BoardLookTests.CaptureEvidence_BoardLook_917x2048_WhenRequested` with `CM_CAPTURE_SIZE=1179x2556 CM_CAPTURE_LEVEL=L009 CM_CAPTURE_TICK=22 CM_CAPTURE_HUD=on` |
| `02-home-1179x2556.png` | Home: carved sign, diorama window, three pills, Daily locked | `UiPhoneCaptureTests` at the same `CM_CAPTURE_SIZE` |
| `03-board-L001-wave-preview-1179x2556.png` | Board with the wave-preview capsule | as above |
| `04-home-daily-unlocked-1179x2556.png` | Home with Daily Line unlocked | as above |
| `05-failure-1179x2556.png` | Failure surface | as above |
| `06-board-L001-1179x2556.png` | L001 board at tick 22, the opening level | `BoardLookTests.CaptureEvidence_BoardLook_917x2048_WhenRequested` with `CM_CAPTURE_SIZE=1179x2556 CM_CAPTURE_LEVEL=L001 CM_CAPTURE_TICK=22 CM_CAPTURE_HUD=on` |

`01-board-L009-capture-state.txt` and `06-board-L001-capture-state.txt` are the deterministic
capture receipts for the two board frames
(level, tick, switch routes, train state, outcome). The capture path replays to a fixed tick
rather than sampling wall-clock, so the frame is reproducible.

**Suggested Play order:** 01, 06, 03, 02, 04. **Devpost hero:** 01.

`03` and the two Home frames are **byte-identical to the 2026-09-11 set**, and that is correct
rather than a stale copy: `03` is L001 at tick 0 with no cat on the board yet — only the switch,
the empty stations and the wave-preview capsule — and the Home diorama mounts its rig through a
different path that the consist scale does not touch. Only `01` and `05` carry riders and moved.
Because `03` shows no cat at all in a game about cats, prefer `06` in that slot.

**`05-failure-1179x2556.png` is the weakest of the set** — a small dimmed board under a banner,
mostly empty cream. It documents the failure surface honestly but reads poorly as a store card.
Prefer 06 in its slot unless the listing specifically wants to show the fail state.

## What these are not

- Not device screenshots. They are render-target captures at the store resolution from the
  editor, which is what the rules ask for (no device frames). Device screenshots from the
  Pixel 9 Pro are still worth taking for the Play listing if time allows; the Pixel was not
  attached when these were made.
- Not final if the board art changes. Re-run the same commands and replace the files; the
  captures replay to a fixed tick, so the only thing that moves is the art.
- The failure frame still uses the older flat marker treatment in places; see the win-frame
  note in `docs/release/shipaton-submission-package.md` before cutting video from this build.
