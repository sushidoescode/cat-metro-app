using System.Collections.Generic;
using CatMetro.Domain;
using CatMetro.Domain.Solver;

namespace CatMetro.Content.Validation
{
    // CM-C5.1: dispositions for the 8 schema mechanics (level_schema.json meta.mechanics enum).
    // Observable   — the optimal-trace observer can measure it in post-step tick state.
    // Unreachable  — the importer pins it before any trace exists (ContentResult PinnedMechanic).
    // Unobservable — no DTO member carries it, so no run can ever exhibit it.
    public enum MechanicDisposition
    {
        Observable = 1,
        Unreachable = 2,
        Unobservable = 3,
    }

    // What the solver-optimal trace actually exhibited, sampled AFTER each step through the one
    // replay seam (A-DM-1: within-step enqueue+release is invisible by design — the player never
    // sees a line form). Ticks are the tick each step processed (post-step Tick - 1).
    public sealed class ExerciseRecord
    {
        public readonly int MaxQueued;          // max over sampled ticks of the summed node queue counts
        public readonly int MaxQueuedAtTick;    // first tick the max was seen; -1 when MaxQueued == 0
        public readonly bool RouteChanged;      // any switch away from its authored initial route
        public readonly int RouteChangedAtTick; // first such tick; -1 when never
        public readonly int SwitchesUsed;       // final commanded-toggle count
        public readonly int[] EmittingSourceNodes; // distinct declared origins, in authored order
        public readonly int WildDeliveries;
        public readonly int FirstWildDeliveryAtTick; // -1 when no Wild train was delivered

        public ExerciseRecord(int maxQueued, int maxQueuedAtTick, bool routeChanged,
            int routeChangedAtTick, int switchesUsed, int[] emittingSourceNodes,
            int wildDeliveries, int firstWildDeliveryAtTick)
        {
            MaxQueued = maxQueued; MaxQueuedAtTick = maxQueuedAtTick;
            RouteChanged = routeChanged; RouteChangedAtTick = routeChangedAtTick;
            SwitchesUsed = switchesUsed;
            EmittingSourceNodes = emittingSourceNodes ?? new int[0];
            WildDeliveries = wildDeliveries;
            FirstWildDeliveryAtTick = firstWildDeliveryAtTick;
        }
    }

    // CM-C5.1: the dead-newMechanic gate (CM-R06.2 liveness limb, ratified BLOCKING; scope =
    // meta.newMechanic ONLY — widening to meta.mechanics is HC-14, a joint human call with
    // HC-10, stop condition 10). The witness is the solver-optimal log (A-DM-2); replay goes
    // through ReplayHasher only — no second scheduler.
    public static class MechanicExercise
    {
        public static readonly IReadOnlyDictionary<string, MechanicDisposition> Dispositions =
            new Dictionary<string, MechanicDisposition>
            {
                ["switch"] = MechanicDisposition.Observable,
                ["queue"] = MechanicDisposition.Observable,
                ["second-source"] = MechanicDisposition.Observable,
                ["wildcard"] = MechanicDisposition.Observable,
                ["cooldown"] = MechanicDisposition.Unobservable,
                ["gate"] = MechanicDisposition.Unobservable,
                ["express"] = MechanicDisposition.Unobservable,
                ["reversible"] = MechanicDisposition.Unobservable,
            };

        public static ExerciseRecord Observe(LevelGraph graph, ulong seed, CommandLog log)
        {
            int maxQueued = 0, maxQueuedAt = -1;
            bool routeChanged = false;
            int routeChangedAt = -1, switchesUsed = 0;
            var observedSourceNodes = new HashSet<int>();
            var wasActive = new bool[graph.TrainsMax];
            var priorColor = new byte[graph.TrainsMax];
            int previousDeliveries = 0;
            int wildDeliveries = 0, firstWildDeliveryAt = -1;
            ReplayHasher.RunToEnd(graph, seed, log, s =>
            {
                int tick = s.Tick - 1; // the tick this step processed (sampled after it)
                int queued = 0;
                for (int n = 0; n < s.NodeQueueCounts.Length; n++) queued += s.NodeQueueCounts[n];
                if (queued > maxQueued) { maxQueued = queued; maxQueuedAt = tick; }
                if (!routeChanged)
                    for (int i = 0; i < s.SwitchRoutes.Length; i++)
                        if (s.SwitchRoutes[i] != graph.SwitchInitialRoute[i])
                        {
                            routeChanged = true;
                            routeChangedAt = tick;
                            break;
                        }
                switchesUsed = s.SwitchesUsed;

                bool deliveredThisTick = s.Deliveries > previousDeliveries;
                for (int t = 0; t < s.Trains.Length; t++)
                {
                    bool active = s.Trains[t].State != TrainState.None;
                    if (!wasActive[t] && active)
                    {
                        int origin = s.Trains[t].NodeId;
                        for (int i = 0; i < graph.SourceNodes.Length; i++)
                            if (origin == graph.SourceNodes[i])
                            {
                                observedSourceNodes.Add(origin);
                                break;
                            }
                    }
                    else if (wasActive[t] && !active && deliveredThisTick
                        && priorColor[t] == CatColor.Wild)
                    {
                        wildDeliveries++;
                        if (firstWildDeliveryAt < 0) firstWildDeliveryAt = tick;
                    }

                    wasActive[t] = active;
                    priorColor[t] = active ? s.Trains[t].Color : CatColor.None;
                }
                previousDeliveries = s.Deliveries;
            });

            var emittingSourceNodes = new List<int>();
            for (int i = 0; i < graph.SourceNodes.Length; i++)
                if (observedSourceNodes.Contains(graph.SourceNodes[i])
                    && !emittingSourceNodes.Contains(graph.SourceNodes[i]))
                    emittingSourceNodes.Add(graph.SourceNodes[i]);
            return new ExerciseRecord(maxQueued, maxQueuedAt, routeChanged, routeChangedAt,
                switchesUsed, emittingSourceNodes.ToArray(), wildDeliveries, firstWildDeliveryAt);
        }

