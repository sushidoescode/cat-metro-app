using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Cosmetics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Cosmetics
{
    /// <summary>Paint-only Wardrobe row. Input and authority stay with WardrobeScreenView.</summary>
    public sealed class CosmeticItemCardView : MonoBehaviour
    {
        private TMP_Text _nameLabel;
        private TMP_Text _statusLabel;
        private TMP_Text _priceLabel;
        private Image _selectionRail;
        private Image _itemBadge;

        public RectTransform RootTransform { get; private set; }
        public bool IsActive => gameObject.activeInHierarchy;
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

        public static CosmeticItemCardView Create(Transform parent)
        {
            var root = new GameObject("CosmeticItemCard", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<CosmeticItemCardView>();
            view.RootTransform = (RectTransform)root.transform;

            var background = root.AddComponent<Image>();
            background.color = Palette.WarmPaper;
            background.material = UiChromeMaterial.Shared;
            background.raycastTarget = false;

            view._selectionRail = MakeImage(root.transform, "SelectionRail",
                new Vector2(0.015f, 0.10f), new Vector2(0.045f, 0.90f), Palette.MetroTeal);
            view._itemBadge = MakeImage(root.transform, "ItemBadge", new Vector2(0.07f, 0.19f),
                new Vector2(0.21f, 0.81f), Palette.CreamCard);
            view._nameLabel = MakeText(root.transform, "ItemNameLabel",
                new Vector2(0.24f, 0.48f), new Vector2(0.70f, 0.90f), 25f,
                Palette.InkNavy, TextAlignmentOptions.Left);
            view._nameLabel.fontStyle = FontStyles.Bold;
            view._statusLabel = MakeText(root.transform, "ItemStatusLabel",
                new Vector2(0.24f, 0.10f), new Vector2(0.70f, 0.48f), 19f,
                Palette.DepotNavy, TextAlignmentOptions.Left);
            view._priceLabel = MakeText(root.transform, "ItemPriceLabel",
                new Vector2(0.70f, 0.10f), new Vector2(0.96f, 0.90f), 21f,
                Palette.InkNavy, TextAlignmentOptions.Center);
            return view;
        }

        public void Configure(string itemId, string displayName, string statusText,
            string priceText, CosmeticWardrobeRoute route, bool selected, Color accent)
        {
            ItemId = itemId ?? string.Empty;
            Route = route;
            _nameLabel.text = displayName ?? string.Empty;
            _statusLabel.text = statusText ?? string.Empty;
            _priceLabel.text = priceText ?? string.Empty;
            _selectionRail.color = selected ? Palette.TicketOrange : accent;
            _itemBadge.color = accent;
            name = "ItemCard-" + ItemId;
            gameObject.SetActive(true);
        }

        private static Image MakeImage(Transform parent, string name, Vector2 min,
            Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
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
