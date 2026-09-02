using System;
using System.Collections.Generic;

namespace CatMetro.Domain
{
    // The ONE step implementation (ADR-0002 §2): solver, validator, runtime and capture rig all
    // call this symbol. Implements the authoritative per-tick order of operations at
    // docs/plan/specs/product_spec.md:218-227. Step 7 (score/combo) is deferred with scoring
    // (pin NEW-Q5 / Q-C); step 5 implements refusal/dwell/reverse plus NEW-Q35's W-auto acceptance.
    //
    // Micro-semantics adopted as handoff assumption A-C1-8 (golden fixture exercises none of them):
    //   (i)  an edge mouth is occupied iff any train on that edge has ProgressTicks == 0;
    //   (ii) node queues release their FIFO head at step 4 if the mouth is free — one release per
    //        node per tick, before same-tick arrivals resolve;
    //   (iii) an emission joins the back of the source queue if the queue is non-empty or the
    //        mouth is occupied; otherwise it enters the edge directly.
    //   (iv) ZERO-DWELL PASS-THROUGH (golden-defining; review finding F2): an arrival whose
    //        outgoing mouth is free departs in the same step and never touches the queue —
    //        this is product_spec.md:224 verbatim ("the train departs immediately on the
    //        switch's current route if the edge mouth is free; otherwise it enters the node
    //        queue"), which wins over the contract 13(d) "is enqueued" phrasing by the
    //        authority order. FIFO holds: a non-empty queue implies an occupied mouth, so an
    //        arrival can only bypass an EMPTY queue (reviewer-verified). Pinned by
    //        Step_NodeArrival_PassThroughLeavesQueueUntouched.
    // Trains that enter an edge during a tick do not advance that tick (their ProgressTicks stays
    // 0 until the next step 3), which is what makes "entered at tick t, arrives at t + travelTicks"
    // hold exactly (contract 13(c)/(d)).
    public static class Simulation
    {
        public const int RejectionDwellTicks = 8;

