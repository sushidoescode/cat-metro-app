using System;
using System.Collections.Generic;

namespace CatMetro.Services.Cosmetics
{
    public enum CosmeticSlot
    {
        Outfit,
        Accessory,
        Frame,
    }

    public enum CosmeticAcquisition
    {
        Starter,
        Earned,
        Entitlement,
    }

    public sealed class CosmeticCatDefinition
    {
        public string Id { get; }
        public string DisplayNameKey { get; }
        public string PortraitAssetId { get; }
        public bool Starter { get; }

        public CosmeticCatDefinition(string id, string displayNameKey, string portraitAssetId,
            bool starter)
        {
            Id = id;
            DisplayNameKey = displayNameKey;
            PortraitAssetId = portraitAssetId;
            Starter = starter;
        }
    }

    public sealed class CosmeticItemDefinition
    {
        public string Id { get; }
        public CosmeticSlot Slot { get; }
        public string DisplayNameKey { get; }
        public string PortraitAssetId { get; }
        public CosmeticAcquisition Acquisition { get; }
        public string EntitlementId { get; }
        public string ProductId { get; }
        public string EarnInstructionKey { get; }
        public string RewardedPlacementId { get; }
        public IReadOnlyList<string> CompatibleCatIds { get; }
        public int Order { get; }

        public CosmeticItemDefinition(string id, CosmeticSlot slot, string displayNameKey,
            string portraitAssetId, CosmeticAcquisition acquisition, string entitlementId,
            string productId, string earnInstructionKey, string rewardedPlacementId,
            IReadOnlyList<string> compatibleCatIds, int order)
        {
            Id = id;
            Slot = slot;
            DisplayNameKey = displayNameKey;
            PortraitAssetId = portraitAssetId;
            Acquisition = acquisition;
            EntitlementId = entitlementId ?? string.Empty;
            ProductId = productId ?? string.Empty;
            EarnInstructionKey = earnInstructionKey ?? string.Empty;
            RewardedPlacementId = rewardedPlacementId ?? string.Empty;
            CompatibleCatIds = Array.AsReadOnly(Copy(compatibleCatIds));
            Order = order;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
            var copy = new string[source.Count];
            for (int i = 0; i < source.Count; i++) copy[i] = source[i];
            return copy;
        }
    }

    public sealed class CosmeticPortraitAssetDefinition
    {
        public string AssetId { get; }
        public string RendererToken { get; }
        public string ProvenanceId { get; }

        public CosmeticPortraitAssetDefinition(string assetId, string rendererToken,
            string provenanceId)
        {
            AssetId = assetId;
            RendererToken = rendererToken;
            ProvenanceId = provenanceId;
        }
    }
}
