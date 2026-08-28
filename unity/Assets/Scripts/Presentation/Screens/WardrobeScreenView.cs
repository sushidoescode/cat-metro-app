using System;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Purchases;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Screens
{
    // A deliberately small, filmable purchase surface. It renders one fixed profile cat; profile
    // selection belongs to TASK 13. Paid, restored, promotional, and rewarded-ad access all paint
    // through PurchaseService.IsUnlocked, so TASK 11 has no second cosmetic state to invent.
    public sealed class WardrobeScreenView : MonoBehaviour
    {
        private const string EntryRegionId = "wardrobe.entry";
        private const string BackRegionId = "wardrobe.back";
        private const string BuyRegionId = "wardrobe.buy";
        private const string RestoreRegionId = "wardrobe.restore";
        private const int EntryPriority = ChromeRegions.HomeScreenPriority;
        private const int ModalPriority = ChromeRegions.StackedModalPriority;

        public Action OpenRequested;
        public Action BackRequested;

        private PurchaseService _service;
        private ChromeRegions _regions;
        private GameObject _entry;
        private GameObject _panel;
        private GameObject _coatGroup;
        private RectTransform _entryRect;
        private RectTransform _backRect;
        private RectTransform _buyRect;
        private RectTransform _restoreRect;
        private RectTransform _statusRect;
        private RectTransform _portraitRect;
        private RectTransform _coatBody;
        private TMP_Text _entryLabel;
        private TMP_Text _buyLabel;
        private TMP_Text _restoreLabel;
        private TMP_Text _statusLabel;
        private Image _entryOwnedDot;

        private Rect _entryRectPx;
        private Rect _backRectPx;
        private Rect _buyRectPx;
        private Rect _restoreRectPx;
        private bool _entryShown;
        private bool _panelShown;
        private bool _entryRegistered;
        private bool _modalRegistered;
        private bool _ledgerSubscribed;
        private bool _purchaseBusy;
        private bool _restoreBusy;

        public Rect EntryRectPx => _entryRectPx;
        public Rect BackRectPx => _backRectPx;
        public Rect BuyRectPx => _buyRectPx;
        public Rect RestoreRectPx => _restoreRectPx;
        public bool EntryVisible => _entry != null && _entry.activeInHierarchy;
        public bool PanelVisible => _panel != null && _panel.activeInHierarchy;
        public bool ConductorCoatVisible => _coatGroup != null && _coatGroup.activeInHierarchy;
        public RectTransform ConductorCoatTransform => _coatBody;
        public string BuyLabelText => _buyLabel != null ? _buyLabel.text : string.Empty;
        public string RestoreLabelText => _restoreLabel != null ? _restoreLabel.text : string.Empty;
        public string StatusText => _statusLabel != null ? _statusLabel.text : string.Empty;

        public static WardrobeScreenView Create(Transform canvasParent, PurchaseService service)
        {
            var root = new GameObject("WardrobeSurface");
            root.transform.SetParent(canvasParent, false);
            var view = root.AddComponent<WardrobeScreenView>();
            view._service = service ?? PurchaseRuntime.Current;
            Stretch(root.AddComponent<RectTransform>());

            view.BuildEntry(root.transform);
            view.BuildPanel(root.transform);

            view._entry.SetActive(false);
            view._panel.SetActive(false);
            root.SetActive(false);
            return view;
        }

        private void BuildEntry(Transform parent)
        {
            _entry = new GameObject("WardrobeCapsule");
            _entry.transform.SetParent(parent, false);
            _entryRect = _entry.AddComponent<RectTransform>();
            Paint(_entry, Palette.DepotNavy, true);

            MakeSurface(_entry.transform, "ProfileCatDot", new Vector2(0.05f, 0.16f),
                new Vector2(0.31f, 0.84f), Palette.MetroTeal, true);
            _entryOwnedDot = MakeSurface(_entry.transform, "CoatEquippedDot",
                new Vector2(0.225f, 0.57f), new Vector2(0.32f, 0.83f), Palette.TicketOrange, true);
            _entryLabel = MakeText(_entry.transform, "WardrobeLabel", new Vector2(0.32f, 0f),
                new Vector2(0.95f, 1f), Text("wardrobe.entry"), 25f, Palette.WarmPaper);
            _entryLabel.fontStyle = FontStyles.Bold;
            _entryLabel.enableAutoSizing = true;
            _entryLabel.fontSizeMin = 16f;
            _entryLabel.fontSizeMax = 25f;
        }

        private void BuildPanel(Transform parent)
        {
            _panel = new GameObject("WardrobePanel");
            _panel.transform.SetParent(parent, false);
            Stretch(_panel.AddComponent<RectTransform>());

            MakeSurface(_panel.transform, "WarmDesk", Vector2.zero, Vector2.one,
                Palette.WarmPaper, false);
            MakeSurface(_panel.transform, "BoardEdge", new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f), Palette.WithAlpha(Palette.DepotNavy, 0.60f), true);
            MakeSurface(_panel.transform, "CreamBoard", new Vector2(0.032f, 0.035f),
                new Vector2(0.968f, 0.968f), Palette.CreamCard, true);

            _backRect = MakeChip(_panel.transform, "BackChip", Palette.InkNavy);
            var back = MakeText(_backRect, "BackLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.back"), 22f, Palette.WarmPaper);
            back.fontStyle = FontStyles.Bold;

            var titleRect = MakeRect(_panel.transform, "WardrobeTitle");
            var title = MakeText(titleRect, "TitleLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.title"), 42f, Palette.InkNavy);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 42f;

            _portraitRect = MakeChip(_panel.transform, "ProfileCatCard", Palette.DepotNavy);
            MakeSurface(_portraitRect, "PortraitInset", new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f), Palette.WarmPaper, true);
            MakeSurface(_portraitRect, "PortraitGlow", new Vector2(0.10f, 0.08f),
                new Vector2(0.90f, 0.86f), Palette.WithAlpha(Palette.MetroTeal, 0.17f), true);

            var profileLabel = MakeText(_portraitRect, "ProfileLabel", new Vector2(0.08f, 0.88f),
                new Vector2(0.92f, 0.98f), Text("wardrobe.profile"), 24f, Palette.InkNavy);
            profileLabel.fontStyle = FontStyles.Bold;
            BuildProfileCat(_portraitRect);

            var product = MakeText(_portraitRect, "ProductName", new Vector2(0.08f, 0.02f),
                new Vector2(0.92f, 0.13f), Text("wardrobe.product"), 28f, Palette.InkNavy);
            product.fontStyle = FontStyles.Bold;

            _statusRect = MakeRect(_panel.transform, "WardrobeStatus");
            _statusLabel = MakeText(_statusRect, "StatusLabel", Vector2.zero, Vector2.one,
                string.Empty, 24f, Palette.InkNavy);
            _statusLabel.enableAutoSizing = true;
            _statusLabel.fontSizeMin = 17f;
            _statusLabel.fontSizeMax = 24f;

            _restoreRect = MakeChip(_panel.transform, "RestorePurchasesChip",
                Palette.WithAlpha(Palette.MetroTeal, 0.95f));
            _restoreLabel = MakeText(_restoreRect, "RestoreLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.restore"), 25f, Palette.DepotNavy);
            _restoreLabel.fontStyle = FontStyles.Bold;

            _buyRect = MakeChip(_panel.transform, "BuyConductorCoatChip", Palette.TicketOrange);
            _buyLabel = MakeText(_buyRect, "BuyLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.store.checking"), 31f, Palette.DepotNavy);
            _buyLabel.fontStyle = FontStyles.Bold;
            _buyLabel.enableAutoSizing = true;
            _buyLabel.fontSizeMin = 20f;
            _buyLabel.fontSizeMax = 31f;
        }

        private void BuildProfileCat(Transform parent)
        {
            // The cat is intentionally chunky and graphic at phone-video scale. TASK 13 may
            // replace this fixed portrait with the selected cat without changing entitlement flow.
            var body = MakeSurface(parent, "PlainCatBody", new Vector2(0.31f, 0.15f),
                new Vector2(0.69f, 0.49f), Palette.MetroTeal, true);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            MakeSurface(parent, "CatHead", new Vector2(0.26f, 0.43f),
                new Vector2(0.74f, 0.82f), Palette.MetroTeal, true);
            var leftEar = MakeSurface(parent, "CatEarLeft", new Vector2(0.29f, 0.72f),
                new Vector2(0.41f, 0.87f), Palette.MetroTeal, false);
            leftEar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            var rightEar = MakeSurface(parent, "CatEarRight", new Vector2(0.59f, 0.72f),
                new Vector2(0.71f, 0.87f), Palette.MetroTeal, false);
            rightEar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            MakeSurface(parent, "EyeLeft", new Vector2(0.38f, 0.59f),
                new Vector2(0.425f, 0.64f), Palette.DepotNavy, true);
            MakeSurface(parent, "EyeRight", new Vector2(0.575f, 0.59f),
                new Vector2(0.62f, 0.64f), Palette.DepotNavy, true);
            MakeSurface(parent, "CatNose", new Vector2(0.48f, 0.53f),
                new Vector2(0.52f, 0.57f), Palette.TicketOrange, true);

            _coatGroup = new GameObject("ConductorCoat");
            _coatGroup.transform.SetParent(parent, false);
            Stretch(_coatGroup.AddComponent<RectTransform>());
            _coatBody = MakeSurface(_coatGroup.transform, "CoatBody", new Vector2(0.27f, 0.14f),
                new Vector2(0.73f, 0.49f), Palette.InkNavy, true).rectTransform;
            var collarLeft = MakeSurface(_coatGroup.transform, "CreamCollarLeft",
                new Vector2(0.39f, 0.38f), new Vector2(0.50f, 0.49f), Palette.CreamCard, false);
            collarLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            var collarRight = MakeSurface(_coatGroup.transform, "CreamCollarRight",
                new Vector2(0.50f, 0.38f), new Vector2(0.61f, 0.49f), Palette.CreamCard, false);
            collarRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            MakeSurface(_coatGroup.transform, "BrassButtonUpper", new Vector2(0.485f, 0.30f),
                new Vector2(0.515f, 0.335f), Palette.TabbyYellow, true);
            MakeSurface(_coatGroup.transform, "BrassButtonLower", new Vector2(0.485f, 0.22f),
                new Vector2(0.515f, 0.255f), Palette.TabbyYellow, true);
            MakeSurface(_coatGroup.transform, "ConductorHatBrim", new Vector2(0.28f, 0.78f),
                new Vector2(0.72f, 0.835f), Palette.InkNavy, true);
            MakeSurface(_coatGroup.transform, "ConductorHatCrown", new Vector2(0.34f, 0.80f),
                new Vector2(0.66f, 0.91f), Palette.InkNavy, true);
            MakeSurface(_coatGroup.transform, "ConductorHatBadge", new Vector2(0.475f, 0.83f),
                new Vector2(0.525f, 0.885f), Palette.TicketOrange, true);
            _coatGroup.SetActive(false);
        }

        public void Attach(ChromeRegions regions) => _regions = regions;

        public void ShowEntry()
        {
            _entryShown = true;
            _panelShown = false;
            _purchaseBusy = false;
            _restoreBusy = false;
            gameObject.SetActive(true);
            _entry.SetActive(true);
            _panel.SetActive(false);
            UnsubscribeLedger();
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            RefreshVisuals();
            RegisterEntry();
            UnregisterModal();
        }

        public void Open()
        {
            _entryShown = false;
            _panelShown = true;
            gameObject.SetActive(true);
            _entry.SetActive(false);
            _panel.SetActive(true);
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            UnregisterEntry();
            RegisterModal();
            SubscribeLedger();
            SetStatus(Text("wardrobe.status.checking"));
            RefreshVisuals();
            _service.Refresh(() =>
            {
                if (this == null || !_panelShown) return;
                RefreshVisuals();
                SetDefaultStatus();
            });
        }

        public void Hide()
        {
            _entryShown = false;
            _panelShown = false;
            _purchaseBusy = false;
            _restoreBusy = false;
            UnregisterEntry();
            UnregisterModal();
            UnsubscribeLedger();
            if (_entry != null) _entry.SetActive(false);
            if (_panel != null) _panel.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            UnregisterEntry();
            UnregisterModal();
            UnsubscribeLedger();
        }

        private void OnEnable()
        {
            if (_entryShown) RegisterEntry();
            if (_panelShown)
            {
                RegisterModal();
                SubscribeLedger();
            }
        }

        private void OnDestroy()
        {
            UnregisterEntry();
            UnregisterModal();
            UnsubscribeLedger();
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _entryRectPx = WardrobeLayout.EntryRect(safeArea, dpi);
            _backRectPx = WardrobeLayout.BackRect(safeArea, dpi);
            _buyRectPx = WardrobeLayout.BuyRect(safeArea, dpi);
            _restoreRectPx = WardrobeLayout.RestoreRect(safeArea, dpi);
            ApplyPx(_entryRect, _entryRectPx);
            ApplyPx(_backRect, _backRectPx);
            ApplyPx(_buyRect, _buyRectPx);
            ApplyPx(_restoreRect, _restoreRectPx);
            ApplyPx(_statusRect, WardrobeLayout.StatusRect(safeArea, dpi));
            ApplyPx(_portraitRect, WardrobeLayout.PortraitRect(safeArea, dpi));

            var title = _panel.transform.Find("WardrobeTitle") as RectTransform;
            if (title != null) ApplyPx(title, WardrobeLayout.TitleRect(safeArea, dpi));
        }

        private void OnBuyTapped()
        {
            if (_purchaseBusy || _restoreBusy) return;
            if (HasPermanentCoat())
            {
                SetStatus(Text("wardrobe.status.equipped"));
                return;
            }

            // A transient CustomerInfo/network failure can mark the backend unreachable after
            // offerings have already supplied a real Package and localized price. In that state
            // the priced button must still reach RevenueCat; the backend owns the real outcome.
            if (!_service.TryGetPrice(ProductIds.Gate, out _))
            {
                SetStatus(Text("wardrobe.status.unavailable"));
                RefreshVisuals();
                return;
            }

            _purchaseBusy = true;
            RefreshVisuals();
            SetStatus(Text("wardrobe.status.opening"));
            _service.Purchase(ProductIds.Gate, result =>
            {
                if (this == null) return;
                _purchaseBusy = false;
                RefreshVisuals();
                if (HasPermanentCoat())
                {
                    SetStatus(Text("wardrobe.status.equipped"));
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
                        SetStatus(Text("wardrobe.status.failed"));
                        break;
                }
            });
        }

        private void OnRestoreTapped()
        {
            if (_purchaseBusy || _restoreBusy) return;
            _restoreBusy = true;
            RefreshVisuals();
            SetStatus(Text("wardrobe.status.restoring"));
            _service.Restore(result =>
            {
                if (this == null) return;
                _restoreBusy = false;
                RefreshVisuals();
                switch (result.Outcome)
                {
                    case RestoreOutcome.Completed when HasPermanentCoat():
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

        private void RefreshVisuals()
        {
            bool unlocked = _service.IsUnlocked(EntitlementIds.OutfitConductor);
            bool permanent = HasPermanentCoat();
            if (_coatGroup != null) _coatGroup.SetActive(unlocked);
            if (_entryOwnedDot != null) _entryOwnedDot.gameObject.SetActive(unlocked);

            if (_restoreLabel != null)
                _restoreLabel.text = _restoreBusy
                    ? Text("wardrobe.restore.running")
                    : Text("wardrobe.restore");

            if (_buyLabel == null) return;
            if (permanent)
                _buyLabel.text = Text("wardrobe.equipped");
            else if (_purchaseBusy)
                _buyLabel.text = Text("wardrobe.store.opening");
            else if (_service.TryGetPrice(ProductIds.Gate, out var price))
                _buyLabel.text = Text("wardrobe.buy").Replace("{price}", price.DisplayText);
            else if (_service.Availability == BackendAvailability.Initializing)
                _buyLabel.text = Text("wardrobe.store.checking");
            else
                _buyLabel.text = Text("wardrobe.store.unavailable");
        }

        private bool HasPermanentCoat()
            => _service.IsUnlocked(EntitlementIds.OutfitConductor) &&
               _service.SecondsUntilExpiry(EntitlementIds.OutfitConductor) == 0L;

        private void SetDefaultStatus()
        {
            SetStatus(_service.IsUnlocked(EntitlementIds.OutfitConductor)
                ? Text("wardrobe.status.equipped")
                : _service.Availability == BackendAvailability.Ready
                    ? Text("wardrobe.status.locked")
                    : Text("wardrobe.status.unavailable"));
        }

        private void OnLedgerChanged()
        {
            if (!_panelShown) return;
            RefreshVisuals();
            if (!_purchaseBusy && !_restoreBusy) SetDefaultStatus();
        }

        private void SetStatus(string value)
        {
            if (_statusLabel != null) _statusLabel.text = value;
        }

        private void SubscribeLedger()
        {
            if (_ledgerSubscribed) return;
            _service.Ledger.Changed += OnLedgerChanged;
            _ledgerSubscribed = true;
        }

        private void UnsubscribeLedger()
        {
            if (!_ledgerSubscribed) return;
            _service.Ledger.Changed -= OnLedgerChanged;
            _ledgerSubscribed = false;
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

        private void RegisterModal()
        {
            if (_regions == null || _modalRegistered || !_panelShown) return;
            _regions.Register(BackRegionId, () => _backRectPx,
                () => BackRequested?.Invoke(), ModalPriority);
            _regions.Register(BuyRegionId, () => _buyRectPx, OnBuyTapped, ModalPriority);
            _regions.Register(RestoreRegionId, () => _restoreRectPx, OnRestoreTapped, ModalPriority);
            _modalRegistered = true;
        }

        private void UnregisterModal()
        {
            if (_regions == null || !_modalRegistered) return;
            _regions.Unregister(BackRegionId);
            _regions.Unregister(BuyRegionId);
            _regions.Unregister(RestoreRegionId);
            _modalRegistered = false;
        }

        private static string Text(string key) => Strings.UiStrings.Get(key);

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static RectTransform MakeChip(Transform parent, string name, Color color)
        {
            var rect = MakeRect(parent, name);
            Paint(rect.gameObject, color, true);
            return rect;
        }

        private static Image MakeSurface(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color, bool rounded)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return Paint(go, color, rounded);
        }

        private static Image Paint(GameObject go, Color color, bool rounded)
        {
            var image = go.AddComponent<Image>();
            if (rounded)
            {
                var material = UiChromeMaterial.Shared;
                if (material != null) image.material = material;
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static void ApplyPx(RectTransform rect, Rect px)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = px.center;
            rect.sizeDelta = px.size;
        }
    }
}
