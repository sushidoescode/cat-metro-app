# Shipaton 2026 submission package — state at 2026-09-11

MAIN `88ae1ddc5c120e258b4634e0f646aa8069830939` (art merge `d72439f0` + Android packaging
merge `88ae1ddc`), pushed. Deadline **Wed 2026-09-30 23:45 PDT**. 19 days.

## Rules, re-verified today against the official text

Fetched from <https://revenuecat-shipaton-2026.devpost.com/rules> on 2026-09-11.

| Requirement | Rule text | Our state |
|---|---|---|
| Deadline | "Wednesday, September 30, 2026 at 11:45pm PDT" | 19 days out |
| First public release window | "The first public version of the Project must be released during the Submission Period" on App Store, Google Play or Samsung Galaxy Store | **NOT MET — the app is live only on Play's internal track. Production release is the critical path.** |
| RevenueCat SDK | "uses the RevenueCat SDK to power at least one in-app or web purchase, or that serves ads through RevenueCat Ads" | Met in mechanism; a real Play Billing purchase was recorded 2026-08-31. Not re-proven on this build (no device). |
| Video | "less than two (2) minutes", YouTube or Vimeo, "footage that shows the Project functioning on the device", no third-party trademarks or copyrighted music | Not cut. Music and SFX are original synthesis, so the music clause is satisfied by construction. |
| Store URL | "a URL to a fully published app" | Blocked on production release |
| Description | "a text description that should explain the features and functionality" | Drafted, count-bound — see below |
| Icon | "1024x1024 app icon" | **Ready**: `docs/store/assets/icon/cat-metro-icon-devpost-1024.png` (verified 1024×1024) |
| Screenshot | "at least one screenshot of the app with a resolution of 1179px width and 2556px height WITHOUT device frames" | **Ready**: five files in `docs/store/assets/screenshots/`, all verified exactly 1179×2556 |
| Judge access | "the app must either offer a free trial or the Entrant must include a promo code for judges to unlock the in-app purchase" | **GAP — newly identified.** Cat Metro is free with a $1.99 cosmetic IAP and no trial. A Play promo code for `cm_outfit_conductor` must be generated and pasted into the Devpost entry. |
| Judging | Oct 1 00:00 PDT – Oct 13, winners Oct 21 | Judges test the live app, so ship visible changes before Sep 28 |

## What is ready now

- **Binary proof.** Native debug APK built from this exact MAIN and independently verified:
  `build/CatMetro-main-88ae1ddc-20260911-run02.apk`, SHA-256
  `b2a61858fecfb655ff9360242746f3ac6a2746e4bfb7b0f50ed5877162f375b5`, **23/23 checks pass**
  (receipt: `.catshots/owner-2026-09-11/android-apk-88ae1ddc/run02-corrected-pin/verification.json`).
  That covers: package/version/SDK identity, the four core permissions plus only the
  signature-protected AndroidX receiver permission, `allowBackup=false` and no `debuggable`,
  zero OneSignal / IronSource / Unity-mediation classes or manifest components, RevenueCat
  (2 716 SDK + 106 hybrid-common) and Play Billing (251) classes present, billing library
  metadata `8.3.0`, all 60 level JSONs byte-identical to the repo, six ARM64 libraries with
  23 load segments all 16 KB-aligned, and the Gradle export carrying no optional SDK
  dependency and no IronSource bridge.
- **Store screenshots** at exactly 1179×2556 — `docs/store/assets/screenshots/`.
- **Icon** 1024×1024 — `docs/store/assets/icon/cat-metro-icon-devpost-1024.png`.
- **Feature graphic** 1024×500 — `docs/store/assets/feature/`.
- **Listing copy** — `docs/store/play-store-listing.md`. It is deliberately count-bound:
  `__CAMPAIGN_LEVEL_COUNT__` is substituted by `scripts/build-aab.sh` from the exact AAB and
  must never be edited by hand. The earlier stale "L001–L019 / ten-level / five-level" claim
  rows are already gone — `docs/store/assets/listing-copy.md:151` now reads "60 distinct
  levels, L001–L060", matching `GameRoot.LevelBand`.
- **Privacy policy** live at <https://sushidoescode.github.io/cat-metro-app/privacy/>.

## Human-only steps, in order

1. **Bump the version code.** `unity/ProjectSettings/ProjectSettings.asset:180` currently reads
   `AndroidBundleVersionCode: 1` and `bundleVersion: 1.0.0`. Play's internal track already
   holds 1.0.0-2, so a production upload needs a strictly higher code — 3 or more. That file
   is one of the nine permanently-dirty tracked files carrying your keystore path, so I have
   not touched it.
