using System;

namespace CatMetro.Services.Cosmetics
{
    public interface ICosmeticPortraitSource
    {
        event Action Changed;
        CosmeticPortraitSnapshot CurrentPortrait { get; }
        bool TryGetPortraitAsset(string assetId, out CosmeticPortraitAssetDefinition asset);
    }
}
