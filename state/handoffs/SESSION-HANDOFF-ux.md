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
- NOT YOURS (the other session owns in-flight): `unity/Assets/Scripts/Bootstrap/**` — which
  INCLUDES `GameRoot.cs` — is a flat deny until BOTH CM-C3-DEVCAP and the device-config fix
  (CM-C2b-DEVFIX) are MERGED to main (check `git log`); after both merge, Bootstrap edits are
  allowed only inside your own reviewed contracts, rebased on latest main. Also not yours:
  `Content/**`, `Domain/**`, `scripts/`, L006–L010 content (tranche-3, other session), and
  `tests/` harness wrappers — with ONE carve-out: `tests/unity/editmode.test.sh` may be edited
  ONLY inside the explicit gate-evolution contract below, sequenced after CM-C3-DEVCAP merges,
  rebased on latest main. (DEVCAP ships its own new wrapper `tests/unity/devcap.test.sh` and does
  not edit editmode.test.sh, so after its merge the file has a single writer: you.)
- Immutable (hooks enforce): `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `state/trust.json`, `evals/` except `evals/results/` —
  and within results, `evals/results/attested/` is HUMAN/CI-ONLY: only attested evidence can
  raise the autonomy dial, so never write there.
- Work in your own git worktree + `task/CM-UX-*` branches. `git add` by EXPLICIT PATH only
  (the human's `.claude/settings.json` is dirty and must never enter a PR). Update
  `state/PROJECT_STATE.md` with ONE appended line per MERGED PR only (two sessions append in
  parallel; the file has a hard ~150-line cap — if it nears the cap, propose rotation to
  `state/archive/`, humans prune). Your running narrative lives here and in your `CM-UX-*.md`
  handoffs, never in PROJECT_STATE.

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
- 2026-08-05 — decompose complete (3-lens panel + adversarial verify + completeness critic, 8 agents): tranche-1 = CM-UX-01..07 ranked in `docs/ux/ux-layer-decompose.md`; CM-UX-01 frozen contract cut; gate posture (a) everywhere — no gate-evolution PR needed in tranche 1; review round 1 (15 findings, 5 blocking) applied incl. demoting the TMP/ADR-0007 deferral to human question Q-6; 6 human questions filed (decompose §6), none blocking slice 1.
- 2026-08-05 — review round 2: MERGE verdict; all 5 blockers confirmed genuinely fixed; N1 (CM-UX-07 reconciliation must be its own reviewed line, never composition-only) + N2 (stale P-2 xref) applied pre-merge; N3/N4 dispositioned as notes; reviewer re-answered Amendment 1 → agent squash-merge permitted (nothing flagged for human judgment; asking Q-1..Q-6 ≠ deciding them). #27 merged under the session's standing delegation.
- 2026-08-05 — CM-UX-01 executed via forge-build (sprint pricing, in-session TDD): red 22aedca (18 tests failing right) → green 6825315 (EM 348/348, PM 27/27) → review round 1 (6 findings: unpinned nearest-center/tie law F1, MeetsMinTarget dpi-slot hazard F2, registry-lifetime F3, +3) → fixes 8257ab3/d1e94bd (disc law extracted pure + pinned resolution-independent; EM 353/353) → round-2 MERGE (all fixed on merits, none waived; Amendment 1 clear) → #28 merged. CM-UX-02 handoff notes: owners Unregister in OnDestroy (F3); reviewer N2: eyeball the first MeetsMinTargetPx call site; N1: pass a count if the scratch is ever pooled.
- 2026-08-05 — HUMAN answers (AskUserQuestion batch, this session): Q-1 land DRAFT halt copy now ("Signal fault — the line stopped"), voice-pass at TG-5 · Q-2 YES restart escape as CM-UX-07's human-gated wiring line · Q-3 HOLD results-panel attach until LoadNext exists · **Q-6 IMPORT TMP+UGUI NOW (recommendation overridden — ADR-0007 honored directly)**. Base moved mid-merge (#26 DEVCAP, #29 ratifications): #28 update-branched, combined tree verified locally (EM 353/353, PM 33/33) pre-merge. #29's ratified sequencing ("DEVFIX's 7 Presentation lines precede UX-lane code") honored by gating CM-UX-02's RED PHASE on DEVFIX's merge (CM-UX-01 shipped no rendering code; TMP shaders belong on the restored-URP baseline). CM-UX-02 frozen contract cut.
