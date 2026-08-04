namespace CatMetro.Domain.Solver
{
    // CM-C4 criterion 11 + A-C4-5: the work bound is an expansion COUNT, never a clock — no
    // wall-time symbol exists under the Domain root (scripts/check.sh bans them).
    public static class SolverBounds
    {
        // Analyst-authored (A-C4-5, flagged in the PR — NOT a corpus number). Derivation: covers
        // a full 500-states-per-tick frontier across the schema's 4000-tick ceiling while keeping
        // a worst-case run to low minutes of allocation-light integer stepping on CI hardware.
        public const int MAX_NODES_EXPANDED = 2000000;

        // ADR-0008:116 / product_spec.md:640 — the three authored widths, ascending. The Solve
        // parameter defaults to this array (a test asserts the identity so the injection point
        // cannot drift; the test-only override exists for the same reason criterion 11 demands
        // the budget parameter).
        public static readonly int[] BEAM_WIDTHS = { 1000, 2500, 5000 };
    }
}
