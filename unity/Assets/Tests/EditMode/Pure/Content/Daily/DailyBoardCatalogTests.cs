using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CatMetro.Content.Daily;
using CatMetro.Tests.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Daily
{
    public sealed class DailyBoardCatalogTests
    {
        [Test]
        public void ShippedArtifact_IsTheExactNinetyDayHorizon_AndEveryBoardImports()
        {
            DailyPipelineConfig config = DFixtures.ShippedPipelineConfig();
            var expectedDates = DateKeys.Enumerate(
                config.AnchorDateKey, config.PrevalidationDays);
            byte[] sourceBytes = ShippedBytes();
            byte[] stagedBytes = File.ReadAllBytes(Path.Combine(RepoRoot(),
                "unity", "Assets", "StreamingAssets", DailyBoardCatalog.RelativePath));
            Assert.That(stagedBytes, Is.EqualTo(sourceBytes),
                "the runtime copy must be byte-identical to the generated source artifact");

            DailyCatalogLoadResult loaded = DailyBoardCatalog.LoadShipped(sourceBytes, config);
            Assert.That(loaded.Ok, Is.True, loaded.Detail);
            Assert.That(loaded.Value.CatalogVersion, Is.EqualTo(1));
            Assert.That(loaded.Value.Generator, Is.EqualTo(DailyLineSeed.GeneratorLabel));
            Assert.That(loaded.Value.FromDateKey, Is.EqualTo(config.AnchorDateKey));
            Assert.That(loaded.Value.ThroughDateKey, Is.EqualTo(expectedDates.Last()));
            Assert.That(loaded.Value.Count, Is.EqualTo(config.PrevalidationDays));

            Assert.That(loaded.Value.DateKeys, Is.EqualTo(expectedDates));

            var factory = new DailyBoardFactory();
            foreach (string dateKey in expectedDates)
            {
                DailyCatalogLookupResult lookup = loaded.Value.Lookup(dateKey);
                Assert.That(lookup.Found, Is.True, dateKey + ": " + lookup.Detail);
                DailyCatalogEntry entry = lookup.Entry;
                Assert.That(entry.DateKey, Is.EqualTo(dateKey));
                Assert.That(entry.Seed, Is.EqualTo(DailyLineSeed.Derive(dateKey)));
                Assert.That(entry.K, Is.Zero, dateKey);
                Assert.That(entry.UsedFallback, Is.False, dateKey);
                Assert.That(entry.Level.Dto.Id, Is.EqualTo("L800"));
                Assert.That(entry.Level.Dto.Meta.Band, Is.EqualTo("daily"));

                var expected = factory.Build(entry.Seed, dateKey, entry.K);
                Assert.That(DailyBoardJson.Serialize(entry.Level.Dto),
                    Is.EqualTo(DailyBoardJson.Serialize(expected)),
                    dateKey + " must be the deterministic UTC-seeded generator output");
            }
        }

        [TestCase("2026-08-23")]
        [TestCase("2026-11-22")]
        [TestCase("not-a-date")]
        public void Lookup_OutsideCatalogOrMalformed_IsACleanMiss(string dateKey)
        {
            DailyCatalogLoadResult loaded = DailyBoardCatalog.Load(ShippedBytes());
            Assert.That(loaded.Ok, Is.True, loaded.Detail);

            DailyCatalogLookupResult lookup = loaded.Value.Lookup(dateKey);

            Assert.That(lookup.Found, Is.False);
            Assert.That(lookup.Entry, Is.Null);
            Assert.That(lookup.Detail, Is.Not.Empty);
        }

        [Test]
        public void MissingCatalog_IsACleanLoadMiss()
        {
            DailyCatalogLoadResult loaded = DailyBoardCatalog.Load(null);

            Assert.That(loaded.Ok, Is.False);
            Assert.That(loaded.Value, Is.Null);
            Assert.That(loaded.Detail, Does.Contain("missing").IgnoreCase);
        }

        [Test]
        public void ShippedLoader_RejectsASelfConsistentShorterHorizon()
        {
            DailyPipelineConfig config = DFixtures.ShippedPipelineConfig();
            JObject root = JObject.Parse(Encoding.UTF8.GetString(ShippedBytes()));
            var entries = (JArray)root["entries"];
            entries.RemoveAt(entries.Count - 1);
            root["entryCount"] = entries.Count;
            root["throughDateKey"] = (string)((JObject)entries[entries.Count - 1])["dateKey"];

            DailyCatalogLoadResult loaded = DailyBoardCatalog.LoadShipped(
                Encoding.UTF8.GetBytes(root.ToString(Formatting.Indented)), config);

            Assert.That(loaded.Ok, Is.False,
                "the runtime contract is the configured horizon, not merely self-consistent JSON");
            Assert.That(loaded.Detail,
                Does.Contain(config.PrevalidationDays.ToString()));
        }

        [Test]
        public void ShippedLoader_RejectsSameCountCatalogWithDifferentConfiguredAnchor()
        {
            DailyPipelineConfig config = DFixtures.ShippedPipelineConfig();
            var shifted = new DailyPipelineConfig(config.PrevalidationDays, config.SaltMaxK,
                DateKeys.Enumerate(config.AnchorDateKey, 2)[1]);

            DailyCatalogLoadResult loaded = DailyBoardCatalog.LoadShipped(
                ShippedBytes(), shifted);

            Assert.That(loaded.Ok, Is.False,
                "matching only entry count would accept a shifted offline horizon");
            Assert.That(loaded.Detail, Does.Contain(shifted.AnchorDateKey));
        }

        [Test]
        public void ArtifactWriter_RoundTripsARealGeneratedAndValidatedRecord()
        {
            string firstDate = DFixtures.ShippedPipelineConfig().AnchorDateKey;
            DailyRunReport pipelineReport = DFixtures.Run(DFixtures.RuntimeRequest(
                new[] { firstDate }, new DailyBoardFactory()));
            var report = new DailyRunReport(pipelineReport.Generator,
                pipelineReport.Records, DailyBoardCatalog.BoardProvenance,
                pipelineReport.ExitFailure);
            Assert.That(report.ExitFailure, Is.False);
            Assert.That(report.BoardProvenance,
                Is.EqualTo(DailyBoardCatalog.BoardProvenance));
            Assert.That(report.Generator, Is.EqualTo(DailyLineSeed.GeneratorLabel));

            string artifact = DailyBoardCatalog.CreateArtifactJson(report);
            DailyCatalogLoadResult loaded = DailyBoardCatalog.Load(
                Encoding.UTF8.GetBytes(artifact));

            Assert.That(loaded.Ok, Is.True, loaded.Detail);
            DailyCatalogLookupResult lookup = loaded.Value.Lookup(firstDate);
            Assert.That(lookup.Found, Is.True, lookup.Detail);
            Assert.That(DailyBoardJson.Serialize(lookup.Entry.Level.Dto),
                Is.EqualTo(report.Records.Single().BoardJson));
        }

        [TestCase("generator")]
        [TestCase("date")]
        [TestCase("seed")]
        [TestCase("board")]
        [TestCase("hash")]
        public void MutatedArtifact_IsRejectedInsteadOfServingAnUnprovenBoard(string mutation)
        {
            JObject root = JObject.Parse(Encoding.UTF8.GetString(ShippedBytes()));
            JObject first = (JObject)((JArray)root["entries"])[0];
            switch (mutation)
            {
                case "generator":
                    root["generator"] = "CM-DAILY-corrupt";
                    break;
                case "date":
                    first["dateKey"] = "2026-08-25";
                    break;
                case "seed":
                    first["seed"] = (long)first["seed"] + 1L;
                    break;
                case "board":
                    ((JObject)first["board"])["name"] = "Mutated Daily";
                    break;
                case "hash":
                    first["boardSha256"] = new string('0', 64);
                    break;
                default:
                    Assert.Fail("unknown mutation " + mutation);
                    break;
            }

            DailyCatalogLoadResult loaded = DailyBoardCatalog.Load(
                Encoding.UTF8.GetBytes(root.ToString(Formatting.Indented)));

            Assert.That(loaded.Ok, Is.False, mutation + " unexpectedly loaded");
            Assert.That(loaded.Value, Is.Null);
            Assert.That(loaded.Detail, Is.Not.Empty);
        }

        [Test]
        public void BoardMetadataMutation_WithMatchingNewHash_IsStillRejected()
        {
            JObject root = JObject.Parse(Encoding.UTF8.GetString(ShippedBytes()));
            JObject first = (JObject)((JArray)root["entries"])[0];
            JObject board = (JObject)first["board"];
            board["id"] = "L801";
            first["boardSha256"] = Sha256(board.ToString(Formatting.Indented));

            DailyCatalogLoadResult loaded = DailyBoardCatalog.Load(
                Encoding.UTF8.GetBytes(root.ToString(Formatting.Indented)));

            Assert.That(loaded.Ok, Is.False);
            Assert.That(loaded.Detail, Does.Contain("L800"));
        }

        private static byte[] ShippedBytes() => File.ReadAllBytes(Path.Combine(
            RepoRoot(), "content", "daily", "precomputed.json"));

        private static string RepoRoot()
        {
            return Fixtures.RepoRoot();
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    .Replace("-", "").ToLowerInvariant();
        }
    }
}
