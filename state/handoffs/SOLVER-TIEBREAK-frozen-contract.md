# SOLVER-TIEBREAK — frozen contract

**Session:** 2026-08-09
**Branch:** `task/solver-tiebreak-fix`
**Anchor before the required pre-commit refetch:** `origin/main` at `1a1bf0915e5ff4c829c7f4fa8b2cd78478f4bd3f`
**Ground truth:** `state/handoffs/PARALLEL-PUSH-2026-08-09.md` from
`origin/session/parallel-push-launch` (PR #63 was not on main at freeze preparation), plus
`state/handoffs/CM-C11.md` §§RULING/FINDING.
**Dependency:** Lane 3 merges after this lane.
**Merge authority:** HC-25 is not delegated; ask for a fresh word in this lane's chat after the PR
is reviewable and green.

The text from “Contract” through “Stop conditions” is frozen by the first lane commit. Later work
records evidence below the final boundary without silently changing the contract.

## Contract

Repair `LevelSolver`'s equal-completion/equal-command tie-break so its canonical winning log uses
the middle of a safe action window instead of the earliest safe boundary. The primary guarantees
remain unchanged: BFS is exact for boards with at most two switches, completion ticks outrank every
tie-break preference, fewer commands rank next, beam semantics and budgets do not move, and output
is deterministic across processes.

The change deliberately invalidates CM-C11's two-sided L006 characteristic pin
`(wins,losses,pinned) == (7,0,13)` / pessimistic `35`. Do not weaken that signal in advance. Capture
the old NUnit pin and the corpus-wrapper pin going red after the solver improves, report the newly
measured values to the human, and update both pins only after the human supplies the re-pin ruling.
In the same ruling exchange, report fresh F4-trigger measurements for L002, L003, L005, and L006;
record and publish them only with the human's acknowledgement.

### Executable ordering definition

1. A winning log with fewer `CompletionTicks` always wins.
2. At equal completion, a log with fewer commands always wins.
3. At equal completion and command count, a command is **mid-window safe** when its selected tick is
   a middle tick of the maximal contiguous interval in which changing only that command's tick keeps
   the replay Won at the same completion tick. An odd-width interval has one middle tick; an
   even-width interval uses the lower middle tick. Tick 0 has no special preference beyond being the
   lower boundary of its interval. Receipt order is preserved.
4. Existing lexicographic `(Tick, SwitchId)` ordering remains the final deterministic tie-break after
   the mid-window preference. Empty logs and logs with no wider same-completion interval remain
   unchanged.

For multi-command logs the observable requirement is stronger than a particular implementation:
each selected non-boundary command whose same-completion window is at least three ticks wide has a
one-tick margin on both sides when the other selected commands are held fixed. If centering one
entry destroys another entry's margin, the implementation must converge deterministically or stop;
it may not trade away exactness, command minimality, or a solver budget to force the result.

## Assumptions

- This is a solver-policy correction inside the existing ADR-0008 architecture, not a schema,
  dependency, assembly, threshold, or public result-shape change. No ADR is required.
- “Mid-window” means the executable definition above. The lower-middle/equal-margin choice retains
  the old lexicographic direction only where two central ticks are equally safe.
- Same-completion is load-bearing: a wider but slower winning log cannot displace a tick-minimal
  solution.
- `LevelGraph`, `Simulation`, `ReplayHasher`, validation stages, validator thresholds, level JSON,
  and content staging are frozen for this lane.
- Baseline on this host at main `1a1bf09`: filtered solver suite 24/24; full corpus plus both stress
  boards `RESULT: OK`, real 55.45 s after a cold restore. Baseline expansion counts include L006
  20,829, L701 146,942, and L702 16,839. These are evidence comparators, not new product constants.
- The old F4 pessimistic readings are L002 65%, L003 75%, L005 60%, L006 35%; every replacement
  number is a measured report value, never inferred from the intended algorithm.

## Acceptance criteria

1. **The tie-break selects a real middle and has red power.** A solver-domain test uses a board whose
   one-command same-completion winning interval is exactly ticks 0 through 4. The canonical result
   is tick 2, and independently replaying ticks 1 and 3 proves both are same-completion wins. The
   unmodified earliest-tick solver fails this test by returning tick 0. A second multi-command or
   corpus-backed check proves every non-zero selected command with a width of at least three retains
   winning `-1` and `+1` neighbors. Mutation proof: restore earliest-boundary selection and capture
   the exact failing assertion, then revert byte-clean.

2. **BFS exactness and deterministic ordering remain intact.** The independent brute-force tests
   still agree with `LevelSolver` on the minimal completion tick, minimal command count, and canonical
   log for the one- and two-switch fixtures; the unsolvable BFS proof stays `Unsolvable`; beam,
   pin-pruning, budget, replay-hash, result-proxy, and two-process `SOLVER_LOG` tests stay green.
   `NodesExpanded` for unchanged corpus/stress inputs must not increase merely to choose the final
   log; no solver bound or beam width changes.

3. **The L006 characterization pin fires before it moves.** After criterion 1's implementation and
   before either pin is edited, both
   `AlternationBandTests.Level_RetentionHolds_UnderBothNEWQ4Readings("L006")` and
   `tests/corpus/alternation-band.test.sh` fail because the live `(7,0,13)/35` expectation observed
   improved retention. Capture both exact messages. Report the new tuple, optimistic percentage,
   pessimistic percentage, command ticks, and action windows to the human. Only a fresh in-chat
   ruling authorizes re-recording the NUnit and wrapper expectations; mutation each new pin to a
   wrong value and prove its own enforcement point red, then revert byte-clean.

4. **F4 is re-measured and acknowledged, not silently rewritten.** One post-change full report
   supplies `(wins,losses,pinned)`, optimistic retention, pessimistic retention, selected command
   ticks, action windows, and solver expansion counts for L002, L003, L005, and L006. Present the
   numbers alongside criterion 3's re-pin request. After the human acknowledges them, append a new
   `CM-C11.md` §RULING-style subsection quoting the ruling/ack with the H-1-class relay caveat and
   update only this lane's named F4-trigger debt row in `state/PROJECT_STATE.md`.

5. **Stress-board and wall-clock budgets hold.** `bash scripts/validate-content.sh --out <tmp>`
   remains green over all campaign levels plus L701/L702; all rows remain within the existing
   `MAX_NODES_EXPANDED`, L701/L702 expansion counts do not increase, and timed before/after runs are
   recorded. A post-change run entering the CM-C11 stop-condition-7 class (one level dominates or
   the gate approaches multiple minutes) is a stop, not grounds to raise a budget. No clock read is
   added to Domain code.

6. **Repository gates and scope close cleanly.** `bash scripts/check.sh`, the focused solver and
   corpus tests, `bash scripts/test.sh`, and `bash scripts/build.sh` exit 0; the risk classifier is
   recorded; no dependency, immutable path, content JSON, validator threshold, non-Solver Domain
   source, Presentation, Bootstrap, ProjectSettings, or Lane-3 file changes. The lane handoff maps
   every criterion to evidence and `state/PROJECT_STATE.md` receives exactly this lane's task row
   plus the explicitly authorized F4-row update.

## Owned paths

- `unity/Assets/Scripts/Domain/Solver/**`
- solver/domain tests under `unity/Assets/Tests/EditMode/Pure/Solver/**`
- the existing CM-C11 corpus pin at
  `unity/Assets/Tests/EditMode/Pure/Corpus/AlternationBandTests.cs`
- the existing wrapper pin at `tests/corpus/alternation-band.test.sh`
- this contract and `state/handoffs/CM-C11.md`
- exactly one solver task row and the named F4-trigger row in `state/PROJECT_STATE.md`

Everything else is out of scope. In particular: no `content/**` edit; no
`unity/Assets/Scripts/Content/Validation/**` edit; no `config/validator_thresholds.json`; no
`Presentation/**`; no `Bootstrap/**`; no Lane-3 L011-L017 or band-wiring/pin paths; no immutable
path; no dependency manifest.

## TDD and evidence sequence

1. Commit this contract alone after the required `git fetch origin main` and baseline check.
2. Add the tie-break/margin tests and update the independent test-side oracle; run them against the
   old solver and commit the exact right-reason RED before implementation.
3. Make the smallest Solver-only change to green; run the focused solver suite and the independent
   exactness legs; commit a green milestone.
4. Run the old L006 NUnit and wrapper pins unchanged; capture their designed RED. Run the full report
   and present L002/L003/L005/L006 measurements for the human's combined re-pin/F4 ruling.
5. After the ruling only, update both pins and records; mutation-prove each enforcement point.
6. Run the full gates, risk classifier, scope audit, review, push, and open the PR. Ask for the
   separate HC-25 merge word; never infer it from the re-pin ruling.

## Stop conditions

- The mid-window rule cannot be implemented without changing completion optimality, command-count
  optimality, public result shape, BFS/beam boundary, a solver bound, or a non-Solver Domain source.
- The old L006 pin does not go red, or the two enforcement points disagree about the new tuple.
- A F4 number is not reproducible from the same report used for the re-pin request.
- L701/L702 expansions rise, a solver result changes verdict, or the corpus wall clock becomes
  hostile under CM-C11 stop condition 7.
- Any test must be weakened/deleted, any content/threshold needs editing, or any owned-path boundary
  is insufficient.
- The human has not yet ruled on the new L006 pin/F4 measurements: work may remain red and reported,
  but the pins and recorded rows do not move.
- The PR is not green/reviewable or HC-25 has not been freshly confirmed: do not arm or merge it.

---

## Evidence/status log (append-only after the frozen first commit)

### RED — 2026-08-09

Command:
`dotnet test dotnet/CatMetro.sln -c Release --no-restore --nologo --filter
'FullyQualifiedName~CatMetro.Tests.Solver.SolverDeterminismTests|FullyQualifiedName~CatMetro.Tests.Solver.SolverBfsTests'
--logger 'console;verbosity=normal'`

Result: 3 passed / 4 failed / 7 total, exit 1. All four failures are the intended old-order
discriminator:

- `TieBreak_PicksTheMiddleOfTheSameCompletionWindow`: expected tick 2, got tick 0.
- `OneSwitch_L001_MatchesBruteForce`: independent centered oracle expected tick 8, got tick 0.
- `TwoSwitch_MatchesBruteForce`: independent centered oracle expected first tick 6, got tick 0.
- `TwoCommandTieBreak_KeepsOneTickOfMarginOnBothSidesOfEveryDecision`: the same independent oracle
  expected first tick 6, got tick 0.

Controls stayed green: the one-switch Unsolvable proof, two in-process runs, and the cross-process
emission case. Production source was byte-unchanged for this run.

### Focused GREEN + mutation — 2026-08-09

- The same 7-test slice is 7/7 green; the full `CatMetro.Tests.Solver` filter is 25/25 green.
- The independent oracle and production agree on centered L001 tick 8 and two-command log
  `(S0,T6)+(S1,T6)`; emitted bytes are `000006000000010006000000`.
- `bash scripts/check.sh` exits 0.
- Mutation: bypass `CenterSameCompletionWindows` and return `best.log` directly. The named
  one-command test goes red with `Expected: 2 / But was: 0`. Reverted byte-clean; production-file
  SHA-256 before and after is
  `810a37ceac2980f9c04a06d5cbe64fb476d9fdf792810c31d0bfbbff15fce620`; the named test is green
  again after the revert.

### Designed characterization RED + re-measure — 2026-08-09

The old L006 expectations remain byte-unchanged pending the human ruling, and both enforcement
points fired independently:

- NUnit retention filter: 4/5 green, L006 red with
  `anchor characteristic drifted — retention=100% (wins=20 losses=0 pinned=0)
  windows=[24,24,24]`; expected `(7,0,13)`, observed `(20,0,0)`.
- `bash tests/corpus/alternation-band.test.sh`: exit 1 with
  `L006 anchor characteristic drifted (expected wins=7 losses=0 pinned=13):
  retention=100% (wins=20 losses=0 pinned=0) windows=[24,24,24]`.

The same post-change in-process corpus run measured the F4 set:

| level | selected ticks | wins/losses/pinned | optimistic / pessimistic | windows | nodes |
|---|---:|---:|---:|---:|---:|
| L002 | `[8,63]` | `20/0/0` | `100% / 100%` | `[18,37]` | 10,228 |
| L003 | `[9,77]` | `20/0/0` | `100% / 100%` | `[20,33]` | 12,860 |
| L005 | `[8,60]` | `20/0/0` | `100% / 100%` | `[18,29]` | 13,820 |
| L006 | `[43,83,123]` | `20/0/0` | `100% / 100%` | `[24,24,24]` | 20,829 |

Temporary measurement output in the corpus test was reverted byte-clean (file SHA-256 before and
after `37bfd6add6ddfde965c5e94cab06c4333908c1df1bad53e2bd4183f9c72b465c`).

Timed `validate-content.sh` post-change: `RESULT: OK`, real 51.30 s / user 45.39 s / sys 2.61 s,
versus cold baseline real 55.45 s / user 47.47 s / sys 3.08 s. Every one of the 12 level rows has
an identical before/after `NodesExpanded` value (delta 0); stress rows remain L701 146,942 and L702
16,839. Their retention also improves to `(20,0,0)` without a budget increase. This lane is now at
the contract's explicit human gate: no pin, CM-C11 ruling record, or PROJECT_STATE F4 row has moved.

### Human ruling + replacement-pin enforcement — 2026-08-09

The lane presented the designed two-pin RED and the F4 table above, then asked: *"may I re-pin L006
at both enforcement points to `(20,0,0)` / pessimistic `100`, and record your acknowledgement of
these F4 measurements?"* The human replied verbatim: **"Yes"**. This is an in-conversation,
agent-relayed ruling with the H-1-class confirmability caveat. It authorizes only the L006 re-pin and
F4 acknowledgement; it is explicitly not the separate HC-25 merge word.

Both authorized expectations now require `(wins,losses,pinned) == (20,0,0)` and pessimistic
retention `100`:

- Filtered NUnit retention tests: 5/5 green. Mutating only the L006 tuple to `(20,0,1)` makes the
  L006 case red: expected `(20,0,1)`, observed `(20,0,0)`; 4/5 remain green. The desired re-pinned
  test file was restored exactly (SHA-256
  `b481a2d2ec6c992b911030248a9d6abddaa548ec3d0238ab20fb4fe65493bba4`).
- `bash tests/corpus/alternation-band.test.sh`: green. Mutating only its L006 pessimistic
  expectation from `100` to `99` makes the wrapper red: expected `99`, observed `100`. The desired
  re-pinned wrapper was restored exactly (SHA-256
  `9ee2ec0327957cb15ef99e5ee38cf5695043a6d064b90d127e9d61b1c1c529e7`).

The ruling, replacement semantics, F4 table, unchanged node counts, and timing comparison are
recorded in `CM-C11.md` under a new §RULING-style subsection. `state/PROJECT_STATE.md` changes only
this lane's task row and its explicitly named F4-trigger row; the historical CM-C11 finding remains
untouched as required by the lane ownership boundary.

### Full-suite RED + human-authorized L701 re-pin — 2026-08-09

The first post-ruling `bash scripts/test.sh` run reached 727/728 dotnet tests before the first
wrapper failed on `StressBoards_AreValidated_WithTheQPStageSet`. Its pre-existing assertion required
`pinned=` but rejected `pinned=0`; the centered solver produced
`retention=100% (wins=20 losses=0 pinned=0) windows=[20,20,24,25,20]`. This is an additional stale
characterization, not a stress verdict, node-budget, or wall-clock regression. The test lies outside
the contract's initially enumerated test files, so the lane stopped without editing it and requested
an explicit ownership extension.

The lane recommended extending ownership to this single validation pin, re-recording the complete
result, mutation-proving it, and recording the ruling. The human replied verbatim: **"I will go with
your recommendation here."** This is an in-conversation, agent-relayed ruling with the H-1-class
confirmability caveat. It authorizes only this exact validation-pin edit and is not HC-25 merge
authority.

The exact replacement assertion is green. Mutation `pinned=0` → `pinned=1` produces the intended
RED in the same named test: expected `pinned=1`, actual `pinned=0`, differing at index 40. Reverting
the mutation returns SHA-256
`caa0e4c8438bc0042880e5c316220cc1eb92b212e111e9b017bc179575d6f77c` and the targeted test passes
1/1 again. Because the assertion pins the complete report rather than only requiring a nonzero
substring, drift sensitivity is retained.

### Full local gates + risk route — 2026-08-09

- `bash scripts/check.sh`: green.
- Focused `CatMetro.Tests.Solver`: 25/25; emitted canonical bytes
  `000006000000010006000000`.
- Correctly selected NUnit retention slice (`Name~Level_RetentionHolds_UnderBothNEWQ4Readings`):
  5/5; `bash tests/corpus/alternation-band.test.sh`: green with L006 100%/100%, `(20,0,0)`,
  windows `[24,24,24]`.
- Human-authorized L701 exact-pin slice: 1/1 after the mutation/revert proof above.
- Final `bash scripts/test.sh`: 15/15 wrappers; Unity EditMode 822/822 and PlayMode 137/137. This
  complete rerun passed the wrapper that had stopped the first post-ruling run at 727/728.
- `bash scripts/build.sh`: staged-content check green; interim harness reports no assembly target.
- Worktree audit after the gates: clean; no generated lockfile or staged-content drift.
- Mechanical classifier against `origin/main` (`11a3335`) on committed product/pin tip `6c7d7d7`:
  `RISKY`, rule `risk.test-semantic`, 9 files / 575 changed lines, production reach true,
  `security_review_required=false`. Sprint policy routes the PR to one fresh code-review round.

The final close-record commit changes only this contract and the already-authorized solver task row;
the classifier is re-run on that tip before review. HC-25 remains unasked and ungranted until the PR
is green and reviewable.

### Independent review round 1 — 2026-08-09 — NOT MERGEABLE / PERF FAIL

Draft PR #66 was classified from the GitHub-attested base/head by the protected-base probe as
`RISKY / risk.test-semantic`; `security_review_required=false`. One fresh code-review round and the
perf-budget leg inspected the PR-description copy of the frozen contract and exact
`11a3335...48a87b1` diff without rerunning the suite. Both legs failed the implementation:

- **F1 HIGH — final lex runs too early.** `wins.Sort(CompareWins)` selects the raw earliest log and
  discards equal-primary alternatives before centering. A later centered tick can reverse the final
  lex order, violating executable-order step 4.
- **F2 HIGH — non-convergence is silently accepted.** The arbitrary `4 * entryCount` pass cap
  returns the last feasible log even when the final pass changed it. The review supplied a
  two-coordinate winning relation whose midpoint sweeps cycle `(1,1) -> (2,2) -> (1,1)`; the
  returned boundary lacks the required margin.
- **F3 HIGH — the reference is structurally the SUT.** The test oracle selects the same raw winner,
  then copies the production scan order, midpoint formula, and pass cap. It can affirm F1/F2 rather
  than independently specifying canonical order.
- **P1 HIGH — refinement work escapes the declared work budget.** The cap permits `O(C^2*T)` full
  replays, each itself scanning a `C`-entry log over up to `T` ticks; the schema permits `T=4000`
  and has no solver-log command cap. Unchanged `NodesExpanded` is honest search accounting but not a
  bound on this new work. Current L701/L702 timing proves only the shipped corpus.

Re-pin/scope audit passed: no assertions were deleted; the L006 pins remain exact/two-sided; the
human-authorized L701 edit strengthens a partial assertion to an exact full report; forbidden paths
and the frozen contract prefix are untouched. Contract-drift verdict nevertheless fails on behavior.
The findings are durably relayed in PR #66's conversation; the PR remains draft and no HC-25 word
has been requested.

**Amended review-fix contract:** preserve the frozen requirements and resolve only F1–F3/P1. First
extract the current coordinate normalizer without behavioral change. Then prove RED with (a) an
equal-primary board whose raw and post-normalization lex winners differ, (b) the reviewer's cycling
relation, and (c) a truly exhaustive oracle that independently identifies fixed mid-window logs
before final lex. Production must normalize every relevant equal-primary candidate before final
lex, accept only a fixed-point result, and stop rather than return a changing fallback. Remove the
command-count-multiplied pass loop: at most one centering sweep plus one independent fixed-point
verification per candidate. Re-run the exact stress/timing/node evidence and the full gates, then
return the resolution to the existing independent reviewers. No unrelated edits.

Lessons audit found no matching recurrence. The review workflow calls for three new `observed` rows
(final tie-break applied before normalization; unverified bounded-iteration fallback; reference
oracle duplicates the SUT), but `docs/lessons.md` is outside Lane 2's exclusive ownership. It stays
untouched unless the human explicitly extends scope; this is disclosed, not silently waived.

### Review-resolution RED 2 + receipt-order ruling gate — 2026-08-09

The existing reviewers inspected local implementation tip `b73f73f` without rerunning the suite.
They confirmed that cycle/non-convergence now fails closed and the exhaustive fixed-point oracle no
longer copies production, but returned `NOT MERGEABLE / PERF FAIL` on four remaining properties:
raw-lex state dedupe can discard histories before terminal normalization; normalization failure is
misreported as pin-only `Indeterminate` and does not stop beam escalation; a centered result can
reverse receipt chronology; and all-candidate replay refinement still has no total work ceiling.
The performance ruling accepts one Solve-wide work meter using the existing `maxNodesExpanded`
value, with `NotFound(Budget)` on exhaustion and search-only `NodesExpanded` reporting.

Strict RED commit `2064a06` adds the reviewer's exact one-switch converged-state board plus the
abstract receipt relation. The focused slice is 0/2 for the intended reasons: exhaustive reference
expects `S0@(0,0)+(0,1)`, production returns reversed `S0@(0,1)+(0,0)`; the receipt relation expects
`[1,2]`, production returns `[3,1]`. Production source is unchanged in that commit.

The smallest neighbor-clipped-window experiment makes both new tests green, but exposes an
irreconcilable reading inside the current tests/contract: the previously supplied cycling relation
then becomes a fixed `[1,1]` instead of stopping, and the established two-command canonical log
moves from `(6,6)` to `(4,8)` (7/9 `SolverDeterminismTests` green). That movement would also require
fresh corpus/F4 measurement authority rather than silently superseding the human-acknowledged table.
The experiment was reverted byte-clean; worktree production remains at `b73f73f`. Human ruling is
required between (A) unrestricted same-completion windows with any chronology-reversing result
treated as an explicit stop, preserving the existing cycle semantics and acknowledged outputs, or
(B) receipt-clipped windows, accepting the changed cycle definition and a full re-measure/re-pin
gate. No production fix, pin, or acknowledged measurement moves before that ruling.

### Human ruling — Option A window semantics — 2026-08-09

The human ruled verbatim: **"Ruling: option A — keep maximal windows unrestricted as frozen, fail
closed if normalization would reverse receipt chronology."** Channel: direct in-conversation
directive, agent-relayed with the usual H-1-class confirmability caveat. The human's stated basis is
that A preserves the freshly acknowledged F4/L006 measurements and cycle-stop semantics, while B
would force a third measurement/re-pin cycle without player-visible gain; the fail-closed guard is
the conservative repository posture.

Scope is explicit and verbatim: **"this approval is window-semantics authority only — not the HC-25
merge word, which you'll ask for separately when the PR is ready."** The ruling additionally orders
the fail-closed path's own RED-first test and a mutation proof that deletes the guard, captures the
exact failure, and restores the desired bytes exactly. Lane 3's dependency is live; once #66 is
green and reviewable, this lane asks promptly for the separate HC-25 word.
