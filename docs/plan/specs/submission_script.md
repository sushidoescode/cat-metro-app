# Devpost Submission Script — Cat Metro (RevenueCat Shipaton 2026)

Status: v1.0, 31 Jul 2026. Governed by `deliverables/DECISIONS_BRIEF.md` (locked 31 Jul 2026).
Siblings honored: `specs/monetization_spec.md`, `specs/onesignal_retention.md`, `specs/liveops_spec.md`,
`specs/product_spec.md`, `specs/growth_aso_plan.md`, `data/roadmap_56_days.csv`.

**Window facts (Official Rules fetched and verified 2026-07-31):** Submission Period Jul 31 2026
8:00am PDT – **Sep 30 2026 11:45pm PDT**. Judging Oct 1–13. Winners Oct 21 (the FAQ says Oct 22 — the
rules prevail; **re-verify before submitting**). First public store release must occur inside the
window. App must be accessible from the USA. Submission requires: description, a **<2 min**
YouTube/Vimeo video with **no third-party trademarks or music**, store URL, **1024×1024** icon,
**1179×2556 frameless** screenshot, and a free trial **or** promo code for judges. **Judges may judge
from text, images, and video alone** — which is the single most important operational fact in this
document: the written submission and the evidence pack must stand entirely on their own.

Award slate (brief §AWARD TARGETING): **P0** Best Game, HAMM, #BuildInPublic, OneSignal, Catvertising.
**P1** Design, Grand Prize. **P2** Stripe Funnel Vision (D35 gate), Samsung Galaxy (non-cash, only if
trivial). **Skip** Replit, JetBrains, influencer categories, Peace Prize. Multi-category entry is
allowed and no one-prize-per-project cap was found (verified 2026-07-31).

---

## 1. The Story-Circle product narrative

Dan Harmon's story circle, applied to the *product* (the player is the protagonist; the developer is
the second voice). This is the skeleton for the Devpost "Story"/"Inspiration" fields, the video VO
(§6), and every press/BIP long-form post.

| Beat | Story-circle step | Cat Metro's content |
|---|---|---|
| 1 | **YOU** (a character in a zone of comfort) | A person who likes puzzle games and installs a new one on the bus. They know the ritual: install, play two levels, watch an ad, play one level, watch an ad. |
| 2 | **NEED** (they want something) | They want the thing the genre used to be: a clean idea, a fair fight, sixty seconds at a time. |
| 3 | **GO** (they enter an unfamiliar situation) | They open Cat Metro. Level one is a single switch and two cats. Nothing asks them for anything. There is no interstitial after level two, or three, or ten. |
| 4 | **SEARCH** (they adapt) | The board gets harder honestly: queues as buffers, a second source, a wildcard commuter. They fail. The camera shows them *why* — the platform that overflowed, and the moment it started. Retry is under a second. |
| 5 | **FIND** (they get what they wanted) | Somewhere around level twelve the game clicks: they stop reacting and start planning a wave ahead. The Daily Line unlocks and the habit forms — the same board as everyone on Earth, from a shared seed, with no server. |
| 6 | **TAKE** (they pay a price for it) | The one moment the game asks: after level five, once, ever, a celebratory paywall for All Access. It closes in one tap. If they fail a level twice and open the rewind sheet themselves, the free options sit above the paid ones, under a line that says the level is solvable without them. That line is enforced by a solver in CI. |
| 7 | **RETURN** (they come back changed) | They come back tomorrow because a notification arrived at 10am that they opted into after their first daily — not because a streak was held hostage. Streaks never gate content — a break costs at most 150 tickets of gift escalation, and a free saver exists. The lapse ladder ends with "no more reminders after this," and keeps that promise with a tag. |
| 8 | **CHANGE** (they are different now) | They tell someone. Usually with a share card: a route ribbon of their own switch timeline on today's board, which nobody can fake without the same deterministic simulation. |

**The developer's half of the circle (the #BuildInPublic spine):** comfort (a solo dev with an idea) →
need (ship something real inside a 60-day window) → go (56-day roadmap with six hard gates) → search
(EDM4U conflicts, three open RevenueCat Paywalls-v2 Android crash issues, a $19/mo plan with a
3-journey ceiling) → find (constraints became the design: rewarded-only ads, one paywall moment,
journeys that self-silence) → take (a public failure log, gate readouts posted the day they failed) →
return (launch inside the window, numbers published with denominators) → change (a finished game and a
build log anyone can audit).

**Devpost "Story" field — 300-word prose version (ready to paste):**

> Every free puzzle game on my phone monetizes the same thing: my patience. Two levels, an ad. One level, an ad. The genre I loved got very good at making me wait.
>
> Cat Metro is what happens if you write the rule first and then find out what it costs. The rule: no forced ads, no energy, no loot boxes, every level solvable free. Then eight weeks to make a real game inside it.
>
> You tap junctions to route cat commuters into matching stations before the platforms overflow. Levels run 45 to 90 seconds. Thirty of them across six districts, plus a Daily Line that generates the same board for every player on Earth from a shared seed, with no server involved.
>
> The rule shaped everything. There is no interstitial, banner, or app-open ad surface in the build — not throttled, absent. Ads exist in five places and every one of them is a thing the player asked for: an extra rewind, double tickets, a three-level test drive of a theme. The paywall fires once, ever, after level five. It closes in one tap. Nothing is ever offered after a first failure — that rule is enforced by a unit test.
>
> And the claim on the rewind sheet — "every level is solvable without rewinds" — is not marketing. A beam-search solver that shares the exact simulation step function runs in CI and refuses to merge a level it cannot prove solvable.
>
> I built it solo in 56 days and posted the numbers the entire way, including the gates I failed and the weeks I was below the retention median. Denominators on every rate. Vintages on every benchmark.
>
> It is free on Google Play. The whole shop lives in one tab you have to open yourself.

**Decision:** One narrative skeleton feeds every field, every award paragraph, and the video; the developer arc is the #BuildInPublic story and the player arc is the Best Game / Design story.
**Evidence:** Every beat maps to a shipped, locked system (paywall exposure rules, solver-in-CI, seeded Daily Line, streaks that never gate content, lapse-ladder final message) documented in the sibling specs.
**Action:** Paste the 300-word version into the Devpost Story field during roadmap week 7 (Sep 12–18); reuse beats 3–6 as the video's middle section.
**Risk:** A narrative this tidy reads as marketing if no evidence sits beside it.
**Fallback:** Every claim in the prose has a screenshot in §5's shot-list; if a claim ever loses its exhibit, the claim comes out of the prose, not the other way around.

---

## 2. Award-by-award positioning

Each subsection quotes the criteria **verbatim as recorded in the locked brief** (all verified
2026-07-31), then maps shipped evidence. Where the brief records a prize but did **not** capture
verbatim criteria text, that is stated explicitly and flagged for re-verification rather than invented.

### 2.1 Best Game (P0) — $15k / $10k / $5k

**Verbatim criteria (brief §VERIFIED EVENT FACTS):** *"great gameplay, art direction, and a monetization
fit that suits the genre"* / *"fun and engaging… unique gameplay experience, progression, or
replayability… How is the game monetized?"*

