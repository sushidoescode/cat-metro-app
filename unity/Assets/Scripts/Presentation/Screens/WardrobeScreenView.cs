using System;
using System.Collections.Generic;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Screens
{
    public sealed class WardrobeScreenView : MonoBehaviour
    {
        private const string EntryRegionId = "wardrobe.entry";
        private const string BackRegionId = "wardrobe.back";
        private const string RestoreRegionId = "wardrobe.restore";
        private const string PrimaryRegionId = "wardrobe.primary";
        private const int EntryPriority = ChromeRegions.HomeScreenPriority;
        private const int ModalPriority = ChromeRegions.StackedModalPriority;

        public Action OpenRequested;
        public Action BackRequested;

        private PurchaseService _purchases;
        private CosmeticProfileService _profile;
        private ICosmeticRewardedRoute _rewarded;
        private ChromeRegions _regions;
        private GameObject _entry;
        private GameObject _panel;
        private RectTransform _entryRect;
        private RectTransform _backRect;
        private RectTransform _titleRect;
        private RectTransform _catSelectorRect;
        private RectTransform _portraitRect;
        private RectTransform _tabsRect;
        private RectTransform _itemsRect;
        private RectTransform _cardsRoot;
        private RectTransform _primaryRect;
        private RectTransform _restoreRect;
        private RectTransform _statusRect;
        private TMP_Text _primaryLabel;
        private TMP_Text _restoreLabel;
        private TMP_Text _statusLabel;
        private TMP_Text _emptyLabel;
        private CosmeticPortraitView _largePortrait;
        private CosmeticPortraitView _entryPortrait;

        private readonly List<SelectorTarget> _catTargets = new List<SelectorTarget>();
        private readonly List<SelectorTarget> _tabTargets = new List<SelectorTarget>();
        private readonly List<CosmeticItemCardView> _cards = new List<CosmeticItemCardView>();
        private readonly List<string> _registeredItemIds = new List<string>();
        private IReadOnlyList<CosmeticWardrobeRow> _rows = Array.Empty<CosmeticWardrobeRow>();
        private CosmeticWardrobeRow? _selectedRow;
        private CosmeticSlot _selectedSlot = CosmeticSlot.Outfit;
        private string _previewItemId = string.Empty;

        private Rect _entryRectPx;
        private Rect _backRectPx;
        private Rect _primaryRectPx;
        private Rect _restoreRectPx;
        private Rect _portraitRectPx;
        private Rect _itemsRectPx;
        private Rect _safeArea;
        private float _dpi;
        private bool _entryShown;
        private bool _panelShown;
        private bool _entryRegistered;
        private bool _staticRegistered;
        private bool _primaryRegistered;
        private bool _profileSubscribed;
        private bool _authoritySubscribed;
        private bool _purchaseBusy;
        private bool _rewardBusy;
        private bool _restoreBusy;
        private bool _destroyed;
        private long _sessionGeneration;
        private long _operationGeneration;

        public Rect EntryRectPx => _entryRectPx;
        public Rect BackRectPx => _backRectPx;
        public Rect BuyRectPx => _primaryRectPx;
        public Rect PrimaryActionRectPx => _primaryRectPx;
        public Rect RestoreRectPx => _restoreRectPx;
        public Rect PortraitRectPx => _portraitRectPx;
        public Rect ItemsRectPx => _itemsRectPx;
        public bool EntryVisible => _entry != null && _entry.activeInHierarchy;
        public bool PanelVisible => _panel != null && _panel.activeInHierarchy;
        public CosmeticSlot SelectedSlot => _selectedSlot;
        public IReadOnlyList<CosmeticItemCardView> VisibleCards => _cards.AsReadOnly();
        public CosmeticPortraitView LargePortrait => _largePortrait;
        public CosmeticPortraitView EntryPortrait => _entryPortrait;
        public string BuyLabelText => _primaryLabel != null ? _primaryLabel.text : string.Empty;
        public string PrimaryActionText => BuyLabelText;
        public string RestoreLabelText => _restoreLabel != null ? _restoreLabel.text : string.Empty;
        public string StatusText => _statusLabel != null ? _statusLabel.text : string.Empty;

        public static WardrobeScreenView Create(Transform canvasParent, PurchaseService purchases)
        {
            return Create(canvasParent, purchases, CosmeticRuntime.Current,
                new DisabledCosmeticRewardedRoute());
        }

        public static WardrobeScreenView Create(Transform canvasParent,
            PurchaseService purchases, CosmeticProfileService profile,
            ICosmeticRewardedRoute rewarded)
        {
            var root = new GameObject("WardrobeSurface", typeof(RectTransform));
            root.transform.SetParent(canvasParent, false);
            Stretch((RectTransform)root.transform);
            var view = root.AddComponent<WardrobeScreenView>();
            view._purchases = purchases ?? PurchaseRuntime.Current;
            view._profile = profile ?? CosmeticRuntime.Current;
            view._rewarded = rewarded ?? new DisabledCosmeticRewardedRoute();
            view.BuildEntry(root.transform);
            view.BuildPanel(root.transform);
            view._entry.SetActive(false);
            view._panel.SetActive(false);
            root.SetActive(false);
            return view;
        }

        private void BuildEntry(Transform parent)
        {
            _entry = new GameObject("WardrobeCapsule", typeof(RectTransform));
            _entry.transform.SetParent(parent, false);
            _entryRect = (RectTransform)_entry.transform;
            Paint(_entry, Palette.DepotNavy, true);

            var portraitMount = MakeRect(_entry.transform, "EntryPortraitMount",
                new Vector2(0.035f, 0.08f), new Vector2(0.30f, 0.92f));
            _entryPortrait = CosmeticPortraitView.Create(portraitMount, _profile,
                "EntryPortrait");
            var label = MakeText(_entry.transform, "WardrobeLabel", new Vector2(0.31f, 0f),
                new Vector2(0.96f, 1f), Text("wardrobe.entry"), 24f, Palette.WarmPaper);
            label.fontStyle = FontStyles.Bold;
        }

        private void BuildPanel(Transform parent)
        {
            _panel = new GameObject("WardrobePanel", typeof(RectTransform));
            _panel.transform.SetParent(parent, false);
            Stretch((RectTransform)_panel.transform);
            MakeSurface(_panel.transform, "WarmDesk", Vector2.zero, Vector2.one,
                Palette.WarmPaper, false);
            MakeSurface(_panel.transform, "BoardEdge", new Vector2(0.018f, 0.015f),
                new Vector2(0.982f, 0.985f), Palette.DepotNavy, true);
            MakeSurface(_panel.transform, "CreamBoard", new Vector2(0.027f, 0.022f),
                new Vector2(0.973f, 0.978f), Palette.CreamCard, true);

            _backRect = MakeChip(_panel.transform, "BackChip", Palette.InkNavy);
            var back = MakeText(_backRect, "BackLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.back"), 21f, Palette.WarmPaper);
            back.fontStyle = FontStyles.Bold;
            _titleRect = MakeRect(_panel.transform, "WardrobeTitle");
            var title = MakeText(_titleRect, "TitleLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.title"), 36f, Palette.InkNavy);
            title.fontStyle = FontStyles.Bold;

            _catSelectorRect = MakeRect(_panel.transform, "CatSelectorBand");
            BuildCatSelectors();

            _portraitRect = MakeChip(_panel.transform, "LargePortraitCard", Palette.DepotNavy);
            MakeSurface(_portraitRect, "PortraitPaper", new Vector2(0.018f, 0.018f),
                new Vector2(0.982f, 0.982f), Palette.WarmPaper, true);
            MakeSurface(_portraitRect, "PortraitGlow", new Vector2(0.05f, 0.05f),
                new Vector2(0.95f, 0.95f), Palette.WithAlpha(Palette.MetroTeal, 0.14f), true);
            var largeMount = MakeRect(_portraitRect, "LargePortraitMount",
                new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.96f));
            _largePortrait = CosmeticPortraitView.Create(largeMount, _profile,
                "LargePortrait");

            _tabsRect = MakeRect(_panel.transform, "TabsBand");
            BuildTabs();
            _itemsRect = MakeRect(_panel.transform, "ItemsBand");
            _cardsRoot = MakeRect(_itemsRect, "CardsRoot", Vector2.zero, Vector2.one);
            _emptyLabel = MakeText(_itemsRect, "EmptyStateLabel", new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f), Text("wardrobe.empty"), 23f, Palette.DepotNavy);
            _emptyLabel.fontStyle = FontStyles.Italic;
            _emptyLabel.gameObject.SetActive(false);

            _statusRect = MakeRect(_panel.transform, "WardrobeStatus");
            _statusLabel = MakeText(_statusRect, "StatusLabel", Vector2.zero, Vector2.one,
                string.Empty, 21f, Palette.InkNavy);
            _primaryRect = MakeChip(_panel.transform, "PrimaryActionChip", Palette.TicketOrange);
            _primaryLabel = MakeText(_primaryRect, "PrimaryActionLabel", Vector2.zero,
                Vector2.one, string.Empty, 27f, Palette.DepotNavy);
            _primaryLabel.fontStyle = FontStyles.Bold;
            _primaryRect.gameObject.SetActive(false);
            _restoreRect = MakeChip(_panel.transform, "RestoreChip", Palette.MetroTeal);
            _restoreLabel = MakeText(_restoreRect, "RestoreLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.restore"), 23f, Palette.DepotNavy);
            _restoreLabel.fontStyle = FontStyles.Bold;
        }

        private void BuildCatSelectors()
        {
            for (int i = 0; i < _profile.Catalog.Cats.Count; i++)
            {
                var cat = _profile.Catalog.Cats[i];
                var rect = MakeChip(_catSelectorRect, "CatSelector-" + cat.Id,
                    CatColor(cat.Id));
                var label = MakeText(rect, "CatLabel-" + cat.Id,
                    new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                    Text(cat.DisplayNameKey), 18f, Palette.InkNavy);
                label.fontStyle = FontStyles.Bold;
                _catTargets.Add(new SelectorTarget(cat.Id, rect));
            }
        }

        private void BuildTabs()
        {
            AddTab("outfit", CosmeticSlot.Outfit, "wardrobe.tab.outfit");
            AddTab("accessory", CosmeticSlot.Accessory, "wardrobe.tab.accessory");
            AddTab("frame", CosmeticSlot.Frame, "wardrobe.tab.frame");
        }

        private void AddTab(string id, CosmeticSlot slot, string key)
        {
            var rect = MakeChip(_tabsRect, "Tab-" + id, Palette.MetroTeal);
            var label = MakeText(rect, "TabLabel-" + id, Vector2.zero, Vector2.one,
                Text(key), 20f, Palette.DepotNavy);
            label.fontStyle = FontStyles.Bold;
            _tabTargets.Add(new SelectorTarget(id, rect, slot));
        }

        public void Attach(ChromeRegions regions) => _regions = regions;

        public void ShowEntry()
        {
            InvalidatePresentationSession();
            ClearPreview();
            _entryShown = true;
            _panelShown = false;
            gameObject.SetActive(true);
            _entry.SetActive(true);
            _panel.SetActive(false);
            UnsubscribeProfile();
            UnregisterPanelRegions();
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            RegisterEntry();
        }

        public void Open()
        {
            InvalidatePresentationSession();
            ClearPreview();
            _entryShown = false;
            _panelShown = true;
            gameObject.SetActive(true);
            _entry.SetActive(false);
            _panel.SetActive(true);
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            UnregisterEntry();
            SubscribeProfile();
            RegisterStaticPanelRegions();
            SetStatus(Text("wardrobe.status.checking"));
            RebuildProjectionAndCards();
            long session = _sessionGeneration;
            _purchases.Refresh(() =>
            {
                if (!IsCurrentSession(session)) return;
                RebuildProjectionAndCards();
                if (!IsOperationBusy) SetStatus(string.Empty);
            });
        }

        public void Hide()
        {
            InvalidatePresentationSession();
            ClearPreview();
            _entryShown = false;
            _panelShown = false;
            UnregisterEntry();
            UnregisterPanelRegions();
            UnsubscribeProfile();
            if (_entry != null) _entry.SetActive(false);
            if (_panel != null) _panel.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            InvalidatePresentationSession(clearSelection: false);
            UnregisterEntry();
            UnregisterPanelRegions();
            UnsubscribeProfile();
        }

        private void OnEnable()
        {
            if (_entryShown) RegisterEntry();
            if (_panelShown)
            {
                SubscribeProfile();
                RebuildProjectionAndCards();
                RegisterStaticPanelRegions();
                RegisterCardRegions();
                RegisterPrimaryRegion();
            }
        }

        private void OnDestroy()
        {
            _destroyed = true;
            InvalidatePresentationSession();
            UnregisterEntry();
            UnregisterPanelRegions();
            UnsubscribeProfile();
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _safeArea = safeArea;
            _dpi = dpi > 0f ? dpi : 160f;
            int visibleCount = _cards.Count;
            bool hasPrimaryAction = _selectedRow.HasValue;
            _entryRectPx = WardrobeLayout.EntryRect(safeArea, _dpi);
            _backRectPx = WardrobeLayout.BackRect(safeArea, _dpi);
            _primaryRectPx = WardrobeLayout.PrimaryActionRect(safeArea, _dpi);
            _restoreRectPx = WardrobeLayout.RestoreRect(safeArea, _dpi, hasPrimaryAction);
            _portraitRectPx = WardrobeLayout.PortraitRect(safeArea, _dpi, visibleCount,
                hasPrimaryAction);
            _itemsRectPx = WardrobeLayout.ItemsRect(safeArea, _dpi, visibleCount,
                hasPrimaryAction);
            ApplyPx(_entryRect, _entryRectPx);
            ApplyPx(_backRect, _backRectPx);
            ApplyPx(_titleRect, WardrobeLayout.TitleRect(safeArea, _dpi));
            ApplyPx(_catSelectorRect, WardrobeLayout.CatSelectorRect(safeArea, _dpi));
            ApplyPx(_portraitRect, _portraitRectPx);
            ApplyPx(_tabsRect, WardrobeLayout.TabsRect(safeArea, _dpi, visibleCount,
                hasPrimaryAction));
            ApplyPx(_itemsRect, _itemsRectPx);
            ApplyPx(_primaryRect, _primaryRectPx);
            ApplyPx(_restoreRect, _restoreRectPx);
            ApplyPx(_statusRect, WardrobeLayout.StatusRect(safeArea, _dpi,
                hasPrimaryAction));
            LayoutHorizontal(_catTargets, WardrobeLayout.CatSelectorRect(safeArea, _dpi), _dpi);
            LayoutHorizontal(_tabTargets, WardrobeLayout.TabsRect(safeArea, _dpi,
                visibleCount, hasPrimaryAction), _dpi);
            LayoutCards();
        }

        private void RebuildProjectionAndCards()
        {
            UnregisterCardRegions();
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].gameObject.SetActive(false);
                Destroy(_cards[i].gameObject);
            }
            _cards.Clear();
            _rows = CosmeticWardrobeProjection.Build(_profile.Catalog, _profile, _purchases,
                _rewarded, _profile.SelectedCatId, _selectedSlot);

            CosmeticWardrobeRow? stillSelected = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (string.Equals(row.Item.Id, _previewItemId, StringComparison.Ordinal))
                    stillSelected = row;
            }
            if (!stillSelected.HasValue)
            {
                if (_selectedRow.HasValue && IsOperationBusy) InvalidateCurrentOperation();
                _previewItemId = string.Empty;
                _selectedRow = null;
                ApplyAuthoritativePortrait();
            }
            else _selectedRow = stillSelected;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var card = CosmeticItemCardView.Create(_cardsRoot);
                card.Configure(row.Item.Id, Text(row.Item.DisplayNameKey), CardStatus(row),
                    row.Price.DisplayText ?? string.Empty, row.Route,
                    string.Equals(_previewItemId, row.Item.Id, StringComparison.Ordinal),
                    SlotColor(row.Item.Slot));
                _cards.Add(card);
            }
            _emptyLabel.gameObject.SetActive(_cards.Count == 0);
            UpdatePrimaryAction();
            ReflowLayout();
            PaintSelectors();
            RegisterCardRegions();
        }

        private void LayoutCards()
        {
            if (_itemsRect == null) return;
            for (int i = 0; i < _cards.Count; i++)
            {
                var screenRect = WardrobeLayout.ItemCardRect(_itemsRectPx, i, _cards.Count, _dpi);
                var localRect = new Rect(screenRect.position - _itemsRectPx.position,
                    screenRect.size);
                ApplyPx(_cards[i].RootTransform, localRect);
            }
        }

        private void OnCardTapped(CosmeticWardrobeRow row)
        {
            if (IsOperationBusy) return;
            _previewItemId = row.Item.Id;
            _selectedRow = row;
            _largePortrait.ApplySnapshot(_profile.PreviewPortrait(_profile.SelectedCatId,
                row.Item.Slot, row.Item.Id));
            RebuildProjectionAndCards();
        }

        private void OnCatTapped(string catId)
        {
            if (IsOperationBusy) return;
            ClearPreview();
            if (!_profile.TrySelectCat(catId))
            {
                SetStatus(Text("wardrobe.status.save.failed"));
                RebuildProjectionAndCards();
            }
        }

        private void OnTabTapped(CosmeticSlot slot)
        {
            if (IsOperationBusy) return;
            ClearPreview();
            _selectedSlot = slot;
            RebuildProjectionAndCards();
        }

        private void OnPrimaryTapped()
        {
            if (IsOperationBusy || !_selectedRow.HasValue) return;
            var row = _selectedRow.Value;
            if (row.IsEquipped && row.Route != CosmeticWardrobeRoute.Purchase)
            {
                if (!_profile.TryUnequip(_profile.SelectedCatId, row.Item.Slot)) SaveFailed();
                return;
            }

            switch (row.Route)
            {
                case CosmeticWardrobeRoute.Equip:
                    if (!_profile.TryEquip(_profile.SelectedCatId, row.Item.Slot, row.Item.Id))
                        SaveFailed();
                    break;
                case CosmeticWardrobeRoute.Purchase:
                    BeginPurchase(row);
                    break;
                case CosmeticWardrobeRoute.Rewarded:
                    BeginRewarded(row);
                    break;
                case CosmeticWardrobeRoute.EarnInstruction:
                    SetStatus(Text(row.Item.EarnInstructionKey));
                    break;
            }
        }

        private void BeginPurchase(CosmeticWardrobeRow row)
        {
            long session = _sessionGeneration;
            long operation = BeginOperation();
            string catId = _profile.SelectedCatId;
            var slot = row.Item.Slot;
            string itemId = row.Item.Id;
            string entitlementId = row.Item.EntitlementId;
            string productId = row.Item.ProductId;
            _purchaseBusy = true;
            SetStatus(Text("wardrobe.status.opening"));
            UpdatePrimaryAction();
            _purchases.Purchase(productId, result =>
            {
                if (!IsCurrentOperation(session, operation)) return;
                CompleteCurrentOperation();
                if (result.Outcome == PurchaseOutcome.SuccessCandidate
                    && result.ConfirmedEntitlements.HasValue
                    && result.ConfirmedEntitlements.Value.IsAuthoritative
                    && _purchases.IsUnlocked(entitlementId))
                {
                    if (!_profile.TryEquip(catId, slot, itemId))
                        SaveFailed();
                    else SetStatus(Text("wardrobe.state.equipped"));
                    return;
                }

                switch (result.Outcome)
                {
                    case PurchaseOutcome.UserCancelled:
                        SetStatus(Text("wardrobe.status.cancelled"));
                        break;
                    case PurchaseOutcome.Pending:
                        SetStatus(Text("wardrobe.status.pending"));
                        break;
                    case PurchaseOutcome.SuccessCandidate:
                        SetStatus(Text("wardrobe.status.unconfirmed"));
                        break;
                    case PurchaseOutcome.Busy:
                        SetStatus(Text("wardrobe.status.opening"));
                        break;
                    default:
                        SetStatus(Text("wardrobe.status.unavailable"));
                        break;
                }
                RebuildProjectionAndCards();
            });
        }

        private void BeginRewarded(CosmeticWardrobeRow row)
        {
            long session = _sessionGeneration;
            long operation = BeginOperation();
            string catId = _profile.SelectedCatId;
            var slot = row.Item.Slot;
            string itemId = row.Item.Id;
            string entitlementId = row.Item.EntitlementId;
            string placementId = row.Item.RewardedPlacementId;
            _rewardBusy = true;
            UpdatePrimaryAction();
            _rewarded.Request(placementId, () =>
            {
                if (!IsCurrentOperation(session, operation)) return;
                CompleteCurrentOperation();
                if (_purchases.IsUnlocked(entitlementId))
                {
                    if (!_profile.TryEquip(catId, slot, itemId)) SaveFailed();
                    else SetStatus(Text("wardrobe.state.equipped"));
                }
                else
                {
                    SetStatus(Text("wardrobe.status.unconfirmed"));
                    RebuildProjectionAndCards();
                }
            });
        }

        private void OnRestoreTapped()
        {
            if (IsOperationBusy) return;
            long session = _sessionGeneration;
            long operation = BeginOperation();
            _restoreBusy = true;
            _restoreLabel.text = Text("wardrobe.restore.running");
            SetStatus(Text("wardrobe.status.restoring"));
            _purchases.Restore(result =>
            {
                if (!IsCurrentOperation(session, operation)) return;
                CompleteCurrentOperation();
                _restoreLabel.text = Text("wardrobe.restore");
                RebuildProjectionAndCards();
                switch (result.Outcome)
                {
                    case RestoreOutcome.Completed when result.RestoredEntitlementCount > 0:
                        SetStatus(Text("wardrobe.status.restored"));
                        break;
                    case RestoreOutcome.Completed:
                        SetStatus(Text("wardrobe.status.none"));
                        break;
                    case RestoreOutcome.Busy:
                        SetStatus(Text("wardrobe.status.restoring"));
                        break;
                    case RestoreOutcome.Unavailable:
                        SetStatus(Text("wardrobe.status.unavailable"));
                        break;
                    default:
                        SetStatus(Text("wardrobe.status.restore.failed"));
                        break;
                }
            });
        }

        private void OnProfileChanged()
        {
            if (!_panelShown) return;
            if (!IsOperationBusy) SetStatus(string.Empty);
            // Keep the selected card/action, but replace its presentation-only preview with
            // the newly durable authoritative portrait. Cat/tab changes clear selection before
            // their mutation; purchase/equip changes intentionally retain it for Unequip.
            ApplyAuthoritativePortrait();
            RebuildProjectionAndCards();
        }

        private void SaveFailed()
        {
            ClearPreview();
            RebuildProjectionAndCards();
            SetStatus(Text("wardrobe.status.save.failed"));
        }

        private void ClearPreview()
        {
            _previewItemId = string.Empty;
            _selectedRow = null;
            ApplyAuthoritativePortrait();
        }

        private bool IsOperationBusy => _purchaseBusy || _rewardBusy || _restoreBusy;

        private long BeginOperation()
        {
            unchecked { _operationGeneration++; }
            return _operationGeneration;
        }

        private void CompleteCurrentOperation()
        {
            unchecked { _operationGeneration++; }
            _purchaseBusy = false;
            _rewardBusy = false;
            _restoreBusy = false;
        }

        private bool IsCurrentSession(long session)
        {
            return this != null && !_destroyed && isActiveAndEnabled && _panelShown
                && session == _sessionGeneration;
        }

        private bool IsCurrentOperation(long session, long operation)
        {
            return IsCurrentSession(session) && operation == _operationGeneration;
        }

        private void InvalidateCurrentOperation()
        {
            CompleteCurrentOperation();
            if (_restoreLabel != null) _restoreLabel.text = Text("wardrobe.restore");
            SetStatus(string.Empty);
        }

        private void InvalidatePresentationSession(bool clearSelection = true)
        {
            unchecked { _sessionGeneration++; }
            InvalidateCurrentOperation();
            UnregisterPrimaryRegion();
            if (!clearSelection) return;
            _previewItemId = string.Empty;
            _selectedRow = null;
            if (_primaryRect != null) _primaryRect.gameObject.SetActive(false);
            if (!_destroyed) ApplyAuthoritativePortrait();
        }

        private void ReflowLayout()
        {
            if (_safeArea.width <= 0f || _safeArea.height <= 0f) return;
            LayoutForViewport(_safeArea, _dpi);
        }

        private void ApplyAuthoritativePortrait()
        {
            if (_largePortrait != null) _largePortrait.ApplySnapshot(_profile.CurrentPortrait);
        }

        private string CardStatus(CosmeticWardrobeRow row)
        {
            if (row.SecondsRemaining > 0)
            {
                string state = row.IsEquipped
                    ? Text("wardrobe.state.equipped")
                    : Text("wardrobe.state.owned");
                return state + " · " + Remaining(row.SecondsRemaining);
            }
            if (row.IsEquipped) return Text("wardrobe.state.equipped");
            if (row.IsAccessible)
            {
                return Text("wardrobe.state.owned");
            }
            if (row.Route == CosmeticWardrobeRoute.Rewarded)
                return Text("wardrobe.action.rewarded");
            if (row.Route == CosmeticWardrobeRoute.EarnInstruction)
                return Text(row.Item.EarnInstructionKey);
            return string.Empty;
        }

        private void UpdatePrimaryAction()
        {
            UnregisterPrimaryRegion();
            if (!_selectedRow.HasValue)
            {
                _primaryRect.gameObject.SetActive(false);
                return;
            }
            var row = _selectedRow.Value;
            string text;
            if (row.IsEquipped && row.Route != CosmeticWardrobeRoute.Purchase)
                text = Text("wardrobe.action.unequip");
            else
            {
                switch (row.Route)
                {
                    case CosmeticWardrobeRoute.Equip:
                        text = Text("wardrobe.action.equip");
                        break;
                    case CosmeticWardrobeRoute.Purchase:
                        text = _purchaseBusy ? Text("wardrobe.store.opening")
                            : Text("wardrobe.buy").Replace("{price}", row.Price.DisplayText);
                        break;
                    case CosmeticWardrobeRoute.Rewarded:
                        text = Text("wardrobe.action.rewarded");
                        break;
                    case CosmeticWardrobeRoute.EarnInstruction:
                        text = Text(row.Item.EarnInstructionKey);
                        break;
                    default:
                        _primaryRect.gameObject.SetActive(false);
                        return;
                }
            }
            _primaryLabel.text = text;
            _primaryRect.gameObject.SetActive(true);
            RegisterPrimaryRegion();
        }

        private void PaintSelectors()
        {
            for (int i = 0; i < _catTargets.Count; i++)
            {
                var image = _catTargets[i].Rect.GetComponent<Image>();
                image.color = string.Equals(_catTargets[i].Id, _profile.SelectedCatId,
                    StringComparison.Ordinal) ? Palette.TicketOrange : CatColor(_catTargets[i].Id);
            }
            for (int i = 0; i < _tabTargets.Count; i++)
            {
                var image = _tabTargets[i].Rect.GetComponent<Image>();
                image.color = _tabTargets[i].Slot == _selectedSlot
                    ? Palette.TicketOrange : Palette.MetroTeal;
            }
        }

        private void SubscribeProfile()
        {
            if (!_profileSubscribed)
            {
                _profile.Changed += OnProfileChanged;
                _profileSubscribed = true;
            }
            if (!_authoritySubscribed)
            {
                _purchases.Ledger.Changed += OnAuthorityChanged;
                _authoritySubscribed = true;
            }
        }

        private void UnsubscribeProfile()
        {
            if (_profileSubscribed)
            {
                _profile.Changed -= OnProfileChanged;
                _profileSubscribed = false;
            }
            if (_authoritySubscribed)
            {
                _purchases.Ledger.Changed -= OnAuthorityChanged;
                _authoritySubscribed = false;
            }
        }

        private void OnAuthorityChanged()
        {
            if (!_destroyed && isActiveAndEnabled && _panelShown)
                RebuildProjectionAndCards();
        }

        private void RegisterEntry()
        {
            if (_regions == null || _entryRegistered || !_entryShown) return;
            _regions.Register(EntryRegionId, () => _entryRectPx,
                () => OpenRequested?.Invoke(), EntryPriority);
            _entryRegistered = true;
        }

        private void UnregisterEntry()
        {
            if (_regions == null || !_entryRegistered) return;
            _regions.Unregister(EntryRegionId);
            _entryRegistered = false;
        }

        private void RegisterStaticPanelRegions()
        {
            if (_regions == null || _staticRegistered || !_panelShown) return;
            _regions.Register(BackRegionId, () => _backRectPx,
                () => BackRequested?.Invoke(), ModalPriority);
            _regions.Register(RestoreRegionId, () => _restoreRectPx,
                OnRestoreTapped, ModalPriority);
            for (int i = 0; i < _catTargets.Count; i++)
            {
                var target = _catTargets[i];
                _regions.Register("wardrobe.cat." + target.Id,
                    () => RectFor(target.Rect), () => OnCatTapped(target.Id), ModalPriority);
            }
            for (int i = 0; i < _tabTargets.Count; i++)
            {
                var target = _tabTargets[i];
                _regions.Register("wardrobe.tab." + target.Id,
                    () => RectFor(target.Rect), () => OnTabTapped(target.Slot), ModalPriority);
            }
            _staticRegistered = true;
        }

        private void RegisterCardRegions()
        {
            if (_regions == null || !_panelShown || !_staticRegistered) return;
            UnregisterCardRegions();
            for (int i = 0; i < _cards.Count && i < _rows.Count; i++)
            {
                var row = _rows[i];
                var card = _cards[i];
                string id = "wardrobe.item." + row.Item.Id;
                _regions.Register(id, () => RectFor(card.RootTransform),
                    () => OnCardTapped(row), ModalPriority);
                _registeredItemIds.Add(id);
            }
        }

        private void UnregisterCardRegions()
        {
            if (_regions != null)
                for (int i = 0; i < _registeredItemIds.Count; i++)
                    _regions.Unregister(_registeredItemIds[i]);
            _registeredItemIds.Clear();
        }

        private void RegisterPrimaryRegion()
        {
            if (_regions == null || _primaryRegistered || !_panelShown
                || !_primaryRect.gameObject.activeInHierarchy) return;
            _regions.Register(PrimaryRegionId, () => _primaryRectPx,
                OnPrimaryTapped, ModalPriority);
            _primaryRegistered = true;
        }

        private void UnregisterPrimaryRegion()
        {
            if (_regions == null || !_primaryRegistered) return;
            _regions.Unregister(PrimaryRegionId);
            _primaryRegistered = false;
        }

        private void UnregisterPanelRegions()
        {
            UnregisterPrimaryRegion();
            UnregisterCardRegions();
            if (_regions == null || !_staticRegistered) return;
            _regions.Unregister(BackRegionId);
            _regions.Unregister(RestoreRegionId);
            for (int i = 0; i < _catTargets.Count; i++)
                _regions.Unregister("wardrobe.cat." + _catTargets[i].Id);
            for (int i = 0; i < _tabTargets.Count; i++)
                _regions.Unregister("wardrobe.tab." + _tabTargets[i].Id);
            _staticRegistered = false;
        }

        private static void LayoutHorizontal(IReadOnlyList<SelectorTarget> targets, Rect band,
            float dpi)
        {
            if (targets.Count == 0) return;
            float gap = 6f * HudBands.PxPerDp(dpi);
            float width = Mathf.Max(0f, (band.width - gap * (targets.Count - 1)) / targets.Count);
            for (int i = 0; i < targets.Count; i++)
            {
                var local = new Rect(i * (width + gap), 0f, width, band.height);
                ApplyPx(targets[i].Rect, local);
            }
        }

        private Rect RectFor(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private static string Remaining(long seconds)
        {
            string value = seconds >= 3600
                ? Mathf.CeilToInt(seconds / 3600f) + "h"
                : Mathf.Max(1, Mathf.CeilToInt(seconds / 60f)) + "m";
            return Text("wardrobe.time.remaining").Replace("{time}", value);
        }

        private static Color CatColor(string catId)
        {
            int separator = string.IsNullOrEmpty(catId) ? -1 : catId.IndexOf('_');
            string lineName = separator > 0 ? catId.Substring(0, separator) : catId;
            return CatLine.ColorOf(lineName);
        }

        private static Color SlotColor(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Outfit: return Palette.InkNavy;
                case CosmeticSlot.Accessory: return Palette.MetroTeal;
                case CosmeticSlot.Frame: return Palette.TabbyYellow;
                default: return Palette.DepotNavy;
            }
        }

        private static void SetStatus(TMP_Text label, string value)
        {
            if (label != null) label.text = value ?? string.Empty;
        }

        private void SetStatus(string value) => SetStatus(_statusLabel, value);
        private static string Text(string key) => Strings.UiStrings.Get(key);

        private static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform MakeRect(Transform parent, string name,
            Vector2 min, Vector2 max)
        {
            var rect = MakeRect(parent, name);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform MakeChip(Transform parent, string name, Color color)
        {
            var rect = MakeRect(parent, name);
            Paint(rect.gameObject, color, true);
            return rect;
        }

        private static Image MakeSurface(Transform parent, string name, Vector2 min,
            Vector2 max, Color color, bool rounded)
        {
            var rect = MakeRect(parent, name, min, max);
            return Paint(rect.gameObject, color, rounded);
        }

        private static Image Paint(GameObject go, Color color, bool rounded)
        {
            var image = go.AddComponent<Image>();
            if (rounded) image.material = UiChromeMaterial.Shared;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text MakeText(Transform parent, string name, Vector2 min,
            Vector2 max, string value, float size, Color color)
        {
            var rect = MakeRect(parent, name, min, max);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = value;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = size;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ApplyPx(RectTransform rect, Rect px)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = px.center;
            rect.sizeDelta = px.size;
        }

        private readonly struct SelectorTarget
        {
            public string Id { get; }
            public RectTransform Rect { get; }
            public CosmeticSlot Slot { get; }

            public SelectorTarget(string id, RectTransform rect,
                CosmeticSlot slot = CosmeticSlot.Outfit)
            {
                Id = id;
                Rect = rect;
                Slot = slot;
            }
        }
    }
}
