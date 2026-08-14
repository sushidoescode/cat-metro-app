# CM-MONETIZATION-CODE implementation plan — dependency-free foundation

**Branch:** `feat/revenuecat-integration`
**Frozen contract commit:** `79cab81c3189792f6f60c7d600663421b0918874`
**Plan base:** `d10509d4294a5e657dbedb3d8d42f0ce16b9fbdf`
**Status:** executable only for Tasks 1–3 below. Every later RevenueCat/package/Save/UI/wiring
step remains stopped by the named contract gates.

## Objective

Land the largest truthful Lane 5 slice that does not require an unsigned dependency/privacy ADR,
an ADR-0006 Save amendment, a RevenueCat call, a commerce renderer, or Bootstrap wiring:

1. passive SDK-free purchase/catalog DTO vocabulary;
2. exact six-product, four-entitlement, five-commerce-placement configuration data with no price;
3. the exact eight-row rewarded model, with every SDK path hard-disabled and the amendment's last
   three rows additionally ADR-disabled; and
4. one pure `CatMetro.Application.Purchases.OfferEligibilityService` implementing the signed
   post-Level-5, phase, failure-cooldown, rewind, and pre-Level-7 fairness decisions; and
5. dormant pure rewarded-outcome policy for the signed cap, decline, no-fill, atomic-commit,
   trial-expiry, cosmetic-integrity, and guest-route laws, with no reachable SDK or grant path.

This plan does not claim the RevenueCat integration is complete. It creates no live purchase,
placement, rewarded, restore, entitlement, persistence, UI, or analytics path.

## Global constraints

- TDD history is mandatory. Each task has two commit boundaries: first commit all covering tests
  while the named targeted run is demonstrably red, then add the minimum production/data change
  in a later commit and rerun to green. Every behavior criterion must exist in the antecedent
  red-only commit. If a normal repository hook refuses the expected-red commit, stop; never bypass
  the hook or collapse red and green into one commit. Never weaken an existing test.
- Work only in the frozen contract's owned paths for this slice:
  `unity/Assets/Scripts/Services/Purchases/**`,
  `unity/Assets/Scripts/Application/Purchases/**`,
  `unity/Assets/Resources/Monetization/**`,
  `unity/Assets/Tests/EditMode/Pure/Purchases/**`,
  `tests/monetization/**`, and the exact new folder `.meta` files named by the contract.
- Services contains passive interfaces/POCO DTOs only and references neither UnityEngine nor any
  SDK. Application contains the only eligibility behavior. No Save file is edited.
- Do not create `IPurchases` yet. The signed implementation sketch and architecture overview
  disagree on initialization, cancellation, placement types, purchase inputs/results, cache/events,
  refresh, paywall, Customer Center, and AdTracker. The frozen contract fixes only part of the
  eventual seam. Choosing the rest would invent public API.
- Do not create an ad interface, `IAdTracker`, analytics seam, SDK wrapper, future adapter, or any
  type containing RevenueCat, RevenueCatUI, BillingClient, GoogleMobileAds, AdTracker,
  TrackAdRevenue, or AdRevenueData.
- Do not create `ofr_*` data outside the future canonical RevenueCat integration assembly. The
  foundation config names game placement IDs only; Offering/package confinement waits for the
  signed dependency work.
- No product price, currency amount, currency code fallback, formatted-price fallback, experiment
  SKU, seventh live SKU, sixth commerce placement, or active rewarded flag is permitted.
- No PlayerPrefs, persistence, clock/local-midnight computation, session identity, counter
  mutation, reward grant, or once-ever marker write. Pure requests receive already-derived facts;
  pure decisions may model required decline/mute, no-fill, cap, atomic-commit, trial, and guest-route
  dispositions, but cannot execute or persist them. Durable ownership remains stopped on ADR-0006.
- No placement lookup, Configure, purchase, restore, CustomerInfo, PaywallsPresenter, custom
  renderer, GameRoot, asmdef, package, pin, Plugin/Android, ProjectSettings, or workflow edit.
- Unity `.meta` files use unique lowercase 32-hex GUIDs. Do not reuse or mechanically copy a GUID.
- The all-disabled rewarded data uses the frozen five-per-local-date rewind cap. It models no D-4
  alternative and exposes no runtime toggle.
