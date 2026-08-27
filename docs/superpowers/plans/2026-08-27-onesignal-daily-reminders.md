# OneSignal Daily Reminders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship one consent-respecting OneSignal Journey reminder for the Daily Line, with an earned soft prompt, durable player controls, and allowlisted Daily routing.

**Architecture:** Extend Task 5’s provider-neutral `IMessaging` contract, keep all OneSignal SDK calls in `CatMetro.Integrations.OneSignal`, and let Bootstrap coordinate durable preferences with the first counted Daily completion. Home adds one programmatic gear and modal reminder sheet; save schema v3 owns the opt-in, prompt-seen bit, and finite time preset.

**Tech Stack:** Unity 6000.3.16f1, C# 9, UGUI/TMP, Newtonsoft JSON, OneSignal Unity SDK 5.3.3, EDM4U 1.2.188, NUnit EditMode/PlayMode plus linked .NET tests.

**Spec:** `docs/superpowers/specs/2026-08-27-onesignal-daily-reminders-design.md`

## Global Constraints

- Reminders default off; `morning` is preselected without implying consent.
- The automatic soft prompt is presented only after a durable `DailyCompletionResult.Counted == true` and never more than once.
- “Not now” is durable and never followed by another automatic prompt.
- The native permission prompt runs only from an explicit player action.
- Only `daily_opt_in` and `daily_reminder_slot` are written as OneSignal tags.
- Only the allowlisted `daily` notification route is accepted.
- No streak, lapse, guilt, urgency, countdown, purchase, or second campaign copy/path may ship.
- OneSignal SDK types remain inside `CatMetro.Integrations.OneSignal`.
- APNs, FCM, signing credentials, and REST keys never enter the repository.
- No store upload is run.

---

### Task 1: Make Task 5’s Messaging Contract Truthful for Journeys

**Files:**
- Modify: `unity/Assets/Scripts/Services/Messaging/IMessaging.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Messaging/DailyChallengeNotificationTests.cs`

**Interfaces:**
- Produces: `MessagingPermission`, `MessagingRoute`, `DailyReminderSlot`, `IMessaging.PromptAsync`, `IMessaging.LinkOpened`, and a recurring `DailyChallengeNotification.Create(DailyReminderSlot)`.

- [ ] **Step 1: Write failing pure tests**

Add literal assertions that:

```csharp
var message = DailyChallengeNotification.Create(DailyReminderSlot.Morning);
Assert.That(message.NotificationId, Is.EqualTo("daily-ready"));
Assert.That(message.Body,
    Is.EqualTo("A fresh little route is waiting when you feel like playing."));
Assert.That(message.DeepLink, Is.EqualTo("catmetro://daily"));
Assert.That(message.Slot.TagValue, Is.EqualTo("morning"));
Assert.That(DailyReminderSlot.FromTagValue("unknown"),
    Is.EqualTo(DailyReminderSlot.Morning));
```

Also compile a fake against every `IMessaging` member and exercise the recurring notification's slot, copy, route, and channel through that boundary.

