# CONTRACT CM-MONETIZATION-CODE — RevenueCat integration (Wave 2, Lane 5)

**Lane:** Wave 2, Lane 5 `MONETIZATION-CODE`. **Branch:**
`feat/revenuecat-integration`. **Worktree:** `cat-metro-app-revenuecat`. **Drafted:**
2026-08-11. **Freeze candidate updated:** 2026-08-14. **Frozen:** 2026-08-14; first-commit SHA
pending. **Base/anchor:** `origin/main` at
`d10509d4294a5e657dbedb3d8d42f0ce16b9fbdf`. **Risk-glob anchor:** PR #70 at
`291dc590a859b314f574219425ca37a6e7a0cdcb`. **Addendum anchors:** merged PR #69 at
`d861eb86660b91973c65f36473c16b58937bf489` (Wave 2 Addendum v2), extended by
`1744facfd31ff29ab32638e15b1a5864743988a0` (Addendum v2.1-v2.3).

**Review state:** FROZEN FOR FIRST COMMIT — the human rulings are recorded below. The conditional Save
grant is currently void because no qualifying in-repo signed ADR-0006 amendment exists; the
authorized `Application/Save/**` file set at this freeze is empty. The ambiguous RevenueCatUI
Error-to-custom-renderer handoff is an explicit no-code stop pending a direct human ruling and a
reviewed contract amendment. Stock Android RCUI and stock package purchase also remain no-code
because each re-resolves unvalidated native commerce data through cache-first ordinary Offerings
lookup before render/billing; a human-approved,
independently reviewed native Offering/Package validation-and-veto remedy is required.

This document is the branch's first authored change. It freezes the task contract before
behavior or dependency changes. The signed monetization specification plus merged #64
amendment, ADR-0010, Addendum v2-v2.3, and the human rulings recorded in this chat are binding.
Where the broad #64 amendment describes a larger future catalog, the human ruling below
constrains this lane to the original six live products and five commerce placements.

## Preconditions discharged before freeze

1. PR #70 was independently reviewed and squash-merged at
   `291dc590a859b314f574219425ca37a6e7a0cdcb`; its risky-path globs remain present on the
   current base.
2. PR #69 is now merged at `d861eb86660b91973c65f36473c16b58937bf489`, and Addendum
   v2.1-v2.3 is on main at `1744facfd31ff29ab32638e15b1a5864743988a0`.
3. A fresh `git fetch origin main` on 2026-08-14 advanced the base from `fac3e354` to
   `d10509d4294a5e657dbedb3d8d42f0ce16b9fbdf`. Lane 5 fast-forwarded before its first authored
   commit. The only intervening PR #84 files were DailyTools/DailyPipeline posture corrections,
   outside every Lane 5 ground-truth, owned, and exception surface; the contract was re-audited.
4. The `forge-risk.sh` parser was reproduced against `origin/main:AGENTS.md`. It classified
   representative files in all five intended tripwires: canonical RevenueCat integration,
   Monetization resources, Android plugins, EDM4U, and Unity Packages. Negative controls
   outside those paths stayed unmatched.
5. The lane branch was fast-forwarded to the current anchor. `git log origin/main..HEAD` was empty
   before this file was authored.
6. A second `git fetch origin main` immediately before the first commit must still resolve both
   HEAD and `origin/main` to the same anchor; any advance requires another fast-forward and
   contract re-audit before freeze.

## Goal

Integrate RevenueCat core and RevenueCatUI 9.7.0 through a single canonical assembly, with
exactly six active product identifiers and exactly five commerce placements. Implement the
one scripted post-Level-5 paywall and the approved rewarded-placement model while enforcing
the signed fairness invariants at every boundary. Store price presentation is localized
store metadata only. No offer may be constructed, fetched, or shown in gameplay phases 1–4.

The implementation is fail-closed: missing configuration, unknown remote identifiers,
missing offerings, package-resolution errors, stale state, unavailable SDK services, and
unsettled purchase outcomes must never grant content, consume a reward, invent a price, or
surface an unauthorized offer.

## Human-approved interpretation

The following rulings were explicitly granted in this chat and are part of this freeze:

- RevenueCat core and UI are pinned to 9.7.0.
- The executable catalog is exactly the six live SKUs below; no experiment SKU or deep-catalog
  candidate is activated or implemented.
- The executable commerce-placement catalog is exactly the five placements below.
- Eight rewarded identifiers are modeled. The three #64 additions remain hard-disabled until
  a human supersedes ADR-0006. All eight rewarded SDK call paths remain disabled until
  NEW-Q45 and device gates are resolved.
- An eligible pre-Level-7 rewind exposes free or already-owned gameplay options only: no
  RevenueCat placement fetch, no rewarded-ad call, and no purchase row.
- Rewarded rewind caps are two per session and five per local day.
- There is one scripted post-Level-5 paywall; no monetization exists in gameplay phases 1–4.
- Runtime price copy comes only from localized store metadata.
- Authoring before the production-mode flip is sanctioned. Merge remains forbidden until the
  human-authored production flip lands and every required independent code, billing/security,
  CI, dependency, device, and human-judgment gate passes.
- Addendum v2.3 retires HC-25's per-merge fresh-word request while preserving the merge census.
  No fresh HC-25 word is requested or treated as a gate.
- The human's later direct 2026-08-13 instruction authorizes this lane agent to squash-merge its
  own Lane 5 PR after all non-delegable gates are satisfied. It supersedes Addendum v2.3's
  Lane-5 human-button floor only for this PR's merge actor; it does not arm production, approve
  an ADR or price, accept a security-severity judgment, delegate an excluded-path PR, authorize
  a console mutation, tag/release/deploy/spend, or weaken any merge precondition.

### Verbatim authority record

This subsection is an agent-authored transcription and therefore carries the usual H-1-class
confirmability caveat until the human confirms it in this chat or a human-authored repository
record replaces it. It preserves the load-bearing words instead of leaving them only in chat:

