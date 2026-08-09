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