- Full branch completion remains gated by production mode, signed ADRs/human rulings, real Unity
  CI, independent code plus billing/security review, NEW-Q45/46/48, device/dashboard evidence, and
  every other stop in the frozen contract.

## Task 1 — passive purchase vocabulary and exact disabled catalogs

### Files

The antecedent red-only commit creates only the owned test artifacts:

- `unity/Assets/Tests/EditMode/Pure/Purchases.meta`
- `unity/Assets/Tests/EditMode/Pure/Purchases/PurchaseFoundationTests.cs` + `.meta`
- `tests/monetization/purchases-foundation.test.sh`

The later green implementation commit creates:

- `unity/Assets/Scripts/Services/Purchases.meta`
- `unity/Assets/Scripts/Services/Purchases/PurchaseIdentifiers.cs` + `.meta`
- `unity/Assets/Scripts/Services/Purchases/PurchaseCatalogDtos.cs` + `.meta`
- `unity/Assets/Scripts/Services/Purchases/PurchaseOutcomeDtos.cs` + `.meta`
- `unity/Assets/Scripts/Services/Purchases/RewardedPlacementDtos.cs` + `.meta`
- `unity/Assets/Resources/Monetization.meta`
- `unity/Assets/Resources/Monetization/product_catalog.json` + `.meta`
- `unity/Assets/Resources/Monetization/rewarded_placements.json` + `.meta`

### Red tests

`PurchaseFoundationTests` and the executable shell gate must initially fail because the files/types
do not exist. They then prove:

1. `product_catalog.json` contains exactly these six unique live product IDs and no other product:
   `cm_all_access`, `cm_supporter_pack`, `cm_theme_sakura`, `cm_theme_neon`, `cm_rewind_5`,
   `cm_rewind_20`.
2. Store types and positive/negative entitlement attachments exactly match the frozen contract:
   All Access → all_access/theme_sakura/theme_neon; Supporter →
   supporter/all_access/theme_sakura/theme_neon; each theme → its namesake; both rewinds → none.
   `cm_all_access_499` is absent.
3. The game placement set is exactly `post_level_5`, `theme_preview`, `bonus_district`, `shop`,
   `rewind_failure`. No Offering identifier or `ofr_*` token appears in Resources or Services.
4. The passive DTO vocabulary distinguishes the six required purchase outcomes: success candidate,
   user cancelled, failure, restored, pending, and unknown/unsettled. `Unavailable` and `Busy` may
   exist as fail-closed transport outcomes, but no outcome grants anything.
5. A localized-price DTO carries display text only. It has no numeric price, conversion,
   currency fallback, formatter, or hard-coded amount.
6. The rewarded config contains exactly these eight unique IDs:
   `rewind_failure`, `double_tickets`, `daily_gift_double`, `streak_saver`, `theme_rental`,
   `cat_skin_trial`, `livery_trial`, `district_guest_route`.
7. Reward/cap tuples are exact:
   - rewind_failure: one rewind, 2/session, 5/local date;
   - double_tickets: named ticket double, 3/local date;
   - daily_gift_double: named gift double, 1/local date;
   - streak_saver: named streak repair, 1/local date;
   - theme_rental: selected theme for 3 eligible completed levels, 1/theme/local date;
   - cat_skin_trial: selected skin for 3 eligible completed levels, one total skin lease/local date;
   - livery_trial: selected livery for 3 eligible completed levels, one total livery lease/local date;
   - district_guest_route: one signed practice route, 1/district/local date and 1/session.
8. Every rewarded row has `sdkCallEnabled=false` and disabled reasons NEW-Q45 plus device gate.
   The last three also include ADR-0006 supersession; the first five do not.
9. Reflection/source checks prove Services DTOs are SDK/Unity/persistence-free and expose no
   operational public methods beyond constructors/accessors. No `IPurchases`, ad interface, handle,
   SDK configuration, async operation, event, or callback seam appears in this task.
10. The shell scanner has a live negative control: a temporary file containing a forbidden SDK/ad/
    `ofr_*` token fails the same scan that the real owned trees pass.

### Minimal implementation