        // commandsThisTick: the commands due at THIS tick's step 1 — i.e. commands enqueued
        // during tick (state.Tick - 1), selected by the caller/runner (CM-R07.3).
        public static void Step(ref SimulationState state, ReadOnlySpan<ToggleSwitchCommand> commandsThisTick)
        {
            if (state.Outcome.Kind != OutcomeKind.Running) return;
            var g = state.Graph;
            int tick = state.Tick;
            var enteredThisTick = new HashSet<int>(); // train slot indices that entered an edge this tick

            // step 1 — apply commands in receipt order. The authored flip budget is a hard
            // accepted-command cap. A cooling switch ignores presses, except that a player command
            // received before a stray established a fresh automatic cooldown keeps receipt
            // priority on this one boundary. Cooldown present at the start of this processing tick
            // decrements only after rejecting this tick's presses, so an accepted flip locks
            // exactly N following processing ticks.
            var coolingAtStart = new bool[state.SwitchRoutes.Length];
            var freshAutoClaimed = new bool[state.SwitchRoutes.Length];
            for (int s = 0; s < state.SwitchRoutes.Length; s++)
                coolingAtStart[s] = SwitchState.Cooldown(state.SwitchRoutes[s]) > 0;
            for (int c = 0; c < commandsThisTick.Length; c++)
            {
                int sw = commandsThisTick[c].SwitchId;
                if (sw >= state.SwitchRoutes.Length)
                    throw new InvalidOperationException(
                        $"command names switch {sw} but the level has {state.SwitchRoutes.Length} — replay log does not belong to this level (F10)");
                if (!FlipBudget.CanAccept(g.PerfectMaxSwitches, state.SwitchesUsed)) continue;
                byte packed = state.SwitchRoutes[sw];
                bool freshAutoPriority = SwitchState.Cooldown(packed) > 0
                    && state.HasFreshAutomaticCooldown(sw)
                    && !freshAutoClaimed[sw];
                if (SwitchState.Cooldown(packed) > 0 && !freshAutoPriority) continue;
                int route = (SwitchState.Route(packed) + 1) % g.SwitchRoutes[sw].Length;
                state.SwitchRoutes[sw] = SwitchState.Pack(route, g.SwitchCooldownTicks[sw]);
                state.SwitchesUsed++;
                if (freshAutoPriority) freshAutoClaimed[sw] = true;
            }
            for (int s = 0; s < state.SwitchRoutes.Length; s++)
                if (coolingAtStart[s] && !freshAutoClaimed[s])
                {
                    byte packed = state.SwitchRoutes[s];
                    state.SwitchRoutes[s] = SwitchState.Pack(
                        SwitchState.Route(packed), SwitchState.Cooldown(packed) - 1);
                }
            // Fresh means exactly one following processing boundary. New stray presses later in
            // this Step set their bits again for the next boundary.
            state.FreshAutomaticCooldowns = 0;

            // step 2a — express trains held outside a source queue retry before new emissions.
            // They read the then-current selected route and never occupy queue digest slots.
            for (int t = 0; t < state.Trains.Length; t++)
            {
                if (state.Trains[t].State != TrainState.ExpressHeldAtSource) continue;
                int sourceNode = state.Trains[t].NodeId;
                var traversal = OutgoingTraversalFor(g, state, sourceNode);
                if (!traversal.IsValid)
                    throw new InvalidOperationException(
                        "source node has no outgoing edge — invalid fixture (F10)");
                if (state.NodeQueueCounts[sourceNode] == 0
                    && EdgeEntryOpen(g, traversal.EdgeId, tick)
                    && MouthFree(state, traversal))
                    EnterTraversal(ref state, t, traversal, enteredThisTick);
            }

            // step 2b — emit waves whose emission tick matches (wave.tick + k*spacing, k < count).
            // A single-count wave is spacing-independent; spacing <= 0 with count > 1 is refused
            // loudly at LevelGraph construction (F9) — no silent non-emission.
            for (int w = 0; w < g.WaveTick.Length; w++)
            {
                int offset = tick - g.WaveTick[w];
                if (offset < 0) continue;
                if (g.WaveCount[w] == 1)
                {
                    if (offset != 0) continue;
                }
                else
                {
                    if (offset % g.WaveSpacingTicks[w] != 0) continue;
                    if (offset / g.WaveSpacingTicks[w] >= g.WaveCount[w]) continue;
                }
                int slot = AllocateTrain(ref state);
                state.Trains[slot].Id = (short)(slot + 1); // 1-based; 0 = empty slot (A-C1-10)
                state.Trains[slot].Color = CatToken.Pack(
                    g.WaveColor[w], g.WaveShape[w], g.WaveStray[w], g.WaveExpress[w]);
                int sourceNode = g.WaveSourceNode[w];
                state.Trains[slot].NodeId = (short)sourceNode;
                var traversal = OutgoingTraversalFor(g, state, sourceNode);
                if (!traversal.IsValid)
                    throw new InvalidOperationException("source node has no outgoing edge — invalid fixture (F10)");
                if (state.NodeQueueCounts[sourceNode] == 0
                    && EdgeEntryOpen(g, traversal.EdgeId, tick)
                    && MouthFree(state, traversal))
                    EnterTraversal(ref state, slot, traversal, enteredThisTick);
                else if (g.WaveExpress[w])
                    HoldExpressAtSource(ref state, slot, sourceNode);
                else
                    Enqueue(ref state, sourceNode, slot);
            }

            // step 3 — advance trains that were already on an edge; collect arrivals. A refused
            // cat occupies its platform for the rejection tick plus seven following ticks, then
            // begins reverse traversal on tick nine. Reverse entry ignores edge occupancy; the
            // optional second-train rule detects opposing occupancy before any delivery.
            var arrivals = new List<int>(); // train slot indices, slot order = deterministic
            for (int t = 0; t < g.TrainsMax; t++)
            {
                if (state.Trains[t].State == TrainState.RejectedAtStation)
                {
                    state.Trains[t].ProgressTicks++;
                    if (state.Trains[t].ProgressTicks >= RejectionDwellTicks)
                        // Refusal escape is exceptional in every direction: it ignores both
                        // authored direction flags and gate state to preserve the exact dwell.
                        ReverseRejectedArrival(ref state, t, enteredThisTick);
                    continue;
                }
                if ((state.Trains[t].State != TrainState.OnEdge
                        && state.Trains[t].State != TrainState.OnEdgeReverse)
                    || enteredThisTick.Contains(t)) continue;
                state.Trains[t].ProgressTicks++;
                if (state.Trains[t].ProgressTicks >= g.EdgeTravelTicks[state.Trains[t].EdgeId])
                    arrivals.Add(t);
            }

            // The second-train mechanic makes either opposing edge occupancy or two same-tick
            // arrivals at one node fatal. Check before station deliveries are committed.
            bool collision = g.CollisionsEnabled
                && (HasOpposingEdgeCollision(g, state)
                    || HasSameNodeArrivalCollision(g, state, arrivals));
            var pendingDeliveries = new List<int>();

            if (!collision)
            {
                // step 4a — queued heads release first (A-C1-8 ii). A stray that already reached
                // a switch retains the route captured before its automatic press.
                for (int n = 0; n < g.NodeCount; n++)
                {
                    if (state.NodeQueueCounts[n] == 0) continue;
                    int head = state.NodeQueueSlots[n][0] - 1; // stored ids are 1-based
                    var traversal = QueuedTraversalFor(g, state, head, n);
                    if (traversal.IsValid
                        && EdgeEntryOpen(g, traversal.EdgeId, tick)
                        && MouthFree(state, traversal))
                    {
                        DequeueHead(ref state, n);
                        EnterTraversal(ref state, head, traversal, enteredThisTick);
                    }
                }

                // step 4b/5 — arrivals resolve: station acceptance, junction routing, bounce,
                // or enqueue. Matching deliveries are deferred until the post-routing collision
                // check, so a collision always wins this processing tick.
                foreach (int t in arrivals)
                {
                    int incomingEdge = state.Trains[t].EdgeId;
                    bool arrivedInReverse = state.Trains[t].State == TrainState.OnEdgeReverse;
                    int node = arrivedInReverse ? g.EdgeFrom[incomingEdge] : g.EdgeTo[incomingEdge];
                    state.Trains[t].State = TrainState.AtNode;
                    state.Trains[t].EdgeId = -1;
                    state.Trains[t].ProgressTicks = 0;
                    state.Trains[t].NodeId = (short)node;

                    byte token = state.Trains[t].Color;
                    bool stray = CatToken.IsStray(token);
                    bool express = CatToken.IsExpress(token);
                    int station = StationIndex(g, node);
                    if (station >= 0)
                    {
                        // A stray never delivers. Other Wild trains accept universally; concrete
                        // cats require both color and shape. Express mismatches bounce immediately,
                        // while every other refusal keeps the exact eight-tick platform contract.
                        if (!stray && Accepts(g, station, token))
                        {
                            pendingDeliveries.Add(t);
                            continue;
                        }

                        state.Rejections++;
                        if (express && !stray)
                            ReverseIncoming(ref state, t, incomingEdge, arrivedInReverse,
                                enteredThisTick);
                        else
                            RefuseAtStation(ref state, t, incomingEdge);
                        continue;
                    }

                    int switchId = SwitchIndexAtNode(g, node);
                    var traversal = OutgoingTraversalFor(g, state, node); // capture before press
                    if (stray && switchId >= 0)
                        PressSwitchForStray(ref state, switchId, coolingAtStart[switchId]);

                    bool mayEnter = traversal.IsValid
                        && EdgeEntryOpen(g, traversal.EdgeId, tick)
                        && MouthFree(state, traversal);
                    if (express)
                    {
                        if (mayEnter)
                            EnterTraversal(ref state, t, traversal, enteredThisTick);
                        else
                            ReverseIncoming(ref state, t, incomingEdge, arrivedInReverse,
                                enteredThisTick);
                    }
                    else if (mayEnter)
                    {
                        EnterTraversal(ref state, t, traversal, enteredThisTick);
                    }
                    else if (stray && switchId >= 0 && traversal.IsValid)
                    {
                        EnqueueCaptured(ref state, node, t, traversal);
                    }
                    else
                    {
                        Enqueue(ref state, node, t);
                    }
                }

                collision = g.CollisionsEnabled && HasOpposingEdgeCollision(g, state);
                if (!collision)
                    foreach (int t in pendingDeliveries)
                    {
                        state.Deliveries++;
                        state.Trains[t] = default; // delivered slot is zeroed (A-C1-10)
                    }
            }

            if (collision)
            {
                state.Outcome = SimOutcome.MakeFailed(FailReason.Collision);
            }
            else
            {
                // step 6a — station platform overflow is immediate. Capacity is simultaneous
                // refused cats, not lifetime rejections; equality is allowed.
                for (int station = 0; station < g.StationNode.Length; station++)
                {
                    int occupied = 0;
                    int stationNode = g.StationNode[station];
                    for (int t = 0; t < state.Trains.Length; t++)
                        if (state.Trains[t].State == TrainState.RejectedAtStation
                            && state.Trains[t].NodeId == stationNode)
                            occupied++;
                    if (occupied > g.StationCapacity[station])
                    {
                        state.Outcome = SimOutcome.MakeFailed(FailReason.PlatformOverflow);
                        break;
                    }
                }

                // step 6b — node queue overflow countdown.
                for (int n = 0; n < g.NodeCount; n++)
                {
                    if (state.Outcome.Kind != OutcomeKind.Running) break;
                    int cap = g.NodeQueueCapacity[n];
                    if (cap <= 0) continue;
                    if (state.NodeQueueCounts[n] >= cap)
                    {
                        if (state.OverloadTimers[n] == 0)
                        {
                            state.OverloadTimers[n] = 16; // 2 s countdown ring (CM-R02.5)
                            state.Overloads++;
                        }
                        else if (--state.OverloadTimers[n] == 0)
                        {
                            state.Outcome = SimOutcome.MakeFailed(FailReason.QueueOverflow);
                        }
                    }
                    else
                    {
                        state.OverloadTimers[n] = 0; // clearing space cancels Overload
                    }
                }
            }

            // step 7 — score/combo: deferred with scoring (Q-C); Score/Chain stay 0.

            state.Tick = tick + 1;

            // step 8 — win first, then time (A-C1-11 tie rule)
            if (state.Outcome.Kind == OutcomeKind.Running)
            {
                if (state.Deliveries >= g.WinDeliveries)
                    state.Outcome = SimOutcome.Won;
                else if (state.Tick >= g.TimeLimitTicks)
                    state.Outcome = SimOutcome.MakeFailed(FailReason.TimeOut);
            }
            if (state.Outcome.Kind != OutcomeKind.Running)
                state.FreshAutomaticCooldowns = 0;
        }

