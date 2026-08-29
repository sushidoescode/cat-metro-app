using System;
using System.Threading;
using System.Threading.Tasks;

namespace CatMetro.Services
{
    public enum MessagingPermission
    {
        Unknown,
        Denied,
        Authorized
    }

    public enum MessagingRoute
    {
        Daily
    }

    public sealed class DailyReminderSlot : IEquatable<DailyReminderSlot>
    {
        public static readonly DailyReminderSlot Morning = new DailyReminderSlot("morning");
        public static readonly DailyReminderSlot Afternoon = new DailyReminderSlot("afternoon");
        public static readonly DailyReminderSlot Evening = new DailyReminderSlot("evening");

        public string TagValue { get; }

        private DailyReminderSlot(string tagValue)
        {
            TagValue = tagValue;
        }

        public static DailyReminderSlot FromTagValue(string tagValue)
        {
            switch (tagValue)
            {
                case "afternoon": return Afternoon;
                case "evening": return Evening;
                case "morning": return Morning;
                default: return Morning;
            }
        }

        public bool Equals(DailyReminderSlot other) =>
            other != null && string.Equals(TagValue, other.TagValue, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as DailyReminderSlot);

        public override int GetHashCode() => TagValue.GetHashCode();
    }

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

    public sealed class DailyChallengeNotification
    {
        public string NotificationId { get; }
        public string Title { get; }
        public string Body { get; }
        public string DeepLink { get; }
        public MessagingRoute Route { get; }
        public string ChannelId { get; }
        public DailyReminderSlot Slot { get; }

        private DailyChallengeNotification(DailyReminderSlot slot)
        {
            NotificationId = "daily-ready";
            Title = "Today's Line is ready";
            Body = "A fresh little route is waiting when you feel like playing.";
            DeepLink = "catmetro://daily";
            Route = MessagingRoute.Daily;
            ChannelId = "daily";
            Slot = slot;
        }

        public static DailyChallengeNotification Create(DailyReminderSlot slot)
        {
            if (slot == null)
                throw new ArgumentNullException(nameof(slot));
            return new DailyChallengeNotification(slot);
        }
    }
}
