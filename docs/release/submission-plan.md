# Cat Metro — Shipaton submission plan

**The video and the screenshot are the product.** Everything in this document follows from one
sentence in the official rules, quoted in `docs/research/shipton-hackathon.md` §5:

> "Judges are not required to test the Project and may choose to judge based solely on the text
> description, images, and video provided."

Plus its companion: judges "are not required to watch beyond two minutes."

So a judge may score Cat Metro having never launched it. The ≤2:00 video, the single 1179×2556
screenshot, and the written description are not marketing attached to the game — for judging
purposes they *are* the game. A beautiful build that photographs badly loses to a mediocre build
that photographs well. This document specifies those three artifacts, how to capture them, and
what has to be true in the build before they can be shot.

**Scope of this document:** docs only. It changes no gameplay code and captures nothing. It is a
production brief for the lanes that will.

**Target tracks** (from the research, §4 "Recommended focus"): Best Game ($20k), OneSignal "Keep
Them Coming Back" ($25k), Catvertising ($20k), Design ($20k), HAMM ($20k). Five tracks, one
product, five different emphases of it.

**Deadline:** 2026-09-30 23:45 PDT. From 2026-08-25 that is 36 days, and the store-release gate
(Gate 1) eats most of them.

---

## What this plan inherits, and where it departs

The 2026-08-02 strategy pack already contains a great deal of usable thinking, and this plan mines
it rather than restarting:

| Source | What is reused | What is changed |
|---|---|---|
| `docs/plan/specs/submission_script.md` §5 | The storyboard discipline, the removable-block structure, the "gameplay in frame one" rule, the judge-testing-instructions block | Its runtime budget and beat list describe a much larger game (30 levels, 6 districts, purr meter, rewind sheet, tickets, themes). Re-timed against the product that exists. |
| `docs/plan/marketing/devpost-video-script.md` | The per-beat evidence-gate idea — every shot names what must be true before it can be shot | Its gate machinery is heavier than this project now runs (`AGENTS.md`: no frozen contracts, no staged approval gates). Gates survive here as a plain gap list, §5. |
| `docs/store/creative-shot-list.md` | The **D0 raw hero export** concept — a frameless 1179×2556 straight from the scene, distinct from the captioned Play screenshot — is exactly right and is adopted | Its S1 editorial composition (562px caption band + 917×1988 inset) stays where it belongs: on the Play listing. The Devpost hero is full-bleed. See §2. |
| `docs/plan/marketing/claim-ledger.md` | The **discipline**: no claim in public copy without a receipt in the build; every rate carries a denominator | Its status table is frozen against 2026-08-10 and is now stale in both directions (17 level files exist, not 10; the daily is wired). Re-run it against the release candidate; do not paste from it. |
| `docs/store/play-store-listing.md` | The voice — plain, concrete, no adjective stacking — and the "Fair by design" positioning line | Expanded, §3. |

Two of the pack's habits are deliberately **not** carried forward. It writes about the game as a
list of systems ("five-point price ladder", "deterministic simulation at 8 ticks per second"), and
it hedges every sentence into a compliance artifact. Judges are indie developers and creators, not
auditors. The copy below is written to be read once, at speed, by someone who is tired.

---

## 1. The video

**Hard rules:** ≤2:00, hosted on YouTube or Vimeo, no third-party trademarks, no third-party music.
**Target runtime: 1:45**, which leaves 15 seconds of headroom under a cap that disqualifies.

**Format:** 1920×1080 landscape H.264, portrait device capture centred against warm-paper side
fields, burned captions. It is watched on a laptop by a judge, not on a phone. Do not add a device
bezel — it costs pixels and adds a trademark risk.

### 1.1 The first five seconds, and why

**Verbatim, as it should be cut:**

> **0:00–0:04 — "The object."**
> Full-frame, locked, low three-quarter isometric on the board. Warm key from upper-left, soft
> contact shadows under every piece. A cream-and-navy toy steam engine is already rolling out of
> the depot at the top of the board, two open carriages behind it, a cat sitting in each. A red
> circle pin and a blue square pin float above the two cats. At the bottom-right corner of frame,
> cropped by the edge and softly out of focus, a coffee cup. **The camera does not move.** Nothing
> in the UI animates. The only motion in the frame is the train.
> *On-screen text: none until 0:02, then* `CAT METRO` *small, lower-left, Ink Navy, no card behind it.*
> *VO: silence until 0:02, then —* "This is a wooden train set on a desk."
>
> **0:04–0:10 — "The verb."**
> Same locked frame, **no cut**. The engine reaches the junction. A thumb enters from the
> bottom-right, taps the orange lever on its teal base; the lever tilts, the point rails slide
> across, and the train takes the right-hand branch instead of the left. It runs down to the
> red-roofed platform with the red circle badge. The red-pin cat hops out. The platform stamps.
> *On-screen text:* `TAP THE SWITCH` *at 0:05, replaced by* `MATCH THE PIN` *at 0:08.*
> *VO:* "Tap a switch and the train takes the other branch. Every cat is carrying a sign — get it
> to the platform that matches."

**Why these five seconds and not others.**

Every entry in this competition can *claim* good gameplay in text. Very few can put a physically
plausible hand-built object on screen. That is Cat Metro's only genuinely scarce asset, so it
spends second one — before any sentence lands — answering the question that separates this
submission from the several hundred others that will finish: *was this made with care?*

Three specific choices, each deliberate:

1. **The camera is locked.** A slow push-in is the default opening of every mobile game trailer
   ever cut, and it reads as advertising. A held frame with motion *inside* it reads as an object
   being observed. Stillness is the craft signal, and it costs nothing.
2. **The verb arrives at 0:04, not 0:00.** Four seconds is long enough for the eye to register
   wood, warmth and scale, and short enough that nobody mistakes it for a screensaver. Any longer
   and a judge starts wondering whether there is a game here.
3. **0:00–0:10 is one unbroken take.** No cut between the object and the verb. A cut is where a
   sceptical viewer assumes the mockup was spliced in; an uninterrupted ten seconds of
   input → response is the cheapest possible proof that this is a real running app. This is worth
   more than any "REAL GAMEPLAY" caption, though we burn that caption too at 0:12.

