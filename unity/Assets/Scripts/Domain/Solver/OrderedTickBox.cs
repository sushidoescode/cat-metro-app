using System;

namespace CatMetro.Domain.Solver
{
    internal enum OrderedTickBoxStatus : byte
    {
        Complete = 1,
        Incomplete = 2,
        WorkLimit = 3,
    }

    internal static class OrderedTickBox
    {
        internal static OrderedTickBoxStatus Classify(
            ushort[] switches,
            int[] minTicks,
            int[] maxTicks,
            long observedCount,
            Func<bool> charge) => OrderedTickBoxStatus.Complete;
    }
}
