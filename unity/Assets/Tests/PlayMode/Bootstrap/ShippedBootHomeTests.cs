using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;

namespace CatMetro.Tests.PlayMode
{
    // CM-BOOT-HOME: the frozen contract's RED-first fixture — every test here drives the REAL
    // GameRoot.Launch() seam (never LaunchWith, never a hand-set delegate) with the dev skip-
    // hatch (GameRoot.DevSkipShippedHome) left at its default false, so these prove the actual
    // shipped topology: Home composed OVER L001, the sim held at tick 0 until the first Play
    // tap, and the promoted Home still honoring every pre-existing session-1 law (S-01
    // commerce-free, render-only tree, real motion-off binding).
    public sealed class ShippedBootHomeTests
    {
        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            // Static-field hygiene (the BootToHome precedent this whole suite follows): make
            // sure no earlier fixture left the dev skip-hatch on, which would silently turn
            // every test below into a no-op (Home never composing, everything vacuously true).
            GameRoot.DevSkipShippedHome = false;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false;
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        // --- item 1: shipped boot composes Home ---

        [UnityTest]
        public IEnumerator ShippedBoot_ComposesHome_ScreensVisibleTrue_BreadcrumbHome_LevelL001()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.ScreensVisible, Is.True,
                "criterion 1: shipped boot (the real Launch() seam, skip-hatch off) composes Home");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True, "Home shown on boot");
            Assert.That(_root.Stack, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"),
                "Home composes OVER the level, not instead of it — L001 is already loaded "
                + "underneath by the time Home shows");
        }

        // --- item 2 (the primary correctness proof): the tick-0 hold ---

        [UnityTest]
        public IEnumerator TickHeldAtZero_WhileHomeShown_ThenAdvances_OncePlayTapDrainsTheStack()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Session.State.Tick, Is.EqualTo(0), "tick 0 immediately after boot");
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                    "criterion 2: the sim must not advance behind Home before the first Play "
                    + "tap (L001 must never auto-run/fail) — pumped frame " + i);
            }

            // simulate the Play tap path: pin -> Intro -> Play (the real Home/Intro handlers,
            // never a hand-invoked delegate)
            int pinTap = _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            Assert.That(pinTap, Is.EqualTo(-3), "the pin is a chrome region");
            yield return null;
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                "still held — Intro is up now, ScreensVisible is still true");

            int playTap = _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            Assert.That(playTap, Is.EqualTo(-3), "the Play chip is a chrome region");
            Assert.That(_root.ScreensVisible, Is.False, "the stack drained on the Play tap");

            Time.timeScale = 8f;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (_root.Session.State.Tick == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.Session.State.Tick, Is.GreaterThan(0),
                "criterion 2: the sim resumes from tick 0 once the Play tap drains the stack — "
                + "no sim leakage behind Home, and no permanent stall after it clears");
        }

        // --- item 3: the full round trip ---

        [UnityTest]
        public IEnumerator RoundTrip_BootHome_PinTap_IntroVisible_PlayTap_ScreensGone_SimAdvancing()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.ScreensVisible, Is.True);
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Intro.IsVisible, Is.False, "Intro not shown yet");

            int pinTap = _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            Assert.That(pinTap, Is.EqualTo(-3), "the pin is a chrome region");
            Assert.That(_root.Intro.IsVisible, Is.True, "Intro visible after the pin tap");
            Assert.That(_root.Home.IsVisible, Is.False, "Home hides once Intro shows");
            CollectionAssert.AreEqual(new[] { "home", "intro" }, _root.Stack.ToBreadcrumb());

            int playTap = _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            Assert.That(playTap, Is.EqualTo(-3), "the Play chip is a chrome region");
            Assert.That(_root.Intro.IsVisible, Is.False);
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.ScreensVisible, Is.False, "screens gone — the stack popped to empty");
            CollectionAssert.AreEqual(new string[0], _root.Stack.ToBreadcrumb());

            var discAgain = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(discAgain), Is.EqualTo(0),
                "board input is live once the screens clear");

            Time.timeScale = 8f;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (_root.Session.State.Tick == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.Session.State.Tick, Is.GreaterThan(0),
                "the sim is genuinely advancing now that the screens are gone");
        }

        // --- item 4: tripwire-clean + whitelist-clean THROUGH shipped boot (reuses
        // HomeScreenTests.cs's own BannedNodeNames/whitelist walk PATTERN — duplicated here
        // rather than shared, since those arrays are private to that file and this fixture
        // deliberately walks the tree produced by the REAL GameRoot.Launch() seam, not a
        // directly-constructed HomeScreenView) ---

        private static readonly string[] BannedNodeNames =
        {
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

        private static readonly System.Type[] Whitelist =
        {
            typeof(Transform), typeof(RectTransform), typeof(Canvas),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.Image), typeof(TextMeshProUGUI),
            typeof(CatMetro.Presentation.Screens.HomeScreenView),
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
        public IEnumerator ShippedBootHome_TripwireClean_NoCommerceNodes_WhitelistClean_RenderOnly()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.gameObject.GetComponentsInChildren<Transform>(true).Length,
                Is.GreaterThan(1), "the composed Home tree has real children to walk");

            Assert.That(FirstBannedNode(_root.Home.gameObject), Is.Null,
                "S-01 / criterion 5 (commerce-free): the SHIPPED-BOOT Home builds NO shop/"
                + "daily/badge surface and neither TG-3 variant");
            var off = FirstOffWhitelist(_root.Home.gameObject);
            Assert.That(off, Is.Null, "render-side types only under the shipped-boot Home; "
                + "found: " + (off != null ? off.GetType().FullName : ""));
            Assert.That(_root.Home.GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length,
                Is.EqualTo(0), "no Selectable under the shipped-boot Home — hits route through "
                + "ChromeRegions");
            Assert.That(_root.Home.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true)
                .Length, Is.EqualTo(0), "no raycaster-driven interactivity");
            Assert.That(_root.Home.GetComponentsInChildren<Animator>(true).Length
                + _root.Home.GetComponentsInChildren<Animation>(true).Length, Is.EqualTo(0),
                "the pulse is code-driven easing — zero animation components");

            // positive controls (anti-vacuity, the HomeScreenTests precedent): both detectors
            // detect on a decoy, proving the absences above are real, not a walk that never runs
            var bannedDecoy = new GameObject("ShopButton");
            bannedDecoy.transform.SetParent(_root.Home.transform, false);
            try
            {
                Assert.That(FirstBannedNode(_root.Home.gameObject), Is.EqualTo("ShopButton"),
                    "the structural tripwire detects a commerce node when one exists");
            }
            finally { Object.Destroy(bannedDecoy); }

            var offDecoy = new GameObject("decoy-off-whitelist");
            try
            {
                offDecoy.AddComponent<UnityEngine.UI.Button>();
                offDecoy.AddComponent<Animator>();
                Assert.That(FirstOffWhitelist(offDecoy), Is.Not.Null,
                    "the whitelist walk detects an off-whitelist component when one exists");
            }
            finally { Object.Destroy(offDecoy); }
        }

        // --- item 5: motion-off through the real GameRoot.MotionOff binding ---

        [UnityTest]
        public IEnumerator MotionOffToggle_ThroughRealGameRootBinding_LocksPinScaleAt1_RingVisible()
        {
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // the P-3 injection style — set on the REAL root
            yield return null;

            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 10; i++)
            {
                min = Mathf.Min(min, _root.Home.PinScale);
                max = Mathf.Max(max, _root.Home.PinScale);
                yield return null;
            }
            Assert.That(min, Is.EqualTo(1f),
                "GameRoot's real MotionOff binding (Home.Attach(..., () => MotionOff)) locks "
                + "the pin scale at rest — no test-side override");
            Assert.That(max, Is.EqualTo(1f));
            Assert.That(_root.Home.RingVisible, Is.True,
                "the ring survives motion-off — easing removed, information kept (P-5)");
        }

        // --- item 6: Q-5, restated for the CM-BOOT-HOME topology ---

        [UnityTest]
        public IEnumerator Q5Restated_ShippedBootToHome_CurrentLevelIdStillL001()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.ScreensVisible, Is.True, "precondition: Home is genuinely up — "
                + "otherwise this pin cannot tell apart 'L001 underneath Home' from 'L001 "
                + "because Home never composed'");
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"),
                "Q-5, restated (frozen contract): Home is the shipped launch screen, but the "
                + "LEVEL underneath still lands at L001, exactly as CM-LOADNEXT criterion 6 "
                + "pinned before this contract superseded ONLY the 'no Home' half of that law");
        }
    }
}
