# CONTRACT CM-C13 — alternation-band re-author L007–L010

**Branch:** `task/CM-C13-alternation-reauthor`
**Frozen:** 2026-08-12 against `origin/main` `1744fac` (contains #66's
tie-break-fixed solver and the Addendum v2.1/v2.2/v2.3 sections), as the FIRST commit
on the branch.
**Lane stewardship:** coordination-session-ADOPTED (the Lane 9 chat was never opened;
the human directed the coordination session to take the lane). Authorship: this
session's implementer agents. Review: fresh-context independent legs ON the PR — the
author never reviews its own diff. Merge: per constitution Amendment 1 under Addendum
v2.3 (no per-merge human word; census facts recorded on the PR).
**Ground truth:** Addendum v2.1 Lane 9 row (on main) as amended by v2.2's count-block
generalization (the CM-R09.1 count BLOCK belongs to whichever lane's merge changes the
`content/levels/**` file count — currently Lanes 3 and 11-B; this lane NEVER
re-records it) and governed by v2.3 (merge authority).

## Rulings honored

1. **L006 is LOCKED** — product_spec anchors clause + AMD-02 + the human's CM-C11
   Option-1 ruling. `content/levels/L006.json` byte-unchanged; its anchor-equality
   criterion, per-level entries, and `(wins=20 losses=0 pinned=0)`/pessimistic-100
   pins stay intact. Verified byte-unchanged at design close; re-verified at PR time.
2. The CM-C11 re-scoped ≥70% pessimistic bar BINDS L007–L010 under the fixed solver.
3. Design finding recorded as a reusable lesson: 4+ decision windows stacked on one
   multi-route switch exhausts the shared work meter (NotFound(Budget)) — distinct
   from CM-C11's uncapacitated-dead-end lesson; relayed cross-lane during the wave.

## Acceptance criteria

1. `content/levels/L007.json`–`L010.json` re-authored as REAL multi-decision boards
   (the #62 duplicate-boards anti-pattern removed, proven mechanically): per-level
   `switchesUsed` ≥3, `StaticAnalysis Pass` (no unreachable decoys), novelty pairwise
   ≥1.5 across L006–L010 and vs L001–L005, with the before/after comparison in the PR.
2. Difficulty: §22 per-level ladder as authored targets; band envelope 0.18–0.28 with
   80–88% FA; computed-vs-authored ±0.05 asserted vs the ladder (CI comparison inert
   corpus-wide — pre-existing human-decided gap; directional evidence only);
   AMD-02 duration ~25–40 s per board.
3. Solver certification per level on the fixed solver: Solved, BFS-exact
   (`beamWidthUsed=0`), optimistic AND pessimistic retention ≥70% (design measured
   100%/100%), safe windows ≥ the 12-tick floor; full metrics table in the PR.
4. Staged copies via `scripts/stage-content.sh --apply`, byte-identical, with metas.
5. The band test suite re-authored honestly against the new boards:
   `AlternationBandTests.cs` L007–L010 portions re-recorded two-sided (L006 criteria
   untouched) and `BandFixtures.cs`'s topology-coupled witness generalized
   (`TrapWitness` replacing the old `GateToHoldWitness`; helpers verified referenced
   nowhere outside this test file). Mutation proofs for the re-recorded load-bearing
   pins (named mutation → red with exact message → byte-clean revert).
6. `tests/corpus/alternation-band.test.sh`: L007–L010 expectations re-recorded as
   needed; the CM-R09.1 count expectation BLOCK untouched (every "N/30"-style literal
   exactly as origin/main shows at PR time; rebase over Lane 3's 17/30 re-record if it
   lands first).
7. Full `bash scripts/validate-content.sh` OK; corpus suite green (design: 44/44);
   full dotnet solution green; Unity EditMode batch compile+run recorded (the
   Is.AnyOf masking lesson); `bash scripts/check.sh` + `bash scripts/test.sh` green.
   Known false-positive: `tests/validation/validator.test.sh` criterion 17c diffs
   against HEAD and fails on uncommitted trees — resolves once committed; verify green
   post-commit.
8. Fresh-context review legs posted ON the PR (risk gate priced; expect RISKY); merge
   per Amendment 1/v2.3; census facts on the PR at merge.

## State writes (exhaustive)

(1) this file; (2) ONE `state/PROJECT_STATE.md` row at merge (second-lander takes the
update-branch merge; >140-line append → STOP and ping the human rotation ask);
(3) Known-debt bullets this contract names: none.

## Must not touch

Everything else — `content/levels/L001..L006` and `L011+`, GameRoot/Bootstrap,
`LoadNextBandTests`, `Domain/**`, `docs/plan/**`, `unity/Packages/**` (revert
`packages-lock.json` after editor sessions), Scene/ProjectSettings/URP,
`Presentation/**`, ValidationStages thresholds, the CM-R09.1 count block.
