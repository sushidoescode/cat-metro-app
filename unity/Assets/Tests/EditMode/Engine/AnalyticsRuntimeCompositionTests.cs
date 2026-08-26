using System.Text;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Application.Save;
using CatMetro.Bootstrap;
using CatMetro.Integrations.Analytics;
using CatMetro.Services;
using CatMetro.Tests.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Engine
{
    public sealed class AnalyticsRuntimeCompositionTests
    {
        private sealed class GuardedStorageRoot : IStorageRoot
        {
            private readonly string _directory;
            public bool AllowAccess = true;
            public GuardedStorageRoot(string directory) { _directory = directory; }
            public string SaveDirectory => AllowAccess ? _directory
                : throw new System.InvalidOperationException("storage root accessed after setup");
            public string CacheDirectory => SaveDirectory;
        }

        [Test]
        public void ProfileArtifact_PersistsOnlyItsFourAllowlistedFields_UsingCachedPaths()
        {
            using var temp = new SFixtures.TempRoot();
            var root = new GuardedStorageRoot(temp.SaveDirectory);
            var fs = new SFixtures.RecordingFs();
            var artifact = new AnalyticsProfileFile(root, fs);
            root.AllowAccess = false;
            var profile = artifact.Load();
            profile[AnalyticsInstallIdentity.ProfileKey] =
                "00112233445566778899aabbccddeeff";
            profile["createdAtUtc"] = 1_800_000_000L;
            profile["lastSeenAtUtc"] = 1_800_000_010L;
            profile["sessionCount"] = 3;
            profile["email"] = "must-not-persist";
            profile["deviceModel"] = "must-not-persist";

            Assert.That(artifact.TryWrite(profile), Is.True);
            root.AllowAccess = true;
            var reloaded = new AnalyticsProfileFile(root, fs).Load();

            Assert.That(reloaded.Properties().Select(x => x.Name), Is.EquivalentTo(new[]
            {
                AnalyticsInstallIdentity.ProfileKey,
                "createdAtUtc", "lastSeenAtUtc", "sessionCount",
            }));
            Assert.That((string)reloaded[AnalyticsInstallIdentity.ProfileKey],
                Is.EqualTo("00112233445566778899aabbccddeeff"));
            Assert.That((int)reloaded["sessionCount"], Is.EqualTo(3));
        }

        [Test]
        public void ProfileArtifact_UnusableMainFallsBackToTheValidAtomicBackup()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var artifact = new AnalyticsProfileFile(root, fs);
            var profile = artifact.Load();
            profile[AnalyticsInstallIdentity.ProfileKey] =
                "00112233445566778899aabbccddeeff";
            profile["createdAtUtc"] = 1_800_000_000L;
            Assert.That(artifact.TryWrite(profile), Is.True);
            profile["lastSeenAtUtc"] = 1_800_000_010L;
            Assert.That(artifact.TryWrite(profile), Is.True,
                "second replace creates the valid previous-state backup");
            foreach (string unusableBody in new[]
            {
                "{\"createdAtUtc\":1,\"createdAtUtc\":2}",
                "{\"analyticsInstallId\":\"bad\",\"createdAtUtc\":1800000000}",
                "{\"analyticsInstallId\":\"00112233445566778899aabbccddeeff\","
                    + "\"createdAtUtc\":0}",
            })
            {
                var unusable = SaveHeader.Write(AnalyticsProfileFile.Magic, 1,
                    AnalyticsProfileFile.Version, Encoding.UTF8.GetBytes(unusableBody));
                SFixtures.WriteRaw(artifact.ProfilePath, unusable);

                var recovered = new AnalyticsProfileFile(root, fs).Load();

                Assert.That((string)recovered[AnalyticsInstallIdentity.ProfileKey],
                    Is.EqualTo("00112233445566778899aabbccddeeff"),
                    "a syntactically or semantically unusable main must not bypass backup: "
                    + unusableBody);
            }
        }

        [Test]
        public void ProfileArtifact_RejectsUnsupportedHeaderFormat()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var artifact = new AnalyticsProfileFile(root, fs);
            var profile = artifact.Load();
            profile[AnalyticsInstallIdentity.ProfileKey] =
                "00112233445566778899aabbccddeeff";
            profile["createdAtUtc"] = 1_800_000_000L;
            Assert.That(artifact.TryWrite(profile), Is.True);
            var parsed = SaveHeader.TryParse(SFixtures.RawFile(artifact.ProfilePath),
                AnalyticsProfileFile.Magic, out var payload);
            Assert.That(parsed, Is.Not.Null);
            SFixtures.WriteRaw(artifact.ProfilePath, SaveHeader.Write(
                AnalyticsProfileFile.Magic, 2, AnalyticsProfileFile.Version, payload));

            var rejected = new AnalyticsProfileFile(root, fs).Load();

            Assert.That(rejected[AnalyticsInstallIdentity.ProfileKey], Is.Null);
            Assert.That((long)rejected["createdAtUtc"], Is.Zero);
        }

        [Test]
        public void FalseRemoteKillSwitch_DiscardsThePersistedOuterQueueArtifact()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var http = new PostHogTransportTests.RecordingHttpClient();
            var config = AnalyticsTransportConfig.Parse(Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"enabled\":true,\"projectToken\":\"phc_public\","
                + "\"host\":\"https://eu.i.posthog.com\","
                + "\"remoteKillSwitchFlag\":\"cat-metro-analytics-enabled\"}")).Value;
            var transport = new PostHogAnalyticsTransport(config,
                "00112233445566778899aabbccddeeff", http, () => 0d);
            var queue = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), transport);
            queue.Log(new AnalyticsEvent("app_open", new JObject
                { ["session_id"] = "before-disable" }));
            Assert.That(queue.QueuedEventCount, Is.EqualTo(1));
            using var runtime = new GameAnalyticsRuntime(queue, transport);

            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":false}}");

            Assert.That(queue.QueuedEventCount, Is.Zero);
            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null);
            Assert.That(reloaded.QueuedEventCount, Is.Zero,
                "the remote kill switch must purge disk, not only memory");
        }

        [Test]
        public void ForegroundRefresh_HoldsQueuedEventsUntilTheNewFlagValueArrives()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var http = new PostHogTransportTests.RecordingHttpClient();
            var config = AnalyticsTransportConfig.Parse(Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"enabled\":true,\"projectToken\":\"phc_public\","
                + "\"host\":\"https://eu.i.posthog.com\","
                + "\"remoteKillSwitchFlag\":\"cat-metro-analytics-enabled\"}")).Value;
            var transport = new PostHogAnalyticsTransport(config,
                "00112233445566778899aabbccddeeff", http, () => 0d);
            var queue = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), transport);
            using var runtime = new GameAnalyticsRuntime(queue, transport);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            runtime.OnBackground();
            queue.Log(Events.AppOpen("00112233445566778899aabbccddeeff", "1.0", 0,
                "development"));

            runtime.OnForeground();

            Assert.That(http.Requests.All(x => !x.url.EndsWith("/batch")), Is.True);
            Assert.That(queue.QueuedEventCount, Is.EqualTo(1));
            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Unknown));
        }
    }
}
