using System;
using NUnit.Framework;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    [TestFixture]
    public class SimConstantsAndTickTests
    {
        [Test]
        public void Constants_Are_8tps_125ms()
        {
            Assert.That(SimConstants.TicksPerSecond, Is.EqualTo(8));
            Assert.That(SimConstants.TickMilliseconds, Is.EqualTo(125));
        }

        // Criterion 5: stepping N times from tick 0 leaves state.Tick == N.
        [TestCase(1)]
        [TestCase(8)]
        [TestCase(100)]
        public void Stepping_N_Times_Leaves_Tick_N(int n)
        {
            var state = SimulationState.CreateInitial(Fixtures.NoWaveShape(timeLimitTicks: 200), 1);
            for (int i = 0; i < n; i++)
                Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(state.Tick, Is.EqualTo(n));
        }
    }

    [TestFixture]
    public class Pcg32Tests
    {
        // Criterion 7a: same seed -> identical sequence over 2000 draws (and non-degenerate).
        [Test]
        public void SameSeed_IdenticalSequence_2000Draws()
        {
            var a = new Pcg32(12345, SimulationState.RngSequence);
            var b = new Pcg32(12345, SimulationState.RngSequence);
            var distinct = new System.Collections.Generic.HashSet<uint>();
            for (int i = 0; i < 2000; i++)
            {
                uint va = a.Next(), vb = b.Next();
                Assert.That(vb, Is.EqualTo(va), $"draw {i} diverged");
                distinct.Add(va);
            }
            Assert.That(distinct.Count, Is.GreaterThan(1), "degenerate generator");
        }

        // Criterion 7b: the RNG is part of the state: one draw changes the digest with all other
        // fields held equal (ADR-0002 §4).
        [Test]
        public void OneDraw_ChangesDigest()
        {
            var state = SimulationState.CreateInitial(Fixtures.L001Shape(), Fixtures.L001Seed);
            var before = new byte[state.DigestLength()];
            state.WriteDigest(before);
            state.Rng.Next();
            var after = new byte[state.DigestLength()];
            state.WriteDigest(after);
            Assert.That(after, Is.Not.EqualTo(before));
        }
    }

    [TestFixture]
    public class DigestTests
    {
        // Criterion 8a: DigestLength(nSwitches, nNodes, nTrainsMax, qCap) on three shapes.
        [Test]
        public void DigestLength_ThreeShapes()
        {
            Assert.That(SimulationState.DigestLength(1, 4, 2, 8), Is.EqualTo(143), "L001 shape");
            Assert.That(SimulationState.DigestLength(0, 2, 1, 4), Is.EqualTo(78), "zero-switch shape");
            Assert.That(SimulationState.DigestLength(2, 6, 5, 8), Is.EqualTo(212), "two-switch shape");

            foreach (var (graph, expected) in new[] {
                (Fixtures.L001Shape(), 143), (Fixtures.ZeroSwitchShape(), 78), (Fixtures.TwoSwitchShape(), 212) })
            {
                var s = SimulationState.CreateInitial(graph, 1);
                var buf = new byte[expected];
                s.WriteDigest(buf); // must fill exactly `expected` bytes without throwing
                Assert.That(s.DigestLength(), Is.EqualTo(expected));
            }
        }

        // Criterion 8b: the offset table on the L001-shape initial state — the digest layout IS
        // the contract (ADR-0002 §7; overview.md:312-320; A-C1-10 slot conventions).
        [Test]
        public void OffsetTable_L001Shape()
        {
            var state = SimulationState.CreateInitial(Fixtures.L001Shape(), Fixtures.L001Seed);
            state.Deliveries = 7;            // marker values prove offsets, not just zeros
            state.SwitchesUsed = 3;
            state.OverloadTimers[2] = 9;
            var d = new byte[143];
            state.WriteDigest(d);

            Assert.That(BitConverter.ToInt32(d, 0), Is.EqualTo(0), "Tick @0");
            Assert.That(BitConverter.ToInt32(d, 4), Is.EqualTo(0), "Score @4");
            Assert.That(BitConverter.ToInt32(d, 8), Is.EqualTo(0), "Chain @8");
            Assert.That(BitConverter.ToInt32(d, 12), Is.EqualTo(7), "Deliveries @12");
            Assert.That(BitConverter.ToInt32(d, 16), Is.EqualTo(0), "Rejections @16");
            Assert.That(BitConverter.ToInt32(d, 20), Is.EqualTo(0), "Overloads @20");
            Assert.That(BitConverter.ToInt32(d, 24), Is.EqualTo(3), "SwitchesUsed @24");
            Assert.That(BitConverter.ToUInt64(d, 28), Is.EqualTo(state.Rng.State), "Rng.State @28");
            Assert.That(BitConverter.ToUInt64(d, 36), Is.EqualTo(state.Rng.Inc), "Rng.Inc @36");
            Assert.That(d[44], Is.EqualTo(1), "SwitchRoutes[0] @44 == initialRoute 1");
            // NodeQueues: node i at 45 + 17*i (1-byte count + 8 shorts)
            for (int i = 0; i < 4; i++)
                Assert.That(d[45 + 17 * i], Is.EqualTo(0), $"queue count node {i}");
            // OverloadTimers: node i at 113 + 2*i
            Assert.That(BitConverter.ToInt16(d, 113 + 2 * 2), Is.EqualTo(9), "OverloadTimers[2] @117");
            // Trains: slot j at 121 + 10*j, zeroed at initial
            for (int b = 121; b < 141; b++)
                Assert.That(d[b], Is.EqualTo(0), $"train bytes zeroed @{b}");
            Assert.That(d[141], Is.EqualTo((byte)OutcomeKind.Running), "Outcome tag @141");
            Assert.That(d[142], Is.EqualTo(0), "FailReason @142");
        }
    }
}
