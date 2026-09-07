using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    public sealed class AudioPreferencesTests
    {
        [TestCase("music")]
        [TestCase("haptics")]
        public void ChannelWritesPreserveVersionFourPayloadAndPopulatedRewindCaps(string channel)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            Assert.That(SaveDefaults.SAVE_VERSION, Is.EqualTo(4));
            store.State.Payload["economy"]["rewindBalance"] = 2;
            store.State.Payload["economy"]["freeRewindDateKey"] = "2026-09-07";
            store.State.Payload["caps"]["sessionCounters"]["rewind_failure"] = 1;
            store.State.Payload["caps"]["counters"]["rewind_failure"] = 2;
            ((JObject)store.State.Payload["settings"])["futureMixerMode"] = "wood";
            var expected = (JObject)store.State.Payload.DeepClone();
            expected["settings"][channel] = false;

            Assert.That(Write(new AudioPreferences(store), channel, false), Is.True);
            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That((int)reloaded.State.Payload["saveVersion"], Is.EqualTo(4));
            Assert.That(JToken.DeepEquals(reloaded.State.Payload, expected), Is.True,
                "a channel opt-out changes only its additive settings key, preserving rewind state and unknown data");
        }

        [TestCase("music")]
        [TestCase("haptics")]
        [TestCase("motion")]
        public void ChannelsPersistIndependentlyWithoutChangingAudioOrFutureKeys(string channel)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            ((JObject)store.State.Payload["settings"])["futureMixerMode"] = "wood";
            var prefs = new AudioPreferences(store);
            Assert.That(Read(prefs, channel), Is.True);
            Assert.That(Write(prefs, channel, false), Is.True);
            var reloaded = SFixtures.Store(root);
            reloaded.Load();
            var saved = new AudioPreferences(reloaded);
            Assert.That(Read(saved, channel), Is.False);
            Assert.That(saved.Enabled, Is.True, "music and motion are independent of the old SFX setting");
            foreach (var other in new[] { "music", "haptics", "motion" })
                if (other != channel) Assert.That(Read(saved, other), Is.True);
            Assert.That((string)reloaded.State.Payload["settings"]["futureMixerMode"], Is.EqualTo("wood"));
            Assert.That(Write(saved, channel, true), Is.True);
            Assert.That(Read(saved, channel), Is.True);
        }

        [TestCase("music")]
        [TestCase("haptics")]
        [TestCase("motion")]
        public void MissingAndMalformedChannelsDefaultOnAndRepairOnWrite(string channel)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            ((JObject)store.State.Payload["settings"]).Remove(channel);
            var prefs = new AudioPreferences(store);
            Assert.That(Read(prefs, channel), Is.True);
            store.State.Payload["settings"][channel] = "false";
            Assert.That(Read(prefs, channel), Is.True);
            store.State.Payload["settings"] = "damaged";
            Assert.That(Write(prefs, channel, false), Is.True);
            Assert.That(Read(prefs, channel), Is.False);
        }

        [TestCase("music")]
        [TestCase("haptics")]
        [TestCase("motion")]
        public void FailedChannelSaveRestoresTheExactAuthoritativePayload(string channel)
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            var original = store.State.Payload;
            fs.FaultPoint = SFixtures.Fault.InReplace;
            var prefs = new AudioPreferences(store);
            Assert.That(Write(prefs, channel, false), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(original));
            Assert.That(Read(prefs, channel), Is.True);
        }

        private static bool Read(AudioPreferences prefs, string channel) => channel == "music"
            ? prefs.MusicEnabled : channel == "haptics" ? prefs.HapticsEnabled : prefs.MotionEnabled;
        private static bool Write(AudioPreferences prefs, string channel, bool value) => channel == "music"
            ? prefs.TrySetMusicEnabled(value) : channel == "haptics"
                ? prefs.TrySetHapticsEnabled(value) : prefs.TrySetMotionEnabled(value);

        [Test]
        public void FreshPreference_DefaultsOn_AndWritesReloadFromCanonicalSetting()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var preferences = new AudioPreferences(store);

            Assert.That(preferences.Enabled, Is.True);
            Assert.That(preferences.TrySetEnabled(false), Is.True);
            Assert.That(preferences.Enabled, Is.False);

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            var reloadedPreferences = new AudioPreferences(reloaded);
            Assert.That(reloadedPreferences.Enabled, Is.False);
            Assert.That((bool)reloaded.State.Payload["settings"]["audio"], Is.False,
                "the preference writes the canonical settings.audio field");

            Assert.That(reloadedPreferences.TrySetEnabled(true), Is.True);
            var enabledReload = SFixtures.Store(root);
            Assert.That(enabledReload.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That(new AudioPreferences(enabledReload).Enabled, Is.True);
        }

        [Test]
        public void MissingOrMalformedSetting_DefaultsOn_AndAWriteRepairsItsContainer()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var settings = (JObject)store.State.Payload["settings"];
            var preferences = new AudioPreferences(store);

            settings.Remove("audio");
            Assert.That(preferences.Enabled, Is.True);

            settings["audio"] = "false";
            Assert.That(preferences.Enabled, Is.True,
                "a string that resembles a boolean is still malformed");

            store.State.Payload["settings"] = "damaged";
            Assert.That(preferences.Enabled, Is.True);
            Assert.That(preferences.TrySetEnabled(false), Is.True);
            Assert.That(store.State.Payload["settings"], Is.TypeOf<JObject>());
            Assert.That(preferences.Enabled, Is.False);
        }

        [Test]
        public void Mutation_RollsBackExactPayloadOnFailure_AndPreservesUnknownData()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            var original = store.State.Payload;
            original["futureAudioExperiment"] = new JObject { ["kept"] = true };
            ((JObject)original["settings"])["futureMixerMode"] = "wood";
            fs.FaultPoint = SFixtures.Fault.InReplace;
            var preferences = new AudioPreferences(store);

            Assert.DoesNotThrow(() => Assert.That(preferences.TrySetEnabled(false), Is.False));
            Assert.That(ReferenceEquals(store.State.Payload, original), Is.True,
                "a failed commit must restore the exact authoritative payload");
            Assert.That(preferences.Enabled, Is.True);
            Assert.That((bool)store.State.Payload["futureAudioExperiment"]["kept"], Is.True);
            Assert.That((string)store.State.Payload["settings"]["futureMixerMode"],
                Is.EqualTo("wood"));

            fs.FaultPoint = SFixtures.Fault.None;
            Assert.That(preferences.TrySetEnabled(false), Is.True);
            Assert.That((bool)store.State.Payload["futureAudioExperiment"]["kept"], Is.True,
                "a successful mutation must preserve unknown root keys");
            Assert.That((string)store.State.Payload["settings"]["futureMixerMode"],
                Is.EqualTo("wood"), "a successful mutation must preserve unknown settings");
        }
    }
}
