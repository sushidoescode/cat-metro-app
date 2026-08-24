# Lane D brief — wire decimated cats into Board/Home: contract + RED tests (2026-08-17)

For a fresh Codex chat. Self-contained; do not assume any other conversation's context.
Written by the orchestrator session (Claude), which holds the lane map — report your
branch/PR back to the human so the orchestrator can track it.

## The task (phase 1 of 2 — ONLY phase 1 is in scope now)

**Phase 1 (now):** freeze the task contract and author the failing (RED) tests for
replacing the Home/Board grey placeholder rectangles with the generated cat models.
**Phase 2 (implementation) is BLOCKED until PR #94 (GLB-DECIMATION) merges** — the
assets and their import pipeline land with it. Do not implement past RED. Stop after the
RED suite is committed and demonstrably red for the right reasons.

## Asset facts your contract can rely on

- 15 decimated derivatives + JSON sidecars at
  `unity/Assets/Art/Generated/incoming/decimated/` (10 cats ≤ 15k tris, 5 props ≤ 10k
  tris; 0.6–2.9 MB each; ~24 MB total). On the main checkout's disk today; the canonical
  landing is PR #94.
- **The plinth ruling is PENDING** (human taste call: strip vs keep display base discs).
  The contract must NOT pin plinth geometry — treat base presence as a variable.
- Known art blemish: `cat-yellow-longhair-wave` has a small detached floating fragment
  near the feet (orchestrator render, 2026-08-17); a curation pass may regenerate or
  strip it. Don't hard-pin that asset's exact bounds/vertex counts.

## What the contract should cover (skeleton — restate, tighten, freeze)

1. Which surfaces change: the shipped Home screen's pins/districts and/or the Board's
   cat placeholders (`unity/Assets/Scripts/Presentation/**`). Name the exact components.
2. Asset selection/mapping: manifest-id → model file; behavior when a model is missing
   (fallback to the current rectangle, never a crash — cats are gitignored on fresh
   clones per the custody posture, so absence is a NORMAL state your tests must pin).
3. Perf guardrails: total triangle/memory budget on screen at once.
4. Visual evidence criterion: rendered frames (editor or emulator `-s emulator-5554`)
   are required evidence at implementation time — code-green is not evidence. Phase 1
   only needs the criterion written in.
5. RED tests: EditMode/PlayMode tests per criterion, each failing against current main
   for the right reason (assert on the new seam, not on incidental current behavior).

## Ownership boundaries (collision map, as of 2026-08-17)

- Yours: `unity/Assets/Scripts/Presentation/**` + your tests. No other active lane
  touches these files.
- NOT yours: `Palette.cs` + `HomeScreenStyleTests` pins (merged #90/#92 taste work —
  build on them, don't repin); `unity/Packages/**` (dependency surface: ADR + review);
  `scripts/**` (Lane C's surface); `docs/adr/**` (Lane B's surface); anything on
  `task/GLB-DECIMATION` (PR #94, frozen under review).
- Branch: **`task/CM-CATS-WIRE`** off current `origin/main`. ONE lane per branch.

## Process rules (binding — this repo runs the Forge workflow)

- Read `AGENTS.md`, `state/PROJECT_STATE.md`, `docs/constitution.md` first.
- Frozen contract = the branch's first commit. RED-first. Never weaken or delete an
  existing test to get green.
- Immutable paths (never touch): `tests/contract/`, `docs/constitution.md`,
  `.claude/hooks/`, `scripts/git-hooks/`, `state/mode`, `evals/` (except
  `evals/results/`), `.github/**`.
- Fresh-context review before merge; **cap: two rounds per artifact**, then findings
  become named follow-up debt on the PR. Census merge-record comment on the PR.
- Do NOT `gh pr update-branch 94` — the orchestrator batches branch updates.

## Unity traps (each has burned a session)

- Unity `-runTests` must NOT get `-quit` (exits before tests run: exit 0, no XML).
- Every Unity build/test run drifts 5 settings files + `packages.lock.json` — revert
  before committing; NEVER `git commit -a`.
- `mktemp` returns EMPTY under the repo sandbox — run affected tests unsandboxed.
- `rg` may be a shell function, unavailable to child scripts — use `grep` in tests.
- Emulator only, `-s emulator-5554`; NEVER the physical Pixel `2G0YC5ZF7Z056Q`; kill
  the emulator when captures finish (it burns ~1000% CPU).
- PR bodies via `--body-file` (hooks scan command prose).
- Never read `.env`.
