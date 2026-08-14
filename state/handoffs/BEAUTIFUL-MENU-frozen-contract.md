# BEAUTIFUL-MENU — frozen contract

**Authority:** the human's 2026-08-14 directive (H-1-class, agent-relayed): "make sure
there is a beautiful menu"; "a rich looking beautiful game that would want to make people
want to purchase some cute cats." Design analysis: `docs/design/ux/BEAUTIFUL-MENU-design.md`
(added by this contract).

**Base:** origin/main @ 440ce5b (post-#85, so this builds on the real
`HomeScreenView.Create(Transform, bool dailyEntryUnlocked)` signature and does not conflict
with the Daily wiring). **Branch:** task/BEAUTIFUL-MENU. **Mode:** sprint.

## Scope — increment 1 of the menu work: palette-to-code + warm-tabletop restyle of the dev Home

This restyles the EXISTING dev Home (it renders only under `DEVELOPMENT_BUILD`/`BootToHome`;
shipped boot still goes straight to gameplay). Promoting Home to the shipped launch screen
is a SEPARATE, human-gated decision (it changes boot behavior + touches the unresolved
NEW-Q30 monetization-visibility conflict) — explicitly OUT of scope here and recommended to
the human in the design doc.

## Criteria

1. **Palette ported to code:** new `unity/Assets/Scripts/Presentation/Theme/Palette.cs` — a
   static class of the 12 product_spec §7 colors as named `Color` constants (sRGB, /255).
   It is a plain static class (not a MonoBehaviour/Component), so it never affects the
   HomeScreenTests whitelist. This kills the inline-`new Color(...)` drift reviewers keep
   flagging and is the single source the restyle + future screens consume.
2. **Home restyled to the warm tabletop diorama**, inside every existing wall:
   - a full-bleed background `Image` in `Palette.WarmPaper` (today: none — transparent);
   - an inset base-board `Image` in `Palette.CreamCard` with the shared `UiChrome` rounded
     material (the diorama "cardboard edge");
   - parked-district silhouettes recolored from flat grey to `Palette.DepotNavy` at low
     alpha (parked scenery, not locks — S-01);
   - the title recolored from pure white to `Palette.InkNavy`;
   - the L001 pin's raised ring recolored to `Palette.TicketOrange` (the single warm CTA
     glow); the pin stays ink navy (via `Palette.InkNavy`, same value).
   New node names avoid every banned tripwire substring
   (shop/store/daily/badge/streak/share/notif/night/harbor/access/paywall/advert/reward/ticket).
3. **Every existing HomeScreenTests / HomeLayoutTests invariant preserved, byte-for-byte:**
   csv-keyed title text unchanged; pin rect == `HomeLayout.PinRect`; pulse varies motion-on
   and locks at exactly 1f motion-off; `RingVisible` true in both modes and false when
   hidden; exactly ONE registered region (the pin); the render-only whitelist holds (every
   new node is `Image`); the commerce tripwire stays clean. No existing test weakened.
4. **TDD:** RED-first — (a) `PaletteTests.cs` (EditMode) pins ≥3 hex values and fails to
   compile/parse until `Palette.cs` exists; (b) a new `HomeScreenStyleTests` (PlayMode)
   asserts the background node exists in `Palette.WarmPaper` and the title color is
   `Palette.InkNavy` — both RED against the current grey/transparent Home, GREEN after.
5. **Visual verification (the standing rule):** build a dev APK with `BootToHome`, render the
   restyled Home on the emulator, and commit the frame(s) to
   `evals/results/ux/beautiful-menu-<date>/` for the human's TASTE gate. Code-green alone
   does not close a visual change; the human judges whether it is actually beautiful.

## Out of scope (recorded, not done)

- Promoting Home to the shipped launch screen (human decision; NEW-Q30 conflict).
- The five-pin district map, bottom chrome band, generated-cat storefront (later contracts;
  several need the monetization posture flip = human-only).
- Migrating other screens' inline colors to `Palette` (follow-up sweep).
- A corner vignette / soft contact shadows (need a sprite/material; deferred to keep this
  increment tight).

## Assumptions (unlisted assumptions are defects)

- `UiChrome` shared material is the right rounded-rect material for the base board (it is
  what every existing chip uses).
- EditMode palette test is locally runnable via `tests/unity/editmode.test.sh`; the PlayMode
  style + existing HomeScreenTests are authoritative at CI (the ~2h suite) — local evidence
  is the EditMode run + the emulator frames + reading the tests against the diff.
- Committing PNG taste frames under `evals/results/ux/` is the established pattern (the
  ui-chrome-pass artev did exactly this).