- [ ] **Step 2: Run the targeted .NET test and verify RED**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter FullyQualifiedName~DailyChallengeNotificationTests
```

Expected: compile/test failure because the Journey-oriented API does not exist.

- [ ] **Step 3: Implement the minimal provider-neutral types**

Use this boundary shape, with no SDK types:

```csharp
public interface IMessaging : IDisposable
{
    bool IsAvailable { get; }
    string SubscriptionId { get; }
    MessagingPermission Permission { get; }
    bool CanRequestPermission { get; }
    event Action<MessagingRoute> LinkOpened;
    Task<MessagingPermission> PromptAsync(bool fallbackToSettings,
        CancellationToken cancellationToken);
    void Schedule(DailyChallengeNotification notification);
    void Cancel(string notificationId);
}
```

`DailyReminderSlot` is a small immutable value with the three declared instances and fail-closed tag parsing. `MessagingRoute` contains only `Daily`.

- [ ] **Step 4: Re-run the targeted test and linked-source build**

Run the targeted test, then:

```bash
dotnet build dotnet/CatMetro.Content/CatMetro.Content.csproj --no-restore
```

Expected: green, zero warnings.

### Task 2: Add Durable Reminder Preferences and Save v3

**Files:**
- Create: `unity/Assets/Scripts/Application/Save/DailyReminderPreferences.cs`
- Modify: `unity/Assets/Scripts/Application/Save/SaveDefaults.cs`
- Modify: `unity/Assets/Scripts/Application/Save/MigrationTable.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Save/DailyReminderPreferencesTests.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Save/SaveMigrationTests.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Save/SavePayloadTests.cs`

**Interfaces:**
- Consumes: `DailyReminderSlot` from Task 1 and the existing `SaveStore` atomic commit path.
- Produces: `Enabled`, `PromptSeen`, `Slot`, `CanOfferPrompt(int lifetimeCompletions)`, `TryMarkPromptSeen()`, `TrySetEnabled(bool)`, and `TrySetSlot(DailyReminderSlot)`.

- [ ] **Step 1: Write failing migration and preference tests**

Cover literal v3 defaults, v2-to-v3 preservation, old-save reload, missing fields, malformed booleans/slot, commit rollback, and:

```csharp
Assert.That(preferences.Enabled, Is.False);
Assert.That(preferences.Slot, Is.EqualTo(DailyReminderSlot.Morning));
Assert.That(preferences.CanOfferPrompt(0), Is.False);
Assert.That(preferences.CanOfferPrompt(1), Is.True);
Assert.That(preferences.TryMarkPromptSeen(), Is.True);
Assert.That(reloaded.CanOfferPrompt(9), Is.False);
```

Name the mutations: inferred consent, repeated prompt, non-atomic state, invalid slot escaping, or unknown keys being deleted.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~DailyReminderPreferencesTests|FullyQualifiedName~SaveMigrationTests|FullyQualifiedName~SavePayloadTests"
```

- [ ] **Step 3: Implement save v3 and the preference owner**

Add these values to `settings`:

```json
"dailyReminderEnabled": false,
"dailyReminderPromptSeen": false,
"dailyReminderSlot": "morning"
```

Register an additive `2 -> 3` migration. Follow `DailyProgressTracker`’s deep-clone, assign, atomic-commit, rollback-on-failure pattern; never mutate the authoritative payload after a failed commit.

- [ ] **Step 4: Re-run focused and full linked tests**

Expected: all new save tests and the existing Daily progress tests pass.

### Task 3: Integrate OneSignal 5.3.3 Behind the Boundary

**Files:**
- Modify: `unity/Packages/manifest.json`
- Modify after Unity resolution: `unity/Packages/packages-lock.json`
- Create: `unity/Assets/Scripts/Integrations.OneSignal/CatMetro.Integrations.OneSignal.asmdef`
- Create: `unity/Assets/Scripts/Integrations.OneSignal/OneSignalRuntimeConfig.cs`
- Create: `unity/Assets/Scripts/Integrations.OneSignal/OneSignalMessaging.cs`
- Create: `unity/Assets/Resources/Config/onesignal.json`
- Create: `unity/ProjectSettings/OneSignalSettings.json`
- Modify: `unity/Assets/Tests/EditMode/CatMetro.Tests.EditMode.asmdef`
- Create: `unity/Assets/Tests/EditMode/Engine/Messaging/OneSignalMessagingTests.cs`

**Interfaces:**
- Consumes: Task 1’s `IMessaging` contract.
- Produces: `OneSignalMessaging.Initialize(string appId)` and a fail-closed runtime config loader.

- [ ] **Step 1: Write failing adapter-boundary tests**

Use a narrow fake SDK bridge and exercise the real adapter. Prove:

```csharp
messaging.Schedule(DailyChallengeNotification.Create(DailyReminderSlot.Afternoon));
Assert.That(bridge.Tags["daily_opt_in"], Is.EqualTo("true"));
Assert.That(bridge.Tags["daily_reminder_slot"], Is.EqualTo("afternoon"));

messaging.Cancel("daily-ready");
Assert.That(bridge.Tags["daily_opt_in"], Is.EqualTo("false"));
Assert.That(bridge.HasTag("daily_reminder_slot"), Is.False);
```

