# Task 10 Fix 2 report

## Status

- Base: `cd96e6832a004a4880e67af449ce9998ce7b5a65`.
- Commit subject: `fix: validate catalogue rewarded wardrobe`.
- The implementation, tests, catalogue/string changes, and this report are one atomic local
  commit. Its final SHA is recorded in the handoff because a Git commit cannot contain its own
  content-derived SHA. Nothing was pushed, merged, uploaded, or submitted to a store.

## RED evidence

The controller's original pre-Fix-2 Unity run exited 6 before discovery with `CS7036` at the then
current `WardrobeRewardedPlacementTests.cs:593`: a retained test still called a removed
three-argument `WardrobeScreenView.Create` overload. There was no NUnit XML. That was the stale
integration RED inherited from Fix 1.

At implementer takeover, the previous partial rewrite had already made the exact
`RewardedAdsWiringTests` filter pass 6/6 in
`/tmp/cm-task10-fix2-red-rewarded-wiring.xml`. This is recorded only as a too-weak retained-suite
baseline: it was not unchanged pre-Fix-2 source and it still did not mount or exercise the actual
catalogue reward card and primary action.

The test-first repairs then exposed three product defects:

1. The linked coordinator suite ran 53 tests and failed one new second-stage exact-callback case
   (52 passed, 1 failed): readiness changed between the public precheck and `ShowCore`, and the
   supplied exact callback was never completed.
2. `/tmp/cm-task10-fix2-mounted-red2.xml` ran the five new mounted Wardrobe tests with 4 passed and
   1 failed. `StartedDoesNotGrant_ExactRewardGrantsAndEquipsTheInitiatingCatOnce` proved that a
   synchronous availability notification rebuilt the selected row during `Started`, invalidating
   its own operation before the exact durable reward arrived.
3. A post-implementation scope audit found that publishing the existing `Installed` event during
   uninstall redefined its established contract. The exact existing Unity test
   `RuntimeInstalledCallback_InstallingNewStore_PreservesNewReplacementOwnership` failed 0/1 in
   `/tmp/cm-task10-fix2-runtime-uninstall-red2.xml`: expected two installation publications, actual
   three. A new runtime contract test was first compiled RED with `CS0117` because the separate
   identity-change signal did not yet exist.

These REDs were behavior or compilation failures, not missing discovery reported as success.

## Implemented behavior

- The shipped Conductor catalogue row alone carries
  `rewardedPlacementId: wardrobe_try_conductor`. A known localized store price still wins as the
  named Purchase route; only an absent price plus that exact available placement projects Watch;
  otherwise the locked candidate is omitted without a gap or input region.
- `RewardedAdCoordinator` now completes every supplied exact callback once on both public and
  second-stage Busy/Unavailable/overflow exits. Started is still non-terminal. Provider rejection,
  throw, and synchronous failure retain the allocated attempt plus exact placement and entitlement
  identity.
- Foreign attempt/placement callbacks are inert. Exact Close completes the visible operation once
  while retaining the attempt for a genuinely later durable reward. Only the coordinator grants
  through `PurchaseService`; duplicates never renew or grant twice.
- `RewardedAdCosmeticRoute` retains one exact pending source/completion, fails a concurrent request
  closed, and completes a pending request NotGranted if the runtime identity changes or uninstalls.
  A late callback from the detached source is inert.
- `RewardedAdRuntime.Installed` remains install-only. A separate `Changed` signal publishes both
  install and successful identity-conditional uninstall; the cosmetic adapter subscribes to that
  seam. The existing reentrant bootstrap ownership/publication contract remains intact.
- `WardrobeScreenView` does not rebuild from the ad's synchronous busy availability transition
  while its own reward operation is active. Authority changes and the exact terminal callback still
  reproject normally.
- The obsolete fixed TryOn strings and required-key assertions are removed. Category filters, one
  selected-row primary action, shared catalogue vocabulary, prices, ownership ledger, and grant
  path remain singular.

## Mounted artifact coverage

`WardrobeRewardedPlacementTests` now contains five dense PlayMode tests over the actual mounted
Wardrobe, shipped resource builders, real `RewardedAdCoordinator`, real
`RewardedAdCosmeticRoute`, durable SaveStore lease persistence, actual item card rect, and actual
`PrimaryActionChip` world-corner projection.

The fixture proves:

- absent placement, mismatched placement, and unavailable provider produce zero Conductor card,
  item target, primary target, ghost region, or extra empty-state band;
- the exact offer paints one admitted Conductor card, tapping its real rect selects it, and the
  registered `wardrobe.primary` painted target meets 48dp;
- tapping Primary calls `wardrobe_try_conductor` exactly once; Started changes neither authority,
  either cat loadout, nor save bytes;
- exact Reward persists one lease and equips only the initiating Blue cat once; a duplicate leaves
  save bytes, lease count, and both in-memory cat loadouts unchanged;
- foreign attempts/placements of every terminal kind leave authority, lease, loadout, save bytes,
  card route, primary target, and the in-flight Restore guard unchanged;
- exact Close releases the visible operation; a later exact reward owns/repaints the card through
  ledger authority but never resurrects auto-equip;
- exact DisplayFailed releases the operation without grant/equip, and every later terminal remains
  inert.

