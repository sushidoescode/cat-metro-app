using CatMetro.Application.Session;
using CatMetro.Domain;
using CatMetro.Tests.Validation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Session
{
    public sealed class GameSessionOccupantGenerationTests
    {
        [Test]
        public void SameColourRefill_IncrementsSlotGenerationAcrossAMultiTickHitch()
        {
            var session = new GameSession(VFixtures.Import(SameColourReuseLevel()));
            Assert.That(session.TrainOccupantGeneration(0), Is.Zero);

            session.AdvanceMs(TickInterpolator.TICK_MS); // tick 0: first cat emits
            Assert.That(session.State.Trains[0].Id, Is.EqualTo(1));
            int firstGeneration = session.TrainOccupantGeneration(0);
            Assert.That(firstGeneration, Is.EqualTo(1));
            Assert.That(session.TrainOccupantSpawnNode(0), Is.EqualTo(0));
            Assert.That(session.TrainOccupantSpawnEdge(0), Is.EqualTo(0));

            // tick 2 delivers; tick 3 refills the same fixed slot; tick 4 advances the
            // replacement. PrevTrains is therefore already live after the hitch, so endpoint
            // ID/colour plus the final interpolation snapshot cannot distinguish the cats.
            session.AdvanceMs(4 * TickInterpolator.TICK_MS);

            Assert.That(session.State.Deliveries, Is.EqualTo(1));
            Assert.That(session.State.Trains[0].Id, Is.EqualTo(1));
            Assert.That(session.State.Trains[0].Color, Is.EqualTo(CatColor.Red));
            Assert.That(session.PrevTrains[0].Id, Is.EqualTo(1));
            Assert.That(session.TrainOccupantGeneration(0), Is.EqualTo(firstGeneration + 1));
            Assert.That(session.TrainDeliveryGeneration(0), Is.EqualTo(1));
            Assert.That(session.TrainDeliveryNode(0), Is.EqualTo(1));
        }

        [Test]
        public void TwoCompleteLifecycles_ExposeTwoDeliveriesWhenTheFinalSlotIsEmpty()
        {
            var session = new GameSession(VFixtures.Import(TwoCollapsedLifecyclesLevel()));
            session.AdvanceMs(TickInterpolator.TICK_MS); // tick 0: A emits

            session.AdvanceMs(3 * TickInterpolator.TICK_MS); // A delivers, B emits, B delivers

            Assert.That(session.State.Trains[0].State, Is.EqualTo(TrainState.None));
            Assert.That(session.State.Deliveries, Is.EqualTo(2));
            Assert.That(session.TrainOccupantGeneration(0), Is.EqualTo(2));
            Assert.That(session.TrainOccupantSpawnNode(0), Is.EqualTo(0));
            Assert.That(session.TrainOccupantSpawnEdge(0), Is.EqualTo(0));
            Assert.That(session.TrainDeliveryGeneration(0), Is.EqualTo(2));
            Assert.That(session.TrainDeliveryNode(0), Is.EqualTo(1));
        }

        [Test]
        public void SameTickSecondEmission_RecordsSourceNodeAndItsSelectedOutgoingEdge()
        {
            var session = new GameSession(VFixtures.Import(SourceQueueWaitingLevel()));

            session.AdvanceMs(TickInterpolator.TICK_MS);

            Assert.That(session.State.Trains[1].State, Is.EqualTo(TrainState.AtNode));
            Assert.That(session.State.Trains[1].NodeId, Is.EqualTo(0));
            Assert.That(session.TrainOccupantSpawnNode(1), Is.EqualTo(0));
            Assert.That(session.TrainOccupantSpawnEdge(1), Is.EqualTo(0),
                "queued presentation uses the same source edge normal as direct emission");
        }

        private static byte[] SameColourReuseLevel() => VFixtures.Level(level =>
        {
            level["meta"]["mechanics"] = new JArray();
            level["meta"]["newMechanic"] = null;
            level["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 2), VFixtures.Node("RED", 0, 0));
            level["board"]["edges"] = new JArray(
                VFixtures.Edge("E1", "SRC", "RED", 2));
            level["sources"] = new JArray(new JObject
            {
                ["nodeId"] = "SRC", ["allowedColors"] = new JArray("red"),
            });
            level["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            level["switches"] = new JArray();
            level["waves"] = new JArray(VFixtures.Wave(0, "red", 2, 3));
            level["win"]["deliveries"] = 2;
            level["win"]["timeLimitTicks"] = 20;
        });

        private static byte[] TwoCollapsedLifecyclesLevel() => VFixtures.Level(level =>
        {
            level["meta"]["mechanics"] = new JArray();
            level["meta"]["newMechanic"] = null;
            level["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 1), VFixtures.Node("RED", 0, 0));
            level["board"]["edges"] = new JArray(
                VFixtures.Edge("E1", "SRC", "RED", 1));
            level["sources"] = new JArray(new JObject
            {
                ["nodeId"] = "SRC", ["allowedColors"] = new JArray("red"),
            });
            level["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            level["switches"] = new JArray();
            level["waves"] = new JArray(VFixtures.Wave(0, "red", 2, 2));
            level["win"]["deliveries"] = 2;
            level["win"]["timeLimitTicks"] = 20;
        });

        private static byte[] SourceQueueWaitingLevel() => VFixtures.Level(level =>
        {
            level["meta"]["mechanics"] = new JArray("queue");
            level["meta"]["newMechanic"] = null;
            level["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 2), VFixtures.Node("RED", 0, 0));
            level["board"]["edges"] = new JArray(
                VFixtures.Edge("E1", "SRC", "RED", 3));
            level["sources"] = new JArray(new JObject
            {
                ["nodeId"] = "SRC", ["allowedColors"] = new JArray("red"),
            });
            level["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            level["switches"] = new JArray();
            level["waves"] = new JArray(
                VFixtures.Wave(0, "red", 1, 1),
                VFixtures.Wave(0, "red", 1, 1));
            level["win"]["deliveries"] = 2;
            level["win"]["timeLimitTicks"] = 20;
        });
    }
}