Also feed complete click payloads with `route=daily`, an unknown route, and null additional data. Assert only the allowlisted route reaches the adapter event, then reinitialize the adapter and prove one SDK click produces exactly one callback (no duplicate listener registration).

- [ ] **Step 2: Run the focused EditMode tests and verify RED**

Run `unity test` for the new fixture without adding `-quit`.

- [ ] **Step 3: Pin packages and implement the adapter**

Add scoped registries for npm (`com.onesignal`) and OpenUPM (`com.google.external-dependency-manager`), then pin:

```json
"com.google.external-dependency-manager": "1.2.188",
"com.onesignal.unity.android": "5.3.3",
"com.onesignal.unity.ios": "5.3.3"
```

Initialize with `OneSignal.Initialize(appId)`, map permission APIs exactly, use `OneSignal.User.PushSubscription.Id`, and add/remove the exact same click delegate in the component lifecycle. Initialization never invokes `RequestPermissionAsync`.

- [ ] **Step 4: Disable unused location and resolve/import packages**

Commit:

```json
{ "disableLocation": true }
```

Let Unity resolve the real package graph and generate metadata. Inspect the resulting lock and Console; do not hand-wave a missing package or duplicate dependency manager.

- [ ] **Step 5: Re-run adapter tests and compile**

Expected: the integration assembly compiles against OneSignal 5.3.3 and the focused tests pass.

### Task 4: Add the Cosy Reminder Sheet and Settings Gear

**Files:**
- Create: `unity/Assets/Scripts/Presentation/Screens/DailyReminderLayout.cs`
- Create: `unity/Assets/Scripts/Presentation/Screens/DailyReminderSheet.cs`
- Modify: `unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs`
- Modify: `unity/Assets/Resources/Strings/ui.csv`
- Create: `unity/Assets/Tests/EditMode/Presentation/DailyReminderLayoutTests.cs`
- Create: `unity/Assets/Tests/PlayMode/Screens/DailyReminderSheetTests.cs`
- Modify: `unity/Assets/Tests/PlayMode/Screens/HomeScreenTests.cs`

**Interfaces:**
- Produces: Home actions `ReminderAccepted`, `ReminderDismissed`, `ReminderEnabledChanged`, `ReminderSlotChanged`; state methods `ConfigureReminder(...)`, `ShowReminderPrompt()`, and `ShowReminderSettings()`.

- [ ] **Step 1: Write failing layout and behavior tests**

At literal portrait safe areas, prove the card and every button remain inside the safe area and meet the existing minimum target size. In PlayMode, assert real painted text/state and actual `ChromeRegions.TryResolve` behavior: modal controls beat Home, background taps do not start a level, and hide/disable/destroy unregister every modal region.

- [ ] **Step 2: Run focused tests and verify RED**

Run the new EditMode/PlayMode fixtures independently.

- [ ] **Step 3: Implement the programmatic sheet**

Use existing `Palette`, `UiChromeMaterial`, TMP, and region-registration conventions. All visible copy comes from CSV keys. Draw the gear from simple UI Images so no unsupported glyph or new bitmap asset is required.

- [ ] **Step 4: Re-run focused tests and inspect a real render**

Render the Home prompt at the target portrait resolution and inspect the screenshot itself for hierarchy, clipping, overlap, legibility, and cosy copy.

### Task 5: Wire the First Counted Win, Consent, Tags, and Daily Route

