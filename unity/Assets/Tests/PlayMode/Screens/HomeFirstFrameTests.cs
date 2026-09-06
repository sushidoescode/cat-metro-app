using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Hud.WavePreview;

namespace CatMetro.Tests.PlayMode
{
    public sealed class HomeFirstFrameTests
    {
        private GameObject _canvas;
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false;
            if (_root != null) Object.Destroy(_root.gameObject);
            if (_canvas != null) Object.Destroy(_canvas);
        }

        private HomeScreenView Home(ChromeRegions regions = null)
        {
            _canvas = new GameObject("HomeFirstFrameCanvas");
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var home = HomeScreenView.Create(canvas.transform);
            home.Attach(regions ?? new ChromeRegions(), () => true);
            home.Show();
            return home;
        }

        [Test]
        public void HomeState_HidesTheWavePreview()
        {
            Assert.That(WavePreviewStrip.VisibleInState("Home"), Is.False);
            Assert.That(WavePreviewStrip.VisibleInState("Playing"), Is.True);
        }

        [UnityTest]
        public IEnumerator HomeAndIntro_HideAllHudPaint_UntilPlayIsRequested()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
            yield return null;
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Preview.IsVisible, Is.False, "Home must not rely on the plaque covering the HUD");
            _root.Home.LevelSelected?.Invoke();
            yield return null;
            Assert.That(_root.Intro.IsVisible, Is.True);
            Assert.That(_root.Preview.IsVisible, Is.False);
            _root.Intro.PlayRequested?.Invoke();
            yield return null;
            Assert.That(_root.Preview.IsVisible, Is.True, "the gameplay HUD returns with play");
        }

        [Test]
        public void HomeWindow_ContainsNoPlaceholderFurnitureOrRouteMarkers()
        {
            var home = Home();
            var names = home.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (string name in new[] { "RouteMarkerA", "RouteMarkerB", "RouteMarkerC",
                "WindowLampLeft", "WindowLampRight", "ParkedDistrictA", "ParkedDistrictC" })
                Assert.That(names, Does.Not.Contain(name));
            Assert.That(names, Does.Contain("ParkedDistrictB"), "the real portrait mount keeps its identity");
            Assert.That(home.MarkerCount, Is.Zero);
            Assert.That(home.MarkerColors, Is.Empty);
        }

        [Test]
        public void AudioControl_IsRoundSpeakerPaint_WithSemanticOnOffLabels()
        {
            var home = Home();
            home.ConfigureAudio(true);
            home.LayoutForViewport(new Rect(0, 0, 360, 640), 160);
            var paint = home.AudioToggleTransform.GetComponent<Image>();
            Assert.That(paint.sprite, Is.SameAs(HudShapeSprites.Disc));
            Assert.That(home.AudioToggleTransform.GetComponentsInChildren<TMP_Text>()
                .Any(t => t.gameObject.activeInHierarchy), Is.False, "SFX text must not compete with the title");
            Assert.That(home.AudioToggleRectPx.width, Is.EqualTo(44));
            Assert.That(home.AudioToggleRectPx.height, Is.EqualTo(44));
            Assert.That(home.AudioToggleRectPx.center.x, Is.GreaterThan(180));
            Assert.That(home.AudioToggleText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("settings.audio.on")));
            var speaker = home.AudioToggleTransform.GetComponentsInChildren<Image>()
                .First(i => i.gameObject.name == "SpeakerGlyph");
            var onSprite = speaker.sprite;
            home.ConfigureAudio(false);
            Assert.That(speaker.sprite, Is.Not.SameAs(onSprite), "off uses a slashed speaker");
            Assert.That(home.AudioToggleText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("settings.audio.off")));
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void HeaderSign_IsCentredRegardlessOfWhichControlsExist(bool audio, bool reminder)
        {
            var safe = new Rect(0, 64, 917, 1920);
            var title = HomeLayout.TitleRect(safe, 408, audio, reminder);
            Assert.That(title.center.x, Is.EqualTo(safe.center.x).Within(0.01f));
        }

        [Test]
        public void Vignette_HasDarkWarmCorners_AndAnUnpaintedDioramaAperture()
        {
            var home = Home();
            home.LayoutForViewport(new Rect(0, 64, 917, 1920), 408, new Rect(0, 0, 917, 2048));
            var shade = home.transform.Find("HomeVignette");
            Assert.That(shade, Is.Not.Null, "one radial surround replaces four flat quads");
            var image = shade.GetComponent<Image>();
            Assert.That(image.color.r, Is.EqualTo(42f / 255f).Within(0.001f));
            Assert.That(image.color.g, Is.EqualTo(26f / 255f).Within(0.001f));
            var texture = image.sprite.texture;
            foreach (var corner in new[] { Vector2.zero, Vector2.one, Vector2.up, Vector2.right })
                Assert.That(texture.GetPixelBilinear(corner.x, corner.y).a, Is.GreaterThanOrEqualTo(0.85f));
            Assert.That(texture.GetPixelBilinear(home.HeroRectPx.center.x / 917f,
                (home.HeroRectPx.y - 20f * 2.55f) / 2048f).a, Is.GreaterThanOrEqualTo(0.85f),
                "the 40dp strip below the outer frame hides protruding props");
            var corners = new Vector3[4];
            home.DioramaWindowTransform.GetWorldCorners(corners);
            foreach (float x in new[] { 0.01f, 0.5f, 0.99f })
                foreach (float y in new[] { 0.01f, 0.5f, 0.99f })
                {
                    var point = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], x),
                        Vector3.Lerp(corners[1], corners[2], x), y);
                    Vector2 uv = new Vector2(point.x / 917f, point.y / 2048f);
                    Assert.That(texture.GetPixelBilinear(uv.x, uv.y).a, Is.Zero,
                        "the surround cannot tint any point inside the live window");
                }
        }

        [Test]
        public void LampGlow_IsRadial_AndTitleShadowHasBlurredEdges()
        {
            var home = Home();
            var lamp = home.transform.Find("TitleLampGlow");
            Assert.That(lamp, Is.Not.Null);
            var glow = lamp.GetComponent<Image>();
            Assert.That(glow.color.a, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(glow.sprite.texture.GetPixelBilinear(0.5f, 0.5f).a, Is.GreaterThan(0.95f));
            Assert.That(glow.sprite.texture.GetPixelBilinear(0, 0).a, Is.Zero);
            var shadow = home.transform.Find("TitlePlaqueShadow").GetComponent<Image>();
            Assert.That(shadow.color.a, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(shadow.sprite.texture.GetPixelBilinear(0, 0.5f).a, Is.LessThan(0.05f));
            Assert.That(shadow.sprite.texture.GetPixelBilinear(0.5f, 0.5f).a, Is.GreaterThan(0.9f));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void FirstHome_RevealsFromNavyInPointThreeSeconds_AndRespectsMotionOff(bool motionOff)
        {
            _root = GameRoot.Launch();
            _root.MotionOffToggle = motionOff;
            var cover = _root.transform.Find("ScreensCanvas/HomeBootFade");
            Assert.That(cover, Is.Not.Null);
            var paint = cover.GetComponent<Image>();
            Assert.That(paint.color, Is.EqualTo(CatMetro.Presentation.Theme.Palette.InkNavy));
            Assert.That(cover.GetSiblingIndex(), Is.EqualTo(cover.parent.childCount - 1));
            var advance = typeof(GameRoot).GetMethod("AdvanceHomeBootFade",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(advance, Is.Not.Null);
            advance.Invoke(_root, new object[] { 0.15f });
            if (motionOff) Assert.That(cover.gameObject.activeSelf, Is.False);
            else
            {
                Assert.That(paint.color.a, Is.EqualTo(0.5f).Within(.001f));
                advance.Invoke(_root, new object[] { 0.15f });
                Assert.That(cover.gameObject.activeSelf, Is.False);
            }
            _root.Home.Hide();
            _root.Home.Show();
            Assert.That(cover.gameObject.activeSelf, Is.False, "returning Home does not replay cold boot");
        }

        [Test]
        public void DailyTeaser_HasSevenPips_AndCannotLaunchBeforeUnlock()
        {
            var regions = new ChromeRegions();
            var home = Home(regions);
            Assert.That(home.DailyPinTransform, Is.Not.Null, "Daily is discoverable on the fresh profile");
            Assert.That(regions.IsRegistered("home.pin.daily"), Is.False, "the campaign gate still owns entry");
            Assert.That(home.GetComponentsInChildren<Image>(true).Count(i => i.name.StartsWith("DailyWinPip")), Is.EqualTo(7));
            Assert.That(home.DailyTallyVisible, Is.False, "a zero lifetime tally is not content");
            Assert.That(home.DailyStatusText, Is.EqualTo("Unlocks after 7 station wins"));
            home.LayoutForViewport(new Rect(0, 64, 917, 1920), 408, new Rect(0, 0, 917, 2048));
            var caption = home.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "DailyStatus");
            caption.ForceMeshUpdate();
            Assert.That(caption.isTextOverflowing, Is.False, "the locked explanation fits its half-width caption");
            home.UnlockDaily(0);
            Assert.That(regions.IsRegistered("home.pin.daily"), Is.True);
            Assert.That(home.DailyTallyVisible, Is.False);
            Assert.That(home.DailyStatusText, Is.EqualTo("Today's Line is ready"));
        }

        [Test]
        public void DailyUnlockRing_HighlightsTheFirstHomeVisitOnly()
        {
            var home = Home();
            home.Hide();
            home.UnlockDaily(0, highlight: true);
            home.Show();
            var ring = home.transform.Find("DailyUnlockRing").GetComponent<Image>();
            Assert.That(ring.gameObject.activeInHierarchy, Is.True);
            Assert.That(ring.color, Is.EqualTo(CatMetro.Presentation.Theme.Palette.TicketOrange));
            home.Hide();
            home.Show();
            Assert.That(ring.gameObject.activeSelf, Is.False, "ordinary later visits do not replay the unlock cue");
        }

        [Test]
        public void DailyErrorCaption_FitsAtThePhoneTypeFloor()
        {
            var home = Home();
            home.UnlockDaily(0);
            home.SetDailyStatusKey("home.daily.unavailable");
            home.LayoutForViewport(new Rect(0, 64, 917, 1920), 408, new Rect(0, 0, 917, 2048));
            var caption = home.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "DailyStatus");
            caption.ForceMeshUpdate();
            float paintedSize = caption.textInfo.characterInfo[0].pointSize;
            Assert.That(paintedSize, Is.GreaterThanOrEqualTo(12f * 2.55f));
            Assert.That(caption.textBounds.size.y, Is.LessThanOrEqualTo(caption.rectTransform.rect.height));
            caption.enableAutoSizing = false;
            caption.fontSize = paintedSize; // measure the size actually used, not the auto-size maximum
            var preferred = caption.GetPreferredValues(caption.text, caption.rectTransform.rect.width, Mathf.Infinity);
            Assert.That(preferred.y, Is.LessThanOrEqualTo(caption.rectTransform.rect.height),
                "a wrapped transient error remains within the reserved caption area");
        }

        [Test]
        public void DioramaFrame_UsesThinCreamEdges_WithoutAnInnerNavyBorder()
        {
            var home = Home();
            var hero = home.transform.Find("HeroCard");
            Assert.That(hero.Cast<Transform>().Any(t => t.name.StartsWith("DioramaInner")), Is.False);
            Assert.That(home.DioramaWindowTransform.anchorMin, Is.EqualTo(new Vector2(.05f, .05f)));
            Assert.That(home.DioramaWindowTransform.anchorMax, Is.EqualTo(new Vector2(.95f, .95f)));
            var left = hero.Find("DioramaFrameLeft") as RectTransform;
            Assert.That(left.anchorMax.x - left.anchorMin.x, Is.EqualTo(.03f).Within(.0001f));
        }

        [Test]
        public void TitleSign_HasTwoCarvedLines_FourNails_AndTheIconCatMark()
        {
            var home = Home();
            home.ConfigureAudio(false);
            home.LayoutForViewport(new Rect(0, 64, 917, 1920), 408, new Rect(0, 0, 917, 2048));
            Assert.That(HomeLayout.HeaderHeightDp, Is.EqualTo(130f));
            Assert.That(home.TitleText, Is.EqualTo("Cat\nMetro"));
            var title = home.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Title");
            Assert.That(title.font.faceInfo.familyName, Is.EqualTo("Fredoka"));
            Assert.That(title.lineSpacing, Is.EqualTo(-10.2f).Within(0.01f));
            var nails = home.GetComponentsInChildren<Image>(true)
                .Where(i => i.name.StartsWith("TitleNail")).ToArray();
            Assert.That(nails.Length, Is.EqualTo(4));
            foreach (var nail in nails)
            {
                Assert.That(nail.sprite, Is.SameAs(HudShapeSprites.Disc));
                Assert.That(nail.rectTransform.rect.width, Is.EqualTo(15.3f).Within(0.01f));
                Assert.That(nail.rectTransform.rect.height, Is.EqualTo(15.3f).Within(0.01f));
            }
            var mark = home.transform.Find("TitlePlaque/TitleCatMark");
            Assert.That(mark, Is.Not.Null);
            var markImage = mark.GetComponent<Image>();
            Assert.That(markImage.sprite, Is.Not.Null);
            Assert.That(markImage.sprite.texture.name, Is.EqualTo("cat-metro-icon-foreground-512"));
            Assert.That(markImage.sprite.rect.size, Is.EqualTo(new Vector2(311, 326)),
                "native sprite crop reuses the icon head");
            Assert.That(markImage.sprite.vertices.Length, Is.EqualTo(47));
            Assert.That(markImage.useSpriteMesh, Is.True, "the head silhouette clips the orange icon backing");
        }
    }
}
