# Project state — Cat Metro
<!-- Agents append; humans prune (weekly / at /forge-retro). Absolute dates only. -->
<!-- HARD CAP: keep this file under ~150 lines. It is mandatory session-start reading — every line
     is a context tax on every session. Rotate anything older than 2 weeks to state/archive/
     (agents propose the rotation; humans prune). A 2,000-line state file is a measured failure mode. -->

## Now
2026-08-02 — phase: discovery → sprint prep (Shipaton 2026 plan landed in docs/plan/ — engine pinned to Unity 6000.3.16f1; no app code scaffolded yet; next: /forge-sprint on EXECUTION_PLAN.md). Mode: see `state/mode` (sprint). Monthly agent budget: $0 API — subscription capacity only (Claude Max + Codex Pro/Max + local model qwen3.6:35b-a3b-coding-nvfp4). Stop-and-rethink trigger: >40% of budget in any week.

**Graduation criterion (human decision, 2026-08-02):** flip `state/mode` to production via human-authored commit BEFORE any monetization code (billing/IAP/ads/payments) merges. Those path globs are already risky-path tripwires in AGENTS.md. At graduation: `git mv .github/workflows/claude-review.yml.disabled .github/workflows/claude-review.yml` and resolve TODO(review-auth) below.

## Active tasks
| id | title | owner (human/session) | status | branch |
|---|---|---|---|---|

## Recently done (last 7 days — one line each, PR links)
- 2026-08-02 — forge-init: substrate installed (kit 3.4.1), mode=sprint, stack deferred pending specs.
- 2026-08-02 — docs/plan/ Shipaton 2026 drop committed (secret-scanned clean); private repo github.com/sushidoescode/cat-metro-app created; first CI run green on push.
- 2026-08-02 — server-side wall LIVE (kit 3.5.0 solo posture, ADR-0001): forge-main-solo + forge-tags + forge-tag-creators applied, `--check` = 3 (match, declared residual); owner is the named tag-creation bypass actor; direct-push probe bounced (GH013). ALL main-bound work now goes branch → PR → green CI → squash self-merge (human).
- 2026-08-02 — forge-specify complete (branch forge/specify-prd, 11-agent workflow): docs/prd/ PRD (58 reqs, 23 pinned branches §4.1) + risks (39 rows) + venture-critique (V-1: tester-clock evidence request; V-2: D7 gate power at n=12) + ux-flows (12 stories, TG-1..8 taste gates) + hypothesis. PENDING human: PRD sign-off, D-1..D-9, NEW-Q1..48, TG-1..8. Asset-gen resources recorded (Meshy key via Unity-MCP owner-only store at asset phase; Tripo pro+CLI; Marble parked).

## Blocked / waiting-on-human
- Repo-name drift: plan of record says `cat-metro`; actual repo/dir is `cat-metro-app`. Human call: rename or amend the plan line.

## Decisions this week (promote to ADR if architectural)
- 2026-08-02 — mode=sprint with a hard graduation criterion (see Now). Rationale: hackathon clock now, revenue later; monetization globs tripwire the switch.

## Metrics tags (append per completed task: intervention? rework? escape? cost-$)

## Known debt / follow-ups
- TODO(stack): engine undecided (Android/Google Play target). When specs land: real check/test/build in scripts/*.sh · registry domains in .claude/settings.json sandbox allowlist · pkg-install ask pattern · engine .gitignore entries · CI toolchain/install/audit steps.
- TODO(deploy): deploy.yml steps empty — no production yet. Fill Google Play rollout + add reviewers to the GitHub `production` environment when the first release exists.
- TODO(secret-scan): wire gitleaks (or equivalent) into CI — required before graduation to production.
- TODO(review-auth): per-push claude-review is disabled (sprint) and the human runs subscriptions, not API keys — at graduation wire claude-code-action OAuth (Claude Max) or keep the review leg human+local.
- Perf budgets: docs/perf/budgets.md rows are TBD (human) — required before /forge-release.
- Update the forge plugin in Claude Code 3.4.1 → 3.5.0 (kit released; skills text drifted slightly).
- Upstream (forge-kit): show_error trailing-blank-line bug — a gh startup failure dies mid-diagnosis with exit 1 (reads as DRIFT) instead of UNVERIFIED 2; fixed locally in scripts/setup-rulesets.sh, issue filed.
