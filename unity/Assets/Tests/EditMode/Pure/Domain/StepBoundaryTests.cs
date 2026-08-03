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

            // Callback states carry the POST-increment tick: entry `tick` == processed tick + 1.
            // Command enqueued during tick 5 applies at step 1 of processing tick 6, i.e. it is
            // first visible in the tick==7 entry. (Same semantics as the passing twin test
            // SingleCommand_AppliesAtNextTickBoundary; this mapping had an off-by-one in the red
            // phase, repaired without changing the assertion set.)
            int before = 0, after = 0; // review F8: both branches must actually execute
            foreach (var (tick, route, used) in seen)
            {
                if (tick <= 6) // states after processing ticks 0..5: not applied yet
                {
                    Assert.That(route, Is.EqualTo(1), $"route must still be initialRoute after processing tick {tick - 1}");
                    Assert.That(used, Is.EqualTo(0));
                    before++;
                }
                else // states after processing ticks 6..: applied at step 1 of tick 6
                {
                    Assert.That(route, Is.EqualTo(0), $"route must have flipped after processing tick {tick - 1}");
                    Assert.That(used, Is.EqualTo(1));
                    after++;
                }
            }
            Assert.That(seen.Count, Is.EqualTo(8), "fixture shortened — observations missing (F8)");
            Assert.That(before, Is.GreaterThan(0).And.LessThan(8), "both sides of the boundary observed");
            Assert.That(after, Is.GreaterThan(0), "the applied branch executed");
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

        [Test] // step 6 — review F3: the 16-tick countdown and the exact expiry tick are pinned
        public void Step_Overflow_RaisesAtCapacityCancelsOnClearFailsOnExpiry()
        {
            // Raise + expiry -> Failed(QueueOverflow). The raise happens during processing tick 0
            // (double emission at tick 0), so the first observed post-state (Tick==1) must carry
            // the full 16-tick timer, and expiry lands exactly 17 post-ticks after the raise
            // (16 decrements on processing ticks 1..16 -> fail at processing tick 16 -> Tick 17).
            int raisePostTick = -1; short timerAtRaise = -1;
            var fail = Fixtures.RunThroughTick(Fixtures.OverflowFailShape(), 7, new CommandLog(), 399, s =>
            {
                if (raisePostTick < 0 && s.OverloadTimers[0] > 0) { raisePostTick = s.Tick; timerAtRaise = s.OverloadTimers[0]; }
            });
            Assert.That(raisePostTick, Is.EqualTo(1), "raised during processing tick 0");
            Assert.That(timerAtRaise, Is.EqualTo(16), "CM-R02.5: the countdown is 16 ticks (2 s), exactly");
            Assert.That(fail.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(fail.Outcome.Reason, Is.EqualTo(FailReason.QueueOverflow));
            Assert.That(fail.Overloads, Is.GreaterThanOrEqualTo(1), "raise was counted");
            Assert.That(fail.Tick, Is.EqualTo(17), "expiry exactly 16 decrements after the raise");

            // Raise + clear -> cancel, run continues and completes Won.
            bool sawFullTimer = false;
            var ok = Fixtures.RunThroughTick(Fixtures.OverflowCancelShape(), 7, new CommandLog(), 399,
                s => { if (s.OverloadTimers[0] == 16) sawFullTimer = true; });
            Assert.That(sawFullTimer, Is.True, "Overload was raised with the full 16-tick timer");
            Assert.That(ok.Outcome.Kind, Is.EqualTo(OutcomeKind.Won), "cleared queue cancels the countdown");
            Assert.That(ok.OverloadTimers[0], Is.EqualTo(0), "timer cleared on cancel");
        }

        [Test] // review F2: the zero-dwell pass-through rule (A-C1-8 iv) is pinned, not silent.
        public void Step_NodeArrival_PassThroughLeavesQueueUntouched()
        {
            // Golden-defining semantic: an arrival whose outgoing mouth is free departs in the
            // same step and never touches the queue (product_spec.md:224 "departs immediately").
            // J1's queue must be empty at EVERY observed tick of the L001 run through tick 19.
            int maxQueueAtJ1 = 0;
            TrainSlot at18 = default;
            Fixtures.RunThroughTick(Fixtures.L001Shape(), Fixtures.L001Seed, new CommandLog(), 19, s =>
            {
                if (s.NodeQueueCounts[1] > maxQueueAtJ1) maxQueueAtJ1 = s.NodeQueueCounts[1];
                foreach (var t in s.Trains) if (t.Id == 1 && s.Tick - 1 == 18) at18 = t;
            });
            Assert.That(maxQueueAtJ1, Is.EqualTo(0), "zero dwell: the pass-through arrival never enqueues");
            Assert.That(at18.State, Is.EqualTo(TrainState.OnEdge), "already re-entered an edge in the same step");
            Assert.That(at18.EdgeId, Is.EqualTo(2), "departed on the current route within the arrival step");
        }

        [Test] // review F9: single-count waves are spacing-independent; multi-count zero spacing is loud
        public void Step_Waves_SingleCountEmitsOnceAndZeroSpacingMultiCountThrows()
        {
            var single = new LevelGraph(
                "FX-1W", 2, new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 5 },
                new[] { 0 },
                new int[0][], new int[0], new byte[0],
                new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new[] { 3 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 0 }, // count 1, spacing 0
                1, 100, 8, 1);
            var end = Fixtures.RunThroughTick(single, 1, new CommandLog(), 99);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won), "the single cat spawned at tick 3 and was delivered");
            Assert.That(end.Deliveries, Is.EqualTo(1));

            Assert.Throws<ArgumentException>(() => new LevelGraph(
                "FX-0SP", 2, new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 5 },
                new[] { 0 },
                new int[0][], new int[0], new byte[0],
                new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new[] { 0 }, new[] { CatColor.Red }, new[] { 2 }, new[] { 0 }, // count 2, spacing 0
                2, 100, 8, 2), "multi-count zero spacing must be refused loudly, never silently emit nothing");
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
