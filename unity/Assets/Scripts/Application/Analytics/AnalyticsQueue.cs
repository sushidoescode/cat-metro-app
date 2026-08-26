using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using CatMetro.Services;

namespace CatMetro.Application.Analytics
{
    // One persisted queue record. The random eight-byte lowercase-hex id is generated once at
    // enqueue and persisted, so retries after process death reuse it. Randomness avoids reusing
    // an earlier id after an acknowledged empty queue restarts its local ordinal at zero.
    public sealed class QueuedAnalyticsEvent
    {
        public readonly string Id;
        public readonly long Ord;
        public readonly string Name;
        public readonly JObject Params;
        public readonly long CapturedAtUnixMs;
        public readonly int Bytes; // the persisted record's UTF-8 length (A-C8-9)

        public QueuedAnalyticsEvent(string id, long ord, string name, JObject parameters,
            long capturedAtUnixMs, int bytes)
        {
            Id = id; Ord = ord; Name = name; Params = parameters;
            CapturedAtUnixMs = capturedAtUnixMs; Bytes = bytes;
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

    // CM-C8: bounded, ordered, atomically persisted, lossy-but-VISIBLE metrics-only queue.
    // analytics_queue.dat sits beside the save file, wears the same 16-byte header ("CMQU")
    // and rides the same temp+replace write helper — and is NEVER written in the same operation
    // as the save file (ADR-0006:280: a crash in the gap loses the EVENT, never the GRANT).
    // Event mutation and transport callbacks stay on the composition/main thread. The production
    // writer receives read-only record snapshot arrays on a worker; only writer state and notes cross
    // threads, and both are locked here.
    public sealed class AnalyticsQueue : IAnalytics
    {
        public const string MAGIC = "CMQU";
        public const ushort QUEUE_VERSION = 1;
        private readonly IStorageRoot _root;
        private readonly string _queuePath;
        private readonly string _tmpPath;
        private readonly string _bakPath;
        private readonly Save.ISaveFileSystem _fs;
        private readonly Save.RuntimeBounds _bounds;
        private readonly IAnalyticsTransport _transport;
        private readonly System.Func<long> _nowUnixMilliseconds;
        private readonly System.Func<string> _newEventId;
        private readonly IAnalyticsPersistenceExecutor _persistenceExecutor;
        private readonly string _ownerId;
        private readonly List<QueuedAnalyticsEvent> _events = new List<QueuedAnalyticsEvent>();
        private readonly List<QueueNote> _notes = new List<QueueNote>();
        private readonly object _notesGate = new object();
        private readonly object _persistenceGate = new object();
        private QueuedAnalyticsEvent[] _pendingPersistence;
        private long _pendingPersistenceRevision;
        private bool _persistenceWorkerRunning;
        private const int NOTES_FIFO_CAP = 200; // review S4: bounded like CM-C7's audit list
        private long _nextOrdinal;
        private int _totalBytes;
        private bool _disabled;
        private bool _deliveryInFlight;
        private long _deliveryGeneration;
        private bool _pumpRequested;
        private bool _deliveryRequested;
        private long _stateRevision;
        private long _persistedRevision;

        public AnalyticsQueue(IStorageRoot root, Save.ISaveFileSystem fs,
            Save.RuntimeBounds bounds, IAnalyticsTransport transport,
            System.Func<long> nowUnixMilliseconds = null,
            System.Func<string> newEventId = null,
            IAnalyticsPersistenceExecutor persistenceExecutor = null,
            string ownerId = null)
        {
            if (root == null || fs == null || bounds == null)
                throw new System.ArgumentException("root, fs and bounds are required");
            _root = root; _fs = fs; _bounds = bounds; _transport = transport;
            // Resolve the engine-backed storage root while construction is still on Unity's
            // main thread. The background writer touches only these ordinary managed strings.
            _queuePath = System.IO.Path.Combine(_root.SaveDirectory, "analytics_queue.dat");
            _tmpPath = _queuePath + ".tmp";
            _bakPath = _queuePath + ".bak";
            _nowUnixMilliseconds = nowUnixMilliseconds
                ?? (() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _newEventId = newEventId ?? GenerateRandomId;
            _persistenceExecutor = persistenceExecutor;
            if (ownerId != null && !AnalyticsInstallIdentity.IsValid(ownerId))
                throw new System.ArgumentException("owner id must be lowercase 32-hex");
            _ownerId = ownerId;
            // Review NIT4: load eagerly — a Log() before an explicit load would persist a
            // one-element array OVER the previous session's queue (silent total loss).
            LoadFromDisk();
        }

        public string QueuePath => _queuePath;
        private string TmpPath => _tmpPath;
        private string BakPath => _bakPath;

        public int QueuedEventCount => _events.Count;
        // The configured byte limb applies to the artifact that actually reaches disk: the
        // fixed header, JSON brackets/commas, and every serialized record body.
        public int PersistedArtifactBytes
        {
            get
            {
                long separators = _events.Count > 0 ? _events.Count - 1L : 0L;
                long bytes = Save.SaveHeader.SIZE + 2L + separators + _totalBytes;
                return bytes >= int.MaxValue ? int.MaxValue : (int)bytes;
            }
        }
        public IReadOnlyList<QueueNote> Notes
        {
            get { lock (_notesGate) return _notes.ToArray(); }
        }

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
                var fileBytes = _fs.ReadAllBytes(QueuePath);
                var header = Save.SaveHeader.TryParse(fileBytes, MAGIC, out var payload);
                if (header == null)
                {
                    Note("queue_dropped", "count=unknown(corrupt) — header/CRC reject, restart empty");
                    return;
                }
                if (header.FormatVersion != 1 || header.SaveVersion != QUEUE_VERSION)
                {
                    Note("queue_dropped", "count=unknown(corrupt) — unsupported version, restart empty");
                    return;
                }
                var token = CatMetro.Content.ContentJson.LoadToken(
                    new System.Text.UTF8Encoding(false, true).GetString(payload));
                if (!(token is JArray arr))
                {
                    Note("queue_dropped", "count=unknown(corrupt) — payload not an array, restart empty");
                    return;
                }
                if (_ownerId != null)
                {
                    foreach (var tokenRecord in arr)
                    {
                        if (!(tokenRecord is JObject ownerRecord)
                            || !(ownerRecord["ownerId"] is JValue ownerValue)
                            || ownerValue.Type != JTokenType.String
                            || (string)ownerValue != _ownerId)
                        {
                            Note("queue_dropped", "count=" + arr.Count
                                + " owner_mismatch — historical records discarded");
                            _stateRevision++;
                            // Recovery/privacy exception: overwrite the mismatched artifact now,
                            // before this instance can ever enable delivery under the new owner.
                            // A background-only clear would leave a process-kill window in which
                            // a later partial profile restore could re-admit the old records.
                            try
                            {
                                Persist(System.Array.Empty<QueuedAnalyticsEvent>());
                                _persistedRevision = _stateRevision;
                            }
                            catch (System.Exception ex)
                            {
                                NotePersistFailure(arr.Count, ex);
                            }
                            return;
                        }
                    }
                }
                int oversizeRestored = 0;
                bool canonicalRewriteRequired = false;
                foreach (var t in arr)
                {
                    var o = (JObject)t;
                    string id = (string)o["id"];
                    long ord = (long)o["ord"];
                    string name = (string)o["name"];
                    var parameters = (JObject)o["params"];
                    string recordOwner = (string)o["ownerId"];
                    long capturedAtUnixMs = (long?)o["capturedAtUnixMs"] ?? 0L;
                    var canonicalRecord = RecordObject(id, ord, name, parameters,
                        capturedAtUnixMs, recordOwner);
                    if (!JToken.DeepEquals(o, canonicalRecord))
                        canonicalRewriteRequired = true;
                    var record = new QueuedAnalyticsEvent(id, ord, name, parameters,
                        capturedAtUnixMs, RecordBytes(canonicalRecord));
                    if (record.Ord >= _nextOrdinal) _nextOrdinal = record.Ord + 1;
                    if (record.Bytes > _bounds.QueueEventMaxBytes)
                    {
                        oversizeRestored++;
                        continue;
                    }
                    if (_totalBytes > int.MaxValue - record.Bytes)
                        throw new System.OverflowException("restored queue byte count overflow");
                    _events.Add(record);
                    _totalBytes += record.Bytes;
                }
                if (oversizeRestored > 0)
                    Note("queue_dropped", "count=" + oversizeRestored
                        + " oversize restored record");
                int overflowRestored = 0;
                while (_events.Count > _bounds.QueueMaxEvents
                    || PersistedArtifactBytes > _bounds.QueueMaxBytes)
                {
                    _totalBytes -= _events[0].Bytes;
                    _events.RemoveAt(0);
                    overflowRestored++;
                }
                if (overflowRestored > 0)
                    Note("queue_dropped", "count=" + overflowRestored
                        + " oldest-first restored overflow");
                if (fileBytes.Length != PersistedArtifactBytes)
                    canonicalRewriteRequired = true;
                if (canonicalRewriteRequired)
                    Note("queue_repaired", "noncanonical restored artifact rewritten");
                if (oversizeRestored > 0 || overflowRestored > 0
                    || canonicalRewriteRequired)
                    PersistLoadedRepair();
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

        private void PersistLoadedRepair()
        {
            _stateRevision++;
            try
            {
                Persist(_events.ToArray());
                _persistedRevision = _stateRevision;
            }
            catch (System.Exception ex)
            {
                NotePersistFailure(_events.Count, ex);
            }
        }

        public void Log(in AnalyticsEvent e)
        {
            if (_disabled) return;
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
            while (_events.Count > _bounds.QueueMaxEvents
                || PersistedArtifactBytes > _bounds.QueueMaxBytes)
            {
                _totalBytes -= _events[0].Bytes;
                _events.RemoveAt(0);
                dropped++;
            }
            if (dropped > 0)
                Note("queue_dropped", "count=" + dropped + " oldest-first overflow");

            _stateRevision++;
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
            lock (_notesGate)
            {
                _notes.Add(new QueueNote(name, detail));
                while (_notes.Count > NOTES_FIFO_CAP) _notes.RemoveAt(0);
            }
        }

        // Criterion 6: flush fires on EXACTLY the configured triggers — and on no timer; no
        // time source exists in this type, which is what makes the negative test decidable.
        public void OnTrigger(string trigger)
        {
            if (_disabled) return;
            bool configured = false;
            foreach (var t in _bounds.QueueFlushTrigger)
                if (t == trigger) { configured = true; break; }
            if (!configured) return;
            // A previous background write may have failed. Every real lifecycle/connectivity
            // trigger retries the newest snapshot before attempting network delivery.
            TryPersist();
            _deliveryRequested = true;
            Flush();
        }

        // A configured trigger may arrive while the production writer is still committing the
        // corresponding snapshot. GameRoot calls this on its ordinary update path so delivery
        // can continue once that already-requested state is durable; this creates no new trigger.
        public void ContinuePendingDelivery()
        {
            if (_disabled || !_deliveryRequested) return;
            Flush();
        }

        private bool _flushing;

        private void Flush()
        {
            if (_flushing)
            {
                _pumpRequested = true;
                return;
            }
            _flushing = true;
            try
            {
                do
                {
                    _pumpRequested = false;
                    if (!_deliveryRequested || _disabled || _deliveryInFlight
                        || _events.Count == 0 || _transport == null
                        || !IsCurrentStatePersisted())
                        break;
                    FlushCore();
                }
                while (_pumpRequested && !_deliveryInFlight);
            }
            finally { _flushing = false; }
        }

        private void FlushCore()
        {
            int maxBatch;
            try { maxBatch = System.Math.Max(1, _transport.MaxBatchSize); }
            catch { return; }
            int count = System.Math.Min(maxBatch, _events.Count);
            var batch = _events.GetRange(0, count).ToArray();
            long generation = ++_deliveryGeneration;
            _deliveryInFlight = true;
            _deliveryRequested = false;
            bool started;
            try
            {
                started = _transport.TryDeliver(batch,
                    result => OnDeliveryCompleted(generation, batch, result));
            }
            catch
            {
                started = false;
            }
            if (!started && generation == _deliveryGeneration)
                _deliveryInFlight = false;
        }

        private void OnDeliveryCompleted(long generation, QueuedAnalyticsEvent[] attempted,
            AnalyticsDeliveryResult result)
        {
            if (generation != _deliveryGeneration) return;
            _deliveryInFlight = false;
            if (!result.ServerAccepted || _disabled) return;

            // Remove only records from this exact attempt. Overflow may have evicted an
            // in-flight prefix while the request was pending, so match id + ordinal instead of
            // assuming the current head is unchanged.
            int removed = 0;
            foreach (var sent in attempted)
            {
                int index = _events.FindIndex(item => item.Id == sent.Id && item.Ord == sent.Ord
                    && item.CapturedAtUnixMs == sent.CapturedAtUnixMs);
                if (index < 0) continue;
                _totalBytes -= _events[index].Bytes;
                _events.RemoveAt(index);
                removed++;
            }
            if (removed > 0) _stateRevision++;
            TryPersist();
            if (_events.Count > 0)
            {
                _deliveryRequested = true;
                _pumpRequested = true;
                Flush();
            }
        }

        // Review B1: an IO fault NEVER escapes Log()/OnTrigger() — ADR-0006 §5's posture is
        // lossy-but-VISIBLE (a full disk on the app-pause flush must not crash the lifecycle
        // path; that is the exact class CM-C7 review F4 closed for the save). The tail stays in
        // memory for the next persist attempt; the risk is a recorded note, not an exception.
        private void TryPersist()
        {
            var snapshot = _events.ToArray();
            long revision = _stateRevision;
            bool dispatch = false;
            lock (_persistenceGate)
            {
                _pendingPersistence = snapshot;
                _pendingPersistenceRevision = revision;
                if (!_persistenceWorkerRunning)
                {
                    _persistenceWorkerRunning = true;
                    dispatch = true;
                }
            }
            if (!dispatch) return;
            try
            {
                if (_persistenceExecutor == null) PersistenceLoop();
                else _persistenceExecutor.Dispatch(PersistenceLoop);
            }
            catch (System.Exception ex)
            {
                lock (_persistenceGate) _persistenceWorkerRunning = false;
                NotePersistFailure(snapshot.Length, ex);
            }
        }

        private void PersistenceLoop()
        {
            while (true)
            {
                QueuedAnalyticsEvent[] snapshot;
                long revision;
                lock (_persistenceGate)
                {
                    snapshot = _pendingPersistence;
                    revision = _pendingPersistenceRevision;
                    _pendingPersistence = null;
                }
                try
                {
                    Persist(snapshot ?? System.Array.Empty<QueuedAnalyticsEvent>());
                }
                catch (System.Exception ex)
                {
                    lock (_persistenceGate)
                    {
                        // Retain the newest requested state. If nothing newer arrived, retain
                        // the failed snapshot for the next enqueue/trigger to retry.
                        if (_pendingPersistence == null)
                        {
                            _pendingPersistence = snapshot;
                            _pendingPersistenceRevision = revision;
                        }
                        _persistenceWorkerRunning = false;
                    }
                    NotePersistFailure(snapshot?.Length ?? 0, ex);
                    return;
                }

                lock (_persistenceGate)
                {
                    if (revision > _persistedRevision) _persistedRevision = revision;
                    if (_pendingPersistence != null) continue;
                    _persistenceWorkerRunning = false;
                    return;
                }
            }
        }

        private void NotePersistFailure(int atRiskCount, System.Exception ex) =>
            Note("queue_dropped", "count=" + atRiskCount + " persist_failed("
                + ex.GetType().Name + ") — tail at risk until the next successful persist");

        private bool IsCurrentStatePersisted()
        {
            lock (_persistenceGate) return _persistedRevision >= _stateRevision;
        }

        public bool TryDrainPersistence(int budgetMilliseconds)
        {
            if (_persistenceExecutor == null)
            {
                lock (_persistenceGate)
                    return !_persistenceWorkerRunning && _pendingPersistence == null;
            }
            bool executorDrained;
            try { executorDrained = _persistenceExecutor.TryDrain(
                System.Math.Max(0, budgetMilliseconds)); }
            catch { return false; }
            lock (_persistenceGate)
                return executorDrained && !_persistenceWorkerRunning
                    && _pendingPersistence == null;
        }

        private QueuedAnalyticsEvent MakeRecord(in AnalyticsEvent e)
        {
            long ord = _nextOrdinal++;
            long capturedAtUnixMs = _nowUnixMilliseconds();
            // Review B2: the caller's JObject is CLONED — storing the live reference let a
            // reused params buffer silently falsify already-queued records after capture.
            var parameters = e.Params == null ? new JObject() : (JObject)e.Params.DeepClone();
            string id = NextUniqueId();
            var o = RecordObject(id, ord, e.Name ?? "", parameters, capturedAtUnixMs,
                _ownerId);
            return new QueuedAnalyticsEvent(id, ord, e.Name ?? "", parameters,
                capturedAtUnixMs, RecordBytes(o));
        }

        private static JObject RecordObject(string id, long ord, string name, JObject parameters,
            long capturedAtUnixMs, string ownerId)
        {
            var record = new JObject
            {
                ["id"] = id,
                ["ord"] = ord,
                ["name"] = name ?? "",
                ["params"] = parameters,
                ["capturedAtUnixMs"] = capturedAtUnixMs,
            };
            if (ownerId != null) record["ownerId"] = ownerId;
            return record;
        }

        private static int RecordBytes(JObject record) =>
            System.Text.Encoding.UTF8.GetByteCount(record.ToString(Newtonsoft.Json.Formatting.None));

        private string NextUniqueId()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string candidate = null;
                try { candidate = _newEventId(); } catch { }
                if (!IsEventId(candidate)) continue;
                if (!_events.Exists(item => item.Id == candidate)) return candidate;
            }
            return GenerateRandomId();
        }

        private static bool IsEventId(string value)
        {
            if (value == null || value.Length != 16) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private static string GenerateRandomId()
        {
            var bytes = new byte[8];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            var value = new System.Text.StringBuilder(16);
            foreach (byte b in bytes)
                value.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return value.ToString();
        }

        // Criterion 1: the SAME header and the SAME temp+replace helper as the save — no second
        // write-path implementation exists. A-C8-10: snapshots are persisted after enqueue and
        // after every acknowledged flush.
        private void Persist(IReadOnlyList<QueuedAnalyticsEvent> snapshot)
        {
            var arr = new JArray();
            foreach (var r in snapshot)
                arr.Add(RecordObject(r.Id, r.Ord, r.Name, r.Params, r.CapturedAtUnixMs,
                    _ownerId));
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

        public void DisableAndDiscard(string reason)
        {
            _disabled = true;
            _deliveryGeneration++;
            _deliveryInFlight = false;
            _deliveryRequested = false;
            int dropped = _events.Count;
            _events.Clear();
            _totalBytes = 0;
            _stateRevision++;
            Note("queue_dropped", "count=" + dropped + " " + (reason ?? "disabled"));
            TryPersist();
        }

        public void Enable() => _disabled = false;
    }
}
