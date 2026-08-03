using System;
using System.Collections.Generic;
using NUnit.Framework;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    // Criterion 13: seven of the eight authoritative step boundaries
    // (docs/plan/specs/product_spec.md:218-227); step 7 (score/combo) is deferred with scoring
    // (Q-C) and covered only as Score==0/Chain==0 in the digest offset test. Eight NUnit cases:
    // seven boundaries, step 4 carries two (arrival + routing).
    [TestFixture]
    public class StepBoundaryTests
    {
        [Test] // step 1
        public void Step_Commands_SwitchFlipsAtNextTickBoundaryAndCountIncrements()
        {
            var graph = Fixtures.L001Shape();
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 5)); // enqueued during tick 5 -> applies at entry Tick 6
            var seen = new List<(int tick, byte route, int used)>();
            Fixtures.RunThroughTick(graph, Fixtures.L001Seed, log, 7,
                s => seen.Add((s.Tick, s.SwitchRoutes[0], s.SwitchesUsed)));

            foreach (var (tick, route, used) in seen)
            {
                if (tick <= 5) // after processing ticks 0..5 the command has not applied yet
                {
                    Assert.That(route, Is.EqualTo(1), $"route must still be initialRoute after tick {tick - 1}");
                    Assert.That(used, Is.EqualTo(0));
                }
                else // the call with entry Tick 6 processed it at step 1
                {
                    Assert.That(route, Is.EqualTo(0), $"route must have flipped by end of tick {tick - 1}");
                    Assert.That(used, Is.EqualTo(1));
                }
            }
        }

        [Test] // step 2
        public void Step_Waves_EmitsAtAuthoredTicksOnly()
        {
            // L001 wave: tick 8, count 2, spacing 20 -> spawn events at ticks 8 and 28, nowhere else.
            var graph = Fixtures.L001Shape();
            var log = Fixtures.GoldenLog();
            var spawnTicks = new List<int>();
            int prevLive = 0;
            Fixtures.RunThroughTick(graph, Fixtures.L001Seed, log, 29, s =>
            {
                int live = 0;
                foreach (var t in s.Trains) if (t.State != TrainState.None) live++;
                // deliveries also remove trains; count spawns as live-count increases only
                int delivered = s.Deliveries;
                int spawnedSoFar = live + delivered;
                if (spawnedSoFar > prevLive)
                    spawnTicks.Add(s.Tick - 1); // the tick just processed
                prevLive = spawnedSoFar;
            });
            Assert.That(spawnTicks, Is.EqualTo(new[] { 8, 28 }));
        }

        [Test] // step 3
        public void Step_Advance_ProgressIncrementsByOnePerTick()
        {
            var graph = Fixtures.L001Shape();
            var log = Fixtures.GoldenLog();
            var progressByTick = new Dictionary<int, int>();
            Fixtures.RunThroughTick(graph, Fixtures.L001Seed, log, 17, s =>
            {
                foreach (var t in s.Trains)
                    if (t.Id == 1 && t.State == TrainState.OnEdge && t.EdgeId == 0)
                        progressByTick[s.Tick - 1] = t.ProgressTicks;
            });
            // Entered E1 during tick 8 at progress 0; advances by exactly 1 per tick 9..17.
            Assert.That(progressByTick[8], Is.EqualTo(0), "no advance on the entry tick");
            for (int tick = 9; tick <= 17; tick++)
                Assert.That(progressByTick[tick], Is.EqualTo(tick - 8), $"progress at tick {tick}");
        }

        [Test] // step 4 — arrival
        public void Step_NodeArrival_LeavesEdgeAtTravelTicks()
        {
            // No toggle: initialRoute 1 -> E3. Cat 1 arrives J1 exactly at tick 18 (enter 8 + travel 10):
            // it leaves E1, touches J1 (NodeId sticky), and departs on the current route the same
            // step (transient enqueue; the durable-queue case is exercised by the overflow tests).
            var graph = Fixtures.L001Shape();
            var log = new CommandLog(); // empty: route stays E3; stop before any station arrival
            TrainSlot at17 = default, at18 = default;
            Fixtures.RunThroughTick(graph, Fixtures.L001Seed, log, 18, s =>
            {
                foreach (var t in s.Trains)
                    if (t.Id == 1)
                    {
                        if (s.Tick - 1 == 17) at17 = t;
                        if (s.Tick - 1 == 18) at18 = t;
                    }
            });
            Assert.That(at17.EdgeId, Is.EqualTo(0), "still on E1 after tick 17");
            Assert.That(at17.ProgressTicks, Is.EqualTo(9));
            Assert.That(at18.EdgeId, Is.EqualTo(2), "departed J1 on E3 (initialRoute) after tick 18");
            Assert.That(at18.NodeId, Is.EqualTo(1), "NodeId records J1 as last node touched");
            Assert.That(at18.ProgressTicks, Is.EqualTo(0), "fresh edge entry");
        }

        [Test] // step 4 — routing sub-assertion (same boundary, product_spec.md:221)
        public void Step_NodeArrival_RoutesViaCurrentSwitchStateNotInitialRoute()
        {
            var graph = Fixtures.L001Shape();
            var log = Fixtures.GoldenLog(); // toggled during tick 12 -> route E2 at the tick-18 arrival
            TrainSlot at18 = default;
            Fixtures.RunThroughTick(graph, Fixtures.L001Seed, log, 18, s =>
            {
                foreach (var t in s.Trains)
                    if (t.Id == 1 && s.Tick - 1 == 18) at18 = t;
            });
            Assert.That(at18.EdgeId, Is.EqualTo(1), "departed on E2 per toggled SwitchRoutes, not authored initialRoute");
        }

        [Test] // step 5 — station acceptance, match only (non-match is pinned: criterion 14)
        public void Step_StationAcceptance_MatchingColorDeliveredAndSlotCleared()
        {
            var graph = Fixtures.L001Shape();
            var log = Fixtures.GoldenLog();
            int deliveriesAfter30 = -1;
            bool slotCleared = false;
            Fixtures.RunThroughTick(graph, Fixtures.L001Seed, log, 30, s =>
            {
                if (s.Tick - 1 == 30)
                {
                    deliveriesAfter30 = s.Deliveries;
                    foreach (var t in s.Trains)
                        if (t.Id == 1) return;
                    slotCleared = true; // no live slot carries Id 1 any more
                }
            });
            Assert.That(deliveriesAfter30, Is.EqualTo(1), "cat 1 delivered at RED at tick 30 (8 + 10 + 12)");
            Assert.That(slotCleared, Is.True, "delivered train's slot is zeroed (A-C1-10)");
        }

        [Test] // step 6
        public void Step_Overflow_RaisesAtCapacityCancelsOnClearFailsOnExpiry()
        {
            // Raise + expiry -> Failed(QueueOverflow): continuous double emission, drain 1/tick.
            var fail = Fixtures.RunThroughTick(Fixtures.OverflowFailShape(), 7, new CommandLog(), 399);
            Assert.That(fail.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(fail.Outcome.Reason, Is.EqualTo(FailReason.QueueOverflow));
            Assert.That(fail.Overloads, Is.GreaterThanOrEqualTo(1), "raise was counted");
            Assert.That(fail.Tick, Is.LessThanOrEqualTo(20), "expiry at ~16 ticks after the raise");

            // Raise + clear -> cancel, run continues and completes Won.
            bool sawTimer = false;
            var ok = Fixtures.RunThroughTick(Fixtures.OverflowCancelShape(), 7, new CommandLog(), 399,
                s => { if (s.OverloadTimers[0] > 0) sawTimer = true; });
            Assert.That(sawTimer, Is.True, "Overload was raised transiently");
            Assert.That(ok.Outcome.Kind, Is.EqualTo(OutcomeKind.Won), "cleared queue cancels the countdown");
            Assert.That(ok.OverloadTimers[0], Is.EqualTo(0), "timer cleared on cancel");
        }

        [Test] // step 8
        public void Step_WinTime_DeliveriesWinsAndTimeoutFails()
        {
            var won = Fixtures.RunThroughTick(Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog(), 159);
            Assert.That(won.Outcome.Kind, Is.EqualTo(OutcomeKind.Won), "win.deliveries=2 reached at tick 50");
            Assert.That(won.Deliveries, Is.EqualTo(2));
            Assert.That(won.Tick, Is.EqualTo(51), "terminal at the tick-50 step (entry Tick 50 -> 51)");

            var timedOut = Fixtures.RunThroughTick(Fixtures.NoWaveShape(200), 1, new CommandLog(), 400);
            Assert.That(timedOut.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(timedOut.Outcome.Reason, Is.EqualTo(FailReason.TimeOut));
            Assert.That(timedOut.Tick, Is.EqualTo(200), "fails when the tick counter reaches timeLimitTicks");
        }
    }
}
