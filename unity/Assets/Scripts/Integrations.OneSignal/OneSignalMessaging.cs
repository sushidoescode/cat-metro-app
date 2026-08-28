using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Services;
using OneSignalSDK;
using OneSignalSDK.Notifications;
using OneSignalSDK.Notifications.Models;
using OneSignalClient = OneSignalSDK.OneSignal;

namespace CatMetro.Integrations.OneSignal
{
    internal enum NativeMessagingPermission
    {
        NotDetermined,
        Denied,
        Authorized,
        Provisional,
        Ephemeral
    }

    internal interface IOneSignalBridge : IDisposable
    {
        bool PermissionGranted { get; }
        NativeMessagingPermission NativePermission { get; }
        bool CanRequestPermission { get; }
        string SubscriptionId { get; }
        event Action<IDictionary<string, object>> NotificationClicked;
        void Initialize(string appId);
        Task<bool> RequestPermissionAsync(bool fallbackToSettings);
        void AddTag(string key, string value);
        void AddTags(Dictionary<string, string> tags);
        void RemoveTag(string key);
        void OptIn();
        void OptOut();
    }

    public sealed class OneSignalMessaging : IMessaging
    {
        private const string DailyNotificationId = "daily-ready";
        private const string DailyRoute = "daily";
        private const string OptInTag = "daily_opt_in";
        private const string SlotTag = "daily_reminder_slot";

        private readonly IOneSignalBridge _bridge;
        private readonly Action<IDictionary<string, object>> _notificationClicked;
        private bool _listenerAttached;
        private bool _disposed;

        public OneSignalMessaging() : this(new OneSignalSdkBridge()) { }

        internal OneSignalMessaging(IOneSignalBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _notificationClicked = HandleNotificationClicked;
        }

        public bool IsAvailable { get; private set; }

        public string SubscriptionId => IsAvailable ? _bridge.SubscriptionId : null;

        public MessagingPermission Permission => IsAvailable
            ? MapPermission(_bridge.PermissionGranted, _bridge.NativePermission)
            : MessagingPermission.Unknown;

        public bool CanRequestPermission => IsAvailable && _bridge.CanRequestPermission;

        public event Action<MessagingRoute> LinkOpened;

        public void Initialize(string appId)
        {
            if (_disposed)
                return;

            DetachListener();
            IsAvailable = false;
            if (!TryNormalizeAppId(appId, out var normalizedAppId))
                return;

            _bridge.NotificationClicked += _notificationClicked;
            _listenerAttached = true;
            try
            {
                _bridge.Initialize(normalizedAppId);
                IsAvailable = true;
            }
            catch (Exception)
            {
                DetachListener();
            }
        }

        public async Task<MessagingPermission> PromptAsync(bool fallbackToSettings,
            CancellationToken cancellationToken)
        {
            if (!IsAvailable)
                return MessagingPermission.Unknown;

            cancellationToken.ThrowIfCancellationRequested();
            var granted = await _bridge.RequestPermissionAsync(fallbackToSettings);
            cancellationToken.ThrowIfCancellationRequested();
            return MapPermission(granted || _bridge.PermissionGranted,
                _bridge.NativePermission);
        }

