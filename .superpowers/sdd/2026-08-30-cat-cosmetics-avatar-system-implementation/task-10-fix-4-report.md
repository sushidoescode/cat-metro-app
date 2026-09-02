# Task 10 Fix 4 report

## Status

- Base: `7999f0fe101fa7aef67c56bd1a760d01287542f0` on
  `feat/cat-cosmetics-v3`.
- Intended commit subject: `fix: close rewarded route ownership windows`.
- This checkpoint was implemented and validated locally. Nothing was pushed, no PR was opened or
  mutated, and no build or store upload was performed.

## Genuine RED evidence

Production was unchanged when
`/tmp/cm-task10-fix4-route-red.xml` and `/tmp/cm-task10-fix4-route-red.log` were produced. Unity
compiled and discovered 30 `RewardedAdCosmeticRouteTests`: 25 passed and five failed for the
intended behavioral reasons.

1. `PendingNotGrantedReentrantReplacement_DoesNotLetOuterRebindClobberIt` observed that original
   binding A was never removed after its pending completion installed C during A-to-B rebinding.
2. `PendingNotGrantedReentrantReturnToOriginal_ReplacesRatherThanLeaksHandler` observed two
   handlers on A after the pending completion returned the runtime from B to A.
3. `PendingNotGrantedCallbackDispose_StillDetachesOriginalBinding` observed that disposal from the
   pending completion still left original A attached.
4. `CanShowNestedRequest_FailsNestedWithoutClearingOuterOwner` observed no terminal result for the
   nested request because it became pending and was then overwritten by the outer continuation.
5. `RealCoordinator_CanShowNestedRequestPreservesOuterExactAttempt` observed that the real
   coordinator's one rewarded attempt completed the outer route as `NotGranted` instead of
   `Granted` after the same ownership overwrite.

There was no compilation, discovery, fixture-setup, or test-run infrastructure failure. One brief
bullet described the opposite owner in this sequence. The confirmed ownership law used here is:
the first/outer request claims before `CanShow`, the nested request fails `NotGranted` once without
clearing it, exactly one `Show` belongs to the outer request, and its exact reward completes that
outer request `Granted` once.

## Implemented behavior

- `RewardedAdCosmeticRoute` now creates one explicit `RequestToken` per call. The first request
  publishes its token before `Resolve` and retains it through exact `CanShow`, exact `Show`, and
  terminal completion.
- A nested request completes only its own unowned token as `NotGranted`. `Complete` clears the
  route owner only when the completed token is the active token, so rejected nested callbacks
  cannot erase the outer request's authority.
- Every continuation after a callback-capable boundary checks the exact request token and the
  exact live binding. Existing attempt-ID, placement-ID, entitlement-ID, runtime-swap, disposal,
  and one-callback fences remain in force. Terminal completion clears ownership before calling
  client code, so a later request can start normally.
- `Rebind` still publishes the desired replacement before calling client code, but now always
  detaches the binding captured as `previous` after a pending completion. A callback may install C,
  dispose, or return to A without stranding A's captured handler or allowing the outer operation to
  clobber the newly desired binding.
- The expanded tests prove A-to-B-to-C leaves A/B at zero subscribers and C at one; A-to-B-to-A
  leaves one current A handler and disposal removes it; callback disposal removes original A;
  stale availability is silent while current availability is single-delivery; nested `CanShow`
  requests fail without clearing the outer owner in both the focused fake and real coordinator;
  and ownership is reusable after terminal completion.
- `RewardedAdCoordinator` remains the only production caller of
  `PurchaseService.GrantRewardedAdEntitlement`; the route still has no grant authority.

## Final GREEN evidence

- Focused route regression fixture:
  `/tmp/cm-task10-fix4-route-final.xml` and `/tmp/cm-task10-fix4-route-final.log` — 30 total,
  30 passed, 0 failed/skipped/inconclusive.
- `bash scripts/check.sh` — exit 0, `check: OK`.
- `dotnet restore dotnet/CatMetro.sln -p:RestoreLockedMode=true` — completed; NuGet emitted only
  `NU1900` because its vulnerability feed was unreachable. No tracked lock file changed.
- Linked `.NET` Presentation/Purchases/Cosmetics/Save/Ads filter:
  `/tmp/cm-task10-fix4-dotnet-final/cm-task10-fix4-linked-final.trx` and diagnostic log
  `/tmp/cm-task10-fix4-linked-final.log` — 529 total, 529 passed, 0 failed or skipped.
- Expanded EditMode:
  `/tmp/cm-task10-fix4-editmode-final.xml` and `/tmp/cm-task10-fix4-editmode-final.log` — 202 total,
  202 passed, 0 failed/skipped/inconclusive. Fixture totals were coordinator 53, runtime 4,
  catalogue 72, projection 17, route 30, CSV 5, and rewarded bootstrap 21.
- Graphics-enabled PlayMode, with neither `-nographics` nor `-quit`:
  `/tmp/cm-task10-fix4-playmode-final.xml` and `/tmp/cm-task10-fix4-playmode-final.log` — 41 total,
  41 passed, 0 failed/skipped/inconclusive. Fixture totals were Wardrobe purchase flow 29, mounted
  rewarded placement 6, and rewarded wiring 6.
- These Fix 4 artifacts are the final post-edit result paths. For the earlier Fix 3 review's
  historical path omission, its actual post-restoration Unity artifacts were
  `/tmp/cm-task10-fix3-editmode-verify.xml` plus `.log` (197/197) and
  `/tmp/cm-task10-fix3-playmode-verify.xml` plus `.log` (41/41); they were not the pre-mutation
  `expanded-final` artifacts cited in that report.
- `git diff --check` and `git diff --cached --check` passed. The final changed-path list contains no
  protected board/look/train presentation file. The route has no production entitlement grant
  call, and the coordinator has the sole production call.
- Final Unity logs contain no C# compiler error, unhandled test exception, null reference, aborted
  run, or test-run failure. They contain the known LicensingClient handshake/access-token startup
  noise followed by a valid Unity Personal entitlement, plus the empty `usbmuxd` shutdown error
  after successful completion.

## Exact changed scope

- `.superpowers/sdd/2026-08-30-cat-cosmetics-avatar-system-implementation/task-10-fix-4-report.md`
- `unity/Assets/Scripts/Services/Cosmetics/RewardedAdCosmeticRoute.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/RewardedAdCosmeticRouteTests.cs`

No scene, prefab, package, project setting, generated Unity asset/meta, save-schema file, APK, or
unrelated application file changed.

## Plain unverified items

- No APK/player build, device install, live RevenueCat purchase, live mediated rewarded ad,
  account restore, airplane-mode cold boot, Google Play upload, or store submission was performed.
- No manual screenshot or human visual review was produced. The three-fixture PlayMode run was
  graphics-enabled and its existing painted-pixel assertions passed, but that is not a visual
  approval artifact.
- The full Unity suite and combined TASK 16 validation slot were not run. Evidence is the linked
  suite plus the exact focused/expanded Unity fixtures above.
- Physical-device lifecycle/network loss, mediation SDK event-accessor behavior, and vendor
  callback timing remain represented by deterministic tests rather than device evidence.
