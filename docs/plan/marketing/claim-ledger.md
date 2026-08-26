# STORE-PACK claim ledger

This ledger prevents a plan, active branch, mockup, or hand-maintained count from becoming a
player-facing fact. Campaign counts are never frozen in this file: `scripts/build-aab.sh` derives the
normal-progression band from the exact AAB, verifies every named level is bundled, and renders the
count-bound sibling of `docs/store/play-store-listing.md`. A branch observation is not publication
copy. The production cut is blocked until TASK 15 lands on `main`; afterward, the 19-level target
becomes eligible only when the exact-AAB receipt actually reports 19. The generated sibling still
requires the release-gated monetization claims to be cleared before anyone pastes it.

The visual target remains pinned to `art/diorama-pass` commit `0ae6593` (PNG SHA-256
`5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a`).
`docs/plan/specs/submission_script.md` is a future-state planning document, not a claim source or
publication source. Its wording proves no feature, result, store state, or asset exists.

Statuses:

- `VERIFIED` — may be used in paste-ready copy now, within the wording shown.
- `BUILD-DERIVED` — the value may appear only as rendered from the exact AAB; never type or maintain
  it in this ledger or the listing template.
- `RELEASE-GATED` — the claim is structurally supported, but may appear only after its named checks
  pass against the exact release candidate.
- `BLOCKED` — must not be written as present. Promote only after the named evidence exists.
- `FUTURE COPY` — an internal revision candidate. It is never paste-ready until promoted.

Where one row names several claim limbs, evidence promotes only the specific limb it proves. The
row remains blocked for every other limb; one working surface never promotes a whole feature family.

## Public claim gates

| ID | Claim | Status | Evidence rule | Allowed wording |
|---|---|---|---|---|
| C-01 | Cat Metro is an Android train-routing puzzle controlled by tapping switches | VERIFIED | `unity/Assets/Scripts/Presentation/Input/TapInput.cs`; `GameRoot.cs`; first Pixel play in `state/PROJECT_STATE.md` §Now | “Tap switches to route cat trains.” |
| C-02 | The exact AAB contains one staged campaign JSON for every normal-progression ID | BUILD-DERIVED | The AAB listing receipt records `GameRoot.LevelBand` and fails if any named bundled JSON is absent | Use only the count rendered into the generated `*-play-listing.md`; never publish a count copied from a branch, plan, or this ledger. |
| C-03 | Every level named in the generated listing passes automated validation and solver gates | RELEASE-GATED | `ValidationStages.cs`; `LevelSolver.cs`; corpus wrappers; full corpus rerun against the exact release candidate | “Every current level is solver-validated” only after the exact-candidate gate passes. |
| C-04 | Current stations use color and symbol together | VERIFIED | `BoardView.cs` renders station color plus a symbol; tests pin the rule | “Match each color-and-symbol station.” Do not claim that current commuters carry symbols or that every line has a distinct silhouette. |
| C-05 | Next-wave information is visible during play | VERIFIED | `WavePreviewStrip.cs`; `FailureTests.WavePreview_NextTwoWaves…` | “Read the next waves.” |
| C-08 | Win, Results, Next, and band wrap are wired | VERIFIED | `GameRoot.LoadNext`; `LoadNextBandTests`; the human won L001 and L002 on Pixel | “Finish a level and move to the next.” |
| C-09 | The game has no forced-ad surface | RELEASE-GATED | Exact-production-binary placement census | “No forced ads.” Rewarded video may exist only when player-initiated; any interstitial invalidates this wording. |
| C-10 | The game has no energy-timer or loot-box system | RELEASE-GATED | Exact-production-binary feature and product census | “No energy timers. No loot boxes.” |
| C-11 | Exact-AAB normal progression is playable without payment | RELEASE-GATED | Exact-candidate purchase/product review proves no campaign pay gate | “Campaign play is free.” Campaign quantity comes only from the generated listing. |
| C-12 | The core level loop has run on a Pixel 9 Pro | VERIFIED | `state/PROJECT_STATE.md` §Now: human-completed L001 and L002, with L003 loaded | “Played on Pixel 9 Pro.” Never call this a public or release build. |
| C-13 | A premium tabletop-diorama direction and golden target exist | VERIFIED | `art/diorama-pass:…/gemini-tabletop-golden.png`; product spec §7 | “Built toward a tabletop model-railway look.” Do not say the current public build matches the golden. |
| C-14 | The public campaign quantity equals the exact AAB's normal-progression band | BUILD-DERIVED | `GameRoot.LevelBand`; `LoadNextBandTests.cs`; generated-listing receipt bound to the AAB SHA-256 | Use the generated numeric wording only. A development override, sibling branch, or target milestone never changes publication copy. |

