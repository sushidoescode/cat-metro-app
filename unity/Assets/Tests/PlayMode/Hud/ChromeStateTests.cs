using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-02 criteria 1/2/3/4/5/7 under the contract's live-wiring + anti-vacuity rule:
    // every absence assert carries a positive control in the same fixture. Criterion 2's
    // transitions use a test-controlled state source over real wiring; criterion 4 drives the
    // REAL halt (the GreyboxTests recipe) with the exact delegate shape CM-UX-07 will bind.
    // Prose in this file is scanned by HaltVocabularyGuardTests — keep banned tokens out.
    public sealed class ChromeStateTests
    {
        private GameRoot _root;
        private string _state; // the test-controlled state source for transition legs

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = 1f;
        }

        private static ImportedLevel Fixture()
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(FixtureJson()));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        private ScreenChromeController AttachControlled(string initial)
        {
            _root = GameRoot.LaunchWith(Fixture());
            _state = initial;
            var chrome = _root.gameObject.AddComponent<ScreenChromeController>();
            chrome.Attach(() => _state);
            return chrome;
        }

        // --- criterion 2: state map proven on TRANSITIONS ---

        [UnityTest]
        public IEnumerator Transition_PlayingToFailureReview_ShowsCtaWithinOneFrame()
        {
            var chrome = AttachControlled("Playing");
            yield return null;
            Assert.That(chrome.Cta == null || !chrome.Cta.IsVisible, Is.True,
                "pre-flip positive control: nothing renders during Playing");
            Assert.That(chrome.Veil == null || !chrome.Veil.IsVisible, Is.True);

            _state = "FailureReview";
            yield return null; // within one pumped frame
            Assert.That(chrome.Cta != null && chrome.Cta.IsVisible, Is.True,
                "the CTA renders on the frame after the state source changes");
            Assert.That(chrome.Veil == null || !chrome.Veil.IsVisible, Is.True,
                "the veil stays hidden on this row of the state map");

            _state = "Playing";
            yield return null;
            Assert.That(chrome.Cta == null || !chrome.Cta.IsVisible, Is.True,
                "returning to Playing hides the CTA again");
        }

        [UnityTest]
        public IEnumerator Transition_WonRendersNeither()
        {
            // CM-UX-04 MAY extend this row to its results panel and may NOT relax the others
            // (bounded supersession, contract criterion 2). R1-M2: the positive control proves
            // this chrome CAN render before the Won row asserts that it doesn't.
            var chrome = AttachControlled("Playing");
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(chrome.Cta != null && chrome.Cta.IsVisible, Is.True,
                "positive control: the chrome demonstrably renders");
            _state = "Won";
            yield return null;
            Assert.That(chrome.Cta.IsVisible, Is.False, "Won renders no CTA");
            Assert.That(chrome.Veil == null || !chrome.Veil.IsVisible, Is.True,
                "Won renders no veil");
        }

        // R1-H1: the shipped layout and the criterion-3 geometry table are JOINED here — the
        // painted rect the component actually applied equals the safe-area thumb band, and the
        // 48dp floor holds on its live tappable intersection with the raw band.
        [UnityTest]
        public IEnumerator Cta_PaintedRect_IsTheSafeAreaThumbBand_AndTappableFloorHolds()
        {
            var chrome = AttachControlled("Playing");
            yield return null;
            _state = "FailureReview";
            yield return null;

            var expected = CatMetro.Presentation.Hud.HudBands.ThumbBand(Screen.safeArea);
            var painted = chrome.Cta.PaintedRectPx;
            Assert.That(painted, Is.EqualTo(expected),
                "the component's applied rect IS the safe-area thumb band — no drift possible");

            var rawBand = new Rect(0f, 0f, Screen.width, Screen.height * 0.25f);
            var tappable = CatMetro.Presentation.Hud.ChromeGeometry.TappableRect(painted, rawBand);
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            Assert.That(CatMetro.Presentation.Hud.HudBands.MeetsMinTargetPx(tappable, dpi),
                Is.True, "the live tappable rect clears the 48dp floor on this host");
        }

        // --- criterion 4: the REAL halt renders the veil (F-DEV-4 ends here) ---

        [UnityTest]
        public IEnumerator RealHalt_RendersTheVeil_WithTheWiringDelegateShape()
        {
            _root = GameRoot.Launch(); // L001 through the real seam
            var chrome = _root.gameObject.AddComponent<ScreenChromeController>();
            chrome.Attach(() => _root.ScreenState); // the EXACT shape CM-UX-07 binds
            // the merged recipe expects the halt's by-design loud error (GreyboxTests:155)
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "run halted at a pinned/guarded Domain boundary"));
            Time.timeScale = 8f;

            // pre-halt positive control: nothing renders while Playing
            yield return null;
            Assert.That(chrome.Veil == null || !chrome.Veil.IsVisible, Is.True,
                "no veil during live play");

            // the GreyboxTests recipe: tap nothing; L001 reaches the pinned boundary
            float deadline = Time.realtimeSinceStartup + 60f;
            while (_root.ScreenState != "Halted" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Halted"),
                "the recipe must reach the real halt within the deadline");

            yield return null; // within one pumped frame of the real transition
            Assert.That(chrome.Veil != null && chrome.Veil.IsVisible, Is.True,
                "the halt is VISIBLE — the F-DEV-4 criterion");
            Assert.That(chrome.Veil.RenderedText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("halt.notice")),
                "the veil text resolves through the csv key, never a literal");
            Assert.That(chrome.Cta == null || !chrome.Cta.IsVisible, Is.True,
                "no Try-again affordance on the halt surface (Q-2 lands in CM-UX-07)");
        }

        // --- criteria 3 + 7: CTA renders in-band, registers NOTHING; counts can move ---

        [UnityTest]
        public IEnumerator Cta_RendersLockedString_AndRegistersNoRegions()
        {
            var chrome = AttachControlled("Playing");
            yield return null;
            int baseline = _root.Input.Regions.Count;

            _state = "FailureReview";
            yield return null;
            Assert.That(chrome.Cta.RenderedText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("retry.cta")),
                "the LOCKED copy resolves through the csv key");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline),
                "the CTA is render-only — the band's own RetryTapped is its action");

            // positive control (anti-vacuity): the counter observes THIS registry
            _root.Input.Regions.Register("decoy", () => new Rect(0, 0, 1, 1), () => { }, 0);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline + 1));
            Assert.That(_root.Input.Regions.Unregister("decoy"), Is.True);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline));

            // the band still retries exactly as pinned while the chip renders
            bool retried = false;
            _root.Input.RetryRegionActive = () => true;
            _root.Input.RetryTapped = () => retried = true;
            var inBand = new Vector2(Screen.width * 0.5f, Screen.height * 0.10f);
            Assert.That(_root.Input.HandleTapAtScreen(inBand), Is.EqualTo(-2));
            Assert.That(retried, Is.True);
        }

        [UnityTest]
        public IEnumerator Cta_TextMeshGeometryExists_RenderabilityProxy()
        {
            var chrome = AttachControlled("FailureReview");
            yield return null;
            yield return null; // TMP layout settles a frame after creation
            var tmp = chrome.Cta.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.That(tmp, Is.Not.Null, "the chip text is TMP (human answer Q-6)");
            tmp.ForceMeshUpdate();
            Assert.That(tmp.textInfo.characterCount, Is.GreaterThan(0),
                "generated glyph geometry exists — the A-UX2-2 renderability proxy");
        }

        // --- criterion 1: render-only tree, whitelist-walked, with a positive control ---

        private static readonly System.Type[] Whitelist =
        {
            typeof(Transform), typeof(RectTransform), typeof(Canvas),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.Image), typeof(TMPro.TextMeshProUGUI),
            typeof(RetryCtaView), typeof(HaltVeilView),
            // (the controller sits on GameRoot's object, never under ChromeRoot — R2 nit)
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
        public IEnumerator ChromeTree_IsRenderOnly_ByWhitelist()
        {
            // R1-H2: walk the WHOLE chrome root (canvas + CTA + veil + labels), never a
            // subtree — a raycaster on the canvas or an affordance on the veil must go red.
            var chrome = AttachControlled("FailureReview");
            yield return null;
            Assert.That(chrome.ChromeRoot, Is.Not.Null);
            var off = FirstOffWhitelist(chrome.ChromeRoot);
            Assert.That(off, Is.Null,
                "chrome carries render-side types only; found: " + (off ? off.GetType().FullName : ""));

            // R1-H2: explicit detection of the interactivity classes the criterion names —
            // belt on top of the whitelist braces.
            Assert.That(chrome.ChromeRoot
                .GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length, Is.EqualTo(0),
                "no Selectable anywhere under chrome");
            Assert.That(chrome.ChromeRoot
                .GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true).Length,
                Is.EqualTo(0), "no raycaster-driven interactivity under chrome");

            // positive control: the walk DOES flag an interactive component elsewhere
            var decoyGo = new GameObject("decoy-tree");
            try
            {
                decoyGo.AddComponent<UnityEngine.UI.Button>();
                Assert.That(FirstOffWhitelist(decoyGo), Is.Not.Null,
                    "the whitelist walk can detect a non-render component");
                Assert.That(decoyGo.GetComponentsInChildren<UnityEngine.UI.Selectable>(true)
                    .Length, Is.GreaterThan(0), "the Selectable scan can detect one too");
            }
            finally { Object.Destroy(decoyGo); }
        }

        // --- criterion 5: motion parity, mechanism named ---

        private static int MotionComponentCount(GameObject root)
        {
            return root.GetComponentsInChildren<Animator>(true).Length
                 + root.GetComponentsInChildren<Animation>(true).Length;
        }

        [UnityTest]
        public IEnumerator MotionOff_RendersEverything_ZeroAnimationComponents()
        {
            // R1-M5: scanned over the WHOLE chrome root, and stated honestly — this chrome
            // ships no motion at all, so the guarantee is structural (zero animation
            // components), not a behavioral A/B: a motion-on-vs-off comparison over a
            // motionless tree would compare a branch to itself and guard nothing.
            var chrome = AttachControlled("Playing");
            _root.MotionOffToggle = true;
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(chrome.Cta != null && chrome.Cta.IsVisible, Is.True,
                "motion-off renders the full information set");
            Assert.That(MotionComponentCount(chrome.ChromeRoot), Is.EqualTo(0),
                "no animation components anywhere under chrome");

            // positive control: the counter counts when one exists
            var decoy = new GameObject("decoy-anim");
            try
            {
                decoy.AddComponent<Animator>();
                Assert.That(MotionComponentCount(decoy), Is.EqualTo(1));
            }
            finally { Object.Destroy(decoy); }
        }

        // --- the #33 standing rule: rendered-frame captures as visual evidence ---
        // Env-gated so suites never write repo files: set CM_UX02_CAPTURE_DIR to emit PNGs.

        [UnityTest]
        public IEnumerator CaptureEvidence_CtaAndVeilFrames_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UX02_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                // The #33 evidence rig, disarmed: Ignore would break the harness wrapper's
                // total==passed gate, so the unarmed path PASSES explicitly and says why.
                Assert.Pass("capture rig disarmed — set CM_UX02_CAPTURE_DIR to emit frames");
                yield break;
            }
            var chrome = AttachControlled("FailureReview");
            yield return null;
            yield return null;
            Capture(dir, "cm-ux-02-cta.png");

            _state = "Halted";
            yield return null;
            Capture(dir, "cm-ux-02-veil.png");
        }

        private void Capture(string dir, string name)
        {
            // R1-M1: the RT MUST match Screen — the ScreenSpaceCamera canvas sizes from the
            // camera's pixel rect while the view lays out from Screen.*; a mismatched RT
            // (the original 720x1280) renders the chip displaced and the evidence stops being
            // probative for layout. Screen-matched, canvas and view agree by construction.
            var rt = new RenderTexture(Screen.width, Screen.height, 24);
            _root.Cam.targetTexture = rt;
            _root.Cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            _root.Cam.targetTexture = null;
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }

        private static string FixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T906"", ""name"": ""Chrome Fixture"", ""seed"": 906,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 12 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": 12 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": [ { ""tick"": 3999, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
