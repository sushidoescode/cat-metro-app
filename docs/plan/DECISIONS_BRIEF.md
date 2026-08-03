# DECISIONS BRIEF — single source of truth for all deliverable drafting
Locked 31 Jul 2026 after independent verification (10-agent research fleet; all findings in deliverables/data/research_results.json and deliverables/FINAL_REPORT.md).
Drafting agents: do not contradict anything here. Cite "verified 2026-07-31" facts as such.

## THE VERDICT
KEEP the Loopline core concept WITH MATERIAL REDESIGNS. The game ships as:

**CAT METRO** (working title; was "Loopline: Cat Metro")
One-thumb, portrait, deterministic route-switching puzzle: tap junction switches to
route color+symbol-coded cat commuter trains to matching stations before platforms
overflow. 45–90s levels. 30 curated launch levels in 6 districts + seeded Daily Line.
Unity, Android-first, Google Play. Public 1.0 target: Aug 24–28, 2026.

Rename rationale (verified): exact "Loopline" apps exist on Play + App Store; active
itch game "Loop Line"; loopline.com taken; crowded low-relevance ASO query. "Cat
Metro" is collision-free on Play/App Store/Steam; catmetro.com/.io/.app all
unregistered (registry RDAP 2026-07-31). Backup brandable name: "Meowtro" (also clean).

