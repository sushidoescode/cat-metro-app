using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class ChromeChipTests
    {
        private GameObject _canvas;

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null) Object.DestroyImmediate(_canvas);
        }

        private RectTransform Paint(Rect band, float dpi, string label, Sprite icon = null)
        {
            _canvas = new GameObject("ChipTestCanvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return ChromeChip.PaintPrimary(_canvas.transform, band, label,
                Palette.TicketOrange, icon, dpi).Root;
        }

        [TestCase(360f, 0f, 160f, 320f, 60f, 20f, 24f)]
        [TestCase(917f, 64f, 408f, 815f, 153f, 51f, 125.2f)]
        [TestCase(1536f, 86f, 408f, 816f, 153f, 360f, 147.2f)]
        public void FaceFitsTheSafeBand_AtPhoneAndTabletDensities(float width, float bottom,
            float dpi, float faceWidth, float faceHeight, float left, float faceBottom)
        {
            var root = Paint(new Rect(0f, bottom, width, 480f), dpi, "Next");
            Assert.That(root.rect.width, Is.EqualTo(faceWidth).Within(0.01f));
            Assert.That(root.rect.height, Is.EqualTo(faceHeight).Within(0.01f));
            Assert.That(root.anchoredPosition.x - root.rect.width * .5f,
                Is.EqualTo(left).Within(0.01f));
            Assert.That(root.anchoredPosition.y - root.rect.height * .5f,
                Is.EqualTo(faceBottom).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator IconAndLocalizedLabel_AreCentredAsOneGroup()
        {
            var root = Paint(new Rect(0f, 0f, 360f, 160f), 160f,
                "Try again", HudShapeSprites.Triangle);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var label = root.GetComponentInChildren<TMP_Text>();
            var icon = (RectTransform)root.Find("Content/Icon");
            var content = (RectTransform)root.Find("Content");
            Assert.That(content.anchoredPosition.x, Is.Zero.Within(.01f));
            Assert.That(label.rectTransform.rect.width,
                Is.EqualTo(label.GetPreferredValues(label.text).x).Within(.5f));
            Assert.That(content.rect.width,
                Is.EqualTo(icon.rect.width + 10f + label.rectTransform.rect.width).Within(.5f));
            Assert.That(label.isTextOverflowing, Is.False);
            Assert.That(content.rect.width, Is.LessThanOrEqualTo(root.rect.width - 40f));
        }

        [TestCase(72f)]
        [TestCase(408f)]
        public void LocalizedLabel_KeepsTheSharedFontMinimumAtEditorAndPhoneDpi(float dpi)
        {
            float scale = HudBands.PxPerDp(dpi);
            var root = Paint(new Rect(0, 0, 220f * scale, 160f * scale), dpi,
                "Recommencer le parcours quotidien", HudShapeSprites.Triangle);
            var label = root.GetComponentInChildren<TMP_Text>();
            float minimum = Mathf.Max(TypeScale.Minimum, TypeScale.Minimum * scale);
            Assert.That(label.font.faceInfo.familyName, Is.EqualTo("Fredoka"));
            Assert.That(label.fontSizeMin, Is.GreaterThanOrEqualTo(minimum));
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(minimum));
            Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        }

        [Test]
        public void CompactIconOnlyPin_CentresTheGlyphWithoutAnEmptyLabelGap()
        {
            _canvas = new GameObject("CompactChipCanvas", typeof(Canvas));
            var chip = ChromeChip.PaintPrimary(_canvas.transform, new Rect(0, 0, 160, 160),
                "", Palette.InkNavy, HudShapeSprites.Triangle, 408f);
            chip.LayoutFace(new Rect(100, 1800, 122.4f, 122.4f), 408f);
            var icon = (RectTransform)chip.Root.Find("Content/Icon");
            Assert.That(chip.Root.InverseTransformPoint(icon.TransformPoint(icon.rect.center)).x,
                Is.Zero.Within(.01f), "an empty label must not leave a 10dp phantom gap");
            Assert.That(chip.Label.fontSizeMin, Is.GreaterThanOrEqualTo(30.6f - .001f));
            Assert.That(chip.Label.fontSizeMax, Is.EqualTo(61.2f).Within(.001f));
        }

        [TestCase("Unlock · CA$2.79")]
        [TestCase("Débloquer · 2,79 $CA")]
        [TestCase("Equip")]
        public void LocalizedActionLabel_RefitsAfterTheOwnerInsetsItsTextRect(string text)
        {
            var root = Paint(new Rect(0, 64, 917, 280), 408f, text);
            var label = root.GetComponentInChildren<TMP_Text>();
            // Wardrobe reserves an inset and a subtitle after the shared paint is measured.
            // A fixed measured font truncates even short labels when this rect contracts.
            label.rectTransform.anchorMin = new Vector2(.04f, .35f);
            label.rectTransform.anchorMax = new Vector2(.96f, .95f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate();
            Assert.That(label.isTextTruncated, Is.False,
                "the whole action must fit: " + text + " font=" + label.fontSize
                + " width=" + label.rectTransform.rect.width);
            Assert.That(label.isTextOverflowing, Is.False);
            Assert.That(label.enableAutoSizing, Is.True);
            Assert.That(label.fontSizeMin, Is.GreaterThanOrEqualTo(30.6f - .001f));
            Assert.That(label.fontSize, Is.InRange(label.fontSizeMin, 61.2f));
            Assert.That(label.fontSizeMax, Is.EqualTo(61.2f).Within(.01f));
        }

        [Test]
        public void PinPaint_IsCreamWithStitchGaps_AndOwnsNoInputOrMotionComponents()
        {
            var root = Paint(new Rect(0f, 0f, 360f, 160f), 160f, "Play");
            Assert.That(root.GetComponentsInChildren<TMP_Text>().Length, Is.EqualTo(1));
            var face = root.Find("Face").GetComponent<Image>();
            Assert.That(face.color, Is.EqualTo(Palette.CreamCard));
            Assert.That(face.sprite, Is.SameAs(HudShapeSprites.RoundedSquare));
            var stitch = root.Find("Stitches").GetComponent<Image>();
            var pixels = stitch.sprite.texture.GetPixels32();
            int clear = 0, solid = 0;
            foreach (var pixel in pixels)
            {
                if (pixel.a == 0) clear++;
                if (pixel.a > 128) solid++;
            }
            Assert.That(clear, Is.GreaterThan(solid), "the stitch has open gaps and a clear centre");
            Assert.That(solid, Is.GreaterThan(30), "the border must actually contain stitches");
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                Assert.That(component is RectTransform || component is CanvasRenderer
                    || component is Image || component is TextMeshProUGUI, Is.True,
                    component.GetType().Name + " is not paint");
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);
        }

        [UnityTest]
        public IEnumerator HiddenParent_CanBuildAndRelabelBeforeShowing()
        {
            _canvas = new GameObject("HiddenChipCanvas", typeof(Canvas));
            _canvas.SetActive(false);
            var chip = ChromeChip.PaintPrimary(_canvas.transform,
                new Rect(0, 0, 360, 160), "Daily", Palette.TicketOrange);
            chip.SetLabel("Daily route");
            _canvas.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(chip.Label.text, Is.EqualTo("Daily route"));
            Assert.That(chip.Label.textInfo.characterCount, Is.EqualTo(11));
            Assert.That(chip.Label.isTextOverflowing, Is.False);
        }

        [UnityTest]
        public IEnumerator SmallPin_LongLabelKeepsReadableTypeAndEllipsizes()
        {
            Paint(new Rect(0, 0, 360, 160), 160f,
                "Recommencer le parcours quotidien", HudShapeSprites.Triangle);
            var chip = ChromeChip.PaintPrimary(_canvas.transform,
                new Rect(0, 0, 360, 160), "Recommencer le parcours quotidien",
                Palette.MetroTeal, HudShapeSprites.Triangle, 160f);
            chip.LayoutFace(new Rect(20, 24, 156, 60), 160f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(chip.Label.fontSize, Is.GreaterThanOrEqualTo(12f));
            Assert.That(chip.Label.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(chip.Label.rectTransform.rect.width, Is.LessThanOrEqualTo(80f));
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_PrimaryPins_917x2048_WhenRequested()
        {
            var directory = Environment.GetEnvironmentVariable("CM_CHROME_CHIP_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Pass("capture disarmed — set CM_CHROME_CHIP_CAPTURE_DIR");
                yield break;
            }
            _canvas = new GameObject("ChipCaptureCanvas", typeof(Canvas));
            var canvas = _canvas.GetComponent<Canvas>();
            var cameraGo = new GameObject("ChipCaptureCamera", typeof(Camera));
            var camera = cameraGo.GetComponent<Camera>();
            var target = new RenderTexture(917, 2048, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D frame = null;
            try
            {
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette.WarmPaper;
                camera.orthographic = true;
                camera.nearClipPlane = .1f;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                string[] labels = { "Play", "Next", "Try again", "Home",
                    "Unlock · CA$2.79", "Débloquer · 2,79 $CA" };
                Color[] rings = { Palette.TicketOrange, Palette.TicketOrange,
                    Palette.MetroTeal, Palette.InkNavy, Palette.TicketOrange, Palette.TicketOrange };
                for (int i = 0; i < labels.Length; i++)
                {
                    var chip = ChromeChip.PaintPrimary(canvas.transform,
                        new Rect(0, 1730 - 270 * i, 917, 280), labels[i], rings[i],
                        i == 0 ? HudShapeSprites.Triangle : null, 408f);
                    if (i >= 4)
                    {
                        chip.Label.rectTransform.anchorMin = new Vector2(.04f, .05f);
                        chip.Label.rectTransform.anchorMax = new Vector2(.96f, .95f);
                        chip.Label.rectTransform.offsetMin = chip.Label.rectTransform.offsetMax = Vector2.zero;
                    }
                }
                var small = ChromeChip.PaintPrimary(canvas.transform,
                    new Rect(0, 0, 917, 280), "Equip", Palette.MetroTeal, null, 408f);
                small.LayoutFace(new Rect(51, 60, 397.8f, 153), 408f);
                var wardrobe = ChromeChip.PaintPrimary(canvas.transform,
                    new Rect(0, 0, 917, 280), "Wardrobe", Palette.InkNavy, null, 408f);
                wardrobe.LayoutFace(new Rect(468, 60, 397.8f, 153), 408f);
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                frame = new Texture2D(917, 2048, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                frame.Apply();
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "chrome-chips-917x2048.png"),
                    frame.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (frame != null) Object.DestroyImmediate(frame);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraGo);
            }
        }
    }
}
