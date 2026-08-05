# SESSION HANDOFF — UX layer build-out (parallel lane, opened 2026-08-05)

Read order: `state/PROJECT_STATE.md` → this file → `docs/prd/ux-flows.md` (in full) →
`docs/adr/0007-*` → the PRD sections each contract cites. Project memory + `state/handoffs/CM-*.md`
carry the landmines; honor all of them.

## Mandate (human decision, 2026-08-05, in-session)
The human approved building the UX layer from the PRD's ux-flows (12 stories, S-01/S-02/S-03
screen specs). **TG-1..TG-8 remain in-build HUMAN taste gates** — a blanket approval does not
pre-pass them; schedule human eyeballs at each. Monetization/store surfaces stay OUT
(mode=sprint; the attempt-1 invariant `docs/prd/PRD.md:208` — no paywall/ad surface may even be
constructed on attempt 1 — is a tripwire, not a suggestion).

## Why this lane exists (device findings, 2026-08-05)
First device run (Pixel 9 Pro) proved the mechanical spine works end-to-end and exposed the UX
gap violently: no tutorial, no menu, no affordances; the halt-at-pinned-boundary posture reads as
a silent freeze (F-DEV-4, feeds open Q-B/NEW-Q4); L001's teach window is ~2 s with zero guidance.
Evidence: `evals/results/device/c2b-crit8/ARTIFACT.md`. Rank the decompose accordingly — the
first-run experience (home → play → teach → fail-visibility) is the pain that motivated this lane.

## Process (non-negotiable)
Same loop as every merged contract: decompose → frozen contract per slice (testable criteria,
scope boundary, stop conditions) → red → green → fresh-context review round → disposition →
merge. Sprint pricing via `scripts/forge-risk.sh`. Reviews are MANDATORY fresh-context rounds.
TDD per criterion — PlayMode/EditMode tests first. Never weaken an existing gate to pass it.

## Ownership boundary (parallel-safety — DISJOINT from the device/content session)
- YOURS: `docs/` UX decompose artifacts · new UX contracts in `state/handoffs/` ·
  `unity/Assets/Scripts/Presentation/**` chrome/screens (per contract) · **append-only** rows in
  `unity/Assets/Resources/Strings/ui.csv` · your own tests under `unity/Assets/Tests/**`.
- NOT YOURS (the other session owns in-flight): `unity/Assets/Scripts/Bootstrap/**` (DEVCAP +
  device-config fix landing there), `GameRoot.cs` (wait until CM-C3-DEVCAP and the device-fix
  contract are MERGED — check `git log` — then coordinate edits via rebase), `Content/**`,
  `Domain/**`, `scripts/`, `tests/` harness wrappers, L006–L010 content (tranche-3, other session).
- Immutable (hooks enforce): `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `evals/` except `evals/results/`.
- Work in your own git worktree + `task/CM-UX-*` branches. `git add` by EXPLICIT PATH only
  (the human's `.claude/settings.json` is dirty and must never enter a PR). Update
  `state/PROJECT_STATE.md` with single appended lines only (merge-conflict discipline); your
  running log lives here and in your `CM-UX-*.md` handoffs.

## Known collision you must resolve BY CONTRACT, not silently
`tests/unity/editmode.test.sh:72-75` enforces ONE input consumer and bans
`EventSystems|IPointerDownHandler|...|OnMouse` tokens across Presentation+Bootstrap (CM-R07.1
one-gesture discipline). ADR-0007's UGUI+TMP chrome collides with that gate head-on. The
resolution is a frozen contract that EXPLICITLY evolves the gate (routing chrome hits through the
one `TapInput` handler, or a reviewed gate amendment with a negative fixture) — reviewers must see
the gate change as a first-class deliverable. Silently editing the wrapper is a failed review.
Related debt: the retry band has no rendered CTA yet (CM-C3 review N2) and must shrink when
Rewind/Back join the band — that lands with S-03 chrome, i.e., with you.

## Prose landmines (they keep firing)
Grep gates in this repo scan comments and HEREDOCs by design. Never name storage-path APIs
outside Bootstrap even in prose; sweep your own comments/scripts before adding any token-guard;
zero literal UI strings in components (ui.csv keys only, appends only, never edit an existing
row). Run `bash scripts/test.sh` only on a committed tree.

## Status log (append below)
- 2026-08-05 — lane opened; awaiting first decompose.
