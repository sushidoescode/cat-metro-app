# OneSignal Push Setup Runbook

Status: project setup committed; all account, credential, dashboard, signing, remote-delivery, and
store operations remain human-owned and unverified.

This is the human runbook for the approved
[`OneSignal Daily Reminders Design`](../superpowers/specs/2026-08-27-onesignal-daily-reminders-design.md).
It describes exactly one recurring Daily Line Journey. Older July retention plans are historical
and are not implementation instructions.

## Safety boundary

- The only OneSignal value allowed in the client repository is the public App ID, in
  `unity/Assets/Resources/Config/onesignal.json`.
- Never place a OneSignal REST API key, Firebase service-account JSON, APNs `.p8`, `.p12`, password,
  Apple signing certificate, provisioning profile, keystore, or other signing material in the
  client or repository.
- Upload FCM and APNs credentials directly through the authenticated OneSignal dashboard. Retain or
  delete local credential copies according to the human credential policy.
- Account creation, dashboard changes, credential handling, device sends, signing, and store work
  are human-only. This runbook records them; it does not authorize automation.
- Do not paste secret values into screenshots, issue comments, terminal logs, or this checklist.

Status values below are `DONE`, `BLOCKED`, and `HUMAN TODO`. A human should replace each `HUMAN
TODO` with dated evidence rather than marking a step complete from memory.

## Current repository baseline

| Owner | Status | Check |
|---|---|---|
| Repository | DONE | Unity is pinned to `6000.3.16f1`. OneSignal Android and iOS packages are pinned to `5.3.3`; EDM4U is pinned once at `1.2.188`. |
| Repository | DONE | Exact installed-editor `mainTemplate.gradle` and `gradleTemplate.properties` are committed and only Custom Main Gradle Template plus Custom Gradle Properties Template are enabled. |
| Repository | DONE | OneSignal location is disabled in `unity/ProjectSettings/OneSignalSettings.json`. |
| Repository | DONE | `onesignal.json` contains an empty `appId`; missing configuration fails closed without blocking play. |
| Repository | DONE | Runtime code writes only `daily_opt_in` and `daily_reminder_slot`, accepts only route `daily`, and queues accepted clicks onto Unity's main loop. |
| Repository | DONE | Reminders default Off. Morning is only the preselected slot. The one automatic soft prompt is earned after the first durable counted Daily win. |
| Release owner | BLOCKED | The five Android small-icon variants do not exist. This is a release blocker; see the Android icon checklist. |
| iOS owner | BLOCKED | The project has no iPhone application identifier, no Apple Team ID, and automatic signing is off. Task 2/final bundle-ID work must finish before provisioning. |
| Human operator | HUMAN TODO | No OneSignal app, credential upload, dashboard Journey, test push, real-device proof, Xcode archive, TestFlight build, or store upload is established by this repository setup. |

## 1. Create the OneSignal app and place the public App ID

| Owner | Status | Human action and evidence |
|---|---|---|
| Account owner | HUMAN TODO | Sign in to OneSignal and create one app for Cat Metro. Record the account owner and app name in the private release record. Do not create it from a repository script. |
| Account owner | HUMAN TODO | Configure the Android and iOS platform credentials in the same OneSignal app unless the release owner deliberately chooses separate environment apps. Record that choice privately. |
| Account owner | HUMAN TODO | From OneSignal **Settings > Keys & IDs**, copy only the public 36-character App ID. Dashboard labels can change; confirm the current label against the official setup page. |
| Repository owner | HUMAN TODO | Put only that public UUID in `unity/Assets/Resources/Config/onesignal.json` as `{"appId":"..."}`. Review the diff to prove no REST key or credential accompanied it. |
| QA owner | HUMAN TODO | Launch a development build and confirm a Subscription appears in **Audience > Subscriptions**. Mark only owned QA devices as Test Users. |

