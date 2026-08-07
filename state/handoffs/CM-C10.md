# CM-C10 — status log (content stager)

Session 2026-08-06 (Fable 5). Contract: `state/handoffs/CM-C10-frozen-contract.md` (9 criteria).
Branch `task/CM-C10-content-stager`, frozen at anchor db1f6ff, rebased onto main @ ca13801
(picks up the N1 writer-grant rows in `state/backlog.md` — the staged-derived-tree exception
class this contract's `--apply` writes ride).

## C6-rule capture (recorded BEFORE the wrapper was written)

- Rebase baseline (`git merge-base HEAD main`): **ca13801**.
- Wrapper count at baseline: `find tests -name '*.test.sh' | wc -l` → **N = 12**
  (analytics, content, daily, domain, save, smoke, solver, unity×4, validation).
- This contract adds exactly one wrapper (`tests/staging/stage-content.test.sh`) → target **N+1 = 13**.

## H6 sequencing (criterion 8)

Order **(i)**: this contract lands first. The UX lane's gate-evolution contract has NOT landed —
`tests/unity/editmode.test.sh` is byte-identical to merge-base ca13801 (wrapper asserts it at run
time, never a pinned commit). The UX contract must be told the criterion-10 block stays author-free.

## Implementer decisions inside ratified policy (A-5)

- Guid derivation: `guid = sha256("catmetro-meta-v1:" + payload path relative to
  unity/Assets/StreamingAssets)[:32]` — path-derived, deterministic, root-independent,
  cross-checked in the wrapper by an independent python3 hashlib implementation. No literal
  guid pinned anywhere (H3(ii)/(iii) ride the PR).
- Prune edge: a bare orphan `.meta` whose payload NAME matches a rule pattern but has neither a
  destination payload nor a source counterpart is pruned with the rule (it is the rule's
  namespace); everything else in a rule dir is reported as drift, never deleted.
- `--apply` exits non-zero when foreign (never-deleted) drift remains — fail-closed for gates.

## Evidence highlights

- Real-root `--apply` produced a **zero diff** (`git status --porcelain` empty) — A-4 held;
  stop condition 3 never fired.
- N-1 (backlog exception-class note): the wrapper now set-gates the staged **config** tree
  (payload set must be exactly `{runtime_bounds.json}`) — landed with this contract as required.
- Criterion 7 self-test: the wrapper re-runs itself against a fixture copy whose expected
  failure was repaired and asserts the wrapper then exits non-zero.

## Open at handoff

- RIDES-WITH-PR table (H2, H3(ii)/(iii), H4, H5-residual, H6, HC-25) — defaults shipped,
  recorded in the PR description for human ratification at review/merge.
- Merge NOT delegated (HC-25) — human call.

- 2026-08-06 — full committed-tree suite at 7675f11 (serial finisher): `scripts/check.sh` OK;
  `scripts/test.sh` 13/13 wrappers passed, EXIT:0 — N+1 = 13 exactly as the C6-rule capture
  predicted (baseline N=12 + this contract's one wrapper). Push + PR follow this entry.
