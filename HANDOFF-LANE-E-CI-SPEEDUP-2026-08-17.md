# Lane E brief — cut CI wall-clock (2026-08-17)

For a fresh Codex chat. Self-contained; do not assume any other conversation's context.
Written by the orchestrator session (Claude), which holds the lane map — report your
branch/PR back to the human so the orchestrator can track it.

## Why this lane exists

CI is **~2 h, ~3 h when runs are concurrent**, and it is the throughput bottleneck for
the whole project: every lane's every push pays it, and this week two days were lost to
serial review rounds while CI sat either idle or re-running. Cutting it compounds across
every future push.

Measured on `main` at `3115ebd` by the orchestrator (verify yourself, don't trust this
number): **29 `dotnet test` / `dotnet run` invocations across 12 wrapper files**:

```
tests/analytics/queue.test.sh          tests/content/importer.test.sh
tests/corpus/alternation-band.test.sh  tests/corpus/queue-reading-band.test.sh
tests/daily/daily-pipeline.test.sh     tests/domain/determinism.test.sh
tests/save/save.test.sh                tests/solver/solver.test.sh
tests/taxonomy/taxonomy.test.sh        tests/validation/validator.test.sh
scripts/validate-content.sh            scripts/validate-dailies.sh
```

## Scope

Reduce CI wall-clock **without weakening a single assertion**. Likely levers, in the
order the orchestrator would try them:

1. **Deduplicate byte-identical solution builds/runs.** Several wrappers invoke the same
   dotnet solution with identical inputs. Build once, reuse the artifact; memoize
   identical invocations behind a content-addressed key (inputs + solution hash).
2. **Gate the long tail.** `tests/daily/daily-pipeline.test.sh`'s horizon proof adds
   roughly 20–41 minutes to *every* PR. `state/PROJECT_STATE.md` records a pending human
   ruling: gate the long leg behind a nightly run or a PR label. **Ask the human for that
   ruling before implementing it** — it trades per-PR coverage for speed and is their call.
3. **Parallelize independent wrappers** if the harness runs them serially and they have
   no shared mutable state. Prove the independence; don't assume it.

### ⚠️ Two things you must NOT memoize

`tests/solver/solver.test.sh` and `tests/domain/determinism.test.sh` **deliberately run
the solution twice and compare the outputs** — they are repeatability/determinism proofs.
Memoizing either turns a real proof into a tautology that passes by construction. That is
exactly the "never weaken a test to get green" failure this repo forbids. If your change
makes either of them run the engine fewer times, you have broken them.

## Evidence the PR must carry

- Before/after wall-clock for the full `bash scripts/test.sh`, measured the same way.
- Proof that the wrapper count and pass count are **unchanged** (currently 27 wrappers;
  a green run reads `test: 27/27 passed`).
- For each memoized invocation, a mutation proof: break the underlying code and show the
  wrapper still goes RED. A cache that hides a real regression is worse than slow CI.
- Explicit statement that solver + determinism still invoke the engine twice each.

## Honest scoping note

The win does **not** reach PR #94 (`task/GLB-DECIMATION`) — that branch is frozen under
review and will not rebase to pick this up. The benefit lands on #95, #96, the curation
lane, and everything after.

## Ownership boundaries (collision map, as of 2026-08-17)

- Yours: `scripts/test.sh` and the wrapper files above, plus any new CI helper.
- NOT yours: `.github/**` (risky path — CI *workflow* changes need independent security
  review and are hook/CODEOWNERS-protected; stay in `scripts/` unless the human
  explicitly opens that door), `unity/Assets/Scripts/**` (Lane D, PR #95),
  `docs/adr/**` (Lane B, PR #96), `task/GLB-DECIMATION` (PR #94), the curation lane's
  `scripts/` decimation additions — coordinate with the orchestrator before touching
  `scripts/decimate-assets.py`, `scripts/blender_decimate.py`, `scripts/glb_metrics.py`,
  or `tests/assets/**`.
- Immutable (never touch): `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `evals/` (except `evals/results/`).
- Branch: **`task/CI-SPEEDUP`** off current `origin/main`. ONE lane per branch.

## Process rules (binding — this repo runs the Forge workflow)

- Read `AGENTS.md`, `state/PROJECT_STATE.md`, `docs/constitution.md` first.
- Frozen contract = first commit. Never weaken or delete a test to get green.
- Fresh-context review before merge; **cap: two rounds per artifact**, then findings
  become named follow-up debt on the PR and the human decides. Census merge-record on the PR.
- PR body via `--body-file` (PreToolUse hooks scan command prose).
- Get it right in one push: each push costs a full CI run, which is the very thing this
  lane is trying to reduce.

## Traps (each has burned a session)

- **`rg` is a shell function in Claude Code, not a binary** — it is unavailable to child
  scripts and absent on the CI runner. Never introduce `rg` into a test; use `grep -E`.
  (This exact bug is why PR #94's CI failed twice today.)
- **`mktemp` returns EMPTY under the repo sandbox** — affected tests fail spuriously; run
  them unsandboxed.
- Unity `-runTests` must NOT get `-quit` (exits before tests run: exit 0, no results XML).
- Every Unity build drifts 5 settings files + `packages.lock.json`; `dotnet restore`
  rewrites `dotnet/CatMetro.DailyTools/packages.lock.json`. Revert before committing —
  **never `git commit -a`**.
- Never read `.env`; never touch the physical Pixel `2G0YC5ZF7Z056Q`; never run any Play
  upload.