One further reason, specific to this panel: **Scott Cameron of Pok Pok is a judge.** Pok Pok ships
precisely this aesthetic — tactile wooden toys, no dark patterns, craft as the product. A held,
warmly lit, physically plausible toy in frame one is aimed squarely at the single expert eye on the
panel most likely to score us well. If the first four seconds land for him, they land for everyone.

### 1.2 Full shot list

Runtime target 1:45 (105s). Times are cumulative from 0:00.

| # | Time | Dur | On screen | Camera | What the viewer understands | On-screen text | Voiceover |
|---|---|---|---|---|---|---|---|
| **1** | 0:00–0:04 | 4s | The board, full frame. Engine leaving the depot with two cats in open carriages, destination pins floating above them. Coffee cup cropped at bottom-right, defocused. Nothing else moves. | **Locked.** No move at all. | This is a warm, hand-built wooden toy on somebody's desk, and cats ride it. | `CAT METRO` at 0:02, small, lower-left | (silence to 0:02) "This is a wooden train set on a desk." |
| **2** | 0:04–0:10 | 6s | **No cut from shot 1.** Engine reaches the junction; thumb enters from bottom-right and throws the orange lever; points slide; train takes the right branch; red-pin cat arrives at the red-circle platform; platform stamps. | Locked, same frame. | I tap a switch, the train changes track, the cat goes to the sign that matches. That is the game. | `TAP THE SWITCH` → `MATCH THE PIN` | "Tap a switch and the train takes the other branch. Every cat is carrying a sign — get it to the platform that matches." |
| **3** | 0:10–0:16 | 6s | Same take continues. Two more cats leave the depot — blue square, orange triangle. The HUD capsule at the top shows who is coming and what they want. Thumb throws two more switches; two more arrivals; the counter climbs to 3/3; the board clears. | Locked, with a barely perceptible 3% push-in across the six seconds. | It is a live routing puzzle with a queue of incoming demand, and it is being played right now. | `ONE UNBROKEN TAKE — REAL DEVICE` small, top-right, 0:12–0:15 | "More keep coming. The strip along the top tells you who is next and where they want to go." |
| **4** | 0:16–0:25 | 9s | A harder board: four cats live, two junctions, one platform filling. A cat is routed to the wrong platform — badge mismatch flashes, the level fails. One tap on retry; the board resets instantly; the same board is solved cleanly on the second run. | Locked. If a cause-focus camera exists, let it snap to the culprit platform on the fail — otherwise hold. | It has real difficulty and real stakes, failure is legible, and retrying costs nothing. | `GET IT WRONG` → `RETRY IN ONE TAP` | "Send one to the wrong platform and you lose the board. Retrying takes one tap. There is no life to spend and no ad to sit through." |
| **5** | 0:25–0:34 | 9s | Six visibly different boards, ~1.5s each: different track topologies, different station colours and shapes, different furniture — depot, pines, fences, the clock, a signpost. Matched exposure across all six so the cuts read as flipping through a box of toys. | Each a locked iso frame. | There is a real amount of authored content, and it is authored, not procedural. | `SEVENTEEN HANDMADE BOARDS` → `EVERY ONE PROVEN SOLVABLE` | "Seventeen handmade boards. A solver runs in CI and proves each one is solvable before it is allowed to ship, so the difficulty is authored rather than accidental." |
| **6** | 0:34–0:44 | 10s | The daily board: today's date on the header, the board generating, four seconds of it being played. Then cut to a phone lock screen at rest; the daily notification arrives; a tap opens straight onto today's board mid-generation. | Locked for the board; the lock-screen beat is a straight-on phone-face capture. | There is a reason to come back tomorrow that exists inside the product, not bolted onto it. | `A NEW BOARD EVERY DAY` → `SAME BOARD FOR EVERYONE` | "And a new board every day, built from the date itself, so everyone gets the same puzzle. One notification, in the morning, that opens straight onto it." |
| **7** | 0:44–1:02 | 18s | The cosmetics screen: cats in a row, some wearing a conductor's cap, a scarf, a little bell collar. Thumb taps a locked one. **The RevenueCat-driven purchase sheet appears**, then the Google Play confirmation, then the purchase completes and the item unlocks. Cut back to the live board — the cat riding past the junction is now wearing the hat. | Locked through the purchase. Then, on the final four seconds, **the one real camera move in the video**: a slow push-in onto the hatted cat riding the train. | There is a working, purchasable thing, RevenueCat powers it, and you can see what you bought from where you play. | `POWERED BY REVENUECAT` on the sheet → `YOU CAN SEE IT FROM HERE` on the push-in | "The cats can be dressed. A conductor's cap, a scarf, a bell collar. That is the whole business — one purchase, RevenueCat handles it, and the rule I set myself is that everything for sale has to be visible from where you actually play. No currency, no energy, no loot boxes." |
| **8** | 1:02–1:13 | 11s | Three-beat strip showing a level-to-level transition with **nothing** in between: win → results → next board loads. Then the single rewarded surface, with the player's own thumb choosing to open it. | Locked. | Nothing interrupts you; the only ad is one you asked for. | `NO INTERSTITIALS. NO BANNERS.` → `ADS ONLY WHEN YOU ASK` | "There is no interstitial and no banner anywhere in this build — not throttled, not capped, absent. The only ad in the game is one you choose to open, for something you wanted." |
| **9** | 1:13–1:31 | 18s | Three held, HUD-free beauty frames, ~6s each: the depot with the engine parked and morning light across the sleepers; a macro on the switch lever and its teal base; the coffee cup steaming at the board's edge with the whole diorama soft behind it. | Locked, or one gentle 5% push on the last frame only. Let them breathe. | Somebody cared about this object. | `A TOY, NOT AN INTERFACE` on the last frame only | "I wanted it to look like something you would find in a box at the back of a cupboard. Warm wood, chunky pieces, soft light. Every line reads by colour *and* shape, so it works if you cannot tell red from green. A toy, not an interface." |
| **10** | 1:31–1:39 | 8s | The Cat Metro wordmark on warm paper. Below it, the store line. | Static card. | Where to get it. | `CAT METRO` / `FREE ON GOOGLE PLAY` | "Cat Metro. Free on Google Play." |
| **11** | 1:39–1:45 | 6s | Category card, static. | Static card. | Which awards this entry is for. | `ENTERED: BEST GAME · KEEP THEM COMING BACK · CATVERTISING · DESIGN · HAMM` | (silence) |

