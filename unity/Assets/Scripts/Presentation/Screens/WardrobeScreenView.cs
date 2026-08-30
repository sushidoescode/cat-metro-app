using System;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Ads;
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

        private readonly struct TryOnSpec
        {
            public readonly string PlacementId;
            public readonly string EntitlementId;
            public readonly string NameKey;

            public TryOnSpec(string placementId, string entitlementId, string nameKey)
            {
                PlacementId = placementId;
                EntitlementId = entitlementId;
                NameKey = nameKey;
            }
        }

        private static readonly TryOnSpec[] TryOnSpecs =
        {
            new TryOnSpec("wardrobe_try_conductor", EntitlementIds.OutfitConductor,
                "wardrobe.tryon.conductor"),
            new TryOnSpec("wardrobe_try_engineer", EntitlementIds.OutfitEngineer,
                "wardrobe.tryon.engineer"),
            new TryOnSpec("wardrobe_try_scarf", EntitlementIds.AccessoryScarf,
                "wardrobe.tryon.scarf"),
            new TryOnSpec("wardrobe_try_goggles", EntitlementIds.AccessoryGoggles,
                "wardrobe.tryon.goggles"),
        };

        public Action OpenRequested;
        public Action BackRequested;

        private PurchaseService _service;
        private IRewardedAds _rewardedAds;
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
        private RectTransform _tryOnStrip;
        private RectTransform _tryOnHeading;
        private RectTransform _coatBody;
        private TMP_Text _entryLabel;
        private TMP_Text _buyLabel;
        private TMP_Text _restoreLabel;
        private TMP_Text _statusLabel;
        private Image _entryOwnedDot;
        private readonly RectTransform[] _tryOnCards = new RectTransform[4];
        private readonly GameObject[] _borrowedAccents = new GameObject[4];
        private readonly GameObject[] _lockedLabels = new GameObject[4];
        private readonly GameObject[] _borrowedLabels = new GameObject[4];
        private readonly GameObject[] _successLabels = new GameObject[4];
        private readonly GameObject[] _actionChips = new GameObject[4];
        private readonly GameObject[] _unavailableLabels = new GameObject[4];
        private readonly bool[] _rewardedRegionsRegistered = new bool[4];

        private Rect _entryRectPx;
        private Rect _backRectPx;
        private Rect _buyRectPx;
        private Rect _restoreRectPx;
        private bool _entryShown;
        private bool _panelShown;
        private bool _entryRegistered;
        private bool _modalRegistered;
        private bool _ledgerSubscribed;
        private bool _adsSubscribed;
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
            => Create(canvasParent, service, RewardedAdRuntime.Current);

        public static WardrobeScreenView Create(Transform canvasParent, PurchaseService service,
            IRewardedAds rewardedAds)
        {
            var root = new GameObject("WardrobeSurface");
            root.transform.SetParent(canvasParent, false);
            var view = root.AddComponent<WardrobeScreenView>();
            view._service = service ?? PurchaseRuntime.Current;
            view._rewardedAds = rewardedAds ?? RewardedAdRuntime.Current;
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

            BuildTryOnStrip(_panel.transform);

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

        private void BuildTryOnStrip(Transform parent)
        {
            _tryOnStrip = MakeRect(parent, "TryOnStrip");
            _tryOnHeading = MakeRect(_tryOnStrip, "TryOnHeading");
            var heading = MakeText(_tryOnHeading, "TryOnHeadingLabel", Vector2.zero, Vector2.one,
                Text("wardrobe.tryon.heading"), 21f, Palette.InkNavy);
            heading.fontStyle = FontStyles.Bold;
            heading.alignment = TextAlignmentOptions.Left;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 14f;
            heading.fontSizeMax = 21f;

            for (int i = 0; i < TryOnSpecs.Length; i++)
                BuildTryOnCard(i);
        }

        private void BuildTryOnCard(int index)
        {
            var spec = TryOnSpecs[index];
            var card = MakeChip(_tryOnStrip, "TryOnCard_" + spec.PlacementId,
                Palette.WithAlpha(Palette.DepotNavy, 0.92f));
            _tryOnCards[index] = card;

            MakeSurface(card, "CreamInset", new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f), Palette.CreamCard, true);
            _borrowedAccents[index] = MakeSurface(card, "BorrowedAccent",
                new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.955f),
                Palette.WithAlpha(Palette.MetroTeal, 0.36f), true).gameObject;

            var name = MakeText(card, "ItemName", new Vector2(0.06f, 0.79f),
                new Vector2(0.94f, 0.96f), Text(spec.NameKey), 16f, Palette.InkNavy);
            name.fontStyle = FontStyles.Bold;
            name.enableAutoSizing = true;
            name.fontSizeMin = 10f;
            name.fontSizeMax = 16f;

            var silhouette = MakeRect(card, "Silhouette");
            silhouette.anchorMin = new Vector2(0.13f, 0.48f);
            silhouette.anchorMax = new Vector2(0.87f, 0.79f);
            silhouette.offsetMin = Vector2.zero;
            silhouette.offsetMax = Vector2.zero;
            BuildTryOnSilhouette(index, silhouette);

            _lockedLabels[index] = MakeText(card, "LockedLabel", new Vector2(0.06f, 0.39f),
                new Vector2(0.94f, 0.50f), Text("wardrobe.tryon.locked"), 13f,
                Palette.InkNavy).gameObject;
            _borrowedLabels[index] = MakeText(card, "BorrowedLabel", new Vector2(0.04f, 0.39f),
                new Vector2(0.96f, 0.50f), Text("wardrobe.tryon.borrowed"), 13f,
                Palette.DepotNavy).gameObject;

            var success = MakeText(card, "SuccessLabel", new Vector2(0.04f, 0.035f),
                new Vector2(0.96f, 0.39f), Text("wardrobe.tryon.success"), 13f,
                Palette.DepotNavy);
            success.fontStyle = FontStyles.Bold;
            success.enableAutoSizing = true;
            success.fontSizeMin = 8f;
            success.fontSizeMax = 13f;
            _successLabels[index] = success.gameObject;

            var action = MakeChip(card, "ActionChip", Palette.TicketOrange);
            action.anchorMin = new Vector2(0.055f, 0.035f);
            action.anchorMax = new Vector2(0.945f, 0.39f);
            action.offsetMin = Vector2.zero;
            action.offsetMax = Vector2.zero;
            var actionLabel = MakeText(action, "ActionLabel", new Vector2(0.04f, 0.02f),
                new Vector2(0.96f, 0.98f), Text("wardrobe.tryon.watch"), 13f,
                Palette.DepotNavy);
            actionLabel.fontStyle = FontStyles.Bold;
            actionLabel.enableAutoSizing = true;
            actionLabel.fontSizeMin = 8f;
            actionLabel.fontSizeMax = 13f;
            _actionChips[index] = action.gameObject;

            var unavailable = MakeText(card, "UnavailableLabel", new Vector2(0.04f, 0.035f),
                new Vector2(0.96f, 0.39f), Text("wardrobe.tryon.unavailable"), 12f,
                Palette.WithAlpha(Palette.InkNavy, 0.72f));
            unavailable.enableAutoSizing = true;
            unavailable.fontSizeMin = 8f;
            unavailable.fontSizeMax = 12f;
            _unavailableLabels[index] = unavailable.gameObject;

            _borrowedAccents[index].SetActive(false);
            _borrowedLabels[index].SetActive(false);
            _successLabels[index].SetActive(false);
            _actionChips[index].SetActive(false);
            _unavailableLabels[index].SetActive(false);
        }

        private static void BuildTryOnSilhouette(int index, RectTransform parent)
        {
            switch (index)
            {
                case 0:
                    MakeSurface(parent, "ConductorCoat", new Vector2(0.20f, 0.08f),
                        new Vector2(0.80f, 0.70f), Palette.InkNavy, true);
                    MakeSurface(parent, "ConductorHat", new Vector2(0.12f, 0.68f),
                        new Vector2(0.88f, 0.84f), Palette.InkNavy, true);
                    MakeSurface(parent, "ConductorHatCrown", new Vector2(0.25f, 0.78f),
                        new Vector2(0.75f, 0.98f), Palette.InkNavy, true);
                    MakeSurface(parent, "ConductorBadge", new Vector2(0.44f, 0.79f),
                        new Vector2(0.56f, 0.96f), Palette.TicketOrange, true);
                    break;
                case 1:
                    MakeSurface(parent, "EngineerBib", new Vector2(0.21f, 0.08f),
                        new Vector2(0.79f, 0.78f), Palette.MetroTeal, true);
                    MakeSurface(parent, "EngineerStrapLeft", new Vector2(0.20f, 0.64f),
                        new Vector2(0.39f, 0.98f), Palette.TicketOrange, true);
                    MakeSurface(parent, "EngineerStrapRight", new Vector2(0.61f, 0.64f),
                        new Vector2(0.80f, 0.98f), Palette.TicketOrange, true);
                    MakeSurface(parent, "EngineerBuckle", new Vector2(0.41f, 0.40f),
                        new Vector2(0.59f, 0.61f), Palette.TicketOrange, true);
                    break;
                case 2:
                    var scarfLeft = MakeSurface(parent, "ScarfLeft", new Vector2(0.22f, 0.06f),
                        new Vector2(0.50f, 0.94f), Palette.TicketOrange, true);
                    scarfLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                    var scarfRight = MakeSurface(parent, "ScarfRight", new Vector2(0.50f, 0.06f),
                        new Vector2(0.78f, 0.94f), Palette.TicketOrange, true);
                    scarfRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                    MakeSurface(parent, "ScarfKnot", new Vector2(0.36f, 0.52f),
                        new Vector2(0.64f, 0.83f), Palette.TabbyYellow, true);
                    break;
                default:
                    MakeSurface(parent, "GoggleLeft", new Vector2(0.05f, 0.27f),
                        new Vector2(0.45f, 0.76f), Palette.InkNavy, true);
                    MakeSurface(parent, "GoggleRight", new Vector2(0.55f, 0.27f),
                        new Vector2(0.95f, 0.76f), Palette.InkNavy, true);
                    MakeSurface(parent, "GoggleLensLeft", new Vector2(0.13f, 0.37f),
                        new Vector2(0.40f, 0.69f), Palette.MetroTeal, true);
                    MakeSurface(parent, "GoggleLensRight", new Vector2(0.60f, 0.37f),
                        new Vector2(0.87f, 0.69f), Palette.MetroTeal, true);
                    MakeSurface(parent, "GoggleBridge", new Vector2(0.42f, 0.45f),
                        new Vector2(0.58f, 0.58f), Palette.InkNavy, true);
                    break;
            }
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
            UnsubscribeAds();
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
            SubscribeAds();
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
            UnsubscribeAds();
            if (_entry != null) _entry.SetActive(false);
            if (_panel != null) _panel.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            UnregisterEntry();
            UnregisterModal();
            UnsubscribeLedger();
            UnsubscribeAds();
        }

        private void OnEnable()
        {
            if (_entryShown) RegisterEntry();
            if (_panelShown)
            {
                RegisterModal();
                SubscribeLedger();
                SubscribeAds();
                RefreshTryOnVisuals();
            }
        }

        private void OnDestroy()
        {
            UnregisterEntry();
            UnregisterModal();
            UnsubscribeLedger();
            UnsubscribeAds();
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

            var strip = WardrobeLayout.PreviewStripRect(safeArea, dpi);
            ApplyPx(_tryOnStrip, strip);
            ApplyPxRelative(_tryOnHeading, WardrobeLayout.PreviewHeadingRect(safeArea, dpi), strip);
            for (int i = 0; i < _tryOnCards.Length; i++)
                ApplyPxRelative(_tryOnCards[i], WardrobeLayout.PreviewCardRect(safeArea, dpi, i),
                    strip);

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

            RefreshTryOnVisuals();

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

        private void RefreshTryOnVisuals()
        {
            if (_rewardedAds == null) return;
            for (int i = 0; i < TryOnSpecs.Length; i++)
            {
                bool unlocked = _service.IsUnlocked(TryOnSpecs[i].EntitlementId);
                bool canShow = _panelShown && !unlocked &&
                    _rewardedAds.CanShow(TryOnSpecs[i].PlacementId);

                _borrowedAccents[i].SetActive(unlocked);
                _lockedLabels[i].SetActive(!unlocked);
                _borrowedLabels[i].SetActive(unlocked);
                _successLabels[i].SetActive(unlocked);
                _actionChips[i].SetActive(canShow);
                _unavailableLabels[i].SetActive(_panelShown && !unlocked && !canShow);
                SyncRewardedRegion(i, canShow);
            }
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

        private void OnAdsAvailabilityChanged()
        {
            if (!_panelShown) return;
            RefreshTryOnVisuals();
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

        private void SubscribeAds()
        {
            if (_adsSubscribed || _rewardedAds == null) return;
            _rewardedAds.AvailabilityChanged += OnAdsAvailabilityChanged;
            _adsSubscribed = true;
        }

        private void UnsubscribeAds()
        {
            if (!_adsSubscribed || _rewardedAds == null) return;
            _rewardedAds.AvailabilityChanged -= OnAdsAvailabilityChanged;
            _adsSubscribed = false;
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
            UnregisterRewardedRegions();
            if (_regions == null || !_modalRegistered) return;
            _regions.Unregister(BackRegionId);
            _regions.Unregister(BuyRegionId);
            _regions.Unregister(RestoreRegionId);
            _modalRegistered = false;
        }

        private void SyncRewardedRegion(int index, bool shouldRegister)
        {
            if (_regions == null || !_modalRegistered) shouldRegister = false;
            if (shouldRegister == _rewardedRegionsRegistered[index]) return;

            string id = RewardedRegionId(TryOnSpecs[index].PlacementId);
            if (shouldRegister)
            {
                int capturedIndex = index;
                _regions.Register(id, () => PaintedScreenRectPx(
                        _actionChips[capturedIndex].transform as RectTransform),
                    () => _rewardedAds.Show(TryOnSpecs[capturedIndex].PlacementId), ModalPriority);
                _rewardedRegionsRegistered[index] = true;
            }
            else
            {
                _regions?.Unregister(id);
                _rewardedRegionsRegistered[index] = false;
            }
        }

        private void UnregisterRewardedRegions()
        {
            for (int i = 0; i < _rewardedRegionsRegistered.Length; i++)
            {
                if (!_rewardedRegionsRegistered[i]) continue;
                _regions?.Unregister(RewardedRegionId(TryOnSpecs[i].PlacementId));
                _rewardedRegionsRegistered[i] = false;
            }
        }

        private static string RewardedRegionId(string placementId)
            => "wardrobe.rewarded." + placementId;

        private static Rect PaintedScreenRectPx(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float xMin = first.x;
            float xMax = first.x;
            float yMin = first.y;
            float yMax = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                xMin = Mathf.Min(xMin, screen.x);
                xMax = Mathf.Max(xMax, screen.x);
                yMin = Mathf.Min(yMin, screen.y);
                yMax = Mathf.Max(yMax, screen.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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

        private static void ApplyPxRelative(RectTransform rect, Rect px, Rect parentPx)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            // With a bottom-left anchor, anchoredPosition is measured from the parent's
            // bottom-left anchor reference, not from its pivot/centre.
            rect.anchoredPosition = px.center - parentPx.min;
            rect.sizeDelta = px.size;
        }
    }
}
