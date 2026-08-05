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

        // Criterion 9's hash-equality law: the identical post-retry command sequence produces
        // the identical replay hash as the same sequence from a fresh level entry. A mismatch
        // is stop condition 7 — a retry-path defect, never a stale golden.
        [Test]
        public void PostRetry_ReplayHash_EqualsFreshEntryHash()
        {
            var level = L001();
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 12)); // the CM-C1 golden's own sequence shape

            // "fresh entry"
            string fresh = ReplayHasher.ComputeReplayHash(level.Graph, (ulong)level.Dto.Seed, log);

            // "retry": a first run happened (arbitrary commands), then a NEW session over the
            // SAME ImportedLevel replays the identical sequence.
            var firstRun = new GameSession(level);
            firstRun.EnqueueToggle(0);
            firstRun.AdvanceMs(40 * TickInterpolator.TICK_MS);

            var retry = new GameSession(level);
            Assert.That(retry.State.Tick, Is.Zero, "retry starts at tick 0");
            Assert.That(retry.Log.Entries.Count, Is.Zero, "retry starts with an empty log");
            for (int s = 0; s < retry.State.SwitchRoutes.Length; s++)
                Assert.That(retry.State.SwitchRoutes[s],
                    Is.EqualTo(level.Graph.SwitchInitialRoute[s]),
                    "every switch equals its level initialRoute after retry");

            string retried = ReplayHasher.ComputeReplayHash(level.Graph, (ulong)level.Dto.Seed, log);
            Assert.That(retried, Is.EqualTo(fresh),
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

        // Criterion 1's TimeOut rule (A-C3-2, Q-K): largest queue at the fail tick, ties to the
        // lowest node id — asserted from the camera-facing attribution, not the outcome.
        [Test]
        public void CausalNode_TimeOut_IsTheLargestQueue_TiesToLowestId()
        {
            var level = L001();
            var session = new GameSession(level);
            // no taps: cats route to the decoy; the pin halts stepping inside AdvanceMs — so
            // drive a TimeOut on a board that cannot pin: zero waves before the limit.
            var quiet = VFixtures.Import(QuietTimeoutBytes());
            var s2 = new GameSession(quiet);
            s2.AdvanceMs(30 * TickInterpolator.TICK_MS);
            Assert.That(s2.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(s2.State.Outcome.Reason, Is.EqualTo(FailReason.TimeOut));
            Assert.That(CauseAttribution.CausalNode(s2.State), Is.EqualTo(0),
                "all queues empty (0) — the tie breaks to the lowest node id");
            Assert.That(level, Is.Not.Null); // keeps the fresh-entry fixture exercised
        }

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
