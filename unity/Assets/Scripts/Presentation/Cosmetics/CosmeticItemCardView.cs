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
            view._selectionOutline.material = UiChromeMaterial.Shared;
            view._selectionOutline.raycastTarget = false;

            MakeImage(root.transform, "CardPaper", new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f), Palette.WarmPaper);
            var portraitMount = MakeRect(root.transform, "ItemPortraitMount",
                new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.95f));
            view.ItemPortrait = CosmeticPortraitView.CreateStaticSnapshot(portraitMount,
                profile, "ItemPortrait");
            view._nameLabel = MakeText(root.transform, "ItemNameLabel",
                new Vector2(0.06f, 0.23f), new Vector2(0.94f, 0.40f), 18f,
                Palette.InkNavy, TextAlignmentOptions.Center);
            view._nameLabel.fontStyle = FontStyles.Bold;
            view._statusLabel = MakeText(root.transform, "ItemStatusLabel",
                new Vector2(0.06f, 0.035f), new Vector2(0.94f, 0.23f), 15f,
                Palette.DepotNavy, TextAlignmentOptions.Center);
            view._priceChip = MakeImage(root.transform, "PriceChip",
                new Vector2(0.07f, 0.035f), new Vector2(0.93f, 0.23f), Palette.MetroTeal);
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
            _selectionOutline.color = selected ? Palette.TicketOrange : accent;
            if (_profile != null && item != null)
                ItemPortrait.ApplySnapshot(_profile.PreviewPortrait(selectedCatId,
                    item.Slot, item.Id));
            name = "ItemCard-" + ItemId;
            gameObject.SetActive(true);
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