**Total: 1:45.** Fifteen seconds of headroom under the 2:00 cap.

### 1.3 Where the RevenueCat purchase appears

**Shot 7, 0:44–1:02.** It gets the single largest block in the video — eighteen seconds, more than
any other beat — for three reasons: the competition is run by a monetization company; HAMM is one
of our five tracks and it is judged entirely on this; and RevenueCat integration is Gate 2 for
eligibility, so showing it working on screen is also showing that we qualify.

The beat must contain, in order and without a cut that could be read as a splice:

1. The player tapping a locked cosmetic — **player-initiated**, not a paywall thrown at them.
2. The RevenueCat-driven purchase sheet, on screen long enough to read (≥2s).
3. The real Google Play purchase confirmation.
4. The unlock landing.
5. **The bought thing visible on the live board**, at normal play distance.

Step 5 is the argument. Anyone can film a paywall. The push-in onto a cat wearing a hat while it
rides past a junction is what makes the monetization thesis legible in one shot: *the product is a
thing you look at, so the only things worth selling are things you can see from here.*

`POWERED BY REVENUECAT` burns on screen at step 2. That is a factual integration credit, not a
sponsor logo — do not use RevenueCat's wordmark or logo file unless the rules explicitly permit it,
because §7 of the research flags third-party marks as a disqualification risk. Set it in our own
type.

### 1.4 Removable blocks

If the cut runs long, or a feature is not ready, these come out in this order without touching the
hook or the purchase:

| Order | Block | Seconds recovered | Cost of removing it |
|---|---|---|---|
| 1 | Shot 9 trimmed from three beauty frames to one | 12s | Design track loses its dedicated coda; the art still carries the whole video |
| 2 | Shot 6 lock-screen half (keep the daily board) | 5s | OneSignal track loses its on-screen proof; the blurb still describes it |
| 3 | Shot 8 | 11s | Catvertising track has no footage; drop the track rather than describe absent ads |
| 4 | Shot 5 cut from six boards to four | 3s | Content scale reads smaller |

**Never removable:** shots 1–3 (the hook), shot 7 (the purchase), shot 11 (the category card).

### 1.5 Audio and rights

- **No third-party music. None.** The rules make this a disqualification risk and there is no
  version of "it was only background" that survives it.
- Original narration, recorded in one take per paragraph, normalized to −16 LUFS.
- **As audited on 2026-08-25 the project has no audio at all** — zero `AudioSource`/`AudioClip`
  references and zero audio files (G13). So the default is **narration over silence**, and that is
  a perfectly respectable submission video. Do not add a foley bed in post: inventing arrival
  chimes the build cannot play is inventing a product feature in the one artifact judges are
  guaranteed to see, and an RC advocate plays the build before winners are finalised.
- Burn captions. Many viewers watch muted, and a full muted watch must carry the entire argument.
- Sweep the final cut for: competitor names, real transit-authority marks, store badges beyond a
  plain text CTA, OS notification content from other apps, any visible third-party logo. The
  lock-screen shot in beat 6 is the highest-risk frame — clear every other notification off that
  device first.

---

## 2. The hero screenshot

**One image. Exactly 1179×2556 px. Opaque sRGB PNG. No device frame.**

### 2.1 The core decision: full-bleed, no caption band

`docs/store/creative-shot-list.md` specifies two different exports from the same moment: **S1** for
the Play listing (a 562px warm-paper caption band above a 917×1988 gameplay inset) and **D0**, a
raw frameless export straight from the scene. **The Devpost hero is D0.** Full-bleed gameplay,
edge to edge, no caption, no band, no overlay, no text of any kind.

The reasoning, since this is the single most consequential composition choice in the submission:

- The written description already carries the words, and a judge reads it on the same page as the
  image. A caption on the image spends 22% of the frame repeating what is in the paragraph
  directly beneath it.
- The one thing text cannot do is prove the object exists. The image's entire job is the thing
  the prose is worst at.
- Two of our five tracks — Design and Best Game — are judged substantially on art direction. A
  band of marketing type across the top of a craft image announces "advertisement" and undoes the
  work shot 1 of the video is doing.
- Play-listing screenshots are a different job with a different audience (a browsing consumer who
  needs the mechanic explained in one line at thumbnail size). Keep S1's caption band there.

The Devpost gallery renders this small. It must survive that — see the thumbnail test in §2.5.

### 2.2 Which board, which camera, which moment

**Level:** the richest board that still reads in one glance. Concretely: **three destination
stations of three different colour-and-shape pairs, two junctions, one train of three carriages
with three cats aboard, each carrying a different pin.** Pick the authored level that matches from
the mid-band (L006–L010 territory) at capture time.

- **Why not L001/L002.** The spare teaching boards — one switch, two stations — are correct
  pedagogy and a weak photograph. In the current render (`.catshots/orchestrator-2026-08-25-r6/`)
  the two-station board leaves roughly half the frame as bare board and desk. A hero has to look
  like there is a game's worth of game here.
- **Why not the hardest board.** Eight cats and five junctions is unreadable at 236px wide and
  reads as clutter, which is the opposite of the Pok Pok-adjacent craft signal.
- **Fallback order:** if the preferred board cannot reach the required state cleanly, take the next
  simpler authored board that still has ≥2 stations of distinct colour *and* shape and ≥2 cats
  visibly aboard. Never hand-place a cat or pose the scene — a posed hero is the one thing that
  can be caught, and the rules require the app to match what the submission shows.

**Camera:** the shipped low three-quarter isometric — **not** the near-top-down currently coming
out of the capture rig. The r6 renders sit close to plan view with the board rotated diagonally in
frame; the concept art (`docs/reference/target-01-tabletop.png`) sits far lower and squarer, which
is what makes it read as an object on a table rather than a diagram of one. The board should fill
the frame corner to corner, with the desk visible only as warm margin.

**Moment:** the decisive instant. **Train mid-junction, lever just thrown, points visibly set
toward the branch that leads to the platform matching the lead cat's pin.**

A static image of a puzzle game has to contain an implied verb or it is furniture. A train sitting
in a siding says "diorama". A train entering a switch that has visibly just been thrown says
"a decision is being made here, and you are the one making it" — and a viewer who never plays the
game can reconstruct the entire ruleset from that one frame: cat has a sign, station has a sign,
lever chooses which one it reaches.

### 2.3 What is in frame

