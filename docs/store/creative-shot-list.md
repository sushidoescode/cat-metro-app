# Cat Metro creative shot list

Status: production brief only. No raster asset is approved by this document.

## Truth and reference rule

All captures must come from the real `Game` scene on one named, merged candidate revision, either
from the target Pixel 9 Pro or from the checked-in capture rig rendering that exact candidate. No
generative-image output, synthetic or redrawn gameplay, posed mockup, hand-composited commuter, or
invented HUD is eligible. Editorial copy may surround an intact real capture; it may not replace,
retouch, or fabricate gameplay pixels. The
committed golden image at
`art/diorama-pass:evals/results/ux/art-diorama-2026-08-09/gemini-tabletop-golden.png`
(`5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a`) is a
style target only. It is not gameplay evidence and must never be inserted, composited, or described
as a frame from the app. In particular, do not copy its cat-preview header, trophy count, group
count, track topology, or other invented HUD and scene details.

The current art-lane Pixel frames are a rejected before-state. They are not eligible source images.
Final production waits for the exact release candidate and newly captured frames after the camera, commuter
scale, desk color, lighting, and preview corrections.

`docs/plan/specs/submission_script.md` is future-state/non-source planning. Its screenshot directions,
feature claims, dimensions, and “paste-ready” labels prove nothing exists and cannot approve a raster.

## Icon — exact 1024 × 1024 master

Use the character-led “Conductor” direction:

- Create one opaque sRGB master at exactly **1024 × 1024 px**. Export the Google Play icon at
  **512 × 512 px** as a 32-bit PNG with alpha, no more than 1024 KB; export the separate Devpost icon
  at the master's native **1024 × 1024 px**. No text, number, badge copy, award
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

## Raster output specification

| Surface | Exact output | Content rule |
|---|---|---|
| Google Play icon | **512 × 512 px**, 32-bit PNG with alpha, ≤1024 KB | Export from the approved 1024 px master |
| Google Play feature graphic | **1024 × 500 px**, JPEG or 24-bit PNG, no alpha | Wordmark-and-track composition; if gameplay appears, use only an intact exact-candidate capture |
| Google Play phone screenshots | **1080 × 1920 px**, opaque sRGB PNG, at least four | S1–S6 below; valid 9:16 Play assets |
| Devpost icon | **1024 × 1024 px** | Separate native-size export from the same approved master |
| Devpost frameless screenshot | **1179 × 2556 px**, opaque sRGB PNG | D0 below; exact native capture, no device frame or editorial overlay |

Every S1–S6 row produces a separate **1080 × 1920 px** Google Play PNG from a real device or
capture-rig render at that exact resolution. D0 is captured separately at **1179 × 2556 px** for
Devpost; it is not a Play upload. Never upscale, stretch, redraw, synthesize, or use an image model to
create gameplay. Every output is frameless: no phone bezel, status bar, navigation bar, notification,
store badge, watermark, developer console, FPS counter, gizmo, or third-party mark.

Use the reviewed production portrait camera with no per-shot pan, zoom, or rotation. It must read
as the approved low three-quarter tabletop view while preserving the shipped board fit. Do not
override the integrated art-lane camera with an older numeric-angle assumption from the product
plan. If the production camera cannot produce the approved read, the art correction remains
blocked; a marketing-only camera is not a fallback.

The final look must retain all of these golden-frame cues:

- visible warm wood grain and a real desk/base-board edge;
- Cream Card/Warm Paper board surfaces with Ink Navy rails and edge detail;
- Metro Teal as the switch body and Ticket Orange restricted to a small lever/CTA accent;
- track-scale colored cat commuters seated inside open cream cars, with the matching station's
  circle/square plaque visible in the same route read;
- soft authored contact shadows, warm key from upper-left, cooler ambient fill, and no orange wash;
- quiet desk-margin props outside the board safe rect, together occupying no more than 6% of frame.

Build each Play export as an editorial composition, never as fake HUD. Keep each caption to eight
words or fewer in a consistent Warm Paper `#FAF6EC` band outside the intact gameplay frame. The
editor may uniformly scale the complete real capture to fit, but may not crop, cover, move, redraw,
retouch, or replace the board or shipped HUD. Preserve the exact raw capture separately; if the
mechanic or HUD fails the 20% contact-sheet read at 1080 × 1920, block the row. D0 carries no caption
band or other editorial layer.

### Evidence gates

- **G-CONTENT — BUILD-DERIVED:** bind the generated listing receipt to the exact AAB SHA-256. The
  receipt derives `GameRoot.LevelBand`, verifies each named JSON exists in the bundle, and supplies
  the only publishable campaign quantity. The production cut remains blocked until TASK 15 lands on
  `main`; afterward, nineteen is still only an expectation until the exact-AAB receipt reports it.
