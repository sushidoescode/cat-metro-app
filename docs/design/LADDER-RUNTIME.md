# Ladder runtime mechanics

**Status:** implemented in the pure Domain and solver.

## Token byte and station matching

`TrainSlot.Color` remains one byte in the canonical ten-byte train slot. `CatToken` packs base
color in bits 0–2, `(shape - Round)` in bits 3–4, `stray` in bit 5, and `express` in bit 6; bit 7
is reserved. A round token without flags is byte-identical to its legacy color value.

A concrete cat delivers only when its base color occurs in `StationAccepts` and its shape equals
`StationShape`. Train-side Wild ignores both color and shape; station-side Wild is still an exact
color token and does not accept a concrete cat. A wrong shape follows the same refusal counter,
eight-tick platform dwell, exceptional reverse, and capacity rule as a wrong color.

## Express and stray

Express trains never occupy `NodeQueueCounts` or `NodeQueueSlots`. A blocked express emission uses
`TrainState.ExpressHeldAtSource` and retries the then-current selected route each processing tick.
At a junction it takes the selected route when open/free; otherwise it immediately reverses its
incoming traversal without a dwell, gate check, direction check, or alternate-route search. A
concrete station mismatch does the same and increments `Rejections`; Wild express delivers.

A stray never delivers. Its station arrival uses ordinary eight-tick refusal even if its token is
also Wild or express. At a switched junction it captures the current route, then attempts one
automatic press for subsequent trains. The captured route remains attached if the stray must wait
in a queue. The automatic press ignores `perfectMaxSwitches`, does not increment `SwitchesUsed`,
and obeys/starts the authored switch cooldown. Multiple later passes may press again after unlock.
If a player tap was already accepted before a stray establishes cooldown during the intervening
processing tick, that tap retains receipt priority at the next boundary. Only the first due tap
for that switch may claim the priority; it applies the route change and starts a full authored
cooldown. The automatic-origin witness expires after that boundary and is never written to the
command log.

## Second-train collision rule

Collision checks are enabled only when `LevelGraph.CollisionsEnabled` is true. `FailReason.Collision`
was appended as byte value 4; the published values 1–3 did not move. A processing tick fails when
two or more trains arrive at the same node, or when forward and reverse trains occupy one edge.
Matching station deliveries are deferred until after that check, so collision wins the tick.

With collision checks disabled, the legacy coexistence rule remains: opposing trains may share and
pass on an edge. Same-direction mouth admission is unchanged.

## Replay and graph scope

Switch cooldown and token identity reuse existing canonical bytes. `TrainSlot`, the switch byte,
and the command format are unchanged. A graph combining any stray wave with any nonzero switch
cooldown adds a two-byte transient automatic-origin mask to its canonical digest; other graphs
retain their previous digest widths, so round/no-flag/no-cooldown replays retain their legacy bytes
and hash. Solver search runs the same state machine, including collision failures, express holds,
stray automatic presses, receipt priority, and shape refusal.

Tunnel and hold flags do not add motion state: they are ordinary routed edges in the Domain. Their
flags remain available to presentation and liveness checks.