        private static int AllocateTrain(ref SimulationState state)
        {
            for (int t = 0; t < state.Trains.Length; t++)
                if (state.Trains[t].State == TrainState.None && state.Trains[t].Id == 0)
                    return t;
            throw new InvalidOperationException("TrainsMax exceeded — fixture outside its digest envelope (A-C1-7)");
        }

        private static bool MouthFree(SimulationState state, EdgeTraversal traversal)
        {
            byte stateAtMouth = traversal.Reverse ? TrainState.OnEdgeReverse : TrainState.OnEdge;
            for (int t = 0; t < state.Trains.Length; t++)
                if (state.Trains[t].State == stateAtMouth
                    && state.Trains[t].EdgeId == traversal.EdgeId
                    && state.Trains[t].ProgressTicks == 0)
                    return false;
            return true;
        }

        private static void EnterTraversal(ref SimulationState state, int slot,
            EdgeTraversal traversal, HashSet<int> enteredThisTick)
        {
            if (traversal.Reverse)
                EnterReverseEdge(ref state, slot, traversal.EdgeId, enteredThisTick);
            else
                EnterEdge(ref state, slot, traversal.EdgeId, enteredThisTick);
        }

        private static void EnterEdge(ref SimulationState state, int slot, int edgeId, HashSet<int> enteredThisTick)
        {
            state.Trains[slot].State = TrainState.OnEdge;
            state.Trains[slot].EdgeId = (short)edgeId;
            state.Trains[slot].ProgressTicks = 0;
            enteredThisTick.Add(slot);
        }

