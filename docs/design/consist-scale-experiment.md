# Consist scale — the experiment, the renders, and what landing 1.30 still costs

The human's ask is "the trains with the cats in them can be slightly larger on screen, and it
can be a lot cuter to look at". This records what was measured, what was rendered, and exactly
what remains.

## The head alone cannot do it

`CatRigPresentation.HeadScale` is capped by the carriage, not by taste. A single-run sweep measured
minimum carriage clearance at five head scales on the two worst corpus cases (WaitingIdle, queue
lane 1, both diagonal source normals):

| HeadScale | minimum gap | margin vs the required 0.045 |
|---|---|---|
| **1.28 (shipped)** | 0.051693 | **+0.006693** |
| 1.34 | 0.041144 | −0.003856 |
| 1.40 | 0.030596 | −0.014404 |
| 1.45 | 0.021805 | −0.023195 |
| 1.52 | 0.009498 | −0.035502 |

Loss is linear at **0.1758 board units of gap per 1.0 of head scale**, so the true ceiling is
**≈1.318** — about +3% of head width, imperceptible, and it would spend the entire margin. The
binding case is the ear corpus at maximum carriage-ward bob, so buying more means damping an
animation the human wants more of, not less. Raw data:
`.catshots/owner-2026-09-11/arrival-queue-and-scale/head-scale-sweep/head-scale-sweep.json`.

## The consist scale does, and it was rendered

Scaling cat, carriage, engine and the seat sink by one factor preserves every internal seat
clearance by construction — those are relative — and changes only clearance against the board.
A probe rendered candidates at ordinary phone framing and measured the rider's own silhouette
against an empty-scene pass:

| ConsistScale | L001 width of frame | L009 width of frame |
|---|---|---|
| **1.00 (shipped)** | 7.63% | 6.22% |
| 1.15 | 8.83% | 7.20% |
| 1.30 | **9.92%** | 8.18% |
| 1.45 | 11.12% | 9.05% |

Renders and JSON: `.catshots/owner-2026-09-11/consist-scale/`. Opened at 1.00/1.15/1.30/1.45: the
face becomes clearly readable by 1.30, and at 1.45 the vehicles overhang the rails in the way
`docs/reference/gen-ref-v2-board.png` actually shows a toy carriage doing. Reference proportion is
~20% of frame width; treat it as an artistic target, not a threshold.

## What is landed

`CatModelCatalog.ConsistScale`, **shipped at 1.0 so it is a no-op today**, with every dependent
quantity derived from it rather than hand-written:

- `PresenterScale = 0.52 × ConsistScale` — and `WalkTravelSpeedAtOneX` already derives from
  `PresenterScale`, so foot cadence follows the scale automatically. Expressive animation is
  preserved by construction.
- the carriage and engine visual wrapper scales
- `OpenCarriageSeatDepth`, `CarriageOffset`, `PlatformSideOffset`, `PlatformEndpointClearance`,
  `PlatformFramingHalfWidth`
- `PlatformBadgeClearance` and `PlatformDeliveredQueueSpacing`, recomputed from the geometry they
  must clear: half the board-fixed station keyline plus half the rider's measured width **at the
  current scale**, plus the margin

**The licensed model bytes are untouched.** This is a presentation wrapper scale, which is the
only place `AGENTS.md` permits a correction.

## What flipping it to 1.30 still costs

A full EditMode run at 1.30 was **2358 total, 2335 passed, 23 failed** — every failure a pinned
pre-scale literal, and **no clearance violation anywhere**, which is the important result: the
geometry scales coherently. 15 of the 23 were mechanical and were confirmed fixable by deriving
the pin from `ConsistScale`. The remaining work is:

1. **Three `DeliveredPassenger_ObservedHandoffKeepsTheArrivalEndpoint` cases** pin hand-derived
   absolute points computed from the station at (2.4, 2.94) **and a 0.48 carriage trailing
   distance**. Scaling `CarriageOffset` moves the seat along the spline, so those literals no
   longer describe the same relationship. They must be **re-derived** — Hermite arc-length at the
   new trailing distance — not re-pinned from observed output, and not restated by reading the
   already-lerped cat transform (I tried that; it reads the platform-path position, not the seat).
2. **`ToyEnginePresentationTests.OriginalEngineKeepsVehicleSpacing…`** carries several vertex-extent
   bounds (±.23001, ±.15001, and at least one more). Each needs deciding individually: some are the
   engine's own envelope and scale with it; any that encode a board or track clearance do not.
3. **`CatModelCatalogTests.Task17HandoffContract`** pins "the strict 5-6% rendered head target".
   Raising the scale deliberately retires that target — it is a design decision to record, not a
   number to bump quietly.
4. Then the full graphics PlayMode suite, including the horizontal safe-frame law in
   `RuntimeSceneRigTests`, which measures cat-vs-board-edge and does **not** scale.

Estimate: a focused half-day with two full admission cycles. It is a look improvement, not an
eligibility blocker, so it should not jump the queue ahead of getting the app publicly live.

## Also available, not tried

The open carriage is **owned original Blender art** with a reproduction script
(`scripts/blender_original_open_carriage.py`), so widening the tub is permitted and would buy head
room without scaling the engine or the track footprint. That is a different, narrower experiment
than the uniform scale and may be the better answer if the uniform scale's board-clearance cost
proves awkward.
