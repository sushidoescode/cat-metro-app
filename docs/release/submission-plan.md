# Cat Metro — Shipaton submission plan

**The video and the screenshot are the product.** Everything in this document follows from the
official-rule excerpt supplied with the release brief:

> "Judges are not required to test the Project and may choose to judge based solely on the text
> description, images, and video provided."

Plus its companion: judges "are not required to watch beyond two minutes."

So a judge may score Cat Metro having never launched it. The ≤2:00 video, the single 1179×2556
screenshot, and the written description are not marketing attached to the game — for judging
purposes they *are* the game. A beautiful build that photographs badly loses to a mediocre build
that photographs well. This document specifies those three artifacts, how to capture them, and
what has to be true in the build before they can be shot.

**Scope of this document:** docs only. It changes no gameplay code and captures nothing. It is a
shot and copy brief for the release candidate.

**Target tracks** (from the research, §4 "Recommended focus"): Best Game ($20k), OneSignal "Keep
Them Coming Back" ($25k), Design ($20k), and HAMM ($20k). Catvertising ($20k) is conditional on a
rewarded-video surface actually reaching the release candidate; the current branch has no ads and
must not claim otherwise.

**Deadline:** 2026-09-30 23:45 PDT. The public-store gate eats most of the remaining margin. Ship
iOS first; Google Play's closed-test lead time makes it the follow-on release, not the critical
path.

---

## What this plan inherits, and where it departs

The release and store material already contains a great deal of usable thinking, and this plan
mines it rather than restarting:

