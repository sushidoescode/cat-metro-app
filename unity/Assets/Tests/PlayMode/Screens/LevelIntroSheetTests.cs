using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-06 criteria 6/7 over the LevelIntro sheet, by DIRECT CONSTRUCTION (P-3). The
    // sheet explains the level before play: injected name, csv-templated goal line, and one
    // explicit thumb-band Play CTA — tap-anywhere dismissal is NOT built (S-05 law), proven
    // by the region count and an out-of-chip resolve with its in-chip positive control.
    public sealed class LevelIntroSheetTests
    {
        private GameObject _canvasGo;
        private LevelIntroSheet _sheet;
        private ChromeRegions _regions;

        private LevelIntroSheet CreateShown(string name = "First Switch", int deliveries = 3)
        {
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _sheet = LevelIntroSheet.Create(canvas.transform);
            _sheet.Attach(_regions);
            _sheet.Show(name, deliveries);
            return _sheet;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            _canvasGo = null;
            _sheet = null;
        }

        // --- criterion 7: the sheet explains the level, through keys + injected data ---

        [UnityTest]
        public IEnumerator Sheet_ShowsName_GoalLine_AndPlayCta_FromKeysAndData()
        {
            CreateShown("First Switch", 3);
            yield return null;

            Assert.That(_sheet.NameText, Is.EqualTo("First Switch"),
                "the level name renders from injected data");
            Assert.That(_sheet.GoalText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("intro.goal")
                    .Replace("{count}", "3")),
                "the goal line is the csv template with {count} substituted — never a literal");
            Assert.That(_sheet.GoalText.Contains("{count}"), Is.False,
                "the token is substituted, not rendered raw");
            Assert.That(_sheet.PlayText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("intro.play")),
                "the CTA resolves through the csv key");
            Assert.That(_sheet.PlayText.Contains("??"), Is.False,
                "the key resolves — not the missing-key sentinel");

            // A-UX2-2 renderability proxy on the sheet's TMP text
            var tmp = _sheet.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.That(tmp, Is.Not.Null, "sheet text is TMP (P-2)");
            tmp.ForceMeshUpdate();
            Assert.That(tmp.textInfo.characterCount, Is.GreaterThan(0),
                "generated glyph geometry exists");
        }

        // --- criterion 7: the Play chip honors the thumb-band + 48dp law ---

        [UnityTest]
        public IEnumerator PlayChip_IsTheSafeAreaThumbBand_AndClearsTheFloor()
        {
            CreateShown();
            yield return null;

            var expected = CatMetro.Presentation.Hud.HudBands.ThumbBand(Screen.safeArea);
            Assert.That(_sheet.PlayChipRectPx, Is.EqualTo(expected),
                "the Play CTA is full-width in the safe-area thumb band (§1.1 primary-CTA law)");
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            Assert.That(CatMetro.Presentation.Hud.HudBands.MeetsMinTargetPx(
                _sheet.PlayChipRectPx, dpi), Is.True, "the chip clears the 48dp floor");
        }

        // --- criterion 6: ONE region; tap-anywhere dismissal is not built ---

        [UnityTest]
        public IEnumerator Sheet_RegistersOnlyThePlayChip_OutsideTapsDoNothing()
        {
            CreateShown();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1),
                "the sheet registers exactly ONE region: the Play chip");

            int played = 0;
            _sheet.PlayRequested = () => played++;

            // positive control first: the chip center resolves and fires the seam
            Assert.That(_regions.TryResolve(_sheet.PlayChipRectPx.center, out var onTap),
                Is.True);
            onTap();
            Assert.That(played, Is.EqualTo(1), "the chip fires PlayRequested");

            // the law: a tap OUTSIDE the chip resolves nothing — no tap-anywhere dismissal
            var midBoard = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            Assert.That(_regions.TryResolve(midBoard, out _), Is.False,
                "outside the thumb band nothing resolves — dismissal is not built");
            Assert.That(played, Is.EqualTo(1), "and the seam did not re-fire");
            Assert.That(_sheet.IsVisible, Is.True, "the sheet stays up — only Play acts");
        }

        [UnityTest]
        public IEnumerator Sheet_UnregistersOnHide_AndOnDestroy()
        {
            CreateShown();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1));

            _sheet.Hide();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0), "Hide unregisters (R1-F3)");
            Assert.That(_sheet.IsVisible, Is.False);

            _sheet.Show("First Switch", 3);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1), "re-Show re-registers exactly once");

            Object.Destroy(_sheet.gameObject);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0), "OnDestroy unregisters (R1-F3)");
        }

        // --- criterion 6: render-only tree with positive control ---

        private static readonly System.Type[] Whitelist =
        {
            typeof(Transform), typeof(RectTransform), typeof(Canvas),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.Image), typeof(TMPro.TextMeshProUGUI),
            typeof(LevelIntroSheet),
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
        public IEnumerator SheetTree_IsRenderOnly_ZeroInteractivity()
        {
            CreateShown();
            yield return null;

            var off = FirstOffWhitelist(_sheet.gameObject);
            Assert.That(off, Is.Null, "render-side types only under the sheet; found: "
                + (off ? off.GetType().FullName : ""));
            Assert.That(_sheet.GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length,
                Is.EqualTo(0), "no Selectable — the chip's hit lives in ChromeRegions");
            Assert.That(_sheet.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true)
                .Length, Is.EqualTo(0), "no raycaster-driven interactivity");
            Assert.That(_sheet.GetComponentsInChildren<Animator>(true).Length
                + _sheet.GetComponentsInChildren<Animation>(true).Length, Is.EqualTo(0),
                "the sheet ships no motion at all");

            var decoy = new GameObject("decoy-tree");
            try
            {
                decoy.AddComponent<UnityEngine.UI.Button>();
                Assert.That(FirstOffWhitelist(decoy), Is.Not.Null,
                    "the whitelist walk can detect a non-render component");
            }
            finally { Object.Destroy(decoy); }
        }
    }
}
