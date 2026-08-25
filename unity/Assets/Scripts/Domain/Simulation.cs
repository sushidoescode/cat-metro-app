using System;
using System.Collections.Generic;

namespace CatMetro.Domain
{
    // The ONE step implementation (ADR-0002 §2): solver, validator, runtime and capture rig all
    // call this symbol. Implements the authoritative per-tick order of operations at
    // docs/plan/specs/product_spec.md:218-227. Step 7 (score/combo) is deferred with scoring
    // (pin NEW-Q5 / Q-C); step 5 carries NEW-Q4 plus NEW-Q35's ratified W-auto acceptance.
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
        // commandsThisTick: the commands due at THIS tick's step 1 — i.e. commands enqueued
        // during tick (state.Tick - 1), selected by the caller/runner (CM-R07.3).
        public static void Step(ref SimulationState state, ReadOnlySpan<ToggleSwitchCommand> commandsThisTick)
        {
            if (state.Outcome.Kind != OutcomeKind.Running) return;
            var g = state.Graph;
            int tick = state.Tick;
            var enteredThisTick = new HashSet<int>(); // train slot indices that entered an edge this tick

            // step 1 — apply commands in receipt order
            for (int c = 0; c < commandsThisTick.Length; c++)
            {
                int sw = commandsThisTick[c].SwitchId;
                if (sw >= state.SwitchRoutes.Length)
                    throw new InvalidOperationException(
                        $"command names switch {sw} but the level has {state.SwitchRoutes.Length} — replay log does not belong to this level (F10)");
                state.SwitchRoutes[sw] = (byte)((state.SwitchRoutes[sw] + 1) % g.SwitchRoutes[sw].Length);
                state.SwitchesUsed++;
            }

            // step 2 — emit waves whose emission tick matches (wave.tick + k*spacing, k < count).
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
                state.Trains[slot].Color = g.WaveColor[w];
                int sourceNode = g.WaveSourceNode[w];
                state.Trains[slot].NodeId = (short)sourceNode;
                int outEdge = SingleOutgoingEdge(g, sourceNode, state);
                if (outEdge < 0)
                    throw new InvalidOperationException("source node has no outgoing edge — invalid fixture (F10)");
                if (state.NodeQueueCounts[sourceNode] == 0 && MouthFree(state, outEdge))
                    EnterEdge(ref state, slot, outEdge, enteredThisTick);
                else
                    Enqueue(ref state, sourceNode, slot);
            }

            // step 3 — advance trains that were already on an edge; collect arrivals
            var arrivals = new List<int>(); // train slot indices, slot order = deterministic
            for (int t = 0; t < g.TrainsMax; t++)
            {
                if (state.Trains[t].State != TrainState.OnEdge || enteredThisTick.Contains(t)) continue;
                state.Trains[t].ProgressTicks++;
                if (state.Trains[t].ProgressTicks >= g.EdgeTravelTicks[state.Trains[t].EdgeId])
                    arrivals.Add(t);
            }

            // step 4a — queued heads release first (A-C1-8 ii)
            for (int n = 0; n < g.NodeCount; n++)
            {
                if (state.NodeQueueCounts[n] == 0) continue;
                int head = state.NodeQueueSlots[n][0] - 1; // stored ids are 1-based
                int outEdge = OutgoingEdgeFor(g, state, n);
                if (outEdge >= 0 && MouthFree(state, outEdge))
                {
                    DequeueHead(ref state, n);
                    EnterEdge(ref state, head, outEdge, enteredThisTick);
                }
            }

