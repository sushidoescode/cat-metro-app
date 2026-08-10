using System;
using System.Collections.Generic;

namespace CatMetro.Domain.Solver
{
    // Internal policy primitive kept separate so interacting-window behavior can be specified
    // without constructing a level whose train routes happen to encode a particular relation.
    internal static class MidWindowNormalizer
    {
        // Constant work envelope: one centering sweep plus at most two repair/verification
        // sweeps. Unlike the rejected 4*C cap, refinement passes never scale with command count.
        private const int MaxSweeps = 3;

        internal static bool TryCenter(
            int[] seedTicks,
            int upperExclusive,
            Func<int[], bool> sameCompletionWin,
            out int[] centeredTicks) =>
            TryCenter(null, seedTicks, upperExclusive, sameCompletionWin, out centeredTicks);

        // The overload carries immutable receipt switch ids into the policy seam; tick-only
        // callers retain the prior behavior while solver integration supplies the ids.
        internal static bool TryCenter(
            ushort[] receiptSwitches,
            int[] seedTicks,
            int upperExclusive,
            Func<int[], bool> sameCompletionWin,
            out int[] centeredTicks)
        {
            centeredTicks = (int[])seedTicks.Clone();
            if (centeredTicks.Length == 0) return true;

            var seen = new HashSet<string> { Key(centeredTicks) };
            for (int sweep = 0; sweep < MaxSweeps; sweep++)
            {
                var before = (int[])centeredTicks.Clone();
                CenterOneSweep(centeredTicks, upperExclusive, sameCompletionWin);
                if (!ReceiptOrderPreserved(receiptSwitches, centeredTicks))
                {
                    centeredTicks = (int[])seedTicks.Clone();
                    return false;
                }
                if (Equal(before, centeredTicks)) return true;
                if (!seen.Add(Key(centeredTicks))) break;
            }

            // Interacting windows either cycled or did not reach a fixed point inside the constant
            // work envelope. Stop deterministically; never present a changing boundary as centered.
            centeredTicks = (int[])seedTicks.Clone();
            return false;
        }

        // Exact-history selection can prove a complete winning relation symbolically. These two
        // read-only queries reuse the same maximal-window definition without copying its scan.
        internal static int MiddleFor(
            int[] ticks,
            int index,
            int upperExclusive,
            Func<int[], bool> sameCompletionWin) =>
            FindMiddle(ticks, index, upperExclusive, sameCompletionWin);

        internal static bool IsFixedPoint(
            int[] ticks,
            int upperExclusive,
            Func<int[], bool> sameCompletionWin)
        {
            for (int i = 0; i < ticks.Length; i++)
                if (FindMiddle(ticks, i, upperExclusive, sameCompletionWin) != ticks[i])
                    return false;
            return true;
        }

        private static bool ReceiptOrderPreserved(ushort[] switches, int[] ticks)
        {
            if (switches != null && switches.Length != ticks.Length) return false;
            for (int i = 1; i < ticks.Length; i++)
                if (ticks[i] < ticks[i - 1]
                    || (switches != null && ticks[i] == ticks[i - 1]
                        && switches[i] < switches[i - 1]))
                    return false;
            return true;
        }

        private static void CenterOneSweep(
            int[] ticks,
            int upperExclusive,
            Func<int[], bool> sameCompletionWin)
        {
            for (int i = 0; i < ticks.Length; i++)
            {
                ticks[i] = FindMiddle(ticks, i, upperExclusive, sameCompletionWin);
            }
        }

        private static bool Equal(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static string Key(int[] ticks) => string.Join(",", ticks);

        private static int FindMiddle(
            int[] ticks,
            int index,
            int upperExclusive,
            Func<int[], bool> sameCompletionWin)
        {
            int current = ticks[index];
            int lower = current;
            while (lower > 0
                && WinsWithTick(ticks, index, lower - 1, sameCompletionWin))
                lower--;

            int upper = current;
            while (upper < upperExclusive - 1
                && WinsWithTick(ticks, index, upper + 1, sameCompletionWin))
                upper++;

            return lower + (upper - lower) / 2;
        }

        private static bool WinsWithTick(
            int[] ticks,
            int index,
            int tick,
            Func<int[], bool> sameCompletionWin)
        {
            // The predicate is synchronous and read-only. Mutate/restore one coordinate so a
            // maximal-window scan does not allocate a full command vector for every probe.
            int original = ticks[index];
            ticks[index] = tick;
            try
            {
                return sameCompletionWin(ticks);
            }
            finally
            {
                ticks[index] = original;
            }
        }
    }
}
