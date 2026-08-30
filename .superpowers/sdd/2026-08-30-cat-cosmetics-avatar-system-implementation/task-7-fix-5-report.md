# Task 7 fix 5 report

## Status

Fix 5 is implemented from clean base
`d6b771f8cd33bd03c2541c5c6ff43c5b951cd2a1`. Nothing was pushed.

## Committed-base gap evidence

Public Purchase forwarding gap:

- Temporarily changed only public `Purchase` to forward `QueryTimeoutSeconds` instead of
  `InteractiveTimeoutSeconds`.
- `/tmp/cm-cosmetics-revenuecat-fix-5-base-gap-purchase-forwarding.xml` and paired log: the old
  combined production-duration test incorrectly passed 1/1 because it reflected only the constant
  and never exercised public Purchase or the real Guard.
- The adapter was restored immediately to base SHA-256
  `9a12b265ee68f97c477e6572106a198b92639f5c72929a12c5d3c6fe810b76bd`.

SDK fixture lifecycle gap:

- `/tmp/cm-cosmetics-revenuecat-fix-5-base-gap-lifecycle.xml` and paired log: the committed
  six-case fixture passed 6/6, but its log contained exactly four unintended
  `[Monetization] RevenueCat configured` entries at lines 654, 681, 756, and 825.
- This proved that the enabled adapter's `Start` coroutine was creating/configuring a replacement
  `Purchases` component after the fixture manually mounted the initialized Editor noop instance.

## Strict test-only RED

Before any production support, only
`unity/Assets/Tests/PlayMode/RevenueCat/RevenueCatOfferingsFailureTests.cs` changed. Production
remained at the exact base hash above. The constant-only case was replaced by independently
discoverable public Purchase and Restore tests, and a two-frame lifecycle test plus exact mounted
SDK assertions were added.

- `/tmp/cm-cosmetics-revenuecat-fix-5-red.xml` and paired log: 8 discovered, 1 passed, 7 failed,
  skipped 0; duration 30.0641104 seconds.
- Public Purchase and Restore failed clean assertions because the requested dormant Guard
  diagnostics seam did not exist.
- `FixtureLifecycle`, Guard, the actual offerings watchdog, and both operation-session tests
  rejected replacement of the manually initialized SDK object with an `Is.SameAs` failure.
- There were no compiler, discovery, reflection-invocation, null-reference, or harness errors.

The controller inspected this boundary and authorized production/harness support.

## Smallest implementation

- `RevenueCatBehaviour` gained one private `[NonSerialized] Action<float>` diagnostics delegate.
  It remains null in production and is invoked with Guard's requested timeout immediately before
  the existing timeout coroutine starts. No scheduler, public API, or shortened production path
  was added.
- The fixture disables `RevenueCatBehaviour` immediately after `AddComponent`, then manually starts
  and retains one real purchases-unity `Purchases` object with `PurchasesWrapperNoop`.
- Public Purchase and Restore tests call the actual public methods, observe `300f` at the real
  Guard boundary, prove no synchronous local callback, and inspect non-null purchases-unity
  `MakePurchaseCallback` / `RestorePurchasesCallback` properties.
- The lifecycle and every yielded watchdog/session proof verifies the backend still references the
  same manual object, the host owns exactly one `Purchases`, the wrapper is the real noop wrapper,
  and the adapter remains disabled. Explicitly started coroutines continue while it is disabled.

## Required mutation evidence

Each mutation was applied independently and restored with `apply_patch` before the next run.

1. Public Purchase forwarded `QueryTimeoutSeconds`:
   - `/tmp/cm-cosmetics-revenuecat-fix-5-purchase-duration-mutation.xml` and log: total 8,
     passed 7, failed 1, skipped 0.
   - Only `PublicPurchase_ForwardsProductionWatchdogToGuard` failed: expected `300f`, observed
     `30f`.
2. Public Restore forwarded `QueryTimeoutSeconds`:
   - `/tmp/cm-cosmetics-revenuecat-fix-5-restore-duration-mutation.xml` and log: total 8,
     passed 7, failed 1, skipped 0.
   - Only `PublicRestore_ForwardsProductionWatchdogToGuard` failed: expected `300f`, observed
     `30f`.
