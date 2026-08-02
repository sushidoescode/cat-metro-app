# Agentic Development Security Checklist
<!-- Re-run quarterly and at /forge-retro after incidents. Derived from the research report Ch. 15 (see forge-kit README for the link). -->

## Identity & access
- [ ] Agents run with dedicated credentials, least-privilege (no prod, no billing, no user PII)
- [ ] Cloud creds: read/plan-only locally; applies happen in CI from protected refs
- [ ] MCP servers pinned & vetted; per-tool allowlists (`mcp__server__tool`); no unvetted community servers
- [ ] GitHub Action token minimal; agent workflows never trigger on untrusted-fork PRs

## Containment
- [ ] Sandbox enabled; credential files deny/masked; network allowlist reviewed this quarter
- [ ] `bypassPermissions` only inside disposable containers, if ever (documented exceptions: ____)
- [ ] Permission rules: destructive-command denies, ask-gates (push/merge/dep-install), path-scoped writes
- [ ] Secrets: manager-only; deny-read rules on env/key paths; secret scanner in CI; none in CLAUDE.md/state files

## Integrity
- [ ] Branch protection + required CI + human review on main
- [ ] Immutable-paths enforcement active at ALL belts (permissions, Claude hooks, git pre-commit): tests/contract/, evals/ (results/ excepted), docs/constitution.md, .claude/hooks/, scripts/git-hooks/ — prove with scripts/forge-doctor.sh
- [ ] Test-diff review rule live: weakened/deleted assertions are findings by default
- [ ] Dependency gate: audit + license check in CI; new deps require ADR + human approval
- [ ] Instruction files (AGENTS.md, CLAUDE.md, .claude/**) reviewed like code

## Injection hygiene
- [ ] External text (issues, tickets, user content, web) → read-only agents; outputs quarantined for review
- [ ] No agent both reads untrusted input and holds write/deploy tools in one session

## Accountability
- [ ] Session transcripts retained per policy; spend caps set (workspace + per-run `--max-budget-usd`)
- [ ] Iteration caps on every headless run (`--max-turns`)
- [ ] Backups exist, restore tested, and stored where NO agent credential reaches (drill date: ____)

## Governance
- [ ] Never-delegate list posted in constitution; gate owners named
- [ ] Incident runbook covers agent-caused incidents: who stops sessions, how keys get revoked
- [ ] Quarterly: seeded-defect reviewer audit · permission-rule review · this checklist re-run (last: ____)
