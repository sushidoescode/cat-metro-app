# Engineering Constitution

Changes to this file: human PR + review window. Agents may cite it, never edit it (hook-enforced).

## Principles
1. **Working software over impressive diffs.** Every merge leaves main deployable.
2. **Tests define done.** Untested behavior is undefined behavior. The definition of "passing" is never owned by the party that must pass it.
3. **Smallest change that ships the requirement.** No speculative abstraction; complexity buys its way in with a named requirement.
4. **Security is a requirement, not a review finding.** AuthN/Z at every boundary; external input is hostile until validated.
5. **Architecture changes require an ADR before implementation** — including any new dependency, schema shape, or external contract.
6. **Irreversible actions get human gates**: merge, deploy, spend, data migration, disclosure. No exceptions for being confident.
7. **State lives in the repo.** Decisions in ADRs, progress in state files, procedures in skills — not in anyone's chat history.

## Definition of Done (any task)
- [ ] Every acceptance criterion in the task contract maps to a passing test
- [ ] `bash scripts/check.sh` and the full test suite pass locally and in CI
- [ ] No out-of-scope changes; noticed-but-not-done items reported
- [ ] No new dependencies without ADR · no secrets/keys anywhere in the diff
- [ ] Docs updated where behavior changed (or explicitly handed to docs-writer)
- [ ] `state/PROJECT_STATE.md` updated; handoff note written if work continues elsewhere

## Review chain (constitutionally fixed)
implementing session → fresh-context code-reviewer (read-only; findings or verification evidence, never bare approval) → [security-reviewer on risky paths] → CI gates → **human merge**. Maximum two agent review rounds, then human tiebreak. Stakes modes (`state/mode`, human-set): in **sprint** mode the agent-review leg is priced by `evals/mode-policy.json` + `scripts/forge-risk.sh` (LOW-RISK diffs may skip it; RISKY diffs get one round) — CI gates and the human-merge floor are unconditional in every mode.

## Never delegated (regardless of model capability)
Problem choice and scope · real-user contact · ADR approval · merge to main · production go/no-go and spend · security-severity acceptance and disclosure · incident command · stakes-mode selection (`state/mode`) · edits to this file, `tests/contract/`, `evals/` definitions and rubrics (`evals/results/` excepted — but `evals/results/attested/` stays human/CI-only, the only evidence that can raise the autonomy dial), `.claude/hooks/`, `scripts/git-hooks/`, or the computed dial `state/trust.json`
