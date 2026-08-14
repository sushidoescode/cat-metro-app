# Beautiful menu — design (BEAUTIFUL-MENU)

Grounds the human's 2026-08-14 directive ("make sure there is a beautiful menu"; "a rich
looking beautiful game that makes people want to purchase cute cats") in what the repo
actually is today. Design artifact only — no code. Authored against product_spec §7 +
prd/ux-flows.md, and against the real constraints the current Home code + tests impose.

## The load-bearing finding — surface to the human before implementing

**The shipped build has no menu.** Everything Home-related in `GameRoot.cs` is fenced
behind `#if DEVELOPMENT_BUILD || UNITY_EDITOR` and only composes when a dev flag
(`BootToHome`) or a dev file override is set. Shipped boot goes straight into `L001`
gameplay. So "make a beautiful menu" forks:

- **(A) Restyle the dev Home** — improve what exists (currently three grey rectangles, a
  white 48pt title, one navy square on a transparent background) to the warm diorama look.
  Reversible, in-scope, low-risk — but it ships to nobody.
- **(B) Promote Home to the shipped launch screen** — unfence the boot path so real players
  see the menu. This is where the commercial value is ("makes people want to buy cats"),
  but it changes shipped boot behavior AND collides with an unresolved monetization
  question (PRD NEW-Q30 / TG-3: monetization_spec wants the paywalled Night Harbor district
  visible from first map view; product_spec wants all-depot-silhouettes in session 1). That
  conflict is human-decision territory, and the current `HomeScreenTests` commerce tripwire
  actively bans shop/daily/paywall nodes in Home.

**Recommendation:** do (A) now (beautiful dev Home, reversible, unblocks the look), and put
(B) to the human as the high-value follow-up with the NEW-Q30 fork attached — because a
beautiful menu nobody ships does not serve the stated commercial goal, and promoting it is
their call, not an agent's.

## Hard constraints the design must honor (from the tests — non-negotiable)

`HomeScreenTests.cs` is the strictest gate and it has positive-control decoys, so these are
real walls, not soft guidance:
- **Render-only whitelist:** every component under Home must be one of `Transform,
  RectTransform, Canvas, CanvasRenderer, CanvasScaler, Image, TextMeshProUGUI,
  HomeScreenView`. So: **no Button, no LayoutGroup, no Animator/Animation, no Shadow/Outline,
  no RawImage, no ParticleSystem.** Every visual is an `Image` or `TextMeshProUGUI`; all
  interaction routes through `ChromeRegions`, never Unity's event system.
- **Commerce tripwire:** no node name may contain shop/store/daily/badge/streak/share/
  notif/night/harbor/access/paywall/advert/reward/ticket (case-insensitive). (This is why
  (B)'s Daily/Shop bottom-band entries can't just be added — they trip the wire until the
  human resolves NEW-Q30 and the test is amended by a human-authored change.)
- **Copy via `ui.csv` only:** the title must equal `UiStrings.Get("home.title")` and contain
  no literal string / no `"??"`. `ui.csv` is append-only and row-count-pinned; any NEW menu
  string needs a human-authored bump of the pinned count (UiCsvDisciplineTests).
- **Motion never carries information:** with motion off, the pulse scale locks at exactly
  `1f` and every element still renders. Beauty via motion is additive only.
- **Layout by law:** `HomeLayout`/`HudBands` compute rects from safe-area + dpi; no direct
  `Screen` reads. The L001 pin stays a 72dp square in the bottom thumb band, ≥48dp target.
