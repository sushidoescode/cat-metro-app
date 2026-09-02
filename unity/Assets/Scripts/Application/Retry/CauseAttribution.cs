using CatMetro.Domain;

namespace CatMetro.Application.Retry
{
    // CM-C3 criterion 1: the causal node, derived FROM STATE at the fail tick — A-C3-1 is a
    // confirmed finding: the shipped outcome carries Kind + Reason only, no node id, and
    // changing the Domain is stop condition 1. Rules:
    //   QueueOverflow — the node whose Overload countdown expired: timer == 0 with a still-full
    //     queue; lowest node index if several expired the same tick (deterministic).
    //   TimeOut — no single causing node exists; the ANALYST-AUTHORED, UNRATIFIED rule (A-C3-2,
    //     Q-K, named in the PR): the node with the largest queue at the fail tick, ties broken
    //     by the lowest node id. Overruling Q-K costs the camera tree only.
    //   PlatformOverflow — the station whose simultaneous refused-cat occupancy exceeded its
    //     capacity; authored station order breaks an impossible same-tick tie deterministically.
    public static class CauseAttribution
    {
        // Returns the causal node index, or -1 when no rule applies (the ambiguous variant:
        // camera on nothing new, no blame — S-03's locked empty-state behaviour).
        public static int CausalNode(SimulationState state)
        {
            if (state == null || state.Outcome.Kind != OutcomeKind.Failed) return -1;

            if (state.Outcome.Reason == FailReason.QueueOverflow)
            {
                for (int n = 0; n < state.NodeQueueCounts.Length; n++)
                {
                    int cap = state.Graph.NodeQueueCapacity[n];
                    if (cap > 0 && state.OverloadTimers[n] == 0 && state.NodeQueueCounts[n] >= cap)
                        return n;
                }
                return -1;
            }

            if (state.Outcome.Reason == FailReason.TimeOut)
            {
                int best = -1, bestCount = -1;
                for (int n = 0; n < state.NodeQueueCounts.Length; n++)
                    if (state.NodeQueueCounts[n] > bestCount) // strict > keeps the lowest index
                    {
                        bestCount = state.NodeQueueCounts[n];
                        best = n;
                    }
                return best;
            }

            if (state.Outcome.Reason == FailReason.PlatformOverflow)
            {
                for (int station = 0; station < state.Graph.StationNode.Length; station++)
                {
                    int node = state.Graph.StationNode[station];
                    int occupied = 0;
                    for (int t = 0; t < state.Trains.Length; t++)
                        if (state.Trains[t].State == TrainState.RejectedAtStation
                            && state.Trains[t].NodeId == node)
                            occupied++;
                    if (occupied > state.Graph.StationCapacity[station]) return node;
                }
            }

            if (state.Outcome.Reason == FailReason.Collision)
            {
                // Same-tick arrivals remain on their completed edges because collision wins
                // before arrival resolution. Prefer that shared junction; otherwise an opposing
                // edge collision frames the lower authored edge's destination endpoint.
                var arrivals = new int[state.Graph.NodeCount];
                for (int t = 0; t < state.Trains.Length; t++)
                {
                    var train = state.Trains[t];
                    if ((train.State != TrainState.OnEdge
                            && train.State != TrainState.OnEdgeReverse)
                        || train.ProgressTicks < state.Graph.EdgeTravelTicks[train.EdgeId])
                        continue;
                    int node = train.State == TrainState.OnEdgeReverse
                        ? state.Graph.EdgeFrom[train.EdgeId]
                        : state.Graph.EdgeTo[train.EdgeId];
                    if (++arrivals[node] >= 2) return node;
                }

                for (int edge = 0; edge < state.Graph.EdgeFrom.Length; edge++)
                {
                    bool forward = false, reverse = false;
                    for (int t = 0; t < state.Trains.Length; t++)
                    {
                        var train = state.Trains[t];
                        if (train.EdgeId != edge) continue;
                        forward |= train.State == TrainState.OnEdge;
                        reverse |= train.State == TrainState.OnEdgeReverse;
                    }
                    if (forward && reverse) return state.Graph.EdgeTo[edge];
                }
            }

            return -1;
        }
    }
}
