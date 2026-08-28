using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Integrations.OneSignal;
using CatMetro.Services;
using NUnit.Framework;

namespace CatMetro.Tests.Engine.Messaging
{
    public sealed class OneSignalMessagingTests
    {
        private const string ValidAppId = "11111111-2222-4333-8444-555555555555";

        [Test]
        public void Initialize_ValidAppIdBecomesAvailableWithoutPrompting()
        {
            var bridge = new FakeOneSignalBridge();
            using (var messaging = new OneSignalMessaging(bridge))
            {
                messaging.Initialize(ValidAppId);

                Assert.That(messaging.IsAvailable, Is.True);
                Assert.That(bridge.InitializedAppId, Is.EqualTo(ValidAppId));
                Assert.That(bridge.RequestPermissionCount, Is.Zero,
                    "initialization must never display the native permission prompt");
                Assert.That(bridge.ClickListenerCount, Is.EqualTo(1));
            }

            Assert.That(bridge.ClickListenerCount, Is.Zero,
                "disposing the adapter must balance its SDK listener");
            Assert.That(bridge.DisposeCount, Is.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("not-an-app-id")]
        public void Initialize_InvalidAppIdFailsClosed(string appId)
        {
            var bridge = new FakeOneSignalBridge();
            using (var messaging = new OneSignalMessaging(bridge))
            {
                messaging.Initialize(appId);

                Assert.That(messaging.IsAvailable, Is.False);
                Assert.That(bridge.InitializedAppId, Is.Null);
                Assert.That(bridge.ClickListenerCount, Is.Zero);
                Assert.That(bridge.RequestPermissionCount, Is.Zero);
            }
        }

        [Test]
        public void RuntimeConfig_AcceptsOnlyANonEmptyParseableAppId()
        {
            Assert.That(OneSignalRuntimeConfig.TryGetAppId(
                "{\"appId\":\"11111111-2222-4333-8444-555555555555\"}", out var appId),
                Is.True);
            Assert.That(appId, Is.EqualTo(ValidAppId));

            Assert.That(OneSignalRuntimeConfig.TryGetAppId("{\"appId\":\"\"}", out _),
                Is.False);
            Assert.That(OneSignalRuntimeConfig.TryGetAppId("{\"appId\":\"wrong\"}", out _),
                Is.False);
            Assert.That(OneSignalRuntimeConfig.TryGetAppId("not-json", out _), Is.False);
            Assert.That(OneSignalRuntimeConfig.TryGetAppId(null, out _), Is.False);
            Assert.That(OneSignalRuntimeConfig.LoadAppId(), Is.Empty,
                "the checked-in public config must start blank");
        }

        [Test]
        public async Task PromptAsync_IsExplicitAndMapsTheReturnedCurrentNativeState()
        {
            var bridge = new FakeOneSignalBridge
            {
                PermissionGranted = false,
                NativePermission = NativeMessagingPermission.NotDetermined,
                PermissionRequestResult = true,
                NativePermissionAfterRequest = NativeMessagingPermission.Authorized
            };
            using (var messaging = CreateInitialized(bridge))
            {
                Assert.That(messaging.Permission, Is.EqualTo(MessagingPermission.Unknown));
                Assert.That(messaging.CanRequestPermission, Is.True);

                var result = await messaging.PromptAsync(true, CancellationToken.None);

                Assert.That(result, Is.EqualTo(MessagingPermission.Authorized));
                Assert.That(bridge.RequestPermissionCount, Is.EqualTo(1));
                Assert.That(bridge.LastFallbackToSettings, Is.True);
            }
        }

        [Test]
        public void Schedule_WhenAuthorizedOptsInAndWritesOnlyTheDailyJourneyTags()
        {
            var bridge = new FakeOneSignalBridge
            {
                PermissionGranted = true,
                NativePermission = NativeMessagingPermission.Authorized
            };
            using (var messaging = CreateInitialized(bridge))
            {
                messaging.Schedule(DailyChallengeNotification.Create(
                    DailyReminderSlot.Afternoon));

                Assert.That(bridge.OptInCount, Is.EqualTo(1));
                Assert.That(bridge.Tags, Has.Count.EqualTo(2));
                Assert.That(bridge.Tags["daily_opt_in"], Is.EqualTo("true"));
                Assert.That(bridge.Tags["daily_reminder_slot"], Is.EqualTo("afternoon"));
            }
        }

        [Test]
        public void Schedule_WithoutMappedAuthorizationHasNoEffect()
        {
            foreach (var nativePermission in new[]
                     {
                         NativeMessagingPermission.NotDetermined,
                         NativeMessagingPermission.Denied
                     })
            {
                var bridge = new FakeOneSignalBridge
                {
                    PermissionGranted = false,
                    NativePermission = nativePermission
                };
                using (var messaging = CreateInitialized(bridge))
                {
                    messaging.Schedule(DailyChallengeNotification.Create(
                        DailyReminderSlot.Morning));

                    Assert.That(bridge.OptInCount, Is.Zero);
                    Assert.That(bridge.Tags, Is.Empty);
                }
            }
        }

        [Test]
        public void Cancel_DailyReadyExitsJourneyRemovesSlotAndOptsOut()
        {
            var bridge = new FakeOneSignalBridge();
            bridge.Tags["daily_reminder_slot"] = "evening";
            using (var messaging = CreateInitialized(bridge))
            {
                messaging.Cancel("daily-ready");

                Assert.That(bridge.Tags["daily_opt_in"], Is.EqualTo("false"));
                Assert.That(bridge.HasTag("daily_reminder_slot"), Is.False);
                Assert.That(bridge.OptOutCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void Cancel_UnknownIdHasNoEffect()
        {
            var bridge = new FakeOneSignalBridge();
            using (var messaging = CreateInitialized(bridge))
            {
                messaging.Cancel("some-other-category");

                Assert.That(bridge.Tags, Is.Empty);
                Assert.That(bridge.OptOutCount, Is.Zero);
                Assert.That(bridge.RemoveTagCount, Is.Zero);
            }
        }

        [Test]
        public void Clicks_AllowOnlyTheExactDailyStringRoute()
        {
            var bridge = new FakeOneSignalBridge();
            using (var messaging = CreateInitialized(bridge))
            {
                var routes = new List<MessagingRoute>();
                messaging.LinkOpened += routes.Add;

                bridge.RaiseClick(new Dictionary<string, object> { ["route"] = "daily" });
                bridge.RaiseClick(new Dictionary<string, object> { ["route"] = "unknown" });
                bridge.RaiseClick(new Dictionary<string, object> { ["route"] = 42 });
                bridge.RaiseClick(null);

                Assert.That(routes, Is.EqualTo(new[] { MessagingRoute.Daily }));
            }
        }

        [Test]
        public void Reinitialize_ReplacesTheListenerRatherThanDuplicatingIt()
        {
            var bridge = new FakeOneSignalBridge();
            using (var messaging = CreateInitialized(bridge))
            {
                var callbackCount = 0;
                messaging.LinkOpened += _ => callbackCount++;

                messaging.Initialize(ValidAppId);
                Assert.That(bridge.ClickListenerCount, Is.EqualTo(1));

                bridge.RaiseClick(new Dictionary<string, object> { ["route"] = "daily" });

                Assert.That(callbackCount, Is.EqualTo(1));
            }
        }

        private static OneSignalMessaging CreateInitialized(FakeOneSignalBridge bridge)
        {
            var messaging = new OneSignalMessaging(bridge);
            messaging.Initialize(ValidAppId);
            return messaging;
        }

        private sealed class FakeOneSignalBridge : IOneSignalBridge
        {
            private Action<IDictionary<string, object>> _clicked;

            public string InitializedAppId { get; private set; }
            public bool PermissionGranted { get; set; }
            public NativeMessagingPermission NativePermission { get; set; }
            public bool CanRequestPermission { get; set; } = true;
            public string SubscriptionId { get; set; } = "test-subscription";
            public bool PermissionRequestResult { get; set; }
            public NativeMessagingPermission NativePermissionAfterRequest { get; set; }
            public int RequestPermissionCount { get; private set; }
            public bool LastFallbackToSettings { get; private set; }
            public int OptInCount { get; private set; }
            public int OptOutCount { get; private set; }
            public int RemoveTagCount { get; private set; }
            public int DisposeCount { get; private set; }
            public Dictionary<string, string> Tags { get; } =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public int ClickListenerCount => _clicked?.GetInvocationList().Length ?? 0;

            public event Action<IDictionary<string, object>> NotificationClicked
            {
                add => _clicked += value;
                remove => _clicked -= value;
            }

            public void Initialize(string appId) => InitializedAppId = appId;

            public Task<bool> RequestPermissionAsync(bool fallbackToSettings)
            {
                RequestPermissionCount++;
                LastFallbackToSettings = fallbackToSettings;
                PermissionGranted = PermissionRequestResult;
                NativePermission = NativePermissionAfterRequest;
                return Task.FromResult(PermissionRequestResult);
            }

            public void AddTag(string key, string value) => Tags[key] = value;

            public void AddTags(Dictionary<string, string> tags)
            {
                foreach (var tag in tags)
                    Tags[tag.Key] = tag.Value;
            }

            public void RemoveTag(string key)
            {
                RemoveTagCount++;
                Tags.Remove(key);
            }

            public void OptIn() => OptInCount++;

            public void OptOut() => OptOutCount++;

            public bool HasTag(string key) => Tags.ContainsKey(key);

            public void RaiseClick(IDictionary<string, object> additionalData) =>
                _clicked?.Invoke(additionalData);

            public void Dispose() => DisposeCount++;
        }
    }
}
