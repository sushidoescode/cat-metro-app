# OneSignal Daily Reminders Design

**Status:** Approved on 2026-08-27

## Goal

Use OneSignal to deliver one thoughtful Daily Line reminder at a player-selected local-time preset, without first-launch prompting, expiring streaks, urgency, guilt, or a second notification campaign.

## Product contract

- Daily reminders are disabled by default.
- Morning is preselected and is described honestly as “around 10:00”.
- Afternoon (“around 15:00”) and evening (“around 18:00”) are the only other launch presets.
- The first automatic soft prompt is earned only by the first durable, counted Daily Line win.
- “Not now” marks that soft prompt as seen. It is never shown automatically again.
- The player can later change their mind from the reminder settings sheet.
- The OS permission prompt is invoked only by the player pressing “Remind me” or manually enabling reminders in settings.
- No prompt is shown on first launch, after a campaign win, after a practice Daily, or after replaying an already-completed Daily.
- One remote push is the complete launch campaign. There are no streak, lapse, purchase, event, countdown, or “we miss you” campaigns.
- The existing cumulative lifetime Daily tally remains the sole return-progress measure.

## Player flow

### First eligible win

1. `DailyProgressTracker.RecordDailyCompletion` returns `Counted == true`.
2. After the results CTA returns to Home and the existing input lockout has elapsed, the game atomically marks the soft prompt as seen.
3. Home shows a modal card:
   - Title: “Would you like tomorrow’s Daily Line delivered?”
   - Body: “One gentle reminder around the time you choose. Nothing expires.”
   - Preselection: “Morning · around 10:00”
   - Actions: “Remind me” and “Not now”
4. “Not now” closes the card. The game remains opted out and never auto-prompts again.
5. “Remind me” invokes the native permission request. A grant enables the selected schedule; a denial leaves reminders off and exposes a user-initiated settings fallback.

If the app is terminated between the qualifying win and the return to Home, a later Home visit may still present the earned prompt because lifetime completions are durable. The prompt is marked seen when it is actually presented, not when the win is recorded.

### Settings

After at least one counted Daily completion, a small gear on Home opens a contained reminder sheet. It exposes:

- an On/Off control;
- Morning, Afternoon, and Evening presets;
- the current OS-permission-aware status;
- a user-initiated “Open notification settings” action when native prompting is exhausted;
- a close action.

Changing a preset while reminders are off only changes the preselection. Changing it while reminders are enabled atomically saves the choice and updates OneSignal Journey tags.

### Notification tap

The only accepted notification route is `daily`. The OneSignal adapter rejects every other route and ignores arbitrary parameters. A valid tap is queued onto the Unity main loop and opens the current Daily Line across cold, resumed, and warm process states. Task 5 remains authoritative for date selection and practice/counting behavior.

## Delivery model

The shipped daily reminder is a OneSignal remote push, not a Unity local notification and not a custom backend schedule.

The client writes only these declared tags:

| Tag | Values | Purpose |
|---|---|---|
| `daily_opt_in` | `true`, `false` | Journey entry and immediate exit rule |
| `daily_reminder_slot` | `morning`, `afternoon`, `evening` | Selects one of three fixed Time Window branches |

One recurring Journey starts from `daily_opt_in == true`, branches on `daily_reminder_slot`, waits for the configured local Time Window, and sends the same Daily Line push on each branch. Re-entry is limited to once per day. An exit rule removes users as soon as `daily_opt_in != true`.

OneSignal Time Windows can release during the 15 minutes after their start, so every player-facing time uses “around”. Arbitrary minute selection is intentionally absent because OneSignal does not interpret a tag value as a dynamic delivery clock.

Journey copy:

- Title: “Today’s Line is ready”
- Body: “A fresh little route is waiting when you feel like playing.”
- Additional data: `{ "route": "daily" }`
- Launch URL: `catmetro://daily`

## Architecture

### Provider-neutral services boundary

Task 5’s `IMessaging` remains the only runtime messaging dependency visible to application and presentation code. It is extended with:

- current provider availability and permission state;
- whether a native prompt can still be requested;
- a cancellable permission request contract;
- an allowlisted `MessagingRoute` event;
- Task 5’s existing `Schedule(DailyChallengeNotification)` and `Cancel(string)` operations.

