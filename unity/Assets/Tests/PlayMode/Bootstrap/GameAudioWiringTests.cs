#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Application.Save;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
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
        public IEnumerator RealBoot_WiresClipsListenerAndDurableHomeMute()
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
            Assert.That(sources, Has.Length.EqualTo(3));
            foreach (var source in sources)
            {
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.spatialBlend, Is.Zero,
                    "all Cat Metro cues are intimate 2D presentation audio");
            }

            Assert.That(_root.Audio.Enabled, Is.True);
            Assert.That(_root.Home.AudioEnabled, Is.True);
            Assert.That(_root.Audio.SnapshotObservationCount, Is.GreaterThan(0),
                "GameRoot explicitly feeds presentation snapshots after its state/view update");
            Assert.That(_root.Input.UiTapAccepted, Is.Not.Null,
                "composition binds accepted button taps to audio");
            Assert.That(_root.Input.SwitchTapAccepted, Is.Not.Null,
                "composition binds accepted switch taps to audio");
            Assert.That(_root.Wardrobe.PurchaseConfirmed, Is.Not.Null,
                "composition binds confirmed purchases to audio");
            Assert.That(_root.Input.HandleTapAtScreen(_root.Home.AudioToggleRectPx.center),
                Is.EqualTo(-3));
            Assert.That(_root.Audio.Enabled, Is.False);
            Assert.That(_root.Home.AudioEnabled, Is.False);

            UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;

            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Audio.Enabled, Is.False,
                "the real boot reads the canonical saved audio preference");
            Assert.That(_root.Home.AudioEnabled, Is.False);
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
            public bool IsAvailable => false;
            public string SubscriptionId => string.Empty;
            public MessagingPermission Permission => MessagingPermission.Unknown;
            public bool CanRequestPermission => false;
            public event Action<MessagingRoute> LinkOpened
            {
                add { }
                remove { }
            }

            public Task<MessagingPermission> PromptAsync(bool fallbackToSettings,
                CancellationToken cancellationToken) =>
                Task.FromResult(MessagingPermission.Unknown);

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
