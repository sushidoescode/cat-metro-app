# SESSION HANDOFF — CAT METRO phases 6–10 (written 2026-08-04, post-#10)

For a fresh session (any model, any account). The repo is the source of truth; this file is the
map. Read order: `state/PROJECT_STATE.md` → this file → the contract you are executing in
`state/backlog.md`. Project law: `AGENTS.md` (+`CLAUDE.md`), `docs/constitution.md` (binding).

## Where the project stands
- Shipaton 2026 deadline: Sep 30, 2026, 11:45pm PDT. Solo dev (sushidoescode), mode=sprint,
  posture=solo, trust level 0 (no `state/trust.json`).
- Merged through PR #10: CM-C1 Domain (8tps deterministic sim, replay hash, golden human-owned) ·
  CM-C2a importer (bytes→DTO→LevelGraph) · CM-C4 solver (`LevelSolver.Solve/EvaluateLog`) ·
  CM-C5 11-stage validator + `scripts/validate-content.sh` (+2 disclosed CM-C2a errata).
- Suite ~169 tests, `bash scripts/check.sh` + `bash scripts/test.sh` (5 wrappers) all green.
- Unity scaffold does NOT exist yet (human-only, Q-G). Pure-dotnet work only.

## Standing human authorizations & hard lines
- "Keep powering through" phases 6–10 without pausing between them (user instruction 2026-08-04).
- Merges: IF constitution Amendment 1 is on main (see
  `state/handoffs/amendment-1-agent-merge.md`), self-squash-merge PRs that meet its conditions.
  If it is not on main, hand every green PR to the human with the merge command.
- NEVER: `fastlane supply`/any Play upload · edits to immutable paths (`tests/contract/`,
  `docs/constitution.md`, `.claude/hooks/`, `scripts/git-hooks/`, `state/mode`, `evals/` except
  `evals/results/`) · new dependency without ADR · touching `**/billing|iap|ads/**` (production-
  mode tripwire) · `.github/**` (human PR, Q-V) · writes under `docs/plan/**`.
- `git add` by EXPLICIT PATH only, never `-A`: the user's `.claude/settings.json` is often dirty
  and `state/usage-ledger.jsonl` is untracked — neither ever enters a PR.
- Hybrid/local-model lane: NOT eligible for C6–C8 (`state/backlog.md:238`). Frontier in-session.
- Asset keys (Meshy/Tripo/Marble): parked until the asset phase; keys never in chat/repo.

## The build loop per contract (forge-build, sprint pricing)
1. Branch `task/<id>-<slug>` off fresh main. Write `state/handoffs/<ID>.md`: verbatim frozen
   contract copy (sed the backlog lines) + restatement + assumption freezes. Commit "milestone:
   anchor".
2. Tests FIRST against skeleton types (NotImplementedException bodies) → run → commit "milestone:
   red — N tests". 3. Implement to green → full suite → commit. 4. Gates: `check.sh`, `test.sh`.
5. PR with per-criterion evidence table. 6. RISKY diffs (these all are): ONE fresh-context review
   round — spawn `forge:code-reviewer` with: the frozen-contract pointer, "re-run everything
   yourself", "findings need concrete failure scenarios", the adversarial angles that matter.
   Apply ALL findings (or dispute with evidence), post a disposition table comment, re-verify,
   push. 7. Merge per the protocol above. 8. Update `state/PROJECT_STATE.md` (mind its 150-line
   cap — rotate to `state/archive/`). Never weaken a test to get green. 3-attempt retry cap per
   failure, then stop and report.

## The five phases
**P6 — CM-C6, daily-seed pre-validation** (`state/backlog.md:1145-1303`; depends C4+C5 ✓).
Seed = lower 32 bits of SHA-256("CM-DAILY-1|" + dateKey + "|" + k); fixed test vectors; the
constant is contract-tested verbatim. No clock anywhere — date keys are inputs. Horizon 90 in
`config/daily_pipeline.json` (corpus number, cite PRD:727/ADR-0009:35 on the row). Bounded salt
loop k→SALT_MAX_K over CM-C5 blocking stages. New host `dotnet/CatMetro.DailyTools/**` (Q-X, same
shape as Validator) + `scripts/validate-dailies.sh` + wrapper. NEW-Q8 conflict: implement the
ADOPTED liveops reading (local dateKey, "CM-DAILY-1|") — carry the conflict note, don't resolve.
Check.sh gets a Daily-root clock ban (DateTime/DateTimeOffset banned there — note CM-C5's
StalenessStage uses DateTimeOffset legally OUTSIDE the Daily root; scope the new block to
`unity/Assets/Scripts/Content/Daily/**` only). Daily boards that reference wildcard/second-source
mechanics will hit importer pins — the generator subset must stay inside shipped mechanics.

