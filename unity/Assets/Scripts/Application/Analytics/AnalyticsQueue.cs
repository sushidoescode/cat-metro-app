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
    // analytics_queue.dat sits beside the save file, wears the same 16-byte header ("CMQU")
    // and rides the same temp+replace write helper — and is NEVER written in the same operation
    // as the save file (ADR-0006:280: a crash in the gap loses the EVENT, never the GRANT).
    // THREAD AFFINITY (recorded): single-threaded by contract — the future adapter/composition
    // root owns marshalling; no lock exists here by design.
    public sealed class AnalyticsQueue : IAnalytics
    {
        public const string MAGIC = "CMQU";
        public const ushort QUEUE_VERSION = 1;
        private const string ID_DOMAIN = "cm-queue-v1";

        private readonly IStorageRoot _root;
        private readonly Save.ISaveFileSystem _fs;
        private readonly Save.RuntimeBounds _bounds;
        private readonly IAnalyticsTransport _transport;
        private readonly List<QueuedAnalyticsEvent> _events = new List<QueuedAnalyticsEvent>();
        private readonly List<QueueNote> _notes = new List<QueueNote>();
        private const int NOTES_FIFO_CAP = 200; // review S4: bounded like CM-C7's audit list
        private long _nextOrdinal;
        private int _totalBytes;

        public AnalyticsQueue(IStorageRoot root, Save.ISaveFileSystem fs,
            Save.RuntimeBounds bounds, IAnalyticsTransport transport)
        {
            if (root == null || fs == null || bounds == null)
                throw new System.ArgumentException("root, fs and bounds are required");
            _root = root; _fs = fs; _bounds = bounds; _transport = transport;
            // Review NIT4: load eagerly — a Log() before an explicit load would persist a
            // one-element array OVER the previous session's queue (silent total loss).
            LoadFromDisk();
        }

        public string QueuePath => System.IO.Path.Combine(_root.SaveDirectory, "analytics_queue.dat");
        private string TmpPath => QueuePath + ".tmp";
        private string BakPath => QueuePath + ".bak";

        public int QueuedEventCount => _events.Count;
        public IReadOnlyList<QueueNote> Notes => _notes;

        // Records are shared READ-ONLY views (review B2): Params is the queue's internal
        // clone; consumers (tests, the transport seam) must not mutate it.
        public IReadOnlyList<QueuedAnalyticsEvent> Snapshot() => _events.ToArray();

        // Rebuilds the in-memory queue from disk; a corrupt file (bad magic/length/CRC/JSON)
        // restarts EMPTY and records queue_dropped with count=unknown(corrupt) — an exact count
        // is unknowable from corrupt bytes (A-C8-7).
        public void LoadFromDisk()
        {
            _events.Clear();
            _totalBytes = 0;
            _nextOrdinal = 0;
            if (!_fs.Exists(QueuePath)) return;
            try
            {
                var header = Save.SaveHeader.TryParse(_fs.ReadAllBytes(QueuePath), MAGIC,
                    out var payload);
                if (header == null)
                {
                    Note("queue_dropped", "count=unknown(corrupt) — header/CRC reject, restart empty");
                    return;
                }
                var token = CatMetro.Content.ContentJson.LoadToken(
                    new System.Text.UTF8Encoding(false, true).GetString(payload));
                if (!(token is JArray arr))
                {
                    Note("queue_dropped", "count=unknown(corrupt) — payload not an array, restart empty");
                    return;
                }
                foreach (var t in arr)
                {
                    var o = (JObject)t;
                    var record = new QueuedAnalyticsEvent(
                        (string)o["id"], (long)o["ord"], (string)o["name"],
                        (JObject)o["params"], RecordBytes(o));
                    _events.Add(record);
                    _totalBytes += record.Bytes;
                    if (record.Ord >= _nextOrdinal) _nextOrdinal = record.Ord + 1;
                }
            }
            catch (System.Exception ex)
            {
                _events.Clear();
                _totalBytes = 0;
                _nextOrdinal = 0;
                Note("queue_dropped",
                    "count=unknown(corrupt) — " + ex.GetType().Name + ", restart empty");
            }
        }

        public void Log(in AnalyticsEvent e)
        {
            var record = MakeRecord(e);
            if (record.Bytes > _bounds.QueueEventMaxBytes)
            {
                // Criterion 5: an oversize single event never enters (ADR-0006:239-241).
                Note("queue_dropped",
                    "count=1 oversize event '" + record.Name + "' " + record.Bytes + " B > "
                    + _bounds.QueueEventMaxBytes);
                return;
            }

            _events.Add(record);
            _totalBytes += record.Bytes;

            // Criterion 4: drop OLDEST-first on either limb, and say so with an exact count.
            int dropped = 0;
            while (_events.Count > _bounds.QueueMaxEvents || _totalBytes > _bounds.QueueMaxBytes)
            {
                _totalBytes -= _events[0].Bytes;
                _events.RemoveAt(0);
                dropped++;
            }
            if (dropped > 0)
                Note("queue_dropped", "count=" + dropped + " oldest-first overflow");

            TryPersist();

            // Criterion 6: the high-water threshold routes through the trigger gate.
            if (_events.Count >= _bounds.QueueFlushHighWater)
                OnTrigger("high_water");
        }

        // A-C8-8: recorded, not silently ignored — the tag flow is RK-30's open question and
        // the taxonomy contract owns the real sink.
        public void SetUserProperty(UserPropertyKey key, string value) =>
            Note("user_property", key + "=" + (value ?? ""));

        private void Note(string name, string detail)
        {
            _notes.Add(new QueueNote(name, detail));
            while (_notes.Count > NOTES_FIFO_CAP) _notes.RemoveAt(0);
        }

        // Criterion 6: flush fires on EXACTLY the configured triggers — and on no timer; no
        // time source exists in this type, which is what makes the negative test decidable.
        public void OnTrigger(string trigger)
        {
            bool configured = false;
            foreach (var t in _bounds.QueueFlushTrigger)
                if (t == trigger) { configured = true; break; }
            if (!configured) return;
            Flush();
        }

        private bool _flushing;

        private void Flush()
        {
            if (_flushing) return; // review S10: a transport that logs during Deliver must not recurse
            if (_events.Count == 0 || _transport == null) return;
            _flushing = true;
            try { FlushCore(); } finally { _flushing = false; }
        }

        private void FlushCore()
        {
            var batch = _events.ToArray();
            // A-C8-3: true = acknowledged; false/exception = keep everything, same ids next time.
            bool acked;
            try
            {
                acked = _transport.Deliver(batch);
            }
            catch
            {
                return; // a detonating transport is an unavailable transport
            }
            if (!acked) return;
            _events.Clear();
            _totalBytes = 0;
            // A failed post-ack persist leaves the delivered batch on disk: the next boot
            // re-hands it with the SAME ids and the consumer dedupes — at-least-once by design.
            TryPersist();
        }

        // Review B1: an IO fault NEVER escapes Log()/OnTrigger() — ADR-0006 §5's posture is
        // lossy-but-VISIBLE (a full disk on the app-pause flush must not crash the lifecycle
        // path; that is the exact class CM-C7 review F4 closed for the save). The tail stays in
        // memory for the next persist attempt; the risk is a recorded note, not an exception.
        private void TryPersist()
        {
            try
            {
                Persist();
            }
            catch (System.Exception ex)
            {
                Note("queue_dropped", "count=" + _events.Count + " persist_failed("
                    + ex.GetType().Name + ") — tail at risk until the next successful persist");
            }
        }

        private QueuedAnalyticsEvent MakeRecord(in AnalyticsEvent e)
        {
            long ord = _nextOrdinal++;
            // Review B2: the caller's JObject is CLONED — storing the live reference let a
            // reused params buffer silently falsify already-queued records and break the
            // id<->payload reproducibility the dedupe design rests on.
            var parameters = e.Params == null ? new JObject() : (JObject)e.Params.DeepClone();
            string canonical = e.Name + "|" + parameters.ToString(Newtonsoft.Json.Formatting.None);
            string id = DeriveId(ord, canonical);
            var o = new JObject
            {
                ["id"] = id,
                ["ord"] = ord,
                ["name"] = e.Name ?? "",
                ["params"] = parameters,
            };
            return new QueuedAnalyticsEvent(id, ord, e.Name ?? "", parameters, RecordBytes(o));
        }

        private static int RecordBytes(JObject record) =>
            System.Text.Encoding.UTF8.GetByteCount(record.ToString(Newtonsoft.Json.Formatting.None));

        private static string DeriveId(long ord, string canonical)
        {
            string preimage = ID_DOMAIN + "|"
                + ord.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|" + canonical;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] d = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(preimage));
                var sb = new System.Text.StringBuilder(16);
                for (int i = 0; i < 8; i++)
                    sb.Append(d[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        // Criterion 1: the SAME header and the SAME temp+replace helper as the save — no second
        // write-path implementation exists (the wrapper greps that this file names no
        // FileStream of its own). A-C8-10: persisted on every enqueue and every acked flush.
        private void Persist()
        {
            var arr = new JArray();
            foreach (var r in _events)
                arr.Add(new JObject
                {
                    ["id"] = r.Id, ["ord"] = r.Ord, ["name"] = r.Name, ["params"] = r.Params,
                });
            var payload = new System.Text.UTF8Encoding(false).GetBytes(
                arr.ToString(Newtonsoft.Json.Formatting.None));
            // formatVersion 1 is the QUEUE header layout's own version, deliberately not
            // coupled to the save's (recorded decision — the two files evolve independently).
            var file = Save.SaveHeader.Write(MAGIC, 1, QUEUE_VERSION, payload);
            _fs.WriteTempDurable(TmpPath, file);
            _fs.Replace(TmpPath, QueuePath, BakPath);
            // Review S9: the replace-produced .bak is never read (reject-and-restart-empty is
            // the mandated recovery) and would silently double the on-disk footprint AND widen
            // criterion 9's backup-exclusion set to a third filename — delete it.
            if (_fs.Exists(BakPath)) _fs.Delete(BakPath);
        }
    }
}
