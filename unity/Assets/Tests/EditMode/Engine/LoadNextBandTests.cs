using System.Threading;
using NUnit.Framework;
using CatMetro.Bootstrap;
using CatMetro.Content;

namespace CatMetro.Tests.Engine
{
    // CM-LOADNEXT: the campaign-band progression policy, pure and test-drivable (the
    // GameRoot.FailKey precedent) — no Unity scene/engine object needed to exercise the id/wrap
    // logic. ASSUMPTION under human review (state/handoffs/CM-LOADNEXT-frozen-contract.md,
    // A-LN-1): end-of-band WRAPS to L001 rather than stopping — GameRoot.WrapAtEndOfBand is the
    // one seam to flip if that assumption is overridden; this file pins the CURRENT policy and
    // would need its own declared amendment the day it changes.
    public sealed class LoadNextBandTests
    {
        [Test]
        public void Band_IsTheNineteenLevelCampaignInOrder()
        {
            // The band runs the FULL authored campaign — it has always tracked the corpus
            // (L001-L005, then L001-L010, then L001-L017), never a product cap. LEVEL-VARIETY
            // added L018/L019, so they belong here or they are unreachable in play. Still
            // two-sided (Is.EqualTo, not loosened), per GameRoot.LevelBand.
            Assert.That(GameRoot.LevelBand, Is.EqualTo(new[]
            {
                "L001", "L002", "L003", "L004", "L005",
                "L006", "L007", "L008", "L009", "L010",
                "L011", "L012", "L013", "L014", "L015", "L016", "L017",
                "L018", "L019",
            }));
        }

        [TestCase("L001", "L002")]
        [TestCase("L002", "L003")]
        [TestCase("L003", "L004")]
        [TestCase("L004", "L005")]
        [TestCase("L005", "L006")]
        [TestCase("L006", "L007")]
        [TestCase("L007", "L008")]
        [TestCase("L008", "L009")]
        [TestCase("L009", "L010")]
        [TestCase("L010", "L011")]
        [TestCase("L011", "L012")]
        [TestCase("L012", "L013")]
        [TestCase("L013", "L014")]
        [TestCase("L014", "L015")]
        [TestCase("L015", "L016")]
        [TestCase("L016", "L017")]
        [TestCase("L017", "L018")]
        [TestCase("L018", "L019")]
        public void NextLevelId_AdvancesOneStepThroughTheBand(string current, string expectedNext)
        {
            Assert.That(GameRoot.NextLevelId(current), Is.EqualTo(expectedNext));
        }

        [Test]
        public void NextLevelId_AtTheEndOfTheBand_WrapsToTheFirstLevel()
        {
            // A demo-friendly infinite loop, not a dead end. A human override lands as a
            // one-line edit to GameRoot.WrapAtEndOfBand.
            // The wrap TARGET is unchanged (still L001); only the level that IS the end moves
            // as the campaign grows — L005, then L017, now L019.
            Assert.That(GameRoot.NextLevelId("L019"), Is.EqualTo("L001"));
        }

        [Test]
        public void NextLevelId_UnknownId_RestartsTheBand_NeverThrows()
        {
            // A dev-override or test-fixture id (never a shipped band id) restarts the band at
            // its first level rather than crashing progression (A-LN-2).
            Assert.That(GameRoot.NextLevelId("not-a-band-level"), Is.EqualTo("L001"));
        }

        [Test]
        public void LevelPath_IsTheStreamingAssetsSeamPath()
        {
            Assert.That(GameRoot.LevelPath("L002"), Is.EqualTo("content/levels/L002.json"));
        }

        [Test]
        public void LevelPath_OfNextLevelId_ReadsThroughTheRealSeam()
        {
            // The engine-host half (BootstrapSeamTests.cs precedent, this same directory): the
            // id-and-wrap logic above is pure, but the file it points at must be REAL — this is
            // the load-bearing proof that NextLevelId("L001") names a file that actually exists
            // on the seam, and that its OWN id is the level the path claims.
            var path = GameRoot.LevelPath(GameRoot.NextLevelId("L001"));
            var source = new StreamingAssetsContentSource();
            Assert.That(source.Exists(path), Is.True, path + " must exist through the real seam");
            var bytes = source.ReadAsync(path, CancellationToken.None).GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            Assert.That(imported.Ok, Is.True, "L002 must import through the seam: " + imported.Error);
            Assert.That(imported.Value.Dto.Id, Is.EqualTo("L002"));
        }
    }
}