            // step 4b/5 — arrivals resolve: station acceptance, junction routing, or enqueue
            foreach (int t in arrivals)
            {
                int node = g.EdgeTo[state.Trains[t].EdgeId];
                state.Trains[t].State = TrainState.AtNode;
                state.Trains[t].EdgeId = -1;
                state.Trains[t].ProgressTicks = 0;
                state.Trains[t].NodeId = (short)node;

                int station = StationIndex(g, node);
                if (station >= 0)
                {
                    // step 5 — match only; non-match is pinned out (NEW-Q4, criterion 14)
                    if (!Accepts(g, station, state.Trains[t].Color))
                        throw new NotSupportedException(
                            "pinned NEW-Q4: a non-matching cat arrived at a station — rejection/reverse traversal is out of CM-C1 scope (state/backlog.md Q-B, criterion 14)");
                    state.Deliveries++;
                    state.Trains[t] = default; // delivered slot is zeroed (A-C1-10)
                    continue;
                }

                int route = OutgoingEdgeFor(g, state, node);
                if (route >= 0 && MouthFree(state, route))
                    EnterEdge(ref state, t, route, enteredThisTick);
                else
                    Enqueue(ref state, node, t);
            }

            // step 6 — overflow checks (queue overflow only; platform overflow is pinned, Q-J)
            for (int n = 0; n < g.NodeCount; n++)
            {
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

            // step 7 — score/combo: deferred with scoring (Q-C); Score/Chain stay 0.

            state.Tick = tick + 1;

            // step 8 — win first, then time (A-C1-11 tie rule)
            if (state.Outcome.Kind != OutcomeKind.Running) return;
            if (state.Deliveries >= g.WinDeliveries)
                state.Outcome = SimOutcome.Won;
            else if (state.Tick >= g.TimeLimitTicks)
                state.Outcome = SimOutcome.MakeFailed(FailReason.TimeOut);
        }

        private static int AllocateTrain(ref SimulationState state)
        {
            for (int t = 0; t < state.Trains.Length; t++)
                if (state.Trains[t].State == TrainState.None && state.Trains[t].Id == 0)
                    return t;
            throw new InvalidOperationException("TrainsMax exceeded — fixture outside its digest envelope (A-C1-7)");
        }

        private static bool MouthFree(SimulationState state, int edgeId)
        {
            for (int t = 0; t < state.Trains.Length; t++)
                if (state.Trains[t].State == TrainState.OnEdge && state.Trains[t].EdgeId == edgeId && state.Trains[t].ProgressTicks == 0)
                    return false;
            return true;
        }

        private static void EnterEdge(ref SimulationState state, int slot, int edgeId, HashSet<int> enteredThisTick)
        {
            state.Trains[slot].State = TrainState.OnEdge;
            state.Trains[slot].EdgeId = (short)edgeId;
            state.Trains[slot].ProgressTicks = 0;
            enteredThisTick.Add(slot);
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

        private static void DequeueHead(ref SimulationState state, int node)
        {
            int count = state.NodeQueueCounts[node];
            var slots = state.NodeQueueSlots[node];
            for (int q = 1; q < count; q++) slots[q - 1] = slots[q];
            slots[count - 1] = 0;
            state.NodeQueueCounts[node] = (byte)(count - 1);
        }

        // The route out of a node: the node's switch's current route if it has one, else its
        // single outgoing edge, else -1 (terminal). Multiple switch-less outgoing edges do not
        // occur in CM-C1 fixtures.
        private static int OutgoingEdgeFor(LevelGraph g, SimulationState state, int node)
        {
            for (int s = 0; s < g.SwitchNode.Length; s++)
                if (g.SwitchNode[s] == node)
                    return g.SwitchRoutes[s][state.SwitchRoutes[s]];
            for (int e = 0; e < g.EdgeFrom.Length; e++)
                if (g.EdgeFrom[e] == node)
                    return e;
            return -1;
        }

        private static int SingleOutgoingEdge(LevelGraph g, int node, SimulationState state) => OutgoingEdgeFor(g, state, node);

        private static int StationIndex(LevelGraph g, int node)
        {
            for (int s = 0; s < g.StationNode.Length; s++)
                if (g.StationNode[s] == node)
                    return s;
            return -1;
        }

        private static bool Accepts(LevelGraph g, int station, byte color)
        {
            // NEW-Q35: Wild is a train color and auto-accepts at the first station reached.
            // A Wild token authored on the station side remains exact-only and therefore does not
            // accept a concrete train.
            if (color == CatColor.Wild) return true;
            var accepts = g.StationAccepts[station];
            for (int i = 0; i < accepts.Length; i++)
                if (accepts[i] == color)
                    return true;
            return false;
        }
    }
}
