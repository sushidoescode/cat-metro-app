# Mis-delivery: what happens when a cat reaches the wrong station

**Status: PROPOSAL with a recommendation. The recommendation is implemented but NOT enabled for
any content — imported levels stay on the existing pin. Enabling it is a human decision.**

This is a genuine design question, not a gap to be quietly filled, so this document lays out four
candidate semantics and argues for one rather than presenting a fait accompli.

## What happens today

`Simulation.Step` step 5, on a cat arriving at a station whose `accepts` does not include its
colour:

```
throw new NotSupportedException(
    "pinned NEW-Q4: a non-matching cat arrived at a station — rejection/reverse traversal is
     out of CM-C1 scope (state/backlog.md Q-B, criterion 14)");
```

Two things about this pin that make it more than dead scaffolding:

1. **The solver depends on it.** `LevelSolver` catches `NotSupportedException`, increments
   `PinnedPruned`, and records the message in `SolveResult.FirstPinMessage`. That is how "route the
   red cats into the blue station" gets pruned out of the search. `SolverPinAndBaselineTests`
   asserts `FirstPinMessage` contains `"NEW-Q4"` for L001. **Redefining mis-delivery changes the
   solver's search space, not just a gameplay rule.**
2. **The state is left half-stepped.** The throw happens after the arrival has already been written
   (`State = AtNode`, `EdgeId = -1`, `NodeId = station`), before `state.Tick` advances and before
   the overflow and win/time checks run. `Step` takes `ref SimulationState` and nothing in the
   Domain catches, so a caller that swallowed the exception would be holding a corrupt state. Any
   new semantics must resolve the arrival inside step 5, not by catching further out.

It has bitten two lanes writing tests, and it blocks several rungs of the mechanic ladder.

## Options

| # | Semantics | Can the Domain express it? | Digest / goldens | Cost |
|---|---|---|---|---|
| **A** | **Refused, cat goes home.** Station declines, `Rejections++`, the cat leaves the board, run continues. | **Yes, today.** `SimulationState.Rejections` is already a digest field at offset 16, documented "stays 0 in CM-C1 (rejection pinned, NEW-Q4)" — reserved for precisely this. | No layout change. Levels that never mis-deliver are byte-identical. | **S** |
| **B** | **Refused, train reverses** and rides back for another try. | **No.** Traversal is structurally one-way: `OutgoingEdgeFor` only matches `EdgeFrom[e] == node`. Needs a per-train direction bit, and `TrainSlot` is exactly the documented 10 bytes. | Train slot 10 → 11 bytes ⇒ `DigestLength`'s `10*nTrainsMax` term changes ⇒ **every golden hash breaks**, and regenerating goldens is human-only (ADR-0002). | **L** |
| **C** | **Costs a life or a star.** | Star: same as A plus a threshold rule. Life: **no** — there is no lives system, and the research recommends against adding one. Failing the level needs a `FailReason`, and the spec'd one (`PlatformOverflow`) actively throws; adding a member is an ADR change. | Layout-safe; goldens move for any level that now fails. | **M**, and process-gated on a human ADR decision |
| **D** | **Cannot be routed there** — the switch refuses to send a cat somewhere it will be rejected. | Needs per-train lookahead at every junction. | Layout-safe. | **M** |
| **E** | **Falls through and queues at the station node.** (Cheapest possible — delete the guard.) | Yes, accidentally. A station is a leaf, so `OutgoingEdgeFor` returns -1 and the cat enqueues at the terminal node, eventually tripping `QueueOverflow`. | No change. | **S**, but semantically incoherent |

## Recommendation: A — refused, and the cat goes home

**Design.** Wrong-colour cat reaches a station. The station politely declines. The cat hops off and
goes home. `Rejections` increments. Nothing fails. You have lost the delivery, so a board whose
wave supply exactly meets `win.deliveries` becomes unwinnable-by-clock rather than
unwinnable-by-rule — which is a nudge the player can read and retry, rather than a wall.

**Why:**

1. **It is the only option the current Domain expresses without touching the digest.** Everything
   else either breaks every golden replay hash (B), needs an ADR-gated enum change (C), or is
   incoherent (E). The `Rejections` field was reserved for this three years of commits ago.
2. **It matches the cosy stance the flip budget already takes.** A mistake costs rating, not the
   run. Two mechanics with one consistent rule is a game; two with different punishment models is
   a mess.
3. **It composes with Perfect Flow for free.** `product_spec.md:238` already gates the stamp on
   *zero rejections*. Option A makes that clause meaningful for the first time without inventing
   anything — the spec already assumed rejections would exist and be counted.
4. **D is actively wrong for this game.** If a mistake is impossible, the flip budget has nothing
   to constrain and the routing decision stops being a decision. Auto-correction is the one option
   that makes the game *less* of a puzzle, which is the opposite of this lane's purpose.
5. **B is the most interesting mechanic and should not be discarded** — a cat that rides back
   around is a genuinely good puzzle element. But it needs bidirectional traversal, which is the
   same blocker as `edges[].oneWay: false`, and it should ride *that* work rather than driving it.

**Rejected:** B (breaks every golden; revisit with bidirectional edges), C-as-lives (no lives
system; research §3.5 recommends against), C-as-failure (ADR-gated, and anti-cosy),
D (removes the decision), E (a queue at a terminal node is not a rule anyone can explain).

## What is implemented

`MisdeliveryPolicy` on `LevelGraph`, a trailing defaulted constructor parameter:

- `Pinned` (**default**) — throws exactly as before. Every existing fixture, every golden, and the
  solver's `PinnedPruned` accounting are unchanged.
- `RefuseAndSendHome` — option A, fully unit-tested in `MisdeliveryTests`.

`LevelImporter` passes nothing, so **all 17 authored levels remain on `Pinned`** — asserted by
`FlipBudgetImportTests.ImportedContent_StaysOnTheMisdeliveryPin`. Nothing about live behaviour
changes until someone decides it should.

## To enable it — the open question for the human

Turning it on is one argument at `LevelImporter.cs` (the `new LevelGraph(...)` call), either
unconditionally or driven by a new level-JSON field. Before that:

1. **Does the human accept option A?** That is the decision this document is asking for.
2. **The solver must be re-proved.** Under `RefuseAndSendHome` the solver no longer prunes
   mis-delivering branches, so its search space grows and `PinnedPruned` drops to zero. Beam widths
   and the band tests' solvability proofs need re-running before any level ships with it. **This is
   the real cost of enabling, and it is larger than the Domain change.**
3. **Does a rejection cost score?** `product_spec.md` says delivery +100 / rejection −25 with a
   chain reset. Scoring does not exist yet, so this is deferred, not decided here.
4. **Should `FailReason` ever gain a rejection member?** This lane says no. It is left open.
