using System.Collections.Generic;

namespace CatMetro.Application.Analytics
{
    public readonly struct AnalyticsDeliveryResult
    {
        public readonly bool ServerAccepted;

        public AnalyticsDeliveryResult(bool serverAccepted)
        {
            ServerAccepted = serverAccepted;
        }
    }

    // The queue retains ownership until completed reports an ingestion-server acknowledgement.
    // TryDeliver returning false means that no request started and completed will not be called.
    // A started attempt calls completed exactly once, including on abort or disposal.
    public interface IAnalyticsTransport
    {
        int MaxBatchSize { get; }
        bool TryDeliver(IReadOnlyList<QueuedAnalyticsEvent> batch,
            System.Action<AnalyticsDeliveryResult> completed);
    }
}
