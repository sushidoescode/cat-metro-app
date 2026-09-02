using System.Collections.Generic;
using CatMetro.Domain;
using CatMetro.Domain.Solver;

namespace CatMetro.Content.Validation
{
    public enum MechanicDisposition
    {
        Observable = 1,
        Unreachable = 2,
        Unobservable = 3,
    }

    // Measurements come from post-step states of the exact solver winning replay. A field is
    // only true when the mechanic changed or occupied authoritative simulation state; authored
    // metadata alone is never accepted as exercise evidence.
    public sealed class ExerciseRecord
    {
        public readonly int MaxQueued;
        public readonly int MaxQueuedAtTick;
        public readonly bool RouteChanged;
        public readonly int RouteChangedAtTick;
        public readonly int SwitchesUsed;
        public readonly int[] EmittingSourceNodes;
        public readonly int WildDeliveries;
        public readonly int FirstWildDeliveryAtTick;
        public readonly int ShapeDeliveries;
        public readonly int MaxCooldown;
        public readonly bool TunnelTraversed;
        public readonly bool GateWaitThenTraverse;
        public readonly bool ReversibleTraversed;
        public readonly int MaxActiveTrains;
        public readonly bool HoldTraversed;
        public readonly bool StrayEmitted;
        public readonly bool StrayAutoToggled;
        public readonly bool ExpressQueued;
        public readonly int ExpressDeliveries;
        public readonly int WildExpressDeliveries;

        public ExerciseRecord(
            int maxQueued, int maxQueuedAtTick, bool routeChanged, int routeChangedAtTick,
            int switchesUsed, int[] emittingSourceNodes, int wildDeliveries,
            int firstWildDeliveryAtTick, int shapeDeliveries, int maxCooldown,
            bool tunnelTraversed, bool gateWaitThenTraverse, bool reversibleTraversed,
            int maxActiveTrains, bool holdTraversed, bool strayEmitted,
            bool strayAutoToggled, bool expressQueued, int expressDeliveries,
            int wildExpressDeliveries)
        {
            MaxQueued = maxQueued;
            MaxQueuedAtTick = maxQueuedAtTick;
            RouteChanged = routeChanged;
            RouteChangedAtTick = routeChangedAtTick;
            SwitchesUsed = switchesUsed;
            EmittingSourceNodes = emittingSourceNodes ?? new int[0];
            WildDeliveries = wildDeliveries;
            FirstWildDeliveryAtTick = firstWildDeliveryAtTick;
            ShapeDeliveries = shapeDeliveries;
            MaxCooldown = maxCooldown;
            TunnelTraversed = tunnelTraversed;
            GateWaitThenTraverse = gateWaitThenTraverse;
            ReversibleTraversed = reversibleTraversed;
            MaxActiveTrains = maxActiveTrains;
            HoldTraversed = holdTraversed;
            StrayEmitted = strayEmitted;
            StrayAutoToggled = strayAutoToggled;
            ExpressQueued = expressQueued;
            ExpressDeliveries = expressDeliveries;
            WildExpressDeliveries = wildExpressDeliveries;
        }
    }

    public static class MechanicExercise
    {
        public static readonly IReadOnlyDictionary<string, MechanicDisposition> Dispositions =
            new Dictionary<string, MechanicDisposition>
            {
                ["switch"] = MechanicDisposition.Observable,
                ["queue"] = MechanicDisposition.Observable,
                ["second-source"] = MechanicDisposition.Observable,
                ["wildcard"] = MechanicDisposition.Observable,
                ["cooldown"] = MechanicDisposition.Observable,
                ["gate"] = MechanicDisposition.Observable,
                ["express"] = MechanicDisposition.Observable,
                ["reversible"] = MechanicDisposition.Observable,
                ["shape"] = MechanicDisposition.Observable,
                ["budget"] = MechanicDisposition.Observable,
                ["tunnel"] = MechanicDisposition.Observable,
                ["second-train"] = MechanicDisposition.Observable,
                ["hold"] = MechanicDisposition.Observable,
                ["stray"] = MechanicDisposition.Observable,
                ["wildcard-express"] = MechanicDisposition.Observable,
            };

