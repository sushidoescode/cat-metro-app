using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Tests.Domain;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Content
{
    [TestFixture]
    public class ContentL001Tests
    {
        private static byte[] L001Bytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json"));

        private static JObject L001Raw() =>
            JObject.Parse(System.Text.Encoding.UTF8.GetString(L001Bytes()));

        private static ImportedLevel Imported()
        {
            var result = LevelImporter.Import(L001Bytes());
            Assert.That(result.Ok, Is.True, $"L001 must import: {result.Error}");
            return result.Value;
        }

        [Test]
        public void Dto_CarriesTheAuthoredArtifactWithoutFrozenCampaignValues()
        {
            var raw = L001Raw();
            var dto = Imported().Dto;

            Assert.That(dto.SchemaVersion, Is.EqualTo((int)raw["schemaVersion"]));
            Assert.That(dto.Id, Is.EqualTo((string)raw["id"]));
            Assert.That(dto.Name, Is.EqualTo((string)raw["name"]));
            Assert.That(dto.Seed, Is.EqualTo((long)raw["seed"]));
            Assert.That(dto.Meta.Band, Is.EqualTo((string)raw["meta"]["band"]));
            Assert.That(dto.Meta.DifficultyTarget,
                Is.EqualTo((double)raw["meta"]["difficultyTarget"]).Within(1e-12));
            Assert.That(dto.Meta.Mechanics.ToArray(),
                Is.EqualTo(raw["meta"]["mechanics"].Select(token => (string)token).ToArray()));
            Assert.That(dto.Meta.NewMechanic, Is.EqualTo((string)raw["meta"]["newMechanic"]));
            Assert.That(dto.Meta.TeachingGoal, Is.EqualTo((string)raw["meta"]["teachingGoal"]));
            Assert.That(dto.Meta.MinActionWindowTicks,
                Is.EqualTo((int)raw["meta"]["minActionWindowTicks"]));
            Assert.That(dto.Meta.AuthoredBy, Is.EqualTo((string)raw["meta"]["authoredBy"]));
            Assert.That(dto.Nodes.Length, Is.EqualTo(((JArray)raw["board"]["nodes"]).Count));
            Assert.That(dto.Edges.Length, Is.EqualTo(((JArray)raw["board"]["edges"]).Count));
            Assert.That(dto.Sources.Length, Is.EqualTo(((JArray)raw["sources"]).Count));
            Assert.That(dto.Stations.Length, Is.EqualTo(((JArray)raw["stations"]).Count));
            Assert.That(dto.Switches.Length, Is.EqualTo(((JArray)raw["switches"]).Count));
            Assert.That(dto.Waves.Length, Is.EqualTo(((JArray)raw["waves"]).Count));
            Assert.That(dto.Win.Deliveries, Is.EqualTo((int)raw["win"]["deliveries"]));
            Assert.That(dto.Win.TimeLimitTicks, Is.EqualTo((int)raw["win"]["timeLimitTicks"]));
            Assert.That(dto.Win.Stars.Two, Is.EqualTo((int)raw["win"]["stars"]["two"]));
            Assert.That(dto.Win.Stars.Three, Is.EqualTo((int)raw["win"]["stars"]["three"]));
            Assert.That(dto.Economy.BaseTickets, Is.EqualTo((int)raw["economy"]["baseTickets"]));
            Assert.That(dto.Economy.PerfectBonus, Is.EqualTo((int)raw["economy"]["perfectBonus"]));
        }

        [Test]
        public void RawJson_HasCoherentLevelOneStructure()
        {
            var raw = L001Raw();
            Assert.That((int)raw["schemaVersion"], Is.EqualTo(2));
            Assert.That((string)raw["id"], Is.EqualTo("L001"));
            Assert.That(raw["meta"]["mechanics"].Select(token => (string)token), Does.Contain("switch"));
            Assert.That((string)raw["meta"]["newMechanic"], Is.EqualTo("switch"));
            Assert.That((string)raw["meta"]["teachingGoal"], Is.Not.Empty);
            Assert.That((string)raw["meta"]["authoredBy"], Is.EqualTo("llm+validator"));

            foreach (string collection in new[] { "nodes", "edges" })
                Assert.That(raw["board"][collection].Select(item => (string)item["id"]), Is.Unique, collection);
            Assert.That(raw["switches"].Select(item => (string)item["id"]), Is.Unique, "switches");

            int emitted = raw["waves"].Sum(wave => (int)wave["count"]);
            Assert.That(emitted, Is.EqualTo((int)raw["win"]["deliveries"]),
                "the introductory level must require its exact non-stray supply");
        }

        [Test]
        public void Meta_HasNoValidatedAtKey_EitherWay()
        {
            Assert.That(((JObject)L001Raw()["meta"]).Property("validatedAt"), Is.Null,
                "raw file must not carry a validatedAt key");
            var dto = Imported().Dto;
            Assert.That(dto.Meta.HasValidatedAt, Is.False);
            Assert.That(dto.Meta.ValidatedAt, Is.Null);
        }
    }
}
