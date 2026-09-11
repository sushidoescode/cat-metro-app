# Google Play Data safety — assessment for `com.catmetro.game`

Written 2026-09-11. Measured against `build/CatMetro-main-88ae1ddc-20260911-run02.apk`
(sha256 `b2a61858…f375b5`, built from `88ae1ddc`, an ancestor of MAIN). The `88ae1ddc..ba603c76`
delta is docs, store PNGs and presentation/test code — nothing that changes data handling.

**Re-verify against the exact uploaded AAB before filing.** That is a Play requirement and it is
also the thing that went wrong last time: the 2026-08-30 AAB contains OneSignal and Firebase, which
this build does not.

## Answer: Yes, and declare exactly two data types

### ① Financial info → Purchase history — **COLLECTED**

- Collected **Yes**; Shared **No** (RevenueCat is a processor under Play's service-provider
  exemption; its [DPA](https://www.revenuecat.com/dpa) states Data Processor).
- Processed ephemerally **No**. Required, not optional — users cannot turn it off.
- Purposes: **App functionality** and **Analytics**.

Evidence: `purchases-hybrid-common:[18.32.1]` declared; RevenueCat + Billing classes present in the
dex; backend registered for `UNITY_ANDROID` at `RevenueCatBackend.cs:47` and configured at `:157`;
production `goog_`-prefixed key present with `useTestStore: false`. RevenueCat's own guidance:
"RevenueCat collects a customer's purchase history… This data collection is required and cannot be
turned off."

### ② Device or other IDs — **COLLECTED**

- Collected **Yes**; Shared **No**. Processed ephemerally **No** — the ID persists across sessions.
- Required. Purpose: **App functionality only**. Do **not** tick Advertising or marketing.

**This row exists because of RevenueCat's anonymous App User ID, not the advertising ID.**
`Builder.Init(apiKey)` is called with no app user ID (`RevenueCatBackend.cs:151`), so the SDK
generates and transmits `$RCAnonymousID:<uuid>`. Play's definition of this type expressly includes
vendor-assigned app-scoped IDs such as the Firebase installation ID.

### Advertising ID — leave undeclared, and do not add the permission

The two facts are different and both are true:

- `play-services-ads-identifier:17.0.1` and the `AdvertisingIdClient` classes **do ship**, as a
  transitive RevenueCat dependency. They cannot be stripped by the export transform, which only
  rewrites declared coordinates, and should not be.
- **No `AD_ID` permission is declared** (17.0.1 predates the AAR that declares one; the merger
  report has zero `AD_ID` hits), the app targets SDK 36, and there is **no first-party call site** —
  `collectDeviceIdentifiers`, `setAttributes` and `logIn` each have **0** occurrences in
  `unity/Assets/Scripts`. On Android 13+ any access without the permission returns zeros.

If Console shows an advertising-ID warning it is reconciling against a permission you do not
declare. Keep both sides negative; do not add the permission.

### Security practices

- Encrypted in transit: **Yes** (HTTPS to RevenueCat).
- Data deletion requests: **Yes** — defensible only because `docs/privacy/privacy-policy.txt:81-83`
  publishes a privacy contact and RevenueCat supports customer deletion. **That mailbox must stay
  monitored.** Practical limit: with anonymous IDs a user cannot prove which `$RCAnonymousID:` is
  theirs without you walking them through it.

## Everything else — NOT COLLECTED, with the reason

| Category | Verdict | Why |
|---|---|---|
| Location (approx/precise) | Not collected | No location permission. `play-services-location` is bundled transitively but unreachable without one; no first-party reference. |
| User payment info | Not collected | Play Billing is the payment service and the app never sees instruments — Play's explicit exemption. |
| Health, fitness | Not collected | No sensor or health API. |
| Messages, email, SMS | Not collected | No messaging. OneSignal does not ship (`oneSignal=False`, 0 dex/zip entries, no `POST_NOTIFICATIONS`). |
| Photos, videos, audio, files | Not collected | No camera/mic/storage permission. `DevFrameCapture` writes to `persistentDataPath` and is dev-only; nothing uploads it. |
| Calendar, contacts | Not collected | No permissions. |
| App interactions / other actions | Not collected | The only telemetry lane is PostHog and it is off: `analytics_transport.json` `enabled:false`, `projectToken:""`, and `GameAnalyticsRuntime.cs:59` returns `Disabled()` before any ID, queue or request is created. |
| In-app search, installed apps, UGC | Not collected | No such features; no `QUERY_ALL_PACKAGES`. |
| Web browsing history | Not collected | No WebView. |
| Crash logs, diagnostics, performance | Not collected | Every Unity Connect sub-service is `m_Enabled: 0` in both committed MAIN and the working tree; no third-party crash SDK. |

Local save data (`save.dat`, `Application.persistentDataPath`, `allowBackup: 0`) is never read by
any network call site — nothing in it is transmitted.

## Confirmed vs unresolved

**Confirmed from artifacts, no device needed:** purchase history leaves the device via RevenueCat;
the transmitted identifier is the SDK's anonymous ID; `collectDeviceIdentifiers` has zero call
sites; no `AD_ID` permission ships; no ad or push SDK ships; the PostHog lane is off before any
identifier exists; every Unity Connect service is disabled; nothing in local save data is
transmitted; no location/camera/mic/contacts/calendar/storage/notification permission exists.

**Unresolved — needs a device capture:** whether RevenueCat's SDK itself calls
`AdvertisingIdClient` at configure time. No first-party code does; vendor behaviour inside
`purchases-10.18.1.aar` is not visible from the repo. Even if it fires, the missing permission means
only zeros are available — but a zeros-valued attribute could still be POSTed, which is an *access*
question rather than an ad-ID-value question. **Resolution:** install the release build on the
Pixel and capture logcat plus a TLS-terminated network trace across a cold start and one purchase.

## Three things to check before filing

1. **`UnityConnectSettings.asset` root `m_Enabled` is `1` in the working tree and `0` at committed
   MAIN**, and the measured binary was built from the working tree. Build the release from a state
   you are willing to ship, or commit the `0`.
2. **`docs/privacy/privacy-policy.txt:49-53` describes OneSignal and LevelPlay as processors.**
   Neither ships. The conditional framing at line 19 is weak cover for a Data safety section that
   must match the actual binary — scope the policy to what ships.
3. **Confirm the RevenueCat dashboard's PostHog integration is disabled**, and re-run the APK
   verification against the actual release AAB.

Sources: [Play Data safety](https://support.google.com/googleplay/android-developer/answer/10787469) ·
[Advertising ID policy](https://support.google.com/googleplay/android-developer/answer/6048248) ·
[Android 13 behaviour changes](https://developer.android.com/about/versions/13/behavior-changes-13) ·
[RevenueCat on Play Data Safety](https://www.revenuecat.com/docs/platform-resources/google-platform-resources/google-plays-data-safety) ·
[RevenueCat privacy](https://www.revenuecat.com/privacy)