Official references: [Mobile SDK setup](https://documentation.onesignal.com/docs/en/mobile-sdk-setup),
[Unity SDK setup](https://documentation.onesignal.com/docs/en/unity-sdk-setup), and
[Test users](https://documentation.onesignal.com/docs/en/test-users).

## 2. Android FCM v1

The final Android application ID is `com.catmetro.game`. Use the Firebase project that owns that
exact Android app identity; changing Firebase sender projects later invalidates existing device
tokens until users reopen the app.

| Owner | Status | Human action and evidence |
|---|---|---|
| Firebase owner | HUMAN TODO | Create or select the Firebase project for `com.catmetro.game`; confirm the Android app entry and project/Sender ID before creating credentials. |
| Firebase owner | HUMAN TODO | In Firebase Project Settings > Cloud Messaging, enable **Firebase Cloud Messaging API (V1)** through Google Cloud if it is disabled. Save a screenshot without credentials. |
| IAM owner | HUMAN TODO | Create a dedicated narrowly scoped service account for OneSignal delivery. Grant `roles/firebasecloudmessaging.admin` and `roles/firebase.viewer`; verify the resulting permissions include `cloudmessaging.messages.create` and `firebase.projects.get`. Do not grant Owner or Editor. |
| Credential owner | HUMAN TODO | Generate the service-account JSON once into approved secure storage. Do not copy, rename, inspect, or stage it inside this repository. |
| Credential owner | HUMAN TODO | In OneSignal, open the Google Android/FCM platform settings and upload the JSON directly under the FCM v1 Service Account field. If the UI asks for an API generation, choose Firebase Cloud Messaging API (V1). |
| Account owner | HUMAN TODO | Match Firebase's Sender ID to the value shown by OneSignal before saving. Record pass/fail without recording the JSON contents. |
| Credential owner | HUMAN TODO | Retain or securely delete the downloaded JSON according to the human credential policy. Record where custody lives; never record its private key. |
| Android owner | HUMAN TODO | After the account and project configuration are stable, run the supported OneSignal/EDM Android resolve workflow once, inspect all generated diffs, and reject unrelated template changes. Task 6 deliberately did not run Force Resolve. |

OneSignal's credential guide names the two required permissions. Google's current IAM catalog is
the authority for the current role identifier `roles/firebasecloudmessaging.admin`.

Official references: [Android Firebase credentials](https://documentation.onesignal.com/docs/en/android-firebase-credentials)
and [Google Cloud FCM roles](https://cloud.google.com/iam/docs/roles-permissions/firebasecloudmessaging).

### Android notification icon — release blocker

The asset is intentionally not fabricated by this task. The art/release owner must provide a simple
white silhouette on a transparent background; Android renders the alpha mask and applies its own
tint. No gradient, shadow, solid background, or multicolor detail belongs in the small icon.

After the OneSignal setup step has copied `OneSignalConfig.androidlib` into Assets, add all five
files with the exact lowercase name and dimensions:

| Owner | Status | Required Unity resource |
|---|---|---|
| Art + Android | BLOCKED | `unity/Assets/Plugins/Android/OneSignalConfig.androidlib/src/main/res/drawable-mdpi/ic_stat_onesignal_default.png` — 24×24 |
| Art + Android | BLOCKED | `unity/Assets/Plugins/Android/OneSignalConfig.androidlib/src/main/res/drawable-hdpi/ic_stat_onesignal_default.png` — 36×36 |
| Art + Android | BLOCKED | `unity/Assets/Plugins/Android/OneSignalConfig.androidlib/src/main/res/drawable-xhdpi/ic_stat_onesignal_default.png` — 48×48 |
| Art + Android | BLOCKED | `unity/Assets/Plugins/Android/OneSignalConfig.androidlib/src/main/res/drawable-xxhdpi/ic_stat_onesignal_default.png` — 72×72 |
| Art + Android | BLOCKED | `unity/Assets/Plugins/Android/OneSignalConfig.androidlib/src/main/res/drawable-xxxhdpi/ic_stat_onesignal_default.png` — 96×96 |

An optional large icon is a different asset:
`drawable-xxxhdpi/ic_onesignal_large_icon_default.png` at 256×256. It does not replace any required
small-icon density. A OneSignal bell means at least one variant is missing or misplaced; a white
square means the alpha/transparency is wrong. Verify the installed build on real Android hardware.

Official reference: [Notification icons](https://documentation.onesignal.com/docs/en/notification-icons).

## 3. iOS coordination and signing

Do not start Apple identifiers or provisioning with a guessed bundle ID. The final main bundle ID
must be settled first. The installed OneSignal Unity 5.3.3 postprocessor generates the following in
the exported Xcode project; the human must inspect the generated artifact rather than assuming the
postprocessor ran:

- Push Notifications plus Background Modes / Remote notifications on the main target;
- App Group `group.{main_bundle_id}.onesignal` on the main app and extension;
- target `OneSignalNotificationServiceExtension`;
- extension bundle ID `{main_bundle_id}.OneSignalNotificationServiceExtension`;
- extension source membership and the CocoaPods target for `OneSignalExtension`.

| Owner | Status | Human action and evidence |
|---|---|---|
| Product + iOS | BLOCKED | Settle the final main iOS bundle ID with Task 2. Record it once; do not substitute the Android ID without a deliberate decision. |
| Apple account owner | HUMAN TODO | Create or update the main App ID with Push Notifications and App Groups enabled. Create shared App Group `group.{main_bundle_id}.onesignal`. |
| Apple account owner | HUMAN TODO | Create or update the extension App ID `{main_bundle_id}.OneSignalNotificationServiceExtension` and associate the same App Group. |
| Signing owner | HUMAN TODO | Create/refresh development and distribution provisioning for both the main app and notification-service-extension identifiers. Confirm both use the intended Team ID. |
| Apple credential owner | HUMAN TODO | Create or select an APNs `.p8` key with APNs enabled. Keep the `.p8` in approved secure storage; Apple permits the download only once. |
| Apple credential owner | HUMAN TODO | In OneSignal's Apple iOS/APNs settings, upload the `.p8` and enter its Key ID, the Apple Team ID, and the final main bundle ID. Upload these only to OneSignal; never put the key in this repository. |
| Unity/iOS owner | HUMAN TODO | Set the final iPhone identifier and Team ID in Unity. Decide automatic versus manual signing deliberately; current automatic signing is Off. |
| Unity/iOS owner | HUMAN TODO | Export the iOS player. Confirm the generated Xcode project contains both targets, both entitlements files, exact shared App Group, Push capability, remote-notification background mode, correct extension source membership, and correct bundle IDs. |
| Unity/iOS owner | HUMAN TODO | Run CocoaPods as required by the generated project and open the generated `.xcworkspace`, not merely the `.xcodeproj`. Build the main scheme with both targets signed. |
| QA owner | HUMAN TODO | Prove APNs sandbox delivery on a development-signed real device, then prove the distribution path through TestFlight. Simulator-only evidence is insufficient. |

Official references: [Unity SDK setup](https://documentation.onesignal.com/docs/en/unity-sdk-setup),
[iOS p8 connection](https://documentation.onesignal.com/docs/en/ios-p8-token-based-connection-to-apns),
[iOS SDK setup](https://documentation.onesignal.com/docs/en/ios-sdk-setup), and
[service extensions](https://documentation.onesignal.com/docs/en/service-extensions).

## 4. Build the single Daily Line Journey

Create one recurring **Audience Segment** Journey and no other notification campaign. The client
owns exactly two tags:

| Tag | Exact values | Meaning |
|---|---|---|
| `daily_opt_in` | `true` or `false` | Entry eligibility and immediate exit condition |
| `daily_reminder_slot` | `morning`, `afternoon`, or `evening` | Selects one fixed local-time branch |

Create dashboard segments that express exact tag equality:

| Owner | Status | Segment contract |
|---|---|---|
| Messaging owner | HUMAN TODO | `Daily reminders enabled`: `daily_opt_in` equals `true`. This is the Journey entry audience. |
| Messaging owner | HUMAN TODO | `Daily reminder morning`: `daily_reminder_slot` equals `morning`. |
| Messaging owner | HUMAN TODO | `Daily reminder afternoon`: `daily_reminder_slot` equals `afternoon`. |
| Messaging owner | HUMAN TODO | `Daily reminder evening`: `daily_reminder_slot` equals `evening`. |

Configure the Journey exactly as follows. Dashboard labels may move; if a label differs, confirm the
concept against the linked official documentation and record the actual label instead of guessing.

| Owner | Status | Journey setting |
|---|---|---|
| Messaging owner | HUMAN TODO | Name: `Daily Line`. Entry type: **Audience Segment**. Included segment: `Daily reminders enabled`. |
| Messaging owner | HUMAN TODO | **Future additions only: OFF.** Already-eligible users must be allowed to enter when the Journey is activated. |
| Messaging owner | HUMAN TODO | Exit rule: **exit when the user no longer matches the audience conditions**. A user must leave as soon as `daily_opt_in` is no longer `true`. |
| Messaging owner | HUMAN TODO | Re-entry: allowed after **12 hours**. The timer starts when the user exits. Twelve hours is longer than the 15-minute window and shorter than a day; the intent is one message per local day. Real dashboard delivery must prove that intent. |
| Messaging owner | HUMAN TODO | First branch: exact morning segment match. Yes → morning Time Window. No → evaluate afternoon. |
| Messaging owner | HUMAN TODO | Second branch: exact afternoon segment match. Yes → afternoon Time Window. No → evaluate evening. |
| Messaging owner | HUMAN TODO | Third branch: exact evening segment match. Yes → evening Time Window. Missing/invalid/else → exit with no message. |
| Messaging owner | HUMAN TODO | Morning recurring local Time Window: every day `10:00–10:15`. |
| Messaging owner | HUMAN TODO | Afternoon recurring local Time Window: every day `15:00–15:15`. |
| Messaging owner | HUMAN TODO | Evening recurring local Time Window: every day `18:00–18:15`. |
| Messaging owner | HUMAN TODO | Place the same push step after each Time Window. Each branch ends after the push so the re-entry timer can begin. |

OneSignal may release a waiting user at a randomized point from the Time Window start through
15 minutes after it. Never promise an exact minute; the game correctly says “around 10:00”,
“around 15:00”, or “around 18:00”. If OneSignal has no user time-zone data, it can use the app's
default time zone, so verify time-zone capture on each test subscription.

Use the exact same push content on all three branches:

| Field | Exact value |
|---|---|
| Title | `Today's Line is ready` |
| Body | `A fresh little route is waiting when you feel like playing.` |
| Additional data | `{"route":"daily"}` |
| Launch URL / deep link | `catmetro://daily` |

Additional data is the runtime authority: the adapter accepts only exact string `route=daily` and
rejects unknown or missing routes. The Launch URL is retained as the declared deep link and must be
verified on both platforms. Do not add arbitrary route parameters.

A slot edit can happen after the current Journey instance already evaluated its branch. Test the
dashboard's actual behavior, and promise only that the **next eligible send/re-entry** uses the new
slot. Do not promise immediate rebucketing or an immediate message.

Explicitly excluded: streak, lapse or win-back, guilt, countdown, hard-level help, purchase,
event-ending, local-backup, and every second notification campaign. There are no Unity local
notifications. Native permission appears only after the player explicitly presses **Remind me** or
explicitly enables reminders in Settings. **Not now** permanently consumes the one automatic soft
prompt, unless the player later chooses Settings.

Official references: [Segments](https://documentation.onesignal.com/docs/en/segmentation),
[Journey settings](https://documentation.onesignal.com/docs/en/journeys-settings),
[Journey actions and Time Windows](https://documentation.onesignal.com/docs/en/journeys-actions),
[Tags](https://documentation.onesignal.com/docs/en/add-user-data-tags), and
[links and deep links](https://documentation.onesignal.com/docs/en/links).

## 5. Dashboard and real-device verification matrix

Before sending, identify the Android device model and serial. The Cat Metro Android target is the
Pixel 9 Pro (`48121FDAP006X4`); never install on the Quest 3 or Pico emulator listed in repository
instructions. Use owned Test Users and the dashboard's **Test & Preview** path. Do not use or expose
a REST key for manual test sends.

For each row, record device model, OS/build, app build identifier, local time zone, OneSignal
Subscription ID, timestamp, expected result, actual result, and screenshot/log path in the private
release evidence record.

| Owner | Status | Platform/state | Verification and expected result |
|---|---|---|---|
| QA + messaging | HUMAN TODO | Android foreground | Exact push arrives; title/body/icon are correct; one tap opens the current Daily Line once. |
| QA + messaging | HUMAN TODO | Android background | Exact push arrives and tap routes to the current Daily Line. |
| QA + messaging | HUMAN TODO | Android killed / cold launch | Tap launches the process and consumes `route=daily` once after boot/save load. |
| QA + messaging | HUMAN TODO | Android resumed / warm launch | Tap leaves any Home/Intro overlay behind and opens the current Daily Line once. |
| QA + messaging | HUMAN TODO | iOS foreground | APNs sandbox push arrives; exact copy/data are visible to the SDK; tap routes once. |
| QA + messaging | HUMAN TODO | iOS background | APNs sandbox push arrives and tap routes to the current Daily Line. |
| QA + messaging | HUMAN TODO | iOS killed / cold launch | Tap launches the process and consumes `route=daily` once after boot/save load. |
| QA + messaging | HUMAN TODO | iOS resumed / warm launch | Tap opens the current Daily Line once without stale screen chrome. |
| QA | HUMAN TODO | Both / invalid route | Missing data and any route other than exact `daily` are ignored; no arbitrary screen or parameter opens. |
| QA | HUMAN TODO | Both / permission grant | No prompt occurs at launch. First counted Daily win earns the soft prompt; explicit **Remind me** invokes native permission and enables only after authorization. |
| QA | HUMAN TODO | Both / permission denial | Denial leaves durable reminders Off and schedules nothing. Record iOS and Android behavior separately. |
| QA | HUMAN TODO | Both / exhausted permission | Explicit Settings action opens system notification settings; returning with permission enabled reconciles and schedules once. |
| QA | HUMAN TODO | Both / `Not now` durability | `Not now` writes prompt-seen, survives process restart/reload, sends no tags/schedule, and never auto-prompts again. |
| QA + messaging | HUMAN TODO | Both / enable | After local save commits, push subscription opts in and exact tags become `daily_opt_in=true` plus the selected slot. |
| QA + messaging | HUMAN TODO | Both / Off | After local save commits, `daily_opt_in=false`, `daily_reminder_slot` is removed, push subscription opts out, and the Journey exits. |
| QA + messaging | HUMAN TODO | Both / slot change while On | Tag changes to the selected exact value; no old value remains; only the next eligible send/re-entry is promised. |
| QA + messaging | HUMAN TODO | Both / slot change while Off | Durable preselection changes but no remote schedule is enabled. |
| QA + messaging | HUMAN TODO | All three slots | Device/account evidence shows one message per local day within 10:00–10:15, 15:00–15:15, or 18:00–18:15 as selected; no exact-minute claim. |
| QA + Android | BLOCKED | Android icon | All five density variants render as the intended silhouette in status bar and notification; no bell or white square. |
| QA + iOS | HUMAN TODO | APNs sandbox | Development provisioning receives from the configured `.p8` and correct bundle ID. |
| QA + iOS | HUMAN TODO | TestFlight | Distribution provisioning signs both targets; installed TestFlight build receives and routes the push. |
| QA + messaging | HUMAN TODO | Journey exit/re-entry | Off exits promptly; re-enable permits re-entry after the configured 12 hours; evidence shows no duplicate inside one 15-minute window. |
| QA + messaging | HUMAN TODO | Dashboard evidence | Capture the single canvas, settings, exact branch windows, message data, two tags on a Test User, Journey report, and device screenshots with all secrets absent. |

Official references: [Test users](https://documentation.onesignal.com/docs/en/test-users),
[Prompt for push permissions](https://documentation.onesignal.com/docs/en/prompt-for-push-permissions),
[Journey analytics](https://documentation.onesignal.com/docs/en/journeys-analytics), and
[deep-link testing](https://documentation.onesignal.com/docs/en/deep-linking).

## 6. Activation gate

| Owner | Status | Gate |
|---|---|---|
| Release owner | HUMAN TODO | All platform credentials are configured without repository exposure. |
| Release owner | HUMAN TODO | Android icon blocker is closed in the actual build. |
| Release owner | HUMAN TODO | Both iOS targets and profiles are correct in the generated `.xcworkspace`. |
| Messaging owner | HUMAN TODO | Exactly one active Journey exists and no scheduled, local, lapse, streak, purchase, event, or help campaign can send. |
| Messaging owner | HUMAN TODO | Future additions only is Off; exit-on-audience-mismatch and 12-hour re-entry are visible in the final canvas. |
| QA owner | HUMAN TODO | The real-device matrix passes with retained evidence, including killed/cold and warm/resumed routing. |
| Release owner | HUMAN TODO | Only after the preceding rows pass, activate the Journey for the intended audience and monitor early exits, deliveries, failures, and clicks. |

If any label, plan capability, or generated artifact differs from this runbook, stop and reconcile it
against current official documentation plus the installed package source. Do not broaden the
campaign to work around a dashboard mismatch.
