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
2. **Build the signed release AAB.** *(Corrected 2026-09-11 — my earlier claim that this
   requires the GUI was wrong.)* `scripts/build-aab.sh` already accepts a custom-signed build:
   its receipt regex at `:542` treats `signing=custom` as first class, the debug-signing refusal
   at `:551-555` only fires for `signing=debug`, and the success epilogue at `:728-735` is
   written for the custom case. What is genuinely missing is a **password seam**, and that is a
   deliberate policy of this repo, not a Unity limitation:
   `PlayerSettings.Android.keystorePass` / `.keyaliasPass` have public setters in 6000.3.16f1, but
   `tests/unity/cli-aab-build.test.sh:56-59` fails the build if `CatMetroCliAabBuild.cs` so much
   as names a keystore identifier or writes any `PlayerSettings.X`. Keystore path and alias are
   persisted in `ProjectSettings.asset:276-277` with `androidUseCustomKeystore: 1` at :289 — but
   only as your uncommitted working-tree drift; committed MAIN has them empty and the flag 0.
   There is no password field in that file at all.
   **Practical consequence:** a batch `scripts/build-aab.sh` run today inherits
   `useCustomKeystore: 1` and dies in `PrepareForBuild` with "Unable to sign the application;
   please provide passwords!" — exactly the 2026-09-05 failure recorded at
   `CatMetroCliBuild.cs:13-20`. So the GUI build remains the path that works **today**, and the
   irreducible human part is supplying the two passwords to whichever process signs. Afterwards
   inspect the git diff — Unity serialises the keystore path and alias into the tracked
   ProjectSettings file; do not commit that.
   **Do NOT upload `build/CatMetro-1.0.0-2.aab`.** It is genuinely custom-signed by your own
   certificate (CN=Sushant Srikrish, verified with `jarsigner`), but it is from 2026-08-30 and
   predates both the art merge and the SDK-export transform: it still carries OneSignal and
   Firebase resources. Cut a fresh bundle.
3. **Verify the AAB before upload**: `unzip` the base manifest and confirm `com.catmetro.game`,
   and `keytool -printcert -jarfile` to confirm your own certificate rather than Android Debug.
4. **Upload to the production track**, complete content rating and data safety, submit for
   review. *(Corrected 2026-09-11 — my earlier "data safety is simple" line was wrong.)* The
   build DOES carry `play-services-ads-identifier:17.0.1` and the `AdvertisingIdClient` classes,
   transitively via RevenueCat; what it lacks is the `AD_ID` permission, because 17.0.1 predates
   the AAR that declares one. Evidence and the corrected rule:
   `.catshots/owner-2026-09-11/android-apk-88ae1ddc/ads-identifier-recheck/`. What the form has to
   declare, at minimum: **purchase history** (RevenueCat + Play Billing, transmitted off-device),
   **device or other IDs** (RevenueCat's anonymous app-user ID — answering "no collection" here is
   the likeliest way to get the form wrong), and possibly **app performance/diagnostics**
   (`api-diagnostics.revenuecat.com` is in the binary; whether the SDK enables it by default is
   vendor behaviour I could not settle). Gameplay analytics currently declare **nothing**:
   `unity/Assets/Resources/Config/analytics_transport.json` has `enabled: false` and an empty
   token — if you turn that on before release, the form changes, per
   `docs/release/analytics-data-declaration.md`. Declare against the **exact uploaded bundle**,
   not against this APK.
5. **Generate a Play promo code** for `cm_outfit_conductor` and keep it for the Devpost entry.
6. **Connect the Pixel 9 Pro** (`48121FDAP006X4`) when you are not playing, so the device pass
   can run. It was absent at every check today (only the Quest 3 and the Pico emulator were
   attached, and neither was touched).
7. **Record the video** once a production build is installable, then submit on Devpost with the
   store URL, description, icon, screenshot and promo code.

## Findings from today worth acting on

- **`export_purchases_retained`: the pin looked in the wrong place, and so did my first
  correction.** `play-services-ads-identifier:17.0.1` arrives **transitively** from
  `com.revenuecat.purchases:purchases`, downstream of the export transform, which only rewrites
  declared `implementation` lines. So it can never appear as a declared coordinate (killing the
  original pin) and it is nevertheless in the binary (killing my correction, which expected none).
  Verified by hand on the APK: `play-services-ads-identifier.properties` → `version=17.0.1`,
  `AdvertisingIdClient` in `classes.dex` and `classes5.dex`, 5 merger-report hits, and zero
  `AD_ID`. The transform's `RevenueCatIdentifier` constant names a real arrival it structurally
  cannot reach — not dead weight. Correct rule and receipt:
  `.catshots/owner-2026-09-11/android-apk-88ae1ddc/ads-identifier-recheck/` (5/5 green).
- **The CLI AAB dependency claim was wrong.** *(Corrected 2026-09-11.)* `**DEPS**` is Unity's own
  substitution token in a *source* template; it does not survive into generated output, and the
  only AAB that exists (`build/CatMetro-1.0.0-2.aab`) was built through that path with a full
  resolved dependency graph. The same post-generate callbacks run for AAB and APK — they share
  one `BuildPlayer` pipeline. The real, smaller finding that survives: `CatMetroCliBuild.cs:55-56`
  calls `LogGradleTemplateState()` + `ResolveAndroidDependencies()` before building while
  `CatMetroCliAabBuild.cs` does not, and resolution is persisted in the
  `unity/Assets/Plugins/Android` templates rather than recomputed. So a batch AAB inherits
  whatever those templates held at build time — if someone changes an EDM4U-managed package and
  builds an AAB with no intervening editor session or APK build, the bundle is cut from stale
  coordinates silently, since the AAB receipt has no template/resolve line to notice it by. That
  is a staleness gap worth closing (a few lines, and it collides with no test gate — the denylist
  at `cli-aab-build.test.sh:56-59` forbids keystore identifiers and `PlayerSettings` writes, not
  a resolver call), not a broken build path.

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
