using NUnit.Framework;
using CatMetro.Application.Retry;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Tests.Validation;

namespace CatMetro.Tests.Retry
{
    // CM-C3 criterion 9 (the determinism keystone) + criterion 1's attribution rules —
    // engine-free, both hosts. Retry is RE-SIMULATION from tick 0 over the same imported level
    // (ADR-0002 §9; no snapshot format exists and none may be created).
    public sealed class RetryDeterminismTests
    {
        private static ImportedLevel L001() => VFixtures.Import(VFixtures.L001Bytes());

        // Criterion 9's hash-equality law, ARMED (review B2: the first cut compared two
        // identical pure calls — referentially transparent, could never fail). Now: a first
        // run genuinely CONTAMINATES (different commands, advanced state), then the RETRIED
        // session PLAYS sequence B and its full observable trajectory must equal a fresh
        // session playing B — and both recorded logs must hash identically through the CM-C1
        // hasher. A mismatch is stop condition 7 — a retry-path defect, never a stale golden.
        [Test]
        public void PostRetry_TrajectoryAndHash_EqualFreshEntry()
        {
            var level = L001();

            // first run: DIFFERENT commands, state advanced well past tick 0
            var firstRun = new GameSession(level);
            firstRun.AdvanceMs(3 * TickInterpolator.TICK_MS);
            firstRun.EnqueueToggle(0); // stamped at tick 3 — sequence A, not B
            firstRun.AdvanceMs(37 * TickInterpolator.TICK_MS);
            Assert.That(firstRun.State.Tick, Is.GreaterThan(0), "the first run really ran");

            // the retried and the fresh session each play SEQUENCE B identically
            var retry = new GameSession(level);
            Assert.That(retry.State.Tick, Is.Zero, "retry starts at tick 0");
            Assert.That(retry.Log.Entries.Count, Is.Zero, "retry starts with an empty log");

            var fresh = new GameSession(level);
            foreach (var s in new[] { retry, fresh })
            {
                s.AdvanceMs(5 * TickInterpolator.TICK_MS);
                s.EnqueueToggle(0); // sequence B: one toggle stamped at tick 5
                s.AdvanceMs(170 * TickInterpolator.TICK_MS);
            }

            // identical observable trajectory, field for field
            Assert.That(retry.State.Tick, Is.EqualTo(fresh.State.Tick));
            Assert.That(retry.State.Deliveries, Is.EqualTo(fresh.State.Deliveries));
            Assert.That(retry.State.Outcome.Kind, Is.EqualTo(fresh.State.Outcome.Kind));
            Assert.That(retry.State.SwitchRoutes, Is.EqualTo(fresh.State.SwitchRoutes));
            Assert.That(retry.State.NodeQueueCounts, Is.EqualTo(fresh.State.NodeQueueCounts));
            Assert.That(retry.State.OverloadTimers, Is.EqualTo(fresh.State.OverloadTimers));
            for (int t = 0; t < retry.State.Trains.Length; t++)
            {
                Assert.That(retry.State.Trains[t].Id, Is.EqualTo(fresh.State.Trains[t].Id));
                Assert.That(retry.State.Trains[t].State, Is.EqualTo(fresh.State.Trains[t].State));
                Assert.That(retry.State.Trains[t].NodeId, Is.EqualTo(fresh.State.Trains[t].NodeId));
            }

            // identical recorded logs, identical replay hash through the CM-C1 law
            Assert.That(retry.Log.Entries.Count, Is.EqualTo(fresh.Log.Entries.Count));
            for (int i = 0; i < retry.Log.Entries.Count; i++)
            {
                Assert.That(retry.Log.Entries[i].SwitchId, Is.EqualTo(fresh.Log.Entries[i].SwitchId));
                Assert.That(retry.Log.Entries[i].Tick, Is.EqualTo(fresh.Log.Entries[i].Tick));
            }
            string hRetry = ReplayHasher.ComputeReplayHash(level.Graph, (ulong)level.Dto.Seed, retry.Log);
            string hFresh = ReplayHasher.ComputeReplayHash(level.Graph, (ulong)level.Dto.Seed, fresh.Log);
            Assert.That(hRetry, Is.EqualTo(hFresh),
                "stop condition 7: a mismatch here is a retry-path defect — STOP");
        }