        public static ExerciseRecord Observe(LevelGraph graph, ulong seed, CommandLog log)
        {
            int maxQueued = 0, maxQueuedAt = -1;
            bool routeChanged = false;
            int routeChangedAt = -1, switchesUsed = 0, previousSwitchesUsed = 0;
            int maxCooldown = 0, maxActive = 0;
            bool tunnel = false, gateWaitThenTraverse = false, reverse = false, hold = false;
            bool strayEmitted = false, strayAutoToggled = false, expressQueued = false;
            int wildDeliveries = 0, firstWildDeliveryAt = -1, shapeDeliveries = 0;
            int expressDeliveries = 0, wildExpressDeliveries = 0;
            int previousDeliveries = 0;
            var observedSourceNodes = new HashSet<int>();
            var wasActive = new bool[graph.TrainsMax];
            var priorToken = new byte[graph.TrainsMax];
            var priorState = new byte[graph.TrainsMax];
            var priorEdge = new short[graph.TrainsMax];
            var exceptionalReverse = new bool[graph.TrainsMax];
            var waitedGateEdge = new int[graph.TrainsMax];
            for (int t = 0; t < waitedGateEdge.Length; t++) waitedGateEdge[t] = -1;
            var priorRoute = new int[graph.SwitchRoutes.Length];
            for (int s = 0; s < priorRoute.Length; s++)
                priorRoute[s] = SwitchState.Route(graph.SwitchInitialRoute[s]);

            ReplayHasher.RunToEnd(graph, seed, log, state =>
            {
                int tick = state.Tick - 1;
                int queued = 0;
                for (int n = 0; n < state.NodeQueueCounts.Length; n++)
                    queued += state.NodeQueueCounts[n];
                if (queued > maxQueued)
                {
                    maxQueued = queued;
                    maxQueuedAt = tick;
                }

                int routeChanges = 0;
                for (int s = 0; s < state.SwitchRoutes.Length; s++)
                {
                    int current = SwitchState.Route(state.SwitchRoutes[s]);
                    int cooling = SwitchState.Cooldown(state.SwitchRoutes[s]);
                    if (cooling > maxCooldown) maxCooldown = cooling;
                    if (current != SwitchState.Route(graph.SwitchInitialRoute[s]))
                    {
                        if (!routeChanged) routeChangedAt = tick;
                        routeChanged = true;
                    }
                    if (current != priorRoute[s]) routeChanges++;
                }

                int activeCount = 0;
                bool activeStray = false;
                bool deliveredThisTick = state.Deliveries > previousDeliveries;
                for (int t = 0; t < state.Trains.Length; t++)
                {
                    var train = state.Trains[t];
                    bool active = train.State != TrainState.None;
                    if (active)
                    {
                        activeCount++;
                        bool stray = CatToken.IsStray(train.Color);
                        bool express = CatToken.IsExpress(train.Color);
                        activeStray |= stray;
                        if (!wasActive[t])
                        {
                            waitedGateEdge[t] = -1;
                            exceptionalReverse[t] = false;
                            if (stray) strayEmitted = true;
                            int origin = train.NodeId;
                            for (int i = 0; i < graph.SourceNodes.Length; i++)
                                if (origin == graph.SourceNodes[i])
                                {
                                    observedSourceNodes.Add(origin);
                                    break;
                                }
                        }

                        if (express && train.State == TrainState.AtNode)
                            expressQueued = true;

                        if (train.State == TrainState.OnEdge
                            || train.State == TrainState.OnEdgeReverse)
                        {
                            int edge = train.EdgeId;
                            if (graph.EdgeTunnel[edge]) tunnel = true;
                            if (graph.EdgeHold[edge]) hold = true;
                            if (waitedGateEdge[t] == edge && GateOpenAt(graph, edge, tick))
                                gateWaitThenTraverse = true;

                            if (train.State == TrainState.OnEdgeReverse)
                            {
                                if (priorState[t] != TrainState.OnEdgeReverse)
                                {
                                    bool refused = priorState[t] == TrainState.RejectedAtStation
                                        && priorEdge[t] == edge;
                                    bool expressBounce = express
                                        && (priorState[t] == TrainState.OnEdge
                                            || priorState[t] == TrainState.OnEdgeReverse)
                                        && priorEdge[t] == edge;
                                    exceptionalReverse[t] = refused || expressBounce;
                                }
                                if (!exceptionalReverse[t] && graph.EdgeReversible[edge])
                                    reverse = true;
                            }
                            else
                            {
                                exceptionalReverse[t] = false;
                            }
                        }
                        else if (train.State != TrainState.RejectedAtStation)
                        {
                            exceptionalReverse[t] = false;
                        }
                    }
                    else
                    {
                        waitedGateEdge[t] = -1;
                        exceptionalReverse[t] = false;
                        if (wasActive[t] && deliveredThisTick)
                        {
                            byte token = priorToken[t];
                            bool wild = CatToken.Color(token) == CatColor.Wild;
                            bool express = CatToken.IsExpress(token);
                            if (wild)
                            {
                                wildDeliveries++;
                                if (firstWildDeliveryAt < 0) firstWildDeliveryAt = tick;
                            }
                            if (!wild && CatToken.Shape(token) != CatShape.Round)
                                shapeDeliveries++;
                            if (express) expressDeliveries++;
                            if (wild && express) wildExpressDeliveries++;
                        }
                    }

                    wasActive[t] = active;
                    priorToken[t] = active ? train.Color : CatColor.None;
                    priorState[t] = train.State;
                    priorEdge[t] = train.EdgeId;
                }
                if (activeCount > maxActive) maxActive = activeCount;

                // Remember a queued train and its selected closed gate. Only later entry onto
                // that same edge satisfies the witness.
                for (int node = 0; node < graph.NodeCount; node++)
                    for (int q = 0; q < state.NodeQueueCounts[node]; q++)
                    {
                        int slot = state.NodeQueueSlots[node][q] - 1;
                        int edge = SelectedEdge(graph, state, slot, node);
                        if (edge >= 0 && HasGate(graph, edge) && !GateOpenAt(graph, edge, tick))
                            waitedGateEdge[slot] = edge;
                    }

                int playerChanges = state.SwitchesUsed - previousSwitchesUsed;
                if (activeStray && routeChanges > playerChanges)
                    strayAutoToggled = true;
                for (int s = 0; s < state.SwitchRoutes.Length; s++)
                    priorRoute[s] = SwitchState.Route(state.SwitchRoutes[s]);
                previousSwitchesUsed = state.SwitchesUsed;
                switchesUsed = state.SwitchesUsed;
                previousDeliveries = state.Deliveries;
            });

            var sources = new List<int>();
            for (int i = 0; i < graph.SourceNodes.Length; i++)
                if (observedSourceNodes.Contains(graph.SourceNodes[i]))
                    sources.Add(graph.SourceNodes[i]);
            return new ExerciseRecord(
                maxQueued, maxQueuedAt, routeChanged, routeChangedAt, switchesUsed,
                sources.ToArray(), wildDeliveries, firstWildDeliveryAt, shapeDeliveries,
                maxCooldown, tunnel, gateWaitThenTraverse, reverse, maxActive, hold,
                strayEmitted, strayAutoToggled, expressQueued, expressDeliveries,
                wildExpressDeliveries);
        }

