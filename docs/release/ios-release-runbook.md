# Cat Metro iOS release runbook

Last verified: **2026-08-26 PDT**. Shipaton closes **2026-09-30 at 11:45pm PDT**.

The contest requirements supplied for this release lane say the entry is eligible only when Cat
Metro is publicly downloadable from a store and the shipped binary uses RevenueCat to power a real
purchase or ads. TestFlight, “Waiting for Review,” and an SDK that is present but not used do not
meet those gates.

## Date answer

**Earliest realistic public-live date: Saturday, 2026-09-05 PDT.** That is day 10 of the locally
researched 10–14 day Apple path, counted from Wednesday, August 26. It assumes individual enrolment
starts today, the RevenueCat purchase path and store material are ready during enrolment, the first
submission is complete, and Apple does not require a correction.

Use **Wednesday, 2026-09-09** as the working launch target. That is the day-14 end of the realistic
range and still leaves three weeks for the Shipaton video, screenshot, and a corrective release.
An earlier date around September 1 is physically possible if membership is already active and every
review is clean, but it is not a defensible plan. Apple says 90% of submissions are reviewed in less
than 24 hours; that statistic is not an SLA, and it excludes enrolment, build preparation, upload
processing, corrections, and the up-to-24-hour public-release propagation window.

For deadline safety, choose **manual release**, obtain approval early, and click **Release This
Version by September 28 PDT**. Apple says a manual release can take up to 24 hours to appear on the
store. Do not plan around September 30.