- **#85 seam:** keep `HomeScreenView.Create`'s post-#85 signature `(Transform, bool
  dailyUnlocked)` and the `LevelSelected`/`DailySelected` action seams intact; restyle
  inside the `Make*` helpers + add a background node in the `Create` body. This is the
  lowest-conflict path and must land AFTER #85 merges.

## Step 1 — port the palette to code (`Palette.cs`, new file, the foundation)

Today every color is an inline `new Color(...)` literal; the authoritative 12-color palette
lives only as a markdown table in product_spec §7 and has never been ported. Create
`unity/Assets/Scripts/Presentation/Theme/Palette.cs` — a static class of named
`Color` constants, values from §7 (sRGB, /255):

| Constant | Hex | Role |
|---|---|---|
| `CreamCard` | #F2EAD9 | board/table base |
| `WarmPaper` | #FAF6EC | paper highlight / UI panels — **Home background** |
| `InkNavy` | #22304A | outlines / primary dark — **title text** |
| `DepotNavy` | #131C30 | deep shadow / parked silhouettes |
| `MetroTeal` | #3BAFA8 | accent 1 / success |
| `TicketOrange` | #F08A3C | accent 2 / CTA — **the L001 pin glow** |
| `SignalRed` #E15A47 · `HarborBlue` #3E7CC9 · `TabbyYellow` #EFC13D · `GardenGreen` #4FA36A · `CatnipViolet` #A06BD8 | line colors (content) |
| `AlarmCoral` | #D93A2B | fail/overflow |

`Palette.cs` is a plain static class (not a component) so it never trips the whitelist. It
becomes the single source every screen migrates to over time (kills the inline-literal
drift the reviewer keeps finding). RED-first: a small EditMode test pins two or three hex
values so a typo in the port fails.

## Step 2 — restyle Home to the warm tabletop diorama (within the whitelist)

Node tree (all `Image`/`TMP`, all under the existing `HomeScreenView`):
1. **Background** — full-bleed `Image`, `Palette.WarmPaper`. (Today: none — transparent.)
   This alone is the biggest single upgrade: the menu goes from floating-on-black to a
   warm paper tabletop.
2. **Base-board bevel** — a slightly inset `Image` in `Palette.CreamCard` with the shared
   `UiChrome` rounded material (min corner radius 12% per §7), giving the "cardboard edge"
   miniature cue. Soft, not sharp.
3. **District silhouettes ×3** — recolor from flat grey `#595F6B@55%` to `Palette.DepotNavy`
   at ~35% over the cream board: parked scenery that reads as "curiosity, not locked."
   Keep the existing anchor rects.
4. **Title** — `home.title` ("Cat Metro") in `Palette.InkNavy` (was pure white), TMP,
   kept in the top band. If a title font asset gets added it goes through Resources like
   `UiChrome` (no whitelist impact — still TMP).
5. **L001 pin** — keep the navy square, add a `Palette.TicketOrange` ring behind it (the
   shape twin already exists as a cream ring; recolor/duplicate to a warm CTA glow). The
   pulse (unscaled sin, 8% amplitude) stays; it's the single CTA per spec, and motion-off
   still renders the ring.
6. **Corner vignette** — an `Image` with a soft radial (via UiChrome material tint) at 8%
   corners per §7, deepening the tabletop feel. Additive; motion-independent.

Everything above is Image/TMP only → whitelist-clean. No new `ui.csv` strings needed (only
`home.title` is text), so no append-law bump.

## Step 3 (human-gated, follow-up) — promote to shipped + richness

Only after the human rules on (B)/NEW-Q30: unfence the boot path so Home is the real launch
screen; then the five-pin district map, the bottom chrome band (Daily once unlocked, per
#85), the generated cat art (RICH-ASSETS #88) posed on the diorama as the "buy cats"
storefront hook. Each piece is its own contract; several need the monetization posture flip
first (billing tripwire) which is human-only.

## TDD / review posture

Palette port + Home restyle land as ONE contract off post-#85 main: frozen contract →
RED (palette hex test + a Home-background-exists / Home-uses-Palette assertion that fails
today) → GREEN → the existing HomeScreenTests/HomeLayoutTests must stay green byte-for-byte
(never-weaken). Visual-verification rule applies: render real frames on the emulator (the
EMU-RIG rig, #89) and eyeball the restyled Home before handoff — code-green is not enough
for anything visual.
