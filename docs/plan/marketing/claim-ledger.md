# STORE-PACK claim ledger

This ledger prevents a plan, active branch, or mockup from becoming a player-facing fact. It is
frozen against `origin/main` `9be8f95` on 2026-08-10, with the visual target pinned to
`art/diorama-pass` commit `0ae6593` (PNG SHA-256
`5626f64998d72044f090dc2905fb35eb9dec4b9504cb7c0b316af1090c34497a`).

Statuses:

- `VERIFIED` — may be used in paste-ready copy now, within the wording shown.
- `BLOCKED` — must not be written as present. Promote only after the named evidence exists.
- `FUTURE COPY` — an internal revision candidate. It is never paste-ready until promoted.

Where one row names several claim limbs, evidence promotes only the specific limb it proves. The
row remains blocked for every other limb; one working surface never promotes a whole feature family.

## Verified public claims

| ID | Claim | Status | Evidence at freeze | Allowed wording |
|---|---|---|---|---|
| C-01 | Cat Metro is an Android train-routing puzzle controlled by tapping switches | VERIFIED | `unity/Assets/Scripts/Presentation/Input/TapInput.cs`; `GameRoot.cs`; first Pixel play in `state/PROJECT_STATE.md` §Now | “Tap switches to route cat trains.” |
| C-02 | Ten authored and staged level files exist, L001–L010 | VERIFIED | Ten JSON files under `content/levels/` and the staged tree; CM-C11 main history | “Ten authored and validated level files.” Do not call all ten player-reachable. |
| C-03 | Current levels pass automated validation and solver gates | VERIFIED | `ValidationStages.cs`; `LevelSolver.cs`; corpus wrappers; main’s CM-C11 certification | “Every current level is solver-validated.” |
| C-04 | Current stations use color and symbol together | VERIFIED | `BoardView.cs` renders station color plus a symbol; tests pin the rule | “Match each color-and-symbol station.” Do not claim that current commuters carry symbols or five distinct silhouettes. |
| C-05 | Next-wave information is visible during play | VERIFIED | `WavePreviewStrip.cs`; `FailureTests.WavePreview_NextTwoWaves…` | “Read the next waves.” |
| C-08 | Win, Results, Next, and band wrap are wired | VERIFIED | `GameRoot.LoadNext`; `LoadNextBandTests`; the human won L001 and L002 on Pixel | “Finish a level and move to the next.” |
| C-09 | The game currently has no forced-ad surface | VERIFIED | No runtime monetization/billing/IAP/ads directory or package on the frozen anchor; mode remains sprint | “No forced ads.” Do not describe optional rewarded surfaces as live. |
| C-10 | The game has no energy or loot-box system | VERIFIED | Locked cut list in `product_spec.md` §28 and absence from runtime | “No energy. No loot boxes.” |
| C-11 | Every normal-progression level is playable without payment; all ten authored levels are solver-validated | VERIFIED | `GameRoot.LevelBand` exposes L001–L005; solver validation covers L001–L010; no purchase gate or monetization runtime exists | “Every level solvable free.” Keep the exact positioning line intact and keep the listing count at five until player reachability changes. |
| C-12 | The core level loop has run on a Pixel 9 Pro | VERIFIED | `state/PROJECT_STATE.md` §Now: human-completed L001 and L002, with L003 loaded | “Played on Pixel 9 Pro.” Never call this a public or release build. |
| C-13 | A premium tabletop-diorama direction and golden target exist | VERIFIED | `art/diorama-pass:…/gemini-tabletop-golden.png`; product spec §7 | “Built toward a tabletop model-railway look.” Do not say the current public build matches the golden. |
| C-14 | Normal player progression currently exposes L001–L005 and wraps to L001 | VERIFIED | `GameRoot.LevelBand`; `LoadNextBandTests.cs` | “Five handcrafted campaign levels.” Never count a development override as player reachability. |

The full positioning line is approved exactly as written:

> Fair by design: no forced ads, no energy, no loot boxes, every level solvable free.

## Blocked claims and promotion gates

