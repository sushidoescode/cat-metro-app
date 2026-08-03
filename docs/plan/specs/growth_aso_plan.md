# Growth & ASO Plan — Cat Metro

Status: v1.0, 31 Jul 2026. Governed by `deliverables/DECISIONS_BRIEF.md` (locked 31 Jul 2026).
Siblings honored: `specs/monetization_spec.md`, `specs/liveops_spec.md`, `specs/onesignal_retention.md`,
`specs/product_spec.md`, `data/roadmap_56_days.csv`, `data/experiment_backlog.csv`.
Public 1.0 target: **Aug 24–28 2026** (Aug 24 is a Monday). Event window closes **Sep 30 2026 11:45pm PDT**.
Budget default: **$0 organic-only**; the $500 and $2,000 ladders (brief §BUDGET SCENARIOS) never unlock
before the D1-retention floor is confirmed, and paid cohorts are always labeled separately.

All competitive numbers below are **verified store data as of 2026-07-31** and are labeled as such
wherever quoted externally. Benchmark vintages are labeled per the brief's honesty rule.

---

## 1. Positioning statement

**One line (the locked spine):**
> **Cat Metro is a 60-second train puzzle that treats you like an adult: no forced ads, no energy, no loot boxes, every level solvable free.**

**Store-facing one-liner (25 words):**
> Route cat commuters through a tabletop metro with one thumb. Forty-five to ninety seconds a level, thirty handcrafted levels, a new Daily Line every day.

**Elevator (60 words, press/influencer):**
> Cat Metro is a deterministic route-switching puzzle for Android. You tap junctions to send color- and symbol-coded cat trains into matching stations before platforms overflow. Levels last 45–90 seconds and every one is solver-proven solvable without spending. It is free, it has no interstitials and no banners, and the only ads are ones you deliberately ask for.

**Category framing (what shelf we stand on):**
Primary: *train / route puzzle* (the Mini Metro–Trainyard–Railbound shelf). Secondary: *cat game* (the
Neko Atsume–Cats & Soup shelf). We enter through the **train-puzzle door with a cat face on it** — the
puzzle shelf gives us an under-contested query lane (§3.4), the cat shelf gives us the icon, the brand,
and the shareability. Never the reverse: a "cat game" that happens to have a puzzle in it loses to
42.7M-install incumbents on day one.

**Three claims we can defend under scrutiny (each maps to shipped behavior, not vibes):**
1. **"No forced ads"** — there is no interstitial, banner, or app-open ad surface in the binary at all (monetization_spec §1.1). Not "few ads." *None.*
2. **"Every level solvable free"** — every launch level is solver-validated against the exact Domain step function before it can merge (architecture.md; CI content gate). The paywall footer states it because it is mechanically true.
3. **"60 seconds"** — 45–90s session target is a locked design constraint with the difficulty table to back it (product_spec §22).

**Anti-positioning (what we will not claim):** not "the best puzzle game," not "relaxing" (it has a
fail state), not "for everyone" (13+ target audience; art deliberately not child-directed per the
Families risk in the brief), not "hardcore." Not "AI-generated" as a headline — generation is a tool
in the pipeline, not a selling point, and leading with it invites a fight we gain nothing from.

**Decision:** Enter as a *train puzzle* first and a *cat game* second, with fairness as the third rail that makes the whole thing quotable.
**Evidence:** Verified 2026-07-31 — no cat-themed metro/route-switching puzzle exists on Play (4 searches); "train puzzle" carries only 3–4 real incumbents while "cat game" is hyper-competitive; Mini Metro owns "metro puzzle" outright at 3.6M installs / 4.63★.
**Action:** Lock these three strings into the store listing (§3), the paywall trust line (already locked in monetization_spec §4.1), the press kit at catmetro.com/press (roadmap D25), and every Devpost field.
**Risk:** "Train puzzle" is a smaller total addressable query pool than "cat game" — we deliberately trade reach for rankability.
**Fallback:** The cat shelf is still reachable through the icon, screenshots, and short-video distribution (§12, §19) without spending a single title character on it; if the train lane underperforms after two ASO iterations (roadmap D29–35 and D36–42), test `Cat Metro: Cat Train Puzzle` as a title variant (28 chars, §3.1) rather than abandoning the lane.

---

## 2. Competitive differentiation grid

All figures **verified 2026-07-31**. Installs are Play Store lifetime installs unless noted.

| Competitor | Price / model | Installs | Rating | Monetization spine | The verified constraint or complaint | What Cat Metro takes | What Cat Metro refuses |
|---|---|---|---|---|---|---|---|
| **Mini Metro** | $0.99 premium, no ads, no IAP | 3.6M | 4.63★ | One-time purchase, nothing after | Endgame **micromanagement complaints**; a paid-upfront wall in front of every install | The fairness halo; the calm transit aesthetic; the "one clean idea" discipline | The $0.99 gate (kills F2P reach), and the unbounded endless run that produces the micromanagement complaint — our levels **end** in 45–90s |
| **Railbound** | $4.99 premium | 211K | — (Apple Design Award winner) | One-time purchase | An Apple Design Award still capped it at 211K — premium reach runs **~25–500× below F2P** in this genre | Handcrafted level craft; award-grade art direction as a real competitive axis | Paid-upfront distribution; we ship the craft *behind a free door* |
| **Arrows – Puzzle Escape** | Free + heavy ads | 103.6M in 12 months | 4.83★ | Interstitial-dense F2P | The loudest review theme is **"ad every other level"** backlash despite the rating | The proof that a simple, legible core loop scales enormously | Forced interruption. There is no interstitial surface in our binary |
| **Bus Traffic Fever** | Free + forced ads | 15.4M in 5 months | **3.72★** | Forced 30s ads, recycled levels | Rating collapse under forced ads + content recycling — the clearest cautionary data point we own | Nothing mechanically | Forced 30s ads; recycled-level content padding. Our content cadence is capped honestly (liveops §5) rather than faked |
| **Neko Atsume** | Free, IAP **≤$3.49** | 13.6M | **4.78★** | Gentle low-ceiling IAP | Proves gentle monetization and a high rating coexist in cat games; no fail state, so no difficulty tension to sell against | The gentle ceiling philosophy; cat-brand warmth; screenshot-shareable moments | The zero-tension loop — we need a fail state for the puzzle to matter, so our "gentleness" has to be structural (free options first) not just cheap |
| **Cats & Soup** | Free, cosmetics spine + whale packs | 42.7M | — | Cosmetics-led with high-end packs | Demonstrates a cosmetics spine can carry a cat game to 42.7M | The cosmetics-as-spine idea: our tickets buy **earnable** cosmetics (600–1,200) and our themes are cosmetic | Whale packs and premium currency. Our ceiling is $9.99 and there is no soft-currency store |
| **→ Cat Metro** | **Free + gentle IAP ($1.99–$9.99) + player-initiated rewarded ads only** | 0 at time of writing | — | All Access $6.99 as the honest recommendation; 5 rewarded surfaces, all opt-in | Unproven; solo-dev content cadence is the real ceiling | — | — |

**The whitespace claim, stated precisely:** verified 2026-07-31, there is **no cat-themed metro or
route-switching puzzle on Google Play** (4 independent searches). Trainyard was delisted in 2019.
Mini Motorways is not on Android. STATIONflow and Overcrowd are PC-only. The adjacent shelf is
literally empty on the platform we are shipping to.

**The differentiation sentence we use everywhere (press, Reddit, Devpost, influencer DMs):**
> Every free puzzle game in this category monetizes your patience. Cat Metro monetizes nothing you
> did not ask for — and the levels are proven solvable without paying, by a solver, in CI.

**Clone risk, priced in:** Meowdoku spawned 7+ clones in ~3 months (verified 2026-07-31). Expect a
fast-follow. Our moat is **brand + fairness reputation + the daily seed loop**, none of which a
two-week clone can copy credibly. Ship the brand hard and early (§10–§18).

**Decision:** Position against the *category's behavior*, not against any single title; the grid's right-hand columns are the messaging map.
**Evidence:** All six comps verified 2026-07-31 with the specific figures in the table; the no-competitor whitespace finding is from 4 Play searches on the same date.
**Action:** Turn the "takes/refuses" columns into the screenshot caption set (§5) and the five Reddit angles (§10); put the differentiation sentence at the top of the press kit.
**Risk:** Naming competitors publicly invites "you're not as good as Mini Metro" replies, and comp installs can move after 2026-07-31.
**Fallback:** In public copy we describe *behaviors* ("games that show an ad every other level"), never brand names; brand names stay in internal docs, Devpost's judged research narrative, and press-kit context where attribution and dates are given.

---

## 3. ASO — Google Play listing

Play indexes the **app title, short description, and long description** (there is no iOS-style keyword
field). Everything below is written to that reality.

### 3.1 App title

**`Cat Metro: Train Puzzle`** — **23 characters** (Play limit 30; 7 chars of headroom).

| Candidate | Chars | Verdict |
|---|---|---|
| `Cat Metro: Train Puzzle` | **23** | **SHIP.** Owns the brand + the best-lane keyword; short titles truncate less in search results and on device home screens |
| `Cat Metro: Train Puzzle Game` | 28 | Hold as ASO iteration-2 variant if "puzzle game" outperforms "puzzle" in Play search terms (roadmap D29–35) |
| `Cat Metro: Metro Puzzle` | 23 | **Reject** — "metro puzzle" is owned by Mini Metro (3.6M / 4.63★, verified 2026-07-31); we would rank third for a query we cannot win |
| `Cat Metro` | 9 | **Reject** — wastes 21 indexable characters |

Brand-name safety: "Cat Metro" verified collision-free on Play, App Store and Steam (2026-07-31);
catmetro.com/.io/.app were unregistered at RDAP check on the same date and are registered on D1
(roadmap). Backup brandable name if a conflict emerges: **Meowtro** (also verified clean).

### 3.2 Short description — 3 options, all ≤80 chars

| # | Copy | Chars | Angle | Role |
|---|---|---|---|---|
| **A** | `Route cat trains with one thumb. 60-second metro puzzles, no forced ads.` | **72** | Mechanic-led | **LAUNCH DEFAULT.** Leads with the verb, carries "cat", "train", "puzzle", and the differentiator |
| **B** | `The fair cat train puzzle: no forced ads, no energy, every level solvable.` | **74** | Fairness-led | Experiment variant for E18 (experiment_backlog) — tests whether values outsell mechanics |
| **C** | `Tap the switch, send the cats home. A 60-second train puzzle you can finish.` | **76** | Emotional/verb-led | Reserve; "you can finish" is the anti-endless-runner hook against the Mini Metro micromanagement complaint |

All three contain **"train"** and **"puzzle"**; A and C also contain **"metro"/"cat"**. None contains a
price, a superlative, an emoji, or a claim we cannot defend.

### 3.3 Long description (full draft — 2,338 characters of the 4,000 limit)

