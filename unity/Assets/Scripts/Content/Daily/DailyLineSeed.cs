using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CatMetro.Content.Daily
{
    public static class DailyLineSeed
    {
        public const string GeneratorLabel = "CM-DAILY-";

        public static uint Derive(string dateKey)
        {
            if (!DateKeys.IsValid(dateKey))
                throw new ArgumentException(
                    "dateKey must be a real UTC calendar date in yyyy-MM-dd form", nameof(dateKey));

            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(GeneratorLabel + dateKey));
                return ((uint)digest[28] << 24) | ((uint)digest[29] << 16)
                    | ((uint)digest[30] << 8) | digest[31];
            }
        }

        public static uint DeriveFromUnixSeconds(long unixSeconds) =>
            Derive(DateKeyFromUnixSeconds(unixSeconds));

        public static string DateKeyFromUnixSeconds(long unixSeconds)
        {
            const long SecondsPerDay = 86400;
            const long MinUnixSeconds = -62135596800L;
            const long MaxUnixSeconds = 253402300799L;
            if (unixSeconds < MinUnixSeconds || unixSeconds > MaxUnixSeconds)
                throw new ArgumentOutOfRangeException(nameof(unixSeconds),
                    "Unix seconds must name a civil date in years 1 through 9999");

            long days = FloorDivide(unixSeconds, SecondsPerDay);
            long z = days + 719468;
            long era = (z >= 0 ? z : z - 146096) / 146097;
            long doe = z - era * 146097;
            long yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
            long year = yoe + era * 400;
            long doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
            long monthPart = (5 * doy + 2) / 153;
            long day = doy - (153 * monthPart + 2) / 5 + 1;
            long month = monthPart + (monthPart < 10 ? 3 : -9);
            year += month <= 2 ? 1 : 0;

            return year.ToString("0000", CultureInfo.InvariantCulture) + "-"
                + month.ToString("00", CultureInfo.InvariantCulture) + "-"
                + day.ToString("00", CultureInfo.InvariantCulture);
        }

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
