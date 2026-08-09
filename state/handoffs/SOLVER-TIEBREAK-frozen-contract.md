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
