using System;

namespace CatMetro.Domain
{
    // The ONE step implementation (ADR-0002 §2): solver, validator, runtime and capture rig all
    // call this symbol. Implements the authoritative per-tick order of operations at
    // docs/plan/specs/product_spec.md:218-227; step 7 (score/combo) is deferred with scoring
    // (pin NEW-Q5 / Q-C) and steps 5-6 carry the criterion-14 pin guards.
    public static class Simulation
    {
        // commandsThisTick: the commands due at THIS tick's step 1 — i.e. commands enqueued
        // during tick (state.Tick - 1), selected by the caller/runner (CM-R07.3).
        public static void Step(ref SimulationState state, ReadOnlySpan<ToggleSwitchCommand> commandsThisTick)
        {
            throw new NotImplementedException("CM-C1: Simulation.Step not implemented yet (TDD red)");
        }
    }
}
