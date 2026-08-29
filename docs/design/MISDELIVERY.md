# Mis-delivery semantics proposal

**Status:** proposal only; human approval is still required. No mis-delivery code or policy was
added in this lane.

## Current behavior

When a wrong-colour cat reaches a station, `Simulation.Step` throws the existing
`NotSupportedException` tagged `NEW-Q4`. `LevelSolver` treats that exception as a pinned branch.
The throw occurs after the train has begun arrival resolution, so callers must not catch it and
continue the same state.

This pin has already blocked tests and later mechanic bands, but replacing it decides player-facing
rules and therefore cannot be inferred from implementation convenience.

## Recommended proposal: refuse, dwell, reverse

On a colour mismatch:

1. The station refuses the train with a clear, friendly animation and `Rejections++`.
2. The train dwells for 8 ticks (one second at the current 8 Hz simulation rate).
3. It reverses along the exact edge it arrived on.
4. At the previous node it rejoins normal routing using the switch's then-current route.

The train is delayed but not deleted, so an exact-supply level remains recoverable. The time limit
continues during the dwell, and any additional flips needed for recovery naturally affect the flip
mastery rating. This is a legible setback rather than an immediate loss.

Proposed edge cases:

- The backing move is allowed even when the authored edge is one-way. It is a station-refusal
  exception, not general reverse traversal.
- The current simulation has no collision mechanic, so a reversing train passes an oncoming train
  just as same-edge trains already coexist. Do not invent a collision failure in this change.
- Multiple refused trains may dwell independently; `stations[].capacity` remains a separate,
  currently unwired design question.
- A second arrival at another wrong station repeats the same behavior. The existing time limit is
  the eventual bound on loops.

The human relayed that this recommendation appears right for a cosy game, but explicitly did not
approve it. The current throw must remain until that decision is made.

## Why not the smaller-looking alternatives?

| Alternative | Problem |
|---|---|
| Remove the cat / send it home | On exact-supply boards this silently converts one mistake into an unwinnable timeout. |
| Fail immediately | Punitive, and requires a new outcome reason plus failure UI and solver changes. |
| Auto-correct the route | Removes the routing decision the puzzle is meant to test. |
| Queue forever at the station leaf | Technically cheap but not a rule a player can understand. |

## Likely wiring cost after approval

This is a medium cross-cutting mechanic, not a one-line exception replacement. Domain needs a
refusal-dwell/reverse state and must retain the incoming edge through arrival. That can likely fit
the existing digest width by assigning new `TrainState` values and encoding reverse direction in
the existing signed `EdgeId`; this must be proved with digest tests before adoption. The solver,
validator, replay tests, HUD/animation, and authored-level solvability all need to exercise the new
path. No-misdelivery golden traces should remain byte-identical, but that too is a required artifact
check rather than an assumption.
