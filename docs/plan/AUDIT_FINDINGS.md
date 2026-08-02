# AUDIT FINDINGS — Cat Metro / Shipaton 2026 Execution Package

**Audit date:** 31 July 2026 (evening PDT; submission window opened this morning).
**Method:** Independent adversarial audit. 23 agents: 10 re-fetched every load-bearing external
claim against live primary sources (Devpost rules, Google support/developer pages, RevenueCat/
OneSignal/Unity docs and GitHub, live Play listings, old.reddit raw HTML); 7 audited the files
mechanically (real ajv JSON-Schema validation, python CSV lint, full-package greps, every
revenue/economy/DAU formula recomputed); 6 attacked the plan's self-declared soft spots with
computed evidence (200k-trial launch-date Monte Carlo, price-elasticity break-evens,
pivot-effort decomposition, independent concept re-scoring). The auditor separately spot-checked
the Devpost rules and key data files first-hand. Confidence labels: fact = verified against a
primary source; inference = labeled; estimate = labeled.

---

## 1. Verdict

**AMEND, THEN EXECUTE. Do not rethink the concept; do not ship the package as written.**

The strategy core survives a genuinely hostile audit: of ~116 externally checkable claims,
**113 re-verified CONFIRMED against primary sources tonight** — including every plan-breaking
fact (revenue-ranked Grand shortlist, 12-tester/14-day rule, API 36 / Billing 8 deadlines,
15% fee, SDK versions and gating, the AGP9 landmine, the market whitespace). The concept
decision survives independent re-scoring and two fresh challenger concepts. The pricing,
ad-restraint, and Unity-pin decisions all survive direct attack. The prior session's research
was largely accurate.

What does **not** survive is the schedule as printed, the fun-gate as operationalized, and the
package's internal consistency:

1. **The Aug 24–28 launch window is a ~P20 outcome presented as the plan.** The plan's own
   stated maxima (apply Aug 15 + ≤7d review + submit Aug 24 + ≤7d first-release review) sum to
   **Aug 29–31 — outside its own window** — and the roadmap's "buffered inside the window"
   claim is arithmetically false. Production-access **rejection** (verified common even after
   compliant 14-day tests; +14 days per cycle) has no branch, no dates, and no trigger before
   Sep 5 anywhere in the plan. Realistic P50 launch ≈ **Sep 1–2**, P80 ≈ **Sep 12–16**;
   P(miss Sep 30 entirely) ≈ 7–11%.
2. **The D7 fun gate cannot fire.** Its fail action is simultaneously "pivot to Meowmelon"
   (FINAL_REPORT), "the concept stays — redesign pacing not premise" (roadmap gate row), and
   "concept pivot is out of scope" (risk register). Its metric (replay ≥1 level) is confounded
   by the instant-retry mechanic, its build contains only tutorial-difficulty content, and the
   pass bar was quietly eroded from the original "replay 3×". The safety net for the plan's
   self-declared biggest product risk is a paragraph its own execution documents override.
3. **The parallel-drafted files disagree in judge-visible places.** The bonus district judges
   are told to tap is named "Rooftop Line" in the judge instructions and "Night Harbor" in the
   store product, paywall, and entitlement map. The package id has three variants on the eve of
   the day it becomes permanent. The submission's headline ethical claim ("streaks are
   cosmetic") is falsified by the package's own economy CSV. FINAL_REPORT's top-line revenue
   summary ($500–900 base case) contradicts its own model ($253.51).

None of these require rethinking. All are fixable in roughly one to two focused days before
Aug 1–2. The 10 changes in §8 are that fix list, in priority order.

---

## 2. Critical findings (ranked by severity)

**C1 — The printed launch window fails its own arithmetic. (Schedule / HIGH)**
Submit 1.0 on Aug 24 (D24) + Google's stated "up to 7 days or longer" first-release review =
live as late as Aug 31; the window closes Aug 28. Launch by Aug 28 requires a ≤4-day review.
Even granting every upstream assumption, Monte Carlo P(public ≤ Aug 28) ≈ 36%; with verified
production-access rejection risk priced in, ≈ 20%. `roadmap_56_days.csv` D24's "buffered inside
the window" and D26's "the window holds" are false claims. The dominant unmodeled risk is
**application rejection**: Google's own page says the application is *graded* (engagement,
feedback acted on, questionnaire quality), and 2025–26 developer reports document full 14-day
tests denied for "testers not engaged enough," each denial costing a fresh 14-day test
(verified threads: r/androiddev 1q7n7qu, 1t7zjg6, 1kr1i7p — one dev lost 42 days to three
cycles). The roadmap has **zero rejection branch** and the risk-register contingency doesn't
trigger until Sep 5. Fix costs nothing: see change #1.

**C2 — The fun gate is contradicted three ways and cannot force a pivot. (Product / HIGH)**
FINAL_REPORT.md:190 "fail → pivot to Meowmelon" vs roadmap D7 GATE row "the concept stays per
the locked KEEP verdict" vs risk_register R-02 "concept pivot is out of scope (verdict
locked)." An executor following the operational documents can never pivot regardless of
evidence. Compounding: "replay ≥1 level" is confounded by <1s instant retry after designed
failures (the gate auto-passes with any functioning build); build 2 contains only L001–L005 at
90–97% first-attempt clear rates (tutorial content structurally cannot exhibit the boredom
failure mode); owner/judge/scribe are all the same person; and the bar was eroded from the
blueprint's "replay 3×." Also: the Plan-B claims are wrong — "80% of the stack" is ~47–55% of
*sunk build effort* at D7 (most of the commercial stack is unbuilt until D10–D19) and ~0% of
design deliverables; both rewind SKUs (the HAMM consumable ladder) die in a physics pivot
("rewind to last safe decision tick" requires the command log); and "ships even faster" is
false — a D7 pivot lands public ~Sep 3–10, and only if the same Play app entry/package is
reused, a load-bearing condition stated nowhere.

**C3 — Judge-visible commerce inconsistencies. (Submission integrity / HIGH)**
(a) Bonus district named **"Rooftop Line"** in submission_script.md:363/374/423, product_spec,
growth_aso_plan — but **"Night Harbor"** in monetization_spec, entitlement_map.json,
offering_and_placement_map.json, and the Play product description in revenuecat_configuration
D1-11 (catalog row 2 says "Bonus District 7"). Judges following the written instructions would
look for a district the paywall calls something else. (b) The offerings evidence screenshot
list (submission_script.md:248) omits `ofr_shop` (a permanent launch offering) while including
`ofr_core_b`, which the runbook deletes after the price test. (c) Three different rewind-sheet
eligibility rules across catalog ("level 11+"), spec/map (no level floor), and judge
instructions ("L004 or later"). (d) Four different judge promo-code quantities (15 / 25 / 25 /
+5). All trivially fixable; all embarrassing under judging.

