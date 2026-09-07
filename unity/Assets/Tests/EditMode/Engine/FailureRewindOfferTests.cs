using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Strings;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests
{
    public sealed class FailureRewindOfferTests
    {
        [TestCase(0f, 48f)]
        [TestCase(160f, 48f)]
        [TestCase(320f, 96f)]
        [TestCase(480f, 144f)]
        public void OfferTouchesUnchangedThumbBandAndHas48DpTarget(float dpi, float height)
        {
            var safeArea = new Rect(12f, 32f, 1080f, 2000f);
            var rect = FailureRewindOfferView.ChipRect(safeArea, dpi);
            Assert.That(rect, Is.EqualTo(new Rect(12f, 532f, 1080f, height)));
            Assert.That(HudBands.ThumbBand(safeArea), Is.EqualTo(new Rect(12f, 32f, 1080f, 500f)));
        }

        [Test]
        public void RewindCopyResolvesThroughShippedLocalization()
        {
            Assert.That(UiStrings.Get("failure.rewind"), Is.EqualTo("Watch to rewind"));
        }
    }
}
