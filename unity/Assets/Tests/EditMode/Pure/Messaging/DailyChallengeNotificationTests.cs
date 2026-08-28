using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using CatMetro.Services;

namespace CatMetro.Tests.Messaging
{
    public sealed class DailyChallengeNotificationTests
    {
        [Test]
        public void Create_BuildsTheRecurringJourneyPayload()
        {
            var message = DailyChallengeNotification.Create(DailyReminderSlot.Morning);

            Assert.That(message.NotificationId, Is.EqualTo("daily-ready"));
            Assert.That(message.Title, Is.EqualTo("Today's Line is ready"));
            Assert.That(message.Body,
                Is.EqualTo("A fresh little route is waiting when you feel like playing."));
            Assert.That(message.DeepLink, Is.EqualTo("catmetro://daily"));
            Assert.That(message.Route, Is.EqualTo(MessagingRoute.Daily));
            Assert.That(message.ChannelId, Is.EqualTo("daily"));
            Assert.That(message.Slot.TagValue, Is.EqualTo("morning"));
        }

        [Test]
        public void ReminderSlot_UsesValueSemanticsAndFailsClosedToMorning()
        {
            Assert.That(DailyReminderSlot.FromTagValue("morning"), Is.EqualTo(DailyReminderSlot.Morning));
            Assert.That(DailyReminderSlot.FromTagValue("afternoon"), Is.EqualTo(DailyReminderSlot.Afternoon));
            Assert.That(DailyReminderSlot.FromTagValue("evening"), Is.EqualTo(DailyReminderSlot.Evening));
            Assert.That(DailyReminderSlot.FromTagValue("unknown"), Is.EqualTo(DailyReminderSlot.Morning));
            Assert.That(DailyReminderSlot.FromTagValue(null), Is.EqualTo(DailyReminderSlot.Morning));
        }

        [Test]
        public async Task MessagingBoundary_ExposesRecurringPayloadAndEveryMember()
        {
            using (var messaging = new FakeMessaging())
            {
                var message = DailyChallengeNotification.Create(DailyReminderSlot.Evening);
                messaging.Schedule(message);

                Assert.That(messaging.Scheduled.Slot, Is.EqualTo(DailyReminderSlot.Evening));
                Assert.That(messaging.Scheduled.Body,
                    Is.EqualTo("A fresh little route is waiting when you feel like playing."));
                Assert.That(messaging.Scheduled.Route, Is.EqualTo(MessagingRoute.Daily));
                Assert.That(messaging.Scheduled.ChannelId, Is.EqualTo("daily"));

                var permission = await messaging.PromptAsync(false, CancellationToken.None);
                Assert.That(permission, Is.EqualTo(MessagingPermission.Authorized));
            }
        }

        private sealed class FakeMessaging : IMessaging
        {
            public bool IsAvailable => true;
            public string SubscriptionId => "fake-subscription";
            public MessagingPermission Permission => MessagingPermission.Authorized;
            public bool CanRequestPermission => true;
            public event Action<MessagingRoute> LinkOpened;
            public DailyChallengeNotification Scheduled { get; private set; }

            public Task<MessagingPermission> PromptAsync(bool fallbackToSettings,
                CancellationToken cancellationToken) =>
                Task.FromResult(MessagingPermission.Authorized);

            public void Schedule(DailyChallengeNotification notification) => Scheduled = notification;

            public void Cancel(string notificationId) { }

            public void Dispose() { }

            public void RaiseLinkOpened(MessagingRoute route) => LinkOpened?.Invoke(route);
        }
    }
}
