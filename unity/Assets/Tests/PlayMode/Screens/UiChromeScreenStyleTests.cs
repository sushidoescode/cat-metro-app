using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // UI-CHROME criteria 1-3. These tests join the palette/type contract to the live
    // runtime-constructed Home and intro trees; component existence alone cannot pass.
    public sealed class UiChromeScreenStyleTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            _host = null;
        }

        [UnityTest]
        public IEnumerator Home_HasBrandedPaperLockup_AndThreeDistinctDistrictSilhouettes()
        {
            var canvas = NewCanvas();
            var regions = new ChromeRegions();
            var home = HomeScreenView.Create(canvas.transform);
            home.Attach(regions, () => true);
            home.Show();
            yield return null;

            AssertImage(home.transform, "HomePaper", "FAF6EC");
            var title = Find(home.transform, "Title").GetComponent<TMP_Text>();
            AssertThemeFont(title);
            Assert.That((Color32)title.color, Is.EqualTo(Hex("22304A")));
            Assert.That((title.fontStyle & FontStyles.Bold) != 0, Is.True);
            Assert.That((title.fontStyle & FontStyles.UpperCase) != 0, Is.True);
            Assert.That(title.characterSpacing, Is.GreaterThanOrEqualTo(4f));

            var mark = Find(home.transform, "BrandMark");
            Assert.That(mark.Find("CatHead"), Is.Not.Null);
            Assert.That(mark.Find("LeftEar"), Is.Not.Null);
            Assert.That(mark.Find("RightEar"), Is.Not.Null);
            AssertImage(mark, "RailLineTeal", "3BAFA8");
            AssertImage(mark, "RailDotOrange", "F08A3C");

            var a = Find(home.transform, "ParkedDistrictA");
            var b = Find(home.transform, "ParkedDistrictB");
            var c = Find(home.transform, "ParkedDistrictC");
            Assert.That(a.childCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(b.childCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(c.childCount, Is.GreaterThanOrEqualTo(6));
            Assert.That(new[] { a.childCount, b.childCount, c.childCount },
                Is.Unique, "the parked districts have distinct silhouettes, not three boxes");
            Assert.That(regions.Count, Is.EqualTo(1),
                "polish preserves the sole session-one action");
        }

        [UnityTest]
        public IEnumerator Intro_IsWarmPaperRouteCard_WithTealAccentAndOrangeCta()
        {
            var canvas = NewCanvas();
            var sheet = LevelIntroSheet.Create(canvas.transform);
            sheet.Attach(new ChromeRegions());
            sheet.Show("First Switch", 3);
            yield return null;

            AssertImage(sheet.transform, "IntroScrim", "22304A", 0.70f, 0.90f,
                requireRoundedSprite: false);
            Assert.That(Find(sheet.transform, "IntroScrim").GetComponent<Image>().sprite,
                Is.Null, "the full-screen staging scrim has no rounded-card corners");
            AssertImage(sheet.transform, "SheetPanel", "FAF6EC");
            AssertImage(sheet.transform, "RouteAccent", "3BAFA8");
            AssertImage(sheet.transform, "PlayChip", "F08A3C");

            var name = Find(sheet.transform, "LevelName").GetComponent<TMP_Text>();
            var goal = Find(sheet.transform, "GoalLine").GetComponent<TMP_Text>();
            var play = Find(sheet.transform, "PlayLabel").GetComponent<TMP_Text>();
            foreach (var text in new[] { name, goal, play })
            {
                AssertThemeFont(text);
                Assert.That((Color32)text.color, Is.EqualTo(Hex("22304A")));
                Assert.That(text.enableAutoSizing, Is.True,
                    text.name + " must fit scaled text without truncation");
                text.ForceMeshUpdate();
                Assert.That(text.isTextOverflowing, Is.False, text.name);
            }
            Assert.That(name.fontSizeMax, Is.GreaterThan(goal.fontSizeMax));
            Assert.That(play.fontSizeMin, Is.GreaterThanOrEqualTo(24f));
        }

        [UnityTest]
        public IEnumerator Intro_RouteMotifStaysInItsOwnBand_AboveTheLevelTitle()
        {
            var canvas = NewCanvas();
            var sheet = LevelIntroSheet.Create(canvas.transform);
            sheet.Attach(new ChromeRegions());
            sheet.Show("First Switch", 3);
            yield return null;

            var title = Find(sheet.transform, "LevelName").GetComponent<RectTransform>();
            var rail = Find(sheet.transform, "RouteAccent").GetComponent<RectTransform>();
            var marker = Find(sheet.transform, "RouteDot").GetComponent<RectTransform>();
            Assert.That(rail.anchorMin.y, Is.GreaterThan(title.anchorMax.y),
                "the teal route rail must not cross the level title glyph band");
            Assert.That(marker.anchorMin.y, Is.GreaterThan(title.anchorMax.y),
                "the orange route marker must not cross the level title glyph band");
        }

        private Canvas NewCanvas()
        {
            _host = new GameObject("UiChromeScreenStyleHost");
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