`DailyChallengeNotification` becomes truthful for recurring Journey enrollment: it contains the stable daily id, copy, route, channel, and a `DailyReminderSlot`. The local-only date, UTC-delivery, and expiry fields are removed because the OneSignal client SDK cannot schedule a remote Journey send at those timestamps.

No OneSignal SDK type crosses this boundary.

### OneSignal adapter

`CatMetro.Integrations.OneSignal` is the only assembly that references `OneSignalSDK`.

The Bootstrap composition root:

1. loads a public OneSignal App ID from a Resources JSON file;
2. constructs the adapter and subscribes to its route event before SDK initialization;
3. initializes OneSignal without requesting permission;
4. synchronizes the durable reminder preference into tags after save load;
5. unsubscribes and disposes the adapter with `GameRoot`.

Missing or malformed App ID configuration disables messaging without blocking play. The game does not pretend a reminder is enabled when the provider is unavailable.

`Schedule` opts the OneSignal push subscription in only after native permission is authorized, then writes the two declared tags. `Cancel` writes `daily_opt_in=false`, removes the slot tag, and opts the push subscription out because Daily is the shipped app’s only push category.

### Persistence

Save schema v3 adds three values under the existing `settings` object:

```json
{
  "dailyReminderEnabled": false,
  "dailyReminderPromptSeen": false,
  "dailyReminderSlot": "morning"
}
```

The v2-to-v3 migration is additive, preserves unknown keys, never infers consent, and never reads legacy streak fields. Missing or invalid enablement fails closed to disabled. An invalid slot becomes morning. A malformed prompt-seen token fails closed to seen so corruption cannot create repeated prompts; genuinely missing v2 data is added as false by migration.

`DailyReminderPreferences` owns clone/mutate/atomic-commit behavior. OneSignal side effects happen only after the local preference commit succeeds. A provider failure can therefore be reconciled from durable state on the next launch.

### Presentation

The Home screen retains its existing programmatic UGUI/TMP composition and `ChromeRegions` lifecycle. It adds only:

- a small settings gear after the first counted Daily completion;
- a `DailyReminderSheet` overlay used in soft-prompt and settings modes;
- localized CSV keys for all visible copy.

The overlay registers above Home’s input priority, unregisters every region on hide/disable/destroy, respects the safe area, and is laid out from measured screen-space rectangles. It does not add a scene or prefab.

## SDK and platform setup

- Pin OneSignal Unity Android and iOS packages at 5.3.3 through the npm scoped registry.
- Keep OneSignal isolated to its integration assembly.
- Disable the unused OneSignal location module in `ProjectSettings/OneSignalSettings.json`.
- Enable Unity’s custom main Gradle and Gradle properties templates using the exact 6000.3.16f1 defaults.
- Keep exactly one EDM4U 1.2.188 installation across OneSignal and the other commercial SDK work.
- Android credentials are an FCM v1 service-account JSON uploaded to OneSignal; no credential JSON is placed in the project.
- iOS credentials are an APNs `.p8`, Key ID, and Team ID uploaded to OneSignal. The human refreshes provisioning for the main app and OneSignal notification service extension with Push Notifications and the `group.{bundle_id}.onesignal` App Group.
- The OneSignal App ID is public configuration and may be committed after the account exists. The initial checked-in value is blank and makes the adapter fail closed.

## Verification contract

Automated evidence must demonstrate behavior, not source text:

- new, migrated, missing-field, malformed-field, commit-failure, and reload preference behavior;
- Task 5’s first `Counted` Daily win is eligible, while practice and replay wins are not;
- presenting the soft prompt durably consumes the one automatic opportunity;
- “Not now” survives reload and does not schedule;
- enable/disable/time changes produce the correct provider-neutral schedule or cancel behavior;
- invalid notification routes are rejected; a valid Daily route is consumed once;
- modal regions beat Home regions and fully unregister;
- the integration assembly compiles with OneSignal 5.3.3;
- a real rendered Home/reminder sheet is visually inspected at the target portrait resolution;
- an Android debug build is installed only on the confirmed Pixel 9 Pro target if a device build is run.

Human-only verification remains explicit: OneSignal account creation, FCM/APNs credential upload, refreshed signing profiles, real remote delivery in foreground/background/killed states, and store builds/uploads. No store upload is part of this task.
