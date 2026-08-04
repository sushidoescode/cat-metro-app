using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // CM-C7 criteria 8-10 (Q-T: a DATA STRUCTURE, not a monetization surface — no SDK, no store
    // type, no purchase flow; the caller that will one day hold a store transaction does not
    // exist yet). TryGrant is the ONLY balance-raising path and its order is non-negotiable:
    // key -> dedupe check -> mutate -> ONE atomic write (dedupe insert + audit entry + balance)
    // -> only then return the grantable value. Grant QUANTITY is a caller parameter: mapping a
    // productId to an amount is an economy value (config/economy_defaults.json, CM-R04.1 human)
    // and is not invented here. grantedAtUtc likewise arrives from the caller's clock seam.
    public sealed class ConsumableLedger
    {
        public const string KEY_SCHEME = "cm-ledger-v1";

        private readonly SaveStore _store;
        private readonly RuntimeBounds _bounds;

        public ConsumableLedger(SaveStore store, RuntimeBounds bounds)
        {
            if (store == null || bounds == null)
                throw new System.ArgumentException("store and bounds are required");
            _store = store; _bounds = bounds;
        }

        // RK-19: domain-separated, product-scoped — 32 lowercase hex chars (first 16 bytes of
        // the SHA-256). The raw transaction id never persists and never leaves this method.
        public static string DedupeKey(string productId, string transactionId)
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(transactionId))
                throw new System.ArgumentException("productId and transactionId are required");
            string preimage = KEY_SCHEME + "|" + productId + "|" + transactionId;
            using (var sha = SHA256.Create())
            {
                byte[] d = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(preimage));
                var sb = new System.Text.StringBuilder(32);
                for (int i = 0; i < 16; i++)
                    sb.Append(d[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public int TryGrant(string transactionId, string productId, int quantity, long grantedAtUtc)
        {
            if (quantity < 1)
                throw new System.ArgumentException("quantity must be >= 1 (economy mapping is the caller's)");
            string key = DedupeKey(productId, transactionId);

            var payload = _store.State.Payload;
            var ledger = (JObject)payload["ledger"];
            var dedupe = (JArray)ledger["dedupe"];

            foreach (var existing in dedupe)
                if ((string)existing == key)
                    return 0; // duplicate: grants zero, forever (criterion 9)

            if (dedupe.Count >= _bounds.LedgerDedupeMaxEntries)
            {
                // RK-20: refuse, never trim — trimming re-opens double-granting.
                _store.Report("error_caught", "domain=ledger_capacity detail=dedupe at "
                    + dedupe.Count + " (LEDGER_DEDUPE_MAX_ENTRIES)");
                return 0;
            }

            // Clone -> mutate -> swap -> commit -> (on fault) restore: a fault before the write
            // durably lands produces neither the balance change nor a grantable event
            // (CM-R27.3/.4). All three mutations ride ONE atomic write.
            var original = payload;
            var mutated = (JObject)payload.DeepClone();
            var mLedger = (JObject)mutated["ledger"];
            ((JArray)mLedger["dedupe"]).Add(key);
            var audit = (JArray)mLedger["audit"];
            audit.Add(new JObject
            {
                ["txnHash"] = key,
                ["productId"] = productId,
                ["qty"] = quantity,
                ["grantedAtUtc"] = grantedAtUtc,
            });
            while (audit.Count > _bounds.LedgerAuditMaxEntries)
                audit.RemoveAt(0); // audit is FIFO-capped; the dedupe set NEVER is (criterion 10)
            var economy = (JObject)mutated["economy"];
            economy["rewindBalance"] = (int)economy["rewindBalance"] + quantity;

            _store.State.Payload = mutated;
            try
            {
                _store.CommitAtomic();
            }
            catch
            {
                _store.State.Payload = original;
                throw;
            }
            return quantity;
        }
    }
}
