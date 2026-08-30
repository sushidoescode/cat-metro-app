using System.Globalization;

namespace CatMetro.Services.Cosmetics
{
    public static class CosmeticDiagnostics
    {
        public static string OneLine(
            CosmeticCatalog catalog,
            CosmeticAssetInventory assets,
            int visibleRowCount,
            int purchasableRowCount,
            bool conductorReady)
        {
            catalog = catalog ?? CosmeticCatalog.Empty;
            assets = assets ?? CosmeticAssetInventory.Empty;

            int assetReadyRows = 0;
            for (int i = 0; i < catalog.Items.Count; i++)
                if (assets.TryGet(catalog.Items[i].PortraitAssetId, out _))
                    assetReadyRows++;

            return string.Format(CultureInfo.InvariantCulture,
                "COSMETICS admittedRows={0} rejectedRows={1} admittedCats={2} " +
                "assetReadyRows={3} visibleRows={4} purchasableRows={5} conductorReady={6}",
                catalog.AdmittedRowCount,
                catalog.RejectedRowCount,
                catalog.AdmittedCatCount,
                assetReadyRows,
                visibleRowCount,
                purchasableRowCount,
                conductorReady ? "true" : "false");
        }
    }
}
