using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    public sealed class AudioPreferencesTests
    {
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