        private static void EnterReverseEdge(ref SimulationState state, int slot, int edgeId,
            HashSet<int> enteredThisTick)
        {
            state.Trains[slot].State = TrainState.OnEdgeReverse;
            state.Trains[slot].EdgeId = (short)edgeId;
            state.Trains[slot].ProgressTicks = 0;
            enteredThisTick.Add(slot);
        }

        private static void ReverseIncoming(ref SimulationState state, int slot, int incomingEdge,
            bool arrivedInReverse, HashSet<int> enteredThisTick)
        {
            // The bounce is exceptional: ignore gate/direction/mouth state. Reversing an ordinary
            // forward arrival travels toward EdgeFrom; reversing a reverse arrival travels back
            // toward EdgeTo.
            if (arrivedInReverse)
                EnterEdge(ref state, slot, incomingEdge, enteredThisTick);
            else
                EnterReverseEdge(ref state, slot, incomingEdge, enteredThisTick);
        }

        private static void HoldExpressAtSource(ref SimulationState state, int slot, int node)
        {
            state.Trains[slot].State = TrainState.ExpressHeldAtSource;
            state.Trains[slot].EdgeId = -1;
            state.Trains[slot].ProgressTicks = 0;
            state.Trains[slot].NodeId = (short)node;
        }

        private static void RefuseAtStation(ref SimulationState state, int slot, int incomingEdge)
        {
            state.Trains[slot].State = TrainState.RejectedAtStation;
            state.Trains[slot].EdgeId = (short)incomingEdge;
            state.Trains[slot].ProgressTicks = 0;
        }

