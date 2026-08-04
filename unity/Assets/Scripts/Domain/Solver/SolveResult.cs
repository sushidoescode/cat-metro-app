namespace CatMetro.Domain.Solver
{
    // CM-C4 criterion 6: the result record, exactly these members. Frontier-authored (planner
    // surface, state/handoffs/CM-C4.md); the executor populates, never reshapes.
    // NO score/star/chain/ticket member may ever be added (criterion 8; pins NEW-Q5/NEW-Q7).
    public enum SolveVerdict : byte
    {
        Solved = 1,
        Unsolvable = 2,     // BFS-only: the reachable space was exhausted (criterion 3)
        NotFound = 3,       // beam miss or budget hit — explicitly NOT a proof (criterion 4/11)
        Indeterminate = 4,  // pinned branches pruned and no win found (criterion 5, Q-N)
    }

    public enum NotFoundReason : byte
    {
        None = 0,
        Beam = 1,
        Budget = 2,
    }

    // The integer inputs for difficulty axes C/T/H/R (product_spec.md:504-511). The weighted
    // axis arithmetic is CM-C5's — real-number work lives outside the Domain's numeric ban.
    // Axis H carries the QUEUE term only
    // while Q-J/NEW-Q4 keep platform overflow unraisable — recorded, not assumed away (A-C4-4).
    // Executable counting rules: state/handoffs/CM-C4.md §Planner rulings round 2.
    public readonly struct DifficultyProxy
    {
        public readonly int MaxSimultaneousPendingDecisions; // axis C
        public readonly int SolverOptimalTicks;              // axis T numerator
        public readonly int TimeLimitTicks;                  // axis T denominator
        public readonly int MinQueueSlackAtPeak;             // axis H, queue term only — PARTIAL(Q-J)
        public readonly int SinglePerturbationsWinnable;     // axis R numerator
        public readonly int SinglePerturbationsTried;        // axis R denominator

        public DifficultyProxy(int maxSimultaneousPendingDecisions, int solverOptimalTicks,
            int timeLimitTicks, int minQueueSlackAtPeak,
            int singlePerturbationsWinnable, int singlePerturbationsTried)
        {
            MaxSimultaneousPendingDecisions = maxSimultaneousPendingDecisions;
            SolverOptimalTicks = solverOptimalTicks;
            TimeLimitTicks = timeLimitTicks;
            MinQueueSlackAtPeak = minQueueSlackAtPeak;
            SinglePerturbationsWinnable = singlePerturbationsWinnable;
            SinglePerturbationsTried = singlePerturbationsTried;
        }
    }

    public sealed class SolveResult
    {
        public readonly SolveVerdict Verdict;
        public readonly NotFoundReason NotFoundReason;   // None unless Verdict == NotFound
        public readonly CommandLog OptimalLog;           // empty log when no win was found
        public readonly int CompletionTicks;             // == RunToEnd(graph, seed, log).Tick - 1 when Solved; 0 otherwise
        public readonly int SwitchesUsed;                // command count of OptimalLog
        public readonly int BeamWidthUsed;               // 0 for BFS; the succeeding width for beam
        public readonly int PinnedPruned;                // Q-N counter
        public readonly int NodesExpanded;
        public readonly string FirstPinMessage;          // "" when PinnedPruned == 0
        public readonly DifficultyProxy Proxy;           // fully populated only when Solved (planner ruling)

        public SolveResult(SolveVerdict verdict, NotFoundReason notFoundReason, CommandLog optimalLog,
            int completionTicks, int switchesUsed, int beamWidthUsed, int pinnedPruned,
            int nodesExpanded, string firstPinMessage, DifficultyProxy proxy)
        {
            Verdict = verdict;
            NotFoundReason = notFoundReason;
            OptimalLog = optimalLog;
            CompletionTicks = completionTicks;
            SwitchesUsed = switchesUsed;
            BeamWidthUsed = beamWidthUsed;
            PinnedPruned = pinnedPruned;
            NodesExpanded = nodesExpanded;
            FirstPinMessage = firstPinMessage ?? "";
            Proxy = proxy;
        }
    }
}
