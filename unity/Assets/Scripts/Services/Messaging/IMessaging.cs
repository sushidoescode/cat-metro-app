using System;
using System.Globalization;

namespace CatMetro.Services
{
    // Provider-neutral boundary. Platform adapters own the SDK-specific scheduling details.
    public interface IMessaging
    {
        void Schedule(DailyChallengeNotification notification);
        void Cancel(string notificationId);
    }

    // Unix timestamps are expressed in seconds so platform adapters can translate them without
    // importing their SDKs into the pure services assembly.
    public sealed class DailyChallengeNotification
    {
        public string NotificationId { get; }
        public string TemplateId { get; }
        public string Variant { get; }
        public string Title { get; }
        public string Body { get; }
        public string DeepLink { get; }
        public string ChannelId { get; }
        public string DateKey { get; }
        public long DeliverAtUtc { get; }
        public long ExpiresAtUtc { get; }
        public string CollapseKey { get; }

        private DailyChallengeNotification(string dateKey, long deliverAtUtc, long expiresAtUtc)
        {
            NotificationId = "daily-ready:" + dateKey;
            TemplateId = "daily_challenge";
            Variant = "A";
            Title = "Today's Line is ready";
            Body = "Same map for everyone. One minute to set your score.";
            DeepLink = "catmetro://daily";
            ChannelId = "daily";
            DateKey = dateKey;
            DeliverAtUtc = deliverAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            CollapseKey = NotificationId;
        }

        public static DailyChallengeNotification Create(string dateKey, long deliverAtUtc, long expiresAtUtc)
        {
            if (!IsUtcDateKey(dateKey))
                throw new ArgumentException("dateKey must be a real UTC date in yyyy-MM-dd form", nameof(dateKey));
            if (deliverAtUtc < 0)
                throw new ArgumentException("deliverAtUtc must be nonnegative", nameof(deliverAtUtc));
            if (expiresAtUtc < 0)
                throw new ArgumentException("expiresAtUtc must be nonnegative", nameof(expiresAtUtc));
            if (expiresAtUtc <= deliverAtUtc)
                throw new ArgumentException("expiresAtUtc must be after deliverAtUtc", nameof(expiresAtUtc));

            return new DailyChallengeNotification(dateKey, deliverAtUtc, expiresAtUtc);
        }

        private static bool IsUtcDateKey(string dateKey) =>
            dateKey != null && dateKey.Length == 10 && DateTime.TryParseExact(
                dateKey,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _);
    }
}
