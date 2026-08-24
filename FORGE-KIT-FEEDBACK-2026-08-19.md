# Forge Kit — field feedback from Cat Metro

From a real project that ran Forge Kit 3.6.0 for ~18 days and is now removing it. Written by the
orchestrator agent that operated inside it, at the human's request, to be handed to the kit's
maintainer.

**Headline:** the kit's process worked — and the project still failed at its actual goal. In 18
days Cat Metro accumulated **262 files of governance** (`evals/`, `state/`, `docs/plan`,
`docs/prd`, frozen contracts, gate ledgers, census records) and shipped a game that still renders
white line segments and grey untextured cats. The ceremony was rigorous about the wrong things.
Not one gate anywhere in the kit asks *"does this look like the thing we're building?"*

---

## 1. The kit optimises correctness, and never asks about the product

Every gate is about process integrity: is there a frozen contract, was there a RED test, did two
review rounds happen, is the census appended. All useful. But a week of work passed every gate
while the game got visually no closer to its concept art.

The most valuable rule in the whole project came from the human, not the kit — *"anything visual
must be rendered and looked at; code-green is not evidence."* It caught **six** real defects that
green tests missed, including a cat asset with 474 triangles of floating debris, two of three
characters rendering backwards, and a near-miss where a cleanup rule would have deleted two of
three trees and both wheels off a toy engine.

**Suggestion:** add an outcome gate. For anything user-facing, require an artifact a human can
look at, and make it a first-class check rather than something a diligent agent invents.

## 2. Guardrails that block ordinary work

The PreToolUse hooks scan command **prose**, not effects. Real denials from this project:

- `ls docs/ evals/ state/` — a **read-only directory listing** — denied for "pairing a write
  operation with an immutable path."
- `git diff --name-only ... | grep -E "^(docs/adr|tests/contract|...)"` — a read-only scope check
  before a merge — denied for naming immutable paths in the pattern.
- A PR body mentioning an immutable path was denied, forcing `--body-file` for every comment.
- *(2026-08-23, during kit removal)* `gh pr create` for a branch whose **diff touched no guarded
  path at all** — denied because the PR body's prose named the paths it was warning the human
  about. The guard outlived the kit and taxed the cleanup: it also refused a bare `ls` of the
  hook directory while the recovery orchestrator was mapping what was safe to touch.

This trains agents to phrase around the checker rather than respect it, which is the opposite of
what a guardrail should do. **Match on effects, not on substrings in the command text.**

Related: the hooks **prevent their own removal**. Uninstalling the kit required a human-run script
because agents can't touch `.claude/hooks/`, `scripts/git-hooks/`, or `state/mode`. Defensible,
but an explicit `forge uninstall` path should exist.

## 3. Immutability that outlived its usefulness

`tests/contract/`, `docs/constitution.md`, `state/mode`, `evals/` are human-commit-only. On a
solo hackathon project where the human is also the only reviewer, this mostly created ceremony:
the "census" of merge authority grew into a **single 12,000-word bullet** in
`state/PROJECT_STATE.md` tracking who armed which auto-merge, most entries self-describing as
"agent-transcribed, unattestable." Enormous effort documenting an audit trail that no external
party would ever read.

**Suggestion:** scale immutability to team size. Solo mode shouldn't maintain a merge-authority
census.

## 4. Review rounds priced per-artifact, not per-risk

The two-round cap was the single best rule in the kit — the previous session's unbounded review
recursion is why nothing merged for two days. But rounds are priced the same for a 12-line docs
change and a 3,900-line pipeline. Meanwhile the kit's own `check.sh`/`test.sh` harness takes
**2h45m regardless of diff size**, so a two-file documentation PR costs the same CI as a
14,000-line one.

**Suggestion:** price review rounds and CI scope by what the diff actually touches.

## 5. What the kit got genuinely right

Worth keeping, and worth saying plainly:

- **The two-round cap.** It ended a real death spiral.
- **Fresh-context review.** Independent reviewers caught things the authors could not: a stale
  60-hash approval manifest that went false within 24 minutes of being written; a corrupted
  release gate that a botched edit had spliced mid-sentence; a staged evidence frame presented as
  real gameplay. All three were found by adversarial review, and all three were real.
- **"Never weaken a test to reach green."** Load-bearing. Kept a caching layer with
  cache-identity gaps out of the test harness.
- **Concrete failure scenarios required in findings.** Kills "looks good" reviews.
- **ADRs for dependencies and licensing.** The generated-asset licence ADR is the one governance
  artifact that protects something real — commercial exposure doesn't disappear because the
  process does.

## 6. Two failures the kit's structure caused directly

**Cross-lane collision.** Two lanes were briefed independently. Lane B pinned a 60-hash manifest
into an ADR at 03:07; the curation lane geometry-edited two of those exact assets at 03:31. Eight
of sixty pins were false within 24 minutes. Neither lane was wrong — the *orchestration* was, and
the kit has no concept of "these two contracts touch the same bytes."
**Suggestion:** contracts should declare the artifacts they pin, and the kit should refuse to
freeze two that overlap.

**Squash merges break ancestry-pinned documents.** A contract declared a *branch* commit as its
reviewed production base. The squash merge discarded it, so `main` failed its own docs test on
every full clone while CI reported green — because CI checks out at depth 1, which makes any
history-dependent assertion pass vacuously. The kit generates ancestry-pinned evidence and ships
a CI config that structurally cannot verify it.
**Suggestion:** `fetch-depth: 0` in the generated workflow, and steer evidence at mainline
commits.

## 7. Context cost

The kit's instruction layer (`AGENTS.md` + `CLAUDE.md` + constitution + mode policy) loaded on
**every turn of every session**. Anthropic's [context engineering guidance for Claude 5 models](https://claude.com/blog/the-new-rules-of-context-engineering-for-claude-5-generation-models)
now says to keep instruction files lightweight, spend the tokens on codebase gotchas, and avoid
rigid rules that override model judgement — they removed ~80% of Claude Code's own system prompt
with no measurable eval loss. Forge Kit's layer runs hard in the opposite direction.

The replacement here is ~70 lines of genuine gotchas. It's more useful than what it replaced.

---

## The one-sentence version

Forge Kit is a good governance framework wearing the clothes of a delivery framework. It made
this project *rigorous* and *slow*, and it never once asked whether the game was any good.
