using System.Collections.Generic;
namespace CatMetro.Application.Save
{
    public sealed class RuntimeBounds
    {
        public readonly int SaveMaxBytes; public readonly int SavePauseBudgetMs;
        public readonly int LedgerDedupeMaxEntries; public readonly int LedgerAuditMaxEntries;
        public readonly string LedgerKeyScheme;
        public readonly int QueueMaxEvents; public readonly int QueueMaxBytes;
        public readonly int QueueEventMaxBytes; public readonly int QueueFlushHighWater;
        public readonly IReadOnlyList<string> QueueFlushTrigger;
        public readonly int AttributionMaxResims;
        public readonly int ContentMaxFileBytes; public readonly int ContentMaxJsonDepth;
        public readonly string ContentBoundsProfile;
        public readonly IReadOnlyList<string> Keys;
        public static CatMetro.Content.ContentResult<RuntimeBounds> Parse(byte[] bytes) => throw new System.NotImplementedException();
    }
}
