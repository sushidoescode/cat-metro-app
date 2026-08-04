namespace CatMetro.Domain.Solver
{
    // CM-C4 criterion 11 + A-C4-5: the work bound is an expansion COUNT, never a clock — no
    // wall-time symbol exists under the Domain root (scripts/check.sh bans them).
    public static class SolverBounds
    {
        // Analyst-authored (A-C4-5, flagged in the PR — NOT a corpus number). Measured cost model
        // (review M6): re-simulation-per-child makes exhaustion ~O(T^2) expansions / ~O(T^3) time
        // — a 640-tick single-switch exhaustion measured 204,541 expansions in ~74 s — so this cap
        // bounds a pathological worst case at tens of minutes, and normal boards (the committed
        // fixtures peak at 23,390 expansions / 1.2 s) never approach it. Lower it by ordinary
        // amendment when CM-C5 batches levels, or optimize ReplayTo to incremental stepping then.
        public const int MAX_NODES_EXPANDED = 2000000;

        // ADR-0008:116 / product_spec.md:640 — the three authored widths, ascending. The Solve
        // parameter defaults to this array (a test asserts the identity so the injection point
        // cannot drift; the test-only override exists for the same reason criterion 11 demands
        // the budget parameter).
        public static readonly int[] BEAM_WIDTHS = { 1000, 2500, 5000 };
    }
}
