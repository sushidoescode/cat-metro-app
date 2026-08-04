using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CatMetro.Content.Validation
{
    // config/validator_thresholds.json, parsed from bytes (the library opens nothing). The four
    // Q-R rows (lowerBoundSlack, starBandSlack, noveltyMinDistance, axisBBandCaps) are nullable
    // and DELIBERATELY absent from the shipped file: an absent row is the UNCONFIGURED verdict,
    // never a default value (stop condition 3 — no agent picks those numbers). jitterSampleCount
    // is an ordinary configured row (A-C5-2: a test-design number, not a product number).
    public sealed class BandCapsRow
    {
        public readonly int MaxComplexity;   // axis B basis: nodes+edges+switches cap for the band
        public readonly int PeakTrainsCap;   // axis E basis: 80-tick peak-spawn cap
        public readonly int MaxConcurrency;  // axis C basis
        public readonly int MaxHeadroom;     // axis H basis (queue slack)

        public BandCapsRow(int maxComplexity, int peakTrainsCap, int maxConcurrency, int maxHeadroom)
        {
            MaxComplexity = maxComplexity; PeakTrainsCap = peakTrainsCap;
            MaxConcurrency = maxConcurrency; MaxHeadroom = maxHeadroom;
        }
    }

    public sealed class ValidatorConfig
    {
        public readonly int JitterSampleCount;
        public readonly int? LowerBoundSlack;
        public readonly int? StarBandSlack;
        public readonly double? NoveltyMinDistance;
        public readonly IReadOnlyDictionary<string, BandCapsRow> AxisBBandCaps; // null == row absent

        public ValidatorConfig(int jitterSampleCount, int? lowerBoundSlack, int? starBandSlack,
            double? noveltyMinDistance, IReadOnlyDictionary<string, BandCapsRow> axisBBandCaps)
        {
            JitterSampleCount = jitterSampleCount;
            LowerBoundSlack = lowerBoundSlack;
            StarBandSlack = starBandSlack;
            NoveltyMinDistance = noveltyMinDistance;
            AxisBBandCaps = axisBBandCaps;
        }

        public static ContentResult<ValidatorConfig> Parse(byte[] bytes)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }
}
