namespace CatMetro.Application.Save
{
    public sealed class ConsumableLedger
    {
        public const string KEY_SCHEME = "cm-ledger-v1";
        public ConsumableLedger(SaveStore store, RuntimeBounds bounds) => throw new System.NotImplementedException();
        public static string DedupeKey(string productId, string transactionId) => throw new System.NotImplementedException();
        public int TryGrant(string transactionId, string productId, int quantity, long grantedAtUtc) => throw new System.NotImplementedException();
    }
}
