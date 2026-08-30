# Task 7 fix 3 report

## Status

Fix 3 is implemented from clean base
`b5c04980ce938e45186d23207a867c579cdd3612`. Nothing was pushed.

## Strict test-only RED

Production remained byte-identical to the base until the controller audited and authorized the
test-only patch.

Focused linked .NET command:

```text
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Purchases|FullyQualifiedName~CatMetro.Tests.Cosmetics' \
  --logger 'trx;LogFileName=/tmp/cm-cosmetics-dotnet-fix-3-red.trx'
```

- `/tmp/cm-cosmetics-dotnet-fix-3-red.trx`: total 308, passed 285, failed 23,
  errors/skipped 0.
- `/tmp/cm-cosmetics-purchases-fix-3-red.xml` and paired log, exact Unity EditMode
  `GracefulDegradationTests`: total 73, passed 50, failed 23, skipped 0.
- `/tmp/cm-cosmetics-wardrobe-fix-3-red.xml` and paired log: total 27, passed 27,
  failed/skipped 0. The Wardrobe fixture was intentionally unchanged and stayed green.
- `/tmp/cm-cosmetics-revenuecat-fix-3-red.xml` and paired log: 3 discovered, 3 failed,
  skipped 0. The real offerings watchdog elapsed 30.0025 seconds and observed Ready before its
  empty callback; the occupied path also observed Ready; the fast Guard trace contained callback
  without a preceding timeout hook. These were behavioral failures, not reflection, compiler,
  assembly-discovery, or harness failures.

The optional RevenueCat test initializes purchases-unity 9.9's actual Editor noop wrapper before
calling `FetchProducts`; its elapsed-time assertion prevents a synchronous null-wrapper exception
from masquerading as watchdog evidence. The assembly is constrained by package-derived
`CATMETRO_REVENUECAT` and `UNITY_EDITOR`, while remaining discoverable by the Editor PlayMode
runner. Builds without the package exclude it through the version define constraint rather than
acquiring an unconditional SDK reference.

After review identified stale non-Completed restore metadata, a second focused RED preceded that
production adjustment:

- `/tmp/cm-cosmetics-stale-restore-fix-3-red.trx`: total 2, passed 0, failed 2,
  errors/skipped 0. Detached Failure and Unavailable responses both leaked a backend-reported
  count of 7 (and would also have retained their authoritative snapshot).

## Implemented authority laws

- Every `AttachBackend`, including same-instance reattachment and A-B-A, monotonically advances
  a backend generation and the entitlement epoch without erasing cached ledger truth.
- Product, purchase, restore, and entitlement requests capture their exact backend/generation.
  Entitlement work captures them at enqueue, so queued work cannot migrate to a replacement.
- Stale purchases return once with store metadata preserved but confirmation stripped and never
  start a fallback on the replacement backend.
- Stale Completed restores become diagnostic Failure; every stale restore outcome has restored
  count and confirmation stripped, while an existing non-Completed store diagnostic is preserved.
- Entitlement refresh acceptance requires backend identity, generation, epoch, and authoritative
  truth. Stale completion is surfaced internally as rejected/non-authoritative.
- The entitlement pump reserves ownership before stale callbacks. Reentrant enqueue cannot start
  a second native request; synchronous fake callbacks release/pump from the callback and are not
  overwritten by an unconditional post-call flag reset.
- Purchase confirmation validates the exact caller-requested catalogue product while retaining
  backend-returned `ProductId` as metadata. Null, unknown, mismatched, and zero-promise metadata
  cannot create vacuous confirmation. At least one active non-reward promise is required and every
  promise must be present.
- Product cache replacement requires a non-null response from the current exact Ready generation.
  Ready empty clears; Ready null and every non-Ready response preserve the last-good cache; partial
  non-null replaces rather than merges.

## RevenueCat offerings boundary

The Integration change is limited to `FetchProducts` failure signaling and the shared Guard's
optional timeout hook:

- offerings timeout sets Unreachable before the empty callback;
- an already occupied offerings slot also sets Unreachable before its empty callback and does not
  release/overwrite the native slot;
- a timeout hook exception is logged, then the callback still fires exactly once;
- the actual timed-out native slot remains occupied until the original native callback arrives;
- the test supplies a valid late empty `cosmetics` offering through purchases-unity's actual native
  callback receiver and proves only that callback releases the slot, returns Availability to Ready,
  and cannot refire the already timed-out consumer.

