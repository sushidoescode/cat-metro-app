# Google Play release-candidate checklist

Companion to `docs/release/play-release-runbook.md`. Use this once per AAB. It records what the
exact candidate proves; a branch name, plan, or prior artifact proves nothing about the upload.

Every Play Console action, keystore action, upload, rollout, account decision, and commercial
asset-licensing decision is human-only. This repository prepares and checks artifacts but never
uploads them.

## 1. Account and calendar

- [ ] Record the Play developer account type and creation date privately.
- [ ] Organization or personal created on/before 2023-11-13: use the exempt path.
- [ ] Personal created after 2023-11-13: use the gated path; keep at least 12 testers opted in
      continuously for 14 days, then obtain production access before production review.
- [ ] Finish identity, payment-profile, email, and device verification.
- [ ] For the gated path, record when Console first shows at least 12 opted-in testers and keep a
      safety cohort opted in until production access is granted. Individual confirmations are
      self-reported planning evidence; Console's aggregate and eligibility state are authoritative.
- [ ] Treat **2026-09-01** as the operational target for the closed release to be live with at least
      12 opted in; every day later consumes production-review safety margin.

## 2. Candidate source and settings

- [ ] TASK 15 has landed on `main` before the production candidate is cut. Do not merge
      `feat/level-variety` into `feat/store-release` as a shortcut; the merge order belongs to
      TASK 15's lane.
- [ ] Exact candidate commit and `git status --short` are recorded.
- [ ] Unity is `6000.3.16f1` and the shipped scene is `Assets/Scenes/Game.unity`.
- [ ] Package ID is the intentional permanent value `com.catmetro.game`.
- [ ] `bundleVersion` is `1.0.0` or the deliberately chosen public version.
- [ ] `AndroidBundleVersionCode` is positive, no greater than Play's `2100000000` maximum, exceeds
      every value previously uploaded to this Play app, and is incremented for every new attempt.
- [ ] Target API is at least 36; current minimum API is 25. API 36 is mandatory for new apps and
      updates submitted from 2026-08-31.
- [ ] Android is ARM64-only and uses IL2CPP.
- [ ] Development Build is off; the AAB builder explicitly selects Android and forces
      `EditorUserBuildSettings.buildAppBundle = true` for its invocation.
- [ ] Forced external-storage and forced-internet settings are off. Final merged permissions are
      reviewed across every module in the built AAB, not inferred from Project Settings.

## 3. Repository gates

- [ ] `bash scripts/check.sh` passes.
- [ ] `bash scripts/test.sh` passes; record its total.
- [ ] Unity EditMode and relevant PlayMode tests pass on the build machine. Never add `-quit` to a
      `-runTests` invocation.
- [ ] The exact campaign corpus passes its validator/solver gates.
- [ ] No unexpected source, lock-file, package, scene, or serialized-setting drift remains.

Shell gates and headless .NET tests do not prove Unity compilation, a player build, visuals, or
device behavior. Record those separately.

## 4. Human signing preparation

- [ ] Create the upload keystore outside every checkout and back it up in two private places.
- [ ] Configure it in Unity without exposing its location, alias, or password to an agent.
- [ ] Confirm `.gitignore` covers common key extensions, `keystore.properties`, `local.properties`,
      AAB/APK outputs, and `build/`.
- [ ] Remember that ignore rules do not protect the already tracked
      `unity/ProjectSettings/ProjectSettings.asset`. Inspect its diff after every signed build and
      remove only signing-path/alias drift you personally introduced.
- [ ] Never commit a keystore, signing password, local key path, tester roster, or service secret.

## 5. Build and exact-artifact proof

The human runs:

```sh
AAB_OUT="build/CatMetro-1.0.0-1.aab"
bash scripts/build-aab.sh "$AAB_OUT"
```

- [ ] Use a fresh versioned `AAB_OUT` for every candidate. Reuse that exact path for the generated
      listing, SHA-256, `keytool -printcert -jarfile "$AAB_OUT"`, device, and upload checks; never
      substitute an older artifact.

The wrapper must exit zero and report all of these before its output is considered a candidate:

- [ ] Unity exited zero and produced a fresh AAB in an isolated same-filesystem staging directory.
- [ ] ZIP/CRC shape distinguishes an AAB from an APK with the wrong extension.
- [ ] `bundletool validate` accepted the bundle.
- [ ] The built base manifest—not merely tracked settings—has the expected package, version
      name/code, minimum/target SDK, and `allowBackup=false`; every module is non-debuggable and
      declares only the explicit permission allowlist.
- [ ] Only the ARM64 IL2CPP native payload is present across every AAB module.
- [ ] Strict `jarsigner` verification proves every archive entry is covered by the JAR signature;
      the human fingerprint comparison below proves which upload certificate signed it.
- [ ] Every normal-progression level named by the build exists in the AAB and its bytes match the
      staged source file.
- [ ] The final AAB SHA-256 matches the SHA embedded in its generated
      `*-play-listing.md` sibling.

The wrapper does not prove that the verified certificate is the upload certificate registered in
Play Console. The human compares the AAB signer SHA-256 fingerprint with the intended upload key and
the Play Console record before upload.

## 6. Device and product proof

- [ ] Before any device command, run `adb devices -l` and confirm the target is the Pixel 9 Pro
      (`model:` for serial `48121FDAP006X4`). Never install on the Quest or Pico devices.
- [ ] Install the exact candidate through a local bundletool install flow or the intended Play test
      track and complete start, route, win, next, pause/resume, and cold-restart smoke tests.
- [ ] Inspect device logs for new Unity or Android runtime errors.
- [ ] Confirm screenshots and video show this exact candidate; use real device/capture-rig frames
      only. Never fabricate gameplay.

## 7. RevenueCat eligibility

- [ ] The public candidate contains the RevenueCat SDK and it powers a deterministic named purchase
      through Google Play Billing, or a compliant RevenueCat Ads surface.
- [ ] No Stripe path exists for in-app digital goods and no paid randomness exists.
- [ ] A physical-device licensed/sandbox purchase grants the expected entitlement.
- [ ] Restart and restore-purchases retain/recover it correctly.
- [ ] The transaction/entitlement appears under the expected product identifier in RevenueCat.
- [ ] For the named one-time product, redeem one Play promo code on the Play-delivered build and
      prove RevenueCat grants/restores it; retain a separate unused judge code outside the repo.
- [ ] The Shipaton submission supplies a free trial or working promo code that unlocks every
      premium feature. The planned one-time cosmetic uses the promo-code path.
- [ ] Privacy policy, Data safety, content rating/digital-purchase answers, and listing claims match
      this same RevenueCat-enabled candidate.

RevenueCat is absent at this branch's current baseline; public rollout is blocked until these checks
pass on the integrated production candidate.

## 8. Listing, graphics, and licensing

- [ ] Paste title, descriptions, and What's new only from the generated listing beside the exact
      AAB. Never hand-type 17 or 19.
- [ ] Use a 512×512 Play icon, 1024×500 feature graphic, and real 1080×1920 Play screenshots.
      The separate 1179×2556 frameless capture is for Devpost, not Play.
- [ ] Target 13+ brackets only; never select Apple's Kids Category in the separate iOS flow.
- [ ] Ads, purchases, privacy, and data declarations describe the binary, not the roadmap.
- [ ] The human has made the commercial licensing decision for every asset embedded in this exact
      AAB. A clean Git status does not prove that gitignored Unity resources were excluded.

## 9. Human upload and public verification

- [ ] Complete all Console listing, graphics, privacy, app-access, audience, ads, content-rating,
      and Data safety prerequisites for the exact binary.
- [ ] Human uploads to the correct closed or production track and resolves every Console error.
- [ ] Gated account only: production access is approved before the separate production submission.
- [ ] After production review, human rolls out and confirms the listing is publicly visible while
      logged out in the intended country.
- [ ] Install the Play-delivered build on the Pixel; repeat the gameplay and RevenueCat
      purchase/restore checks.
- [ ] Record the public URL and live timestamp before the Shipaton deadline.

## Candidate receipt

| Field | Value |
|---|---|
| Git commit / tree status | |
| versionName / versionCode | |
| AAB path / SHA-256 | |
| Generated listing path / embedded SHA | |
| Upload-certificate SHA-256 matched | |
| Account branch | exempt / gated |
| Closed-track tester-12 timestamp | n/a or private-record reference |
| Unity tests / device smoke result | |
| RevenueCat purchase / restore result | |
| Human licensing decision reference | private record |
| Track / upload date / public-live date | |
