using CatMetro.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.Presentation.Audio
{
    public sealed class MusicDirectorTests
    {
        [TestCase(MusicScene.Home, false, 0, 3, 0f, .5f, 0f, 0f, 0f)]
        [TestCase(MusicScene.Playing, false, 0, 3, 0f, .7f, 0f, 0f, 0f)]
        [TestCase(MusicScene.Playing, true, 0, 3, 0f, .7f, 1f, 0f, 0f)]
        [TestCase(MusicScene.Playing, true, 1, 3, 0f, .7f, 1f, 1f, 0f)]
        [TestCase(MusicScene.Playing, false, 2, 3, 0f, .7f, 0f, 1f, 1f)]
        [TestCase(MusicScene.Won, false, 3, 3, .5f, .245f, 0f, .35f, .35f)]
        [TestCase(MusicScene.Won, false, 3, 3, 1.3f, .7f, 0f, 1f, 1f)]
        [TestCase(MusicScene.Failed, false, 1, 3, 1f, .5f, 0f, 0f, 0f)]
        [TestCase(MusicScene.Quiet, true, 2, 3, 0f, 0f, 0f, 0f, 0f)]
        public void StateChangesBringInOnlyTheNeededStems(MusicScene scene, bool moving,
            int delivered, int goal, float elapsed, float bed, float shaker,
            float melody, float sparkle)
        {
            Vector4 actual = MusicDirector.TargetsFor(scene, moving, delivered, goal, elapsed);
            Assert.That(Vector4.Distance(actual, new Vector4(bed, shaker, melody, sparkle)),
                Is.LessThan(.00001f));
        }

        [Test]
        public void FailureMufflesOverFourTenthsAndRetryOpensTheFilter()
        {
            Assert.That(MusicDirector.LowPassFor(MusicScene.Failed, 0f), Is.EqualTo(22000f));
            Assert.That(MusicDirector.LowPassFor(MusicScene.Failed, .2f), Is.EqualTo(11350f));
            Assert.That(MusicDirector.LowPassFor(MusicScene.Failed, .4f), Is.EqualTo(700f));
            Assert.That(MusicDirector.LowPassFor(MusicScene.Failed, 2f), Is.EqualTo(700f));
            Assert.That(MusicDirector.LowPassFor(MusicScene.Playing, 0f), Is.EqualTo(22000f));
        }

        [Test]
        public void CrossfadeUsesElapsedTimeInsteadOfFrameCount()
        {
            float whole = MusicDirector.Fade(0f, 1f, .6f);
            float halves = MusicDirector.Fade(MusicDirector.Fade(0f, 1f, .3f), 1f, .3f);
            Assert.That(whole, Is.EqualTo(.63212056f).Within(.00001f));
            Assert.That(halves, Is.EqualTo(whole).Within(.00001f));
        }
    }
}
