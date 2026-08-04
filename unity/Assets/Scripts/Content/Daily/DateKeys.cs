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
        private static readonly int[] MonthDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        private static readonly int[] Sakamoto = { 0, 3, 2, 5, 0, 3, 5, 1, 4, 6, 2, 4 };

        private static bool IsLeap(int y) => (y % 4 == 0 && y % 100 != 0) || y % 400 == 0;

        private static int DaysInMonth(int y, int m) =>
            m == 2 && IsLeap(y) ? 29 : MonthDays[m - 1];

        // True iff dateKey is exactly "yyyy-MM-dd" and names a real calendar date.
        public static bool IsValid(string dateKey)
        {
            if (dateKey == null || dateKey.Length != 10) return false;
            for (int i = 0; i < 10; i++)
            {
                if (i == 4 || i == 7)
                {
                    if (dateKey[i] != '-') return false;
                }
                else if (dateKey[i] < '0' || dateKey[i] > '9') return false;
            }
            var (y, m, d) = Parse(dateKey);
            return y >= 1 && m >= 1 && m <= 12 && d >= 1 && d <= DaysInMonth(y, m);
        }

        private static (int y, int m, int d) Parse(string key)
        {
            int Digits(int start, int len)
            {
                int v = 0;
                for (int i = start; i < start + len; i++) v = v * 10 + (key[i] - '0');
                return v;
            }
            return (Digits(0, 4), Digits(5, 2), Digits(8, 2));
        }

        private static string Render(int y, int m, int d) =>
            y.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + "-"
            + m.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "-"
            + d.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

        // The count consecutive date keys starting at fromKey (fromKey itself is entry 0).
        public static IReadOnlyList<string> Enumerate(string fromKey, int count)
        {
            if (!IsValid(fromKey))
                throw new System.ArgumentException(
                    "fromKey must be a real calendar date in yyyy-MM-dd form, got: "
                    + (fromKey ?? "<null>"));
            if (count < 1)
                throw new System.ArgumentException("count must be >= 1, got " + count);

            var (y, m, d) = Parse(fromKey);
            var keys = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                keys.Add(Render(y, m, d));
                d++;
                if (d > DaysInMonth(y, m))
                {
                    d = 1; m++;
                    if (m > 12) { m = 1; y++; }
                }
            }
            return keys;
        }

        // 0 = Monday .. 6 = Sunday (the liveops weekday-table row order). Sakamoto's congruence
        // yields 0 = Sunday; shifted by 6 to the table's Monday-first order.
        public static int WeekdayIndex(string dateKey)
        {
            if (!IsValid(dateKey))
                throw new System.ArgumentException(
                    "dateKey must be a real calendar date in yyyy-MM-dd form, got: "
                    + (dateKey ?? "<null>"));
            var (y, m, d) = Parse(dateKey);
            if (m < 3) y--;
            int sundayFirst = (y + y / 4 - y / 100 + y / 400 + Sakamoto[m - 1] + d) % 7;
            return (sundayFirst + 6) % 7;
        }
    }
}