- **G-BUILD — BLOCKED:** record one merged candidate commit, package `com.catmetro.game`, AAB
  SHA-256, installed binary identity, and successful check/test/build results. Do not source a
  player-facing asset from an unmerged art, UI, or level branch.
- **G-ART — BLOCKED:** newly captured Pixel 9 Pro or checked-in capture-rig frames from that exact
  candidate must pass the human
  tabletop-composition review. The rejected top-down, oversized-cat, orange-heavy baseline cannot
  satisfy this gate.
- **G-STATE — BLOCKED per row:** reach the named state through live simulation and player input in
  the real `Game` scene; retain a short before/after clip or state log. No generated image, posed
  mockup, reference composite, relocated HUD, or hand-placed commuter.
- **G-REACH — BLOCKED for any out-of-band level:** the named merged candidate exposes the level
  through ordinary player progression or released navigation. A development-build override,
  hidden command, or Editor-only load does not satisfy this gate. Read the band from the exact-AAB
  receipt; never decide reachability from a hand-maintained range in this brief.
- **G-QUEUE — BLOCKED:** an on-device Playing frame from the exact candidate shows an occupied queue
  as distinct commuters rather than overlapping primitives, with the associated route decision
  readable and no development aid. This gate applies only to the preferred L004/L005 take; if it
  fails, S4 takes the named L002 preview fallback and drops G-QUEUE.
- **G-CLEAN — BLOCKED:** the source run has no development overlay, notification, crash, or relevant
  Unity/AndroidRuntime error. In particular, do not capture while the recorded WavePreviewStrip
  primitive/collider error remains.
- **G-FORMAT — BLOCKED per export:** probes report exact **1080 × 1920** S1–S6 Play PNGs and an
  exact **1179 × 2556** D0 Devpost PNG; visual inspection confirms frameless output, intact real
  gameplay, and no prohibited mark. For Play exports, confirm the safe caption region and
  unobscured shipped HUD; for D0, confirm no editorial layer is present. Record raw and final PNG
  SHA-256.
- **G-A11Y — BLOCKED:** inspect fresh grayscale/deutan/protan/tritan renders from the exact candidate
  at full size and 20% scale. Color-and-symbol identity, switch state, commuter, route, and caption
  must remain readable. Old comparison sheets do not clear this gate.
- **G-13PLUS — BLOCKED:** the human reviews the complete icon, feature graphic, captions, description,
  and screenshots together before upload. Keep the puzzle and track legible, remove child-coded
  language, select 13+ brackets only, and do not use a cat-face-only screenshot sequence.
- **G-SEAM — RECAPTURE REQUIRED:** the Win → Next → next-level seam has run on the Pixel, but the
  final still and clip must come from the named release candidate, not an old device frame.

## Six-shot sequence

