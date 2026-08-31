using CatMetro.Presentation.Cats;
using NUnit.Framework;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatMicroMotionTests
    {
        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(123456789u)]
        [TestCase(uint.MaxValue)]
        public void StableSeed_ProducesACadenceInsideTheBlinkRange(uint seed)
        {
            var motion = new CatMicroMotion(seed);

            Assert.That(motion.BlinkInterval, Is.InRange(
                CatMicroMotion.BlinkIntervalMinimum, CatMicroMotion.BlinkIntervalMaximum));
        }

        [Test]
        public void MotionOff_ReturnsAnExactNeutralPose()
        {
            var motion = new CatMicroMotion(42u);

            var pose = motion.Evaluate(1.73f, true, true);

            Assert.That(pose.Bob, Is.EqualTo(0f));
            Assert.That(pose.EyeYScale, Is.EqualTo(1f));
            Assert.That(pose.EarTwitchDegrees, Is.EqualTo(0f));
            Assert.That(pose.ArrivalHeadTurnDegrees, Is.EqualTo(0f));
        }
    }
}