Approximate placement in the 1179×2556 canvas. The shipped HUD renders where it renders — **do not
move it for the shot**; these are the targets the composition should be built toward, not an
instruction to relocate UI in an image editor.

| Element | Where | Why |
|---|---|---|
| HUD capsule — upcoming cats with destination badges | Top, roughly y=120–330 | Proves the game has forward-looking information, and reads as UI craft |
| Objective counter | Just below the capsule | One number that says "this is a goal, not a sandbox" |
| The board | Roughly y=380–2290, filling the frame width | The subject |
| **The junction, lever thrown** | Optical centre, around x=590, y=1500 — slightly *below* geometric centre | The eye lands low on a portrait frame; the decision point must be where the eye lands |
| Train with three cats and three distinct pins | Entering the junction, upper-left to centre | The mechanic and the charm in one object |
| Two matching platforms, distinct colour **and** shape | Lower third, left and right | The rule, stated visually and colourblind-safely |
| Depot | Upper area | Establishes where cats come from; anchors the composition's top |
| Furniture — pines, fences, the clock, a signpost | Scattered, filling dead board | LOOK.md: "full without being busy" |
| Coffee cup | Bottom-left corner, cropped by the frame edge, softly defocused | The single strongest "this is a real object in a real room" cue in the concept art |
| Warm key light from upper-left, soft contact shadows under every piece | Throughout | The difference between a toy and a mesh |

### 2.4 What is deliberately not in frame

- **No caption, band, overlay, or text of any kind.** (§2.1)
- **No device frame, bezel, status bar, or navigation bar.** Required by the rules.
- **No thumb, finger, or touch indicator.** The video shows the hand; the still should be the
  object. A finger in a hero still reads as an app-store mockup.
- **No fail state, win state, results card, menu, or paywall.** One idea per image.
- **No dev console, FPS counter, gizmo, or editor chrome.**
- **No store badge, no award laurel, no "as seen in", no RevenueCat logo.**
- **No second mechanic.** If a board has a mechanic the first fifteen seconds of the video did not
  teach, it is the wrong board for the hero.
- **No motion blur, no post-process bloom past what the game ships.** The still must be a frame the
  player can actually reach.

### 2.5 Format and the two ways to get the pixels

**1179×2556 is 1:2.1679.** The Pixel 9 Pro screen is 1280×2856, which is 1:2.2313. **These do not
match.** A full-screen device capture scaled to width 1179 lands at 1179×2630 — 74 pixels too tall.
Never fix that by stretching. Two acceptable paths:

- **Preferred — render natively.** Drive the in-editor capture rig at exactly 1179×2556 and write
  the PNG at that size. No resample, no crop, no aspect fudge, and the composition is framed for
  the target aspect from the start.
- **Acceptable — device capture, then crop.** `adb exec-out screencap -p` at 1280×2856 → downscale
  proportionally to 1179×2630 → crop 37px from the top and 37px from the bottom. Frame the shot
  knowing those 74 rows will go.

Also required: opaque (no alpha), sRGB, and **captured at target resolution rather than upscaled
from anything smaller.** Record the source commit, the level ID, and the PNG's SHA-256 next to the
file.

**The thumbnail test — run it before accepting the shot.** Downsample to 20% (236×511) and look at
it cold. It must still read as: *a wooden toy railway, a train, cats aboard, two coloured platforms
with different shapes.* If the pins vanish or the platforms merge into the board at that size, the
camera is too high or the board is too busy. Re-shoot; do not fix it in post.

---

## 3. The written description

Voice: plain, concrete, first person, short sentences, no adjective stacking, no feature-list
prose. Say what the thing is and what happens when you touch it. This is the voice the Play
listing should use too — `docs/store/play-store-listing.md` already writes this way and should stay
that way.

**Standing rule inherited from `claim-ledger.md`, and the only piece of its machinery worth
keeping:** nothing goes in this copy that does not exist in the build being submitted. If a
feature slips, the sentence is deleted, not softened into an implication.

### 3.1 Main description

> Cat Metro is a wooden train set that lives on your phone.
>
> Cats queue up at the depot. Each one is holding a sign — a red circle, a blue square, an orange
> triangle — and each one wants the platform that matches it. They ride out on a little steam
> engine, and the only thing you control is the switches. Tap a lever, the points slide across, and
> the train takes the other branch.
>
> That is the whole game. Get every cat to the right platform and the board clears. Send one to the
> wrong platform and it doesn't. A board takes under a minute, and retrying takes one tap.
>
> There are seventeen handmade boards, and a solver runs in CI that has to prove a board is
> solvable before it is allowed to ship — so when a board is hard, it is hard on purpose. There is
> also a new board every day, built from the date itself, so everyone gets the same puzzle without
> a server being involved.
>
> The look is the point of the project. It is a low-poly wooden toy on a warm desk, lit like late
> afternoon, with a coffee cup at the edge of the board. Navy rails on cream sleepers. Chunky
> pieces with soft edges, the way a good children's toy is made. Every line is readable by colour
> *and* by shape, so it still works if you can't tell red from green.
>
> You can dress the cats — a conductor's cap, a scarf, a bell collar — and that is the entire
> business model. One purchase, handled by RevenueCat, for a thing you can see from where you play.
> No currency, no energy, no loot boxes, no subscription. There is no interstitial and no banner
> anywhere in the build; the only ad is one you choose to open.
>
> Fair by design: no forced ads, no energy, no loot boxes, every board solvable free.

**Do not paste this yet.** Four sentences in it describe things that do not exist in the build as
audited on 2026-08-25, and each is deleted rather than softened if its gap does not close:

| Sentence | Depends on | Gap |
|---|---|---|
| "Cats queue up… holding a sign" | cats on trains, destination pins | G1, G4 |
| "readable by colour *and* by shape" | station badges being shapes, not letters | G5 |
| "You can dress the cats… handled by RevenueCat" | the entire monetization stack | G2 |
| "the only ad is one you choose to open" | rewarded ads existing | G2 |

And "seventeen" must be recounted on the release candidate against boards **reachable through
ordinary player flow**, not against files in `content/levels/` (G14).

### 3.2 Per-track blurbs

Five tracks, five different arguments. Written separately on purpose — a judge reading the
Catvertising entry and a judge reading the Design entry want almost nothing in common.

---

**Best Game** — *"great gameplay, art direction, and a monetization fit that suits the genre"*

