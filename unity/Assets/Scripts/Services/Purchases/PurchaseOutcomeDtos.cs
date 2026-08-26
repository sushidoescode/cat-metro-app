using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    public enum PurchaseOutcome
    {
        // The store says it went through. Named "SuccessCandidate" rather than "Success" by the
        // rescued foundation, and kept that way: on Android a purchase can be reported complete
        // before RevenueCat's entitlement refresh confirms it, so the honest name for this state
        // is "the store thinks so". Entitlements are only ever believed from CustomerInfo.
        SuccessCandidate,

        UserCancelled,
        Failure,

        // Deferred/pending payment — real on Google Play (slow card, parental approval). The
        // player has not been charged and owns nothing yet, and the app must not grant.
        Pending,

        // Store returned, but in a shape we cannot classify. Distinct from Failure so it shows
        // up separately in analytics instead of inflating the real failure rate.
        UnknownUnsettled,

        // The purchase never reached a store: no SDK, no network, unconfigured key, editor.
        // This is the graceful-degradation outcome and must never read as a failure to the
        // player — nothing went wrong, the shop simply is not reachable.
        Unavailable,

        // A purchase is already in flight. Guards the double-tap.
        Busy
    }

    public enum RestoreOutcome
    {
        Completed,
        Failure,
        Unavailable,
        Busy
    }

    // A price as the STORE formatted it, in the player's currency and locale. Never assembled
    // from a number and a symbol in our code: currency formatting is the store's job and getting
    // it wrong is both embarrassing and, in some jurisdictions, a compliance problem.
    public readonly struct LocalizedPrice
    {
        public readonly string DisplayText;

        // True when this came from a real store product rather than a placeholder. UI uses it to
        // decide between showing a price and showing nothing — never to show "$0.00".
        public bool IsKnown => !string.IsNullOrEmpty(DisplayText);

        public LocalizedPrice(string displayText)
        {
            DisplayText = displayText;
        }
    }

    // One purchasable thing as the store currently describes it: our catalogue entry joined to
    // live store pricing. Absent from the list entirely if the store does not offer it.
    public readonly struct StoreProductView
    {
        public readonly string ProductId;
        public readonly string DisplayName;
        public readonly LocalizedPrice Price;

        public StoreProductView(string productId, string displayName, LocalizedPrice price)
        {
            ProductId = productId;
            DisplayName = displayName;
            Price = price;
        }
    }

    public readonly struct PurchaseResult
    {
        public readonly PurchaseOutcome Outcome;
        public readonly string ProductId;
        public readonly LocalizedPrice LocalizedPrice;

        // Non-null only on Failure/UnknownUnsettled. For logs, never for display: store error
        // text is not written for players.
        public readonly string DiagnosticMessage;
        // RevenueCat returns authoritative CustomerInfo with a successful native purchase.
        // Carry it across the seam so the coat can paint on the return frame without waiting
        // for a redundant request. PurchaseService still applies it through the one ledger path.
        public readonly EntitlementSnapshot? ConfirmedEntitlements;

        public bool IsGrantable => Outcome == PurchaseOutcome.SuccessCandidate;

        public PurchaseResult(PurchaseOutcome outcome, string productId,
            LocalizedPrice localizedPrice = default, string diagnosticMessage = null,
            EntitlementSnapshot? confirmedEntitlements = null)
        {
            Outcome = outcome;
            ProductId = productId;
            LocalizedPrice = localizedPrice;
            DiagnosticMessage = diagnosticMessage;
            ConfirmedEntitlements = confirmedEntitlements;
        }

        public static PurchaseResult Unavailable(string productId, string why)
            => new PurchaseResult(PurchaseOutcome.Unavailable, productId, default, why);
    }

    public readonly struct RestoreResult
    {
        public readonly RestoreOutcome Outcome;

        // How many of OUR catalogue entitlements came back active. Zero with Completed is a
        // perfectly normal answer ("you have not bought anything") and the UI must say so
        // rather than implying an error.
        public readonly int RestoredEntitlementCount;
        public readonly string DiagnosticMessage;
        public readonly EntitlementSnapshot? ConfirmedEntitlements;

        public RestoreResult(RestoreOutcome outcome, int restoredEntitlementCount = 0,
            string diagnosticMessage = null, EntitlementSnapshot? confirmedEntitlements = null)
        {
            Outcome = outcome;
            RestoredEntitlementCount = restoredEntitlementCount;
            DiagnosticMessage = diagnosticMessage;
            ConfirmedEntitlements = confirmedEntitlements;
        }
    }

    // What the backend knows about the customer's entitlements right now. The ledger's Store
    // limb is replaced from exactly this.
    public readonly struct EntitlementSnapshot
    {
        public readonly bool IsAuthoritative;
        public readonly IReadOnlyList<EntitlementGrant> Grants;

        // IsAuthoritative false means "we could not reach RevenueCat" — offline, no SDK, error.
        // The ledger must NOT be replaced from a non-authoritative snapshot, or an offline
        // launch would revoke everything the player paid for.
        public EntitlementSnapshot(bool isAuthoritative, IReadOnlyList<EntitlementGrant> grants)
        {
            IsAuthoritative = isAuthoritative;
            Grants = grants;
        }

        public static EntitlementSnapshot Unreachable()
            => new EntitlementSnapshot(false, System.Array.Empty<EntitlementGrant>());
    }
}
