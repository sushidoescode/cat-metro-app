using System;
using System.Collections.Generic;

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
            Func<bool> charge)
        {
            if (switches == null || minTicks == null || maxTicks == null || charge == null
                || switches.Length != minTicks.Length || switches.Length != maxTicks.Length
                || observedCount < 0)
                return OrderedTickBoxStatus.Incomplete;
            if (switches.Length == 0)
                return observedCount == 1
                    ? OrderedTickBoxStatus.Complete : OrderedTickBoxStatus.Incomplete;

            int maxTick = 0;
            for (int i = 0; i < maxTicks.Length; i++)
            {
                if (!charge()) return OrderedTickBoxStatus.WorkLimit;
                if (minTicks[i] < 0 || maxTicks[i] < minTicks[i])
                    return OrderedTickBoxStatus.Incomplete;
                if (maxTicks[i] > maxTick) maxTick = maxTicks[i];
            }

            long[] previous = null;
            for (int i = 0; i < switches.Length; i++)
            {
                // Reserve the zeroed DP row before allocating it; the following loop separately
                // charges the actual recurrence over that row.
                for (int tick = 0; tick <= maxTick; tick++)
                    if (!charge()) return OrderedTickBoxStatus.WorkLimit;
                var current = new long[maxTick + 1];
                long prefix = 0;
                bool equalAllowed = i == 0 || switches[i - 1] <= switches[i];
                for (int tick = 0; tick <= maxTick; tick++)
                {
                    if (!charge()) return OrderedTickBoxStatus.WorkLimit;
                    if (tick >= minTicks[i] && tick <= maxTicks[i])
                    {
                        if (i == 0)
                        {
                            current[tick] = 1;
                        }
                        else
                        {
                            long count = prefix;
                            if (equalAllowed)
                            {
                                if (count > long.MaxValue - previous[tick])
                                    return OrderedTickBoxStatus.Incomplete;
                                count += previous[tick];
                            }
                            current[tick] = count;
                        }
                    }

                    if (i > 0)
                    {
                        if (prefix > long.MaxValue - previous[tick])
                            return OrderedTickBoxStatus.Incomplete;
                        prefix += previous[tick];
                    }
                }
                previous = current;
            }

            long total = 0;
            for (int tick = 0; tick < previous.Length; tick++)
            {
                if (!charge()) return OrderedTickBoxStatus.WorkLimit;
                if (total > long.MaxValue - previous[tick])
                    return OrderedTickBoxStatus.Incomplete;
                total += previous[tick];
            }
            return total == observedCount
                ? OrderedTickBoxStatus.Complete : OrderedTickBoxStatus.Incomplete;
        }
    }

    internal readonly struct OccurrenceTickBoxGroup
    {
        internal readonly ushort[] Switches;
        internal readonly int[] MinTicks;
        internal readonly int[] MaxTicks;
        internal readonly long Count;
        internal readonly bool CountOverflow;

        internal OccurrenceTickBoxGroup(
            ushort[] switches, int[] minTicks, int[] maxTicks,
            long count, bool countOverflow = false)
        {
            Switches = switches;
            MinTicks = minTicks;
            MaxTicks = maxTicks;
            Count = count;
            CountOverflow = countOverflow;
        }
    }

    internal static class OccurrenceTickProduct
    {
        // Caller precondition: every group is a unique, complete chronological box already
        // certified by OrderedTickBox.Classify. This second certificate proves whether their
        // union is a full product of strictly separated per-switch occurrence envelopes.
        internal static OrderedTickBoxStatus Classify(
            int switchCount,
            int timeLimitTicks,
            IReadOnlyList<OccurrenceTickBoxGroup> groups,
            Func<long, bool> charge,
            out ToggleSwitchCommand[] midpointEntries)
        {
            midpointEntries = null;
            if (switchCount <= 0 || timeLimitTicks <= 0 || groups == null
                || groups.Count == 0 || charge == null)
                return OrderedTickBoxStatus.Incomplete;

            var first = groups[0];
            if (first.Switches == null || first.MinTicks == null || first.MaxTicks == null
                || first.Switches.Length == 0
                || first.Switches.Length != first.MinTicks.Length
                || first.Switches.Length != first.MaxTicks.Length)
                return OrderedTickBoxStatus.Incomplete;

            int commandCount = first.Switches.Length;
            long setupUnits = Math.Max(1L,
                (long)commandCount * 4 + (long)switchCount * 3 + groups.Count);
            if (!charge(setupUnits)) return OrderedTickBoxStatus.WorkLimit;

            var counts = new int[switchCount];
            foreach (ushort switchId in first.Switches)
            {
                if (switchId >= switchCount) return OrderedTickBoxStatus.Incomplete;
                counts[switchId]++;
            }

            var minTicks = new int[switchCount][];
            var maxTicks = new int[switchCount][];
            for (int switchId = 0; switchId < switchCount; switchId++)
            {
                minTicks[switchId] = new int[counts[switchId]];
                maxTicks[switchId] = new int[counts[switchId]];
                for (int occurrence = 0; occurrence < counts[switchId]; occurrence++)
                    minTicks[switchId][occurrence] = int.MaxValue;
            }

            long observedCount = 0;
            foreach (var group in groups)
            {
                if (group.Switches == null || group.MinTicks == null || group.MaxTicks == null
                    || group.Switches.Length != commandCount
                    || group.MinTicks.Length != commandCount
                    || group.MaxTicks.Length != commandCount
                    || group.Count < 0 || group.CountOverflow
                    || observedCount > long.MaxValue - group.Count)
                    return OrderedTickBoxStatus.Incomplete;
                observedCount += group.Count;

                if (!charge(Math.Max(1, commandCount)))
                    return OrderedTickBoxStatus.WorkLimit;
                var seen = new int[switchCount];
                for (int i = 0; i < commandCount; i++)
                {
                    int switchId = group.Switches[i];
                    if (switchId < 0 || switchId >= switchCount)
                        return OrderedTickBoxStatus.Incomplete;
                    int occurrence = seen[switchId]++;
                    if (occurrence >= counts[switchId])
                        return OrderedTickBoxStatus.Incomplete;
                    if (group.MinTicks[i] < minTicks[switchId][occurrence])
                        minTicks[switchId][occurrence] = group.MinTicks[i];
                    if (group.MaxTicks[i] > maxTicks[switchId][occurrence])
                        maxTicks[switchId][occurrence] = group.MaxTicks[i];
                }
                for (int switchId = 0; switchId < switchCount; switchId++)
                    if (seen[switchId] != counts[switchId])
                        return OrderedTickBoxStatus.Incomplete;
            }

            long productCount = 1;
            for (int switchId = 0; switchId < switchCount; switchId++)
            {
                for (int occurrence = 0; occurrence < counts[switchId]; occurrence++)
                {
                    int min = minTicks[switchId][occurrence];
                    int max = maxTicks[switchId][occurrence];
                    if (min < 0 || max < min || max >= timeLimitTicks)
                        return OrderedTickBoxStatus.Incomplete;
                    if (occurrence > 0
                        && maxTicks[switchId][occurrence - 1] >= min)
                        return OrderedTickBoxStatus.Incomplete;

                    long width = (long)max - min + 1;
                    if (productCount > long.MaxValue / width)
                        return OrderedTickBoxStatus.Incomplete;
                    productCount *= width;
                }
            }
            if (productCount != observedCount) return OrderedTickBoxStatus.Incomplete;

            long sortUnits = Math.Max(1, commandCount);
            for (int width = commandCount; width > 1; width = (width + 1) / 2)
                sortUnits += commandCount;
            if (!charge(sortUnits)) return OrderedTickBoxStatus.WorkLimit;

            midpointEntries = new ToggleSwitchCommand[commandCount];
            int at = 0;
            for (int switchId = 0; switchId < switchCount; switchId++)
                for (int occurrence = 0; occurrence < counts[switchId]; occurrence++)
                {
                    int min = minTicks[switchId][occurrence];
                    int max = maxTicks[switchId][occurrence];
                    midpointEntries[at++] = new ToggleSwitchCommand(
                        (ushort)switchId, min + (max - min) / 2);
                }
            Array.Sort(midpointEntries, (a, b) =>
            {
                int byTick = a.Tick.CompareTo(b.Tick);
                return byTick != 0 ? byTick : a.SwitchId.CompareTo(b.SwitchId);
            });
            return OrderedTickBoxStatus.Complete;
        }
    }
}