**C4 — Package id has three variants on the eve of permanence. (Execution / HIGH)**
`com.yourstudio.catmetro` (FINAL_REPORT:206) vs `com.catmetro.game` (roadmap D1) vs
`io.catmetro.game` (github_issue_backlog:13; CM-009 reconciles only the latter two). The
application id is frozen at first AAB upload — scheduled for Aug 1. Also `@playcatmetro`
(FINAL_REPORT) vs `@CatMetroGame` (roadmap, backlog). Decide once, tonight, propagate.

**C5 — The submission text contains a falsifiable ethical claim. (Reputation / MEDIUM-HIGH)**
"Streaks are cosmetic" appears in submission_script.md:37 and :125 and is the stated
loss-aversion mitigation in onesignal_retention.md:89 — but economy_sources_and_sinks.csv
row 7 makes the daily gift **streak-scaled (30→80, resetting on break; 150-ticket climb-back
loss)**, and streak-risk pushes fire 6h before the midnight deadline with a local-notification
backup. A judge or journalist who cross-reads two files falsifies the headline ethical claim
in minutes. Same family: the paywall sells "Ad-free, guaranteed forever" — removal of ad
surfaces that do not exist in the free game. Fix per change #10.

**C6 — FINAL_REPORT's revenue summary contradicts its own model. (Honesty / MEDIUM-HIGH)**
FINAL_REPORT.md:171: "organic base case ≈ 3k installs, ~$500–900 net revenue … enough for
category awards (2025 calibration: winners at $1–2k)." The model it cites computes **$253.51**
(verified by recomputation; no scenario row lands in $500–900), the row itself says it "lands
short of a category win on revenue alone," and $500–900 is not inside $1–2k even at face value.
The artifact repeats it. The report also calls the $2k-optimistic case "where a Grand-Prize-
shaped revenue curve becomes conceivable" while the CSV shows that row $1,662 worse off
net-of-spend than doing nothing and "nowhere near" the $30k 2025 Grand calibration. For a
build-in-public entry planning radical numbers-transparency, this is the wrong sentence to
have in the flagship document.

**C7 — Missing Google Play requirements the plan never mentions. (Compliance / MEDIUM)**
(a) **Device verification** for new personal accounts (Play Console mobile app on a physical
Android device before publishing submissions — support answer/14316361): absent from all 34
checklist rows and D1 (grep-verified). Day-1-blocking if discovered late. (b) Judge promo
codes for one-time products require the app to **"integrate In-app Promotions"** and only
active buy options can be promoted (answer/6321495) — the plan treats promo codes as a
dashboard-only task; whether purchases-unity coexists with the In-app Promotions redemption
flow is unverified. If this breaks, the judge-access mechanism named in the rules breaks.
(c) Reviewer forms are sometimes emailed (occasionally to spam) and never appear in the
Console — no deliverable monitors email during review windows.

