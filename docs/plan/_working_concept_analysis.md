# Independent Concept Analysis (pre-verification draft)

Drafted 31 Jul 2026 BEFORE web-verification results returned, to keep the concept
audit unanchored from the prior reports' conclusions. Final scores are locked only
after market research lands.

## Scoring framework

24 criteria from the master brief, grouped and weighted. Weights sum to 100.
Rationale: the hackathon is won by (1) shipping a polished public game early,
(2) real traction, (3) award-criteria fit. Feasibility therefore outweighs ceiling.

| Group | Criteria | Weight |
|---|---|---|
| Ship probability (30) | Solo feasibility 8, Unity tech risk 6, Content burden 6, Launch by D28 6, Polish by D56 4 |
| Fun & retention (25) | Immediate comprehension 5, Fun in 30s 5, Depth after 30 sessions 5, OneSignal/live-ops fit 5, Daily replayability 5 |
| Commercial (20) | RevenueCat monetization fit 7, Rewarded-ad fit 5, Ethical fit 4, HAMM narrative 4 |
| Growth & judges (25) | Short-form video potential 6, Organic acquisition 5, ASO discoverability 4, Visual distinction 4, Best Game/Design appeal 3, Catvertising appeal 3 |

## Candidates

### C1. Loopline: Cat Metro (prior recommendation)
Deterministic fixed-tick graph sim; tap two-state junction switches; route
color+symbol cat trains to matching stations before platform queues overflow.
30 curated levels + seeded daily.
- Strengths: deterministic => AI-generatable + solver-verifiable content; watchable
  "flow" moments for shorts; distinctive theme; clean premium+cosmetic+consumable
  catalog; daily seed = honest retention hooks; low Unity risk (no physics).
- Red-team concerns (to resolve): Is "tap the switch" readable in 3s of video?
  (One step above tap-to-release arrow games, below draw-track Railbound. Likely OK
  with color-coded route highlighting.) Is watching the sim satisfying on repeat?
  (Depends entirely on juice tier; risk is medium, mitigated by Day-7 fun gate.)
  Does failure feel fair? (Yes if cause-camera works.) Post-campaign repeat play?
  (Daily seed + score chase; adequate for 8-week window, thin for year-2 — fine.)

### C2. ChronoRoute: Tokyo Express (earlier variant)
Same routing family; drag-and-tap junctions, Tokyo theme, energy+interstitials in
original spec. Strictly dominated by C1: same core with (a) less distinctive theme
(licensed-city risk, weaker character attachment), (b) drag input (worse one-thumb),
(c) worse ad ethics baseline (interstitial cap 1/3 levels), (d) under-specified sim.
Include in matrix for completeness; expect elimination.

### C3. CineCraft: Director's Cut (prior Plan B)
Arrange shot cards into sequences; rating meter; renders animated scene.
- Strengths: novelty, Design-award appeal, share-the-clip loop.
- Weaknesses: "is my edit good" scoring is subjective => fairness problem;
  2D asset volume enormous (every scene needs art); non-deterministic fun; hard to
  explain in 6s; weak rewarded-ad surface; weak daily loop. High content burden.

### C4 (independent). "Meowmelon" — cat merge-drop physics puzzle
Suika-style: drop cats, identical cats merge into bigger cats, don't overflow the
box. Cat theme native; one-thumb; 2D circle physics (trivial in Unity).
- Strengths: proven ultra-viral loop; instant comprehension; juicy; endless mode =
  infinite content (zero level burden); themes/skins monetize; daily seed possible.
- Weaknesses: extremely crowded post-Suika clone space; near-zero mechanical
  differentiation => weak Best Game/Design narrative; physics nondeterminism breaks
  the AI-validator story; judge appeal "another watermelon clone" risk is high;
  ASO dominated by incumbents.

### C5 (independent). "Whisker Watch" — daily deduction logic puzzle
One handcrafted daily 5-minute logic grid (Cluedo-style: which cat stole the fish),
Wordle share-square output, archive behind premium.
- Strengths: minimal art; daily habit native; share loop native; premium archive =
  clean IAP; tiny content pipeline via constraint solver (deterministic, AI-friendly).
- Weaknesses: single-puzzle/day caps session count and traction ceiling in an
  8-week window; monetization surface small (weak HAMM/Catvertising); crowded
  (NYT-style dailies); low spectacle for shorts; Best Game appeal low.

### C6 (independent). "Cat Snack Line" — idle queue-management café
Cats queue at a station café; player taps to serve/upgrade; offline earnings.
- Strengths: proven monetization depth (Cat Snack Bar comps); cat attachment high;
  rewarded ads native (2x earnings); OneSignal native (offline earnings full).
- Weaknesses: content/economy tuning burden heavy; differentiation near zero;
  idle-genre judges may read it as cynical; ethical-monetization story weaker
  (timers are the genre); art volume high. Solo 8-week polish risk high.

## Provisional expectation (to be confirmed/falsified by research)
C1 leads on risk-adjusted EV; C4 is the strongest challenger on virality but fails
differentiation/judge narrative; C6 strongest on raw monetization but fails
feasibility+ethics narrative; C5 is the low-risk floor but caps traction; C2/C3
eliminated. Verdict candidate: KEEP C1 WITH MATERIAL REDESIGNS (naming/ASO-first
title, first-30-seconds readability work, juice-gated go/no-go, monetization
catalog trimmed + one addition TBD after RevenueCat feature verification).

Open questions for research: arrow-routing market saturation; cat-metro name
collisions; whether RC Experiments/Paywalls work in Unity (affects catalog and
paywall plan); Play closed-testing gate (affects launch timeline feasibility);
current eCPM/retention benchmarks (affects revenue scenarios).
