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

        // Criterion 8b: the offset table on the L001-shape state — the digest layout IS the
        // contract (ADR-0002 §7; overview.md:312-320; A-C1-10 slot conventions). Review F1:
        // EVERY field group carries a DISTINCT NON-ZERO marker and is asserted as RAW BYTES so
        // (a) no field-order swap can hide behind zeros and (b) little-endianness is positively
        // asserted (BitConverter is host-endian and is deliberately not used here). This is a
        // LAYOUT test: setting Score/Chain markers here does not contradict their stay-zero
        // gameplay rule (which the golden and the boundary tests pin).
        [Test]
        public void OffsetTable_L001Shape_EveryFieldPinnedLittleEndian()
        {
            var state = SimulationState.CreateInitial(Fixtures.L001Shape(), Fixtures.L001Seed);
            state.Tick = 101; state.Score = 102; state.Chain = 103; state.Deliveries = 104;
            state.Rejections = 105; state.Overloads = 106; state.SwitchesUsed = 107;
            state.Rng.State = 0x1112131415161718UL;
            state.Rng.Inc = 0x2122232425262728UL;
            // SwitchRoutes[0] stays 1 (initialRoute)
            state.NodeQueueCounts[0] = 2; state.NodeQueueSlots[0][0] = 513; state.NodeQueueSlots[0][1] = 1027;
            state.NodeQueueCounts[1] = 1; state.NodeQueueSlots[1][0] = 1541;
            state.OverloadTimers[0] = 21; state.OverloadTimers[1] = 22; state.OverloadTimers[2] = 23; state.OverloadTimers[3] = 24;
            state.Trains[0] = new TrainSlot { Id = 31, Color = 32, EdgeId = 33, ProgressTicks = 34, NodeId = 35, State = 36 };
            state.Trains[1] = new TrainSlot { Id = 41, Color = 42, EdgeId = 43, ProgressTicks = 44, NodeId = 45, State = 46 };
            state.Outcome = SimOutcome.MakeFailed(FailReason.TimeOut);

            var d = new byte[143];
            state.WriteDigest(d);

            void I32At(int off, int v, string name)
            {
                Assert.That(d[off], Is.EqualTo((byte)v), $"{name} low byte @{off} (LE)");
                Assert.That(d[off + 1], Is.EqualTo(0), $"{name} @{off + 1}");
                Assert.That(d[off + 2], Is.EqualTo(0), $"{name} @{off + 2}");
                Assert.That(d[off + 3], Is.EqualTo(0), $"{name} @{off + 3}");
            }
            void I16At(int off, int v, string name)
            {
                Assert.That(d[off], Is.EqualTo((byte)(v & 0xFF)), $"{name} low byte @{off} (LE)");
                Assert.That(d[off + 1], Is.EqualTo((byte)(v >> 8)), $"{name} high byte @{off + 1}");
            }

            I32At(0, 101, "Tick"); I32At(4, 102, "Score"); I32At(8, 103, "Chain");
            I32At(12, 104, "Deliveries"); I32At(16, 105, "Rejections"); I32At(20, 106, "Overloads");
            I32At(24, 107, "SwitchesUsed");
            var rngState = new byte[] { 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11 };
            var rngInc = new byte[] { 0x28, 0x27, 0x26, 0x25, 0x24, 0x23, 0x22, 0x21 };
            for (int i = 0; i < 8; i++)
            {
                Assert.That(d[28 + i], Is.EqualTo(rngState[i]), $"Rng.State byte {i} @28+{i} (LE)");
                Assert.That(d[36 + i], Is.EqualTo(rngInc[i]), $"Rng.Inc byte {i} @36+{i} (LE)");
            }
            Assert.That(d[44], Is.EqualTo(1), "SwitchRoutes[0] @44 == initialRoute 1");
            // NodeQueues: node i at 45 + 17*i (1-byte count + 8 LE shorts, unused slots zero)
            Assert.That(d[45], Is.EqualTo(2), "node0 count @45");
            I16At(46, 513, "node0 slot0");   // 0x0201
            I16At(48, 1027, "node0 slot1");  // 0x0403
            for (int b = 50; b < 62; b++) Assert.That(d[b], Is.EqualTo(0), $"node0 unused slot byte @{b}");
            Assert.That(d[62], Is.EqualTo(1), "node1 count @62");
            I16At(63, 1541, "node1 slot0");  // 0x0605
            Assert.That(d[79], Is.EqualTo(0), "node2 count @79");
            Assert.That(d[96], Is.EqualTo(0), "node3 count @96");
            // OverloadTimers: node i at 113 + 2*i
            I16At(113, 21, "OverloadTimers[0]"); I16At(115, 22, "OverloadTimers[1]");
            I16At(117, 23, "OverloadTimers[2]"); I16At(119, 24, "OverloadTimers[3]");
            // Trains: slot j at 121 + 10*j — Id:2 Color:1 EdgeId:2 Progress:2 NodeId:2 State:1
            I16At(121, 31, "train0 Id"); Assert.That(d[123], Is.EqualTo(32), "train0 Color @123");
            I16At(124, 33, "train0 EdgeId"); I16At(126, 34, "train0 Progress"); I16At(128, 35, "train0 NodeId");
            Assert.That(d[130], Is.EqualTo(36), "train0 State @130");
            I16At(131, 41, "train1 Id"); Assert.That(d[133], Is.EqualTo(42), "train1 Color @133");
            I16At(134, 43, "train1 EdgeId"); I16At(136, 44, "train1 Progress"); I16At(138, 45, "train1 NodeId");
            Assert.That(d[140], Is.EqualTo(46), "train1 State @140");
            Assert.That(d[141], Is.EqualTo((byte)OutcomeKind.Failed), "Outcome tag @141");
            Assert.That(d[142], Is.EqualTo((byte)FailReason.TimeOut), "FailReason @142");
        }
    }
}
