using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Solver
{
    // CM-C4 fixtures — constructed in code (the A-C1-2 pattern; the solver never parses JSON).
    // Board designs and their rationale: state/handoffs/CM-C4.md §Planner rulings.
    public static class SolverFixtures
    {
        // The L701 stress-board SHAPE (stress_boards.json: nodes/edges/switches/stations verbatim)
        // with a TRUNCATED wave script (first two waves; win re-scoped to 4) so the criterion-5
        // pin-pruning run completes in test time — the shape is what the criterion names; the
        // truncation is a runtime bound, recorded in the handoff.
        public static LevelGraph L701ShapeTruncated() => new LevelGraph(
            "L701T", 6,
            new[] { 8, 3, 2, 8, 8, 8 },                    // SRC, J1(qc3), J2(qc2), RED, BLU, YEL
            new[] { 0, 1, 1, 2, 2 },                       // E1 SRC->J1, E2 J1->RED, E3 J1->J2, E4 J2->BLU, E5 J2->YEL
            new[] { 1, 3, 2, 4, 5 },
            new[] { 8, 10, 6, 8, 8 },
            new[] { 0 },
            new[] { new[] { 1, 2 }, new[] { 3, 4 } },      // S1@J1 [E2,E3], S2@J2 [E4,E5]
            new[] { 1, 2 },
            new byte[] { 0, 0 },
            new[] { 3, 4, 5 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue }, new[] { CatColor.Yellow } },
            new[] { 5, 5, 4 },
            new[] { 8, 40 }, new[] { CatColor.Red, CatColor.Blue }, new[] { 2, 2 }, new[] { 12, 12 },
            winDeliveries: 4, timeLimitTicks: 100,
            qCapBound: 8, trainsMax: 4);

        // Two switches, optimum needs exactly 2 commands (S1 then S2); GRN decoy station makes a
        // wrong second route a pinned branch. Red: spawn t0 -> J1 t4 -> RED t9 on init. Blue:
        // spawn t6 -> J1 t10 (needs S1 toggled) -> J2 t14 (needs S2 toggled) -> BLU t19.
        public static LevelGraph TwoSwitchTwoCmd() => new LevelGraph(
            "FX-2SW", 5,
            new[] { 8, 8, 8, 8, 8 },                        // SRC, J1, J2, RED, BLU (GRN folded: node 4 dual-use? no —
            new[] { 0, 1, 1, 2, 2 },                        // E0 SRC->J1(4), E1 J1->RED(5), E2 J1->J2(4), E3 J2->GRNnode?  see below
            new[] { 1, 3, 2, 4, 3 },                        // E3 J2->BLU(5), E4 J2->RED(5)  (RED as the decoy: blue at RED = pin)
            new[] { 4, 5, 4, 5, 5 },
            new[] { 0 },
            new[] { new[] { 1, 2 }, new[] { 4, 3 } },       // S1@J1 [E1(RED), E2(J2)] init0; S2@J2 [E4(RED, decoy), E3(BLU)] init0
            new[] { 1, 2 },
            new byte[] { 0, 0 },
            new[] { 3, 4 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            new[] { 6, 6 },
            new[] { 0, 6 }, new[] { CatColor.Red, CatColor.Blue }, new[] { 1, 1 }, new[] { 1, 1 },
            winDeliveries: 2, timeLimitTicks: 60,
            qCapBound: 8, trainsMax: 2);

        // One switch, both routes end at the (only, red) station; one red train but win needs 2
        // deliveries: every line delivers exactly 1 then times out — Unsolvable with zero pins.
        public static LevelGraph UnsolvableNoPins() => new LevelGraph(
            "FX-UNS", 3,
            new[] { 8, 8, 8 },
            new[] { 0, 1, 1 }, new[] { 1, 2, 2 }, new[] { 4, 5, 9 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 0 },
            new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            winDeliveries: 2, timeLimitTicks: 40,
            qCapBound: 8, trainsMax: 1);

        // Three switches in series; every initial route points at a red-accepting dead station,
        // the blue train must be forwarded by toggling S1..S3. Zero-toggle line dies pinned, so a
        // width-1 beam (which always keeps the least-toggled state — escalation law) exhausts and
        // escalates; the next width finds the 3-command win.
        public static LevelGraph ThreeSwitchEscalation() => new LevelGraph(
            "FX-ESC", 8,
            new[] { 8, 8, 8, 8, 8, 8, 8, 8 },              // SRC, J1, J2, J3, BLU, DR1, DR2, DR3
            new[] { 0, 1, 1, 2, 2, 3, 3 },                 // E0 SRC->J1, E1 J1->DR1, E2 J1->J2, E3 J2->DR2, E4 J2->J3, E5 J3->DR3, E6 J3->BLU
            new[] { 1, 5, 2, 6, 3, 7, 4 },
            new[] { 3, 3, 3, 3, 3, 3, 3 },
            new[] { 0 },
            new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 } },
            new[] { 1, 2, 3 },
            new byte[] { 0, 0, 0 },                        // all inits -> dead stations
            new[] { 4, 5, 6, 7 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Red }, new[] { CatColor.Red }, new[] { CatColor.Red } },
            new[] { 6, 6, 6, 6 },
            new[] { 0 }, new[] { CatColor.Blue }, new[] { 1 }, new[] { 1 },
            winDeliveries: 1, timeLimitTicks: 60,
            qCapBound: 8, trainsMax: 1);

        // Three switches, everything deliverable, but win needs 2 deliveries with 1 train: no line
        // wins, no line pins — a beam miss that must report NotFound(Beam, last width), never
        // Unsolvable (criterion 4; ADR-0008:117 admits a human witness where beam fails).
        public static LevelGraph BeamMissNoPins() => new LevelGraph(
            "FX-MISS", 8,
            new[] { 8, 8, 8, 8, 8, 8, 8, 8 },
            new[] { 0, 1, 1, 2, 2, 3, 3 },
            new[] { 1, 5, 2, 6, 3, 7, 4 },
            new[] { 3, 3, 3, 3, 3, 3, 3 },
            new[] { 0 },
            new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 } },
            new[] { 1, 2, 3 },
            new byte[] { 0, 0, 0 },
            new[] { 4, 5, 6, 7 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Blue }, new[] { CatColor.Blue }, new[] { CatColor.Blue } },
            new[] { 6, 6, 6, 6 },
            new[] { 0 }, new[] { CatColor.Blue }, new[] { 1 }, new[] { 1 },
            winDeliveries: 2, timeLimitTicks: 40,
            qCapBound: 8, trainsMax: 1);

        // One switch whose init route is a blue-accepting dead end for the red train: any single
        // toggle tick from 0..(arrival-2) wins at the same completion tick — the tie-break must
        // pick entry (Tick=0, SwitchId=0).
        public static LevelGraph TieBreakBoard() => new LevelGraph(
            "FX-TIE", 4,
            new[] { 8, 8, 8, 8 },                          // SRC, J1, BLUdead, RED
            new[] { 0, 1, 1 }, new[] { 1, 2, 3 }, new[] { 6, 5, 5 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 0 },  // init -> BLUdead (pin for red)
            new[] { 2, 3 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Red } },
            new[] { 6, 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            winDeliveries: 1, timeLimitTicks: 40,
            qCapBound: 8, trainsMax: 1);

        // Init route already correct: the empty command log wins (criterion 10's second limb).
        public static LevelGraph AlreadyCorrect() => new LevelGraph(
            "FX-OK", 3,
            new[] { 8, 8, 8 },
            new[] { 0, 1, 1 }, new[] { 1, 2, 2 }, new[] { 4, 5, 9 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 0 },
            new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            winDeliveries: 1, timeLimitTicks: 40,
            qCapBound: 8, trainsMax: 1);

        // Review H1 (the reviewer's crash-class board): on the authored line (init -> RED,
        // spacing 4, travel 7) at most 2 trains are ever alive, so trainsMax=3 holds. An
        // exploratory toggle sends trains around the J1->DET->J1 loop instead — never delivered,
        // never freed — and the 4th spawn trips the Domain's TrainsMax envelope guard
        // (InvalidOperationException, Simulation's AllocateTrain). The search must prune that
        // branch, not crash, and must NOT count it as a pin (no wrong-colour arrival exists here).
        public static LevelGraph EnvelopeTrap() => new LevelGraph(
            "FX-ENV", 4,
            new[] { 8, 8, 8, 8 },                          // SRC, J1, RED, DET(our loop node)
            new[] { 0, 1, 1, 3 }, new[] { 1, 2, 3, 1 }, new[] { 2, 5, 5, 5 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 0 },  // S0@J1 [E1 RED, E2 DET], init -> RED (correct)
            new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 6 }, new[] { 4 },
            winDeliveries: 6, timeLimitTicks: 60,
            qCapBound: 8, trainsMax: 3);

        // Review M3: every line dies pinned — 1 switch, both routes end at blue-only stations,
        // one red train. BFS exhausts with pins > 0 and must report Indeterminate, never
        // Unsolvable.
        public static LevelGraph AllPinned() => new LevelGraph(
            "FX-PIN", 4,
            new[] { 8, 8, 8, 8 },                          // SRC, J1, B1, B2
            new[] { 0, 1, 1 }, new[] { 1, 2, 3 }, new[] { 3, 4, 4 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 0 },
            new[] { 2, 3 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Blue } },
            new[] { 6, 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            winDeliveries: 1, timeLimitTicks: 30,
            qCapBound: 8, trainsMax: 1);

        // --- helpers ---

        public static CommandLog Log(params (int switchId, int tick)[] entries)
        {
            var log = new CommandLog();
            foreach (var (s, t) in entries) log.Append(new ToggleSwitchCommand((ushort)s, t));
            return log;
        }

        // Criterion 7c emission format (planner ruling): lowercase hex of
        // concat(ushort SwitchId LE, int Tick LE) per entry; empty log -> "empty".
        public static string LogHex(CommandLog log)
        {
            if (log.Entries.Count == 0) return "empty";
            var sb = new StringBuilder(log.Entries.Count * 12);
            foreach (var e in log.Entries)
            {
                var b = new byte[6];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0, 2), e.SwitchId);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(2, 4), e.Tick);
                foreach (var by in b) sb.Append(by.ToString("x2"));
            }
            return sb.ToString();
        }

        // Won-or-not under Q-N semantics: pinned throws count as not-won (mirrors the pruning
        // rule so the brute-force comparator and the solver agree on what a win is).
        public static bool RunsToWin(LevelGraph graph, ulong seed, CommandLog log, out int completionTicks)
        {
            completionTicks = 0;
            try
            {
                var end = ReplayHasher.RunToEnd(graph, seed, log);
                if (end.Outcome.Kind != OutcomeKind.Won) return false;
                completionTicks = end.Tick - 1; // criterion 6's equation
                return true;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false; // envelope-broken log: not-won, mirroring the solver (review H1)
            }
        }

        // Criterion 3's independent comparator: exhaustive over all command logs with <=2 entries
        // (fixtures are designed so the true optimum needs <=2 commands). Returns the optimal
        // (completionTicks, log) under the criterion-7 ordering, or null if none wins.
        public static (int ticks, CommandLog log)? BruteForceBest(LevelGraph graph, ulong seed)
        {
            int switches = graph.SwitchRoutes.Length;
            int horizon = graph.TimeLimitTicks;
            (int ticks, CommandLog log)? best = null;

            void Consider(CommandLog log)
            {
                if (!RunsToWin(graph, seed, log, out int t)) return;
                if (best == null || Better(t, log, best.Value.ticks, best.Value.log))
                    best = (t, log);
            }

            Consider(Log()); // zero commands
            for (int s = 0; s < switches; s++)
                for (int t = 0; t < horizon; t++)
                    Consider(Log((s, t)));
            for (int s1 = 0; s1 < switches; s1++)
                for (int t1 = 0; t1 < horizon; t1++)
                    for (int s2 = 0; s2 < switches; s2++)
                        for (int t2 = 0; t2 < horizon; t2++)
                        {
                            // canonical order: logs are ordered by (Tick, SwitchId) receipt
                            if (t2 < t1 || (t2 == t1 && s2 < s1)) continue;
                            Consider(Log((s1, t1), (s2, t2)));
                        }
            return best;
        }

        // The criterion-7 total order: fewer completion ticks, then fewer commands, then
        // lexicographic over (Tick, SwitchId) pairs.
        public static bool Better(int ticksA, CommandLog a, int ticksB, CommandLog b)
        {
            if (ticksA != ticksB) return ticksA < ticksB;
            if (a.Entries.Count != b.Entries.Count) return a.Entries.Count < b.Entries.Count;
            for (int i = 0; i < a.Entries.Count; i++)
            {
                var ea = a.Entries[i]; var eb = b.Entries[i];
                if (ea.Tick != eb.Tick) return ea.Tick < eb.Tick;
                if (ea.SwitchId != eb.SwitchId) return ea.SwitchId < eb.SwitchId;
            }
            return false;
        }

        public static void AssertSameLog(CommandLog expected, CommandLog actual, string context)
        {
            NUnit.Framework.Assert.That(actual.Entries.Count, NUnit.Framework.Is.EqualTo(expected.Entries.Count), context + ": entry count");
            for (int i = 0; i < expected.Entries.Count; i++)
            {
                NUnit.Framework.Assert.That(actual.Entries[i].SwitchId, NUnit.Framework.Is.EqualTo(expected.Entries[i].SwitchId), $"{context}: entry {i} switch");
                NUnit.Framework.Assert.That(actual.Entries[i].Tick, NUnit.Framework.Is.EqualTo(expected.Entries[i].Tick), $"{context}: entry {i} tick");
            }
        }
    }
}
