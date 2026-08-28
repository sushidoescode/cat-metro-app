# Lane C brief — source-art curation: plinth strip + fragment strip (2026-08-17)

For a fresh Codex chat. Self-contained; do not assume any other conversation's context.
Written by the orchestrator session (Claude), which holds the lane map — report your
branch/PR back to the human so the orchestrator can track it.

## The ruling this lane executes

The human ruled in the orchestrator chat on 2026-08-17 (agent-relayed here — H-1-class
evidentiary caveat, record this caveat in your frozen contract): **uniform NO-plinth**,
i.e. accept the orchestrator's recommendation from the annotated contact sheet:

1. **Strip the display base disc from `cat-blue-siamese-loaf`** — the only model of the
   15 that has one (verified by rendering all 15 decimated derivatives from two angles).
2. **Strip the small detached floating fragment near `cat-yellow-longhair-wave`'s feet**
   — a disconnected loose component (generation debris; it would render as a floating
   blob in-game).
3. The other 13 assets are **byte-untouched** — hash-pin them in your tests.

The ruling is taste-final; do not re-litigate strip-vs-keep. Anything visual must be
RENDERED AND LOOKED AT — code-green is not evidence.

## Branch strategy — this lane is STACKED, read carefully

The decimation pipeline (`scripts/decimate-assets.py`, `scripts/blender_decimate.py`,
`scripts/glb_metrics.py`, `scripts/glb-silhouette.py`, tests under `tests/assets/`)
exists only on PR #94's branch, not on main yet.

- Branch **`task/GLB-CURATION`** off the **current** `origin/task/GLB-DECIMATION` head
  (`git fetch origin task/GLB-DECIMATION` first, then branch off `FETCH_HEAD` and RECORD
  the SHA you used in your frozen contract). **That branch is NOT frozen — it is actively
  moving**: another session owns it and pushed ~20 commits on 2026-08-17 (`16e20e3` →
  `c62d6b8`), so any SHA quoted in a handoff is stale on arrival. **NEVER push to
  `task/GLB-DECIMATION` itself** — another lane owns it; a push there restarts a ~3 h CI
  run and voids its review.
- Build and evidence the work now, push your branch, but **HOLD the PR** until #94
  merges (the orchestrator/human will say when). #94 lands as a squash, so afterwards:
  `git rebase --onto origin/main <the-base-SHA-you-recorded> task/GLB-CURATION`, re-run
  the gates at the rebased head, then open the PR against main.

## Scope (contract skeleton — restate, tighten, freeze as your first commit)

1. Curate at the SOURCE level: edit the two source GLBs under
   `unity/Assets/Art/Generated/incoming/` (990 MB dir, gitignored, exists only on this
   machine — treat as irreplaceable INPUT; never delete or overwrite a source in place,
   write curated copies and promote via the pipeline's existing transactional pattern).
   - Fragment strip (wave): separate-by-loose-parts, delete components below a size
     threshold you justify; the main body is one large component.
   - Plinth strip (loaf): remove the thin disc at min-Y that extends beyond the body
     footprint; state your geometric criterion and show it selects ONLY the disc.
   Blender headless is the established tool (see `scripts/blender_decimate.py` on your
   base for the invocation pattern and its sandbox/temp-dir traps).
2. Re-run the existing decimation pipeline for exactly those two assets so derivatives
   AND sidecars regenerate coherently (cats target 15k tris — verify the regenerated
   pair still meets target).
3. Update the evidence docs your diff invalidates (`docs/design/assets/
   GLB-DECIMATION-EVIDENCE.md` / `GLB-DECIMATION-METRICS.json` entries for the two
   assets) — in YOUR diff, never #94's.
4. Evidence (all three required):
   - Before/after silhouette renders of both assets, actually looked at, committed to
     the evidence location the repo already uses;
   - a full 15-asset re-render proving the other 13 are visually unchanged;
   - hash pins: the 13 untouched derivatives byte-identical, asserted by a test.
5. RED-first where testable: a test that fails on the plinthed/fragmented derivatives
   and passes on the curated ones (e.g. loose-component count == 1; no sub-min-Y disc
   beyond footprint), plus the 13-asset hash-pin test.

## Ownership boundaries (collision map, as of 2026-08-17)

- Yours: `scripts/` curation additions, the two regenerated derivatives + sidecars,
  the evidence-doc entries for those two assets, your tests under `tests/assets/`.
- NOT yours: anything else on #94's tree (frozen); `unity/Assets/Scripts/**` (Lane D,
  PR #95); `docs/adr/**` (Lane B); `unity/Packages/**`; `.github/**`.
- Immutable (never touch): `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `evals/` (except `evals/results/`).

## Process rules (binding — this repo runs the Forge workflow)

- Read `AGENTS.md`, `state/PROJECT_STATE.md`, `docs/constitution.md` first.
- Frozen contract = first commit. Never weaken or delete an existing test to get green
  — the pipeline's own tests must stay green over your changes.
- Fresh-context review before merge; **cap: two rounds**, then findings become named
  follow-up debt on the PR. Census merge-record comment on the PR.
- PR body via `--body-file` (PreToolUse hooks scan command prose).

## Traps (each has burned a session)

- `mktemp` returns EMPTY under the repo sandbox — run affected tests unsandboxed.
- `rg` may be a shell function, unavailable to child scripts — use `grep` in scripts.
- The GLB test suite is heavy (`tests/assets/glb-decimation-pipeline.test.sh` is 8.4k
  lines); scope your runs to what your diff touches while iterating, full suite before
  freeze.
- Never read `.env` (generation keys are human-armed only — you need no generation;
  curation is pure local geometry).
- Never touch the physical Pixel `2G0YC5ZF7Z056Q`; no emulator needed for this lane.
- Worktrees go under `/private/tmp/` — note macOS reaps /tmp files older than ~3 days;
  push your branch the same day you commit.
