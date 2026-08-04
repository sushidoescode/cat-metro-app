using System;
using System.Collections.Generic;

namespace CatMetro.Domain.Solver
{
    // CM-C4: exact BFS for <=2-switch boards, beam search at the authored widths beyond, sharing
    // the ONE shipped Simulation.Step symbol (ADR-0002 §2). States are built exclusively through
    // CreateInitial + Step re-simulation — the solver never writes a state field (criterion 2's
    // grep enforces it), and every branch replays its log from the origin, which is also what
    // keeps parent states unshared across branches.
    //
    // Provenance: the hybrid lane's local executor did not converge on this cut (escalation
    // packet state/hybrid-escalations/cm-c4-g2-bfs-exact.md); frontier implementation per the
    // two-strike rule. Semantics per state/handoffs/CM-C4.md planner rulings.
    public static class LevelSolver
    {
        public static SolveResult Solve(LevelGraph graph, ulong seed,
            int maxNodesExpanded = SolverBounds.MAX_NODES_EXPANDED,
            int[] beamWidths = null)
        {
            var widths = beamWidths ?? SolverBounds.BEAM_WIDTHS;

            if (graph.SwitchRoutes.Length <= 2)
                return Search(graph, seed, maxNodesExpanded, int.MaxValue, 0, exhaustiveIsProof: true);

            SolveResult last = null;
            foreach (var w in widths)
            {
                last = Search(graph, seed, maxNodesExpanded, w, w, exhaustiveIsProof: false);
                if (last.Verdict == SolveVerdict.Solved || last.NotFoundReason == NotFoundReason.Budget)
                    return last;
            }
            // Missed at every width: NotFound(Beam, last width) — never Unsolvable (ADR-0008:117).
            return new SolveResult(SolveVerdict.NotFound, NotFoundReason.Beam, new CommandLog(),
                0, 0, widths[widths.Length - 1], last?.PinnedPruned ?? 0, last?.NodesExpanded ?? 0,
                last?.FirstPinMessage ?? "", ZeroProxy(graph));
        }

        private static DifficultyProxy ZeroProxy(LevelGraph graph) =>
            new DifficultyProxy(0, 0, graph.TimeLimitTicks, 0, 0, 0);

        // One search core for both modes: width == int.MaxValue is BFS (exhaustion proves
        // Unsolvable/Indeterminate); a finite width is one beam leg (miss is only a miss).
        private static SolveResult Search(LevelGraph graph, ulong seed, int budget, int width,
            int reportWidth, bool exhaustiveIsProof)
        {
            int pinnedPruned = 0;
            string firstPin = "";
            int nodesExpanded = 0;
            int timeLimit = graph.TimeLimitTicks;

            // Layer L = logs whose replay has taken exactly L steps and is still Running.
            var layer = new List<CommandLog> { new CommandLog() };
            var seen = new HashSet<string>();

            for (int depth = 0; depth < timeLimit && layer.Count > 0; depth++)
            {
                var wins = new List<(int ticks, CommandLog log)>();
                // Within-layer dedupe resolves collisions by the criterion-7 comparator, NOT by
                // expansion order — otherwise the untoggled-prefix branch (always expanded first)
                // claims every converged state and the canonical log carries the LATEST possible
                // toggle, inverting the tie-break.
                var next = new Dictionary<string, (CommandLog log, SimulationState state)>();

                foreach (var log in layer)
                {
                    nodesExpanded++;
                    if (nodesExpanded > budget)
                        return new SolveResult(SolveVerdict.NotFound, NotFoundReason.Budget,
                            new CommandLog(), 0, 0, reportWidth, pinnedPruned, nodesExpanded,
                            firstPin, ZeroProxy(graph));

                    // Transition depth -> depth+1: the (depth+1)th Step call has entry tick
                    // == depth, so its due commands carry entry.Tick == depth - 1. The very
                    // first step is uncommandable by Domain design.
                    foreach (var combo in Combos(graph, depth - 1, depth == 0))
                    {
                        var childLog = Extend(log, combo);
                        SimulationState state;
                        try
                        {
                            state = ReplayTo(graph, seed, childLog, depth + 1);
                        }
                        catch (NotSupportedException e)
                        {
                            pinnedPruned++;
                            if (firstPin.Length == 0) firstPin = e.Message;
                            continue;
                        }

                        if (state.Outcome.Kind == OutcomeKind.Won)
                        {
                            int t = state.Tick; // never write state fields; read once
                            wins.Add((t - 1, childLog));
                        }
                        else if (state.Outcome.Kind == OutcomeKind.Running)
                        {
                            var key = DigestKey(state);
                            if (!seen.Contains(key)) // earlier layers always win outright
                            {
                                if (!next.TryGetValue(key, out var incumbent)
                                    || CompareWins((0, childLog), (0, incumbent.log)) < 0)
                                    next[key] = (childLog, state);
                            }
                        }
                        // Failed states are dead branches.
                    }
                }

                if (wins.Count > 0)
                {
                    wins.Sort(CompareWins);
                    var best = wins[0];
                    return new SolveResult(SolveVerdict.Solved, NotFoundReason.None, best.log,
                        best.ticks, best.log.Entries.Count, reportWidth, pinnedPruned,
                        nodesExpanded, firstPin,
                        ComputeProxy(graph, seed, best.log, best.ticks));
                }

                var survivors = new List<(CommandLog log, SimulationState state)>(next.Count);
                foreach (var kv in next)
                {
                    seen.Add(kv.Key);
                    survivors.Add(kv.Value);
                }
                // Deterministic layer order regardless of dictionary iteration: beam order.
                survivors.Sort(CompareBeam);
                if (width != int.MaxValue && survivors.Count > width)
                    survivors.RemoveRange(width, survivors.Count - width);

                layer = new List<CommandLog>(survivors.Count);
                foreach (var (log, _) in survivors) layer.Add(log);
            }

            if (!exhaustiveIsProof)
                return new SolveResult(SolveVerdict.NotFound, NotFoundReason.Beam, new CommandLog(),
                    0, 0, reportWidth, pinnedPruned, nodesExpanded, firstPin, ZeroProxy(graph));
            if (pinnedPruned > 0)
                return new SolveResult(SolveVerdict.Indeterminate, NotFoundReason.None, new CommandLog(),
                    0, 0, reportWidth, pinnedPruned, nodesExpanded, firstPin, ZeroProxy(graph));
            return new SolveResult(SolveVerdict.Unsolvable, NotFoundReason.None, new CommandLog(),
                0, 0, reportWidth, pinnedPruned, nodesExpanded, firstPin, ZeroProxy(graph));
        }

