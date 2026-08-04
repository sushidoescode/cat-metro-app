using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // config/runtime_bounds.json, parsed from bytes (criterion 12; Q-T). The [ARCH] values are
    // COPIED from ADR-0006 §4, never chosen — the four drift tests couple every constant this
    // assembly uses to the file's rows, and the file's key set is asserted exactly (15 keys).
    public sealed class RuntimeBounds
    {
        public readonly int SaveMaxBytes;
        public readonly int SavePauseBudgetMs;
        public readonly int LedgerDedupeMaxEntries;
        public readonly int LedgerAuditMaxEntries;
        public readonly string LedgerKeyScheme;
        public readonly int QueueMaxEvents;
        public readonly int QueueMaxBytes;
        public readonly int QueueEventMaxBytes;
        public readonly int QueueFlushHighWater;
        public readonly IReadOnlyList<string> QueueFlushTrigger;
        public readonly int AttributionMaxResims;
        public readonly int ContentMaxFileBytes;
        public readonly int ContentMaxJsonDepth;
        public readonly string ContentBoundsProfile;
        public readonly IReadOnlyList<string> Keys; // the file's top-level key set, for drift test (a)

        private RuntimeBounds(JObject o, List<string> keys)
        {
            SaveMaxBytes = Req(o, "SAVE_MAX_BYTES");
            SavePauseBudgetMs = Req(o, "SAVE_PAUSE_BUDGET_MS");
            LedgerDedupeMaxEntries = Req(o, "LEDGER_DEDUPE_MAX_ENTRIES");
            LedgerAuditMaxEntries = Req(o, "LEDGER_AUDIT_MAX_ENTRIES");
            LedgerKeyScheme = ReqStr(o, "LEDGER_KEY_SCHEME");
            QueueMaxEvents = Req(o, "QUEUE_MAX_EVENTS");
            QueueMaxBytes = Req(o, "QUEUE_MAX_BYTES");
            QueueEventMaxBytes = Req(o, "QUEUE_EVENT_MAX_BYTES");
            QueueFlushHighWater = Req(o, "QUEUE_FLUSH_HIGH_WATER");
            var triggers = new List<string>();
            foreach (var t in (JArray)o["QUEUE_FLUSH_TRIGGER"]) triggers.Add((string)t);
            QueueFlushTrigger = triggers;
            AttributionMaxResims = Req(o, "ATTRIBUTION_MAX_RESIMS");
            ContentMaxFileBytes = Req(o, "CONTENT_MAX_FILE_BYTES");
            ContentMaxJsonDepth = Req(o, "CONTENT_MAX_JSON_DEPTH");
            ContentBoundsProfile = ReqStr(o, "CONTENT_BOUNDS_PROFILE");
            Keys = keys;
        }

        private static int Req(JObject o, string name)
        {
            var t = o[name];
            if (t == null || t.Type != JTokenType.Integer)
                throw new System.Exception(name + " (integer) is required — ADR-0006 §4");
            return (int)t;
        }

        private static string ReqStr(JObject o, string name)
        {
            var t = o[name];
            if (t == null || t.Type != JTokenType.String)
                throw new System.Exception(name + " (string) is required — ADR-0006 §4");
            return (string)t;
        }

        public static CatMetro.Content.ContentResult<RuntimeBounds> Parse(byte[] bytes)
        {
            try
            {
                if (bytes == null)
                    return CatMetro.Content.ContentResult<RuntimeBounds>.Failure(
                        CatMetro.Content.ContentErrorKind.MalformedJson, "null bounds payload");
                // Review F12: through the hardened one-truth loader (duplicate-key + depth
                // guards), not a raw parse — this file is the single source every bound reads.
                var token = CatMetro.Content.ContentJson.LoadToken(
                    System.Text.Encoding.UTF8.GetString(bytes));
                if (!(token is JObject o))
                    return CatMetro.Content.ContentResult<RuntimeBounds>.Failure(
                        CatMetro.Content.ContentErrorKind.MalformedJson, "bounds root must be an object");
                var schema = o["schemaVersion"];
                if (schema == null || schema.Type != JTokenType.Integer || (int)schema != 1)
                    return CatMetro.Content.ContentResult<RuntimeBounds>.Failure(
                        CatMetro.Content.ContentErrorKind.SchemaVersionMismatch,
                        "runtime_bounds schemaVersion must be 1");
                var keys = new List<string>();
                foreach (var p in o.Properties()) keys.Add(p.Name);
                return CatMetro.Content.ContentResult<RuntimeBounds>.Success(
                    new RuntimeBounds(o, keys));
            }
            catch (System.Exception ex)
            {
                return CatMetro.Content.ContentResult<RuntimeBounds>.Failure(
                    CatMetro.Content.ContentErrorKind.MalformedJson,
                    ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