3. Deleted only the setup `_backend.enabled = false`:
   - `/tmp/cm-cosmetics-revenuecat-fix-5-lifecycle-mutation.xml` and log: total 8, passed 1,
     failed 7, skipped 0; five unintended configuration entries returned.
   - The explicit lifecycle test failed after two frames on SDK-object reference identity, and all
     public/yielded claims carrying the shared exact-mount assertion rejected the invalid harness.

After the first two restorations, `RevenueCatBackend.cs` returned exactly to intended SHA-256
`02e5c57e3bdb90164f1335da35a909e61e3d5c3135afbe919a7407384e0f63d7`. After the lifecycle
restoration, the test returned to its recorded pre-mutation hash; a later comment/redundant-disable
cleanup produced final test SHA-256
`be1ede1b347d505f9cdf4bc6e930f36c6975406142ad966c0e1e12a6359c2105` without changing behavior.

## Fresh GREEN and gates

- `/tmp/cm-cosmetics-revenuecat-fix-5-green.xml` and log: Editor PlayMode total 8, passed 8,
  failed/skipped 0; total duration 30.0670469 seconds and real offerings watchdog duration
  30.004110 seconds. The final log contains zero unintended `RevenueCat configured` lines.
- `/tmp/cm-cosmetics-dotnet-fix-5-green.trx`: exact Fix 4 linked Purchases/Cosmetics filter,
  total/executed 321, passed 321, failed/errors/skipped 0, including all three cosmetics payload
  and migration cases omitted by the initially narrower 318-case command.
- `/tmp/cm-cosmetics-purchases-fix-5-green.xml` and log: Unity EditMode total 83, passed 83,
  failed/skipped 0.
- `/tmp/cm-cosmetics-wardrobe-fix-5-green.xml` and log: Wardrobe PlayMode total 27, passed 27,
  failed/skipped 0.
- `/tmp/cm-cosmetics-csv-fix-5-green.xml` and log: CSV EditMode total 5, passed 5,
  failed/skipped 0.
- `/tmp/cm-cosmetics-wardrobe-fix-5-capture.xml` and log: armed capture total 1, passed 1,
  failed/skipped 0.
- `bash scripts/check.sh`: exit 0, `check: OK`.
- `git diff --check`: clean before staging.

All XML/TRX and logs were inspected. There are no compiler errors, aborted runs, unhandled
exceptions, null references, or reflection invocation failures. The RevenueCat log contains only
the intentional throwing-hook exception expected and contained by its existing test.

## Visual regression artifacts

No visual production file changed. The armed capture regenerated all eight PNGs byte-identically:

- `wardrobe-contact-sheet.png` — `6afd8d5a908415dd8cf15dcbeef6094527fa8782d936e59582815391c8e5007b`
- `wardrobe-frame-brass.png` — `0cd2afe125900a57e63cfc1198da88ec8fdfde36472d17cc56ac53bcb2a1e3ec`
- `wardrobe-frame-lantern.png` — `88631d5876f3254a8f0f0fb6a715bab5442dfbea87d426d6c5e915e1241f0eeb`
- `wardrobe-lapsed.png` — `3a23c23781a6e7a77bf14ee55081a9ee21140bc0906f114c80bbc775e2749188`
- `wardrobe-locked-preview.png` — `7b73bdf5d7a5c5e6cf2564c61f525f2066a85a43963194e7a33ec3e36a43b4f0`
- `wardrobe-plain.png` — `87be9ee8889e39116921ff107e4c14598d67fdf89bd7290f749f1d378bb3cc62`
- `wardrobe-purchased-equipped.png` — `990bbe73910a3e201d142dc092b85d2f7c7d3b99ff9329d806f0600802ca6cd8`
- `wardrobe-restored.png` — `abbb81a343ed52b73e95e3a70652bbb2767244e681303e785319eb77cf89ff9e`

## Exact changed scope

- `unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs`
- `unity/Assets/Tests/PlayMode/RevenueCat/RevenueCatOfferingsFailureTests.cs`
- this report

No Presentation, Wardrobe production, board, Bootstrap, Home, GameRoot, HUD-owner/protected file,
scene, prefab, package, project setting, profile/catalogue/save content, or unrelated test changed.
No generated `InitTestScene` or crash artifact remains.

## Unverified outside task authority

- No real store/device Purchase or Restore was attempted.
- The callback properties, noop wrapper, lifecycle, and 30-second watchdog are proven against
  purchases-unity 9.9 in Editor, not Android/iOS native billing transport.
- No Play build, upload, or install was performed, and package absence was not simulated by
  changing package configuration.