        public void Schedule(DailyChallengeNotification notification)
        {
            if (!IsAvailable || notification == null
                || !string.Equals(notification.NotificationId, DailyNotificationId,
                    StringComparison.Ordinal)
                || Permission != MessagingPermission.Authorized)
                return;

            _bridge.OptIn();
            _bridge.AddTags(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OptInTag] = "true",
                [SlotTag] = notification.Slot.TagValue
            });
        }

        public void Cancel(string notificationId)
        {
            if (!IsAvailable || !string.Equals(notificationId, DailyNotificationId,
                    StringComparison.Ordinal))
                return;

            _bridge.AddTag(OptInTag, "false");
            _bridge.RemoveTag(SlotTag);
            _bridge.OptOut();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DetachListener();
            IsAvailable = false;
            _disposed = true;
            _bridge.Dispose();
        }

        private void HandleNotificationClicked(IDictionary<string, object> additionalData)
        {
            if (additionalData == null
                || !additionalData.TryGetValue("route", out var value)
                || !(value is string route)
                || !string.Equals(route, DailyRoute, StringComparison.Ordinal))
                return;

            LinkOpened?.Invoke(MessagingRoute.Daily);
        }

        private void DetachListener()
        {
            if (!_listenerAttached)
                return;

            _bridge.NotificationClicked -= _notificationClicked;
            _listenerAttached = false;
        }

        private static bool TryNormalizeAppId(string appId, out string normalizedAppId)
        {
            normalizedAppId = string.Empty;
            if (string.IsNullOrWhiteSpace(appId))
                return false;

            var candidate = appId.Trim();
            if (!Guid.TryParse(candidate, out _))
                return false;

            normalizedAppId = candidate;
            return true;
        }

        private static MessagingPermission MapPermission(bool granted,
            NativeMessagingPermission nativePermission)
        {
            if (granted)
                return MessagingPermission.Authorized;
            return nativePermission == NativeMessagingPermission.Denied
                ? MessagingPermission.Denied
                : MessagingPermission.Unknown;
        }
    }

    internal sealed class OneSignalSdkBridge : IOneSignalBridge
    {
        private readonly EventHandler<NotificationClickEventArgs> _sdkClicked;
        private bool _sdkListenerAttached;

        public OneSignalSdkBridge()
        {
            _sdkClicked = HandleSdkClick;
        }

        public bool PermissionGranted => OneSignalClient.Notifications.Permission;

        public NativeMessagingPermission NativePermission =>
            MapNativePermission(OneSignalClient.Notifications.PermissionNative);

        public bool CanRequestPermission => OneSignalClient.Notifications.CanRequestPermission;

        public string SubscriptionId => OneSignalClient.User.PushSubscription.Id;

        public event Action<IDictionary<string, object>> NotificationClicked;

        public void Initialize(string appId)
        {
            DetachSdkListener();
            OneSignalClient.Initialize(appId);
            OneSignalClient.Notifications.Clicked += _sdkClicked;
            _sdkListenerAttached = true;
        }

        public Task<bool> RequestPermissionAsync(bool fallbackToSettings) =>
            OneSignalClient.Notifications.RequestPermissionAsync(fallbackToSettings);

        public void AddTag(string key, string value) => OneSignalClient.User.AddTag(key, value);

        public void AddTags(Dictionary<string, string> tags) => OneSignalClient.User.AddTags(tags);

        public void RemoveTag(string key) => OneSignalClient.User.RemoveTag(key);

        public void OptIn() => OneSignalClient.User.PushSubscription.OptIn();

        public void OptOut() => OneSignalClient.User.PushSubscription.OptOut();

        public void Dispose() => DetachSdkListener();

        private void HandleSdkClick(object sender, NotificationClickEventArgs args) =>
            NotificationClicked?.Invoke(args?.Notification?.AdditionalData);

        private void DetachSdkListener()
        {
            if (!_sdkListenerAttached)
                return;

            OneSignalClient.Notifications.Clicked -= _sdkClicked;
            _sdkListenerAttached = false;
        }

        private static NativeMessagingPermission MapNativePermission(
            NotificationPermission permission)
        {
            switch (permission)
            {
                case NotificationPermission.Denied:
                    return NativeMessagingPermission.Denied;
                case NotificationPermission.Authorized:
                    return NativeMessagingPermission.Authorized;
                case NotificationPermission.Provisional:
                    return NativeMessagingPermission.Provisional;
                case NotificationPermission.Ephemeral:
                    return NativeMessagingPermission.Ephemeral;
                default:
                    return NativeMessagingPermission.NotDetermined;
            }
        }
    }
}
