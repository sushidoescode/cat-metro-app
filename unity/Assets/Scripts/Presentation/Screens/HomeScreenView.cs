using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criteria 3/4/5/6: the greybox session-1 Home. One pulsing L001 pin (72dp,
    // thumb-band-centered via pure HomeLayout math; live Screen reads only here, the
    // RetryCtaView precedent), parked-district silhouettes, csv-keyed title — and NOTHING
    // else: no shop, no daily, no badges (S-01 layout intent; the tree test is the
    // monetization tripwire), and neither TG-3 variant of the bonus-district tile exists.
    // Render-only by law (P-1): the pin's hit routes through the INJECTED ChromeRegions
    // registry — this view is the tranche's first registrar and honors the R1-F3 lifetime
    // law (unregister on Hide AND OnDestroy). Motion posture (P-5, A11Y-S01-2): the pulse is
    // easing and vanishes under the injected motion-off delegate; the raised-ring shape twin
    // renders in BOTH modes — motion never carries the information.
    public sealed class HomeScreenView : MonoBehaviour
    {
        private const string PinRegionId = "home.pin.l001";
        private const int PinRegionPriority = 0; // explicit per A-UX1-3

        public System.Action LevelSelected;

        private ChromeRegions _regions;
        private System.Func<bool> _motionOff;
        private bool _registered;
        private bool _shown; // #46 review F4: Show()-left-shown intent, survives OnDisable/OnEnable
        private TMP_Text _title;
        private RectTransform _pin;
        private RectTransform _ring;
        private Rect _pinRectPx;
        private float _phase;

        public Rect PinPaintedRectPx => _pinRectPx;
        // #42 review F1: the world-corners read-back seam — tests measure the REAL transform
        // against the painted claim instead of comparing the claim to itself.
        public RectTransform PinTransform => _pin;
        public bool RingVisible => _ring != null && _ring.gameObject.activeInHierarchy;
        public float PinScale => _pin != null ? _pin.localScale.x : 1f;
        public bool IsVisible => gameObject.activeSelf;
        public string TitleText => _title != null ? _title.text : "";

        public static HomeScreenView Create(Transform canvasParent)
        {
            var go = new GameObject("HomeScreen");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<HomeScreenView>();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            view._title = MakeText(go.transform, "Title",
                new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.97f),
                Strings.UiStrings.Get("home.title"), 48f); // key-only, never a literal

            // Parked-district silhouettes: scenery, not buttons (S-01 — curiosity, no locks).
            MakeSilhouette(go.transform, "ParkedDistrictA",
                new Vector2(0.08f, 0.55f), new Vector2(0.46f, 0.72f));
            MakeSilhouette(go.transform, "ParkedDistrictB",
                new Vector2(0.54f, 0.60f), new Vector2(0.92f, 0.78f));
            MakeSilhouette(go.transform, "ParkedDistrictC",
                new Vector2(0.16f, 0.32f), new Vector2(0.62f, 0.48f));

            // The raised-ring shape twin sits BEHIND the pin (sibling order = draw order).
            view._ring = MakeChip(go.transform, "PinRingL001",
                new Color(0.95f, 0.92f, 0.85f, 0.85f));
            view._pin = MakeChip(go.transform, "PinL001",
                new Color(0.13f, 0.19f, 0.29f, 0.95f));

            go.SetActive(false);
            return view;
        }

        private static TMP_Text MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string text, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.color = Color.white;
            return tmp;
        }

        private static void MakeSilhouette(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            var mat = UiChromeMaterial.Shared;
            if (mat != null) img.material = mat;
            img.color = new Color(0.35f, 0.38f, 0.42f, 0.55f); // muted parked scenery
        }

        private static RectTransform MakeChip(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            var mat = UiChromeMaterial.Shared;
            if (mat != null) img.material = mat;
            img.color = color;
            return rect;
        }

        public void Attach(ChromeRegions regions, System.Func<bool> motionOff)
        {
            _regions = regions;
            _motionOff = motionOff; // GameRoot.MotionOff binding is CM-UX-07's (P-3)
        }

        public void Show()
        {
            _shown = true;
            gameObject.SetActive(true);
            LayoutPin();
            RegisterPin();
        }

        public void Hide()
        {
            _shown = false;
            UnregisterPin();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnregisterPin(); // R1-F3 lifetime law
        }

        // CM-UX-07 W-1 (R2-3, audit M-3): mirrors OnDestroy — a deactivated-but-not-destroyed
        // Home (SetActive(false) directly on the host, not through Hide()) must drop its
        // registration too, or a live rect provider survives over a host that stopped posting
        // frames.
        private void OnDisable()
        {
            UnregisterPin();
        }

        // #46 review F4: mirrors OnDisable — a host reactivated directly (SetActive(true), not
        // through Show()) must re-register, or a visible-but-pulsing pin sits inert over an
        // unstartable game (the ghost-affordance asymmetry). Gated on _shown, not on _regions
        // alone: Create()'s AddComponent fires OnEnable transiently (the GameObject starts
        // active by default before Create() parks it inactive) BEFORE Attach()/Show() ever run,
        // and a composed-but-never-shown component must register nothing — boot semantics stay
        // unchanged. Hide() clears _shown, so a bare re-activation after Hide() also registers
        // nothing (Hide()'s "not shown" intent survives OnEnable too).
        private void OnEnable()
        {
            if (_shown) RegisterPin();
        }

        private void RegisterPin()
        {
            if (_regions != null && !_registered)
            {
                _regions.Register(PinRegionId, () => _pinRectPx,
                    () => LevelSelected?.Invoke(), PinRegionPriority);
                _registered = true;
            }
        }

        private void UnregisterPin()
        {
            if (_regions != null && _registered)
            {
                _regions.Unregister(PinRegionId);
                _registered = false;
            }
        }

        // The live binding site (A-UX1-5 law): Screen.safeArea/dpi are read HERE and handed
        // to pure math; px placement via bottom-left anchoring, the RetryCtaView pattern.
        private void LayoutPin()
        {
            var safeArea = Screen.safeArea;
            float dpi = Screen.dpi;
            _pinRectPx = HomeLayout.PinRect(safeArea, dpi);
            ApplyPx(_pin, _pinRectPx);
            ApplyPx(_ring, HomeLayout.RingRect(safeArea, dpi));
        }

        private static void ApplyPx(RectTransform rect, Rect px)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(px.center.x, px.center.y);
            rect.sizeDelta = new Vector2(px.width, px.height);
        }

        // The pulse: code-driven easing only (zero Animator components — the whitelist walk
        // proves it). Motion-off locks the rest pose exactly; the ring twin carries the
        // "available" information in both modes. Time is presentation-only (A-UX6-3) and
        // never enters the sim (P-6).
        private void Update()
        {
            if (_pin == null) return;
            float scale = 1f;
            bool off = _motionOff != null && _motionOff();
            if (!off)
            {
                _phase += Time.unscaledDeltaTime * 5f;
                scale = 1f + 0.08f * Mathf.Sin(_phase);
            }
            _pin.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
