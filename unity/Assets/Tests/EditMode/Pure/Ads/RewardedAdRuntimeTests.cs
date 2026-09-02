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
        public void ChangedPublishesInstallAndSuccessfulIdentityUninstall_WithoutRedefiningInstalled()
        {
            RewardedAdRuntime.ResetForTests();
            using var coordinator = RewardedAdFixtures.Coordinator();
            using var foreign = RewardedAdFixtures.Coordinator();
            int installed = 0;
            int changed = 0;
            RewardedAdRuntime.Installed += () => installed++;
            RewardedAdRuntime.Changed += () => changed++;

            RewardedAdRuntime.Install(coordinator);
            Assert.That(RewardedAdRuntime.Uninstall(foreign), Is.False);
            Assert.That(RewardedAdRuntime.Uninstall(coordinator), Is.True);

            Assert.That(installed, Is.EqualTo(1),
                "Installed remains an install-only publication");
            Assert.That(changed, Is.EqualTo(2),
                "only the installed identity and its successful uninstall change the runtime");
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(RewardedAdRuntime.Current.CanShow("p0"), Is.False);
        }

        [Test]
        public void ResetRestoresNoOpAndClearsInstalledSubscribers()
        {
            int installed = 0;
            int changed = 0;
            RewardedAdRuntime.ResetForTests();
            RewardedAdRuntime.Installed += () => installed++;
            RewardedAdRuntime.Changed += () => changed++;
            RewardedAdRuntime.ResetForTests();
            using var coordinator = RewardedAdFixtures.Coordinator();

            RewardedAdRuntime.Install(coordinator);

            Assert.That(installed, Is.Zero);
            Assert.That(changed, Is.Zero);
            Assert.That(RewardedAdRuntime.Current, Is.SameAs(coordinator));
        }
    }
}