| Source | What is reused | What is changed |
|---|---|---|
| `docs/store/creative-shot-list.md` | The **D0 raw hero export** concept — a frameless 1179×2556 straight from the scene, distinct from the captioned Play screenshot — is exactly right and is adopted | Its S1 editorial composition (562px caption band + 917×1988 inset) stays where it belongs: on the Play listing. The Devpost hero is full-bleed. See §2. |
| `docs/store/play-store-listing.md` | The voice — plain, concrete, no adjective stacking — and the "Fair by design" positioning line | Expanded, §3. |
| `docs/release/release-checklist.md` | Store evidence, signing and human-only release steps | Keep those operational checks out of the video itself; this document only names the proof the final cut needs. |

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
| **5** | 0:25–0:34 | 9s | Six visibly different boards, ~1.5s each: different track topologies, different station colours and shapes, different furniture — depot, pines, fences, the clock, a signpost. Matched exposure across all six so the cuts read as flipping through a box of toys. | Each a locked iso frame. | There is a real amount of authored content, and it is authored, not procedural. | `NINETEEN HANDMADE BOARDS` → `EVERY ONE PROVEN SOLVABLE` | "Nineteen handmade boards. A solver checks every shipped board, so the difficulty is authored rather than accidental." |
| **6** | 0:34–0:44 | 10s | The daily board: today's date on the header, the board generating, four seconds of it being played. Then cut to a phone lock screen at rest; the daily notification arrives; a tap opens straight onto today's board mid-generation. | Locked for the board; the lock-screen beat is a straight-on phone-face capture. | There is a reason to come back tomorrow that exists inside the product, not bolted onto it. | `A NEW BOARD EVERY DAY` → `SAME BOARD FOR EVERYONE` | "And a new board every day, built from the date itself, so everyone gets the same puzzle. One notification, in the morning, that opens straight onto it." |
| **7** | 0:44–1:02 | 18s | The Wardrobe shows the profile cat without its conductor's coat. Thumb taps the named, priced item. **The RevenueCat-driven purchase flow appears**, then the App Store confirmation, then the entitlement completes and the same profile cat visibly gains its coat and hat. | Locked through the purchase; no cut across tap → platform sheet → unlock. | There is a working named purchase, RevenueCat powers it, and the delivered item is visible immediately. | `POWERED BY REVENUECAT` on the sheet → `CONDUCTOR'S COAT UNLOCKED` on return | "The Wardrobe sells one named thing: a conductor's coat for the profile cat. RevenueCat handles the purchase and entitlement. No currency, no energy, no loot boxes." |
| **8** | 1:02–1:13 | 11s | **Conditional: include only if rewarded video is present in the release candidate.** Show win → results → next board with nothing between, then the player deliberately opening the one rewarded surface. | Locked. | Nothing interrupts play; any ad is explicitly requested. | `NO INTERSTITIALS. NO BANNERS.` → `REWARDED, NEVER FORCED` | "Nothing interrupts a board. Rewarded video only appears when you ask for it." |
| **9** | 1:13–1:31 | 18s | Three held, HUD-free beauty frames, ~6s each: the depot with the engine parked and morning light across the sleepers; a macro on the switch lever and its teal base; the coffee cup steaming at the board's edge with the whole diorama soft behind it. | Locked, or one gentle 5% push on the last frame only. Let them breathe. | Somebody cared about this object. | `A TOY, NOT AN INTERFACE` on the last frame only | "I wanted it to look like something you would find in a box at the back of a cupboard. Warm wood, chunky pieces, soft light. Every line reads by colour *and* shape, so it works if you cannot tell red from green. A toy, not an interface." |
| **10** | 1:31–1:39 | 8s | The Cat Metro wordmark on warm paper. Below it, the store line. | Static card. | Where to get it. | `CAT METRO` / `DOWNLOAD ON THE APP STORE` | "Cat Metro. Available on the App Store." |
| **11** | 1:39–1:45 | 6s | Category card, static. | Static card. | Which awards this entry is for. | `ENTERED: BEST GAME · KEEP THEM COMING BACK · DESIGN · HAMM` (add `CATVERTISING` only if shot 8 is proven) | (silence) |

**Total: 1:45.** Fifteen seconds of headroom under the 2:00 cap.

### 1.3 Where the RevenueCat purchase appears

**Shot 7, 0:44–1:02.** It gets the single largest block in the video — eighteen seconds, more than
any other beat — for three reasons: the competition is run by a monetization company; HAMM is one
of our confirmed tracks and it is judged entirely on this; and RevenueCat integration is the second eligibility gate for
eligibility, so showing it working on screen is also showing that we qualify.

The beat must contain, in order and without a cut that could be read as a splice:

1. The player tapping a locked cosmetic — **player-initiated**, not a paywall thrown at them.
2. The RevenueCat-driven purchase sheet, on screen long enough to read (≥2s).
3. The real App Store purchase confirmation.
4. The unlock landing.
5. **The same profile cat visibly wearing the bought coat** when the Wardrobe returns.

Step 5 is the argument. Anyone can film a paywall. The same profile cat gaining its coat after the
purchase makes the entitlement legible without claiming the current item appears on the live
board. If a later release does render owned cosmetics during play, re-shoot this beat then; do not
stage that behavior for the submission.

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
- **As audited on 2026-08-28 the project has no audio at all** — zero `AudioSource`/`AudioClip`
  references and zero audio files. So the default is **narration over silence**, and that is
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
- Two of our confirmed tracks — Design and Best Game — are judged substantially on art direction. A
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
  pedagogy and a weak photograph. In the prior phone-aspect captures, the two-station board leaves
  roughly half the frame as bare board and desk. A hero has to look
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

**Standing rule:** nothing goes in this copy that is not proven in the exact build being
submitted. If a feature slips, delete the sentence rather than soften it into an implication.

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
> There are nineteen handmade boards, and a solver checks every shipped board — so when a board is
> hard, it is hard on purpose. There is
> also a new board every day, built from the date itself, so everyone gets the same puzzle without
> a server being involved.
>
> The look is the point of the project. It is a low-poly wooden toy on a warm desk, lit like late
> afternoon, with a coffee cup at the edge of the board. Navy rails on cream sleepers. Chunky
> pieces with soft edges, the way a good children's toy is made. Every line is readable by colour
> *and* by shape, so it still works if you can't tell red from green.
>
> The Wardrobe has one named item: a conductor's coat for the profile cat. The purchase and
> entitlement are handled by RevenueCat. No currency, no energy, no loot boxes, no subscription.
> There is no interstitial and no banner anywhere in the build.
>
> Fair by design: no forced ads, no energy, no loot boxes, every board solvable free.

**Do not paste this until it is proven against the release candidate.** The contributing branches
now contain these features, but only capture and an on-device sandbox purchase prove the public
copy:

| Sentence | Depends on | Gap |
|---|---|---|
| "Cats queue up… holding a sign" | cats on trains, destination pins | release-candidate render |
| "readable by colour *and* by shape" | station badges and pins | colour-vision render sweep |
| "The Wardrobe has one named item… handled by RevenueCat" | configured product, offering and entitlement | real App Store sandbox purchase + restore |
| "There are nineteen handmade boards" | all 19 boards reachable and solver-checked | final headless suite + device progression |

The count is still a release claim, not a filename claim: verify all nineteen through ordinary
player progression on the release candidate before recording it.

### 3.2 Per-track blurbs

Four confirmed tracks, plus one conditional template, with different arguments. Written
separately on purpose — a judge reading the Catvertising entry and a judge reading the Design entry
want almost nothing in common.

---

**Best Game** — *"great gameplay, art direction, and a monetization fit that suits the genre"*

> One verb: tap a switch. Cats ride out of the depot carrying destination signs, and you decide
> which branch each train takes. A board is under a minute; the whole game is legible in about
> eight seconds of watching someone play it.
>
> The difficulty is authored rather than accidental. A beam-search solver shares the exact
> simulation the game runs and checks every shipped board, so a hard board
> is hard because it was designed that way, not because the generator got lucky. Nineteen
> handmade boards, plus a daily board generated from the date so every player gets the same puzzle
> with no server involved.
>
> The art direction is the reason the project exists: a low-poly wooden toy railway on a warm desk,
> navy rails on cream sleepers, chunky rounded pieces, late-afternoon light, a coffee cup at the
> board's edge. Not a UI that looks like a toy — an object that happens to be playable.
>
> The monetization fits the genre because it is barely there. One named conductor's coat, one
> purchase, no currency and no consumables. The verified poles of this genre are all one-time
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
> One chosen reminder window, tagged through OneSignal and deep-linked straight onto today's board
> rather than to a home screen or a store. No streak is held hostage: a break costs nothing
> you cannot get back, and a missed day never gates content, because a puzzle you are being
> punished for missing is not a puzzle you come back to.
>
> The permission ask is spent late and once — after a player has finished their first daily board,
> not at install, because the first thing a new app should do is not ask for something.
>
> Turning reminders off removes the slot tag and opts the subscription out. The measure of good
> retention messaging is whether the player would opt in again knowing exactly what they were
> signing up for.

---

**Catvertising — template only; omit unless rewarded video ships** — *"clever placements, smart
integration with the rest of the revenue stack, and an experience users don't hate"*

> Cat Metro has no interstitial, banner, or app-open ad surface. If rewarded video reaches the
> release candidate, its argument starts there: nothing interrupts a board.
>
> The rewarded surface must be player-initiated, must grant a named reward, and must report through
> RevenueCat Ads in the shipped app. Write the rest of this blurb from the captured candidate;
> until those facts are proven, do not enter this category.

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

> The monetization has a rule: **named, visible, permanent.**
>
> The Wardrobe sells one thing: a conductor's coat for the profile cat. The player sees the plain
> cat, taps the named item with its localized store price, completes the platform purchase, and
> returns to the same cat wearing the coat and hat. The current build does not claim that outfit is
> rendered on cats riding the board.
>
> That rule excludes consumables, soft currency, energy, loot boxes and season passes. It is a
> permanent named entitlement, restored through the same purchase surface.
>
> Everything runs through RevenueCat — products, entitlements, and the purchase sheet — so the
> whole economy is one integration and one dashboard, which for a solo build is the difference
> between having a business model and having a backlog item.
>
> On revenue honesty: this is a model that deliberately caps its own ceiling. Whatever RevenueCat
> reports for the window is what we will report, with its date range, and we would rather show a
> small honest number than a rate without a denominator.

---

## 4. Capture and release proof

This is a creative brief, not the release authority. The current release checklist and store
runbooks own signing, packaging, and human-only publication. Every submission image and clip must
come from the exact release-candidate behavior; do not stage a feature that the public build does
not contain.

### 4.1 Current capture limits

- The in-editor still rigs emit 917×2048. The 1179×2556 hero therefore needs either a verified
  resolution override or a device capture framed for the documented crop. Never upscale a smaller
  render.
- The repository has no dedicated gameplay-video recorder. Device capture is the practical path.
  Before any device command, run `adb devices -l` and confirm the serial reports the Pixel 9 Pro
  model. The Quest and Pico targets belong to other projects.
- RenderTexture captures need a frame after binding before screen-space UI is laid out. Inspect the
  first frame for missing HUD, wrong material bindings, placeholder cats, and prop-catalog
  admission; those failures can pass structural tests.
- Capture from the signed candidate that matches the store build. A development-only screen,
  hand-posed cat, or editor-only flag cannot support a public claim.

### 4.2 Claims that need candidate evidence

| Claim or gate | Required evidence before it enters the cut or copy |
|---|---|
| Public availability | A working public App Store product URL. “In review” is not evidence. |
| RevenueCat eligibility | Human-supplied production configuration, a real named product and entitlement, and an on-device purchase plus restore through the shipped RevenueCat SDK. |
| Nineteen handmade boards | Final solver-backed headless suite and ordinary player progression reaching L001–L019 in the candidate. |
| Cats, pins, badges and warm tabletop art | Release-candidate render inspected at phone size and at the 20% thumbnail size. |
| Daily + OneSignal | Permission requested only after value is shown, the configured notification received on device, and its tap deep-linking to Daily. |
| Rewarded-video claims / Catvertising | A working player-initiated rewarded surface in the candidate. If absent, remove shot 8 and the category entirely. |
| No forced ads / no paid randomness | Binary and ordinary-flow inspection of the candidate; no implication that an unimplemented rewarded surface exists. |

The committed RevenueCat example configuration is not production configuration. Supplying the
real public SDK values, creating the store product, and making the commercial licensing decision
are human release prerequisites.

### 4.3 Capture order

1. Capture the hero still from the candidate and pass the 20% thumbnail test.
2. Bank several unbroken takes of shots 1–3 on the target phone.
3. Capture the real purchase and restore as soon as the configured App Store flow works.
4. Capture Daily and the OneSignal deep link on device.
5. Capture montage and beauty frames last, after the art state is frozen.

For each accepted asset, record the candidate commit, build hash, level ID, device, and date. That
small provenance note is what makes a single late re-shoot possible without recreating the whole
video.

### 4.4 Human-only boundaries

- Store uploads and the decision to distribute paid Meshy, Tripo, or Polyfork assets remain
  human-only.
- The human runs Unity builds and owns the device/Unity validation slot.
- No submission claim is promoted from “planned” to “shipped” until the public candidate itself
  supplies the evidence.