- Use enums and readonly DTOs for semantic vocabulary; arbitrary raw SDK objects never cross them.
- Keep catalog/reward definitions in the two JSON resources as identifier/configuration data, not
  duplicated executable allowlists. The later canonical adapter owns remote validation after its
  ADR and may consume an injected validated projection; this task does not design that parser.
- Rewarded metadata is descriptive and unreachable. It contains no `CanShow`, request, load, show,
  complete, grant, telemetry, counter, or clock behavior.

### Verification

- Before any production/data file exists, run the targeted EditMode namespace/filter and shell
  gate, record their expected failures, and commit that red-only test state.
- Run the targeted EditMode namespace/filter for `CatMetro.Tests.Purchases` and record total/pass.
- Run `bash tests/monetization/purchases-foundation.test.sh`.
- Run `bash scripts/check.sh`, `bash scripts/test.sh`, and `bash scripts/build.sh`; record real Unity
  EditMode and PlayMode totals rather than accepting an Editor-deferred half-pass.
- Commit the Task 1 green implementation separately, then complete independent spec-and-quality
  review before Task 2.

## Task 2 — single pure fairness choke point

### Files

The antecedent red-only commit creates:

- `unity/Assets/Tests/EditMode/Pure/Purchases/OfferEligibilityServiceTests.cs` + `.meta`

and extends the owned `tests/monetization/purchases-foundation.test.sh` with Task 2 static
single-source/forbidden-token assertions. The later green implementation commit creates:

- `unity/Assets/Scripts/Services/Purchases/OfferEligibilityDtos.cs` + `.meta`
- `unity/Assets/Scripts/Application/Purchases.meta`
- `unity/Assets/Scripts/Application/Purchases/OfferEligibilityService.cs` + `.meta`

### Passive DTO shape

Use passive request/decision DTOs and enums; behavior stays in Application:

- gameplay phase vocabulary: Read, FirstRoute, Rhythm, Crunch, FailureReview, PostWin,
  NonGameplay;
- a post-Level-5 request carrying: phase, level number, first-completion fact,
  celebration-complete fact, before-map-return fact, exposure-consumed fact, any active durable
  entitlement, any prior SKU purchase, refund suppression, whether any system-initiated
  monetization surface has already appeared this session, whether the prior surface was commerce,
  whether a gameplay failure has occurred, and exact non-negative elapsed milliseconds since the
  most recent failure;
- a rewind request carrying: phase, level number, attempt number, integer progress percent,
  safe-decision-tick fact, explicit eligible-chip-tap fact, free rewind availability, owned rewind
  availability, refund suppression, 24-hour post-purchase pack-cooldown-active fact, whether the
  previous surface was commerce, and a validated rewind-offering-with-localized-prices fact;
- decisions carry explicit booleans/row sequence plus flags-style blocker sets. A flags set avoids
  inventing a blocker priority.

No request contains a Save object, SDK type, service callback, clock, environment flag, ad-enable
toggle, or dependency-gate bypass.

### Red tests and exact policy

1. All four gameplay phases Read/FirstRoute/Rhythm/Crunch block both post-L5 exposure and every
   rewind chip/sheet/placement/purchase decision. Non-gameplay and wrong boundary combinations also
   fail closed.
2. Post-Level-5 is policy-eligible only when all are true: PostWin phase, level 5, first completion,
   celebration complete, still before map return, exposure not consumed, no active durable
   entitlement, no prior SKU purchase, no refund suppression, zero earlier system-initiated
   monetization surfaces this session, previous surface not commerce, and no gameplay failure inside
   the preceding 60 seconds. “System surfaces” includes commerce and rewarded. Each individual
   mutation blocks and appears in the blocker flags.
3. Failure cooldown boundaries: no prior failure allows; 59,999 elapsed milliseconds blocks;
   60,000 allows. Negative elapsed input fails closed. This system exposure has no rewind-tap
   exception, and the policy performs no wall-clock computation or rounding.
4. Rewind chip eligibility requires FailureReview, attempt ≥2, progress ≥40, and a safe decision
   tick. Test attempt 1/2 and progress 39/40 boundaries plus missing safe tick.
5. Eligibility alone never opens a sheet. Only an explicit tap of an eligible rewind chip can open
   gameplay options; this is the sole 60-second failure exception modeled by the slice.
