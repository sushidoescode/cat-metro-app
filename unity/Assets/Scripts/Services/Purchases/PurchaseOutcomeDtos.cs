namespace CatMetro.Services.Purchases
{
    public enum PurchaseOutcome
    {
        SuccessCandidate,
        UserCancelled,
        Failure,
        Restored,
        Pending,
        UnknownUnsettled,
        Unavailable,
        Busy
    }

    public readonly struct LocalizedPrice
    {
        public readonly string DisplayText;

        public LocalizedPrice(string displayText)
        {
            DisplayText = displayText;
        }
    }

    public readonly struct PurchaseResult
    {
        public readonly PurchaseOutcome Outcome;
        public readonly ProductIdentifier Product;
        public readonly LocalizedPrice LocalizedPrice;

        public PurchaseResult(PurchaseOutcome outcome, ProductIdentifier product,
            LocalizedPrice localizedPrice)
        {
            Outcome = outcome;
            Product = product;
            LocalizedPrice = localizedPrice;
        }
    }
}
