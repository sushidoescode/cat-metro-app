using CatMetro.Services.Purchases;

namespace CatMetro.Services.Cosmetics
{
    public static class CosmeticRuntime
    {
        private static CosmeticProfileService _current;
        private static bool _ownsCurrent;

        public static CosmeticProfileService Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Degraded();
                _ownsCurrent = true;
                return _current;
            }
        }

        public static void Install(CosmeticProfileService service)
        {
            if (service == null) return;
            if (ReferenceEquals(_current, service))
            {
                service.BindPurchases(PurchaseRuntime.Current);
                SubscribeToPurchasesOnce();
                return;
            }

            if (_ownsCurrent) _current?.Dispose();
            _current = service;
            _ownsCurrent = false;
            service.BindPurchases(PurchaseRuntime.Current);
            SubscribeToPurchasesOnce();
        }

        public static void Uninstall(CosmeticProfileService expected)
        {
            if (!ReferenceEquals(_current, expected)) return;
            if (_ownsCurrent) _current.Dispose();
            _current = Degraded();
            _ownsCurrent = true;
        }

        public static void ResetForTests()
        {
            PurchaseRuntime.Installed -= OnPurchasesInstalled;

            if (_ownsCurrent) _current?.Dispose();
            _current = Degraded();
            _ownsCurrent = true;
        }

        private static void SubscribeToPurchasesOnce()
        {
            PurchaseRuntime.Installed -= OnPurchasesInstalled;
            PurchaseRuntime.Installed += OnPurchasesInstalled;
        }

        private static void OnPurchasesInstalled()
        {
            Current.BindPurchases(PurchaseRuntime.Current);
        }

        private static CosmeticProfileService Degraded()
        {
            return new CosmeticProfileService(CosmeticCatalog.Empty,
                CosmeticAssetInventory.Empty,
                new InMemoryCosmeticProfilePersistence(CosmeticProfileSnapshot.Empty),
                PurchaseRuntime.Current);
        }
    }
}