`RewardedAdsWiringTests` now uses the admitted Conductor row rather than the retired fixed strip.
It retains durable-before-ledger, exact expiry/restart bytes, caps, duplicates, original-attempt
late reward, four-placement provider mapping, failure degradation, Home/Back/Restore/board
usability, and now also taps the real Conductor card and selected primary purchase target while
asserting one backend purchase call. Non-admitted Engineer/Scarf/Goggles remain service-only truth;
no visual catalogue rows were invented.

## Mutation proof

After the final runtime-event repair, the adapter source SHA-256 was
`a634d4c9746eca61cfa881fd93cc57934ea16d1b6988128a87ab3b59f72d9895`.

I temporarily removed the placement equality from the exact completion predicate and ran
`RewardedAdCosmeticRouteTests`. The result was 18 total, 17 passed, 1 failed, specifically:

`NonExactOrNonGrantedCompletion_FailsClosedOnce(4L,"wrong","outfit_conductor",Granted)`

It expected NotGranted and observed Granted. I restored the source with `apply_patch`; SHA-256
returned byte-for-byte to
`a634d4c9746eca61cfa881fd93cc57934ea16d1b6988128a87ab3b59f72d9895`, and the same focused suite
passed 18/18.

## Final GREEN evidence

- `bash scripts/check.sh` — exit 0, `check: OK` (the repository labels this an interim harness).
- Linked .NET, locked metadata already restored, `--no-restore`, filters for coordinator, runtime,
  adapter, projection, and catalogue — 164 total, 164 passed, 0 failed/skipped. NuGet emitted only
  `NU1900` vulnerability-feed reachability warnings.
- Expanded Unity EditMode:
  `/tmp/cm-task10-fix2-editmode-expanded-final.xml` and paired log — 190 total, 190 passed,
  0 failed/skipped/inconclusive. Fixture counts were coordinator 53, runtime 4, adapter 18,
  projection 17, catalogue 72, CSV 5, and rewarded bootstrap 21.
- Exact required Unity PlayMode:
  `/tmp/cm-task10-fix2-playmode-expanded-final.xml` and paired log — 40 total, 40 passed,
  0 failed/skipped/inconclusive. Fixture counts were Wardrobe purchase flow 29, mounted rewarded
  placement 5, and rewarded wiring 6.
- The painted starter-cat assertion was additionally run graphics-enabled twice in isolation
  (1/1 each) and after its immediately preceding alphabetical `SavedCoat` test (2/2), in
  `/tmp/cm-task10-fix2-pixels-graphics-1.xml`, `-2.xml`, and `-paired.xml`.
- The exact reentrant bootstrap regression test passed 1/1 after the `Changed` repair in
  `/tmp/cm-task10-fix2-runtime-uninstall-green.xml` and is also included in the 21/21 expanded
  bootstrap fixture.

The final Unity logs have no compiler error, failed test, unhandled runtime exception, discovery
failure, or aborted run. They retain environmental startup/shutdown noise: the initial licensing
client handshake/access-token messages recover to a valid Personal entitlement, and an empty
`usbmuxd` shutdown error follows test exit 0. Existing unrelated compile warnings remain for
obsolete TMP word wrapping and a duplicate `GameRoot` using directive.

An earlier combined run made with `-nographics` reported Blue painted pixels as exactly zero.
Two isolated repeats and a paired repeat under the same invalid flag also failed. Temporary
readback instrumentation showed a uniform grey render with no Canvas, identifying the command as
invalid visual evidence rather than a product intermittency. The instrumentation was removed, the
file restored, and only the graphics-enabled results above are claimed.

## Exact changed scope

- `.superpowers/sdd/2026-08-30-cat-cosmetics-avatar-system-implementation/task-10-fix-2-report.md`
- `unity/Assets/Resources/Cosmetics/cosmetic_catalog.json`
- `unity/Assets/Resources/Strings/ui.csv`
- `unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs`
- `unity/Assets/Scripts/Services/Cosmetics/RewardedAdCosmeticRoute.cs`
- `unity/Assets/Scripts/Services/RewardedAds/RewardedAdCoordinator.cs`
- `unity/Assets/Scripts/Services/RewardedAds/RewardedAdRuntime.cs`
- `unity/Assets/Tests/EditMode/Presentation/UiCsvWardrobeTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdCoordinatorTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdFixtures.cs`
- `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdRuntimeTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticCatalogTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticWardrobeProjectionTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/RewardedAdCosmeticRouteTests.cs`
- `unity/Assets/Tests/PlayMode/Bootstrap/RewardedAdsWiringTests.cs`
- `unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs`
- `unity/Assets/Tests/PlayMode/Screens/WardrobeRewardedPlacementTests.cs`

No protected board/look/train presentation file, scene, prefab, package, project setting, generated
Unity asset/meta, APK, or unrelated application/save file changed. Test helper visibility is
internal to the test assembly; no production fixture API was added.

## Plain unverified items

- No APK/player build, device install, live RevenueCat purchase, live rewarded ad, account restore,
  airplane-mode cold boot, Google Play upload, or store submission was performed.
- No manual screenshot or visual approval was produced. The capture-only PlayMode case was
  disarmed because `CM_WARDROBE_CAPTURE_DIR` was not set; painted-pixel and mounted-geometry tests
  ran, but they are not a substitute for human visual inspection.
- The full Unity suite and broad repository test script were not run; validation was the focused
  linked suite plus the expanded exact Unity fixtures listed above.
- Physical-device background/foreground, network loss/recovery, mediation SDK callbacks, and late
  reward timing remain represented by deterministic fakes, not device evidence.
