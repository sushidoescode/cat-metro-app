using CatMetro.Services.Purchases;

namespace CatMetro.Services.Cosmetics
{
    public static class CosmeticRuntime
    {
        private static CosmeticProfileService _current;
        private static bool _ownsCurrent;
        private static long _publicationGeneration;

        public static CosmeticProfileService Current
        {
            get
            {
                if (_current != null) return _current;
                Publish(Degraded(), true);
                return _current;
            }
        }

        public static void Install(CosmeticProfileService service)
        {
            if (service == null) return;
            SubscribeToPurchasesOnce();
            var current = Current;
            bool ownsCurrent = _ownsCurrent;
            long generation = _publicationGeneration;
            var purchases = PurchaseRuntime.Current;
            bool identityInstall = ReferenceEquals(current, service);
            var binding = service.PreparePurchaseBinding(purchases);

            if (!ReferenceEquals(_current, current)
                || _ownsCurrent != ownsCurrent
                || _publicationGeneration != generation
                || !ReferenceEquals(PurchaseRuntime.Current, purchases))
            {
                service.CancelPurchaseBinding(binding);
                throw new System.InvalidOperationException(
                    "cosmetic runtime changed during installation");
            }

            bool effectiveChanged = service.CommitPurchaseBinding(binding);
            if (identityInstall)
            {
                service.NotifyPurchaseBindingChanged(effectiveChanged);
                return;
            }

            if (ownsCurrent) current.Dispose();
            Publish(service, false);
            service.NotifyPurchaseBindingChanged(effectiveChanged);
        }

        public static void Uninstall(CosmeticProfileService expected)
        {
            if (!ReferenceEquals(_current, expected)) return;
            if (_ownsCurrent) _current.Dispose();
            Publish(Degraded(), true);
        }

        public static void ResetForTests()
        {
            PurchaseRuntime.Installed -= OnPurchasesInstalled;

            if (_ownsCurrent) _current?.Dispose();
            Publish(Degraded(), true);
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

        private static void Publish(CosmeticProfileService service, bool ownsCurrent)
        {
            _current = service;
            _ownsCurrent = ownsCurrent;
            unchecked
            {
                _publicationGeneration++;
            }
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
