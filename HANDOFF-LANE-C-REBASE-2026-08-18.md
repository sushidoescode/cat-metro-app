# Lane C continuation — rebase onto main and open the PR (2026-08-18)

For a fresh chat with no prior context. Self-contained. Written by the orchestrator session,
which holds the lane map. Supersedes the task sections of
`HANDOFF-LANE-C-CURATION-2026-08-17.md` and `HANDOFF-LANE-C-ADDENDUM-2026-08-17.md` — those
remain useful as background on *why* the curation was done.

## Status: the curation work is DONE and independently verified. Only integration remains.

Branch `task/GLB-CURATION` at **`18d5ba521bd306cb89e9bb5bf08afe8aa16b9ca0`**, pushed.
The previous chat completed both curation passes. The orchestrator **independently re-measured
every claim on disk** rather than trusting the report — all confirmed:

| Check | Required | Measured | |
|---|---|---|---|
| wave derivative components | 1 | **1** (15,000 tris) | ✅ |
| wave body bounds preserved | Y −0.4727..0.5001, X −0.3852..0.3853 | **identical** to pre-correction body | ✅ |
| loaf derivative | byte-identical, 1 component | **`9a7b2ef9…`, 1 component, 14,999 tris** | ✅ |
| provider-original backup | untouched | **`e3015351…` / `cc1ff113…`, exact** | ✅ |
| second backup generation | created | `GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3` | ✅ |

The body-bounds match is the strong evidence: only the 474-triangle detached blob was removed;
no cat geometry was lost. The previous chat also reports an independent review verdict of
MERGEABLE and a full suite at 28/28 — at the pre-rebase head.

## YOUR TASK — three steps, nothing more

### 1. Rebase onto main

**PR #94 merged on 2026-08-18** as squash commit `1b2ea7d`. This branch was based on
`16e20e3`, which no longer exists as a merge base — everything it contained is now inside that
squash. Rebase:

```bash
git fetch origin
git rebase --onto origin/main 16e20e3 task/GLB-CURATION
```

### 2. Resolve exactly these four conflicts — all documentation, no logic

```
docs/design/assets/GLB-DECIMATION-EVIDENCE.md   <- #94's final rg-fix commit also edited this
docs/lessons.md
state/PROJECT_STATE.md
tests/assets/glb-decimation-docs.test.sh
```

Rule: **take main's version of the decimation-pipeline content, then re-apply your curation
additions on top.** Do not revert any of #94's hardening — it went through its own multi-round
review and a 27/27 CI run. If a conflict looks like it needs a *logic* decision rather than a
merge of two documentation edits, stop and report rather than guessing.

### 2b. ALSO FIX: `main` is currently RED on a full clone — fold the fix into this PR

You will hit this the moment you run the gates, so fix it here rather than filing it.

```
glb-decimation-docs.test.sh: FAIL — declared production commit is not an ancestor of HEAD
```

**Cause.** The docs declare `ba3b31c52cb9536711488bef228b5221da908d0e` as the reviewed
production / reproduction base. That was a commit on `task/GLB-DECIMATION`; PR #94 landed as a
**squash** (`1b2ea7d`), so those individual commits are not in main's history and the ancestry
assertion fails. Verified by the orchestrator at a clean `origin/main` checkout.

**Why CI did not catch it, and why you must not "fix" it by trusting CI.** `.github/workflows/ci.yml`
uses `actions/checkout@v5` with no `fetch-depth`, i.e. a **shallow (depth-1) clone**. The
historical SHA cannot be resolved there, so the ancestry check passes vacuously. CI is green on
main right now while every full clone is red. **CI cannot protect this test.**

**The fix — a documentation repoint, not a test change.** Replace the declared base with the
squash commit in both places:

```
old: ba3b31c52cb9536711488bef228b5221da908d0e
new: 1b2ea7deb2626fc90e4f1a6d7508fcdaf048a72a

docs/design/assets/GLB-DECIMATION-EVIDENCE.md:11   "…reproduction base: `<sha>`."
state/PROJECT_STATE.md (GLB-DECIMATION row)        "final reviewed production base is `<sha>`"
```

This is safe and keeps the evidence honest: the orchestrator verified that
`scripts/decimate-assets.py` is **byte-identical** at `ba3b31c` and at `origin/main` — both
hash `dc0b371b63fe3d91c8f8beba5ff70541cac96636ab756dd0d35c4ce3c0f338a9`, which is exactly the
value the evidence doc pins as "Decimation driver SHA-256 at the reproduction base". So the
pinned driver hash remains true at the new base. **Verify that yourself before relying on it.**

