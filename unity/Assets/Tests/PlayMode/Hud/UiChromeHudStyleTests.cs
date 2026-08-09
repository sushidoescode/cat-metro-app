using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;

namespace CatMetro.Tests.PlayMode
{
    // UI-CHROME criteria 4/5. Exact role colors and named geometry make the visual law
    // mutation-capable while rendered frames remain the final composition evidence.
    public sealed class UiChromeHudStyleTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            _host = null;
        }

        [UnityTest]
        public IEnumerator Banner_IsTmpPaperCard_WithOutcomeSpecificShapeKeyline()
        {
            _host = new GameObject("BannerStyleHost");
            var banner = BannerView.Create(_host.transform);
            banner.ShowKey("fail.banner.timeout");
            yield return null;

            Assert.That(banner.GetComponentsInChildren<TextMesh>(true).Length, Is.Zero,
                "outcome chrome is TMP/UGUI, not a new world-space TextMesh");
            AssertImage(banner.transform, "BannerCard", "FAF6EC");
            AssertImage(banner.transform, "BannerKeyline", "D93A2B");
            var text = Find(banner.transform, "BannerText").GetComponent<TMP_Text>();
            AssertThemeFont(text);
            Assert.That((Color32)text.color, Is.EqualTo(Hex("22304A")));

            banner.ShowKey("win.banner");
            yield return null;
            AssertImage(banner.transform, "BannerKeyline", "3BAFA8");
        }

        [UnityTest]
        public IEnumerator HintRetryAndHalt_UseTheSharedPaletteFontAndShapeSignals()
        {
            var canvas = NewCanvas();
            var retry = RetryCtaView.Create(canvas.transform);
            var hint = HintChipView.Create(canvas.transform);
            var halt = HaltVeilView.Create(canvas.transform);
            retry.SetVisible(true);
            hint.SetVisible(true);
            halt.SetVisible(true);
            yield return null;

            AssertImage(retry.transform, "RetryCta", "F08A3C");
            Assert.That((Color32)retry.GetComponentInChildren<TMP_Text>(true).color,
                Is.EqualTo(Hex("22304A")));

            AssertImage(hint.transform, "HintChip", "3BAFA8");
            var hintMarker = Find(hint.transform, "CatEarMarker");
            Assert.That(hintMarker, Is.Not.Null,
                "the hint has a non-color shape marker");
            var hintEarLeft = Find(hintMarker, "MarkerEarLeft").GetComponent<Image>();
            var hintEarRight = Find(hintMarker, "MarkerEarRight").GetComponent<Image>();
            Assert.That(hintEarLeft.sprite.name,
                Is.EqualTo("SymbolTriangle"), "the hint marker reads as cat ears");
            Assert.That(hintEarRight.sprite.name, Is.EqualTo("SymbolTriangle"));
            Assert.That(hintEarLeft.rectTransform.anchorMax.y,
                Is.GreaterThanOrEqualTo(1.25f), "the ear protrudes above the marker head");
            Assert.That(hintEarRight.rectTransform.anchorMax.y,
                Is.GreaterThanOrEqualTo(1.25f));
            Assert.That((Color32)hint.GetComponentInChildren<TMP_Text>(true).color,
                Is.EqualTo(Hex("FAF6EC")));

            AssertImage(halt.transform, "HaltVeil", "131C30", 0.70f, 0.90f,
                requireRoundedSprite: false);
            Assert.That((Color32)halt.GetComponentInChildren<TMP_Text>(true).color,
                Is.EqualTo(Hex("FAF6EC")));

            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                AssertThemeFont(text);
        }

        [UnityTest]
        public IEnumerator Results_HasPaperCompletionCardMotifConfettiAndOneOrangeCta()
        {
            _host = new GameObject("ResultsStyleHost");
            string state = "Won";
            var regions = new ChromeRegions();
            var panel = _host.AddComponent<ResultsPanel>();
            panel.Attach(() => state, regions);
            yield return null;

            AssertImage(panel.PanelRoot.transform, "CompletionCard", "FAF6EC");
            AssertImage(panel.PanelRoot.transform, "RouteMotif", "3BAFA8");
            AssertImage(panel.PanelRoot.transform, "ConfettiTeal", "3BAFA8",
                requireRoundedSprite: false);
            AssertImage(panel.PanelRoot.transform, "ConfettiOrange", "F08A3C",
                requireRoundedSprite: false);
            AssertImage(panel.PanelRoot.transform, "PrimaryCta", "F08A3C");
            var cat = Find(panel.PanelRoot.transform, "CompletionCat");
            var completionEarLeft = Find(cat, "CompletionEarLeft").GetComponent<Image>();
            var completionEarRight = Find(cat, "CompletionEarRight").GetComponent<Image>();
            Assert.That(completionEarLeft.sprite.name,
                Is.EqualTo("SymbolTriangle"), "the completion motif reads as a cat");
            Assert.That(completionEarRight.sprite.name, Is.EqualTo("SymbolTriangle"));
            Assert.That(completionEarLeft.rectTransform.anchorMax.y,
                Is.GreaterThanOrEqualTo(1.25f), "the ear protrudes above the cat head");
            Assert.That(completionEarRight.rectTransform.anchorMax.y,
                Is.GreaterThanOrEqualTo(1.25f));
            var cta = Find(panel.PanelRoot.transform, "CtaLabel").GetComponent<TMP_Text>();
            AssertThemeFont(cta);
            Assert.That((Color32)cta.color, Is.EqualTo(Hex("22304A")));

            Assert.That(regions.Count, Is.EqualTo(1));
            Assert.That(panel.FooterRoot.childCount, Is.Zero,
                "polish does not invent footer content");
            Assert.That(Find(panel.PanelRoot.transform, "CompletionCard"), Is.Not.Null,
                "mutation target: removing the completion card must turn this red");
        }

        [UnityTest]
        public IEnumerator Results_ConfettiClearsTheForegroundWinBannerBand()
        {
            _host = new GameObject("ResultsConfettiHost");
            var panel = _host.AddComponent<ResultsPanel>();
            panel.Attach(() => "Won", new ChromeRegions());
            yield return null;

            var teal = Find(panel.PanelRoot.transform, "ConfettiTeal")
                .GetComponent<RectTransform>();
            var orange = Find(panel.PanelRoot.transform, "ConfettiOrange")
                .GetComponent<RectTransform>();
            Assert.That(teal.anchorMax.y, Is.LessThan(0.67f),
                "teal confetti must sit below the foreground banner's lower edge");
            Assert.That(orange.anchorMax.y, Is.LessThan(0.67f),
                "orange confetti must sit below the foreground banner's lower edge");
            Assert.That(teal.anchorMax.x, Is.LessThanOrEqualTo(0.09f),
                "teal confetti stays visible beside the completion card");
            Assert.That(orange.anchorMin.x, Is.GreaterThanOrEqualTo(0.91f),
                "orange confetti stays visible beside the completion card");
        }

        private Canvas NewCanvas()
        {
            _host = new GameObject("UiChromeHudStyleHost");
            var canvas = _host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item;
            Assert.Fail("UI-CHROME RED: missing styled node " + name);
            return null;
        }

        private static void AssertImage(Transform root, string name, string hex,
            float minAlpha = 0.99f, float maxAlpha = 1.01f,
            bool requireRoundedSprite = true)
        {
            var image = Find(root, name).GetComponent<Image>();
            Assert.That(image, Is.Not.Null, name + " must be an Image");
            var expected = Hex(hex);
            var actual = (Color32)image.color;
            Assert.That(actual.r, Is.EqualTo(expected.r), name);
            Assert.That(actual.g, Is.EqualTo(expected.g), name);
            Assert.That(actual.b, Is.EqualTo(expected.b), name);
            Assert.That(image.color.a, Is.InRange(minAlpha, maxAlpha), name);
            if (requireRoundedSprite)
                Assert.That(image.sprite, Is.Not.Null,
                    name + " uses the shared rounded sprite");
        }

        private static void AssertThemeFont(TMP_Text text)
        {
            Assert.That(text.font, Is.Not.Null, text.name);
            Assert.That(text.font.name, Is.EqualTo("CatMetroSans SDF"), text.name);
        }

        private static Color32 Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            return color;
        }
    }
}