        private static void ReverseRejectedArrival(
            ref SimulationState state, int slot, HashSet<int> enteredThisTick)
        {
            var train = state.Trains[slot];
            int edge = train.EdgeId;
            int rejectedNode = train.NodeId;
            var graph = state.Graph;

            // NodeId remains the station endpoint throughout the dwell, so it preserves which
            // way the train arrived without widening the fixed ten-byte train digest. A forward
            // arrival ends at EdgeTo and leaves in reverse; a reverse arrival ends at EdgeFrom
            // and must leave forward. Using only EdgeId here made reverse arrivals teleport to
            // EdgeTo and drive back into the same rejecting station.
            if (graph.EdgeTo[edge] == rejectedNode && graph.EdgeFrom[edge] != rejectedNode)
            {
                EnterReverseEdge(ref state, slot, edge, enteredThisTick);
                return;
            }
            if (graph.EdgeFrom[edge] == rejectedNode)
            {
                EnterEdge(ref state, slot, edge, enteredThisTick);
                return;
            }

            throw new InvalidOperationException(
                $"rejected train node {rejectedNode} is not on incoming edge {edge}");
        }

        private static void Enqueue(ref SimulationState state, int node, int slot)
        {
            int count = state.NodeQueueCounts[node];
            if (count >= state.Graph.QCapBound)
                throw new InvalidOperationException("queue exceeded the digest slot bound qCap — fixture outside its envelope (A-C1-7/A-C1-12)");
            state.NodeQueueSlots[node][count] = state.Trains[slot].Id;
            state.NodeQueueCounts[node] = (byte)(count + 1);
            state.Trains[slot].State = TrainState.AtNode;
            state.Trains[slot].EdgeId = -1;
            state.Trains[slot].ProgressTicks = 0;
            state.Trains[slot].NodeId = (short)node;
        }

        private static void EnqueueCaptured(ref SimulationState state, int node, int slot,
            EdgeTraversal traversal)
        {
            Enqueue(ref state, node, slot);
            state.Trains[slot].EdgeId = (short)traversal.EdgeId;
        }

        private static void DequeueHead(ref SimulationState state, int node)
        {
            int count = state.NodeQueueCounts[node];
            var slots = state.NodeQueueSlots[node];
            for (int q = 1; q < count; q++) slots[q - 1] = slots[q];
            slots[count - 1] = 0;
            state.NodeQueueCounts[node] = (byte)(count - 1);
        }

        private static EdgeTraversal QueuedTraversalFor(
            LevelGraph g, SimulationState state, int slot, int node)
        {
            var train = state.Trains[slot];
            if (CatToken.IsStray(train.Color) && train.EdgeId >= 0)
                return TraversalFromNode(g, node, train.EdgeId);
            return OutgoingTraversalFor(g, state, node);
        }

        private static int SwitchIndexAtNode(LevelGraph g, int node)
        {
            LevelGraph g = state.Graph;
            for (int s = 0; s < g.SwitchNode.Length; s++)
                if (g.SwitchNode[s] == node)
                    return s;
            return -1;
        }

        private static void PressSwitchForStray(
            ref SimulationState state, int switchId, bool coolingAtTickStart)
        {
            byte packed = state.SwitchRoutes[switchId];
            if (coolingAtTickStart || SwitchState.Cooldown(packed) > 0) return;
            var g = state.Graph;
            int route = (SwitchState.Route(packed) + 1) % g.SwitchRoutes[switchId].Length;
            state.SwitchRoutes[switchId] = SwitchState.Pack(
                route, g.SwitchCooldownTicks[switchId]);
            if (g.SwitchCooldownTicks[switchId] > 0)
                state.FreshAutomaticCooldowns |= (ushort)(1 << switchId);
            // Automatic presses deliberately neither consult the hard player cap nor increment
            // SwitchesUsed. The packed cooldown is still authoritative for later presses.
        }