        // Criterion 1's QueueOverflow rule, driven by a REAL Domain run to the fail tick.
        [Test]
        public void CausalNode_QueueOverflow_IsTheExpiredOverloadNode()
        {
            var imported = VFixtures.Import(OverflowBytes());
            var session = new GameSession(imported);
            session.AdvanceMs(40 * TickInterpolator.TICK_MS);

            Assert.That(session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(session.State.Outcome.Reason, Is.EqualTo(FailReason.QueueOverflow));
            int causal = CauseAttribution.CausalNode(session.State);
            Assert.That(causal, Is.EqualTo(0), "SRC (node 0) raised the failure");
        }

        // Criterion 1's TimeOut rule (A-C3-2, Q-K), IN SUBSTANCE (review S2: the degenerate
        // all-zero case alone let a `return 0` mutant pass). A dummy node sits at index 0 and
        // the queued source at index 1: the rule must pick index 1 — largest queue wins, not
        // lowest-index-by-default.
        [Test]
        public void CausalNode_TimeOut_PicksTheLargestQueue_NotIndexZero()
        {
            var busy = VFixtures.Import(BusyTimeoutBytes());
            var s = new GameSession(busy);
            s.AdvanceMs(20 * TickInterpolator.TICK_MS);
            Assert.That(s.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(s.State.Outcome.Reason, Is.EqualTo(FailReason.TimeOut));
            Assert.That(s.State.NodeQueueCounts[1], Is.GreaterThan(0),
                "the source (index 1) holds a real queue at the fail tick");
            Assert.That(CauseAttribution.CausalNode(s.State), Is.EqualTo(1),
                "A-C3-2: the LARGEST queue wins — a return-0 mutant fails here");
        }

        // ...and the tie limb: all queues equal (zero) → the lowest node id.
        [Test]
        public void CausalNode_TimeOut_TiesBreakToLowestId()
        {
            var quiet = VFixtures.Import(QuietTimeoutBytes());
            var s2 = new GameSession(quiet);
            s2.AdvanceMs(30 * TickInterpolator.TICK_MS);
            Assert.That(s2.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(s2.State.Outcome.Reason, Is.EqualTo(FailReason.TimeOut));
            Assert.That(CauseAttribution.CausalNode(s2.State), Is.EqualTo(0),
                "all queues equal — the tie breaks to the lowest node id");
        }

        private static byte[] BusyTimeoutBytes() =>
            System.Text.Encoding.UTF8.GetBytes(@"{
  ""schemaVersion"": 2, ""id"": ""T905"", ""name"": ""Busy Timeout"", ""seed"": 905,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch"", ""queue""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""AAA"", ""x"": 0, ""y"": 0 },
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9, ""queueCapacity"": 8 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E0"", ""from"": ""AAA"", ""to"": ""J1"", ""travelTicks"": 40 },
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 12 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": 12 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": [
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 14, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 9, ""timeLimitTicks"": 20, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}");

        [Test]
        public void CausalNode_NonFailed_IsAmbiguous()
        {
            var session = new GameSession(L001());
            Assert.That(CauseAttribution.CausalNode(session.State), Is.EqualTo(-1));
        }

        private static byte[] OverflowBytes() =>
            System.Text.Encoding.UTF8.GetBytes(@"{
  ""schemaVersion"": 2, ""id"": ""T902"", ""name"": ""Overflow Pure"", ""seed"": 902,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch"", ""queue""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9, ""queueCapacity"": 4 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 12 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": 12 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": [
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 14, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 8, ""spacingTicks"": 1 },
    { ""tick"": 22, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 4, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}");

        private static byte[] QuietTimeoutBytes() =>
            System.Text.Encoding.UTF8.GetBytes(@"{
  ""schemaVersion"": 2, ""id"": ""T903"", ""name"": ""Quiet Timeout"", ""seed"": 903,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 12 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": 12 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": [
    { ""tick"": 100, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 1, ""timeLimitTicks"": 20, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}");
    }
}