**P7 — L002–L005 authored + validated.** No frozen backlog contract exists: write a mini-contract
in the handoff note FIRST (acceptance per level: schema+static+solver+triviality+brittleness
blocking stages green via `bash scripts/validate-content.sh`; band table row honoured; mechanic
order honoured; solver-optimal seconds printed (NEW-Q1 stays pinned/non-blocking); zero-input must
NOT win; `validatedAt` ABSENT). Band law (product_spec §21): L002–L005 onboarding band,
difficultyTarget 0.05–0.16 ascending from L001's 0.08, minActionWindowTicks 12–16, mechanics:
switch only for L002–L003 (CM-R13 no-text tutorial trio), queue enters at L004 as `newMechanic`
(schema enum `queue`; queueCapacity nodes become meaningful). Design boards the SOLVER can prove:
1–2 switches (BFS-exact), decoy stations warn (fine), every misroute on multi-colour boards pins
(fine, non-blocking). Iterate: author JSON → run validator → fix → repeat. Taste/fun stays human
(stage 11 pending) — greybox-validated is the bar. Levels are `content/levels/L00X.json`
(campaign path ⇒ campaign assertions apply).

**P8 — CM-C7, save v1** (`state/backlog.md:1305-1509`; read the full contract before starting).
Header+payload, atomic write, v1→v2 migration, ledger dedupe, authors `config/runtime_bounds.json`
(Q-T) + the StreamingAssets byte-identity copy step if the contract names it. Serialisation MUST
reuse `ContentJson.Settings` (the single settings site — never construct JsonSerializerSettings
anywhere else; check.sh enforces).

**P9 — CM-C8, analytics offline queue** (`state/backlog.md:1511+`). Bounded, ordered,
lossy-but-visible, metrics-only. QUEUE_MAX_EVENTS/500 smoke-vs-criterion pattern per ADR-0006.

**P10 — re-decompose for CM-C2b/C3.** BLOCKED on the human Unity scaffold (Q-G). If the scaffold
exists at `unity/` (check for `unity/Packages/manifest.json` or asmdefs): run the forge-decompose
shape — refresh CM-C2b/C3 contracts against what actually landed, update
`state/backlog.md`-adjacent state (backlog itself is agent-writable), queue them. If NOT: write
the prep note (what the scaffold must contain, citing Q-G's pins: 6000.3.16f1, IL2CPP/ARM64/URP/
Input System, minSdk 25/targetSdk 36, `com.catmetro.game`, created IN PLACE without deleting
`unity/Assets/Scripts/**`) and STOP there — do not scaffold Unity yourself.

## Landmines this repo has already stepped on (don't repeat)
- check.sh scans COMMENTS too: never write the tokens "System.IO"/"UnityEngine" (Content roots),
  float/double/DateTime/Stopwatch/System.Random (Domain root), or "Unity " (validate-content.sh)
  even in prose. StringReader IS System.IO. Reword or scope new check blocks carefully.
- `ContentJson.LoadToken` returns the Settings-honouring token (dates stay strings) with
  `JToken.Parse` as the duplicate-key belt only — do not "simplify" it back (erratum E-C2a-2).
- `meta.newMechanic` is nullable (E-C2a-1). `meta.validatedAt` must be a string when present,
  never null (AMD-09; tooling deletes the key).
- Commands schedule: `entry.Tick == stepTick − 1`; the first step is uncommandable;
  `Due()` scans are order-independent. `CompletionTicks == RunToEnd().Tick − 1`.
- Verdict conventions: UNCONFIGURED(row) for absent Q-R rows (never invent a number — stop
  condition), PINNED(id) where an open pin blocks meaning, SKIPPED(reason), PARTIAL(Q-J) for H.
  Only blocking FAILs move exit codes.
- Envelope guards (`InvalidOperationException` from TrainsMax/QCapBound) and the NEW-Q4 pin
  (`NotSupportedException`) are DISTINCT: pins are counted, envelope prunes are not.
- PR flow: squash via `gh pr merge --squash --delete-branch` (web-flow signed, forge-policy ✓).
  Never delete a base branch a stacked PR points at. `git pull --ff-only` main after merges.
- Sandbox: run `dotnet`, `git push`, `gh`, and anything needing nuget/network unsandboxed;
  `$TMPDIR` for scratch. MSBuild named pipes break in-sandbox (SocketException 13).
- Stress boards L701/L702 SOLVE (146,942/16,839 expansions) — a budget regression that flips them
  to NotFound(Budget) will fail the positive stage-8 corpus test loudly; that's intended.
- Reviews: spawn fresh-context, demand re-runs + concrete failure scenarios. Expect real findings
  — the CM-C4 and CM-C5 rounds each caught genuine defects (escaping exception class; a dead
  acceptance rule). Budget one round; apply everything or dispute with evidence.

## Stop-and-queue (ask the human; never improvise)
NEW-Q1 (45–90s invariant vs anchors) · NEW-Q4 (rejection) · NEW-Q5 (scoring) · NEW-Q8 (UTC vs
local — adopted liveops, flag if it bites) · NEW-Q35 (wild) · Q-R threshold numbers · Q-O stamping
policy changes · D-6 tester roster · anything needing a Domain edit (golden-invalidating) · any
schema change (frozen for the window) · Unity scaffold decisions (Q-G).

## Quick verification of a healthy start
`git checkout main && git pull --ff-only && bash scripts/check.sh && bash scripts/test.sh`
→ expect `check: OK`, `test: 5/5 passed`. Then start P6.
