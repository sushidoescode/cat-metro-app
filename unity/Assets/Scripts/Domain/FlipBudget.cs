using System;

namespace CatMetro.Domain
{
    public enum FlipRating : byte
    {
        Ungated = 0,
        Solved = 1,
        Efficient = 2,
        Perfect = 3,
    }

    public readonly struct FlipBudgetStatus
    {
        public FlipBudgetStatus(
            int perfectMaxSwitches,
            int used,
            int twoStarMaxSwitches,
            FlipRating rating)
        {
            PerfectMaxSwitches = perfectMaxSwitches;
            Used = used;
            TwoStarMaxSwitches = twoStarMaxSwitches;
            Rating = rating;
        }

        public int PerfectMaxSwitches { get; }
        public int Used { get; }
        public int TwoStarMaxSwitches { get; }
        public FlipRating Rating { get; }
        public bool IsBudgeted => PerfectMaxSwitches >= 0;
        public int RemainingToPerfect => IsBudgeted ? PerfectMaxSwitches - Used : 0;
        public bool IsOverPerfect => IsBudgeted && Used > PerfectMaxSwitches;
        public int RatingStars => (int)Rating;
    }

    public static class FlipBudget
    {
        public const int Unbudgeted = -1;

        public static FlipBudgetStatus Evaluate(int perfectMaxSwitches, int used)
        {
            if (used < 0) throw new ArgumentOutOfRangeException(nameof(used));
            if (perfectMaxSwitches < Unbudgeted)
                throw new ArgumentOutOfRangeException(nameof(perfectMaxSwitches));

            if (perfectMaxSwitches == Unbudgeted)
                return new FlipBudgetStatus(Unbudgeted, used, Unbudgeted, FlipRating.Ungated);

            // Budgeted runtime states can never exceed this hard cap. Preserve the public
            // rating-shaped value for constructor/API compatibility: accepted play is Perfect;
            // an externally fabricated over-cap count is visibly invalid rather than receiving
            // the retired soft Efficient band.
            int twoStarMaxSwitches = perfectMaxSwitches;
            FlipRating rating = used <= perfectMaxSwitches
                ? FlipRating.Perfect
                : FlipRating.Solved;

            return new FlipBudgetStatus(
                perfectMaxSwitches,
                used,
                twoStarMaxSwitches,
                rating);
        }

        public static bool CanAccept(int perfectMaxSwitches, int used)
        {
            if (used < 0) throw new ArgumentOutOfRangeException(nameof(used));
            if (perfectMaxSwitches < Unbudgeted)
                throw new ArgumentOutOfRangeException(nameof(perfectMaxSwitches));
            return perfectMaxSwitches == Unbudgeted || used < perfectMaxSwitches;
        }
    }
}
