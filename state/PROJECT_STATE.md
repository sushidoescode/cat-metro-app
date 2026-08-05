# Project state — Cat Metro
<!-- Agents append; humans prune (weekly / at /forge-retro). Absolute dates only. -->
<!-- HARD CAP: keep this file under ~150 lines. It is mandatory session-start reading — every line
     is a context tax on every session. Rotate anything older than 2 weeks to state/archive/
     (agents propose the rotation; humans prune). A 2,000-line state file is a measured failure mode. -->

## Now
2026-08-04 — phase: build, Sprint 1. ALL pure-dotnet contracts merged (CM-C1..C8, L001-L005; suite 324 tests (both hosts), test.sh 8/8). Unity scaffold (Q-G) IN PROGRESS: human created the pinned 6000.3.16f1 shell + merged #14-#18; agent executing the mechanical scaffold on chore/qg-unity-scaffold. CM-C2b (#21) + CM-C3 (#22) MERGED: the game is PLAYABLE end-to-end in-engine (suite 334 EditMode + 20 PlayMode + 331 dotnet, replay hash identical across hosts). Next: HUMAN device session (C2b crit 8 + C3 crit 2/4/7 tables) → tranche-3 decompose (taxonomy, L006-L010, dead-mechanic gate, content shipping) → art/taste pass (TG gates) (taxonomy CM-R43.1-.3, L006-L010, dead-newMechanic gate, content shipping pipeline). Mode: see `state/mode` (sprint). Monthly agent budget: $0 API — subscription capacity only (Claude Max + Codex Pro/Max + local model qwen3.6:35b-a3b-coding-nvfp4). Stop-and-rethink trigger: >40% of budget in any week.

**Graduation criterion (human decision, 2026-08-02):** flip `state/mode` to production via human-authored commit BEFORE any monetization code (billing/IAP/ads/payments) merges. Those path globs are already risky-path tripwires in AGENTS.md. At graduation: `git mv .github/workflows/claude-review.yml.disabled .github/workflows/claude-review.yml` and resolve TODO(review-auth) below.