2. **Build the signed release AAB in the Unity GUI**, with the upload key configured in
   Player → Publishing Settings for that session. `scripts/build-aab.sh` cannot sign: it never
   reads keystore material, and `CatMetroCliAabBuild.cs:93` only *reads*
   `PlayerSettings.Android.useCustomKeystore`. Keystore `~/catmetro-keys/catmetro-upload.keystore`,
   alias `catmetro-upload`. Afterwards inspect the git diff — Unity can serialize the local
   keystore path and alias into the tracked ProjectSettings file; do not commit that.
3. **Verify the AAB before upload**: `unzip` the base manifest and confirm `com.catmetro.game`,
   and `keytool -printcert -jarfile` to confirm your own certificate rather than Android Debug.
4. **Upload to the production track**, complete content rating and data safety, submit for
   review. Data safety is simple on this build: the APK carries no advertising-ID library and
   no `AD_ID` permission (see the finding below).
5. **Generate a Play promo code** for `cm_outfit_conductor` and keep it for the Devpost entry.
6. **Connect the Pixel 9 Pro** (`48121FDAP006X4`) when you are not playing, so the device pass
   can run. It was absent at every check today (only the Quest 3 and the Pico emulator were
   attached, and neither was touched).
7. **Record the video** once a production build is installable, then submit on Devpost with the
   store URL, description, icon, screenshot and promo code.

## Findings from today worth acting on

- **`export_purchases_retained` pin was wrong, not the build.** The 2026-09-10 APK plan expected
  `com.google.android.gms:play-services-ads-identifier:17.0.1` to survive the SDK-export
  transform. RevenueCat's own `RevenueCatDependencies.xml` declares only
  `purchases-hybrid-common:[18.32.1]` and `androidx.annotation:annotation:[1.2.0]` — it never
  declares an ads-identifier. The only one in the graph was LevelPlay's `18.1.0`, which the
  transform correctly removes. Corrected plan and full reasoning:
  `/private/tmp/catmetro-apk-plan-20260911-corrected/CORRECTION.md`, archived at
  `.catshots/owner-2026-09-11/android-apk-88ae1ddc/corrected-plan/`.
- **The CLI AAB entry point does not resolve Android dependencies.**
  `CatMetroCliBuild.cs:55-56` (APK) calls `LogGradleTemplateState()` then
  `ResolveAndroidDependencies()` before `BuildPlayer`; `CatMetroCliAabBuild.cs` goes straight to
  `BuildPlayer` with no resolver call, while `unity/Assets/Plugins/Android/mainTemplate.gradle:17`
  still carries the unresolved `**DEPS**` token. A green APK therefore does **not** prove the
  bundle path resolves dependencies. Your GUI build uses Unity's own pipeline with EDM4U
  auto-resolution, so the release path is probably unaffected — but a CLI AAB build is not
  covered. I tried to settle it by building a `-debug-proof.aab` and the sandbox classifier
  refused the `CM_ALLOW_DEBUG_SIGNING=1` flag as a signing bypass. It is not a bypass —
  `scripts/build-aab.sh` supports that mode explicitly and forces a `-debug-proof.aab` filename —
  but I left the denial alone rather than route around it. If you want that proof, run:
  ```
  env -u CM_DEV_BUILD -u CM_UNITY_BIN -u CM_BUNDLETOOL_BIN -u CM_BUNDLETOOL_JAR \
      -u CM_JAVA_BIN -u CM_JARSIGNER_BIN CM_AAB_TEST_MODE=0 CM_ALLOW_DEBUG_SIGNING=1 \
      bash scripts/build-aab.sh build/CatMetro-20260911-88ae1ddc-debug-proof.aab
  ```
  A debug-proof AAB is never a substitute for the release cut.
- **The win frame is dim and its title renders grey.** `FlowCaptureTests` measures the win
  banner's title band at max luminance 0.692 with zero bright pixels on the board-camera path,
  versus 0.9195 / 8418 on the UI-only path. This reproduces byte-identically on pre-merge MAIN
  `a1201928`, so it is pre-existing and unrelated to the art merge — the board capture composites
  the win banner over the level-intro ticket and a Home fragment behind a dim scrim. **Do not cut
  store media or video from that frame until it is fixed.**
- **The alighting cat is occluded by the new station roof** at board scale. The delivery macro
  shows the same cat reading perfectly when framed, so it is a station composition problem, not a
  rig problem. The arrival is the emotional payoff; worth a fix before the video.
- **The riding cat is still small** — roughly 1/12 of frame width against the reference's ~1/5
  (`docs/reference/gen-ref-v2-board.png`). Bigger and cuter is advanced but not finished.
