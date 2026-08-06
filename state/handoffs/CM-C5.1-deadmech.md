# CM-C5.1 — dead-`newMechanic` gate: session handoff / evidence record

Contract: `state/handoffs/CM-C5.1-frozen-contract.md` (frozen at branch anchor `8a98e16`;
ratification addendum `28fc8ba`: posture **BLOCKING**, scope **`meta.newMechanic` only** —
HC-10 x HC-14 defaults CONFIRMED). Branch: `task/CM-C5.1-dead-mechanic-gate`.

## Baseline capture (C6 rule — recorded BEFORE any code was written)

- Rebase baseline: `git merge-base HEAD origin/main` = `5bf63cd9ebb1821d4b74071eab1c66b351cbf5ff`.
- **N = 11** wrappers at the branch anchor (`find tests -name '*.test.sh' | wc -l` → 11:
  analytics/queue, content/importer, daily/daily-pipeline, domain/determinism, save/save,
  smoke/substrate, solver/solver, unity/devcap, unity/editmode, unity/failure,
  validation/validator). This contract adds **no wrapper**: target is N unchanged (11) with
  `PASS tests/validation/validator.test.sh` present.
- dotnet test before-total: **331 passed, 0 failed** (`dotnet test dotnet/CatMetro.sln`).

## Design decisions inside the contract's envelope

- **Liveness rides `CampaignVerdicts` as one row per campaign level** (criterion 8 "every
  campaign level's liveness row"), labelled `Stage.NoveltyCheck` (A-DM-4). No 12th `Stage`
  member, no 12th per-level row (criterion 4).
- **Tags (criterion 6 / H6):** every campaign verdict's `Value` is prefixed with a machine tag:
  `tag=CM-R06.2` (order), `tag=CM-R09.1` (count), `tag=CM-R09.3` (band),
  `tag=CM-R06.2-liveness:<levelId>` per liveness row (level-qualified so criterion 6c's
  uniqueness holds on a multi-level campaign; the zero-campaign SKIPPED row carries the bare
  `tag=CM-R06.2-liveness`). The two amended selectors match tags by exact equality
  (`v.Value == "tag=..."`), immune to the liveness prefix. Campaign rows now render `value` in
  BOTH output forms (JSON `campaign[].value`, table `[...]` suffix) — required by criterion 8;
  flagged as the H6 report-shape change in the PR.
- **Fixture solvability:** `L004-dead-queue.json` removes L004's second tick-8 wave (2 cats
  remain), so `win.deliveries` drops 4 → 2 — otherwise stage 4 would FAIL Unsolvable, the
  liveness row would read `SKIPPED(no winning log)`, and the fixture would fire the WRONG gate.
  The live twin restores the wave AND deliveries=4 (semantically the shipped L004).
- **Evidence format** (criterion 8): `tag=...; newMechanic=<m|null>; exercised=<true|false>;
  evidence=<maxQueued=N@tick T | toggles=N,routeChangedAtTick=T | none>`; `none` covers the
  skipped/pinned/unexercised limbs (the false verdict IS the printed measurement).
- **UNREACHABLE detail wording:** `PINNED(<m> unreachable — the importer refuses it first)`
  (honest: `ContentResult.cs:22` pins it at import), vs the contract's single template
  `PINNED(<m> unobservable — no DTO field)` which is kept verbatim for the four UNOBSERVABLE
  mechanics. The only criterion-2 verdict check (newMechanic "gate") asserts the template
  verbatim. Recorded here as a wording deviation, not a semantic one: both are Pinned,
  non-blocking, never Pass.
- **Observer tick convention (A-DM-1):** sampled AFTER each step; the recorded tick is
  `state.Tick - 1` (the tick the step processed), so L004's same-tick double emission reads
  `maxQueued=1@tick 8` exactly as hand-derived in criterion 1a.

## First red-bar run (F-K answer — A-DM-6 measured)

- Shipped corpus L001–L005 through the real CLI with the gate BLOCKING: **exit 0 — all five
  pass**. L001 `switch` exercised (toggles=1), L004 `queue` exercised (maxQueued=1@tick 8),
  L002/L003/L005 `newMechanic: null` → `SKIPPED(no declared newMechanic)`. Stop condition 1
  did NOT fire; the pre-ratified contingency was not needed. CM-C11 may author against a known
  BLOCKING gate.

## Evidence summary

See the PR's per-criterion table. Gates: check.sh OK; scripts/test.sh green at N=11 with
`PASS tests/validation/validator.test.sh`; dotnet test after-total 331 → 354 (+23 new cases,
0 failed); EditMode suite green via tests/unity/editmode.test.sh (pinned editor); golden hash
unchanged (tests/domain/determinism.test.sh PASS).

## RIDES-WITH-PR human calls (unchanged from the contract)

- H1 posture: **ratified BLOCKING** (post-freeze addendum) — shipped so.
- H2/HC-14 scope: **ratified newMechanic-only** — shipped so; widening later re-opens the
  CM-C11 L006 anchor (joint note; stop condition 10).
- H3/HC-15 (witness = solver-optimal log; exercised = post-step visibility): default shipped;
  evidence prints per level so a human can overrule per board.
- H6/HC-16 (tags + campaign `value` in the JSON report — report-shape change): shipped, needs
  the human's eyes at review.
- H7/HC-17 (liveness in the daily artifact): NO — dailies are non-campaign; criterion 5 holds.
- H8/HC-18: moot this run (corpus passed).
- HC-25 merge delegation: NOT delegated this session — the merge is the human's, and the
  `state/PROJECT_STATE.md` edit (append one line + strike the `:58` debt row) is scoped
  "on merge only", so it is deliberately NOT in this diff.
