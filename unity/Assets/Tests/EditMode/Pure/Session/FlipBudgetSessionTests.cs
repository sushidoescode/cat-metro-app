using System.IO;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Tests.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Session
{
    [TestFixture]
    public sealed class FlipBudgetSessionTests
    {
        [Test]
        public void FlipStatusCountsACommittedTapBeforeTheSimulationAppliesIt()
        {
            var session = L001Session();

            session.EnqueueToggle(0);

            Assert.That(session.State.SwitchesUsed, Is.Zero);
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1));
            Assert.That(session.FlipStatus.RemainingToPerfect, Is.Zero);

            session.AdvanceMs(125);
            Assert.That(session.State.SwitchesUsed, Is.Zero, "the first step is uncommandable");
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1));

            session.AdvanceMs(125);
            Assert.That(session.State.SwitchesUsed, Is.EqualTo(1));
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1), "applied and committed counts converge");
        }

        [Test]
        public void FlipStatusAfterWinIgnoresAToggleThatCanNeverBeApplied()
        {
            var session = L001Session();
            var golden = Fixtures.GoldenLog().Entries.ToArray();

            while (session.State.Outcome.Kind == OutcomeKind.Running)
            {
                foreach (var command in golden)
                    if (command.Tick == session.State.Tick)
                        session.EnqueueToggle(command.SwitchId);
                session.AdvanceMs(125);
            }

            Assert.That(session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            int appliedAtWin = session.State.SwitchesUsed;

            session.EnqueueToggle(0);

            Assert.That(session.FlipStatus.Used, Is.EqualTo(appliedAtWin),
                "terminal rating must ignore commands the stopped simulation cannot apply");
        }

        private static GameSession L001Session()
        {
            string path = Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json");
            var import = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(import.Ok, Is.True, import.Error?.ToString());
            return new GameSession(import.Value);
        }
    }
}
