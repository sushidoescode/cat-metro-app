using System;
using System.Collections.Generic;

namespace CatMetro.Services.Cosmetics
{
    public readonly struct CosmeticLoadout
    {
        public string CatId { get; }
        public string OutfitId { get; }
        public string AccessoryId { get; }
        public string FrameId { get; }

        public CosmeticLoadout(string catId, string outfitId, string accessoryId, string frameId)
        {
            CatId = catId ?? string.Empty;
            OutfitId = outfitId ?? string.Empty;
            AccessoryId = accessoryId ?? string.Empty;
            FrameId = frameId ?? string.Empty;
        }

        public string ItemFor(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Outfit: return OutfitId;
                case CosmeticSlot.Accessory: return AccessoryId;
                case CosmeticSlot.Frame: return FrameId;
                default: return string.Empty;
            }
        }

        public CosmeticLoadout With(CosmeticSlot slot, string itemId)
        {
            switch (slot)
            {
                case CosmeticSlot.Outfit:
                    return new CosmeticLoadout(CatId, itemId, AccessoryId, FrameId);
                case CosmeticSlot.Accessory:
                    return new CosmeticLoadout(CatId, OutfitId, itemId, FrameId);
                case CosmeticSlot.Frame:
                    return new CosmeticLoadout(CatId, OutfitId, AccessoryId, itemId);
                default:
                    return this;
            }
        }
    }

    public sealed class CosmeticProfileSnapshot
    {
        private static readonly CosmeticProfileSnapshot EmptySnapshot =
            new CosmeticProfileSnapshot(string.Empty, null, null, null);

        public static CosmeticProfileSnapshot Empty => EmptySnapshot;

        public string SelectedCatId { get; }
        public IReadOnlyList<string> EarnedCatIds { get; }
        public IReadOnlyList<string> EarnedItemIds { get; }
        public IReadOnlyList<CosmeticLoadout> Loadouts { get; }

        public CosmeticProfileSnapshot(string selectedCatId, IReadOnlyList<string> earnedCatIds,
            IReadOnlyList<string> earnedItemIds, IReadOnlyList<CosmeticLoadout> loadouts)
        {
            SelectedCatId = selectedCatId ?? string.Empty;
            EarnedCatIds = Array.AsReadOnly(CopyUniqueIds(earnedCatIds));
            EarnedItemIds = Array.AsReadOnly(CopyUniqueIds(earnedItemIds));
            Loadouts = Array.AsReadOnly(CopyLoadouts(loadouts));
        }

        public CosmeticLoadout LoadoutFor(string catId)
        {
            if (string.IsNullOrEmpty(catId)) return default;
            for (int i = 0; i < Loadouts.Count; i++)
                if (string.Equals(Loadouts[i].CatId, catId, StringComparison.Ordinal))
                    return Loadouts[i];
            return default;
        }

        public CosmeticProfileSnapshot WithSelectedCat(string catId)
        {
            if (string.IsNullOrEmpty(catId)) return this;
            return new CosmeticProfileSnapshot(catId, EarnedCatIds, EarnedItemIds, Loadouts);
        }

        public CosmeticProfileSnapshot WithLoadout(CosmeticLoadout loadout)
        {
            if (string.IsNullOrEmpty(loadout.CatId)) return this;
            var copy = CopyList(Loadouts);
            for (int i = 0; i < copy.Count; i++)
            {
                if (!string.Equals(copy[i].CatId, loadout.CatId, StringComparison.Ordinal))
                    continue;
                copy[i] = loadout;
                return new CosmeticProfileSnapshot(SelectedCatId, EarnedCatIds, EarnedItemIds,
                    copy);
            }
            copy.Add(loadout);
            return new CosmeticProfileSnapshot(SelectedCatId, EarnedCatIds, EarnedItemIds, copy);
        }

        public CosmeticProfileSnapshot WithEarnedCat(string catId)
        {
            if (string.IsNullOrEmpty(catId) || Contains(EarnedCatIds, catId)) return this;
            var copy = CopyList(EarnedCatIds);
            copy.Add(catId);
            return new CosmeticProfileSnapshot(SelectedCatId, copy, EarnedItemIds, Loadouts);
        }

        public CosmeticProfileSnapshot WithEarnedItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || Contains(EarnedItemIds, itemId)) return this;
            var copy = CopyList(EarnedItemIds);
            copy.Add(itemId);
            return new CosmeticProfileSnapshot(SelectedCatId, EarnedCatIds, copy, Loadouts);
        }

        private static string[] CopyUniqueIds(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var id = source[i];
                if (string.IsNullOrEmpty(id) || Contains(result, id)) continue;
                result.Add(id);
            }
            return result.ToArray();
        }

        private static CosmeticLoadout[] CopyLoadouts(IReadOnlyList<CosmeticLoadout> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<CosmeticLoadout>();
            var result = new List<CosmeticLoadout>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var loadout = source[i];
                if (string.IsNullOrEmpty(loadout.CatId)) continue;
                int existing = IndexOf(result, loadout.CatId);
                if (existing >= 0) result[existing] = loadout;
                else result.Add(loadout);
            }
            return result.ToArray();
        }

        private static int IndexOf(IReadOnlyList<CosmeticLoadout> source, string catId)
        {
            for (int i = 0; i < source.Count; i++)
                if (string.Equals(source[i].CatId, catId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static bool Contains(IReadOnlyList<string> source, string id)
        {
            for (int i = 0; i < source.Count; i++)
                if (string.Equals(source[i], id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static List<T> CopyList<T>(IReadOnlyList<T> source)
        {
            var copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++) copy.Add(source[i]);
            return copy;
        }
    }

    public readonly struct CosmeticPortraitSnapshot : IEquatable<CosmeticPortraitSnapshot>
    {
        public string CatId { get; }
        public string BaseAssetId { get; }
        public string OutfitAssetId { get; }
        public string AccessoryAssetId { get; }
        public string FrameAssetId { get; }

        public CosmeticPortraitSnapshot(string catId, string baseAssetId, string outfitAssetId,
            string accessoryAssetId, string frameAssetId)
        {
            CatId = catId ?? string.Empty;
            BaseAssetId = baseAssetId ?? string.Empty;
            OutfitAssetId = outfitAssetId ?? string.Empty;
            AccessoryAssetId = accessoryAssetId ?? string.Empty;
            FrameAssetId = frameAssetId ?? string.Empty;
        }

        public bool Equals(CosmeticPortraitSnapshot other) =>
            Same(CatId, other.CatId)
            && Same(BaseAssetId, other.BaseAssetId)
            && Same(OutfitAssetId, other.OutfitAssetId)
            && Same(AccessoryAssetId, other.AccessoryAssetId)
            && Same(FrameAssetId, other.FrameAssetId);

        public override bool Equals(object obj) =>
            obj is CosmeticPortraitSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(CatId ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(BaseAssetId ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(OutfitAssetId ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(AccessoryAssetId ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(FrameAssetId ?? string.Empty);
                return hash;
            }
        }

        private static bool Same(string left, string right) => string.Equals(
            left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
    }
}
