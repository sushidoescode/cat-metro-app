using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;

namespace CatMetro.Tests.PlayMode
{
    // UI-CHROME criterion 6. This directly reproduces the recorded 640x480-class defect:
    // the live first chip must carry fitted TMP geometry plus a separate symbol sprite.
    public sealed class WavePreviewStyleTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator LiveWaveChip_IsUguiTmp_FittedReadableAndSymbolCoded()
        {
            _root = GameRoot.Launch();
            yield return null;

            var preview = _root.Preview;
            Assert.That(preview.GetComponentInChildren<Canvas>(true), Is.Not.Null,
                "UI-CHROME RED: preview is still world-space greybox geometry");
            Assert.That(preview.GetComponentsInChildren<Renderer>(true).Length, Is.Zero,
                "preview owns no world-space renderer after the UGUI migration");
            Assert.That(preview.GetComponentsInChildren<TextMesh>(true).Length, Is.Zero);
            Assert.That(preview.GetComponentsInChildren<Collider>(true).Length, Is.Zero);
            Assert.That(preview.GetComponentsInChildren<Selectable>(true).Length, Is.Zero,
                "the top strip remains display-only");
            Assert.That(preview.GetComponentsInChildren<GraphicRaycaster>(true).Length, Is.Zero);

            var chip = Find(preview.transform, "WaveChip0");
            var card = chip.GetComponent<Image>();
            Assert.That(card, Is.Not.Null);
            Assert.That((Color32)card.color, Is.EqualTo(Hex("FAF6EC")));
            Assert.That(card.sprite, Is.Not.Null);

            var symbol = Find(chip, "WaveSymbol").GetComponent<Image>();
            Assert.That(symbol, Is.Not.Null);
            Assert.That(symbol.sprite, Is.Not.Null);
            Assert.That(symbol.sprite.name, Is.EqualTo("SymbolCircle"),
                "L001 red wave uses the circle shape twin");
            Assert.That((Color32)symbol.color, Is.EqualTo(Hex("E15A47")));

            var count = Find(chip, "WaveCount").GetComponent<TMP_Text>();
            Assert.That(count, Is.Not.Null);
            Assert.That(count.text, Is.EqualTo("x2"));
            Assert.That(count.font.name, Is.EqualTo("CatMetroSans SDF"));
            Assert.That((Color32)count.color, Is.EqualTo(Hex("22304A")));
            float pxPerDp = HudBands.PxPerDp(Screen.dpi);
            Assert.That(count.fontSize, Is.GreaterThanOrEqualTo(24f * pxPerDp),
                "the recorded tiny count is replaced by a 24dp-equivalent glyph size");
            count.ForceMeshUpdate();
            Assert.That(count.textBounds.size.x, Is.GreaterThan(8f));
            Assert.That(count.textBounds.size.y, Is.GreaterThan(8f));
            Assert.That(count.preferredWidth,
                Is.LessThanOrEqualTo(count.rectTransform.rect.width + 0.5f));
            Assert.That(count.isTextOverflowing, Is.False);
            Assert.That(preview.InTopBand(0), Is.True);
        }

        [UnityTest]
        public IEnumerator WaveTray_UnderDioramaCamera900x2000Aspect_CornersStayInsideTopBand()
        {
            // F-2 regression (round-1 review): reproduces the reviewed finding under the
            // diorama board camera's off-centre projection/view matrices
            // (CauseCameraController), forced to the exact 900x2000 portrait aspect Lane 1A
            // used to measure the defect — the same aspect its own evidence-capture rig applies
            // in DioramaConstructionTests.cs's Capture() helper
            // (`causeCamera.ApplyDioramaFraming(width / (float)height)` with width=900,
            // height=2000). Aspect-forcing rather than a literal Screen.SetResolution matches
            // that file's own headless-safe precedent
            // (PolyforkDressing_ProjectsAtReadablePhoneScaleInsideThePlayfield) and is provably
            // equivalent here: the ScreenSpaceCamera bug is a projection-matrix/camera-state
            // mismatch, not a raw-resolution effect. Before the F-2 fix, this same camera state
            // sent WorldToScreenPoint results to a viewportY far outside the top band (Lane 1A
            // measured y~=2958-3073 on a 2000-high screen); the fix's Overlay canvas removes
            // the camera from this computation entirely
            // (RectTransformUtility.WorldToScreenPoint(null, worldPos) returns worldPos.x/y
            // verbatim), so this stays green regardless of the board camera's framing.
            _root = GameRoot.Launch();
            yield return null;

            _root.Cam.aspect = 900f / 2000f;
            _root.CauseCam.ApplyDioramaFraming(_root.Cam.aspect);

            var preview = _root.Preview;
            var canvas = Find(preview.transform, "WavePreviewCanvas").GetComponent<Canvas>();
            var tray = Find(preview.transform, "WaveTray").GetComponent<RectTransform>();

            var corners = new Vector3[4];
            tray.GetWorldCorners(corners);
            // Mirrors WavePreviewStrip.InTopBand's own eventCamera/viewportY math exactly
            // (:87-99), but walks all four rect corners instead of only the center.
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            foreach (var corner in corners)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corner);
                float viewportY = Screen.height > 0 ? screenPoint.y / Screen.height : 0f;
                Assert.That(viewportY, Is.GreaterThanOrEqualTo(0.85f).And.LessThanOrEqualTo(1f),
                    "wave tray corner " + corner + " projects to viewportY=" + viewportY
                    + " under the diorama camera's 900x2000-aspect off-centre projection; the "
                    + "strip must stay inside the top 0-15% band");
            }
        }

        [TestCase("red", "SymbolCircle")]
        [TestCase("blue", "SymbolSquare")]
        [TestCase("yellow", "SymbolTriangle")]
        [TestCase("green", "SymbolDiamond")]
        [TestCase("wild", "SymbolStar")]
        public void ColorToSymbolMap_IsPinned(string color, string expected)
        {
            var method = typeof(WavePreviewStrip).GetMethod("SymbolNameFor",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "UI-CHROME RED: symbol mapping does not exist yet");
            Assert.That(method.Invoke(null, new object[] { color }), Is.EqualTo(expected));
        }

        [Test]
        public void WavePreviewSource_ContainsNoPrimitiveConstruction()
        {
            var text = File.ReadAllText(
                "Assets/Scripts/Presentation/Hud/WavePreview/WavePreviewStrip.cs");
            Assert.That(text, Does.Not.Contain("GameObject.CreatePrimitive"),
                "Lane 1A must land the gate re-author before this red is implemented");
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item;
            Assert.Fail("UI-CHROME RED: missing wave node " + name);
            return null;
        }

        private static Color32 Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }
    }
}