## VERIFIED EVENT FACTS (Official Rules fetched 2026-07-31 — no longer pending)
- Submission Period: Jul 31 2026 8:00am PDT – Sep 30 2026 11:45pm PDT. Judging Oct 1–13. Winners Oct 21 (FAQ says Oct 22 — rules prevail; reverify).
- First public store release must occur inside the window; pre-work allowed; updates to existing apps ineligible. App must be accessible from the USA.
- Must use RevenueCat SDK for ≥1 in-app/web purchase OR serve ads through "RevenueCat Ads".
- Submission: description, <2min YouTube/Vimeo video (no third-party trademarks/music), store URL, 1024² icon, 1179×2556 frameless screenshot, free trial OR promo code for judges. Judges may judge from text/images/video alone.
- Prizes: Grand $100k (shortlist = TOTAL REVENUE reported in RevenueCat during the window; highest revenue doesn't auto-win; criteria "Early and Effective Release" + "Growth by numbers"). #BuildInPublic $30k/$20k/$10k. OneSignal $25k/$15k/$5k ("a single deployed message is sufficient for eligibility"; criteria Implementation, User value, Resourcefulness). Best Game / HAMM / Catvertising / Design / Peace / Noise / Layers / Stripe Funnel Vision / JetBrains / Replit: $15k/$10k/$5k. Samsung Galaxy: NON-CASH (3 weeks featured placement; 20% of score = Galaxy optimization). Enumerated total $685k; ">$700k" is marketing (more categories may appear by Aug 1).
- Best Game criteria: "great gameplay, art direction, and a monetization fit that suits the genre" / "fun and engaging… unique gameplay experience, progression, or replayability… How is the game monetized?"
- Catvertising criteria: creative+effective ads, "clever placements, smart integration with the rest of your revenue stack… an experience users don't hate". Requires describing use of RevenueCat Ads.
- HAMM: "smartest use of RevenueCat to drive real revenue… well-crafted paywall, thoughtful pricing and packaging, strong conversion".
- Multi-category entry allowed (only one Influencer category); NO one-prize-per-project cap found.
- NO paid-UA restrictions (Grand Prize criteria credit marketing experiments); NO AI-disclosure requirement; IP must be original/owned; open source allowed if enhanced.
- 2025 calibration: Grand winner Payout 17k users/$30,017 revenue/1,750 payers (organic+ASO). Category awards won at 1k–13k users, $1k–2k revenue. 812 submissions.
- Skip: Replit award (requires building with Replit Agent), JetBrains (requires KMP on BOTH stores), Noise (requires Noise platform — evaluate later, cheap), influencer gaming award (requires "gaming bucket list" app — different product).

## VERIFIED PLATFORM FACTS
- Google Play: target API 36 required for new apps from Aug 31 2026 (ext to Nov 1). Billing Library 8+ required from Aug 31 2026. 16 KB pages mandatory (API 35+ since Nov 2025). Personal accounts: 12 testers opted in continuously 14 days → apply for production (review ≤7d typical) → first-release review up to 7d. Total time-to-store 3–5 weeks. START CLOSED TEST IMMEDIATELY — the 14-day clock starts when 12/12 are opted in (target Aug 1, latest Aug 2). SUBMIT-ON-GRANT: the first production release is submitted the day production access is granted (P50 ~Aug 20–21), using the current commercial-beta build, managed publishing ON, publish held; the polished 1.0 ships as an immediate update. Launch: Aug 24–28 best case; planning basis P50 Sep 1–2, P80 Sep 12–16; latest viable Sep 19.
- Play fees (US/UK/EEA since Jun 30 2026): 10% service fee (first $1M) + 5% billing fee via Play billing → 15% effective for us. Use 15% in models.
- Promo codes: Play one-time codes WORK for one-time in-app products → judge access solved.
- Families: do NOT declare under-13 age groups; keep store listing art from reading child-directed (Play may reject listings with child-appealing art if not Families-compliant). Target audience: 13+.
- In-app review API: quota-limited, no visible CTA button, never after failure.
- Unity: pin **6000.3.16f1** (Gradle 8.13/AGP 8.10.0). Do NOT jump to 6000.3.17f1+ (Gradle 9/AGP 9.0 breaks unverified SDKs) until a googleads-mobile-unity release closing **#4212** AND a green smoke build. Unity 6.3 LTS supported to Dec 2027. Android **min API 25** (Android 7.1+) — Unity 6.3's documented Android minimum. Accepted tradeoff: the 6000.3.16f1 pin forfeits the .19 libcurl CVE-2026-27135 mitigation (UnityWebRequest surface only; the SDKs use their own native networking) — accepted, low relevance. IL2CPP+ARM64 mandatory; URP; profile early on low-end (known Unity 6 URP Android frametime regression reports). Input System package. Unity Personal free <$200k. Unity Recorder 5.1.6 records 1080×1920 in Editor.
- SDK pins (latest as of 2026-07-31): purchases-unity **9.7.0** (Billing 8.3.0 — compliant), purchases-ui-unity (RevenueCatUI) paired, OneSignal Unity **5.3.2** (still Google EDM4U), Google Mobile Ads Unity **11.3.0**, EDM4U **1.2.188**. Keep exactly ONE EDM4U copy. Known pitfalls: duplicate BillingClient (exclude via Gradle if Unity IAP present — don't install Unity IAP), stale local AndroidX AARs, OneSignal needs custom Gradle templates + Force Resolve.
- RevenueCat Unity capabilities (all verified): Offerings/Packages/Entitlements ✓; Placements (Unity 6.9.0+) ✓; Paywalls v2 + Customer Center via RevenueCatUI (8.4.0+; device-only, 3 open Android paywall crash issues #745/#736/#732 — device-test heavily, custom paywall fallback ready); Experiments = server-side, **Pro/Enterprise plan gated** (verify project plan; fallback = sequential offering changes + Placements); Targeting Pro-gated; Test Store (8.3.0+) ✓; Virtual Currency (8.1.0+) ✓ — NOT used at launch (tickets stay client-side; revisit post-launch); consumables ✓ (fulfillment ledger ours; webhooks recommended); promoted purchases ✗ (iOS-only concept anyway); win-back offers ✗ Android (iOS 18 feature); AdTracker (9.1.0+) ✓ = TrackAdLoaded/Displayed/Opened/Revenue/FailedToLoad; server-verified ad rewards ✗ Unity (grant client-side with own ledger).
- RevenueCat Ads = Ad Monetization PUBLIC BETA: tracking layer over an existing ad SDK (does NOT serve ads). Request access via dashboard Ads page Day 1. AdMob convenience module NOT for Unity → manual AdTracker integration. Ad SDK: **Google AdMob Unity plugin 11.3.0** (fallback: AppLovin MAX 8.6.4).
- Funnels (Stripe Funnel Vision award): RevenueCat Funnels GA on Pro plan; needs connected Stripe; Unity Android consumes via Redemption Links. Play now permits external billing (US/UK/EEA). Judged on web payment volume. Decision: **P2 stretch**, go/no-go Day 35; do not let it touch launch scope.
- OneSignal Unity 5.3.2: push, IAM, tags, custom events (5.2.0+, "all PAID plans"), outcomes, channels, deep links, Login(external_id) ✓. **Journeys plan-gated: Free = 1 active journey/2 message steps; Growth $19/mo = 3 journeys/6 steps.** Frequency capping Enterprise-only → enforce caps in-app + journey design. No quiet hours → use Time Window steps. FCM v1 service-account JSON required. RC integration: $onesignalUserId attribute; RC writes purchase tags to OneSignal.
- DECISION: OneSignal **Growth plan ($19/mo)**. 3 active journeys: (1) Daily Line + streak (custom-event entry, Wait Until, Time Window), (2) Lapse ladder (48h→7d→14d branches in one journey), (3) Hard-level help. Everything else via IAM, tags, one-off scheduled sends, and Unity Mobile Notifications (local) for streak-expiry backup. This REPLACES the prior 7-journey design.

## VERIFIED MARKET FACTS
- Whitespace: NO cat-themed metro/route-switching puzzle on Play (4 searches, 2026-07-31). Trainyard delisted 2019; Mini Motorways not on Android; STATIONflow/Overcrowd PC-only.
- Poles: Mini Metro $0.99, 3.6M installs, 4.63★, no ads/IAP, endgame micromanagement complaints. Railbound $4.99 premium: 211K installs despite Apple Design Award → premium caps reach ~25–500× below F2P. Arrows–Puzzle Escape: 103.6M installs in 12 months, 4.83★, but "ad every other level" backlash. Bus Traffic Fever: 15.4M in 5 months, 3.72★ (forced 30s ads, recycled levels). Cat games: Neko Atsume 13.6M/4.78★ with IAP ≤$3.49 (gentle works); Cats&Soup 42.7M (cosmetics spine, whale packs); Cat Snack Bar 29.8M. Meowdoku spawned 7+ clones in ~3 months → expect fast-follow; brand matters.
- Strategy the data supports: FREE + gentle IAP + optional rewarded ads + explicit "no forced ads" positioning = attacks the loudest complaint in every F2P comp while avoiding premium's install ceiling.
- Benchmarks (use ranges, label vintage): US Android rewarded eCPM $15–30 (Tenjin Q2'24 $30.25; Appodeal Q4'24 $9-16). Interstitial US ~$10–16. Retention CURRENT medians (GameAnalytics 2025 data): D1 ~22%, D7 ~4%, D30 ~0.7%; top-10% ≈ 40/12/4 — these are ALL-GENRE medians, not puzzle figures; no doc may claim "puzzle retention is 22/4/0.7". The widely-quoted "puzzle 31.85/12.18/5.35" is 2022 data — OUTDATED. CPI: NA gaming $1.68 avg (Adjust 2026); casual Android ~$0.95 global. Casual D90 IAP ARPU $1.34/ARPPU $7.26 (AppsFlyer 2026). Play listing CVR ~16% US avg (AppTweak 2025 data; games below avg). Play fee 15% effective. No credible puzzle ARPDAU/opt-in%/refund benchmarks exist — say so.

## PRODUCT DECISIONS (LOCKED)
- Sim: pure C# fixed tick 8/s, seeded PCG32, command log, solver-validated JSON levels (schema v2 in deliverables/data/level_schema.json). No physics/NavMesh/free-drawing.
- Mechanics at launch (4): two-state switch, queue capacity, second source, wildcard commuter. Cooldown+gates enter post-launch bands (31–60). Express/reversible are expansion-only.
- Session: 45–90s. Instant retry <1s. Cause-first failure camera. Next-wave preview.
- Controls: tap only, ≥48dp targets, colorblind-safe (color+symbol+silhouette), planning-pause accessibility mode, haptics/motion toggles.
- Content: 30 launch levels (6 districts × 5) + seeded Daily Line (unlocks after L7) + weekly mini-event from Week 5 (District Cup, async score, participation cosmetic).
- Art: premium tabletop diorama, modular low-poly (Meshy/Tripo + Blender cleanup), 1 lighting rig, 1 toon shader family, cream/navy/teal/orange palette; readability outranks beauty; NOT childish (Families risk).
- Offline-first: full campaign offline; queue analytics/events; entitlement cache.

## MONETIZATION (Model B — Balanced Hybrid, LOCKED)
Positioning: "Fair by design: no forced ads, no energy, no loot boxes, every level solvable free."
Catalog (Google Play product IDs; entitlements in parens):
1. cm_all_access $6.99 non-consumable (ent: all_access) — bonus district (10 levels), both premium themes, daily free rewind, removes ALL non-rewarded ad surfaces permanently, gold conductor badge. Was $4.99 in prior plan; raised because (a) downside is bounded (~$40 net base-case) with a 28.6% conversion-loss cushion, (b) $4.99 breaks the ladder — the everything-tier would price below its own two themes ($5.98) and tie cm_rewind_20, the decoy-confusion grounds on which the theme bundle was cut, and (c) the verified $7.26 casual D90 ARPPU shape supports a ~$7 completion price. The Grand-shortlist revenue argument is immaterial at our scale. A/B $4.99 vs $6.99 via Experiments if plan allows, else sequential (directional, pre-registered as non-significant at our scale).
2. cm_supporter_pack $9.99 non-consumable (ent: supporter) — All Access contents + exclusive Founder livery + name-a-cat cameo (local) + supporter badge. Shown ONLY in shop, never interrupts.
3. cm_theme_sakura $2.99 non-consumable (ent: theme_sakura). 4. cm_theme_neon $2.99 non-consumable (ent: theme_neon). (Bundle cut — decoy confusion; All Access IS the bundle.)
5. cm_rewind_5 $1.99 consumable — 5 rewinds. 6. cm_rewind_20 $4.99 consumable — 20 rewinds. Rewind = rewind to last safe decision tick; never required; 1 free/day for all; extra via rewarded ad.
7. CUT: currency packs, streak savers as IAP (streak saver = rewarded/free only), chapter packs (folded into All Access), audio packs, monthly club, season pass, ANY subscription.
Subscription decision: REJECTED at launch and for the event window (no recurring content cadence a solo dev can honestly sustain in 8 weeks; one-time premium converts better for this genre; revisit only post-event if weekly event cadence proves durable).
Paywall exposure: first exposure after L5 win (celebratory, dismissible); theme preview from map; bonus-district lock; shop always; eligible-failure rewind sheet (owned/free/rewarded first, purchase secondary, never after first failure). RC Placements: post_level_5, theme_preview, bonus_district, shop, rewind_failure. RC Paywalls v2 for post_level_5 (device-tested; custom fallback), custom UI elsewhere.
Rewarded ads (AdMob, RC AdTracker on every event): rewind_failure (2/session, 5/day), double_tickets (3/day), daily_gift_double (1/day), streak_saver (1/day), theme_rental (3 levels, 1/theme/day). NO interstitials, NO banners, NO app-open ads at launch — this IS the Catvertising story ("ads only when the player asks").
Economy: tickets (soft, client-side): earn 20–50/level, 100 first daily, 30–80 daily gift; sinks: earnable cosmetic variants 600–1200, nothing gameplay-gated. No premium currency.

## AWARD TARGETING (priority order)
P0: Best Game, HAMM, #BuildInPublic ($30k!), OneSignal, Catvertising. P1: Design, Grand Prize (revenue+early launch), Most Viral (evaluate Noise platform cost ~Day 21). P2: Stripe Funnel Vision (Day 35 gate), Samsung Galaxy (non-cash — only if trivial). SKIP: Replit, JetBrains, influencer categories, Peace Prize.

## SCHEDULE SPINE (2026)
D1 Aug 1: accounts (Play dev, RC, OneSignal, AdMob, Firebase), repo, Unity 6000.3.16f1 project, closed-test track + 12 testers recruited, RC Ads beta access requested; the 14-day clock starts when 12/12 are opted in (target Aug 1, latest Aug 2). D7 Aug 7: fun gate — four pre-registered metrics + outside confirmer; fail rule: YELLOW (2 of 4 metrics missed) = 48h mechanic surgery + re-gate D9; RED (3+ of 4, or metric (i) alone) = execute the Plan-B runbook (PLAN_B_RUNBOOK.md). D14 Aug 14: 20 validated levels, solver, commercial SDK spikes pass on device. D14–15: clock completes → apply for production access the same day. SUBMIT-ON-GRANT: first production release submitted the day access is granted (P50 ~Aug 20–21; commercial-beta build, managed publishing ON, publish held; polished 1.0 ships as an immediate update). D21 Aug 21: commercial beta full smoke. PUBLIC 1.0: Aug 24–28 best case; planning basis P50 Sep 1–2, P80 Sep 12–16; if rejected (~Aug 20–22), keep testers opted in until grant and refile when the trailing-14-day criterion re-satisfies (P50 re-grant ~Sep 8, launch ~Sep 13–15). Sep: iterate/growth/experiments; Sep 26–30: submission freeze + Devpost. Hard cut gates at D7/D14/D21/D24/D24-28/D35(+35b)/D42/D54 per roadmap CSV.

## BUDGET SCENARIOS
$0 organic-only (default plan) / $500 (micro-tests $20–50/day only after an organic creative wins) / $2,000 (scale winning creative + $300 ASO experiments). Never buy installs before D1-retention floor confirmed; label paid cohorts.
