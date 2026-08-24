# Orchestrator handoff — 2026-08-17

For the incoming Claude chat that will act as **main orchestrator and monitor**. Prior handoffs:
`HANDOFF-GLB-DECIMATION-2026-08-16.md` (decimation detail + exact SHAs) and
`HANDOFF-2026-08-15.md` (repo-wide state + operating traps). Read both.

---

## 1. STATE: what is merged vs unmerged

### Merged to `main` (= `3115ebd`) — 8 PRs, all landed
`#83` keystore ignores · `#85` Daily Line wiring · `#87` **ORIENT-LOCK** (portrait lock — the
"must stay straight" bug) · `#88` **RICH-ASSETS** (Meshy/Tripo generation pipeline) · `#89`
**EMU-RIG** (emulator self-test rig) · `#90` **BEAUTIFUL-MENU** (§7 palette → `Palette.cs` +
warm-tabletop Home) · `#92` MENU-POLISH · `#91` **CM-BOOT-HOME** (Home is now the SHIPPED launch
screen; superseded the Q-5 law) · `#93` generation fixes (Tripo `model` required; Meshy poll
corrupted its own stdout data channel).

### Unmerged
- **`#94` GLB-DECIMATION — JUST OPENED, CI RUNNING.** `task/GLB-DECIMATION` @ `16e20e3`,
  **114 commits / 20 files / ~29.8k insertions**. This is the big one and it is
  **functionally complete**: 25,352,000 → 199,998 triangles (−99.2%), 990 MB → 24 MB, all 15
  assets on target (15k cats / 10k props), 15 derivatives + sidecars on disk at
  `unity/Assets/Art/Generated/incoming/decimated/`. HUMAN-MERGE.
- **`#65` ART-DIORAMA** — pre-existing, `DIRTY` (conflicts), stale for days. Decide: rebase or close.
- ~20 local `task/GLB-*` branches; 4 pushed. The coordinator has absorbed the hardening by
  rebase, so `merge-base --is-ancestor` shows the side branches as "pending" — that is
  misleading. **Verify by content, not ancestry** (the silhouette 8M+POSITION fix and the 97
  transaction refs are confirmed present in the coordinator).

### Bottom line
**The engineering is done; the bottleneck is process.** Two days of serial review rounds
produced excellent work and zero merges. CI sat idle the whole time — which is why opening #94
was the first action of this session.

---

## 2. WHY IT IS SLOW, AND HOW TO SPEED IT UP

Diagnosis of the Codex session's pattern (it did genuinely good work — this is about throughput):

1. **Serial review-of-review recursion.** Reviewers audited implementations, then reviewers
   audited the *test oracles*, then those were re-reviewed. Each round is ~30–60 min of agent
   time and they ran one-at-a-time. It caught real bugs (an exit-0-on-corrupted-output race is
   serious) but there is no bound on the recursion.
2. **CI never started.** ~3 h per run, and nothing was queued for two days. Pure dead time.
3. **One session, one lane.** Independent work streams (licence ADR, plinth curation, Board/Home
   wiring, `#65`) were all blocked behind the decimation review.
4. **Content filters killed agents mid-report** when describing redaction/leakage cases,
   forcing restarts with fresh agents.

### The fix — parallelize by independent surface
These four streams touch **disjoint files** and can run as separate chats right now:

| Lane | Work | Files touched | Blocked by |
|---|---|---|---|
| **A — Monitor/merge (this orchestrator)** | Watch #94 CI, disposition review findings, merge, keep branches un-stale | none | nothing |
| **B — Licence ADR** | `docs/adr/00NN-generated-asset-licensing.md`; Meshy/Tripo paid-tier terms; all sidecars carry `plan_tier: paid` | new ADR + maybe PIPELINE.md | nothing — **start now** |
| **C — Plinth / source-art curation** | Decide + implement: some models sit on a display base disc (siamese yes, tabby no). Strip all or keep all | `scripts/`, derivative regeneration | needs a human taste call first |
| **D — Board/Home wiring** | Replace grey rectangles with decimated cats; the beautiful menu is already shipped | `unity/Assets/Scripts/Presentation/**` | wants #94 merged (needs the assets), but the *contract + RED tests* can be written now |

**Rules that keep parallel lanes from colliding** (learned the hard way this week — two sessions
raced on one branch and forced a two-round review): one lane per branch; announce the branch in
the PR before starting; never push to a branch another lane owns; a review only certifies a
**frozen** tree, so freeze before requesting review.

### Bound the review recursion
Recommend an explicit cap: **two review rounds per artifact, then the human decides.** Findings
after round 2 become named follow-up debt in the PR, not another round. The constitution's
Addendum already prices rounds this way; the Codex session exceeded it.

---

## 3. IMMEDIATE ACTIONS FOR THE ORCHESTRATOR

1. **Watch `#94`.** CI ~2–3 h. If green → the human merges (HUMAN-MERGE by contract).
   If red → triage; note the suite calls **`rg` (ripgrep)**, which GitHub's ubuntu runners ship
   but which is not a POSIX guarantee — check that first if tests error oddly.
2. **Do not let #94 go stale.** When any other PR merges, `gh pr update-branch 94` — each push
   restarts a ~3 h CI run, so batch updates rather than trickling them.
3. **Start Lane B (licence ADR) immediately** — zero dependencies, and it is a hard gate: nothing
   ships in the Play binary without it.
4. **Get the human's plinth ruling** (Lane C is blocked on taste, not code).
5. **Decide `#65`** — rebase or close; it has been `DIRTY` for days.
6. **Housekeeping:** ~31 GLB worktrees under `/private/tmp/catmetro-glb-*` (each Unity worktree
   `Library/` is ~8.3 GB — disk hit 3.7 GB free earlier this week). Two carry uncommitted
   one-off Editor scripts that exist nowhere else: `DevfixUrpSetup.cs` (wt-devfix) and
   `SpikeUrpSetup.cs` (wt-spike-urp) — commit, record, or discard; human call.

---

## 4. TRAPS (do not re-learn these)

- CI is ~2 h, ~3 h when several run concurrently. A merged PR leaves siblings `BEHIND` →
  `gh pr update-branch` → another full run.
- **`mktemp` returns EMPTY under this repo's sandbox** — any test using it fails spuriously.
  Run those unsandboxed.
- **`rg` is a shell function in Claude Code**, not a binary — unavailable to child bash scripts.
- Unity `-runTests` must **not** get `-quit` (it exits before tests run: exit 0, no results XML).
- Every Unity build drifts 5 settings files + `packages.lock.json` — revert before committing,
  never `git commit -a`.
- The headless emulator burns ~1000 % CPU — kill it when captures are done.
- Android swallows the **first** touch after focus (proven with a dead-space tap; not a bug).
- **Never touch the physical Pixel `2G0YC5ZF7Z056Q`** — scope every adb call to `-s emulator-5554`.
- PreToolUse hooks scan command **prose**: a PR body naming immutable paths gets denied — use
  `--body-file`. That is the system working; report it, never route around it.
- `.env` is permission-denied to agents **by design**. Never read it; generation is armed only by
  the human running an in-session `!` command.

---

## 5. PROCESS RULES THAT BIND EVERY LANE

Frozen contract as the branch's first commit → RED-first test → minimal implementation → **fresh
context review (never review your own diff)** → census merge-record comment on the PR. Never
weaken or delete a test to reach green. Immutable: `tests/contract/`, `docs/constitution.md`,
`.claude/hooks/`, `scripts/git-hooks/`, `state/mode`, `evals/` (except `evals/results/`).
Anything visual must be **rendered and looked at** — code-green is not evidence.
