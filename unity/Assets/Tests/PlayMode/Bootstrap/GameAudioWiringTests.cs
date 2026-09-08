#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Application.Save;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Presentation.Screens;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class GameAudioWiringTests
    {
        private GameRoot _root;
        private TestStorageRoot _storage;

        [SetUp]
        public void SetUp()
        {
            ResetSeams();
            _storage = new TestStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _storage;
            GameRoot.MessagingFactoryOverride = () => new InertMessaging();
            GameRoot.AnalyticsRuntimeFactory = () =>
                new GameAnalyticsRuntime(new InertAnalytics());
            string devCaptureDirectory = Path.Combine(_storage.SaveDirectory, "devcap");
            Directory.CreateDirectory(devCaptureDirectory);
            DevBootOverride.DirectoryOverride = devCaptureDirectory;
            DevLevelOverride.DirectoryOverride = devCaptureDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;
            ResetSeams();
            _storage?.Dispose();
            _storage = null;
        }

        [UnityTest]
        public IEnumerator RealBoot_WiresAllChannelsAndPersistsSettingsAcrossRelaunch()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Audio, Is.Not.Null);
            Assert.That(_root.Audio.LoadedClipCount,
                Is.EqualTo(CatMetro.Presentation.Audio.GameAudio.ExpectedClipCount));
            Assert.That(_root.Cam.GetComponents<AudioListener>(), Has.Length.EqualTo(1),
                "the runtime camera owns exactly one listener");
            Assert.That(_root.Cam.GetComponent<AudioListener>().enabled, Is.True);
            int enabledListeners = 0;
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (listener.enabled) enabledListeners++;
            Assert.That(enabledListeners, Is.EqualTo(1),
                "only one managed listener is active in the test scene");

            var sources = _root.Audio.GetComponents<AudioSource>();
            Assert.That(sources, Has.Length.EqualTo(6));
            foreach (var source in sources)
            {
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.spatialBlend, Is.Zero,
                    "all Cat Metro cues are intimate 2D presentation audio");
            }

            Assert.That(_root.Audio.Enabled, Is.True);
            Assert.That(_root.Music, Is.Not.Null);
            Assert.That(_root.Music.LoadedClipCount, Is.EqualTo(5));
            Assert.That(_root.Music.Enabled, Is.True);
            Assert.That(_root.Haptics, Is.Not.Null);
            Assert.That(_root.Haptics.Enabled, Is.True);
            Assert.That(_root.MotionOffToggle, Is.False);
            Assert.That(_root.Home.AudioEnabled, Is.True);
            Assert.That(_root.Audio.SnapshotObservationCount, Is.GreaterThan(0),
                "GameRoot explicitly feeds presentation snapshots after its state/view update");
            Assert.That(_root.Input.UiTapAccepted, Is.Not.Null,
                "composition binds accepted button taps to audio");
            Assert.That(_root.Input.SwitchTapAccepted, Is.Not.Null,
                "composition binds accepted switch taps to audio");
            Assert.That(_root.Wardrobe.PurchaseConfirmed, Is.Not.Null,
                "composition binds confirmed purchases to audio");
            Assert.That(_root.Input.Regions.IsRegistered("home.audio.toggle"), Is.True,
                "C's speaker keeps the existing Home audio entry region");
            Assert.That(_root.Input.HandleTapAtScreen(_root.Home.AudioToggleRectPx.center),
                Is.EqualTo(-3));
            Assert.That(_root.Settings, Is.Not.Null);
            Assert.That(_root.Settings.IsVisible, Is.True);
            Assert.That(_root.Stack.Current, Is.EqualTo("settings"));
            Assert.That(_root.Audio.Enabled, Is.True, "opening settings does not mute a channel");
            foreach (SettingsChannel channel in Enum.GetValues(typeof(SettingsChannel)))
                Assert.That(_root.Input.HandleTapAtScreen(_root.Settings.RowRectPx(channel).center), Is.EqualTo(-3));
            Assert.That(_root.Audio.Enabled, Is.False);
            Assert.That(_root.Home.AudioEnabled, Is.False);
            Assert.That(_root.Music.Enabled, Is.False);
            Assert.That(_root.Haptics.Enabled, Is.False);
            Assert.That(_root.MotionOffToggle, Is.True);
            Assert.That(_root.Input.HandleTapAtScreen(_root.Settings.CloseRectPx.center), Is.EqualTo(-3));
            Assert.That(_root.Settings.IsVisible, Is.False);
            Assert.That(_root.Stack.Current, Is.EqualTo("home"));

            UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;

            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Audio.Enabled, Is.False,
                "the real boot reads the canonical saved audio preference");
            Assert.That(_root.Home.AudioEnabled, Is.False);
            Assert.That(_root.Music.Enabled, Is.False);
            Assert.That(_root.Haptics.Enabled, Is.False);
            Assert.That(_root.MotionOffToggle, Is.True, "settings.motion=false binds the runtime reduce-motion toggle at boot");
            Assert.That(_root.Input.HandleTapAtScreen(_root.Home.AudioToggleRectPx.center), Is.EqualTo(-3));
            Assert.That(_root.Settings.IsVisible, Is.True);
            Assert.That(_root.Audio.Enabled, Is.False, "a muted speaker opens settings without enabling Sound");
            Assert.That(_root.Input.HandleTapAtScreen(_root.Settings.RowRectPx(SettingsChannel.Sound).center), Is.EqualTo(-3));
            Assert.That(_root.Home.AudioEnabled, Is.True, "Sound updates the state used to paint the Home speaker");
            Assert.That(_root.Music.Enabled, Is.False);
            Assert.That(_root.Haptics.Enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator DailyNavigationClosesSettingsAndItsBlocker()
        {
            GameRoot.DailyEntryUnlocked = true;
            _root = GameRoot.Launch();
            yield return null;
            _root.ShowSettings();
            Assert.That(_root.Settings.IsVisible, Is.True);
            _root.DailyClockUnixSeconds = () => 1787572800L;
            _root.SelectDaily();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!_root.IsDailySession && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.Settings.IsVisible, Is.False);
            Assert.That(_root.Input.Regions.IsRegistered("settings.blocker"), Is.False);
            Assert.That(_root.ScreensVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator UnlockedDailyReminderRefreshesBeforeTheFirstDailyCompletion()
        {
            GameRoot.DailyEntryUnlocked = true;
            GameRoot.MessagingFactoryOverride = () => new InertMessaging(available: true);
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.LifetimeDailyCompletions, Is.Zero);
            _root.ShowSettings();
            Assert.That(_root.Settings.ReminderVisible, Is.True);
            _root.Input.HandleTapAtScreen(_root.Settings.ReminderRectPx.center);
            Assert.That(_root.Home.ReminderSheet.IsVisible, Is.True);
            _root.Input.HandleTapAtScreen(_root.Home.ReminderSheet.EveningRectPx.center);
            Assert.That(_root.Home.ReminderSheet.SelectedSlot, Is.EqualTo(DailyReminderSlot.Evening));
            Assert.That(_root.Home.ReminderSheet.OpenSettingsVisible, Is.False);
            _root.Input.HandleTapAtScreen(_root.Home.ReminderSheet.OnRectPx.center);
            yield return null;
            Assert.That(_root.Home.ReminderSheet.OpenSettingsVisible, Is.True,
                "a denied permission must refresh the fallback control before the first Daily completion");
        }

        private static void ResetSeams()
        {
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.BootToHome = false;
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.MessagingFactoryOverride = null;
            GameRoot.AnalyticsRuntimeFactory = null;
            DevBootOverride.DirectoryOverride = null;
            DevLevelOverride.DirectoryOverride = null;
            SaveRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
        }

        private sealed class InertAnalytics : IAnalytics
        {
            public int QueuedEventCount => 0;
            public void Log(in AnalyticsEvent e) { }
            public void SetUserProperty(UserPropertyKey key, string value) { }
        }

        private sealed class InertMessaging : IMessaging
        {
            private readonly bool _available;
            private MessagingPermission _permission = MessagingPermission.Unknown;
            public InertMessaging(bool available = false) { _available = available; }
            public bool IsAvailable => _available;
            public string SubscriptionId => string.Empty;
            public MessagingPermission Permission => _permission;
            public bool CanRequestPermission => _available && _permission == MessagingPermission.Unknown;
            public event Action<MessagingRoute> LinkOpened
            {
                add { }
                remove { }
            }

            public Task<MessagingPermission> PromptAsync(bool fallbackToSettings,
                CancellationToken cancellationToken)
            {
                _permission = _available ? MessagingPermission.Denied : MessagingPermission.Unknown;
                return Task.FromResult(_permission);
            }

            public void Schedule(DailyChallengeNotification notification) { }
            public void Cancel(string notificationId) { }
            public void Dispose() { }
        }

        private sealed class TestStorageRoot : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public TestStorageRoot()
            {
                SaveDirectory = Path.Combine(Path.GetTempPath(),
                    "cat-metro-audio-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(SaveDirectory);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(SaveDirectory))
                        Directory.Delete(SaveDirectory, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup of test-only files.
                }
            }
        }
    }
}
#endif
