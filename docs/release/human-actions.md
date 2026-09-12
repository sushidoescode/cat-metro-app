# What only you can do

Everything else on the release path is done or automated. These five are blocked on you, and
nothing an agent session can do will unblock them. Ordered by what unblocks the most.

| # | Action | Where | Unblocks | Notes |
|---|---|---|---|---|
| 1 | **Report the highest versionCode** across ALL tracks — internal, closed, open, production — including superseded, halted, draft and rejected releases | Play Console ▸ the app ▸ each track's release history | the release build | `ProjectSettings.asset:180` is still `1`. Play refuses any upload at or below the highest code it has ever seen, drafts included. |
| 2 | **Type the keystore passwords into Unity** and cut the bundle | Unity ▸ Player ▸ Publishing Settings, then Build | the release artifact | Unity keeps them in **session memory only** — not in `ProjectSettings.asset`, not in `UserSettings/`, not in the prefs plist. They must be re-entered after every editor relaunch, and no agent session can supply them. Proven: a real batchmode build stops at *Prepare For Build* with `Unable to sign the application; please provide passwords!` **This is a separate gate from #1 — knowing the version code does not start the build.** Steps: `signed-aab-local-steps.md`. |
| 3 | **Report the upload-certificate SHA-256** and whether Play App Signing is enrolled | Console ▸ Release ▸ Setup ▸ App integrity | calling the bundle upload-ready | Feed it to `python3 scripts/verify-android-artifact.py <bundle> --expect-version-code <N> --expect-cert-sha256 <fingerprint>`. Without it the verifier exits 3 and refuses to declare readiness: it can prove the bundle is correctly signed and by whom, but not that Play expects that certificate. If enrolled, use the **upload** certificate, not the app-signing one. The 2026-08-30 bundle was `CN=Sushant Srikrish`, `548257b29e36012b06ca6577f422fd843d411110accbf14cdfeac9bb95354408` — a different fingerprint means the keystore changed. |
| 4 | **List the RevenueCat dashboard integrations** | RevenueCat ▸ Project ▸ Integrations | finalising Data safety and the privacy policy | Name every integration; for each, whether it receives data as a service provider on your behalf, and whether it consumes `gpsAdId` / `androidId`. RevenueCat's own guidance is that a non-service-provider integration can make the answer **Shared: Yes**, and an ad-identifier integration puts the advertising ID in scope on a different basis. The repo cannot see the dashboard, so `docs/release/data-safety-assessment.md` stays **PROVISIONAL** until this lands, and `docs/privacy/privacy-policy-correction-2026-09-11.md` cannot be applied. Check the PostHog integration specifically. |
| 5 | **Attach the idle Pixel 9 Pro** `48121FDAP006X4` | USB, when you are not playing | the device pass and the video | Absent at every check across two sessions; only the Quest 3 `2G0YC5ZF7Z056Q` and the Pico emulator `emulator-5554` have been attached, and neither may ever be installed to. `build/CatMetro-e1a2a288-20260912-consist130.apk` is built and waiting. Then: gameplay, navigation, purchase/restore, offline relaunch, performance and logcat, followed by the video in `docs/store/video-sequence.md`. |

Two more that are quick and only you can do, but block nothing until submission:

- **Generate a Play promo code** for `cm_outfit_conductor`. The app is free with one $1.99 cosmetic
  and no free trial, so the Shipaton rules require a judge code in the Devpost entry.
- **Confirm the current review/track state** of the app, so the release is filed as a new release
  rather than an edit to a blocked draft.

**Play uploads and review submissions remain human-only.** No agent session performs one.

## What is NOT waiting on you

- Code, tests and art: EditMode 2359/2359, graphics PlayMode 744/743 with the one expected skip.
- Artifact verification: one command, proven in both directions, with a signature-control suite.
- Store screenshots: six frames at exactly 1179×2556, refreshed from the current candidate.
- Video sequence and Devpost text: drafted; see `video-sequence.md` and `devpost-draft.md`.
