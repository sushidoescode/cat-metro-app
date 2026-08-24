# Lane D phase 2 — implement the cat wiring (2026-08-18)

For a fresh chat with no prior context. Self-contained. Written by the orchestrator session,
which holds the lane map. The governing contract is
`state/handoffs/CM-CATS-WIRE-frozen-contract.md` **on your branch** — read it in full; it is
frozen and authoritative. This file only carries what changed since it was frozen.

## Where things stand

Branch `task/CM-CATS-WIRE` = **draft PR #95**, head `5c1cf96`. Phase 1 is complete and correct:
the contract is frozen and the RED tests exist. **Your job is phase 2: the implementation that
turns them green.** Do not re-freeze the contract, do not rewrite the tests to suit the
implementation, and do not weaken a test to reach green — if a test looks wrong, stop and say so.

The RED suite you must satisfy:

```
EditMode  CatModelManifestMapTests
  OracleControl_SwappedBoardMappingIsRejected          (a control — must stay RED-on-mutation)
  ClosedMap_ResolvesTheEightFrozenManifestRows
  ClosedMap_UnknownKeysReturnNull_NeverAnArbitraryCat
  Budgets_PinBothSurfaceCaps_CombinedTriangles_AndSourceBytes
PlayMode  CatModelWiringTests
  Board_MapsLiveCats_CapsAtNine_AndSharesPrefabAssets
  SafetyOracleControl_RejectsAnImportedCollider        (a control)
  Board_WithNoCatalog_UsesTheCurrentCapsuleQuietly
  Home_PartialCatalog_ReplacesOnlyResolvedDistricts_AndPreservesPins
  Home_FullCatalog_UsesTheThreeFrozenModels_WithinItsOwnCap
```

## What changed since your contract froze — this is the whole point of this file

The contract was frozen against `origin/main` at `3115ebd`. Four things have changed:

**1. PR #94 (GLB-DECIMATION) MERGED.** `main` is now `1b2ea7d`. The phase boundary your
contract names — "production implementation is blocked until PR #94 merges" — **is discharged.
You are cleared to implement.** Your branch is BEHIND main; update it before you start
(`git fetch origin && git merge origin/main`, or rebase — your call, but do it first and
re-run the gates after).

**2. The plinth ruling is RESOLVED, and it touches one of your slots.** Your contract says
"the pending human plinth ruling remains open" and forbids asserting against a display base.
The human ruled **uniform no-plinth** on 2026-08-17, and the curation lane executed it. Two
assets were geometry-edited, and **one of them is in your map**: `cat-blue-siamese-loaf`
(Home `ParkedDistrictB`). It no longer has its display disc.

The contract's prohibition still stands and you should still honour it — do not assert against
a model's base, child count, exact bounds, or vertex count. The ruling removes a *risk* to your
slice (your Home district can no longer show a base disc that the other two lack); it does not
license new geometric assertions.

**3. `cat-yellow-longhair-wave`'s detached fragment is fixed.** Your contract correctly excluded
that asset from this slice. It has since been corrected to a single connected component, so the
exclusion is no longer protective — but leave the map as frozen. Changing it is a contract
amendment, not a convenience.

**4. All 15 derivatives are final and verified.** The orchestrator independently measured every
asset on disk: all ten cats are single-component; the props are legitimately multi-part (trees 3,
desk-clutter 7, toy-engine 3 = body + two wheels, kiosk 2 = body + interior fixture). The two
curated assets carry these verified hashes — **do not modify these bytes**, a downstream licence
ADR (PR #96) pins them:

```
cat-blue-siamese-loaf  derivative  9a7b2ef923f923a78466f18d8bf0cfb82140aebbd30ba3e7cddd3f814fd2953c
cat-yellow-longhair-wave derivative a3c4a363b06064ecc5dc03509c36ddd5ab91200a41314a3c674cd91ef4386696
```

## Asset reality you must design around

The derivatives live at `unity/Assets/Art/Generated/incoming/decimated/` and that directory is
**gitignored** (`.gitignore`, `incoming/`). They are on this machine only; they are NOT tracked
by #94 or any ref. This is exactly why your contract makes absent assets a normal state rather
than an error: **on a clean clone the catalog and models simply are not there, and the app must
render the current placeholders quietly, with no crash and no error log.** That fallback path is
the one a CI machine and any other developer will actually execute — treat it as the primary
path, not the edge case.

## Evidence — the part that has caught every real defect on this project

**Anything visual must be RENDERED AND LOOKED AT. Code-green is not evidence.** This is a
binding project rule and it has caught three defects in two days that green tests missed. Your
PR must carry rendered frames of the real scene showing cats on the Board and on the shipped
Home, and you must state that you looked at them and what you saw.

Capture on the **emulator** (`-s emulator-5554`) or the Unity editor. **Never touch the physical
Pixel `2G0YC5ZF7Z056Q`.** Kill the emulator when captures are done — it burns ~1000% CPU.
Be aware of a known capture trap: Overlay canvases never render into `Camera.targetTexture`, so
RT-based capture rigs silently produce frames missing chrome; take taste frames from the screen
or Game view instead.

## Boundaries

- **Yours:** `unity/Assets/Scripts/Presentation/**` and your own tests, per the contract's
  "Exact surfaces" section. The contract limits you to `BoardView`'s `train:<slot>` visual and
  `HomeScreenView`'s three `ParkedDistrict*` images. The interactive Home pins
  (`PinL001`, `PinRingL001`, `PinDaily`), their hit rects, priorities, pulse, colors, and
  callbacks are explicitly **excluded** — do not touch them.
- **Not yours:** `docs/adr/**` (PR #96) · `scripts/`, `tests/assets/`, and the curated GLBs
  (PR #98 — active) · `unity/Packages/**` (dependency surface: ADR + review) · `.github/**`.
- **Immutable — never edit:** `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `evals/` (except `evals/results/`).
- Do not modify anything under `unity/Assets/Art/Generated/incoming/` — including
  `curation-backups/`, which holds the only surviving provider-delivered originals for two
  paid-tier assets.

## Process

Read `AGENTS.md` and `state/PROJECT_STATE.md` first. Minimal implementation to green, then the
full gate suite (`bash scripts/check.sh`, `bash scripts/test.sh`, `bash scripts/build.sh`).
Fresh-context review before merge — **two rounds maximum**, then remaining findings become named
follow-up debt on the PR and the human decides. Census merge-record on the PR. PR body via
`--body-file` (PreToolUse hooks scan command prose and will deny a body naming immutable paths).
Report your final head SHA back so the orchestrator can track it.

## Traps (each has cost this project real time)

- **`rg` does not exist on the CI runner** and is a shell function, not a binary, in agent
  shells — never put it in a test; use `grep -E`. Plain `grep` (BRE) is not a safe substitute
  where a pattern uses `|`, `(`, or `+`.
- **CI checks out at depth 1**, so any test depending on git history passes vacuously there.
  Do not rely on CI to prove a history-dependent claim.
- Unity `-runTests` must **not** be given `-quit` — it exits before tests run (exit 0, no XML).
- Every Unity build drifts 5 settings files, and `dotnet restore` rewrites
  `dotnet/CatMetro.DailyTools/packages.lock.json`. Revert before committing — **never
  `git commit -a`**.
- `mktemp` returns EMPTY under the repo sandbox; run affected tests unsandboxed.
- Android swallows the **first** touch after focus — proven, not a hit-rect bug; don't chase it.
- Never read `.env`. Never run `fastlane supply` or any Play upload.