**Files:**
- Modify: `unity/Assets/Scripts/Bootstrap/CatMetro.Bootstrap.asmdef`
- Modify: `unity/Assets/Scripts/Bootstrap/GameRoot.cs`
- Modify: `unity/Assets/Tests/PlayMode/Bootstrap/DailyWireTests.cs`
- Create: `unity/Assets/Tests/PlayMode/Bootstrap/DailyReminderWireTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces: production composition, one-shot prompt orchestration, preference-to-provider reconciliation, and queued `daily` route consumption.

- [ ] **Step 1: Write failing orchestration tests**

Inject a full `IMessaging` fake and exercise the real Task 5 flow. Prove no prompt on launch/campaign/practice/replay, exactly one prompt after the first counted win and Home return, durable dismissal, explicit-only native prompting, permission-denial behavior, enable/disable/slot reconciliation, and valid notification routing across pre-compose and warm states.

- [ ] **Step 2: Run the fixtures and verify RED**

The expected failure is missing orchestration, not fixture setup.

- [ ] **Step 3: Implement Bootstrap wiring**

Create messaging after save load and before Home composition. Subscribe before adapter initialization. In the win block, rely only on `completion.Counted`; do not duplicate Task 5’s date/replay logic. Present only after Home’s existing deferred input lockout. Queue SDK link callbacks and consume them from `Update` on the main thread.

- [ ] **Step 4: Re-run all Daily PlayMode tests**

Expected: existing Task 5 behavior remains green and new reminder tests pass.

### Task 6: Complete Native Templates and the Human Runbook

**Files:**
- Create: `unity/Assets/Plugins/Android/mainTemplate.gradle`
- Create: `unity/Assets/Plugins/Android/gradleTemplate.properties`
- Modify: `unity/ProjectSettings/ProjectSettings.asset`
- Create: `docs/runbooks/onesignal-push-setup.md`
- Replace obsolete behavior: `docs/plan/specs/onesignal_retention.md`
- Correct shipped-demo copy: `docs/plan/specs/submission_script.md`
- Update pin: `docs/adr/0004-toolchain-and-sdk-version-pins.md`

**Interfaces:**
- Produces: reproducible Unity/OneSignal project setup plus human-only FCM/APNs/Journey steps.

- [ ] **Step 1: Commit exact Unity 6000.3.16f1 Gradle defaults and enable them**

Copy the installed Editor’s `mainTemplate.gradle` and `gradleTemplate.properties` byte-for-byte, then set `useCustomMainGradleTemplate` and `useCustomGradlePropertiesTemplate` to `1`. Do not alter unrelated Player settings.

- [ ] **Step 2: Write the no-secrets runbook**

Document account/App creation, public App ID placement, FCM v1 service-account upload, APNs `.p8` upload, app-group/push entitlements, both-target signing/provisioning, CocoaPods workspace use, exact Journey tags/branches/copy/exit/re-entry, dashboard test pushes, Android icon placement, and foreground/background/killed verification. Explicitly prohibit REST keys and credential files in the client/repository.

- [ ] **Step 3: Remove stale pressure instructions**

Replace the old multi-campaign/streak/lapse spec with the approved one-Journey contract, update the submission shot language, and pin OneSignal 5.3.3.

### Task 7: Verify the Artifact, Review, Commit, and Push

**Files:** all changed files.

- [ ] **Step 1: Run static and linked-source checks**

Run `bash scripts/check.sh`, `bash scripts/test.sh`, locked-mode .NET tests, and `git diff --check`.

- [ ] **Step 2: Run Unity tests without `-quit`**

Run focused tests first, then the full EditMode suite and relevant PlayMode suite. Treat missing result files or infrastructure exits as unverified, never as success.

- [ ] **Step 3: Validate build/runtime artifacts**

Inspect Unity Console after package import. If the Pixel 9 Pro is connected, first identify every device by model, then install only a locally built development APK on that confirmed serial and capture the rendered prompt. Never touch the Quest or Pico devices and never upload to a store.

- [ ] **Step 4: Review the final diff and run a focused code review**

Check lifecycle cleanup, save compatibility, allowlist behavior, SDK isolation, copy tone, secret absence, package-lock accuracy, and unrelated file drift.

- [ ] **Step 5: Commit and push**

Stage only intentional files. Commit with:

```text
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
```

Use a plain push of `feat/push-notifications`; never force-push.
