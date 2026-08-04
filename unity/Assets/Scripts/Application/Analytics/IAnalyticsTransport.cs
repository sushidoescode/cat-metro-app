using System.Collections.Generic;

namespace CatMetro.Application.Analytics
{
    // A-C8-3: the "transport" is an injected seam — no real transport exists in this contract.
    // "Flush" means hand the ordered batch to this seam; true = acknowledged (the queue may
    // clear the batch), false = unavailable or ack lost (the queue keeps everything, and the
    // NEXT flush re-hands the SAME records with the SAME idempotency ids, which is what lets
    // the consumer dedupe instead of inflating counts — CM-R43.4(a)).
    public interface IAnalyticsTransport
    {
        bool Deliver(IReadOnlyList<QueuedAnalyticsEvent> batch);
    }
}
