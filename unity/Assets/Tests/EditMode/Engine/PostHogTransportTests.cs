using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.Networking;
using CatMetro.Application.Analytics;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Integrations.Analytics;
using CatMetro.Services;

namespace CatMetro.Tests.Engine
{
    public sealed class PostHogTransportTests
    {
        public sealed class RecordingHttpClient : IAnalyticsHttpClient
        {
            public enum SynchronousFailure
            {
                None,
                Refuse,
                Throw,
            }

            public readonly List<UnityWebRequest> Requests = new List<UnityWebRequest>();
            public int AbortCount;
            public SynchronousFailure FailureMode;
            private Action<AnalyticsHttpResponse> _completed;

            public bool IsBusy => _completed != null;

            public bool TrySend(UnityWebRequest request,
                Action<AnalyticsHttpResponse> completed)
            {
                if (request == null || completed == null || IsBusy) return false;
                if (FailureMode == SynchronousFailure.Refuse) return false;
                if (FailureMode == SynchronousFailure.Throw)
                    throw new InvalidOperationException("synthetic synchronous send failure");
                Requests.Add(request);
                _completed = completed;
                return true;
            }

            public void Complete(bool requestSucceeded, long statusCode, string body = "")
            {
                var callback = _completed;
                _completed = null;
                callback?.Invoke(new AnalyticsHttpResponse(requestSucceeded, statusCode, body));
            }

            public void Abort()
            {
                AbortCount++;
                _completed = null;
            }

            public void Dispose()
            {
                _completed = null;
                foreach (var request in Requests)
                    try { request.Dispose(); } catch { }
            }
        }

        private static AnalyticsTransportConfig Config()
        {
            var bytes = Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"enabled\":true,\"projectToken\":\"phc_public\","
                + "\"host\":\"https://eu.i.posthog.com\","
                + "\"remoteKillSwitchFlag\":\"cat-metro-analytics-enabled\"}");
            return AnalyticsTransportConfig.Parse(bytes).Value;
        }

        private static QueuedAnalyticsEvent Item(string id, long capturedAt, int ordinal = 0,
            JObject properties = null) => new QueuedAnalyticsEvent(id, ordinal, "level_started",
                properties ?? new JObject
                {
                    ["level_id"] = "L001",
                    ["mode"] = "campaign",
                    ["attempt"] = 1,
                    ["difficulty_target"] = "1.25",
                }, capturedAt, 100);

        private static string Body(UnityWebRequest request) =>
            Encoding.UTF8.GetString(request.uploadHandler.data);

        private static JObject ParsedBody(UnityWebRequest request)
        {
            using var text = new StringReader(Body(request));
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }

        [Test]
        public void Initialize_SendsOnlyThePersonlessGeoIpDisabledFlagRequest_AndFailsClosed()
        {
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => 0d);