> One verb: tap a switch. Cats ride out of the depot carrying destination signs, and you decide
> which branch each train takes. A board is under a minute; the whole game is legible in about
> eight seconds of watching someone play it.
>
> The difficulty is authored rather than accidental. A beam-search solver shares the exact
> simulation the game runs and proves every board solvable before CI will merge it, so a hard board
> is hard because it was designed that way, not because the generator got lucky. Seventeen
> handmade boards, plus a daily board generated from the date so every player gets the same puzzle
> with no server involved.
>
> The art direction is the reason the project exists: a low-poly wooden toy railway on a warm desk,
> navy rails on cream sleepers, chunky rounded pieces, late-afternoon light, a coffee cup at the
> board's edge. Not a UI that looks like a toy — an object that happens to be playable.
>
> The monetization fits the genre because it is barely there. Cosmetics for the cats, one purchase
> each, no currency and no consumables. The verified poles of this genre are all one-time
> purchases; its verified failure mode is forced interruption. We built for the first and refused
> the second.

---

**Keep Them Coming Back (OneSignal)** — *"thoughtful notifications, campaigns, or Journeys that
improve the experience and give users a meaningful reason to return"*

> The reason to come back already existed in the product before a notification was written, and
> that is the whole design. There is a new board every day, derived from the date itself, so every
> player in the world gets the same puzzle — which means the notification is not a manufactured
> hook, it is a delivery mechanism for a thing that is genuinely there and genuinely different
> today.
>
> One message. In the morning, in the player's own time window, deep-linked straight onto today's
> board rather than to a home screen or a store. No streak is held hostage: a break costs nothing
> you cannot get back, and a missed day never gates content, because a puzzle you are being
> punished for missing is not a puzzle you come back to.
>
> The permission ask is spent late and once — after a player has finished their first daily board,
> not at install, because the first thing a new app should do is not ask for something.
>
> And the lapse sequence ends by saying "no more reminders after this," and then keeps that
> promise with a tag that permanently blocks re-entry. The measure of good retention messaging is
> whether the player would opt in again knowing exactly what they were signing up for.

---

**Catvertising** — *"clever placements, smart integration with the rest of the revenue stack, and
an experience users don't hate"*

> Our entry is an inversion: the cleverest placement in Cat Metro is the one we refused to build.
>
> There is no interstitial, no banner, and no app-open ad surface anywhere in the binary. Not
> capped, not throttled, not behind a remote flag — absent. You can play board after board after
> board and nothing will ever interrupt you, because there is no code path that could.
>
> The only ad in the game is rewarded and player-initiated: you open it, on purpose, because you
> want the thing on the other side of it. It sits exactly where a player already wants something,
> and if you decline it repeatedly the surface goes quiet rather than asking again — telling us no
> is a signal we obey.
>
> It reports through RevenueCat alongside the in-app purchases, so one dashboard answers "what is
> this player worth, and which half of it came from an ad they chose to watch."
>
> The market evidence for building it this way is not subtle. The genre's biggest ad-driven hits
> carry "an ad every other level" in their review backlash, and the forced-30-second-ad titles sit
> a full point lower in rating than their install counts would predict. "An experience users don't
> hate" is not a nice-to-have in a game people play for forty seconds at a time on a bus. It is the
> product.

---

**Design** — *"the craft of app development, separate from viability as a business… innovative
ideas and/or beautiful app design and animations"*

> The brief was one sentence: it should look like a wooden train set someone left out on a desk.
>
> Not a flat UI with a wood texture — an object. Chunky low-poly pieces with soft rounded edges,
> the way a good children's toy is actually made. Navy rails on cream sleepers, with real thickness,
> curving in arcs across a board that has a visible edge and sits on a real table. Raised wooden
> platforms with coloured roofs. A depot, pines, fences, a little clock. A coffee cup and a pencil
> just outside the board, so the frame reads as a room rather than a screen. One warm key light
> from the upper left and soft contact shadows under every piece, because nothing that is flat-lit
> ever looks like it has weight.
>
> The discipline underneath it is that readability outranks beauty, and the game is beautiful
> anyway. No line is ever identified by colour alone — every destination carries a colour *and* a
> shape, on the cat, on the platform, and in the preview strip, so the board survives deutan,
> protan and tritan simulation. The camera never moves during play, because a puzzle you are
> planning three moves ahead in should not be a thing that drifts.
>
> The whole project is a record of chasing one reference image. The concept art and the current
> build sit side by side in the repository, and the gap between them has been the entire
> engineering plan.

---

**HAMM (Help Apps Make Money)** — *"the smartest use of RevenueCat to drive real revenue"*

> The monetization has a name: **you can see it from here.**
>
> Cat Metro is a diorama. The player spends the entire session looking at one warm, well-lit object
> from a fixed camera. So the rule is that nothing goes on sale unless it is visible from that
> camera, at that distance, during ordinary play. A hat on a cat riding past a junction qualifies.
> A stat boost, a currency balance, a menu-screen badge does not — you would never see it, so it is
> not worth your money and we do not sell it.
>
> That rule kills more than it permits, and that is what makes it a design decision rather than a
> slogan. It rules out consumables, soft currency, energy, loot boxes and season passes, all of
> which we could have shipped and none of which pass the test. It also sets a natural price ceiling:
> we are selling a thing you look at, so it is priced like a thing you look at, once.
>
> Everything runs through RevenueCat — products, entitlements, and the purchase sheet — so the
> whole economy is one integration and one dashboard, which for a solo build is the difference
> between having a business model and having a backlog item.
>
> On revenue honesty: this is a model that deliberately caps its own ceiling. Whatever RevenueCat
> reports for the window is what we will report, with its date range, and we would rather show a
> small honest number than a rate without a denominator.

---

## 4. Capture plan

### 4.1 What exists — audited, 2026-08-25

**There is no video capture path in this repository.** Not a stub, not a broken one — none. Both
plans for one are documents: `docs/plan/marketing/devpost-video-script.md` assumes
`adb screenrecord`, and `growth_aso_plan.md` §23.1 specifies Unity Recorder 5.1.6. **Unity Recorder
is not in `unity/Packages/manifest.json`.** A grep for `screenrecord|ffmpeg|\.mp4|Recorder` across
the tree hits documentation only. Video capture is a plan, not a capability.

What does exist is three **still** mechanisms:

| # | Mechanism | Captures | Trigger |
|---|---|---|---|
| 1 | `unity/Assets/Tests/PlayMode/Screens/UiPhoneCaptureTests.cs` | `step-7-home.png`, `step-7-failure.png` — renders `GameRoot.Cam` into a RenderTexture → `ReadPixels` → `EncodeToPNG` | PlayMode test, opt-in via `CM_UI_CAPTURE_DIR`; unset ⇒ `Assert.Pass("capture rig disarmed")` |
| 2 | `unity/Assets/Tests/PlayMode/Board/BoardLookTests.cs` → `CaptureEvidence_BoardLook_917x2048_WhenRequested` | One gameplay board frame via `GameRoot.Cam` | `CM_BOARD_LOOK_CAPTURE_DIR`, or CLI `-cmBoardLookCapture` (defaults to `unity/Library/Captures`) |
| 3 | `scripts/emu-selftest.sh frame out.png` | Device framebuffer → PNG via `adb exec-out screencap -p` | Shell. **Emulator-only by design** — it rejects any serial not matching `emulator-*` |

**Two hard constraints these impose:**

- **Resolution is hardcoded at 917×2048.** `BoardLookTests.cs:210-211` and the shared constants
  (`CaptureWidth = 917`, `CaptureHeight = 2048`, `CaptureDpi = 408`) are compile-time literals.
  917×2048 is 1:2.233 — **not** the 1:2.168 the hero needs. Every frame in `.catshots/` is this
  size, including our current best (`orchestrator-2026-08-25-r6/rigA-board-train-midedge.png`).
  Nothing in the repo can currently emit 1179×2556.
- **`emu-selftest.sh` cannot target the Pixel.** It hard-rejects non-emulator serials, so the one
  scripted device-capture path in the repo is pointed at the wrong device for our purposes.

`DevFrameCapture.cs` is not a capture rig — it writes `framelog.csv` (frame timings), and only
flushes on `OnApplicationPause(true)`. The only `[MenuItem]` in the project is
`CatMetro/Build Game Scene`; there is no editor capture menu. `.catshots/` is an untracked
convention that agents write to by hand, not a tool.

**The E-1 trap, and its current status.** `state/PROJECT_STATE.md` records that Overlay canvases
never render into RenderTexture `Capture()` rigs — a capture that looks fine and silently contains
no HUD. Both Unity rigs above are RenderTexture rigs, so this is live risk. However, the r6 frames
*do* show the HUD capsule and counters, which suggests the board-look path composites correctly (or
that the HUD is not on an Overlay canvas). **Do not assume either way — verify on the first capture
by taking one frame and looking at it.** This is the same class of failure as the URP base-colour
bug in `AGENTS.md`: no test can see it, a human looking at the render can.

**Provenance warning on our current best frame.** `.catshots/orchestrator-2026-08-25-r6/` shows a
cream HUD capsule with a cat-face destination badge and round/square station badges. **Neither
`main` nor `integration/look-stack` produces that** — on both, `WavePreviewStrip` builds
colour-tinted `Quad` chips with a `TextMesh`, and the station symbol is a `TextMesh` letter. So r6
was composed from lane branches not yet integrated. Treat it as a preview of the merged stack, not
as a reproducible baseline, and re-shoot from the integration branch before trusting any
composition decision made against it.

### 4.2 The four capture paths, and what each is for

| Path | Produces | Use it for | Needs |
|---|---|---|---|
| **A. Device screen recording** — `adb -s 48121FDAP006X4 shell screenrecord` | 1280×2856 H.264 at device framerate | **All video gameplay footage** (shots 1–8). The only path that produces a real thumb, real touch latency and real frame pacing — and the only one that proves the app runs. | **Must be built** — no script exists. Plus a dev APK on the device (human-run `scripts/build-apk.sh`), DND on, dev console off, notifications cleared |
| **B. Device still capture** — `adb -s 48121FDAP006X4 exec-out screencap -p` | 1280×2856 PNG | Hero screenshot, fallback route; crop per §2.5 | **Must be built or invoked by hand** — `emu-selftest.sh` refuses non-emulator serials |
| **C. In-editor still rig** — `CM_BOARD_LOOK_CAPTURE_DIR` | PNG, currently 917×2048 only | Hero screenshot, **preferred route** once it can emit 1179×2556 natively — the only path with no resample and no aspect fudge. Also shot 9's beauty frames, if a HUD-off flag exists. | A Unity slot (human), plus the resolution override and HUD-off flag in §4.3 |
| **D. Screen/Game-view capture** — OS-level recording of the Unity Game view | Video or stills at the Game-view size | Fallback for any beat blocked by E-1, and for editor-only states | A Unity slot. Lowest fidelity; use only if A is unavailable |

### 4.3 What has to be built

Small, specific, and each one buys a shot that cannot otherwise be taken. Ordered by what blocks
the most.

1. **A device screen-recording script.** `adb -s <serial> shell screenrecord` wrapped with the
   device-safety check `AGENTS.md` demands (read `adb devices -l`, verify `model:` is Pixel 9 Pro,
   refuse otherwise), plus pull-and-name. Roughly the shape of `emu-selftest.sh` but pointed at a
   real device and recording rather than screencapping. *(Buys: the entire video. Blocking for
   shots 1–8.)*
2. **A capture resolution override.** `BoardLookTests.cs` hardcodes 917×2048 as `const int`. It
   needs to take the output size from the environment so the hero can be rendered natively at
   1179×2556. *(Buys: the hero with no resample and no aspect fudge.)*
3. **A HUD-off capture flag.** Shot 9's three beauty frames need the board with no capsule and no
   counter. Today the only way to get that is to not have a HUD. *(Buys: the 18-second Design coda.)*
4. **Verify the Overlay-canvas composite (E-1)** on the first capture from the integration branch.
   Five minutes of looking, not a build task — but if it fails, every UI-bearing frame moves to
   path A or D. *(Blocking, cheap, do it first.)*
5. **Scripted input replay to a named tick.** The hero needs one specific frame: train mid-junction,
   lever just thrown. The simulation is already deterministic with a command log, so replaying to a
   known tick turns "hunt across forty takes" into "re-render". *(Buys: the hero and shot 4's
   fail-then-clean-solve pair, repeatably.)*
