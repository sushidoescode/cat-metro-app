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
            // SwitchesUsed and Rejections are write-only counters during Simulation.Step. For
            // exact BFS, a lower-command history that reaches the same remaining state dominates
            // a higher-command one for every suffix even when their lifetime refusal counts
            // differ. Normalizing both counters prevents recoverable bounce loops from exploding
            // the proof search without erasing any future behavior.
            bool behavioralStateDominance = exhaustiveIsProof;

            // Layer L = distinct running states whose replay has taken exactly L steps. A state
            // is simulated once, while its minimum-command receipt histories travel as a compact
            // predecessor DAG so state convergence cannot erase the eventual canonical win.
            // No cross-layer visited set: Tick occupies digest bytes 0-3, so states in different
            // layers can never share a key — a set spanning layers is provably inert (review M6).
            var initial = new SearchFrontier(
                SimulationState.CreateInitial(graph, seed), new CommandLog());
            initial.AddRootHistory();
            var layer = new List<SearchFrontier> { initial };
            var provenanceOrder = new List<ProvenanceNode> { initial.Provenance };

            for (int depth = 0; depth < timeLimit && layer.Count > 0; depth++)
            {
                WinningFrontier wins = null;
                // Within-layer dedupe resolves collisions by the criterion-7 comparator, NOT by
                // expansion order — otherwise the untoggled-prefix branch (always expanded first)
                // claims every converged state and the canonical log carries the LATEST possible
                // toggle, inverting the tie-break.
                var next = new Dictionary<string, SearchFrontier>();

                foreach (var frontier in layer)
                {
                    var log = frontier.ReplayLog;
                    nodesExpanded++;
                    if (!work.SetNodesExpanded(nodesExpanded))
                        return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                            pinnedPruned, nodesExpanded, firstPin);

                    // Transition depth -> depth+1: the (depth+1)th Step call has entry tick
                    // == depth, so its due commands carry entry.Tick == depth - 1. The very
                    // first step is uncommandable by Domain design.
                    foreach (var combo in Combos(graph, depth - 1, depth == 0))
                    {
                        // A high-route-count board may spend most of its time generating and
                        // digesting successor attempts rather than retaining states. Charge every
                        // attempt so branching work cannot escape the Solve-wide ceiling.
                        if (!work.TryCharge(1))
                            return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                                pinnedPruned, nodesExpanded, firstPin);
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
                            int completionTicks = state.Tick - 1;
                            int commandCount = childLog.Entries.Count;
                            if (wins == null || commandCount < wins.CommandCount)
                                wins = new WinningFrontier(completionTicks, commandCount);
                            if (commandCount == wins.CommandCount)
                                if (!wins.MergeFrom(frontier, combo, work))
                                    return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                                        pinnedPruned, nodesExpanded, firstPin);
                        }
                        else if (state.Outcome.Kind == OutcomeKind.Running)
                        {
                            var key = DigestKey(state, behavioralStateDominance);
                            if (!next.TryGetValue(key, out var incumbent))
                            {
                                incumbent = new SearchFrontier(state, childLog);
                                if (!incumbent.ReplaceFrom(frontier, combo, work))
                                    return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                                        pinnedPruned, nodesExpanded, firstPin);
                                next[key] = incumbent;
                                provenanceOrder.Add(incumbent.Provenance);
                            }
                            else if (childLog.Entries.Count < incumbent.CommandCount)
                            {
                                incumbent.State = state;
                                incumbent.ReplayLog = childLog;
                                if (!incumbent.ReplaceFrom(frontier, combo, work))
                                    return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                                        pinnedPruned, nodesExpanded, firstPin);
                            }
                            else if (childLog.Entries.Count == incumbent.CommandCount)
                            {
                                if (CompareWins((0, childLog), (0, incumbent.ReplayLog)) < 0)
                                    incumbent.ReplayLog = childLog;
                                if (!incumbent.MergeFrom(frontier, combo, work))
                                    return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                                        pinnedPruned, nodesExpanded, firstPin);
                            }
                        }
                        // Failed states are dead branches.
                    }
                }

                if (wins != null)
                {
                    var status = TrySelectCanonicalWin(
                        graph, seed, wins, provenanceOrder, work, out var centeredLog);
                    if (status == CenterStatus.Budget)
                        return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                            pinnedPruned, nodesExpanded, firstPin);

                    if (centeredLog == null)
                        return CanonicalStop(NotFoundReason.None, graph, reportWidth,
                            pinnedPruned, nodesExpanded, firstPin);

                    if (!work.TryChargeProxy(
                        centeredLog.Entries.Count, wins.CompletionTicks + 1,
                        graph.TimeLimitTicks))
                        return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                            pinnedPruned, nodesExpanded, firstPin);

                    return new SolveResult(SolveVerdict.Solved, NotFoundReason.None, centeredLog,
                        wins.CompletionTicks, centeredLog.Entries.Count, reportWidth, pinnedPruned,
                        nodesExpanded, firstPin,
                        ComputeProxy(graph, seed, centeredLog, wins.CompletionTicks));
                }

                var survivors = new List<SearchFrontier>(next.Count);
                foreach (var kv in next)
                    survivors.Add(kv.Value);
                // Deterministic layer order regardless of dictionary iteration: beam order.
                survivors.Sort(CompareBeam);
                if (width != int.MaxValue && survivors.Count > width)
                    survivors.RemoveRange(width, survivors.Count - width);

                layer = survivors;
            }

            if (work.Exceeded)
                return CanonicalStop(NotFoundReason.Budget, graph, reportWidth,
                    pinnedPruned, nodesExpanded, firstPin);
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
        // selects only the replay representative for a deduped state. Equal-state histories remain
        // in the predecessor DAG, so this raw order is no longer the returned-log tie-break.
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

        // SOLVER-TIEBREAK: move each command to the lower middle of its unrestricted contiguous
        // same-completion winning interval. Every probe replays the shipped Domain through the
        // target completion boundary. MidWindowNormalizer returns only a fixed point; cycles,
        // non-convergence, work exhaustion, and receipt-order reversal all fail closed.
        private static CenterStatus TryCenterSameCompletionWindows(
            LevelGraph graph, ulong seed, CommandLog winningLog, int completionTicks,
            SolverWorkMeter work, Dictionary<string, bool> probeCache,
            out CommandLog centeredLog)
        {
            if (winningLog.Entries.Count == 0)
            {
                centeredLog = winningLog;
                return CenterStatus.Centered;
            }

            int commandCount = winningLog.Entries.Count;
            if (!work.TryCharge(Math.Max(1L, (long)commandCount * 3)))
            {
                centeredLog = null;
                return CenterStatus.Budget;
            }
            var entries = new ToggleSwitchCommand[winningLog.Entries.Count];
            for (int i = 0; i < entries.Length; i++) entries[i] = winningLog.Entries[i];
            var receiptSwitches = new ushort[entries.Length];
            var seedTicks = new int[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                receiptSwitches[i] = entries[i].SwitchId;
                seedTicks[i] = entries[i].Tick;
            }
            if (!MidWindowNormalizer.TryCenter(receiptSwitches, seedTicks, graph.TimeLimitTicks,
                ticks => IsSameCompletionWin(
                    graph, seed, entries, ticks, completionTicks, work, probeCache),
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
            SolverWorkMeter work, Dictionary<string, bool> probeCache)
        {
            // Reserve the linear key construction before allocating it. A cache hit pays only
            // this lookup/key cost; a miss additionally reserves the full replay below.
            if (!work.TryCharge(Math.Max(1, entries.Length))) return false;
            string key = ProbeKey(entries, ticks);
            if (probeCache.TryGetValue(key, out bool cached)) return cached;

            int replayTicks = Math.Min(graph.TimeLimitTicks, completionTicks + 1);
            if (!work.TryChargeReplay(entries.Length, replayTicks)) return false;
            var candidate = new CommandLog();
            for (int i = 0; i < entries.Length; i++)
                candidate.Append(new ToggleSwitchCommand(entries[i].SwitchId, ticks[i]));

            try
            {
                var end = ReplayTo(graph, seed, candidate, replayTicks);
                bool result = end.Outcome.Kind == OutcomeKind.Won
                    && end.Tick - 1 == completionTicks;
                probeCache.Add(key, result);
                return result;
            }
            catch (NotSupportedException)
            {
                probeCache.Add(key, false);
                return false;
            }
            catch (InvalidOperationException)
            {
                probeCache.Add(key, false);
                return false;
            }
        }

        private static string ProbeKey(ToggleSwitchCommand[] entries, int[] ticks)
        {
            var parts = new string[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                parts[i] = entries[i].SwitchId + ":" + ticks[i];
            return string.Join(";", parts);
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

        private static CenterStatus TrySelectCanonicalWin(
            LevelGraph graph, ulong seed, WinningFrontier wins,
            List<ProvenanceNode> provenanceOrder, SolverWorkMeter work,
            out CommandLog centeredLog)
        {
            centeredLog = null;
            if (!TryBuildWinningHistories(
                wins, provenanceOrder, work, out var histories))
                return CenterStatus.Budget;
            if (!wins.ProvenanceComplete || work.Exceeded) return CenterStatus.Budget;
            if (graph.SwitchRoutes.Length <= 2)
            {
                var certificate = TryCreateCertifiedRelation(histories, work, out var relation);
                if (certificate == CertificateStatus.Budget) return CenterStatus.Budget;
                if (certificate == CertificateStatus.Certified)
                {
                    var occurrenceCertificate = TrySelectIndependentOccurrenceFixedPoints(
                        graph, histories, relation, work, out centeredLog);
                    if (occurrenceCertificate == CertificateStatus.Budget)
                        return CenterStatus.Budget;

                    var status = occurrenceCertificate == CertificateStatus.Certified
                        ? CenterStatus.Centered
                        : SelectCertifiedFixedPoint(
                            graph, histories, relation, work, out centeredLog);
                    if (status != CenterStatus.Centered) return status;

                    // The compact relation proves which fixed point is lex-first. Re-run that one
                    // selected log through the shipped Domain normalizer as a defensive witness:
                    // it must be the same-completion win and already be the same fixed point.
                    var verificationCache = new Dictionary<string, bool>();
                    var verification = TryCenterSameCompletionWindows(
                        graph, seed, centeredLog, wins.CompletionTicks, work,
                        verificationCache, out var verifiedLog);
                    if (verification != CenterStatus.Centered) return verification;
                    if (CompareLogsLex(centeredLog, verifiedLog) != 0)
                    {
                        centeredLog = null;
                        return CenterStatus.Unresolved;
                    }
                    return CenterStatus.Centered;
                }
            }

            var probeCache = new Dictionary<string, bool>();

            foreach (var group in histories.Values)
            {
                var status = CenterEveryHistory(
                    graph, seed, wins.CompletionTicks, group, work, probeCache,
                    ref centeredLog);
                if (status == CenterStatus.Budget) return CenterStatus.Budget;
            }

            return centeredLog == null ? CenterStatus.Unresolved : CenterStatus.Centered;
        }

        private static CertificateStatus TryCreateCertifiedRelation(
            Dictionary<string, HistoryGroup> histories,
            SolverWorkMeter work,
            out CertifiedWinRelation relation)
        {
            relation = null;
            // A group contains unique chronological tick vectors for one switch-id sequence:
            // deterministic replay gives each CommandLog exactly one path through the DAG. Its
            // observed count therefore proves the entire ordered integer box when it equals the
            // independently counted box cardinality. Any hole, duplicate/overflow uncertainty,
            // or work stop rejects the certificate and falls back to exact path enumeration.
            foreach (var group in histories.Values)
            {
                var status = IsFullOrderedBox(group, work);
                if (status != CertificateStatus.Certified) return status;
            }
            relation = new CertifiedWinRelation(histories, work);
            return CertificateStatus.Certified;
        }

        private static CertificateStatus IsFullOrderedBox(
            HistoryGroup group, SolverWorkMeter work)
        {
            if (group.CountOverflow) return CertificateStatus.NotCertified;
            var status = OrderedTickBox.Classify(
                group.Switches, group.MinTicks, group.MaxTicks, group.Count,
                () => work.TryCharge(1));
            if (status == OrderedTickBoxStatus.WorkLimit) return CertificateStatus.Budget;
            return status == OrderedTickBoxStatus.Complete
                ? CertificateStatus.Certified : CertificateStatus.NotCertified;
        }

        // A second independent cardinality certificate recognizes a common exact shape without
        // enumerating every chronological interleaving. Label the kth command received for each
        // switch as that switch's kth occurrence. Every chronological history maps injectively to
        // one vector of per-occurrence ticks. If the observed union has the same cardinality as the
        // product of its occurrence envelopes, it is that full product. Strictly separated
        // same-switch envelopes then make every command's maximal connected interval independent,
        // so the lower midpoint of each envelope is the unique fixed point for that switch-count
        // family. Families cannot interact because moving ticks never changes switch counts.
        private static CertificateStatus TrySelectIndependentOccurrenceFixedPoints(
            LevelGraph graph,
            Dictionary<string, HistoryGroup> histories,
            CertifiedWinRelation relation,
            SolverWorkMeter work,
            out CommandLog centeredLog)
        {
            centeredLog = null;
            int switchCount = graph.SwitchRoutes.Length;
            if (!work.TryCharge(Math.Max(1, histories.Count)))
                return CertificateStatus.Budget;
            var families = new Dictionary<string, List<HistoryGroup>>();
            foreach (var group in histories.Values)
            {
                if (!work.TryCharge(Math.Max(1, group.Switches.Length)))
                    return CertificateStatus.Budget;
                var counts = new int[switchCount];
                foreach (ushort switchId in group.Switches)
                {
                    if (switchId >= switchCount) return CertificateStatus.NotCertified;
                    counts[switchId]++;
                }
                string key = string.Join(",", counts);
                if (!families.TryGetValue(key, out var family))
                    families.Add(key, family = new List<HistoryGroup>());
                family.Add(group);
            }

            foreach (var family in families.Values)
            {
                var status = TrySelectIndependentOccurrenceFixedPoint(
                    graph, family, relation, work, out var candidate);
                if (status != CertificateStatus.Certified) return status;
                if (centeredLog == null || CompareLogsLex(candidate, centeredLog) < 0)
                    centeredLog = candidate;
            }
            return centeredLog == null
                ? CertificateStatus.NotCertified : CertificateStatus.Certified;
        }

        private static CertificateStatus TrySelectIndependentOccurrenceFixedPoint(
            LevelGraph graph,
            List<HistoryGroup> family,
            CertifiedWinRelation relation,
            SolverWorkMeter work,
            out CommandLog candidateLog)
        {
            candidateLog = null;
            if (family.Count == 0) return CertificateStatus.NotCertified;
            if (!work.TryCharge(Math.Max(1, family.Count)))
                return CertificateStatus.Budget;
            var boxes = new OccurrenceTickBoxGroup[family.Count];
            for (int i = 0; i < family.Count; i++)
            {
                var group = family[i];
                boxes[i] = new OccurrenceTickBoxGroup(
                    group.Switches, group.MinTicks, group.MaxTicks,
                    group.Count, group.CountOverflow);
            }
            var productStatus = OccurrenceTickProduct.Classify(
                graph.SwitchRoutes.Length, graph.TimeLimitTicks, boxes,
                units => work.TryCharge(units), out var entries);
            if (productStatus == OrderedTickBoxStatus.WorkLimit)
                return CertificateStatus.Budget;
            if (productStatus != OrderedTickBoxStatus.Complete)
                return CertificateStatus.NotCertified;

            int commandCount = entries.Length;
            var switches = new ushort[commandCount];
            var ticks = new int[commandCount];
            for (int i = 0; i < commandCount; i++)
            {
                switches[i] = entries[i].SwitchId;
                ticks[i] = entries[i].Tick;
            }
            if (!relation.Contains(switches, ticks))
                return work.Exceeded
                    ? CertificateStatus.Budget : CertificateStatus.NotCertified;
            if (!MidWindowNormalizer.IsFixedPoint(
                    ticks, graph.TimeLimitTicks,
                    probe => relation.Contains(switches, probe)))
                return work.Exceeded
                    ? CertificateStatus.Budget : CertificateStatus.NotCertified;

            candidateLog = LogFrom(entries, CommandLog.CurrentFormatVersion);
            return CertificateStatus.Certified;
        }

        private static CenterStatus SelectCertifiedFixedPoint(
            LevelGraph graph,
            Dictionary<string, HistoryGroup> histories,
            CertifiedWinRelation relation,
            SolverWorkMeter work,
            out CommandLog centeredLog)
        {
            centeredLog = null;
            foreach (var group in histories.Values)
            {
                var status = EnumerateCertifiedFixedPoints(
                    graph, group, relation, work, ref centeredLog);
                if (status == CenterStatus.Budget) return CenterStatus.Budget;
            }
            return centeredLog == null ? CenterStatus.Unresolved : CenterStatus.Centered;
        }

        private static CenterStatus EnumerateCertifiedFixedPoints(
            LevelGraph graph,
            HistoryGroup group,
            CertifiedWinRelation relation,
            SolverWorkMeter work,
            ref CommandLog centeredLog)
        {
            int count = group.Switches.Length;
            if (count == 0)
            {
                if (!work.TryCharge(1)) return CenterStatus.Budget;
                centeredLog = new CommandLog();
                return CenterStatus.Centered;
            }

            // Reserve the component scans and their linear scratch before allocating it.
            if (!work.TryCharge(Math.Max(1L, (long)count * 3)))
                return CenterStatus.Budget;

            var componentStarts = new List<int> { 0 };
            // A gap leaves the first tick immediately outside either interval in the same receipt
            // order. Because the certified box contains every winner in that order, that outside
            // tick is a loss and the isolated interval's middle is forced. Inside the one chosen
            // overlapping component, enumerate all but its first coordinate; every fixed point is
            // represented by exactly one such assignment, and MiddleFor reconstructs the omitted
            // coordinate. Other overlapping components remain fully enumerated, avoiding any
            // independence assumption between them.
            for (int i = 1; i < count; i++)
                if ((long)group.MaxTicks[i - 1] + 1 < group.MinTicks[i])
                    componentStarts.Add(i);

            var componentEnds = new List<int>(componentStarts.Count);
            for (int i = 0; i < componentStarts.Count; i++)
                componentEnds.Add(i + 1 < componentStarts.Count
                    ? componentStarts[i + 1] - 1 : count - 1);

            var free = new List<int>();
            var ticks = new int[count];
            int computedStart = -1;
            for (int component = 0; component < componentStarts.Count; component++)
            {
                int start = componentStarts[component];
                int end = componentEnds[component];
                if (start == end)
                {
                    ticks[start] = group.MinTicks[start]
                        + (group.MaxTicks[start] - group.MinTicks[start]) / 2;
                }
                else
                {
                    if (computedStart < 0)
                    {
                        computedStart = start;
                        ticks[start] = group.MinTicks[start];
                    }
                    else
                    {
                        ticks[start] = group.MinTicks[start];
                        free.Add(start);
                    }
                    for (int i = start + 1; i <= end; i++)
                    {
                        ticks[i] = group.MinTicks[i];
                        free.Add(i);
                    }
                }
            }

            bool more = true;
            while (more)
            {
                // Bounds/order checks, coordinate reset, and vector increment are each linear.
                if (!work.TryCharge(Math.Max(1L, (long)count * 3)))
                    return CenterStatus.Budget;

                if (computedStart >= 0) ticks[computedStart] = group.MinTicks[computedStart];

                if (MatchesOrderedBox(group, ticks))
                {
                    bool viable = true;
                    if (computedStart >= 0)
                    {
                        ticks[computedStart] = MidWindowNormalizer.MiddleFor(
                            ticks, computedStart, graph.TimeLimitTicks,
                            candidate => relation.Contains(group.Switches, candidate));
                        if (work.Exceeded)
                            return CenterStatus.Budget;
                        if (!MatchesOrderedBox(group, ticks))
                            viable = false;
                    }

                    if (viable
                        && MidWindowNormalizer.IsFixedPoint(
                            ticks, graph.TimeLimitTicks,
                            candidate => relation.Contains(group.Switches, candidate)))
                    {
                        if (work.Exceeded) return CenterStatus.Budget;
                        if (!work.TryCharge(Math.Max(1, count)))
                            return CenterStatus.Budget;
                        var entries = new ToggleSwitchCommand[count];
                        for (int i = 0; i < count; i++)
                            entries[i] = new ToggleSwitchCommand(group.Switches[i], ticks[i]);
                        var candidate = LogFrom(entries, CommandLog.CurrentFormatVersion);
                        if (centeredLog == null || CompareLogsLex(candidate, centeredLog) < 0)
                            centeredLog = candidate;
                    }
                    else if (work.Exceeded)
                    {
                        return CenterStatus.Budget;
                    }
                }

                more = IncrementTicks(free, ticks, group.MinTicks, group.MaxTicks);
            }

            return centeredLog == null ? CenterStatus.Unresolved : CenterStatus.Centered;
        }

        private static bool IncrementTicks(
            List<int> indices, int[] ticks, int[] minTicks, int[] maxTicks)
        {
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                int index = indices[i];
                if (ticks[index] < maxTicks[index])
                {
                    ticks[index]++;
                    for (int j = i + 1; j < indices.Count; j++)
                        ticks[indices[j]] = minTicks[indices[j]];
                    return true;
                }
            }
            return false;
        }

        private static bool MatchesOrderedBox(HistoryGroup group, int[] ticks)
        {
            for (int i = 0; i < ticks.Length; i++)
            {
                if (ticks[i] < group.MinTicks[i] || ticks[i] > group.MaxTicks[i])
                    return false;
                if (i > 0 && (ticks[i] < ticks[i - 1]
                    || (ticks[i] == ticks[i - 1]
                        && group.Switches[i] < group.Switches[i - 1])))
                    return false;
            }
            return true;
        }

        private sealed class CertifiedWinRelation
        {
            private readonly Dictionary<string, HistoryGroup> _groups;
            private readonly SolverWorkMeter _work;

            internal CertifiedWinRelation(
                Dictionary<string, HistoryGroup> groups, SolverWorkMeter work)
            {
                _groups = groups;
                _work = work;
            }

            internal bool Contains(ushort[] switches, int[] ticks)
            {
                // A shifted coordinate may cross a neighbor even though the returned log may not.
                // Canonicalize the probe back to receipt chronology before consulting the proven
                // boxes. Same-tick toggles commute: Simulation.Step only increments the addressed
                // route byte (and SwitchesUsed), so switch-id sorting is behavior-preserving.
                int count = switches.Length;
                long linear = Math.Max(1, count);
                int sortLevels = 1;
                for (int width = count; width > 1; width = (width + 1) / 2)
                    sortLevels++;
                // Array.Sort plus three arrays/key/range scans. Reserve before any allocation.
                if (!_work.TryCharge(linear * (sortLevels + 4L))) return false;
                var order = new int[count];
                for (int i = 0; i < count; i++) order[i] = i;
                Array.Sort(order, (a, b) =>
                {
                    int byTick = ticks[a].CompareTo(ticks[b]);
                    return byTick != 0 ? byTick : switches[a].CompareTo(switches[b]);
                });

                var orderedSwitches = new ushort[count];
                var orderedTicks = new int[count];
                for (int i = 0; i < count; i++)
                {
                    orderedSwitches[i] = switches[order[i]];
                    orderedTicks[i] = ticks[order[i]];
                }

                if (!_groups.TryGetValue(SwitchKey(orderedSwitches), out var group))
                    return false;
                return MatchesOrderedBox(group, orderedTicks);
            }
        }

        private static string SwitchKey(ushort[] switches)
        {
            if (switches.Length == 0) return "empty";
            var parts = new string[switches.Length];
            for (int i = 0; i < switches.Length; i++) parts[i] = switches[i].ToString();
            return string.Join(";", parts);
        }

        private static CenterStatus CenterEveryHistory(
            LevelGraph graph, ulong seed, int completionTicks, HistoryGroup group,
            SolverWorkMeter work, Dictionary<string, bool> probeCache,
            ref CommandLog centeredLog)
        {
            var stack = new List<HistoryFrame> { new HistoryFrame(group, false) };
            var reverseCombos = new List<ToggleSwitchCommand[]>();

            while (stack.Count > 0)
            {
                var frame = stack[stack.Count - 1];
                if (frame.Group.Switches.Length == 0)
                {
                    if (!work.TryCharge(Math.Max(1, group.Switches.Length)))
                        return CenterStatus.Budget;
                    var log = MaterializeHistory(reverseCombos);
                    var status = TryCenterSameCompletionWindows(
                        graph, seed, log, completionTicks, work, probeCache,
                        out var candidate);
                    if (status == CenterStatus.Budget) return CenterStatus.Budget;
                    if (status == CenterStatus.Centered
                        && (centeredLog == null || CompareLogsLex(candidate, centeredLog) < 0))
                        centeredLog = candidate;
                    stack.RemoveAt(stack.Count - 1);
                    if (frame.RemoveComboOnPop)
                        reverseCombos.RemoveAt(reverseCombos.Count - 1);
                    continue;
                }

                if (frame.NextContribution >= frame.Group.Contributions.Count)
                {
                    stack.RemoveAt(stack.Count - 1);
                    if (frame.RemoveComboOnPop)
                        reverseCombos.RemoveAt(reverseCombos.Count - 1);
                    continue;
                }

                if (!work.TryCharge(1)) return CenterStatus.Budget;
                var contribution = frame.Group.Contributions[frame.NextContribution++];
                reverseCombos.Add(contribution.Combo);
                stack.Add(new HistoryFrame(contribution.Source, true));
            }

            return centeredLog == null ? CenterStatus.Unresolved : CenterStatus.Centered;
        }

        private static CommandLog MaterializeHistory(List<ToggleSwitchCommand[]> reverseCombos)
        {
            var result = new CommandLog();
            for (int i = reverseCombos.Count - 1; i >= 0; i--)
                foreach (var command in reverseCombos[i]) result.Append(command);
            return result;
        }

        private sealed class HistoryFrame
        {
            internal readonly HistoryGroup Group;
            internal readonly bool RemoveComboOnPop;
            internal int NextContribution;

            internal HistoryFrame(HistoryGroup group, bool removeComboOnPop)
            {
                Group = group;
                RemoveComboOnPop = removeComboOnPop;
            }
        }

        private static bool TryBuildWinningHistories(
            WinningFrontier wins, List<ProvenanceNode> provenanceOrder,
            SolverWorkMeter work,
            out Dictionary<string, HistoryGroup> result)
        {
            result = null;
            if (!wins.ProvenanceComplete || work.Exceeded) return false;

            var reachable = new HashSet<ProvenanceNode>();
            var pending = new Stack<ProvenanceNode>();
            foreach (var arc in wins.Incoming)
            {
                if (!work.TryCharge(1)) return false;
                pending.Push(arc.Source);
            }
            while (pending.Count > 0)
            {
                var frontier = pending.Pop();
                if (!reachable.Add(frontier)) continue;
                if (!frontier.Complete) return false;
                foreach (var arc in frontier.Incoming)
                {
                    if (!work.TryCharge(1)) return false;
                    pending.Push(arc.Source);
                }
            }

            // Nodes are appended when first created, so every incoming arc points backward in
            // this deterministic creation order. It is already a topological order and avoids
            // an unmetered O(V log V) depth sort after a win.
            var built = new Dictionary<ProvenanceNode, Dictionary<string, HistoryGroup>>();
            foreach (var frontier in provenanceOrder)
            {
                if (!work.TryCharge(1)) return false;
                if (!reachable.Contains(frontier)) continue;

                var histories = new Dictionary<string, HistoryGroup>();
                if (frontier.IsRoot)
                {
                    histories.Add("empty", HistoryGroup.Root());
                }
                else
                {
                    foreach (var arc in frontier.Incoming)
                    {
                        if (!built.TryGetValue(arc.Source, out var source)) return false;
                        if (!AddDerivedHistories(histories, source, arc.Combo, work))
                            return false;
                    }
                }
                built.Add(frontier, histories);
            }

            result = new Dictionary<string, HistoryGroup>();
            foreach (var arc in wins.Incoming)
            {
                if (!built.TryGetValue(arc.Source, out var source)) return false;
                if (!AddDerivedHistories(result, source, arc.Combo, work)) return false;
            }
            return true;
        }

        private sealed class SearchFrontier
        {
            internal SimulationState State;
            internal readonly ProvenanceNode Provenance;
            internal CommandLog ReplayLog;
            internal int CommandCount => ReplayLog.Entries.Count;
            internal bool ProvenanceComplete => Provenance.Complete;

            internal SearchFrontier(SimulationState state, CommandLog replayLog)
            {
                State = state;
                ReplayLog = replayLog;
                Provenance = new ProvenanceNode();
            }

            internal void AddRootHistory()
            {
                Provenance.IsRoot = true;
            }

            internal bool ReplaceFrom(
                SearchFrontier source, ToggleSwitchCommand[] combo, SolverWorkMeter work)
            {
                return Provenance.ReplaceFrom(source.Provenance, combo, work);
            }

            internal bool MergeFrom(
                SearchFrontier source, ToggleSwitchCommand[] combo, SolverWorkMeter work)
            {
                return Provenance.MergeFrom(source.Provenance, combo, work);
            }
        }

        // Persistent history-only nodes deliberately exclude SimulationState and ReplayLog. Old
        // layer states can be collected while descendants retain just immutable parent arcs.
        // A lower-count collision replaces arcs; an equal-count collision unions them. Appending
        // the same future suffix to an equal state preserves that dominance relation.
        private sealed class ProvenanceNode
        {
            internal readonly List<HistoryArc> Incoming = new List<HistoryArc>();
            internal bool Complete = true;
            internal bool IsRoot;

            internal bool ReplaceFrom(
                ProvenanceNode source, ToggleSwitchCommand[] combo, SolverWorkMeter work)
            {
                Incoming.Clear();
                Complete = source.Complete;
                if (!Complete) return false;
                return AddArc(source, combo, work);
            }

            internal bool MergeFrom(
                ProvenanceNode source, ToggleSwitchCommand[] combo, SolverWorkMeter work)
            {
                Complete &= source.Complete;
                if (!Complete) return false;
                return AddArc(source, combo, work);
            }

            private bool AddArc(
                ProvenanceNode source, ToggleSwitchCommand[] combo, SolverWorkMeter work)
            {
                // The enclosing successor-attempt unit covers the O(1) arc insertion. Only the
                // additional commands in a multi-switch combo need extra units here.
                if (combo.Length > 1 && !work.TryCharge(combo.Length - 1))
                {
                    Complete = false;
                    return false;
                }
                Incoming.Add(new HistoryArc(source, combo));
                return true;
            }
        }

        private sealed class WinningFrontier
        {
            internal readonly int CompletionTicks;
            internal readonly int CommandCount;
            internal readonly List<HistoryArc> Incoming = new List<HistoryArc>();
            internal bool ProvenanceComplete = true;

            internal WinningFrontier(int completionTicks, int commandCount)
            {
                CompletionTicks = completionTicks;
                CommandCount = commandCount;
            }

            internal bool MergeFrom(
                SearchFrontier source, ToggleSwitchCommand[] combo, SolverWorkMeter work)
            {
                ProvenanceComplete &= source.ProvenanceComplete;
                if (!ProvenanceComplete) return false;
                // The enclosing successor-attempt unit covers the O(1) arc insertion. Only the
                // additional commands in a multi-switch combo need extra units here.
                if (combo.Length > 1 && !work.TryCharge(combo.Length - 1))
                {
                    ProvenanceComplete = false;
                    return false;
                }
                Incoming.Add(new HistoryArc(source.Provenance, combo));
                return true;
            }
        }

        private readonly struct HistoryArc
        {
            internal readonly ProvenanceNode Source;
            internal readonly ToggleSwitchCommand[] Combo;

            internal HistoryArc(ProvenanceNode source, ToggleSwitchCommand[] combo)
            {
                Source = source;
                Combo = combo;
            }
        }

        private sealed class HistoryGroup
        {
            internal readonly ushort[] Switches;
            internal readonly int[] MinTicks;
            internal readonly int[] MaxTicks;
            internal long Count;
            internal bool CountOverflow;
            internal readonly List<HistoryContribution> Contributions =
                new List<HistoryContribution>();

            private HistoryGroup(ushort[] switches, int[] minTicks, int[] maxTicks,
                long count, bool countOverflow)
            {
                Switches = switches;
                MinTicks = minTicks;
                MaxTicks = maxTicks;
                Count = count;
                CountOverflow = countOverflow;
            }

            internal static HistoryGroup Root() =>
                new HistoryGroup(Array.Empty<ushort>(), Array.Empty<int>(), Array.Empty<int>(),
                    1, false);

            internal static HistoryGroup Derive(
                HistoryGroup source, ToggleSwitchCommand[] combo)
            {
                int oldLength = source.Switches.Length;
                int length = oldLength + combo.Length;
                var switches = new ushort[length];
                var minTicks = new int[length];
                var maxTicks = new int[length];
                Array.Copy(source.Switches, switches, oldLength);
                Array.Copy(source.MinTicks, minTicks, oldLength);
                Array.Copy(source.MaxTicks, maxTicks, oldLength);
                for (int i = 0; i < combo.Length; i++)
                {
                    switches[oldLength + i] = combo[i].SwitchId;
                    minTicks[oldLength + i] = combo[i].Tick;
                    maxTicks[oldLength + i] = combo[i].Tick;
                }

                var result = new HistoryGroup(
                    switches, minTicks, maxTicks, source.Count, source.CountOverflow);
                result.Contributions.Add(new HistoryContribution(source, combo));
                return result;
            }

            internal void Merge(HistoryGroup source, ToggleSwitchCommand[] combo)
            {
                int oldLength = source.Switches.Length;
                for (int i = 0; i < oldLength; i++)
                {
                    if (source.MinTicks[i] < MinTicks[i]) MinTicks[i] = source.MinTicks[i];
                    if (source.MaxTicks[i] > MaxTicks[i]) MaxTicks[i] = source.MaxTicks[i];
                }
                for (int i = 0; i < combo.Length; i++)
                {
                    int index = oldLength + i;
                    if (combo[i].Tick < MinTicks[index]) MinTicks[index] = combo[i].Tick;
                    if (combo[i].Tick > MaxTicks[index]) MaxTicks[index] = combo[i].Tick;
                }

                if (CountOverflow || source.CountOverflow || Count > long.MaxValue - source.Count)
                {
                    CountOverflow = true;
                    Count = long.MaxValue;
                }
                else
                {
                    Count += source.Count;
                }
                Contributions.Add(new HistoryContribution(source, combo));
            }
        }

        private readonly struct HistoryContribution
        {
            internal readonly HistoryGroup Source;
            internal readonly ToggleSwitchCommand[] Combo;

            internal HistoryContribution(HistoryGroup source, ToggleSwitchCommand[] combo)
            {
                Source = source;
                Combo = combo;
            }
        }

        private static bool AddDerivedHistories(
            Dictionary<string, HistoryGroup> target,
            Dictionary<string, HistoryGroup> source,
            ToggleSwitchCommand[] combo,
            SolverWorkMeter work)
        {
            foreach (var sourceGroup in source.Values)
            {
                long units = Math.Max(1, sourceGroup.Switches.Length + combo.Length);
                if (!work.TryCharge(units)) return false;

                string key = ExtendedSwitchKey(sourceGroup.Switches, combo);
                if (target.TryGetValue(key, out var incumbent))
                    incumbent.Merge(sourceGroup, combo);
                else
                    target.Add(key, HistoryGroup.Derive(sourceGroup, combo));
            }
            return true;
        }

        private static string ExtendedSwitchKey(
            ushort[] prefix, ToggleSwitchCommand[] combo)
        {
            if (prefix.Length + combo.Length == 0) return "empty";
            var parts = new string[prefix.Length + combo.Length];
            for (int i = 0; i < prefix.Length; i++) parts[i] = prefix[i].ToString();
            for (int i = 0; i < combo.Length; i++)
                parts[prefix.Length + i] = combo[i].SwitchId.ToString();
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

        private enum CertificateStatus : byte
        {
            Certified = 1,
            NotCertified = 2,
            Budget = 3,
        }

        // One Solve-wide ceiling. NodesExpanded remains the public search-only count; this meter
        // prevents canonical provenance/candidate/replay work from escaping the same numeric cap.
        private sealed class SolverWorkMeter
        {
            private readonly long _limit;
            private long _nodesExpanded;
            private long _extraWork;

            internal bool Exceeded { get; private set; }

            internal SolverWorkMeter(int limit)
            {
                _limit = Math.Max(0, limit);
            }

            internal bool SetNodesExpanded(int nodesExpanded)
            {
                if (nodesExpanded > _nodesExpanded) _nodesExpanded = nodesExpanded;
                if (_nodesExpanded > _limit || _extraWork > _limit - _nodesExpanded)
                    return Reject();
                return true;
            }

            internal bool TryChargeReplay(int commandCount, int timeLimitTicks)
            {
                long commands = Math.Max(1, commandCount);
                long ticks = Math.Max(1, timeLimitTicks);
                if (commands > long.MaxValue / ticks) return Reject();
                return TryCharge(commands * ticks);
            }

            internal bool TryChargeProxy(
                int commandCount, int completionHorizon, int timeLimitTicks)
            {
                long commands = Math.Max(0, commandCount);
                long primaryCommands = Math.Max(1, commands);
                long completion = Math.Max(1, completionHorizon);
                long timeLimit = Math.Max(1, timeLimitTicks);
                if (primaryCommands > long.MaxValue / completion) return Reject();
                long units = primaryCommands * completion;
                if (commands == 0) return TryCharge(units);

                if (commands > long.MaxValue / commands) return Reject();
                long squared = commands * commands;
                if (squared > long.MaxValue / timeLimit) return Reject();
                long perturbation = squared * timeLimit;
                if (perturbation > long.MaxValue / 2) return Reject();
                perturbation *= 2;
                if (units > long.MaxValue - perturbation) return Reject();
                return TryCharge(units + perturbation);
            }

            internal bool TryCharge(long units)
            {
                if (Exceeded) return false;
                if (units < 0) return Reject();
                if (_nodesExpanded > _limit || _extraWork > _limit - _nodesExpanded)
                    return Reject();
                long remaining = _limit - _nodesExpanded - _extraWork;
                if (units > remaining) return Reject();
                _extraWork += units;
                return true;
            }

            private bool Reject()
            {
                Exceeded = true;
                return false;
            }
        }

        private static int CompareBeam(SearchFrontier a, SearchFrontier b)
        {
            if (a.State.Deliveries != b.State.Deliveries)
                return b.State.Deliveries.CompareTo(a.State.Deliveries);
            var da = Digest(a.State);
            var db = Digest(b.State);
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

        private static string DigestKey(SimulationState state, bool omitNonBehavioralCounters = false)
        {
            var digest = Digest(state);
            if (omitNonBehavioralCounters)
            {
                const int rejectionsOffset = 4 * 4; // fifth int in WriteDigest's frozen layout
                const int switchesUsedOffset = 6 * 4; // seventh int in WriteDigest's frozen layout
                Array.Clear(digest, rejectionsOffset, 4);
                Array.Clear(digest, switchesUsedOffset, 4);
            }
            return Convert.ToBase64String(digest);
        }

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
                            bool forwardInbound = tr.State == TrainState.OnEdge
                                && graph.EdgeTo[tr.EdgeId] == node;
                            bool reverseInbound = tr.State == TrainState.OnEdgeReverse
                                && graph.EdgeFrom[tr.EdgeId] == node;
                            if (forwardInbound || reverseInbound)
                            {
                                active = true;
                                break;
                            }
                        }
                    }
                    if (active) pending++;
                }
                if (pending > maxPending) maxPending = pending;

                // Axis H (queue term only): find the peak-load tick and the
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
