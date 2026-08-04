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
            throw new System.NotImplementedException();
        }
    }
}
