# Lane 13 DLC-DESIGN — frozen contract (coordination-ADOPTED)

Frozen 2026-08-13 by the coordination session at main=`2fe2a2a` (SHA verified post-fetch).

**ADOPTION RECORD (ADDENDUM v2.3 clause 5, human-directed):** the human ruled in the
coordination chat 2026-08-13 in-session, selecting "Adopt Lane 13 + Lane 10" in answer to
the session's explicit ask (agent-relayed, H-1-class caveat, per the Lanes 3/9 precedent).
The lane was verified unstarted/unpublished before adoption: no `docs/dlc-design` branch on
origin; a LOCAL branch of that name existed pointing at `1744fac` with ZERO commits beyond
its merge-base, checked out in a paused chat's worktree at `/private/tmp/cat-metro-lane13-dlc-design`
containing only dotnet lock-file restore drift and no `docs/design/` directory — removed
under this adoption and re-cut at current main. The adopted lane's PRs are authored, owned,
and — under Amendment 1 — merged by the coordination session as its own.

**Charter (ADDENDUM v2.2, binding):** owns `docs/design/dlc/**` (new) — ALL deliverables
incl. production checklists live there. Read-only: `docs/plan/**` (incl. the HUMAN-SIGNED
`monetization_spec.md` §8.10 amendment — the design source), `docs/prd/**`, `docs/adr/**`,
`docs/architecture/**`. Untouchable: `docs/store/**`, `docs/release/**` + `docs/runbooks/**`
(Lane 10), all code, `unity/**`, `content/**`, `state/mode`. State writes (v2.1 enumeration):
this contract file (first commit) + ONE `state/PROJECT_STATE.md` row at merge (140-line
tripwire: file sits at 113 — STOP and ping the human rotation ask if an append would pass
140). Prices/SKUs PROPOSED only — human-signed. Docs-lane reading: the truthfulness
standard + BOTH review legs stand in for TDD/mutation proofs; both legs are
contract-mandated ON the PR record regardless of the machine gate's verdict; two-round cap.
GATE: v2.2 on main — OPEN (verified).

## Criteria

1. **District-pack design doc** (`docs/design/dlc/districts.md`): the proposed paid-district
   catalog per §8.10's deep-catalog direction — per district: name/theme, content shape
   (level count, cosmetic set, livery/theme assets), player-facing promise, and the
   fair-core boundary restated concretely (optional side content, never a bridge between
   free districts; no gameplay-channel change from any paid item; every durable item a
   one-time permanently-restorable non-consumable).
2. **Fair-core conformance matrix** (`docs/design/dlc/fair-core-matrix.md`): every proposed
   item × §8.1 laws 1–4, explicit PASS/inapplicable cells with one-line reasons. Any cell
   that cannot honestly read PASS blocks that item from the catalog (drop or redesign — do
   not ship a failing row).
3. **Proposed SKU/price matrix** (`docs/design/dlc/sku-price-proposal.md`): SKU ids, types,
   entitlement attachments (design-level, referencing the §8.10 authority table's CM-R23/24
   rows as future replacement targets), candidate prices — every row marked **PROPOSED /
   UNSIGNED / NON-EXECUTABLE**; restate the production tripwire verbatim (no monetization
   code before the human flips `state/mode`; this lane is docs-only and activates nothing).
4. **Per-district production checklists** (`docs/design/dlc/production-checklist.md`):
   art pipeline (Polyfork custody rules per ADR-0011's pending amendment — cite as pending,
   not signed), level authoring through the EXISTING validator/stager pipeline (cite the
   real scripts), staging, entitlement design reference, QA/device pass, human gates
   (taste, signatures) marked HUMAN at each occurrence.
5. **Truthfulness:** nothing unbuilt presented as shipped; every claim about an existing
   repo system cites its real file; every human-only act labeled; the Night Harbor
   standalone-copy honesty rule and once-ever/decline law from §8.10 are not contradicted
   anywhere.
6. **Process:** both review legs (fresh-context code review + security review) posted ON
   the PR with every finding dispositioned; merge under Amendment 1 + v2.3 by the adopting
   session; census merge-record comment at merge; ONE PROJECT_STATE row.

## STOP conditions

Any edit outside `docs/design/dlc/**` + the enumerated state writes · any contradiction
between §8.10 and the PRD that requires a ruling (SURFACE it, do not resolve) · any price
or SKU presented as signed/active · the 140-line state tripwire · anything resembling
monetization code.
