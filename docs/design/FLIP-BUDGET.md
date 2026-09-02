# Flip budget

**Status:** implemented. `win.perfectMaxSwitches` is a binding cap on accepted player flips.

## Runtime rule

When `perfectMaxSwitches >= 0`, the simulation accepts at most that many switch commands for the
entire run. Once the count is exhausted, further commands are ignored: they do not change a route,
do not increment `SwitchesUsed`, and do not change the outcome. A cap of zero therefore makes every
switch read-only. When the optional field is absent, the importer uses `FlipBudget.Unbudgeted`
(`-1`) and flips remain unlimited.

`GameSession.EnqueueToggle` enforces the same rule before appending to the command log. It returns
`true` only for a tap admitted into the authoritative replay. A budget-rejected tap therefore does
not appear as pending UI state and does not change `GameSession.FlipStatus`. Direct or historical
replay logs may still contain excess commands; `Simulation.Step` is the final authority and ignores
them deterministically.

Exhausting the cap is not a new failure outcome. Trains continue moving and the run may still win
or fail under the existing rules; only additional player route changes are unavailable.

## Cooldown interaction

Budget and cooldown count accepted commands, not attempted taps. An accepted flip with authored
`cooldownTicks: N` locks that switch for exactly the next `N` processing ticks. A press while it is
locked is ignored by the simulation and does not consume budget. `GameSession` rejects the same tap
before logging it, including a second tap while an earlier command for that switch is pending.

The current route occupies the low two bits of the existing `SwitchRoutes` byte and remaining
cooldown occupies its upper six bits. `SwitchState.Route`, `SwitchState.Cooldown`, and
`SwitchState.Pack` are the only decoding/encoding surface. `Pack(route, 0)` preserves every legacy
route byte, so no-cooldown replay digests keep their previous layout and values.

## Compatibility HUD surface

The existing `FlipBudgetStatus` and `FlipRating` shapes remain source-compatible, but the former
soft middle band is retired:

| Runtime snapshot | `Rating` | Display pips |
|---|---:|---:|
| Budgeted and `used <= cap` | `Perfect` | 3 |
| Fabricated budgeted snapshot with `used > cap` | `Solved` | 1 |
| Unbudgeted | `Ungated` | 0; hide the cap |

`Efficient` remains an enum member only for API compatibility and is not emitted by
`FlipBudget.Evaluate`. `TwoStarMaxSwitches` likewise remains a public member but aliases the hard
cap. In valid budgeted runtime state, `IsOverPerfect` is always false because excess taps cannot be
accepted.

During a running game, `GameSession.FlipStatus` uses the accepted log count so an admitted tap is
visible immediately, before its next-boundary application. Once the outcome is terminal it uses
`State.SwitchesUsed`. `SimulationState.FlipStatus` always reflects applied flips.

## Solver and content proof

The solver generates only commands the current state can accept. It prunes toggle successors when
the hard cap is exhausted, waits through cooldown, and verifies that every command in a returned
optimal log was applied. `EvaluateLog` preserves an externally supplied attempted log but reports
the simulation's accepted `SwitchesUsed` count.

Authored caps still require an artifact proof: import the actual level, solve it with the runtime
rules, replay the returned log, and require both a win and `SwitchesUsed <= perfectMaxSwitches`.
Merely loading a numeric cap does not prove the board is fair.

## Determinism and scope

The cap and cooldown rules are integer-only and have no `UnityEngine` dependency. The command
format, `TrainSlot` width, and switch-state byte width are unchanged. A later ladder rule adds a
two-byte transient digest mask only when stray waves and nonzero cooldown coexist; graphs without
that interaction keep their canonical digest width. The committed no-cooldown happy-path replay
retains its previous hash.

This does not implement campaign score, persistence, ticket bonuses, result-screen UI, or the
schema's `win.stars.two` / `win.stars.three` thresholds.