> I GRANT Lane 5 the additional surfaces: Scripts/Services/Purchases/** (IPurchases + SDK-free
> DTOs), Bootstrap wiring in GameRoot.cs + its asmdef (funnel position LAST), amendment of the
> EXISTING EditMode test asmdef (no third test assembly, per ADR-0005), the narrow
> taxonomy-gate allowlist for the canonical assembly only, config/pins.json, and the pinned
> EDM4U + unity/Packages/** surface.

> Q1 RULING: you MAY author monetization code on the lane branch before the mode flip. The
> production-mode flip remains a hard precondition to MERGE, alongside the independent
> security review and my fresh HC-25 word. THIS IS the Q1 answer; it is not HC-25 and arms
> nothing.

Later direct user authorization:

> Once you do, I authorize you to do the PR merges yourself. Thanks

And the user's immediately following confirmation:

> You have my full authorization to do this yourself.

Final surface and conflict rulings for this freeze:

> I GRANT Lane 5 unity/Assets/Scripts/Application/Purchases/** and the exact Application/Save
> files named by the in-repo signed ADR-0006 amendment, plus Lane 5's own tests. This grant is
> void as to the Save files until that amendment is signed in-repo.

> I GRANT the exact HashAndParityTests.cs package-pin parity exception from the original brief.

> I RULE that #64's "No Offering = no UI" supersedes CM-R25's cached-offering fallback, and I
> GRANT Presentation/Commerce/** and Tests/PlayMode/Commerce/** as NEW directories, solely for
> the post-Level-5 fallback behavior you described.

> I GRANT unity/Assets/Editor/RevenueCatBuildConfigInjector.cs (+ .meta), and I AUTHORIZE the
> separate security-reviewed .github Test Store CI prerequisite PR, human-merged.

> None of the above is HC-25; it arms no merge.

Addendum v2.3 later retired the HC-25 per-merge fresh-word requirement while keeping census
recording. After that addendum landed, the human gave this direct Lane 5 instruction on
2026-08-13:

> keep authoring toward complete, review-ready state, and reminder that your merges are for you
> to do, and I give you full authorization after you have reviewed and everything looks good to
> go ahead and merge please.

**2026-08-13 merge-actor supersession (direct Lane 5 chat; agent-relayed, H-1-class):** the
instruction authorizes this lane agent to squash-merge its own Lane 5 PR after every governing
check, independent code/billing/security review, and required human gate is satisfied. This
supersedes Addendum v2.3's Lane 5 human-button floor only for this PR's merge actor. “After you
have reviewed” means the constitutionally required independent review chain, never author
self-approval. It does not delegate `state/mode`, ADR or price approval, security-severity
acceptance, `.github/**` or other Amendment-1-excluded PRs, production-console changes,
tags/releases/deploys/spend, or publication. If this PR gains an Amendment-1-excluded path, the
agent merge route closes and the human must merge. HC-25's fresh-word requirement remains retired.

## Exact executable catalog

Only these six product identifiers may be represented as live products:

| Product identifier | Store type | Product role |
|---|---|---|
| `cm_all_access` | non-consumable | permanent All Access entitlement |
| `cm_supporter_pack` | non-consumable | permanent supporter grant; Shop only |
| `cm_theme_sakura` | non-consumable | permanent Sakura theme entitlement |
| `cm_theme_neon` | non-consumable | permanent Neon theme entitlement, still subject to its content/device gate |
| `cm_rewind_5` | consumable | five-rewind pack |
| `cm_rewind_20` | consumable | twenty-rewind pack |

`cm_all_access_499` is experiment-only and inactive. It must not be added to the runtime
catalog, requested packages, Shop, post-Level-5 surface, entitlement code, tests' expected
active set, or any fallback. The additional products proposed by the merged #64 amendment
are future signed-catalog work and are non-executable in this lane.

No currency amount, formatted price, currency code, or locale-specific price fallback may be
hard-coded in runtime code or player-facing resources. A missing localized store price hides
the affected purchase affordance rather than fabricating price copy.

`cm_theme_neon` remains one of the six frozen products, but its runtime activation is a signed
CM-R21 merge gate, not an assumption. Before it may appear in `ofr_themes` or this lane may merge,
human-owned shipped-asset evidence must prove deutan/protan/tritan simulation passes; every
line-colored element retains both symbol and distinct silhouette identifiers; Sakura/Neon leave
that encoding byte-identical; the color-removed symbol+silhouette run scores at least 23/25 under
the five-rater protocol; and the silhouette-only 64 px run independently scores at least 23/25.
Lane 5 consumes that evidence and does not modify the theme or line assets.

The executable entitlement attachment map is exactly:

| Product identifier | RevenueCat entitlements |
|---|---|
| `cm_all_access` | `all_access`, `theme_sakura`, `theme_neon` |
| `cm_supporter_pack` | `supporter`, `all_access`, `theme_sakura`, `theme_neon` |
| `cm_theme_sakura` | `theme_sakura` |
| `cm_theme_neon` | `theme_neon` |
| `cm_rewind_5` | none; durable consumable ledger only |
| `cm_rewind_20` | none; durable consumable ledger only |

Each feature checks its single exact entitlement. Client code must not recreate umbrella
attachments with boolean combinations. Future district and cosmetic entitlement attachments
from #64 are non-executable until their own coordinated catalog supersession.

Local allowlists cannot prove RevenueCat/Play console attachment truth. Before merge, human-owned
Play product plus RevenueCat product/entitlement exports must prove all six live identifiers are
active with the exact one-time store types above, `cm_all_access_499` remains inactive, and the
attachment table is exact in both positive and negative directions. A Play license-tester sandbox
matrix purchases each live SKU once: All Access returns exactly `all_access`, `theme_sakura`, and
`theme_neon` (not `supporter`); Supporter returns exactly those three plus `supporter`; each theme
returns only its namesake entitlement; each rewind SKU emits zero entitlement changes and commits
exactly +5 or +20 through the sole consumable ledger. Test Store alone cannot sign off this
store/attachment matrix.

## Exact commerce placements and offering confinement

Game-side code may name only these five placement identifiers. The integration assembly alone
may contain their remote RevenueCat offering mapping:

| Game placement | Integration-only offering | Exact ordered package → product shape | Lane 5 renderer |
|---|---|---|---|
| `post_level_5` | `ofr_core` | `all_access` → `cm_all_access` | signed target: RevenueCatUI primary + custom crash fallback, All Access only; primary stopped by 9.7.0 Android source conflict |
| `theme_preview` | `ofr_themes` | `theme_sakura` → `cm_theme_sakura`; `theme_neon` → `cm_theme_neon`; `all_access` → `cm_all_access` | none; custom UI is deferred |
| `bonus_district` | `ofr_core` | `all_access` → `cm_all_access` | none; custom UI is deferred |
| `shop` | `ofr_shop` | `all_access` → `cm_all_access`; `theme_sakura` → `cm_theme_sakura`; `theme_neon` → `cm_theme_neon`; `rewind_5` → `cm_rewind_5`; `rewind_20` → `cm_rewind_20`; `supporter` → `cm_supporter_pack` | none; custom UI is deferred |
| `rewind_failure` | `ofr_rewind` | `rewind_5` → `cm_rewind_5`; `rewind_20` → `cm_rewind_20` | none; custom UI is deferred |

These are custom package identifiers; no `$rc_*` package is accepted. Order is part of each
complete shape, not a merchandising hint.

All `ofr_*` tokens are forbidden outside
`unity/Assets/Scripts/Integrations/RevenueCat/**`. The taxonomy-gate exception permits the
RevenueCat token only beneath that canonical path and retains a failing negative control for
the same token outside it. A separate Lane 5-owned static test enforces `ofr_*` confinement;
the narrow E-1 taxonomy edit is not silently expanded. No future #64 commerce placement or
offering enters this lane.

Remote offering contents are untrusted input. The adapter validates the original Offering
identifier, package identifiers, and product identifiers against the exact requested-placement
shape. It rejects the entire Offering on any unknown, missing, duplicate, or cross-placement
entry, order mismatch, `$rc_*` package, or non-null Offering/Package `WebCheckoutUrl`; it never
synthesizes or mutates a filtered RevenueCat `Offering`. Every retained package's
`StoreProduct.ProductCategory` must equal `Purchases.ProductCategory.NON_SUBSCRIPTION`; a
`SUBSCRIPTION` or fail-closed `UNKNOWN` category rejects the entire Offering. In particular,
Supporter is Shop-only and the original `post_level_5`/`ofr_core` Offering must contain exactly
the All Access package/product required by the signed surface. Empty or invalid results produce
no UI. Per the human's #64 supersession ruling, no current-offering or cached last-good fallback
may substitute for a missing Placement result.

Package and attribution shape is also part of whole-Offering validation. Every package's exact
SDK package type must be `CUSTOM`. Every Package and StoreProduct `PresentedOfferingContext` must
be present, have `OfferingIdentifier == Offering.Identifier`, and have `PlacementIdentifier ==`
the exact requested game placement. All package/product contexts in the Offering must agree
exactly, including targeting-context null versus value and, when present, its revision and rule ID;
normal targeting may be null and no targeting ID is invented. Any missing, mismatched, cross-flow,
or cross-package context rejects the entire Offering. This equality is required because custom-
impression attribution derives context from `AvailablePackages[0]` while a purchase may select a
different package.

Pinned 9.7.0 placement lookup cannot currently satisfy the no-cached-substitution law. Hybrid
18.29.0 calls ordinary cache-first `getOfferingsWith`; purchases-android 10.16.0 may immediately
vend memory-cached Offerings and refresh later, or parse disk-cached Offerings after a network
failure. `originalSource`/`loadedFromDiskCache` are internal and are omitted by the hybrid/Unity
mapping, and Unity exposes neither provenance nor force-current/cache invalidation. A dashboard
change to no Offering can therefore still return a prior valid allowlisted Offering. Dashboard
fallback=None remains necessary but is insufficient. No production placement call or placement-
dependent commerce is authored until a direct human ruling plus reviewed contract amendment either
accepts these exact SDK cache semantics under “No Offering = no UI,” or authorizes an independently
reviewed native boundary that forces current data, exposes provenance, and rejects memory/disk
cache. Such a boundary reopens dependency, integrity, privacy, and security review.

The earlier dashboard runbook still says otherwise at
`docs/plan/data/revenuecat_configuration.csv:29,34,36,37,39`; row 31 incorrectly cross-lines All
Access into `ofr_rewind`, and row 40 still directs the RevenueCat Paywalls-v2 template to use the
superseded “complete Cat Metro” subhead. RevenueCat returns a dashboard-selected
Placement fallback as an ordinary `Offering`, so client allowlisting cannot recover the original
empty result. Before device acceptance or Lane 5 merge, a human must set the Placement fallback
to **None** for all five locked placements, disable web checkout for the governed Paywalls-v2
configuration, and attach dashboard export/screenshot plus device null-resolution evidence. The
export and device fixtures must also prove the exact complete package/product shape of every
offering: `ofr_core` = All Access only, `ofr_themes` = Sakura + Neon + All Access, `ofr_shop` =
the six live products only, and `ofr_rewind` = rewind-5 + rewind-20 only.
`ofr_core` may remain the dashboard Current offering, but Lane 5 never calls ordinary
`GetOfferings` and never substitutes it. This is human-owned console state, not a dashboard
mutation or repository-file edit by Lane 5. An authorized owner must also correct the
stale fallback instructions at rows 29, 34, 36, 37, and 39, the invalid cross-line at row 31,
and the stale dashboard-copy instruction at row 40 before Lane 5 merge. The fallback correction
prevents silent fallback
re-enablement; row 29 may continue to mark `ofr_core` Current but must remove Supporter from that
Offering and no longer prescribe it as a Placement fallback. Row 40 must require the
§4.1-as-amended-by-§8.2 copy in the RC template,
including `every playable line in Cat Metro`, explicit Sakura Line + Neon Line, and no “complete
edition” claim. That docs correction is not part of this branch.

Two machine-readable companions are also stale and cannot remain competing authorities:
`docs/plan/data/offering_and_placement_map.json` still mandates cached/current fallback,
Supporter inside `ofr_core`, and All Access inside `ofr_rewind` (including its post-Level-5
fallback field), while `docs/plan/data/entitlement_map.json` repeats the forbidden Offering
memberships. Before Lane 5 merge, an authorized human-button predecessor must either correct or
explicitly retire/supersede those exact fields so both files agree with No Offering = no UI and
the complete four-Offering shapes frozen above. Its proof must reject cached/current substitution,
Supporter in `ofr_core`, and All Access in `ofr_rewind`; Lane 5 does not edit either `docs/plan/**`
file.

## Rewarded-placement model

The SDK-free model contains exactly these eight identifiers:

| Rewarded placement | Signed modeled reward/cap | Lane 5 state |
|---|---|---|
| `rewind_failure` | one rewind; two/session and five/local-date | SDK call hard-disabled pending NEW-Q45/device gates |
| `double_tickets` | named ticket double; three/local-date | SDK call hard-disabled pending NEW-Q45/device gates |
| `daily_gift_double` | named gift double; one/local-date | SDK call hard-disabled pending NEW-Q45/device gates |
| `streak_saver` | named streak repair; one/local-date | SDK call hard-disabled pending NEW-Q45/device gates |
| `theme_rental` | selected theme for three eligible completed levels; one/theme/local-date | SDK call hard-disabled pending NEW-Q45/device gates |
| `cat_skin_trial` | selected skin for three eligible completed levels; one total skin lease/local-date | additionally hard-disabled pending human ADR-0006 supersession |
| `livery_trial` | selected livery for three eligible completed levels; one total livery lease/local-date | additionally hard-disabled pending human ADR-0006 supersession |
| `district_guest_route` | one signed practice route; one district/local-date and one/session | additionally hard-disabled pending human ADR-0006 supersession |

No rewarded-ad SDK dependency or live ad call is introduced by this lane while those gates are
closed. There are no banner, interstitial, forced, or app-open ad surfaces. RevenueCat
AdTracker, `TrackAdRevenue`, and `AdRevenueData` types, calls, wrappers, and future adapter seams
are absent from compiled Lane 5 code. Addendum v2 defers analytics wiring, so the lane models only
the eight SDK-free rewarded identifiers and their disabled metadata; it does not create a dark
factory for later ad telemetry.
No reward is granted without an independently verified completed-ad result and a durable,
deduplicated grant path authorized by the ADR-0006 amendment described below.

The dormant model also preserves the amendment's pressure and grant laws: three consecutive
declines mute every ad row for 24 hours; a decline consumes no cap; load/no-fill eventually
consumes the applicable slot but grants nothing and hides that row for the session; completion
commits cap use and the deduplicated reward atomically. Any player-initiated ad tap resets the
consecutive-decline mute. Trial expiry never interrupts a level. Cosmetic trials cannot enter
Daily, Cup, share-card, or global-rank evidence, and the district guest route writes no score,
ticket, star, progress, medal, Daily/Cup result, or global-rank state. These behaviors remain
non-reachable until the ad and persistence gates are signed and green.

## Fairness invariants

These rules are defense in depth: the SDK-free policy decides eligibility, Bootstrap calls
only after that decision, and the integration boundary rejects disallowed requests again.

1. **Gameplay-phase embargo.** During Read, First-route, Rhythm, and Crunch (phases 1–4), no
   commerce or rewarded surface is constructed and no offering or ad request is made. Phase 5
   is the earliest eligible failure path. Post-win is the other allowed boundary.
2. **Attempt one is commerce-free.** The first failed attempt shows no rewind chip, performs no
   placement fetch, calls no ad SDK, and exposes no purchase action.
3. **Failure eligibility.** A rewind chip is eligible only when attempt is at least two,
   progress is at least 40 percent, and a safe decision-tick snapshot exists. Eligibility does
   not itself fetch anything; the sheet may be built only after the player taps the chip.
4. **Pre-Level-7 protection.** Before Level 7, an eligible sheet contains free and already-owned
   gameplay choices only. It performs no `rewind_failure` placement fetch, no ad call, and no
   purchase-row construction.
5. **Level-7-plus order.** At Level 7 and later, an eligible tapped sheet orders rows as free,
   owned, rewarded, then packs. A purchase row remains secondary and appears only after a
   valid allowlisted offering with localized store metadata.
6. **Rewarded rewind caps.** Rewarded rewinds are capped at two per session and five per local
   date. Both limits are checked before any eventual ad request and rechecked before a grant.
7. **Rank integrity.** A run using any free, rewarded, or purchased rewind is ineligible for
   Google Play Games global-rank submission under ADR-0010. Its existing local result remains;
   a local Cup completion is capped at Silver and retains the participation livery, while
   rewind use never changes scoring rules or buys Gold eligibility.
8. **System-surface pacing.** At most one system-initiated monetization surface appears in a
   session, and no commerce surfaces appear back-to-back. The 9.7.0 API cannot reveal whether
   RevenueCatUI became visible before returning an unlatched `PaywallResult.Error`, so an
   automatic RCUI-to-custom handoff could violate this law. That handoff is not authorized by
   inference and remains non-operative pending the explicit human choice below. No offer may
   appear within 60 seconds of any gameplay failure unless the player explicitly taps the
   eligible rewind chip.
9. **Payer and entitlement suppression.** A player with any durable entitlement, any prior
   purchase, or a refund suppression marker does not receive the scripted post-Level-5
   surface. Entitlement truth is local-first and must not depend on a network round-trip to
   keep paid content usable.

## Scripted post-Level-5 paywall

The signed target names RevenueCatUI as primary at `post_level_5` and a custom UGUI renderer under
the newly granted `Presentation/Commerce/**` directory only as its crash fallback. Exact pinned-
source review established that stock RevenueCatUI 9.7.0 cannot enforce the signed Offering
boundary on Android, so primary RCUI orchestration is a no-code source conflict at this freeze.
The custom renderer may be built and validated independently, but it does not silently become the
primary; its real purchase row/call is also stopped by the native Package conflict below, and no
automatic handoff is production-reachable:

1. It is considered only on the first completed Level-5 win, after the celebration and before
   map/advance navigation.
2. The durable once-per-install marker is committed before any offering fetch. An empty,
   unavailable, invalid, cancelled, or failed result never re-arms it.
3. Entitlement, prior-purchase, and refund-suppression checks run before the marker/fetch.
4. The integration serializes placement fetches, obtains the placement's original Offering,
   and rejects it unless its identifier and complete package/product set are exactly the signed
   All Access-only shape. It retains that original object for the custom renderer's display/
   purchase handles and never filters or reconstructs one, but does not pass it to the stock
   Android presenter.
5. Stock `PaywallOptions` retains the C# Offering only locally. The 9.7.0 Android bridge sends
   native code only its identifier and first-package `PresentedOfferingContext`; Java calls
   `setOfferingId`, and purchases-android 10.16.0 calls `awaitOfferings()` before selecting
   `offerings[id] ?: offerings.current`. The refreshed/current native Offering is never exposed to
   C# for whole-Offering validation before visibility. Worse, native purchase initiation invokes
   its resumable with `true` before the later observational `OnPurchaseStarted` callback, so that
   listener cannot veto a foreign package. A validated All-Access-only snapshot can therefore be
   replaced by changed/current packages before render or billing.
   Stock `PurchasePackage(originalPackage)` has a parallel billing TOCTOU: Unity sends Android only
   package identifier plus PresentedOfferingContext; hybrid-common 18.29.0 calls ordinary
   `getOfferingsWith`, finds the package identifier case-insensitively inside the cache-first
   re-resolved
   native Offering, and purchases that new Package. A remote change can retain `all_access` while
   swapping its product, so the later billing target is not the validated C# object. `PurchaseProduct`
   is no escape: it has no PresentedOfferingContext/package lineage and defaults to `subs` when
   explicit type is omitted. Selecting explicit `inapp` instead is a new human/spec decision, not
   an agent-authorized workaround.
6. No stock `PaywallsPresenter.Present`, stock `PurchasePackage`, `PurchaseProduct`, primary RCUI
   orchestration, real custom purchase row, or RCUI automatic-impression claim is authored until a
   direct human ruling plus reviewed SDK-boundary remedy preserves and validates the exact native
   Offering before render and the exact native Package/product before billing, with a veto before
   each operation. A vendor patch, fork, or custom Android bridge reopens dependency, integrity,
   privacy, and independent-security scope. Replacing RCUI primary with custom UGUI instead
   requires an explicit signed renderer/spec and scope supersession and still requires a safe
   native purchase boundary; the existing fallback-only grant is insufficient.
7. After such a remedy, the primary surface still has no Supporter row, experiment product,
   future #64 product, or counteroffer; automatic RCUI impressions are never re-reported as custom.
8. After such a remedy, dismissal, cancellation, and empty offering continue to map/advance
   without blocking play.
   RevenueCatUI receives a `PaywallListener`; its `OnPurchaseError` and `OnRestoreError` callbacks
   are guaranteed by the inspected 9.7.0 source to run before the terminal result. Therefore
   `PaywallResult.Error` plus either observed commerce-operation error advances without opening
   another purchase UI. For `PaywallResult.Error` with neither callback observed, the API exposes
   no reliable “was visible” signal. A direct human ruling plus reviewed contract amendment must
   choose exactly one behavior before the orchestration is implemented: **A**, authorize the
   immediate custom handoff as a narrow one-logical-exposure exception even though both renderers
   may have been visible; or **B**, preserve the no-back-to-back invariant and fail closed/advance,
   selecting custom fallback only before any RevenueCatUI presentation attempt. Until then, no
   production code or test may select, activate, or claim either branch.
9. A missing Placement offering never uses current/cached fallback and never opens either
   renderer. Stock Android RCUI's internal `offerings[id] ?: offerings.current` is exactly why it
   is stopped, not an allowed exception. The contract does not invent a separate thrown-exception
   path: RevenueCatUI 9.7.0 reports caught presentation exceptions, including Android presentation
   exceptions, as `PaywallResult.Error`.
10. No crash-marker strike state is authored under this frozen scope. CM-R26.2 consumes the only
   eligible exposure before fetch and never re-arms it, so CM-R26.6's second strike and permanent
   renderer disable are unreachable in production. A unit test that bypasses eligibility is not
   evidence. The signed ADR-0006 amendment and a reviewed contract amendment must explicitly
   either authorize a production-reachable retry while superseding the once-ever/no-rearm law,
   or supersede the two-strike threshold. Until that human ruling, there is no strike code,
   persistence, permanent-disable flag, or strike-boundary test.
11. The custom fallback replicates §4.1 as amended by signed §8.2, top to bottom: immediate
    close, Night Harbor hero, `One ticket. Every line.`, an amended subhead using the exact
    phrase `every playable line in Cat Metro` and no “complete edition” claim, four signed
    benefit rows that explicitly name Sakura Line + Neon Line, localized one-time price,
    localized unlock CTA, `Keep playing free`,
    all three signed disclosure lines, and the signed fair-design trust line. It consumes the
    canonical `UiStrings` keys from `Resources/Strings/ui.csv`; Lane 5 does not create a parallel
    string table or edit that Lane-1B-owned file. The required append and both current exact-row-
    count pins—`UiCsvDisciplineTests.cs` (`5 + 7`) and `UiCsvUx06Tests.cs` (`12`)—must be
    reauthored by an authorized predecessor before fallback implementation. The hero may live in
    the owned `Resources/Monetization/**` tree. No price literal enters the binary.
    The Night Harbor benefit is governed by the signed content-truth fallback in
    `product_spec.md`: if validated L901–L910 are present in the shipping build it may say the
    signed ten-level benefit; otherwise both RCUI and custom variants must use the signed
    `includes the Night Harbor district (arriving this week)` disclosure and a human must attest
    that delivery window remains truthful. Neither renderer may activate or Lane 5 merge unless
    one of those two states is evidenced consistently in `UiStrings`, dashboard export, and
    device frames.
12. The three disclosure lines remain visible without scrolling at 720p 16:9. Open-to-render is
    ≤2.0 seconds and purchase-to-entitlement is ≤3.0 seconds on the signed mid-tier device. The
    custom fallback is otherwise fair-core UI: All Access only, immediate ≥48dp close/back/free
    paths, no urgency/countdown/preselection/counteroffer, and no purchase affordance visually
    privileged over free dismissal. Color is never the sole carrier of state; text/symbol shape
    twins remain visible in every state and motion mode.
13. After the SDK-boundary remedy, RevenueCatUI keeps its automatic impression. The signed two-argument custom-impression shape
    also requires a `paywallId`, but no identifier is signed for the post-Level-5 fallback. Lane 5
    neither invents that identifier nor substitutes the available Offering-only overload. Custom
    tracking remains no-code until a direct human ruling plus reviewed contract amendment pins the
    exact fallback paywall ID or explicitly authorizes the Offering-only overload. After that
    ruling, the custom fallback reports exactly one impression only when actually visible and never
    for an empty offering; same-renderer duplicates remain forbidden. Cross-renderer impression
    semantics separately remain part of the unresolved A/B ruling. The visibility callback carries
    only the SDK-free opaque handle issued with the validated snapshot; the integration retains the
    original 9.7.0 `Offering`, never an identifier-reconstructed substitute.
14. Every future custom purchase row carries the SDK-free `PackageRef` issued from that same
    validated snapshot, but no real row/tap is reachable before the native Package remedy. The
    integration retains the exact original `Purchases.Package` only for lineage comparison; stock
    `PurchasePackage` cannot be claimed to purchase that object. After the reviewed remedy, a tap
    must validate/veto the exact original native Package and product before billing; stale/replayed/
    foreign/cross-flow refs, a package/product swap, refetch, identifier reconstruction, stock
    `PurchasePackage`, and every `PurchaseProduct` overload fail closed. Pending lineage uses that
    same ref.

## Architecture and exact SDK boundary

### SDK-free services

`unity/Assets/Scripts/Services/Purchases/**` owns `IPurchases` plus immutable SDK-free request,
result, catalog, placement, localized-price, entitlement-summary, and eligibility DTOs. It
contains interfaces and POCO DTOs only, with zero behavior, as required by ADR-0003. It must
reference neither RevenueCat nor UnityEngine and must not persist state itself.

No RevenueCat type may cross `IPurchases`. Purchase outcomes distinguish success,
user-cancelled, failure, restored, pending, and unknown/unsettled. In 9.7.0 a pending one-time
purchase arrives as `PurchaseResult.Error`; its exact test is
`Error.Code == 20 && Error.ReadableErrorCode == "PaymentPendingError"` because
`ReadableErrorCode` is a string field, not an enum. The adapter maps only that encoding to the
SDK-free Pending outcome without inventing an SDK pending enum. Pending never grants, opens fallback, or
enters the ordinary failure-copy path. It waits for later `CustomerInfo` reconciliation. No
fulfillment result alone grants a consumable; durable ledger confirmation remains the authority
after the required ADR amendment.

An offering snapshot may carry an SDK-free opaque `OfferingHandle` plus display DTOs, each buyable
row carrying its own opaque SDK-free `PackageRef`. Both handles are flow-scoped, non-persisted,
non-serializable, and convey no SDK object or offering identifier. A narrow Services callback/
interface accepts the offering handle when the custom renderer first becomes visible, while the
signed purchase seam accepts `PurchaseAsync(PackageRef, placementId)`. Only the integration may
resolve those handles to the exact original validated 9.7.0 `Offering` and its exact retained
`Purchases.Package`; unknown, expired, replayed, foreign-placement, or cross-flow handles fail
closed. At this freeze, successful C# handle resolution still returns SDK-free Unavailable and
invokes no billing API because the stock Android bridge would cache-first re-resolve a different
native Package;
only the reviewed boundary remedy may turn that seam into a real purchase.

### Application single source and Save authority

ADR-0003 and PRD CM-R29 require behavior and reward decisions in `CatMetro.Application`, with
all offer suppression routed through exactly one `OfferEligibilityService`. Neither Services,
Bootstrap, nor the RevenueCat adapter may become a shadow eligibility implementation.

The human grants new `unity/Assets/Scripts/Application/Purchases/**` plus Lane 5's own tests.
That directory may own the single `OfferEligibilityService` and non-persisting purchase
orchestration. It may not create a second consumable ledger or mutate `rewindBalance` directly.
After the signed persistence gate, every purchase-funded rewind grant must route through the
existing sole `Application/Save/ConsumableLedger.TryGrant(...)` path; any authorized durable
entitlement-cache mutation likewise stays within the exact later-signed Save surface. No
unrelated Application behavior is implied.

The conditional `Application/Save/**` grant is **void at this freeze**. Self-verifying audit at
the anchor:

| Required referent | Frozen evidence |
|---|---|
| ADR path | `docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md` |
| ADR status | `Proposed` |
| latest human commit touching the ADR | `4fbc57c44cbcbe584f0fb8e7e7be465b2bb022a8` |
| qualifying signed Lane 5 amendment | none |
| qualifying signing SHA | none |
| exact authorized `Application/Save/**` file list | empty set |

The cited commit ratifies earlier queue/backup matters only; it does not enumerate Lane 5 Save
source files. The ADR still leaves purchase lifecycle and durable session-cap shape open. A later
human-signed in-repo amendment does not dynamically expand this contract: before any Save edit,
an explicit reviewed contract amendment must cite the ADR path, its signing SHA, and its exact
source-file list verbatim.

### Canonical integration

`unity/Assets/Scripts/Integrations/RevenueCat/**` is the sole SDK-aware assembly. Its asmdef name
is exactly `CatMetro.Integrations.RevenueCat`. Its only internal Cat Metro reference is
`CatMetro.Services`; its external references are the exact pinned RevenueCat core/UI assemblies.
Among Cat Metro product assemblies only Bootstrap may reference it, with the already-authorized
EditMode test assembly as the sole test consumer. A renamed assembly, an Application/Domain/
Presentation reference, or another product consumer is forbidden. It owns:

- the six-product allowlist and five placement-to-offering map;
- initialization and callback-to-SDK-free result conversion;
- per-callback-slot serialization, coalescing, or deterministic Busy rejection so no 9.7.0
  callback property can be overwritten;
- source characterization and a hard gate for `post_level_5` RevenueCatUI; no stock presentation
  call before the reviewed native-Offering remedy;
- restore and purchase adapters needed by the approved interface, with fail-closed result
  mapping and no grant authority.

The implementation must follow the inspected 9.7.0 source, not the stale implementation
sketch. In particular:

- The global `Purchases` type is a `MonoBehaviour`; there is no `RevenueCat.Purchases` or
  `SharedInstance` API.
- Runtime owns exactly one persistent global `Purchases` component on a never-renamed
  `CatMetroRevenueCatPurchases` GameObject. Before its first `Start`, `useRuntimeSetup` is true and
  exactly one `Purchases.UpdatedCustomerInfoListener` instance is assigned, and public component
  field `proxyURL` is null. `Start` checks that field and can call `SetProxyURL` even before its
  runtime-setup early return, so production may not inherit or serialize a proxy endpoint. `Start` must install
  the native wrapper before the active instance receives its one synchronous-void
  `Configure(Purchases.PurchasesConfiguration)` call, which immediately delegates to wrapper
  `Setup`. The object is never duplicated, renamed, destroyed, or reconfigured. Runtime setup
  disables only the C# `Start` path's automatic `Configure(string)` and `GetProducts`; it does not
  disable native Offering fetch/cache. Android native callbacks address the stable GameObject name;
  tests may not replace this with a static/shared-instance API the source does not expose.
- No runtime configuration code is authored until a human-signed dependency/privacy ADR binds
  every `Purchases.PurchasesConfiguration.Builder` field in the source-default census below.
  Omitting a field is a privacy/product decision, not an implementation default. In particular,
  the Builder defaults automatic device-identifier collection to true, so the prior draft's
  blanket “no device-derived identifier” assertion is withdrawn pending that explicit human
  choice. The already-signed identity boundary still requires a null custom App User ID,
  SDK-generated anonymous identity, no `LogIn`/`LogOut`, no account/custom identity override, and
  zero subscriber-attribute writes. `$onesignalUserId` and all OneSignal/taxonomy wiring belong to
  a later contract; Lane 5 exposes no dark-factory seam.
- The signed target requires RevenueCat initialization even when paywall presentation is dark so
  CustomerInfo/restore can reconcile; the presentation flag may not gate initialization. Pinned
  native source simultaneously proves Configure/instantiation and foreground lifecycle can fetch,
  construct, cache, and predownload Offering assets without a Cat Metro placement call. This
  conflicts with the phases-1–4 and marker-before-fetch embargo. Consequently no production
  `Configure` or persistent-component wiring is authored until the direct human fairness ruling/
  native lifecycle remedy below. After that resolution, candidate wiring still requires the
  controlled-capture authorization, and production activation/merge still requires final human
  privacy/Data-Safety approval. CM-R53 reset/anonymous-ID rotation remains a separate human-
  decision conflict and no reset/rotation API is authored by this lane.
- Placement, offering, customer-info, restore, and purchase APIs are callback-based.
- In 9.7.0, `_getCurrentOfferingForPlacement` checks a missing/null `offering` before
  `ResponseHasError`; Android error-only JSON therefore collapses to `(null, null)`, the same
  observable shape as No Offering. That shape maps to NoOffering, no UI, no current/cached
  substitution, and no retry. Lane 5 does not claim placement-error telemetry or retries the SDK
  cannot observe. The signed three-retry requirement remains a hard source conflict: no placement
  retry code is authored until a direct human ruling and reviewed contract amendment either
  accept this source limitation as the governed fail-closed behavior or authorize an exact,
  independently reviewed SDK-boundary remedy.
- Independently, the placement path runs through cache-first `getOfferingsWith` and strips the
  internal memory/disk provenance before Unity. A valid-looking cached Offering is therefore
  observationally indistinguishable from a current response. No real placement callback is invoked
  before the human cache-semantics ruling or a reviewed force-current/provenance boundary; remote
  allowlist success alone cannot authorize commerce.
- `useRuntimeSetup`/empty C# products do not prevent native Offering traffic. Unity documents
  Offerings fetched/cached on Purchases instantiation. Android installs an Activity lifecycle
  observer; cold/foreground `OfferingsManager.onAppForeground` fetches when cache is null or stale
  (foreground stale after five minutes), constructs/caches Offerings, and predownloads current-
  paywall images/fonts. A resume can occur during gameplay phases 1–4. No Configure path is
  reachable until the human either narrows the embargo to explicitly accept SDK-internal prefetch/
  refresh while still forbidding every Cat-Metro-initiated request/surface, or authorizes a reviewed
  delayed/native-controlled lifecycle that prevents Offering traffic in the embargo while
  preserving the dark-presentation reconciliation requirement. Traffic/device characterization and
  a reviewed contract amendment are mandatory; `GetProducts=false` is not evidence.
- Purchase methods collectively share one mutable purchase callback; restore, customer-info,
  sync-purchases, ordinary offerings, and placement offerings each have their own mutable callback
  property.
  Cat Metro's adapter does not call ordinary `GetOfferings`; the placement API is its only offering
  path. Stock Android RevenueCatUI does internally call `awaitOfferings()` and current-fallback,
  which is why that presenter remains forbidden rather than an exception to this law.
  Every exposed operation therefore gets an explicit same-slot concurrency rule: purchases
  and modal restore/paywall calls reject overlap as Busy; read operations serialize or
  coalesce deterministically; placement requests serialize in FIFO order. No second call may
  overwrite a live SDK callback. SDK callbacks are not cancellable. The signed eight-second
  timeout applies only to Offering `Fetching`, never purchase, restore, CustomerInfo, or
  reconciliation. A timed-out fetch may complete its detached caller's SDK-free result but never
  clears or replaces the live placement slot; that slot remains quarantined/Busy until the actual
  late callback is marshaled and drained without updating that timed-out caller or a later request.
  A fetch retry may begin only after an actual SDK failure callback frees the slot; timeout alone
  never starts the 1/4/10 schedule. Purchase/restore/reconciliation have no invented eight-second
  completion or late-discard rule: even if their UI caller detaches, the definitive callback still
  enters main-thread reconciliation and cannot be discarded. At most one commercial modal flow is
  active at a time.
- Before the native Package remedy, a custom `PackageRef` may resolve only for SDK-free lineage/
  display validation and then returns Unavailable; no billing call follows. Stock
  `PurchasePackage` is not object-preserving on Android: it sends identifier/context, hybrid-common
  calls ordinary `getOfferingsWith`, chooses a case-insensitive package-ID match from the refreshed
  native Offering, and bills that new Package. `PurchaseProduct` is also forbidden because it loses
  package/presented context and defaults to `subs` only when explicit type is omitted; selecting
  explicit `inapp` as an alternative still requires a direct human/spec amendment. Any later approved boundary must
  validate and permit/veto the exact retained native Package and product before billing without
  identifier reconstruction, refetch, swap, or defaulted type. Stock RevenueCatUI's
  `OnPurchaseStarted` is likewise observational and arrives after Android has unconditionally
  resumed purchase initiation; it cannot serve as the safety boundary.
- Bootstrap captures the Unity main-thread identity and injects one
  `RunInlineIfMainThreadElsePost` dispatcher. Core callbacks arriving off-thread are posted before
  completing a result or touching cache, event, or state. A callback already on the Unity main
  thread runs inline exactly once. RevenueCatUI listener latches must run inline on that thread;
  enqueuing them a second time can let the terminal `Task` beat its guaranteed listener-before-
  result ordering. Every dev-build `IPurchases` emission asserts main-thread execution.
- The stock 9.7.0 C# presentation shape is
  `RevenueCatUI.PaywallsPresenter.Present(new RevenueCatUI.PaywallOptions(originalOffering,
  listener: listener))`. It returns `Task<PaywallResult>`; characterization code switches on the public
  `result.Result` enum values `NotPresented`, `Cancelled`, `Error`, `Purchased`, and `Restored`.
  The static result helpers are internal and are not called. This API does **not** preserve the
  validated Offering on Android and is not called by production orchestration at this freeze; the
  exact source conflict below is a prior hard stop. After an approved remedy supplies a safe
  presentation boundary, its listener implements all nine
  events: `OnPurchaseStarted(Package)`, `OnPurchaseCompleted(CustomerInfo, StoreTransaction)`,
  `OnPurchaseError(Error)`, `OnPurchaseCancelled()`, `OnRestoreStarted()`,
  `OnRestoreCompleted(CustomerInfo)`, `OnRestoreError(Error)`, `OnWebCheckoutOpened()`, and
  `OnUrlOpened(string)`. `OnPurchaseStarted` preserves the requested package/SKU in SDK-free flow
  state; `OnPurchaseCompleted` is only a validated success candidate when its CustomerInfo and
  StoreTransaction match that lineage and a rewind transaction satisfies the canonical nonblank-
  identity rule below; `OnPurchaseError` maps Pending/failure as defined below;
  `OnPurchaseCancelled` latches Cancelled; `OnRestoreStarted` latches restore-in-flight;
  `OnRestoreCompleted` requests idempotent CustomerInfo reconciliation; and `OnRestoreError`
  latches restore failure. The web/URL callbacks are non-grant events. Terminal Purchased/Restored
  results carry no CustomerInfo or transaction and never grant by themselves.
  `OnPurchaseError` and `OnRestoreError` latch before the terminal Task result. An
  `OnPurchaseError` whose exact fields are code 20 and readable string `PaymentPendingError` maps
  the preserved request to Pending; it grants nothing, shows no ordinary failure copy, invokes no
  fallback, and completes only through later CustomerInfo reconciliation. Because the public
  bridge strips transaction/phase provenance from other purchase errors, every post-initiation
  non-cancel/non-pending purchase Error remains Unknown/unsettled, retains reconciliation state,
  never uses “you were not charged,” never retries, and never invokes fallback. Restore Error also
  grants nothing and never invokes fallback. Every public `result.Result` enum value maps without
  granting content. An unlatched Error maps to an unresolved SDK-free outcome; no
  production orchestrator consumes it until the A/B ruling amends this contract.
- Web checkout is disabled in the governed RevenueCat Paywalls-v2 configuration. If the SDK
  nevertheless emits `OnWebCheckoutOpened` or `OnUrlOpened`, each is a non-grant event only: it
  is never treated as purchase completion, entitlement authority, fallback eligibility, or a
  reason to open another commerce surface. Later `CustomerInfo` reconciliation remains the only
  possible entitlement path.
- No custom-paywall impression call is authored before the fallback-ID/overload ruling. The
  signed specification names
  `TrackCustomPaywallImpression(new Purchases.CustomPaywallImpressionParams(paywallId,
  actualOffering))`, while 9.7.0 also exposes an Offering-only overload; availability is not
  authority to choose it. Once amended, the integration uses the active `Purchases` instance and
  retains the original validated `Purchases.Offering` behind the flow-scoped SDK-free handle so
  `PresentedOfferingContext` is preserved. Presentation never stores or reconstructs an SDK
  offering or paywall identifier. RevenueCatUI automatic impressions are never re-reported, and
  cross-renderer behavior remains independently stopped by the A/B ruling.
- Direct `PurchaseResult` mapping is ordered and fail-closed: `UserCancelled` first maps to
  Cancelled; exact `Error.Code == 20 && Error.ReadableErrorCode == "PaymentPendingError"` then maps
  to Pending; any other non-null Error after purchase invocation maps to Unknown/unsettled and
  preserves reconciliation state because pinned native code may already hold a StoreTransaction
  before backend receipt posting fails while the public bridge returns only Error. It never proves
  no charge, never uses CM-R32's ordinary no-charge copy, and never permits an automatic retry.
  Only an independently reviewed native provenance boundary plus direct human copy/state ruling
  may classify a proven pre-billing error as Failure. Only an expected retained `PackageRef`
  with non-null CustomerInfo, non-null expected StoreTransaction, and matching allowlisted
  `StoreTransaction.ProductIdentifier` is a success candidate. For a rewind SKU, the exact
  `StoreTransaction.TransactionIdentifier` must additionally satisfy
  `!string.IsNullOrWhiteSpace(...)`; null, empty, or whitespace remains Unknown/unsettled with
  reconciliation and zero grant. The identifier is not trimmed, normalized, synthesized, or
  treated as interchangeable with a different callback path's identifier. Every contradictory or
  incomplete shape maps to Unknown/unsettled. Pending/error lineage uses the retained PackageRef
  because ProductIdentifier is available only from StoreTransaction. Entitlement-backed
  non-consumables reconcile through validated CustomerInfo rather than the consumable ledger.
- Exactly one `UpdatedCustomerInfoListener` subclass handles
  `CustomerInfoReceived(CustomerInfo)` and converts it on the main thread into a quarantinable SDK-
  free reconciliation candidate. Before the signed ADR-0006/Save amendment it mutates no durable
  entitlement, ledger, or balance.
  For non-consumables, an `Entitlements.Active` row is only a candidate when the dictionary key
  equals `EntitlementInfo.Identifier`, `IsActive` is true, the entitlement identifier is one of the
  four signed identifiers, and `ProductIdentifier` belongs to that entitlement's exact inverse
  source set: `all_access` ← `cm_all_access|cm_supporter_pack`; `supporter` ←
  `cm_supporter_pack`; `theme_sakura` ← `cm_all_access|cm_supporter_pack|cm_theme_sakura`;
  `theme_neon` ← `cm_all_access|cm_supporter_pack|cm_theme_neon`. Row/global Verification,
  Store/channel, IsSandbox, expiration, and renewal facts must additionally satisfy a field-by-
  field policy signed into ADR-0006; this contract invents none. Test Store/sandbox rows remain
  debug evidence, never release authority. A foreign, experiment, future, cross-attached,
  mismatched, subscription-like, wrong-channel, or failed-verification row grants nothing and is
  quarantined with scrubbed diagnostics only.
  `AllPurchasedProductIdentifiers` is payer/suppression evidence only and never consumable
  fulfillment because it has no transaction multiplicity. `CustomerInfo.NonSubscriptionTransactions`
  is complete historical purchase data, not an authorized fulfillment feed. Native Android
  history exposes a RevenueCat/backend transaction identifier plus product/date and some store,
  sandbox, original-date, price, and nullable store-transaction facts, but it exposes no refund/
  revocation or current-versus-history marker. The Unity history mapper further drops all but the
  backend identifier/product/date. Direct purchase instead maps the nullable Play `orderId` as its
  transaction identifier. A mapper-only expansion therefore cannot prove refund/currentness or
  canonical identity, and the backend history ID and Play order ID are never treated as equal or
  interchangeable. At this freeze every CustomerInfo history row grants zero consumables, and a
  fresh install never regrants historical rewinds even if Block Store recovers the anonymous ID.
  Before any post-ADR history-assisted recovery, a reviewed contract/ADR must sign either a safe
  local baseline/cursor or explicit no-regrant policy. Any backend/external authority is outside
  Lane 5 and the no-server 1.0 architecture unless a separate human product/scope grant,
  architecture ADR, owned-surface allocation, and fresh threat-model/privacy/security review land;
  any lower-level SDK remedy likewise needs amended dependency/privacy authority and independent
  security review. A permitted recovery authority must supply refund/current-history truth and one
  canonical nonblank store transaction identity proven equal across direct and recovery delivery;
  merely exposing more fields from the native public `Transaction` is insufficient. Only a
  transaction proven locally attributable under that signed policy may pass through the existing
  sole atomic
  `ConsumableLedger.TryGrant(transactionId, productId, signedQuantity, signedGrantedAtUtc)` boundary;
  whole-history enumeration, lost/pruned-ledger replay, and restore-as-consumable-fulfillment remain
  forbidden. Direct result, listener, AutoSync/UpdatedCustomerInfo, Pending-next-session, and
  process-death paths may converge only through that same signed authority and canonical identity;
  an unequal direct Play order ID and RevenueCat/backend history ID must never deduplicate by
  assumption. Quantity/time and
  retention/refund semantics come from the signed ADR/config, not this adapter. Raw `OriginalJson`,
  Signature, transaction ID, and SDK DTOs never enter diagnostics or durable state outside the
  ledger's existing transaction-ID hashing boundary.
- `SyncPurchases` owns a separate mutable callback slot, but neither overload is authorized for
  this RevenueCat-completed purchase architecture. Pinned Unity/Android source reserves explicit
  sync for migration or `PurchasesAreCompletedBy.MyApp` and warns not to combine it with SDK
  purchase methods; serialization cannot make that global usage supported. The current target fixes
  `DangerousSettings.AutoSyncPurchases=true` and uses AutoSyncPurchases plus the sole
  UpdatedCustomerInfo listener, with device recovery proof. Both explicit and callbackless
  `SyncPurchases` calls remain absent. Any explicit-sync alternative requires a direct human/source
  override, a different mutually exclusive purchase-completion architecture, and a reviewed
  contract/ADR amendment; the callbackless overload is never an invisible substitute.
- Player price text comes only from `Purchases.StoreProduct.PriceString`; runtime display code
  never formats or converts the floating `Price` value. This lane contains no ad-revenue SDK seam.
  For source exactness only, a later separately authorized contract must treat
  `RevenueCat.AdRevenueData.RevenueMicros` as `long`, never a floating-point signature.

### Runtime configuration and privacy census

The inspected 9.7.0 Builder has the following runtime defaults. They are source facts, not
approved Cat Metro values:

| Builder field | 9.7.0 source default before an explicit selection |
|---|---|
| required positional `ApiKey` | no managed null/empty/prefix validation; native rejects blank late and otherwise may only log a wrong-store prefix |
| `AppUserId` | `null` |
| `PurchasesAreCompletedBy` | RevenueCat |
| `UseAmazon` | `false` |
| `DangerousSettings.AutoSyncPurchases` | `true` |
| `StoreKitVersion` | enum-zero `StoreKit1` (despite the inspector's `Default` label) |
| `ShouldShowInAppMessagesAutomatically` | `false` |
| `EntitlementVerificationMode` | `Disabled` |
| `PendingTransactionsForPrepaidPlansEnabled` | `false` |
| `DiagnosticsEnabled` | `false` |
| `AutomaticDeviceIdentifierCollectionEnabled` | `true` |
| `PreferredUILocaleOverride` | `null` |
| `UserDefaultsSuiteName` | `null` |

The human-signed dependency/privacy ADR must record the exact chosen value for every row and
explicitly decide automatic device-identifier collection, entitlement verification, diagnostics,
automatic in-app messages, automatic purchase sync, and the remaining platform fields before any
`PurchasesConfiguration` Builder or `Configure` call enters production code. The one-time purchase
Pending mapping is independent of the prepaid-subscription flag and must not be disabled or
inferred from it. Selecting `EntitlementVerificationMode` also requires the signed ADR to decide
which global and per-`EntitlementInfo` `VerificationResult` values—`NotRequested`, `Verified`,
`Failed`, `VerifiedOnDevice`—may update durable entitlement or consumable authority and the exact
fail-closed/offline behavior; Informational mode can still place Failed rows in Active. The same
decision binds release Store/channel, IsSandbox, expiration, and renewal invariants. Under the
current RevenueCat-completed architecture, AutoSyncPurchases is fixed true and recovery uses its
UpdatedCustomerInfo delivery; explicit SyncPurchases is absent. Any new 9.7.0 configuration field
found during implementation reopens the ADR census and stops configuration work.
The component field `Purchases.proxyURL` is outside the Builder but is frozen to null and receives
its own prefab/scene/runtime mutation proof; controlled privacy capture uses the designated device/
network proxy and never serializes an SDK proxy URL into the player.

Two additional source surfaces are binding. Public readonly `ObserverMode` is inert/default false:
the Builder never assigns it and `Purchases.Configure` never passes it. Actual observer mode is
selected only by `PurchasesAreCompletedBy.MyApp`; Lane 5 selects RevenueCat completion and tests
that no code reads or treats the inert field as authority. `ShouldShowInAppMessagesAutomatically`
is fixed to false under the current phase/system-surface laws, and no explicit
`ShowInAppMessages` call exists; choosing true requires a direct human fairness supersession because
native code may show a store message on every Activity start.

Native logging is also a configuration/privacy surface outside the Builder. The published Android
AAR starts at INFO; C# exposes `SetLogLevel`/`SetLogHandler` but no Silent enum. INFO restore-history
messages can include order ID and purchase token, and native `errorLog` bypasses level filtering.
Before Configure, after Start has installed the wrapper, the dependency/privacy ADR must bind the
exact log level and handler/order or explicitly accept the default; a custom handler, if selected,
must be scrubbed. This cannot suppress the unconditional C# raw-JSON logs, so the release-logcat
hard stop remains independently binding.

Changing SDKs also reopens the human-owned privacy and Play Data Safety review under PRD CM-R45 and
the signed monetization privacy section. This is a two-stage gate. First, after the signed
dependency/privacy ADR and before candidate GameRoot wiring or any proxy run, a human explicitly
authorizes one controlled, non-published branch-candidate capture on designated test devices and
accounts. That authorization permits the exact candidate wiring/build solely to produce evidence;
it does not activate production, mutate a console, approve the result, or arm merge. Second, after
the capture, the human reviewer must approve the exact happy-path **and forced-crash** production-
proxy traces, resolved dependency and merged-manifest inventory, data categories and purposes,
retention/deletion behavior, privacy notice, age/consent posture, and Play Data Safety answers
before production activation or Lane 5 merge. The capture covers cold initialization with
presentation dark, offering fetch, purchase, restore, Pending, CustomerInfo reconciliation, and a
forced crash at the signed commerce boundary. A stale disclosure, missing crash leg, or review
limited to visible commerce UI is not evidence because SDK initialization itself may send data.

### Bootstrap wiring

`GameRoot.cs` and `CatMetro.Bootstrap.asmdef` are a human-granted funnel exception. They are edited
last. The declared funnel is now 3 → 6 → 8 → 5 → 11; Lane 5 waits for its Lane-8 predecessor and
rebases over any Lane-11 tail wiring that has already landed under Addendum v2.2's second-lander
rule. Bootstrap composes the SDK-free service and
canonical adapter, invokes the Application eligibility service, performs the post-Level-5
trigger at the specified navigation boundary, and never introduces a direct eligibility or
offer bypass.

The existing EditMode test asmdef may add the canonical integration assembly reference. No
third test assembly is created.

### Build configuration injection

The granted `RevenueCatBuildConfigInjector.cs` is an automatic Unity pre-build integration, not
a manual helper. Without editing `CatMetroCliAabBuild.cs`, it must run on the real
`CatMetroCliAabBuild.BuildAndroidAab` → `BuildPipeline.BuildPlayer` path before player compilation
or artifact production proceeds. Both prebuild and runtime-before-Configure boundaries trim the
unlogged `CM_RC_PUBLIC_KEY` once, validate it, and forward that same trimmed value: null/empty/
whitespace, exact `goog_`/`test_`, and a prefix followed only by whitespace always fail. Release
and non-Test-Store Google Play builds require `goog_` plus at least one non-whitespace suffix
character; Test Store is debug/development-only, requires `CM_TEST_STORE`, and requires `test_`
plus at least one non-whitespace suffix character. No stronger undocumented key grammar is
invented. A flag/prefix mismatch fails. Release `test_`, wrong-store
`amzn_`/`galx_`, and every unknown prefix fail before BuildPlayer/Configure rather than relying on
native behavior that may only log and continue or show a simulated-store error activity/crash.
Debug/Test Store selection is compile-time only; there is no runtime switch.

The injector never logs the key and never leaves it in a committed or persistent scene, prefab,
resource, source file, settings asset, or generated artifact outside the built player. Any
temporary define, generated configuration, or editor setting is restored after both success and
failure. No code stringifies `PurchasesConfiguration`, whose `ToString` includes the key. End-to-end
evidence exercises the existing CLI AAB entry point, proves blank/whitespace, bare `goog_`/`test_`,
prefix-plus-whitespace, every wrong prefix, release-`test_`, Test-Store-`goog_`, and flag mismatch
reject before the build proceeds, and proves
`unity/Assets/Editor/CatMetroCliAabBuild.cs` remains byte-untouched.

## Dependency and package pins

The dependency ADR must be human-signed before any dependency implementation enters this
branch and must be referenced in the PR. Before that signature, package/source investigation
is read-only or scratch-only: no `config/pins.json`, manifest, lock, vendored SDK, Android
plugin, EDM4U payload, or resolved artifact may enter the branch diff. The intended
machine-readable pins after signature are:

| Component | Exact pin | Verified source fact |
|---|---:|---|
| `com.revenuecat.purchases-unity` | `9.7.0` | upstream tag peels to `0a280fab0a533bdd042d2f002440b93d1716f392` |
| `com.revenuecat.purchases-ui-unity` | `9.7.0` | same inspected upstream tag |
| `com.google.external-dependency-manager` | `1.2.188` | lightweight tag/commit `32c34224fd3f34c813c97c69508d2a3930105dc7`; one EDM4U pin only |

An isolated Unity 6000.3.16f1 scratch project resolved the exact three roots through one and only
one scoped registry named `OpenUPM`, URL `https://package.openupm.com`, whose scope set is exactly
`com.google.external-dependency-manager`, `com.revenuecat.purchases-unity`, and
`com.revenuecat.purchases-ui-unity`. All three lock nodes are depth zero/source registry. The two
runtime assembly references are exactly `revenuecat.purchases-unity` and
`revenuecat.purchases-unity-ui`. The dependency ADR must approve this UPM-only topology before it
is implemented; no alternate registry, git URL, embedded package, Unity Asset Store import, or
vendored `Assets/RevenueCat`/EDM copy may be substituted.

The exact observed OpenUPM artifacts are:

| Root | Tarball | SHA-1 | SHA-512 |
|---|---|---|---|
| core 9.7.0 | `https://package.openupm.com/com.revenuecat.purchases-unity/-/com.revenuecat.purchases-unity-9.7.0.tgz` | `2a26ccea27af84876c441bd290196841443160bb` | `sha512-a+wN+hk1qyhx+WAuZ9p9wjvH1+9TVg1dz8QYG8qLDh2bebW4e6+QJtLfpIHX8bn72eoakLpRS12LouvB5HokCA==` |
| UI 9.7.0 | `https://package.openupm.com/com.revenuecat.purchases-ui-unity/-/com.revenuecat.purchases-ui-unity-9.7.0.tgz` | `c898704093ad59366d169323e7a858f5b18e21a0` | `sha512-okAUPFC7jqrHt8eXZHgcnfobmOqwSZ1pVvWB5XpERUgPZQJ8smsN7inMlriFNu2EIlK2fVHOc/2Y94hQ6NUPog==` |
| EDM4U 1.2.188 | `https://package.openupm.com/com.google.external-dependency-manager/-/com.google.external-dependency-manager-1.2.188.tgz` | `99b9e5db7e033364f9438330bbeff299e8d09f77` | `sha512-37WxzUCpsGhR30owmBH4UC7VjyHfndiQIEFiDtIgEptFGhFNJLN326PAUJqnt/SunKrdRhxBkpK3cF4ljZXEgA==` |

All three artifacts are unsigned and `packages-lock.json` carries no integrity field. The signed
ADR must explicitly accept that posture and the exact artifact lineage above. Once accepted,
`config/pins.json` binds version, tarball, SHA-1, and SHA-512 for all three roots, and validation
normalizes each package payload and compares it with the inspected upstream tag/commit, including
the exact RevenueCat Unity commit `0a280fab0a533bdd042d2f002440b93d1716f392`, purchases-android
10.16.0 annotated-tag peel `517e2bd5b2fe13957fb74eda9033ef368d08099a`, hybrid-common
18.29.0 peel `5001785fce0bfdfbcc260c8422f60f0a26a81f47`, and EDM4U commit
`32c34224fd3f34c813c97c69508d2a3930105dc7`. Scratch resolution alone does not authorize a
branch diff.

The UI package nevertheless declares its core dependency as the literal vendor-relative
`"file:../RevenueCat"`. The parity gate may recognize that exact quirk only within the UI lock
node while independently requiring the root core 9.7.0 depth-zero registry node; it may not
generalize acceptance of file dependencies. Under the approved UPM-only route,
`unity/Assets/ExternalDependencyManager/**` must remain absent; any file there is a duplicate
payload and a failing topology control, not a second granted installation route.

Pinned core source at commit `0a280fab0a533bdd042d2f002440b93d1716f392` in
`RevenueCat/Scripts/Purchases.cs` also contains unconditional Unity `Debug.Log` calls for raw
callback JSON in customer-info, purchase, restore, sync, and offering paths. Those payloads bypass Cat Metro's
`IDiagnostics` scrubber and may contain the anonymous App User ID, transaction/purchase fields,
tokens, prices, or raw error data. The dependency ADR and independent billing/security review
must treat this as an explicit data-exposure risk. Release-AAB device logcat evidence must exercise
purchase, restore, and CustomerInfo reconciliation and prove no raw RevenueCat JSON, identifier,
transaction/purchase token, or price escapes. If it does, the lane stops for a human-approved
mitigation or vendor patch; a fork/patched package changes dependency and integrity scope and
requires its own signed ADR plus reviewed contract amendment.
Cat Metro source additionally never logs or interpolates a `PurchasesConfiguration`,
`PurchaseResult`, `CustomerInfo`, `StoreTransaction`, `Offering`, `Package`, `StoreProduct`, or raw
RevenueCat `Error`: configuration stringification includes the API key, while transaction
stringification includes raw JSON/signature material. A scrubbed summary DTO is the only allowed
diagnostic boundary.

After that signature, the scoped-registry route is used only if Unity reproduces both RevenueCat
packages at those exact versions and both exact assembly references compile.
`config/pins.json`, `unity/Packages/manifest.json`, and `unity/Packages/packages-lock.json` must
agree. No `Assets/RevenueCat` or `Assets/ExternalDependencyManager` copy is imported and no
duplicate SDK is vendored.

The base package surface is already stale outside Lane 5: the manifest requests URP 17.5.0,
Test Framework 1.7.0, and UGUI 2.5.0 while the lock currently records 17.3.0, 1.6.0, and 2.0.0.
RevenueCat/EDM resolution must not launder those unrelated upgrades into this one-task diff. An
authorized predecessor must first reconcile and validate the package baseline, or a Lane 5
scratch resolve must prove every non-RevenueCat/non-EDM manifest entry and lock node remains
byte- and semantically identical to the reviewed base. If Unity cannot resolve the three new
roots without unrelated package churn, Lane 5 stops; “explained transitive drift” is not an
exception.

The dependency/privacy ADR and independent security review must census and explicitly accept the
full resolved Android graph, not only the four EDM declarations. The source-proven lineage and
notable transitive set below is a mandatory minimum, not an exhaustive substitute for the machine-
resolved graph and per-AAR manifest fixture:

- Unity 9.7.0 → hybrid core/UI 18.29.0 → purchases-android/UI 10.16.0 → Google Play Billing
  8.3.0; exactly one BillingClient lineage is allowed;
- hybrid core requests purchases-android 10.16.0, purchases-store-amazon 10.16.0, Kotlin 2.0.21,
  and coroutines 1.6.4; the reproduced `releaseRuntimeClasspath` instead selected coroutines core+
  Android 1.7.3 by conflict resolution, and the exported Unity player graph must prove that exact
  selected version. Amazon SDK 3.0.5 remains in the graph even when `UseAmazon=false`;
- hybrid UI brings purchases-ui 10.16.0, hybrid core, and fragment-ktx 1.6.2; purchases-ui also
  requests 1.6.1, so the resolved graph must prove the selected 1.6.2 node;
- core transitively brings Blockstore 16.4.0, Tink 1.8.0, Ads Identifier 17.0.1, lifecycle/core/
  serialization, Kotlin, and coroutines. The reproduced hybrid UI runtime graph selects lifecycle
  2.8.3 over requested 2.5.0, core/core-ktx 1.13.1, profileinstaller 1.3.1, startup-runtime 1.1.1,
  and play-services-places-placereport 17.0.0; the exported Unity graph must prove those exact
  selections or stop for a reviewed amendment;
- Billing 8.3.0 directly brings activity 1.2.3, transport-api 3.0.0,
  transport-backend-cct/runtime 3.1.8, play-services-base 18.5.0, basement 18.9.0, location 19.0.0,
  and tasks 18.2.0; resolution must select `androidx.activity:activity` 1.9.3 over the Billing
  1.2.3 and EDM 1.8.2 requests, while `activity-compose` remains 1.9.3, parallel to the
  fragment-ktx collision proof;
- UI transitively brings Compose BOM 2024.09.00, activity-compose 1.9.3, WebKit 1.12.1,
  browser 1.8.0, Coil 2.4.0, and CommonMark 0.21.0;
- EDM emits the direct declarations `purchases-hybrid-common:[18.29.0]`,
  `purchases-hybrid-common-ui:[18.29.0]`, `androidx.annotation:annotation:[1.2.0]`, and
  `androidx.activity:activity:1.8.2`.

The signed ADR must make an explicit privacy/security decision for the otherwise surprising
Amazon, Ads Identifier, Blockstore, Tink, Compose, WebKit, browser, Places place-reporting,
automatic startup/lifecycle, and profile-installer payloads. Generic SCA output
or `UseAmazon=false` is not acceptance. Unity IAP remains absent.

Blockstore is active behavior, not only a dependency. Native code always constructs its client,
stores purchased anonymous RevenueCat App User IDs under
`com.revenuecat.purchases.app_user_id` with Google cloud backup enabled, retrieves/aliases a prior
ID before restore, and aliases/stores before purchase completion. Unity exposes no switch;
`UseAmazon=false`, automatic-device-ID false, and Android `allowBackup=false` do not disable this
Google Block Store path. Before Configure/purchase, the human privacy/Data-Safety/reset decision
must explicitly accept that cloud persistence/recovery and its CM-R53 orphaning effect or authorize
a reviewed native/dependency remedy. Controlled proxy/device evidence must exercise the flow.

Merged-manifest evidence must match the source census: RevenueCat core declares minSdk 23,
`INTERNET`, `ACCESS_NETWORK_STATE`, and Amazon proxy/simulated-error activities;
purchases-store-amazon contributes exported `com.amazon.device.iap.ResponseReceiver`, protected by
`com.amazon.inapp.purchasing.Permission.NOTIFY`, with the same action. Hybrid UI/purchases-ui
require minSdk 24 and add PaywallActivity, CustomerCenterActivity, and a Unity trampoline with
`exported=false`. Billing declares minSdk 23/target 34, `com.android.vending.BILLING`, Play billing/
test-companion queries, billing-version metadata, and exported-false ProxyBillingActivity/V2.
Transport runtime contributes an exported-false BIND_JOB_SERVICE JobInfoSchedulerService, alarm
receiver, and backend discovery; CCT adds INTERNET/NETWORK plus backend metadata; play-services-base
adds exported-false GoogleApiActivity, while play-services-basement adds
`com.google.android.gms.version` metadata. Lifecycle-process 2.8.3 and startup-runtime 1.1.1 merge
`androidx.startup.InitializationProvider` with `ProcessLifecycleInitializer`; profileinstaller
1.3.1 adds initializer metadata and an exported `ProfileInstallReceiver` protected by
`android.permission.DUMP`, with INSTALL_PROFILE, SKIP_FILE, SAVE_PROFILE, and BENCHMARK_OPERATION
actions. These entries are a named minimum: the generated fixture must enumerate and compare every
component, permission, query, provider/receiver/service/activity, metadata entry, export flag, and
protection permission from every resolved AAR manifest, failing on unreviewed additions or
omissions. The Cat Metro base currently sets Android minSdk 25, so the known requirements are
compatible; any raised resolved minimum is a stop.

R8 evidence must account for core keep rules covering `com.revenuecat.**`, serialization, and
`org.json`; the two UI `dontwarn` rules; Amazon's `com.amazon.**` keep/dontwarn plus annotation
rules; Billing's AIDL/test-companion, ProxyBillingActivity-name, proto-field, and `dontwarn` rules;
DataTransport CCT's AutoValue `dontwarn` rules; and every other packaged consumer rule in the
resolved graph. The sampled direct/transitive AARs contain no native `.so`, but that observation
does not prove the complete AAB is 16-KB-page compatible: the full resolved graph and release AAB
must pass the 16-KB native-library/alignment gate.

License review must cover the complete transitive graph. RevenueCat core and its ordinary Maven
artifacts are MIT and EDM4U is Apache-2.0, but the Amazon SDK 3.0.5 JAR is governed by the Amazon
Program Materials License. The OpenUPM UI tarball has no embedded license and no SPDX license field
even though its metadata links MIT; both discrepancies and every transitive license require
explicit disposition. Resolution, complete license/SCA output, merged-manifest/R8 inspection, and
an Android release build are hard gates; drift or an unreviewed license stops the lane.

Scratch proof also establishes the reproducible resolver entry point and its mutation surface:
`GooglePlayServices.PlayServicesResolver.ResolveSync(true)` returned true and wrote the same four
exact declarations to `Assets/Plugins/Android/mainTemplate.gradle`, plus generated/updated
`settingsTemplate.gradle`, `gradleTemplate.properties`, and
`ProjectSettings/AndroidResolverDependencies.xml`. The scratch workspace also contained
`LauncherManifest.xml` and `ProjectSettings/GvhProjectSettings.xml`, but their mtimes came from a
prior async/import path and do not prove that the synchronous call wrote them. They remain part of
the resolver mutation census without false attribution. The menu method `MenuForceResolve` is
void/asynchronous; `-executeMethod ...MenuForceResolve -quit` can exit before resolution and is
not acceptable evidence. CI must invoke a synchronous wrapper/test, assert the true result, and
inspect the exact outputs.

That explicit call is not automatically the first resolver mutation. EDM4U 1.2.188 defaults
automatic resolution and resolve-on-build enabled, schedules resolution after asset changes, and
in batch mode can resolve directly during import. `EditorMeasurement` defaults analytics enabled
with consent false and includes Unity version/platform in reports. Its current Universal Analytics
POST implementation is commented out, so this contract does not claim current outbound EDM
telemetry; the resolver still calls the reporting/prompt path and can persist settings. Before any
branch package import or resolver run, the signed dependency/privacy ADR and deterministic wrapper
must bind `UseProjectSettings`, measurement disabled, `AutoResolverEnabled=false`, and
`AutoResolveOnBuild=false`; any alternative requires direct human approval, exact mutation-surface
ownership, and equivalent evidence. A clean noninteractive import must prove zero resolve/settings
mutation before the one explicit synchronous wrapper. No implicit asset-import or build-triggered
resolution is evidence.

The generated `ProjectSettings/AndroidResolverDependencies.xml` and
`ProjectSettings/GvhProjectSettings.xml` (whose scratch configuration had resolver analytics
disabled) are outside this frozen lane's current grant and outside the narrow transferred
`ProjectSettings.asset` category discussed below, which remains wholly unowned until exact fields
and values are granted. Neither XML may enter the diff unless the human-signed
dependency ADR names it as a committed pin/settings surface, Lane 1A's applicable ProjectSettings
ownership has transferred, and a direct human scope grant plus reviewed contract amendment adds
that exact path. Until then, Force Resolve stays scratch-only and both generated copies are
excluded from the lane diff.

## Owned surfaces and declared exceptions

### Primary owned surfaces

- `unity/Assets/Scripts/Integrations/RevenueCat/**` (new canonical assembly)
- `unity/Assets/Resources/Monetization/**` (new; identifiers/configuration plus the post-Level-5
  fallback hero only; no price copy or parallel string table)
- `unity/Assets/Scripts/Services/Purchases/**` (new SDK-free interfaces and POCO DTOs only)
- `unity/Assets/Scripts/Application/Purchases/**` (new Application behavior; no Save edit)
- `unity/Assets/Scripts/Presentation/Commerce/**` (new directory; post-Level-5 fallback only)
- `unity/Assets/Tests/EditMode/Pure/Purchases/**` (new SDK-free Services/Application policy
  tests, linked by the existing dotnet glob)
- `unity/Assets/Tests/EditMode/Engine/Integrations/RevenueCat/**` (new Unity/SDK adapter tests,
  excluded from the dotnet glob)
- `unity/Assets/Tests/PlayMode/Commerce/**` (new directory; fallback tests only)
- `tests/monetization/**` (new executable static/config wrappers owned by this lane)
- pinned `unity/Assets/Plugins/Android/**`
- `unity/Packages/**`
- `config/pins.json`
- `unity/Assets/Editor/RevenueCatBuildConfigInjector.cs` + `.meta`

The exhaustive Unity companion scope additionally includes these otherwise sibling new-folder
metadata files and no others:

- `unity/Assets/Scripts/Integrations.meta`
- `unity/Assets/Scripts/Integrations/RevenueCat.meta`
- `unity/Assets/Resources/Monetization.meta`
- `unity/Assets/Scripts/Services/Purchases.meta`
- `unity/Assets/Scripts/Application/Purchases.meta`
- `unity/Assets/Scripts/Presentation/Commerce.meta`
- `unity/Assets/Tests/EditMode/Pure/Purchases.meta`
- `unity/Assets/Tests/EditMode/Engine/Integrations.meta`
- `unity/Assets/Tests/EditMode/Engine/Integrations/RevenueCat.meta`
- `unity/Assets/Tests/PlayMode/Commerce.meta`

Inside the canonical tree, the assembly definition file is exactly
`CatMetro.Integrations.RevenueCat.asmdef` and declares the exact assembly identity already frozen
above. Its own file `.meta` is covered by the canonical-tree glob.

The Addendum v2 shorthand `EditMode/Integrations/**` is realized inside ADR-0005's already
binding two-folder split under the human's later “Lane 5's own tests” grant: engine-free policy
tests live under `Pure/Purchases/**`; Unity/SDK tests live under
`Engine/Integrations/RevenueCat/**`. No third EditMode source root or test assembly is created.

### Declared narrow exceptions

1. `unity/Assets/Scripts/Bootstrap/GameRoot.cs` and
   `unity/Assets/Scripts/Bootstrap/CatMetro.Bootstrap.asmdef`: funnel wiring only. Wait for
   Lane 8, rebase over any already-landed Lane-11 tail under the second-lander rule, preserve
   the declared 3 → 6 → 8 → 5 → 11 funnel, then author the Lane 5 wiring last.
2. `unity/Assets/Tests/EditMode/CatMetro.Tests.EditMode.asmdef`: add the canonical integration
   reference; no third test assembly.
3. `unity/Assets/Tests/EditMode/Pure/Domain/HashAndParityTests.cs`: the exact human-granted
   package-manifest parity-gate re-author; no unrelated parity assertion changes.
4. Four E-1 gate amendments, each the narrowest possible change:
   - `tests/taxonomy/taxonomy.test.sh`: RevenueCat token allowed only under the canonical path;
   - `tests/save/save.test.sh`: engine-free scan excludes Integrations;
   - `tests/save/save.test.sh`: `UNITY_ANDROID` scan likewise excludes Integrations;
   - `tests/unity/editmode.test.sh`: launcher/backup validation accepts the pinned SDK payload
     while retaining backup-off enforcement and the single-BillingClient invariant.
5. `state/handoffs/CM-MONETIZATION-CODE-frozen-contract.md`: this first-commit contract and its
   evidence-only status log.
6. `state/handoffs/CM-MONETIZATION-CODE-implementation-plan.md`: the one contract-named lane
   handoff permitted by Addendum v2.1's exhaustive state-write rule; red-first plan and evidence
   mapping only, with no new product authority.
7. `state/PROJECT_STATE.md`: exactly one concise Lane 5 row at merge closeout, with any displaced
   detail rotated under the existing state/archive policy; no ongoing per-commit diary.

No `unity/Assets/Scripts/Application/Save/**` file is owned at this freeze. No existing
Presentation source or test file may be edited; both granted Commerce trees are new-directory
additions only. The `.github/**` Test Store workflow is a separate risky-path prerequisite PR,
not part of this branch; it receives its own independent security review and human-button merge.

No `unity/ProjectSettings/ProjectSettings.asset` field is owned at this freeze. The categorical
“Android gradle-template/proguard block” description does not identify which of
`useCustomMainGradleTemplate`, `useCustomLauncherGradleManifest`,
`useCustomBaseGradleTemplate`, `useCustomGradlePropertiesTemplate`,
`useCustomGradleSettingsTemplate`, `useCustomProguardFile`, `AndroidMinifyRelease`, or
`AndroidMinifyDebug` may change, nor their intended values; Lane 5 may not choose among them.
After Lane 1A merges and transfers the block, a direct human grant plus reviewed contract
amendment must enumerate every exact serialized field name and value before those named fields can
become a conditional exception. Every other `ProjectSettings/**` path or field remains unowned.
`ProjectSettings/AndroidResolverDependencies.xml` remains separately unowned until every exact-
path condition in the dependency section is met; the same is true of
`ProjectSettings/GvhProjectSettings.xml`. The human grant of an EDM surface does not authorize a
second installation topology: under the proposed UPM-only route,
`unity/Assets/ExternalDependencyManager/**` remains absent and is validated as a duplicate-payload
negative control.

## Explicit non-goals

- No future #64 product catalog, new commerce placement, experiment activation, or remote
  RevenueCat dashboard mutation.
- No custom Shop, theme, district, or rewind renderer.
- No live rewarded-ad SDK call, reward grant, banner, interstitial, forced ad, or app-open ad.
- No Google Play Games implementation; this lane exposes/preserves only ADR-0010's rewind-used
  exclusion signal.
- No analytics taxonomy expansion beyond the already signed identifiers and no telemetry
  payload containing prices or sensitive purchase data.
- No PlayerPrefs, second save file, sentinel file, wall-clock-derived entitlement, or
  best-effort consumable fulfillment.
- No Play Console upload, RevenueCat dashboard publication, product activation, price edit, or
  production credential commit.
- No Domain-layer, scene, prefab, existing Presentation file, `Application/Save/**`, or unrelated
  gate edit. Application and Presentation work stays inside the two granted new directories.

## Human rulings and remaining explicit conflict at freeze

The four human scope/conflict rulings and sixteen newly discovered source/privacy/governance conflicts are
recorded as follows; item 3 grants the fallback surface but does not answer the 9.7.0 visibility
ambiguity:

1. **Application and Save.** `Application/Purchases/**` is granted. The Save grant is void because
   the current Proposed ADR has neither a qualifying signed amendment nor a source-file list;
   the authorized Save set is empty until a later reviewed contract amendment records both.
2. **Package parity.** The exact `HashAndParityTests.cs` package-pin parity exception is granted.
3. **Fallback.** Merged #64's “No Offering = no UI” supersedes CM-R25 current/cached fallback.
   New `Presentation/Commerce/**` and `Tests/PlayMode/Commerce/**` directories are granted solely
   for the described post-Level-5 crash fallback; existing Presentation files remain out of
   scope. Source review then established that 9.7.0 cannot report whether RCUI was visible before
   an unlatched Error. The grant is not stretched into an A/B decision: automatic handoff stays
   stopped until the human explicitly authorizes the narrow same-exposure exception or instead
   requires fail-closed advance. Neither branch may add a second fetch, different offer, or
   counteroffer.
4. **Build key and CI.** The exact new build injector file is granted. Test Store CI lands in a
   separate `.github/**` PR with its own independent security review and human-button merge; that
   prerequisite must be green and merged before Lane 5 can merge.
5. **Placement-error observability.** RevenueCat 9.7.0 collapses an Android placement error-only
   response into the same `(null, null)` shape as a legitimate missing Placement Offering. To
   preserve the human-ruled No Offering = no UI law, the observable result fails closed with no
   retry. That makes the signed placement-error retry schedule unimplementable through the public
   9.7.0 API. A direct human ruling plus reviewed contract amendment must either accept the source
   limitation as superseding the retry rule or authorize an exact SDK-boundary remedy; this lane
   invents neither error telemetry nor a wrapper patch.
6. **Fallback impression identifier.** The signed custom-impression call requires a `paywallId`,
   but no identifier is signed for the post-Level-5 fallback. Tracking remains absent until the
   human pins that exact identifier or explicitly authorizes 9.7.0's Offering-only overload and a
   reviewed contract amendment records the choice.
7. **Anonymous-ID reset.** Normal configuration is SDK-generated anonymous identity with no
   `LogIn`/`LogOut`. CM-R53's Reset Progress requirement simultaneously asks to rotate that ID and
   records the resulting purchase-orphaning/Data-Safety conflict as a human decision. Lane 5
   implements no reset/rotation semantics until that decision is signed and its scope assigned.
8. **Runtime configuration.** Builder source defaults are not privacy approval and automatic
   device-identifier collection defaults true. The required positional API key has no managed
   null/empty/prefix validation, and the native wrong-store check may only log and continue. The
   dependency/privacy ADR must select every frozen Builder and non-Builder configuration surface,
   exact logging policy, single persistent-component lifecycle, RevenueCat-completed purchase
   architecture, and fixed-false automatic in-app-message behavior before configuration code or a
   production `Configure` path is authored. Both prebuild and runtime boundaries trim once,
   forward that same value, and reject a blank, bare-prefix, whitespace-only-suffix, wrong-store,
   or flag-mismatched key before Build/Configure. This contract does not
   silently select defaults or treat the inert C# `ObserverMode` field as authority.
9. **SDK privacy activation.** RevenueCat initialization can transmit data even while paywall
   presentation is dark. Human review of the production proxy capture, manifest/dependency/data-
   handling inventory, privacy/age/consent disclosures, and Play Data Safety answers is required
   after a separately human-authorized controlled candidate capture and before production
   activation or merge. Code review cannot substitute for that human product/legal judgment.
10. **Native Offering and Package re-resolution.** Stock Unity 9.7.0 RCUI forwards only Offering ID/context to
    Android; native 10.16.0 re-resolves cache-first and chooses
    `offerings[id] ?: offerings.current`, exposes no
    pre-render validation callback, and auto-resumes purchase initiation before the observational
    C# listener. Stock `PurchasePackage` separately sends only package ID/context, cache-first
    re-resolves
    offerings, and bills the new case-insensitive package match; `PurchaseProduct` loses the signed
    context. These violate exact whole-Offering validation, No Offering = no UI, and exact validated-
    product billing. Primary RCUI and all real purchase rows/calls stay no-code until the human
    signs a reviewed Offering-pre-render and Package/product-pre-billing remedy or explicitly
    supersedes the renderer/spec/scope. A fork/bridge is a new dependency, integrity, privacy, and
    independent-security decision.
11. **Placement cache provenance.** The stock placement API is cache-first, may vend memory or disk
    Offerings, strips provenance before Unity, and exposes no force-current/invalidation API. That
    conflicts with the human no-cached-substitution ruling even when dashboard fallback is None.
    All real placement calls/commerce remain no-code until the human explicitly accepts the exact
    cache semantics or approves a reviewed native force-current/provenance boundary and contract/
    dependency amendment.
12. **CustomerInfo authority.** `AllPurchasedProductIdentifiers` is payer evidence, not a
    consumable ledger. Complete `NonSubscriptionTransactions` history is likewise not fulfillment:
    the native public history model has no refund/revocation or current-versus-history facts, Unity
    additionally drops available store/sandbox/store-transaction facts, and direct Play `orderId`
    differs in kind from the history RevenueCat/backend ID. A fresh/lost/pruned ledger can therefore
    regrant old consumables or double-grant one purchase under unequal identifiers. ADR-0006 plus a
    reviewed contract amendment must bind a safe local baseline/cursor or explicit no-regrant
    policy before history-assisted recovery through AutoSync plus the sole UpdatedCustomerInfo
    listener. A mapper-only change is insufficient. Any backend/external authority remains
    out-of-scope pending a separate human product/scope grant, architecture ADR, owned surfaces,
    and fresh threat-model/privacy/security review; a lower-level SDK remedy needs its amended
    dependency/privacy authority and independent security review. Any permitted recovery must
    supply refund/current-history evidence and one canonical nonblank store identity proven equal
    across direct and recovery paths. It must also bind entitlement source-product attachments, global/row Verification,
    Store/channel, sandbox, expiration/renewal, retention/refund, and offline failure semantics
    before any durable reconciliation. Explicit and callbackless `SyncPurchases` remain absent under
    the RevenueCat-completed architecture; a contrary design needs a direct human/source override
    and mutually exclusive architecture. No active-key-only, transaction-collapsing, or whole-
    history grant is inferred.
13. **Native startup/foreground Offering traffic.** `useRuntimeSetup=true` disables only the C#
    `Start` calls to Configure/GetProducts. Pinned Android source still installs a lifecycle
    observer during Configure and can fetch, construct, cache, and predownload Offering assets on
    cold/foreground when cache is null or stale, including a resume during gameplay phases 1–4.
    That conflicts with the signed fetch embargo while the separate target requires dark-
    presentation initialization. No production `Configure` or persistent Purchases wiring is
    authored until the human either narrows the embargo to accept precisely characterized SDK-
    internal prefetch/refresh with zero Cat-Metro-initiated request/surface, or authorizes a
    reviewed lifecycle boundary that prevents the traffic. A contract amendment and device/proxy
    traffic characterization are mandatory.
14. **Google Block Store identity persistence.** Pinned native code always constructs Blockstore,
    backs up a purchased anonymous RevenueCat App User ID to Google cloud under
    `com.revenuecat.purchases.app_user_id`, restores/aliases it before restore, and aliases/stores it
    before purchase completion. Unity exposes no disable switch; `UseAmazon=false`, automatic-
    device-ID false, and Android `allowBackup=false` do not disable it. Before Configure or purchase,
    the human privacy/Data-Safety/reset decision must accept this cloud persistence/recovery and
    its CM-R53 orphaning effect, or authorize an independently reviewed native/dependency remedy.
15. **EDM editor settings and mutation timing.** EDM4U defaults automatic resolution and automatic
    resolve-on-build on; its measurement settings default analytics enabled with consent false and
    include Unity version/platform, although the current Universal Analytics POST body is commented
    out and no current outbound telemetry is claimed. Import/report/prompt paths can still mutate
    settings before an explicit ResolveSync. The dependency/privacy ADR must bind
    `UseProjectSettings`, measurement disabled, `AutoResolverEnabled`, and `AutoResolveOnBuild`
    before package import/resolve. The lane requires both auto-resolution switches disabled unless
    an exact human-approved alternative owns and audits every earlier mutation. No pre-sync or
    ungranted `GvhProjectSettings.xml` mutation is acceptable.
16. **Charged-but-Error ambiguity.** Android can create a Play StoreTransaction and then fail
    RevenueCat receipt posting; hybrid/Unity returns only Error and strips the phase/transaction.
    The public API cannot safely distinguish that charged outcome from a pre-billing failure, so
    every post-initiation non-cancel/non-pending error remains Unknown/unsettled, reconciles, and
    never uses the signed “you were not charged” copy or retries. Real billing remains no-code until
    the human signs the copy/state supersession or approves a reviewed native provenance boundary
    that can prove an exact pre-billing class.
17. **Consumable reinstall/history ambiguity.** Complete non-subscription history plus Block Store
    identity recovery and an empty/pruned local ledger can regrant spent or refunded rewinds after
    reinstall. Neither the native public Transaction nor its Unity mapping carries refund/
    revocation or current-history truth, and direct versus history identifiers are not canonically
    equal. Whole-history enumeration and mapper-only provenance are forbidden. The human-signed
    ADR/contract must choose a safe local baseline/cursor or explicit no-regrant policy before
    consumable recovery is implemented. Any backend/external or lower-level remedy remains no-code
    until its additional product/scope, architecture, ownership, dependency, threat-model/privacy,
    and independent-security gates land as applicable.
18. **Production graduation and real CI.** A human mode flip cannot substitute for the binding
    graduation checklist. Human-owned predecessor PRs must close rulesets, CI secret scanning,
    dependency audit, incident runbook, backup-restore drill, review-authentication posture, and an
    actual pinned-Unity CI compile/EditMode/PlayMode job. A shell wrapper that reports the editor
    half deferred is not production evidence. Those risky workflow changes remain outside Lane 5,
    independently security-reviewed, and human-merged.
19. **NEW-Q46 offline-entitlement residual.** Indefinite offline entitlement honoring and
    unenforceable offline revocation are not agent-accepted defaults. Before durable entitlement
    code or merge, a human-signed PRD/ADR record must accept NEW-Q46 and make the containing
    invariant binding: no entitlement, reward, cap, or grant unlocks anything with marginal cost or
    anything another player's outcome depends on.
20. **NEW-Q48 release credential custody.** Before the first signed build or Lane 5 merge, the
    human must resolve NEW-Q48 and evidence Play App Signing/upload-key custody, encrypted CI
    secrets, a human-approved production environment, debug-only PR smoke with zero release
    secrets, SHA-pinned third-party actions, least-privilege permissions, no PR-trigger secrets,
    hook-plus-CI secret scanning, and an app-scoped/rotated service account. The binding technical
    invariant is that no agent-reachable context ever holds a credential capable of publishing,
    mutating entitlements, or messaging the install base. The human must also choose and record
    either acceptance of ADR-0009's path-filtered `android-smoke` residual or a per-PR-always job.
    All `.github/**` and console changes remain separate, independently security-reviewed, and
    human-owned/merged. Hook-side scanning is a separate human-authored immutable-path predecessor
    in exact `scripts/git-hooks/pre-commit`, independently reviewed under the protected-path process;
    an agent-authored or alternate hook is not evidence.

## ADR-0006 and dependency stop gate

The current save contract does not define a durable once-per-install post-Level-5 marker,
rewarded session counters, or the complete purchase/restore/consumable breadcrumb state. It is
therefore forbidden to invent persistence in PlayerPrefs, memory-only state, or an extra file.

Before ledger, entitlement-cache, purchase-breadcrumb, reward-grant, session-cap persistence,
or durable post-Level-5 orchestration is implemented, a human-signed ADR-0006 amendment must
define those fields, defaults, migration, write ordering, crash recovery, deduplication,
refund/revocation semantics, session identity, NonSubscriptionTransactions retention/recovery,
the safe local consumable baseline/cursor or explicit no-regrant policy, the prohibition on a
mapper-only provenance remedy, any separately authorized backend/external/lower-level authority,
one canonical nonblank store transaction identity proven equal across direct and recovery paths,
direct/listener/AutoSyncPurchases convergence, the fixed absence of explicit/callbackless
`SyncPurchases` under RevenueCat completion, global/per-row
Verification acceptance, exact EntitlementInfo source-product/store/channel/sandbox/expiration/
renewal rules, and offline failure behavior. The three new rewarded identifiers also
remain hard-disabled until that human supersession. A separate human-signed dependency ADR
must authorize the exact RevenueCat and EDM4U pins, OpenUPM integrity/topology, complete Android
dependency/license/privacy census, every Builder and non-Builder runtime configuration value,
production-key classification, native logging policy, Blockstore decision, and deterministic EDM
settings/mutation policy before any dependency or configuration edit enters the branch. Neither
ADR alone resolves the separate gameplay-phase/native-prefetch conflict; that requires the direct
human fairness ruling and reviewed contract amendment described above.

No durable entitlement implementation or merge may precede a human-signed NEW-Q46 PRD/ADR record
accepting indefinite offline honoring and unenforceable offline revocation, together with the
binding invariant that no entitlement, reward, cap, or grant unlocks marginal-cost or cross-player
outcomes. ADR-0006 must cite that record rather than silently accepting the residual. An external/
backend fulfillment authority remains outside the 1.0 no-server architecture and this lane; it
requires its own human product/scope grant, architecture ADR, owned surfaces, and fresh threat-
model/privacy/security review in addition to any ADR-0006 amendment. A lower-level SDK remedy
similarly requires amended dependency/privacy authority and independent security review.

That amendment must also resolve the binding CM-R26 contradiction without an agent-authored
guess: marker-before-fetch once-ever/no-rearm permits only one production RCUI attempt, while
CM-R26.6 requires two strikes before permanent disable. It must either authorize a
production-reachable retry and explicitly supersede the conflicting once-ever/no-rearm clause,
or supersede the two-strike threshold. A test that invokes the presenter twice while bypassing
eligibility is forbidden as false evidence.

After that ADR-0006 amendment lands, this frozen contract must be explicitly amended before
Save work: the amendment records the ADR path, signing SHA, and exact `Application/Save/**` file
list, plus the exact human strike-law resolution. Until then, the lane may neither stage nor
modify any Save file or author crash-strike/permanent-disable behavior, even if the future ADR's
schema prose appears sufficient.

Until the ADR-0006 signature exists, implementation may cover SDK-free identifiers/DTOs,
pure eligibility and non-persisting orchestration inside the granted Application directory, the
fail-closed non-configuration SDK adapter once its dependency/privacy ADR is signed, and tests that
pass persistence state into pure code. Only the SDK-free rewarded model is permitted; no ad SDK
type, call, wrapper, or future adapter seam is authored. Dependency proof before its ADR remains
read-only/scratch-only and is never committed. No pre-signature work may claim the full scripted
flow is complete.

## Red-first acceptance criteria and mutation proofs

Every behavior criterion begins with a failing test committed before its minimal production
implementation. Absence assertions carry an executable positive control or mutation that
demonstrates the gate still fails.

1. **Catalog exactness.** Test the active set, store types, and entitlement attachments are
   exactly those above. Mutating one, adding the experiment identifier, adding a future
   product, or recreating umbrella access with client boolean logic fails. Neon activation is
   additionally blocked until the exact CM-R21 deutan/protan/tritan, byte-identical symbol+
   silhouette, grayscale 23/25, and silhouette-only-at-64px 23/25 evidence is attached; bypassing
   that evidence gate fails. Human console exports plus the six-SKU Play sandbox matrix prove the
   exact active/inactive state, one-time types, positive and negative attachment sets, +5/+20 sole-
   ledger outcomes, and zero entitlement changes for rewind purchases; Test Store-only evidence
   fails this criterion.
2. **Placement exactness and confinement.** Test exactly five game placements and the exact
   integration mapping. A foreign `ofr_*` token outside the canonical assembly fails the
   Lane 5-owned static confinement test; the canonical token passes. Independently, the E-1
   taxonomy gate permits the RevenueCat SDK token only under the canonical assembly and proves
   an outside-path negative control still fails.
3. **Remote allowlisting.** Unknown/missing/duplicate/reordered packages, any `$rc_*` identifier,
   any non-null Offering/Package `WebCheckoutUrl`, any package whose
   `StoreProduct.ProductCategory` is not `Purchases.ProductCategory.NON_SUBSCRIPTION`, Supporter
   outside Shop, any non-`CUSTOM` PackageType, and any non-All-Access post-Level-5 package reject
   the entire original Offering. Every Package and StoreProduct PresentedOfferingContext must have
   the exact returned Offering identifier and requested placement, and every package context must
   agree on targeting null/value, revision, and rule ID;
   no filtered Offering is synthesized.
   Each exact custom-package→product ordered shape and NON_SUBSCRIPTION category is table-tested.
   Mutations accepting SUBSCRIPTION/UNKNOWN/non-CUSTOM, missing/mismatched/cross-flow/cross-package
   context, reordering, or reconstruction fail; null targeting remains valid and is never invented.
   Human-owned dashboard
   evidence proves all five Placement fallback settings are None and a forced null resolves
   null; enabling a current-offering fallback is a failing device/config control. Cat Metro's
   adapter has no ordinary `GetOfferings`; a source/static positive control fails if stock Android
   RCUI is reachable because its native `awaitOfferings()` and `offerings.current` substitution
   bypass this validation. A separate positive control proves placement lookup may vend memory/disk
   cache with no Unity provenance; current scope requires zero real placement calls until the human
   cache ruling/remedy, and a cold forced-null dashboard run is not cache-provenance evidence. The
   authorized predecessor's machine-map proof fails if either
   JSON companion retains cached/current fallback, Supporter in `ofr_core`, or All Access in
   `ofr_rewind`.
4. **Phase embargo.** Table-test all gameplay phases. Mutating any phase 1–4 to eligible fails
   and asserts zero service fetch calls, not merely hidden UI.
5. **Attempt-one embargo.** Attempt one has no chip/fetch/ad/purchase. Changing the attempt
   threshold or prefetching before a tap fails.
6. **Rewind eligibility.** Boundary tests cover attempt 1/2, progress immediately below/at 40
   percent, missing/present safe decision tick, and untapped/tapped chip.
7. **Pre-Level-7 protection.** Levels 1–6 expose only free/owned options with zero placement
   fetch, zero ad call, and zero purchase row. A mutation that fetches at Level 6 fails.
8. **Level-7-plus ordering.** Rows are free → owned → rewarded → packs; a reordered or primary
   purchase row fails. Disabled rewarded availability cannot create an ad request.
9. **Rewarded catalog and caps.** Exactly eight identifiers and their signed reward/cap metadata
   are modeled; all SDK calls are disabled; the three additions carry the extra ADR-disabled
   bit. Rewarded rewind boundaries prove two/session and five/day, including a recheck before
   grant. Decline/mute, no-fill, atomic grant, trial, and guest-route laws are red-tested as
   dormant pure policy without making an SDK call or persistence claim.
10. **Post-Level-5 timing and once semantics.** After the signed ADR amendment and explicit
    Save-scope contract amendment, tests prove first L5 win only, celebration-before-paywall,
    paywall-before-map, marker-before-fetch, and no re-arm on empty/error/cancel. A
    marker-after-fetch mutation fails.
11. **Post-Level-5 fallback.** Empty Placement resolution opens neither renderer and never reads
    current/cached offerings. Current-scope tests prove zero stock `PaywallsPresenter.Present`
    calls and reject primary RCUI orchestration. They reproduce the source boundary—ID/context-
    only JNI, native `awaitOfferings()`, `offerings[id] ?: offerings.current`, no pre-render C#
    validation callback, and purchase auto-resume before `OnPurchaseStarted`—so deleting the
    no-code guard fails. Only after a human-approved, independently reviewed boundary remedy may
    listener-order tests prove purchase/restore errors latch before the
    terminal Task result: a purchase latch whose exact fields are code 20 plus readable string
    `PaymentPendingError` preserves the `OnPurchaseStarted` package/SKU and returns Pending; every
    other `Error` with either commerce-operation latch advances and opens no second UI, while an
    unlatched `Error` maps to an unresolved outcome that no production orchestrator consumes. No
    unobservable thrown-exception branch exists. Current-scope tests
    reject automatic handoff, crash-strike, and permanent-disable behavior; each remains stopped
    pending its exact human resolution and reviewed contract amendment.
    PlayMode tests prove the custom renderer's exact signed §4.1 layout/copy order as amended by §8.2—including
    `every playable line in Cat Metro`, explicit Sakura Line + Neon Line, and no “complete
    edition” claim—plus all three disclosures without
    scrolling at 720p 16:9, ≥48dp immediate dismissal, free-dismissal parity, no
    urgency/preselection/counteroffer, and text/symbol twins so color and motion are never the
    sole state carriers. Mid-tier device evidence proves ≤2.0-second open-to-render and
    ≤3.0-second purchase-to-entitlement. Before the fallback-ID/overload ruling, tests prove zero
    custom-impression calls; no invented identifier or Offering-only substitution is permitted.
    After its reviewed amendment, the standalone custom renderer records exactly one custom
    impression only when actually visible and same-renderer duplication fails. Adapter tests prove
    that visibility returns the issued SDK-free handle, resolves the exact original validated
    `Offering`, preserves its `PresentedOfferingContext`, and rejects expired/replayed/foreign
    handles. Reconstructing an Offering from its identifier or leaking an SDK type into
    Presentation fails. Before the native Package remedy, Package-ref tests permit only SDK-free
    lineage/display resolution and prove every real `PurchasePackage`/`PurchaseProduct` call stays
    zero/Unavailable. They reproduce stock `PurchasePackage`'s identifier/context-only bridge,
    internal `getOfferingsWith`, case-insensitive cache-first re-resolved-package selection, and changed-product
    billing as a failing positive control. After the reviewed remedy, tests validate/veto the exact
    original native Package/product before billing, preserve `PresentedOfferingContext`, and reject
    expired/replayed/foreign/cross-flow refs, swaps, refetches, stock calls, and identifier
    reconstruction. Cross-renderer
    handoff/impression tests are forbidden until the A/B amendment selects observable behavior.
    After the boundary remedy, human-owned dashboard export plus device screenshots prove the
    primary RCUI renderer uses the same amended copy/layout and contains no superseded “complete
    edition” claim. Content-truth
    fixtures select exactly one signed Night Harbor state: validated L901–L910 with the ten-level
    benefit, or the `includes the Night Harbor district (arriving this week)` disclosure backed by
    a still-current human delivery attestation. RCUI/custom mismatch or sale with neither state
    fails.
12. **Post-Level-5 suppression.** Any entitlement, prior purchase, or refund marker suppresses
    the surface. The valid path requests only All Access and never counteroffers.
13. **Localized pricing.** Display models accept only localized SDK metadata. A source/resource
    sweep rejects hard-coded currency copy and use/formatting of floating `StoreProduct.Price`;
    the only player price string is exact `StoreProduct.PriceString`. Missing metadata hides
    purchase UI.
14. **Callback-slot safety.** Purchase-family calls reject a second in-flight purchase as Busy;
    restore and any later-approved RevenueCatUI boundary reject overlapping modal work as Busy;
    customer-info and, after its cache ruling/remedy, placement calls serialize FIFO by their
    respective callback slots. Current scope proves zero real placement call. The RevenueCat-
    completed architecture fixes `AutoSyncPurchases=true`, proves recovery through the sole
    UpdatedCustomerInfo listener on device, and contains no explicit or callbackless
    `SyncPurchases` invocation; a mutation adding either overload fails. Cat Metro's production adapter has no
    ordinary `GetOfferings`; the unreachable stock presenter/purchase source's internal
    `awaitOfferings`/`getOfferingsWith` is a negative-control reason, not an allowed call path.
    Placement A/B and same-slot tests complete in order without overwrite, misattribution, or
    callback loss. Only Offering-fetch fixtures have the signed eight-second caller timeout: the
    placement slot stays quarantined, a retry remains Busy, and only the actual late callback may
    drain the slot without updating the timed-out caller or a later request. Purchase, restore, and
    reconciliation tests prove no eight-second completion/discard exists; after UI-caller
    detachment, their late definitive callback still enters idempotent reconciliation. Removing a
    slot guard or discarding a late commerce callback fails deterministically.
15. **Main-thread boundary and listener ordering.** Worker-thread core-callback fixtures prove the
    adapter posts before completing a result or touching cache/event/state. Already-main-thread
    callbacks run inline exactly once. After the native-Offering remedy, a RevenueCatUI test drives
    an inline listener latch followed by the terminal Task result through the reviewed adapter and
    fails if the latch is enqueued behind the result. Before then, the testable law is no presenter
    call at all. Dev-build emissions assert main thread; any later presenter entry made off-main
    fails while the dispatched call passes.
16. **SDK API and lifecycle fidelity.** Compilation and adapter tests pin one never-renamed,
    persistent `CatMetroRevenueCatPurchases` GameObject with one global `Purchases` component,
    `useRuntimeSetup=true`, exactly one UpdatedCustomerInfoListener assigned before first `Start`,
    `proxyURL == null`, native wrapper creation before exactly one synchronous `Configure`, no C#
    automatic `Configure(string)`/`GetProducts`, and no duplicate/rename/destroy/reconfigure path.
    Until the direct native-prefetch fairness ruling/remedy, tests instead require zero production
    `Configure` and zero persistent wiring, and a pinned-source positive control demonstrates that
    Configure/foreground can fetch/cache/predownload Offerings despite runtime setup. Stale `RevenueCat.Purchases`,
    `SharedInstance`, async-Configure, or static result-helper calls fail. Source-characterization
    tests pin stock `new PaywallOptions(originalOffering, listener: listener)`,
    `Task<PaywallResult>`, the public `result.Result`, all five enum outcomes, and all nine listener
    callbacks while a static/behavior gate proves production orchestration calls none of them
    before the native remedy. After that remedy, terminal Purchased/Restored alone grant nothing.
    Listener fixtures prove `OnWebCheckoutOpened` and
    `OnUrlOpened` are non-grant events that never complete a purchase, trigger fallback, or open
    another surface; dashboard evidence keeps web checkout disabled.

    Configuration tests cannot select values before the dependency/privacy ADR. After its signed
    amendment and the separate native-prefetch fairness resolution, a table test pins every Builder
    and non-Builder field to the human-approved value and fails on any omitted/new/defaulted field,
    including the positional API key, automatic device-ID collection, log level/handler order, and
    null proxy URL. It pins RevenueCat completion, `AutoSyncPurchases=true`, automatic in-app
    messages false, zero explicit `ShowInAppMessages`, and proves the inert readonly `ObserverMode`
    is never read as authority. Runtime-before-Configure key tests trim once and forward that same
    value; reject blank/whitespace, exact bare `goog_`/`test_`, prefix plus whitespace-only suffix,
    `amzn_`, `galx_`, unknown, release `test_`, Test-Store `goog_`, and every flag/prefix mismatch;
    and require at least one non-whitespace suffix character without inventing a stronger key grammar.
    Identity tests retain
    null custom App User ID, no `LogIn`/`LogOut`, no custom/account-ID override, subscriber-
    attribute write, OneSignal seam, or other dark factory. Production initialization with
    presentation dark is tested only after the privacy activation gate. Reset/anonymous-ID rotation
    remains absent pending CM-R53's human decision. Purchase source-characterization tests prove
    stock `PurchasePackage` cache-first re-resolves by identifier/context and stock `PurchaseProduct` loses
    context and defaults to `subs` when explicit type is omitted; explicit `inapp` remains an
    unauthorized alternative. Production calls to both remain zero before the native Package
    remedy. Post-remedy tests require a pre-billing exact native Package/product validation and
    veto. A source sweep proves zero `AdTracker`, `TrackAdRevenue`, or `AdRevenueData`
    reference in compiled Lane 5 code; the recorded future-source fact is `RevenueMicros : long`.
17. **Purchase state machine, post-ADR and native-boundary remedy.** After the signed ADR-0006,
    reviewed contract amendments, and exact native Package/product remedy,
    tests cover Idle → Fetching → Presenting → Purchasing → Pending/Verifying → Granting → Done,
    plus Cancelled and Failed. Exact `Error.Code == 20` plus
    `Error.ReadableErrorCode == "PaymentPendingError"` maps to Pending on both direct-purchase and
    RevenueCatUI listener paths, not Failed; it grants nothing, opens no fallback, shows no
    ordinary not-charged failure copy, and can complete only through later `CustomerInfo`
    reconciliation. `OnPurchaseStarted` and the later Pending state preserve/match the exact issued
    `PackageRef`, package identifier, product identifier, and placement lineage. Only Granting may mutate
    durable state, and success is exposed only after the authorized entitlement cache or
    exactly-once consumable ledger commits atomically. A source/mutation proof rejects every
    purchase-rewind grant path that does not call the existing sole
    `Application/Save/ConsumableLedger.TryGrant(...)`; `Application/Purchases/**` contains no
    duplicate ledger or direct `rewindBalance` mutation. Direct-result table tests enforce the
    exact precedence UserCancelled → code-20/string Pending → other post-initiation Error Unknown/
    unsettled with reconciliation and without the no-charge copy or retry → fully matched
    retained-PackageRef/non-null CustomerInfo/non-null StoreTransaction/allowlisted transaction
    product success candidate → Unknown/unsettled. A rewind success candidate additionally requires
    exact `!string.IsNullOrWhiteSpace(StoreTransaction.TransactionIdentifier)` before Granting;
    null, empty, and whitespace identifiers each remain Unknown/unsettled with reconciliation and
    zero grant. The ID is neither normalized nor substituted from another callback path. Product
    identifiers are never fabricated for Pending/error. `AllPurchasedProductIdentifiers` is tested as payer/suppression evidence only;
    any mutation using it for consumable fulfillment fails. Complete
    `NonSubscriptionTransactions` history also grants zero by default. Fresh-install, lost-ledger,
    pruned-ledger, Block-Store-ID-recovery, refunded/foreign-store/sandbox, and repeated-history
    fixtures prove old rewinds never regrant. Only after a signed local baseline/cursor or explicit
    no-regrant policy, plus any separately authorized lower-level/backend authority, may a locally
    attributable allowlisted transaction with signed quantity/time and a canonical nonblank store
    identity proven equal across permitted direct/recovery paths enter the sole ledger. Mapper-only
    provenance fails. Unequal Play order ID versus RevenueCat/backend history ID is a mandatory
    negative control and never deduplicates by assumption. Multiple same-product
    current transactions, duplicates, out-of-order delivery, direct-result-plus-listener,
    AutoSyncPurchases/UpdatedCustomerInfo redelivery, lost direct callback, Pending-next-session,
    process death, and late callbacks then converge idempotently without double/under-grant.
    Whole-history enumeration remains a failing mutation. OriginalJson/Signature/raw transaction
    data never leaks outside the adapter/ledger hashing boundary.

    Entitlement mutations accept a durable candidate only when key==Identifier, IsActive, signed
    entitlement ID, exact allowed source ProductIdentifier attachment, and every ADR-approved
    global/row Verification, Store/channel, IsSandbox, expiration, and renewal invariant passes.
    Test Store/sandbox never grants release authority. Experiment/future/unknown source SKU, cross-
    attachment, mismatched key, subscription-like expiry/renewal, wrong store/channel, and every
    disallowed VerificationResult are mutation controls. Foreign/contradictory rows grant nothing
    and emit scrubbed quarantine diagnostics; repeated/reordered reconciliation is idempotent.
18. **Retry and recovery policy.** Purchases are never auto-retried. The inspected placement API
    collapses Android error-only JSON into the same `(null, null)` result as No Offering, so that
    result fails closed with no UI/current/cache substitution and no retry or invented error
    telemetry. The signed 1/4/10-second, eight-second-attempt placement-retry law remains a
    no-code source conflict until its direct human ruling and contract amendment. Separately,
    cache-first memory/disk Offering provenance is hidden; no placement-dependent commerce runs
    until the human accepts those semantics or a reviewed force-current/provenance remedy rejects
    cached results. For any later
    authorized retry-capable SDK callback, timeout alone cannot free the slot or start a retry;
    only the actual failure callback can drain it and schedule the next attempt. User cancellation
    stays non-error, already-owned triggers at most one restore, and
    network/store/backend/pending outcomes remain distinct. After Save authorization, a durable
    purchase breadcrumb drives silent boot verification and uses the exact expiry law signed into
    ADR-0006, never a value invented from the draft implementation spec.
19. **Restore integrity, post-ADR.** Restore is single-flight, applies non-consumable
    entitlements and payer suppression in the same main-thread frame, never calls a consumable
    history “restored,” handles none-found honestly, and grants nothing without durable state.
    Fresh-install, wrong-account/none-found, consumables-only, already-owned-auto-restore-once,
    duplicate callback, and process-death device cases are evidenced. Presentation entry points
    and copy remain a named prerequisite outside the granted fallback-only Commerce directory.
20. **Offline entitlement truth, post-ADR and NEW-Q46.** Only after a human-signed NEW-Q46 PRD/ADR
    acceptance and ADR-0006 citation, boot serves the durable SDK-free entitlement cache before
    network initialization; non-consumables remain honored indefinitely offline;
    staleness is diagnostic/reconcile priority only and never revokes. Explicit RC revocation
    applies only at the signed next-Home/session boundary, never mid-level. The signed containing
    invariant is statically and behaviorally proved: no entitlement, reward, cap, or grant unlocks
    a marginal-cost or cross-player outcome. Failed reconciliation retains cache and queues a later
    foreground retry. Without the human acceptance, no durable entitlement code is authored and
    merge is blocked.
21. **Build-key and Test Store discipline.** The granted automatic build injector reads
    `CM_RC_PUBLIC_KEY` without committing or logging the key. `CM_TEST_STORE` is compile-time,
    debug-only, has no runtime switch, and is rejected/stripped for release. Prebuild classification
    accepts only a once-trimmed `goog_` key with at least one non-whitespace suffix character for
    non-Test-Store Google builds and only a once-trimmed `test_` key with such a suffix for a debug/
    development Test Store build; that same trimmed value is forwarded. Blank/whitespace, exact
    bare prefixes, prefix-plus-whitespace, `amzn_`, `galx_`, unknown, prefix/flag mismatch, and
    release `test_` fail without logging/stringifying the key. No stronger undocumented key grammar
    is invented. Tests plus a real
    headless call through the existing `CatMetroCliAabBuild.BuildAndroidAab` path prove the hook
    executes without editing that file; a missing production key or release Test Store selection
    fails in the pre-build boundary before player compilation/artifact production. Temporary
    defines/configuration/settings restore on success and failure. The separately merged CI
    prerequisite proves a Test Store purchase with a debug key and zero release secrets;
    evidence states that pending/deferred and real Billing-sheet behavior require Play sandbox.
22. **Rank exclusion.** Each free/rewarded/purchased rewind marks the run globally ineligible;
    a no-rewind run remains eligible and local rewards remain intact.
23. **Package parity.** `config/pins.json`, manifest, and lock agree on core, UI, and EDM4U.
    Mutating any one version fails the exact human-granted `HashAndParityTests.cs` gate. The
    vendor's UI-node-only `file:../RevenueCat` declaration is accepted only alongside the
    independent depth-zero registry core 9.7.0 root; the same file dependency anywhere else
    fails. Machine checks pin the single OpenUPM name/URL, exact three scopes and depth-zero roots,
    both exact assembly references, and every frozen version/tarball/SHA-1/SHA-512 value. Mutating
    any integrity byte or adding a registry/scope fails. All-three-unsigned status and normalized
    payload-to-upstream evidence match the signed dependency ADR. A clean import proves
    measurement disabled, the signed `UseProjectSettings`/prompt policy, both automatic resolver
    switches false, zero pre-wrapper mutation, exactly one explicit `ResolveSync(true)`, and no
    asynchronous `MenuForceResolve` false-green path.
24. **Package topology.** Resolution proves one RevenueCat core, one RevenueCat UI, one EDM4U,
    no Unity IAP, zero `Assets/ExternalDependencyManager/**` payload, and one BillingClient 8.3.0
    lineage through the exact 18.29.0/10.16.0 chain. A duplicate fixture fails. The resolved graph
    census includes Amazon SDK despite `UseAmazon=false`, Ads Identifier, Blockstore, Tink,
    Compose, WebKit/browser, requested coroutines 1.6.4 versus selected core+Android 1.7.3, the
    selected activity 1.9.3 and fragment-ktx 1.6.2 conflicts, lifecycle 2.8.3, core/core-ktx 1.13.1,
    profileinstaller 1.3.1, startup-runtime 1.1.1, places-placereport 17.0.0, and every frozen
    version above; removing one from review evidence fails. The merged-manifest fixture is
    exhaustive over every resolved AAR and fails on any unreviewed or omitted component,
    permission, query, provider/receiver/service/activity, metadata, export flag, or protection
    permission. It includes the startup/lifecycle initializers, DUMP-protected exported profile
    receiver and four actions, and Google Play Services version metadata. R8 keep/dontwarn
    fixtures—including Amazon and DataTransport CCT consumer rules—match the source
    census, current minSdk 25 remains compatible, the complete license/SCA report disposes the UI
    tarball's missing embedded/SPDX license and all transitives, and the release AAB passes the
    full-graph 16-KB gate despite the four direct AARs containing no `.so`. Every unrelated
    manifest entry and lock node matches the separately reconciled predecessor/base exactly;
    normalization of the stale URP/Test Framework/UGUI nodes inside Lane 5 fails.
25. **Gate evolution keeps teeth.** The taxonomy outside-path control stays red; save scans
    exclude only Integrations while a forbidden token elsewhere still fails; backup remains
    disabled; a second BillingClient fixture fails.
26. **Architecture.** Services contain interfaces/POCO DTOs only and stay SDK/Unity-free;
    Application is the single eligibility source; `ofr_*` stays integration-only. The exact
    `CatMetro.Integrations.RevenueCat` asmdef references only `CatMetro.Services` among Cat Metro
    assemblies plus the exact pinned SDK assemblies; only Bootstrap and the authorized EditMode
    test asmdef reference it. Rename, Application/Domain/Presentation dependency, extra product
    consumer, and third-test-asmdef mutations fail. Bootstrap references integration only after the
    funnel rebase; Presentation changes stay in the new Commerce directory; every explicitly listed
    root folder `.meta` is present and no unlisted companion scope appears; `Application/Save/**`
    remains untouched.
27. **Surface audit.** Diff contains only primary surfaces and declared exceptions. No mode
    file, production credential, dashboard mutation, upload, or unrelated lane edit appears.
28. **Privacy and Data Safety activation.** Human-owned evidence approves the exact production
    proxy capture for dark-presentation initialization and cold/foreground native Offering
    prefetch/refresh, Google Block Store anonymous-ID backup/recovery/aliasing, offering, purchase, restore, Pending,
    CustomerInfo reconciliation, and the CM-R45 forced-crash leg; merged dependency/manifest data
    flows; retention/deletion; privacy notice; age/consent; and Play Data Safety answers. A separate
    prior human authorization names the controlled branch-candidate build, devices/accounts, and
    capture window without approving production or merge. Release fault-injection logcat covers
    native INFO/error-handler paths as well as unconditional C# raw-JSON sites. A stale disclosure,
    missing crash/happy-path/Blockstore/prefetch leg, capture outside that authorization, leaked raw
    identifier/token/JSON, or production activation before final approval fails the gate.

## Validation and evidence

The PR must carry exact commands and results, including failing red runs and green mutation
proofs. At minimum:

- `git diff --check` and a scoped changed-path audit;
- `bash scripts/check.sh`, `bash scripts/test.sh`, and `bash scripts/build.sh`;
- headless Unity EditMode and PlayMode suites at the pinned editor version;
- clean Unity Package Manager resolution with manifest/lock parity, the single exact OpenUPM
  registry and three scopes, exact root/assembly/tarball/SHA-1/SHA-512 parity, normalized upstream
  payloads, and zero `Assets/ExternalDependencyManager/**` or other duplicate payload;
- a synchronous `PlayServicesResolver.ResolveSync(true)` wrapper/test that asserts true, plus an
  inventory of its exact resolved Android dependencies and mutation surface; the asynchronous
  `MenuForceResolve -quit` path is not evidence; both observed ProjectSettings XML paths are
  audited and stay absent unless separately granted. Clean-import evidence pins the signed
  `UseProjectSettings` and noninteractive prompt policy, measurement disabled,
  `AutoResolverEnabled=false`, `AutoResolveOnBuild=false`, and zero import/build resolution or
  settings mutation before the explicit wrapper;
- exact runtime lifecycle tests for one stable Purchases GameObject/component/listener,
  null `proxyURL`, Start-before-Configure-once, no automatic C# Configure/GetProducts, and no
  duplicate/rename/reconfigure. Until the direct fairness ruling/remedy, source/runtime controls
  prove zero production Configure/persistent wiring while characterizing cold/foreground native
  Offering fetch/cache/predownload traffic despite runtime setup;
- exact main-thread tests for inline RevenueCatUI listener latches versus posted off-thread core
  callbacks; all-nine-listener-event, five-terminal-result, direct PurchaseResult-precedence, and
  idempotent UpdatedCustomerInfo reconciliation matrices;
- operation-specific timeout mutations proving eight seconds exists only for Offering Fetching;
  purchase/restore/CustomerInfo/reconciliation never time out or discard their definitive late
  callback after UI-caller detachment. Post-initiation error fixtures remain Unknown/unsettled,
  keep reconciliation, and never use no-charge copy or retry absent the signed provenance ruling;
- whole-Offering mutation matrices for exact `CUSTOM` PackageType and Package/StoreProduct
  PresentedOfferingContext equality across Offering ID, requested placement, targeting null/value,
  revision, and rule ID; first-package impression versus selected-package attribution cannot diverge;
- post-ADR exact Builder/non-Builder census including the positional API key, automatic device-ID
  collection, RevenueCat completion, AutoSyncPurchases true, automatic in-app messages false,
  absent explicit ShowInAppMessages, inert/unread ObserverMode, and native log level/handler order,
  with a mutation for every field. Prebuild and runtime key matrices trim/forward once and reject
  blank, bare-prefix, prefix-plus-whitespace, wrong-prefix, and environment mismatches without
  logging the key. The signed global/row Verification and Store/
  channel/sandbox/expiration/renewal authority table is also required; no configuration or durable
  reconciliation code/evidence may predate its ADR and direct fairness decisions;
- post-ADR CustomerInfo tests proving AllPurchasedProductIdentifiers is suppression-only;
  complete NonSubscriptionTransactions history grants zero by default, and fresh/lost/pruned-ledger,
  reinstall, Block-Store identity recovery, refund/store/sandbox, and replay mutations cannot
  regrant. A mapper-only change cannot authorize history. After the signed local baseline/cursor or
  no-regrant policy plus any separately authorized lower-level/backend authority, only locally
  attributable transactions with a canonical nonblank store identity proven equal across direct
  and recovery delivery may dedupe through sole TryGrant. Null/empty/whitespace direct IDs and
  unequal Play-order-ID/RevenueCat-history-ID fixtures remain Unknown with zero grant. Raw JSON,
  signature, and transaction ID never leak. Both explicit and callbackless SyncPurchases overloads
  are absent, and device evidence proves AutoSync listener recovery;
- Android compile/build plus merged-manifest/minSdk/activity/permission and R8-rule inspection for
  the frozen graph, one BillingClient 8.3.0 lineage, requested coroutines 1.6.4 versus selected
  core+Android 1.7.3, selected activity 1.9.3/fragment-ktx 1.6.2, lifecycle 2.8.3, core/core-ktx
  1.13.1, profileinstaller 1.3.1, startup-runtime 1.1.1, places-placereport 17.0.0, Amazon receiver
  and consumer rules, and Billing/DataTransport/Play Services components and consumer rules. An
  exhaustive per-resolved-AAR manifest fixture proves every component/permission/query/metadata/
  export/protection fact, including startup initializers, the DUMP-protected exported profile
  receiver with four actions, and Google Play Services version metadata; the release AAB passes
  the full 16-KB check;
- complete transitive dependency license and vulnerability/SCA evidence, explicitly including the
  UI tarball license discrepancy and Amazon/Ads Identifier/Blockstore/Tink/Compose/WebKit/browser;
- release-AAB device/proxy captures across cold/foreground native Offering refresh, Google Block
  Store backup/recovery/aliasing, purchase, restore, and CustomerInfo reconciliation. Fault
  injection exercises configured native INFO/error-handler paths and the pinned C# raw-JSON
  `Debug.Log` sites, proving no RevenueCat JSON, anonymous ID, transaction/purchase token, or price
  escapes;
- a source sweep proving Cat Metro never logs/interpolates PurchasesConfiguration, PurchaseResult,
  CustomerInfo, StoreTransaction, Offering, Package, StoreProduct, or raw Error and contains no
  compiled RevenueCat ad-tracker/reference seam;
- `scripts/forge-risk.sh` classification from the actual PR base;
- source/resource sweeps for foreign products, foreign placements, `ofr_*` leakage, price
  literals, duplicate SDKs, PlayerPrefs, additional save files, and live ad calls;
- asmdef/source graph proof pinning exact `CatMetro.Integrations.RevenueCat`, its Services-only
  internal dependency, Bootstrap-only product consumer, and sole authorized EditMode-test consumer;
  rename/extra-reference mutations fail. Changed-path proof includes every exact new root-folder
  `.meta` listed in scope and rejects any unlisted sibling metadata;
- pinned-source/static evidence reproducing stock Android RCUI's ID/context-only JNI boundary,
  native `awaitOfferings()` plus `offerings[id] ?: offerings.current`, lack of pre-render C#
  validation, and purchase auto-resume before `OnPurchaseStarted`; plus stock `PurchasePackage`'s
  package-ID/context-only bridge, internal `getOfferingsWith`, and changed-product billing. Current-
  scope source and runtime tests prove zero reachable stock Presenter or purchase call. Any later
  boundary remedy needs its direct human ruling, amended dependency/integrity/privacy evidence,
  independent security review, and device proof that a changed/missing native Offering is rejected
  before render and a changed Package/product is rejected before billing;
- pinned-source positive control proving placement lookup is cache-first, can vend memory/disk
  Offerings, strips `originalSource`/`loadedFromDiskCache`, and exposes no Unity force-current/
  invalidation API; production placement calls remain zero until the direct human cache ruling or
  device-proven provenance remedy rejects both cached sources;
- human-owned RevenueCat dashboard evidence that all five Placement fallbacks are None and all
  four Offerings have the exact full package/product shapes frozen above, plus a forced-null
  device run proving the SDK returns no offering and no commerce UI opens, and Paywalls-v2 export
  proving web checkout is disabled;
- human-owned Play product and RevenueCat entitlement exports plus one Play license-tester
  purchase per live SKU, proving the exact active/type/attachment matrix, inactive experiment SKU,
  positive and negative CustomerInfo sets, zero rewind entitlement changes, and exact sole-ledger
  +5/+20 grants;
- the authorized human-button predecessor SHA that reconciles or retires the stale fields in
  `offering_and_placement_map.json` and `entitlement_map.json`, with executable proof that neither
  machine-readable file can reintroduce cached/current fallback or the two forbidden package
  memberships;
- after the native-Offering boundary remedy, human-owned Paywalls-v2 export and device frames
  proving the primary RCUI template uses the §4.1-as-amended-by-§8.2 copy, including the exact
  playable-line phrase and Sakura/Neon names;
- fallback frames at required device classes, visually inspected for immediate dismissal,
  color+symbol state twins, localized price, and fair-core hierarchy;
- the human-owned Neon CM-R21 evidence bundle: deutan/protan/tritan pass, symbol+silhouette asset
  lint, theme encoding byte-parity, grayscale 23/25, and silhouette-only-at-64px 23/25;
- validated L901–L910 content evidence or a still-current human delivery attestation for the
  signed `arriving this week` RCUI/custom copy variant, with both renderers matching;
- the authorized predecessor SHA that adds the exact §4.1-as-amended-by-§8.2 `UiStrings` rows and
  reauthors both existing exact-row-count pins in
  `unity/Assets/Tests/EditMode/Presentation/UiCsvDisciplineTests.cs` (`5 + 7`) and
  `unity/Assets/Tests/EditMode/Presentation/UiCsvUx06Tests.cs` (`12`), with no parallel Lane 5
  string table;
- the direct human fallback impression-ID/overload ruling and post-amendment exact-once evidence;
- changed-path proof that both Commerce trees are new-only and `Application/Save/**` is untouched;
- the separate Test Store CI PR's merged SHA and independent security-review evidence;
- human-owned production-graduation predecessor SHAs plus a fresh signed security-checklist walk
  proving rulesets applied, CI secret scanning and dependency/license audit live, incident runbook
  complete, backup restore drill passed, and the review-authentication route resolved. The real CI
  must install/pin Unity 6000.3.16f1 and fail on compile, EditMode, or PlayMode failure; an editor-
  absent/deferred shell result is not green evidence. Every `.github/**` predecessor is independently
  security-reviewed and human-merged;
- the human-signed NEW-Q46 PRD/ADR acceptance of indefinite offline entitlement honoring and
  unenforceable offline revocation, with static and behavior evidence that no entitlement, reward,
  cap, or grant unlocks marginal-cost or cross-player outcomes;
- the human NEW-Q48 decision and pre-first-signed-build evidence: Play App Signing with upload-key
  custody, encrypted CI secrets, a required-human-approval production environment, debug-only PR
  smoke with zero release secrets, commit-SHA-pinned third-party actions, least-privilege workflow
  permissions, no secrets on pull-request triggers, hook-plus-CI secret scanning, and an app-scoped/
  rotated service account; an executable proof that agent-reachable contexts never hold a
  publish-, entitlement-mutation-, or install-base-messaging credential; and the human's recorded
  choice to accept ADR-0009's path-filtered `android-smoke` residual or require per-PR-always.
  Its `.github/**` and console changes are separate, independently security-reviewed, and human-
  owned/merged. The hook-side scan is an independently reviewed, human-authored immutable-path
  predecessor at exact `scripts/git-hooks/pre-commit`; any other hook path or agent-authored edit
  fails this evidence gate;
- the authorized package-baseline reconciliation SHA, or exact proof that every unrelated
  manifest/lock node stayed byte- and semantically identical while the three pinned roots resolved;
- the authorized stale-dashboard-runbook correction SHA;
- device validation required by NEW-Q45 before any rewarded call can be enabled;
- human authorization for one controlled branch-candidate capture followed by human privacy/Data
  Safety approval of both CM-R45 happy-path and forced-crash production-proxy traces, dependency and
  merged-manifest data-flow census, retention/deletion, privacy notice, age/consent, and Play Data
  Safety answers before production activation or merge;
- an independent billing/security review by a reviewer that did not author the diff,
  regardless of the risk classifier's verdict.

## Sequencing and merge gates

1. Freeze this contract as the branch's first authored commit.
2. Write the red-first implementation plan. Record proposed dependency-ADR text and the exact
   ADR-0006 amendment request only inside that contract-named handoff, or route them through
   separate human-owned ADR PRs; no `docs/adr/**` file enters this Lane 5 diff, and no dependency
   or Save edit precedes its governing signed in-repo ADR.
3. Implement dependency-free tests, DTOs, pure policy, and non-persisting orchestration first.
4. After the dependency/privacy ADR human-signs every runtime Builder/non-Builder value, API-key
   classification, RevenueCat completion/AutoSync architecture, fixed-false in-app messages, native
   logging policy, Blockstore decision, deterministic EDM settings/prompt/mutation policy, the exact
   OpenUPM topology/integrity, the complete Android graph/license/privacy census, and the unrelated package
   baseline is reconciled (or exact non-Lane-5 byte/semantic stability is proven), add the exact
   pins and non-configuration adapter in red-first slices. Configuration and `Configure` wiring
   wait for that same signed field table plus the separate native-prefetch fairness ruling in step
   9. If the ADR requires committed
   `ProjectSettings/AndroidResolverDependencies.xml` or `ProjectSettings/GvhProjectSettings.xml`,
   obtain the exact human scope grant, Lane-1A transfer, and reviewed contract amendment first.
5. Land the separate human-merged, security-reviewed Test Store CI prerequisite PR. Through
   separately scoped human-owned predecessors, close the complete production-graduation checklist:
   rulesets, CI gitleaks/equivalent secret scan, CI dependency/license audit, incident runbook,
   backup restore drill, resolved review-authentication posture, and a pinned Unity 6000.3.16f1 CI
   job that compiles and runs EditMode plus PlayMode without the deferred-editor escape. Attach a
   fresh signed security-checklist walk. Before the first signed build, the human must also resolve
   NEW-Q48 and evidence Play App Signing/upload-key custody, encrypted secrets, a human-approved
   production environment, debug-only PR smoke with zero release secrets, commit-SHA-pinned
   third-party actions, least-privilege permissions, no PR-trigger secrets, hook-plus-CI scanning,
   an app-scoped/rotated service account, and the invariant that no agent-reachable context ever
   holds a publish-, entitlement-mutation-, or install-base-messaging credential. The human must
   record either acceptance of the path-filtered `android-smoke` residual or a per-PR-always
   requirement. None of these `.github/**` or console edits enters Lane 5; each risky-path PR is
   independently security-reviewed and human-merged. Exact `scripts/git-hooks/pre-commit` receives
   the hook-side scanner only through a separate independently reviewed human-authored immutable-
   path predecessor; Lane 5 and other agents never edit it.
6. Have the human disable dashboard Placement fallback for all five locked placements, replace
   the Paywalls-v2 stale complete-edition copy with the signed §8.2 amendment, disable web checkout,
   and attach dashboard plus device evidence for those states and the exact full package/product
   shape of all four Offerings.
   Human owners also attach the Play/RevenueCat product-entitlement exports and complete the six-
   SKU Play license-tester purchase matrix; Test Store is insufficient for this gate.
   An authorized owner also corrects runbook rows 29, 31, 34, 36, 37, 39, and 40. Lane 5 performs
   none of those mutations.
   A human-button predecessor also reconciles or retires the stale conflicting fields in
   `offering_and_placement_map.json` and `entitlement_map.json`.
7. Receive the exact §4.1-as-amended-by-§8.2 `UiStrings` append plus authorized reauthors of both
   `UiCsvDisciplineTests.cs`'s `5 + 7` and `UiCsvUx06Tests.cs`'s `12` exact-row-count pins from a
   predecessor, including both signed Night Harbor content-truth variants; Lane 5 does not create
   a parallel localization source or edit those Lane-1B files. Before activation/merge, receive
   Neon CM-R21 evidence and either validated L901–L910 or the still-current human-attested
   `arriving this week` variant consistently across RCUI and custom UI.
8. After a human-signed ADR-0006 amendment, amend this contract with its signing SHA, exact
   Save source-file list, exact strike-law resolution, NonSubscriptionTransactions/AutoSync and
   UpdatedCustomerInfo recovery with explicit SyncPurchases absent, the exact safe consumable
   local baseline/cursor or explicit no-regrant policy, the mapper-only-provenance prohibition,
   and any separately authorized lower-level/backend source of refund/current-history truth plus
   one canonical nonblank store identity proven equal across direct and recovery paths,
   and EntitlementInfo Verification/source-product/store/channel/sandbox/expiration/renewal policy
   before any persistence, durable reconciliation, or strike work.
   Any external/backend design first needs its separate product/scope grant, architecture ADR,
   owned surfaces, and fresh threat-model/privacy/security review. Also obtain the human-signed
   NEW-Q46 residual-risk acceptance and containing marginal-cost/cross-player invariant before
   durable entitlement code.
9. Obtain the direct human native-startup/foreground Offering ruling: either narrowly accept the
   device/proxy-characterized SDK-internal prefetch/refresh under the phase embargo while retaining
   zero Cat-Metro-initiated request/surface, or approve a reviewed lifecycle remedy that prevents
   it while preserving dark-presentation reconciliation. The human privacy/Data-Safety/reset
   decision must also accept the characterized Google Block Store cloud persistence/recovery or
   approve a native/dependency remedy. Amend the contract/dependency/privacy authority before any
   production Configure or persistent Purchases wiring.
10. Obtain the direct human placement-cache ruling: explicitly accept the pinned memory/disk cache-
   first semantics under No Offering = no UI, or approve an independently reviewed force-current/
   provenance boundary that rejects cached results. Amend the contract/dependency/privacy authority
   before any real placement call or placement-dependent commerce.
11. Obtain the direct human native-Offering/Package ruling and an independently reviewed remedy
   that validates and can reject the exact Android Offering before render and the exact native
   Package/product before billing; or
   obtain an explicit signed renderer/spec/scope supersession. Amend this contract and, for any
   fork/bridge, the dependency/integrity/privacy ADR before primary RCUI code or tests.
   Before any real billing, separately obtain the direct human copy/state ruling for ambiguous
   post-initiation Error and any reviewed native provenance boundary needed to prove a pre-billing
   failure class; otherwise every such Error remains Unknown/unsettled without the no-charge copy
   or retry and the signed ordinary-failure path remains no-code.
12. After that remedy, obtain the direct human A/B ruling for unlatched RCUI Error behavior and
    separately pin the fallback custom-impression `paywallId` or explicitly authorize the Offering-
    only overload; amend this contract before any cross-renderer handoff or custom tracking.
13. Obtain the direct human placement-error observability ruling and amend this contract before
    any placement retry or error-telemetry implementation.
14. Implement remaining authorized tests before behavior, in independently reviewable slices.
15. After the signed dependency/privacy ADR and the direct decisions in step 9, receive a human authorization naming the controlled
    branch-candidate capture build, devices/accounts, and window; it arms evidence collection only.
16. Wait for Lane 8, rebase over every already-landed funnel change including any Lane-11 tail
    wiring, and edit GameRoot last. Candidate wiring may then run only inside the authorized capture
    stage and may not be published or treated as production activation.
17. Capture both CM-R45 happy-path and forced-crash traces, including native Offering lifecycle,
    Blockstore, and logging fault paths, then obtain final human privacy/Data
    Safety approval of the exact traces and frozen data-flow census before production activation or
    merge.
18. Keep `ProjectSettings.asset` byte-untouched until Lane 1A transfers the relevant block and a
    direct human grant plus reviewed contract amendment enumerates every exact serialized field
    name and intended value. Both resolver XML files separately require exact grants plus the ADR/
    contract amendment.
19. Complete all validation and independent code plus billing/security review; dispose every
    finding on the PR record.
20. Do not merge until every production-graduation predecessor, NEW-Q46 acceptance, every NEW-Q48
    credential-non-reachability/smoke-routing/immutable-hook decision and control enumerated in
    step 5, and fresh security-checklist walk above is closed and the human-authored
    `state/mode=production` commit is on the PR base. A mode flip alone is insufficient.
21. HC-25 requires no fresh request under Addendum v2.3; record the required merge-census facts
    on the PR instead.
22. Under the human's direct 2026-08-13 delegation, the lane agent may squash-merge its own PR
    only after every preceding gate is evidenced and no Amendment-1-excluded path is present.
    Never publish to Google Play or mutate production consoles.

## Assumptions

- The human's six-SKU/five-commerce-placement ruling narrows the larger #64 amendment for this
  lane; it does not erase the amendment's three modeled rewarded identifiers.
- Custom renderers for theme, district, Shop, and rewind placements are future Presentation
  work. The post-Level-5 crash fallback is the only custom renderer in Lane 5.
- A missing Placement offering must mean no UI and Cat Metro implements no client current/cached
  fallback. Stock SDK memory/disk provenance is hidden, so no real placement call exists until the
  direct cache-semantics ruling/remedy; this contract does not assume cached data is current.
- The 9.7.0 placement callback's indistinguishable `(null, null)` result is treated only as
  NoOffering/no UI/no retry pending the explicit source-conflict ruling; it is not assumed to be
  a retryable error.
- The UPM-only OpenUPM route has been reproduced in scratch but remains unapproved until the signed
  dependency/privacy ADR accepts its exact topology, integrity, unsigned-package, license, and
  Android graph posture; failure stops work rather than prompting a substitute package.
- `useRuntimeSetup` is not assumed to suppress native Offering traffic. Production Configure and
  persistent Purchases wiring remain absent until the direct phase-embargo ruling/remedy; this
  contract does not silently narrow “no offer fetched” to “no Cat Metro API call.”
- Google Block Store cloud backup/recovery of the anonymous purchased-user ID is not assumed to be
  acceptable or disabled. It remains a human privacy/Data-Safety/reset decision before Configure.
- EDM package import is not assumed deterministic: measurement is disabled, both automatic
  resolver switches remain off, and the signed `UseProjectSettings`/prompt policy plus zero pre-
  wrapper mutation proof is required before branch resolution.
- No SDK key value is committed in a scene, prefab, resource, or source file. The public Android
  SDK key and Test Store key are injected through the approved build configuration, with the
  Test Store key impossible to select in a release build; CI proof is the separately merged
  risky-path prerequisite.
- The Save-file grant is void at freeze: no qualifying signing SHA or file list exists, so the
  authorized `Application/Save/**` set is empty.
- The unlatched-Error A/B behavior is deliberately not assumed; both automatic handoff and
  fail-closed production wiring remain absent until the direct ruling and contract amendment.
- Stock Android RevenueCatUI is deliberately unreachable because it re-resolves by ID/current
  before render; no native-Offering remedy or replacement-renderer authority is assumed.
- A non-cancel/non-pending Error after purchase invocation is never assumed to mean no charge. It
  remains Unknown/unsettled and retains reconciliation until a direct human copy/state ruling and
  reviewed provenance boundary say otherwise.
- Complete NonSubscriptionTransactions history is never assumed safe to fulfill. A fresh/lost/
  pruned ledger grants zero from it until a signed safe local baseline/cursor or no-regrant policy
  exists. A native public mapper cannot supply refund/currentness or canonical identity. Any
  lower-level/backend/external authority remains out of scope until its additional human scope,
  architecture, ownership, dependency, threat-model/privacy, and security gates land.
- Indefinite offline entitlement residual risk and release-credential custody are not assumed
  accepted. Human-signed NEW-Q46 and NEW-Q48 records/evidence remain mandatory before the governed
  durable code, first signed build, or merge as applicable.
- There are no additional assumptions. Any newly discovered ambiguity that changes behavior,
  persistence, catalog, pricing, grant authority, or scope is a stop condition.

## Stop conditions

1. The dependency ADR is absent where a dependency diff would begin, or the signed ADR-0006 plus
   explicit contract amendment is absent where persistence/strike work would begin, or that
   amendment does not resolve the once-ever/two-strike contradiction exactly.
2. Any implementation or test would select unlatched-Error handoff versus fail-closed behavior
   before the direct human A/B ruling and reviewed contract amendment.
3. Any implementation or test would claim placement-error retries/telemetry before the direct
   source-conflict ruling and reviewed contract amendment, or would retry the indistinguishable
   `(null, null)` NoOffering result.
4. The requested package versions or exact source APIs cannot be resolved and reproduced, or the
   dependency/privacy ADR has not explicitly disposed every Builder/non-Builder setting, API-key
   classification, logging policy, completion/sync/IAM behavior, Blockstore, EDM mutation policy,
   unsigned-package/integrity posture, complete Android graph, privacy impact, and license census.
5. A requirement needs a seventh live SKU, sixth commerce placement, experiment activation,
   hard-coded price, or any custom renderer other than an explicitly granted post-Level-5
   crash fallback.
6. A live rewarded call is needed before NEW-Q45/device gates and the appropriate human ADR
   supersession are complete.
7. A persistence requirement appears to need PlayerPrefs, an extra file, an undefined field,
   or unapproved migration semantics.
8. GameRoot funnel predecessors have not landed, or concurrent work touches the funnel output.
9. Android ProjectSettings ownership has not transferred from Lane 1A, or a direct human grant and
   reviewed contract amendment has not enumerated each exact serialized field name and intended
   value. A categorical gradle/proguard block name is insufficient.
10. Package resolution produces duplicate RevenueCat/EDM4U payloads, Unity IAP, more than one
    BillingClient lineage, a resolved coroutines/activity/fragment version that differs from the
    frozen selected graph without a reviewed amendment, any unrelated URP/Test Framework/UGUI or
    other package churn, an omitted selected lifecycle/core/profileinstaller/startup/Places node,
    a non-exhaustive per-AAR manifest fixture, a missing Amazon/Billing/DataTransport/AndroidX/Play-
    Services manifest/R8/license disposition, or
    another unresolved license/security issue; or it needs ungranted
    `ProjectSettings/AndroidResolverDependencies.xml` or `ProjectSettings/GvhProjectSettings.xml`.
11. Any existing test must be weakened or removed to reach green.
12. Production mode, the fresh production security-checklist closure, human-signed NEW-Q46
    residual-risk acceptance, human-resolved NEW-Q48 release-custody controls, real pinned-Unity CI
    compile/EditMode/PlayMode evidence, independent code review, independent billing/security
    review, or required human/device judgment is missing at merge time. An editor-deferred shell
    pass fails.
13. The separate Test Store CI prerequisite is not security-reviewed and human-merged before
    Lane 5 merge; the human-owned ruleset/secret-scan/dependency-audit/incident-runbook/backup-
    restore/review-auth/real-Unity-CI graduation predecessors are incomplete; NEW-Q48 lacks Play
    App Signing/upload-key custody, encrypted secrets, human-approved release environment, debug-
    only zero-release-secret PR smoke, SHA-pinned actions, least privilege, PR-secret isolation,
    scoped/rotated service-account evidence, the invariant that no agent-reachable context holds a
    publish/entitlement-mutation/install-base-messaging credential, or the human's path-filtered-
    `android-smoke`-residual versus per-PR-always decision; exact immutable
    `scripts/git-hooks/pre-commit` lacks the separately reviewed human-authored hook scanner or CI
    scanning is absent; or any
    `Application/Save/**` file appears before its signed scope amendment.
14. Any locked Placement still has a dashboard current/cached fallback, web checkout remains
    enabled, any Offering's full package/product shape differs from the frozen allowlist, or
    forced-null device evidence cannot prove the human-ruled No Offering = no UI behavior.
    The stop also holds if any live product type/active state or positive/negative entitlement
    attachment differs, the experiment SKU is active, or the six-SKU Play sandbox matrix is absent.
15. The stale dashboard-runbook fallback/copy rows, matching amended RCUI dashboard/device proof,
    corrected/retired machine-readable JSON fields, canonical `UiStrings` plus both named exact-
    count prerequisites, Neon CM-R21 evidence, or one truthful Night Harbor content/copy state has
    not landed through its authorized owner before Lane 5 merge.
16. The Lane 5 PR gains an Amendment-1-excluded path; in that case the agent merge route closes
    and the human must merge.
17. Any custom-impression call would be authored before the exact fallback paywall-ID/overload
    ruling, or any CM-R53 reset/anonymous-ID rotation behavior would be authored before its human
    orphaning/Data-Safety decision and explicit scope assignment.
18. Release-AAB logcat exposes raw RevenueCat JSON, anonymous identity, transaction/purchase
    token, or price and no human-approved dependency mitigation/ADR amendment has landed.
19. Runtime configuration would be authored before the signed field-by-field Builder/non-Builder
    decision; a blank/bare-prefix/whitespace-only-suffix/wrong-prefix/environment-mismatched key can
    reach BuildPlayer or Configure, or the validated trimmed value differs from the forwarded value; a
    second/renamed/reconfigured Purchases object or listener appears; automatic device-identifier
    collection or any other field would be inherited by omission; RevenueCat does not own purchase
    completion; `AutoSyncPurchases` is not true; automatic/explicit in-app messaging is reachable;
    inert `ObserverMode` is read as authority; `proxyURL` is non-null; native log policy is unsigned;
    or a callback implementation
    posts an already-main-thread RevenueCatUI listener latch behind its terminal Task result.
20. Compiled Lane 5 code references RevenueCat AdTracker/TrackAdRevenue/AdRevenueData, formats
    `StoreProduct.Price`, logs/interpolates a raw SDK object, or creates any deferred analytics or
    rewarded-ad adapter seam.
21. The human-controlled-capture authorization is absent where candidate Bootstrap wiring or proxy
    evidence would begin; either CM-R45 happy-path or forced-crash trace is missing; or final human
    privacy/Data Safety approval of the exact capture, dependency/manifest data-flow inventory,
    retention/deletion, privacy notice, age/consent, and Play disclosures is absent at production
    activation or merge.
22. Any stock `PaywallsPresenter.Present`, `PurchasePackage`, `PurchaseProduct`, primary RCUI
    orchestration, or real custom purchase row/call is authored before a direct human native-
    Offering/Package ruling and an independently reviewed exact-Offering pre-render plus exact-
    Package/product pre-billing validation/veto remedy; or the custom fallback is promoted to
    primary without an explicit signed renderer/spec/scope supersession. A fork or bridge without
    amended dependency/integrity/privacy authority and a new independent security review also
    stops the lane.
23. Any real `GetCurrentOfferingForPlacement` or placement-dependent commerce is authored before
    the direct human memory/disk-cache semantics ruling, or before a chosen native force-current/
    provenance remedy is independently reviewed and proves cached/disk results are rejected.
24. Whole-Offering validation accepts a non-`CUSTOM` package; a missing/mismatched Package or
    StoreProduct PresentedOfferingContext; inconsistent Offering/placement/targeting/revision/rule-
    ID context across packages; or invents a targeting value.
25. Durable reconciliation trusts an Active entitlement key without exact EntitlementInfo source-
    product and signed Verification/store/channel/sandbox/expiration/renewal policy; uses
    AllPurchasedProductIdentifiers or complete NonSubscriptionTransactions history for consumable
    fulfillment; permits fresh/lost/pruned-ledger historical regrant; fails to require the signed
    local baseline/cursor or explicit no-regrant policy; treats mapper-only fields as refund/
    current-history or canonical-identity authority; implicitly adds an external/backend ledger
    without its separate product/scope, architecture, ownership, threat-model/privacy, and security
    gates; accepts a null/empty/whitespace or cross-path-unequal transaction identity; lets an
    authorized locally attributable transaction bypass sole TryGrant; invokes callbackless
    SyncPurchases; or starts before ADR-0006 signs AutoSync/UpdatedCustomerInfo recovery and
    retention/refund semantics or before the human-signed NEW-Q46 acceptance and containing
    marginal-cost/cross-player invariant.
26. Any explicit `SyncPurchases` overload is invoked under the RevenueCat-completed purchase
    architecture, or a different MyApp/migration design is inferred without the direct human/source
    override, mutually exclusive completion architecture, acknowledgement/refund scope, ADR, and
    reviewed contract amendment.
27. Production `Configure` or persistent Purchases wiring is authored before the human resolves
    native cold/foreground Offering fetch/cache/predownload against the phases-1–4 embargo and the
    selected behavior is characterized on device/proxy under a reviewed contract amendment.
28. Configure, restore, or purchase can run before the human explicitly accepts Google Block Store
    cloud backup/recovery/aliasing of the anonymous purchased-user ID and its CM-R53/Data-Safety
    effect, or before an authorized native/dependency remedy is independently reviewed.
29. EDM imports/resolves before the signed `UseProjectSettings`/prompt policy is applied with
    measurement disabled and both automatic resolver switches false; any import/build-triggered
    resolve or ungranted settings/XML mutation occurs before the one explicit synchronous wrapper.
30. A post-initiation non-cancel/non-pending Error is classified as ordinary Failure, uses “you were
    not charged,” clears reconciliation, or enables retry before the direct human copy/state ruling
    and an independently reviewed provenance boundary proves an authorized pre-billing class.
31. An eight-second timeout or late-callback discard is applied to purchase, restore, CustomerInfo,
    or reconciliation rather than only Offering Fetching; a detached commerce caller prevents the
    definitive callback from entering idempotent reconciliation.
32. The exact `CatMetro.Integrations.RevenueCat` assembly is renamed, references an internal Cat
    Metro assembly other than Services, gains a product consumer other than Bootstrap, or is
    referenced by a test assembly beyond the authorized EditMode asmdef; or any required root-folder
    `.meta` above is absent/unlisted while an ungranted sibling metadata path enters the diff.

## Status log

- 2026-08-11 — PR #70 merged; fresh `origin/main` anchor and all five risky-path classifications
  verified with negative controls.
- 2026-08-11 — Lane branch fast-forwarded to `291dc590`; no authored diff remained.
- 2026-08-11 — review draft independently audited; Application/save, parity-test, fallback, and
  build-key/CI conflicts routed to the human with no implementation diff.
- 2026-08-11 — four human rulings recorded. `Application/Purchases/**`, exact parity exception,
  new-only fallback directories, and exact build injector granted. Save grant audited void:
  current ADR Proposed, qualifying signing SHA none, exact Save source-file set empty.
- 2026-08-11 — exact 9.7.0 source review characterized the listener-qualified Error result; review
  exposed dashboard fallback drift, the once-ever/two-strike contradiction, and canonical-string
  prerequisites. Each is now an explicit external prerequisite or human-resolution stop, never
  an invented implementation path.
- 2026-08-13 — branch fast-forwarded to current `origin/main` at `fac3e354`; merged Addendum
  v2-v2.3 re-audited. HC-25 fresh-word requests were removed, the latest direct human instruction
  restored Lane 5 agent merge authority after all governing gates, and the unlatched-Error
  dual-visible ambiguity was converted from an inferred handoff into an explicit no-code A/B stop.
- 2026-08-14 — the mandated pre-first-commit fetch advanced `origin/main` to `d10509d` through PR
  #84, whose complete two-file delta touches only DailyTools/DailyPipeline posture text. Lane 5
  fast-forwarded; no ground-truth, ownership, API, package, monetization, or gate input changed.
- 2026-08-14 — exact RevenueCat 9.7.0/OpenUPM/Android source audit added the singleton lifecycle,
  main-thread-inline listener ordering, full result/reconciliation maps, artifact integrity,
  transitive dependency/license census, zero ad-telemetry seams, and human privacy/Data Safety
  activation gate. No dependency, configuration, product, or persistence diff was authored.
- 2026-08-14 — independent reviewers reproduced two billing-critical Android TOCTOU boundaries:
  stock RCUI cache-first re-resolves Offering/current before render, and stock `PurchasePackage`
  cache-first re-resolves a
  package/product before billing. Both paths are now explicit no-code stops pending a human-approved
  native validation/veto remedy; CM-R45 evidence uses a separately authorized candidate capture
  followed by final human approval. No SDK call or product diff was authored.
- 2026-08-14 — final pinned-source pass exposed hidden memory/disk placement provenance, explicit
  CUSTOM/context allowlist fields, and transaction-/EntitlementInfo-level CustomerInfo authority.
  Real placement calls and all durable grants now stop for direct human cache/ADR decisions;
  complete NonSubscriptionTransactions history and AllPurchasedProductIdentifiers both grant zero
  consumables absent a later specifically signed safe authority. No placement, sync, billing, or
  Save call was authored.
- 2026-08-14 — independent native/dependency cross-check exposed cold/foreground Offering
  prefetch despite runtime setup, fail-open key-prefix handling, unsupported explicit Sync under
  RevenueCat completion, automatic in-app-message/logging surfaces, Blockstore cloud identity
  persistence, the full Billing/DataTransport/Amazon graph and rules, selected coroutines 1.7.3,
  and EDM pre-wrapper mutation defaults. Each is now an explicit signed-decision, evidence, or
  no-code stop. No SDK, package, configuration, or Bootstrap diff was authored.
- 2026-08-14 — exact-hash review then caught bare-prefix credentials, charged-but-Error ambiguity,
  unsafe complete-history consumable replay, an overbroad eight-second timeout, additional selected
  AndroidX/Play Services manifest surfaces, exact asmdef/root-meta scope, and open production-
  graduation/real-Unity-CI gates. The contract now fails closed on each and requires the matching
  signed authority/evidence; no product/dependency diff was authored. Clean baseline remained green:
  `check.sh`, `build.sh`, and `test.sh` 17/17, including EditMode 963/963 and PlayMode 137/137.
- 2026-08-14 — the next exact-hash review proved native public history lacks refund/currentness,
  direct Play order IDs differ from RevenueCat/backend history IDs, and rewind success lacked a
  nonblank-ID predicate. It also surfaced human-only NEW-Q46/NEW-Q48 gates, implicit external-
  backend scope, and an unenumerated ProjectSettings category. History now grants zero unless a
  separately authorized safe authority supplies canonical identity and provenance; mapper-only,
  backend-scope, credential-custody, offline-residual, and ProjectSettings ambiguities are explicit
  no-code/merge stops. No product, package, Save, workflow, or ProjectSettings diff was authored.
