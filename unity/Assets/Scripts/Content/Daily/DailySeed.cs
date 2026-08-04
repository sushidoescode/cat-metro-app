using System.Security.Cryptography;
using System.Text;

namespace CatMetro.Content.Daily
{
    // CM-C6 criterion 1: seed = lower 32 bits of SHA-256(GENERATOR_CONSTANT + "|" + dateKey +
    // "|" + k). "Lower 32 bits" = the last 4 digest bytes read big-endian — the digest as the
    // canonical big-endian 256-bit integer, mod 2^32 (handoff A-C6-6; golden-adjacent: changing
    // this changes every daily seed). k is rendered as its invariant decimal form (A-C6-3).
    // The constant is FROZEN through Sep 30 (CM-R11.7, liveops_spec.md:57); a contract test
    // asserts it verbatim. NEW-Q8 carried, not resolved: this is the adopted liveops reading.
    public static class DailySeed
    {
        public const string GENERATOR_CONSTANT = "CM-DAILY-1";

        public static uint Derive(string dateKey, int k)
        {
            if (!DateKeys.IsValid(dateKey))
                throw new System.ArgumentException(
                    "dateKey must be a real calendar date in yyyy-MM-dd form (A-C6-4), got: "
                    + (dateKey ?? "<null>"));
            if (k < 0)
                throw new System.ArgumentException("salt k must be >= 0, got " + k);

            string preimage = GENERATOR_CONSTANT + "|" + dateKey + "|"
                + k.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using (var sha = SHA256.Create())
            {
                byte[] d = sha.ComputeHash(Encoding.UTF8.GetBytes(preimage));
                return ((uint)d[28] << 24) | ((uint)d[29] << 16) | ((uint)d[30] << 8) | d[31];
            }
        }
    }
}