        public static StageVerdict Liveness(LevelDto dto, LevelGraph graph, SolveResult solve)
        {
            string id = dto.Id;
            string mechanic = dto.Meta.NewMechanic;
            string tag = "tag=CM-R06.2-liveness:" + id;
            if (mechanic == null)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Skipped,
                    "SKIPPED(no declared newMechanic)",
                    tag + "; newMechanic=null; exercised=false; evidence=none", false);

            string prefix = tag + "; newMechanic=" + mechanic + "; ";
            if (!Dispositions.TryGetValue(mechanic, out var disposition))
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pinned,
                    "PINNED(" + mechanic + " is outside the schema mechanic enum)",
                    prefix + "exercised=false; evidence=none", false);
            if (disposition != MechanicDisposition.Observable)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pinned,
                    "PINNED(" + mechanic + " cannot be observed)",
                    prefix + "exercised=false; evidence=none", false);
            if (solve == null || solve.Verdict != SolveVerdict.Solved)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Skipped,
                    "SKIPPED(no winning log)", prefix + "exercised=false; evidence=none", false);

            var record = Observe(graph, (ulong)dto.Seed, solve.OptimalLog);
            bool exercised;
            string evidence;
            switch (mechanic)
            {
                case "switch":
                    exercised = record.RouteChanged && record.SwitchesUsed >= 1;
                    evidence = "toggles=" + record.SwitchesUsed
                        + ",routeChangedAtTick=" + record.RouteChangedAtTick;
                    break;
                case "queue":
                    exercised = record.MaxQueued > 0;
                    evidence = "maxQueued=" + record.MaxQueued + "@tick " + record.MaxQueuedAtTick;
                    break;
                case "second-source":
                    exercised = record.EmittingSourceNodes.Length >= 2;
                    var names = new List<string>();
                    var nodes = dto.Nodes.Span;
                    for (int i = 0; i < record.EmittingSourceNodes.Length; i++)
                    {
                        int node = record.EmittingSourceNodes[i];
                        if (node >= 0 && node < nodes.Length) names.Add(nodes[node].Id);
                    }
                    evidence = "sources=" + (names.Count == 0 ? "none" : string.Join(",", names));
                    break;
                case "wildcard":
                    exercised = record.WildDeliveries > 0;
                    evidence = "wildDeliveries=" + record.WildDeliveries + "@tick "
                        + record.FirstWildDeliveryAtTick;
                    break;
                case "shape":
                    exercised = record.ShapeDeliveries > 0;
                    evidence = "nonRoundDeliveries=" + record.ShapeDeliveries;
                    break;
                case "budget":
                    exercised = graph.PerfectMaxSwitches >= 0
                        && record.SwitchesUsed == graph.PerfectMaxSwitches;
                    evidence = "used=" + record.SwitchesUsed + "/cap=" + graph.PerfectMaxSwitches;
                    break;
                case "cooldown":
                    exercised = record.SwitchesUsed > 0 && record.MaxCooldown > 0;
                    evidence = "maxCooldown=" + record.MaxCooldown;
                    break;
                case "tunnel":
                    exercised = record.TunnelTraversed;
                    evidence = "tunnelTraversed=" + record.TunnelTraversed.ToString().ToLowerInvariant();
                    break;
                case "gate":
                    exercised = record.GateWaitThenTraverse;
                    evidence = "closedWaitThenTraverse="
                        + record.GateWaitThenTraverse.ToString().ToLowerInvariant();
                    break;
                case "reversible":
                    exercised = record.ReversibleTraversed;
                    evidence = "ordinaryReverse="
                        + record.ReversibleTraversed.ToString().ToLowerInvariant();
                    break;
                case "second-train":
                    var zero = ReplayHasher.RunToEnd(graph, (ulong)dto.Seed, new CommandLog());
                    bool zeroCollision = zero.Outcome.Kind == OutcomeKind.Failed
                        && zero.Outcome.Reason == FailReason.Collision;
                    exercised = graph.CollisionsEnabled && record.MaxActiveTrains >= 2
                        && zeroCollision;
                    evidence = "maxActive=" + record.MaxActiveTrains
                        + ",zeroInputCollision=" + zeroCollision.ToString().ToLowerInvariant();
                    break;
                case "hold":
                    exercised = record.HoldTraversed;
                    evidence = "holdTraversed=" + record.HoldTraversed.ToString().ToLowerInvariant();
                    break;
                case "stray":
                    exercised = record.StrayEmitted && record.StrayAutoToggled;
                    evidence = "emitted=" + record.StrayEmitted.ToString().ToLowerInvariant()
                        + ",autoToggle=" + record.StrayAutoToggled.ToString().ToLowerInvariant();
                    break;
                case "express":
                    exercised = record.ExpressDeliveries > 0 && !record.ExpressQueued;
                    evidence = "expressDeliveries=" + record.ExpressDeliveries
                        + ",queued=" + record.ExpressQueued.ToString().ToLowerInvariant();
                    break;
                case "wildcard-express":
                    exercised = record.WildExpressDeliveries > 0 && !record.ExpressQueued;
                    evidence = "wildExpressDeliveries=" + record.WildExpressDeliveries
                        + ",queued=" + record.ExpressQueued.ToString().ToLowerInvariant();
                    break;
                default:
                    exercised = false;
                    evidence = "none";
                    break;
            }

            string value = prefix + "exercised=" + (exercised ? "true" : "false")
                + "; evidence=" + evidence;
            if (exercised)
                return new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pass,
                    "newMechanic liveness OK (CM-R06.2): " + id + " exercises '" + mechanic + "'",
                    value, false);
            return StageVerdict.Fail(Stage.NoveltyCheck,
                "newMechanic liveness violation (CM-R06.2): " + id + " declares '" + mechanic
                + "' but the solver-optimal trace never exercises it", value);
        }

        private static int SelectedEdge(LevelGraph graph, SimulationState state, int slot, int node)
        {
            var train = state.Trains[slot];
            if (CatToken.IsStray(train.Color) && train.EdgeId >= 0)
                return train.EdgeId;
            for (int s = 0; s < graph.SwitchNode.Length; s++)
                if (graph.SwitchNode[s] == node)
                    return graph.SwitchRoutes[s][SwitchState.Route(state.SwitchRoutes[s])];
            for (int e = 0; e < graph.EdgeFrom.Length; e++)
                if (graph.EdgeFrom[e] == node) return e;
            for (int e = 0; e < graph.EdgeTo.Length; e++)
                if (graph.EdgeTo[e] == node && (!graph.EdgeOneWay[e] || graph.EdgeReversible[e]))
                    return e;
            return -1;
        }

        private static bool HasGate(LevelGraph graph, int edge)
        {
            for (int g = 0; g < graph.GateEdge.Length; g++)
                if (graph.GateEdge[g] == edge) return true;
            return false;
        }

        private static bool GateOpenAt(LevelGraph graph, int edge, int tick)
        {
            for (int g = 0; g < graph.GateEdge.Length; g++)
            {
                if (graph.GateEdge[g] != edge) continue;
                var windows = graph.GateOpenWindows[g];
                for (int w = 0; w < windows.Length; w++)
                    if (tick >= windows[w].StartTick && tick < windows[w].EndTick)
                        return true;
                return false;
            }
            return true;
        }
    }
}