| ID | Claim | Status | Why blocked | Promotion evidence required |
|---|---|---|---|---|
| C-18 | Players can read and manage a queue in a normal campaign run | BLOCKED | Queue state exists in the simulation, but frozen presentation has no accepted on-device queue-readability receipt and current primitives can overlap at a node | Exact-candidate on-device Playing capture showing a distinct occupied queue, its route decision, and clean HUD without development aids |
| C-19 | A normal campaign run reaches FailureReview, cause focus, and immediate retry | BLOCKED | Failure/retry PlayMode coverage uses a synthetic T904 overflow fixture; wrong-route authored runs hit pinned NEW-Q4/Halted, and L004/L005 have no proven overflow path | Reproducible authored-level path in the exact candidate, real `Game`-scene state evidence, and clean on-device failure → cause → retry capture |
| C-20 | The shipped game matches the diorama golden frame | BLOCKED | Lane 1A’s evidence calls earlier editor/Pixel frames rejected baselines; corrected final Pixel evidence is open | One exact integrated candidate commit and APK; two inspected on-device frames from that APK with device/date/hashes; corrected composition checks; human TG disposition bound to the same evidence |
| C-21 | Distinct cat silhouettes accompany every line | BLOCKED | Implemented on the art branch, not evidenced in the frozen main/release build | Exact-candidate runtime line→silhouette assignment tests and asset inventory; on-device frames covering every line identity; and signed CM-R21.3 marker plus CM-R21.6 silhouette runs, each using 5 non-author raters/25 trials and scoring ≥23/25, with dated remedy records where required |
| C-22 | Thirty levels are available through ordinary player flow | BLOCKED | Main has ten authored files but exposes only five through normal progression | Exact production census and solver/validator pass, ordinary-player-reachable ID census covering all 30, and on-device Play-installed traversal evidence from the same package/build |
| C-22D | Six districts and district navigation are available | BLOCKED | District surfaces and navigation are not in the frozen player flow | Exact-candidate district inventory plus ordinary-flow on-device navigation across all six from the Play-installed build |
| C-23D | A playable Daily Line exists | BLOCKED | Only the pre-validation substrate exists; the player surface and playable path do not | Merged exact-candidate generation/validation, released entry surface, and clean on-device daily run through result |
| C-23P | Every player receives the same new daily board without a server | BLOCKED | A seed substrate does not prove cross-device parity, daily rollover/variation, or the shipped network architecture | Same-date/seed parity on at least two clean installs, consecutive-date rollover with date→seed/board variation, and exact-candidate algorithm/config and network evidence supporting the no-server limb |
| C-23S | A persisted daily streak exists | BLOCKED | No player-facing or persisted streak state is evidenced | Exact-candidate streak persistence/state tests plus multi-day on-device increment, lapse, and displayed-state receipts |
| C-24 | Level select or Back navigation exists | BLOCKED | Lane 8 is queued and cannot start until UI Lane 1B lands | Promote each claimed limb only after its exact-candidate route tests and rendered, on-device ordinary-flow navigation receipt |
| C-25 | Shop, IAP, paywalls, placements, prices, premium themes, DLC, restores, or promo-code access exists | BLOCKED | Monetization code is absent and production mode has not been human-flipped | Human mode flip and security-reviewed exact candidate; then promote only each specifically evidenced limb with its on-device flow, signed catalog/config, and backend/store receipt—purchase/restore alone proves no other limb |
| C-26 | Rewarded ads exist or are tracked through RevenueCat | BLOCKED | Taxonomy rows are dark declarations, not an ad implementation | Merged ad code after production flip, on-device opt-in surface, reward ledger proof, and RevenueCat dashboard evidence |
| C-27 | “Ads only when you ask” describes live inventory | BLOCKED | It implies optional ad surfaces that do not exist yet and a negative claim about every other placement | C-26 plus an exact-production-binary census proving no interstitial, banner, app-open, or other forced surface and proving every live ad entry is player-initiated; reverify C-09 at that candidate |
| C-28 | Sakura/Neon themes or player theme switching exists | BLOCKED | Theme code/assets and player-facing selection are absent | Exact-candidate theme assets plus ordinary-player selection/equip flow, persisted selection evidence, and an on-device default/theme pair from the same game state |
| C-29 | Share cards, challenge links, Daily leaderboards, or District Cup exists | BLOCKED | These are plan/spec features only | Promote only the named limb after its exact-candidate implementation and on-device end-to-end receipt; a share/open proves neither a leaderboard nor District Cup, and vice versa |
| C-30 | OneSignal journeys or messages exist | BLOCKED | Taxonomy destinations do not equal deployed OneSignal integration | Merged adapter, exact live campaign/message configuration, device delivery receipt, and delivery counts with denominators for each claimed limb; retention outcomes remain C-32 |
| C-31 | Cat Metro is public or free on Google Play | BLOCKED | No public store URL or production release is evidenced | USA-visible public listing checked logged out, matching package/build installed from Play |
| C-32 | Any install, retention, conversion, revenue, rating, experiment, press, or community result | BLOCKED | No launch dataset exists | Dated source; raw numerator/denominator; defined cohort and date window; maturity and exclusions; benchmark population/vintage where compared. Promote only the measured result limb. |
| C-33 | A 56-day daily #BuildInPublic corpus exists | BLOCKED | A content plan is not publication history, and Aug 1–9 cannot be backdated | Exactly 56 dated public URLs, one for every required calendar day, with the unchanged four-metric gate in post 1 before data; a missed day keeps this exact claim blocked unless a human amends CM-R56 |
| C-34 | Optional reminders exist | BLOCKED | No deployed reminder path is evidenced on the frozen anchor | Exact-candidate optional enable/disable surface, delivery/config evidence, and on-device opt-in/opt-out behavior; only then add “optional reminders” to the listing |

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
   frozen hashes and status cells.
7. A grouped blocked row is never promoted wholesale. Record the exact claim limb, wording, candidate,
   and evidence being promoted; all unproved limbs stay blocked.
