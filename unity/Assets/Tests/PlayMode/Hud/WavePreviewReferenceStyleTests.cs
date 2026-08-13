using System.Collections;
using System.IO;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Hud;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    // UI-CHROME criterion 6, visual-only increment: the golden target presents upcoming
    // waves as colored cat faces with redundant symbol badges inside one cream tray.
    // Primitive-path removal is intentionally tested in WavePreviewStyleTests instead.
    public sealed class WavePreviewReferenceStyleTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator TwoAuthoredWaves_RenderAsCreamTray_CatFaces_SymbolsAndCounts()
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content/levels/L002.json");
            var imported = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(imported.Ok, Is.True, "L002 must import: " + imported.Error);
            _root = GameRoot.LaunchWith(imported.Value);
            yield return null;

            var preview = _root.Preview;
            var canvas = Find(preview.transform, "WavePreviewCanvas").GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            // F-2 (round-1 review fix): ScreenSpaceCamera disagrees with the diorama board
            // camera's off-centre custom projection/view matrices and projects the strip off
            // its safe-area band (measured y~=2958-3073 on a 2000-high screen). This PR-added
            // pin is revised to Overlay, which this PR's own fix now uses; it is not a
            // weakened pre-existing law.
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(preview.GetComponentsInChildren<GraphicRaycaster>(true).Length,
                Is.Zero, "the reference strip remains display-only");

            var tray = Find(preview.transform, "WaveTray").GetComponent<Image>();
            Assert.That(tray, Is.Not.Null);
            Assert.That((Color32)tray.color, Is.EqualTo(Hex("FAF6EC")));
            Assert.That(tray.sprite, Is.Not.Null, "the cream tray uses rounded geometry");
            Assert.That(tray.rectTransform.rect.width,
                Is.GreaterThan(tray.rectTransform.rect.height * 3f),
                "the composition reads as one horizontal strip");

            AssertEntry(preview.transform, 0, "E15A47", "SymbolCircle", "x2");
            AssertEntry(preview.transform, 1, "3E7CC9", "SymbolSquare", "x2");
            Assert.That(preview.ChipSummary, Is.EqualTo("red x2|blue x2"));
            Assert.That(preview.InTopBand(0), Is.True);
            Assert.That(preview.InTopBand(1), Is.True);
        }

        private static void AssertEntry(Transform preview, int index, string colorHex,
            string symbolName, string countValue)
        {
            var chip = Find(preview, "WaveChip" + index);
            var card = chip.GetComponent<Image>();
            Assert.That(card, Is.Not.Null);
            Assert.That((Color32)card.color, Is.EqualTo(Hex("FAF6EC")));
            Assert.That(card.sprite, Is.Not.Null);

            var face = Find(chip, "WaveCatFace").GetComponent<Image>();
            Assert.That(face, Is.Not.Null);
            Assert.That((Color32)face.color, Is.EqualTo(Hex(colorHex)));
            Assert.That(face.sprite, Is.Not.Null);
            Assert.That(face.rectTransform.rect.height,
                Is.GreaterThanOrEqualTo(32f * HudBands.PxPerDp(Screen.dpi)));

            AssertTint(chip, "WaveCatEarLeft", colorHex);
            AssertTint(chip, "WaveCatEarRight", colorHex);
            AssertTint(chip, "WaveCatEyeLeft", "22304A");
            AssertTint(chip, "WaveCatEyeRight", "22304A");
            AssertTint(chip, "WaveCatNose", "22304A");

            var badge = Find(chip, "WaveSymbolBadge").GetComponent<Image>();
            Assert.That((Color32)badge.color, Is.EqualTo(Hex("FAF6EC")));
            Assert.That(badge.sprite, Is.Not.Null);
            var symbol = Find(chip, "WaveSymbol").GetComponent<Image>();
            Assert.That(symbol.sprite, Is.Not.Null);
            Assert.That(symbol.sprite.name, Is.EqualTo(symbolName));
            Assert.That((Color32)symbol.color, Is.EqualTo(Hex(colorHex)));

            var count = Find(chip, "WaveCount").GetComponent<TMP_Text>();
            Assert.That(count.text, Is.EqualTo(countValue));
            Assert.That(count.font.name, Is.EqualTo("CatMetroSans SDF"));
            Assert.That((Color32)count.color, Is.EqualTo(Hex("22304A")));
            Assert.That(count.fontSize,
                Is.GreaterThanOrEqualTo(24f * HudBands.PxPerDp(Screen.dpi)));
            count.ForceMeshUpdate();
            Assert.That(count.textBounds.size.x, Is.GreaterThan(8f));
            Assert.That(count.textBounds.size.y, Is.GreaterThan(8f));
            Assert.That(count.preferredWidth,
                Is.LessThanOrEqualTo(count.rectTransform.rect.width + 0.5f));
            Assert.That(count.isTextOverflowing, Is.False);
        }

        private static void AssertTint(Transform root, string name, string rgb)
        {
            var image = Find(root, name).GetComponent<Image>();
            Assert.That(image, Is.Not.Null);
            Assert.That((Color32)image.color, Is.EqualTo(Hex(rgb)));
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
