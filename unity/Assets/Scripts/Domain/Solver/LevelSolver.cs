using System;

namespace CatMetro.Domain.Solver
{
    // CM-C4: BFS for <=2-switch boards (exact, tick-minimal), beam search at the authored widths
    // beyond, sharing the ONE shipped Simulation.Step symbol (ADR-0002 §2 — the solver may only
    // reach state through Step; criterion 2's grep enforces no tick-advancing writes here).
    //
    // EXECUTOR CANVAS (hybrid lane): implement per the frozen sub-contracts. Every load-bearing
    // semantic is already decided in state/handoffs/CM-C4.md (search-node/dedupe = WriteDigest
    // byte image; action space = 0..routeCount-1 toggles per switch per tick with
    // entry.Tick == stepTick - 1; BFS layer-minimality + the criterion-7 tie-break; beam ordering
    // Deliveries desc then digest-lex asc; Q-N pin pruning catches NotSupportedException around
    // Step ONLY; budget counts expansions; proxy rules in the handoff). The tests are the
    // executable spec — code to them; never edit them.
    public static class LevelSolver
    {
        public static SolveResult Solve(LevelGraph graph, ulong seed,
            int maxNodesExpanded = SolverBounds.MAX_NODES_EXPANDED,
            int[] beamWidths = null)
        {
            throw new NotImplementedException("CM-C4: Solver.Solve not implemented yet (hybrid lane, TDD red)");
        }
    }
}
