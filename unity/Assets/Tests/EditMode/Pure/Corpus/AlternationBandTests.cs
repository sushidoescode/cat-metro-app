using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Corpus
{
    [TestFixture]
    public class EarlyCampaignArtifactTests
    {
        private static JObject Raw(string id) =>
            JObject.Parse(System.Text.Encoding.UTF8.GetString(BandFixtures.Bytes(id)));

        [TestCaseSource(typeof(BandFixtures), nameof(BandFixtures.FirstTwentyIds))]
        public void DtoMetadataAndCollectionShapeMatchTheAuthoredJson(string id)
        {
            var raw = Raw(id);
            var dto = BandFixtures.Import(id).Dto;

            Assert.That(dto.SchemaVersion, Is.EqualTo((int)raw["schemaVersion"]), id);
            Assert.That(dto.Id, Is.EqualTo((string)raw["id"]), id);
            Assert.That(dto.Name, Is.EqualTo((string)raw["name"]), id);
            Assert.That(dto.Seed, Is.EqualTo((long)raw["seed"]), id);
            Assert.That(dto.Meta.Band, Is.EqualTo((string)raw["meta"]["band"]), id);
            Assert.That(dto.Meta.DifficultyTarget,
                Is.EqualTo((double)raw["meta"]["difficultyTarget"]).Within(1e-12), id);
            Assert.That(dto.Meta.Mechanics.ToArray(),
                Is.EqualTo(raw["meta"]["mechanics"].Select(token => (string)token)), id);
            Assert.That(dto.Meta.NewMechanic, Is.EqualTo((string)raw["meta"]["newMechanic"]), id);
            Assert.That(dto.Meta.TeachingGoal, Is.EqualTo((string)raw["meta"]["teachingGoal"]), id);
            Assert.That(dto.Meta.MinActionWindowTicks,
                Is.EqualTo((int)raw["meta"]["minActionWindowTicks"]), id);
            Assert.That(dto.Meta.AuthoredBy, Is.EqualTo((string)raw["meta"]["authoredBy"]), id);
            Assert.That(dto.Nodes.Length, Is.EqualTo(((JArray)raw["board"]["nodes"]).Count), id);
            Assert.That(dto.Edges.Length, Is.EqualTo(((JArray)raw["board"]["edges"]).Count), id);
            Assert.That(dto.Sources.Length, Is.EqualTo(((JArray)raw["sources"]).Count), id);
            Assert.That(dto.Stations.Length, Is.EqualTo(((JArray)raw["stations"]).Count), id);
            Assert.That(dto.Switches.Length, Is.EqualTo(((JArray)raw["switches"]).Count), id);
            Assert.That(dto.Waves.Length, Is.EqualTo(((JArray)raw["waves"]).Count), id);
            Assert.That(dto.Meta.HasValidatedAt, Is.False, id);
            Assert.That(dto.Meta.ValidatedAt, Is.Null, id);
        }

        [Test]
        public void FirstTwentyDifficultyTargetsRiseAndIntroductionsAreHonest()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            double previousDifficulty = -1;

            foreach (string id in BandFixtures.FirstTwentyIds)
            {
                var dto = BandFixtures.Import(id).Dto;
                Assert.That(dto.Meta.DifficultyTarget, Is.GreaterThan(previousDifficulty), id);
                previousDifficulty = dto.Meta.DifficultyTarget;

                var mechanics = dto.Meta.Mechanics.ToArray();
                Assert.That(mechanics, Does.Contain("switch"), id);
                if (dto.Meta.NewMechanic != null)
                {
                    Assert.That(mechanics, Does.Contain(dto.Meta.NewMechanic), id);
                    Assert.That(seen, Does.Not.Contain(dto.Meta.NewMechanic),
                        id + " must introduce a mechanic only once");
                }

                foreach (string mechanic in mechanics)
                {
                    Assert.That(seen.Contains(mechanic) || mechanic == dto.Meta.NewMechanic,
                        Is.True, id + " names an unexplained mechanic " + mechanic);
                    seen.Add(mechanic);
                }
            }
        }

        [Test]
        public void FirstTwentyExactSupplyMatchesTheDeliveryGoal()
        {
            foreach (string id in BandFixtures.FirstTwentyIds)
            {
                var imported = BandFixtures.Import(id);
                int nonStraySupply = 0;
                for (int wave = 0; wave < imported.Graph.WaveCount.Length; wave++)
                    if (!imported.Graph.WaveStray[wave])
                        nonStraySupply += imported.Graph.WaveCount[wave];
                Assert.That(nonStraySupply, Is.EqualTo(imported.Graph.WinDeliveries), id);
            }
        }
    }
}
