using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using CatMetro.Services;

namespace CatMetro.Application.Analytics
{
    // One persisted queue record. Id derivation (A-C8-2/A-C8-6, recorded here as the contract
    // requires): lowercase-hex(first 8 bytes of SHA-256("cm-queue-v1|" + ordinal + "|" +
    // canonical event JSON)) — deterministic, reproducible in a test, generated at ENQUEUE and
    // persisted with the record, so a reload after process death re-flushes the SAME ids.
    public sealed class QueuedAnalyticsEvent
    {
        public readonly string Id;
        public readonly long Ord;
        public readonly string Name;
        public readonly JObject Params;
        public readonly int Bytes; // the persisted record's UTF-8 length (A-C8-9)

        public QueuedAnalyticsEvent(string id, long ord, string name, JObject parameters, int bytes)
        {
            Id = id; Ord = ord; Name = name; Params = parameters; Bytes = bytes;
        }
    }

    // A recorded note (queue_dropped counts, user-property sets) — the CM-C7 event-record
    // pattern (A-C8-8): the typed taxonomy contract turns these into real events later;
    // nothing is silently dropped meanwhile.
    public sealed class QueueNote
    {
        public readonly string Name;
        public readonly string Detail;
        public QueueNote(string name, string detail) { Name = name; Detail = detail ?? ""; }
    }

    // CM-C8: bounded, ordered, crash-safe, lossy-but-VISIBLE, metrics-only offline queue.
    // analytics_queue.dat sits beside save.dat, wears the same 16-byte header (magic "CMQU")
    // and rides the same temp+replace write helper — and is NEVER written in the same operation
    // as the save file (ADR-0006:280: a crash in the gap loses the EVENT, never the GRANT).
    public sealed class AnalyticsQueue : IAnalytics
    {
        public const string MAGIC = "CMQU";
        public const ushort QUEUE_VERSION = 1;

        public AnalyticsQueue(IStorageRoot root, Save.ISaveFileSystem fs,
            Save.RuntimeBounds bounds, IAnalyticsTransport transport)
        {
            throw new System.NotImplementedException();
        }

        public string QueuePath => throw new System.NotImplementedException();
        public int QueuedEventCount => throw new System.NotImplementedException();
        public IReadOnlyList<QueueNote> Notes => throw new System.NotImplementedException();
        public IReadOnlyList<QueuedAnalyticsEvent> Snapshot() => throw new System.NotImplementedException();

        // Rebuilds the in-memory queue from disk; a corrupt file (bad magic/length/CRC/JSON)
        // restarts EMPTY and records queue_dropped with count=unknown(corrupt) (A-C8-7).
        public void LoadFromDisk() => throw new System.NotImplementedException();

        public void Log(in AnalyticsEvent e) => throw new System.NotImplementedException();

        public void SetUserProperty(UserPropertyKey key, string value) =>
            throw new System.NotImplementedException();

        // Criterion 6: flush fires on EXACTLY the four configured triggers (plus the high-water
        // threshold, which routes through here) — and on no timer; no time source exists in
        // this type at all, which is what makes the negative test decidable.
        public void OnTrigger(string trigger) => throw new System.NotImplementedException();
    }
}
