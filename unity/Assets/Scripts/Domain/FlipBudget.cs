using System;

namespace CatMetro.Domain
{
    // FLIP BUDGET — "how few switch flips did you solve it in?"
    //
    // `win.perfectMaxSwitches` has been authored in all 17 levels since the corpus was written
    // and has never been read by anything (it loads into WinDto and stops there). This file is
    // the missing consumer. It turns that authored number into par for a golf-shaped rating.
    //
    // WHY A RATING AND NOT A WALL
    // ---------------------------
    // Exceeding par does NOT fail the level. Project Horseshoe's coziness report defines cosy as
    // "an absence of danger and risk"; a budget that ends the run is a danger. The budget instead
    // decides which of three tiers the win lands in, so the constraint drives *replay* ("beat it
    // again in par") rather than *loss*. See docs/design/FLIP-BUDGET.md for the full argument and
    // for the exact three-line diff that converts this to a hard wall if the human prefers one —
    // `ExceedsHardWall` below is the predicate that build would call, and it is already tested.
    //
    // DIGEST SAFETY (this is the load-bearing property)
    // -------------------------------------------------
    // Everything here is a PURE FUNCTION of numbers the simulation already tracks:
    // `SimulationState.SwitchesUsed` (incremented at Step's step 1) and `LevelGraph
    // .PerfectMaxSwitches` (immutable board data, explicitly NOT part of the digest). No new
    // field joins SimulationState, so `DigestLength` is unchanged and every golden replay hash
    // still matches. That is why this lands without touching the 857-test core.
    //
    // No UnityEngine types. Deterministic, integer-only (ADR-0002 §3).

    /// <summary>How a level treats a flip budget that has been spent.</summary>
    public enum FlipBudgetPolicy : byte
    {
        /// <summary>Over par costs rating, never the run. The shipped semantics.</summary>
        Rating = 0,

        /// <summary>Over par ends the run. NOT WIRED — see docs/design/FLIP-BUDGET.md §"Switching".</summary>
        HardWall = 1,
    }

    /// <summary>Which band a finished (or in-progress) flip count falls into.</summary>
    public enum FlipTier : byte
    {
        /// <summary>Above the generous band. A win is still a win.</summary>
        Over = 0,

        /// <summary>Solved, but not tidily. The default landing spot.</summary>
        Within = 1,

        /// <summary>At or under par. The thing worth replaying for.</summary>
        Perfect = 2,
    }

    /// <summary>
    /// The read-only flip-budget snapshot. This is the surface the HUD binds to — see
    /// <see cref="SimulationState.FlipStatus"/> and the GameSession properties. Value type, so
    /// reading it can never mutate the run.
    /// </summary>
    public readonly struct FlipBudgetStatus
    {
        /// <summary>Authored `win.perfectMaxSwitches`, or <see cref="FlipBudget.Unbudgeted"/>.</summary>
        public readonly int Par;

        /// <summary>Flips counted so far.</summary>
        public readonly int Used;

        /// <summary>Highest flip count that still earns <see cref="FlipTier.Within"/>.</summary>
        public readonly int WithinMax;

        /// <summary>The band <see cref="Used"/> currently falls in.</summary>
        public readonly FlipTier Tier;

        internal FlipBudgetStatus(int par, int used, int withinMax, FlipTier tier)
        {
            Par = par;
            Used = used;
            WithinMax = withinMax;
            Tier = tier;
        }

        /// <summary>False when the level authored no budget — the HUD should hide the counter.</summary>
        public bool IsBudgeted => Par >= 0;

        /// <summary>
        /// Flips left before par is passed. Goes NEGATIVE once over — the magnitude is how many
        /// flips over par the player is, which is exactly the near-miss number worth showing.
        /// Always 0 when unbudgeted.
        /// </summary>
        public int Remaining => IsBudgeted ? Par - Used : 0;

        /// <summary>True once par has been passed. Never means the run is lost.</summary>
        public bool IsOverPar => IsBudgeted && Used > Par;

        /// <summary>
        /// Stars this flip count would earn ON A WIN: 3 Perfect / 2 Within / 1 Over. A loss earns
        /// none — callers gate on the outcome themselves (see <see cref="FlipBudget.StarsFor"/>).
        /// </summary>
        public int Stars => FlipBudget.StarsFor(Tier);
    }

    /// <summary>Pure evaluation of a flip budget. No state, no allocation, no engine types.</summary>
    public static class FlipBudget
    {
        /// <summary>Par sentinel for a level that authored no budget.</summary>
        public const int Unbudgeted = -1;

        /// <summary>True when <paramref name="par"/> represents a real authored budget.</summary>
        public static bool IsBudgeted(int par) => par >= 0;

        /// <summary>
        /// The generous band's ceiling: twice par, floored at par + 1 so that par 0 ("solve it
        /// without touching a switch") still has a middle band rather than collapsing to
        /// pass/fail. Authored pars today are 1..4, giving Within ceilings of 2..8.
        /// </summary>
        public static int WithinMaxFor(int par) => IsBudgeted(par) ? par + Math.Max(1, par) : Unbudgeted;

        /// <summary>Which band a flip count falls into. Unbudgeted levels always report Within.</summary>
        public static FlipTier TierFor(int par, int switchesUsed)
        {
            if (!IsBudgeted(par)) return FlipTier.Within;
            if (switchesUsed <= par) return FlipTier.Perfect;
            return switchesUsed <= WithinMaxFor(par) ? FlipTier.Within : FlipTier.Over;
        }

        /// <summary>Stars a tier is worth on a win. A loss is worth none regardless of tier.</summary>
        public static int StarsFor(FlipTier tier)
        {
            switch (tier)
            {
                case FlipTier.Perfect: return 3;
                case FlipTier.Within: return 2;
                default: return 1;
            }
        }

        /// <summary>Stars actually awarded, outcome included. The one callers should use.</summary>
        public static int StarsFor(FlipTier tier, bool won) => won ? StarsFor(tier) : 0;

        /// <summary>Build a status snapshot from raw numbers.</summary>
        public static FlipBudgetStatus Evaluate(int par, int switchesUsed) =>
            new FlipBudgetStatus(par, switchesUsed, WithinMaxFor(par), TierFor(par, switchesUsed));

        /// <summary>Build a status snapshot for a live run.</summary>
        public static FlipBudgetStatus Evaluate(SimulationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int par = state.Graph != null ? state.Graph.PerfectMaxSwitches : Unbudgeted;
            return Evaluate(par, state.SwitchesUsed);
        }

        /// <summary>
        /// The predicate a <see cref="FlipBudgetPolicy.HardWall"/> build would consult. Nothing in
        /// the shipped simulation calls it — it exists, and is tested, so that switching semantics
        /// is a wiring change rather than a design exercise. See docs/design/FLIP-BUDGET.md.
        /// </summary>
        public static bool ExceedsHardWall(int par, int switchesUsed) =>
            IsBudgeted(par) && switchesUsed > par;

        /// <summary>
        /// The "Perfect Flow" stamp exactly as product_spec.md:238 already specifies it — a win
        /// with zero rejections, zero Overloads, and switchesUsed within par. Written down since
        /// 2026 and never evaluated until now; this is the evaluator.
        /// </summary>
        public static bool IsPerfectFlow(int par, int switchesUsed, int rejections, int overloads, bool won) =>
            won && rejections == 0 && overloads == 0 && TierFor(par, switchesUsed) == FlipTier.Perfect;
    }
}
