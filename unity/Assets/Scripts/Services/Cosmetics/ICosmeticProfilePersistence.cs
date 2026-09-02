namespace CatMetro.Services.Cosmetics
{
    public interface ICosmeticProfilePersistence
    {
        bool TryLoad(out CosmeticProfileSnapshot snapshot);
        bool TryReplace(CosmeticProfileSnapshot snapshot);
    }

    public sealed class InMemoryCosmeticProfilePersistence : ICosmeticProfilePersistence
    {
        private CosmeticProfileSnapshot _snapshot;

        public InMemoryCosmeticProfilePersistence(CosmeticProfileSnapshot initial)
        {
            _snapshot = initial ?? CosmeticProfileSnapshot.Empty;
        }

        public bool TryLoad(out CosmeticProfileSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }

        public bool TryReplace(CosmeticProfileSnapshot snapshot)
        {
            if (snapshot == null) return false;
            _snapshot = snapshot;
            return true;
        }
    }
}
