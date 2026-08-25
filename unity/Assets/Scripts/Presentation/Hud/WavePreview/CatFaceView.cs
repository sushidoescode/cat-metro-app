using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Hud.WavePreview
{
    // HUD-WAVE: one upcoming cat, drawn as the target art draws it — a tinted round head with
    // two ears and a simple face, carrying a destination badge on its lower right.
    //
    // Deliberately a CHEAP procedural vocabulary, not character art: two triangles, three discs
    // and a badge. A sibling branch (feat/cats-on-trains) is separately solving "make a chibi
    // cat read at small scale" for the 3D board cats; this is not an attempt to pre-empt that
    // answer, and if the two disagree the 3D one should win and this should follow it.
    //
    // Pure UGUI: Image + TextMeshProUGUI draw through CanvasRenderer, so nothing here is a
    // Renderer and nothing here can cast a shadow into the diorama.
    public sealed class CatFaceView : MonoBehaviour
    {
        // Fractions of the face box, tuned against docs/reference/target-01-tabletop.png.
        private const float HeadSize = 0.86f;
        private const float EarSize = 0.40f;
        private const float EarInset = 0.22f;
        private const float EarRise = 0.30f;
        private const float EarTilt = 18f;
        private const float EyeSize = 0.10f;
        private const float EyeSpread = 0.17f;
        private const float EyeRise = 0.04f;
        private const float MuzzleSize = 0.08f;
        private const float MuzzleDrop = 0.12f;
        private const float BadgeSize = 0.46f;
        private const float BadgeRingScale = 1.26f;
        private const float BadgeOffset = 0.30f;

        private RectTransform _rect;
        private Image _head;
        private Image _earLeft;
        private Image _earRight;
        private Image _badge;
        private Image _badgeRing;
        private Image _eyeLeft;
        private Image _eyeRight;
        private Image _muzzle;
        private TMP_Text _glyph;

        public string ColorName { get; private set; } = "";
        public Color HeadColor => _head != null ? _head.color : UnityEngine.Color.clear;
        public Color BadgeColor => _badge != null ? _badge.color : UnityEngine.Color.clear;
        public DestinationShape Shape { get; private set; }
        public string Glyph => _glyph != null ? _glyph.text : "";
        public RectTransform Rect => _rect;

        public static CatFaceView Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CatFaceView>();
            view._rect = go.AddComponent<RectTransform>();

            // Ears first so the head draws over their bases — sibling order IS draw order in
            // UGUI, which is the whole reason there is no ear-base seam to hide.
            view._earLeft = AddImage(go.transform, "earL", HudShapeSprites.Triangle);
            view._earRight = AddImage(go.transform, "earR", HudShapeSprites.Triangle);
            view._head = AddImage(go.transform, "head", HudShapeSprites.Disc);
            var eyeLeft = AddImage(go.transform, "eyeL", HudShapeSprites.Disc);
            var eyeRight = AddImage(go.transform, "eyeR", HudShapeSprites.Disc);
            var muzzle = AddImage(go.transform, "muzzle", HudShapeSprites.Disc);
            eyeLeft.color = Palette.InkNavy;
            eyeRight.color = Palette.InkNavy;
            muzzle.color = Palette.InkNavy;

            // Keyline first, badge over it — the target's badges all sit on a cream ring so
            // they stay legible against a same-hue head.
            view._badgeRing = AddImage(go.transform, "badgeRing", HudShapeSprites.Disc);
            view._badgeRing.color = Palette.WarmPaper;
            view._badge = AddImage(go.transform, "badge", HudShapeSprites.Disc);

            var glyphGo = new GameObject("glyph");
            glyphGo.transform.SetParent(go.transform, false);
            glyphGo.AddComponent<RectTransform>();
            view._glyph = glyphGo.AddComponent<TextMeshProUGUI>();
            view._glyph.alignment = TextAlignmentOptions.Center;
            view._glyph.enableWordWrapping = false;
            view._glyph.enableAutoSizing = true;
            view._glyph.fontSizeMin = 6f;
            view._glyph.fontSizeMax = 48f;
            view._glyph.fontStyle = FontStyles.Bold;
            view._glyph.color = Palette.WarmPaper;
            view._glyph.raycastTarget = false;

            view._eyeLeft = eyeLeft;
            view._eyeRight = eyeRight;
            view._muzzle = muzzle;
            return view;
        }

        // Read-only binding: the caller hands the colour NAME off the wave DTO, never a Color.
        public void Bind(string color)
        {
            ColorName = color ?? "";
            var tint = CatLine.ColorOf(ColorName);
            Shape = CatLine.ShapeOf(ColorName);

            _head.color = tint;
            // Ears sit a shade deeper so they read as separate forms against the head at the
            // ~40px the capsule actually gives a face on a phone.
            var earTint = UnityEngine.Color.Lerp(tint, Palette.InkNavy, 0.18f);
            _earLeft.color = earTint;
            _earRight.color = earTint;

            _badge.color = tint;
            _badge.sprite = HudShapeSprites.ForShape(Shape);
            _badgeRing.sprite = HudShapeSprites.ForShape(Shape);
            _glyph.text = CatLine.GlyphOf(ColorName);
        }

        // Pure placement, driven entirely by the face box size the capsule allocates.
        public void LayoutAt(Vector2 centrePx, float sizePx)
        {
            Place(_rect, centrePx, new Vector2(sizePx, sizePx));

            float ear = sizePx * EarSize;
            Place(_earLeft.rectTransform,
                new Vector2(-sizePx * EarInset, sizePx * EarRise), new Vector2(ear, ear));
            Place(_earRight.rectTransform,
                new Vector2(sizePx * EarInset, sizePx * EarRise), new Vector2(ear, ear));
            _earLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, EarTilt);
            _earRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -EarTilt);

            float head = sizePx * HeadSize;
            Place(_head.rectTransform, Vector2.zero, new Vector2(head, head));

            float eye = sizePx * EyeSize;
            Place(_eyeLeft.rectTransform,
                new Vector2(-sizePx * EyeSpread, sizePx * EyeRise), new Vector2(eye, eye));
            Place(_eyeRight.rectTransform,
                new Vector2(sizePx * EyeSpread, sizePx * EyeRise), new Vector2(eye, eye));

            float muzzle = sizePx * MuzzleSize;
            Place(_muzzle.rectTransform,
                new Vector2(0f, -sizePx * MuzzleDrop), new Vector2(muzzle, muzzle));

            float badge = sizePx * BadgeSize;
            var badgeCentre = new Vector2(sizePx * BadgeOffset, -sizePx * BadgeOffset);
            Place(_badge.rectTransform, badgeCentre, new Vector2(badge, badge));
            Place(_badgeRing.rectTransform, badgeCentre,
                new Vector2(badge * BadgeRingScale, badge * BadgeRingScale));
            Place((RectTransform)_glyph.transform, badgeCentre, new Vector2(badge, badge));
        }

        // Children are centre-anchored inside the face box, so a child's anchoredPosition is
        // simply its offset from the face centre and the whole face scales from one number.
        private static void Place(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            var mat = UiChromeMaterial.Shared;
            if (mat != null) image.material = mat;
            // Render-only by law: the wave preview has ZERO interactive elements, and a UGUI
            // raycast target is exactly the kind of invisible one that creeps in.
            image.raycastTarget = false;
            return image;
        }
    }
}
