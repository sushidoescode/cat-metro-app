# Menu polish — device evidence (2026-08-14, MENU-POLISH)

Agent claim (not attested). Two iterations, **both rendered on device and looked at** — the
first is kept because it FAILED, and a failed visual attempt is evidence too.

## Provenance

Dev APKs from `task/MENU-POLISH` (both builds Succeeded, 0 errors), emulator-5554
(pixel_7, arm64-v8a, headless SwiftShader), portrait 1080x2400, Home reached through the
CM-BOOT-HOME shipped boot path (no boot.json).

## Frames (sha256)

| # | frame | verdict |
|---|-------|---------|
| 01 | `40e3bdd2bb65115a777106809d2877c1af2c1ce470e12fd9b33ce46f189a13c9` 01-v1-washed-grey-frame-FAILED.png | **v1 — FAILED.** `InkNavy` @ 0.28 over WarmPaper computes to ≈#BEBEBF: instead of a bevel it drew a washed **light-grey frame** around the board, symmetric on all four sides — reads as a UI panel border, the opposite of the §7 intent. The silhouette deepening (0.30→0.44) in the same build DID land and is visibly better. |
| 02 | `0262ee2c5a65a7b522b6cfef07aa20e880535f659f99381c4880a6440eca1761` 02-v2-shipped-bottom-band.png | **v2 — shipped here.** `DepotNavy` @ 0.55 offset downward: a visible band under the board's bottom lip and a hairline down the right, nothing above the top. The board now has definition it entirely lacked. Dev console dismissed for this capture so the bottom band is actually judgeable — in frame 01 the console covered exactly the region under assessment. |

## Honest verdict (this is a taste artifact — the human decides)

**Won:** the parked districts read as real scenery instead of washing out, and the board is
no longer invisible against the background. The orange CTA pin still clearly dominates, so
S-01's single-affordance law holds.

**Not won:** the band renders GREY, not a warm shadow, so it reads more like a seam than
cardboard on a wooden desk. The cause is structural rather than a bad value — §7's only
darks (`InkNavy`, `DepotNavy`) are both blue-navy; the palette has no warm dark to cast a
believable desk shadow with.

**The stronger candidate, deliberately not taken:** swap the two surfaces — background
`CreamCard` (darker) and board `WarmPaper` (lighter) — so the board reads as a lit surface
on a darker desk, needing no extra node at all. Not done unilaterally because it inverts
colors pinned by a merged contract's tests (#90's `HomeScreenStyleTests` asserts
`BackgroundColor == WarmPaper`), and because "which surface is the desk" is a taste call
that belongs to the human, not an inference from a spec line.

**Also still pending and bigger than any of this:** the pins and districts are still plain
rectangles. The generated cats and props (RICH-ASSETS #88) are the actual richness the
directive asks for, and they need the human to arm generation with their keys.

## Suite

PlayMode 153/153 passed, 0 failed at this tip — the new `BoardEdge` node is whitelist-clean
(plain `Image`), tripwire-clean (name checked against all 14 banned substrings), registers no
region, and changes no layout, pulse, or registration behavior.

## Blinded-rigs disclosure

Captured and judged by the same session that authored the change, including the negative
verdict on its own v1.
