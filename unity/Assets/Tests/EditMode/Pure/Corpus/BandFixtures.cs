using System.IO;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Domain;

namespace CatMetro.Tests.Corpus
{
    // CM-C11: shared loaders + replay helpers for the alternation band (L006-L010). Tests may use
    // file APIs; the shipped Content assembly may not (same rule as ContentL001Tests.cs).
    public static class BandFixtures
    {
        public static readonly string[] Ids = { "L006", "L007", "L008", "L009", "L010" };

        public static string RepoRoot() => CatMetro.Tests.Domain.Fixtures.RepoRoot();

        public static byte[] Bytes(string id) =>
            File.ReadAllBytes(Path.Combine(RepoRoot(), "content", "levels", id + ".json"));

        public static ImportedLevel Import(string id)
        {
            var r = LevelImporter.Import(Bytes(id));
            Assert.That(r.Ok, Is.True, $"{id} must import: {r.Error}");
            return r.Value;
        }

        // Per-node max queue depth over a full replay of `log` (criterion 6's witness). Sampled
        // AFTER each Step call — the same seam MechanicExercise.Observe uses (A-DM-1: within-step
        // enqueue+release is invisible by design).
        public static int[] MaxNodeQueueDepth(LevelGraph graph, ulong seed, CommandLog log)
        {
            var max = new int[graph.NodeCount];
            ReplayHasher.RunToEnd(graph, seed, log, s =>
            {
                for (int n = 0; n < s.NodeQueueCounts.Length; n++)
                    if (s.NodeQueueCounts[n] > max[n]) max[n] = s.NodeQueueCounts[n];
            });
            return max;
        }

        // Criterion 7's witness (CM-C13 re-author): `toggles` toggles of `switchIndex` at tick 0
        // — all applying at step 1 of tick 1, before any wave emits — permanently diverting every
        // cat that reaches that switch onto its trap route. Every L007-L010 switch's LAST route
        // is a dead-end TRAP node (never a station), so no cat any resulting log produces can
        // mismatch-throw; the trap's declared `queueCapacity: 1` guarantees the diversion
        // overflows quickly rather than lingering (CM-C11's stop-condition-7 lesson: an
        // uncapacitated dead end makes solver-exploratory branches immortal). `toggles` is the
        // number of single-step route advances needed from the switch's authored `initialRoute`
        // to reach the trap index — generic across the four re-authored topologies rather than
        // hardcoded to one board's shape (the prior GateToHoldWitness() assumed a single
        // 2-route switch at index 0; L007-L010 now mix 2- and 3-route switches at different
        // indices).
        public static CommandLog TrapWitness(int switchIndex, int toggles)
        {
            var log = new CommandLog();
            for (int i = 0; i < toggles; i++)
                log.Append(new ToggleSwitchCommand((ushort)switchIndex, 0));
            return log;
        }
    }
}