```
Cat Metro is a one-thumb train puzzle about routing cat commuters through a tiny tabletop city. Tap a junction, throw the switch, and send the right cat down the right line before the platform overflows. Levels run 45 to 90 seconds. Every single one is solvable without paying anything.

FAIR BY DESIGN
No forced ads. No energy. No loot boxes. Every level solvable free.
Ads exist in exactly one place: where you ask for one. Want an extra rewind, double tickets, or a three-level test drive of a premium theme? Watch an ad on purpose. Otherwise you will never see one. No interstitial between levels, no banner under the board, no full-screen surprise when you open the app.

HOW IT PLAYS
- Tap junctions to route color- and symbol-coded cat trains into matching stations
- Read the next-wave preview, hold a train in a queue, keep the flow going
- When it jams, the cause-first replay shows you exactly where it started
- Retry in under a second: no loading screen, no lives, no ad

WHAT IS IN THE GAME
- 30 handcrafted levels across 6 districts: Whisker Yard, Harbor Line, Market Cross, Twin Platforms, Catnip Gardens, Midnight Terminus
- A new Daily Line every day, generated from a shared seed, so every player in the world gets the same board and can compare
- Stars, streaks, and earnable cosmetics, all cosmetic and none of it sold
- Weekly District Cup rounds with participation liveries
- Full offline play: the whole campaign works on a plane

BUILT TO BE READ
Color is never the only signal. Every line carries a color, a symbol, and its own cat silhouette, so the board reads for colorblind players. Tap targets are large. Motion and haptics have toggles. Planning-pause mode freezes the simulation while you think it through.

IF YOU WANT MORE
All Access is one purchase, never a subscription: the Night Harbor bonus district with 10 extra levels, both premium themes, a doubled daily rewind, a permanent ad-free guarantee, and a gold conductor badge. Themes are also sold separately. Rewind packs exist if you want them. That is the entire shop, and it lives in one tab you have to open yourself.

BUILT IN PUBLIC
Cat Metro was made by one developer in 56 days for RevenueCat Shipaton 2026, with the numbers posted openly along the way: installs, retention, revenue, and the mistakes.

One thumb. Sixty seconds. Nine lives.
```

Deliberate omissions and why:
- **No numeric price anywhere.** Play renders the IAP price range on the listing automatically and localized prices drift; monetization_spec §7 rule 4 already bans hard-coded price strings in-product, and the same discipline avoids a stale listing. "One purchase, never a subscription" carries the meaning.
- **No child-directed language, no "kids", no "for all ages"** — target audience is 13+ and Play may reject child-appealing listings that are not Families-compliant (verified 2026-07-31).
- **No "no ads" absolute claim.** The listing will carry the **"Contains ads"** label because rewarded ads exist; the copy says precisely what is true ("where you ask for one"). Claiming "no ads" next to a "Contains ads" badge is the fastest way to earn 1★ reviews.
- **Keyword density is natural**: "puzzle" ×4, "train" ×3, "cat/cats" ×6, "metro" ×2, "levels" ×6 across 406 words. No keyword stuffing block, which Play's policy treats as spam.

### 3.4 Keyword strategy

| Term | Verified signal (2026-07-31) | Our lane | Placement | Priority |
|---|---|---|---|---|
| `train puzzle` | **Only 3–4 real incumbents — the best available lane** | **Primary. Own it.** | Title + short desc A/C + long desc ×3 | **P0** |
| `metro puzzle` | **Owned by Mini Metro** (3.6M / 4.63★) | Do not contest head-on; ride the association | Long desc only (natural prose), never the title | P1 |
| `cat game` | **Hyper-competitive** (Cats & Soup 42.7M, Cat Snack Bar 29.8M, Neko Atsume 13.6M) | Brand-entry only, never a ranking bet | Icon + screenshots + brand name; long desc prose | P2 |
| `cat puzzle` | Middle ground: cat-genre traffic, puzzle intent | Secondary target once "train puzzle" ranks | Long desc; title variant test in iteration 2 | P1 |
| `route puzzle` / `switch puzzle` | Thin, low-volume, high-intent | Cheap incremental | Long desc prose | P2 |
| `daily puzzle` | Large, generic, dominated by word/number games | Not contested | Long desc ("a new Daily Line every day") | P2 |
| `offline puzzle game` | Real recurring user need; low differentiation cost for us | Free win — we *are* offline-first | Long desc ("works on a plane") | P1 |
| `no ads puzzle` / `no forced ads` | Query exists as a complaint-shaped search | Perfectly aligned with our brand | Short desc A/B + long desc | P1 |
| `colorblind` / `accessible puzzle` | Small volume, high loyalty, zero competition | Free credibility | Long desc "BUILT TO BE READ" block | P2 |
| `Mini Metro`, `Railbound`, brand names | — | **Never used.** Competitor-brand keywords violate Play policy and invite takedowns | Nowhere | Cut |

**Iteration loop (locked to the roadmap):** ASO iteration 1 on **Aug 29–Sep 4** from Play Console
search-term data; iteration 2 on **Sep 5–11**. Each iteration changes **one field at a time** so the
Console's search-term deltas remain attributable.

**Decision:** Title `Cat Metro: Train Puzzle` (23 chars), short description A at launch, 2,338-char long description as drafted, "train puzzle" as the single owned query.
**Evidence:** Verified 2026-07-31 — "train puzzle" has only 3–4 incumbents; "metro puzzle" is Mini Metro's; "cat game" is hyper-competitive; Play listing CVR ~16% US average with games below average (AppTweak 2025 data — vintage labeled).
**Action:** Draft the full listing at roadmap D20 (Aug 20), lock at D26, run the E16/E17/E18 listing experiments from ~Aug 29 (§8).
**Risk:** Play search-term data is thin for a new app for ~2 weeks, so iteration 1 may be guesswork.
**Fallback:** If Console search terms are too sparse by Sep 4, hold the listing constant and spend the iteration on screenshots instead (higher-leverage on CVR than text, per the E17>E18 ordering in the experiment backlog).

---

## 4. Icon brief

**Deliverable:** 1024×1024 PNG (Play + the Shipaton submission both require it), plus a 512-safe crop
check and an adaptive-icon foreground/background pair for the launcher.

