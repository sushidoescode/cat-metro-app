# Generated reference images (2026-08-31)

AI-generated target references (Gemini/ChatGPT, human-curated), extending `docs/LOOK.md`.
Primary file per subject; `-alt` is the runner-up kept for its own strengths.

| File | Use it for |
|---|---|
| `gen-ref-menu.png` | Home/menu identity: carved navy title sign, stitched cream buttons with icons, lamp-lit diorama behind |
| `gen-ref-board-framing.jpeg` | **TASK 7 framing target**: frontal camera, board fills the portrait frame, cat heads readable in carriages |
| `gen-ref-wardrobe.png` | Wardrobe layout: big cat portrait on a stand, item cards with price chips, Equip/Shop buttons |
| `gen-ref-cats-on-train.jpeg` | Cat-on-carriage **scale**: heads sit well above carriage walls; faceted style matches our real assets |

## Take the look, NOT these details

- **No coin/gem currency.** Both wardrobe refs show a coin balance — Cat Metro has no
  virtual currency (settled: real-money named items + earned unlocks). Prices are
  store-localized real prices.
- **No text station signs, no Score/Moves HUD.** `gen-ref-board-framing.jpeg` shows
  "RED CAT STATION" signs and a Level/Score/Moves bar — our vocabulary is shape badges
  and the wave capsule. Copy the framing, fill, and scale only.
- Palette and materials law stays `docs/LOOK.md`: navy, cream, warm wood, teal, tomato
  red, marigold; wooden-toy matte, warm late-afternoon light.


# Round 2 references (2026-09-06) — curated by the orchestrator from the human's generations

Primary files are ChatGPT outputs from `gen-ref-PROMPTS-2026-09-06.md`; `-alt` files are the
Gemini runs kept for the specific detail named below. Lanes take the LOOK, never the text.

| File | Lane | Take | Do NOT copy |
|---|---|---|---|
| `gen-ref-v2-board.png` | A, B | The board fills the portrait edge to edge and nearly full height; cat head+ears ≈ 1/5 of frame width, clearly above the carriage wall; station = platform + coloured roof + big white SHAPE badge (circle, square), no letters; thin navy rails on cream sleepers; props at the margins, not on the route; warm mid-tone wood, dark desk only as a sliver | Its camera is more top-down than our 38° pitch — keep our tilt, take the fill and scale |
| `gen-ref-v2-home.png` | C | Carved navy sign with four brass nails and a cream cat-face mark, two-line cream letters in a chunky rounded face; cream-framed diorama window with rounded corners; the conductor cat in a small gilt frame INSIDE the window; three centred stitched cream pills with navy glyph + label; lamp glow top-left, plant, books, cup and pencil at the edges, soft vignette | The book titles and any decorative text; the framed portrait must be the LICENSED rig, not a painting |
| `gen-ref-v2-flow.png` | D | Intro: a cream ticket with a perforated edge over the dimmed board, level name large, one-line goal, a red circle + lever glyph as the "what you'll meet" icons, dotted tear line, stitched Play pin on the ticket. Win: carved navy plaque "All cats home!" above the board, cream/orange confetti burst over the station, the cat mid-hop, stitched Next pin at the bottom band | "STATION" text sign on the roof (never text on stations) |
| `gen-ref-v2-flow-alt.jpeg` | D | The ticket lying at a slight angle with a torn stub; the big navy plaque bevel; confetti density | Phone bezel/notch, teal building, "STATION" sign |
| `gen-ref-v2-wardrobe.png` | F | The licensed cat on a round wooden plinth with a brass nameplate ("Conductor's Coat"); lamp from top-left; three stitched cream cards with the item drawn large, name, navy price chip with a REAL localized price; the selected card outlined in teal; one big stitched red Equip pin; "Restore purchases" as an underlined text link; back arrow top-left | Coins/gems (none here — good); prices are whatever the store returns, not $1.99 literal |
| `gen-ref-v2-wardrobe-alt.jpeg` | F | Standing pose on the plinth; dashed-stitch card borders; varied prices ($0.99/$1.49) proving the chip handles widths | Phone bezel, status bar, "TOYSHOP" |
| `gen-ref-v2-props.png` | A, B | Station platform = base + two posts + roof (red/blue/yellow/green) + a white round badge on a post with the SHAPE (circle/square/triangle/diamond) in the roof colour; depot shed navy with orange doors; lever left/right on a teal base; buffer stop for sidings; lamp post, signpost, pine, round tree, bush, cup, pencil — all matte faceted wood | The caption text and the "Cat Metro" wordmark on the sheet |
| `gen-ref-v2-props-alt.jpeg` | B | Badge as a rounded square sign with the shape in black on cream — a readable alternative at 20% size; lever OFF/ON silhouettes | Rustic weathering, caption text |
| `gen-ref-v2-moments.png` | B, D | Lever mid-swing with two motion arcs and small tick marks; delivery: three cream hearts pop above the cat, the station badge bounces (motion ticks); train smoke: three round cream puffs, engine leaning; wrong station: the badge shows the cat's own shape with a navy cross-bar, platform recoil ticks, cat frowning | Nothing — this is the VFX spec |
| `gen-ref-v2-moments-alt.jpeg` | B | Wood-chip dust puff on the lever throw; dust cloud on the recoil | "WAYSTATION", "N°3/N°7" text, the red X, weathered paint |

## Design decisions made from these images (orchestrator, 2026-09-06 — not open)

- **Type**: Fredoka (Google Fonts, OFL) for display, plaques, buttons and level names — it is the
  face every primary image uses. Nunito (OFL) for body, captions and prices. Bake TMP atlases
  for both; Fredoka SemiBold is the TMP default so every existing MakeText inherits it.
- **Station badge law**: white disc on a post, shape filled in the roof colour (v2-props), no
  letters anywhere; at 20% size fall back to the props-alt rounded-square silhouette if the disc
  does not read — lane B decides with a 20% capture.
- **Home holder**: the licensed rig in a small gilt frame inside the diorama window (v2-home).
- **Chips**: one stitched cream pill vocabulary everywhere (Home routes, ticket Play, Next,
  Try again, Equip in red); selected cards get a teal outline; Restore is a text link.
- **Wardrobe hero**: sitting pose on the plinth (v2-wardrobe primary), brass nameplate.
- **Daily**: teased before unlock with a locked pin "Unlocks after 7 station wins" and seven
  small pips (lifetime wins are progress, not a streak).
- **Accessory tab**: hidden while its catalog slot is empty.
- **Unity splash**: off (Unity 6 permits it for every tier).
- **Audio sourcing**: commercial-free-use music and SFX (CC0 / royalty-free with the licence
  text saved beside the file) and Gemini-generated audio are both allowed; every clip gets a
  PROVENANCE row (source URL or generator + prompt, licence, SHA-256). The in-repo synth path
  stays as the fallback so the build is never silent.
- **Ladder**: the small content edits in lane A are allowed (route order L010/L011, time limits
  L009–L012); budget levels get the content fix (L014 lever start), not a rules change.
- **MSAA 4x** on, verified for frame time on L041+ during the device pass.
