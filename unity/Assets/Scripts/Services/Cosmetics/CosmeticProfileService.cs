using System;
using System.Collections.Generic;
using CatMetro.Services.Purchases;

namespace CatMetro.Services.Cosmetics
{
    public sealed class CosmeticProfileService : ICosmeticPortraitSource, IDisposable
    {
        private readonly CosmeticAssetInventory _assets;
        private readonly ICosmeticProfilePersistence _persistence;
        private readonly CosmeticAccessResolver _access;

        private CosmeticProfileSnapshot _profile;
        private PurchaseService _purchases;
        private string _selectedCatId;
        private CosmeticPortraitSnapshot _currentPortrait;
        private bool _disposed;

        public CosmeticCatalog Catalog { get; }
        public CosmeticProfileSnapshot Profile => _profile;
        public string SelectedCatId => _selectedCatId;
        public CosmeticPortraitSnapshot CurrentPortrait => _currentPortrait;
        public event Action Changed;

        public CosmeticProfileService(CosmeticCatalog catalog, CosmeticAssetInventory assets,
            ICosmeticProfilePersistence persistence, PurchaseService purchases)
        {
            Catalog = catalog ?? CosmeticCatalog.Empty;
            _assets = assets ?? CosmeticAssetInventory.Empty;
            _persistence = persistence ?? new InMemoryCosmeticProfilePersistence(
                CosmeticProfileSnapshot.Empty);
            _profile = TryLoad(out var loaded) ? loaded : CreateStarterDefault();
            _purchases = purchases ?? PurchaseRuntime.Current;
            _access = new CosmeticAccessResolver(_purchases);

            ComputeCurrentPortrait(_access, out _selectedCatId, out _currentPortrait);
            _purchases.Ledger.Changed += OnLedgerChanged;
        }

        public bool IsAccessible(string itemId)
        {
            return Catalog.TryGetItem(itemId, out var item)
                   && _access.IsAccessible(item, _profile);
        }

        public bool TrySelectCat(string catId)
        {
            if (!TryGetAccessibleCat(catId, out _)) return false;
            return TryPublish(_profile.WithSelectedCat(catId));
        }

        public bool TryEquip(string catId, CosmeticSlot slot, string itemId)
        {
            if (!TryGetAccessibleCat(catId, out _)
                || !IsValidSlot(slot)
                || !Catalog.TryGetItem(itemId, out var item)
                || item.Slot != slot
                || !IsCompatible(item, catId)
                || !_assets.TryGet(item.PortraitAssetId, out _)
                || !_access.IsAccessible(item, _profile))
                return false;

            var loadout = LoadoutForMutation(catId).With(slot, itemId);
            return TryPublish(_profile.WithLoadout(loadout));
        }

        public bool TryUnequip(string catId, CosmeticSlot slot)
        {
            if (!TryGetAccessibleCat(catId, out _) || !IsValidSlot(slot)) return false;
            var loadout = LoadoutForMutation(catId).With(slot, string.Empty);
            return TryPublish(_profile.WithLoadout(loadout));
        }

        public bool TryGrantEarnedCat(string catId)
        {
            if (!Catalog.TryGetCat(catId, out var cat)
                || cat.Starter
                || Contains(_profile.EarnedCatIds, catId))
                return false;

            return TryPublish(_profile.WithEarnedCat(catId));
        }

        public bool TryGrantEarnedItem(string itemId)
        {
            if (!Catalog.TryGetItem(itemId, out var item)
                || item.Acquisition != CosmeticAcquisition.Earned
                || Contains(_profile.EarnedItemIds, itemId))
                return false;

            return TryPublish(_profile.WithEarnedItem(itemId));
        }

        public CosmeticPortraitSnapshot EffectivePortraitFor(string catId)
        {
            return EffectivePortraitFor(catId, _access);
        }

        private CosmeticPortraitSnapshot EffectivePortraitFor(string catId,
            CosmeticAccessResolver access)
        {
            if (!TryGetAccessibleCat(catId, out var cat)) return default;

            string baseAssetId = _assets.TryGet(cat.PortraitAssetId, out _)
                ? cat.PortraitAssetId
                : string.Empty;
            var loadout = _profile.LoadoutFor(catId);
            return new CosmeticPortraitSnapshot(catId, baseAssetId,
                EffectiveAsset(catId, CosmeticSlot.Outfit, loadout.OutfitId, access),
                EffectiveAsset(catId, CosmeticSlot.Accessory, loadout.AccessoryId, access),
                EffectiveAsset(catId, CosmeticSlot.Frame, loadout.FrameId, access));
        }

        public CosmeticPortraitSnapshot PreviewPortrait(string catId, CosmeticSlot slot,
            string itemId)
        {
            var portrait = EffectivePortraitFor(catId);
            if (string.IsNullOrEmpty(portrait.CatId)
                || !IsValidSlot(slot)
                || !Catalog.TryGetItem(itemId, out var item)
                || item.Slot != slot
                || !IsCompatible(item, catId)
                || !_assets.TryGet(item.PortraitAssetId, out _))
                return portrait;

            return WithLayer(portrait, slot, item.PortraitAssetId);
        }

        public bool TryGetPortraitAsset(string assetId,
            out CosmeticPortraitAssetDefinition asset)
        {
            return _assets.TryGet(assetId, out asset);
        }

        public void BindPurchases(PurchaseService purchases)
        {
            if (_disposed) return;
            BindPurchasesCore(purchases ?? PurchaseRuntime.Current, true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_purchases != null) _purchases.Ledger.Changed -= OnLedgerChanged;
            _purchases = null;
            _access.BindPurchases(null);
        }

