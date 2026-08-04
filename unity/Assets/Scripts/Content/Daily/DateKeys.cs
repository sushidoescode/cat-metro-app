using System.Collections.Generic;

namespace CatMetro.Content.Daily
{
    // CM-C6 criterion 2: date keys are INPUTS ("yyyy-MM-dd" strings, A-C6-4) — never read from a
    // clock. Everything here is pure civil-calendar integer arithmetic: form validation with real
    // month/day/leap rules, enumeration by y/m/d increment, and weekday via Sakamoto's congruence
    // (A-C6-10). No time type, no timezone, no locale — which is exactly what keeps the
    // local-midnight/DST question (liveops_spec.md:29-31) out of this contract.
    public static class DateKeys
    {
        // True iff dateKey is exactly "yyyy-MM-dd" and names a real calendar date.
        public static bool IsValid(string dateKey)
        {
            throw new System.NotImplementedException();
        }

        // The count consecutive date keys starting at fromKey (fromKey itself is entry 0).
        public static IReadOnlyList<string> Enumerate(string fromKey, int count)
        {
            throw new System.NotImplementedException();
        }

        // 0 = Monday .. 6 = Sunday (the liveops weekday-table row order).
        public static int WeekdayIndex(string dateKey)
        {
            throw new System.NotImplementedException();
        }
    }
}