**C8 — The OneSignal cap promises are mathematically incompatible with the design. (MEDIUM)**
liveops_spec.md:200 promises "max 1 push/day, 3/week across all sources"; the design sends up
to 2/day from Journey 1 alone and ~14–19/week to an engaged streak-holder in a Cup week
(computed worst case). The permission soft-prompt copy promises "at most one reminder a day" —
broken by design for exactly the users who granted it. liveops also misstates the Growth
budget as "6 message steps EACH" and claims a "spare step" that doesn't exist (design is 6/6
total). And the journeys CSV's P0 `streak_risk` row triggers on `streak_at_risk`, an event
that exists nowhere in the taxonomy — configured from the CSV, the journey would never fire
(the retention spec's tag-based design is the working version).

**C9 — The original brief is not in the repo. (Auditability / MEDIUM)**
The "14 phases / 27 sections / ~23 appendices" master brief exists only as citations (Blueprint
source matrix [U01]; FINAL_REPORT:129). Exhaustive search of all PDFs, the appendix, and the
prior session's caches found no copy. Completeness cannot be verified by anyone, including the
developer. Corollary: FINAL_REPORT §27's "~150 findings" audit trail lives at
`/tmp/catmetro-extract/research_results.json` (verified present, 187KB — but volatile /tmp,
not the repo; it will not survive a cleanup), and FINAL_REPORT §6's cross-reference to a "full
per-criterion table" in `_working_concept_analysis.md` is **false** — no per-concept scores
exist anywhere in the repo, so the headline 8.08/7.10 scores are unreproducible as published.

---

## 3. Fact-check results — the 17 items (all re-fetched 2026-07-31)

| # | Claim | Status | Key source | Notes |
|---|---|---|---|---|
| 1 | Window Jul 31 8:00am – Sep 30 11:45pm PDT; judging Oct 1–13; winners Oct 21 (FAQ: Oct 22) | **CONFIRMED** (High) | revenuecat-shipaton-2026.devpost.com/rules; shipaton.com/faq | Verbatim. Winners-date conflict is real and unresolved (2 Devpost sources say Oct 21 9:00am). Judging ends **12:00pm** Oct 13. FAQ: don't publish before Aug 1. |
| 2 | Grand shortlist = total revenue reported in RevenueCat | **CONFIRMED** (High) | devpost.com …/rules | Verbatim: "The Sponsor will compare the total revenue generated by eligible Projects during the Submission Period, as reported in RevenueCat, to create a shortlist." Criteria: Early & Effective Release + Growth by numbers. "Highest revenue doesn't auto-win" is inference from structure, not rules text. |
| 3 | First public release in window; updates ineligible; US-accessible | **CONFIRMED** (High) | …/rules | Verbatim, incl. Galaxy Store. FAQ sharpens: pre-window Play release can't re-qualify via App Store. "Public release" is never formally defined; pre-registration is addressed nowhere (verified absence — plan's skip posture correct). |
| 4 | Prize table; Samsung non-cash | **CONFIRMED** (High) | devpost.com (prizes) | $100k / 30-20-10 / 25-15-5 / 15-10-5 ×4; Samsung = featuring + trip/billboard, no cash. **Gap:** plan's enumeration omits the NEW student-only Next Gen award; pool page says "$685,000+", shipaton.com says "$700k+ / 21 categories". |
| 5 | Verbatim criteria (Best Game, HAMM, Catvertising, OneSignal, #BuildInPublic) | **CONFIRMED** (High) | …/rules | All match. Additions the plan under-uses: HAMM also rewards a "**diverse mix of revenue streams**"; OneSignal's third criterion is "Resourcefulness **and creativity**", rewards "less-common or advanced" features; Catvertising is judged on "natural, useful, or additive rather than interruptive" — **no revenue-volume bullet** — and **requires RevenueCat Ads**. |
| 6 | One project can win multiple categories | **CONFIRMED-as-absence** (Medium) | …/rules + 2025 winners blog | No one-prize-per-project cap found (two targeted passes). Influencer: max one category (verbatim). **2025 precedent: no project won 2+ awards** — possible but unprecedented. Email to shipaton@revenuecat.com still worthwhile. |
| 7 | 12 testers / 14 continuous days; production review ≤7d; first release ≤7d | **CONFIRMED with material caveats** (High) | support.google.com answer/14151465, 9859751; r/androiddev raw threads | 12 is current (20→12 in Dec 2024; applies to personal accounts created after Nov 13 2023). Application review ≤7d matches. **But:** application is graded, rejections common (+14d/cycle); per-tester trailing-14-day rule (dropouts' days never accumulate); first-release tail is 8–15+ days on new accounts; codelab's "start by Sep 1" verified — and plan's Aug 1 start matches RevenueCat's stronger blog advice. NEW: device verification (answer/14316361) missed by plan. |
| 8 | API 36 from Aug 31 2026 (ext Nov 1); Billing 8 same date; 16 KB mandatory | **CONFIRMED** (High) | answer/11926878; developer.android.com/…/deprecation-faq; …/page-sizes | All three verbatim. Billing 8 applies to new apps AND updates, extension available. Brief's billing URL 404s (page moved to /deprecation-faq). Billing lib latest is 9.1.0; bundled 8.3.0 is compliant. |
| 9 | purchases-unity 9.7.0, Billing 8.3.0 | **CONFIRMED** (High) | github.com/RevenueCat/purchases-unity releases + VERSIONS.md | 9.7.0 released **today** (cadence ~weekly — re-pin at project start). Billing 8.3.0 double-verified via wrapped Android SDK. |
| 10 | Paywalls v2 + Customer Center (8.4.0+, device-only); Experiments Pro/Enterprise-gated; Placements 6.9.0+; Test Store 8.3.0+ | **CONFIRMED** (High) | docs.revenuecat.com (paywalls/installation, customer-center-unity, experiments-v1, targeting/placements, test-store) | All verbatim, incl. crash issues #745/#736/#732 all real, all open, all Android paywall-UI. **Nuance the plan misses:** current RC pricing has no free/starter split — **Pro is the default plan, free under $2,500 MTR, "all features included"** — so the Experiments gate likely costs nothing at hackathon scale (verify in dashboard Day 1 as planned). |
| 11 | RC Ads = public-beta tracking layer; AdTracker ≥9.1.0; no server-verified rewards in Unity; AdMob module not Unity | **CONFIRMED** (High/Medium) | docs.revenuecat.com/ad-monetization (+/admob, /rewards); AdTracker.cs in repo; changelog 2026-03-25 | PascalCase TrackAd* API verified in source — marked "**Experimental: unstable**". Unity absence from convenience-module and SSV docs is confirmed-by-omission (Medium). Access tied to Charts v3 enablement; approval latency undocumented. Ad revenue does NOT count toward RC MTR. |
| 12 | OneSignal 5.3.2; Growth $19/mo = 3 journeys / 6 steps; custom events paid-only; capping Enterprise-only; no quiet hours | **CONFIRMED, one reading corrected** (High) | github releases; onesignal.com/pricing; documentation.onesignal.com | 5.3.2 latest. **Pricing tooltip says step limit is per-journey** ("Number of message steps in a Journey") — the plan's 6-total reading is stricter than necessary (safe, but liveops' "6 each" phrasing is accidentally right where the brief is conservative; the "spare step" claim is still wrong under the brief's own rule). Branching = Growth+ (lapse ladder needs Growth anyway). Custom events: paid plans, Unity 5.2.0+, may need support enablement. Capping Enterprise-only (pricing table). Quiet hours: absent for push (Medium, absence-based); Time Window randomizes 0–15min. RC integration writes **tags, not custom events**. Growth is "starts at $19/mo" (usage-scaled). |
| 13 | 6000.3.17f1+ = Gradle 9 / AGP 9.0 breaking; 6.3 LTS ≈ Dec 2027 | **CONFIRMED** (High) | unityreleases.com/releases/6000.3.17f1; docs.unity3d.com 6000.3 Gradle table; discussions.unity.com AGP announcement | Exact: Gradle 9.1.0/AGP 9.0.0 from .17f1 (Jun 4–5 2026); .1–.16 = Gradle 8.13/AGP 8.10.0. 16 KB + API 36 are stream-wide (in .16). **AdMob Unity 11.3.0 confirmed broken on AGP9** (googleads-mobile-unity #4212, open, no fix) — the pin is not just prudent, it's forced. Pin forfeits only the .19 libcurl CVE-2026-27135 mitigation (low relevance). **Plan error: Unity 6.3 min Android API is 25, not the brief's 24.** |
| 14 | Play fees US/UK/EEA since Jun 30 2026: 10% + 5% = 15% | **CONFIRMED** (High) | android-developers.googleblog.com/2026/06/play-expanded-billing; answers 112622, 16954621 | US explicitly included. First $1M 10%; +5% when using Play billing; external billing permitted (external-offer fee details partly "forthcoming"). Rollout extends AU/JP Sep 30 2026. 15% correct for this app. |
| 15 | One-time promo codes work for one-time IAP | **CONFIRMED with caveat** (High) | answer/6321495 | Yes — but requires "**integrate In-app Promotions in your app**" and only active buy options; 500 codes/quarter (non-subs). Plan misses the integration requirement (see C7b). |
| 16 | No cat metro/route-switching puzzle on Play; comp store data | **CONFIRMED whitespace; CHANGED comps** (High) | Live US Play listings (embedded install counts machine-readable) | Whitespace holds across 6 queries. Every claimed figure verified exactly (Mini Metro 3,638,982/$0.99/4.63★; Railbound 211,486/$4.99 — note Android rating only 4.1; Arrows 103,631,506/4.83★ with the verbatim complaint; Bus Traffic Fever 15,357,007/3.72★ verbatim complaints; Neko Atsume 13.6M/4.78★/≤$3.49; Cats&Soup 42.7M; Trainyard gone; Mini Motorways no Android; STATIONflow desktop-only). **Missed comps:** Cat Train Tycoon (~547K installs, ~Jul 2025, ranks #1 for most cat+train queries — real ASO adjacency), Bus Jam Meow (DoubleUGames — big publisher entering cat-transit puzzles), Metro Connect – Train Control (1.1M, non-cat). "Cat Metro"/"Meowtro" clean on both stores. |
| 17 | 31.85/12.18/5.35 is 2022 AppsFlyer; current medians ~22/4/0.7 | **CONFIRMED vintage; comparison is a category error** (High) | GameAnalytics 2026 benchmarks PDF (investgame.net mirror); Mistplay citing AppsFlyer Q3-2022 | Old figure = AppsFlyer **puzzle-genre averages**, 2022. New figure = GameAnalytics **all-genre per-game medians** over ~16k mostly-small games. Apples-to-oranges (genre mix + statistic type + vintage); the deck itself proposes 35/15/5 as a strong-game target. Plan's D1 ≥28%/35% targets are still sane, but the "puzzle retention collapsed to 22/4/0.7" implication is unsupported. Also: Appodeal $9–16 endpoints not pinnable to the primary (Low conf.); "casual Android ~$0.95 CPI" not found in the Adjust report cited; AppTweak 16% is all-category — games run 5–7%. |

Fact-tally: **113 CONFIRMED / 1 CHANGED (missed comps) / 2 UNVERIFIABLE** (2025 winner's
"organic+ASO" channel mix — inference, numbers verified; RC-OneSignal-integration plan gating —
no gating statement exists in current docs, likely free under Pro).

---

## 4. Soft-spot verdicts (A–H)

**A. Catvertising — NOT a self-own. Severity Low. Keep, with 4 edits.**
The verified rubric has **no revenue-volume bullet**; it scores "natural, useful, additive
rather than interruptive," integration "alongside purchases," and "an experience users don't
hate" — a rewarded-only stack with hard caps, a 3-decline→24h mute, and full AdTracker
instrumentation is arguably the highest-alignment reading, and marginal entry cost is ~zero
(the stack ships anyway for Model B + HAMM's "diverse mix" bullet). **But the plan leaves its
best material unscheduled:** the two concepts its own map scores 5/5 ("flagship for the
Catvertising writeup" bonus_gold_train; "strongest pure Catvertising narrative"
poster_wall_gallery) appear in no roadmap cell, and bonus_gold_train's "Week 3–4 post-launch"
window straddles the Sep 11 feature freeze — an internal contradiction. The five shipped
surfaces are the five most generic rewarded placements in mobile; the restraint narrative
alone must carry the entry. Also: submission_script.md:110's "Ad revenue therefore lands in
the same RevenueCat dashboard as IAP revenue" must be verified at the D10 spike (the package's
own A3 warns RC Ads must not be assumed to contribute to RC-reported revenue). Edits: schedule
poster_wall_gallery into Week 6 above levels 36–40 in the existing cut-line (or delete the
"flagship" framing); fix row 8's timing; condition the dashboard sentence; add a
rental→purchase conversion exhibit to close the "effective = $34?" flank.

**B. Aug 24–28 launch — NOT achievable as a plan. Severity High.**
See C1. P50 = Sep 1–2; P80 = Sep 12–16; P(≤Aug 28) ≈ 20–36% depending on generosity;
P(miss Sep 30) ≈ 7–11%. The plan's own "3–5 weeks total time-to-store" fact spans Aug 22–Sep 5
from an Aug 1 start — its midpoint is outside its own printed window. The Aug 1–2 closed-test
start is decisively right (the codelab's Sep 1 is a bare-eligibility bound: a Sep 1 start has
zero rejection capacity and near-zero live data), and it is the strongest schedule decision in
the package — but the plan spends the earned margin badly: 8 idle days sit between clock
completion (Aug 15) and production submission (Aug 24) on the review-critical path. Stress
test: Sep 10 launch keeps ~$142 of the $253 base case and kills D30 cohorts; Sep 20 keeps ~$73
and kills the price experiment, District Cup, and v1.1. Eligibility survives ~90–96%. Fix: see
change #1 (submit-on-grant re-sequencing roughly doubles in-window probability at zero cost).

**C. Core loop / D7 gate / Plan B — gate is theater as written. Severity High.**
See C2. Steelman that stands: deterministic tap-to-toggle with consequence deferred 1–3s at
~0.2Hz is a *different feedback-loop class* from the cited Arrows comp (~1Hz tap-to-release
with per-tap payoff); "103M installs prove the input class" proves the motor input, not this
loop. The D7 build (greybox, majority-audio reward with a sound-off primary persona, purr
meter at P1, tutorial-band content) tests the boredom-riskiest configuration and cannot
attribute a failure to concept vs missing juice. **Bonus defect found:** all three shipped
anchor levels violate the LOCKED 45–90s session invariant (timeLimits = 20 / 32.5 / 37.5s;
solver-optimal ~6–19s vs the validator's required 40–75s) — the fun-thesis phase table
describes levels that don't exist. Plan B: viable only as a same-app-entry pivot with an
honest ~50%-of-sunk-effort framing and a Sep 3–10 launch; "80% of stack, ships even faster"
is false. Fix per change #2.

**D. Revenue scenarios — arithmetic essentially perfect; two framing defects. Severity Low-Medium.**
All 9 rows recomputed: every money-chain and DAU-chain figure reproduces within 0.25% (most
0.00%) exactly per formula_notes, including the power-law ADPI derivations, paid-cohort blends,
and the $33.30 / 6.7% ROAS conclusion ("ROAS never exceeds 19%" holds across all six paid
rows). The plan's spend advice is consistent with its own math (gated tiers, "spend as
research… or not at all"; growth plan contains zero paid-UA advocacy). Defects: (a)
**FINAL_REPORT:171's $500–900 summary contradicts the model** (C6); (b) optimistic-tier CPIs
($0.80/$0.90) sit *below both cited benchmark poles* while A1's text claims "between and
above" — compounding-optimism inside the paid-optimistic rows (which still conclude
don't-spend, so bounded); (c) paid rows apply 37-day uniform-arrival ADPI to a cohort the
brief locks to ≥Sep 1 (~30 days) — stated ROAS is an upper bound (strengthens the no-spend
conclusion, but the figure isn't reproducible under the file's own constraint); (d) minor
economy-CSV arithmetic drift (engaged D1 = 485 not 510; catalog exhaustion ~D16–17 not ~D24).

**E. Concept anchoring — process dirty, outcome sound. Severity Medium.**
The working draft pre-wrote the verdict ("KEEP C1 WITH MATERIAL REDESIGNS") before
verification; the challenger set carried each challenger's fatal flaw in its own description;
the criterion set silently changed post-verification (20 criteria drafted → 23 scored, with
C1-favoring additions like "AI pipeline"); and **FINAL_REPORT:129's cited "full per-criterion
table" does not exist** — the 8.08/7.10 scores are unreproducible from the repo. HOWEVER: an
independent re-scoring with weights committed before reading the plan's (revenue 25 / ship 20 /
fun 15 / monetization 15 / awards 10 / differentiation 10 / virality 5) reproduces Cat Metro #1
(7.05 vs Meowmelon 6.25); no defensible minimal weight change flips C1 vs C4 (a flip needs
~14–15 points stripped from award-fit axes in a hackathon that literally awards Best Game and
Design); a content-level leakage test on the draft comes back clean (zero verification-only
facts; consistent mtimes); and two purpose-built challenger concepts (ambient widget cat
collector 6.65; cat seat-jam 6.30) both lose to C1 on exactly the axes that are externally
verified (whitespace, name, award surface). Keep the decision; repair the audit trail
(change #9).

**F. $6.99 — right price, wrong rationale. Severity Low.**
The stated reason ("Grand shortlist = revenue") is arithmetically hollow: the whole
$6.99-vs-$4.99 decision is worth **$39.53 net in the base case** against a $29,763 gap to the
2025 Grand calibration (0.13%). The claim "comps support $6.99 complete editions" cites
evidence the package doesn't contain (no comp above $4.99 anywhere in it). What actually
defends $6.99: break-even elasticity is forgiving ($6.99 wins unless conversion drops >28.6%);
$4.99 breaks the ladder (an everything-tier $0.99 below its own two themes at $5.98, tied with
rewind_20 — the exact decoy-confusion grounds on which two SKUs were cut); and the one payer
anchor in the package ($7.26 casual D90 ARPPU) sits just above $6.99. The PW01 A/B cannot
reach significance at base scale (~300–490 views/arm vs ~7,700 needed; ~6 vs ~8 purchases) —
the package discloses this, but monetization_spec:522's "validated, not assumed, via
experiment" overclaims. PW06 (supporter attach) is the equal-dollar, zero-risk lever. Keep
$6.99; rewrite the rationale in all four places (change #6).

**G. Unity pin — sound; in fact forced. Severity Low.**
6000.3.16f1 has 16 KB + API 36 (stream-wide), sits in LTS to Dec 2027, and **AdMob Unity
11.3.0 is confirmed broken on AGP9** (#4212, open, no fix) — so .17f1+ is not currently an
option at all. The pin forfeits only the .19 libcurl/nghttp2 CVE mitigation (UnityWebRequest
surface; the SDKs use their own native networking). Two corrections: the unpin trigger "until
GMA/RC/OneSignal confirm AGP9 compat" is miscalibrated (RC/OneSignal have made no statement
and likely never will — the observable trigger is a GMA release closing #4212 + one green
smoke build); and **min API must be 25, not 24** (Unity 6.3's documented Android minimum).

**H. Ethics vs commerce — Model B is right for this event; the *claims* overreach. Severity Low.**
Full quantified generosity cost in the base case ≈ **$125 gross** (~$58 forgone interstitials —
which would land in AdMob and be invisible to the RC-reported Grand shortlist anyway — ~$25
consumable cannibalization, ~$42 cut streak-saver IAP) against a 118× gap to Grand. Reversing
generosity cannot buy Grand but would forfeit Catvertising (whose entire entry is the
restraint), Best Game's "monetization fit," and the store-listing differentiation. The genuine
exposure is HAMM's "drive real revenue" read literally at ~42 payers/$262 gross — pre-empt by
quoting the 2025 category calibration next to the number. The real risk runs the other way:
overstated absolute claims ("streaks are cosmetic" — falsified by the package's own economy
CSV; "Ad-free, guaranteed forever" selling removal of nonexistent surfaces; a post-win paywall
the spec itself calls "peak honeymoon" timing). Keep the model; fix the claims (change #10).

---

## 5. Internal inconsistencies found (the ones the previous session missed)

Beyond the three it caught (offering IDs, package types, ragged CSV rows), this audit found
**~45 distinct cross-document defects**. The complete set with file:line detail is in §2/§4
above and the list below (severity-ordered; High first):

1. `roadmap_56_days.csv` D24/D26 — "review …buffered inside the window" / "the window holds": false arithmetic (Aug 24+7d=Aug 31>Aug 28). Same defect at FINAL_REPORT:187 and CM-032.
2. FINAL_REPORT:190 vs roadmap D7 GATE row vs risk_register R-02 — three contradictory fun-gate fail actions (pivot / tune-only / pivot-out-of-scope).
3. submission_script:363/374/423 + product_spec:398 + growth_aso_plan:143 ("Rooftop Line") vs monetization_spec:13/425 + entitlement_map:66 + offering_and_placement_map:165 + revenuecat_configuration D1-11 ("Night Harbor") vs monetization_catalog row 2 ("Bonus District 7") — bonus-district name three ways, judge-facing.
4. FINAL_REPORT:206 `com.yourstudio.catmetro` vs roadmap D1 `com.catmetro.game` vs backlog:13 `io.catmetro.game`; `@playcatmetro` vs `@CatMetroGame` — identity three/two ways on the day it becomes permanent.
5. Four incompatible post-launch content schedules: roadmap W6+CM-039 (31–40 in v1.1 by Sep 11) vs liveops:112/116/118 (31–35 only, one patch Sep 9, second patch Cut — makes the D42 gate criterion unsatisfiable) vs product_spec:605/726 (31–60 in two updates Sep 12 + Sep 22 — Sep 22 violates the plan's own freeze and Sep 19 submission cutoff) vs growth_aso_plan:353 (31–60 post-event).
6. liveops:176 "6 message steps EACH" + :183 "one spare" vs brief:44 and onesignal_retention:19/54 "6 total, 6/6 used" — budget misread; the "spare A/B step" would exceed the plan's own budget. (Live pricing tooltip says per-journey, making the brief conservative — but the package must agree with itself.)
7. liveops:200 global cap "1 push/day, 3/week" vs a design that sends up to 2/day (J1) and ~14–19/week worst case; soft-prompt copy "at most one reminder a day" (retention:216) broken by the same design; journeys.csv daily_challenge "1/day; counts toward 3/week global" self-contradiction.
8. `streak_at_risk` (journeys.csv row 3, P0 journey entry) exists in no taxonomy — journey unbuildable as specified; retention spec's tag design is the working one.
9. `EXP-M01…M08` namespace (catalog experiment-hooks; also revenue_scenarios, PW05 cross-ref) defined nowhere; PW01–07 and E01–E26 are the declared-disjoint schemes; EXP-M04/07/08 have no counterpart; catalog hooks a theme-price test (EXP-M07) that E09/E23 explicitly forbid.
10. Rewind-sheet eligibility three ways: catalog "level 11+" vs spec/map "attempt≥2 + progress≥40%, no floor" vs submission_script:365 "L004 or later".
11. submission_script:248 offerings screenshot omits `ofr_shop`, includes to-be-deleted `ofr_core_b`.
12. Judge promo-code quantity four ways: 15 (play checklist:33) / 25 (rc config D2-51) / 25 (rc impl:66) / +5 fresh (submission:422).
13. FINAL_REPORT:171 "$500–900 net… enough for category awards ($1–2k)" vs its own CSV ($253.51; row says "lands short"); artifact repeats it; "$500–900 inside $1–2k" is false on its face.
14. FINAL_REPORT:175 "20-row test matrix" vs actual 22 (README correct); FINAL_REPORT:169 "8+ evaluated" vs actual 12 (README correct); FINAL_REPORT:184+README:57 "~45 issues" vs actual 49; AUDIT_PROMPT:41 "~5,400 lines" vs actual 4,033; README:39 "10 cut products" vs 9 cut + 1 P2 web SKU; README:56 "~25 items" vs 32; "54-row runbook" vs 56.
15. README:33–35 documents 3 `#`-prefixed CSVs; actually **7** (paywall_experiments has 5 comment lines, revenuecat_configuration 3 — "skip first line" parsing fails on those).
16. rc_configuration D1-29 reconciliation note describes a `$rc_lifetime` discrepancy that doesn't exist in the catalog.
17. Catalog launch-priority P0 for supporter/themes (+ ad map P0 ×5) vs monetization_spec/entitlement_map P1 — two artifacts would triage differently under pressure.
18. `cm_all_access_499` (real Day-1 Play product per D1-17) missing from the catalog that inventories even cut SKUs.
19. Quiet-hours boundary three ways: retention 21:00 hard stop vs liveops:182 "09:00–22:00" vs CSV "never 22:00–09:00".
20. J3 entry: liveops `level_stuck` (undefined anywhere) vs retention's filtered `level_failed`; J2/J3 exits reference `level_completed`/`feedback_submitted`/`purchase_failed` as OneSignal events the taxonomy never forwards (tag/analytics-only) — retention's segment workaround is the working design; CSV/liveops were never updated.
21. purchase_issue mechanism: +2h local notification (retention, matches CSV) vs IAM + 4h local (liveops).
22. journeys.csv has no ships-as/active column — standalone it designs 13 journeys/~12 steps, unbuildable on Growth; only retention §2's triage makes it legal.
23. event deep links `catmetro://event` (CSV) vs registered route `catmetro://event/{id}` (retention:273 + all copy rows) — falls back to Home.
24. ad map row 17 "District Cup from Week 5 ~Sep 21" — Cup starts ~Aug 31; Sep 21+ is the no-new-routes freeze; row 8 bonus_gold_train "build Week 3–4 post-launch" straddles the Sep 11 freeze.
25. Milestone map M5/M6 dates↔day-numbers wrong (Sep 21 is D52 not D49; M6 dates lie outside the 56-day map).
26. D1 "clock starts" vs its own acceptance "≥8 of 12 opted in" (clock needs 12); brief spine asserts "(started Aug 1)" as fact; D15 cites a "brief window Aug 15–16" that exists in no brief; gate list "D7/14/21/28/35/42" omits D24 and D54 and includes a D28 gate row that doesn't exist.
27. No `district` field in level_schema.json / example levels despite district-based unlock being LOCKED; bonus district L901–L910 breaks any implicit id-range mapping.
28. example_levels.json `validatedAt` dates are all in the future (Aug 2–Sep 5) for a field defined as "date of last validator pass"; product_spec:576 claims anchors "already validated."
29. Anchor levels L001/L006/L018 timeLimits 20/32.5/37.5s violate the LOCKED 45–90s invariant and the validator's own 40–75s solver-optimal band (product_spec:384/389 vs :537).
30. report_artifact.html:150 invents "Pro 20" journeys (nowhere in any source doc — package claims "no invented figures"); :187 "20 files · ~5,400 lines" (actual 30 files, ~4,800).
31. agents prompts:99 instructs emitting `soft_prompt_viewed`/`permission_result` — taxonomy names are `push_soft_prompt_viewed`/`push_permission_result`; E26 guardrail uses `domain=paywall` vs everything else's `rcui_paywall`.
32. retention:72/93 claims the taxonomy lacks `onesignal_event` on daily_unlocked and files an action item — the CSV already has it (stale delta).
33. Economy CSV: engaged-D1 components contradict stated rates (510 vs derivable 485); two 7× products off by 2; "catalog cleared ~D24" vs own rates ~D16–17.
34. Price-test cells: roadmap "7 days each" vs CM-037/liveops 6-then-7 days; ASO slot E17 can run into the freeze (guard written only for E18); liveops separately stops all experiments Sep 21/24.
35. Priority labels for payer_thanks/purchase_issue/event_ending/review_coordination differ CSV↔retention spec (breaks the "3 variants per priority message" claim under one reading).

**Clean checks worth recording** (attempted and passed): product IDs, prices, entitlement IDs,
offering composition, placement IDs and wiring, rewind free-daily rule, and all five ad-cap
sets are **identical across every file** (the core commerce spine is solid); all 4 JSON files
parse; `example_levels.json` **passes real ajv draft-2020 validation** against
`level_schema.json` (schema itself compiles; referential integrity, star ordering, mechanic
ordering, teaching metadata all verified by custom checker); all 14 CSVs are structurally
clean; E01–E26 and PW01–PW07 unique and disjoint; 45 events exactly as claimed; every
offering-map/ad-map/experiment event name resolves to the taxonomy; all 24 "Loopline" hits and
all 9 "ChronoRoute" hits are deliberate; roadmap day↔date mapping is perfect (all 41 rows);
day-of-week claims all correct; 30 levels = 6×5 and bonus-count 10 consistent everywhere;
district Cup dates consistent (except ad-map row 17); notification copy = 31 variants mapping
exactly to journeys; judging-period logistics (codes expire post-Oct 13, no experiment
mid-flight) coherent.

---

## 6. Completeness gaps vs the original brief

**The original brief is not in the repo** (High) — the 14-phase / 27-section / ~23-appendix
requirement exists only as citations (Blueprint source matrix "[U01] Master deep-research and
execution brief. User-supplied text file; 2026-07-30"). Completeness is therefore
**unverifiable by construction**; the numbers themselves could be misremembered. Commit the
brief as `deliverables/ORIGINAL_BRIEF.md`.

Against what is checkable (FINAL_REPORT's own 27-section structure + README promises + the
predecessor appendix):

- **All 35 indexed files exist**; the 27-section numbering is continuous; 13 of 27 sections
  (48%) are pointer stubs, but **every pointed-to file verifies at promised depth** except §27.
- **§27 "Complete Source List" is a stub pointing outside the package** at volatile
  `/tmp/catmetro-extract/research_results.json` (present tonight, 187KB — one reboot from
  gone) and at session transcripts that are already unrecoverable. The predecessor shipped a
  machine-readable `sources/source_matrix.csv`; the new package dropped that artifact class —
  a regression.
- **Silently lost from the Loopline appendix:** the three reusable `SKILL.md` agent workflows
  (launch-content, level-generation, unity-feature) — no successor, no conscious-drop record
  (7 of 9 appendix asset classes were properly superseded). Also partially lost: the
  validator's numeric classification thresholds and beam-evaluation weights (11-stage pipeline
  survives; the constants don't, and no file says the appendix remains authoritative).
- **`liveops_spec.md` is orphaned** from the 27-section index — no numbered section points to
  it (README only).
- **Appendix count:** 20 delivered vs "~23" claimed — the gap of ~3 aligns exactly with the
  dropped source matrix, skills, and a standalone validator appendix. Suggestive, not provable
  without the brief.
- Self-described volumes drift: "~5,400 lines of specs" → actual 4,033; "~34 files" → 35 (fine);
  count claims per §5 item 14.

---

## 7. Outcome assessment (Task 5)

**7.1 Probability distribution if executed exactly as written** (estimate, calibrated to the
schedule Monte Carlo, the 2025 field of 812 submissions with a 2026 pool that doubled — so
plausibly 1,500+ entries — and ~50+ prize slots across ~21 categories):

| Outcome | Probability | Reasoning |
|---|---|---|
| Ships nothing (no public release by Sep 30) | **~8%** | 7–11% modeled miss risk from review/rejection tails, tempered by the Sep 30 backstop discipline already in the plan; add solo-dev life risk. |
| Ships late & unpolished (public Sep 15+, thin live data) | **~22%** | P80 launch is Sep 12–16; a rejection cycle or fun-gate surgery lands here. Eligibility survives; most data-driven award cases don't. |
| Ships on time-ish (≤~Sep 5), wins nothing | **~50%** | The modal outcome. Execution quality is above the field's median, but 2025 produced zero multi-award winners and every category has hundreds of entrants; craft categories are subjectively judged. |
| Wins ≥1 category prize (any tier) | **~19%** | Five P0 categories × a polished, evidence-heavy, honestly-documented entry with daily BIP is a genuinely strong ticket — #BuildInPublic ($30k tier) and OneSignal (constraint-as-story fits "Resourcefulness and creativity" verbatim) are the best shots; Best Game/Design/HAMM/Catvertising are real but crowded. |
| Wins Grand Prize | **~0.5%** | Revenue shortlist. Base case ~$253, ceiling ~$2.7k, vs $30k for the 2025 winner in a smaller field. The plan's own P1 designation is correct; the report's "Grand-Prize-shaped curve" line is not. |

**7.2 Single highest-leverage change:** decouple the first production submission from the
polish path — **submit the commercial-beta build the moment production access is granted
(P50 ~Aug 20–21), managed publishing ON, publish held**, then ship the polished build as a
day-1 update. It costs nothing, starts the riskiest review 3–4 days earlier, and roughly
doubles P(launch inside Aug 24–28). (Second: reconcile the fun-gate fail action + pre-register
the harder gate — it protects everything the schedule protects.)

**7.3 Scope theater — looks impressive, moves nothing. Cut or shrink:**
- **The experiment apparatus beyond ~5 tests.** 26 backlog experiments + 7 paywall experiments
  for a game whose base case is 81 installs/day, where the package's own power math shows the
  flagship A/B needs ~20× the traffic to decide. Keep PW01 (directional), PW06, EXP the
  send-time test, and the ASO listing iterations; mark the rest post-event. The three-way
  experiment ID namespace (E/PW/EXP-M) is bureaucracy for tests that will never run.
- **The 10-agent build fleet prompt pack** (172 lines of role fences and merge gates for a
  solo dev who is also the only human in the loop). Useful as inspiration; theater as process.
  One engineer-agent + one reviewer-agent covers reality.
- **The 100-level framework and bands 41–60 detail.** Post-event content specced during the
  window's scarcest weeks; the roadmap's own freeze already implies it — delete the Sep 22
  update and the growth doc's 1.2 planning from in-window scope (this also resolves
  inconsistency #5).
- **Galaxy/Stripe/Noise stubs are already correctly gated** (P2, go/no-go dates) — leave as-is;
  they're cheap options, not theater.
- What is *not* theater despite its size: the deterministic-sim/solver/CI pipeline (it's the
  content factory, the marketing capture rig, and the Devpost story), the revenue-scenario
  file (it's the honesty spine), and the daily BIP cadence (it's a $30k category).

**7.4 Is the volume solo-feasible in 8 weeks?** Borderline-yes with AI assistance, and only
with the pre-designated cuts. Recomputed: **412 est-hours across 56 consecutive days, zero
rest days, zero contingency days**; weeks 1–3 sustain 57–59h; the ≤10h/day ceiling is achieved
by packing (D17 fits all six SKUs end-to-end including the fulfillment ledger, refund
revocation, Play product creation, and badge art into 9h; D2 fits the entire deterministic
core + RC/EDM4U install into 8h — those are 12h+ days as scoped). The gate fallbacks are real
scope valves, but the schedule holds only if nothing goes wrong for 56 straight days. Add 4–6
explicit contingency/rest days by pre-cutting (theme #2, levels 36–40, District Cup round 1
are the named sacrifices) and treat week-1–3 hours as the burnout risk they are.

**7.5 Embarrassment / policy / eligibility exposure:**
- "Streaks are cosmetic" (submission text) is falsifiable from the package's own files — fix
  before any judge reads it (C5).
- "Ad-free, guaranteed forever" sells removal of ads that don't exist — ready-made critical
  headline; demote to the trust footer.
- FINAL_REPORT's $500–900 claim, if quoted in a BIP post, contradicts the model the same repo
  publishes — fatal to the "honest numbers" moat if caught (C6).
- Judge promo codes may silently require In-app Promotions integration — if discovered at
  submission time, the rules-mandated judge-access path breaks (C7b).
- Recruiting the 12 testers via "Shipaton Discord" tester-exchange channels: a
  self-identified Play reviewer publicly warns tester-exchange patterns can flag accounts by
  association — recruit from personal network first (unverified-identity source, but the
  graded-application page is consistent with it).
- Client-side ad-reward grants (no SSV in Unity) are a fraud surface the package already
  mitigates (ledger, caps, callback-only grants) — acceptable, documented.
- No AI-disclosure requirement exists in the rules (verified; 2025 winner was AI-built) — the
  build-in-AI-public story is safe.
- Publishing before Aug 1 would break eligibility per the FAQ — moot at an Aug 24+ launch, but
  the D1 "do NOT publish" note should cite it.

---

## 8. The 10 changes, in priority order

1. **Re-sequence the launch critical path and print honest dates.** Edit roadmap D21/D24 +
   google_play_checklist row 29: submit the first production release the day production access
   is granted (managed publishing ON, publish held) using the commercial-beta build; polish
   ships as an immediate update. Delete "buffered inside the window" (D24) and "the window
   holds" (D26). Reprint: target Aug 24–28 (best case), **P50 Sep 1–2, P80 Sep 12–16**. Add a
   dated rejection branch (reject ~Aug 20–22 → keep all testers opted in through *grant*, not
   application → fix stated reasons → re-apply when the trailing-14-day criterion re-satisfies
   → P50 re-grant ~Sep 8 → launch ~Sep 13–15) and move R-01's trigger from "Sep 5" to
   "the day a rejection email arrives."
2. **Make the fun gate able to fire, and fix what it tests.** One fail rule in all three
   documents: YELLOW = 48h surgery + re-gate D9; RED = execute a pre-written one-page Meowmelon
   runbook (same Play app entry and package — state this; rewind SKUs deleted; new target
   Sep 3–8; replace "80% of stack / ships even faster" with "~50% of sunk effort, ~0% of
   design deliverables"). Pre-register the harder gate publicly in BIP post 1/56: ≥6/12
   testers return unprompted on a second day (pushes off); ≥4/12 replay an already-**won**
   level (excludes fail-retries by construction); median session ≥3 levels; quit-without-retry
   <50%; 2/4 missed = YELLOW, 3/4 = RED; a named outside person confirms the tally. Add 2
   greybox stress boards (~difficulty 0.30) to build 2; promote the purr meter (or a visual
   equivalent) to P0 for the sound-off persona. Fix the anchors: re-author L001/L006/L018 to
   the 45–90s invariant or amend the invariant to a per-band table.
3. **Fix every judge-visible commerce inconsistency in one pass:** one bonus-district name in
   all six files; submission screenshot list → `ofr_core, ofr_themes, ofr_rewind, ofr_shop`;
   one rewind eligibility rule everywhere; one promo-code quantity (25); and add a CM issue +
   D17 acceptance criterion: "In-app Promotions integrated; one promo code redeemed end-to-end
   on a clean device" (answer/6321495 requirement).
4. **Freeze identity tonight:** pick the package id (recommend `com.catmetro.game` — already in
   the roadmap D1 and backlog CM-009 orbit) and the handle (`@CatMetroGame`), propagate to
   FINAL_REPORT §24, roadmap, backlog. Package id is unchangeable after the first upload.
5. **Reconcile the OneSignal layer:** correct liveops "6 steps each"/"one spare" to the 6/6
   total allocation; replace the impossible "1/day, 3/week" global cap with the true worst
   case (≤2/day; state the weekly reality or redesign J1); fix the soft-prompt copy ("about
   one a day" is still false — write "a daily nudge and a streak warning at most"); add a
   `ships_as` column to onesignal_journeys.csv; replace `streak_at_risk` with the tag-based
   entry the retention spec specifies (or add the event to the taxonomy + adapter); fix event
   deep links to `catmetro://event/{event_id}`; align the 21:00 quiet boundary and the
   purchase_issue/feedback_request mechanisms across the three artifacts.
6. **Correct the flagship numbers and rationales:** FINAL_REPORT:171 → "base ≈ $253 net —
   short of the $1–2k 2025 category band on revenue alone; competes on craft and narrative";
   drop "Grand-Prize-shaped"; fix 20→22 rows, 8+→12 concepts, ~45→49 issues, ~5,400→~4,000
   lines; rewrite the $6.99 rationale in the four places per soft-spot F (bounded downside +
   ladder coherence + $7.26 anchor — not "Grand shortlist revenue" and not "comps support
   $6.99"); soften "validated via experiment" to "stress-tested directionally (pre-registered
   as non-significant at our scale)"; add the benchmark caveat that 22/4/0.7 are all-genre
   medians, not puzzle figures.
7. **One post-launch content plan:** adopt the roadmap version (31–35 in Week 5, 36–40 in v1.1
   by Sep 11, 41–60 post-event); edit liveops (restore the second patch or amend the D42 gate
   criterion), product_spec (delete the Sep 22 update), growth_aso_plan (already post-event —
   align wording); fix milestone M5/M6 day↔date mapping; extend the ASO freeze guard to every
   slot ("no listing experiment past Sep 25").
8. **Absorb the missed Play requirements into Day 1:** device verification via the Play
   Console app on a physical device (before any track publish); daily email+spam monitoring
   during both review windows; record whether the fallback "pre-verified personal account"
   predates Nov 13 2023 — **if it does, it legally bypasses the entire 12/14 gate and should be
   evaluated as the primary path**; raise the tester pool to 18–20 recruited primarily from
   personal network (not tester-exchange channels); min API 25 (not 24); re-check the
   purchases-unity pin at project start (9.7.0 shipped today; cadence is weekly).
9. **Repair the audit trail:** copy `research_results.json` into `deliverables/data/`
   (regenerate a source-matrix CSV if time allows); commit the original master brief as
   `ORIGINAL_BRIEF.md`; publish the 6×23 per-criterion concept matrix or delete FINAL_REPORT
   :129's false cross-reference and restate the totals as structured judgment; add the
   §6 disclosure that the criterion set changed post-verification (name the additions); set
   `validatedAt` to null until the validator actually runs; fix report_artifact.html's
   invented "Pro 20" and its file/line counts; document all 7 comment-prefixed CSVs in README.
10. **Sharpen the two narrative categories:** Catvertising — schedule `poster_wall_gallery`
    into Week 6 above levels 36–40 in the existing cut-line (or delete the "flagship" framing
    from the ad map); fix bonus_gold_train's freeze-straddling build window; make the "ad
    revenue lands in the RC dashboard" sentence conditional on the D10 spike; add the
    theme-rental→purchase conversion exhibit. Ethics — de-couple the daily gift from streak
    length (flat 50/day) **or** rewrite "streaks are cosmetic" everywhere to "streaks never
    gate content; a break costs at most 150 tickets of gift escalation and a free saver
    exists"; demote "Ad-free, guaranteed forever" to the trust footer; quote the 2025
    calibration next to the revenue number in the HAMM paragraph; optionally cut rewarded
    rewinds 5/day → 3/day.

---

## 9. What could not be verified — manual checklist for the developer

1. **The original master brief** — not in the repo; completeness vs 14/27/23 is unverifiable
   until you commit it (C9).
2. **Multi-award positively allowed** — no cap exists in the rules and Influencer's one-category
   clause implies entry breadth, but 2025 had zero double-winners; the planned email to
   shipaton@revenuecat.com remains the only definitive answer. Same email: whether a pre-order
   listing counts as "public release" (rules silent — plan's skip stance is correct).
3. **RevenueCat dashboard realities** — your project's actual plan tier (current pricing says
   Pro is free under $2.5k MTR with Experiments included, but legacy projects may differ); Ads
   beta approval latency (docs tie it to Charts v3 enablement); **whether RC-Ads-tracked AdMob
   revenue appears in any dashboard view a Grand-shortlist query would count** (the package
   itself warns not to assume it — verify at the D10 spike before submission copy claims it).
4. **OneSignal Growth plan** — whether "6 message steps" is enforced per-journey (pricing
   tooltip wording) or in total; whether custom events need a support-enablement step (5.2.0
   release notes suggest contacting support); actual Growth pricing at your MAU ("starts at"
   $19/mo).
5. **Play promo codes × purchases-unity** — that In-app Promotions redemption coexists with
   RevenueCat's purchase handling on Android (no doc covers the combination; test on a clean
   device at D24, per change #3).
6. **The pre-verified personal account's creation date** (pre-Nov-13-2023 ⇒ the 12/14 rule
   doesn't apply — potentially schedule-erasing; check in 2 minutes in Play Console).
7. **Devpost prizes page on/after Aug 1** — finalized category list (Next Gen appeared; more
   promised), the ">$700k" reconciliation, and the Oct 21-vs-22 winners date.
8. **unity.com support-table wording** (403s automated fetch; endoflife.date says Dec 2027) and
   one **smoke build on 6000.3.21f1+** only after a GMA release closes googleads-mobile-unity
   #4212 — that issue, not vendor statements, is the real unpin trigger.
9. **Trademark screens** — USPTO TESS / EUIPO for "Cat Metro" and "Meowtro" (store-level
   collision screens came back clean tonight, incl. the new adjacent title Cat Train Tycoon;
   registers were not searchable programmatically).
10. **Mini Metro's $0.99** — possibly a sale price (affects one comparison sentence only);
    Tenjin's image-locked 2026 eCPM charts if you want tighter ad-scenario bands; and the
    unsourced "casual Android CPI ~$0.95" figure in the brief (not found in the Adjust report
    cited — either source it or drop it).

---

*Prepared by the independent audit session, 31 Jul 2026. Fact statuses reflect sources as
fetched tonight; the rules state dates and terms may change at the Sponsor's sole discretion —
keep the plan's Monday re-verification cadence.*