While you are in that `state/PROJECT_STATE.md` row: it still reads `IN REVIEW` and describes the
branch as unmerged with a `Next:` plan that has already happened. Correct it to reflect that #94
merged as `1b2ea7d` on 2026-08-18.

**Do NOT weaken the ancestry test to make this pass.** The test is correct — it is catching a
genuinely stale claim. Repoint the claim.

Separately, **report but do not fix** the CI blind spot (adding `fetch-depth: 0` would be a
`.github/**` change — a risky path needing its own human-gated PR and security review). Note it
in your PR body so it is on the record.

### 3. Re-run gates, then open the PR against `main`

The full suite must pass at the **rebased** head — a review only certifies a frozen tree, and
the tree just changed. Note this is now meaningful in a way it never was before: the GLB test
suite executes end-to-end on CI for the first time as of #94, because the runner lacks `rg` and
those wrappers used to abort. **Never introduce `rg` into a test; use `grep -E`.**

Open the PR against `main` (it was correctly held until #94 merged). Body via `--body-file` —
PreToolUse hooks scan command prose and will deny a body that names immutable paths inline.

State in the PR body that the pre-rebase head `18d5ba5` carried an independent MERGEABLE review
and 28/28, and that this PR needs a **delta confirmation** over the rebase rather than a fresh
full review. Report your final head SHA back so the orchestrator can track it.

## Final hash chains — verified by the orchestrator, needed downstream

ADR-0013 (PR #96) pins a 60-hash approval manifest that is currently **stale** for these two
assets and must be re-pinned to exactly these values once this PR lands. Do not change these
bytes again without telling the orchestrator.

```
cat-blue-siamese-loaf
  source            257e59ebac613e3260bfd1161b228ec2be4aa7024969b4b1a3fec2366ffe0097
  source sidecar    93fd18c00ec6a1b369bed7849a0bfdb4c00cba5dfe6b16358995998a86bb1f66
  derivative        9a7b2ef923f923a78466f18d8bf0cfb82140aebbd30ba3e7cddd3f814fd2953c
  deriv. sidecar    2265679b91ff5feb5ab5ef7a277af6c3abfe1fda43e4dff2eccb5cceacc684e4

cat-yellow-longhair-wave
  source            bf4626c2a41214444a483bde1920c7fd95a06069feca202df860861edb540d64
  source sidecar    0bedeeb207fcb02277c7b0b1d0bcf8ec8118d4b0cf2e20abbaa3d85b1a64260f
  derivative        a3c4a363b06064ecc5dc03509c36ddd5ab91200a41314a3c674cd91ef4386696
  deriv. sidecar    9c7bd939fc493caa44d0250531e2137c8c848d5b9bbfc62de320e2dbab16317e
```

## Boundaries

- **Yours:** this branch only. Do not touch `unity/Assets/Scripts/**` (PR #95),
  `docs/adr/**` (PR #96), or any other lane's branch.
- **Immutable — never edit:** `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `evals/` (except `evals/results/`). `.github/**` and
  `unity/Packages/**` are risky paths requiring separate review — stay out.
- **Never weaken or delete a test to get green.** If a test seems wrong, stop and say so.
- Do **not** modify the curated GLBs further; their hashes are pinned above and a downstream
  ADR depends on them.
- Do **not** touch `curation-backups/` — it holds the only surviving provider-delivered
  originals for two paid-tier assets.

## Traps (each has cost a session real time)

- `rg` is unavailable on the CI runner and to child scripts; use `grep -E`. `grep -q` (BRE) is
  NOT a safe substitute where the pattern uses `|`, `(`, or `+`.
- `mktemp` returns EMPTY under the repo sandbox — affected tests fail spuriously; run unsandboxed.
- `dotnet restore` rewrites `dotnet/CatMetro.DailyTools/packages.lock.json`, and Unity builds
  drift 5 settings files. Revert before committing — **never `git commit -a`**.
- The worktree is under `/private/tmp`, which macOS reaps after ~3 days. **Push the same day
  you commit** — that reaper already destroyed two irreplaceable files this week.
- Never read `.env`. Never touch the physical Pixel `2G0YC5ZF7Z056Q`. Never run a Play upload.