        // The criterion-7 total order: fewer completion ticks, then fewer commands, then
        // lexicographic over (Tick, SwitchId) pairs.
        private static int CompareWins((int ticks, CommandLog log) a, (int ticks, CommandLog log) b)
        {
            if (a.ticks != b.ticks) return a.ticks.CompareTo(b.ticks);
            if (a.log.Entries.Count != b.log.Entries.Count) return a.log.Entries.Count.CompareTo(b.log.Entries.Count);
            for (int i = 0; i < a.log.Entries.Count; i++)
            {
                var ea = a.log.Entries[i];
                var eb = b.log.Entries[i];
                if (ea.Tick != eb.Tick) return ea.Tick.CompareTo(eb.Tick);
                if (ea.SwitchId != eb.SwitchId) return ea.SwitchId.CompareTo(eb.SwitchId);
            }
            return 0;
        }

        private static int CompareBeam((CommandLog log, SimulationState state) a, (CommandLog log, SimulationState state) b)
        {
            if (a.state.Deliveries != b.state.Deliveries) return b.state.Deliveries.CompareTo(a.state.Deliveries);
            var da = Digest(a.state);
            var db = Digest(b.state);
            for (int i = 0; i < da.Length && i < db.Length; i++)
                if (da[i] != db[i]) return da[i].CompareTo(db[i]);
            return da.Length.CompareTo(db.Length);
        }

        // Per switch, k = 0..routeCount-1 toggles at the given entry tick (k identical entries,
        // appended in switch-id order), cartesian across switches. On the uncommandable first
        // transition only the empty combo exists.
        private static IEnumerable<ToggleSwitchCommand[]> Combos(LevelGraph graph, int entryTick, bool emptyOnly)
        {
            if (emptyOnly)
            {
                yield return Array.Empty<ToggleSwitchCommand>();
                yield break;
            }
            int switches = graph.SwitchRoutes.Length;
            var counts = new int[switches];
            var k = new int[switches];
            for (int s = 0; s < switches; s++) counts[s] = graph.SwitchRoutes[s].Length;

            while (true)
            {
                int total = 0;
                for (int s = 0; s < switches; s++) total += k[s];
                var combo = new ToggleSwitchCommand[total];
                int idx = 0;
                for (int s = 0; s < switches; s++)
                    for (int i = 0; i < k[s]; i++)
                        combo[idx++] = new ToggleSwitchCommand((ushort)s, entryTick);
                yield return combo;

                int carry = 1;
                for (int s = 0; s < switches && carry > 0; s++)
                {
                    k[s]++;
                    if (k[s] >= counts[s]) { k[s] = 0; }
                    else carry = 0;
                }
                if (carry > 0) yield break;
            }
        }

        private static CommandLog Extend(CommandLog log, ToggleSwitchCommand[] combo)
        {
            if (combo.Length == 0 && log.Entries.Count == 0) return log; // the empty log is shareable
            var child = new CommandLog();
            foreach (var e in log.Entries) child.Append(e);
            foreach (var c in combo) child.Append(c);
            return child;
        }

