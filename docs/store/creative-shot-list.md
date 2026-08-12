# Cat Metro creative shot list

Status: production brief only. No raster asset is approved by this document.

## Truth and reference rule

All captures must come from the real `Game` scene on one named, merged candidate revision. The
committed golden image at
`art/diorama-pass:evals/results/ux/art-diorama-2026-08-09/gemini-tabletop-golden.png`
(`5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a`) is a
style target only. It is not gameplay evidence and must never be inserted, composited, or described
as a frame from the app. In particular, do not copy its cat-preview header, trophy count, group
count, track topology, or other invented HUD and scene details.

The current art-lane Pixel frames are a rejected before-state. They are not eligible source images.
Final production waits for a newly built APK and newly captured frames after the camera, commuter
scale, desk color, lighting, and preview corrections.

## Icon — exact 1024 × 1024 master

Use the character-led “Conductor” direction:

- Export one opaque sRGB PNG at exactly **1024 × 1024 px**. No text, number, badge copy, award
  treatment, transparency-dependent edge, or drop shadow.
- Center a front-facing Cream Card (`#F2EAD9`) cat face in a simple Ink Navy (`#22304A`)
  conductor cap. Use a small Metro Teal (`#3BAFA8`) cap badge over a flat Ticket Orange
  (`#F08A3C`) roundel. The roundel must not contain a bar, wordmark, or geometry associated with a
  real transit authority.
- Key the material and warmth to the golden frame: rounded miniature forms, matte cream/navy
  surfaces, restrained warm top-left light, and a clean silhouette. Do not reproduce the golden
  frame's HUD or make the icon a crop of the reference.
- Preserve exactly two value steps at small sizes: the cream face and navy cap/features do the
  recognition work. Ticket Orange is the only large accent; confine the teal badge to no more than
  2% of the canvas so it does not create a third competing value step.
- Keep the eyes, muzzle, cap crown, and teal badge inside the centered **512 × 512 safe-crop
  square**. Ears may break the orange roundel but must remain inside the central 80% of the master.
- Adult-premium, not child-directed: normal feline proportions, calm expression, no baby
  head-to-body ratio, blush, drool, primary-color burst, or toy-store gloss.
- Inspect native exports at 512, 192, 96, and **48 px**; do not resize a smaller source upward.
  Pass grayscale plus deutan, protan, and tritan simulations. At 48 px, both ears, both eyes, the
  muzzle, and the cap silhouette must remain distinct without relying on color.

Icon evidence gate: record the source revision and SHA-256 of the 1024 px PNG; attach the centered
512-safe crop and the 512/192/96/48 contact sheet; record grayscale and three color-vision
simulations; obtain the human taste/legal check; and clear G-13PLUS below with a second reviewer.
Until all are present, label the icon `BLOCKED — BRIEF ONLY`.

## Screenshot master specification

Every row below produces a separate **1179 × 2556 px** opaque sRGB Play PNG from a real raw scene
capture at that same 1179 × 2556 resolution. Never upscale, stretch, redraw, or synthesize gameplay.
Output is frameless: no phone bezel, status bar, navigation bar, notification, store badge,
watermark, developer console, FPS counter, gizmo, or third-party mark.

Use the reviewed production portrait camera with no per-shot pan, zoom, or rotation. It must read
as the approved low three-quarter tabletop view while preserving the shipped board fit. Do not
override the integrated art-lane camera with an older numeric-angle assumption from the product
plan. If the production camera cannot produce the approved read, the art correction remains
blocked; a marketing-only camera is not a fallback.

The final look must retain all of these golden-frame cues:

- visible warm wood grain and a real desk/base-board edge;
- Cream Card/Warm Paper board surfaces with Ink Navy rails and edge detail;
- Metro Teal as the switch body and Ticket Orange restricted to a small lever/CTA accent;
- track-scale cat commuters seated inside open cream cars, with color plus the matching
  circle/square symbol visible;
- soft authored contact shadows, warm key from upper-left, cooler ambient fill, and no orange wash;
- quiet desk-margin props outside the board safe rect, together occupying no more than 6% of frame.

Build the Play export as an editorial composition, never as fake HUD. Reserve the top **562 px**
(22% of 2556, rounded to a whole pixel) in Warm Paper `#FAF6EC`; set Ink Navy copy inside
`x=96…1083, y=112…450`. Keep each caption to eight words or fewer. Uniformly downsample the complete
raw capture by exactly 7/9 into the aspect-ratio-identical **917 × 1988 px** inset at
`x=131…1047, y=565…2552`; fill the surrounding fields with Warm Paper. This keeps the runtime's
screen-relative preview at the top of the intact gameplay inset, below the caption panel. The editor
may not crop, cover, move, redraw, or replace the board or shipped HUD. Preserve the exact raw
capture separately; if the downsampled mechanic or HUD fails the 20% contact-sheet read, block the
row.

### Evidence gates

- **G-CONTENT — VERIFIED at freeze:** authored/staged L001–L010 exist in the frozen tree and pass
  the repository's content-validation and solver gates. This gate does not prove player
  reachability; the frozen normal-progression band is L001–L005.
