# Flip mastery rating

**Status:** implemented. This is a soft mastery rating, not a failure condition and not the
campaign's unfinished score-star award system.

## Decision

`win.perfectMaxSwitches` is par. Going over par never changes `SimOutcome`: a solved board stays
solved. The pure Domain evaluator maps the applied or committed flip count to:

| `FlipRating` | Condition | Display pips |
|---|---:|---:|
| `Perfect` | `used <= par` | 3 |
| `Efficient` | `used <= par + max(1, par)` | 2 |
| `Solved` | above the efficient ceiling | 1 |
| `Ungated` | no par is authored | 0; hide the mastery target |

The `max(1, par)` term preserves a real middle band for a future par-zero board. An absent optional
`perfectMaxSwitches` imports as `FlipBudget.Unbudgeted` (`-1`).

This is intentionally softer than a hard inventory wall. The engagement research recommends
agency, legible near-misses, and low punishment for cosy play. A player may finish freely, then
choose to replay a route more efficiently. The hard-wall alternative remains small because
`FlipBudgetStatus.IsOverPerfect` is already the exact predicate it would need.

## Authored-par gate

Loading a par is not proof that it is fair. `PerfectMaxSwitchesCorpusTests` enumerates every
`content/levels/L*.json`, runs the real `LevelImporter` and `LevelSolver`, and requires a winning
trace whose `SwitchesUsed` is at or below the authored par. It is a blocking NUnit test, not a
report-only check.

All 17 current levels pass that proof. If a future level fails, do not weaken the assertion or ship
an impossible target: remove its optional `perfectMaxSwitches` until the content is corrected. The
importer will then expose that level as `Ungated`.

## Read-only HUD surface

The HUD gets one read-only entry point: `GameSession.FlipStatus`. It returns the immutable
`FlipBudgetStatus` value:

| Member | Meaning |
|---|---|
| `PerfectMaxSwitches` | Authored par, or `-1` |
| `Used` | Count represented by this snapshot |
| `TwoStarMaxSwitches` | Inclusive efficient-band ceiling |
| `Rating` | `Ungated`, `Solved`, `Efficient`, or `Perfect` |
| `IsBudgeted` | Whether the HUD should show a target |
| `RemainingToPerfect` | `par - used`; negative means flips over par |
| `IsOverPerfect` | True only after a real par is exceeded |
| `RatingStars` | Numeric 0/1/2/3 display value; not a persisted campaign-star award |

During a running game, `GameSession.FlipStatus` uses `Log.Entries.Count`, so a committed tap appears
immediately instead of lagging until the next simulation boundary. Once the outcome is terminal,
it uses `State.SwitchesUsed`, so a late command that can never be applied cannot alter the result.
Code holding only a raw Domain state can read the corresponding `SimulationState.FlipStatus`,
which always reflects applied flips.

`WavePreviewStrip.cs` is deliberately untouched.

## Determinism and scope

The evaluator is integer-only, deterministic, and has no `UnityEngine` dependency. Par lives on
the immutable `LevelGraph`, outside the canonical state digest. Rating is computed on read from the
existing `SwitchesUsed` counter, so no digest field or command format changed. A test runs the same
winning replay with and without par and compares every digest byte.

This change does not implement score, persistence, ticket bonuses, results-screen UI, or the
schema's `win.stars.two` / `win.stars.three` thresholds. Those fields remain a separate unfinished
score system.

## If the human later chooses a hard wall

The rating evaluator does not need replacement. After each applied toggle, the simulation could
consult `state.FlipStatus.IsOverPerfect` and produce a new out-of-flips outcome. That decision
would still require a new `FailReason`, failure UI/copy, solver pruning, and renewed replay/golden
verification. None of that is implemented here.
