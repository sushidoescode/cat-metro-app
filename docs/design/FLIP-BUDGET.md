# Flip budget

**Status:** implemented (Domain + importer + HUD surface). Semantics chosen deliberately — see
"Why a rating and not a wall". Switching to a hard wall is a small, named diff, spelled out below.

## What was already there

`win.perfectMaxSwitches` has been authored in all 17 levels since the corpus was written. It is
1..4 across the corpus. Until this change it was parsed into `WinDto.PerfectMaxSwitches` and read
by **nothing** — it never reached the Domain, and the simulation never compared it to anything.

`SimulationState.SwitchesUsed` has existed just as long. It is incremented at `Simulation.Step`
step 1, once per applied command, and it already occupies digest offset 24.

So both halves of the constraint were sitting in the codebase, three files apart, never introduced.
This change is the introduction. It is one line in the importer plus a pure evaluator.

## Semantics: par, not a wall

Exceeding the budget does **not** fail the level. The budget sorts a win into one of three tiers:

| Tier | Condition | Stars on a win |
|---|---|---|
| `Perfect` | `switchesUsed <= par` | 3 |
| `Within` | `switchesUsed <= par + max(1, par)` | 2 |
| `Over` | anything above | 1 |

`WithinMax` is twice par, floored at `par + 1` so that a par-0 level ("solve it without touching a
switch") keeps a middle band instead of collapsing to pass/fail. Authored pars of 1..4 give
`Within` ceilings of 2..8.

A level that authored no budget reports `IsBudgeted == false`, tier `Within`, one star. No budget
means no free three-star.

### Why a rating and not a wall

1. **A wall is anti-cosy by the definition we are working to.** Project Horseshoe's coziness report
   defines cosy as "an absence of danger and risk" and warns specifically against "the threat of a
   lost opportunity". A budget that ends the run is exactly that threat.
2. **The complaint we are answering is staleness, not easiness.** The human's report is that every
   level feels the same. A rating gives every one of the 17 existing levels a second, harder,
   optional objective *without touching a single level file*. A wall would instead make some of
   them unwinnable until content is re-authored.
3. **It creates the replay loop, which is the thing we actually lack.** Research §3.3: the
   near-miss effect only motivates when the player genuinely chose the moves that fell short
   (Clark et al. — the effect requires perceived agency). "I won, but in 5 flips instead of 2" is a
   real, self-caused near miss with an instant retry. A loss screen is not.
4. **It costs nothing to reverse and something to un-ship.** Shipping the wall first and softening
   later means retuning 17 levels twice.
5. **It is free.** See below — this lands without moving a single golden replay hash. A wall does
   not.

### What makes it bite

A rating is only a constraint if the player can see it while they play. The HUD must show par and
the live count; that is the sibling lane's job and the surface is below. Without the HUD this is
inert bookkeeping — the design does not work as a results-screen-only feature.

## Digest safety (the property that let this land on the tested core)

Everything is a pure function of numbers the simulation already tracks. `par` lives on
`LevelGraph`, which is explicitly *not* digest material; the tier is computed on read. **No field
joins `SimulationState`, so `DigestLength` is unchanged and every golden replay hash still
matches.** `FlipBudgetTests.Par_DoesNotChangeASingleDigestByte` proves it by running the L001
golden log with and without a budget and comparing the digests byte for byte.

## The read-only surface the HUD binds to

The HUD lane owns `WavePreviewStrip.cs`; this lane does not touch it. The contract is these
members, all read-only, none of which can mutate a run:

On `CatMetro.Application.Session.GameSession` — **bind these**:

| Member | Type | Meaning |
|---|---|---|
| `FlipPar` | `int` | Authored par, or `FlipBudget.Unbudgeted` (-1) |
| `FlipsApplied` | `int` | Flips the sim has stepped. Lags a tap by up to one tick |
| `FlipsCommitted` | `int` | Applied **plus** taps committed but not yet stepped |
| `FlipStatus` | `FlipBudgetStatus` | Snapshot over `FlipsCommitted` |
| `FlipStars` | `int` | 3/2/1 on a win by tier, 0 otherwise |

On `CatMetro.Domain.FlipBudgetStatus`:

| Member | Type | Meaning |
|---|---|---|
| `Par` | `int` | Par, or -1 |
| `Used` | `int` | Flips counted |
| `WithinMax` | `int` | Ceiling of the 2-star band |
| `Tier` | `FlipTier` | `Over` / `Within` / `Perfect` |
| `IsBudgeted` | `bool` | **False → hide the counter entirely** |
| `Remaining` | `int` | `Par - Used`. Goes **negative** = flips over par |
| `IsOverPar` | `bool` | Past par. Never means the run is lost |
| `Stars` | `int` | Stars this count would earn *on a win* |

Also on `SimulationState`, for anything holding a raw state: `FlipStatus`, `FlipStars`,
`IsPerfectFlow`.

**Bind `FlipsCommitted`, not `FlipsApplied`.** A tap stamps a command that `Step` does not apply
until the next tick boundary, so `SwitchesUsed` lags the player's finger by up to a tick. The lever
art already solves this the same way through `GameSession.PendingToggleCount`, whose comment reads
"the visual must not lie". Painting the applied count makes the counter visibly stutter on every tap.

**Suggested copy** (not binding): `Remaining >= 0` → "2 flips left"; `Remaining < 0` → "3 over".
Never red, never a warning chime — over par is not a failure state.

## Perfect Flow

`product_spec.md:238` already specified the stamp: a win with zero rejections, zero Overloads, and
`switchesUsed <= win.perfectMaxSwitches`. It had never been evaluated. `FlipBudget.IsPerfectFlow`
and `SimulationState.IsPerfectFlow` are that evaluator, gate for gate.

## Switching to a hard wall

If the human decides over-budget should end the run, the change is small and the predicate already
exists and is already tested (`FlipBudget.ExceedsHardWall`, `FlipBudgetPolicy.HardWall`):

1. Add `OutOfFlips = 4` to `FailReason` (`Domain/Outcomes.cs`). **This is an ADR-0002 change** —
   the file states the enum is contract-tested at exactly three members and that members appear in
   player-facing copy and the analytics taxonomy.
2. Update `GuardTests.FailReason_HasExactlyThreeMembers` to four.
3. In `Simulation.Step`, after the step-1 command loop increments `SwitchesUsed`, add:
   `if (g.FlipPolicy == FlipBudgetPolicy.HardWall && FlipBudget.ExceedsHardWall(g.PerfectMaxSwitches, state.SwitchesUsed)) { state.Outcome = SimOutcome.MakeFailed(FailReason.OutOfFlips); return; }`
   (this requires carrying `FlipBudgetPolicy` on `LevelGraph` the way `Misdelivery` already is —
   one more trailing defaulted ctor parameter).

Digest length is unaffected either way; `Outcome.Reason` is already the last digest byte. But note
that goldens **do** move for any level that would now fail, and the solver's notion of a win
changes — a hard wall prunes the search space, so `LevelSolver` bounds and every band test that
asserts a solved level would need re-proving. That is the real cost, and it is why this is not the
default.

## What this does not do

- It does not implement scoring. `Score`/`Chain` are still 0 and `win.stars.two`/`three` are still
  compared against zero. Flip tier is the first real star signal, and it is deliberately named
  `FlipStars` rather than claiming the whole star system.
- It does not change any level JSON. Content is a sibling lane's.
- It does not spend `economy.perfectBonus`, which remains unread.
