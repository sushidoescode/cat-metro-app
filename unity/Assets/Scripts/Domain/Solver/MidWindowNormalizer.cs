using System;

namespace CatMetro.Domain.Solver
{
    // Internal policy primitive kept separate so interacting-window behavior can be specified
    // without constructing a level whose train routes happen to encode a particular relation.
    internal static class MidWindowNormalizer
    {
        internal static bool TryCenter(
            int[] seedTicks,
            int upperExclusive,
            Func<int[], bool> sameCompletionWin,
            out int[] centeredTicks)
        {
            centeredTicks = (int[])seedTicks.Clone();
            if (centeredTicks.Length == 0) return true;

            // Behavior-neutral extraction of the first implementation. Review round 1 pins this
            // arbitrary cap as the defect: it reports success even when its final pass changed.
            int passLimit = Math.Max(1, centeredTicks.Length * 4);
            for (int pass = 0; pass < passLimit; pass++)
            {
                bool changed = false;
                for (int i = 0; i < centeredTicks.Length; i++)
                {
                    int current = centeredTicks[i];
                    int lower = current;
                    while (lower > 0
                        && WinsWithTick(centeredTicks, i, lower - 1, sameCompletionWin))
                        lower--;

                    int upper = current;
                    while (upper < upperExclusive - 1
                        && WinsWithTick(centeredTicks, i, upper + 1, sameCompletionWin))
                        upper++;

                    int middle = lower + (upper - lower) / 2;
                    if (middle == current) continue;
                    centeredTicks[i] = middle;
                    changed = true;
                }

                if (!changed) return true;
            }

            return true;
        }

        private static bool WinsWithTick(
            int[] ticks,
            int index,
            int tick,
            Func<int[], bool> sameCompletionWin)
        {
            var candidate = (int[])ticks.Clone();
            candidate[index] = tick;
            return sameCompletionWin(candidate);
        }
    }
}
