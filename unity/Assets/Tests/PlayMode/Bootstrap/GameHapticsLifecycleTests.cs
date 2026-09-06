using System.Collections;
using CatMetro.Presentation.Haptics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class GameHapticsLifecycleTests
    {
        [UnityTest]
        public IEnumerator DestroyCancelsAndDisposesTheNativeHandle()
        {
            var host = new GameObject("HapticsLifecycle");
            var device = new Device();
            var haptics = host.AddComponent<GameHaptics>();
            haptics.Initialize(device);
            yield return null;
            host.SendMessage("OnApplicationPause", true);
            haptics.PlayButtonTap();
            Assert.That(device.Played, Is.Zero);
            Assert.That(device.Cancelled, Is.True);
            host.SendMessage("OnApplicationPause", false);
            haptics.PlayButtonTap();
            Assert.That(device.Played, Is.EqualTo(1));
            haptics.enabled = false;
            haptics.PlayButtonTap();
            Assert.That(device.Played, Is.EqualTo(1));
            Object.Destroy(host);
            yield return null;
            Assert.That(device.Cancelled, Is.True);
            Assert.That(device.Disposed, Is.True);
        }
        private sealed class Device : IHaptics
        {
            public bool Cancelled, Disposed;
            public int Played;
            public void Play(HapticCue cue) => Played++;
            public void Cancel() => Cancelled = true;
            public void Dispose() => Disposed = true;
        }
    }
}