6. Before Level 7, an eligible tap returns only available free then owned gameplay rows. It never
   marks placement policy eligible, never includes rewarded or pack rows, and has no purchase/ad
   operation.
7. At Level 7+, the modeled row order is free, owned, rewarded, then packs when those rows are
   available. The rewarded position is explicitly disabled with `SdkCallAllowed=false`; it cannot
   create an ad request and no input can enable it. Packs appear only when a validated rewind
   Offering with localized display prices is supplied.
8. The decision may report `PlacementPolicyEligible=true` only for an eligible, explicitly tapped,
   Level-7+ rewind with no refund suppression, no active 24-hour pack cooldown, and no preceding
   commerce surface. This is a pure future game-policy fact, not permission to invoke the currently
   source-blocked placement API. No production caller exists in this slice.
9. At Level 7+, refund suppression, the supplied 24-hour post-purchase pack cooldown, or a preceding
   commerce surface removes every pack row and makes `PlacementPolicyEligible=false`; available
   free/owned rows and the modeled disabled rewarded position remain in their signed order. The
   slice does not compute the 30-day refund window or 24-hour cooldown.
10. Post-Level-5 entitlement/prior-purchase/refund suppression and rewind decisions are implemented
   only in `OfferEligibilityService`. A static scan fails if another Services/Application source
   declares eligibility/suppression behavior or if Bootstrap/Presentation/Integration is touched.
11. Mutations that change phase acceptance, attempt/progress comparisons, Level-7 boundary, row
    order, 60-second comparison, payer/prior-purchase/refund suppression, session count, or
    back-to-back/24-hour-pack-cooldown rule must make at least one named test red.

The wider CM-R29 Shop/supporter matrix is not implemented in this slice. In particular, the signed
documents disagree on the already-active-supporter card state (reframed/visible versus already-owned
non-buyable). No caller for those future custom surfaces exists, so choosing that cell now would be
invented behavior. All future suppression decisions must extend this same service after a human
ruling; they may not create a second policy class.

### Verification

- Before production files exist, run the targeted EditMode namespace/filter and shell gate, record
  their expected failures, and commit that red-only test state.
- Run the targeted EditMode namespace/filter for `CatMetro.Tests.Purchases` and record total/pass.
- Run `bash tests/monetization/purchases-foundation.test.sh`.
- Run `bash scripts/check.sh`, `bash scripts/test.sh`, and `bash scripts/build.sh`; record real Unity
  EditMode and PlayMode totals and the relevant dotnet-linked gate.
- Commit the Task 2 green implementation separately, then complete independent spec-and-quality
  review before Task 3.

## Task 3 — dormant pure rewarded outcome laws

### Files

The antecedent red-only commit creates:

- `unity/Assets/Tests/EditMode/Pure/Purchases/RewardedOutcomePolicyTests.cs` + `.meta`

and extends the owned shell gate for the Task 3 no-SDK/no-persistence/single-policy assertions. The
later green implementation commit creates:

- `unity/Assets/Scripts/Services/Purchases/RewardedOutcomeDtos.cs` + `.meta`
- `unity/Assets/Scripts/Application/Purchases/RewardedOutcomePolicy.cs` + `.meta`

`RewardedOutcomePolicy` is not a second offer-eligibility or suppression service. It transforms
already-derived dormant outcome facts only; `OfferEligibilityService` remains the sole eligibility
choke point.

### Red tests and exact dormant policy

1. Every decision carries `SdkCallAllowed=false`. No request/load/show/callback/telemetry method,
   SDK type, ad interface, clock, Save object, or production caller exists.
2. Rewarded rewind caps are checked before a hypothetical request and rechecked before a
   hypothetical grant: counts immediately below two/session and five/local-date pass the cap test;
   counts at either limit fail closed. No decision itself increments a count.
3. A decline consumes no cap and grants nothing. From an already-derived consecutive-decline count,
   the third consecutive decline requests a 24-hour mute disposition for every rewarded row. A
   modeled player-initiated ad tap requests reset of the decline sequence/mute; neither transition
   is persisted here.
4. A load/no-fill failure requests consumption of that placement's applicable daily and session
   slot, hides the row for the session, grants nothing, and never substitutes a purchase prompt.
