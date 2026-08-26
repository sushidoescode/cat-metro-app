using System;

namespace CatMetro.Domain
{
    // MIS-DELIVERY — what happens when a cat reaches a station that will not take it.
    //
    // Today (and still, by default, after this file) the simulation THROWS
    // `NotSupportedException("pinned NEW-Q4 ...")` at Simulation.Step's step 5. That pin is not
    // inert scaffolding — the solver DEPENDS on it: LevelSolver catches the exception and counts
    // the branch in `PinnedPruned`, which is how "route the reds into the blue station" gets
    // pruned out of the search. Redefining mis-delivery therefore changes the solver's search
    // space, not just a gameplay rule, which is why the default here stays `Pinned` and the new
    // semantics is opt-in per level.
    //
    // The proposal, the four candidate semantics and the recommendation live in
    // docs/design/MISDELIVERY.md. Short version: `RefuseAndSendHome` is the only candidate the
    // current Domain can express without changing the digest layout, because `SimulationState
    // .Rejections` is ALREADY a field in the frozen byte layout, reserved for exactly this and
    // pinned to 0 since CM-C1. Riding on needs an outgoing edge a leaf station does not have;
    // reversing needs a direction bit per train, which widens TrainSlot and breaks every golden
    // replay hash.

    /// <summary>What the simulation does with a cat that arrives at a station refusing its colour.</summary>
    public enum MisdeliveryPolicy : byte
    {
        /// <summary>
        /// Throw. The CM-C1 pin, and the default, so every existing fixture and the solver's
        /// pruning behaviour are byte-for-byte unchanged.
        /// </summary>
        Pinned = 0,

        /// <summary>
        /// The recommendation. The station politely declines, the cat leaves the board,
        /// `Rejections` increments, and the run continues. Costs rating, never the run — the same
        /// cosy stance the flip budget takes. Delivery count is NOT incremented, so a level whose
        /// wave supply exactly meets `win.deliveries` becomes unwinnable-by-time rather than
        /// unwinnable-by-rule, which is a nudge the player can read.
        /// </summary>
        RefuseAndSendHome = 1,
    }

    /// <summary>Helpers for reasoning about mis-delivery without reaching into Simulation.</summary>
    public static class Misdelivery
    {
        /// <summary>The exact pin message, so tests and the solver assert one string, not two.</summary>
        public const string PinnedMessage =
            "pinned NEW-Q4: a non-matching cat arrived at a station — rejection/reverse traversal is out of CM-C1 scope (state/backlog.md Q-B, criterion 14)";

        /// <summary>True when <paramref name="policy"/> lets the run survive a mis-delivery.</summary>
        public static bool IsSurvivable(MisdeliveryPolicy policy) => policy == MisdeliveryPolicy.RefuseAndSendHome;

        /// <summary>Throwing helper, kept in one place so the message cannot drift.</summary>
        public static NotSupportedException PinnedException() => new NotSupportedException(PinnedMessage);
    }
}