Sources: [App Review](https://developer.apple.com/app-store/review/),
[release options](https://developer.apple.com/help/app-store-connect/manage-your-apps-availability/select-an-app-store-version-release-option).

## What must happen today — August 26

- [ ] **Human:** start individual [Apple Developer Program enrolment](https://developer.apple.com/programs/enroll/)
      if it is not already active. It is USD 99/year; use the legal name, 2FA Apple Account, phone,
      and a physical address. The legal name becomes the seller name. Organization enrolment adds
      D-U-N-S and organization-verification work and is not the fast path. Follow Apple’s current
      [identity-verification flow](https://developer.apple.com/help/account/membership/enrolling-in-the-app),
      have a government-issued photo ID ready, and check the Apple Developer app’s current device
      and app prerequisites.
- [ ] **Human:** decide whether the paid Meshy and Tripo assets may ship in a public App Store
      binary. A local build does not answer that licensing question.
- [ ] Lock the intended bundle ID. The repo currently uses `com.catmetro.game`; confirm that exact
      App ID can be registered before treating it as final.
- [ ] As soon as membership activates, the Account Holder must accept the latest agreement in App
      Store Connect, including the Paid Apps Agreement needed for IAP. App Store Connect blocks new
      app-record and IAP creation while the latest agreement is unsigned. Then create the App ID
      and App Store Connect record and immediately submit tax and banking information; do not treat
      submitted information as cleared until each applicable status is active/accepted.
- [ ] Start the RevenueCat iOS path now: one deterministic named product, purchase, entitlement,
      cancellation, and restore is the smallest qualifying Shipaton slice. The SDK, purchase
      service, and Wardrobe path are wired; the human must still supply production config, create
      the App Store product/offering/entitlement, and prove purchase plus restore on device.
- [ ] Start the final-SDK privacy inventory and the privacy-policy page. Store answers must describe
      the binary submitted, not the planned stack.
- [ ] Assign the 1024×1024 App Store icon and screenshot work. Every iOS icon slot is empty in the
      current Player Settings. This is a visual-art lane: open `docs/LOOK.md` and the reference art
      before producing it.

If a Google Play release is also wanted, its 12-testers-for-14-continuous-days clock is independent
and should start today. It is not on the iOS critical path.

## Current repository readiness

| Item | Evidence on 2026-08-28 | Status |
|---|---|---|
| Unity | `6000.3.16f1`; editor and `PlaybackEngines/iOSSupport` installed | Ready |
| Apple toolchain | Xcode 26.5 selected; iPhoneOS 26.5 SDK installed | Meets Apple’s current Xcode 26 / iOS 26 SDK upload floor |
| Bundle ID | `com.catmetro.game` | Configured locally; Apple registration unverified |
| Version/build | `1.0.0` / iOS build `1` | Configured; human confirms the launch marketing version |
| Target | Universal iPhone + iPad; device SDK; minimum iOS 15.0 | Configured |
| Signing | Team ID empty; automatic signing off; no profile | Blocks archive until the human selects a team |
| Protected-data descriptions | Camera, location, microphone, and Bluetooth descriptions empty | Correct only while the final binary never requests them |
| Export declaration | Postprocessor leaves `ITSAppUsesNonExemptEncryption` unset and logs `export-compliance=unset` | Human must determine the answer from the final archive/SDK set in App Store Connect |
| App icons | All iOS/App Store icon texture slots empty | Submission blocker |
| Privacy manifest | No project-owned `PrivacyInfo.xcprivacy` found | Audit final generated project and every SDK; absence alone is not proof of failure |
| Monetization | RevenueCat purchase/restore path and Conductor's Coat UI are present | Production config, App Store product/entitlement, native link, purchase and restore remain unverified blockers |
| Generated project | CLI path exists; no Xcode project has been generated in this lane | Not validated in Unity |
| Package resolution | Manifest asks for URP 17.5.0, Test Framework 1.7.0, and UGUI 2.5.0 while the committed lock records 17.3.0, 1.6.0, and 2.0.0 | Pre-existing reproducibility risk; validate one clean Unity resolution/build and reconcile separately |

Apple’s current build floor is documented at
[Upcoming Requirements](https://developer.apple.com/news/upcoming-requirements/).

### Android-assumption audit

This is source-level evidence, not a substitute for an iOS artifact/device run:

- `UNITY_ANDROID`: no runtime production source is guarded by it. The sole hit is a save-test grep
  that forbids new conditional compilation outside Bootstrap. The iOS plist postprocessor is
  correctly guarded by `UNITY_IOS`.
- Android Java/JNI: no `AndroidJavaClass`, `AndroidJavaObject`, or Android JNI use was found. The
  only Android plug-in payload is `Assets/Plugins/Android/LauncherManifest.xml`; no AAR, JAR, `.so`,
  Apple framework, or bundle plug-in was found. Unity should exclude that manifest from iOS, but
  inspect the generated Xcode project to prove it.
- APK/AAB tooling: `scripts/build-apk.sh` and `CatMetroCliBuild` explicitly produce an Android APK.
  `CatMetroCliAabBuild` requires `CM_AAB_OUT`, a `.aab` extension, and `BuildTarget.Android`. These
  are Editor-only, platform-specific paths and are not called by the new iOS wrapper.
- Save/cache storage: `EngineStorageRoot` uses Unity’s `Application.persistentDataPath` and
  `Application.temporaryCachePath` with no Android conditional. Those APIs map to each platform’s
  sandbox and are the correct iOS boundary; save semantics still require a device test.
- Shipped content: `StreamingAssetsContentSource` handles Android’s `jar:file://` form but constructs
  a local URL as `"file://" + path` when no scheme exists. Whether `UnityWebRequest` normalizes an
  iOS `.app` path containing spaces is unproven here, so signed-build cold boot is a release blocker.
- Development-only capture files live below `persistentDataPath`, so their runtime paths are
  portable, but the documented injection workflow uses `adb push` and has no iOS equivalent yet.
  `DevelopmentConsoleGuard` also suppresses the native console only on Android development builds;
  this does not affect release players but means iOS development diagnostics differ.

## Parallel critical path

| Lane | Start | Typical elapsed time | Depends on | Can run beside |
|---|---:|---:|---|---|
| Individual enrolment | Aug 26 | hours to ~2 days planning allowance; Apple publishes no SLA | Human identity/payment | Product code, art, listing, privacy draft |
| Agreements, tax, banking | Membership activation | Start immediately; clearance time is not guaranteed | Active membership | Code, App ID, listing, TestFlight prep |
| App ID + App Store record + IAP records | Membership activation | Project estimate, not Apple-published timing: 2–4 hours when identifiers/copy are ready | Active membership, latest agreements including Paid Apps accepted, locked IDs | RevenueCat integration, art/privacy |
| RevenueCat purchase slice | Aug 26 | project estimate, not an Apple SLA: 2–4 focused days | Product IDs and SDK integration | Enrolment, listing, signing setup |
| First unsigned Xcode project | Aug 26 | Project estimate, not yet measured in this lane: allow 25–45 minutes for Unity generation, plus fixes | iOS module and build path | Enrolment, listing/privacy/IAP metadata |
| First signed archive | After membership/signing | Project estimate, not yet measured in this lane: allow 5–15 minutes, plus fixes | Generated project, signing team | Listing/privacy/IAP metadata |
| Internal TestFlight | After first upload processes | Same day is possible; processing has no guaranteed SLA | Signed uploaded build | External-beta metadata, store material |
| External TestFlight | Optional | First build assigned to an external group requires Beta App Review; later builds may not require a full review | Uploaded build + beta info | Public-submission preparation |
| App Review | Submit target Sep 2–3 | Apple reports 90% under 24h; budget 1–3 days and one correction | Complete binary, IAPs, metadata, privacy | Shipaton video draft |
| Public propagation | After approval/release | Up to 24 hours | Manual release | Final evidence capture |

## 1. Enrolment, agreements, and account setup

1. Enrol as an individual unless a verified organization account already exists. Individual
   requirements and seller-name implications are on Apple’s
   [enrolment page](https://developer.apple.com/programs/enroll/).
2. When membership becomes active, open App Store Connect → Business:
   - accept the latest agreements, including the Paid Apps Agreement;
   - provide the applicable tax forms;
   - add and verify the bank account.
   Verify the Paid Apps Agreement status is **Active** before sandbox IAP testing or release. Apple
   publishes no SLA for tax or bank review, so submitted details are not evidence of clearance.
3. Complete the EU Digital Services Act trader declaration if launching in EU storefronts. If its
   verification threatens the deadline, the human can decide to exclude EU storefronts from the
   first release; do not silently make that market decision.
4. Keep certificates, private keys, provisioning profiles, and API keys outside git. Never place
   them in repo-relative config or an agent-readable shell environment.

Apple references: [agreements](https://developer.apple.com/help/app-store-connect/manage-agreements/sign-and-update-agreements/),
[tax](https://developer.apple.com/help/app-store-connect/manage-tax-information/provide-tax-information/),
[banking](https://developer.apple.com/help/app-store-connect/manage-banking-information/enter-banking-information/),
[EU trader status](https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-european-union-digital-services-act-trader-requirements/).

## 2. Identifiers, signing, and the app record

1. In Certificates, Identifiers & Profiles, register an explicit iOS App ID whose bundle ID exactly
   matches the committed Player Setting. Enable In-App Purchase. Add other capabilities only when
   the final code uses them.
2. In App Store Connect → Apps → **+** → New App, create the record before uploading:
   - platform: iOS;
   - name: final store name;
   - primary language;
   - bundle ID: the registered explicit App ID;
   - SKU: a stable internal identifier. It is not customer-facing and cannot be changed later.
3. Keep the iOS build number monotonically increasing for every upload. Never reuse a processed
   build number. Choose the customer-facing marketing version deliberately; `0.1.0` is the current
   repo value, not a release decision.
4. Open the generated `Unity-iPhone.xcodeproj`, select the `Unity-iPhone` target, turn on
   **Automatically manage signing**, and select the developer team. Xcode should manage the Apple
   Development/Distribution certificates and provisioning profile for a solo release. Re-check
   signing in every freshly generated project unless a persistent local signing workflow is in use.
5. If automatic signing fails, diagnose the App ID/capability/team mismatch before creating manual
   profiles. A manual App Store distribution profile is the fallback, and still remains human-held.

Apple references: [create the app record](https://developer.apple.com/help/app-store-connect/create-an-app-record/add-a-new-app/),
[certificates](https://developer.apple.com/help/account/create-certificates/certificates-overview),
[App Store provisioning profile](https://developer.apple.com/help/account/provisioning-profiles/create-an-app-store-provisioning-profile).

## 3. RevenueCat and launch IAP

The minimum qualifying and reviewable path is one named non-consumable, such as a named cosmetic or
supporter/remove-ads entitlement. Real money must never reach randomness; the surprise currency
remains earn-only.

1. Install the supported RevenueCat Purchases Unity SDK and configure an iOS RevenueCat app with
   the exact bundle ID. Keep the public SDK key in the intended runtime configuration; keep App
   Store Connect private keys out of the repo. The release archive must use RevenueCat’s public iOS
   SDK key, never a RevenueCat Test Store key.
2. In App Store Connect, create stable product IDs, localizations, prices, availability, tax
   category, review screenshot, and review notes for every launch product.
3. In RevenueCat, create the entitlement and offering and attach the Apple products. For the iOS
   15+/StoreKit 2 path, upload RevenueCat’s required In-App Purchase Key (Issuer ID, Key ID, and
   private `.p8` key) and validate its status. An App Store Connect API key is recommended for
   product/price import; it is a different credential. Keep all private keys outside the repo.
4. Implement purchase, cancel/error, entitlement refresh, and **Restore Purchases**. A successful
   StoreKit dialog without a RevenueCat entitlement is not a qualifying implementation.
5. Test the exact release path in sandbox/TestFlight on a real iPhone:
   - successful purchase grants the named item;
   - cancellation grants nothing and leaves the game usable;
   - relaunch preserves/re-fetches entitlement;
   - restore on a fresh install restores the non-consumable;
   - network failure does not consume money twice or lock the boot flow.
   Before platform-sandbox testing, confirm the Paid Apps Agreement is Active. For a development
   build installed directly, create and use a Sandbox Apple Account. An ordinary TestFlight tester
   uses their production Apple Account while TestFlight purchases still occur in sandbox. For
   special failure/renewal scenarios on an app in your own developer account, Apple also documents
   an optional TestFlight flow that signs Media & Purchases out of production and into a Sandbox
   Apple Account; follow Apple’s current procedure exactly. Treat TestFlight sandbox prices and
   localized metadata as non-authoritative. Verify the actual purchase flow, resulting entitlement,
   and transaction in RevenueCat’s sandbox dashboard.
6. Capture evidence for the contest video: show the product, Apple purchase sheet, completed
   RevenueCat-backed entitlement, and resulting in-game item. The contest requirements supplied for
   this lane cap that submission video at two minutes.

If the contest rules explicitly accept RevenueCat Ads, name the exact RevenueCat Ads/AdTracker
integration and the ad network used. RevenueCat ad monetization is currently beta and works beside
an ad SDK; it does not serve an ad itself. For Cat Metro v1, rewarded video only is a product-scope
decision, not an Apple rule, and ATT refusal must never block the reward. If server-verified rewards
are used, configure and test the network-specific verification path. Shipping both IAP and ads is
not required by the supplied contest gate; shipping at least one genuinely RevenueCat-powered path
is.

References: [RevenueCat Unity installation](https://www.revenuecat.com/docs/getting-started/installation/unity),
[connect the App Store](https://www.revenuecat.com/docs/projects/connect-a-store),
[RevenueCat service credentials](https://www.revenuecat.com/docs/store-configuration/app-store/service-credentials-index),
[RevenueCat Apple sandbox testing](https://www.revenuecat.com/docs/test-and-launch/sandbox/apple-app-store),
[RevenueCat ad monetization](https://www.revenuecat.com/docs/ad-monetization),
[configure In-App Purchases](https://developer.apple.com/help/app-store-connect/configure-in-app-purchase-settings/overview-for-configuring-in-app-purchases/).

## 4. Generate and inspect the Xcode project

An agent does not launch Unity. The human should run this from a clean checkout **today**, even if
membership and signing are still pending. Unsigned project generation can expose package,
compilation, IL2CPP, postprocessor, and generated-project failures while enrolment runs in parallel:

```bash
bash scripts/check.sh
bash scripts/test.sh
bash scripts/build-ios.sh build/ios-release-1
```

The output directory must be new or empty. The wrapper deliberately stops on a non-empty directory
so an old `project.pbxproj` cannot turn a failed build into a false success. It generates an Xcode
project only; it does not sign, archive, export, or upload.

Before archiving, inspect the generated project and final dependency set:

- bundle ID, marketing version, and build number match App Store Connect;
- deployment target is iOS 15.0 and device target is universal iPhone/iPad;
- Release configuration is selected and the build is not a Unity Development Build;
- team and automatic signing are valid for every generated target;
- the required 1024×1024 App Store icon and device icons are present;
- `Info.plist` contains the expected export-compliance key;
- every protected-data usage description is present **only when the final binary requests that
  API**;
- for each third-party SDK, determine whether it is on Apple’s listed-SDK requirement or otherwise
  uses required-reason APIs, collects data, enables collection, or contacts tracking domains;
  include the required manifest and, for listed binary dependencies, the required signature;
- every app executable or dynamic library that uses a required-reason API has an accurate bundled
  privacy manifest;
- no Android manifest, Gradle, keystore, AAR/JAR, or Android-only native plug-in entered the project.

Then the human archives with Xcode’s Product → Archive and uses Organizer → Validate App. This
runbook intentionally does not automate archive or upload. Record the source commit, clean/dirty
tree state, archive version/build, and any validation warnings.

## 5. Privacy, encryption, audience, and listing

### Privacy nutrition label and manifest

- Publish a real privacy-policy URL. Apple requires one for iOS apps.
- Inventory the exact final versions of RevenueCat, Unity services, ad/CMP, analytics, crash, and
  any other SDK. App Privacy answers include data collected by third-party partners and whether it
  is linked to identity or used for tracking.
- App Privacy responses and privacy manifests are distinct. App Privacy answers are required for the
  submitted app and its third-party partners. A manifest is required where the app or SDK uses
  required-reason APIs, and Apple-listed third-party SDKs have manifest/signature requirements;
  audit both against the final archive.
- If the final app or a third-party SDK performs tracking as Apple defines it, request ATT before
  tracking, add a truthful `NSUserTrackingUsageDescription`, and test both allow and deny. Do not
  add ATT merely because ads exist, and never gate a purchase/reward behind the prompt.

References: [manage App Privacy](https://developer.apple.com/help/app-store-connect/manage-app-information/manage-app-privacy/),
[privacy manifests](https://developer.apple.com/documentation/bundleresources/privacy-manifest-files),
[third-party SDK requirements](https://developer.apple.com/support/third-party-SDK-requirements/).

### Export compliance

`CatMetroIosPostProcess` does not write `ITSAppUsesNonExemptEncryption`. That omission is
intentional: the human release owner must determine the answer from the **final binary**, including
RevenueCat, OneSignal, analytics, and every other shipped SDK. App Store Connect will ask during
the human-only upload. Record the determination and provide any documentation it requests; do not
turn a source-only assumption into a plist declaration.

Reference: [export compliance overview](https://developer.apple.com/help/app-store-connect/manage-app-information/overview-of-export-compliance).

### Audience and store material

- For this launch strategy, do not select Apple’s Kids Category. Complete the age-rating
  questionnaire truthfully; do not misrepresent content to obtain the intended 13+ floor.
- Listing copy and imagery stay general-audience: puzzle-first screenshots, no child-coded terms.
- The App Store needs its own required screenshots and app icon; the Shipaton’s one screenshot is a
  separate judging asset.
- Complete category, age-rating questionnaire, copyright, support URL, privacy-policy URL,
  description, keywords, promotional text if used, pricing, and territory availability. Also
  provide the required App Review contact name, email, and international-format phone number; add
  non-expiring demo credentials only if login is required.

## 6. TestFlight

Internal TestFlight supports up to 100 App Store Connect users and does not require Beta App Review.
External testing supports up to 10,000 testers; the first build assigned to an external group
requires review, while later builds may not. Builds expire after 90 days. External testing is
optional and should not hold the public critical path.

On the exact candidate, test at minimum:

- cold boot reaches the first level and reads all StreamingAssets content;
- the content load works from the signed `.app` bundle, whose path may contain spaces;
- save, quit, relaunch, overwrite, and restore work in the iOS app container;
- purchase, cancellation, entitlement refresh, and restore pass through RevenueCat;
- any rewarded ad grants once on success and never depends on ATT acceptance;
- airplane-mode startup and return-to-network do not corrupt save or entitlement state;
- background/resume, rotation/orientation, audio interruption, and safe-area UI are usable;
- no new Xcode/Unity errors occur, and a clean install behaves like an upgrade where applicable.

The production content loader currently constructs a local `file://` URL by string concatenation.
That works on the exercised Android/editor paths but has not been validated in an iOS app-bundle
path with spaces. Treat the TestFlight cold-boot check as required evidence, not a paper conclusion.

Reference: [TestFlight overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview).

## 7. Submit and release

1. Upload through Xcode Organizer or Transporter — human only — and wait for processing.
2. Select the processed build in the App Store version.
3. Attach every launch IAP to the app submission. Apple requires the first consumable and the first
   non-consumable of their respective types to be submitted with a new app version; additional
   products of an already-approved type may be submitted separately. Do not submit the binary alone
   and assume a launch product becomes saleable later.
4. In App Review notes, give exact navigation steps for the purchase/ad, explain what it unlocks,
   identify the restore action, and provide credentials only if the app truly has an account login.
5. Complete export-compliance, content-rights, advertising identifier/tracking, age-rating, and
   privacy answers for the submitted binary.
6. Select **Manually release this version** and submit app + IAPs for review.
7. If rejected, answer the precise issue, produce a new monotonically numbered build when needed,
   rerun the candidate tests, and resubmit. Do not remove functionality merely to hide it from
   review.
8. After approval, release by September 28 PDT. Confirm the public product page from a signed-out
   browser/device in at least one enabled storefront; “Pending Developer Release” is not live.
9. Record the public App Store URL and capture the final Shipaton screenshot/video from the shipped
   behavior. The judges may never install the app.

Apple references: [submit an app](https://developer.apple.com/help/app-store-connect/manage-submissions-to-app-review/submit-an-app),
[submit an IAP](https://developer.apple.com/help/app-store-connect/manage-submissions-to-app-review/submit-an-in-app-purchase/),
[release a version](https://developer.apple.com/help/app-store-connect/manage-your-apps-availability/select-an-app-store-version-release-option).

## What this lane did not verify

- No Unity iOS build or Xcode project was generated.
- No clean Unity package resolution verified the current manifest/lock mismatch; this lane did not
  update packages or their lock.
- No archive was compiled, signed, validated, exported, uploaded, or installed.
- Apple membership, agreements, bank/tax status, App ID availability, App Store Connect record,
  certificates, profiles, and store metadata are not visible from the repo.
- No final SDK/archive exists from which to prove privacy labels, privacy manifests, usage
  descriptions, or export classification.
- The RevenueCat purchase/restore path exists in source, but native iOS linking, production
  configuration, localized product loading, purchase, cancellation, entitlement refresh, and
  restore are unverified.
- No iOS device has exercised boot, StreamingAssets, save persistence, purchase, restore, or ads.
- Paid-asset permission for public distribution remains a human decision.