5. Only an already-derived independently verified completion with both cap rechecks open and an
   already-derived dedupe-eligible fact can output one indivisible
   `CommitCapAndRewardAtomically=true` disposition. Missing verification, failed cap recheck, or
   duplicate/late status yields no commit and no grant instruction. This slice performs no commit.
6. Trial expiry during a level defers silent reversion until the next safe Home/result boundary;
   it never interrupts the level. The request uses an exact completion-kind enum rather than a
   caller-controlled generic eligibility boolean: only campaign first-completion or explicitly
   marked local-practice completion requests decrement. Table tests reject Daily, Cup, failed,
   abandoned, replayed, and unknown/other completion kinds.
7. Cosmetic theme/cat/livery trials are always excluded from Daily, Cup, share-card, and global-rank
   evidence. Mutating any exclusion red-lines the policy test.
8. A district guest-route decision requests zero score, tickets, stars, progress, medal, Daily/Cup
   result, or global-rank evidence. With already-derived first-success and grant-local-date-rollover
   facts, unlimited full retries remain free until the first successful completion or grant-local-
   date rollover, whichever comes first. Task death during an attempt returns the route to an
   available free retry when neither terminal boundary has occurred; no attempt counter is created.
9. Mutations to either rewind cap comparison, the third-decline boundary, 24-hour duration,
   no-fill slot/hide/no-grant disposition, atomic-commit conjunction, trial boundary, cosmetic
   exclusions, or guest-route zero-write set make a named test red.

### Verification

- Before production files exist, run the targeted EditMode namespace/filter and shell gate, record
  their expected failures, and commit that red-only test state.
- Run the targeted EditMode namespace/filter for `CatMetro.Tests.Purchases` and record total/pass.
- Run `bash tests/monetization/purchases-foundation.test.sh`.
- Run `bash scripts/check.sh`, `bash scripts/test.sh`, and `bash scripts/build.sh`; record real Unity
  EditMode and PlayMode totals and the relevant dotnet-linked gate.
- Commit the Task 3 green implementation separately, then complete independent spec-and-quality
  review.

## Broad review and foundation handoff

After Tasks 1–3 all pass their task reviews:

1. run `bash scripts/check.sh`, `bash scripts/test.sh`, and `bash scripts/build.sh`;
2. record exact Unity EditMode and PlayMode totals rather than accepting an editor-deferred pass;
3. run source/path sweeps for foreign SKU/placement/reward identifiers, `ofr_*`, price literals,
   SDK/ad tokens, PlayerPrefs, Save edits, extra asmdefs, and out-of-scope files;
4. obtain a broad independent review of the complete branch diff from `d10509d`; and
5. leave all later gates explicitly open. Do not open or merge a completion PR while the product
   integration is still stopped.

## Later execution queue — not dispatchable under current authority/state

These are sequencing markers, not authorized implementation tasks:

- full `IPurchases`, opaque handle lifetime, adapter callbacks, package pins, UPM/EDM resolution,
  build-key injection, and RevenueCat configuration wait for the signed dependency/privacy ADR and
  package-baseline/ProjectSettings decisions;
- every real placement call waits for the memory/disk-cache and placement-error rulings/remedy;
- RCUI and purchase calls wait for exact native Offering-before-render and Package/product-before-
  billing validation/veto remedies, the error/copy ruling, and fallback impression-ID/A-B rulings;
- entitlement cache, purchase breadcrumbs, once-ever marker, consumable recovery, session/local-day
  counters, reward grants, and strike behavior wait for signed ADR-0006, NEW-Q46, exact Save files,
  canonical transaction identity/provenance, and reviewed contract amendment;
- custom fallback UI waits for canonical strings/assets, dashboard copy/fallback corrections,
  Night Harbor/Neon evidence, and the renderer rulings;
- Bootstrap/GameRoot remains funnel-last after Lane 8 and any already-landed Lane 11 tail;
- Test Store CI, immutable hook scanning, real Unity CI, NEW-Q48, production mode, device/proxy/
  dashboard evidence, independent billing/security review, and every remaining frozen-contract
  gate must close before the final Lane 5 PR can merge.
