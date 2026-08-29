using System.Text;
using Newtonsoft.Json.Linq;

namespace CatMetro.Content.Daily
{
    // Shipped tuning for Daily entry. Keeping the threshold in staged data lets capture builds
    // set it to zero without a code change while production defaults to seven campaign clears.
    public sealed class DailyLiveConfig
    {
        public const string RelativePath = "config/daily_live.json";
        // Used only when the staged file is unavailable/corrupt. The shipped artifact test
        // couples its normal-path value to this documented production default.
        public const int ProductionDefaultUnlockAfterCampaignCompletions = 7;

        public int UnlockAfterCampaignCompletions { get; }

        private DailyLiveConfig(int unlockAfterCampaignCompletions)
        {
            UnlockAfterCampaignCompletions = unlockAfterCampaignCompletions;
        }

        public static DailyLiveConfig ProductionDefault() =>
            new DailyLiveConfig(ProductionDefaultUnlockAfterCampaignCompletions);

        public static ContentResult<DailyLiveConfig> Parse(byte[] bytes)
        {
            try
            {
                if (bytes == null)
                    return ContentResult<DailyLiveConfig>.Failure(
                        ContentErrorKind.MalformedJson, "null daily_live payload");
                var token = ContentJson.LoadToken(Encoding.UTF8.GetString(bytes));
                if (!(token is JObject root))
                    return ContentResult<DailyLiveConfig>.Failure(
                        ContentErrorKind.MalformedJson, "daily_live root must be an object");
                if (root.Count != 2 || root["schemaVersion"] == null
                    || root["unlockAfterCampaignCompletions"] == null)
                    return ContentResult<DailyLiveConfig>.Failure(
                        ContentErrorKind.MissingField,
                        "daily_live must contain exactly schemaVersion and unlockAfterCampaignCompletions");
                if (root["schemaVersion"].Type != JTokenType.Integer
                    || (int)root["schemaVersion"] != 1)
                    return ContentResult<DailyLiveConfig>.Failure(
                        ContentErrorKind.SchemaVersionMismatch,
                        "daily_live schemaVersion must be 1");
                var threshold = root["unlockAfterCampaignCompletions"];
                if (threshold.Type != JTokenType.Integer)
                    return ContentResult<DailyLiveConfig>.Failure(
                        ContentErrorKind.IntegerExpected,
                        "unlockAfterCampaignCompletions must be an integer");
                int value = (int)threshold;
                if (value < 0)
                    return ContentResult<DailyLiveConfig>.Failure(
                        ContentErrorKind.BoundViolation,
                        "unlockAfterCampaignCompletions must be >= 0");
                return ContentResult<DailyLiveConfig>.Success(new DailyLiveConfig(value));
            }
            catch (System.Exception ex)
            {
                return ContentResult<DailyLiveConfig>.Failure(
                    ContentErrorKind.MalformedJson, ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