**Paragraph (submission-ready):**
> Cat Metro is a deterministic route-switching puzzle: tap junctions to send color- and symbol-coded cat commuters into matching stations before platforms overflow, 45–90 seconds a level. **Great gameplay:** one verb (tap), a pure-C# simulation at 8 ticks per second with a command log, and a beam-search solver sharing the exact step function that proves every level solvable before CI will merge it — the difficulty is authored, measured, and honest. **Art direction:** a premium tabletop diorama — a hand-built model railway of a cat city on a desk — rendered with one lighting rig and one toon shader family, on a locked twelve-color palette where line color is never the only signal (color + symbol + cat silhouette, so the board reads for colorblind players). **Unique gameplay experience, progression, or replayability:** 30 handcrafted levels across 6 districts, a bonus district behind All Access, and a Daily Line that derives the same board for every player on Earth from a shared seed with no server — replayability that costs nothing to run and produces a shareable route-ribbon artifact per player. **How is the game monetized — the fit:** free, with a single $6.99 complete-edition purchase, two $2.99 cosmetic themes, two consumable rewind packs, a shop-only $9.99 supporter tip jar, and five player-initiated rewarded surfaces. No interstitials, no banners, no energy, no loot boxes, no subscription. For a calm 60-second puzzle, that fit is the point: the genre's verified poles are all one-time purchases (Mini Metro $0.99 at 4.63★, Railbound $4.99, Neko Atsume IAP ≤$3.49 at 4.78★ — all verified 2026-07-31), and the genre's verified failure mode is forced interruption (Bus Traffic Fever 3.72★ under forced 30s ads).

**Exhibits:** gameplay video (§6), screenshots 1–3 and 6 (`growth_aso_plan.md` §5), the 30-level
progression table with difficulty and first-attempt targets, the solver CI job, the palette and
colorblind-simulation pass.

### 2.2 HAMM — Hard As a Motherfather Monetizer (P0) — $15k / $10k / $5k

**Verbatim criteria (brief):** *"smartest use of RevenueCat to drive real revenue… well-crafted
paywall, thoughtful pricing and packaging, strong conversion"*.

