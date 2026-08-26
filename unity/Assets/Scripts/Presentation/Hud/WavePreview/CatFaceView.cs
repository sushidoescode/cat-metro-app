using UnityEngine;
using UnityEngine.UI;
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
    // Pure UGUI, and now pure Image: every part of the face is a tinted sprite drawing through
    // CanvasRenderer, so nothing here is a Renderer and nothing here can cast a shadow into the
    // diorama. The one TextMeshProUGUI this class used to own — the letter inside the badge —
    // is gone; see Bind for the arithmetic that removed it.
    public sealed class CatFaceView : MonoBehaviour
    {
        // Fractions of the face box, MEASURED off docs/reference/target-01-tabletop.png rather
        // than guessed. Reading the three faces out of that render by connected component:
        // heads span 113/105/95 px wide in a 221px-tall capsule (mean 0.47 of capsule height),
        // and the badges are 53/49/57 px (mean 0.23 of capsule height) centred +55,-49 px from
        // the head centre. Divided through by this file's face box — 0.62 of capsule height —
        // that is head 0.76, badge 0.37, badge offset (0.40, -0.354).
        private const float HeadSize = 0.76f;

        // Ears, also measured rather than guessed, because the old numbers drew DEVIL HORNS —
        // visible in the validation capture and unmistakable once the face was rendered at
        // phone size. Scanning the reference red cat row by row, the ears surface at y=140 as
        // two 10px stubs and merge into the head at y=152, so against its 110px head each ear
        // is a base 0.27 of the head diameter, centred 0.29 of the head diameter off centre,
        // clearing the crown by only ~0.09 of it, and tilted barely at all. The old values were
        // a 0.41 base tilted 18 degrees: too wide, too splayed, and the tilt drove the inner
        // edge down into the crown so the silhouette notched into a pair of spikes.
        private const float EarSize = 0.24f;
        private const float EarInset = 0.22f;
        private const float EarRise = 0.343f;
        private const float EarTilt = 12f;
        private const float EyeSize = 0.088f;
        private const float EyeSpread = 0.15f;
        private const float EyeRise = 0.035f;
        private const float MuzzleSize = 0.07f;
        private const float MuzzleDrop = 0.106f;
        private const float BadgeSize = 0.37f;
        private const float BadgeRingScale = 1.26f;

        // The badge sits OUTSIDE the head, tucked at its lower right — the single biggest
        // legibility win in this file. It used to be one BadgeOffset of 0.30 on both axes with
        // a 0.46 badge against a 0.86 head, which put the badge CENTRE (0.424 of the face box)
        // essentially on the head's own edge (0.43): more than half the badge lay on the face,
        // and on a 917x2048 phone that is a 39px badge sunk 20px into a 74px head. The cream
        // ring plus the letter that used to sit inside it then read as a registered-trademark
        // mark rather than a destination symbol, which is exactly what the validation capture
        // showed.
        //
        // These two are chosen so the badge FILL clears the head disc outright:
        //   |offset| = sqrt(0.44^2 + 0.40^2) = 0.5946
        //   head radius + badge radius = 0.38 + 0.185 = 0.565
        // leaving 0.0296 of the face box — 2.5px at the phone frame — of daylight between them.
        // The target art is fractionally tighter than this (0.534, a few px of overlap); the
        // extra separation is deliberate, because the art is a 1536px render and this has to
        // survive at a third of that. The cream RING still kisses the head by ~1.7px, which is
        // what the art does and what keeps the badge attached to its cat rather than floating.
        private const float BadgeOffsetX = 0.44f;
        private const float BadgeOffsetY = 0.40f;

        private RectTransform _rect;
        private Image _head;
        private Image _earLeft;
        private Image _earRight;
        private Image _badge;
        private Image _badgeRing;
        private Image _eyeLeft;
        private Image _eyeRight;
        private Image _muzzle;

        public string ColorName { get; private set; } = "";
        public Color HeadColor => _head != null ? _head.color : UnityEngine.Color.clear;
        public Color BadgeColor => _badge != null ? _badge.color : UnityEngine.Color.clear;
        public Color EarColor => _earLeft != null ? _earLeft.color : UnityEngine.Color.clear;
        public DestinationShape Shape { get; private set; }

        // The letter this line is known by. The face KNOWS its letter and any surface that
        // wants to stamp one can ask — the BOARD does, on its station plates. The HUD badge
        // deliberately does NOT paint it any more: see the badge comment in Bind.
        public string Glyph => CatLine.GlyphOf(ColorName);

        // The badge's rasterised symbol. This is the channel that carries destination identity
        // WITHOUT colour, so a test can assert two lines differ here even when a viewer cannot
        // tell SignalRed from GardenGreen.
        public Sprite BadgeSprite => _badge != null ? _badge.sprite : null;

        // Named FaceRect, not Rect: a member called Rect would shadow the UnityEngine.Rect
        // TYPE inside this class the moment anyone here needs one.
        public RectTransform FaceRect => _rect;
        public RectTransform HeadRect => _head != null ? _head.rectTransform : null;
        public RectTransform BadgeRect => _badge != null ? _badge.rectTransform : null;

        // The badge-vs-head separation law, as pure arithmetic on the face box so a test can
        // assert it without laying anything out. Positive means daylight between the two fills.
        public static float BadgeClearance(float faceSizePx)
        {
            float offset = Mathf.Sqrt(BadgeOffsetX * BadgeOffsetX + BadgeOffsetY * BadgeOffsetY);
            return (offset - (HeadSize + BadgeSize) * 0.5f) * faceSizePx;
        }

        public static float HeadDiameter(float faceSizePx) => HeadSize * faceSizePx;
        public static float BadgeDiameter(float faceSizePx) => BadgeSize * faceSizePx;

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
            // Ears take the vocabulary colour EXACTLY — no darkened variant. They briefly had
            // a Lerp toward InkNavy for form separation, which was solving a problem that does
            // not exist: the ears poke ABOVE the head circle, so they read against the cream
            // capsule by silhouette, and the target art draws them the same colour as the head
            // anyway. It also would have been a real hazard — a tinted line colour is no
            // longer CatLine.ColorOf(name), and the board lane sweeps for exactly that
            // equality. Every surface here that carries line identity is the token itself.
            _earLeft.color = tint;
            _earRight.color = tint;

            _badge.color = tint;
            _badge.sprite = HudShapeSprites.ForShape(Shape);
            _badgeRing.sprite = HudShapeSprites.ForShape(Shape);

            // NO LETTER on the badge, and that is a deliberate reversal. target-01 draws these
            // badges as bare shapes — a red circle, a blue square, an orange triangle — and the
            // arithmetic says why. At the pinned 917x2048 frame the badge is a 31.7px box, so a
            // bold cap letter inside it lands about 22px tall with a ~4px stroke, drawn in
            // WarmPaper on a saturated fill that is itself ringed in WarmPaper about 8px away.
            // Two concentric cream marks that close together stop reading as "letter on a disc"
            // and start reading as a registered-trademark glyph; the validation capture shows
            // precisely that. Below roughly 4px of stroke there is nothing to recover.
            //
            // Dropping it does NOT put identity back on colour alone, which is the rule that
            // actually matters here. The badge SHAPE is a full non-colour channel — circle,
            // square, triangle, diamond, star, one per line, straight out of CatLine.ShapeOf —
            // and at 31.7px every one of those is far above the detail floor. The letter
            // channel still exists where it has room to work: the board stamps it on station
            // plates at world scale. Glyph above still reports it for anyone who needs it.
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
            var badgeCentre = new Vector2(sizePx * BadgeOffsetX, -sizePx * BadgeOffsetY);
            Place(_badge.rectTransform, badgeCentre, new Vector2(badge, badge));
            Place(_badgeRing.rectTransform, badgeCentre,
                new Vector2(badge * BadgeRingScale, badge * BadgeRingScale));
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