            transport.Initialize();

            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Unknown));
            Assert.That(transport.AnonymousId, Is.EqualTo("a18f00112233445566778899aabbccdd"));
            Assert.That(http.Requests.Count, Is.EqualTo(1));
            var request = http.Requests.Single();
            Assert.That(request.url, Is.EqualTo("https://eu.i.posthog.com/flags/?v=2"));
            var root = ParsedBody(request);
            Assert.That(root.Properties().Select(x => x.Name), Is.EquivalentTo(new[]
            {
                "api_key", "distinct_id", "geoip_disable", "flag_keys_to_evaluate",
            }));
            Assert.That((string)root["api_key"], Is.EqualTo("phc_public"));
            Assert.That((string)root["distinct_id"],
                Is.EqualTo("a18f00112233445566778899aabbccdd"));
            Assert.That((bool)root["geoip_disable"], Is.True);
            Assert.That(root["flag_keys_to_evaluate"].Values<string>(),
                Is.EqualTo(new[] { "cat-metro-analytics-enabled" }));
            Assert.That(root["person_properties"], Is.Null);
            Assert.That(root["$anon_distinct_id"], Is.Null);

            bool completionCalled = false;
            Assert.That(transport.TryDeliver(new[] { Item("a", 1_800_000_000_000L) },
                _ => completionCalled = true), Is.False);
            Assert.That(completionCalled, Is.False);
            Assert.That(http.Requests.Count, Is.EqualTo(1),
                "an unresolved flag must never start a batch request");
        }

        [Test]
        public void FalseFlag_DisablesAndNeverStartsAQueuedBatch()
        {
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => 0d);
            var states = new List<AnalyticsRemoteState>();
            transport.RemoteStateChanged += states.Add;
            transport.Initialize();

            http.Complete(true, 200,
                "{\"flags\":{\"cat-metro-analytics-enabled\":{\"enabled\":false}}}");

            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Disabled));
            Assert.That(states, Is.EqualTo(new[] { AnalyticsRemoteState.Disabled }));
            Assert.That(transport.TryDeliver(new[] { Item("held", 1L) }, _ => { }), Is.False);
            Assert.That(http.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnabledFlag_BatchIsAcknowledgedOnlyAfterHttp2xx()
        {
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => 0d);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            AnalyticsDeliveryResult? result = null;

            bool started = transport.TryDeliver(new[]
            {
                Item("0123456789abcdef", 1_800_000_000_000L),
            }, value => result = value);

            Assert.That(started, Is.True);
            Assert.That(result, Is.Null, "request start is not server acceptance");
            Assert.That(http.Requests.Count, Is.EqualTo(2));
            Assert.That(http.Requests[1].url, Is.EqualTo("https://eu.i.posthog.com/batch"));

            http.Complete(true, 202, "{\"status\":\"Ok\"}");

            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.ServerAccepted, Is.True);
        }

        [Test]
        public void BatchArtifact_IsExactAllowlistedPersonlessAndStableAcrossRetry()
        {
            var properties = new JObject
            {
                ["level_id"] = "L001",
                ["mode"] = "campaign",
                ["attempt"] = 1,
                ["difficulty_target"] = "1.25",
                ["email"] = "must-not-leave",
                ["$device_model"] = "must-not-leave",
                ["$screen_width"] = 1344,
            };
            var batch = new[]
            {
                Item("0123456789abcdef", 1_800_000_000_123L, 7, properties),
            };

            using var first = PostHogWireProtocol.CreateBatchRequest(Config(),
                "a18f00112233445566778899aabbccdd", batch);
            using var retry = PostHogWireProtocol.CreateBatchRequest(Config(),
                "a18f00112233445566778899aabbccdd", batch);
            Assert.That(first, Is.Not.Null);
            Assert.That(retry, Is.Not.Null);
            var root = ParsedBody(first);
            var retried = ParsedBody(retry);

            Assert.That(root.Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "api_key", "batch" }));
            Assert.That(root["sent_at"], Is.Null,
                "request construction time must not alter PostHog's deduplication tuple");
            Assert.That(Body(retry), Is.EqualTo(Body(first)),
                "the complete retry body must be byte-stable");
            Assert.That((string)root["api_key"], Is.EqualTo("phc_public"));
            var outbound = (JObject)root["batch"][0];
            Assert.That(outbound.Properties().Select(x => x.Name), Is.EquivalentTo(
                new[] { "uuid", "event", "distinct_id", "timestamp", "properties" }));
            Assert.That((string)outbound["event"], Is.EqualTo("level_started"));
            Assert.That((string)outbound["distinct_id"],
                Is.EqualTo("a18f00112233445566778899aabbccdd"));
            Assert.That(DateTimeOffset.Parse((string)outbound["timestamp"])
                .ToUnixTimeMilliseconds(), Is.EqualTo(1_800_000_000_123L));
            string uuid = (string)outbound["uuid"];
            Assert.That(Guid.TryParse(uuid, out _), Is.True);
            Assert.That(uuid[14], Is.EqualTo('7'), "stable event id is a valid UUIDv7");
            Assert.That((string)retried["batch"][0]["uuid"], Is.EqualTo(uuid));

            var sent = (JObject)outbound["properties"];
            Assert.That(sent.Properties().Select(x => x.Name), Is.EquivalentTo(new[]
            {
                "level_id", "mode", "attempt", "difficulty_target", "cm_event_id",
                "$geoip_disable", "$process_person_profile",
            }));
            Assert.That((string)sent["cm_event_id"], Is.EqualTo("0123456789abcdef"));
            Assert.That((bool)sent["$geoip_disable"], Is.True);
            Assert.That((bool)sent["$process_person_profile"], Is.False);
            Assert.That(sent["email"], Is.Null);
            Assert.That(sent["$device_model"], Is.Null);
        }

        [Test]
        public void LegacyRecordWithoutCaptureTime_UsesStableUnixEpochAcrossRetry()
        {
            var batch = new[]
            {
                Item("0123456789abcdef", 0L, 7),
            };

            using var first = PostHogWireProtocol.CreateBatchRequest(Config(),
                "a18f00112233445566778899aabbccdd", batch);
            using var retry = PostHogWireProtocol.CreateBatchRequest(Config(),
                "a18f00112233445566778899aabbccdd", batch);

            var outbound = ParsedBody(first)["batch"][0];
            Assert.That(DateTimeOffset.Parse((string)outbound["timestamp"])
                .ToUnixTimeMilliseconds(), Is.Zero);
            Assert.That(Body(retry), Is.EqualTo(Body(first)),
                "a legacy record must not acquire a new identity on every attempt");
        }

        [Test]
        public void UnknownOrMalformedTaxonomyRecord_NeverCreatesARequest()
        {
            var unknown = new QueuedAnalyticsEvent("0123456789abcdef", 0,
                "future_event", new JObject { ["email"] = "no" }, 1L, 50);
            var missingRequired = Item("fedcba9876543210", 1L, properties: new JObject
            {
                ["level_id"] = "L001",
            });

            Assert.That(PostHogWireProtocol.CreateBatchRequest(Config(),
                "a18f00112233445566778899aabbccdd", new[] { unknown }), Is.Null);
            Assert.That(PostHogWireProtocol.CreateBatchRequest(Config(),
                "a18f00112233445566778899aabbccdd", new[] { missingRequired }), Is.Null);
        }

        [Test]
        public void EveryCurrentlyProducedShape_MatchesItsDeclaredWireFields()
        {
            var cases = new[]
            {
                (Events.FirstOpen("1.0", "mid", "35"),
                    new[] { "app_version", "device_tier", "os_api_level" }),
                (Events.AppOpen("00112233445566778899aabbccddeeff", "1.0", 3,
                    "production"), new[]
                    { "session_id", "app_version", "install_age_days", "build_channel" }),
                (Events.LevelStarted("L005", "campaign", 2, "1.25", "results"),
                    new[] { "level_id", "mode", "attempt", "difficulty_target", "from_screen" }),
                (Events.LevelCompleted("L005", "campaign", 2, 42, 3, true, 900, 3),
                    new[] { "level_id", "mode", "attempt", "duration_s", "switches_used",
                        "perfect", "score", "stars" }),
                (Events.DailyStarted(12345L, "2026-08-26"),
                    new[] { "seed", "local_date" }),
            };

            int ordinal = 0;
            foreach (var item in cases)
            {
                var record = new QueuedAnalyticsEvent(
                    ordinal.ToString("x16"), ordinal, item.Item1.Name, item.Item1.Params,
                    1_800_000_000_000L + ordinal, 200);
                using var request = PostHogWireProtocol.CreateBatchRequest(Config(),
                    "a18f00112233445566778899aabbccdd", new[] { record });
                Assert.That(request, Is.Not.Null, item.Item1.Name);
                var fields = ((JObject)ParsedBody(request)["batch"][0]["properties"])
                    .Properties().Select(x => x.Name);
                Assert.That(fields, Is.EquivalentTo(item.Item2.Concat(new[]
                {
                    "cm_event_id", "$geoip_disable", "$process_person_profile",
                })), item.Item1.Name);
                ordinal++;
            }
        }

        [Test]
        public void OfflineBatchFailure_DefersAndRequestsRetryAfterBoundedBackoff()
        {
            double now = 10d;
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => now);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            AnalyticsDeliveryResult? result = null;
            int retryRequests = 0;
            transport.DeliveryRequested += () => retryRequests++;
            Assert.That(transport.TryDeliver(new[] { Item("0123456789abcdef", 1L) },
                value => result = value), Is.True);

            http.Complete(false, 0);

            Assert.That(result.Value.ServerAccepted, Is.False);
            now = 14.999d;
            transport.Tick();
            Assert.That(retryRequests, Is.Zero);
            now = 15d;
            transport.Tick();
            Assert.That(retryRequests, Is.EqualTo(1));
        }

        [Test]
        public void PendingBatchBackoff_BlocksOtherDeliveryTriggersUntilDue()
        {
            double now = 10d;
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => now);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            var batch = new[] { Item("0123456789abcdef", 1L) };
            Assert.That(transport.TryDeliver(batch, _ => { }), Is.True);
            http.Complete(false, 0);

            now = 14.999d;
            Assert.That(transport.TryDeliver(batch, _ => { }), Is.False,
                "a foreground or high-water trigger must not bypass the backoff");
            Assert.That(http.Requests.Count, Is.EqualTo(2));
            now = 15d;
            Assert.That(transport.TryDeliver(batch, _ => { }), Is.True);
            Assert.That(http.Requests.Count, Is.EqualTo(3));
        }

        [Test]
        public void ForegroundFlagRefresh_DoesNotErasePendingBatchBackoff()
        {
            double now = 10d;
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => now);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            var batch = new[] { Item("0123456789abcdef", 1L) };
            Assert.That(transport.TryDeliver(batch, _ => { }), Is.True);
            http.Complete(false, 0);

            now = 11d;
            transport.RefreshRemoteFlag();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            Assert.That(transport.TryDeliver(batch, _ => { }), Is.False,
                "foreground flag revalidation must preserve the pending batch delay");
            Assert.That(http.Requests.Count, Is.EqualTo(3));

            now = 15d;
            Assert.That(transport.TryDeliver(batch, _ => { }), Is.True);
            Assert.That(http.Requests.Count, Is.EqualTo(4));
        }

        [TestCase(RecordingHttpClient.SynchronousFailure.Refuse)]
        [TestCase(RecordingHttpClient.SynchronousFailure.Throw)]
        public void BatchSendThatCannotStart_UsesNormalBoundedRetrySchedule(
            RecordingHttpClient.SynchronousFailure failure)
        {
            double now = 10d;
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => now);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            http.FailureMode = failure;
            var batch = new[] { Item("0123456789abcdef", 1L) };
            int deliveryRequests = 0;
            transport.DeliveryRequested += () =>
            {
                deliveryRequests++;
                Assert.That(transport.TryDeliver(batch, _ => { }), Is.False);
            };

            Assert.That(transport.TryDeliver(batch, _ => { }), Is.False);
            var dueTimes = new[] { 15d, 25d, 45d, 75d };
            for (int i = 0; i < dueTimes.Length; i++)
            {
                double dueAt = dueTimes[i];
                now = dueAt - 0.001d;
                transport.Tick();
                Assert.That(deliveryRequests, Is.EqualTo(i));
                now = dueAt;
                transport.Tick();
                Assert.That(deliveryRequests, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public void TrueFlagExpiresAfterFifteenMinutes_AndRefreshFailsClosed()
        {
            double now = 100d;
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => now);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");

            now = 999.999d;
            transport.Tick();
            Assert.That(http.Requests.Count, Is.EqualTo(1));
            now = 1_000d;
            Assert.That(transport.TryDeliver(
                new[] { Item("0123456789abcdef", 1L) }, _ => { }), Is.False,
                "a delivery call must not race ahead of an expired flag refresh");

            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Unknown));
            Assert.That(http.Requests.Count, Is.EqualTo(2));
            Assert.That(http.Requests[1].url, Does.Contain("/flags/"));
            Assert.That(http.Requests.All(x => !x.url.EndsWith("/batch")), Is.True);

            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Enabled));
            now = 1_900d;
            transport.Tick();
            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Unknown));
            Assert.That(http.Requests.Count, Is.EqualTo(3),
                "continuous foreground operation must keep revalidating the flag");
        }

        [Test]
        public void FailedFlagRequest_RemainsUnknownAndRetriesWithoutADataBatch()
        {
            double now = 20d;
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => now);
            transport.Initialize();
            http.Complete(false, 0);

            now = 24.999d;
            transport.Tick();
            Assert.That(http.Requests.Count, Is.EqualTo(1));
            now = 25d;
            transport.Tick();

            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Unknown));
            Assert.That(http.Requests.Count, Is.EqualTo(2));
            Assert.That(http.Requests.All(x => x.url.Contains("/flags/")), Is.True);
        }

        [Test]
        public void Refresh_AbortsActiveBatchDefersItsRecordsAndReturnsToUnknown()
        {
            double now = 0d;
            var http = new RecordingHttpClient();
            using var transport = new PostHogAnalyticsTransport(Config(),
                "a18f00112233445566778899aabbccdd", http, () => now);
            transport.Initialize();
            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            AnalyticsDeliveryResult? result = null;
            transport.TryDeliver(new[] { Item("0123456789abcdef", 1L) },
                value => result = value);

            transport.RefreshRemoteFlag();

            Assert.That(transport.RemoteState, Is.EqualTo(AnalyticsRemoteState.Unknown));
            Assert.That(http.AbortCount, Is.EqualTo(1));
            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.ServerAccepted, Is.False);
            Assert.That(http.Requests.Last().url, Does.Contain("/flags/"));
            Assert.That(transport.TryDeliver(new[] { Item("held", 2L) }, _ => { }), Is.False);

            http.Complete(true, 200,
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}");
            var retained = new[] { Item("0123456789abcdef", 2L) };
            now = 4.999d;
            Assert.That(transport.TryDeliver(retained, _ => { }), Is.False,
                "an aborted batch must observe the normal first retry delay");
            now = 5d;
            Assert.That(transport.TryDeliver(retained, _ => { }), Is.True);
        }

        [TestCase("{\"featureFlags\":{\"cat-metro-analytics-enabled\":true}}", true)]
        [TestCase("{\"flags\":{\"cat-metro-analytics-enabled\":{\"enabled\":false}}}", false)]
        public void KillSwitchResponse_AcceptsLegacyAndCurrentBooleanShapes(string json,
            bool expected)
        {
            Assert.That(PostHogWireProtocol.TryParseKillSwitch(json,
                "cat-metro-analytics-enabled", out bool enabled), Is.True);
            Assert.That(enabled, Is.EqualTo(expected));
        }

        [Test]
        public void KillSwitchResponse_FailsClosedOnErrorsOrNonBooleanValues()
        {
            Assert.That(PostHogWireProtocol.TryParseKillSwitch(
                "{\"errorsWhileComputingFlags\":true,\"featureFlags\":{"
                + "\"cat-metro-analytics-enabled\":true}}",
                "cat-metro-analytics-enabled", out _), Is.False);
            Assert.That(PostHogWireProtocol.TryParseKillSwitch(
                "{\"featureFlags\":{\"cat-metro-analytics-enabled\":\"variant\"}}",
                "cat-metro-analytics-enabled", out _), Is.False);
        }
    }
}
