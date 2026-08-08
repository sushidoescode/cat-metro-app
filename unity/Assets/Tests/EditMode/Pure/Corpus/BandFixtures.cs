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

        // Criterion 7's witness: toggle the GATE switch (S1, index 0) once at tick 0 — applies at
        // step 1 of tick 1, before any wave emits — permanently diverting every cat from GATE's
        // real route (index 0, -> J1) onto the trap route (index 1, -> HOLD). HOLD is never a
        // station, so no cat this log ever produces can mismatch-throw.
        public static CommandLog GateToHoldWitness()
        {
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 0));
            return log;
        }
    }
}
