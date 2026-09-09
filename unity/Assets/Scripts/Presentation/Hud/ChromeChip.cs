using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Hud
{
    // Shared paint only. The owner keeps its existing band registration and drives layout
    // and animation; no component, input target, material instance or hidden Update is added.
    public sealed class ChromeChip
    {
        public const float HeightDp = 60f;
        public const float MaxWidthDp = 320f;
        public const float SideInsetDp = 20f;
        public const float BottomBreathDp = 24f;

        private readonly Image _halo;
        private readonly Image _shadow;
        private readonly Image _ring;
        private readonly Image _stitches;
        private readonly Image _icon;
        private readonly RectTransform _content;
        private Color _ringColour;
        private float _opacity = 1f;
        private float _pxPerDp;

        public RectTransform Root { get; }
        public Image Face { get; }
        public TMP_Text Label { get; }
        public Rect FaceRectPx { get; private set; }

        private ChromeChip(Transform parent, string label, Color ringColour, Sprite icon)
        {
            Root = MakeRect(parent, "ChromeChip");
            _shadow = MakeImage(Root, "Shadow", HudShapeSprites.SoftRoundedHalo);
            _halo = MakeImage(Root, "Halo", HudShapeSprites.SoftRoundedHalo);
            _ring = MakeImage(Root, "Ring", HudShapeSprites.RoundedSquare);
            Face = MakeImage(Root, "Face", HudShapeSprites.RoundedSquare);
            _stitches = MakeImage(Root, "Stitches", HudShapeSprites.DashedRoundedRing);
            _content = MakeRect(Root, "Content");
            if (icon != null)
            {
                _icon = MakeImage(_content, "Icon", icon);
                _icon.type = Image.Type.Simple;
                _icon.preserveAspect = true;
            }
            // TMP must run Awake before preferred-width measurement, including when the
            // owner is hidden (for example Home unlocking its Daily pin during a win).
            var labelGo = new GameObject("Label", typeof(RectTransform));
            Label = labelGo.AddComponent<TextMeshProUGUI>();
            labelGo.transform.SetParent(_content, false);
            Label.text = label ?? "";
            Label.alignment = TextAlignmentOptions.Center;
            Label.enableWordWrapping = false;
            Label.overflowMode = TextOverflowModes.Ellipsis;
            Label.raycastTarget = false;
            _ringColour = ringColour;
            SetOpacity(1f);
        }

        public static ChromeChip PaintPrimary(Transform parent, Rect bandPx, string label,
            Color ringColour, Sprite iconSprite = null) =>
            PaintPrimary(parent, bandPx, label, ringColour, iconSprite, Screen.dpi);

        public static ChromeChip PaintPrimary(Transform parent, Rect bandPx, string label,
            Color ringColour, Sprite iconSprite, float dpi)
        {
            var chip = new ChromeChip(parent, label, ringColour, iconSprite);
            chip.Layout(bandPx, dpi);
            return chip;
        }

        public static Rect PrimaryFaceRect(Rect bandPx, float dpi)
        {
            float scale = HudBands.PxPerDp(dpi);
            float width = Mathf.Min(MaxWidthDp * scale,
                Mathf.Max(0f, bandPx.width - 2f * SideInsetDp * scale));
            float height = Mathf.Min(HeightDp * scale, Mathf.Max(0f, bandPx.height));
            float breath = Mathf.Min(BottomBreathDp * scale,
                Mathf.Max(0f, bandPx.height - height));
            return new Rect(bandPx.center.x - width * .5f, bandPx.y + breath, width, height);
        }

        public void Layout(Rect bandPx, float dpi) => LayoutFace(PrimaryFaceRect(bandPx, dpi), dpi);

        // Home and Wardrobe already own stacked-pin placement. They can supply that face rect
        // directly while using exactly the same paint and measured icon/label group.
        public void LayoutFace(Rect facePx, float dpi)
        {
            FaceRectPx = facePx;
            _pxPerDp = HudBands.PxPerDp(dpi);
            Root.anchorMin = Root.anchorMax = Vector2.zero;
            Root.pivot = new Vector2(.5f, .5f);
            Root.anchoredPosition = facePx.center;
            Root.sizeDelta = facePx.size;
            Stretch(_shadow.rectTransform, 6f * _pxPerDp, new Vector2(0f, -4f * _pxPerDp));
            Stretch(_halo.rectTransform, 6f * _pxPerDp, Vector2.zero);
            Stretch(_ring.rectTransform, 2f * _pxPerDp, Vector2.zero);
            Stretch(Face.rectTransform, 0f, Vector2.zero);
            Stretch(_stitches.rectTransform, -7f * _pxPerDp, Vector2.zero);
            _stitches.sprite = HudShapeSprites.DashedRoundedRingForWidth(
                facePx.width / _pxPerDp - 14f);
            _shadow.pixelsPerUnitMultiplier = _halo.pixelsPerUnitMultiplier = 1.4f / _pxPerDp;
            _ring.pixelsPerUnitMultiplier = Face.pixelsPerUnitMultiplier = .6f / _pxPerDp;
            _stitches.pixelsPerUnitMultiplier = 2f / _pxPerDp;
            LayoutContent();
        }

        public void SetLabel(string label)
        {
            Label.text = label ?? "";
            LayoutContent();
        }

        public void SetRingColour(Color colour)
        {
            _ringColour = colour;
            SetOpacity(_opacity);
        }

        public void SetOpacity(float opacity)
        {
            _opacity = Mathf.Clamp01(opacity);
            _shadow.color = Palette.WithAlpha(Palette.DepotNavy, .24f * _opacity);
            _halo.color = Palette.WithAlpha(_ringColour, .4f * _opacity);
            _ring.color = Palette.WithAlpha(_ringColour, _opacity);
            Face.color = Palette.WithAlpha(Palette.CreamCard, _opacity);
            _stitches.color = Palette.WithAlpha(Palette.InkNavy, .46f * _opacity);
            Label.color = Palette.WithAlpha(Palette.InkNavy, _opacity);
            if (_icon != null) _icon.color = Palette.WithAlpha(Palette.InkNavy, _opacity);
        }

        private void LayoutContent()
        {
            float iconSize = _icon != null ? 26f * _pxPerDp : 0f;
            float gap = _icon != null && !string.IsNullOrEmpty(Label.text) ? 10f * _pxPerDp : 0f;
            float available = Mathf.Max(0f, FaceRectPx.width - 40f * _pxPerDp - iconSize - gap);
            TypeScale.Apply(Label, 24f, _pxPerDp * HudBands.FallbackDpi);
            // Measure the icon/label group at the title size, then leave fitting live:
            // owners can inset the label again to reserve space for a status subtitle.
            Label.enableAutoSizing = false;
            float preferred = Label.GetPreferredValues(Label.text).x;
            if (preferred > available && preferred > 0f)
                Label.fontSize = Mathf.Max(Label.fontSizeMin, Label.fontSize * available / preferred);
            float labelWidth = Mathf.Min(available, Label.GetPreferredValues(Label.text).x);
            _content.anchorMin = _content.anchorMax = new Vector2(.5f, .5f);
            _content.pivot = new Vector2(.5f, .5f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(iconSize + gap + labelWidth, FaceRectPx.height);
            if (_icon != null)
                PlaceInContent(_icon.rectTransform, iconSize * .5f, iconSize, iconSize);
            PlaceInContent(Label.rectTransform, iconSize + gap + labelWidth * .5f,
                labelWidth, FaceRectPx.height);
            Label.enableAutoSizing = true;
        }

        private static void PlaceInContent(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image MakeImage(Transform parent, string name, Sprite sprite)
        {
            var rect = MakeRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.material = UiChromeMaterial.Shared;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, float grow, Vector2 offset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-grow, -grow) + offset;
            rect.offsetMax = new Vector2(grow, grow) + offset;
        }
    }
}
