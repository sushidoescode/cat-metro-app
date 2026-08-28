# RevenueCat purchase setup

This is the human-only setup for the one Shipaton eligibility purchase. The shipped path is:

`cm_outfit_conductor` store product → `outfit_conductor` RevenueCat entitlement → the fixed
profile cat visibly gains the Conductor's Coat and hat in Wardrobe.

The code and SDK are already wired. This runbook does not create accounts, change a store
dashboard, build a release, or upload anything.

## 1. Create the store product

Create the same product identifier in each store that will ship:

- Product identifier: `cm_outfit_conductor`
- Type: non-consumable / one-time product
- Display name: `Conductor's Coat`
- Intended launch price: USD 1.99, with the store's normal regional pricing

For Android, the Unity application identifier currently is `com.catmetro.game`. For iOS, set and
freeze the final bundle identifier in the release lane before creating the App Store Connect app;
this branch does not currently define one.

Complete all store metadata and activate/submit the product with the app. A product left in a
draft or unavailable state will not appear in RevenueCat offerings on a real device.

## 2. Configure RevenueCat

In a human-owned RevenueCat project:

1. Add the Apple and/or Google Play app using the exact store bundle/application identifier.
2. Connect the store credentials requested by RevenueCat.
3. For the Apple app, generate an App Store Connect **In-App Purchase Key** and configure it in
   RevenueCat. This is required because the shipped SDK configuration explicitly uses StoreKit 2.
4. Import `cm_outfit_conductor` from each connected store.
5. Create entitlement `outfit_conductor`.
6. Attach each platform's `cm_outfit_conductor` product to that entitlement.
7. Create offering `cosmetics`.
8. Add a custom package (for example `conductor_coat`) containing `cm_outfit_conductor` to that
   offering. The package must be available; making `cosmetics` current is also recommended even
   though the app fetches it by identifier.
9. Review the project's restore/transfer behavior. The app uses RevenueCat's anonymous user ID,
   so the same-store-account reinstall test below must prove the selected policy re-associates
   this non-consumable as intended.

Spelling is exact and case-sensitive. A wrong product or entitlement leaves the purchase
unavailable. If `cosmetics` is missing, the app logs a warning and may temporarily fall back to
the current offering, but that fallback is not release-ready and must not satisfy the checklist.

## 3. Supply the public SDK keys locally

RevenueCat dashboard → Project Settings → API keys supplies the app-specific **public SDK key**:

- Google Play key: starts with `goog_`
- Apple key: starts with `appl_`

Never put a RevenueCat secret key beginning with `sk_` in Unity. The app does not need one.

Copy:

`unity/Assets/Resources/Monetization/revenuecat_config.example.json`

to this ignored local path:

`unity/Assets/Resources/Monetization/revenuecat_config.json`

Then fill `googleApiKey` and/or `appleApiKey`. For every store release, set:

```json
"useTestStore": false
```

The real config and its Unity `.meta` file are gitignored. They must be supplied securely on the
human release machine or CI before building. If the file or the current platform's key is absent,
the app deliberately uses its no-store backend: the game still works, but the Shipaton purchase
gate is **not** satisfied.

A RevenueCat Test Store key (prefix `test_`) may be used only in a development build on a
physical device, with `useTestStore` set to `true`. The parser validates the key prefix against
both the platform and this flag, and refuses Test Store keys in every release build.

## 4. Test on devices

RevenueCat purchases cannot be tested in the Unity Editor. Editor Play Mode intentionally shows
the graceful no-store state because the SDK's native purchase callbacks do not run there.

Test the real path before release:

### iOS

1. Build with the final bundle identifier and the `appl_` key.
2. Use an App Store sandbox account or TestFlight and the configured non-consumable product.
3. Open Home → Wardrobe. Confirm a localized store price appears.
4. Buy the coat. Complete the native StoreKit sheet.
5. Confirm the same Wardrobe screen remains visible and the navy coat, cream collar, brass
   buttons, and conductor hat appear on the profile cat.
6. Reinstall or clear local app data while retaining the sandbox account, open Wardrobe, tap
   **Restore Purchases**, and confirm the coat visibly appears again.

### Android

1. Build a signed test build with `com.catmetro.game` and the `goog_` key.
2. Install through a Play test track with a licensed tester account; a sideloaded build is not a
   reliable Play Billing purchase test.
3. Repeat the localized-price, purchase, visible-coat, reinstall/clear-data, and restore checks
   above.
4. Verify that returning from the Play purchase sheet resumes the existing Cat Metro activity and
   the entitlement refresh completes.

Do not upload from an automated agent. Store uploads are human-only for this project.

## 5. Capture the judging evidence

Record a portrait device session, not an Editor simulation:

1. Start on the Home diorama with the **Wardrobe** capsule visible.
2. Open Wardrobe and briefly show the plain fixed profile cat and localized price.
3. Tap **Buy**, show the native purchase sheet without exposing account details, and complete it.
4. Hold on the unchanged Wardrobe screen long enough to make the newly painted coat and hat
   unmistakable.
5. Capture a second clean sequence after reinstall/clear-data: tap **Restore Purchases** and hold
   on the same visible transformation.

Use the unlocked wardrobe frame as the submission screenshot. Keep the product name, cat, coat,
and successful status legible; the preceding video beat establishes the localized price before
the CTA correctly changes to **Coat equipped**.

## Release gate

Before the deadline, verify all of these against artifacts rather than dashboard intent:

- A physical-device purchase reaches a native store sheet and the coat appears afterward.
- A physical-device restore makes the same coat appear through the same entitlement consumer.
- The release build uses a production `appl_`/`goog_` public SDK key and `useTestStore: false`.
- The installed build logs RevenueCat configured and `RevenueCat offering 'cosmetics' loaded`,
  and Wardrobe displays the store-localized price from that offering.
- The exact submitted binary is publicly live on at least one store. “In review” is not live.
- The final video and screenshot clearly show the purchase landing.
