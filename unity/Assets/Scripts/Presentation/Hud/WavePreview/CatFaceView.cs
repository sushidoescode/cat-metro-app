using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Cats;
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
        private const float BobFractionOfFace = 0.03f;

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
        private CatMicroMotion _microMotion;
        private System.Func<bool> _motionOffSource;
        private Vector2 _layoutCentre;
        private float _layoutSize;
        private Vector3 _faceScaleBaseline = Vector3.one;
        private Vector3 _eyeLeftScaleBaseline = Vector3.one;
        private Vector3 _eyeRightScaleBaseline = Vector3.one;
        private Quaternion _earLeftRotationBaseline = Quaternion.identity;
        private Quaternion _earRightRotationBaseline = Quaternion.identity;
        private bool _hasLayout;
        private bool _renderCallbackBound;
        private Canvas.WillRenderCanvases _renderCallback;

        public string ColorName { get; private set; } = "";
        public Color HeadColor => _head != null ? _head.color : UnityEngine.Color.clear;
        public Color BadgeColor => _badge != null ? _badge.color : UnityEngine.Color.clear;
        public Color EarColor => _earLeft != null ? _earLeft.color : UnityEngine.Color.clear;
        public DestinationShape Shape { get; private set; }
        public string Glyph => _glyph != null ? _glyph.text : "";
        // Named FaceRect, not Rect: a member called Rect would shadow the UnityEngine.Rect
        // TYPE inside this class the moment anyone here needs one.
        public RectTransform FaceRect => _rect;

        private void Awake()
        {
            // Face names come from this preview's fixed pool (face0..face5), so this cadence
            // remains stable across rebuilds without ever reading a gameplay/session seed.
            _microMotion = new CatMicroMotion(PresentationSeed(gameObject.name));
            _renderCallback = ApplyRuntimeMotion;
        }

        private void OnEnable()
        {
            SubscribeRenderCallback();
            ResetNeutralGeometry();
        }

        private void OnDisable()
        {
            UnsubscribeRenderCallback();
            ResetNeutralGeometry();
        }

        private void OnDestroy()
        {
            UnsubscribeRenderCallback();
        }

        private void SubscribeRenderCallback()
        {
            if (_renderCallbackBound) return;
            if (_renderCallback == null) _renderCallback = ApplyRuntimeMotion;
            Canvas.willRenderCanvases += _renderCallback;
            _renderCallbackBound = true;
        }

        private void UnsubscribeRenderCallback()
        {
            if (!_renderCallbackBound) return;
            Canvas.willRenderCanvases -= _renderCallback;
            _renderCallbackBound = false;
        }

        // WavePreviewStrip can rebuild its authoritative layout in LateUpdate when the session
        // tick changes. Sampling here happens after that layout and immediately before UGUI
        // renders, so the sampled pose is the one the player sees in the current frame.
        private void ApplyRuntimeMotion()
        {
            ApplyVisualTime(Time.unscaledTime);
        }

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
            _glyph.text = CatLine.GlyphOf(ColorName);
        }

        // GameRoot supplies its existing motion preference source once when a preview is
        // composed. A detached face deliberately remains animated, matching the old preview.
        public void BindMotionOff(System.Func<bool> motionOffSource)
        {
            _motionOffSource = motionOffSource;
            if (MotionOff()) ResetNeutralGeometry();
        }

        // Pure placement, driven entirely by the face box size the capsule allocates.
        public void LayoutAt(Vector2 centrePx, float sizePx)
        {
            // Layout is the authority. Clear the previous sampled pose before collecting this
            // layout's baselines so a blink/twitch can never become the next neutral geometry.
            ResetNeutralGeometry();
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

            _layoutCentre = centrePx;
            _layoutSize = sizePx;
            _faceScaleBaseline = _rect.localScale;
            _eyeLeftScaleBaseline = _eyeLeft.rectTransform.localScale;
            _eyeRightScaleBaseline = _eyeRight.rectTransform.localScale;
            _earLeftRotationBaseline = _earLeft.rectTransform.localRotation;
            _earRightRotationBaseline = _earRight.rectTransform.localRotation;
            _hasLayout = true;
            ResetNeutralGeometry();
        }

        // Explicit time is the test/presentation seam. Runtime calls it with unscaled time so
        // the HUD respects the same accessibility motion source without touching sim timing.
        public void ApplyVisualTime(float visualTime)
        {
            if (!_hasLayout || _rect == null) return;

            var motion = _microMotion ?? (_microMotion = new CatMicroMotion(
                PresentationSeed(gameObject.name)));
            var pose = motion.Evaluate(visualTime, MotionOff(), false);
            _rect.anchoredPosition = _layoutCentre
                + Vector2.up * (pose.Bob * _layoutSize * BobFractionOfFace);
            _rect.localScale = _faceScaleBaseline;
            _eyeLeft.rectTransform.localScale = ScaleEye(_eyeLeftScaleBaseline, pose.EyeYScale);
            _eyeRight.rectTransform.localScale = ScaleEye(_eyeRightScaleBaseline, pose.EyeYScale);
            _earLeft.rectTransform.localRotation = _earLeftRotationBaseline
                * Quaternion.Euler(0f, 0f, pose.EarTwitchDegrees);
            _earRight.rectTransform.localRotation = _earRightRotationBaseline
                * Quaternion.Euler(0f, 0f, -pose.EarTwitchDegrees);
        }

        private bool MotionOff() => _motionOffSource != null && _motionOffSource();

        private void ResetNeutralGeometry()
        {
            if (_hasLayout)
            {
                if (_rect != null)
                {
                    _rect.anchoredPosition = _layoutCentre;
                    _rect.localScale = _faceScaleBaseline;
                }
                if (_eyeLeft != null) _eyeLeft.rectTransform.localScale = _eyeLeftScaleBaseline;
                if (_eyeRight != null) _eyeRight.rectTransform.localScale = _eyeRightScaleBaseline;
                if (_earLeft != null) _earLeft.rectTransform.localRotation = _earLeftRotationBaseline;
                if (_earRight != null) _earRight.rectTransform.localRotation = _earRightRotationBaseline;
                return;
            }

            // Creation and pool deactivation can happen before WavePreviewStrip provides a
            // viewport. There is no layout baseline yet, so identity is the only truthful
            // neutral geometry and prevents a pre-layout inactive face inheriting a pose.
            if (_rect != null)
            {
                _rect.anchoredPosition = Vector2.zero;
                _rect.localScale = Vector3.one;
            }
            if (_eyeLeft != null) _eyeLeft.rectTransform.localScale = Vector3.one;
            if (_eyeRight != null) _eyeRight.rectTransform.localScale = Vector3.one;
            if (_earLeft != null) _earLeft.rectTransform.localRotation = Quaternion.identity;
            if (_earRight != null) _earRight.rectTransform.localRotation = Quaternion.identity;
        }

        private static Vector3 ScaleEye(Vector3 baseline, float eyeYScale) =>
            new Vector3(baseline.x, baseline.y * eyeYScale, baseline.z);

        private static uint PresentationSeed(string name)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (name == null) return hash;
                for (int i = 0; i < name.Length; i++)
                {
                    hash ^= name[i];
                    hash *= 16777619u;
                }
                return hash;
            }
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
