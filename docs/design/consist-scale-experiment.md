# Consist scale — the experiment, the renders, and what 1.30 actually cost

The human's ask is "the trains with the cats in them can be slightly larger on screen, and it
can be a lot cuter to look at". **Shipped: `CatModelCatalog.ConsistScale = 1.30`.** This records
what was measured, what was rendered and looked at, and what had to be corrected to land it.

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

## Choosing the factor from renders

A probe rendered candidates at ordinary phone framing and measured the rider's own silhouette
against an empty-scene pass:

| ConsistScale | L001 width of frame | L009 width of frame |
|---|---|---|
| **1.00 (was shipped)** | 7.63% | 6.22% |
| 1.15 | 8.83% | 7.20% |
| **1.30 (shipped)** | **9.92%** | 8.18% |
| 1.45 | 11.12% | 9.05% |

Renders and JSON: `.catshots/owner-2026-09-11/consist-scale/`. Opened at 1.00/1.15/1.30/1.45: the
face becomes clearly readable by 1.30, and at 1.45 the vehicles overhang the rails in the way
`docs/reference/gen-ref-v2-board.png` actually shows a toy carriage doing. Reference proportion is
~20% of frame width; treat it as an artistic target, not a threshold.

## The 5–6% target is retired

TASK 17 pinned `PresenterScale = 0.52` with the comment "the GridY=1.47 phone composition retains
the strict 5-6% rendered head target". That target is **explicitly retired**, and not quietly: it
was written against a different camera framing and 0.52 had already outgrown it — the rendered
probe measured the rider at 7.63% of frame width on L001 while that line still claimed 5–6%.

What replaces it is a rendered floor, not a comment. `BoardLookTests`
`AdmittedRigPassengerHeadAndEars_AreReadableAtPhoneScale` measured the licensed head at ≥5% of
frame width on L001 and ≥4% on L002/L009; those floors now carry the consist scale, because the
camera fits the **board** and not the consist, so a rendered rider's width is proportional to the
scale. The retirement itself is recorded in
`CatModelCatalogTests.Task17HandoffContract_UsesThePinnedResourceAndStateLiterals`.

## What scales, and what does not

Scaling "the whole consist" is not one multiplication. Three different kinds of quantity are
involved, and getting the classification wrong is what the work actually consisted of:

1. **Vehicle-own dimensions** — carriage width/depth/height, engine envelope, the coupling
   distance `CarriageOffset`. These are the toy's own size: multiply.
2. **Contacts with fixed board geometry** — the wheel bottom at the rail crown
   (`ToyTrainView.RailCrownDepth`, 0.235 anchor-local). The rails do **not** move when the toy
   gets bigger, so this does not scale, and every vertical vehicle dimension is measured *from*
   it. A probe of the imported tub confirms the contact holds: its deepest vertex measures exactly
   0.235 at both 1.00 and 1.30.
3. **Composites** — `PlatformBadgeClearance` is half the board-fixed station keyline (0.46575)
   plus half the rider's rendered width (0.2696, which scales) plus a 0.044 margin;
   `PlatformDeliveredQueueSpacing` is the scaled rider width plus that same fixed margin. Only
   the rider's share moves.

### The one place the classification was wrong

`OpenCarriageSeatDepth` — how far the rider sinks into the open tub — was written as
`.0983 × ConsistScale`, i.e. measured from the carriage *anchor origin*. It belongs to class 2:
the tub grows upward from the rail crown, so its floor **rises** as the vehicle scales, and a
taller rider must sit **higher**, not deeper. Measured with the real imported meshes and the real
licensed skin:

| | tub floor (Carriage-local z) | seated rider's deepest vertex | clearance |
|---|---|---|---|
| 1.00 | 0.150000 | 0.143856 | **+0.006144** |
| 1.30, `.0983 × k` | 0.124500 | 0.187013 | **−0.062513** — 6cm of cat through the floor |
| 1.30, corrected | 0.124500 | 0.116513 | **+0.007988** = 0.006144 × 1.2999 |

The corrected form is `RailCrownDepth − (RailCrownDepth − .0983) × ConsistScale`. **No horizontal
clearance test could see this**: every one of them projects onto a board-plane direction. The
guard added for it, `SeatFloorClearanceTests`, finds the floor plateau in the actual mesh and
bakes the actual seated skin, so it holds at any scale; reverting the constant fails it at
−0.0625.

## Production evidence, not a probe

`ConsistProductionCaptureTests` (opt-in, `CM_CONSIST_PRODUCTION_DIR`) runs the **shipped GameRoot
loop** with the shipped `ConsistScale` — so every derived constant is exercised the way the player
gets it — with the **UI left on**, and `Time.captureDeltaTime` pinning presentation, simulation and
Animator to one clock so frame N is the same moment at either scale. Frames from
`/private/tmp/catmetro-verify-…`, opened and compared side by side at 1.00 and 1.30:

| moment | levels | what the frames show at 1.30 |
|---|---|---|
| riding mid-edge | L001 sparse, L009 crowded | rider clearly larger, face readable, consist still inside the track corridor |
| boarding | L001, L008, L009 | unchanged staging, larger cat |
| arrival / Alight | L001, L008 | steps clear of the station badge exactly as at 1.00 |
| successive arrivals to one station | L008 | two delivered cats still read as two, with a visible gap |
| celebration | L008 | unchanged beat and stagger |
| win chrome | L001 | "All cats home!" banner unchanged and bright |

Alongside the frames it records per-frame numbers, so "the animation is preserved by construction"
is not the evidence:

- **Feet actually move.** Foot stride, measured in scale-free MODEL units from the baked skin,
  ranges 0.4409→0.7840 at 1.30 against 0.4390→0.7824 at 1.00 — the same walk, rendered bigger.
- **Seating holds through motion.** Live playback goes slightly deeper than the bare Ride clip
  because the micro-motion breathing pose does: minimum floor clearance is **−0.004483** at 1.00
  and **−0.005826** at 1.30, a ratio of **1.2996**. That sub-millimetre overlap is shipped
  behaviour hidden by the tub walls, unchanged in relative terms; the test's law is that it must
  not get proportionally worse, which a mis-anchored sink immediately would.

## Verification at 1.30

- **EditMode: 2359 / 2359 passed, 0 failed, 466.3s** (all captures armed).
- The 60-level rig clearance sweep passes with a *better* proportional margin: `minimumGap`
  0.083850 against a required 0.058500, versus 0.051693 against 0.045 at 1.00 — identical case
  counts (endpointSamples 395, stationArrivalCases 118, uniqueStationHeadings 69,
  retainedHeadingSamples 72, departureStates 2, earSamples 17).
- One earlier full run recorded that sweep at 664.4s and it tripped its 600s timeout. That was
  **not** the candidate: the same test at 1.30 runs 363.77s alone and 364.82s inside the full
  suite, against 367.50s at 1.00, on identical case counts. The timeout was left at 600000.

## Also available, not needed

The open carriage is **owned original Blender art** with a reproduction script
(`scripts/blender_original_open_carriage.py`), so widening the tub is permitted and would buy head
room without scaling the engine or the track footprint. The uniform scale did not need it.