The full positioning line is approved exactly as written:

> Fair by design: no forced ads, no energy timers, no loot boxes. Campaign play is free.

## Blocked claims and promotion gates

| ID | Claim | Status | Why blocked | Promotion evidence required |
|---|---|---|---|---|
| C-18 | Players can read and manage a queue in a normal campaign run | BLOCKED | Queue state exists in the simulation, but the release candidate has no accepted on-device queue-readability receipt and current primitives can overlap at a node | Exact-candidate on-device Playing capture showing a distinct occupied queue, its route decision, and clean HUD without development aids |
| C-19 | A normal campaign run reaches FailureReview, cause focus, and immediate retry | BLOCKED | Failure/retry PlayMode coverage uses a synthetic T904 overflow fixture; wrong-route authored runs hit pinned NEW-Q4/Halted, and L004/L005 have no proven overflow path | Reproducible authored-level path in the exact candidate, real `Game`-scene state evidence, and clean on-device failure → cause → retry capture |
| C-20 | The shipped game matches the diorama golden frame | BLOCKED | Earlier editor/Pixel frames are rejected baselines; corrected final Pixel evidence is open | One exact integrated candidate commit and AAB; two inspected on-device frames from the Play-installed build with device/date/hashes; corrected composition checks; human visual disposition bound to the same evidence |
| C-21 | Distinct cat silhouettes accompany every line | BLOCKED | Implemented on an art branch, not evidenced in the exact release candidate | Exact-candidate runtime line→silhouette assignment tests and asset inventory, plus inspected on-device frames covering every line identity |
| C-22 | Thirty levels are available through ordinary player flow | BLOCKED | No exact production AAB and generated-listing receipt proves this quantity | Exact production census and solver/validator pass, ordinary-player-reachable ID census covering all 30, and on-device Play-installed traversal evidence from the same package/build |
| C-22D | Six districts and district navigation are available | BLOCKED | District surfaces and navigation are not in the current player flow | Exact-candidate district inventory plus ordinary-flow on-device navigation across all six from the Play-installed build |
| C-23D | A playable Daily Line exists | BLOCKED | Only the pre-validation substrate exists; the player surface and playable path do not | Merged exact-candidate generation/validation, released entry surface, and clean on-device daily run through result |
| C-23P | Every player receives the same new daily board without a server | BLOCKED | A seed substrate does not prove cross-device parity, daily rollover/variation, or the shipped network architecture | Same-date/seed parity on at least two clean installs, consecutive-date rollover with date→seed/board variation, and exact-candidate algorithm/config and network evidence supporting the no-server limb |
| C-23S | A persisted daily streak exists | BLOCKED | No player-facing or persisted streak state is evidenced | Exact-candidate streak persistence/state tests plus multi-day on-device increment, lapse, and displayed-state receipts |
| C-24 | Level select or Back navigation exists | BLOCKED | Neither surface is implemented and reachable in the exact candidate | Promote each claimed limb only after its exact-candidate route tests and rendered, on-device ordinary-flow navigation receipt |
| C-25 | Shop, IAP, paywalls, placements, prices, premium themes, DLC, restores, or promo-code access exists | BLOCKED | Monetization code is absent and production monetization is not yet authorized | Human authorization and security-reviewed exact candidate; then promote only each specifically evidenced limb with its on-device flow, signed catalog/config, and backend/store receipt—purchase/restore alone proves no other limb |
| C-26 | Rewarded ads exist or are tracked through RevenueCat | BLOCKED | Taxonomy rows are dark declarations, not an ad implementation | Human-authorized merged ad code, on-device opt-in surface, reward ledger proof, and RevenueCat dashboard evidence |
| C-27 | “Ads only when you ask” describes live inventory | BLOCKED | It implies optional ad surfaces that do not exist yet and a negative claim about every other placement | C-26 plus an exact-production-binary census proving no interstitial, banner, app-open, or other forced surface and proving every live ad entry is player-initiated; reverify C-09 at that candidate |
| C-28 | Sakura/Neon themes or player theme switching exists | BLOCKED | Theme code/assets and player-facing selection are absent | Exact-candidate theme assets plus ordinary-player selection/equip flow, persisted selection evidence, and an on-device default/theme pair from the same game state |
| C-29 | Share cards, challenge links, Daily leaderboards, or District Cup exists | BLOCKED | These are plan/spec features only | Promote only the named limb after its exact-candidate implementation and on-device end-to-end receipt; a share/open proves neither a leaderboard nor District Cup, and vice versa |
| C-30 | OneSignal journeys or messages exist | BLOCKED | Taxonomy destinations do not equal deployed OneSignal integration | Merged adapter, exact live campaign/message configuration, device delivery receipt, and delivery counts with denominators for each claimed limb; retention outcomes remain C-32 |
| C-31 | Cat Metro is public or free on Google Play | BLOCKED | No public store URL or production release is evidenced | USA-visible public listing checked logged out, matching package/build installed from Play |
| C-32 | Any install, retention, conversion, revenue, rating, experiment, press, or community result | BLOCKED | No launch dataset exists | Dated source; raw numerator/denominator; defined cohort and date window; maturity and exclusions; benchmark population/vintage where compared. Promote only the measured result limb. |
| C-33 | A 56-day daily #BuildInPublic corpus exists | BLOCKED | A content plan is not publication history, and Aug 1–9 cannot be backdated | Exactly 56 dated public URLs, one for every required calendar day, with the unchanged four-metric gate in post 1 before data; a missed day keeps this exact claim blocked unless a human amends CM-R56 |
| C-34 | Optional reminders exist | BLOCKED | No deployed reminder path is evidenced in the exact candidate | Exact-candidate optional enable/disable surface, delivery/config evidence, and on-device opt-in/opt-out behavior; only then add “optional reminders” to the listing |

