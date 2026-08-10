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
            var widths = beamWidths == null || beamWidths.Length == 0 ? SolverBounds.BEAM_WIDTHS : beamWidths; // review L4
            var work = new SolverWorkMeter(maxNodesExpanded);

            if (graph.SwitchRoutes.Length <= 2)
                return Search(graph, seed, maxNodesExpanded, int.MaxValue, 0,
                    exhaustiveIsProof: true, priorExpanded: 0, work);

            // Review L1: the budget caps TOTAL expansions across all beam legs, and the reported
            // NodesExpanded is cumulative.
            SolveResult last = null;
            int expandedSoFar = 0;
            foreach (var w in widths)
            {
                last = Search(graph, seed, maxNodesExpanded, w, w,
                    exhaustiveIsProof: false, priorExpanded: expandedSoFar, work);
                if (last.Verdict == SolveVerdict.Solved
                    || last.NotFoundReason == NotFoundReason.Budget
                    || (last.Verdict == SolveVerdict.NotFound
                        && last.NotFoundReason == NotFoundReason.None))
                    return last;
                expandedSoFar = last.NodesExpanded;
            }
            // Missed at every width: NotFound(Beam, last width) — never Unsolvable (ADR-0008:117).
            // Precedence ruling (review M3, recorded in the handoff): a beam miss reports
            // NotFound(Beam) even when pins were pruned — Indeterminate is BFS-exhaustion's
            // refinement only, because only exhaustion proves there was nothing else to find.
            return new SolveResult(SolveVerdict.NotFound, NotFoundReason.Beam, new CommandLog(),
                0, 0, widths[widths.Length - 1], last?.PinnedPruned ?? 0, last?.NodesExpanded ?? 0,
                last?.FirstPinMessage ?? "", ZeroProxy(graph));
        }

        // Review M4 / criterion 10: the zero-input baseline entry point — SolveResult for a FIXED
        // command log (CM-R12.2's stage-5 consumer). Won -> Solved; a pinned replay ->
        // Indeterminate (the L001 empty-log baseline); ran-to-terminal-without-winning ->
        // NotFound(None) — "this log is not a win" is proof of nothing about the board.
        public static SolveResult EvaluateLog(LevelGraph graph, ulong seed, CommandLog log)
        {
            var fixedLog = log ?? new CommandLog();
            try
            {
                var end = ReplayHasher.RunToEnd(graph, seed, fixedLog);
                if (end.Outcome.Kind == OutcomeKind.Won)
                {
                    int t = end.Tick;
                    return new SolveResult(SolveVerdict.Solved, NotFoundReason.None, fixedLog,
                        t - 1, fixedLog.Entries.Count, 0, 0, 0, "",
                        ComputeProxy(graph, seed, fixedLog, t - 1));
                }
                return new SolveResult(SolveVerdict.NotFound, NotFoundReason.None, fixedLog,
                    0, fixedLog.Entries.Count, 0, 0, 0, "", ZeroProxy(graph));
            }
            catch (NotSupportedException e)
            {
                return new SolveResult(SolveVerdict.Indeterminate, NotFoundReason.None, fixedLog,
                    0, fixedLog.Entries.Count, 0, 1, 0, e.Message, ZeroProxy(graph));
            }
            catch (InvalidOperationException e)
            {
                // Envelope guard tripped by this specific log (review H1's sibling case).
                return new SolveResult(SolveVerdict.Indeterminate, NotFoundReason.None, fixedLog,
                    0, fixedLog.Entries.Count, 0, 0, 0, e.Message, ZeroProxy(graph));
            }
        }

        private static DifficultyProxy ZeroProxy(LevelGraph graph) =>
            new DifficultyProxy(0, 0, graph.TimeLimitTicks, 0, 0, 0);

        // One search core for both modes: width == int.MaxValue is BFS (exhaustion proves
        // Unsolvable/Indeterminate); a finite width is one beam leg (miss is only a miss).
        private static SolveResult Search(LevelGraph graph, ulong seed, int budget, int width,
            int reportWidth, bool exhaustiveIsProof, int priorExpanded, SolverWorkMeter work)
        {
            int pinnedPruned = 0;
            string firstPin = "";
            int nodesExpanded = priorExpanded;
            int timeLimit = graph.TimeLimitTicks;

            // Layer L = logs whose replay has taken exactly L steps and is still Running.
            // No cross-layer visited set: Tick occupies digest bytes 0-3, so states in different
            // layers can never share a key — a set spanning layers is provably inert (review M6).
            var layer = new List<CommandLog> { new CommandLog() };

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
                    work.SetNodesExpanded(nodesExpanded);
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
                        catch (InvalidOperationException)
                        {
                            // Review H1: the search can invent command sequences that push a board
                            // past its digest envelope (TrainsMax/QCapBound guards). Such a
                            // successor is un-simulatable by the Domain's own refusal — a dead
                            // branch, pruned WITHOUT the pin counter (planner ruling in the
                            // handoff): the authored line's envelope validity is CM-C2a/CM-C5's
                            // gate, not a search verdict.
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
                            if (!next.TryGetValue(key, out var incumbent)
                                || CompareWins((0, childLog), (0, incumbent.log)) < 0)
                                next[key] = (childLog, state);
                        }
                        // Failed states are dead branches.
                    }
                }

                if (wins.Count > 0)
                {
                    wins.Sort(CompareWins);
                    int bestTicks = wins[0].ticks;
                    int bestCommands = wins[0].log.Entries.Count;
                    if (!CollectEqualPrimaryHistories(
                        graph, seed, bestTicks, bestCommands, width, work,
                        out var canonicalWins))
                        return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                            pinnedPruned, nodesExpanded, firstPin);
                    CommandLog centeredLog = null;
                    foreach (var win in canonicalWins)
                    {
                        if (!work.TryCharge(1))
                            return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                                pinnedPruned, nodesExpanded, firstPin);
                        var status = TryCenterSameCompletionWindows(
                            graph, seed, win, bestTicks, work, out var candidate);
                        if (status == CenterStatus.Budget)
                            return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                                pinnedPruned, nodesExpanded, firstPin);
                        if (status == CenterStatus.Unresolved)
                            continue;
                        if (centeredLog == null || CompareLogsLex(candidate, centeredLog) < 0)
                            centeredLog = candidate;
                    }

                    if (centeredLog == null)
                        return CanonicalStop(NotFoundReason.None, graph, reportWidth,
                            pinnedPruned, nodesExpanded, firstPin);

                    return new SolveResult(SolveVerdict.Solved, NotFoundReason.None, centeredLog,
                        bestTicks, centeredLog.Entries.Count, reportWidth, pinnedPruned,
                        nodesExpanded, firstPin,
                        ComputeProxy(graph, seed, centeredLog, bestTicks));
                }

                var survivors = new List<(CommandLog log, SimulationState state)>(next.Count);
                foreach (var kv in next)
                    survivors.Add(kv.Value);
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

        // The primary order: fewer completion ticks, then fewer commands. Lexicographic order
        // selects a deterministic representative for search-state dedupe and seeds the final
        // mid-window refinement below; it is no longer the returned log's earliest-boundary rule.
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

        // SOLVER-TIEBREAK: starting from CompareWins' earliest representative, move each command
        // to the lower middle of its contiguous same-completion winning interval. Every accepted
        // move is independently replayed through the shipped Domain, so completion optimality and
        // command count cannot change. Repeated passes handle interacting windows; the bounded
        // fallback is still the last independently-proven same-completion win.
        private static CenterStatus TryCenterSameCompletionWindows(
            LevelGraph graph, ulong seed, CommandLog winningLog, int completionTicks,
            SolverWorkMeter work, out CommandLog centeredLog)
        {
            if (winningLog.Entries.Count == 0)
            {
                centeredLog = winningLog;
                return CenterStatus.Centered;
            }

            var entries = new ToggleSwitchCommand[winningLog.Entries.Count];
            for (int i = 0; i < entries.Length; i++) entries[i] = winningLog.Entries[i];
            var seedTicks = new int[entries.Length];
            for (int i = 0; i < entries.Length; i++) seedTicks[i] = entries[i].Tick;
            if (!MidWindowNormalizer.TryCenter(seedTicks, graph.TimeLimitTicks,
                ticks => IsSameCompletionWin(
                    graph, seed, entries, ticks, completionTicks, work),
                out var centeredTicks))
            {
                centeredLog = null;
                return work.Exceeded ? CenterStatus.Budget : CenterStatus.Unresolved;
            }
            if (work.Exceeded)
            {
                centeredLog = null;
                return CenterStatus.Budget;
            }
            for (int i = 0; i < entries.Length; i++)
                entries[i] = new ToggleSwitchCommand(entries[i].SwitchId, centeredTicks[i]);
            centeredLog = LogFrom(entries, winningLog.FormatVersion);
            return CenterStatus.Centered;
        }

        private static bool IsSameCompletionWin(LevelGraph graph, ulong seed,
            ToggleSwitchCommand[] entries, int[] ticks, int completionTicks,
            SolverWorkMeter work)
        {
            if (!work.TryChargeReplay(entries.Length, graph.TimeLimitTicks)) return false;
            var candidate = new CommandLog();
            for (int i = 0; i < entries.Length; i++)
                candidate.Append(new ToggleSwitchCommand(entries[i].SwitchId, ticks[i]));

            try
            {
                var end = ReplayHasher.RunToEnd(graph, seed, candidate);
                return end.Outcome.Kind == OutcomeKind.Won && end.Tick - 1 == completionTicks;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static CommandLog LogFrom(ToggleSwitchCommand[] entries, int formatVersion)
        {
            var log = new CommandLog(formatVersion);
            foreach (var entry in entries) log.Append(entry);
            return log;
        }

        private static int CompareLogsLex(CommandLog a, CommandLog b)
        {
            for (int i = 0; i < a.Entries.Count; i++)
            {
                var ea = a.Entries[i];
                var eb = b.Entries[i];
                if (ea.Tick != eb.Tick) return ea.Tick.CompareTo(eb.Tick);
                if (ea.SwitchId != eb.SwitchId) return ea.SwitchId.CompareTo(eb.SwitchId);
            }
            return 0;
        }

        private static bool CollectEqualPrimaryHistories(
            LevelGraph graph, ulong seed, int completionTicks, int commandCount, int width,
            SolverWorkMeter work, out List<CommandLog> result)
        {
            result = null;
            var layer = new List<CanonicalFrontier>
            {
                new CanonicalFrontier(SimulationState.CreateInitial(graph, seed), new CommandLog())
            };
            if (!layer[0].TryAddHistory(new CommandLog(), work)) return false;

            for (int depth = 0; depth <= completionTicks && layer.Count > 0; depth++)
            {
                var wins = new List<CommandLog>();
                var winKeys = new HashSet<string>();
                var next = new Dictionary<string, CanonicalFrontier>();

                foreach (var frontier in layer)
                {
                    if (!work.TryCharge(1)) return false;
                    foreach (var combo in Combos(graph, depth - 1, depth == 0))
                    {
                        var replayLog = Extend(frontier.ReplayLog, combo);
                        SimulationState state;
                        try
                        {
                            state = ReplayTo(graph, seed, replayLog, depth + 1);
                        }
                        catch (NotSupportedException)
                        {
                            continue;
                        }
                        catch (InvalidOperationException)
                        {
                            continue;
                        }

                        if (state.Outcome.Kind == OutcomeKind.Won)
                        {
                            if (state.Tick - 1 != completionTicks) continue;
                            foreach (var history in frontier.Histories)
                            {
                                if (!work.TryCharge(1)) return false;
                                var child = Extend(history, combo);
                                if (child.Entries.Count != commandCount) continue;
                                var key = HistoryKey(child);
                                if (winKeys.Add(key)) wins.Add(child);
                            }
                            continue;
                        }
                        if (state.Outcome.Kind != OutcomeKind.Running) continue;

                        var digest = DigestKey(state);
                        if (!next.TryGetValue(digest, out var incumbent))
                        {
                            incumbent = new CanonicalFrontier(state, replayLog);
                            next[digest] = incumbent;
                        }
                        else if (CompareWins((0, replayLog), (0, incumbent.ReplayLog)) < 0)
                        {
                            incumbent.ReplayLog = replayLog;
                        }

                        foreach (var history in frontier.Histories)
                        {
                            if (!work.TryCharge(1)) return false;
                            var child = Extend(history, combo);
                            if (child.Entries.Count <= commandCount)
                                if (!incumbent.TryAddHistory(child, work)) return false;
                        }
                    }
                }

                if (wins.Count > 0)
                {
                    wins.Sort(CompareLogsLex);
                    result = wins;
                    return true;
                }

                var survivors = new List<CanonicalFrontier>(next.Values);
                survivors.Sort(CompareCanonicalBeam);
                if (width != int.MaxValue && survivors.Count > width)
                    survivors.RemoveRange(width, survivors.Count - width);
                layer = survivors;
            }

            result = new List<CommandLog>();
            return true;
        }

        private static int CompareCanonicalBeam(CanonicalFrontier a, CanonicalFrontier b)
        {
            if (a.State.Deliveries != b.State.Deliveries)
                return b.State.Deliveries.CompareTo(a.State.Deliveries);
            var da = Digest(a.State);
            var db = Digest(b.State);
            for (int i = 0; i < da.Length && i < db.Length; i++)
                if (da[i] != db[i]) return da[i].CompareTo(db[i]);
            return da.Length.CompareTo(db.Length);
        }

        private sealed class CanonicalFrontier
        {
            private readonly HashSet<string> _historyKeys = new HashSet<string>();

            internal readonly SimulationState State;
            internal CommandLog ReplayLog;
            internal readonly List<CommandLog> Histories = new List<CommandLog>();

            internal CanonicalFrontier(SimulationState state, CommandLog replayLog)
            {
                State = state;
                ReplayLog = replayLog;
            }

            internal bool TryAddHistory(CommandLog history, SolverWorkMeter work)
            {
                if (!work.TryCharge(1)) return false;
                if (_historyKeys.Add(HistoryKey(history))) Histories.Add(history);
                return true;
            }
        }

        private static string HistoryKey(CommandLog log)
        {
            if (log.Entries.Count == 0) return "empty";
            var parts = new string[log.Entries.Count];
            for (int i = 0; i < log.Entries.Count; i++)
            {
                var entry = log.Entries[i];
                parts[i] = entry.Tick + ":" + entry.SwitchId;
            }
            return string.Join(";", parts);
        }

        private static SolveResult CanonicalStop(NotFoundReason reason, LevelGraph graph,
            int reportWidth, int pinnedPruned, int nodesExpanded, string firstPin) =>
            new SolveResult(SolveVerdict.NotFound, reason, new CommandLog(),
                0, 0, reportWidth, pinnedPruned, nodesExpanded, firstPin, ZeroProxy(graph));

        private enum CenterStatus : byte
        {
            Centered = 1,
            Unresolved = 2,
            Budget = 3,
        }

        // One Solve-wide ceiling. NodesExpanded remains the public search-only count; this meter
        // prevents canonical provenance/candidate/replay work from escaping the same numeric cap.
        private sealed class SolverWorkMeter
        {
            private readonly long _limit;
            private long _nodesExpanded;
            private long _canonicalWork;

            internal bool Exceeded { get; private set; }

            internal SolverWorkMeter(int limit)
            {
                _limit = Math.Max(0, limit);
            }

            internal void SetNodesExpanded(int nodesExpanded)
            {
                if (nodesExpanded > _nodesExpanded) _nodesExpanded = nodesExpanded;
            }

            internal bool TryChargeReplay(int commandCount, int timeLimitTicks)
            {
                long commands = Math.Max(1, commandCount);
                long ticks = Math.Max(1, timeLimitTicks);
                if (commands > long.MaxValue / ticks) return Reject();
                return TryCharge(commands * ticks);
            }

            internal bool TryCharge(long units)
            {
                if (units < 0) return Reject();
                if (_nodesExpanded > _limit || _canonicalWork > _limit - _nodesExpanded)
                    return Reject();
                long remaining = _limit - _nodesExpanded - _canonicalWork;
                if (units > remaining) return Reject();
                _canonicalWork += units;
                return true;
            }

            private bool Reject()
            {
                Exceeded = true;
                return false;
            }
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
                return false; // pinned perturbation counts as not-won (Q-N)
            }
            catch (InvalidOperationException)
            {
                return false; // envelope-broken perturbation likewise (review H1)
            }
        }
    }
}
