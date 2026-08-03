# Prior-Research Claim Inventory (audit targets)

Extracted 31 Jul 2026 from the four supplied PDFs + technical appendix.
Doc keys: BP = Winning_Game_Blueprint (82p), GS = Shipaton 2026 Game Strategy (64p, ChronoRoute),
HS = RC Shipaton 2026 Hackathon & Mobile Game Strategy (10p), PX = "I'm going to be participating..." (Perplexity export),
TA = Loopline_Technical_Appendix.

## A. Event / rules claims
| # | Claim | Where |
|---|---|---|
| A1 | Event window Aug 1 – Sep 30 2026; judging Oct 1–13; winners Oct 21 | BP p5, HS p1, GS p1 |
| A2 | First public release must occur in window; updates to existing apps ineligible; pre-work allowed | HS, GS, PX, BP |
| A3 | RevenueCat SDK must power ≥1 purchase path OR RevenueCat Ads | HS, BP |
| A4 | Grand Prize $100,000; emphasizes traction + growth momentum | BP p5, HS |
| A5 | OneSignal award $25k/15k/5k; other categories 15k/10k/5k; >$700k total cash | HS |
| A6 | Categories: Best Game, HAMM, Catvertising, Design, OneSignal, Most Viral (Noise), Growth Loop (Layers), Funnel Vision (Stripe), Galaxy (Samsung), Next Gen, Peace Prize, influencer awards | HS |
| A7 | Best Game judged on gameplay, art direction, monetization-genre fit | HS, PX |
| A8 | Submission: description, ≤2min video (YouTube/Vimeo), store URL, 1024² icon, 1179×2556 frame-free screenshot, judge access (trial/promo) | HS, GS, PX |
| A9 | Final Official Rules still pending as of 30 Jul 2026 | BP p2 |
| A10 | Pre-order listing acceptable if public availability starts in window (Devpost manager response) | BP [S04] |
| A11 | Paid acquisition not banned; marketing legitimately influences Grand Prize | BP p5 |
| A12 | 2025: 812 submissions; Payout won: 17k users, $30,017 revenue, 1,750 paying subscribers, 500k+ impressions | PX, HS |
| A13 | Next Gen student track relaxes store-release requirement | HS |

## B. Google Play / Android claims
| # | Claim | Where |
|---|---|---|
| B1 | Target API 36 (Android 16) required for new apps + updates from 31 Aug 2026 | BP [S07] |
| B2 | New personal accounts: 12 testers continuously opted-in for 14 days before production access | BP [S08] |
| B3 | Play Billing required for all digital goods | BP [S09] |
| B4 | Ads policy: no unexpected interstitials at level start/during play; rewarded = safest | BP [S10] |
| B5 | POST_NOTIFICATIONS runtime permission; Android 13+ default off | BP [S12] |
| B6 | 16 KB page-size compatibility audit needed for all native SDKs | BP [S13] |
| B7 | No paid random rewards (loot boxes) without odds disclosure | BP |
| B8 | Min API 25 / target API 36 baseline is right for Unity 6 (min corrected 24→25 per Unity 6.3 docs, AMD-08) | BP, TA |

## C. RevenueCat claims
| # | Claim | Where |
|---|---|---|
| C1 | Unity SDK supports purchases, entitlements, offerings, analytics, webhooks | BP [S15][S16] |
| C2 | RevenueCatUI paywalls available in Unity; Android min-API floor met by our API-25 baseline | BP [S17] |
| C3 | Experiments can test offerings/paywalls "when plan and SDK support it" | BP [S18] |
| C4 | Ad Monetization is opt-in BETA reporting layer over existing ad SDK (AdMob/MAX); does NOT serve ads; Unity has AdTracker | BP [S19][S21], TA README |
| C5 | Test Store exists for pre-store-setup sandbox | BP [S20] |
| C6 | Customer Center exists in Unity repo | BP [S21] |
| C7 | Consumables have no entitlement; need own transaction ledger | BP p17 |
| C8 | Catalog: $4.99 All Access non-consumable, $1.99 themes, $2.99 bundle, $0.99/5 + $2.99/20 rewinds | BP p17 |

## D. OneSignal claims
| # | Claim | Where |
|---|---|---|
| D1 | Unity SDK current = Core 5.3.2; requires Unity 2022.3+, Android 7+ | BP [S22][S27] |
| D2 | Supports push, IAM, tags, identity, in-app triggers, outcomes | BP [S22-27] |
| D3 | Journeys support entry by event/tag, Wait Until, branches, exits, frequency caps | BP [S24-26] |
| D4 | Custom events usable for journey entry/exit + personalization | BP [S24] |

## E. Unity / tech claims
| # | Claim | Where |
|---|---|---|
| E1 | Unity 6.3 LTS current, supported through Dec 2027 | BP [S14] |
| E2 | Deterministic fixed-tick pure-C# sim; no physics/NavMesh — feasible & right | BP, TA |
| E3 | 60fps mid-tier Android with URP/IL2CPP/ARM64 achievable | TA |
| E4 | Solver (BFS/beam) can validate level fairness at authoring time | BP, TA |

## F. Market / benchmark claims
| # | Claim | Where |
|---|---|---|
| F1 | May/Jun 2026: arrow-routing puzzles + Block Blast prominent in downloads; Royal Match/MONOPOLY GO revenue via live-ops | BP [S28][S29] |
| F2 | Puzzle D1 ~20%, D7 ~7%, D30 ~2%; match-3 24/11/5 | BP [S30] |
| F3 | GameDropDaily top-10 charts (Jul 22 2026): Smash Fest!, Meowdoku, Arrows, Block Blast!, Vita Mahjong, Bus Traffic Fever etc. | PX, HS |
| F4 | Casual games commonly hybrid IAP+IAA | BP [S31][S32] |
| F5 | ACECRAFT $330k/day IAP at launch (gacha) | GS |

## G. Strategy claims (prior verdicts)
| # | Claim | Where |
|---|---|---|
| G1 | ChronoRoute 9.35/10 → best concept (GS matrix) | GS |
| G2 | BP overrode GS: Loopline: Cat Metro 9.2 "Proceed"; CineCraft Plan B | BP p4 |
| G3 | Launch Week 4 (Aug 21–28), not Week 8 | BP p3 |
| G4 | 30 curated levels at launch, not 100 | BP p3 |
| G5 | No MVP subscription | BP p3 |
| G6 | Day-7 fun gate: 5 testers voluntarily replay 3×, else pivot to CineCraft | BP p4 |
| G7 | Defer interstitials at launch entirely | BP p18 |
| G8 | Contradiction: GS proposed interstitials 1/3 levels + $1.99/mo Unlimited Fuel sub (GridLock) + battle pass (Aether) — BP rejects all | GS vs BP |
| G9 | Contradiction: GS Week-7/8 store launch vs BP Week-4 launch | GS vs BP |
| G10 | Contradiction: HS says "energy systems common → suitable"; BP bans energy in MVP | HS vs BP |
