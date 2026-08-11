using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Daily
{
    public sealed class DailyBoardFactoryTests
    {
        [Test]
        public void ContentAssembly_HasExactlyOneConcreteDailyBoardFactory()
        {
            var implementations = typeof(DailyBoardFactory).Assembly.GetTypes()
                .Where(type => type.Namespace == typeof(DailyBoardFactory).Namespace
                    && type.IsClass
                    && !type.IsAbstract
                    && typeof(IBoardFactory).IsAssignableFrom(type))
                .ToArray();

            Assert.That(implementations, Is.EqualTo(new[] { typeof(DailyBoardFactory) }),
                "direct and inherited IBoardFactory implementations are both production owners");
        }

        [TestCase("2026-08-24", 0.30)]
        [TestCase("2026-08-25", 0.35)]
        [TestCase("2026-08-26", 0.38)]
        [TestCase("2026-08-27", 0.42)]
        [TestCase("2026-08-28", 0.45)]
        [TestCase("2026-08-29", 0.50)]
        [TestCase("2026-08-30", 0.55)]
        public void Difficulty_UsesUtcWeekdayTable(string date, double expected) =>
            Assert.That(DailyDifficulty.Target(date), Is.EqualTo(expected));

        [TestCase("2026-08-24", 1449106418u, 0, 0.30)]
        [TestCase("2026-08-25", 3466517912u, 3, 0.35)]
        [TestCase("2026-08-27", 69047575u, 0, 0.42)]
        public void Build_UsesDailyIdentityAndEconomy(
            string dateKey, uint seed, int k, double difficulty)
        {
            var factory = new DailyBoardFactory();
            var dto = factory.Build(seed, dateKey, k);

            Assert.That(factory, Is.InstanceOf<IBoardFactory>());
            Assert.That(factory, Is.InstanceOf<IDailyFallbackBoardFactory>());
            Assert.That(dto.SchemaVersion, Is.EqualTo(2));
            Assert.That(dto.Id, Is.EqualTo("L800"));
            Assert.That(dto.Name, Is.EqualTo("Daily Line"));
            Assert.That(dto.Seed, Is.EqualTo(seed));
            Assert.That(dto.Meta.Band, Is.EqualTo("daily"));
            Assert.That(dto.Meta.DifficultyTarget, Is.EqualTo(difficulty));
            CollectionAssert.AreEqual(new[] { "switch", "queue" }, dto.Meta.Mechanics.ToArray());
            Assert.That(dto.Meta.NewMechanic, Is.Null);
            Assert.That(dto.Meta.TeachingGoal,
                Is.EqualTo("Route each cat to the matching station"));
            Assert.That(dto.Meta.MinActionWindowTicks, Is.EqualTo(12));
            Assert.That(dto.Meta.AuthoredBy, Is.EqualTo("generator+validator"));
            Assert.That(dto.Meta.HasValidatedAt, Is.False);
            Assert.That(dto.Economy.BaseTickets, Is.EqualTo(100));
            Assert.That(dto.Economy.PerfectBonus, Is.Zero);
        }

        [Test]
        public void QueuedFork_AppliesMirrorRouteAndColorTransforms()
        {
            var dto = new DailyBoardFactory().Build(1449106418u, "2026-08-24", 0);

            Assert.That(Nodes(dto), Is.EqualTo(
                "SRC:3,9,q5|GATE:3,7,-|HOLD:1,7,q1|J1:3,5,-|RED:5,1,-|BLU:1,1,-"));
            Assert.That(Edges(dto), Is.EqualTo(
                "E1:SRC>GATE@6|E2:GATE>J1@6|E3:GATE>HOLD@20|E4:J1>RED@8|E5:J1>BLU@8"));
            Assert.That(Sources(dto), Is.EqualTo("SRC:green,blue"));
            Assert.That(Stations(dto), Is.EqualTo("RED:green:c8|BLU:blue:c8"));
            Assert.That(Switches(dto), Is.EqualTo("S1@GATE:E2,E3:i0|S2@J1:E5,E4:i0"));
            Assert.That(Waves(dto), Is.EqualTo("8:SRC:green:3:8|64:SRC:blue:2:16"));
            AssertWin(dto, 5, 130, 2, 400, 600);
        }

        [Test]
        public void AlternatingFork_AppliesMirrorRouteAndColorTransforms()
        {
            var dto = new DailyBoardFactory().Build(3466517912u, "2026-08-25", 3);

            Assert.That(Nodes(dto), Is.EqualTo("SRC:3,9,q3|J1:3,6,-|RED:5,2,-|BLU:1,2,-"));
            Assert.That(Edges(dto), Is.EqualTo("E1:SRC>J1@8|E2:J1>RED@12|E3:J1>BLU@12"));
            Assert.That(Sources(dto), Is.EqualTo("SRC:red,yellow"));
            Assert.That(Stations(dto), Is.EqualTo("RED:red:c6|BLU:yellow:c6"));
            Assert.That(Switches(dto), Is.EqualTo("S1@J1:E3,E2:i1"));
            Assert.That(Waves(dto), Is.EqualTo(
                "8:SRC:red:2:16|48:SRC:yellow:2:16|88:SRC:red:2:16|128:SRC:yellow:2:16"));
            AssertWin(dto, 8, 260, 4, 700, 950);
        }

        [Test]
        public void Cascade_AppliesUnmirroredMixedRoutesAndColorTransforms()
        {
            var dto = new DailyBoardFactory().Build(69047575u, "2026-08-27", 0);

            Assert.That(Nodes(dto), Is.EqualTo(
                "SRC:3,10,q4|J1:3,7,q3|RED:0,1,-|J2:5,5,q3|BLU:4,1,-|YEL:6,1,-"));
            Assert.That(Edges(dto), Is.EqualTo(
                "E1:SRC>J1@8|E2:J1>RED@12|E3:J1>J2@8|E4:J2>BLU@10|E5:J2>YEL@10"));
            Assert.That(Sources(dto), Is.EqualTo("SRC:red,yellow,blue"));
            Assert.That(Stations(dto), Is.EqualTo("RED:red:c6|BLU:yellow:c6|YEL:blue:c6"));
            Assert.That(Switches(dto), Is.EqualTo("S1@J1:E2,E3:i1|S2@J2:E5,E4:i1"));
            Assert.That(Waves(dto), Is.EqualTo(
                "8:SRC:red:2:16|56:SRC:yellow:2:16|104:SRC:blue:2:16"));
            AssertWin(dto, 6, 180, 3, 500, 750);
        }

        [Test]
        public void BuildFallback_UsesFixedShapeAndShuffleOnlyColorMap()
        {
            var dto = new DailyBoardFactory().BuildFallback(1449106418u, "2026-08-24");

            Assert.That(Nodes(dto), Is.EqualTo("SRC:3,8,-|J1:3,5,-|RED:1,1,-|BLU:5,1,-"));
            Assert.That(Edges(dto), Is.EqualTo("E1:SRC>J1@8|E2:J1>RED@12|E3:J1>BLU@12"));
            Assert.That(Sources(dto), Is.EqualTo("SRC:green"));
            Assert.That(Stations(dto), Is.EqualTo("RED:green:c8|BLU:yellow:c8"));
            Assert.That(Switches(dto), Is.EqualTo("S1@J1:E2,E3:i1"));
            Assert.That(Waves(dto), Is.EqualTo("8:SRC:green:2:20"));
            CollectionAssert.AreEqual(new[] { "switch" }, dto.Meta.Mechanics.ToArray());
            Assert.That(dto.Meta.DifficultyTarget, Is.EqualTo(0.30));
            Assert.That(dto.Economy.BaseTickets, Is.EqualTo(100));
            Assert.That(dto.Economy.PerfectBonus, Is.Zero);
            AssertWin(dto, 2, 160, 1, 200, 300);
        }

        [Test]
        public void RepeatedBuilds_ReturnFreshDtoArrays()
        {
            var factory = new DailyBoardFactory();
            var first = factory.Build(1449106418u, "2026-08-24", 0);
            var second = factory.Build(1449106418u, "2026-08-24", 0);

            Assert.That(first.Nodes.Equals(second.Nodes), Is.False);
            Assert.That(first.Edges.Equals(second.Edges), Is.False);
            Assert.That(first.Sources.Equals(second.Sources), Is.False);
            Assert.That(first.Stations.Equals(second.Stations), Is.False);
            Assert.That(first.Switches.Equals(second.Switches), Is.False);
            Assert.That(first.Waves.Equals(second.Waves), Is.False);
            Assert.That(first.Meta.Mechanics.Equals(second.Meta.Mechanics), Is.False);
            Assert.That(first.Sources.Span[0].AllowedColors.Equals(
                second.Sources.Span[0].AllowedColors), Is.False);
            Assert.That(first.Stations.Span[0].Accepts.Equals(
                second.Stations.Span[0].Accepts), Is.False);
            Assert.That(first.Switches.Span[0].Routes.Equals(
                second.Switches.Span[0].Routes), Is.False);
            Assert.That(Hash(first), Is.EqualTo(Hash(second)));
        }

        [TestCase("2026-08-24", 1449106418u, 0,
            "e626bac94f5a2683ff243a5ce5f5d99ce25ccc76b1997669b232f73179f0f9c8")]
        [TestCase("2026-08-25", 3466517912u, 3,
            "43cb5a7f978ef98e19c8025706bac2e7a5af68057b16570e0d280b1d2ba1a4a8")]
        [TestCase("2026-08-27", 69047575u, 0,
            "3655bb161fb4420660d9d002753666686c72fbe4b5878bda4a96f4b6f728f6df")]
        public void CanonicalJson_MatchesIndependentLiteralHash(
            string dateKey, uint seed, int k, string expectedHash)
        {
            Assert.That(Hash(new DailyBoardFactory().Build(seed, dateKey, k)),
                Is.EqualTo(expectedHash));
        }

        [Test]
        public void FallbackCanonicalJson_MatchesIndependentLiteralHash()
        {
            Assert.That(Hash(new DailyBoardFactory().BuildFallback(1449106418u, "2026-08-24")),
                Is.EqualTo("7f6b851b77b06a55912ccb450736965bb03026523971b54e98a8f4cbc8a971e9"));
        }

        private static string Nodes(LevelDto dto) => string.Join("|", dto.Nodes.ToArray().Select(n =>
            n.Id + ":" + n.X + "," + n.Y + "," + (n.HasQueueCapacity ? "q" + n.QueueCapacity : "-")));

        private static string Edges(LevelDto dto) => string.Join("|", dto.Edges.ToArray().Select(e =>
            e.Id + ":" + e.From + ">" + e.To + "@" + e.TravelTicks));

        private static string Sources(LevelDto dto) => string.Join("|", dto.Sources.ToArray().Select(s =>
            s.NodeId + ":" + string.Join(",", s.AllowedColors.ToArray())));

        private static string Stations(LevelDto dto) => string.Join("|", dto.Stations.ToArray().Select(s =>
            s.NodeId + ":" + string.Join(",", s.Accepts.ToArray()) + ":c" + s.Capacity));

        private static string Switches(LevelDto dto) => string.Join("|", dto.Switches.ToArray().Select(s =>
            s.Id + "@" + s.NodeId + ":" + string.Join(",", s.Routes.ToArray()) + ":i" + s.InitialRoute));

        private static string Waves(LevelDto dto) => string.Join("|", dto.Waves.ToArray().Select(w =>
            w.Tick + ":" + w.SourceNode + ":" + w.Color + ":" + w.Count + ":" + w.SpacingTicks));

        private static void AssertWin(LevelDto dto, int deliveries, int limit, int perfect,
            int two, int three)
        {
            Assert.That(dto.Win.Deliveries, Is.EqualTo(deliveries));
            Assert.That(dto.Win.TimeLimitTicks, Is.EqualTo(limit));
            Assert.That(dto.Win.PerfectMaxSwitches, Is.EqualTo(perfect));
            Assert.That(dto.Win.Stars.Two, Is.EqualTo(two));
            Assert.That(dto.Win.Stars.Three, Is.EqualTo(three));
        }

        private static string Hash(LevelDto dto)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(DailyBoardJson.Serialize(dto));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
