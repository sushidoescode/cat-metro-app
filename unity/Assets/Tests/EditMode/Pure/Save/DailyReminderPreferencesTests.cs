using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;
using CatMetro.Services;

namespace CatMetro.Tests.Save
{
    public sealed class DailyReminderPreferencesTests
    {
        [Test]
        public void FreshPreferences_DefaultOff_EarnsExactlyOnePromptAfterFirstCompletion()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var preferences = new DailyReminderPreferences(store);

            Assert.That(preferences.Enabled, Is.False);
            Assert.That(preferences.Slot, Is.EqualTo(DailyReminderSlot.Morning));
            Assert.That(preferences.CanOfferPrompt(0), Is.False);
            Assert.That(preferences.CanOfferPrompt(1), Is.True);
            Assert.That(preferences.TryMarkPromptSeen(), Is.True);
            Assert.That(preferences.CanOfferPrompt(9), Is.False,
                "marking the displayed prompt prevents a repeated automatic prompt");

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That(new DailyReminderPreferences(reloaded).CanOfferPrompt(9), Is.False);
        }

        [Test]
        public void InvalidOrMissingSettings_FailClosedWithoutInferringConsentOrRepeatingPrompt()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var settings = (JObject)store.State.Payload["settings"];
            settings["dailyReminderEnabled"] = "true";
            settings["dailyReminderPromptSeen"] = "false";
            settings["dailyReminderSlot"] = "midnight";

            var preferences = new DailyReminderPreferences(store);

            Assert.That(preferences.Enabled, Is.False, "a non-boolean must not infer consent");
            Assert.That(preferences.PromptSeen, Is.True,
                "a malformed current-version prompt state must not create a repeated prompt");
            Assert.That(preferences.Slot, Is.EqualTo(DailyReminderSlot.Morning),
                "an invalid slot must not escape the declared presets");

            settings.Remove("dailyReminderEnabled");
            settings.Remove("dailyReminderSlot");
            Assert.That(preferences.Enabled, Is.False);
            Assert.That(preferences.Slot, Is.EqualTo(DailyReminderSlot.Morning));
            settings.Remove("dailyReminderPromptSeen");
            Assert.That(preferences.PromptSeen, Is.True,
                "a missing current-version prompt state must not create a repeated prompt");
        }

        [Test]
        public void Mutations_RollBackExactAuthoritativePayloadWhenCommitThrows()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            var original = store.State.Payload;
            original["futureReminderExperiment"] = new JObject { ["kept"] = true };
            fs.FaultPoint = SFixtures.Fault.InReplace;
            var preferences = new DailyReminderPreferences(store);

            Assert.DoesNotThrow(() => Assert.That(preferences.TrySetEnabled(true), Is.False));
            Assert.That(ReferenceEquals(store.State.Payload, original), Is.True,
                "a failed commit must not leave non-atomic state installed");
            Assert.That((bool)store.State.Payload["futureReminderExperiment"]["kept"], Is.True,
                "a preference mutation must not delete unknown keys");
            Assert.That(preferences.Enabled, Is.False);

            fs.FaultPoint = SFixtures.Fault.None;
            Assert.That(preferences.TrySetSlot(DailyReminderSlot.Evening), Is.True);
            Assert.That((bool)store.State.Payload["futureReminderExperiment"]["kept"], Is.True,
                "a successful preference mutation must preserve unknown keys too");
        }

        [Test]
        public void V1Reload_MigratesToV3WithDefaultOffAndKeepsUnknownKeys()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var v1 = SaveDefaults.FreshPayload();
            v1["saveVersion"] = 1;
            ((JObject)v1["settings"]).Remove("dailyReminderEnabled");
            ((JObject)v1["settings"]).Remove("dailyReminderPromptSeen");
            ((JObject)v1["settings"]).Remove("dailyReminderSlot");
            v1["futureReminderExperiment"] = new JObject { ["kept"] = true };
            SFixtures.WriteRaw(store.SavePath, SFixtures.FileWithVersion(1, v1));

            Assert.That(store.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That((int)store.State.Payload["saveVersion"], Is.EqualTo(3));
            var preferences = new DailyReminderPreferences(store);
            Assert.That(preferences.Enabled, Is.False);
            Assert.That(preferences.PromptSeen, Is.False,
                "the single v1->v2 migration receives an unseen prompt state");
            Assert.That(preferences.Slot, Is.EqualTo(DailyReminderSlot.Morning));
            Assert.That((bool)store.State.Payload["futureReminderExperiment"]["kept"], Is.True);
        }
    }
}