6. **A photo-mode camera preset** at the low three-quarter concept angle — **only if** the shipped
   camera cannot get there while remaining playable. A marketing-only camera is a last resort: the
   rules require the app to match what the submission shows, so if the shipped camera cannot
   produce the hero, the correct fix is to change the shipped camera. This is what `feat/composition`
   is for.

### 4.4 Human-gated steps

Flagged explicitly, because none of these can be done from inside a sandboxed agent session:

- **Unity slot required** for: any editor capture (path C), items 1, 2, 3, 5 above, and any
  verification that a capture actually contains the HUD.
- **Device required** for: the dev APK build (`scripts/build-apk.sh` — `scripts/build.sh` builds
  nothing), all of path A and B, and the lock-screen notification beat in shot 6.
- **Read `adb devices -l` and check `model:` before every adb command.** `48121FDAP006X4` is the
  Pixel 9 Pro and is the only correct target; `2G0YC5ZF7Z056Q` (Quest 3) and `emulator-5554` (Pico)
  belong to other projects.
- **Human-only:** the Google Play upload, and the licensing decision about shipping Meshy/Tripo/
  Polyfork assets in a distributed binary (`AGENTS.md`). The research (§8.1) establishes all three
  paid tiers permit it; the decision is still the human's.

### 4.5 Capture order

Shoot in this order so that a failure late in the list does not invalidate work earlier in it:

1. **Hero still first**, from the release-candidate build. It is the highest-value single artifact
   and the one most sensitive to art state.
2. **Shots 1–3 second**, as one long unbroken take, repeated until clean. This is the hardest shot
   in the video to get — ten unbroken seconds with correct thumb timing — and everything else is
   easier. Bank several takes.
3. Shots 4, 5, 8 — ordinary gameplay, cheap, many takes.
4. Shot 7 — the purchase. Requires a live RevenueCat sandbox or real purchase on device. Bank it
   the day it first works, because it depends on the most fragile integration in the project.
5. Shot 6 — daily plus notification. Needs a second day, or a clock change, or a manual send.
6. Shot 9 — beauty frames. Last, because they depend on the final art state and take minutes once
   the flag exists.

Record for every clip and still: device, APK hash, commit, level ID, date. Not as a governance
ritual — as the thing that lets you re-shoot one broken asset in September without re-shooting all
of them.

---

## 5. Gap list — what must be true before this can be shot

Audited against `main` and `integration/look-stack` on 2026-08-25. Ordered by how much of the
submission each one blocks. **Every item is a shot in §1 or §2 that currently cannot be taken.**

Each gap names the lane branch that appears to own it, so this doubles as a sequencing list.

### What is already true (so the list below reads in proportion)

Boot → Home → level intro → tap a switch → a train runs real spline track over a warm wooden
tabletop with a board that has an edge → win banner "All cats home!" + Next → fail with a
cause-focus camera and a one-tap retry. That is a genuine, complete, playable loop, and it is more
than most entries will have. `TapInput` resolves the nearest switch disc and refreshes the lever
the same frame; `ToyTrackMeshBuilder` builds real sleeper-and-rail geometry per edge over a
`TrackSplineGraph`; the desk, board body, cream rim and warm background are real code with tests
pinning them. The daily generator is fourteen source files and an offline validator, and it works.

### Tier 0 — blocks the submission itself, not just a shot

**G1. There are no cats.** `BoardView.cs:308` — a train is
`GameObject.CreatePrimitive(PrimitiveType.Capsule)` at scale 0.35, tinted by colour code. No cat
mesh, no rider, no seat. Ten decimated cat GLBs exist (`cat-red-tabby.glb`, `cat-blue-siamese.glb`,
`cat-conductor.glb`, …) under `unity/Assets/Art/Generated/incoming/decimated/`, but they are never
imported, are not under any `Resources/` folder, and nothing loads them — `CatModelCatalog` does not
exist on either branch.
*Blocks:* shots 1, 2, 3, 5, 7, 9 **and the hero screenshot**. Every frame of the submission.
*Why it is Tier 0:* the game is called Cat Metro, one judge is a children's-toy specialist, and a
judge who watches the whole video sees no cat. This is the single highest-value item in the
project. *Owner:* `feat/cats-on-trains`.

**G2. There is no monetization of any kind.** No RevenueCat package in
`unity/Packages/manifest.json` (dependencies are Unity first-party only). No Unity IAP, no AdMob,
no purchase flow, no store screen — `Presentation/Screens/` contains only Home, LevelIntro and
ScreenStack. No cosmetics: `settings.equippedThemeId` is an empty string with no catalog and no
equip path behind it. What exists is an analytics *event schema* (`Application/EventTaxonomy/` —
`purchase_completed`, `rewarded_ad_started`, `cosmetic_equipped`, and a sink string literally named
`"revenuecat_adtracker"`) plus reserved save keys (`SaveDefaults.cs` — `economy`, `entitlements`,
`caps.counters`), all defaulting to zero and nothing writing them.
*Blocks:* shot 7 (18s, marked unremovable), shot 8, the HAMM blurb, the Catvertising blurb, and the
purchase paragraph of the main description.
*Why it is Tier 0:* **this is Gate 2 of the competition.** Per the rules, an app must use the
RevenueCat SDK to power at least one purchase or serve ads through RevenueCat Ads. Without it there
is no entry at all, in any track. *Owner:* `feat/revenuecat`.
*Tripwire to expect:* `ResultsPanel.cs:20` documents that its "exactly one CTA, structurally empty
footer" assertion is a **deliberate** monetization tripwire. A test will go red the moment a store
button is added to the win screen. That is intentional, not a regression — read it before deleting it.

**G3. No video capture path exists.** Covered in §4.1/§4.3. There is nothing to record with.
*Blocks:* shots 1–8, i.e. the video. *Owner:* unassigned — needs a lane.

### Tier 1 — blocks the hook (shots 1–3) or the hero

**G4. No destination pins.** No `DestinationPin`/`CatPin` on either branch. A cat currently
communicates its destination through the tint of the capsule that *is* the cat.
*Blocks:* the VO line "every cat is carrying a sign" at 0:06, and the hero's entire central read —
the whole point of the hero moment is that the pin and the platform badge match. *Owner:*
`feat/cat-pins`.

