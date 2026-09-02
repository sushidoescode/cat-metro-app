# Task 10 Fix 3 report

## Status

- Base: `23d5249e2039ce6784c241bb7177e94125b22740` on
  `feat/cat-cosmetics-v3`.
- Commit subject: `fix: harden rewarded wardrobe reentrancy`.
- This checkpoint was implemented and validated locally. Nothing was pushed, no PR was opened or
  mutated, and no build or store upload was performed.

## Genuine RED evidence

Production was unchanged when both new regression surfaces were run.

1. `/tmp/cm-task10-fix3-route-red.xml` ran 25
   `RewardedAdCosmeticRouteTests`: 19 passed and 6 failed. The six behavioral failures were:
   - `CanOffer_ReplacementDuringExactCanShowFailsClosed` returned the detached source's stale
     `true`;
   - `CanOffer_UninstallDuringExactCanShowFailsClosed` returned the uninstalled source's stale
     `true`;
   - `Request_DisposeDuringExactCanShowNeverCallsShow` called `Show` once after disposal;
   - `AvailabilityAddReentrantReplacement_RemovesLateOldAttachment` let the late old handler
     publish availability;
   - `AvailabilityRemoveReentrantReplacement_DoesNotLetOuterRebindClobberIt` let a superseded
     source publish after the remove accessor installed a newer runtime;
   - `PendingNotGrantedReentrantReplacement_DoesNotLetOuterRebindClobberIt` subscribed the
     intermediate runtime after the completion installed the actual replacement.
2. `/tmp/cm-task10-fix3-authority-red.xml` ran the new mounted PlayMode case and failed 0/1 for
   the intended reason: a real, persisted `outfit_engineer` lease changed the ledger while the
   Conductor ad was open, and the Wardrobe rebuilt to zero Conductor cards before its exact reward
   could arrive.

Both runs discovered and executed the intended tests. Neither failed from compilation, fixture
setup, missing discovery, or infrastructure.

## Implemented behavior

- `RewardedAdCosmeticRoute` now represents each desired runtime binding with an immutable source,
  generation, and captured availability handler. Every continuation validates that exact token,
  not merely an object remembered before a callback.
- `CanOffer` and `Request` fail closed after a synchronous install, uninstall, or disposal from
  `CanShow`; `Request` never crosses into `Show` after losing its binding.
- Rebinding publishes the desired token before pending completions and event accessors. A nested
  rebind therefore supersedes the outer operation. An add that attaches late is compensated after
  its accessor returns; a remove commits loss of ownership before crossing its accessor.
- Only the current live token can publish availability. Superseded handlers remain inert even if
  a hostile source fails to remove them. Runtime replacement/uninstall still completes an old
  request exactly once as NotGranted, and late old completions remain inert.
- Attempt ID, placement ID, and entitlement ID fencing remains exact. The real-coordinator route
  test proves a positive foreign attempt cannot complete or grant; `RewardedAdCoordinator` remains
  the only production caller of `PurchaseService.GrantRewardedAdEntitlement`.
- `WardrobeScreenView` defers ledger-driven reprojection only while its exact reward operation is
  busy. The exact terminal callback still owns completion, durable entitlement verification,
  initiating-cat equip, and final profile-driven reprojection.
- The mounted test now also asserts that tapping the actual admitted Conductor card applies
  `outfit.conductor` to `LargePortrait`.
- Eleven verified-unconsumed copy rows were removed from `ui.csv` and its required-key list.
  `wardrobe.buy` remains live, required, and tested to contain exactly one store-localized
  `{price}` token. Retired negative assertions for `wardrobe.buy`, `TryOnStrip`, and
  `wardrobe.rewarded.*` were removed; the positive Conductor target and exact nine-region count
  remain.

## Mounted authority proof

`UnrelatedAuthorityChangeDuringStarted_PreservesRowAndExactEquip` uses the shipped purchase and
placement resources, real `RewardedAdCoordinator`, real `RewardedAdCosmeticRoute`, actual
catalogue card and tap regions, `SaveStore` lease persistence, and a real `CosmeticProfileService`.
It selects Blue Siamese, previews the Conductor asset, starts the exact Conductor ad, durably grants
the unrelated Engineer lease, and proves the Conductor row, primary target, and preview remain
live. The exact Conductor reward then persists the second lease and equips only Blue once. A
duplicate leaves both the profile change count and committed save bytes unchanged.

