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

## Blocked / waiting-on-human
- Server-side rulesets blocked by GitHub plan tier: repo is private on Free (rulesets → HTTP 403 "Upgrade to GitHub Pro"). Plan of record requires private (EXECUTION_PLAN.md D1 §4), so the options are: upgrade to GitHub Pro (~$4/mo) or defer rulesets — deferral is acceptable in sprint but rulesets are one of the five production-graduation gates (docs/security/checklist-runs/2026-08-02-init.md).
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
