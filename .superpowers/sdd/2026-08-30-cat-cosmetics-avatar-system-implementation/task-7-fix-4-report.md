# Task 7 fix 4 report

## Status

Fix 4 is implemented from clean base
`c99d358cdb7e1fedd6b15865164149ddc22d57b1`. Nothing was pushed.

## Strict test-only RED

Production and interfaces remained byte-identical to the base while the first ABA and
same-instance assertions compiled against the existing untagged event. The controller inspected
the test-only boundary and authorized production work after these behavioral failures:

```text
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Purchases|FullyQualifiedName~CatMetro.Tests.Cosmetics' \
  --logger 'trx;LogFileName=/tmp/cm-cosmetics-dotnet-fix-4-red.trx'
```

- `/tmp/cm-cosmetics-dotnet-fix-4-red.trx`: total/executed 314, passed 310, failed 4,
  errors/skipped 0.
- `/tmp/cm-cosmetics-purchases-fix-4-red.xml` and paired log: exact Unity EditMode
  `GracefulDegradationTests`, total 79, passed 75, failed 4, skipped 0.
- The four failures were purchase and restore variants of
  `AbaReattachedBackend_RejectsLateUpdateCapturedByItsPriorAttachment` and
  `SameInstanceReattach_RejectsAlreadyCapturedLateTransactionUpdate`.
- There were no compiler, reflection, discovery, helper, fake, or harness failures.

## Implemented authority session

- `TransactionEntitlementUpdate` is an engine-free envelope carrying an opaque backend authority
  session and an authoritative entitlement snapshot.
- The optional transaction-update backend now begins an exclusive authority session for every
  `PurchaseService.AttachBackend`, including same-instance reattachment and A-B-A. Its contract
  records the production singleton/exclusive-consumer composition rather than claiming that one
  backend supports concurrent services.
- `PurchaseService` stores the exact per-attachment event delegate for unsubscription. The closure
  captures backend identity, service generation, and authority session; all three plus an
  authoritative snapshot must match before the existing confirmed-snapshot path can advance the
  epoch or ledger.
- Stale/default/unknown/non-authoritative events are no-ops and do not invalidate an in-flight
  current refresh. Equal numeric sessions from distinct backend objects remain fenced.
- The focused fake captures authority at purchase/restore operation start. Its tests cover A1-B-A3,
  same-instance reattachment, current-session acceptance, older-refresh invalidation, rejected
  event epoch preservation, exact one-handler subscription cardinality, ledger change cardinality,
  and exact local callback counts.
- `RevenueCatBehaviour` begins nonzero monotonic sessions and captures the current session at the
  start of Purchase and Restore. A native success after the local watchdog publishes that captured
  session, never callback-time authority. Public interactive watchdogs remain exactly 300 seconds.

## Mutation-sensitive real SDK evidence

The optional Editor PlayMode fixture now has six independently discoverable cases: occupied
offerings handling, the real 30-second offerings watchdog, throwing timeout-hook containment, the
production timeout constant, Purchase session capture, and Restore session capture.

Occupied-slot mutation:

- Temporarily removed only the occupied `return;` from `RevenueCatBackend.FetchProducts`.
- `/tmp/cm-cosmetics-revenuecat-fix-4-occupied-mutation.xml` and paired log: total 6, passed 5,
  failed 1, skipped 0; real watchdog duration 30.004361 seconds.
- The only failure was
  `OfferingsWatchdog_SignalsFailureBeforeItsSingleEmptyCallback`: purchases-unity's actual private
  `GetOfferingsCallback` was not the same delegate after the retry. The assertion uses
  `GetProperty(..., Instance | NonPublic)` and `Is.SameAs`, so flag/label behavior cannot conceal
  native callback overwrite.

Operation-session mutation:

- Temporarily changed only the two late Purchase/Restore publish arguments from captured
  `authoritySession` to callback-time `_authoritySession`.
- `/tmp/cm-cosmetics-revenuecat-fix-4-session-mutation.xml` and paired log: total 6, passed 4,
  failed 2, skipped 0.
- The only failures were
  `PurchaseLateCallback_PublishesItsOperationStartAuthoritySession` and
  `RestoreLateCallback_PublishesItsOperationStartAuthoritySession`: each expected S1 (`1`) and
  observed S2 (`2`). Each test separately drives purchases-unity's real `_makePurchase` or
  `_restorePurchases` receiver after a short local watchdog and also pins one local callback.

