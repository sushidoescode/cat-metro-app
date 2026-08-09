# Cat Metro concept-art reference note — 2026-08-09

- **Status:** Reference-only documentation; not a shipping asset, art acceptance, dependency, or
  permission to modify another lane's files
- **Date:** 2026-08-09
- **Relates:** `docs/plan/specs/product_spec.md` §§3 and 17,
  `docs/plan/specs/monetization_spec.md` §8, `docs/prd/leaderboards-contract.md`, and the 2026-08-09
  parallel-push handoff
- **Authority order:** product specification → human-reviewed rendered-frame evidence → these concept
  renders. A concept-render color or detail never overrides a specified value.

## 1. Frozen source identity

The two images remain local references in `~/Downloads`; this docs lane does not copy, crop, edit,
re-encode, import, or ship either file.

| Role | Local source filename | Pixel dimensions / format | SHA-256 |
|---|---|---|---|
| **Primary implementation reference** | `Gemini_Generated_Image_seqsafseqsafseqs.png` | 1536 × 2752, PNG RGBA | `5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a` |
| **Secondary mood/material reference** | `ChatGPT Image Aug 9, 2026, 02_34_08 AM.png` | 941 × 1672, PNG RGB | `128bb34836dc4bf38637dd527c124673c0f165e8b4b24950602862fe74e30381` |

A hash mismatch means the file is a different reference and requires a new note. Filenames are labels,
not provenance or rights evidence. Before any pixels or derived texture/mesh enter a shipping artifact,
the owning art contract must record the source, permitted use, transformations, attribution/notice needs,
and human approval. Rebuilding the visual language with original project assets is the expected path.

## 2. What each render contributes

### Gemini render — primary implementation reference

Use it for the composition and shape-language target:

- a tactile wooden tabletop/diorama base rather than a floating digital board;
- chunky cream rail/board pieces with ink-navy edging and clear physical bevels;
- toy-like depot shed and station platforms integrated into the route model;
- a large teal/orange lever that reads as the switch interaction affordance before any label;
- rounded, low-poly/chibi cats whose line identity is visible at gameplay scale; and
- restrained, friendly UI chrome that leaves the board as the visual hero.

It is not a pixel-perfect layout contract. Camera crop, path geometry, station count, cat count, text,
icons, and UI placement follow the playable scene and product spec, not the render.

### ChatGPT render — secondary mood/material reference

Use it for qualitative lighting/material cues:

- cozy desk-scale warmth, softened contact shadows, and a hand-built model-railway feeling;
- wood grain and painted-toy material contrast;
- warm key light with cooler ambient separation; and
- enough surface imperfection to feel tactile without becoming noisy at phone scale.

Do not use its realism, camera composition, object inventory, track topology, typography, or unspecced
colors as implementation requirements. It loses whenever it conflicts with the Gemini composition or
the product specification.

## 3. Authoritative palette and redundancy

These values are copied from `product_spec.md` only to make concept review deterministic; that spec
remains the source of truth.

| Role | Name | Hex |
|---|---|---|
| Board/table base | Cream Card | `#F2EAD9` |
| Paper highlight / UI panels | Warm Paper | `#FAF6EC` |
| Primary dark / outlines / night UI | Ink Navy | `#22304A` |
| Deep shadow / night sky | Depot Navy | `#131C30` |
| Accent / success / water | Metro Teal | `#3BAFA8` |
| CTA / soft warning | Ticket Orange | `#F08A3C` |
| RED line | Signal Red | `#E15A47` |
| BLUE line | Harbor Blue | `#3E7CC9` |
| YELLOW line | Tabby Yellow | `#EFC13D` |
| GREEN line | Garden Green | `#4FA36A` |
| WILD | Catnip Violet | `#A06BD8` |
| Failure / overflow | Alarm Coral | `#D93A2B` |

Line color never appears alone:

| Line | Required symbol | Required base cat silhouette |
|---|---|---|
| RED | `●` | round-eared tabby |
| BLUE | `■` | slim siamese |
| YELLOW | `▲` | fluffy longhair |
| GREEN | `◆` | sleek shorthair |
| WILD | `★` | scruffy alley cat with bent ear |

Themes and paid cosmetics may recolor secondary surfaces, clothing, train paint, particles, and ambient
lighting. They may not recolor away, cover, reshape, or animate ambiguously any required line symbol,
base silhouette, destination indicator, switch state, track connection, queue occupancy, wave preview,
hazard, success, or failure signal.

## 4. Monetization-art contract

The deep catalog proposed in `monetization_spec.md` §8 must look desirable without selling clarity:

- **Cat skins:** overlay material/accessory slots only. Review the five destination silhouettes naked
  and with every skin; the routing-readable silhouette and symbol must remain recognizable at 64 px.
- **Train liveries:** paint/decal/emissive slots on the train only. They cannot resemble line-routing
  colors in a way that makes the train look like a destination signal, and cannot copy the Founder,
  Cup participation, or Cup gold-trim reward language.
- **Seasonal themes:** board, train, particles, props, sky/lighting LUT, and UI ornament may change;
  track topology and every functional color-plus-symbol channel remain unchanged. “Seasonal” does not
  license holiday countdowns or ownership expiry.
- **DLC districts:** Night Harbor establishes optional 10-level side-content scope. Paid district art
  may be richer, but its interaction readability, performance budget, accessibility, and tutorial
  assumptions are identical to free districts.
- **Trials/previews:** the preview is the real signed asset under the same camera and accessibility
  rules, never a concept image that over-promises the purchasable result.

No cosmetic receives a larger hit target, clearer gameplay cue, reduced visual obstruction, faster
animation, lower particle load, or other competitive/performance advantage. If a paid treatment is
more legible than the default, improve the default rather than selling the fix.

## 5. Required visual evidence for later art/catalog work

Each implemented theme/skin/livery supplies, under its owning task contract:

1. the same golden-frame board with all five lines/symbols/cat silhouettes at the gameplay camera;
2. default versus cosmetic captures on the 720p/low-tier floor and the Pixel target;
3. deutan, protan, tritan, grayscale, and bright-ambient/sunlight review frames;
4. cat-silhouette crops at 64 px, plus switch/queue/wave-preview crops at actual HUD size;
5. a profiler capture against the same board with the default look (cosmetics keep the same performance
   budget); and
6. purchase-preview versus owned/equipped frames proving the preview matches the delivered item.

The human applies the existing TG visual rubric to rendered frames. This note cannot declare a visual
pass, and its two source images are not evidence that an implemented frame matches the target.

## 6. Lane boundary

This note owns reference interpretation only. It authorizes no edits to scenes, ProjectSettings,
URP/lighting assets, board/camera code, prefabs, textures, materials, models, fonts, or generated art.
Those paths remain with their parallel-lane owners. The monetization-docs lane adds no art dependency
and no shipping bytes.

The hard monetization tripwire also applies to art wired through a purchasable or rewarded unlock: a
human-authored commit must set `state/mode` to `production` before any billing, IAP, ad, payment, reward-
grant, or equivalent monetization code merges. Producing or reviewing neutral art does not bypass that
gate.
