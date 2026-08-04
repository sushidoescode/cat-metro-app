using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Content
{
    // Criterion 1: every named authored field, asserted BOTH from the parsed DTO and from a raw
    // JSON key walk, so a parser bug cannot mask a content bug. Tests may use System.IO; the
    // shipped Content assembly may not.
    [TestFixture]
    public class ContentL001Tests
    {
        private static byte[] L001Bytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json"));

        private static JObject L001Raw() =>
            JObject.Parse(System.Text.Encoding.UTF8.GetString(L001Bytes()));

        private static ImportedLevel Imported()
        {
            var r = LevelImporter.Import(L001Bytes());
            Assert.That(r.Ok, Is.True, $"L001 must import: {r.Error}");
            return r.Value;
        }

        [Test]
        public void Dto_CarriesEveryAuthoredFieldValue()
        {
            var dto = Imported().Dto;
            Assert.That(dto.SchemaVersion, Is.EqualTo(2));
            Assert.That(dto.Id, Is.EqualTo("L001"));
            Assert.That(dto.Seed, Is.EqualTo(1001));
            Assert.That(dto.Meta.Band, Is.EqualTo("onboarding"));
            Assert.That(dto.Meta.DifficultyTarget, Is.EqualTo(0.08).Within(1e-12));
            Assert.That(dto.Meta.Mechanics.ToArray(), Is.EqualTo(new[] { "switch" }));
            Assert.That(dto.Meta.NewMechanic, Is.EqualTo("switch"));
            Assert.That(dto.Meta.MinActionWindowTicks, Is.EqualTo(16));
            Assert.That(dto.Meta.AuthoredBy, Is.EqualTo("human"));
            Assert.That(dto.Nodes.Length, Is.EqualTo(4));
            Assert.That(dto.Edges.Length, Is.EqualTo(3));
            Assert.That(dto.Sources.Length, Is.EqualTo(1));
            Assert.That(dto.Stations.Length, Is.EqualTo(2));
            Assert.That(dto.Switches.Length, Is.EqualTo(1));
            Assert.That(dto.Switches.Span[0].InitialRoute, Is.EqualTo(1));
            Assert.That(dto.Waves.Length, Is.EqualTo(1));
            var w = dto.Waves.Span[0];
            Assert.That(w.Tick, Is.EqualTo(8));
            Assert.That(w.Color, Is.EqualTo("red"));
            Assert.That(w.Count, Is.EqualTo(2));
            Assert.That(w.SpacingTicks, Is.EqualTo(20));
            Assert.That(dto.Win.Deliveries, Is.EqualTo(2));
            Assert.That(dto.Win.TimeLimitTicks, Is.EqualTo(160));
            Assert.That(dto.Win.PerfectMaxSwitches, Is.EqualTo(1));
            Assert.That(dto.Win.Stars.Two, Is.EqualTo(200));
            Assert.That(dto.Win.Stars.Three, Is.EqualTo(300));
            Assert.That(dto.Economy.BaseTickets, Is.EqualTo(20));
            Assert.That(dto.Economy.PerfectBonus, Is.EqualTo(10));
        }

        [Test]
        public void RawJson_CarriesEveryAuthoredFieldValue()
        {
            var j = L001Raw();
            Assert.That((int)j["schemaVersion"], Is.EqualTo(2));
            Assert.That((string)j["id"], Is.EqualTo("L001"));
            Assert.That((long)j["seed"], Is.EqualTo(1001));
            Assert.That((string)j["meta"]["band"], Is.EqualTo("onboarding"));
            Assert.That((double)j["meta"]["difficultyTarget"], Is.EqualTo(0.08).Within(1e-12));
            Assert.That(j["meta"]["mechanics"].Select(t => (string)t), Is.EqualTo(new[] { "switch" }));
            Assert.That((string)j["meta"]["newMechanic"], Is.EqualTo("switch"));
            Assert.That((int)j["meta"]["minActionWindowTicks"], Is.EqualTo(16));
            Assert.That((string)j["meta"]["authoredBy"], Is.EqualTo("human"));
            Assert.That(((JArray)j["board"]["nodes"]).Count, Is.EqualTo(4));
            Assert.That(((JArray)j["board"]["edges"]).Count, Is.EqualTo(3));
            Assert.That(((JArray)j["sources"]).Count, Is.EqualTo(1));
            Assert.That(((JArray)j["stations"]).Count, Is.EqualTo(2));
            Assert.That(((JArray)j["switches"]).Count, Is.EqualTo(1));
            Assert.That((int)j["switches"][0]["initialRoute"], Is.EqualTo(1));
            Assert.That(((JArray)j["waves"]).Count, Is.EqualTo(1));
            Assert.That((int)j["waves"][0]["tick"], Is.EqualTo(8));
            Assert.That((string)j["waves"][0]["color"], Is.EqualTo("red"));
            Assert.That((int)j["waves"][0]["count"], Is.EqualTo(2));
            Assert.That((int)j["waves"][0]["spacingTicks"], Is.EqualTo(20));
            Assert.That((int)j["win"]["deliveries"], Is.EqualTo(2));
            Assert.That((int)j["win"]["timeLimitTicks"], Is.EqualTo(160));
            Assert.That((int)j["win"]["perfectMaxSwitches"], Is.EqualTo(1));
            Assert.That((int)j["win"]["stars"]["two"], Is.EqualTo(200));
            Assert.That((int)j["win"]["stars"]["three"], Is.EqualTo(300));
            Assert.That((int)j["economy"]["baseTickets"], Is.EqualTo(20));
            Assert.That((int)j["economy"]["perfectBonus"], Is.EqualTo(10));
        }

        [Test]
        public void Meta_HasNoValidatedAtKey_EitherWay()
        {
            // AMD-09 / ADR-0008:119-123: the key is DELETED when unvalidated, never null.
            Assert.That(((JObject)L001Raw()["meta"]).Property("validatedAt"), Is.Null,
                "raw file must not carry a validatedAt key");
            var dto = Imported().Dto;
            Assert.That(dto.Meta.HasValidatedAt, Is.False);
            Assert.That(dto.Meta.ValidatedAt, Is.Null);
        }
    }
}
