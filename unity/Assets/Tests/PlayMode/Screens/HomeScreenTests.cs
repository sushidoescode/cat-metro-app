using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-06 criteria 3/4/5/6 over the greybox Home, by DIRECT CONSTRUCTION (P-3: no
    // Bootstrap objects — the mount is CM-UX-07's). PlayMode because instantiating render
    // objects in EditMode trips material-leak log noise (the CM-UX-02 R1-F13 precedent).
    // Anti-vacuity law inherited: every absence assert carries a positive control.
    public sealed class HomeScreenTests
    {
        private GameObject _canvasGo;
        private HomeScreenView _home;
        private ChromeRegions _regions;
        private bool _motionOff;

        private HomeScreenView CreateShown()
        {
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _motionOff = false;
            _home = HomeScreenView.Create(canvas.transform);
            _home.Attach(_regions, () => _motionOff);
            _home.Show();
            return _home;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            _canvasGo = null;
            _home = null;
        }

        // --- criterion 3: the session-1 read renders; the pin honors the layout law ---

        [UnityTest]
        public IEnumerator Home_RendersCsvTitle_AndPinHonorsThePureLayoutLaw()
        {
            CreateShown();
            yield return null;
            Assert.That(_home.TitleText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("home.title")),
                "the title resolves through the csv key, never a literal");
            Assert.That(_home.TitleText.Contains("??"), Is.False,
                "the key resolves — not the missing-key sentinel");

            float dpi = Screen.dpi;
            var expected = HomeLayout.PinRect(Screen.safeArea, dpi);
            Assert.That(_home.PinPaintedRectPx, Is.EqualTo(expected),
                "the painted pin rect IS the pure-math rect — no drift possible");
            Assert.That(CatMetro.Presentation.Hud.HudBands.MeetsMinTargetPx(
                _home.PinPaintedRectPx, dpi > 0f ? dpi : 160f), Is.True,
                "the live pin clears the 48dp floor on this host");
        }

        // --- criterion 4: pulse is easing; the ring is information (A11Y-S01-2) ---

        [UnityTest]
        public IEnumerator Pin_Pulses_MotionOn_AndHoldsStill_MotionOff_RingInBothModes()
        {
            CreateShown();
            yield return null;

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 10; i++)
            {
                min = Mathf.Min(min, _home.PinScale);
                max = Mathf.Max(max, _home.PinScale);
                yield return null;
            }
            Assert.That(max - min, Is.GreaterThan(0.0001f),
                "motion ON: the pin scale varies across frames — the pulse exists");
            Assert.That(_home.RingVisible, Is.True,
                "the raised-ring shape twin renders WITH motion on — never motion-only state");

            _motionOff = true;
            yield return null;
            min = float.MaxValue; max = float.MinValue;
            for (int i = 0; i < 10; i++)
            {
                min = Mathf.Min(min, _home.PinScale);
                max = Mathf.Max(max, _home.PinScale);
                yield return null;
            }
            Assert.That(min, Is.EqualTo(1f), "motion OFF locks the scale at rest");
            Assert.That(max, Is.EqualTo(1f), "no easing survives the motion-off delegate");
            Assert.That(_home.RingVisible, Is.True,
                "the ring survives motion-off — easing removed, information kept (P-5)");

            // positive control: the ring flag observes the real tree
            _home.Hide();
            yield return null;
            Assert.That(_home.RingVisible, Is.False, "a hidden Home shows no ring");
        }

        // --- criterion 5: the session-1 structural law + TG-3, decoy-controlled ---

        private static readonly string[] BannedNodeNames =
        {
            // S-01: "No shop entry, no daily entry, no badges rendered in session 1" — plus
            // the monetization-embargo family and BOTH TG-3 Night-Harbor variants (tile absent).
            "shop", "store", "daily", "badge", "streak", "share", "notif",
            "night", "harbor", "access", "paywall", "advert", "reward", "ticket",
        };

        private static string FirstBannedNode(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var name = t.gameObject.name.ToLowerInvariant();
                foreach (var banned in BannedNodeNames)
                    if (name.Contains(banned)) return t.gameObject.name;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator Home_SessionOneStructuralLaw_NoCommerceNodes_DecoyControlled()
        {
            CreateShown();
            yield return null;

            // the walk must be over a tree that actually renders something (anti-vacuity)
            Assert.That(_home.gameObject.GetComponentsInChildren<Transform>(true).Length,
                Is.GreaterThan(1), "the Home tree has real children to walk");

            Assert.That(FirstBannedNode(_home.gameObject), Is.Null,
                "session-1 Home builds NO shop/daily/badge surface and NEITHER TG-3 variant");

            // positive control: a decoy commerce node makes the walk fire
            var decoy = new GameObject("ShopButton");
            decoy.transform.SetParent(_home.transform, false);
            try
            {
                Assert.That(FirstBannedNode(_home.gameObject), Is.EqualTo("ShopButton"),
                    "the structural tripwire detects a commerce node when one exists");
            }
            finally { Object.Destroy(decoy); }
        }

        // --- criterion 6: first registrar — lifetime law + routing seam ---

        [UnityTest]
        public IEnumerator Pin_RegistersExactlyOneRegion_FiresSeam_UnregistersOnHideAndDestroy()
        {
            CreateShown();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1),
                "Home registers exactly ONE region: the pin");

            bool selected = false;
            _home.LevelSelected = () => selected = true;
            var center = _home.PinPaintedRectPx.center;
            Assert.That(_regions.TryResolve(center, out var onTap), Is.True,
                "the pin center resolves to the registered region");
            onTap();
            Assert.That(selected, Is.True, "the region fires the LevelSelected seam");

            // negative + its control in the same fixture: outside the pin resolves nothing
            var outside = new Vector2(
                _home.PinPaintedRectPx.center.x, Screen.height * 0.6f);
            Assert.That(_regions.TryResolve(outside, out _), Is.False,
                "only the pin is a target — parked districts are scenery, not buttons");

            _home.Hide();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0), "Hide unregisters (R1-F3 lifetime law)");

            _home.Show();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1), "re-Show re-registers exactly once");

            Object.Destroy(_home.gameObject);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "OnDestroy unregisters — the R1-F3 law's second half");
        }

        // --- criterion 6: render-only tree, whitelist-walked with positive controls ---

        private static readonly System.Type[] Whitelist =
        {
            typeof(Transform), typeof(RectTransform), typeof(Canvas),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.Image), typeof(TMPro.TextMeshProUGUI),
            typeof(HomeScreenView),
        };

        private static Component FirstOffWhitelist(GameObject root)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                bool ok = false;
                foreach (var t in Whitelist)
                    if (t.IsInstanceOfType(c)) { ok = true; break; }
                if (!ok) return c;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator HomeTree_IsRenderOnly_ZeroInteractivity_ZeroAnimationComponents()
        {
            CreateShown();
            yield return null;

            var off = FirstOffWhitelist(_home.gameObject);
            Assert.That(off, Is.Null, "render-side types only under Home; found: "
                + (off ? off.GetType().FullName : ""));
            Assert.That(_home.GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length,
                Is.EqualTo(0), "no Selectable under Home — hits route through ChromeRegions");
            Assert.That(_home.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true)
                .Length, Is.EqualTo(0), "no raycaster-driven interactivity");
            Assert.That(_home.GetComponentsInChildren<Animator>(true).Length
                + _home.GetComponentsInChildren<Animation>(true).Length, Is.EqualTo(0),
                "the pulse is code-driven easing — zero animation components");

            // positive controls: both detectors detect
            var decoy = new GameObject("decoy-tree");
            try
            {
                decoy.AddComponent<UnityEngine.UI.Button>();
                decoy.AddComponent<Animator>();
                Assert.That(FirstOffWhitelist(decoy), Is.Not.Null);
                Assert.That(decoy.GetComponentsInChildren<Animator>(true).Length,
                    Is.EqualTo(1));
            }
            finally { Object.Destroy(decoy); }
        }
    }
}
