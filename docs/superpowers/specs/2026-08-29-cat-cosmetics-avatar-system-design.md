# Cat Cosmetics and Avatar System Design

**Date:** 2026-08-29

**Task:** 13 — Cat cosmetics and avatar system

**Base:** `main` at `abe4fa6` (PR #115 merge)

**Status:** Approved in chat; written review pending

## Outcome

Cat Metro will let players choose a profile cat, preview named cosmetics on that cat, acquire them through an explicit route, and equip one outfit, one accessory, and one avatar frame per cat. The selected cat and desired loadouts survive app restarts through the existing atomic `SaveStore`.

Paid purchases, promotional grants, and rewarded-ad leases remain indistinguishable downstream. Cosmetics asks only `PurchaseService.IsUnlocked(entitlementId)` and never creates a second paid-ownership ledger.

The conductor coat is the only ship-blocking SKU. It already has Play and RevenueCat configuration and is sufficient for the Shipaton eligibility gate. Every other item is optional launch content and appears only when its actual asset and acquisition route are ready.

## Product principles

- No gacha, paid randomness, duplicate pulls, purchased intermediary currency, or indirect paid randomness.
- Sell named items. Show the complete look before purchase and state exactly how to obtain it.
- Cosmetics never alter gameplay statistics.
- No countdown pressure, fake discounts, purchase counters, or rarity/power tiers.
- No launch SKU exceeds $2.99.
- Profile portraits and the HUD portrait seam are the primary display surfaces. Board cats are too small to carry the product.
- Generated assets are admissible only with paid-tier commercial provenance.

## ACECRAFT teardown

ACECRAFT is useful as a character-showcase reference and unsuitable as Cat Metro's economy model.

Its catalogue is pilot-first: named pilots lead to pilot-specific A/S-tier outfits, with separate profile avatars, backdrops, card collections, passes, gear, and event rewards. Official release notes describe outfit activation in an archive, switching from the Pilot interface, and displaying owned pilots or outfits in the lobby. The US storefront exposes general packs from $0.99 to $19.99, a $4.99 monthly pass, $7.49/$19.99 growth funds, and a $14.99 Navigator Pass. Community screenshots place specific outfit offers around $15–25 under promotional framing. Google Play labels the game as containing random items.

Sources:

- [ACECRAFT App Store listing and release notes](https://apps.apple.com/us/app/acecraft-sky-hero/id6746043251)
- [ACECRAFT Google Play listing](https://play.google.com/store/apps/details?id=com.vizta.weflyam&hl=en_US)
- [Community outfit offer screenshot and discussion](https://www.reddit.com/r/Acecraft/comments/1oes53q/2000_people_did_not_buy_this_dont_fall_for_it/)

Copy the character-first gallery, explicit owned/equipped states, one-tap equipping, and persistent high-frequency showcasing. Reject cosmetic stat boosts, rarity tiers, paid pulls, currency sprawl, expensive outfit anchors, timed pressure, and social-proof counters. Avatar frames were not found as an ACECRAFT category; they are an original Cat Metro addition.

## Scope

### In scope

- Services-level cosmetics catalogue, access resolver, profile state, loadouts, events, and runtime locator.
- Application-level v2-to-v3 migration and `SaveStore` persistence adapter.
- Presentation-level profile selection, Wardrobe catalogue, complete preview, equipping, frames, Home portrait, and reusable portrait component.
- Typed mounting seam for the HUD-owning lane.
- Minimal Bootstrap composition using the already-open production `SaveStore`.
- Launch catalogue data, availability filtering, diagnostics, tests, device proof, pricing/setup documentation, and generated-content plan.

### Out of scope

- Changes to `BoardView.cs`, `BoardSceneLook.cs`, `BoardPropDecorator.cs`, `ToyTrainView.cs`, `ToyTrackMeshBuilder.cs`, or `WavePreviewStrip.cs`.
- A second payment, entitlement, rewarded-ad, or save path.
- New store uploads or dashboard mutations performed by the agent.
- Paid or earned random rewards.
- Depending on 3D board cosmetics for product visibility.
- New gameplay-stat effects, currencies, progression hooks, or expiring streaks.

## Chosen architecture

```text
Task 1 product catalogue ──> PurchaseService / EntitlementLedger
                                      │ IsUnlocked
                                      ▼
Cosmetics catalogue ───────> CosmeticProfileService <──── SaveStore adapter
                                      │                     atomic v3 save
                              portrait snapshots
                         ┌────────────┼────────────┐
                         ▼            ▼            ▼
                     Wardrobe       Home      typed HUD seam
                         └────────────┴────────────┘
                                      │
                            CosmeticPortraitView
```

The cosmetics catalogue is independent of the purchase catalogue. A visual item may be starter-owned, earned, entitlement-backed, authored before its store product is live, or not sold at all. Authoring a row does not make it player-visible: the candidate-not-slot projection below admits it only after its asset and acquisition route are ready. Folding these concepts into `PurchaseCatalog` would make RevenueCat metadata the visual source of truth and would not represent free cats or deterministic earned items cleanly.

Unity prefabs or ScriptableObjects are not the system of record. The catalogue is data-driven and engine-free so real rows, mutations, save migrations, ownership, and availability can be tested headlessly.

## Components

### `CosmeticCatalog`

Parses and validates cat and item rows without throwing. It owns identifiers, slots, compatibility, display keys, acquisition metadata, portrait asset identifiers, ordering, and structured problems. Asset and provenance inventories enter as injected identifier sets, keeping admission deterministic and engine-free rather than making the parser inspect Unity objects or the filesystem.

Public diagnostics include:

- `AdmittedRowCount`
- `RejectedRowCount`
- `Problems`

`AdmittedRowCount` counts item rows that pass schema, compatibility, acquisition, portrait-asset, and provenance admission. `RejectedRowCount` counts submitted item rows that fail any of those checks. Cat admission is reported separately in the one-line boot summary.

No parser result silently claims a full catalogue. Boot emits one line containing admitted rows, rejected rows, admitted cats, asset-ready rows, runtime-visible rows, purchasable rows, and conductor-gate readiness.

### `CosmeticProfileService`

Owns:

- selected profile cat;
- per-cat desired outfit/accessory/frame identifiers;
- starter and deterministically earned access;
- validation and disk-first mutations;
- immutable effective portrait snapshots;
- `Changed` notifications.

The service preserves desired IDs even when they are unknown to the current catalogue or inaccessible. It excludes them from the effective portrait instead of deleting forward-compatible or temporarily inaccessible intent.

### `CosmeticAccessResolver`

Resolves access through exactly three routes:

- `starter`: declared in the cosmetics catalogue;
- `earned`: ID present in the local earned set;
- `entitlement`: `PurchaseService.IsUnlocked(entitlementId)`.

There is no `WasPurchased`, paid/ad discriminator, locally cached store grant, or alternate entitlement API.

### `ICosmeticProfilePersistence`

A Services-level boundary for loading and durably replacing a complete profile snapshot. Its Application implementation uses the existing concrete `SaveStore`, including `TryCommitAtomic`, and no second file or `PlayerPrefs`.

### `ICosmeticPortraitSource`

Provides an immutable portrait snapshot for a cat and a `Changed` event. It contains base-cat visual tokens and only the currently effective outfit, accessory, and frame layers. The HUD lane consumes this interface and mounts the same `CosmeticPortraitView` used by Wardrobe and Home.

### `CosmeticPortraitView`

The single UGUI portrait vocabulary. It renders the cat, outfit, accessory, and frame from a snapshot. Wardrobe preview, equipped Wardrobe portrait, Home, and the future HUD mount do not recreate those layers independently.

### `CosmeticRuntime`

Mirrors `PurchaseRuntime`: `Current` is never null, tests can reset it, and Bootstrap installs the persistent production service before composing Home or Wardrobe. The degraded default remains usable in memory with starter content if save startup fails.

## Catalogue model

The cosmetics resource has its own schema version. Prices are absent by design.

```json
{
  "schemaVersion": 1,
  "cats": [
    {
      "id": "red_tabby",
      "displayNameKey": "cosmetics.cat.red_tabby",
      "portraitAssetId": "cat.red_tabby",
      "starter": true
    }
  ],
  "items": [
    {
      "id": "outfit_conductor",
      "slot": "outfit",
      "displayNameKey": "cosmetics.item.outfit_conductor",
      "portraitAssetId": "outfit.conductor",
      "acquisition": "entitlement",
      "entitlementId": "outfit_conductor",
      "productId": "cm_outfit_conductor",
      "compatibleCatIds": ["red_tabby", "blue_siamese", "yellow_longhair"]
    }
  ]
}
```

The store remains authoritative for localized price and product availability. A locked entitlement-backed item is purchasable only when `PurchaseService.TryGetPrice(productId, out price)` returns a known price. The cosmetics catalogue never contains `$1.99`, a currency code, or a copied display-price string.

## Save schema v3

PR #115 established the unified public v2 schema in `SaveSchemaV2.cs`. Cosmetics does not edit or reinterpret it.

`SaveDefaults.SAVE_VERSION` advances to 3. A fresh payload adds this object below `profile`:

```json
"cosmetics": {
  "selectedCatId": "red_tabby",
  "earnedCatIds": [],
  "earnedItemIds": [],
  "loadouts": [
    {
      "catId": "red_tabby",
      "outfitId": "",
      "accessoryId": "",
      "frameId": ""
    }
  ]
}
```

`SaveSchemaV3.MigrateFromV2` is registered after the existing v1-to-v2 step in `MigrationTable.CreateDefault()`.

The v2-to-v3 migration:

- validates only the root `profile` container and `profile.cosmetics` when present;
- adds the default cosmetics object only when absent;
- preserves every existing value and unknown sibling;
- returns null for a present malformed owned container;
- never reads, normalizes, clones selectively, or rewrites `entitlements` or `localLeases`.

A migration test deep-compares the complete `localLeases` token before and after v2-to-v3.

## Disk-first mutation law

Selecting a cat or equipping an item follows this order:

1. Validate the requested cat, slot, compatibility, and current access.
2. Build a complete candidate profile without changing observable service state.
3. Clone the current save payload and place the candidate below `profile.cosmetics`.
4. Swap the candidate payload into `SaveStore` and call `TryCommitAtomic`.
5. On refusal or exception, restore the previous payload identity and leave the service snapshot unchanged.
6. Only after durable success publish the new in-memory profile and raise `Changed`.

A crash after disk commit but before publication is safe because the committed candidate loads on the next boot. A failed commit never creates a look that exists only in RAM.

`GameRoot.OnApplicationPause(true)` also invokes the existing save pause-budget path. Selection and equipment are still committed immediately; pause commit is a durability backstop, not the primary write.

## Entitlement lifecycle

The saved loadout expresses intent, not authority.

At render time, an entitlement-backed layer is effective only when `IsUnlocked` is true. If a rewarded lease expires, a refund removes a store grant, or RevenueCat reports a lapse, the portrait omits that layer while the saved ID remains unchanged. When access returns through purchase, restore, promotion, or another valid lease, the same saved loadout displays the layer again without another save mutation.

Before RevenueCat supplies an authoritative or cached snapshot, no paid access is fabricated. The system also does not clear the desired ID during that window. `EntitlementLedger.Changed` triggers snapshot recomputation, never save cleanup.

RevenueCat documents that `CustomerInfo` is cached between launches and the default cached-or-fetched policy returns cached information while offline. That supports, but does not replace, the required device proof: [RevenueCat caching documentation](https://www.revenuecat.com/docs/test-and-launch/debugging/caching).

## Wardrobe and portrait presentation

The Wardrobe is character-first:

- a large selected-cat portrait shows the complete preview;
- a compact selector changes the profile cat;
- Outfit, Accessory, and Frame tabs show a compact, gap-free list;
- tapping a locked item previews it on the large portrait without equipping it;
- cards show exactly one route/state: `Equipped`, `Owned`, localized price, `Watch to borrow`, or a deterministic earning instruction;
- temporary access shows time remaining without naming its source;
- Restore Purchases remains visible;
- the Home Wardrobe entry uses the same selected portrait.

Preview state is Presentation-only and is never persisted. Equip requires current access and a successful atomic commit.

The rewarded route is a typed request seam to Task 11. Cosmetics may show `Watch to borrow` only when the shared purchase service and rewarded-placement provider say the route is currently offerable. Only the ad lane handles network callbacks; only its verified reward callback calls the shared grant path.

No protected board or HUD file changes. The HUD owner receives `ICosmeticPortraitSource` and mounts `CosmeticPortraitView` directly instead of reproducing ears, face, outfit, accessory, or frame layers.

## Candidate-not-slot availability

Catalogue order does not reserve visible holes. A row enters the Wardrobe projection only when:

- its portrait asset and provenance were admitted;
- it is compatible with the selected cat; and
- it is already accessible, has a known localized store price, has a deterministic earning route, or has a currently available rewarded route.

Consequences:

- A missing asset omits the row entirely.
- A locked item with no live store product and no other route is omitted.
- An already accessible item remains visible if its store product later disappears.
- Hidden rows do not produce placeholders, disabled purchase cards, blank spaces, or player-facing errors.
- Structured diagnostics still explain every static rejection and summarize dynamic filtering in one log line.

## Launch catalogue and staging

### Price ladder

- Outfits: $1.99 each.
- Accessories: $0.99 each.
- Avatar frames: $0.99 each.
- Stationmaster Set: $2.99.
- No launch SKU above $2.99.

The proposed full set totals about $6.96 when buying the Stationmaster bundle plus every remaining named item, close to the research's approximately $7.26 casual D90 ARPPU and deliberately far from ACECRAFT's rejected $15–25 outfit positioning.

Dashboard prices remain human configuration. The app shows only store-localized prices.

### Delivery order

1. Profile/save system and reusable portrait vocabulary.
2. Three starter cats with no store products: Red Tabby, Blue Siamese, Yellow Longhair.
3. Brass and Lantern avatar frames as 2D-only assets.
4. Existing conductor-coat portrait, localized price, purchase, restore, persistence, and offline-cold-boot evidence.
5. Remaining 2D accessories.
6. New 3D outfit/accessory generation only after the conductor gate is proven.

The conductor coat alone blocks release. The catalogue shipped around September 20 is the set whose assets, provenance, product routes, and evidence are genuinely complete.

## Generated-content plan and licensing

Tripo and Meshy output remains separate from project-owned presentation correction.

- Generate cats, outfits, and accessories as modular sources.
- Normalize scale, forward axis, attachment sockets, and mesh bounds in Presentation.
- Never edit provider-delivered source bytes to hide inconsistency.
- Require a portrait layer for every shippable item because portraits are the commercial surface.
- Treat 3D board counterparts as optional later enhancement.
- Generate deterministic named variants rather than random rewards.

Every admitted generated asset records:

- provider;
- paid plan tier;
- provider task ID;
- exact prompt;
- generation timestamp;
- source hash;
- derivative hashes and transformation chain;
- custody/backup location;
- applicable terms evidence.

Paid tiers permit commercial distribution; this settled licensing position is not reopened by implementation. Missing provenance makes the asset inadmissible even if it renders correctly.

## Failure behavior

- Malformed catalogue: reject affected rows, preserve counts/problems, and continue boot.
- Missing visual asset or provenance: omit the row quietly.
- Missing live store product: do not present a purchase action or gap.
- Save commit refusal/exception: retain the prior selected cat and look; show a non-blocking status.
- Cancelled or failed purchase: retain the existing look.
- Expired lease/refund: omit only the inaccessible effective layer; retain saved intent.
- Access restored later: show the saved layer again automatically.
- RevenueCat unreachable without cache: fabricate no ownership and erase no intent.
- Unknown or incompatible saved ID: preserve raw save data but exclude it from effective rendering.
- Save startup unavailable: use a starter-only in-memory profile; campaign remains playable.

## Bootstrap integration and Task 19 handoff

The production base already creates and loads one `SaveStore` in `GameRoot`. The cosmetics delta is limited to:

1. one `CosmeticProfileService` field;
2. creation from the already-loaded `_saveStore`, with a degraded fallback when `_saveStore` is null;
3. installing `CosmeticRuntime` before `ComposeScreenFlow`;
4. passing the service to Wardrobe and binding the Home portrait;
5. one pause-budget commit call inside the existing `OnApplicationPause(true)` branch;
6. teardown/unsubscription if the runtime owns subscriptions.

No second `SaveStore`, storage root, file, or purchase service is created.

After implementation, the branch records the literal zero-context diff produced for `GameRoot.cs`, lists every added symbol, and gives that artifact to Task 19. Line numbers alone are insufficient because contested merges move them.

## Validation

### Pure and EditMode

- Parse the real cosmetics catalogue and assert exact admitted/rejected counts.
- Mutate duplicate IDs, unknown slots, incompatible cats, invalid routes, missing assets, and missing provenance; assert affected rows are rejected and counts change.
- Assert no catalogue row contains a price or random acquisition route.
- Prove runtime projection compacts missing asset/product rows without holes.
- Prove starter, earned, paid, promotional, and rewarded access converge on the resolver's one effective result.
- Prove selecting/equipping commits before publication and rolls back payload identity and service state on refusal and exception.
- Prove fresh v3, v2-to-v3, and v1-to-v2-to-v3 paths.
- Deep-compare `localLeases` and unknown siblings across v2-to-v3.
- Load a saved premium layer, remove access, verify the effective snapshot omits it while saved payload remains unchanged, restore access, and verify the layer returns without a save.

### Presentation and PlayMode

- Select every starter cat.
- Preview a complete locked look without equipping it.
- Equip owned outfit/accessory/frame layers and persist them.
- Render both 2D frames.
- Remove optional assets/products and prove the card list compacts with no null errors or gaps.
- Prove Home and Wardrobe consume the same portrait snapshot.
- Mount `CosmeticPortraitView` through the typed HUD seam using a fake host.
- Capture phone-sized plain, previewed, purchased, lapsed, and restored portraits.
- Inspect painted pixels/layers so a field claiming `Equipped` cannot pass while the coat or frame is invisible.
- Verify screen-space layout after the necessary frame boundary before asserting geometry.

### Commerce and device artifact

The conductor gate requires all of:

- admitted portrait asset;
- known localized price;
- successful RevenueCat sandbox purchase;
- visible equipped coat;
- atomic reload of selected cat/loadout;
- visible Restore Purchases path;
- actual restored state.

Offline cold boot uses the verified target device only. First run `adb devices -l` and confirm the Pixel 9 Pro model/serial before any device command. Then:

1. purchase and equip the conductor coat online;
2. kill the app;
3. enable airplane mode;
4. relaunch;
5. capture RevenueCat's cache read-back (`Vending customerInfo from cache` or the pinned 9.9.0 equivalent);
6. capture the coat visibly painted without a network response.

The same proof should be repeated on iOS TestFlight before release when an iOS device is available. Any missing iOS proof is reported plainly.

### Regression

- `bash scripts/check.sh`
- full repository test script
- locked-mode dotnet restore/build/tests against the linked Unity sources
- Unity EditMode without `-quit`
- Unity PlayMode without `-quit`
- dev APK and target-device pass

No store upload is performed.

## Acceptance criteria

The feature is complete when:

1. A player can choose one of three starter profile cats.
2. The chosen cat and desired per-cat loadouts survive restart through the existing save file.
3. Wardrobe previews complete named looks before purchase.
4. Equip succeeds only for starter, earned, or `IsUnlocked` content and only after atomic persistence.
5. A lapsed entitlement hides its layer without erasing saved intent; restored access returns it.
6. Missing optional products/assets collapse out of the Wardrobe without errors or gaps.
7. Catalogue admitted/rejected counts and the one-line runtime summary diagnose sparse content.
8. Home and the typed HUD seam consume the same reusable portrait vocabulary.
9. The conductor coat completes a real RevenueCat sandbox purchase and remains visibly equipped after an offline cold boot from cached `CustomerInfo`.
10. v2-to-v3 preserves `localLeases` and unknown data exactly.
11. Protected lane files remain untouched.
12. Task 19 receives the literal zero-context `GameRoot.cs` hunk and unverified-evidence list.
