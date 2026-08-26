using System;
using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    public enum BackendAvailability
    {
        // No SDK compiled in, or running somewhere the SDK cannot work (the Unity Editor, per
        // RevenueCat's own docs). Permanent for this process — do not retry.
        NotCompiled,

        // SDK present but not configured: no API key supplied. Also permanent for this process.
        NotConfigured,

        // Configured, first offerings fetch not finished yet.
        Initializing,

        // Working.
        Ready,

        // Configured and was working, but the last call failed — usually no network. TRANSIENT:
        // callers should keep offering the shop and retry, not disable it forever.
        Unreachable
    }

    // The seam between the game and RevenueCat.
    //
    // Everything above this interface is pure C# in the engine-free CatMetro.Services assembly
    // and is fully testable in EditMode. Everything below it is the SDK, lives in
    // CatMetro.Integrations, and can only be exercised on a device — which is why the seam sits
    // exactly here and is this narrow. ADR-0003 already reserved this shape: "SDK types live
    // only in Integrations.*".
    //
    // Contract every implementation must honour, because the whole graceful-degradation story
    // rests on it:
    //   * No method throws. Ever. Failure is reported through the callback's result value.
    //   * Every callback is invoked exactly once, including on the failure paths.
    //   * A null callback is legal and ignored.
    //   * Callbacks arrive on the Unity main thread (the SDK guarantees this; a fake must too,
    //     and the tests rely on synchronous delivery).
    public interface IPurchaseBackend
    {
        BackendAvailability Availability { get; }

        // Products the store will actually sell, joined against our catalogue by the caller.
        // An empty list is a legitimate answer and means "the shop is empty right now".
        void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone);

        void Purchase(string productId, Action<PurchaseResult> onDone);

        void Restore(Action<RestoreResult> onDone);

        // Re-read entitlements without a purchase. Called on launch and on foreground.
        void RefreshEntitlements(Action<EntitlementSnapshot> onDone);
    }

    // Optional lifecycle signal for backends whose native SDK configures after attachment.
    // PurchaseService owns the reaction so every integration gets the same first offerings and
    // entitlement refresh, and so a replaced backend cannot update the active shop later.
    public interface IPurchaseBackendReadiness
    {
        event Action Ready;
    }

    // Optional push path for authoritative CustomerInfo returned by a PURCHASE OR RESTORE after
    // that transaction's local callback window. Ordinary refresh responses must never use this
    // event: they may have been requested before a newer purchase and retain their request epoch.
    public interface IPurchaseBackendTransactionUpdates
    {
        event Action<EntitlementSnapshot> TransactionEntitlementsConfirmed;
    }

    // The backend used when there is no store to talk to: no SDK compiled in, running in the
    // Editor, or no API key configured. Answers everything immediately and safely, which is what
    // makes "the game must never hard-fail because a purchase system is unreachable" true by
    // construction rather than by remembering to null-check at every call site.
    //
    // This is not a test double — it ships, and it is what runs in the Editor for all 1250
    // existing tests and for every play-in-editor session.
    public sealed class NullPurchaseBackend : IPurchaseBackend
    {
        private readonly string _why;

        public BackendAvailability Availability { get; }

        public NullPurchaseBackend(BackendAvailability availability = BackendAvailability.NotCompiled,
            string why = "purchases are not available in this build")
        {
            Availability = availability;
            _why = why;
        }

        public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
            => onDone?.Invoke(Array.Empty<StoreProductView>());

        public void Purchase(string productId, Action<PurchaseResult> onDone)
            => onDone?.Invoke(PurchaseResult.Unavailable(productId, _why));

        public void Restore(Action<RestoreResult> onDone)
            => onDone?.Invoke(new RestoreResult(RestoreOutcome.Unavailable, 0, _why));

        // Deliberately NOT authoritative. An unreachable backend reporting "you own nothing,
        // authoritatively" would make the ledger revoke every paid entitlement the moment the
        // game launched in the Editor or offline. This single flag is the difference between
        // degrading gracefully and destroying the player's purchases.
        public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
            => onDone?.Invoke(EntitlementSnapshot.Unreachable());
    }
}
