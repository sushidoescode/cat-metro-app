using Newtonsoft.Json.Linq;

namespace CatMetro.Content.Daily
{
    // config/daily_pipeline.json, parsed from bytes (the library opens nothing — the host reads
    // through the content-source seam, CM-C6 criterion 6). Three rows:
    //   DAILY_PREVALIDATION_DAYS — the horizon; a corpus number copied from PRD:727 / ADR-0009:35
    //     with the citation on the row (A-C6-1). Never hard-coded here or in the tests.
    //   SALT_MAX_K — the one analyst-authored number (A-C6-2); derivation on the row. The salt
    //     loop attempts k = 0..SALT_MAX_K inclusive, then reports exhaustion.
    //   PIPELINE_ANCHOR_DATE — the host's default --from (A-C6-7, liveops_spec.md:53), so the
    //     criterion-6 invocation needs zero clock reads. CI passes a real --from from outside.
    public sealed class DailyPipelineConfig
    {
        public readonly int PrevalidationDays;
        public readonly int SaltMaxK;
        public readonly string AnchorDateKey;

        public DailyPipelineConfig(int prevalidationDays, int saltMaxK, string anchorDateKey)
        {
            PrevalidationDays = prevalidationDays; SaltMaxK = saltMaxK; AnchorDateKey = anchorDateKey;
        }

        public static ContentResult<DailyPipelineConfig> Parse(byte[] bytes)
        {
            try
            {
                if (bytes == null)
                    return ContentResult<DailyPipelineConfig>.Failure(
                        ContentErrorKind.MalformedJson, "null config payload");
                var o = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));

                int ReqInt(string name, int min)
                {
                    var t = o[name];
                    if (t == null || t.Type != JTokenType.Integer)
                        throw new ConfigWalk(ContentErrorKind.MissingField,
                            name + " (integer) is required");
                    int v = (int)t;
                    if (v < min)
                        throw new ConfigWalk(ContentErrorKind.BoundViolation,
                            name + "=" + v + " must be >= " + min);
                    return v;
                }

                int days = ReqInt("DAILY_PREVALIDATION_DAYS", 1);
                int saltMaxK = ReqInt("SALT_MAX_K", 0);

                var anchor = o["PIPELINE_ANCHOR_DATE"];
                if (anchor == null || anchor.Type != JTokenType.String)
                    return ContentResult<DailyPipelineConfig>.Failure(ContentErrorKind.MissingField,
                        "PIPELINE_ANCHOR_DATE (string) is required — A-C6-7");
                string anchorKey = (string)anchor;
                if (!DateKeys.IsValid(anchorKey))
                    return ContentResult<DailyPipelineConfig>.Failure(ContentErrorKind.BoundViolation,
                        "PIPELINE_ANCHOR_DATE '" + anchorKey + "' is not a yyyy-MM-dd calendar date");

                return ContentResult<DailyPipelineConfig>.Success(
                    new DailyPipelineConfig(days, saltMaxK, anchorKey));
            }
            catch (ConfigWalk walk)
            {
                return ContentResult<DailyPipelineConfig>.Failure(walk.Kind, walk.Message);
            }
            catch (System.Exception ex)
            {
                return ContentResult<DailyPipelineConfig>.Failure(ContentErrorKind.MalformedJson,
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        private sealed class ConfigWalk : System.Exception
        {
            public readonly ContentErrorKind Kind;
            public ConfigWalk(ContentErrorKind kind, string detail) : base(detail) { Kind = kind; }
        }
    }
}
