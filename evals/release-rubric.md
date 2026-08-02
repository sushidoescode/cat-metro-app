# Release-readiness rubric
<!-- Applied by release-manager (with evidence links) and spot-checked by evaluator. Read-only to agents; changes are human PRs. -->

Every item: **pass / fail / waived-by-<human> (<date>, reason)**. An item without an evidence link is a fail.

1. CI green on the release candidate (link)
2. Every shipped contract's acceptance criteria have passing tests (evidence table links)
3. No open high-severity findings (code-reviewer, security-reviewer, evaluator)
4. Test/coverage delta vs last release is flat-or-better, or the drop is explained
5. Migration dry-run clean AND rollback is one step (expand/contract discipline)
6. Rollback path tested this cycle (not just documented)
7. Monitoring + alerts cover every new surface; baseline snapshot captured
8. Docs/changelog current; doc-truthfulness spot-check (5 claims) passed
9. Feature flags in intended states, listed
10. No secrets in diff history since last release (scanner link)
11. `state/PROJECT_STATE.md` shows no in-flight P0
12. Residual-risk statement written for the human (one screen max)
