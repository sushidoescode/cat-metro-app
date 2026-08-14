# Beautiful menu — restyle evidence (2026-08-14)

Agent claim (not attested): the warm-tabletop restyle of the dev Home, rendered on the
Android emulator. **This is a TASTE artifact for the human's judgment — the constitution
reserves taste (and XR feel) to the human; the agent verifies it renders correctly and
matches the spec palette, not that it is "beautiful."**

## Provenance

- Build: dev APK from `task/BEAUTIFUL-MENU` @ `b41c63f` (CatMetroCliBuild dev path,
  CLI_BUILD_RESULT Succeeded, 0 errors — the palette + restyle compiles clean).
- Device: AVD `catmetro-test` (pixel_7, arm64-v8a, headless SwiftShader), emulator-5554,
  portrait 1080x2400.
- Home reached via the device seam: `{"bootToHome": true}` pushed to
  `<persistentDataPath>/devcap/boot.json` (DevBootOverride), then a cold start — the only
  device-side way to compose the dev screen flow without a rebuild.

## Frames (sha256)

| # | frame | what it shows |
|---|-------|----------------|
| 01 | `9686dd4c890ece0fcf968f530318169a51d6b28ea141a938edf6da4a41e6a630` 01-home-restyled.png | The restyled Home: warm-paper full-bleed ground, inset cream base board, "Cat Metro" title in ink navy, three parked-district silhouettes, and the L001 pin (ink navy) ringed by the ticket-orange CTA glow, bottom thumb band. |
| 02 | `36e1998aa4dcceb9284b23e8a5f7c9e8bad96a6b99225784392e8236964b1825` 02-home-restyled-alt.png | The same, second capture (near-identical; the pulse is mid-cycle). |

## What the restyle changed (vs the pre-restyle greybox)

Before: three flat grey rectangles + a pure-white 48pt title on a fully transparent
(black) background. After: the product_spec §7 warm-tabletop palette — `WarmPaper`
background, `CreamCard` base board, `InkNavy` title, `DepotNavy`@30% silhouettes,
`TicketOrange` CTA ring, `InkNavy` pin — all from the new `Palette` source of truth.

## Honest taste notes FOR THE HUMAN (render-correct, but candidates to refine)

1. **Silhouettes read light-taupe**, not deep navy — 30% `DepotNavy` over `CreamCard`
   blends light. If "parked scenery" wants more presence, raise the alpha or drop the
   board under them.
2. **The base-board inset is subtle** — `CreamCard` on `WarmPaper` is low-contrast by
   design (both warm neutrals); the "cardboard edge" is faint. A slightly darker board or a
   thin `InkNavy` hairline border would read more as a diorama base.
3. **Corner rounding is minimal** — the shared `UiChrome` material rounds little at these
   large rect sizes; the §7 "min 12% corner radius" is not visually strong yet.
4. **The pins/silhouettes are still plain rectangles** — no cat/district shapes. That is
   the RICH-ASSETS (#88) generated-art follow-up: the cats posed on this diorama are the
   "makes people want to buy cats" hook, and need the human to arm generation first.
5. **The dev console + halt banner are dev-build NOISE**, not the menu — the six
   "CapsuleCollider doesn't exist" lines are the known art-chain greybox debt, and the
   console only auto-shows in a Development Build on error. A real build shows none of it.

## Scope reminder

This restyles the DEV Home (fenced behind `DEVELOPMENT_BUILD`/`BootToHome`). Shipped boot
still goes straight to gameplay — **promoting Home to the shipped launch screen is a
separate, human-gated decision** (it changes boot behavior + touches the unresolved
NEW-Q30 monetization-visibility conflict). See `docs/design/ux/BEAUTIFUL-MENU-design.md`.

## Blinded-rigs disclosure

Frames captured and described by the same session that wrote the restyle. The palette
values are independently pinned by `PaletteTests` (hex-derived); the on-screen colors
match the spec by eye, but the taste judgment is explicitly the human's.
