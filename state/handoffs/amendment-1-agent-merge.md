# DRAFT — Constitution Amendment 1: delegated merges (requires YOUR human-authored commit)

Drafted 2026-08-04 at the human's request ("figure out how to do the merges yourself"). Agents
cannot apply this — `docs/constitution.md` is hook-enforced human-only, which is exactly why this
file is a draft next to it, not an edit to it.

## How to apply (you, not an agent)

```bash
git checkout main && git pull
git checkout -b chore/amendment-1-agent-merge
# 1) Edit docs/constitution.md per the three hunks below
# 2) Edit AGENTS.md hard rule 7 per the fourth hunk
git add docs/constitution.md AGENTS.md
FORGE_HUMAN_OVERRIDE=1 git commit -m "constitution: Amendment 1 — delegated merges under conditions (human-authored)"
git push -u origin chore/amendment-1-agent-merge
gh pr create --base main --title "constitution: Amendment 1 — delegated merges" --body "Human-authored per docs/constitution.md line 3."
gh pr merge --squash --delete-branch
```
(If forge-policy objects to the branch commit's signature, this is the same shape as the CM-C1
golden: the squash into main via gh is web-flow signed and passes.)

## Hunk 1 — Principles, item 6

Replace:
> 6. **Irreversible actions get human gates**: merge, deploy, spend, data migration, disclosure. No exceptions for being confident.

with:
> 6. **Irreversible actions get human gates**: deploy, spend, data migration, disclosure, tag/release. No exceptions for being confident. Merge to main is delegated ONLY under Amendment 1's conditions; otherwise it stays human.

## Hunk 2 — Review chain, final leg

Replace:
> → CI gates → **human merge**.

with:
> → CI gates → **merge per Amendment 1** (agent squash-merge only when every Amendment-1 condition holds; human merge otherwise).

## Hunk 3 — Never delegated list

Replace:
> merge to main

with:
> merge to main outside Amendment 1's conditions

…and append this section to the end of the file:

> ## Amendment 1 — delegated merges (2026-08-04, human-authored)
> An agent may squash-merge its own PR to main via `gh` when ALL of the following hold:
> 1. The PR is a task-contract or docs/state branch targeting main; all required checks are green.
> 2. The sprint review pricing was honoured: RISKY diffs (per `scripts/forge-risk.sh`) carry a
>    completed fresh-context review round with every finding dispositioned on the PR; LOW-RISK
>    diffs need green CI. A standing REQUEST CHANGES with unapplied findings blocks delegation.
> 3. The diff touches none of: the immutable paths (constitution list above), `.github/**`,
>    `infra/**`, `**/billing/**`, `**/iap/**`, `**/ads/**`, `docs/plan/**`, `tests/contract/**`,
>    and adds no dependency lacking an ADR.
> 4. Still human-only regardless: tag pushes, releases, deploys, spend, `state/mode`, ADR
>    approval, and anything a review explicitly flags for human judgment.
> 5. This amendment is revocable by deleting it (human commit); deletion restores the
>    unconditional human-merge floor.

## Hunk 4 — AGENTS.md, hard rule 7

Replace:
> the human-merge floor never moves

with:
> the merge floor is constitution Amendment 1: agent squash-merges only under its conditions, human otherwise

That is the whole change. Until this is committed on main, every merge stays yours.
