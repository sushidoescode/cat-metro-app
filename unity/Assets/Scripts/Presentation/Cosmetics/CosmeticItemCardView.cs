using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Cosmetics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Cosmetics
{
    /// <summary>Paint-only Wardrobe tile. Input and authority stay with WardrobeScreenView.</summary>
    public sealed class CosmeticItemCardView : MonoBehaviour
    {
        private CosmeticProfileService _profile;
        private TMP_Text _nameLabel;
        private TMP_Text _statusLabel;
        private TMP_Text _priceLabel;
        private Image _selectionOutline;
        private Image _priceChip;
        private Image _paper;
        private RectTransform _portraitMount;
        private CosmeticSlot _itemSlot;

        public RectTransform RootTransform { get; private set; }
        public CosmeticPortraitView ItemPortrait { get; private set; }
        public bool IsActive => gameObject.activeInHierarchy;
        public bool PriceChipVisible => _priceChip != null && _priceChip.gameObject.activeSelf;
        public string ItemId { get; private set; } = string.Empty;
        public string DisplayedNameText => _nameLabel != null ? _nameLabel.text : string.Empty;
        public string DisplayedStatusText => _statusLabel != null ? _statusLabel.text : string.Empty;
        public string DisplayedPriceText => _priceLabel != null ? _priceLabel.text : string.Empty;
        public CosmeticWardrobeRoute Route { get; private set; }

        public Rect ScreenRect
        {
            get
            {
                if (RootTransform == null) return default;
                var corners = new Vector3[4];
                RootTransform.GetWorldCorners(corners);
                var canvas = RootTransform.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
                var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y,
                    topRight.x, topRight.y);
            }
        }

        public static CosmeticItemCardView Create(Transform parent,
            CosmeticProfileService profile)
        {
            var root = new GameObject("CosmeticItemCard", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<CosmeticItemCardView>();
            view._profile = profile;
            view.RootTransform = (RectTransform)root.transform;

            view._selectionOutline = root.AddComponent<Image>();
            view._selectionOutline.color = Palette.DepotNavy;
            view._selectionOutline.sprite = HudShapeSprites.RoundedSquare;
            view._selectionOutline.type = Image.Type.Sliced;
            view._selectionOutline.material = UiChromeMaterial.Shared;
            view._selectionOutline.raycastTarget = false;

            view._paper = MakeImage(root.transform, "CardPaper", new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f), Palette.WarmPaper);
            view._portraitMount = MakeRect(root.transform, "ItemPortraitMount",
                new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.64f));
            view._portraitMount.gameObject.AddComponent<RectMask2D>();
            view.ItemPortrait = CosmeticPortraitView.CreateStaticSnapshot(view._portraitMount,
                profile, "ItemPortrait");
            view._nameLabel = MakeText(root.transform, "ItemNameLabel",
                new Vector2(0.04f, 0.64f), new Vector2(0.96f, 0.98f), 18f,
                Palette.InkNavy, TextAlignmentOptions.Center);
            view._nameLabel.fontStyle = FontStyles.Bold;
            // Two readable lines need more than the old 34dp header. Keep the price band
            // fixed and let long names wrap above the separately clipped illustration.
            view._nameLabel.textWrappingMode = TextWrappingModes.Normal;
            view._nameLabel.maxVisibleLines = 2;
            view._nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            view._statusLabel = MakeText(root.transform, "ItemStatusLabel",
                new Vector2(0.06f, 0.035f), new Vector2(0.94f, 0.23f), 15f,
                Palette.DepotNavy, TextAlignmentOptions.Center);
            view._statusLabel.enableWordWrapping = false;
            view._statusLabel.maxVisibleLines = 1;
            view._statusLabel.overflowMode = TextOverflowModes.Ellipsis;
            view._priceChip = MakeImage(root.transform, "PriceChip",
                new Vector2(0.07f, 0.025f), new Vector2(0.93f, 0.20f), Palette.MetroTeal);
            view._priceLabel = MakeText(view._priceChip.transform, "ItemPriceLabel",
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f), 16f,
                Palette.InkNavy, TextAlignmentOptions.Center);
            view._priceLabel.fontStyle = FontStyles.Bold;
            view._priceChip.gameObject.SetActive(false);
            return view;
        }

        public void Configure(string selectedCatId, CosmeticItemDefinition item,
            string displayName, string statusText, string priceText,
            CosmeticWardrobeRoute route, bool selected, Color accent)
        {
            ItemId = item?.Id ?? string.Empty;
            Route = route;
            _nameLabel.text = displayName ?? string.Empty;
            _statusLabel.text = statusText ?? string.Empty;
            _priceLabel.text = priceText ?? string.Empty;
            bool showPrice = !string.IsNullOrEmpty(priceText);
            _priceChip.gameObject.SetActive(showPrice);
            _statusLabel.gameObject.SetActive(!showPrice);
            _selectionOutline.color = selected ? Palette.MetroTeal : accent;
            if (_profile != null && item != null)
            {
                _itemSlot = item.Slot;
                ItemPortrait.ApplySnapshot(_profile.PreviewPortrait(selectedCatId,
                    item.Slot, item.Id));
                // Cards isolate the item being sold; an equipped frame or coat must never
                // appear to be bundled with a different tile.
                ItemPortrait.SetBaseLayerSuppressed(true);
                ItemPortrait.OutfitLayerTransform.gameObject.SetActive(item.Slot == CosmeticSlot.Outfit);
                ItemPortrait.AccessoryLayerTransform.gameObject.SetActive(item.Slot == CosmeticSlot.Accessory);
                ItemPortrait.FrameLayerTransform.gameObject.SetActive(item.Slot == CosmeticSlot.Frame);
            }
            name = "ItemCard-" + ItemId;
            gameObject.SetActive(true);
        }

        public void LayoutForDpi(float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            _nameLabel.fontSize = _nameLabel.fontSizeMax = 18f * px;
            _nameLabel.fontSizeMin = 16f * px;
            _statusLabel.fontSize = _statusLabel.fontSizeMax = 14f * px;
            _statusLabel.fontSizeMin = 12f * px;
            _priceLabel.fontSize = _priceLabel.fontSizeMax = 16f * px;
            _priceLabel.fontSizeMin = 14f * px;
            _selectionOutline.pixelsPerUnitMultiplier = _paper.pixelsPerUnitMultiplier
                = _priceChip.pixelsPerUnitMultiplier = 1.2f / px;
            var paper = _paper.rectTransform;
            paper.anchorMin = Vector2.zero;
            paper.anchorMax = Vector2.one;
            paper.offsetMin = Vector2.one * 3f * px;
            paper.offsetMax = -paper.offsetMin;
            FitItemRegion();
        }

        private void FitItemRegion()
        {
            Vector2 size = _portraitMount.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            RectTransform portrait = ItemPortrait.RootTransform;
            float side = Mathf.Min(size.x, size.y);
            if (_itemSlot == CosmeticSlot.Outfit)
            {
                // Fit the shipped coat's y=.06..49 region. Use the viewport's short side
                // for its width so one wide tile never stretches its buttons or collar.
                float halfWidth = 0.90f * side / size.x;
                portrait.anchorMin = new Vector2(0.5f - halfWidth, -0.04f);
                portrait.anchorMax = new Vector2(0.5f + halfWidth, 2.06f);
                portrait.offsetMin = portrait.offsetMax = Vector2.zero;
            }
            else
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.5f);
                portrait.sizeDelta = Vector2.one * side * 0.8f;
                portrait.anchoredPosition = Vector2.zero;
            }
        }

        private static RectTransform MakeRect(Transform parent, string name, Vector2 min,
            Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image MakeImage(Transform parent, string name, Vector2 min,
            Vector2 max, Color color)
        {
            var rect = MakeRect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = HudShapeSprites.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.material = UiChromeMaterial.Shared;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text MakeText(Transform parent, string name, Vector2 min,
            Vector2 max, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }
    }
}
