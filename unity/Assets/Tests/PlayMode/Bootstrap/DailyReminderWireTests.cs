using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Application.Save;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using CatMetro.Services;

namespace CatMetro.Tests.PlayMode
{
    public sealed class DailyReminderWireTests
    {
        private const long PinnedUnixSeconds = 1787572800L; // 2026-08-24T12:00:00Z
        private const string PinnedDateKey = "2026-08-24";

        private string _tmpDir;
        private GameRoot _root;
        private FakeMessaging _messaging;

        [SetUp]
        public void SetUp()
        {
            GameRoot.BootToHome = false;
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = true;
            _tmpDir = Path.Combine(Path.GetTempPath(),
                "cm-daily-reminder-wire-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpDir);
            File.Copy(Path.Combine(UnityEngine.Application.streamingAssetsPath,
                    GameRoot.LevelPath("L001")),
                Path.Combine(_tmpDir, "level.json"), overwrite: true);
            DevLevelOverride.DirectoryOverride = _tmpDir;
            GameRoot.DailyStorageRootOverride = () =>
                new TestStorageRoot(Path.Combine(_tmpDir, "save"));
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.MessagingFactoryOverride = null;
            GameRoot.BootToHome = false;
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.DailyStorageRootOverride = null;
            DevLevelOverride.DirectoryOverride = null;
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;
            _messaging = null;
            if (!string.IsNullOrEmpty(_tmpDir) && Directory.Exists(_tmpDir))
                Directory.Delete(_tmpDir, true);
            _tmpDir = null;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator FreshLaunch_BuildsNoReminderTree_AndNeverPromptsOrSchedules()
        {
            Launch(new FakeMessaging());

            Assert.That(_root.Home.ReminderGearTransform, Is.Null,
                "zero durable Daily completions must construct no reminder affordance");
            Assert.That(_root.Home.ReminderSheet, Is.Null);
            yield return null;

            Assert.That(_root.Home.ReminderGearTransform, Is.Null);
            Assert.That(_messaging.PromptCalls, Is.Zero,
                "composition and launch reconciliation are never native-prompt sites");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.Zero);
            CollectionAssert.AreEqual(new[] { "daily-ready" }, _messaging.CancelAttempts,
                "default-off launch exits the one Daily Journey without inferring consent");
        }

        [UnityTest]
        public IEnumerator FirstCountedWin_ShowsPromptOnlyAfterHomeLockout_WithSeenAlreadyDurable()
        {
            Launch(new FakeMessaging());
            yield return null;
            _messaging.ClearProviderCalls();

            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.SelectDaily();
            Assert.That(_root.IsDailySession, Is.True, "the pinned Daily is precomputed");
            yield return WinCurrentDaily();

            Assert.That(_root.LifetimeDailyCompletions, Is.EqualTo(1));
            Assert.That(_root.Home.ReminderGearTransform, Is.Not.Null,
                "the gear is created only after the durable counted completion");
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.False,
                "a win alone does not present over gameplay/results");
            Assert.That(ReadPreferences().PromptSeen, Is.False,
                "eligibility is not consumed before Home is actually presented");

            TapResultsCta();
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.False);
            yield return null;
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.False,
                "the existing one-yield repeat-tap lockout remains empty");
            yield return null;

            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.True);
            Assert.That(ReadPreferences().PromptSeen, Is.True,
                "the visible one-shot prompt must already be durable");
            Assert.That(_messaging.PromptCalls, Is.Zero,
                "the soft prompt itself is not the OS prompt");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DurableEarnedState_RecoversPrompt_AndNotNowSurvivesSecondBoot()
        {
            SeedCountedDaily(promptSeen: false);
            Launch(new FakeMessaging());
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.False,
                "cold composition queues, rather than immediately paints, the earned prompt");
            yield return null;

            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.True,
                "durable lifetime progress recovers a win interrupted before Home return");
            Assert.That(ReadPreferences().PromptSeen, Is.True);
            Assert.That(_messaging.PromptCalls, Is.Zero);
            Assert.That(_root.Input.HandleTapAtScreen(
                _root.Home.ReminderSheet.DismissRectPx.center), Is.EqualTo(-3));
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.False);
            Assert.That(_messaging.PromptCalls, Is.Zero);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.Zero);

            yield return DestroyRoot();
            Launch(new FakeMessaging());
            yield return null;

            Assert.That(_root.Home.ReminderGearTransform, Is.Not.Null);
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.False,
                "Not now is permanent across a real save reload");
            Assert.That(ReadPreferences().PromptSeen, Is.True);
            Assert.That(_messaging.PromptCalls, Is.Zero);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReplayAfterDismissal_DoesNotArmAnotherAutomaticPrompt()
        {
            Launch(new FakeMessaging());
            yield return null;
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.SelectDaily();
            yield return WinCurrentDaily();
            TapResultsCta();
            yield return null;
            yield return null;
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.True);
            Assert.That(_root.Input.HandleTapAtScreen(
                _root.Home.ReminderSheet.DismissRectPx.center), Is.EqualTo(-3));

            _root.SelectDaily();
            Assert.That(_root.IsDailySession, Is.True);
            yield return WinCurrentDaily();
            Assert.That(_root.LifetimeDailyCompletions, Is.EqualTo(1),
                "the same date is a replay and cannot count twice");
            TapResultsCta();
            yield return null;
            yield return null;

            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.False);
            Assert.That(_messaging.PromptCalls, Is.Zero);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PromptAccept_IsTheFirstNativePrompt_AndDenialStaysDurablyOff()
        {
            SeedCountedDaily(promptSeen: false);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Unknown,
                CanRequestPermissionValue = true,
                PromptResult = MessagingPermission.Denied,
            };
            messaging.DurableStateProbe = DurableState;
            Launch(messaging);
            yield return null;

            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.True);
            Assert.That(_messaging.PromptCalls, Is.Zero,
                "automatic presentation must not request native permission");
            int cancelsBefore = _messaging.CancelAttempts.Count;
            Assert.That(_root.Input.HandleTapAtScreen(
                _root.Home.ReminderSheet.AcceptRectPx.center), Is.EqualTo(-3));
            yield return null;

            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { false }, _messaging.PromptFallbackArguments);
            Assert.That(ReadPreferences().Enabled, Is.False);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.Zero);
            Assert.That(_messaging.CancelAttempts.Count, Is.EqualTo(cancelsBefore + 1));
            Assert.That(_messaging.ProviderStateAtCall[^1], Is.EqualTo("cancel:false:morning"),
                "the durable disabled commit must precede the provider cancel");
        }

        [UnityTest]
        public IEnumerator Settings_CommitBeforeProvider_ForSlotsOnOffAndAuthorizedEnable()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Authorized,
                CanRequestPermissionValue = false,
            };
            messaging.DurableStateProbe = DurableState;
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Tap(_root.Home.ReminderSheet.AfternoonRectPx);
            Assert.That(ReadPreferences().Slot, Is.EqualTo(DailyReminderSlot.Afternoon));
            Assert.That(_messaging.ScheduleAttempts.Count, Is.Zero,
                "slot while effectively off changes only durable preselection");
            Assert.That(_messaging.CancelAttempts.Count, Is.Zero);

            Tap(_root.Home.ReminderSheet.OnRectPx);
            yield return null;
            Assert.That(_messaging.PromptCalls, Is.Zero,
                "already-authorized explicit On needs no native prompt");
            Assert.That(ReadPreferences().Enabled, Is.True);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            Assert.That(_messaging.ScheduleAttempts[0].NotificationId, Is.EqualTo("daily-ready"));
            Assert.That(_messaging.ScheduleAttempts[0].Slot,
                Is.EqualTo(DailyReminderSlot.Afternoon));
            Assert.That(_messaging.ProviderStateAtCall[0],
                Is.EqualTo("schedule:true:afternoon"));

            Tap(_root.Home.ReminderSheet.EveningRectPx);
            Assert.That(ReadPreferences().Slot, Is.EqualTo(DailyReminderSlot.Evening));
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(2));
            Assert.That(_messaging.ScheduleAttempts[1].Slot,
                Is.EqualTo(DailyReminderSlot.Evening));
            Assert.That(_messaging.ProviderStateAtCall[1],
                Is.EqualTo("schedule:true:evening"));

            Tap(_root.Home.ReminderSheet.OffRectPx);
            Assert.That(ReadPreferences().Enabled, Is.False);
            CollectionAssert.AreEqual(new[] { "daily-ready" }, _messaging.CancelAttempts);
            Assert.That(_messaging.ProviderStateAtCall[2], Is.EqualTo("cancel:false:evening"));
        }

        [UnityTest]
        public IEnumerator CommitRefusal_RepaintsDurableOnOffAndSlot_WithoutProviderMutation()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Authorized,
                CanRequestPermissionValue = false,
            };
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();
            OpenReminderSettings();

            string blockedSaveBackup = BlockSaveWrites();
            Tap(_root.Home.ReminderSheet.AfternoonRectPx);
            Assert.That(_root.Home.ReminderSheet.SelectedSlot,
                Is.EqualTo(DailyReminderSlot.Morning),
                "a refused slot commit must repaint the durable preselection");
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
            Assert.That(_messaging.CancelAttempts, Is.Empty);
            Tap(_root.Home.ReminderSheet.OnRectPx);
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOff")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal),
                "a refused On commit must repaint the durable Off choice");
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
            Assert.That(_messaging.CancelAttempts, Is.Empty,
                "no player provider mutation may follow a refused local commit");
            RestoreSaveWrites(blockedSaveBackup);

            Tap(_root.Home.ReminderSheet.OnRectPx);
            Assert.That(ReadPreferences().Enabled, Is.True,
                "control: restored persistence accepts the same real On action");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            _messaging.ClearProviderCalls();

            blockedSaveBackup = BlockSaveWrites();
            Tap(_root.Home.ReminderSheet.EveningRectPx);
            Assert.That(_root.Home.ReminderSheet.SelectedSlot,
                Is.EqualTo(DailyReminderSlot.Morning),
                "a refused enabled-slot commit must repaint the durable slot");
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
            Assert.That(_messaging.CancelAttempts, Is.Empty);
            Tap(_root.Home.ReminderSheet.OffRectPx);
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOn")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal),
                "a refused Off commit must repaint the durable On choice");
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
            Assert.That(_messaging.CancelAttempts, Is.Empty);
            RestoreSaveWrites(blockedSaveBackup);

            Assert.That(ReadPreferences().Enabled, Is.True);
            Assert.That(ReadPreferences().Slot, Is.EqualTo(DailyReminderSlot.Morning));
        }

        [UnityTest]
        public IEnumerator ExhaustedPermission_UsesFallbackOnlyFromExplicitSettingsAction()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                PromptResult = MessagingPermission.Authorized,
            };
            Launch(messaging);
            yield return null;

            Assert.That(_messaging.PromptCalls, Is.Zero);
            OpenReminderSettings();
            Assert.That(_root.Home.ReminderSheet.OpenSettingsVisible, Is.True);
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            yield return null;

            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { true }, _messaging.PromptFallbackArguments);
            Assert.That(ReadPreferences().Enabled, Is.True);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SettingsFallback_GrantOnResume_EnablesAndSchedulesOnceWithoutSecondTap()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                PromptResult = MessagingPermission.Unknown,
            };
            messaging.DurableStateProbe = DurableState;
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            yield return null;
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { true }, _messaging.PromptFallbackArguments);
            Assert.That(ReadPreferences().Enabled, Is.False,
                "the fallback's initial Unknown result cannot infer consent");
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);

            _messaging.PermissionValue = MessagingPermission.Authorized;
            ResumeApplicationTwice();
            Assert.That(ReadPreferences().Enabled, Is.False,
                "focus callbacks queue work but do not mutate before the main-thread Update");
            yield return null;

            Assert.That(ReadPreferences().Enabled, Is.True,
                "the original explicit settings intent completes without a second tap");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            Assert.That(_messaging.ProviderStateAtCall[^1],
                Is.EqualTo("schedule:true:morning"),
                "the durable enabled commit precedes Journey scheduling");
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications allowed."));
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOn")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal),
                "the visible settings sheet repaints effectively On");

            ResumeApplicationTwice();
            yield return null;
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1),
                "duplicate focus/resume callbacks cannot schedule twice");
        }

        [UnityTest]
        public IEnumerator EnabledReminder_ExternalPermissionRevocation_ReconcilesOnceOnNextUpdate()
        {
            SeedCountedDaily(promptSeen: true, enabled: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Authorized,
                CanRequestPermissionValue = false,
            };
            messaging.DurableStateProbe = DurableState;
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications allowed."));
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOn")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal), "precondition: effective state starts On");

            _messaging.PermissionValue = MessagingPermission.Denied;
            _messaging.CanRequestPermissionValue = false;
            ResumeApplicationTwice();

            Assert.That(ReadPreferences().Enabled, Is.True,
                "external OS revocation must preserve the player's durable reminder intent");
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications allowed."),
                "lifecycle callbacks queue work but cannot mutate Unity UI immediately");
            Assert.That(_messaging.PromptCalls, Is.Zero);
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
            Assert.That(_messaging.CancelAttempts, Is.Empty);

            yield return null;

            Assert.That(ReadPreferences().Enabled, Is.True);
            Assert.That(_messaging.PromptCalls, Is.Zero,
                "permission reconciliation is never a native-prompt site");
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
            CollectionAssert.AreEqual(new[] { "daily-ready" },
                _messaging.CancelAttempts,
                "duplicate focus/pause callbacks coalesce into one Journey exit");
            CollectionAssert.AreEqual(new[] { "cancel:true:morning" },
                _messaging.ProviderStateAtCall,
                "provider cleanup must not rewrite the durable player choice");
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications are off in device settings."));
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOff")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal),
                "denied permission repaints the durable choice effectively Off");
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOn")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.CreamCard));

            yield return null;
            Assert.That(_messaging.CancelAttempts.Count, Is.EqualTo(1),
                "reconciliation is event-driven, not polled every frame");

            _messaging.ClearProviderCalls();
            _messaging.PermissionValue = MessagingPermission.Authorized;
            ResumeApplicationTwice();
            Assert.That(_messaging.ScheduleAttempts, Is.Empty,
                "re-authorization also waits for the main-thread Update");
            yield return null;

            Assert.That(ReadPreferences().Enabled, Is.True);
            Assert.That(_messaging.PromptCalls, Is.Zero);
            Assert.That(_messaging.CancelAttempts, Is.Empty);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1),
                "restored OS permission restores the retained Journey intent once");
            Assert.That(_messaging.ProviderStateAtCall,
                Is.EqualTo(new[] { "schedule:true:morning" }));
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications allowed."));
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOn")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal));

            yield return null;
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1),
                "restoration is event-driven, not polled every frame");
        }

        [UnityTest]
        public IEnumerator SettingsFallback_FocusGrantWinsOnce_WhenPromptTaskCompletesLater()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                HoldPermissionRequest = true,
            };
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);

            _messaging.PermissionValue = MessagingPermission.Authorized;
            ResumeApplicationTwice();
            yield return null;
            Assert.That(ReadPreferences().Enabled, Is.True);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));

            _messaging.CompleteHeldPrompt(MessagingPermission.Authorized);
            yield return null;
            yield return null;
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1),
                "late PromptAsync completion cannot repeat focus reconciliation");
            Assert.That(ReadPreferences().Enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SettingsFallback_OffThenAuthorizedOn_SupersedesHeldTaskOutcome()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                HoldPermissionRequest = true,
            };
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));

            _messaging.PermissionValue = MessagingPermission.Authorized;
            ResumeApplicationTwice();
            yield return null;
            Assert.That(ReadPreferences().Enabled, Is.True);
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));

            _messaging.ClearProviderCalls();
            Tap(_root.Home.ReminderSheet.OffRectPx);
            Assert.That(ReadPreferences().Enabled, Is.False);
            CollectionAssert.AreEqual(new[] { "daily-ready" }, _messaging.CancelAttempts);

            _messaging.ClearProviderCalls();
            Tap(_root.Home.ReminderSheet.OnRectPx);
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1),
                "authorized On must not start a second native request");
            Assert.That(ReadPreferences().Enabled, Is.True,
                "the later explicit On must not be blocked by the old held task");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            Assert.That(_messaging.CancelAttempts, Is.Empty);

            _messaging.CompleteHeldPrompt(MessagingPermission.Denied);
            yield return null;
            yield return null;
            Assert.That(ReadPreferences().Enabled, Is.True,
                "the superseded task's stale denial cannot undo the newer explicit On");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            Assert.That(_messaging.CancelAttempts, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SettingsFallback_OffThenDeniedOn_DoesNotStartSecondHeldPrompt()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                HoldPermissionRequest = true,
            };
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));

            Tap(_root.Home.ReminderSheet.OffRectPx);
            Assert.That(ReadPreferences().Enabled, Is.False);

            _messaging.PermissionValue = MessagingPermission.Denied;
            _messaging.CanRequestPermissionValue = false;
            Tap(_root.Home.ReminderSheet.OnRectPx);
            int promptCallsBeforeCompletion = _messaging.PromptCalls;

            _messaging.CompleteHeldPrompt(MessagingPermission.Denied);
            yield return null;
            yield return null;

            Assert.That(promptCallsBeforeCompletion, Is.EqualTo(1),
                "a superseded but physically pending native request still owns the duplicate-request guard");
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));
            Assert.That(ReadPreferences().Enabled, Is.False);
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SettingsFallback_CancelFailure_DoesNotStrandLaterAuthorizedResume()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                PromptResult = MessagingPermission.Unknown,
            };
            messaging.DurableStateProbe = DurableState;
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();
            _messaging.ThrowOnCancel = true;

            OpenReminderSettings();
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            yield return null;
            Assert.That(ReadPreferences().Enabled, Is.False);
            CollectionAssert.AreEqual(new[] { "daily-ready" }, _messaging.CancelAttempts);
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications unavailable on this device."));

            _messaging.ThrowOnCancel = false;
            _messaging.ClearProviderCalls();
            _messaging.PermissionValue = MessagingPermission.Authorized;
            ResumeApplicationTwice();
            yield return null;

            Assert.That(ReadPreferences().Enabled, Is.True,
                "the explicit settings intent survives a transient Cancel failure");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            Assert.That(_messaging.ProviderStateAtCall[^1],
                Is.EqualTo("schedule:true:morning"));
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications allowed."));
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOn")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal));
        }

        [UnityTest]
        public IEnumerator SettingsFallback_ExplicitOffClearsPendingResumeEnable()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                HoldPermissionRequest = true,
            };
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));
            _messaging.ClearProviderCalls();

            Tap(_root.Home.ReminderSheet.OffRectPx);
            Assert.That(ReadPreferences().Enabled, Is.False);
            CollectionAssert.AreEqual(new[] { "daily-ready" }, _messaging.CancelAttempts);
            _messaging.PermissionValue = MessagingPermission.Authorized;
            ResumeApplicationTwice();
            _messaging.CompleteHeldPrompt(MessagingPermission.Authorized);
            yield return null;
            yield return null;

            Assert.That(ReadPreferences().Enabled, Is.False,
                "explicit Off cancels the earlier settings-enable intent");
            Assert.That(_messaging.ScheduleAttempts, Is.Empty);
            CollectionAssert.AreEqual(new[] { "daily-ready" }, _messaging.CancelAttempts);
        }

        [UnityTest]
        public IEnumerator SettingsFallback_ResumeCommitRefusal_PerformsNoProviderSideEffect()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                PromptResult = MessagingPermission.Unknown,
            };
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();

            OpenReminderSettings();
            Tap(DailyReminderLayout.Calculate(Screen.safeArea, Screen.dpi,
                DailyReminderLayout.SheetMode.Settings, showSettingsFallback: true)
                .OpenSettings);
            yield return null;
            _messaging.ClearProviderCalls();

            string blockedSaveBackup = BlockSaveWrites();
            _messaging.PermissionValue = MessagingPermission.Authorized;
            ResumeApplicationTwice();
            yield return null;

            Assert.That(_messaging.ScheduleAttempts, Is.Empty,
                "an authorized OS state cannot schedule after the local commit was refused");
            Assert.That(_messaging.CancelAttempts, Is.Empty);
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOff")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal),
                "the sheet repaints from the rolled-back durable Off state");

            RestoreSaveWrites(blockedSaveBackup);
            Assert.That(ReadPreferences().Enabled, Is.False);
            yield return null;
            Assert.That(_messaging.ScheduleAttempts, Is.Empty,
                "there is no frame polling or automatic write retry");

            ResumeApplicationTwice();
            yield return null;
            Assert.That(ReadPreferences().Enabled, Is.True,
                "a later real lifecycle event may retry the retained explicit intent");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ProviderScheduleException_DoesNotBreakHome_AndReconcilesNextBoot()
        {
            SeedCountedDaily(promptSeen: true, enabled: true,
                slot: DailyReminderSlot.Evening);
            var failing = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Authorized,
                CanRequestPermissionValue = false,
                ThrowOnSchedule = true,
            };

            Assert.DoesNotThrow(() => Launch(failing));
            yield return null;
            Assert.That(_root.Home.IsVisible, Is.True,
                "provider failure must not block the playable Home");
            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            Assert.That(ReadPreferences().Enabled, Is.True,
                "provider failure keeps durable intent for reconciliation");
            OpenReminderSettings();
            Assert.That(_root.Home.ReminderSheet.StatusText,
                Is.EqualTo("Notifications unavailable on this device."));
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOff")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.MetroTeal),
                "a failed provider is painted effectively Off in this running process");
            Assert.That(Find(_root.Home.ReminderSheet.transform, "ReminderOn")
                    .GetComponent<Image>().color,
                Is.EqualTo(Palette.CreamCard));

            yield return DestroyRoot();
            var recovered = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Authorized,
                CanRequestPermissionValue = false,
            };
            Launch(recovered);
            yield return null;

            Assert.That(_messaging.ScheduleAttempts.Count, Is.EqualTo(1));
            Assert.That(_messaging.ScheduleAttempts[0].Slot,
                Is.EqualTo(DailyReminderSlot.Evening));
            Assert.That(ReadPreferences().Enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ColdSubscriptionCallback_IsQueuedUntilUpdate_ThenOpensCurrentDailyOnce()
        {
            var messaging = new FakeMessaging { RaiseDailyWhenSubscribed = true };
            Launch(messaging);
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;

            Assert.That(_root.IsDailySession, Is.False,
                "an SDK callback during composition may enqueue but may not mutate Unity state");
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_messaging.ListenerAdds, Is.EqualTo(1));
            yield return null;

            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.ActiveDailyDateKey, Is.EqualTo(PinnedDateKey));
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_messaging.ListenerAdds, Is.EqualTo(1),
                "one callback cannot be duplicated by repeated listener registration");
        }

        [UnityTest]
        public IEnumerator WorkerThreadCallback_MutatesNoUnityStateUntilMainThreadUpdate()
        {
            Launch(new FakeMessaging());
            yield return null;
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;

            Task.Run(() => _messaging.RaiseLinkOpened(MessagingRoute.Daily))
                .GetAwaiter().GetResult();
            Assert.That(_root.IsDailySession, Is.False,
                "the provider thread may enqueue only");
            Assert.That(_root.Home.IsVisible, Is.True);
            yield return null;

            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.ActiveDailyDateKey, Is.EqualTo(PinnedDateKey));
        }

        [UnityTest]
        public IEnumerator WorkerThreadCallback_FromIntro_HidesTheLiveSheetBeforeEnteringDaily()
        {
            Launch(new FakeMessaging());
            yield return null;
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;

            Tap(_root.Home.PinPaintedRectPx);
            Assert.That(_root.Intro.IsVisible, Is.True);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1),
                "precondition: only Intro's real Play region remains registered");
            CollectionAssert.AreEqual(new[] { "home", "intro" },
                _root.Stack.ToBreadcrumb());

            Task.Run(() => _messaging.RaiseLinkOpened(MessagingRoute.Daily))
                .GetAwaiter().GetResult();
            Assert.That(_root.Intro.IsVisible, Is.True,
                "the provider thread must not touch the active Unity screen");
            Assert.That(_root.IsDailySession, Is.False);
            yield return null;

            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.ActiveDailyDateKey, Is.EqualTo(PinnedDateKey));
            Assert.That(_root.Intro.IsVisible, Is.False,
                "Daily entry must remove the visible Intro overlay");
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.Stack.Count, Is.Zero);
            Assert.That(_root.Input.Regions.Count, Is.Zero,
                "Intro's Play region must not remain live over the Daily board");
        }

        [UnityTest]
        public IEnumerator Destroy_CancelsAndDisposesOnce_AndLateGrantCannotWriteOrSchedule()
        {
            SeedCountedDaily(promptSeen: true);
            var messaging = new FakeMessaging
            {
                PermissionValue = MessagingPermission.Denied,
                CanRequestPermissionValue = false,
                HoldPermissionRequest = true,
            };
            Launch(messaging);
            yield return null;
            _messaging.ClearProviderCalls();
            OpenReminderSettings();
            Tap(_root.Home.ReminderSheet.OnRectPx);
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { true }, _messaging.PromptFallbackArguments);
            Assert.That(_messaging.LastPromptToken.CanBeCanceled, Is.True);
            Tap(_root.Home.ReminderSheet.OnRectPx);
            Assert.That(_messaging.PromptCalls, Is.EqualTo(1),
                "a second explicit tap cannot overlap the in-flight native prompt");

            var destroyedMessaging = _messaging;
            yield return DestroyRoot();
            Assert.That(destroyedMessaging.LastPromptToken.IsCancellationRequested, Is.True);
            Assert.That(destroyedMessaging.ListenerRemoves, Is.EqualTo(1));
            Assert.That(destroyedMessaging.DisposeCalls, Is.EqualTo(1));

            destroyedMessaging.CompleteHeldPrompt(MessagingPermission.Authorized);
            yield return null;
            yield return null;
            Assert.That(ReadPreferences().Enabled, Is.False,
                "a late completion cannot mutate save after the Unity owner is destroyed");
            Assert.That(destroyedMessaging.ScheduleAttempts.Count, Is.Zero);
            Assert.That(destroyedMessaging.DisposeCalls, Is.EqualTo(1));
        }

        private void Launch(FakeMessaging messaging)
        {
            _messaging = messaging;
            GameRoot.MessagingFactoryOverride = () => messaging;
            _root = GameRoot.Launch();
        }

        private IEnumerator DestroyRoot()
        {
            var go = _root.gameObject;
            _root = null;
            UnityEngine.Object.Destroy(go);
            yield return null;
        }

        private void OpenReminderSettings()
        {
            Assert.That(_root.Home.ReminderGearTransform, Is.Not.Null);
            Tap(_root.Home.ReminderGearRectPx);
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.True);
        }

        private void Tap(Rect rect)
        {
            Assert.That(_root.Input.HandleTapAtScreen(rect.center), Is.EqualTo(-3));
        }

        private void ResumeApplicationTwice()
        {
            for (int i = 0; i < 2; i++)
            {
                _root.gameObject.SendMessage("OnApplicationFocus", true,
                    SendMessageOptions.DontRequireReceiver);
                _root.gameObject.SendMessage("OnApplicationPause", false,
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void TapResultsCta()
        {
            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(_root.Input.HandleTapAtScreen(panel.ChipPaintedRectPx.center),
                Is.EqualTo(-3));
        }

        private IEnumerator WinCurrentDaily()
        {
            var level = _root.Session.Level;
            var solve = CatMetro.Domain.Solver.LevelSolver.Solve(
                level.Graph, (ulong)level.Dto.Seed);
            Assert.That(solve.Verdict,
                Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.Solved));
            foreach (var entry in solve.OptimalLog.Entries)
            {
                while (_root.Session.State.Tick < entry.Tick
                    && _root.Session.State.Outcome.Kind
                        == CatMetro.Domain.OutcomeKind.Running)
                    _root.Session.AdvanceMs(
                        CatMetro.Application.Session.TickInterpolator.TICK_MS);
                _root.Session.EnqueueToggle(entry.SwitchId);
            }
            _root.Session.AdvanceMs(
                400 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;
            Assert.That(_root.GetComponent<ResultsPanel>().IsVisible, Is.True);
        }

        private void SeedCountedDaily(bool promptSeen, bool enabled = false,
            DailyReminderSlot slot = null)
        {
            var store = LoadStore();
            var progress = new DailyProgressTracker(store);
            var selection = progress.ObserveUtcDate(PinnedDateKey);
            var completion = progress.RecordDailyCompletion(selection);
            if (!completion.Counted || completion.LifetimeCompletions != 1)
                throw new InvalidOperationException("failed to seed one durable Daily completion");

            var preferences = new DailyReminderPreferences(store);
            if (promptSeen && !preferences.TryMarkPromptSeen())
                throw new InvalidOperationException("failed to seed prompt-seen");
            if (slot != null && !preferences.TrySetSlot(slot))
                throw new InvalidOperationException("failed to seed reminder slot");
            if (enabled && !preferences.TrySetEnabled(true))
                throw new InvalidOperationException("failed to seed enabled intent");
        }

        private DailyReminderPreferences ReadPreferences() =>
            new DailyReminderPreferences(LoadStore());

        private string BlockSaveWrites()
        {
            string saveDirectory = Path.Combine(_tmpDir, "save");
            string backupDirectory = Path.Combine(_tmpDir,
                "save-backup-" + Guid.NewGuid().ToString("N"));
            Directory.Move(saveDirectory, backupDirectory);
            File.WriteAllText(saveDirectory, "commit blocker");
            return backupDirectory;
        }

        private void RestoreSaveWrites(string backupDirectory)
        {
            string saveDirectory = Path.Combine(_tmpDir, "save");
            File.Delete(saveDirectory);
            Directory.Move(backupDirectory, saveDirectory);
        }

        private string DurableState()
        {
            var preferences = ReadPreferences();
            return preferences.Enabled.ToString().ToLowerInvariant() + ":"
                + preferences.Slot.TagValue;
        }

        private SaveStore LoadStore()
        {
            var boundsBytes = File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "config", "runtime_bounds.json"));
            var bounds = RuntimeBounds.Parse(boundsBytes);
            if (!bounds.Ok) throw new InvalidOperationException(bounds.Error.ToString());
            var store = new SaveStore(
                new TestStorageRoot(Path.Combine(_tmpDir, "save")),
                new RealSaveFileSystem(), bounds.Value, new MigrationTable());
            store.Load();
            return store;
        }

        private sealed class TestStorageRoot : IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public TestStorageRoot(string path)
            {
                SaveDirectory = path;
                Directory.CreateDirectory(path);
            }
        }

        private sealed class FakeMessaging : IMessaging
        {
            private Action<MessagingRoute> _linkOpened;
            private readonly TaskCompletionSource<MessagingPermission> _heldPrompt =
                new TaskCompletionSource<MessagingPermission>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public bool IsAvailableValue = true;
            public MessagingPermission PermissionValue = MessagingPermission.Unknown;
            public bool CanRequestPermissionValue = true;
            public MessagingPermission PromptResult = MessagingPermission.Authorized;
            public bool HoldPermissionRequest;
            public bool RaiseDailyWhenSubscribed;
            public bool ThrowOnSchedule;
            public bool ThrowOnCancel;
            public Func<string> DurableStateProbe;

            public int PromptCalls { get; private set; }
            public int ListenerAdds { get; private set; }
            public int ListenerRemoves { get; private set; }
            public int DisposeCalls { get; private set; }
            public CancellationToken LastPromptToken { get; private set; }
            public List<bool> PromptFallbackArguments { get; } = new List<bool>();
            public List<DailyChallengeNotification> ScheduleAttempts { get; } =
                new List<DailyChallengeNotification>();
            public List<string> CancelAttempts { get; } = new List<string>();
            public List<string> ProviderStateAtCall { get; } = new List<string>();

            public bool IsAvailable => IsAvailableValue;
            public string SubscriptionId => "fake-subscription";
            public MessagingPermission Permission => PermissionValue;
            public bool CanRequestPermission => CanRequestPermissionValue;

            public event Action<MessagingRoute> LinkOpened
            {
                add
                {
                    ListenerAdds++;
                    _linkOpened += value;
                    if (RaiseDailyWhenSubscribed)
                    {
                        RaiseDailyWhenSubscribed = false;
                        value(MessagingRoute.Daily);
                    }
                }
                remove
                {
                    ListenerRemoves++;
                    _linkOpened -= value;
                }
            }

            public async Task<MessagingPermission> PromptAsync(bool fallbackToSettings,
                CancellationToken cancellationToken)
            {
                PromptCalls++;
                PromptFallbackArguments.Add(fallbackToSettings);
                LastPromptToken = cancellationToken;
                MessagingPermission result = HoldPermissionRequest
                    ? await _heldPrompt.Task
                    : PromptResult;
                PermissionValue = result;
                if (result == MessagingPermission.Denied)
                    CanRequestPermissionValue = false;
                return result;
            }

            public void Schedule(DailyChallengeNotification notification)
            {
                ScheduleAttempts.Add(notification);
                ProviderStateAtCall.Add("schedule:"
                    + (DurableStateProbe != null ? DurableStateProbe() : "unobserved"));
                if (ThrowOnSchedule) throw new InvalidOperationException("provider unavailable");
            }

            public void Cancel(string notificationId)
            {
                CancelAttempts.Add(notificationId);
                ProviderStateAtCall.Add("cancel:"
                    + (DurableStateProbe != null ? DurableStateProbe() : "unobserved"));
                if (ThrowOnCancel) throw new InvalidOperationException("cancel unavailable");
            }

            public void Dispose()
            {
                DisposeCalls++;
                _linkOpened = null;
            }

            public void RaiseLinkOpened(MessagingRoute route) => _linkOpened?.Invoke(route);

            public void CompleteHeldPrompt(MessagingPermission permission) =>
                _heldPrompt.TrySetResult(permission);

            public void ClearProviderCalls()
            {
                ScheduleAttempts.Clear();
                CancelAttempts.Clear();
                ProviderStateAtCall.Clear();
            }
        }
    }
}