**G5. Stations are colour + *letter*, not colour + shape.** `BoardView.cs:135` builds a `TextMesh`
named "Symbol" showing the first character of the colour name — "R", "B", "Y", "G".
*Blocks:* the Design blurb's colourblind claim, the description's "readable by colour *and* by
shape" sentence, and shot 9's VO. Also note the letter derives from the **English** colour name, so
it does not localise and is not actually a shape channel — two problems, one line of code.
*Owner:* `feat/station-badges`.

**G6. The wave-preview strip carries colour only.** `WavePreviewStrip.cs` builds `Quad` chips with
a tinted material and a `TextMesh`; the per-wave count exists only inside an assert string, never
in pixels. No destination badge, no cat face.
*Blocks:* shot 3's VO ("the strip tells you who is next *and where they want to go*") and the HUD
capsule at the top of the hero screenshot. *Owner:* `feat/hud-wave`, `feat/hud-parity`.

**G7. The capture rig cannot emit 1179×2556.** Hardcoded `const int width = 917; height = 2048`.
*Blocks:* the hero, natively. Path B (device screencap + crop) is the workaround. *Owner:* §4.3 item 2.

**G8. Props render on exactly one machine.** `PropModelCatalog` loads
`Resources.Load("CatMetroProps/" + id)`, but `unity/Assets/Resources/` holds only `Strings/ui.csv`
and two materials. The real prefabs sit under `unity/Assets/Art/Generated/incoming/Resources/`,
which `.gitignore:30` ignores — `git ls-files unity/Assets/Art` returns zero tracked files.
*Blocks:* the depot, trees, fences and desk clutter in every shot, and the **coffee cup** the hero
spec calls its strongest "real object in a real room" cue. Note there is no prop named "coffee cup"
at all; the nearest declared id is `prop-desk-clutter`.
*Also:* this fails silently — a clean checkout renders `AdmittedEntryCount == 0` with no log line,
which `AGENTS.md` names as a known class. Whoever shoots this must confirm the count is non-zero
before trusting a frame. *Owner:* `feat/polyfork-furnish`.

### Tier 2 — blocks a named shot

**G9. The daily puzzle is finished and unreachable.** `GameRoot.DailyEntryUnlocked` is a static
that defaults to `false` (`GameRoot.cs:120`), documented as dev/test-only, and `HomeScreenView`
only builds the Daily pin when it is true — **so a shipped build never constructs it.** Behind that
flag: fourteen files under `Content/Daily/` (`DailyPipeline.cs` alone is 20 KB), plus
`scripts/validate-dailies.sh` running an offline solver-backed validation over a date range.
*Blocks:* shot 6 (10s) and the **entire OneSignal blurb — our single largest target prize at
$25,000.**
*Why this is the best value in the list:* the work is done. What is missing is a player-facing
route to it and an unlock condition. *Owner:* `feat/daily-live` — currently sitting at the base
commit, i.e. not started.

**G10. Waiting cats are invisible.** `SimulationState` tracks `NodeQueueCounts[]` and
`NodeQueueSlots[][]` — the simulation knows exactly who is waiting where — and `BoardView` never
reads either.
*Blocks:* shot 4's legibility. The core fail condition is queue overflow, and it currently builds up
with no on-screen signal at all, so "you lose the board" reads as arbitrary rather than as
something you could have seen coming. *Owner:* `stale/CM-C12-queue-reading-band` — **stale branch,
needs reviving or re-cutting.**

**G11. `FailReason.PlatformOverflow` throws by design.** `Outcomes.cs:40-42`, pinned behind an
unanswered human question, even though `fail.platformoverflow` exists in `ui.csv`. Only
`QueueOverflow` is a live fail path.
*Blocks:* shot 4 if the chosen board fails by platform overflow. Pick a queue-overflow board, or
get the human question answered.

**G12. No level select.** One pin on Home, linear `LoadNext()`.
*Blocks:* shot 5's six-board montage as a single take — it needs six separate captures, and a dev
override cannot source a marketing claim because the rules require the app to match what the video
shows.

**G13. No audio exists.** Zero `AudioSource`/`AudioClip`/`PlayOneShot` references across
`unity/Assets/Scripts`, `unity/Assets/Tests` and `dotnet`; zero `.wav`/`.mp3`/`.ogg` files anywhere.
The only trace is `settings.audio = true` in `SaveDefaults.cs` with nothing behind it.
*Blocks:* nothing outright — §1.5 already routes to narration-over-silence. But it means the
"platform stamps" beat in shot 2 and the arrival feedback in shot 3 land silently, and a judge
playing the build hears nothing at all. Decide deliberately rather than by default.

### Tier 3 — accuracy and risk, not shots

**G14. Recount the level number before it goes in copy.** There are **17** files in
`content/levels/` (L001–L017) — not 19, as the briefing for this document assumed, and not the 10
the frozen claim ledger records. Separately, *files* are not *reachable levels*: the ledger's frozen
baseline exposed only L001–L005 through ordinary progression. Shot 5's on-screen
`SEVENTEEN HANDMADE BOARDS`, its VO, and the description's "seventeen handmade boards" must all be
recounted against **levels reachable through ordinary player flow on the release candidate**, and
corrected to whatever that number actually is.

**G15. The Home screen is a flat 2D card, not the tabletop.** `HomeScreenView` paints flat UI
rectangles — `HeroDeck`, `RouteBed`, twelve `Sleeper00-11`, and `CatBayA/B/C` which are coloured
rectangles, not cats — over the already-live L001 board.
*Blocks:* nothing in this script, because Home is deliberately absent from the shot list. But it is
the first thing a judge who *does* install sees, and cutting from it into the 3D diorama would read
as two different products. Either bring it toward the tabletop or keep it out of frame — do not let
it into the video by accident.

**G16. Our best-looking frame is not reproducible from the integration branch.** See §4.1. The r6
capture shows HUD and station badges that neither `main` nor `integration/look-stack` produces.
Before any composition decision in §2 is treated as settled, re-shoot from the branch the release
candidate will actually be cut from.

### The one-line sequencing read

**G1 (cats) and G2 (RevenueCat) are the whole game.** G1 is every frame of the submission; G2 is
eligibility. Everything else is recoverable, degradable, or removable — §1.4 already lists which
shots come out and in what order. If only two things land before the capture window, those are the
two. **G9 (surface the daily) is the cheapest large win on the board** — a finished subsystem
behind a `false`, standing between us and the largest single prize we are targeting.
