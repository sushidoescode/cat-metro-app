using System;

namespace CatMetro.Services.Purchases
{
    // How Presentation finds the purchase service without an assembly cycle.
    //
    // CatMetro.Presentation references CatMetro.Services; CatMetro.Integrations references
    // CatMetro.Services. Neither references the other, so Integrations publishes the built
    // service here on boot and Presentation reads it. GameRoot is deliberately untouched — this
    // lane shares the composition root with six other lanes and editing it would be a merge
    // conflict for no benefit, since nothing about purchases needs to happen inside the game
    // session's tick loop.
    //
    // `Current` is never null. Before Integrations installs anything it is a fully degraded
    // service over an empty catalogue and a null backend, which answers every query safely.
    // Presentation therefore has no "is the store ready yet" branch to forget.
    public static class PurchaseRuntime
    {
        private static PurchaseService _current;

        public static PurchaseService Current => _current ??= Degraded();

        // True when a real service has been installed. Only diagnostics and the device
        // self-test should care; UI code must not gate on this, it should just ask IsUnlocked.
        public static bool IsInstalled { get; private set; }

        public static event Action Installed;

        public static void Install(PurchaseService service)
        {
            if (service == null) return;
            _current = service;
            IsInstalled = true;
            Installed?.Invoke();
        }

        // EditMode/PlayMode tests share a domain by default, so a test that installs a fake
        // service would otherwise leak into every test that runs after it.
        public static void ResetForTests()
        {
            _current = Degraded();
            IsInstalled = false;
            Installed = null;
        }

        private static PurchaseService Degraded()
            => new PurchaseService(PurchaseCatalog.Empty,
                new NullPurchaseBackend(BackendAvailability.NotCompiled,
                    "purchase runtime has not been installed"));
    }
}
