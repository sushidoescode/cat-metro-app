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

- 2026-08-07 — review round 1 (completion review, PR #43): NOT MERGEABLE — F1 (whole-repo
  porcelain assert red on cold checkout: earlier dotnet wrappers rewrite packages.lock.json;
  the PR's ci job was red), F2 (criterion-8 merge-base unresolvable in CI's detached
  no-main checkout), F3 (no census proving write mode stays inside the N1 grant), F4 (stager
  could report OK after a failed write — no -e, no rc checks). Stager itself verified clean
  by the reviewer (independent SHA census; zero escapes).
- 2026-08-07 — fixes at f76dcf4: F1→start-snapshot dirt diff with offending paths printed
  (F6, F7 ride along); F2→base resolves origin/main→main→fetch, with a shallow-history
  fallback comparing the verifier blob at the two tips directly; F3→temp-root census: every
  added path must be under the staged tree AND source SHAs unchanged; F4→post-apply verify
  re-walks both rules read-only before claiming OK. Proofs: seeded escape RED (named);
  neutralized-cp stager RED via post-verify; pre-existing lock-file dirt GREEN; CI-shaped
  shallow single-branch detached clone GREEN; clean tree GREEN.
- 2026-08-07 — merged main (9527a75): UX wave + taxonomy landed; baseline re-captured
  N=13 → target 14. Full committed-tree suite: `scripts/check.sh` OK; `scripts/test.sh`
  14/14 wrappers passed, EXIT:0. ERRATA (F8, frozen text stays frozen): contract line 18
  says branch task/CM-C10-stage-content; the real branch is task/CM-C10-content-stager.

- 2026-08-07 — review round 2 (same reviewer, fix verification by seeded defect): MERGEABLE,
  no new HIGH. F1-F4 confirmed closed (six seeded defects; CI-shaped clone; read-only-dest
  probe with control); real CI green on 6d044d9 (run 31145961972, 14/14). Non-blocking
  carried to the hardening follow-up: census covers only the empty-dest leg (prune/rm path
  and in-tree non-goal writes uncensused — hoist census into apply_to + narrow whitelist to
  the two rule dirs); new_dirt() is additive-only (same-path writes invisible outside CI).
- 2026-08-07 — errata completion (round-2 finding 3): F5 — criterion-3 comparison excludes
  ALL *.meta; the three FOLDER metas (StreamingAssets/config.meta, content.meta,
  content/levels.meta) are neither authored nor verified by the stager; CM-C11 inherits
  this gap KNOWINGLY (state/backlog.md:146-152 assigns folder metas to this class). F9 —
  f86ec39 landed stager+wrapper+fixtures in one commit (no failing-test-first commit);
  sprint mode prices this, recorded not hidden. Pointer correction (finding 4): the fix
  commit reachable from this branch is 5ff8956 (f76dcf4 was orphaned by an amend).