**Paragraph (submission-ready):**
> **Well-crafted paywall:** the flagship post-level-5 paywall renders through RevenueCat Paywalls v2 (RevenueCatUI), device-tested against three open Android crash issues (#745/#736/#732) with a pixel-matched custom Unity fallback behind a feature flag — we show the risk management, not just the happy path. It fires **once per install, ever**, closes in one tap, has no countdown, nothing preselected, an equal-weight decline button, and a trust line on the paywall itself. The hard case is the rewind sheet: it monetizes failure without monetizing frustration — no offer *ever* appears after a first failure (enforced by a unit test on the eligibility service), the sheet only opens when the player taps the rewind chip, free and rewarded options sit above the divider, and the footer states the level is solvable without rewinds — which a solver proves in CI. **Thoughtful pricing and packaging:** a five-point ladder where every price does a different job — $1.99 small consumable, $2.99 cosmetic, $4.99 large consumable, $6.99 complete edition, $9.99 tip jar — plus two documented *negative* decisions: the theme bundle was cut on decoy-confusion grounds (All Access **is** the bundle), and subscriptions were formally rejected with a written record, because a solo dev cannot honestly sustain a recurring content promise in eight weeks. All Access was raised from $4.99 to $6.99: raised because (a) downside is bounded (~$40 net base-case) with a 28.6% conversion-loss cushion, (b) $4.99 breaks the ladder — the everything-tier would price below its own two themes ($5.98) and tie cm_rewind_20, the decoy-confusion grounds on which the theme bundle was cut, and (c) the verified $7.26 casual D90 ARPPU shape supports a ~$7 completion price. The Grand-shortlist revenue argument is immaterial at our scale. Then tested rather than assumed. **Strong conversion:** 6 live SKUs, 4 entitlements, a durable consumable ledger with double-grant protection, and all five RC Placements (`post_level_5`, `theme_preview`, `bonus_district`, `shop`, `rewind_failure`) resolving offerings server-side, with `paywall_viewed → purchase_started → purchase_completed` instrumented with placement and offering_id on every event. The $6.99 vs $4.99 test ran through RC Experiments where the plan allowed and a pre-declared sequential offering swap where it did not — either way judges see hypothesis → test → decision inside the window, with the method disclosed. For revenue calibration: 2025 category winners reported $1–2k in the window; our organic base case computes to ≈ $253 net — this entry argues craft of monetization, not volume.

**Exhibits:** RC dashboard products/entitlements/offerings/placements, the Paywalls v2 editor, the live
paywall on device, per-placement funnel numbers with denominators, the experiment readout, the
subscription-rejection record (`monetization_spec.md` §5).

### 2.3 Catvertising (P0) — $15k / $10k / $5k

**Verbatim criteria (brief):** creative + effective ads, *"clever placements, smart integration with the
rest of your revenue stack… an experience users don't hate"*. **Requires describing use of RevenueCat
Ads.**

**Paragraph (submission-ready):**
> Our entry is an inversion: the cleverest ad placement is the one you refuse to build. Cat Metro ships with **no interstitial, no banner, and no app-open ad surface anywhere in the binary** — not capped, not throttled, absent. Every ad in the game is rewarded and player-initiated, across five surfaces with hard caps: `rewind_failure` (2/session, 5/day), `double_tickets` (3/day), `daily_gift_double` (1/day), `streak_saver` (1/day), and `theme_rental` (3 levels, 1/theme/day). **Clever placements:** each one sits exactly where the player already wants something — a rewind after their own second failure, doubled tickets at the results screen, a three-level test drive of a theme they just previewed on their live board. Three consecutive declines mute ad rows entirely for 24 hours: telling us no is a signal we obey. **Smart integration with the rest of the revenue stack:** we use **RevenueCat Ads** (Ad Monetization, public beta) as the tracking layer over Google Mobile Ads Unity 11.3.0, wired manually through **AdTracker** — `TrackAdLoaded`, `TrackAdDisplayed`, `TrackAdOpened`, `TrackAdRevenue`, `TrackAdFailedToLoad` on every ad event, because the AdMob convenience module is not available for Unity. Verified at the D10 integration spike rather than assumed (RC Ads is in public beta and we do not assume tracked ad revenue reaches the dashboard views until we see it), ad revenue then lands in the same RevenueCat dashboard as IAP revenue, so a single view answers "what is this player worth, and which half came from an ad they chose to watch." **An experience users don't hate:** the verified market data is unambiguous — Arrows – Puzzle Escape reached 103.6M installs in 12 months at 4.83★ but carries "ad every other level" backlash, and Bus Traffic Fever sits at 3.72★ on 15.4M installs under forced 30s ads (all verified 2026-07-31). We built the opposite on purpose, published the opt-in and decline rates, and made "ads only when you ask" the store listing's first paragraph.

**Exhibits:** RC AdTracker charts in the RC dashboard, the rewind sheet with free options above the
divider (screenshot 4), the ad-surface UX recordings from roadmap week 6, opt-in/decline rates with
denominators, the store listing's ads paragraph, and the theme_rental→purchase conversion table
(rentals started → cm_theme_* purchases, with denominators — the ad surface that closes as a sale).
**Contingency:** if RC Ads beta access or AdMob end-to-end fails the D14 gate, Model A fires,
`ads_enabled` ships OFF, and **this category is dropped** with effort redirected to HAMM + OneSignal
(`monetization_spec.md` §2.4). Do not submit a Catvertising entry describing ads that are not live.

### 2.4 OneSignal (P0) — $25k / $15k / $5k

**Verbatim criteria (brief):** *"a single deployed message is sufficient for eligibility"*; criteria
**Implementation, User value, Resourcefulness**.

**Paragraph (submission-ready):**
> Eligibility takes one message; we are past it in week 2. The entry is about the other three words. **Implementation:** the full surface in one small game — push, in-app messages, tags, custom events, Time Windows, deep links, outcomes, `Login(external_id)`, and the RevenueCat `$onesignalUserId` integration so purchase state flows RC → OneSignal with no server of our own. It is wired through a typed adapter behind an `IMessaging` interface, with a taxonomy-enforced tag registry (an unknown tag or event name is a build error in development) and cold/warm/killed deep-link routing verified on the device matrix. **User value:** messaging that gives before it asks. Journey 3 (hard-level help) sends a free rewind and a route tip and **never sells** — the deep link lands on the level, not a paywall, and the purchase row is suppressed on that attempt. Journey 2 ends with an explicit "no more reminders after this" and keeps the promise with a tag that permanently blocks re-entry past that rung. Streaks never gate content (a break costs at most 150 tickets of gift escalation), the streak-saver is free or rewarded and never sold, and the Android 13 permission budget of two system dialogs is spent only at real value moments — the first soft prompt appears after the player's first completed Daily Line, not at install. **Resourcefulness — the core story:** a 13-touchpoint retention design compressed into the **Growth plan's hard ceiling of 3 active journeys and 6 message steps** (2+3+1). Frequency capping is Enterprise-only, so we rebuilt it client-side in the adapter (an honest ceiling of 2 pushes/day for an engaged streak-holder — a daily nudge plus a streak warning at most — enforced across journeys, scheduled sends *and* local notifications). Quiet hours do not exist on our plan, so every message step sits inside a Time Window. Streak protection is backstopped by Unity local notifications so the system degrades gracefully when push fails. Purchase recovery ships as a +2h local notification needing no plan feature at all. Calendar-known content — event start/end, content patches — never consumes a journey slot; it goes out as scheduled sends. A solo developer on the $19/month plan getting Enterprise-shaped behavior by design.

**Exhibits:** all three journey canvases, the copy variant table, the outcomes chart with revenue
linkage, delivery/open rates with denominators, unsubscribe rate, the caps table, an adapter code
excerpt.

### 2.5 #BuildInPublic (P0) — $30k / $20k / $10k

**Criteria status: the locked brief records the prize tiers ($30k/$20k/$10k) but does NOT capture
verbatim judging-criteria text for this award.** Do not invent criteria language. **Re-verify the
official criteria wording on the award's Devpost page during the T-72h rules re-check (§8, item 0) and
tune this paragraph to the actual wording before submitting.**

**Paragraph (submission-ready, criteria-agnostic):**
> Cat Metro was built in public for all 56 days, starting the day the event opened. One post per day, without exception: what shipped, what broke, one number with its denominator, and what is next. Six hard gates — fun (D7), level system (D14), commercial beta (D21), store-ready (D24), retention experiments (D35), content-complete (D42) — each with pass criteria written *before* the gate and the result published the same day, including the ones we failed. Every material decision has a public ADR with the reasoning and a falsifiable "what would change my mind." The corpus includes the unglamorous parts: EDM4U dependency conflicts, three open RevenueCat Paywalls-v2 Android crash issues and the fallback we built for them, a retention week below the GameAnalytics 2025 median, and the categories we deliberately skipped and why. Every benchmark we quote is labeled with its vintage — we publicly corrected the widely-repeated "puzzle D1 31.85% / D7 12.18%" figures as **2022** data and used the 2025 medians (D1 ~22%, D7 ~4%, D30 ~0.7%) instead, because quoting a flattering old number is the easiest way to make a build log worthless. Store listing experiments, pricing tests, and journey copy tests were all announced before they ran and reported afterward with their sample sizes, including the ones that produced no detectable difference. The full index lives at catmetro.com/build, and every post is archived as an image because accounts break.

**Exhibits:** the post index page, gate readout posts (especially a failed one), an ADR, the vintage
correction post, an experiment "no detectable difference at n=X" post, the 56-day retro thread.

### 2.6 Design (P1) — $15k / $10k / $5k

**Criteria status: the locked brief records the prize tiers but does NOT capture verbatim judging
criteria for the Design award.** Re-verify the wording at T-72h before submitting; the paragraph below
is written to defensible design substance rather than to guessed rubric language.

**Paragraph (submission-ready):**
> The design thesis is one sentence: **readability outranks beauty, and the game is beautiful anyway.** Cat Metro looks like a hand-built model railway of a cat city sitting on a wooden desk — cardboard bevels, contact shadows, an 8% vignette, and desk-margin props confined to under 6% of screen area, outside the board's safe rect and never animated during play. It is rendered with **one** lighting rig and **one** toon shader family in two presets (day/night), on a locked twelve-color palette, because a solo dev who ships two shader families ships neither well. Every gameplay decision is legible: line color is **never** the only signal — each line carries a color, a symbol (● ■ ▲ ◆ ★), and its own cat silhouette, so the board survives deutan, protan and tritan simulation, which is a merge gate for any palette change. Every interactive target is at least 48dp with expanded hit zones, and the board validator rejects layouts that put junction centers too close together rather than shrinking targets. Failure is designed as information: the camera goes to the *cause*, not the score, with a replay scrub of the moment the jam started, and retry is under one second with no loading screen. Accessibility is structural, not a settings afterthought: a planning-pause mode freezes the simulation while you think, and motion and haptics have independent toggles. Even the commerce UI is designed against itself — no countdowns exist because we run no time-limited sales, decline buttons carry equal weight, and confirm-shaming copy is a banned pattern in the review checklist.

**Exhibits:** screenshots 3 and 6, the colorblind simulation comparison, the golden-frame test scene,
the failure-camera clip, the planning-pause clip, the palette table.

### 2.7 Grand Prize (P1) — $100k

**Verbatim criteria (brief):** shortlist = **"TOTAL REVENUE reported in RevenueCat during the window"**;
*"highest revenue doesn't auto-win"*; criteria **"Early and Effective Release"** + **"Growth by numbers"**.

**Paragraph (submission-ready):**
> **Early and Effective Release:** Cat Metro's first public release landed on **{launch_date}**, inside the window and inside our own planned Aug 24–28 target — hit by starting the Google Play closed test on Aug 1 with 12 testers so the 14-day continuous-tester clock and production-access review were cleared before the build was even feature-complete. That was not luck; it was the schedule's first constraint, because on a personal Play account the path to production is 3–5 weeks and everything else is downstream of it. Effective means it kept working: staged rollout with pre-written halt criteria (crash-free below 99% or ANR above 0.47%), a hotfix branch prepared before launch day, a rehearsed release runbook, and a content patch shipped on Sep 9 inside the same window. **Growth by numbers:** organic only, at a $0 default budget — store listing experiments on icon, screenshot order and short description; ASO iteration driven by Play Console search terms; five staggered community launches; a Product Hunt launch a week after release once we had real numbers to lead with; two waves of micro-creator outreach; a share-card loop built on a Daily Line whose seed produces the same board worldwide with no server. Every reported figure carries its denominator and every benchmark its vintage. **On revenue:** the shortlist reads total revenue reported in RevenueCat during the window, and we deliberately chose a monetization model that caps that number — no forced ads, no subscription, a $9.99 ceiling — because the model *is* the product. For calibration, the 2025 grand winner reported 17k users, $30,017, and 1,750 payers, organic and ASO-led. We report ours exactly as RevenueCat has it, whatever it is.

**Exhibits:** the RevenueCat revenue chart for the window (the shortlist's own source), the Play
release timeline, the staged-rollout and vitals screenshots, the growth readouts.

### 2.8 Skipped and conditional categories (state briefly, do not pad the slate)

| Category | Status | Reason (from the locked brief) |
|---|---|---|
| Stripe Funnel Vision | **P2, conditional** | Go/no-go at D35; needs RC Funnels on plan + connected Stripe + ≤8h build via Redemption Links; default NO-GO with zero launch-scope impact |
| Samsung Galaxy | **P2, non-cash** | 3 weeks featured placement; 20% of score is Galaxy optimization; enter only if optimization is already trivially satisfied |
| Most Viral / Noise | P1 evaluate ~D21 | Requires the Noise platform; cheap to evaluate, easy to skip |
| Replit | **Skip** | Requires building with Replit Agent |
| JetBrains | **Skip** | Requires KMP on **both** stores |
| Influencer categories | **Skip** | Only one influencer category may be entered and the gaming one requires a "gaming bucket list" app — a different product |
| Peace Prize | **Skip** | Not a fit; entering categories we do not fit dilutes the submission |

**Decision:** Enter the five P0 categories plus Design and Grand Prize; write one purpose-built paragraph per category rather than one generic description reused seven times.
**Evidence:** All criteria quotes above are verbatim from the locked brief (verified 2026-07-31); multi-category entry is allowed and no one-prize-per-project cap was found.
**Action:** Draft all seven paragraphs in roadmap week 7 (Sep 12–18); tighten to the exact official wording during week 8 (Sep 19–25); re-verify #BuildInPublic and Design criteria text at T-72h.
**Risk:** Two of the seven paragraphs are written without verbatim criteria in hand and may miss the actual rubric.
**Fallback:** Both are written to substance that any plausible rubric rewards, and both are short enough to rewrite in 30 minutes on Sep 27 once the official wording is read.

### 2.9 Category-specific questions (Devpost form) — drafted answers

**Rule (official judging guide, Aug 1): a targeted category whose category-specific question is left
empty is not judged in that category. No targeted category's question may be left blank — ever.** The
exact question wording becomes visible once the submission is live (~Sep 15, submit-early flow); the
drafts below carry each category's core answer and are retuned to the actual question text during the
week-8 copy pass and again at the T-72h re-check.

- **Best Game:** One verb — tap — routing color- and symbol-coded cat commuters in 45–90-second
  levels, every level proven solvable by a solver in CI before it can merge. Monetization fits the
  genre: a single $6.99 complete edition, $2.99 cosmetic themes, and five player-initiated rewarded
  surfaces — no interstitials, no banners, no energy, no subscription. (Long form: §2.1.)
- **HAMM:** A five-point price ladder where every price does a different job, a once-ever
  post-level-5 paywall through RC Placements + Paywalls v2 with a pixel-matched fallback, two
  documented negative decisions (theme bundle cut on decoy-confusion grounds; subscriptions rejected
  in writing), and a $6.99-vs-$4.99 test disclosed method-first. (Long form: §2.2.)
- **Catvertising:** The cleverest placement is the one we refused to build — zero forced ad surfaces
  in the binary. Five rewarded, player-initiated surfaces with hard caps and a 3-decline→24h mute,
  each tracked through RC AdTracker. (Long form: §2.3.)
- **OneSignal:** Enterprise-shaped behavior on the $19 plan: 3 journeys / 6 message steps,
  client-side frequency caps, Time Windows as quiet hours, and a hard-level-help journey that gives a
  free rewind and never sells. (Long form: §2.4.)
- **#BuildInPublic:** 56 posts in 56 days — gates pre-registered before data existed, failures
  published the same day, every number with its denominator and every benchmark with its vintage,
  including our public correction of the outdated 2022 retention figures. (Long form: §2.5.)
- **Design:** Readability outranks beauty and the game is beautiful anyway: a tabletop-diorama cat
  city, color + symbol + cat silhouette on every line (colorblind simulation is a merge gate), and
  failure rendered as information by the cause-first camera. (Long form: §2.6.)
- **Grand Prize:** Released early inside our own Aug 24–28 target by making the closed-test clock the
  schedule's first constraint; grown organically at a $0 default budget; revenue reported exactly as
  RevenueCat has it, with the 2025 winner's numbers quoted as calibration, not as a claim.
  (Long form: §2.7.)

---

## 3. Metrics — what we show, and what we refuse to overstate

Rule, applied without exception: **every rate ships with its denominator, every benchmark with its
vintage, and every non-randomized comparison labeled as such.** This is not modesty; it is the reason a
judge can trust the numbers that *are* good.

### 3.1 Show these (with the stated framing)

| Metric | Source | Framing we use | Why it is honest |
|---|---|---|---|
| Total window revenue | **RevenueCat dashboard** | Absolute dollars, exact date range, gross with the 15% effective Play fee noted separately | It is the Grand Prize shortlist's own source of truth; no reconciliation dispute is possible |
| Payers / installs | RC + Play Console | "n payers of m installs = x%" | Both numbers stated; the 2025 winner calibration (1,750 payers / 17k users) is quoted as *their* number, not a target we hit |
| Paywall funnel per placement | Analytics taxonomy + RC | "`paywall_viewed` n → `purchase_started` n → `purchase_completed` n" per placement, raw counts first | Placement-level counts are small; showing raw counts prevents a 2-of-30 from being dressed as 6.7% |
| Rewarded-ad opt-in and decline | AdTracker + our events | "x of n offers accepted; y of n declined; 3-consecutive-decline mute triggered z times" | The Catvertising claim is about behavior, and behavior is countable |
| Crash-free sessions / ANR | Play Console vitals + Crashlytics | Percentage **with session count** and the date range | A 99.7% on 400 sessions is a different claim than on 400k, and we say which |
| D1 retention by weekly install cohort | Analytics | "D1 = x% of n installs in cohort {week}", against **GameAnalytics 2025 medians D1 ~22% / D7 ~4% / D30 ~0.7%** | Vintage-labeled. We also publicly note that the widely-quoted puzzle figures (31.85/12.18/5.35) are **2022** data and outdated |
| D7 retention | Analytics | Same treatment; only for cohorts with a full 7 days elapsed | Partial cohorts are excluded, not annualized |
| Daily Line participation | Analytics | "daily_started / DAU-with-daily-unlocked", both numbers shown | The denominator is the non-obvious part and it changes the meaning |
| OneSignal delivery / open / outcomes | OneSignal dashboard | Sends, delivered, opened, and outcome counts — never open rate alone | Open rate without send volume is unreadable at our scale |
| Store listing experiment results | Play Console | Play's own readout, including "no winner declared" | Play randomizes; we report what it says, including nulls |
| Levels shipped / validated | CI | "40 levels, 100% solver-validated, n CI runs" | A binary fact with a machine-checkable source |

### 3.2 Do NOT overstate these (and how we phrase them instead)

| Metric | Why it is immature or fragile | How we phrase it |
|---|---|---|
| **D30 retention** | Launch is Aug 24–28 and the window closes Sep 30 — only the very first cohorts have 30 days elapsed, and those are our smallest and least representative. | "D30 is not yet meaningful for this app: only the {n}-install launch cohort has 30 days elapsed as of Sep 30. We report it with that caveat or not at all." |
| **Conversion rates from small samples** | A placement with 40 views and 1 purchase is 2.5% — and also indistinguishable from 0% or 6%. | Lead with raw counts; give the percentage second; never quote a placement rate below ~100 views without the count adjacent. |
| **Price experiment "winners"** | Per `experiment_backlog.csv` E07, revenue-per-view is high variance; at our n only a ~2× difference is readable. | "Directional, not significant. $6.99 arm: n views, n purchases, $X. $4.99 arm: … We chose {price} on {reason}; a larger sample could reverse this." |
| **ARPDAU / ARPPU / LTV** | No credible public puzzle ARPDAU or opt-in benchmarks exist (verified 2026-07-31), and our window is 37 days. | Report total revenue and payer counts. If ARPU is shown at all, it is stated as "revenue ÷ installs over a 37-day window," never extrapolated to a year. |
| **Rewarded eCPM** | US Android rewarded benchmarks span $15–30 (Tenjin Q2'24) and $9–16 (Appodeal Q4'24) — different vintages, different methodologies. | Quote our actual observed eCPM with impression count, and cite both benchmark ranges *with their vintages* rather than picking the flattering one. |
| **Refund rate** | No credible genre benchmark exists; at low transaction counts one refund moves it several points. | "n refunds of m transactions" — count only. |
| **A/B statistical significance** | Almost nothing at our scale is powered (the experiment backlog says so per-experiment). | Never use the word "significant." Use "directional," give n, and state the method (sequential vs randomized) explicitly. |
| **Press / influencer attribution** | No paid attribution stack; install spikes are correlational. | "Installs on the day of {event}: n, versus a trailing 7-day average of m. Correlation, not attribution." |
| **Store listing CVR vs the ~16% US average** | AppTweak **2025** data, and games are stated to be below that average. | Always vintage-labeled, always with "games index below this average." |
| **Community size** | 50 Discord members is a real number and a small one. | State it plainly. Small honest communities read better than vague "growing community" language. |

**Decision:** Absolute counts lead, rates follow with denominators, immature metrics are named as immature rather than omitted quietly.
**Evidence:** Brief's benchmark-honesty rules (label vintages; the 2022 puzzle retention figures are outdated; no credible puzzle ARPDAU/opt-in/refund benchmarks exist); `experiment_backlog.csv` states the power limitation of nearly every test; judges may judge from text/images/video alone, so the text must be self-defending.
**Action:** Build the final metrics snapshot on Sep 30 from RC + Play Console + OneSignal + analytics, with denominators baked into the cell labels, not the footnotes.
**Risk:** Honest small numbers look weak beside competitors quoting big rates without denominators.
**Fallback:** That trade is accepted deliberately. The Grand Prize shortlist reads revenue straight from RevenueCat regardless of our framing, and every category we target rewards craft and fit over raw scale — while a single inflated number that a judge can disprove costs the entire submission's credibility.

---

## 4. Evidence screenshot shot-list

Captured **Sep 22–29** (real data, not staged), stored in `/submission/evidence/` as
`NN_slug_YYYY-MM-DD.png`, at native resolution, with the capture date visible in the UI wherever the
dashboard shows one. Redact nothing except personal account identifiers and API keys.

**A. Store presence (proves the release)**
1. `01_play_listing_full` — the live Play store listing, full page, showing title, icon, screenshots, "Contains ads" and "In-app purchases" labels.
2. `02_play_listing_search` — Cat Metro appearing in Play search results for `train puzzle` (the ASO lane claim, with the query visible).
3. `03_play_console_release` — Play Console release dashboard showing the **production release date** and staged-rollout percentage (Early-and-Effective-Release evidence).
4. `04_play_console_vitals` — Android vitals: **crash-free sessions rate with session count**, ANR rate, date range visible.
5. `05_play_ratings_reviews` — ratings distribution plus at least one developer reply visible.

**B. RevenueCat (HAMM + Grand Prize)**
6. `06_rc_products` — the Products list: all six SKUs (`cm_all_access`, `cm_supporter_pack`, `cm_theme_sakura`, `cm_theme_neon`, `cm_rewind_5`, `cm_rewind_20`) plus the experiment SKU.
7. `07_rc_entitlements` — Entitlements: `all_access`, `supporter`, `theme_sakura`, `theme_neon`, with product attachments (showing Supporter attaching to both `supporter` and `all_access`).
8. `08_rc_offerings` — Offerings `ofr_core`, `ofr_themes`, `ofr_rewind`, `ofr_shop` (the four permanent launch offerings) with their packages. `ofr_core_b` is a PW01 experiment artifact deleted after the readout — it appears in exhibit 13, not here.
9. `09_rc_placements` — all five Placements (`post_level_5`, `theme_preview`, `bonus_district`, `shop`, `rewind_failure`) mapped to offerings.
10. `10_rc_paywall_editor` — the Paywalls v2 editor showing the post-level-5 paywall configuration.
11. `11_rc_charts_revenue` — the revenue chart for **Jul 31 – Sep 30 2026** (the Grand Prize shortlist's own source), totals visible.
12. `12_rc_charts_conversion` — conversion / active-subscriptions-and-purchases view with counts.
13. `13_rc_experiment_readout` — the price experiment readout (or, if the plan gated Experiments, the sequential-offering comparison built from our own analytics, clearly labeled as the fallback method).
14. `14_rc_customer_center` — Customer Center / restore flow on device.

**C. RevenueCat Ads / AdTracker (Catvertising)**
15. `15_rc_ads_adtracker_events` — AdTracker events flowing in the RC dashboard (loaded / displayed / opened / revenue / failed-to-load).
16. `16_rc_ads_revenue_chart` — ad revenue chart alongside IAP revenue (the "one dashboard" claim).
17. `17_device_rewind_sheet` — the rewind sheet on device with free/owned/rewarded rows **above** the divider and pack rows below.
18. `18_device_no_ad_surfaces` — the level-to-level transition showing no interstitial (a 3-frame strip: win → results → next level).

**D. OneSignal**
19. `19_os_journey1_canvas` — Journey 1 (Daily Line + streak) canvas, both branches and Time Window steps visible.
20. `20_os_journey2_canvas` — Journey 2 (lapse ladder) canvas, three rungs and the tag step that sets `lapse_final_sent`.
21. `21_os_journey3_canvas` — Journey 3 (hard-level help) canvas.
22. `22_os_delivery_stats` — delivery/open statistics **with send counts**, date range visible.
23. `23_os_outcomes` — outcomes including `purchase_completed` with value (revenue linkage inside OneSignal's own reporting).
24. `24_os_iam` — an in-app message as rendered on device (payer thanks or the soft push prompt).

**E. Engineering credibility**
25. `25_ci_level_validation` — the GitHub Actions run showing the solver validating levels, including a deliberately-broken level failing the gate.
26. `26_ci_replay_hash` — the determinism/replay-hash test passing.
27. `27_crashlytics_dashboard` — crash-free rate over the window with session counts.
28. `28_cohort_table` — the retention cohort table (D1/D7 by weekly install cohort) **with n per cohort**, D30 row explicitly marked immature.
29. `29_analytics_funnel` — `first_open → tutorial_completed → level_completed(L5) → paywall_viewed → purchase_completed` with raw counts at each step.

**F. Build-in-public**
30. `30_bip_index` — the catmetro.com/build index page showing the daily post series.
31. `31_bip_failed_gate` — a published gate post reporting a *failure* (the credibility exhibit).
32. `32_adr_example` — an ADR from the repo with reasoning and the falsifiable "what would change my mind."

**Decision:** 32 numbered exhibits captured from live systems in the final week, each tied to a specific award paragraph's claim.
**Evidence:** Judges may judge from text, images and video alone (verified 2026-07-31); roadmap week 7 already schedules "OneSignal evidence pack" and "revenue + conversion snapshots"; liveops §7 sets the daily ops checks these screenshots come from.
**Action:** Capture window Sep 22–29; assemble into the Devpost gallery and a single linked PDF; re-shoot exhibits 11 and 22 on **Sep 30** so the headline numbers are final.
**Risk:** A dashboard UI changes or a chart renders empty for a date range, on the last day.
**Fallback:** Capture each exhibit twice, a week apart (Sep 22 and Sep 29), so a broken final capture still has a dated predecessor; every number also exists in the written submission text, which is what judges read first.

---

## 5. The 2-minute demo video

**Hard rules (verified 2026-07-31):** under **2:00**, hosted on **YouTube or Vimeo**, **no third-party
trademarks and no third-party music**. Our target runtime is **1:58**. All audio is the game's own
original SFX and stems. No competitor names, no store badges beyond the Google Play text CTA, no real
transit authority marks, no stock footage, no licensed track.

**Structure follows RevenueCat's own "how to win" guidance: the gameplay hook lands in the first 15
seconds.** No logo sting, no talking head, no "hi, I'm…" — the first frame is a thumb on a live board.

**Judging-funnel rules (official judging guide, Aug 1):** prescreeners see only the video's **first
two minutes** plus the text fields — the elevator pitch, the app running on a device, and an
**explicit statement of the targeted categories** must all land inside 2:00. In this cut: the pitch
and live on-device play run from 0:00, and the category-target card is at 1:52. Two verification
rules bind the assets: the submitted **package name must exactly match the live app** (the RevenueCat
SDK integration is verified programmatically against it), and the **app must match what the video
shows** — an RC advocate downloads and plays the build before winners are finalized.

### 5.1 Time-coded storyboard

| Time | Visual | On-screen text | VO (segment) |
|---|---|---|---|
| 0:00–0:05 | Cold open, no logo: live board, thumb enters, throws a switch, a cat train visibly changes line and reaches its station | `TAP THE SWITCH` | "Tap a junction. Throw the switch." |
| 0:05–0:15 | Real-time play, three deliveries, purr-meter chain climbing, next-wave preview visible, win stamp | `45–90 SECONDS A LEVEL` | "Send the cat down the matching line before the platform overflows. That is Cat Metro, a one-thumb train puzzle. Forty-five to ninety seconds a level. This is real gameplay, first frame, no mockups." |
| 0:15–0:28 | A jam builds, Overload ring, fail; cause-first camera snaps to the culprit platform, replay scrub; instant retry; clean solve | `THE CAMERA SHOWS YOU WHY` | "When you lose, the camera goes to the cause, not to the score. Retry takes under a second. No life to spend, no ad to sit through, no reason to put the phone down." |
| 0:28–0:42 | District map pan across all 6 districts; cut to the Daily Line board with date header; two devices side by side showing the identical board; share card with route ribbon | `30 LEVELS · 6 DISTRICTS · A DAILY LINE` | "Thirty handcrafted levels across six districts, plus a new Daily Line every day. Every player on Earth gets the same board, generated from a shared seed, with no server involved." |
| 0:42–1:05 | Screen recording of a level-to-level transition showing **nothing** appears between levels; then the five rewarded surfaces in quick cuts, each with its cap label; then the RC dashboard AdTracker events ticking in | `NO INTERSTITIALS. NO BANNERS. NO APP-OPEN ADS.` → `ADS ONLY WHEN YOU ASK` | "Now the part I actually care about. Most free puzzle games in this category monetize your patience. Cat Metro has no interstitials, no banners, no app-open ads. Those surfaces do not exist in the build. Ads happen only when you ask for one: an extra rewind, double tickets, a three-level test drive of a premium theme. RevenueCat AdTracker records every one of them." |
| 1:05–1:30 | The post-level-5 paywall appearing (once), closing in one tap; cut to RC dashboard Placements screen; cut to the rewind sheet with free rows above the divider; cut to the CI log showing the solver validating a level | `ONE PAYWALL. ONCE. EVER.` → `FREE OPTIONS FIRST` → `SOLVER-PROVEN SOLVABLE` | "The paywall fires once, ever, after level five, through RevenueCat Placements and Paywalls v2. All Access is a single purchase, never a subscription. And when you fail, the rewind sheet puts the free options above the paid ones, under a footer that says every level is solvable without them. That footer is true, because a solver proves it in CI before a level is allowed to merge." |
| 1:30–1:42 | OneSignal journey canvases (three, quick cuts), then the copy table, then a device receiving the daily notification | `3 JOURNEYS · 6 STEPS · $19/MO` | "Retention runs on three OneSignal journeys and six message steps, on the nineteen-dollar plan, with the frequency caps rebuilt in our own code." |
| 1:42–1:52 | Fast montage of the build-in-public feed: daily posts, a failed gate post, the numbers thread with denominators circled | `56 DAYS · PUBLISHED DAILY` | "Fifty-six days. One developer. Every number published with its denominator while it happened." |
| 1:52–1:58 | Wordmark on Cream Card, cat conductor tips its cap, Play CTA, category-target card | `CAT METRO — FREE ON GOOGLE PLAY` → `ENTERED: BEST GAME · HAMM · CATVERTISING · ONESIGNAL · #BUILDINPUBLIC · DESIGN · GRAND PRIZE` | "Cat Metro. Free on Google Play." |

### 5.2 Voice-over script (275 words — read at ~140 wpm, lands at ~1:58)

> Tap a junction. Throw the switch. Send the cat down the matching line before the platform overflows. That is Cat Metro, a one-thumb train puzzle. Forty-five to ninety seconds a level. This is real gameplay, first frame, no mockups.
>
> When you lose, the camera goes to the cause, not to the score. Retry takes under a second. No life to spend, no ad to sit through, no reason to put the phone down.
>
> Thirty handcrafted levels across six districts, plus a new Daily Line every day. Every player on Earth gets the same board, generated from a shared seed, with no server involved.
>
> Now the part I actually care about. Most free puzzle games in this category monetize your patience. Cat Metro has no interstitials, no banners, no app-open ads. Those surfaces do not exist in the build. Ads happen only when you ask for one: an extra rewind, double tickets, a three-level test drive of a premium theme. RevenueCat AdTracker records every one of them.
>
> The paywall fires once, ever, after level five, through RevenueCat Placements and Paywalls v2. All Access is a single purchase, never a subscription. And when you fail, the rewind sheet puts the free options above the paid ones, under a footer that says every level is solvable without them. That footer is true, because a solver proves it in CI before a level is allowed to merge.
>
> Retention runs on three OneSignal journeys and six message steps, on the nineteen-dollar plan, with the frequency caps rebuilt in our own code.
>
> Fifty-six days. One developer. Every number published with its denominator while it happened.
>
> Cat Metro. Free on Google Play.

**Production notes:** record VO in one take per paragraph, normalize to −16 LUFS, duck the game audio
to −22 LUFS under speech. Burn captions (accessibility, and most viewers watch muted). Export
1920×1080 landscape for YouTube — the *listing* promo is the vertical one; this submission video is
watched on a laptop by a judge. Upload **unlisted** first, verify playback and runtime, then set
public before submitting. If VO recording overruns, ship the on-screen-text-only cut: the storyboard's
text track carries the full argument alone.

**Decision:** 1:58, gameplay in frame one, monetization argument in the middle third, no third-party assets of any kind.
**Evidence:** Verified rules cap the video under 2 minutes and ban third-party trademarks and music; roadmap week 7 schedules "sub-2-minute video scripted + cut from banked takes (original music only)"; RevenueCat's own guidance is to lead with the gameplay hook in the first 15 seconds.
**Action:** Cut from the takes banked at roadmap D23; v1 by Sep 18, final by Sep 25, uploaded unlisted by Sep 28 morning (§8 item 4).
**Risk:** Runtime creep past 2:00 disqualifies the video, and last-minute trimming breaks the VO sync.
**Fallback:** The storyboard is built in removable blocks — the 1:30–1:42 OneSignal block and the 1:42–1:52 BIP block can each be cut to a 4-second still with a text card, buying 16 seconds without touching the gameplay or monetization sections.

---

## 6. Judge testing instructions (Devpost "How to test" field)

Paste-ready text, with the operational reasoning kept in this spec.

> **Platform:** Android 7.1+ (API 25). Google Play, available in the USA. Free download, no account, no sign-up, works offline.
> **Store URL:** {play_url}
>
> **You do not need a code to evaluate the game.** The entire 30-level campaign, the Daily Line, the shop, and every paywall surface are reachable for free. Codes are only for the paid content.
>
> **⚠️ Do this in order.** Redeeming an All Access code permanently suppresses the system-initiated paywalls (that suppression is a deliberate feature — payers stop being sold to). **So: look at the paywalls first, redeem second.**
>
> **Fastest route to every commerce surface (from a fresh install):**
> | What you want to see | How to get there | Time |
> |---|---|---|
> | Shop (RC placement `shop`) | Home → Shop tab | instant |
> | Theme preview sheet (`theme_preview`) | Home → tap a locked theme swatch in the map header. The live board re-skins behind the sheet | instant |
> | Bonus-district paywall (`bonus_district`) | Home → tap the Night Harbor district tile | instant |
> | **The flagship paywall (`post_level_5`, RC Paywalls v2)** | Play levels 1–5 and win L5. It fires once, ever, after the celebration | ~5 min |
> | **The rewind sheet (`rewind_failure`)** | Fail L006 twice: fail once — **note that nothing is offered, by design; the sheet never appears on a first failure** — then fail a second time with the level at least 40% complete. A ⏪ chip appears next to "Try again"; tap it | ~2 min |
> | Rewarded ad | Inside the rewind sheet, "Watch an ad for a rewind"; or "Double your tickets" on any results screen | ~2 min |
> | Daily Line + streak | Unlocks after level 7 | ~8 min total |
> | Restore purchases | Shop → footer → "Restore purchases" (also in Settings and on every paywall) | instant |
>
> **Promo codes (Google Play one-time codes, `cm_all_access`):**
> `{CODE_1}` `{CODE_2}` `{CODE_3}` `{CODE_4}` `{CODE_5}`
> Redeem in the Play Store app → profile picture → **Payments & subscriptions → Redeem code**, or at **play.google.com/redeem**. Each code is single-use. Email {support_email} and I will send more within a few hours — no questions asked.
>
> **To verify the restore path:** redeem a code, confirm All Access unlocks (Night Harbor district opens, both themes show "Owned ✓", the gold conductor badge appears), then uninstall and reinstall, open Shop → Restore purchases. Entitlements return on the same Google account.
>
> **To verify "no forced ads":** play ten levels in a row. Nothing will interrupt you — there is no interstitial, banner, or app-open ad surface in the build.
>
> **Notifications** are opt-in and the first prompt only appears after your first completed Daily Line. Journey messages are time-windowed (nothing sends 21:00–09:00 local) so an automated message will not arrive during a short test session — the journey canvases and delivery statistics are in the evidence gallery instead.
>
> **If anything at all is broken for you:** {support_email}. I reply the same day during judging.

**Operational notes (not in the Devpost text):** mint **fresh** promo codes on Sep 28 and redeem-test
one on a clean device the same day (roadmap already does this at D24 and again in week 8); Play
one-time promo codes are verified to work for one-time in-app products (brief). Keep a reserve of at
least 10 unminted codes. Codes have expiry dates — confirm every listed code's expiry extends past
**Oct 13** (the end of judging), not just past Sep 30.

**Decision:** Free-play-first instructions with an explicit "paywalls before codes" ordering, a per-surface fastest-route table, and codes valid through the end of judging.
**Evidence:** Verified 2026-07-31 — Play one-time promo codes work for one-time products; judging runs Oct 1–13; payer suppression is a locked product behavior (monetization_spec §3.11), so the ordering warning prevents judges from accidentally hiding the very surfaces they came to evaluate.
**Action:** Mint and test codes Sep 28; paste this block into Devpost Sep 29; re-verify code expiry dates against Oct 13 before submitting.
**Risk:** A judge redeems first, sees no paywalls, and concludes the monetization does not exist.
**Fallback:** The warning is the first thing in the block, the evidence gallery shows every paywall as a screenshot, and the video shows the post-level-5 paywall firing — the claim survives even if no judge ever installs the app.

---

## 7. The final 48-hour submission checklist

Deadline: **Wed Sep 30 2026, 11:45pm PDT.** The submission has been **live on Devpost since ~Sep 15**
and edited continuously (submit early, edit continuously — official judging guide, Aug 1). Working
target: **final edit pass complete by Tue Sep 29 18:00 PDT**, with Sep 30 reserved for the final
metrics refresh and pure buffer. Nothing on this list is code — the
feature freeze started Sep 24 and the Sep 26–30 window is submission-only (roadmap).

**T-72h — Sun Sep 27**

| # | Time (PDT) | Item | Done |
|---|---|---|---|
| 0 | 10:00 | **Re-read the Official Rules end to end** (≥72h before the 11:45pm Sep 30 deadline). Diff against the facts in DECISIONS_BRIEF §VERIFIED EVENT FACTS. Confirm: deadline time, required assets list, video length + third-party trademark/music rule, judge-access requirement, USA-accessibility requirement, and whether any new award categories appeared after Aug 1. | ☐ |
| 0b | 11:00 | **Re-verify the two criteria texts the brief did not capture** — #BuildInPublic and Design — on their official award pages, and tune §2.5 / §2.6 to the actual wording. | ☐ |
| 0c | 11:30 | Re-verify the **winners-date discrepancy** (rules say Oct 21, FAQ says Oct 22) and record which is current. | ☐ |
| 0d | 12:00 | Confirm every targeted category is still open and that multi-category entry rules are unchanged. | ☐ |

**Mon Sep 28 — assets and access**

| # | Time (PDT) | Item | Done |
|---|---|---|---|
| 1 | 09:00 | Final video export verified: runtime **≤1:59**, 1920×1080, audio present, captions burned in. | ☐ |
| 2 | 09:15 | Video third-party audit: no competitor names, no store badges beyond the Play text CTA, no transit-authority marks, **no third-party music** — confirm every audio stem is ours. | ☐ |
| 3 | 09:30 | **Upload the video to YouTube (unlisted) — early, on purpose.** Verify playback on a device that is not the upload machine, and verify captions render. | ☐ |
| 4 | 10:00 | Set the video to **Public** (or Unlisted if the rules permit — confirm at item 0) and copy the canonical URL into the submission draft. | ☐ |
| 5 | 10:30 | Verify the **1024×1024 icon** export: exact dimensions, no alpha issues, no text, matches the live store icon. | ☐ |
| 6 | 10:45 | Verify the **1179×2556 frameless screenshot**: exact dimensions, rendered at that resolution (not upscaled), no device frame, no overlay. | ☐ |
| 7 | 11:00 | Verify the **store URL** loads publicly in an incognito window and that the app is **available in the USA** (check the listing's country availability in Play Console). | ☐ |
| 8 | 11:30 | **From the 25-code batch minted at launch (15 judges / 5 press / 5 spare), pick the 5 judge codes to list on Devpost** — mint fresh replacements for any that were used or expire early; record each listed code's **expiry date** and confirm all extend past **Oct 13**. | ☐ |
| 9 | 12:00 | **Redeem-test one code on a clean device**: entitlement grants, Night Harbor unlocks, themes show Owned ✓, badge appears. Then burn that code (do not list a used code). | ☐ |
| 10 | 12:30 | Reinstall on the same clean device → Shop → **Restore purchases** → entitlements return. Screenshot the result for exhibit 14. | ☐ |
| 11 | 13:30 | Capture/refresh evidence exhibits 1–14 (store + RevenueCat) per §4. | ☐ |
| 12 | 15:00 | Capture/refresh evidence exhibits 15–24 (AdTracker + OneSignal) per §4. | ☐ |
| 13 | 16:30 | Capture/refresh evidence exhibits 25–32 (engineering + BIP) per §4. | ☐ |
| 14 | 17:30 | Assemble the evidence gallery: 32 files, correct naming, ordered, plus a single linked PDF. | ☐ |

**Tue Sep 29 — write, review, submit**

| # | Time (PDT) | Item | Done |
|---|---|---|---|
| 15 | 09:00 | Paste the Devpost **Story** field (§1, 300-word version); read it aloud once for tone. | ☐ |
| 16 | 09:30 | Paste the seven **award paragraphs** (§2.1–2.7), each tuned to its now-verified criteria wording. | ☐ |
| 17 | 10:30 | Paste the **judge testing instructions** (§6) with the five live codes and the support email. | ☐ |
| 18 | 11:00 | Fill "**Built with**": Unity 6000.3.16f1, C#, RevenueCat (purchases-unity 9.7.0, RevenueCatUI, Placements, Paywalls v2, AdTracker), OneSignal Unity 5.3.2, Google Mobile Ads Unity 11.3.0, Firebase Crashlytics, GitHub Actions. | ☐ |
| 19 | 11:30 | **Select every targeted category**: Best Game, HAMM, Catvertising, OneSignal, #BuildInPublic, Design, Grand Prize (+ Stripe Funnel Vision / Samsung only if their gates passed). Screenshot the selection. | ☐ |
| 19b | 11:40 | **Category-question audit (§2.9):** every targeted category's category-specific question has a non-empty, tuned answer — an empty question means the entry is not judged in that category. | ☐ |
| 19c | 11:50 | **Package-name and video-match audit:** the submitted package name exactly matches the live app (`com.catmetro.game` — the RevenueCat SDK integration is verified programmatically against it), and the final video is re-watched against the live production build: the app must match what the video shows (an RC advocate downloads and plays it before winners are finalized). | ☐ |
| 20 | 12:00 | **Claims audit** — read the whole submission and mark every number. For each: does it have a denominator? A date range? A vintage if it is a benchmark? Delete or fix anything that fails. | ☐ |
| 21 | 13:00 | **Overstatement audit** against §3.2: no "significant", no D30 without its caveat, no extrapolated LTV, no attribution language for correlational spikes. | ☐ |
| 22 | 13:45 | Verify **every link** in the submission: store URL, video URL, press kit, build-in-public index, privacy policy, support email (send a test email to it and confirm it arrives). | ☐ |
| 23 | 14:15 | Verify the **gallery images** render in Devpost's preview (some hosts mangle large PNGs). | ☐ |
| 24 | 15:00 | Cold read by a second pair of eyes if available (a tester from the original 12); otherwise read the whole thing on a phone, which surfaces different errors than a laptop. | ☐ |
| 25 | 16:00 | Fill every remaining Devpost field: tagline, elevator pitch, "what's next", team/solo declaration, eligibility and location fields. **No field left blank** (roadmap D50–56 dry-run criterion). | ☐ |
| 26 | 17:00 | Final rules cross-check of the submission itself: description ✓, <2min video ✓, store URL ✓, 1024² icon ✓, 1179×2556 frameless screenshot ✓, judge access ✓. | ☐ |
| 27 | **18:00** | **SUBMIT.** Do not wait for Sep 30. | ☐ |
| 28 | 18:15 | Screenshot the submission confirmation; save the confirmation email; note the submission timestamp. | ☐ |
| 29 | 18:30 | Re-open the submission as a logged-out viewer and read it as a judge would; fix anything broken (Devpost allows edits until the deadline — verify at item 0). | ☐ |
| 30 | 19:00 | Post the #BuildInPublic "submitted" beat with the submission screenshot. | ☐ |

**Wed Sep 30 — refresh, verify, stop**

| # | Time (PDT) | Item | Done |
|---|---|---|---|
| 31 | 09:00 | Confirm the app is still live, installable, and USA-available; confirm crash-free rate and ANR are inside halt criteria. | ☐ |
| 32 | 10:00 | Confirm all 5 promo codes are still unredeemed and unexpired; replace any that were used. | ☐ |
| 33 | 11:00 | Confirm OneSignal journeys are active and will run **unattended through judging (Oct 1–13)**; confirm no scheduled send lands during judging with stale event copy. | ☐ |
| 34 | 12:00 | Confirm RevenueCat offerings are stable and no experiment is mid-flight in a state that shows a judge an odd price. | ☐ |
| 35 | **20:00** | **Final metrics refresh**: recapture exhibits 11 (RC revenue, full window) and 22 (OneSignal delivery), and update any number in the submission text that moved materially. This is the only Sep 30 edit that touches copy. | ☐ |
| 36 | 21:00 | Final submission re-read; confirm submitted status; confirm the video still plays; **stop touching it**. | ☐ |
| 37 | 21:30 | Post the 56-day retro thread (BIP-10) and the thank-you to the 12 original testers. | ☐ |
| 38 | 22:00 | Buffer block held empty until **23:45 PDT** for emergencies only. Nothing new starts after 22:00. | ☐ |

**Decision:** Submit Sep 29 at 18:00 PDT with Sep 30 as refresh-and-buffer only; the rules re-check happens at T-72h on Sep 27, before any copy is finalized.
**Evidence:** Deadline Sep 30 11:45pm PDT and the full required-asset list are verified 2026-07-31; roadmap D54 (Sep 23) already gates on "every rules-required element verified" and flags the winners-date discrepancy for re-verification; roadmap week 8 requires a dry run that fills every Devpost field with no blanks.
**Action:** Put items 0–38 into the repo as `/submission/checklist.md` with checkboxes and work it top to bottom; block Sep 28–30 in the calendar with no other commitments.
**Risk:** A last-minute rules detail (video hosting, an asset spec, a category eligibility clause) invalidates work done in the preceding week.
**Fallback:** The T-72h re-check exists precisely to catch that with three days of runway; every asset in the pack is regenerable in under two hours from banked masters, and the submission is filed a full 29 hours before the deadline so a discovered problem is a fix, not a loss.