## Mutation proof

The final adapter source SHA-256 was
`f3ea8bab64106b334e1cd68d925f049d257df711e9429af30c8fd8194c95f923`.
I temporarily changed `CanOffer` to return the source's `canShow` result without the post-callback
live-binding fence. `/tmp/cm-task10-fix3-route-mutation.xml` ran 25 tests: 23 passed and exactly the
replacement/uninstall `CanOffer` tests failed. After restoring with `apply_patch`, the source hash
returned byte-for-byte to the value above and
`/tmp/cm-task10-fix3-route-after-mutation.xml` passed 25/25.

## Final GREEN evidence

- `bash scripts/check.sh` — exit 0, `check: OK`.
- `dotnet restore dotnet/CatMetro.sln -p:RestoreLockedMode=true` — completed. Every tracked
  `packages.lock.json` hash was identical before and after. NuGet emitted only `NU1900` because its
  vulnerability feed was unreachable.
- `dotnet build dotnet/CatMetro.Content/CatMetro.Content.csproj --no-restore` — succeeded with
  0 errors; the two warnings were the same `NU1900` feed warning.
- Linked `.NET` Presentation/Purchases/Cosmetics/Save/Ads filter — 524 total, 524 passed, 0 failed
  or skipped.
- Focused EditMode after copy cleanup:
  `/tmp/cm-task10-fix3-editmode-focused-green.xml` — 30/30.
- Focused mounted PlayMode:
  `/tmp/cm-task10-fix3-playmode-focused-green.xml` — 6/6.
- Expanded EditMode:
  `/tmp/cm-task10-fix3-editmode-expanded-final.xml` and paired log — 197 total, 197 passed,
  0 failed/skipped/inconclusive. Fixture totals were coordinator 53, runtime 4, adapter 25,
  projection 17, catalogue 72, CSV 5, and rewarded bootstrap 21.
- Graphics-enabled expanded PlayMode:
  `/tmp/cm-task10-fix3-playmode-expanded-final.xml` and paired log — 41 total, 41 passed,
  0 failed/skipped/inconclusive. Fixture totals were Wardrobe purchase flow 29, mounted rewarded
  placement 6, and rewarded wiring 6.
- `git diff --check` and `git diff --cached --check` passed. The changed-path list contains no
  protected board/look/train presentation file. The dead CSV rows and retired visual test
  vocabulary have zero remaining literal matches in their audited surfaces.
- Final Unity logs contain no C# compiler error, unhandled test exception, null reference, aborted
  run, or test-run failure. They contain the known licensing-client handshake/access-token startup
  noise, followed by a valid Personal entitlement, plus the empty `usbmuxd` shutdown error after
  successful test completion.

## Exact changed scope

- `.superpowers/sdd/2026-08-30-cat-cosmetics-avatar-system-implementation/task-10-fix-3-report.md`
- `unity/Assets/Resources/Strings/ui.csv`
- `unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs`
- `unity/Assets/Scripts/Services/Cosmetics/RewardedAdCosmeticRoute.cs`
- `unity/Assets/Tests/EditMode/Presentation/UiCsvWardrobeTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/RewardedAdCosmeticRouteTests.cs`
- `unity/Assets/Tests/PlayMode/Bootstrap/RewardedAdsWiringTests.cs`
- `unity/Assets/Tests/PlayMode/Screens/WardrobeRewardedPlacementTests.cs`

No scene, prefab, package, project setting, generated Unity asset/meta, save-schema file, APK, or
unrelated application file changed.

## Plain unverified items

- No APK/player build, device install, live RevenueCat purchase, live mediated rewarded ad,
  account restore, airplane-mode cold boot, Google Play upload, or store submission was performed.
- No manual screenshot or human visual review was produced. The combined PlayMode run was
  graphics-enabled and its existing painted-pixel assertions passed, but that is not a visual
  approval artifact.
- The full Unity suite and the combined TASK 16 slot with `feat/composition` were not run. Evidence
  is the focused linked suite plus the exact expanded Unity fixtures above.
- Physical-device background/foreground, network loss, mediation SDK accessor behavior, and
  vendor callback timing remain represented by deterministic test doubles rather than device
  evidence.