## Active tasks
| id | title | owner (human/session) | status | branch |
|---|---|---|---|---|
| CM-C1 | Domain skeleton + replay-hash test | session 2026-08-02/03 | DONE — golden human-committed, merged (#2/#3) | (merged) |
| CM-C2a | Content importer (bytes → LevelGraph) | session 2026-08-03 | DONE — merged (#8) after 9-finding review | (merged) |
| CM-C4 | Solver (BFS ≤2-switch, beam beyond) | session 2026-08-03/04 | DONE — merged (#9) after review round | (merged) |
| CM-C5 | 11-stage validator + validate-content leg | session 2026-08-04 | DONE — merged (#10) after 14-finding review round | (merged) |
| CM-C6 | Daily-seed pre-validation pipeline | session 2026-08-04 (Fable 5) | DONE — merged (#14) after 11-finding review round | (merged) |
| L002-L005 | Onboarding band authored+validated | session 2026-08-04 (Fable 5) | DONE — merged (#15) after 9-finding round (L005 dead-queue caught+fixed) | (merged) |
| CM-C7 | Save v1 (header/atomic/SI/migration/ledger/bounds) | session 2026-08-04 (Fable 5) | DONE — merged (#16) after 12-finding round (read-only grant hole caught+fixed); human acked the 6 gates | (merged) |
| CM-C8 | Analytics offline queue | session 2026-08-04 (Fable 5) | DONE — merged (#18, was stacked on #16) after 13-finding round | (merged) |
| Q-G scaffold | Unity 6000.3.16f1 project shell in place | human shell + agent mechanics 2026-08-04 | IN PROGRESS | chore/qg-unity-scaffold |
| CM-C2b | Greybox playable + Bootstrap seams + StreamingAssets | session 2026-08-04/05 (Fable 5) | DONE — merged (#21) after 13-finding round; criterion 8 device artifact OPEN (human) | (merged) |
| CM-C3 | Fail/retry loop: cause camera, tick-0 retry, wave preview | session 2026-08-05 (Fable 5) | DONE — merged (#22) after 19-finding round (5 blockers incl. camera-parented ring, tautological hash law) | (merged) |
| CM-UX-00 | UX tranche-1 decompose: first-run chrome CM-UX-01..07 ranked + CM-UX-01 frozen contract (`docs/ux/ux-layer-decompose.md`) | session 2026-08-05 (UX lane, Fable 5) | DONE — merged (#27) after 2-round review (15+4 findings, 5 blocking fixed; TMP/ADR-0007 call routed to human as Q-6; 6 human Qs filed §6, none block slice 1) | (merged) |

## Recently done (last 7 days — one line each, PR links)
- 2026-08-04 — phases 6-10 session (Fable 5): CM-C6 #14 · L002-L005 #15 · CM-C7 #16 · CM-C8 #18 · prep note #17 all merged same-day. 4 fresh-context review rounds, 45 findings, 4 real blockers caught pre-merge (dead L005 queue; read-only double-grant; pause-path IOException; Guid-survivable id tests). Cross-tool pinned vectors (python↔C#) for seeds/ledger keys/queue ids. Suite 169→257 tests, wrappers 5→8.
- 2026-08-02 — forge-init: substrate installed (kit 3.4.1), mode=sprint, stack deferred pending specs.
- 2026-08-02 — docs/plan/ Shipaton 2026 drop committed (secret-scanned clean); private repo github.com/sushidoescode/cat-metro-app created; first CI run green on push.
- 2026-08-02 — server-side wall LIVE (kit 3.5.0 solo posture, ADR-0001): forge-main-solo + forge-tags + forge-tag-creators applied, `--check` = 3 (match, declared residual); owner is the named tag-creation bypass actor; direct-push probe bounced (GH013). ALL main-bound work now goes branch → PR → green CI → squash self-merge (human).
- 2026-08-02 — forge-specify complete (branch forge/specify-prd, 11-agent workflow): docs/prd/ PRD (58 reqs, 23 pinned branches §4.1) + risks (39 rows) + venture-critique (V-1: tester-clock evidence request; V-2: D7 gate power at n=12) + ux-flows (12 stories, TG-1..8 taste gates) + hypothesis. PENDING human: PRD sign-off, D-1..D-9, NEW-Q1..48, TG-1..8. Asset-gen resources recorded (Meshy key via Unity-MCP owner-only store at asset phase; Tripo pro+CLI; Marble parked).
- 2026-08-02 — kit 3.6.0 upgrade (branch chore/forge-upgrade-3.6.0): stamp provenance repointed to released v3.4.1 (pre-release-stamp trust dead-end in forge-upgrade — upstream issue drafted), provenance-only apply clean, doctor 19 ok/0 fail; local setup-rulesets show_error fix kept (bug still ships in 3.6.0). Manual ports pending → see debt.
- 2026-08-03 — merged: kit upgrade #4, PRD+ADRs+CM-C1 #2/#3, Phase-0 plan amendments #5, tranche-2 backlog #6, CM-C2a importer #8 (all human squash-merges; forge-policy green).
- 2026-08-03 — hybrid lane validated end-to-end on CM-C4 cut g2: qwen draft failed on RunToEnd-vs-layer semantics + anchor drift → two-strike escalation fired correctly → frontier implemented; 4 forge-hybrid dogfood findings banked for upstream (exit-0 on unreachable Ollama; no cold-load retry; no write_file fallback on anchor drift; ask-rules default vs solo posture). Lane retries later on a write_file-shaped contract.

## Blocked / waiting-on-human
- Repo-name drift: plan of record says `cat-metro`; actual repo/dir is `cat-metro-app`. Human call: rename or amend the plan line.

## Decisions this week (promote to ADR if architectural)
- 2026-08-05 — **Ratification batch (human, in-session):** RESTORE URP (tree ships no pipeline asset — ADR-0004 conformance fix via CM-C2b-DEVFIX; all device numbers re-measured on the URP build) · 1%-low pinned = mean-of-worst-1% · CM-C3 device legs use a dev-only failable-level override + Pixel-9-Pro tier deviation recorded · tranche-3: stager (CM-C10 script) is THE single StreamingAssets author with derived guids, ContentSync re-cut later assert-only · taxonomy declares all 45 events dark (no tripwire) · backlog ownership batch DELEGATED and applied this commit (5 amendments, annotated in place) · sequencing: DEVFIX's 7 Presentation lines precede UX-lane code; Wave 1 = CM-C9/CM-C5.1/CM-C10 parallel, CM-C11 after stager. OPEN (human): N1 StreamingAssets writer-grant annotation + CM-C10 row (CM-C11 stops without it) · CM-C5.1 blocking-vs-Warn posture.
- 2026-08-05 — **Human approved the UX-layer build-out from the PRD** (ux-flows 12 stories; TG-1..8 stay in-build human gates; monetization surfaces stay out). Runs as a PARALLEL session per `state/handoffs/SESSION-HANDOFF-ux.md` (ownership boundaries + the one-input-surface gate collision are in that file). Device session same day: criterion-8 artifact captured — frame leg FAILED honestly (30 fps default cap) + shader-stripping magenta; fix contract next; evidence in `evals/results/device/c2b-crit8/`.
- 2026-08-02 — mode=sprint with a hard graduation criterion (see Now). Rationale: hackathon clock now, revenue later; monetization globs tripwire the switch.
- 2026-08-04 — **Constitution Amendment 1 (human-authored, e9e9675 via #12/#13):** agent squash-merges to main delegated under conditions (green checks; sprint review pricing honoured; no immutable/risky/docs-plan paths; tags/releases/spend/state-mode/ADRs stay human). Bootstrap route: unsigned human commit → staging squash (web-flow signed) → main; SSH commit signing still on the debt list.
- 2026-08-04 — phases 6–10 hand to a fresh session (new Max account, Fable 5) per state/handoffs/SESSION-HANDOFF-phase6-10.md; this session ends after the first delegated merge (#11).

## Metrics tags (append per completed task: intervention? rework? escape? cost-$)

## Known debt / follow-ups
- TODO(stack): engine undecided (Android/Google Play target). When specs land: real check/test/build in scripts/*.sh · registry domains in .claude/settings.json sandbox allowlist · pkg-install ask pattern · engine .gitignore entries · CI toolchain/install/audit steps.
- TODO(deploy): deploy.yml steps empty — no production yet. Fill Google Play rollout + add reviewers to the GitHub `production` environment when the first release exists.
- TODO(secret-scan): wire gitleaks (or equivalent) into CI — required before graduation to production.
- TODO(review-auth): per-push claude-review is disabled (sprint) and the human runs subscriptions, not API keys — at graduation wire claude-code-action OAuth (Claude Max) or keep the review leg human+local.
- Perf budgets: docs/perf/budgets.md rows are TBD (human) — required before /forge-release.
- Kit 3.6.0 manual ports (human-authored commits; validated patches + upstream issue drafts in state/handoffs/kit-3.6.0-manual/): .github/workflows/forge-policy.yml content-based state-mode downgrade refusal; posture comment block for the mode file (values stay sprint/solo).
- Upstream (forge-kit): show_error trailing-blank-line bug — a gh startup failure dies mid-diagnosis with exit 1 (reads as DRIFT) instead of UNVERIFIED 2; fixed locally in scripts/setup-rulesets.sh, issue filed.
- OPEN DEVICE SESSION (human, one sitting): CM-C2b criterion 8 (Pixel-6a-class, 60 s frametimes + SEAM_LOADED line + merged-manifest paste) + CM-C3 criteria 2/4/7 low+mid-tier tables. The scene/boot/manifest wiring is DONE — build and run.
- Retry affordance debt (CM-C3 review N2): the bottom-band retry region has no rendered CTA and no "Try again" copy row yet — lands with the S-03 chrome; the full-band grab must shrink when Rewind/Back join the band.
- Follow-up (from PR #15 review F5): no gate detects a declared-but-dead `newMechanic` — every blocking stage stays green whether L004's queue is alive or dead. Candidate: a corpus assertion that the declared mechanic is exercised in the solver-optimal trace. Needs a contract.
- From CM-C8 review (human calls, recorded in state/handoffs/CM-C8.md A-C8-10/13): (a) ratify or amend the per-enqueue fsync cadence (~139 MB per offline fill to cap); (b) ADR-0006:238 errata — the byte limb is unreachable at shipped bounds (contradicts :229-231 + CM-C7 drift-(d)).
- Risk trigger (PR #15 review F4): brittleness retention is measured over UNPINNED jitter samples; if Q-B/NEW-Q4 resolves misroute-at-station as a LOSS, L002/L003/L005 must re-run brittleness (would read 65%/75%/60% under that rule) and may need redesign. Re-check when Q-B lands.