**Concept A — "Conductor" (character-led, LAUNCH DEFAULT):** a cream cat's face, front-on, wearing a
navy conductor cap with a small teal badge, on a flat Ticket Orange (#F08A3C) roundel. Ears break the
roundel's top edge slightly to create silhouette interest at 48px. No text. No drop shadow.

**Concept B — "The Line" (system-led, experiment variant E16):** an Ink Navy (#22304A) metro-map
line-diagram that resolves into a cat's head silhouette — two stations become the eyes, a curve becomes
the tail — on a Cream Card (#F2EAD9) field with a single Ticket Orange interchange dot.

**Hard constraints (all sourced):**
- Palette from the locked authoritative set: Cream Card #F2EAD9, Ink Navy #22304A, Metro Teal #3BAFA8, Ticket Orange #F08A3C (product_spec §7).
- **Must not read child-directed** — no oversized head-to-body ratio, no baby proportions, no primary-color explosion, no drooling/blushing expression (Families risk, verified 2026-07-31). The conductor cap and flat geometric treatment do this work.
- Legible at **48×48** in a Play search row and on a busy home screen: exactly **two** value steps (dark shape on light field or the inverse), one accent.
- No text, no numbers, no badges, no "NEW", no store-award imagery (Play policy).
- No third-party trademarks — no real transit roundel, no real metro logotype, nothing that reads as a licensed transit authority mark (the Shipaton video rule bans third-party trademarks; the same discipline applies to the icon to keep one asset kit legal everywhere).

**Test gate before shipping:** render at 512/192/96/48px; grayscale test; deutan/protan/tritan
simulation (the same colorblind merge gate the palette uses); side-by-side against a screenshot of the
actual Play search results row for `train puzzle` and for `cat game` — if it disappears in either, it fails.

**Decision:** Ship Concept A (Conductor) at launch; Concept B is the E16 store-listing experiment variant.
**Evidence:** Character-led icons win across the verified cat-genre comps (Neko Atsume 13.6M / 4.78★, verified 2026-07-31); E16 in `experiment_backlog.csv` already specifies exactly this A/B with Play handling randomization.
**Action:** 3 icon concepts due roadmap D20 (Aug 20) with the icon poll as a #BuildInPublic engagement beat; final 1024² export at D23 (Aug 23).
**Risk:** A cat-face icon pulls cat-game traffic that bounces when it sees a puzzle board — CVR up, D1 down.
**Fallback:** E16's guardrail is exactly this (D1 of installs acquired during each arm); if the character icon wins CVR but costs >3pp D1, ship Concept B and take the smaller, better-matched audience.

---

## 5. Six-screenshot sequence spec

Format: **portrait 1080×1920 PNG**, captured through the replay-driven Capture scene (architecture.md)
so every frame is a real game state, not a mockup. Caption band occupies the **top 22%** in Warm Paper
#FAF6EC over Ink Navy #22304A type; the board is never obscured. Play shows the first 2–3 in search
results, so screenshots 1 and 2 carry the entire pitch.

| # | Frame | Caption (exact copy) | Job |
|---|---|---|---|
| **1** | Mid-level board, District 2 Harbor Line, four cat trains in motion, one switch mid-throw with the thrown-arm arrow visible, next-wave preview strip lit | **"Tap the switch. Send every cat home."** | Teach the verb in one glance. This is the only screenshot most people see |
| **2** | Same board 3 seconds later, a red ● train entering the red station, purr-meter chain at ×4, score popping | **"45 to 90 seconds a level. Then it's over."** | Answer "how long is this?" — the anti-endless hook aimed straight at the Mini Metro micromanagement complaint |
| **3** | Close-up three-quarter of the diorama: cat silhouettes on a platform, cardboard bevel and desk props visible at the margin | **"A model railway of a cat city, on your desk."** | Sell the art direction (Design award evidence, and the reason someone screenshots it) |
| **4** | The rewind sheet open over FailureReview, showing free / owned / rewarded rows above the divider and pack rows below it | **"Ads only when you ask. Never forced, ever."** | The differentiator, shown as UI rather than asserted as a slogan. This is the Catvertising exhibit |
| **5** | Daily Line board with the date header, streak badge, and the share-card ribbon visible | **"A new Daily Line every day. Same board worldwide."** | Retention hook + the social loop |
| **6** | Split composition: the same board in the default cream theme and in the Sakura theme, with the colorblind symbol legend (● ■ ▲ ◆ ★) called out | **"Color plus symbol plus silhouette. Reads for everyone."** | Accessibility credibility + the cosmetic upside, without ever showing a price |

Rules that apply to all six: no fake UI, no invented ratings or awards, no "5,000,000 downloads"
claims, no device frames (Play prefers frameless and the Shipaton submission needs a frameless
1179×2556 asset anyway), no third-party marks, captions ≤ 8 words, one idea per screenshot.

**Also produced from the same capture session:** the rules-mandated **1179×2556 frameless screenshot**
for the Shipaton submission — same content as screenshot 1, re-rendered at that exact resolution rather
than upscaled (roadmap D23).

**Decision:** Gameplay board leads; fairness is screenshot 4, not screenshot 1.
**Evidence:** E17 in `experiment_backlog.csv` states this hypothesis explicitly ("players buy the mechanic first and the values second") and queues the reverse order as the test variant; the brief's own fallback note ("first screenshot caption reads 'Ads only when you ask'") is preserved as caption 4 and as the E17 variant.
**Action:** Capture at roadmap D23 (Aug 23) from banked replay logs; export all six + the 1179×2556 frameless asset in one session.
**Risk:** A board screenshot is visually busy at search-result thumbnail size and may read as noise.
**Fallback:** Screenshot 1 has a "clean" alternate take with 2 trains instead of 4 and a wider margin; swap it in if Console CVR sits below the ~16% US listing average (AppTweak 2025 data, vintage labeled) after two weeks.

---

## 6. Feature graphic brief

**Deliverable:** 1024×500 JPG/PNG, no alpha. Play may overlay a play button (if a promo video is
attached) and crops on some surfaces — so nothing important within 80px of any edge, and nothing
important dead-center.

**Composition:** a wide side-on cut of the diorama at desk height. Left third: the CAT METRO wordmark
in Ink Navy with the cat-ear cab silhouette as the logo mark. Center-right: a Harbor Line train
crossing a junction with three cats visible in the windows, warm key light from the top-left (the one
locked lighting rig), soft contact shadow, cardboard bevel edge along the bottom. Far right: a hint of
the Neon theme bleeding in as a color gradient — one image, two themes, zero explanation needed.

**Text on the graphic:** the wordmark only, plus optionally **"No forced ads. Ever."** in ≤ 5 words at
28pt-equivalent. No feature list (illegible at real display sizes), no store badges, no ratings, no
"Editor's Choice"-style imagery, no third-party marks.

**Variants to produce in the same session:** a 1024×500 "Daily Line" seasonal variant for the Sep 9
content patch; a 2:1 crop and a 16:9 crop for press kit and social headers, from the same source render
at 4096px wide so every downstream crop is a downsample, never an upscale.

**Decision:** Wordmark + one hero train + a theme gradient; the graphic sells *world*, the screenshots sell *mechanic*.
**Evidence:** Art direction is locked (premium tabletop diorama, one lighting rig, one toon shader family, cream/navy/teal/orange, readability over beauty) in the brief and product_spec §7.
**Action:** Render at roadmap D23 alongside the screenshot session; store the 4096px master in the press kit.
**Risk:** Feature graphics have modest measurable impact and can eat a day of art time in launch week.
**Fallback:** Timebox to 3 hours; if it overruns, ship a clean wordmark-on-Cream-Card graphic with the conductor cat at 1/3 and revisit after launch — it is not a gating asset.

---

## 7. Preview video (Play listing promo video)

Separate asset from the Shipaton 2-minute demo (that one is specced in `submission_script.md`). This
one is **YouTube-hosted, 24–30 seconds, silent-first**: Play autoplays muted for most users, so every
beat must land with no audio.

| Time | Shot | On-screen text |
|---|---|---|
| 0:00–0:03 | Cold open on a live board, a thumb enters frame and throws a switch, a cat train visibly changes line | `TAP THE SWITCH` |
| 0:03–0:09 | Speed-ramped 6s of a full level: waves arriving, two queues buffering, purr-meter chain climbing | `ROUTE THE CATS` |
| 0:09–0:13 | A jam builds, overflow ring counts down, level fails, cause-first camera snaps to the culprit platform | `SEE WHY YOU LOST` |
| 0:13–0:17 | Instant retry, clean solve, 3 stars land | `RETRY IN ONE SECOND` |
| 0:17–0:22 | District map pan across all 6 districts, then the Daily Line card flips over | `30 LEVELS + A DAILY LINE` |
| 0:22–0:27 | The rewind sheet appears showing free/owned/rewarded rows; then the shop tab, calm and small | `NO FORCED ADS. NO ENERGY.` |
| 0:27–0:30 | Wordmark on Cream Card, cat conductor tips its cap | `CAT METRO — FREE ON GOOGLE PLAY` |

Audio (for the unmuted minority): the game's own layered stems + tap/arrival/overflow SFX. **Original
audio only** — the Shipaton rules ban third-party music and trademarks, and running one audio policy
across all assets means no asset ever has to be re-cut for a different venue.

**Decision:** 30-second, silent-first, mechanic→failure→recovery→breadth→values arc, ending on the free CTA.
**Evidence:** Play autoplays muted; the brief's verified rules ban third-party trademarks/music in submission video, and a single audio policy keeps assets interchangeable.
**Action:** Cut from banked Unity Recorder takes at roadmap D23; upload unlisted, attach to the listing before the D24 production submission.
**Risk:** A weak promo video can *lower* store CVR versus no video at all.
**Fallback:** The listing ships fine without a video — if the cut is not clearly good by D23 evening, detach it, launch on screenshots, and attach it in the Sep 9 patch window.

---

## 8. Play store-listing experiment plan

Tool: Play Console → **Store listing experiments**. Play randomizes traffic and reports first-time
installer conversion per variant; it is one of the very few properly randomized tests available at our
scale (most in-game tests are underpowered — see `experiment_backlog.csv` sample-size notes).

Confirm the current variant limit and minimum-traffic requirements in Console the day you create each
test (not verified in this brief). One element at a time, sequential, never parallel — with ~200
listing visitors/day, parallel tests interact and nothing is attributable.

| Slot | Exp | Test | Start | Length | Primary metric | Guardrail | Ship rule |
|---|---|---|---|---|---|---|---|
| 1 | **E16-store-icon** | Icon A (Conductor, character-led) vs Icon B (metro lines forming a cat) | ~Aug 29 (listing live ≥3 days) | 14d | First-time installer conversion | D1 retention of installs acquired in each arm; no child-appealing art | Adopt only at Play-reported >90% probability to beat |
| 2 | **E17-screenshot-order** | Board-first (default) vs fairness-card-first | ~Sep 12 | 14d | First-time installer conversion | D1 of each arm (message-fit) | Adopt at >90%; **do not adopt** if fairness-first wins CVR but costs >3pp D1 |
| 3 | **E18-short-description** | A (mechanic-led) vs B (fairness-led) | ~Sep 26 — **only if it does not touch the freeze** | 14d, runs past the window into judging | First-time installer conversion | D1 | Accept a null after 14d and keep control |

Slot 3 lands inside the **Sep 26–30 submission freeze**. A store-listing experiment does not change the
binary and does not affect judge access, but it *does* change what a judge sees on the store page.
**Rule: slot 3 only starts if the Devpost submission is already staged complete and the experiment's
variant copy is one we would be happy for a judge to read.** Otherwise it starts Oct 1.

Also tracked but not an experiment: **custom store listings** are out of scope (they require audience
segmentation we do not have), and **pre-registration is skipped** (§9).

**Decision:** Three sequential listing experiments, icon first, 14 days each, adopt only above Play's >90% probability-to-beat readout.
**Evidence:** E16/E17/E18 in `experiment_backlog.csv` (each with stated power limits: at ~200 visitors/day, 14 days detects only >15% relative lifts); brief's marketing-experiment credit under Grand Prize "Growth by numbers".
**Action:** Create slot 1 the day the production listing has 3 days of stable traffic (~Aug 29); log each readout as an ADR and as a #BuildInPublic post.
**Risk:** Traffic too low for Play to declare a winner at all — the most likely outcome at our scale.
**Fallback:** Report "no detectable difference at n=X" honestly (that is itself a good #BuildInPublic and Devpost artifact) and keep the control; never adopt a variant on a coin-flip readout.

---

## 9. Pre-registration decision — **SKIP**

**Decision: do not run Google Play pre-registration.** Reasoning, in order of weight:

1. **It is structurally incompatible with the event's core requirement.** The brief's verified rules require the **first public store release to occur inside the submission window** and reward "Early and Effective Release." Pre-registration front-loads weeks of listing-live time *before* the app is installable, which converts our scarcest resource — days of live revenue inside the window — into a mailing list.
2. **The math does not work in a 37-day window.** Launch is Aug 24–28; the window closes Sep 30. A pre-registration campaign worth running needs 3–4 weeks of accrual *before* launch, which would mean listing pre-reg from ~Aug 1 — the exact days we are in closed testing with an unfinished game and no assets.
3. **We do not have the traffic to fill it.** Pre-registration converts an existing audience; at $0 budget with no audience on Aug 1, we would be pre-registering the same ~50 people we can simply message on launch day.
4. **It adds Play Console surface area during the highest-risk approval period** (production access application, first-release review up to 7 days, data safety, content rating). Every additional listing state is another thing that can block the release we cannot afford to block.
5. **The reward mechanic tempts a policy problem.** Pre-registration rewards work best as an in-game item; ours would have to be cosmetic-only to stay inside the fair-by-design constraint, which makes it weak — and a strong version would violate our own positioning.

**What we do instead with the same energy:** the **#BuildInPublic audience** (§21) is our pre-registration
list. It costs nothing, it is already accruing from Aug 1 (roadmap BIP post 1/56), it converts on
launch day via a single "it's live" post across every channel, and it doubles as the $30k
#BuildInPublic award corpus. A pre-reg list produces installs once; the BIP corpus produces installs
*and* an award submission *and* press hooks.

**Revisit condition:** pre-registration becomes correct for the **post-event content update** (a
1.2 release with bands 41–60 and a new district — 31–40 ship in-window per the one content
schedule), where there is no release-timing constraint. Evaluate
in October.

**Decision:** SKIP pre-registration for 1.0; revisit for the post-event content release.
**Evidence:** Verified rules — first public store release must occur inside the window; Grand Prize criteria explicitly include "Early and Effective Release" and shortlist on total in-window revenue reported in RevenueCat.
**Action:** Record as ADR-0009 (tracked under backlog issue CM-038) so the decision is not relitigated in launch week; redirect the effort into §21's BIP cadence.
**Risk:** We forgo a day-one install spike that pre-registration sometimes produces.
**Fallback:** The launch-day burst is manufactured instead by the §10–§18 stack (5 Reddit posts, Discord, press, influencer wave, and the BIP audience) landing on the same 48 hours — controllable, free, and not dependent on Play's pre-reg notification delivery.

---

## 10. Community launch plan — Reddit (5 angles, disclosure-first)

**Non-negotiable rules for every post.** (a) Read the subreddit's current self-promotion rules the
morning of posting — they change, and "I didn't know" is not a defense. (b) Disclose in the **first
line**, before any pitch: "I'm the solo developer of this game." (c) Use the required flair. (d) Lead
with the interesting thing, not the link — the link goes in the body or the first comment per each
sub's rule. (e) Answer **every** comment for the first 6 hours, including hostile ones, without
defensiveness. (f) Never vote-manipulate, never ask friends to upvote, never post the same text to two
subs on the same day. (g) If a mod removes it, thank them and move on — do not repost.

| # | Subreddit | Angle | Title (draft) | Opening disclosure line | Asset | Timing |
|---|---|---|---|---|---|---|
| 1 | **r/AndroidGaming** | The launch post proper: a free Android puzzle with no forced ads | "I made a cat-train puzzle with zero forced ads — 30 levels, free, no energy" | "Solo dev here, this is my game — Cat Metro just went live on Play today." | 15s gameplay GIF (§19 concept 1) | **Launch day**, 09:00 ET (US morning + EU evening overlap) |
| 2 | **r/puzzlevideogames** | Genre-craft angle: deterministic route-switching and why levels *end* | "Every level in my puzzle game is proven solvable by a solver before it can ship" | "I'm the developer — sharing the design side, happy to talk mechanics." | Solver-visualization clip (§19 concept 7) | Launch day **+1**, 13:00 ET |
| 3 | **r/gamedev** | Technical post-mortem: 8 ticks/s deterministic sim, command log, CI level validation, 56 days solo | "56 days, one dev, one deterministic sim: how CI validates every level before merge" | "Solo dev post-mortem, my game shipped this week — technical write-up, link at the bottom." | Architecture diagram + CI screenshot | Launch day **+3** (needs real launch numbers to be worth reading) |
| 4 | **r/IndieGaming** | Showcase angle: the tabletop-diorama art direction | "A model railway of a cat city — my solo-made Android puzzle is out" | "Dev here — this is mine, made solo over 8 weeks." | 6-second loop of the diorama pan (§19 concept 4) | Launch day **+2**, 11:00 ET |
| 5 | **r/incremental_games** | **Weakest fit — gated behind a mod DM.** Angle: designing a non-predatory progression loop (tickets, streaks, cosmetic sinks) as a discussion post | "Designing a progression loop with no premium currency and no gates — what breaks?" | "Dev here, and up front: this is a puzzle game, not a true incremental — I'm posting for the economy discussion, and I'll delete if that's off-topic." | Economy table screenshot (600–1,200 sinks, 20–50/level earn) | Launch week, **only after a mod says yes** |

Bonus (not counted in the five): **r/playmygame** during closed test for tester backfill (already in
roadmap D1 fallback), and **r/CatsWithJobs**-style cat communities **only** with a genuinely funny
standalone asset and full disclosure — a cat subreddit is a place to be charming, not to advertise.
Also standing in the channel list (event-hosted, not a launch angle): the **r/AppBusiness Shipaton
check-in threads** — post the weekly check-in there with the **Shipaton flair** (official judging
guide, Aug 1), subject to the same non-negotiable rules at the top of this section.

**Decision:** Five angles across five subs, staggered over five days, each with a genuinely different artifact and first-line disclosure.
**Evidence:** Roadmap D26 already schedules "Reddit drafts for r/AndroidGaming per sub self-promo rules"; the brief's verified rules place no restriction on organic promotion.
**Action:** Draft all five posts by Aug 26 (roadmap D26), including the mod DM to r/incremental_games; keep them in `/marketing/reddit/` in the repo so the drafts are reviewable evidence for #BuildInPublic.
**Risk:** A removal or a self-promo ban on the biggest sub (r/AndroidGaming) removes our largest single organic channel on the most important day.
**Fallback:** Stagger by design so no single removal is fatal; if r/AndroidGaming removes the post, the same asset runs on r/AndroidApps and r/incremental_games' sibling communities, and the day's traffic plan shifts to the influencer wave (§15) which is scheduled independently.

---

## 11. Discord plan

**Do not build a large server.** A solo dev with a 56-day schedule cannot moderate one, and an empty
server is worse than no server. Two-tier plan:

**Tier 1 — be a good guest (P0, starts Aug 1).**
- The **official RevenueCat Shipaton Discord** (discord.gg/shipaton26): post the daily #BuildInPublic beat in the appropriate build-log channel, answer other builders' RevenueCat/Unity/EDM4U questions with real answers. This is simultaneously the highest-signal audience for the event's awards and a genuine peer group. 15 min/day cap.
- Unity and gamedev communities where I already have standing: share the determinism/solver work as *content*, not as promotion; link only when asked.
- Cozy/puzzle player communities: lurk from Aug 1, participate for three weeks before ever mentioning the game, and then only in the designated self-promo channel.

**Tier 2 — a small home server (P1, opens launch day).**
- Channels, deliberately few: `#announcements` (dev-only), `#daily-line` (share codes — the `CM-YYMMDD-score` grammar from liveops §1.3), `#bug-reports`, `#feedback`, `#screenshots`.
- Two rules, posted: be decent; no leaks of unreleased levels.
- One automation: a webhook post at local midnight UTC−? — **no.** No bot infrastructure at launch. The daily thread is posted manually or not at all; a broken bot in launch week is a self-inflicted incident.
- Invite surfaces: the game's Settings → Community row, the press kit, and the Reddit/BIP posts. **Never** an in-game modal (that would be a system-initiated interruption, which our own rules forbid).
- Success bar, honestly set: 50 members by Sep 30 is a *win* at our scale. This server is for the 20 people who love the game, not for reach.

**Decision:** Guest-first everywhere, one small home server opened on launch day with five channels, zero bots.
**Evidence:** Live-ops budget is capped at ≤10 h/week (liveops §5) and the brief's solo-dev sustainability principle; the Shipaton Discord is where the event's own audience is.
**Action:** Join and start the daily Shipaton-Discord beat on Aug 1; create the home server Aug 23 (pre-launch), open it Aug 24; add the Settings → Community row in the same build.
**Risk:** Discord moderation load spikes during a bad launch day (crash reports, refund questions) exactly when engineering attention is scarcest.
**Fallback:** `#bug-reports` is read-only-triaged once daily with a pinned "known issues" post; if load exceeds one hour/day, the server goes invite-only and support routes to the published support email instead.

---

## 12. X / TikTok / Reels / Shorts

Handles claimed on D1 (roadmap): **@CatMetroGame** on X and TikTok; same handle on Instagram and
YouTube where available.

| Channel | Role | Cadence | Format | What wins here |
|---|---|---|---|---|
| **X** | **#BuildInPublic home** ($30k award corpus) + industry/press reach | 1 post/day, Aug 1 → Sep 30, non-negotiable | Numbers, screenshots, GIFs, threads on Fridays | Honest numbers, specific failures, receipts. Threads on gate days (D7/D14/D21/D28/D35/D42) |
| **TikTok** | Reach engine for players | 4–5/week from launch week; 1/week before | 9:16, 12–25s, hook in the first 6s (§20), text-on-screen always, trending-adjacent audio *only if licensed for commercial use* — otherwise our own SFX | Satisfying-solve loops, "watch this jam happen", cat close-ups. The cat shelf we deliberately did not chase in ASO pays off here for free |
| **YouTube Shorts** | Same assets, different audience; also the home of the 30s promo and the 2-min demo | Same cadence as TikTok, cross-posted | 9:16, ≤60s, always with a pinned link | Longer tolerance for explanation; "how it's made" performs |
| **Instagram Reels** | Art-direction audience; cozy/desk-setup crossover | 3/week | 9:16, prettier grade, diorama macro shots | The tabletop-diorama look. This is where the Design award narrative gets its public evidence |

**Cross-posting policy:** one capture session feeds all four (§23). Re-export per platform rather than
re-share with a visible watermark — a TikTok watermark on Reels is suppressed and looks lazy. Captions
are rewritten per platform; the video is the same.

**Hard rules:** never use third-party music we do not have commercial rights to (the Shipaton video
rule is the strictest venue and we apply it everywhere); never use a real transit authority's marks;
never post a fake "review" or engagement-bait poll about the game's quality; no giveaways in exchange
for reviews (Play policy, §17).

**Decision:** X carries the award corpus, TikTok/Shorts carry reach, Reels carries the art; one capture session feeds all four.
**Evidence:** #BuildInPublic is a **P0** target at **$30k** (brief §AWARD TARGETING) and the roadmap already schedules a daily BIP post for all 56 days; the brief confirms no AI-disclosure requirement and no paid-UA restriction, so organic short-form is unconstrained.
**Action:** Claim handles D1; batch-produce the first 8 short-video concepts (§19) during the D23 capture session so launch week is never blocked on capture.
**Risk:** Daily posting across four channels collapses under launch-week firefighting.
**Fallback:** X is the only channel with a *hard* daily commitment (it is the award corpus). TikTok/Shorts/Reels drop to whatever the week allows; a batched queue of 8 pre-cut videos covers two weeks of silence without anyone noticing.

---

## 13. Product Hunt — **GO, ~Tue Sep 1, value-led**

**Decision: launch on Product Hunt on Tuesday Sep 1 2026**, roughly one week after the Play launch.

Why Sep 1 rather than launch day: PH rewards a product that already works and already has a story.
Launching PH on Aug 24 would spend the shot on a day when the app is minutes old, reviews are empty,
and the dev is watching crash dashboards. By Sep 1 we have a week of real numbers, the first
#BuildInPublic metrics post, at least one live District Cup week starting Aug 31, and the launch-week
bugs are fixed. Tuesday–Thursday are the higher-traffic PH days; Sep 1 is a Tuesday.

**The angle is value-led, not "please upvote":**
- **Tagline:** "A 60-second cat train puzzle with no forced ads — and the receipts to prove it."
- **First comment (the maker comment)** is the actual pitch: what fair-by-design meant in code — no interstitial surface exists in the binary, the paywall fires exactly once ever, the rewind sheet lists free options above paid ones, every level is solver-proven solvable free — plus the first week's honest numbers with denominators.
- **Assets:** the 30s promo video, the six screenshots, one GIF of the failure→cause→retry loop.
- **What we ask for:** feedback on the fairness model from people who build monetized products. That is a real question, and it makes the thread worth reading.

**Prep checklist:** PH profile filled out well before Sep 1; gallery assets sized for PH (not just
Play); maker comment written Aug 30 and reviewed; 6-hour presence booked in the calendar to reply to
every comment; the Play listing, press kit, and Discord invite all live and linked. No upvote
solicitation of any kind — PH penalizes it and it is the same integrity line we hold everywhere.

**Decision:** GO on Tue Sep 1, value-led, with the week-one numbers as the substance.
**Evidence:** Roadmap Week 5 (Aug 29–Sep 4) is explicitly the growth/experiments week with the first public metrics-dashboard post scheduled; brief places no restrictions on organic launch venues.
**Action:** PH profile + assets ready Aug 30; maker comment drafted Aug 30; launch 00:01 PT Sep 1; block 09:00–15:00 PT for replies.
**Risk:** A PH launch consumes a full day of solo-dev attention during the price-experiment setup week for a traffic source that skews non-gamer.
**Fallback:** If Aug 29–31 contains a live incident (crash cluster, review-bomb, Play issue), push PH to Tue Sep 8 without renegotiating anything else; PH has no deadline pressure and a distracted launch is a wasted one.

---

## 14. Press outreach — 10 named targets

Reality check first: a free indie Android puzzle from an unknown solo dev converts press at a low
single-digit rate. This list is built so that **half the targets are monetization/industry outlets**
where our actual story (a fair-monetization experiment with public numbers, built for a hackathon) is
genuinely newsworthy — that is a much better fit than "new cat game exists."

| # | Target | Type | Why they'd care (the specific hook) | Route |
|---|---|---|---|---|
| 1 | **GameDiscoverCo** (Simon Carless) | Newsletter, discoverability/indie business | A solo dev publishing full ASO + install + revenue numbers for a 56-day launch is exactly this newsletter's subject matter | Newsletter reply / site contact |
| 2 | **Mobile Dev Memo** (Eric Seufert) | Newsletter/site, mobile monetization | "Rewarded-only, no-interstitial F2P with published conversion data" is a monetization thesis with numbers attached | Site contact form |
| 3 | **Deconstructor of Fun** | Newsletter/podcast, F2P design deconstruction | The catalog design (5-point price ladder, documented subscription rejection) is a deconstruction ready-made | Site contact / LinkedIn |
| 4 | **PocketGamer.biz** | Industry trade | Shipaton participation + solo-dev launch economics; they run hackathon and indie-launch coverage | Site news tip form |
| 5 | **Pocket Gamer** (consumer) | Consumer mobile reviews | New Android puzzle with a clean hook and a review-friendly free download | Site tip form |
| 6 | **Pocket Tactics** | Consumer mobile guides/news | Strong Android puzzle coverage; "best puzzle games" list candidacy is the real prize | Site contact |
| 7 | **Droid Gamers** | Android-only games site | Android-exclusive launch is their entire beat | Site contact form |
| 8 | **GamingOnPhone** | Mobile games news | Reliable coverage of smaller Android launches | Site submission form |
| 9 | **Android Police** | Android site, weekly apps/games roundup | The weekly "best new Android apps and games" roundup is a realistic, high-value slot | Tip email listed on their contact page |
| 10 | **Indie Games Plus** | Indie coverage | Art-direction-led indie coverage; the diorama look is the hook | Site submission form |

**Do not fabricate contact addresses.** Confirm the current tip form or editorial contact on each
site the day you send; addresses rot and a bounced pitch is a wasted shot.

**Pitch discipline:** one email, ≤150 words, subject line = the hook not the product name, press kit
link (catmetro.com/press, live from roadmap D25), Play link, one embedded GIF, promo code offered
explicitly, no attachments, no follow-up before 7 days and never more than one follow-up. Send on
**Aug 25** (day after launch, so the link works) at 08:00 in the target's local morning.

**Press kit contents (D25):** 150/50/25-word descriptions, the differentiation sentence (§2), fact
sheet (price, platform, release date, dev name, engine), 6 screenshots + 3 GIFs + logo pack + icon at
1024², the 30s video, a "what makes it different" one-pager, promo codes on request, and contact.

**Decision:** Ten targets, half industry/monetization and half consumer Android, one send on Aug 25, one follow-up max.
**Evidence:** Roadmap D25 already schedules "press kit live at catmetro.com/press; 15-name outreach list"; the brief's verified market data (comp ratings vs ad load) is the substance of the pitch.
**Action:** Build the press kit Aug 25 morning; send all ten that afternoon; log responses in the BIP thread (a "0 for 10" is a legitimately good BIP post).
**Risk:** Zero coverage — the base-rate outcome.
**Fallback:** The influencer wave (§15) and Reddit (§10) are the primary traffic plan; press is upside. If nobody bites by Sep 8, the Sep 9 content patch is the second and final press moment, pitched with real numbers attached ("here's what happened in two weeks").

---

## 15. Micro-influencer strategy + 10 outreach templates

**Target profile:** 1k–50k followers, Android-first, puzzle/cozy/cat/gamedev adjacent, currently
posting (last 14 days), and — critically — someone whose audience actually installs things. Ten
well-chosen micro creators beat one 500k creator who posts once and vanishes.

**Offer, identical to everyone:** the game is free (so there is nothing to gate), plus (a) a Play
one-time promo code for `cm_all_access` so they can show the full content, (b) a named-cat cameo
through the Supporter Pack's name-a-cat feature if they want one, (c) early access to the next content
patch. **Never money for coverage without disclosure**, never a code in exchange for a positive
review, never a review-for-code trade — we ask for honest coverage or none.

**Cadence:** wave 1 (10 sends) on **Aug 25**; wave 2 (10 sends) on **Sep 8** timed to the content
patch; one follow-up each, then stop. Track in a simple CSV: handle, platform, sent date, reply,
posted, link, installs-on-that-day.

**Disclosure:** if any arrangement ever becomes an exchange of value for coverage, the creator must
disclose it per their platform's rules and the FTC's — we say so in the template itself, unprompted.

### The 10 templates (ready to send; replace `{}` only)

**T1 — Android puzzle YouTuber (email/DM)**
> Subject: A free Android puzzle with zero interstitials — 30 levels, solver-proven fair
> Hi {name} — I'm the solo dev behind Cat Metro, out on Play as of Aug 24. It's a one-thumb train puzzle: tap junctions, route cat commuters into matching stations, 45–90 seconds a level. The angle I think your audience would actually care about: there is **no interstitial, banner, or app-open ad surface in the build at all** — the only ads are rewarded ones you deliberately tap. Free to download, so nothing's gated. Happy to send an All Access code so you can show the bonus district and themes. No strings, no script, honest take welcome — including a negative one. {play_link}

**T2 — Puzzle-game TikToker (short DM)**
> Hi {name}! Solo dev here. I made a cat train puzzle where every level is 60 seconds and proven solvable without paying — no forced ads at all. It's free on Android. The failure replay ("here's exactly where your jam started") makes a really satisfying clip if that's useful to you. Code for the paid content if you want it, no obligation to post. {play_link}

**T3 — Cozy/aesthetic game creator (Instagram DM)**
> Hi {name} — I'm the developer of Cat Metro, a puzzle game built to look like a hand-made model railway of a cat city sitting on a desk. It launched free on Android this week. I thought of your feed because the whole art direction is one lighting rig, a paper-cream palette, and cardboard bevels — it photographs well. Happy to send the paid unlock so you can see both premium themes. No expectations, and an honest opinion is more useful to me than a nice one.

**T4 — Twitch variety streamer (email)**
> Subject: 10-minute segment idea: a 60-second-per-level puzzle that's actually chat-friendly
> Hi {name} — solo dev of Cat Metro (free, Android, launched Aug 24). Levels are 45–90 seconds, which makes it unusually good for chat: they can call the switch before you throw it, and when it fails the replay shows exactly who was wrong. Free download, no ads interrupting a stream (there are literally no interstitials in the build). I can send an All Access code for the bonus district. Zero requirements from me — if it doesn't fit the channel, no worries at all.

**T5 — Cat-content creator (non-gaming; DM)**
> Hi {name} — this is an odd one. I'm a solo game developer and I just released Cat Metro, a puzzle game about routing little cat commuters through a toy metro. It's free, it's genuinely cute, and there are five distinct cat silhouettes because the game has to be readable for colorblind players. If you ever post non-cat-photo content, the 15-second clips are pretty charming. Totally fine to ignore this — I'd rather ask than spam.

**T6 — Accessibility-focused creator (email)**
> Subject: Colorblind-first puzzle design — would value your critique
> Hi {name} — I'm the solo developer of Cat Metro (free on Android). Color is never the only signal in it: every line carries a color **plus** a symbol **plus** a distinct cat silhouette, tap targets are ≥48dp, motion and haptics have toggles, and there's a planning-pause mode that freezes the simulation while you think. I'd genuinely value a critical look — I'm sure I got things wrong, and I'd rather hear it from you than from a review. Code for the paid content available; happy to fix things and credit you.

**T7 — Gamedev creator / devlog channel (email)**
> Subject: 56 days, one dev, a deterministic sim, and every level validated in CI
> Hi {name} — solo dev, shipped Cat Metro to Play on Aug 24 for RevenueCat's Shipaton. The technically interesting parts: a pure-C# 8-tick/second deterministic simulation with a command log, a beam-search solver that shares the *exact* step function as the game, and a CI job that refuses to merge a level the solver can't prove solvable. I've been posting the numbers publicly the whole time — installs, retention, revenue, mistakes. Happy to hand over any of it (code excerpts, dashboards, the failures) if it's useful for a video.

**T8 — Newsletter writer (indie/mobile; email)**
> Subject: A no-forced-ads F2P experiment, with the numbers published
> Hi {name} — I'm running what is effectively a public experiment: a free mobile puzzle game with **no interstitials, no banners, no app-open ads** — rewarded-only, player-initiated — and I'm publishing conversion, retention, and revenue with denominators as it runs. It launched Aug 24 on Android. If a "does fair monetization actually work" data point is useful to you, everything I have is yours, including the parts that go badly. Play link and press kit: {press_link}

**T9 — Discord community owner (mobile/puzzle server; DM)**
> Hi {name} — I'm the solo dev of Cat Metro (free, Android, launched this week) and I want to ask before doing anything rather than after. Would a post in your self-promo channel be welcome, and are there rules I should follow? Happy to do something useful for the server instead of an ad — an AMA, a design breakdown, or a set of promo codes to give away with no conditions attached. If it's a no, that's completely fine.

**T10 — Subreddit / community moderator (mod mail)**
> Hi mods — I'm the solo developer of Cat Metro, a free Android puzzle that launched Aug 24. I've read the self-promotion rules and I want to check before posting rather than get removed: would a {post_type} post be acceptable, with the developer disclosure in the first line and the link in the body? I'm also happy to post it as a design write-up rather than a launch announcement if that fits better. Thanks either way.

**Decision:** Two waves of 10 micro-creators, identical no-strings offer, honest-take-welcome framing, one follow-up max.
**Evidence:** Roadmap D25 schedules the 15-name outreach list; the brief permits promo codes for one-time products (verified working), which is what makes the offer possible without a paid tier.
**Action:** Build the 20-name list Aug 22–23 (research is not launch-critical work, so it fits the review-wait days); send wave 1 Aug 25, wave 2 Sep 8.
**Risk:** Bulk DMs read as spam and can get accounts limited on TikTok/Instagram.
**Fallback:** Cap at 5 DMs/platform/day, always personalized in the first sentence with something specific to that creator, and prefer email where a public address exists.

---

## 16. UGC and the daily-seed challenge loop

The **Daily Line's shared seed is the growth mechanic** — a genuinely social artifact that costs no
server. Every player on the same calendar date plays a byte-identical board (liveops §1.1), so scores
are directly comparable and a screenshot is an argument.

**The loop, concretely:**
1. Player finishes the Daily Line → results screen offers **Share** (feature flag `share_card`).
2. Client renders a **1080×1350 share card** on-device: wordmark, date, score, stars, streak badge, and the player's **route ribbon** (their switch timeline drawn as a metro-map diagram — the visual signature nobody can copy without the same deterministic sim), in the equipped theme's colors. No PII, no username (none exists).
3. Card copy includes the share code grammar `CM-260824-3120` and the deep link `catmetro://daily?d=2026-08-24&b=3120` (plus `https://catmetro.io/d/260824?b=3120` once App Links are live).
4. Recipient opens the link → same board, their friend's score to beat → `challenge_opened` fires with the source tag.

**What we amplify (dev-side, weekly, ~30 min):**
- **#DailyLine repost ritual:** every day, repost the best community share card to X/IG stories with credit. Zero cost, makes sharing feel seen.
- **Saturday Express callout:** Saturdays are the hardest daily (difficultyTarget 0.75, liveops §1.2) — post "today's is brutal, show me your ribbon" with our own score as the target. A dev score to beat is the single best UGC prompt we have.
- **District Cup result cards** during Cup weeks (from Aug 31): same card, Cup framing.
- **Route-ribbon gallery:** monthly, a grid of the most beautiful/absurd ribbons. This is the Reels/IG content that requires no new capture work.

**What we do not do:** no giveaway-for-share, no "share to unlock", no reward for posting (all three
convert badly, cheapen the artifact, and edge toward the incentivized-behavior policies we avoid
elsewhere). Sharing is offered, never required, never rewarded — same principle as the ad surfaces.

**Measurement (with denominators, per the honesty rule):** `scorecard_shared / daily_completed` =
share rate; `challenge_opened / scorecard_shared` = yield per share; `first_open` with a challenge
source = installs attributable to the loop. E21 in `experiment_backlog.csv` tests the card design
(minimal vs route-trace) sequentially and warns openly that opens-per-share is a ratio of small numbers
— report it as such.

**Decision:** The share card is the UGC engine; the dev amplifies daily at near-zero cost; nothing is ever rewarded for sharing.
**Evidence:** Share-card spec and share-code grammar already locked (product_spec §share card, liveops §1.3); E21 already queued in the experiment backlog; the same-seed-worldwide property is a locked design decision.
**Action:** Ship `share_card` on in 1.0; start the daily repost ritual on Aug 24; run the Saturday Express callout every Saturday from Aug 29.
**Risk:** Share rate is near zero (the common outcome), making the loop a rounding error.
**Fallback:** Report it honestly and stop spending time on amplification after two zero weeks; the card still costs nothing to keep, and the Cup rounds give it a second at-bat in W2–W4.

---

## 17. Policy-compliant review generation

Ratings are the highest-leverage ASO asset we can influence — and the fastest way to get an app in
trouble. Every rule below is a hard constraint.

**What we do:**
1. **Google Play In-App Review API, correctly used.** Trigger conditions (locked): after a **3-star win**, `session_count >= 5`, **never after a failure**, never within a purchase or ad flow, never twice for the same user. The API is **quota-limited** and shows **no visible CTA button** — so the code must treat the prompt as best-effort, never assume it appeared, and never branch on the result (Play does not tell us).
2. **Reply to every review**, positive and negative, within 24h during launch week and within 72h after. A reply is the only public evidence of how we handle problems, and Play surfaces it. Never argue; never explain away a 1★; fix, then reply with what changed and in which version.
3. **Fix-then-ask.** A player who hits a bug is never prompted for a review. The review prompt is gated behind a *win*, deliberately.
4. **Support email published** and monitored, so an angry player has a non-public first option.
5. **Refunds handled without shaming** and with progress preserved (monetization_spec §3.14) — refund reviews are the ugliest reviews, and generosity here is cheaper than reputation repair.

**What we never do (each of these violates Play policy or our own positioning):**
- Never incentivize reviews with in-game currency, cosmetics, rewinds, or entries into anything.
- Never gate content, features, or rewards behind "rate us".
- Never use a custom "Enjoying the game? → Yes → Rate us / No → Send feedback" pre-prompt funnel that filters out unhappy users before the store prompt.
- Never solicit reviews in a push notification (locked: `review_coordination` is in-app review API **only**, never push — onesignal_retention §6.2).
- Never buy, trade, swap, or ask friends/family for ratings; never join review-exchange groups.
- Never prompt after a failure, mid-level, or inside 60s of any commerce surface.

**Realistic expectation:** at a few thousand installs, rating counts are small and volatile; a single
1★ moves the average visibly. Plan for that emotionally and operationally rather than trying to
engineer around it.

**Decision:** In-app review API on a strict win-gated trigger, universal reply discipline, zero incentivization of any kind.
**Evidence:** Brief verified 2026-07-31 — the in-app review API is quota-limited, has no visible CTA button, and must never be shown after a failure; `review_coordination` is locked to the native API in onesignal_retention §6.2.
**Action:** Implement the review flow in roadmap week 6 (Sep 5–11, already scheduled); write the review-reply macros (bug / difficulty / refund / ad-confusion / praise) during the Aug 25–26 review-wait days.
**Risk:** A review-bomb after a bad build tanks the rating during the judging window (Oct 1–13).
**Fallback:** Staged rollout with halt criteria (crash-free <99% or ANR >0.47%) is the primary containment; publicly reply to every affected review with the fix version, and if the average drops below 4.0, prioritize the fix over any growth activity that week.

---

## 18. 30-day content calendar (Aug 24 – Sep 22)

One row per day. Channels: **X** (daily, non-negotiable — the #BuildInPublic corpus), **TT** TikTok,
**YTS** YouTube Shorts, **IG** Reels, **RD** Reddit, **DC** Discord, **PH** Product Hunt, **PR** press.
"BIP #" refers to the roadmap's continuing daily build-in-public post numbering.

| # | Date | Day | Pillar | Asset | Channel | CTA |
|---|---|---|---|---|---|---|
| 1 | Aug 24 | Mon | **LAUNCH** | "It's live" post + 15s gameplay GIF + store link (BIP 24/56 continuation) | X, RD (r/AndroidGaming), DC, TT, YTS | "Free on Google Play — tell me what breaks" |
| 2 | Aug 25 | Tue | Data/transparency | Day-1 numbers with denominators (installs, D1 pending, first purchases) | X (thread), DC | "Follow the whole run — numbers posted daily" |
| 3 | Aug 26 | Wed | Craft | Short-video #2: "Watch this jam happen" failure→cause→retry | TT, YTS, IG | "Can you see it before I do?" |
| 4 | Aug 27 | Thu | Community | Reply-to-every-review post + the first bug fixed from a public report | X, DC, RD (comment threads) | "Report anything — I'm shipping fixes daily" |
| 5 | Aug 28 | Fri | Values | "There is no interstitial in this build" — code/UI proof post | X (thread), RD (r/puzzlevideogames) | "Ads only when you ask. Here's the sheet" |
| 6 | Aug 29 | Sat | UGC | **Saturday Express** callout with my own Daily score to beat | X, IG, DC | "Beat my ribbon: CM-260829-{score}" |
| 7 | Aug 30 | Sun | Craft | Diorama macro Reel (art direction, no UI) | IG, TT | "Every board is a model railway" |
| 8 | Aug 31 | Mon | **Event** | Neon Nights Cup round 1 goes live + livery reveal | X, DC, TT | "3 neon routes this week — participation livery for all 3" |
| 9 | Sep 1 | Tue | **Product Hunt** | PH launch + maker comment with week-1 numbers | PH, X, DC | "Feedback on the fairness model, not upvotes" |
| 10 | Sep 2 | Wed | Data | Week-1 retention readout vs GameAnalytics 2025 medians (D1 ~22%, vintage labeled) | X (thread) | "Here's where I'm below median and why" |
| 11 | Sep 3 | Thu | Craft | Short-video: "Solver says this is solvable in 4 switches. I need 9." | TT, YTS | "Try the level: L018" |
| 12 | Sep 4 | Fri | Build-in-public | **D35 gate readout** — retention levers chosen, publicly | X (thread), DC | "Two levers picked. Watch them work or fail" |
| 13 | Sep 5 | Sat | UGC | Saturday Express callout #2 + best community ribbon repost | X, IG, DC | "Post your ribbon" |
| 14 | Sep 6 | Sun | Values | "Why there's no subscription" (monetization_spec §5, public version) | X (thread), RD (r/gamedev comment) | "Read the reasoning, tell me I'm wrong" |
| 15 | Sep 7 | Mon | **Event** | Commuter Rescue Cup round 2 live; wildcard-commuter explainer | X, DC, TT | "Wildcards are slack — here's how to spend them" |
| 16 | Sep 8 | Tue | Outreach | Influencer wave 2 (10 sends) + press follow-up; publish the response rate | X, PR, DM | "0 for 10 last time. Trying again in public" |
| 17 | Sep 9 | Wed | **Content patch** | v1.1: levels 36–40 + depot-pass streak repair (levels 31–35 shipped in the Week-5 build) | X, DC, TT, YTS, PR | "Five new levels, one new mechanic. Free." |
| 18 | Sep 10 | Thu | Craft | Cooldown-mechanic teaching clip (how the new lock reads) | TT, YTS, IG | "New rule: switches lock after you throw them" |
| 19 | Sep 11 | Fri | Data | **D42 content-complete gate** + pricing A/B interim readout (HAMM evidence) | X (thread) | "$6.99 vs $4.99 — here's the interim, with n" |
| 20 | Sep 12 | Sat | UGC | Saturday Express #3; route-ribbon gallery grid | IG, X, DC | "Nine ribbons, one board, one day" |
| 21 | Sep 13 | Sun | Craft | "How the Daily Line works with no backend" (seed derivation explainer) | X (thread), RD (r/gamedev) | "Same board worldwide, zero servers" |
| 22 | Sep 14 | Mon | **Event** | District Cup Championship (remix routes) + share-code push | X, DC, TT | "Beat my Cup score" |
| 23 | Sep 15 | Tue | Values | Catvertising story: opt-in rates and decline rates, published | X (thread) | "Nobody is forced. Here's what they chose" |
| 24 | Sep 16 | Wed | Craft | Short-video: 6 districts in 20 seconds (art-direction montage) | TT, YTS, IG | "Six districts, one desk" |
| 25 | Sep 17 | Thu | Build-in-public | OneSignal write-up: 13 touchpoints into 3 journeys on a $19 plan | X (thread), DC | "Resourcefulness, literally" |
| 26 | Sep 18 | Fri | Data | Mid-window revenue + conversion post, denominators included | X (thread) | "Every number has an n next to it" |
| 27 | Sep 19 | Sat | UGC | Saturday Express #4 + community shout-outs | X, IG, DC | "Post your ribbon" |
| 28 | Sep 20 | Sun | Craft | "The failure camera" design post (why we show the cause first) | X, IG, RD (r/puzzlevideogames) | "Losing should teach something" |
| 29 | Sep 21 | Mon | **Event** | Founding Riders wrap week opens (play any 3 days → badge) | X, DC, TT | "Ten days. Three plays. One badge." |
| 30 | Sep 22 | Tue | Submission | Devpost submission teaser + the 2-minute demo video published | X, YTS, DC, PH comment | "Here's the whole 56 days in two minutes" |

Weekly rhythm baked into the table: **Mon = event/product beat, Tue = data or outreach, Wed = craft,
Thu = teach, Fri = numbers/gate, Sat = UGC, Sun = essay.** If a day collapses, the pillar tells you
what to substitute from the pre-cut queue. The Sunday essay doubles as the **weekly long-form recap,
republished on HackerNoon tagged #shipaton** — HackerNoon republication feeds an extra $2,500 BIP
prize pool (official judging guide, Aug 1) — and the weekly numbers beat is cross-posted to the
**r/AppBusiness Shipaton check-in threads with the Shipaton flair**.

**Decision:** 30 days, one pillar per day, X every single day, a weekly repeating rhythm so no day starts from a blank page.
**Evidence:** Roadmap already commits to daily BIP posts through 56/56 and schedules the Sep 9 patch, the Cup rounds (liveops §3), the D35/D42 gates, and the Sep 22 submission work; #BuildInPublic is a **P0** $30k target.
**Action:** Pre-write rows 1–7 during the Aug 25–26 review-wait days; pre-cut 8 short videos in the D23 capture session; keep the calendar in the repo as `/marketing/calendar.md` and check off publicly.
**Risk:** Launch-week incidents eat the calendar in the first five days — exactly when it matters most.
**Fallback:** Rows 1–7 are pre-written and pre-cut before launch day; if a fire starts, **posting the fire is the content** (an honest incident post outperforms a polished feature post in the BIP corpus, and it is true).

---

## 19. 15 short-video concepts (shot-by-shot)

All 9:16, 1080×1920, captured per §23. Text-on-screen always; audio optional. Target 12–25s unless noted.

1. **"One tap changes everything" (12s).** B1 0–2s: static board, four cats inbound, timer ticking. B2 2–5s: thumb enters, throws one switch, the whole flow re-routes. B3 5–9s: three deliveries in a row, purr-meter chain climbing. B4 9–12s: win stamp, 3 stars. Text: `ONE SWITCH` → `EVERY CAT HOME`.
2. **"Watch this jam happen" (18s).** B1 0–3s: clean board. B2 3–8s: subtle over-commit to one line. B3 8–11s: queue fills, Overload ring appears, fail. B4 11–15s: cause-first camera snaps to the culprit platform, replay scrub. B5 15–18s: instant retry, clean solve. Text: `SPOT IT BEFORE I DID`.
3. **"60 seconds, start to finish" (25s).** Single unbroken real-time level, no cuts, timer visible. Text: `NO CUTS. NO EDITS. ONE LEVEL.` The proof-of-claim video.
4. **"A model railway of a cat city" (15s).** B1: macro on the cardboard bevel and desk props. B2: slow pull-back revealing the whole diorama. B3: a train enters frame at scale. B4: theme swap cream→sakura in one dissolve. Text: `IT'S A DESK. IT'S A CITY.`
5. **"Ads only when you ask" (16s).** B1 0–4s: play a level, fail, rewind sheet opens. B2 4–9s: finger scrolls slowly past free/owned/rewarded rows. B3 9–13s: hovers over the pack rows below the divider, then taps the **free** one. B4 13–16s: "Every level is solvable without rewinds" footer, held. Text: `FREE OPTIONS FIRST. ALWAYS.`
6. **"Five cats, five silhouettes" (14s).** B1: all five cat types line up. B2: colorblind simulation filter sweeps across the screen (deutan). B3: they remain distinguishable — symbols and silhouettes intact. Text: `COLOR IS NEVER THE ONLY SIGNAL.`
7. **"The solver plays it first" (20s).** B1: editor view, solver runner executing on L018 at beam width 2.5k. B2: solution overlay draws the switch timeline. B3: cut to the real game, same level, my human attempt. B4: I need more switches than the solver did. Text: `IT CANNOT SHIP UNTIL A SOLVER PROVES IT.`
8. **"Same board, everywhere" (15s).** B1: split-screen two devices, both showing the same Daily Line date. B2: identical boards confirmed. B3: two different route ribbons on the share cards. Text: `ONE SEED. ONE WORLD. NO SERVER.`
9. **"Saturday Express" (12s).** B1: the date header reads Saturday. B2: fast-cut of the harder board, denser waves. B3: my score card. Text: `SATURDAYS ARE MEANER. BEAT THIS.`
10. **"What $6.99 buys" (20s).** B1: the Night Harbor district tile unlocking. B2: quick pan through the 10 bonus levels' names. B3: both themes swapping. B4: the ad-free guarantee line. Text: `ONE PURCHASE. NOT A SUBSCRIPTION.` (No price numeral on screen — the store shows it.)
11. **"Building it in public, day {n}" (20s).** Format template: B1 today's Unity editor screen. B2 the thing that broke. B3 the fix. B4 today's number. Text: `DAY {n} OF 56`.
12. **"Planning pause" (14s).** B1: board at max pressure. B2: hold anywhere → sim freezes, switches still tappable. B3: 3-2-1 countdown, resume, clean solve. Text: `HOLD TO THINK. IT'S NOT CHEATING.`
13. **"The wildcard cat" (16s).** B1: scruffy bent-ear cat spawns among colored ones. B2: it gets accepted at the "wrong" station. B3: the plan it just rescued. Text: `THE WILDCARD IS YOUR SLACK.`
14. **"Rejected" (12s).** B1: a cat arrives at the wrong station. B2: sits on the platform looking confused for 8 ticks. B3: rides back up the line. B4: score −25, chain broken. Text: `HE IS NOT SORRY.` (Pure charm; the highest-ceiling cat-shelf clip.)
15. **"Two weeks of numbers" (25s).** Screen-recorded scroll through the RevenueCat dashboard, Play Console vitals, and OneSignal delivery stats, with each denominator called out on screen. Text: `EVERY NUMBER HAS AN N.` (The #BuildInPublic flagship clip.)

**Decision:** Fifteen concepts, eight pre-cut before launch, seven produced live from real events.
**Evidence:** Every concept is capturable from systems already specced and shipped (cause-first camera, planning pause, wildcard, solver runner, share card, rewind sheet); no concept requires art or code that does not exist.
**Action:** Cut concepts 1–5, 9, 11-template, and 14 in the D23 capture session; produce the rest weekly per §18.
**Risk:** Vertical capture of a portrait game is easy, but *interesting* capture requires good play — bad runs make bad clips.
**Fallback:** All capture is replay-driven (architecture.md Capture scene), so a good run can be re-rendered cleanly at any time from its command log rather than re-performed.

---

## 20. 10 six-second hooks

For TikTok/Shorts/Reels cold opens. Text-on-screen in the first frame, spoken or silent.

1. "This is a puzzle game where the ads only exist if you ask for one."
2. "Every level in this game has been proven solvable by a computer before you ever see it."
3. "Sixty seconds. Nine cats. One switch. Go."
4. "I built a model railway of a cat city and then made it a puzzle."
5. "Watch me lose this in three seconds and then show you exactly why."
6. "There is no interstitial in this build. Not one. Let me prove it."
7. "Everyone on Earth is playing this exact board today. No server involved."
8. "This cat went to the wrong station and he is not sorry about it."
9. "Day {n} of building a game in public. Today something broke."
10. "The solver says four switches. I need nine. I made this game."

**Decision:** Hooks lead with a claim, a number, or a loss — never with a logo or a title card.
**Evidence:** Every hook maps to a verified fact or a shipped system; nothing here is a claim we cannot show on screen within the same clip.
**Action:** Each of the 15 concepts in §19 pairs to a hook; script the pairing into the capture shot-list.
**Risk:** Claim-led hooks invite "prove it" comments.
**Fallback:** That is the desired outcome — the proof is the second half of the video, and the comment section is free distribution. Keep the proof in-clip so no reply is needed.

---

## 21. 10 #BuildInPublic post templates (P0 — the $30k award)

The #BuildInPublic award is **P0 at $30k/$20k/$10k** (brief). The roadmap already commits to a post
every day for all 56 days; these templates make that survivable. **The moat is honesty with
denominators** — polished growth-hacker posts are indistinguishable from everyone else's; a public
failure with real numbers is not.

Note: the locked brief records the #BuildInPublic prize tiers but does **not** capture verbatim judging
criteria for this award. Re-verify the criteria text on the official award page before writing the
final Devpost paragraph (tracked as a submission-checklist item in `submission_script.md`).

**BIP post 1/56 (Day 1, Aug 1) — seeded draft: the pre-registration post (AMD-02).** Post 1 does double
duty: it announces the build AND publicly pre-registers the D7 fun-gate bar before any data exists —
pre-registration is the honesty moat applied to ourselves. Draft:

> Day 1/56 of building Cat Metro for #Shipaton. Before a single tester touches it, here is the Day-7
> fun-gate bar we will grade ourselves against in public (12 closed testers, pushes disabled):
> (i) ≥6/12 testers open the app unprompted on a second calendar day during D5–D7;
> (ii) ≥4/12 replay an already-won level (excludes fail-retries by construction);
> (iii) median session ≥3 levels;
> (iv) quit-without-retry after failure <50%.
> Fail rule, pre-committed: YELLOW (2 of 4 metrics missed) = 48h mechanic surgery + re-gate D9; RED (3+ of 4, or metric (i) alone) = execute the Plan-B runbook (PLAN_B_RUNBOOK.md).
> A named outside person confirms the tally before the gate decision is written. Results on Day 7, pass or fail.

**BIP-1 — Daily standup**
> Day {n}/56 of building Cat Metro for #Shipaton.
> Today: {one thing shipped}
> Broke: {one thing that broke}
> Number: {one metric, with its denominator}
> Tomorrow: {one thing}
> {screenshot or 5s clip}

**BIP-2 — The gate post (D7/D14/D21/D28/D35/D42)**
> {GATE NAME} gate. The bar was: {exact criteria}.
> Result: {PASS/FAIL}, {number} of {denominator}.
> What I'm doing about it: {decision}.
> Gates only work if you publish the ones you fail. Here's the decision log: {ADR link}

**BIP-3 — The failure post**
> This did not work.
> {What I tried} → {what happened} → {the number}.
> My theory about why: {hypothesis}.
> What I'm changing: {change}.
> If you've solved this, I'd genuinely like to hear it.

**BIP-4 — The numbers post (weekly, Fridays)**
> Week {n} of Cat Metro, all numbers with denominators:
> Installs: {n} · D1: {x}% of {n} · Paywall views: {n} · Purchases: {n} · Revenue: ${x}
> Context: GameAnalytics 2025 medians are D1 ~22% / D7 ~4% (2025 data — the widely-quoted 31%/12% puzzle numbers are from 2022 and outdated).
> Where I'm below median: {honest answer}.

**BIP-5 — The technical deep-dive (weekly, Sundays)**
> How {system} works in Cat Metro, in {n} tweets.
> 1/ The problem: {constraint}
> 2/ The naive approach and why it fails: {why}
> 3/ What I did instead: {approach}
> 4/ The bit I got wrong first: {mistake}
> 5/ Code/diagram: {artifact}

**BIP-6 — The decision post**
> Decision: {what}. Rejected: {alternative}.
> Why: {reason with evidence}.
> What would change my mind: {falsifiable condition}.
> Logged as ADR-{nnnn}. I'll report back on {date} whether it was right.

**BIP-7 — The receipts post**
> People ask if "no forced ads" is real. Here is the proof:
> {screenshot of the rewind sheet with free options above the divider}
> There is no interstitial, banner, or app-open ad surface in the build. Not throttled — absent.
> Opt-in rate on the surfaces that do exist: {x}% of {n} offers.

**BIP-8 — The ask post (use sparingly, max 1/week)**
> I need {specific thing}: {one sentence of context}.
> What I've already tried: {list}.
> If you've done this, {specific question}?
> {No link, no pitch — the ask is the whole post.}

**BIP-9 — The milestone post**
> {Milestone} 🎉 — {number} {unit}.
> The unglamorous version: it took {n} days, {n} rejected approaches, and {n} hours of {specific pain}.
> Thank you specifically to {names of the 12 testers / community}.
> Next milestone: {next} by {date}.

**BIP-10 — The retro thread (end of window)**
> 56 days, one dev, one game. The full retro:
> Shipped: {list} · Cut: {list} · Revenue: ${x} from {n} payers of {n} installs
> What I'd do again: {2 things}
> What I'd never do again: {2 things}
> Everything I published along the way: {index link}

**Decision:** Ten reusable shapes, one post every day, numbers always carry denominators, failures get posted at the same volume as wins.
**Evidence:** #BuildInPublic is P0 at $30k (brief §AWARD TARGETING); roadmap commits BIP posts 1/56 → 56/56 with gate readouts explicitly called "prime BIP content"; the brief's benchmark-honesty rule (label vintages, no credible puzzle ARPDAU/opt-in benchmarks exist) is exactly the discipline that makes these posts credible.
**Action:** Post daily from Aug 1 (already running by launch); maintain a public index page at catmetro.com/build so the whole corpus is one link for judges; screenshot-archive every post (accounts can break).
**Risk:** The corpus is judged as a body of work — a two-week gap during launch firefighting is visible and damaging.
**Fallback:** BIP-1 (daily standup) takes 4 minutes and works on the worst day of the project; on a truly lost day, post BIP-3 (the failure post) about the fire itself. There is no acceptable day with zero posts.

---

## 22. Press angles (5) and Devpost narrative hooks (5)

### Five press angles

1. **"The no-interstitial experiment."** A free mobile puzzle ships with *zero* forced-ad surfaces in the binary and publishes conversion, retention and revenue with denominators. Verified context: Arrows – Puzzle Escape hit 103.6M installs in 12 months but carries "ad every other level" backlash; Bus Traffic Fever sits at 3.72★ with forced 30s ads. Hook: does fairness pay, in numbers?
2. **"The empty shelf."** Verified 2026-07-31: there is no cat-themed metro or route-switching puzzle on Google Play. Trainyard delisted in 2019, Mini Motorways never came to Android, STATIONflow and Overcrowd are PC-only. A whole genre-crossing niche was vacant on the world's largest game platform.
3. **"Every level is proven solvable before it ships."** A beam-search solver shares the exact simulation step function with the game, and CI refuses to merge a level it cannot solve. The paywall's "every level is solvable without paying" line is a build gate, not a marketing claim.
4. **"56 days, solo, in public."** One developer, a published daily log including the failures, six hard gates with pre-written kill criteria, and a public retro. Built for RevenueCat Shipaton 2026 with a launch inside a 60-day window.
5. **"$19/month enterprise behavior."** A 13-touchpoint retention design compressed into 3 journeys and 6 message steps, with frequency capping and quiet hours rebuilt client-side because the plan does not include them. A resourcefulness story for anyone building on a budget.

### Five Devpost narrative hooks

1. **"Fair by design was a constraint, not a slogan."** We wrote the rule first — no forced ads, no energy, no loot boxes, every level solvable free — and then found out what it cost. Every product decision in the submission traces to that line.
2. **"The whitespace was real."** Four Play searches on 2026-07-31 found no cat-themed route-switching puzzle. We built the missing game and can show the search receipts.
3. **"We monetized failure without monetizing frustration."** No offer ever appears after a first failure. The rewind sheet is player-opened and lists free options above paid ones. The footer says the level is solvable without rewinds — and a solver in CI makes that true.
4. **"Three journeys, six message steps, one $19 plan."** The retention system was designed *around* plan limits rather than despite them, with frequency caps and quiet hours rebuilt in our own code and a local-notification failover when push dies.
5. **"We published the numbers, including the bad ones."** Denominators on every rate, vintages on every benchmark, gate failures posted the same day they happened. The submission is auditable because the build log already was.

**Decision:** Five press angles and five Devpost hooks, all anchored to verifiable artifacts rather than adjectives.
**Evidence:** Each hook maps to a locked, dated fact in the brief or a shipped system in the sibling specs.
**Action:** Press angles into the press kit (D25); Devpost hooks into the submission draft during roadmap week 7 (Sep 12–18) — full treatment in `submission_script.md`.
**Risk:** Angles 1 and 5 both invite scrutiny of numbers that may be small or unflattering.
**Fallback:** Publish them anyway with denominators attached. Small honest numbers are a defensible story; inflated numbers are an unrecoverable one, and the Grand Prize shortlist reads revenue straight from RevenueCat regardless of what we say.

---

## 23. Low-cost vertical capture workflow

Two capture paths, one output spec: **1080×1920, 60fps source, H.264 High, no watermark, original
audio only.** Everything is replay-driven so any take can be re-rendered from its command log rather
than re-performed (architecture.md: the `Capture` scene is a dev-only replay-driven portrait rig).

### 23.1 Path A — Unity Recorder 5.1.6 in-Editor (primary; highest quality)

Setup once (`Window → General → Recorder → Recorder Window`):

| Setting | Value | Why |
|---|---|---|
| Recorder type | **Movie** | Single-file output; use *Image Sequence* only for the 1179×2556 still (below) |
| Source | **Game View** (with the Game view aspect set to a custom **1080×1920** resolution) | Guarantees exact portrait framing; avoids letterboxing |
| Output Resolution | **Custom → W 1080 × H 1920** | The locked deliverable size; Unity Recorder 5.1.6 records 1080×1920 in Editor (verified 2026-07-31) |
| Aspect Ratio | 9:16 | — |
| Frame Rate | **Constant 60** FPS | Slow-mo headroom and clean 30fps downsample |
| Cap FPS | **On** (Playback: Variable → Constant) | Recorder drives the clock, so a slow render never drops frames from the *output* |
| Encoder | H.264 (built-in), Quality **High** | Universally re-encodable by every platform |
| Include Audio | On | Original game audio only |
| Capture Alpha | Off | No alpha in delivery formats |
| Recording Mode | **Frame Interval** (e.g. 0–1800 for a 30s take at 60fps) | Deterministic, repeatable takes from a replay |
| Output path | `Recordings/<yyyyMMdd>/cm_<concept>_take<NN>.mp4` | Sortable, traceable to a shot-list row |

For the **1179×2556 frameless submission screenshot** (rules-mandated): same Recorder window, add an
**Image Sequence** recorder, source Game View at custom **1179×2556**, format **PNG**, capture a single
frame. Render at that resolution — never upscale a 1080-wide capture.

Editor checklist before any take: Quality tier set to the *mid* profile (what most players see), Game
view at exactly 1080×1920, dev overlays and the FPS counter off, Gizmos off, `daily_overrides` on a
known date so the board is reproducible, and the replay command log loaded so the run is identical
every time.

### 23.2 Path B — on-device capture via adb (for authenticity: real device, real touches)

```bash
# 0) confirm the device and its real screen size
adb devices -l
adb shell wm size

# 1) record on device (screenrecord caps at 3 minutes per file)
adb shell screenrecord \
  --bit-rate 16000000 \
  --time-limit 180 \
  /sdcard/cm_raw_take01.mp4
#    ...play the take, then Ctrl-C to stop early; wait ~2s before pulling so the file finalizes

# 2) pull and clean up
adb pull /sdcard/cm_raw_take01.mp4 ./captures/cm_raw_take01.mp4
adb shell rm /sdcard/cm_raw_take01.mp4

# optional: show touches while recording (visualizes the one-thumb claim)
adb shell settings put system show_touches 1
# ...record...
adb shell settings put system show_touches 0
```

Do **not** pass `--size 1080x1920` unless the device is natively 9:16 — on a 20:9 phone it letterboxes.
Record native, then normalize in ffmpeg (below). Turn on Do Not Disturb before recording so no
notification banner lands in the take.

### 23.3 ffmpeg normalization and delivery

```bash
# A) normalize ANY source to exactly 1080x1920 (center-crop, never stretch)
ffmpeg -i captures/cm_raw_take01.mp4 \
  -vf "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920" \
  -r 30 -c:v libx264 -preset slow -crf 19 -pix_fmt yuv420p \
  -c:a aac -b:a 128k -movflags +faststart \
  out/cm_take01_1080x1920.mp4

# B) trim to a hook window (fast, stream-copy; keyframe-accurate enough for social)
ffmpeg -ss 00:00:04.5 -to 00:00:22.0 -i out/cm_take01_1080x1920.mp4 \
  -c copy out/cm_take01_trim.mp4

# C) burn a caption band (top 22%), Warm Paper on Ink Navy, no external assets
ffmpeg -i out/cm_take01_trim.mp4 -vf \
"drawbox=x=0:y=0:w=1080:h=422:color=0x22304A@0.92:t=fill,\
drawtext=fontfile=/System/Library/Fonts/Supplemental/Arial\\ Bold.ttf:\
text='TAP THE SWITCH. SEND EVERY CAT HOME.':fontcolor=0xFAF6EC:fontsize=58:\
x=(w-text_w)/2:y=180:line_spacing=12" \
  -c:v libx264 -crf 19 -preset slow -pix_fmt yuv420p -c:a copy \
  out/cm_take01_captioned.mp4

# D) concatenate multi-shot edits (all clips normalized by step A first)
printf "file '%s'\n" out/shot1.mp4 out/shot2.mp4 out/shot3.mp4 > out/list.txt
ffmpeg -f concat -safe 0 -i out/list.txt -c copy out/cm_concept02.mp4

# E) platform deliveries from one master
#   TikTok / Reels / Shorts (<=60s, 1080x1920, H.264, AAC)
ffmpeg -i out/cm_concept02.mp4 -t 60 -c:v libx264 -crf 20 -preset slow \
  -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart out/deliver_tiktok.mp4
#   X (smaller ceiling; keep it lean)
ffmpeg -i out/cm_concept02.mp4 -c:v libx264 -crf 23 -preset slow -maxrate 5M -bufsize 10M \
  -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart out/deliver_x.mp4

# F) high-quality GIF for Reddit/press (two-pass palette; 480px wide keeps it under typical limits)
ffmpeg -i out/cm_take01_trim.mp4 -vf "fps=20,scale=480:-1:flags=lanczos,palettegen=stats_mode=diff" \
  -y out/palette.png
ffmpeg -i out/cm_take01_trim.mp4 -i out/palette.png -lavfi \
  "fps=20,scale=480:-1:flags=lanczos[v];[v][1:v]paletteuse=dither=bayer:bayer_scale=3" \
  -y out/cm_take01.gif

# G) pull a still frame for press/screenshots (exact frame, no re-encode artifacts)
ffmpeg -ss 00:00:09.2 -i out/cm_take01_1080x1920.mp4 -frames:v 1 -q:v 1 out/still_09s.png

# H) verify what you're about to publish
ffprobe -v error -show_entries stream=width,height,r_frame_rate,codec_name,duration \
  -of default=noprint_wrappers=1 out/deliver_tiktok.mp4
```

**Cost:** $0. Unity Recorder is free with Unity Personal (free under $200k revenue, per the brief),
adb ships with the Android SDK, ffmpeg is free. No stock footage, no music licensing, no editor
subscription. **No third-party music, ever** — the Shipaton rules ban third-party trademarks and music
in the submission video, and running one audio policy everywhere means no asset is ever venue-locked.

**Decision:** Unity Recorder for hero/clean takes, adb for authentic on-device takes, ffmpeg for all normalization and delivery; one master per concept, platform variants derived, never re-shot.
**Evidence:** Verified 2026-07-31 — Unity Recorder 5.1.6 records 1080×1920 in Editor; Unity Personal free under $200k; submission rules ban third-party trademarks and music; roadmap D23 already banks Unity Recorder takes for the sub-2-minute video.
**Action:** Build the Capture scene shot-list during roadmap D23 (Aug 23) and bank raw takes for concepts 1–5, 9, 11, 14 in that one session; store masters in `/marketing/masters/` with the shot-list row id in the filename.
**Risk:** Editor captures look subtly "wrong" (dev overlays, wrong quality tier, non-representative framerate) and reviewers notice.
**Fallback:** Every hero clip is re-verifiable on-device via Path B; if an Editor take and a device take disagree visually, publish the device take — authenticity beats fidelity for this audience, and it removes any "that's not what the game looks like" reply.
