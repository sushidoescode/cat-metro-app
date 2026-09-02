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
        public void Band_IsTheSixtyLevelCampaignInOrder()
        {
            var expected = new string[60];
            for (int i = 0; i < expected.Length; i++)
                expected[i] = "L" + (i + 1).ToString("000");
            Assert.That(GameRoot.LevelBand, Is.EqualTo(expected));
        }

        [Test]
        public void NextLevelId_AdvancesOneStepThroughTheBand()
        {
            for (int i = 1; i < 60; i++)
            {
                string current = "L" + i.ToString("000");
                string expectedNext = "L" + (i + 1).ToString("000");
                Assert.That(GameRoot.NextLevelId(current), Is.EqualTo(expectedNext), current);
            }
        }

        [Test]
        public void NextLevelId_AtTheEndOfTheBand_WrapsToTheFirstLevel()
        {
            // The shipped campaign remains a demo-friendly loop instead of a dead end.
            Assert.That(GameRoot.NextLevelId("L060"), Is.EqualTo("L001"));
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
