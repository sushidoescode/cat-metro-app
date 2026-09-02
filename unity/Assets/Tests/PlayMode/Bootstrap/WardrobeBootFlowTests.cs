using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed class WardrobeBootFlowTests
    {
        private GameRoot _root;
        private TestStorageRoot _storage;
        private TestDirectory _devCaptureRoot;

        [SetUp]
        public void SetUp()
        {
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            ResetGameRootSeams();
            _devCaptureRoot = new TestDirectory();
            DevBootOverride.DirectoryOverride = _devCaptureRoot.RootPath;
            DevLevelOverride.DirectoryOverride = _devCaptureRoot.RootPath;
            _storage = new TestStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _storage;
            GameRoot.MessagingFactoryOverride = () => new InertMessaging();
            GameRoot.AnalyticsRuntimeFactory = () =>
                new GameAnalyticsRuntime(new InertAnalytics());
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;
            ResetGameRootSeams();
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            _storage?.Dispose();
            _storage = null;
            _devCaptureRoot?.Dispose();
            _devCaptureRoot = null;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator ShippedHome_WardrobeRoundTrip_IsVisibleTappableAndKeepsSimulationPaused()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Wardrobe, Is.Not.Null,
                "the real shipped composition owns the monetization surface");
            Assert.That(_root.Wardrobe.EntryVisible, Is.True,
                "the wardrobe capsule is visible on Home, not hidden behind a debug path");
            Assert.That(_root.Wardrobe.PanelVisible, Is.False);
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0));

            var profile = CosmeticRuntime.Current;
            Assert.That(profile.Catalog.AdmittedCatCount, Is.EqualTo(3));
            Assert.That(profile.Catalog.AdmittedRowCount, Is.EqualTo(3));
            Assert.That(_root.Home.ProfilePortrait, Is.Not.Null);
            Assert.That(_root.Home.ProfilePortrait.AppliedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_root.Wardrobe.EntryPortrait.AppliedCatId, Is.EqualTo("red_tabby"));
            Assert.That(profile.TrySelectCat("blue_siamese"), Is.True);
            yield return null;
            Assert.That(_root.Home.ProfilePortrait.AppliedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(_root.Wardrobe.EntryPortrait.AppliedCatId,
                Is.EqualTo("blue_siamese"));

            int entryTap = _root.Input.HandleTapAtScreen(_root.Wardrobe.EntryRectPx.center);
            Assert.That(entryTap, Is.EqualTo(-3), "the painted capsule is reachable by real input");
            yield return null;

            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.Wardrobe.PanelVisible, Is.True);
            Assert.That(_root.Wardrobe.LargePortrait.AppliedCatId,
                Is.EqualTo("blue_siamese"));
            CollectionAssert.AreEqual(new[] { "home", "wardrobe" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                "opening the wardrobe never advances the puzzle behind it");

            int backTap = _root.Input.HandleTapAtScreen(_root.Wardrobe.BackRectPx.center);
            Assert.That(backTap, Is.EqualTo(-3));
            yield return null;

            Assert.That(_root.Wardrobe.PanelVisible, Is.False);
            Assert.That(_root.Wardrobe.EntryVisible, Is.True);
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Home.ProfilePortrait.AppliedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(_root.Wardrobe.EntryPortrait.AppliedCatId,
                Is.EqualTo("blue_siamese"));
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0));
        }

        private static void ResetGameRootSeams()
        {
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.BootToHome = false;
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.MessagingFactoryOverride = null;
            GameRoot.AnalyticsRuntimeFactory = null;
            DevBootOverride.DirectoryOverride = null;
            DevLevelOverride.DirectoryOverride = null;
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
                CancellationToken cancellationToken)
                => Task.FromResult(MessagingPermission.Unknown);

            public void Schedule(DailyChallengeNotification notification) { }
            public void Cancel(string notificationId) { }
            public void Dispose() { }
        }

        private sealed class TestDirectory : IDisposable
        {
            public string RootPath { get; }

            public TestDirectory()
            {
                RootPath = Path.Combine(Path.GetTempPath(),
                    "cm-cosmetics-task8-wardrobe-devcap-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(RootPath)) Directory.Delete(RootPath, true);
                }
                catch
                {
                    // Best-effort test-artifact cleanup.
                }
            }
        }

        private sealed class TestStorageRoot : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public TestStorageRoot()
            {
                SaveDirectory = Path.Combine(Path.GetTempPath(),
                    "cm-cosmetics-task8-wardrobe-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(SaveDirectory);
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(SaveDirectory, true);
                }
                catch
                {
                    // Best-effort test-artifact cleanup.
                }
            }
        }
    }
}
