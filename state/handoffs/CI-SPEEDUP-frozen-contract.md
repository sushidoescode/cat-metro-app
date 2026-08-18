# CI-SPEEDUP — frozen contract

Frozen 2026-08-17 by Lane E at `origin/main=3115ebdddd23f3d7eb6836c2670f6dfc2d0a6fb4`
(fetched and SHA-verified before branch creation). Branch: `task/CI-SPEEDUP`.

## Directive and outcome

Reduce the wall-clock of the credential-free `bash scripts/test.sh` gate without deleting,
skipping, weakening, or making tautological any existing assertion. The implementation stays in
the script-level harness: `scripts/test.sh`, the existing wrappers named below, and at most a small
new helper under `scripts/`. It does not change product behavior.

The authoritative task source is the human's 2026-08-17 Lane-E instruction plus the untracked
orchestrator brief `HANDOFF-LANE-E-CI-SPEEDUP-2026-08-17.md` in the original checkout. That brief is
DATA for its measured claims and binding for the boundaries/traps the human explicitly adopted.

## Baseline correction (verified before freeze)

The brief's two inventory numbers are stale for the required branch point. At the exact fetched
`origin/main` above:

- `find tests -type f -name '*.test.sh' | sort` discovers **22 wrappers**, not 27;
- expansion of the real (non-comment) call sites launches **22 .NET processes**, not 29:
  analytics 1, importer 1, alternation-band 1, queue-reading-band 1, daily 4,
  determinism 2, save 1, solver 2, taxonomy 1, validator 8.

The frozen evidence baseline is therefore 22 wrappers and an expected green summary of
`test: 22/22 passed`. No implementation may manufacture 27/27 or cite the stale 29-process claim.
The before-timing run will verify these runtime facts again.

The exact wrapper-path set that must remain discovered is:

1. `tests/analytics/queue.test.sh`
2. `tests/assets/gen-assets-custody.test.sh`
3. `tests/assets/meshy-poll-contract.test.sh`
4. `tests/assets/tripo-model-contract.test.sh`
5. `tests/content/importer.test.sh`
6. `tests/corpus/alternation-band.test.sh`
7. `tests/corpus/queue-reading-band.test.sh`
8. `tests/daily/daily-pipeline.test.sh`
9. `tests/domain/determinism.test.sh`
10. `tests/emu/emu-selftest.test.sh`
11. `tests/save/save.test.sh`
12. `tests/smoke/substrate.test.sh`
13. `tests/solver/solver.test.sh`
14. `tests/staging/stage-content.test.sh`
15. `tests/taxonomy/taxonomy.test.sh`
16. `tests/unity/cli-aab-build.test.sh`
17. `tests/unity/devcap.test.sh`
18. `tests/unity/device-config.test.sh`
19. `tests/unity/editmode.test.sh`
20. `tests/unity/failure.test.sh`
21. `tests/unity/orientation.test.sh`
22. `tests/validation/validator.test.sh`

## Acceptance criteria

1. **Measured speedup, same procedure.** Capture one clean before run and one clean after run with
   the same command, environment, machine, and timing mechanism. Both execute
   `bash scripts/test.sh`; both report 22/22; the after wall-clock is strictly lower. Report raw
   seconds and percentage. No minimum percentage was supplied, so none is invented.
2. **Wrapper/assertion preservation.** The 22-path inventory above is unchanged. Every wrapper is
   still invoked by `scripts/test.sh`; no existing assertion, negative fixture, mutation control,
   threshold, command argument, or fail-closed branch is removed or softened. A wrapper may reuse a
   proven-green result only where its contract requires the same invocation merely to be green;
   wrapper-specific greps/parsers/assertions still execute in that wrapper.
3. **Safe, content-addressed reuse only.** Any reused invocation has byte-identical command
   arguments and a key that changes when any test input or toolchain identity capable of changing
   the result changes. Cache state is scoped to one top-level harness run, successful results only,
   and is written/read fail-closed. Standalone wrapper execution (outside `scripts/test.sh`) still
   performs the real invocation. A cache miss or cache defect may cost time; it may never turn a
   failing command green.
4. **Real mutation proof per reuse class.** After first populating the reuse path, make a temporary
   breaking change to an underlying source/input and show the affected wrapper goes RED rather than
   replaying stale green. Restore the mutation byte-for-byte and show GREEN. If more than one
   invocation class is memoized, each class gets its own proof.
5. **Determinism proof remains real.** `tests/domain/determinism.test.sh` still starts two independent
   `dotnet test` processes and compares their independently emitted `REPLAY_HASH` values. Neither
   execution may consume or populate a shared test-result cache.