- **G-BUILD — BLOCKED:** record one merged candidate commit, package
  `com.catmetro.game`, APK SHA-256, and successful check/test/build results. Do not source a
  player-facing asset from an unmerged art or UI branch.
- **G-ART — BLOCKED:** newly captured Pixel 9 Pro frames from that APK must pass the human
  tabletop-composition review. The rejected top-down, oversized-cat, orange-heavy baseline cannot
  satisfy this gate.
- **G-STATE — BLOCKED per row:** reach the named state through live simulation and player input in
  the real `Game` scene; retain a short before/after clip or state log. No posed mockup, reference
  composite, relocated HUD, or hand-placed commuter.
- **G-REACH — BLOCKED for any out-of-band level:** the named merged candidate exposes the level
  through ordinary player progression or released navigation. A development-build override,
  hidden command, or Editor-only load does not satisfy this gate. At the frozen anchor, L006–L010
  fail G-REACH, so their named fallback is mandatory and the fallback drops G-REACH.
- **G-QUEUE — BLOCKED:** an on-device Playing frame from the exact candidate shows an occupied queue
  as distinct commuters rather than overlapping primitives, with the associated route decision
  readable and no development aid. This gate applies only to the preferred L004/L005 take; if it
  fails, S4 takes the named L002 preview fallback and drops G-QUEUE.
- **G-CLEAN — BLOCKED:** the source run has no development overlay, notification, crash, or relevant
  Unity/AndroidRuntime error. In particular, do not capture while the recorded WavePreviewStrip
  primitive/collider error remains.
- **G-FORMAT — BLOCKED per export:** probes report an exact 1179 × 2556 raw source and final PNG;
  visual inspection confirms frameless output and no prohibited mark. For Play exports, it also
  confirms the 917 × 1988 proportional gameplay inset, safe caption region, and unobscured shipped
  HUD; for D0, it confirms that no editorial layer is present. Record raw and final PNG SHA-256.
- **G-A11Y — BLOCKED:** bind fresh grayscale/deutan/protan/tritan renders to the exact shipped
  assets/candidate; superseded pre-correction sheets are inputs only. Then run CM-R21's protocol
  separately for (a) color-removed, in-game-size markers retaining symbol+silhouette and (b) 64 px
  silhouette-only renders with color and symbols removed. Each run uses five non-author raters, 25
  total trials, randomized one-at-a-time presentation, no prompting or legend, and no answer list
  beyond the five line names; each must score at least 23/25 pooled correct. Bind signed/dated raw
  results and any asset-id/re-topology/cut/remedy decision to the candidate. A missing run, lower
  score without the required dated remedy record, or stale asset blocks every row.
- **G-13PLUS — BLOCKED:** do not invent the missing rubric in this lane. Before the icon or any
  screenshot can ship, the committed `docs/prd/art-review-rubric.md` must exist; Play target audience
  must declare 13+ only with no under-13 group; and a second reviewer must mark every rubric row
  present/absent with a one-line justification, sign and date the artifact, and record zero rows
  present. Any present or incomplete row blocks the complete asset set.
- **G-SEAM — VERIFIED for the frozen baseline; re-capture required:** L001 and L002 were won on a
  Pixel 9 Pro through the real Win → Next → next-level seam. The final still must come from the
  named candidate, not the old device frame.

## Six-shot sequence

