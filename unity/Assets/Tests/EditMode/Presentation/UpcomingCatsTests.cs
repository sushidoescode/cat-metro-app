using System.Collections.Generic;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests
{
    // HUD-WAVE: the projection from authored WAVES to the per-cat queue the capsule paints.
    // Pure — no Unity objects — so the ordering law can be pinned exhaustively here instead of
    // through a rendered strip.
    public sealed class UpcomingCatsTests
    {
        private static WaveDto Wave(int tick, string color, int count, int spacing) =>
            new WaveDto(tick, "SRC", color, count, spacing);

        private static string Summarise(List<UpcomingCat> cats)
        {
            var parts = new List<string>();
            foreach (var c in cats) parts.Add(c.Color + "@" + c.Tick);
            return string.Join("|", parts);
        }

        [Test]
        public void OneWaveExpandsToOneCatPerCount_SpacedByItsSpacingTicks()
        {
            var cats = UpcomingCats.Next(new[] { Wave(8, "red", 3, 20) }, 0, 10);

            Assert.That(Summarise(cats), Is.EqualTo("red@8|red@28|red@48"),
                "a wave of 3 is three cats, not one chip labelled x3");
        }

        [Test]
        public void CatsAreOrderedByEmissionTick_AcrossWaves()
        {
            var waves = new[]
            {
                Wave(10, "blue", 2, 20),   // blue@10, blue@30
                Wave(5, "red", 1, 10),     // red@5
                Wave(20, "yellow", 1, 10), // yellow@20
            };

            Assert.That(Summarise(UpcomingCats.Next(waves, 0, 10)),
                Is.EqualTo("red@5|blue@10|yellow@20|blue@30"),
                "emission order, never authoring order");
        }

        [Test]
        public void TiesOnEmissionTick_KeepTheAuthoredWaveOrder()
        {
            // Two waves fire on the same tick. List.Sort is introsort and is NOT stable, so
            // this is the case that catches a missing explicit tie-break.
            var waves = new[]
            {
                Wave(8, "red", 1, 1),
                Wave(8, "blue", 1, 1),
                Wave(8, "green", 1, 1),
            };

            Assert.That(Summarise(UpcomingCats.Next(waves, 0, 10)),
                Is.EqualTo("red@8|blue@8|green@8"));
        }

        [Test]
        public void EmittedCatsFallOutOfTheQueueAsTheTickAdvances()
        {
            var waves = new[] { Wave(8, "red", 3, 20) }; // 8, 28, 48

            Assert.That(UpcomingCats.Next(waves, 0, 10).Count, Is.EqualTo(3));
            Assert.That(UpcomingCats.Next(waves, 8, 10).Count, Is.EqualTo(3),
                "a cat is still upcoming ON its emission tick");
            Assert.That(UpcomingCats.Next(waves, 9, 10).Count, Is.EqualTo(2));
            Assert.That(UpcomingCats.Next(waves, 49, 10), Is.Empty);
        }

        [Test]
        public void TheCapTrimsTheTail_KeepingTheSoonestCats()
        {
            var waves = new[] { Wave(0, "red", 10, 5) };

            Assert.That(Summarise(UpcomingCats.Next(waves, 0, 3)),
                Is.EqualTo("red@0|red@5|red@10"));
            Assert.That(UpcomingCats.Next(waves, 0, 0), Is.Empty);
            Assert.That(UpcomingCats.Next(waves, 0, -1), Is.Empty);
        }

        [Test]
        public void RemainingCountIgnoresTheDisplayCap()
        {
            var waves = new[] { Wave(0, "red", 10, 5), Wave(3, "blue", 4, 5) };

            Assert.That(UpcomingCats.Next(waves, 0, 3).Count, Is.EqualTo(3));
            Assert.That(UpcomingCats.RemainingCount(waves, 0), Is.EqualTo(14),
                "the overflow tail needs the true remainder, not the capped one");
        }

        [Test]
        public void NoWavesIsAnEmptyQueue_NotAThrow()
        {
            Assert.That(UpcomingCats.Next(new WaveDto[0], 0, 6), Is.Empty);
            Assert.That(UpcomingCats.RemainingCount(new WaveDto[0], 0), Is.Zero);
        }
    }

    // The shared line vocabulary: colour, destination SHAPE and destination LETTER.
    public sealed class CatLineTests
    {
        private static readonly string[] Lines = { "red", "blue", "yellow", "green" };

        [Test]
        public void EveryLineBindsAPaletteToken()
        {
            Assert.That(CatLine.ColorOf("red"), Is.EqualTo(Palette.SignalRed));
            Assert.That(CatLine.ColorOf("blue"), Is.EqualTo(Palette.HarborBlue));
            Assert.That(CatLine.ColorOf("yellow"), Is.EqualTo(Palette.TabbyYellow));
            Assert.That(CatLine.ColorOf("green"), Is.EqualTo(Palette.GardenGreen));
        }

        [Test]
        public void EveryLineGetsItsOwnShape_SoColourIsNeverTheOnlyCarrier()
        {
            var seen = new HashSet<DestinationShape>();
            foreach (var line in Lines)
                Assert.That(seen.Add(CatLine.ShapeOf(line)), Is.True,
                    line + " reuses another line's shape — that puts identity back on colour");
        }

        [Test]
        public void RedAndBlueKeepTheShapesTheBoardAlreadyPaints()
        {
            // BoardPropDecorator builds a cylinder plate when the station glyph is "R" and a
            // cube otherwise. The HUD must agree with the board a player is looking at.
            Assert.That(CatLine.ShapeOf("red"), Is.EqualTo(DestinationShape.Circle));
            Assert.That(CatLine.ShapeOf("blue"), Is.EqualTo(DestinationShape.Square));
        }

        [Test]
        public void GlyphMatchesBoardViewsFirstLetterRule()
        {
            foreach (var line in Lines)
                Assert.That(CatLine.GlyphOf(line),
                    Is.EqualTo(line.Substring(0, 1).ToUpperInvariant()));
            Assert.That(CatLine.GlyphOf(""), Is.EqualTo("?"));
            Assert.That(CatLine.GlyphOf(null), Is.EqualTo("?"));
        }
    }
}