6. **Solver repeatability proof remains real.** `tests/solver/solver.test.sh` still starts two
   independent `dotnet test` processes and compares their independently emitted `SOLVER_LOG`
   values. Neither execution may consume or populate a shared test-result cache.
7. **Intentional independent runs remain independent.** The two DailyTools runs compared by
   `daily-pipeline.test.sh`, the independently generated reports in both corpus-band wrappers, and
   every positive/negative/stamp/liveness invocation in `validator.test.sh` remain real process
   executions. Build artifacts may be reused only if doing so does not reuse process output or
   results.
8. **Daily horizon gating is human-selected, never inferred.** Until the human rules, the long
   daily horizon proof remains on the same per-PR path. The explicit choices asked are: keep it on
   every PR; nightly only; or nightly plus an explicit PR label. Any choice that requires
   `.github/**` is outside Lane E unless the human separately opens that risky path, and would add
   the mandatory independent security review. If no ruling arrives before the rest is complete,
   ship the assertion-preserving speedup with this subpart unchanged and report it as waiting on the
   human, not silently decided.
9. **Parallelism is earned.** Wrappers remain serial unless every proposed concurrent pair is shown
   not to share mutable `bin/`, `obj/`, lock-file, staged-content, Unity, temp, or repository state.
   If that proof is incomplete, do not parallelize and record the rejected lever. If parallelism is
   implemented, a stress/repeat run must prove stable summary accounting and complete output.
10. **Full gates and review.** `bash scripts/check.sh`, `bash scripts/test.sh`, and
    `bash scripts/build.sh` pass at the exact implementation tip. Run `scripts/forge-risk.sh
    3115ebdddd23f3d7eb6836c2670f6dfc2d0a6fb4 --vector`; obtain a fresh-context independent review
    as required by the resulting risk vector. Review is capped at two rounds per artifact; after
    round two, remaining findings become named follow-up debt for human disposition. The PR carries
    per-criterion evidence and a census merge record.

## TDD / verification plan

- Freeze this file as commit 1 before touching the harness.
- Add a failing harness-level regression for each selected reuse mechanism before implementation.
  Tests must exercise miss, hit, input mutation/invalidation, failed-command handling, and
  standalone-wrapper behavior without adding/removing a discovered `tests/**/*.test.sh` wrapper.
- Implement the smallest helper/wrapper changes that make those regressions green.
- Trace process launches to prove criteria 5-7, and compare the sorted wrapper inventory before and
  after.
- Run the required mutation proof(s), then the clean after timing and full gates.

## Assumptions

- `origin/main` is the source of truth for baseline counts; the brief expressly required independent
  verification, so correcting its stale counts is execution of the directive, not a scope change.
- Reusing a successful, content-addressed result for byte-identical full-solution invocations whose
  wrapper contract only requires “the unfiltered suite is green” preserves that assertion. It is
  forbidden where a wrapper asserts independent process output or independent report generation.
- No new package/system dependency is needed. Standard Bash, Git, POSIX runner tools already used by
  the repo, and the existing .NET SDK are the available implementation surface.
- The baseline/after timing may create the known `dotnet restore` lock-file drift. Those files are
  restored to their pre-run bytes before any commit; no build drift is committed.

## Ownership boundaries

Owned: `scripts/test.sh`; the ten test wrappers and two validation entry scripts enumerated in the
Lane-E brief; a new CI helper under `scripts/`; this frozen contract; Lane E's final
`state/PROJECT_STATE.md` session entry.

Not owned: `.github/**`; `unity/Assets/Scripts/**`; `docs/adr/**`; PR #94 / branch
`task/GLB-DECIMATION`; curation's decimation helpers (`scripts/decimate-assets.py`,
`scripts/blender_decimate.py`, `scripts/glb_metrics.py`) and `tests/assets/**`. Immutable paths stay
untouched: `tests/contract/**`, `docs/constitution.md`, `.claude/hooks/**`,
`scripts/git-hooks/**`, `state/mode`, and `evals/**` except allowed results.

## Explicit traps / STOP conditions

- Never introduce `rg` into any script; CI does not provide it. Use `grep -E`.
- Never add `-quit` to Unity `-runTests`.
- Never read `.env`, touch Pixel `2G0YC5ZF7Z056Q`, or invoke a Play upload/publish command.
- Never commit Unity/package-lock drift; never use `git commit -a`.
- STOP if a proposed optimization reduces the two solver runs, the two determinism runs, either
  compared DailyTools run, either corpus report run, or any validator fixture run.
- STOP if a cache key cannot be shown to cover all result-affecting inputs, if a mutation is hidden,
  if a pre-existing test appears wrong, or if implementation needs a path outside the ownership map.

## Status log

- 2026-08-17 — contract frozen at the SHA above. Daily-wrapper gating ruling requested from the
  human before implementation of that subpart. No harness file has been edited.