| # | Master | Scene | Authored level | Game state | HUD state | Safe text region | Camera / composition | Action / capture moment | Caption | Fallback | Evidence gate |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **S1 — hero** | 1179 × 2556 frameless PNG | `Game` | **L006 — Alternating Line** | `Playing`; first red pair has cleared, first blue pair approaches J1, later red/blue waves remain | Real Playing chrome only; next-wave preview present and accurate; retry, results, hint, and dev overlays hidden | Top editorial panel; safe box `x=96…1083, y=112…450`; intact gameplay begins at y=565 | Reviewed production camera; full board readable; warm desk edge on bottom/right; J1 in lower-center; both symbol stations and open cars visible; props ≤6% | Capture the visible S1 lever throw toward the blue route, with a blue square commuter in motion and no touch marker over the switch | **Tap the switch. Send every cat home.** | L005 — Evening Rush, then L002 — Colour Split; preserve the caption and verb. Never add Harbor scenery or a second mechanic. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS; add G-REACH only for L006 |
| **S2 — readable routing** | 1179 × 2556 frameless PNG | `Game` | **L002 — Colour Split** | `Playing`; red delivery completed and a blue commuter reaches its matching station | Real Playing chrome; current preview strip only; no results/failure layer | Same editorial panel and safe box; complete shipped HUD remains inside the inset | Production camera unchanged; keep both red-circle and blue-square signage in frame; blue commuter and square tag must separate at thumbnail size | Capture the blue square car crossing the station threshold while the red circle route remains visible for comparison | **Color plus symbol. Match every route.** | Recapture an earlier or later L002 delivery; if no clean moment exists, block S2. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS |
| **S3 — preview and timing** | 1179 × 2556 frameless PNG | `Game` | **L006 — Alternating Line** | `Playing`; first red wave consumed, blue wave approaching, following red wave still pending | Real next-wave preview shows the actual next two entries; no invented count, score, or purr meter | Same editorial panel and safe box; preview remains at the top of the intact gameplay inset | Production camera unchanged; J1 and the approaching line occupy the central vertical read; preview, switch arm, and incoming car form one eye path | Capture just before the timing tap, with the current route visibly wrong for the approaching blue car | **Read the next wave. Time one tap.** | Use early L002 Playing before the first red pair reaches the switch: real red+blue preview visible, initial route wrong for red, capture immediately before the red-routing tap; keep the caption. If the preview cannot remain clear, block S3. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS; add G-REACH only for L006 |
| **S4 — queue pressure** | 1179 × 2556 frameless PNG | `Game` | **L004 — Platform Queue** | `Playing`; a real queue is visibly occupied while a routed commuter clears the junction | Real Playing chrome and preview only; no failure, result, hint, dev, or fabricated queue-count layer | Same editorial panel and safe box; queue, switch, and preview remain intact in the inset | Production camera unchanged; keep the occupied queue, outgoing route, junction, and matching station in one readable path | Capture the last stable frame before the queued commuter advances; do not force overflow or route a cat to a non-matching station | **Watch the queue. Time the switch.** | First try the same occupied-queue Playing state on L005. If G-QUEUE fails, use L002 Playing after the red delivery and immediately before the blue timing tap: the real single blue preview entry and standard Playing chrome, both station signs and the switch in frame, no queue claim; caption becomes **Read the wave. Time the switch.** | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS; add G-QUEUE only for L004/L005 |
| **S5 — wordless first lesson** | 1179 × 2556 frameless PNG | `Game` | **L001 — First Switch** | Initial `Playing` state before the first input; two red commuters approach the single junction | Real teach pulse/ring and real preview state; no hint chip unless the live attempt rules display it | Same editorial panel and safe box; pulse, switch, commuters, and preview remain intact | Production camera unchanged; single junction near center, two stations readable, desk edge visible; keep the composition intentionally spare | Capture the strongest pulse frame immediately before the first player tap | **One switch. One tap. Learn by playing.** | L002 — Colour Split, first approach; fallback caption becomes **One thumb. One tap. Learn by playing.** | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS |
| **S6 — complete loop** | 1179 × 2556 frameless PNG | `Game` | **L002 — Colour Split** | `Won` / Results after a real clear, before Next is tapped | Localized **All cats home!** result and exactly one primary **Next** CTA; footer stays as shipped and contains no planned commerce | Same editorial panel and safe box; complete Results treatment remains intact in the inset | Production camera unchanged behind the real result treatment; enough board remains visible to connect the result to gameplay | Capture the stable result frame, then retain a companion clip showing the same Next tap load L003 for provenance | **Win. Tap Next. Keep the line moving.** | Use the verified L001 Won frame and companion L001 → L002 transition; keep the caption. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS + G-SEAM |

FailureReview/cause/retry is deliberately absent from the six-shot sequence at freeze. Its code and
synthetic T904 fixture do not prove a reproducible authored-level player path. Reconsider it only
after an exact candidate supplies a clean, normal-progression, on-device failure → cause → retry
receipt; never induce the pinned NEW-Q4 halt and label it as a game failure.

### D0 — raw Devpost hero export

Use the same live L006 moment, scene, state, HUD, composition, action, fallback order, and evidence
gates as S1, but make D0 a separate exact **1179 × 2556** export directly from the real scene. D0
contains the shipped HUD and **no marketing caption, Warm Paper caption band, or other editorial
overlay**. It is the preferred Shipaton frameless hero; it must not be a crop of a 1080-wide video
frame. Preserve the raw source receipt so the art lane can derive captioned S1 and uncaptioned D0
from the same evidenced run without treating one exported asset as the other.

## Final contact-sheet audit

Before release, inspect all six at full size and at a 20% thumbnail:

1. The mechanic is legible without reading the caption.
2. Every commuter is track-scale and seated in an open car.
3. Color is always paired with the real circle/square symbol used by the captured level.
4. The warm desk edge, cream board, Ink Navy rail detail, restrained orange, teal switch, and soft
   shadows read consistently across the sequence.
5. No frame contains invented HUD, a device shell, a public-availability claim, a planned feature,
   or the golden reference itself.
6. Each row has its own commit, state/capture receipt, dimensions probe, and SHA-256. A missing
   receipt blocks that row rather than triggering a mockup.

## Sources

- `docs/plan/marketing/STORE-PACK-frozen-contract.md`
- `docs/plan/specs/growth_aso_plan.md` §§4–7 and §23
- `docs/plan/specs/product_spec.md` §§7–12 and §22
- `docs/prd/PRD.md` CM-R50.3
- `art/diorama-pass:evals/results/ux/art-diorama-2026-08-09/EVIDENCE.md`