        // The per-campaign-level liveness verdict. Rides CampaignVerdicts labelled
        // Stage.NoveltyCheck (A-DM-4: the frozen house label for campaign rows — a known wart).
        // The Value always prints the measurement (criterion 8), prefixed with the level-unique
        // machine tag the criterion-6 selectors match on.
        public static StageVerdict Liveness(LevelDto dto, LevelGraph graph, SolveResult solve)
        {
            string id = dto.Id;
            string mech = dto.Meta.NewMechanic;
            string tag = "tag=CM-R06.2-liveness:" + id;
            if (mech == null)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Skipped,
                    "SKIPPED(no declared newMechanic)",
                    tag + "; newMechanic=null; exercised=false; evidence=none", false);

            string prefix = tag + "; newMechanic=" + mech + "; ";
            if (!Dispositions.TryGetValue(mech, out var disposition))
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pinned,
                    "PINNED(" + mech + " is outside the 8-mechanic schema enum — stage 1 owns the failure)",
                    prefix + "exercised=false; evidence=none", false);
            if (disposition == MechanicDisposition.Unobservable)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pinned,
                    "PINNED(" + mech + " unobservable — no DTO field)",
                    prefix + "exercised=false; evidence=none", false);
            if (disposition == MechanicDisposition.Unreachable)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pinned,
                    "PINNED(" + mech + " unreachable — the importer refuses it first)",
                    prefix + "exercised=false; evidence=none", false);

            if (solve == null || solve.Verdict != SolveVerdict.Solved)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Skipped,
                    "SKIPPED(no winning log)", prefix + "exercised=false; evidence=none", false);

            var rec = Observe(graph, (ulong)dto.Seed, solve.OptimalLog);
            bool exercised = false;
            string evidence = "none";
            if (mech == "queue")
            {
                exercised = rec.MaxQueued > 0;
                evidence = exercised
                    ? "maxQueued=" + rec.MaxQueued + "@tick " + rec.MaxQueuedAtTick
                    : "none";
            }
            else if (mech == "switch") // A-DM-3: observational, with stage 5 as the causal backstop
            {
                exercised = rec.RouteChanged && rec.SwitchesUsed >= 1;
                evidence = exercised
                    ? "toggles=" + rec.SwitchesUsed + ",routeChangedAtTick=" + rec.RouteChangedAtTick
                    : "none";
            }
            else if (mech == "second-source")
            {
                exercised = rec.EmittingSourceNodes.Length >= 2;
                var names = new List<string>();
                var nodes = dto.Nodes.Span;
                for (int i = 0; i < rec.EmittingSourceNodes.Length; i++)
                {
                    int node = rec.EmittingSourceNodes[i];
                    if (node >= 0 && node < nodes.Length) names.Add(nodes[node].Id);
                }
                evidence = "sources=" + (names.Count == 0 ? "none" : string.Join(",", names));
            }
            else if (mech == "wildcard")
            {
                exercised = rec.WildDeliveries >= 1;
                evidence = exercised
                    ? "wildDeliveries=" + rec.WildDeliveries + "@tick " + rec.FirstWildDeliveryAtTick
                    : "wildDeliveries=0";
            }
            string value = prefix + "exercised=" + (exercised ? "true" : "false")
                + "; evidence=" + evidence;
            if (exercised)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pass,
                    "newMechanic liveness OK (CM-R06.2): " + id + " exercises '" + mech + "'",
                    value, false);
            return StageVerdict.Fail(Stage.NoveltyCheck,
                "newMechanic liveness violation (CM-R06.2): " + id + " declares '" + mech
                + "' but the solver-optimal trace never exercises it", value);
        }
    }
}