The `IPurchaseBackend.FetchProducts` contract now states the shared cache law: Ready plus non-null
empty is authoritative empty; failure must report non-Ready before returning empty; null is never
authoritative.

## Fresh GREEN and gates

- `/tmp/cm-cosmetics-dotnet-fix-3-green.trx`: total 310, passed 310, failed/skipped 0.
- `/tmp/cm-cosmetics-purchases-fix-3-green.xml` and log: Unity EditMode total 75,
  passed 75, failed/skipped 0.
- `/tmp/cm-cosmetics-wardrobe-fix-3-green.xml` and log: PlayMode total 27,
  passed 27, failed/skipped 0.
- `/tmp/cm-cosmetics-revenuecat-fix-3-green.xml` and log: Editor PlayMode total 3,
  passed 3, failed/skipped 0. The real watchdog case elapsed 30.0043 seconds.
- `/tmp/cm-cosmetics-csv-fix-3-green.xml` and log: total 5, passed 5,
  failed/skipped 0.
- `/tmp/cm-cosmetics-wardrobe-fix-3-capture.xml` and log: total 1, passed 1,
  failed/skipped 0.
- `bash scripts/check.sh`: exit 0, `check: OK`.
- `git diff --check`: clean before staging.

All XML/TRX totals and logs were inspected. There are no compiler errors, aborted test runs,
null references, or unhandled exceptions. The RevenueCat log contains only the intentional,
expected throwing-hook exception asserted by that test.

## Visual artifacts

All eight fresh originals were opened, as was the contact sheet. Coats, frames, labels, tabs,
localized prices, restore/action controls, and state transitions remain readable and consistent.
The deliberately simple card swatches and unused cream breathing room in sparse states remain the
approved non-asset-dependent presentation; this authority-only fix provides no new art and does
not justify visual mutation.

Every regenerated PNG is byte-identical to its approved Fix 2 predecessor:

- `wardrobe-contact-sheet.png` — `6afd8d5a908415dd8cf15dcbeef6094527fa8782d936e59582815391c8e5007b`
- `wardrobe-frame-brass.png` — `0cd2afe125900a57e63cfc1198da88ec8fdfde36472d17cc56ac53bcb2a1e3ec`
- `wardrobe-frame-lantern.png` — `88631d5876f3254a8f0f0fb6a715bab5442dfbea87d426d6c5e915e1241f0eeb`
- `wardrobe-lapsed.png` — `3a23c23781a6e7a77bf14ee55081a9ee21140bc0906f114c80bbc775e2749188`
- `wardrobe-locked-preview.png` — `7b73bdf5d7a5c5e6cf2564c61f525f2066a85a43963194e7a33ec3e36a43b4f0`
- `wardrobe-plain.png` — `87be9ee8889e39116921ff107e4c14598d67fdf89bd7290f749f1d378bb3cc62`
- `wardrobe-purchased-equipped.png` — `990bbe73910a3e201d142dc092b85d2f7c7d3b99ff9329d806f0600802ca6cd8`
- `wardrobe-restored.png` — `abbb81a343ed52b73e95e3a70652bbb2767244e681303e785319eb77cf89ff9e`

## Changed scope

- `unity/Assets/Scripts/Services/Purchases/PurchaseService.cs`
- `unity/Assets/Scripts/Services/Purchases/IPurchaseBackend.cs`
- `unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs`
- `unity/Assets/Tests/EditMode/Pure/Purchases/GracefulDegradationTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Purchases/PurchaseFixtures.cs`
- `unity/Assets/Tests/PlayMode/RevenueCat.meta`
- `unity/Assets/Tests/PlayMode/RevenueCat/CatMetro.Tests.RevenueCat.PlayMode.asmdef`
- its `.meta`
- `unity/Assets/Tests/PlayMode/RevenueCat/RevenueCatOfferingsFailureTests.cs`
- its `.meta`
- this report

No Home, Bootstrap, GameRoot, HUD-owner/protected file, scene, prefab, package, project setting,
catalogue/profile/save content, or Wardrobe production file changed. No generated InitTestScene or
crash artifact remains.

## Unverified outside task authority

- No store/device purchase was attempted and no Play build/upload/install was performed.
- The 30-second watchdog is proven against the real SDK's Editor noop wrapper and actual callback
  receiver, not against Android/iOS native billing transport.
- Package absence was not simulated by editing package configuration; optionality is enforced by
  the package version define/assembly constraint and the existing package-optional architecture.