Both mutations were restored with `apply_patch`. The intended `RevenueCatBackend.cs` SHA-256 was
`9a12b265ee68f97c477e6572106a198b92639f5c72929a12c5d3c6fe810b76bd` before mutation and is exactly
that value after both restorations and final GREEN.

## Fresh GREEN and gates

- `/tmp/cm-cosmetics-dotnet-fix-4-green.trx`: linked Purchases/Cosmetics total/executed 321,
  passed 321, failed/errors/skipped 0.
- `/tmp/cm-cosmetics-purchases-fix-4-green.xml` and log: Unity EditMode total 83, passed 83,
  failed/skipped 0.
- `/tmp/cm-cosmetics-wardrobe-fix-4-green.xml` and log: Wardrobe PlayMode total 27, passed 27,
  failed/skipped 0.
- `/tmp/cm-cosmetics-revenuecat-fix-4-green.xml` and log: Editor PlayMode total 6, passed 6,
  failed/skipped 0; total duration 30.0658892 seconds and actual production watchdog case duration
  30.004133 seconds.
- `/tmp/cm-cosmetics-csv-fix-4-green.xml` and log: total 5, passed 5, failed/skipped 0.
- `/tmp/cm-cosmetics-wardrobe-fix-4-capture.xml` and log: armed capture total 1, passed 1,
  failed/skipped 0.
- `bash scripts/check.sh`: exit 0, `check: OK`.
- `git diff --check`: clean before staging.

Every XML/TRX and paired log was inspected. There are no compiler errors, aborted runs, null
references, or unhandled exceptions. The RevenueCat log contains the intentional throwing-hook
exception that its test expects and contains.

## Visual artifacts

All seven fresh full-resolution state frames were opened individually, then the contact sheet was
opened. Coats, frames, labels, tabs, localized prices, restore/action controls, and state changes
remain readable and consistent. Card swatches are painted category/item cues, not blank asset
holes. Sparse one-card states retain a large cream region, but it functions as stable breathing
room while keeping the restore and context-sensitive primary action anchored consistently; this
authority-only fix adds no art or visual requirement that would justify changing that approved
layout.

Every regenerated PNG is byte-identical to the prior approved artifact:

- `wardrobe-contact-sheet.png` — `6afd8d5a908415dd8cf15dcbeef6094527fa8782d936e59582815391c8e5007b`
- `wardrobe-frame-brass.png` — `0cd2afe125900a57e63cfc1198da88ec8fdfde36472d17cc56ac53bcb2a1e3ec`
- `wardrobe-frame-lantern.png` — `88631d5876f3254a8f0f0fb6a715bab5442dfbea87d426d6c5e915e1241f0eeb`
- `wardrobe-lapsed.png` — `3a23c23781a6e7a77bf14ee55081a9ee21140bc0906f114c80bbc775e2749188`
- `wardrobe-locked-preview.png` — `7b73bdf5d7a5c5e6cf2564c61f525f2066a85a43963194e7a33ec3e36a43b4f0`
- `wardrobe-plain.png` — `87be9ee8889e39116921ff107e4c14598d67fdf89bd7290f749f1d378bb3cc62`
- `wardrobe-purchased-equipped.png` — `990bbe73910a3e201d142dc092b85d2f7c7d3b99ff9329d806f0600802ca6cd8`
- `wardrobe-restored.png` — `abbb81a343ed52b73e95e3a70652bbb2767244e681303e785319eb77cf89ff9e`

## Changed scope

- `unity/Assets/Scripts/Services/Purchases/IPurchaseBackend.cs`
- `unity/Assets/Scripts/Services/Purchases/PurchaseService.cs`
- `unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs`
- `unity/Assets/Tests/EditMode/Pure/Purchases/GracefulDegradationTests.cs`
- `unity/Assets/Tests/PlayMode/RevenueCat/RevenueCatOfferingsFailureTests.cs`
- this report

No Home, Bootstrap, GameRoot, Wardrobe production, HUD-owner/protected file, scene, prefab, package,
project setting, catalogue/profile/save content, or unrelated test changed. No generated
`InitTestScene` or crash artifact remains.

## Unverified outside task authority

- No store/device purchase was attempted and no Play build, upload, or install was performed.
- The 30-second watchdog and native callback/session behavior are proven through purchases-unity
  9.9's real Editor noop wrapper and callback receivers, not Android/iOS native billing transport.
- Package absence was not simulated by changing package configuration; the optional test assembly
  retains its existing package/editor constraints.
