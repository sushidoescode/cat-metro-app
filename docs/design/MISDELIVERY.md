# Mis-delivery semantics

**Status:** implemented.

## Player rule

When a concrete-colour cat reaches a station that does not accept its colour, the station refuses
the train. The train occupies the platform for exactly 8 simulation ticks (one second at 8 Hz),
then travels backward along the edge it arrived on. When it reaches that edge's `from` node, it
rejoins normal routing under the switch route that is current at that moment.

The refusal is a recoverable delay, not a delivery and not an immediate failure:

- `Rejections` increments on every wrong-colour arrival, including repeat arrivals by the same cat.
- `Deliveries` does not increment and the train slot is not removed.
- The time limit continues while the train dwells and reverses.
- A later wrong-colour arrival repeats the same eight-tick rule from zero.
- Train-side `Wild` still auto-accepts. A station-side `Wild` token remains exact-only and therefore
  refuses a concrete-colour train.

## Exact state-machine timing

`TrainState.RejectedAtStation` and `TrainState.OnEdgeReverse` are byte values 3 and 4. A refused
train retains the incoming `EdgeId`; `ProgressTicks` is its dwell clock. The arrival step leaves it
at progress 0. That post-step state plus progress 1 through 7 are the eight occupied platform
snapshots. On the next advance, progress reaches 8 and the train enters reverse traversal at
progress 0.

Reverse arrival resolves to `EdgeFrom[incomingEdge]`. Normal forward arrival resolves to
`EdgeTo[incomingEdge]`. The return node then performs the ordinary station-or-junction resolution,
including reading a switch's then-current route and same-step pass-through when the outgoing mouth
is free.

This backing move is allowed even when `oneWay` is true or `reversible` is false. It is the narrow
station-refusal exception, not the general reversible-track mechanic.

## Capacity and coexistence

`stations[].capacity` is the maximum number of simultaneously refused trains occupying that
station. Equality is allowed. Occupancy above capacity fails immediately in the same simulation
step with the existing `Failed(PlatformOverflow)` outcome. Cause attribution selects the first
over-capacity station in authored station order.

A reverse train does not reserve the forward edge mouth. When the level does not enable the
`second-train` collision rule, forward and reverse trains may still occupy and pass on the same
edge. With that rule enabled, the same opposing occupancy fails with `Collision`; the exceptional
refusal entry itself remains unconditional and the collision check resolves afterward.

## Replay, solver, and presentation invariants

The serialized train slot remains 10 bytes:

`Id:2 + Color:1 + EdgeId:2 + ProgressTicks:2 + NodeId:2 + State:1`

No digest field, command-log field, or digest width changed. Runs that never mis-deliver therefore
retain their prior bytes; the committed L001 happy-path replay hash remains the executable golden
check.

The solver searches refusal, dwell, and reverse states normally. They no longer increment
`PinnedPruned` or produce `Indeterminate`. Exact BFS keeps command-count dominance enabled, and
difficulty axis C treats a reverse train approaching `EdgeFrom` as inbound to a switch. The board
view leaves a refused train at its station node during dwell, then evaluates the same track spline
from `1 - progress` while it reverses.

Executable coverage includes exact dwell boundaries, current-route re-entry, repeated refusal,
the strict-above-capacity failure boundary and attribution, collision-disabled forward/reverse travel,
fixed digest slot width, an exact-BFS recovery with zero pins, and a detected/traversed directed
cycle that solves with `BeamWidthUsed == 0`.

There is not yet a bespoke station-refusal animation or sound. The shipped presentation currently
communicates the rule through the stationary dwell followed by backward train movement.
