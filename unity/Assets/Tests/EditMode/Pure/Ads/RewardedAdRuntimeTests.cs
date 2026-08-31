using CatMetro.Services.Ads;
using NUnit.Framework;

namespace CatMetro.Tests.Ads
{
    public sealed class RewardedAdRuntimeTests
    {
        [TearDown]
        public void TearDown() => RewardedAdRuntime.ResetForTests();

        [Test]
        public void CurrentIsNeverNullAndFailsClosedBeforeInstall()
        {
            RewardedAdRuntime.ResetForTests();

            Assert.That(RewardedAdRuntime.Current, Is.Not.Null);
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(RewardedAdRuntime.Current.CanShow("p0"), Is.False);
            Assert.That(RewardedAdRuntime.Current.Show("p0"),
                Is.EqualTo(RewardedShowOutcome.Unavailable));
        }

        [Test]
        public void InstallPublishesNonNullImplementationAndNullDoesNotReplaceIt()
        {
            RewardedAdRuntime.ResetForTests();
            using var coordinator = RewardedAdFixtures.Coordinator();
            int installed = 0;
            RewardedAdRuntime.Installed += () => installed++;

            RewardedAdRuntime.Install(coordinator);
            RewardedAdRuntime.Install(null);

            Assert.That(RewardedAdRuntime.Current, Is.SameAs(coordinator));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.True);
            Assert.That(installed, Is.EqualTo(1));
        }

        [Test]
        public void ResetRestoresNoOpAndClearsInstalledSubscribers()
        {
            int installed = 0;
            RewardedAdRuntime.ResetForTests();
            RewardedAdRuntime.Installed += () => installed++;
            RewardedAdRuntime.ResetForTests();
            using var coordinator = RewardedAdFixtures.Coordinator();

            RewardedAdRuntime.Install(coordinator);

            Assert.That(installed, Is.Zero);
            Assert.That(RewardedAdRuntime.Current, Is.SameAs(coordinator));
        }
    }
}