| # | Master | Scene | Authored level | Game state | HUD state | Safe text region | Camera / composition | Action / capture moment | Caption | Fallback | Evidence gate |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **S1 — hero** | 1080 × 1920 Play PNG | `Game` | **L006 — Alternating Line** | `Playing`; first red pair has cleared, first blue pair approaches J1, later red/blue waves remain | Real Playing chrome only; next-wave preview present and accurate; retry, results, hint, and dev overlays hidden | Consistent top editorial band; intact gameplay and shipped HUD below it | Reviewed production camera; full board readable; warm desk edge on bottom/right; J1 in lower-center; both symbol stations and open cars visible; props ≤6% | Capture the visible S1 lever throw toward the blue route, with a blue commuter moving toward the square-marked station and no touch marker over the switch | **Tap the switch. Send every cat home.** | L005 — Evening Rush, then L002 — Colour Split; preserve the caption and verb. Never add Harbor scenery or a second mechanic. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS; add G-REACH only for L006 |
| **S2 — readable routing** | 1080 × 1920 Play PNG | `Game` | **L002 — Colour Split** | `Playing`; red delivery completed and a blue commuter reaches its matching station | Real Playing chrome; current preview strip only; no results/failure layer | Same editorial band; complete shipped HUD remains intact | Production camera unchanged; keep both red-circle and blue-square station signs in frame; the blue commuter and square station plaque must remain distinct at thumbnail size | Capture the blue commuter crossing the square-marked station threshold while the red-circle route remains visible for comparison | **Color plus symbol. Match every route.** | Recapture an earlier or later L002 delivery; if no clean moment exists, block S2. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS |
| **S3 — preview and timing** | 1080 × 1920 Play PNG | `Game` | **L006 — Alternating Line** | `Playing`; first red wave consumed, blue wave approaching, following red wave still pending | Real next-wave preview shows the actual next two entries; no invented count, score, or purr meter | Same editorial band; preview and shipped HUD remain intact | Production camera unchanged; J1 and the approaching line occupy the central vertical read; preview, switch arm, and incoming car form one eye path | Capture just before the timing tap, with the current route visibly wrong for the approaching blue car | **Read the next wave. Time one tap.** | Use early L002 Playing before the first red pair reaches the switch: real red+blue preview visible, initial route wrong for red, capture immediately before the red-routing tap; keep the caption. If the preview cannot remain clear, block S3. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS; add G-REACH only for L006 |
| **S4 — queue pressure** | 1080 × 1920 Play PNG | `Game` | **L004 — Platform Queue** | `Playing`; a real queue is visibly occupied while a routed commuter clears the junction | Real Playing chrome and preview only; no failure, result, hint, dev, or fabricated queue-count layer | Same editorial band; queue, switch, preview, and HUD remain intact | Production camera unchanged; keep the occupied queue, outgoing route, junction, and matching station in one readable path | Capture the last stable frame before the queued commuter advances; do not force overflow or route a cat to a non-matching station | **Watch the queue. Time the switch.** | First try the same occupied-queue Playing state on L005. If G-QUEUE fails, use L002 Playing after the red delivery and immediately before the blue timing tap: the real single blue preview entry and standard Playing chrome, both station signs and the switch in frame, no queue claim; caption becomes **Read the wave. Time the switch.** | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS; add G-QUEUE only for L004/L005 |
| **S5 — first route** | 1080 × 1920 Play PNG | `Game` | **L001 — First Switch** | Initial `Playing` state before the first input; two red commuters approach the single junction | Real teach pulse/ring and real preview state; no hint chip unless the live attempt rules display it | Same editorial band; pulse, switch, commuters, preview, and HUD remain intact | Production camera unchanged; single junction near center, two stations readable, desk edge visible; keep the composition intentionally spare | Capture the strongest pulse frame immediately before the first player tap | **One switch. One tap. Read the route.** | L002 — Colour Split, first approach; fallback caption becomes **One thumb. One tap. Read the route.** | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS |
| **S6 — complete loop** | 1080 × 1920 Play PNG | `Game` | **L002 — Colour Split** | `Won` / Results after a real clear, before Next is tapped | Localized **All cats home!** result and exactly one primary **Next** CTA; footer stays as shipped and contains no planned commerce | Same editorial band; complete Results treatment and shipped HUD remain intact | Production camera unchanged behind the real result treatment; enough board remains visible to connect the result to gameplay | Capture the stable result frame, then retain a companion clip showing the same Next tap load L003 for provenance | **Win. Tap Next. Keep the line moving.** | Use the verified L001 Won frame and companion L001 → L002 transition; keep the caption. | G-CONTENT + G-BUILD + G-ART + G-STATE + G-CLEAN + G-FORMAT + G-A11Y + G-13PLUS + G-SEAM |

FailureReview/cause/retry is deliberately absent from the current six-shot sequence. Its code and
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

## Shipaton video — real public-candidate footage, under two minutes

The submission video is the primary judging surface. Target **1:45–1:55** at 1920×1080 and never
exceed two minutes. Every visual frame must be real footage or a real capture from the exact public
candidate. Concept, reference, generated, redrawn, or fabricated art is excluded from the video,
even if labeled.

1. **0:00–0:10 — hook:** a clean real switch throw that saves a cat route; show the title once.
2. **0:10–0:40 — explain by play:** junction tap, color-plus-symbol station, next-wave preview, and
   a successful delivery. Keep cursor/touch marks out unless they aid comprehension.
3. **0:40–1:05 — depth:** two visibly different real levels and a real Win → Next transition. State
   the campaign count only if copied from the exact AAB receipt.
4. **1:05–1:35 — RevenueCat proof:** in the real app, show the named product, licensed/sandbox
   purchase, entitlement unlock, and restart/restore behavior; an in-app product identifier may be
   visible if the shipped UI actually displays it. Keep RevenueCat dashboard receipts as private
   evidence outside the submitted video. Do not claim rewarded ads unless that shipped path is the
   qualifier.
5. **1:35–1:50 — close:** return to the strongest routing moment, show the public store name/URL,
   and end. The app must already be public before the submission is called complete.

Use direct cuts, readable captions, and game audio/music the human has cleared for distribution.
Delete any planned-feature voice-over from `submission_script.md` unless the public candidate and a
real capture independently prove it. Preserve the final video hash and source-capture hashes.

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

- `docs/LOOK.md` and `docs/reference/` — visual direction only.
- `docs/store/play-store-listing.md` — exact-candidate copy template.
- `docs/plan/marketing/claim-ledger.md` — claim status and evidence rules.
- `docs/plan/specs/submission_script.md` — future-state shot ideas only, never evidence or copy.