        private bool TryLoad(out CosmeticProfileSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                return _persistence.TryLoad(out snapshot) && snapshot != null;
            }
            catch (Exception)
            {
                snapshot = null;
                return false;
            }
        }

        private CosmeticProfileSnapshot CreateStarterDefault()
        {
            for (int i = 0; i < Catalog.Cats.Count; i++)
            {
                var cat = Catalog.Cats[i];
                if (!cat.Starter) continue;
                return new CosmeticProfileSnapshot(cat.Id, Array.Empty<string>(),
                    Array.Empty<string>(), new[]
                    {
                        new CosmeticLoadout(cat.Id, string.Empty, string.Empty, string.Empty),
                    });
            }

            return CosmeticProfileSnapshot.Empty;
        }

        private bool TryPublish(CosmeticProfileSnapshot candidate)
        {
            try
            {
                if (!_persistence.TryReplace(candidate)) return false;
            }
            catch (Exception)
            {
                return false;
            }

            _profile = candidate;
            RecomputeCurrentPortrait();
            Changed?.Invoke();
            return true;
        }

        private void BindPurchasesCore(PurchaseService purchases, bool publishEffectiveChange)
        {
            if (ReferenceEquals(_purchases, purchases)) return;

            var candidateAccess = new CosmeticAccessResolver(purchases);
            ComputeCurrentPortrait(candidateAccess, out var selectedCatId,
                out var currentPortrait);

            if (_purchases != null) _purchases.Ledger.Changed -= OnLedgerChanged;
            _purchases = purchases;
            _access.BindPurchases(purchases);
            if (_purchases != null) _purchases.Ledger.Changed += OnLedgerChanged;

            var before = _currentPortrait;
            _selectedCatId = selectedCatId;
            _currentPortrait = currentPortrait;
            if (publishEffectiveChange && !_currentPortrait.Equals(before)) Changed?.Invoke();
        }

        private void OnLedgerChanged()
        {
            if (_disposed) return;
            var before = _currentPortrait;
            RecomputeCurrentPortrait();
            if (!_currentPortrait.Equals(before)) Changed?.Invoke();
        }

        private void RecomputeCurrentPortrait()
        {
            ComputeCurrentPortrait(_access, out var selectedCatId, out var currentPortrait);
            _selectedCatId = selectedCatId;
            _currentPortrait = currentPortrait;
        }

        private void ComputeCurrentPortrait(CosmeticAccessResolver access,
            out string selectedCatId, out CosmeticPortraitSnapshot currentPortrait)
        {
            selectedCatId = ResolveSelectedCatId();
            currentPortrait = string.IsNullOrEmpty(selectedCatId)
                ? default
                : EffectivePortraitFor(selectedCatId, access);
        }

        private string ResolveSelectedCatId()
        {
            if (TryGetAccessibleCat(_profile.SelectedCatId, out _))
                return _profile.SelectedCatId;

            for (int i = 0; i < Catalog.Cats.Count; i++)
            {
                var cat = Catalog.Cats[i];
                if (cat.Starter) return cat.Id;
            }

            return string.Empty;
        }

        private bool TryGetAccessibleCat(string catId, out CosmeticCatDefinition cat)
        {
            if (!Catalog.TryGetCat(catId, out cat)) return false;
            return cat.Starter || Contains(_profile.EarnedCatIds, cat.Id);
        }

        private string EffectiveAsset(string catId, CosmeticSlot slot, string itemId,
            CosmeticAccessResolver access)
        {
            if (string.IsNullOrEmpty(itemId)
                || !Catalog.TryGetItem(itemId, out var item)
                || item.Slot != slot
                || !IsCompatible(item, catId)
                || !access.IsAccessible(item, _profile)
                || !_assets.TryGet(item.PortraitAssetId, out _))
                return string.Empty;

            return item.PortraitAssetId;
        }

        private CosmeticLoadout LoadoutForMutation(string catId)
        {
            var loadout = _profile.LoadoutFor(catId);
            return string.Equals(loadout.CatId, catId, StringComparison.Ordinal)
                ? loadout
                : new CosmeticLoadout(catId, string.Empty, string.Empty, string.Empty);
        }

        private static bool IsCompatible(CosmeticItemDefinition item, string catId)
        {
            return Contains(item.CompatibleCatIds, catId);
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsValidSlot(CosmeticSlot slot)
        {
            return slot == CosmeticSlot.Outfit
                   || slot == CosmeticSlot.Accessory
                   || slot == CosmeticSlot.Frame;
        }

        private static CosmeticPortraitSnapshot WithLayer(CosmeticPortraitSnapshot portrait,
            CosmeticSlot slot, string assetId)
        {
            switch (slot)
            {
                case CosmeticSlot.Outfit:
                    return new CosmeticPortraitSnapshot(portrait.CatId, portrait.BaseAssetId,
                        assetId, portrait.AccessoryAssetId, portrait.FrameAssetId);
                case CosmeticSlot.Accessory:
                    return new CosmeticPortraitSnapshot(portrait.CatId, portrait.BaseAssetId,
                        portrait.OutfitAssetId, assetId, portrait.FrameAssetId);
                case CosmeticSlot.Frame:
                    return new CosmeticPortraitSnapshot(portrait.CatId, portrait.BaseAssetId,
                        portrait.OutfitAssetId, portrait.AccessoryAssetId, assetId);
                default:
                    return portrait;
            }
        }
    }
}