## Future-copy bank

The following phrases are intentionally quarantined. They may be promoted only by the matching
blocked-claim gate above:

| Candidate phrase | Status | Gate |
|---|---|---|
| “Thirty handcrafted levels across six districts.” | FUTURE COPY | C-22 and C-22D |
| “A new Daily Line every day—the same board worldwide.” | FUTURE COPY | C-23D and C-23P |
| “Ads only when you ask.” | FUTURE COPY | C-26 and C-27 |
| “One purchase, never a subscription.” | FUTURE COPY | C-25 |
| “Switch between premium themes.” | FUTURE COPY | C-28 |
| “Share your route and challenge a friend.” | FUTURE COPY | C-29 |
| “Free on Google Play.” | FUTURE COPY | C-31 |
| “Built in public for 56 days.” | FUTURE COPY | C-33 |

## Pack-wide usage rules

1. A `VERIFIED` claim may be made narrower, never broader.
2. A sibling branch proves work exists, not that the release contains it. Release copy needs evidence
   from the integrated build.
3. Screenshot and video directions may name a desired state, but their row must remain `BLOCKED` until
   a real capture passes its evidence gate.
4. Mockups and the Gemini golden frame are composition references only. They are never labeled as
   gameplay, on-device footage, or a shipped screenshot.
5. Every rate carries its numerator and denominator. Every benchmark carries source vintage. A result
   that does not exist is deleted, not replaced with a plausible number.
6. Before publishing any field, rerun this ledger against the exact production commit and replace its
   candidate hashes and status cells; take the campaign quantity only from the generated listing
   bound to the exact AAB SHA-256.
7. A grouped blocked row is never promoted wholesale. Record the exact claim limb, wording, candidate,
   and evidence being promoted; all unproved limbs stay blocked.
8. `docs/plan/specs/submission_script.md` is future-state/non-source material. It may suggest work to
   verify, but no sentence, number, screenshot direction, or “paste-ready” label in it promotes a
   claim or supplies publication copy.