        // Replays exactly `steps` Step calls from the origin, feeding each call the entries due
        // at its boundary (entry.Tick == entry-tick-of-call - 1). States are reached only
        // through the one Step symbol.
        private static SimulationState ReplayTo(LevelGraph graph, ulong seed, CommandLog log, int steps)
        {
            var state = SimulationState.CreateInitial(graph, seed);
            for (int i = 0; i < steps && state.Outcome.Kind == OutcomeKind.Running; i++)
            {
                Simulation.Step(ref state, Due(log, state.Tick));
            }
            return state;
        }

        private static ReadOnlySpan<ToggleSwitchCommand> Due(CommandLog log, int entryTick)
        {
            if (log.Entries.Count == 0) return ReadOnlySpan<ToggleSwitchCommand>.Empty;
            List<ToggleSwitchCommand> due = null;
            foreach (var e in log.Entries)
                if (e.Tick == entryTick - 1)
                    (due ?? (due = new List<ToggleSwitchCommand>())).Add(e);
            return due == null ? ReadOnlySpan<ToggleSwitchCommand>.Empty : due.ToArray();
        }

        private static byte[] Digest(SimulationState state)
        {
            var d = new byte[state.DigestLength()];
            state.WriteDigest(d);
            return d;
        }

        private static string DigestKey(SimulationState state) => Convert.ToBase64String(Digest(state));

        // Difficulty proxy per the handoff rulings — populated only for a Solved verdict.
        private static DifficultyProxy ComputeProxy(LevelGraph graph, ulong seed, CommandLog log, int completionTicks)
        {
            int maxPending = 0;
            int peakQueued = -1;
            int slackAtPeak = 0;
            int minCapacity = int.MaxValue;
            for (int n = 0; n < graph.NodeCount; n++)
                if (graph.NodeQueueCapacity[n] > 0 && graph.NodeQueueCapacity[n] < minCapacity)
                    minCapacity = graph.NodeQueueCapacity[n];

            var state = SimulationState.CreateInitial(graph, seed);
            while (state.Outcome.Kind == OutcomeKind.Running)
            {
                Simulation.Step(ref state, Due(log, state.Tick));

                // Axis C: switches with a train inbound (on an edge into the switch node) or
                // queued at the switch node.
                int pending = 0;
                for (int s = 0; s < graph.SwitchNode.Length; s++)
                {
                    int node = graph.SwitchNode[s];
                    bool active = state.NodeQueueCounts[node] > 0;
                    if (!active)
                    {
                        foreach (var tr in state.Trains)
                        {
                            if (tr.State == TrainState.OnEdge && graph.EdgeTo[tr.EdgeId] == node)
                            {
                                active = true;
                                break;
                            }
                        }
                    }
                    if (active) pending++;
                }
                if (pending > maxPending) maxPending = pending;

                // Axis H (queue term only, PARTIAL(Q-J)): find the peak-load tick and the
                // minimum capacity slack at it; earliest peak wins ties.
                int totalQueued = 0;
                for (int n = 0; n < graph.NodeCount; n++) totalQueued += state.NodeQueueCounts[n];
                if (totalQueued > peakQueued)
                {
                    peakQueued = totalQueued;
                    int minSlack = int.MaxValue;
                    for (int n = 0; n < graph.NodeCount; n++)
                    {
                        if (graph.NodeQueueCapacity[n] <= 0) continue;
                        int slack = graph.NodeQueueCapacity[n] - state.NodeQueueCounts[n];
                        if (slack < minSlack) minSlack = slack;
                    }
                    slackAtPeak = minSlack == int.MaxValue ? 0 : minSlack;
                }
            }
            if (peakQueued <= 0) slackAtPeak = minCapacity == int.MaxValue ? 0 : minCapacity;

            // Axis R: per entry, {removed} and {shifted +1 tick}; a perturbation wins iff the
            // replay still ends Won (pinned throws count as not-won).
            int tried = 0, winnable = 0;
            for (int i = 0; i < log.Entries.Count; i++)
            {
                var removed = new CommandLog();
                var shifted = new CommandLog();
                for (int j = 0; j < log.Entries.Count; j++)
                {
                    var e = log.Entries[j];
                    if (j != i) removed.Append(e);
                    shifted.Append(j == i ? new ToggleSwitchCommand(e.SwitchId, e.Tick + 1) : e);
                }
                tried += 2;
                if (WinsQuietly(graph, seed, removed)) winnable++;
                if (WinsQuietly(graph, seed, shifted)) winnable++;
            }

            return new DifficultyProxy(maxPending, completionTicks, graph.TimeLimitTicks,
                slackAtPeak, winnable, tried);
        }

        private static bool WinsQuietly(LevelGraph graph, ulong seed, CommandLog log)
        {
            try
            {
                return ReplayHasher.RunToEnd(graph, seed, log).Outcome.Kind == OutcomeKind.Won;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}
