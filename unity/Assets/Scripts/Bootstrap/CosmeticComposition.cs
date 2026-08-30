using CatMetro.Application.Save;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using UnityEngine;

namespace CatMetro.Bootstrap
{
    public static class CosmeticComposition
    {
        public static CosmeticProfileService Create(SaveStore saveStore,
            PurchaseService purchases)
        {
            CosmeticProfileService service = null;
            try
            {
                var portraitAssets = Resources.Load<TextAsset>("Cosmetics/portrait_assets");
                var cosmeticCatalog = Resources.Load<TextAsset>("Cosmetics/cosmetic_catalog");
                var inventory = CosmeticAssetInventory.Parse(
                    portraitAssets != null ? portraitAssets.text : null,
                    CosmeticPortraitPainter.SupportedRendererTokens);
                var catalog = CosmeticCatalog.Parse(
                    cosmeticCatalog != null ? cosmeticCatalog.text : null,
                    inventory.AssetIds, inventory.ProvenanceAssetIds);

                ICosmeticProfilePersistence persistence;
                if (saveStore != null)
                    persistence = new SaveStoreCosmeticProfilePersistence(saveStore);
                else
                    persistence = new InMemoryCosmeticProfilePersistence(
                        CosmeticProfileSnapshot.Empty);

                var resolvedPurchases = purchases ?? PurchaseRuntime.Current;
                service = new CosmeticProfileService(catalog, inventory, persistence,
                    resolvedPurchases);
                LogDiagnostic(service, inventory, resolvedPurchases);
                return service;
            }
            catch (System.Exception)
            {
                service?.Dispose();
                var degraded = new CosmeticProfileService(CosmeticCatalog.Empty,
                    CosmeticAssetInventory.Empty,
                    new InMemoryCosmeticProfilePersistence(CosmeticProfileSnapshot.Empty),
                    purchases ?? PurchaseRuntime.Current);
                Debug.Log(CosmeticDiagnostics.OneLine(CosmeticCatalog.Empty,
                    CosmeticAssetInventory.Empty, 0, 0, false));
                return degraded;
            }
        }

        private static void LogDiagnostic(CosmeticProfileService service,
            CosmeticAssetInventory inventory, PurchaseService purchases)
        {
            int visibleRows = 0;
            int purchasableRows = 0;
            bool conductorReady = false;
            var rewarded = new DisabledCosmeticRewardedRoute();

            CountProjection(CosmeticSlot.Outfit);
            CountProjection(CosmeticSlot.Accessory);
            CountProjection(CosmeticSlot.Frame);

            Debug.Log(CosmeticDiagnostics.OneLine(service.Catalog, inventory,
                visibleRows, purchasableRows, conductorReady));

            void CountProjection(CosmeticSlot slot)
            {
                var rows = CosmeticWardrobeProjection.Build(service.Catalog, service,
                    purchases, rewarded, service.SelectedCatId, slot);
                visibleRows += rows.Count;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].Route == CosmeticWardrobeRoute.Purchase)
                        purchasableRows++;
                    if (rows[i].Item != null
                        && rows[i].Item.Id == "outfit_conductor")
                        conductorReady = true;
                }
            }
        }
    }
}
