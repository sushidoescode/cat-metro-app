# Project state — Cat Metro
<!-- Agents append; humans prune (weekly / at /forge-retro). Absolute dates only. -->
<!-- HARD CAP: keep this file under ~150 lines. It is mandatory session-start reading — every line
     is a context tax on every session. Rotate anything older than 2 weeks to state/archive/
     (agents propose the rotation; humans prune). A 2,000-line state file is a measured failure mode. -->

## Now
2026-08-04 — phase: build, Sprint 1 (PRD/ADR-0002..0009 ratified + merged; Domain CM-C1 + Content CM-C2a on main; CM-C4 solver in review; next: CM-C5 validator → CM-C6 daily seed → L002-L005 → CM-C7 save → CM-C8 analytics; CM-C2b/C3 re-decompose when the human Unity scaffold lands). Mode: see `state/mode` (sprint). Monthly agent budget: $0 API — subscription capacity only (Claude Max + Codex Pro/Max + local model qwen3.6:35b-a3b-coding-nvfp4). Stop-and-rethink trigger: >40% of budget in any week.

**Graduation criterion (human decision, 2026-08-02):** flip `state/mode` to production via human-authored commit BEFORE any monetization code (billing/IAP/ads/payments) merges. Those path globs are already risky-path tripwires in AGENTS.md. At graduation: `git mv .github/workflows/claude-review.yml.disabled .github/workflows/claude-review.yml` and resolve TODO(review-auth) below.

## Active tasks
| id | title | owner (human/session) | status | branch |
|---|---|---|---|---|
| CM-C1 | Domain skeleton + replay-hash test | session 2026-08-02/03 | DONE — golden human-committed, merged (#2/#3) | (merged) |
| CM-C2a | Content importer (bytes → LevelGraph) | session 2026-08-03 | DONE — merged (#8) after 9-finding review | (merged) |
| CM-C4 | Solver (BFS ≤2-switch, beam beyond) | session 2026-08-03/04 | DONE — merged (#9) after review round | (merged) |
| CM-C5 | 11-stage validator + validate-content leg | session 2026-08-04 | DONE — merged (#10) after 14-finding review round | (merged) |
| CM-C6 | Daily-seed pre-validation pipeline | session 2026-08-04 (Fable 5) | PR #14 GREEN, review round done (11 findings, 5 applied) — **WAITING-ON-HUMAN: ratify A-C6-9 (host stub vs criterion-8 prose, reviewer-flagged) then `gh pr merge 14 --squash --delete-branch`** | task/CM-C6-daily-seed-pipeline |
| CM-C7/C8, L002-L005 | phases 7–9 | session 2026-08-04 (Fable 5) | IN PROGRESS — see state/handoffs/SESSION-HANDOFF-phase6-10.md | — |
| CM-C2b/C3 | Greybox board + L001 in-engine | — | BLOCKED-ON human Unity scaffold (Q-G) | — |

## Recently done (last 7 days — one line each, PR links)
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
- Follow-up (from PR #15 review F5): no gate detects a declared-but-dead `newMechanic` — every blocking stage stays green whether L004's queue is alive or dead. Candidate: a corpus assertion that the declared mechanic is exercised in the solver-optimal trace. Needs a contract.
- Risk trigger (PR #15 review F4): brittleness retention is measured over UNPINNED jitter samples; if Q-B/NEW-Q4 resolves misroute-at-station as a LOSS, L002/L003/L005 must re-run brittleness (would read 65%/75%/60% under that rule) and may need redesign. Re-check when Q-B lands.
