# MENU-POLISH — frozen contract

Acts on the coordination session's OWN honest taste notes recorded in
`evals/results/ux/beautiful-menu-2026-08-14/ARTIFACT.md` (notes 1 and 2), which flagged the
restyled Home as falling short of product_spec §7 in two specific, *spec-checkable* ways —
not as taste preferences. Taste itself remains the human's gate.

**Authority:** the human's standing 2026-08-14 directive ("a rich looking beautiful game").
This is a visual refinement of BEAUTIFUL-MENU (#90, merged), stacked on CM-BOOT-HOME so the
shipped Home is the polished one.

**Base:** task/CM-BOOT-HOME @ 988e758. **Branch:** task/MENU-POLISH. **Mode:** sprint.

## Criteria

1. **Parked districts read as scenery, not wash.** §7's silhouettes must have presence; at
   `DepotNavy` alpha 0.30 over CreamCard they rendered light taupe (device-verified). Raise to
   0.44 — real presence, still far below the L001 pin's full-strength navy so S-01's "one
   pulsing affordance" keeps visual primacy.
2. **The base board reads as a board.** §7 asks for "a base-board bevel with a visible
   cardboard edge" and "soft contact shadows"; CreamCard on WarmPaper is a ~8-value delta and
   registered as no board at all. Add a `BoardEdge` Image drawn BEHIND the board, offset
   DOWNWARD so it shows as thickness under the bottom lip with a hairline at the sides and
   NOTHING above the top edge (a symmetric rim reads as a UI panel border, not a board on a
   desk).
3. **Every Home invariant preserved.** New node is a plain `Image` (render-only whitelist),
   name `BoardEdge` checked against all 14 commerce-tripwire substrings, no new region, no
   layout/pulse/registration change. PlayMode suite stays green.

## Verified on device (the standing visual rule — two iterations, both looked at)

- **v1 FAILED and is recorded as a failure:** `InkNavy` @ 0.28 over WarmPaper computes to
  ≈#BEBEBF and rendered as a washed light-grey *frame* — the opposite of a bevel. Kept in the
  code comment so the value is not re-tried.
- **v2 (shipped here):** `DepotNavy` @ 0.55, offset down — the board now has a visible bottom
  band and right edge, giving it definition it entirely lacked.
- **Honest residual (for the human's taste gate):** the band still reads GREY rather than a
  warm shadow, because the palette has no warm dark (§7's darks are both blue-navy). It gives
  the board an edge but does not yet sell "cardboard on a wooden desk." A stronger candidate,
  deliberately NOT taken here because it changes a merged contract's pinned colors: swap the
  two surfaces — background `CreamCard` (darker) and board `WarmPaper` (lighter) — so the
  board reads as a lit surface on a darker desk with no extra node at all. Recorded for the
  human to direct.

## Out of scope

The §7 12% corner radius (a `UiChrome` material/shader property, not a per-instance value),
the corner vignette (needs a sprite), and the cat/prop art itself (RICH-ASSETS #88 — the
generated assets are the real richness and need the human to arm generation).
