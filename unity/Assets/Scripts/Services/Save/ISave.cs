using Newtonsoft.Json.Linq;

namespace CatMetro.Services
{
    // ADR-0003: ISave is DECLARED here, implemented in CatMetro.Application, rooted by an
    // IStorageRoot implemented in CatMetro.Bootstrap (Q-G; not in this queue). Signature per
    // docs/architecture/overview.md:224-232. The state is mutable in memory and durable only on
    // commit; nothing on the load path throws (ADR-0006 §1).
    public enum LoadResult
    {
        Ok = 1,
        RecoveredFromBackup = 2,
        Fresh = 3,
        RefusedDowngrade = 4,
    }

    // The mutable in-memory save: the payload JObject IS the state (unknown keys round-trip
    // untouched — ADR-0006:72 "a migration step never deletes a key it does not understand";
    // CM-C7 criterion 3), with typed views only where the ledger needs arithmetic.
    // INVARIANT (review F6): a successful grant swaps Payload to a mutated clone by identity —
    // callers must NOT hold JToken subtree references across a commit; re-read from Payload
    // after every commit. The recut of this boundary (field vs guarded setter) rides the
    // A-C7-6 human ratification.
    public sealed class SaveState
    {
        public JObject Payload;

        public SaveState(JObject payload) { Payload = payload; }

        public int Tickets
        {
            get => (int?)Payload?["economy"]?["tickets"] ?? 0;
            set => Payload["economy"]["tickets"] = value;
        }

        public int RewindBalance
        {
            get => (int?)Payload?["economy"]?["rewindBalance"] ?? 0;
            set => Payload["economy"]["rewindBalance"] = value;
        }
    }

    public interface ISave
    {
        SaveState State { get; }
        LoadResult Load();
        void CommitAtomic();
        bool TryCommitWithin(int budgetMs);
        int LastCommittedBytes { get; }
    }
}
