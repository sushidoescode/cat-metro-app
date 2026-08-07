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
    // CM-UX-04 criteria 1-9 under the inherited live-wiring + anti-vacuity rule: every test
    // drives a panel over GameRoot.LaunchWith (real session/view/camera/registry), and every
    // negative or absence assert carries a positive control in the same fixture. The panel is
    // a NEEDS-WIRING component — attached here by direct construction only; CM-UX-07 does NOT
    // attach it until LoadNext exists (human answer Q-3).
    public sealed class ResultsPanelTests
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

        private static ImportedLevel Fixture(string json)
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        private ResultsPanel AttachControlled(string initial)
        {
            _root = GameRoot.LaunchWith(Fixture(QuietFixtureJson()));
            _state = initial;
            var panel = _root.gameObject.AddComponent<ResultsPanel>();
            panel.Attach(() => _state, _root.Input.Regions);
            return panel;
        }

        // --- criterion 1: state map proven on TRANSITIONS ---

        [UnityTest]
        public IEnumerator Transition_PlayingToWon_ShowsPanelWithinOneFrame_AndBackHides()
        {
            var panel = AttachControlled("Playing");
            yield return null;
            Assert.That(panel.IsVisible, Is.False,
                "pre-flip positive control: nothing renders during Playing");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(0),
                "no region registered while hidden");

            _state = "Won";
            yield return null; // within one pumped frame
            Assert.That(panel.IsVisible, Is.True,
                "the panel renders on the frame after the state source changes");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1),
                "the CTA region registers with the panel");

            _state = "Playing";
            yield return null;
            Assert.That(panel.IsVisible, Is.False, "returning to Playing hides the panel");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(0),
                "hiding unregisters the CTA region");
        }

        [UnityTest]
        public IEnumerator OtherStates_RenderNothing_WithAVisiblePositiveControl()
        {
            var panel = AttachControlled("Won");
            yield return null;
            Assert.That(panel.IsVisible, Is.True,
                "positive control: the panel demonstrably renders on Won");

            _state = "FailureReview";
            yield return null;
            Assert.That(panel.IsVisible, Is.False, "FailureReview renders no results panel");

            _state = "Halted";
            yield return null;
            Assert.That(panel.IsVisible, Is.False, "Halted renders no results panel");
        }

        // --- criterion 2: the REAL Won transition, with the wiring delegate shape ---

        [UnityTest]
        public IEnumerator RealWin_ShowsPanel_WithTheWiringDelegateShape()
        {
            _root = GameRoot.LaunchWith(Fixture(WinnableFixtureJson()));
            var panel = _root.gameObject.AddComponent<ResultsPanel>();
            panel.Attach(() => _root.ScreenState, _root.Input.Regions); // the wiring shape
            yield return null;
            Assert.That(panel.IsVisible, Is.False, "pre-win positive control: hidden in play");

            // The GreyboxTests Win recipe: the winnable fixture routes its one cat to the
            // matching station on the initial route — no taps, deterministic.
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null; // GameRoot.Update picks up the outcome
            Assert.That(_root.ScreenState, Is.EqualTo("Won"), "the real sim outcome");
            yield return null; // within one pumped frame of the real transition
            Assert.That(panel.IsVisible, Is.True, "the win dead-end renders its exit");
            Assert.That(panel.CtaText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("results.next")),
                "the LOCKED copy resolves through the csv key, never a literal");
            Assert.That(_root.Banner.Visible, Is.True,
                "the merged win banner is untouched — this slice edits no Bootstrap code");
        }

        // --- criterion 3: the count==1 invariant (CM-R19.3 + TG-4 + the §5 tripwire) ---

        [UnityTest]
        public IEnumerator ExactlyOneRegion_AndFooterStructurallyEmpty()
        {
            var panel = AttachControlled("Won");
            yield return null;
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1),
                "EXACTLY one primary CTA — any footer button forces this red (§5 tripwire)");
            Assert.That(panel.FooterRoot, Is.Not.Null,
                "the footer container exists so its emptiness is assertable");
            Assert.That(panel.FooterRoot.childCount, Is.EqualTo(0),
                "TG-4's empty-footer posture, structural");

            // positive controls: both counters observe live structures
            _root.Input.Regions.Register("decoy", () => new Rect(0, 0, 1, 1), () => { }, 0);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(2));
            Assert.That(_root.Input.Regions.Unregister("decoy"), Is.True);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1));

            var decoyChild = new GameObject("footer-decoy");
            decoyChild.transform.SetParent(panel.FooterRoot, false);
            Assert.That(panel.FooterRoot.childCount, Is.EqualTo(1),
                "the child counter can detect a footer addition");
            Object.Destroy(decoyChild);
            yield return null;
            Assert.That(panel.FooterRoot.childCount, Is.EqualTo(0));
        }

        // --- criteria 4 + 5: registry-routed tap; the seam is a seam ONLY ---

        [UnityTest]
        public IEnumerator ChipTap_RoutesThroughTheRegistry_SeamOnly_NoBoardEffect()
        {
            var panel = AttachControlled("Won");
            yield return null;

            var center = panel.ChipPaintedRectPx.center;
            int commandsBefore = _root.Session.Log.Entries.Count;
            int routeBefore = _root.View.CommittedRoute(0);

            // criterion 5 first: NO subscriber — the tap is a silent no-op, never a throw
            Assert.That(_root.Input.HandleTapAtScreen(center), Is.EqualTo(-3),
                "the chrome region consumes the tap (CM-UX-01's -3 code)");

            int fired = 0;
            panel.NextRequested = () => fired++;
            Assert.That(_root.Input.HandleTapAtScreen(center), Is.EqualTo(-3));
            Assert.That(fired, Is.EqualTo(1), "the seam fires exactly once per tap");
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(commandsBefore),
                "no session command — the tap never falls through to a disc");
            Assert.That(_root.View.CommittedRoute(0), Is.EqualTo(routeBefore),
                "no lever flips beneath the panel");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"),
                "the seam advances NOTHING — level advance is Bootstrap-owned");

            // criterion 4 positive control: hidden panel, same tap resolves as merged
            _state = "Playing";
            yield return null;
            Assert.That(_root.Input.HandleTapAtScreen(center), Is.Not.EqualTo(-3),
                "with the panel hidden the registry no longer claims this tap");
            Assert.That(fired, Is.EqualTo(1), "the seam cannot fire while hidden");
        }

        [UnityTest]
        public IEnumerator ChipPaintedRect_IsTheSafeAreaThumbBand_AndFloorHoldsLive()
        {
            var panel = AttachControlled("Won");
            yield return null;

            var expected = ResultsPanel.ChipRect(Screen.safeArea);
            Assert.That(panel.ChipPaintedRectPx, Is.EqualTo(expected),
                "the applied rect IS the pure chip law over the live safe area");
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            Assert.That(HudBands.MeetsMinTargetPx(panel.ChipPaintedRectPx, dpi), Is.True,
                "the live painted (== registered) rect clears the 48dp floor on this host");
        }

        // --- criterion 6: registry lifetime law (CM-UX-01 R1-F3) ---

        [UnityTest]
        public IEnumerator RegistryLifetime_HideReshowDestroy_NeverLeaksARegion()
        {
            _root = GameRoot.LaunchWith(Fixture(QuietFixtureJson()));
            // #39 review F7: the host must die with the test on ANY exit path — a leaked live
            // host polls a stale registry for the rest of the session and cascades failures.
            var host = new GameObject("panel-host");
            try
            {
            var panel = host.AddComponent<ResultsPanel>();
            _state = "Won";
            panel.Attach(() => _state, _root.Input.Regions);
            yield return null;
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1), "shown → registered");

            _state = "Playing";
            yield return null;
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(0), "hidden → unregistered");

            _state = "Won";
            yield return null;
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1),
                "re-shown → re-registered, idempotent (no duplicate-id throw across flips)");

            Object.Destroy(host);
            yield return null;
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(0),
                "destroy mid-show → the OnDestroy law unregisters (a live provider over a "
                + "destroyed view would throw on the next tap otherwise)");
            }
            finally
            {
                if (host != null) Object.Destroy(host); // F7: no live host past this test
            }
        }

        // --- criterion 7: render-only tree, whitelist-walked ---

        private static readonly System.Type[] Whitelist =
        {
            typeof(Transform), typeof(RectTransform), typeof(Canvas),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image),
            typeof(TMPro.TextMeshProUGUI),
            // (the ResultsPanel component sits on the HOST object, never under PanelRoot)
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
        public IEnumerator PanelTree_IsRenderOnly_ByWhitelist()
        {
            var panel = AttachControlled("Won");
            yield return null;
            Assert.That(panel.PanelRoot, Is.Not.Null);
            var off = FirstOffWhitelist(panel.PanelRoot);
            Assert.That(off, Is.Null, "the panel carries render-side types only; found: "
                + (off ? off.GetType().FullName : ""));
            Assert.That(panel.PanelRoot
                .GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length, Is.EqualTo(0),
                "no Selectable anywhere under the panel");
            Assert.That(panel.PanelRoot
                .GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true).Length,
                Is.EqualTo(0), "no raycaster-driven interactivity under the panel");

            // positive control: the walk DOES flag an interactive component elsewhere
            var decoyGo = new GameObject("decoy-tree");
            try
            {
                decoyGo.AddComponent<UnityEngine.UI.Button>();
                Assert.That(FirstOffWhitelist(decoyGo), Is.Not.Null,
                    "the whitelist walk can detect a non-render component");
            }
            finally { Object.Destroy(decoyGo); }

            // A-UX2-2 renderability proxy: the CTA text generates real glyph geometry
            var tmp = panel.PanelRoot.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.That(tmp, Is.Not.Null, "the CTA text is TMP (human answer Q-6)");
            tmp.ForceMeshUpdate();
            Assert.That(tmp.textInfo.characterCount, Is.GreaterThan(0),
                "generated glyph geometry exists");
        }

        // --- criteria 8 + 9: no fabricated content; text+shape; zero animation components ---

        [UnityTest]
        public IEnumerator ContentSet_IsExactlyTheLockedCta_TextPlusShape_NoMotion()
        {
            var panel = AttachControlled("Won");
            _root.MotionOffToggle = true; // motion-off removes nothing because nothing moves
            yield return null;

            Assert.That(panel.IsVisible, Is.True,
                "motion-off renders the full information set");
            var texts = panel.PanelRoot.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.That(texts.Length, Is.EqualTo(1),
                "EXACTLY one text: the LOCKED CTA — no score/stars/tickets (Q-C pins score "
                + "at 0; rendering one would fabricate data)");
            Assert.That(texts[0].text,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("results.next")));
            Assert.That(texts[0].text, Is.Not.Empty, "text present — never color-alone state");
            Assert.That(panel.PanelRoot
                .GetComponentsInChildren<UnityEngine.UI.Image>(true).Length,
                Is.GreaterThan(0), "shape present — never color-alone state");

            Assert.That(MotionComponentCount(panel.PanelRoot), Is.EqualTo(0),
                "no animation components anywhere under the panel");
            var decoy = new GameObject("decoy-anim");
            try
            {
                decoy.AddComponent<Animator>();
                Assert.That(MotionComponentCount(decoy), Is.EqualTo(1),
                    "the counter counts when one exists");
            }
            finally { Object.Destroy(decoy); }
        }

        private static int MotionComponentCount(GameObject root)
        {
            return root.GetComponentsInChildren<Animator>(true).Length
                 + root.GetComponentsInChildren<Animation>(true).Length;
        }

        // --- the #33 standing rule: rendered-frame captures as visual evidence ---
        // Env-gated so suites never write repo files: set CM_UX04_CAPTURE_DIR to emit PNGs.

        [UnityTest]
        public IEnumerator CaptureEvidence_ResultsFrame_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UX04_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                // Disarmed path PASSES explicitly (an Ignore would break the wrapper's
                // total==passed gate) — the CM-UX-02 rig precedent.
                Assert.Pass("capture rig disarmed — set CM_UX04_CAPTURE_DIR to emit frames");
                yield break;
            }
            _root = GameRoot.LaunchWith(Fixture(WinnableFixtureJson()));
            var panel = _root.gameObject.AddComponent<ResultsPanel>();
            panel.Attach(() => _root.ScreenState, _root.Input.Regions);
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            yield return null; // panel applies + TMP layout settles
            Capture(dir, "cm-ux-04-results.png");
        }

        private void Capture(string dir, string name)
        {
            // Screen-matched RT (the CM-UX-02 R1-M1 lesson): canvas and view must agree on
            // the frame geometry or the evidence stops being probative for layout.
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

        // Quiet fixture (the T905 shape): no waves in play range — Playing persists while the
        // controlled state source drives the panel.
        private static string QuietFixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T907"", ""name"": ""Results Fixture"", ""seed"": 907,
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

        // Winnable fixture: ONE red cat, wave tick 3, initial route E2 aims at the matching
        // RED station, win.deliveries 1 — the real sim reaches Won with zero taps well inside
        // the 200-tick advance (criterion 2 / A-UX4-4).
        private static string WinnableFixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T908"", ""name"": ""Winnable Fixture"", ""seed"": 908,
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
  ""waves"": [ { ""tick"": 3, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 1, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