        private static bool HasSameNodeArrivalCollision(
            LevelGraph g, SimulationState state, List<int> arrivals)
        {
            var countByNode = new int[g.NodeCount];
            foreach (int slot in arrivals)
            {
                var train = state.Trains[slot];
                int node = train.State == TrainState.OnEdgeReverse
                    ? g.EdgeFrom[train.EdgeId]
                    : g.EdgeTo[train.EdgeId];
                countByNode[node]++;
                if (countByNode[node] >= 2) return true;
            }
            return false;
        }

        private static bool HasOpposingEdgeCollision(LevelGraph g, SimulationState state)
        {
            var forward = new bool[g.EdgeFrom.Length];
            var reverse = new bool[g.EdgeFrom.Length];
            foreach (var train in state.Trains)
            {
                if (train.State == TrainState.OnEdge)
                {
                    if (reverse[train.EdgeId]) return true;
                    forward[train.EdgeId] = true;
                }
                else if (train.State == TrainState.OnEdgeReverse)
                {
                    if (forward[train.EdgeId]) return true;
                    reverse[train.EdgeId] = true;
                }
            }
            return false;
        }

        // The traversal out of a node: a switch selects its current incident edge. Without a
        // switch, authored forward edges win before an eligible reverse edge. A closed gate never
        // selects an alternate traversal; the caller leaves the train queued on the chosen route.
        private static EdgeTraversal OutgoingTraversalFor(
            LevelGraph g, SimulationState state, int node)
        {
            for (int s = 0; s < g.SwitchNode.Length; s++)
                if (g.SwitchNode[s] == node)
                {
                    int route = SwitchState.Route(state.SwitchRoutes[s]);
                    return TraversalFromNode(g, node, g.SwitchRoutes[s][route]);
                }
            for (int e = 0; e < g.EdgeFrom.Length; e++)
                if (g.EdgeFrom[e] == node)
                    return new EdgeTraversal(e, reverse: false);
            for (int e = 0; e < g.EdgeTo.Length; e++)
                if (g.EdgeTo[e] == node && (!g.EdgeOneWay[e] || g.EdgeReversible[e]))
                    return new EdgeTraversal(e, reverse: true);
            return EdgeTraversal.Invalid;
        }

        private static EdgeTraversal TraversalFromNode(LevelGraph g, int node, int edge)
        {
            if (edge < 0 || edge >= g.EdgeFrom.Length) return EdgeTraversal.Invalid;
            if (g.EdgeFrom[edge] == node) return new EdgeTraversal(edge, reverse: false);
            if (g.EdgeTo[edge] == node && (!g.EdgeOneWay[edge] || g.EdgeReversible[edge]))
                return new EdgeTraversal(edge, reverse: true);
            return EdgeTraversal.Invalid;
        }

        private static bool EdgeEntryOpen(LevelGraph g, int edge, int tick)
        {
            for (int gate = 0; gate < g.GateEdge.Length; gate++)
            {
                if (g.GateEdge[gate] != edge) continue;
                var windows = g.GateOpenWindows[gate];
                for (int w = 0; w < windows.Length; w++)
                    if (tick >= windows[w].StartTick && tick < windows[w].EndTick)
                        return true;
                return false;
            }
            return true;
        }

        private readonly struct EdgeTraversal
        {
            public static readonly EdgeTraversal Invalid = new EdgeTraversal(-1, false);

            public readonly int EdgeId;
            public readonly bool Reverse;
            public bool IsValid => EdgeId >= 0;

            public EdgeTraversal(int edgeId, bool reverse)
            {
                EdgeId = edgeId;
                Reverse = reverse;
            }
        }

        private static int StationIndex(LevelGraph g, int node)
        {
            for (int s = 0; s < g.StationNode.Length; s++)
                if (g.StationNode[s] == node)
                    return s;
            return -1;
        }

        private static bool Accepts(LevelGraph g, int station, byte token)
        {
            byte color = CatToken.Color(token);
            // Train-side Wild auto-accepts regardless of station shape. A Wild token authored on
            // the station side remains exact-only and therefore does not accept a concrete train.
            if (color == CatColor.Wild) return true;
            if (CatToken.Shape(token) != g.StationShape[station]) return false;
            var accepts = g.StationAccepts[station];
            for (int i = 0; i < accepts.Length; i++)
                if (accepts[i] == color)
                    return true;
            return false;
        }
    }
}
