using System;
using System.Linq;
using NUnit.Framework;
using CatMetro.Services;

namespace CatMetro.Tests.Messaging
{
    public sealed class DailyChallengeNotificationTests
    {
        [Test]
        public void Create_BuildsThePinnedProviderNeutralPayload()
        {
            var notification = DailyChallengeNotification.Create("2026-08-26", 1766188800, 1766275200);
            var sameDate = DailyChallengeNotification.Create("2026-08-26", 1766188800, 1766275200);
            var sameDateDifferentWindow = DailyChallengeNotification.Create("2026-08-26", 0, 1);
            var nextDate = DailyChallengeNotification.Create("2026-08-27", 1766275200, 1766361600);

            Assert.That(notification.TemplateId, Is.EqualTo("daily_challenge"));
            Assert.That(notification.Variant, Is.EqualTo("A"));
            Assert.That(notification.Title, Is.EqualTo("Today's Line is ready"));
            Assert.That(notification.Body, Is.EqualTo("Same map for everyone. One minute to set your score."));
            Assert.That(notification.DeepLink, Is.EqualTo("catmetro://daily"));
            Assert.That(notification.ChannelId, Is.EqualTo("daily"));
            Assert.That(notification.DateKey, Is.EqualTo("2026-08-26"));
            Assert.That(notification.DeliverAtUtc, Is.EqualTo(1766188800));
            Assert.That(notification.ExpiresAtUtc, Is.EqualTo(1766275200));
            Assert.That(notification.NotificationId, Is.EqualTo("daily-ready:2026-08-26"));
            Assert.That(notification.CollapseKey, Is.EqualTo("daily-ready:2026-08-26"));
            Assert.That(notification.NotificationId, Is.EqualTo(sameDate.NotificationId));
            Assert.That(notification.NotificationId, Is.EqualTo(sameDateDifferentWindow.NotificationId));
            Assert.That(notification.CollapseKey, Is.EqualTo(sameDate.CollapseKey));
            Assert.That(notification.CollapseKey, Is.EqualTo(sameDateDifferentWindow.CollapseKey));
            Assert.That(notification.NotificationId, Is.Not.EqualTo(nextDate.NotificationId));
            Assert.That(notification.CollapseKey, Is.Not.EqualTo(nextDate.CollapseKey));
            Assert.That(typeof(DailyChallengeNotification).IsSealed, Is.True);
            Assert.That(typeof(DailyChallengeNotification).GetProperties().All(p => p.SetMethod == null), Is.True);
            Assert.That(typeof(IMessaging).GetMethod(nameof(IMessaging.Schedule),
                new[] { typeof(DailyChallengeNotification) }), Is.Not.Null);
            Assert.That(typeof(IMessaging).GetMethod(nameof(IMessaging.Cancel), new[] { typeof(string) }), Is.Not.Null);
        }

        [TestCase("2026-02-29")]
        [TestCase("2026-8-26")]
        [TestCase("2026/08/26")]
        [TestCase("")]
        [TestCase(null)]
        public void Create_RejectsAKeyThatIsNotARealUtcDate(string dateKey)
        {
            Assert.Throws<ArgumentException>(() =>
                DailyChallengeNotification.Create(dateKey, 0, 1));
        }

        [TestCase(-1, 1)]
        [TestCase(0, -1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        public void Create_RejectsInvalidDeliveryWindow(long deliverAtUtc, long expiresAtUtc)
        {
            Assert.Throws<ArgumentException>(() =>
                DailyChallengeNotification.Create("2026-08-26", deliverAtUtc, expiresAtUtc));
        }
    }
}
