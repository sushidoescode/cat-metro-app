// Only unavailable engine/native boundaries are stubbed. Production adapters and config parse run.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityEngine
{
    public enum RuntimePlatform { Android, IPhonePlayer, OSXEditor }
    public static class Application { public static RuntimePlatform platform; }
    public class TextAsset { public string text; }
    public static class Resources { public static T Load<T>(string path) => throw new Exception("unexpected Resources read"); }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            var value = Activator.CreateInstance(typeof(T), true);
            using (var document = System.Text.Json.JsonDocument.Parse(json))
                foreach (var field in typeof(T).GetFields())
                    if (document.RootElement.TryGetProperty(field.Name, out var property))
                        field.SetValue(value, property.GetString());
            return (T)value;
        }
    }
}
namespace CatMetro.Services.Ads { public interface IRewardedAdProvider { } }
namespace OneSignalSDK.Notifications.Models
{
    public enum NotificationPermission { NotDetermined, Denied, Authorized, Provisional, Ephemeral }
    public sealed class Notification { public IDictionary<string, object> AdditionalData; }
}
namespace OneSignalSDK.Notifications
{
    public sealed class NotificationClickEventArgs : EventArgs { public Models.Notification Notification; }
}
namespace OneSignalSDK
{
    public static class OneSignal
    {
        public static int NativeCalls;
        public static void Initialize(string appId) { NativeCalls++; }
        public static NotificationsBoundary Notifications { get { NativeCalls++; return new NotificationsBoundary(); } }
        public static UserBoundary User { get { NativeCalls++; return new UserBoundary(); } }
    }
    public sealed class NotificationsBoundary
    {
        public bool Permission => true;
        public Notifications.Models.NotificationPermission PermissionNative => Notifications.Models.NotificationPermission.Authorized;
        public bool CanRequestPermission => true;
        public event EventHandler<Notifications.NotificationClickEventArgs> Clicked { add { } remove { } }
        public Task<bool> RequestPermissionAsync(bool fallback) => Task.FromResult(true);
    }
    public sealed class UserBoundary
    {
        public PushBoundary PushSubscription => new PushBoundary();
        public void AddTag(string key, string value) { }
        public void AddTags(Dictionary<string, string> tags) { }
        public void RemoveTag(string key) { }
    }
    public sealed class PushBoundary
    {
        public string Id => "fixture";
        public void OptIn() { }
        public void OptOut() { }
    }
}
