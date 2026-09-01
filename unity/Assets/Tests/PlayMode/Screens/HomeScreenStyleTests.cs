using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests.PlayMode
{
    // BEAUTIFUL-MENU criterion 4(b): the warm-tabletop restyle paints the product_spec §7
    // palette, not the old grey/transparent greybox. Direct construction (the HomeScreenTests
    // pattern); every assertion reads the REAL tree. STYLE assertions layered ON TOP of —
    // never replacing — HomeScreenTests' structural/whitelist/tripwire laws, which stay green.
    public sealed class HomeScreenStyleTests
    {
        private GameObject _canvasGo;
        private HomeScreenView _home;

        private HomeScreenView CreateShown()
        {
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _home = HomeScreenView.Create(canvas.transform);
            _home.Attach(new ChromeRegions(), () => false);
            _home.Show();
            return _home;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            _canvasGo = null; _home = null;
        }

        [UnityTest]
        public IEnumerator Home_PaintsTheTransparentBoardWindowPalette()
        {
            CreateShown();
            yield return null;

            Assert.That(_home.BackgroundColor,
                Is.EqualTo(Palette.WithAlpha(Palette.WarmPaper, 0f)),
                "Home leaves the already-loaded tick-0 board visible");
            Assert.That(_home.TitleColor, Is.EqualTo(Palette.CreamCard),
                "the raised title lettering is cream on the carved navy plaque");
            Assert.That(_home.PinRingColor, Is.EqualTo(Palette.TicketOrange),
                "the L001 pin's raised ring is the ticket-orange CTA glow");
        }

        [UnityTest]
        public IEnumerator Restyle_PreservesTheStructuralInvariants()
        {
            CreateShown();
            yield return null;

            // the restyle must not disturb what HomeScreenTests locks: csv title, ring visible.
            Assert.That(_home.TitleText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("home.title")),
                "the restyle keeps the csv-keyed title");
            Assert.That(_home.RingVisible, Is.True, "the ring still renders after the restyle");
        }

        [UnityTest]
        public IEnumerator DioramaWindow_IsGraphicFree_AndFramedWithPaletteSurfaces()
        {
            CreateShown();
            yield return null;

            var hero = Find("HeroCard");
            var window = Find("DioramaWindow");
            Assert.That(hero, Is.Not.Null, "Home supplies one dominant diorama stage");
            Assert.That(hero.GetComponent<Graphic>(), Is.Null,
                "the hero parent cannot paint over the live board");
            Assert.That(window, Is.Not.Null, "the stage names its transparent board window");
            Assert.That(window.GetComponent<Graphic>(), Is.Null,
                "the window itself is a real hole, not a clear-looking opaque card");

            Assert.That(ImageColor("DioramaFrameTop"), Is.EqualTo(Palette.CreamCard));
            Assert.That(ImageColor("DioramaFrameBottom"), Is.EqualTo(Palette.CreamCard));
            Assert.That(ImageColor("DioramaFrameLeft"), Is.EqualTo(Palette.CreamCard));
            Assert.That(ImageColor("DioramaFrameRight"), Is.EqualTo(Palette.CreamCard));
            Assert.That(ImageColor("DioramaInnerTop"), Is.EqualTo(Palette.InkNavy));
            Assert.That(ImageColor("BackdropLeft"),
                Is.EqualTo(Palette.WithAlpha(Palette.DepotNavy, 0.48f)),
                "palette shade surrounds the window without filling its center");

            var plaque = Find("TitlePlaque");
            Assert.That(plaque, Is.Not.Null);
            Assert.That(plaque.GetComponent<Image>().color, Is.EqualTo(Palette.DepotNavy));
        }

        [UnityTest]
        public IEnumerator Routes_UseRaisedCreamFaces_WithNavyLabels()
        {
            CreateShown();
            _home.UnlockDaily(12);
            yield return null;

            var label = Find("PlayLabel");
            Assert.That(label, Is.Not.Null, "the primary action is labelled");
            Assert.That(label.GetComponent<TMP_Text>().text,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("intro.play")));
            Assert.That(label.GetComponent<TMP_Text>().color, Is.EqualTo(Palette.InkNavy));
            Assert.That(ImageColor("PlayButtonFace"), Is.EqualTo(Palette.CreamCard));
            Assert.That(ImageColor("DailyButtonFace"), Is.EqualTo(Palette.CreamCard));
            Assert.That(Find("PinDailyLabel").GetComponent<TMP_Text>().color,
                Is.EqualTo(Palette.InkNavy));
        }

        [UnityTest]
        public IEnumerator Restyle_PreservesHolderIdentity_AndModelRootLocalPosition()
        {
            CreateShown();
            yield return null;

            var hero = Find("HeroCard");
            Transform holderB = null;
            foreach (var name in new[]
                     {
                         "ParkedDistrictA", "ParkedDistrictB", "ParkedDistrictC",
                     })
            {
                var holder = DirectChild(hero, name);
                Assert.That(holder.GetComponent<Image>(), Is.Not.Null,
                    name + " retains its exact Image holder type");
                if (name == "ParkedDistrictB") holderB = holder;
            }

            var modelRoot = new GameObject("PinnedModelRoot").transform;
            modelRoot.SetParent(holderB, false);
            var pinned = new Vector3(0.031f, -0.017f, 0.044f);
            modelRoot.localPosition = pinned;
            _home.LayoutForViewport(new Rect(0f, 64f, 917f, 1920f), 408f);
            _home.Hide();
            _home.Show();
            Assert.That(modelRoot.parent, Is.SameAs(holderB));
            Assert.That(modelRoot.localPosition, Is.EqualTo(pinned),
                "Home layout/show must never reset an admitted prefab root's localPosition");
        }

        [UnityTest]
        public IEnumerator DirectConstructionWithoutPortraitSource_PreservesFallbackHolderPaint()
        {
            CreateShown();
            yield return null;

            var hero = Find("HeroCard");
            Assert.That(hero, Is.Not.Null);
            foreach (var name in new[]
                     {
                         "ParkedDistrictA", "ParkedDistrictB", "ParkedDistrictC",
                     })
            {
                var holder = DirectChild(hero, name);
                var image = holder.GetComponent<UnityEngine.UI.Image>();
                Assert.That(image, Is.Not.Null, name + " remains an Image holder");
                Assert.That(image.color,
                    Is.EqualTo(Palette.WithAlpha(Palette.DepotNavy, 0.18f)),
                    name + " retains the no-source fallback paint");
                Assert.That(holder.GetComponentsInChildren<CosmeticPortraitView>(true),
                    Is.Empty, name + " has no shared portrait without a source");
            }
        }

        [UnityTest]
        public IEnumerator LayoutForViewport_ReportsTheCaptureHero_Label_AndMarkers()
        {
            CreateShown();
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            _home.LayoutForViewport(safeArea, 408f);
            Canvas.ForceUpdateCanvases();

            Assert.That(_home.HeroRectPx.x, Is.EqualTo(51f).Within(0.01f));
            Assert.That(_home.HeroRectPx.y, Is.EqualTo(472f).Within(0.01f));
            Assert.That(_home.HeroRectPx.width, Is.EqualTo(815f).Within(0.01f));
            Assert.That(_home.HeroRectPx.height, Is.EqualTo(1257f).Within(0.01f));
            Assert.That(_home.PrimaryLabelText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("intro.play")));
            Assert.That(_home.MarkerCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[]
            {
                Palette.SignalRed, Palette.HarborBlue, Palette.TabbyYellow,
            }, _home.MarkerColors);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitViewport_ShadeCoversOnlyOutsideTheDioramaAperture()
        {
            CreateShown();
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            var viewport = new Rect(0f, 0f, 917f, 2048f);
            _home.LayoutForViewport(safeArea, 408f, viewport);

            var hero = HomeLayout.HeroRect(safeArea, 408f);
            float xMin = hero.x + hero.width * 0.075f;
            float xMax = hero.x + hero.width * 0.925f;
            float yMin = hero.y + hero.height * 0.075f;
            float yMax = hero.y + hero.height * 0.93f;
            AssertRect(PaintedRect(Find("BackdropTop") as RectTransform),
                new Rect(0f, yMax, viewport.width, viewport.height - yMax));
            AssertRect(PaintedRect(Find("BackdropBottom") as RectTransform),
                new Rect(0f, 0f, viewport.width, yMin));
            AssertRect(PaintedRect(Find("BackdropLeft") as RectTransform),
                new Rect(0f, yMin, xMin, yMax - yMin));
            AssertRect(PaintedRect(Find("BackdropRight") as RectTransform),
                new Rect(xMax, yMin, viewport.width - xMax, yMax - yMin));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnlockedDaily_ReservesItsRouteSlot_BelowTheDiorama()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _home = HomeScreenView.Create(canvas.transform,
                dailyEntryUnlocked: true, lifetimeDailyCompletions: 7);
            _home.Attach(new ChromeRegions(), () => false);
            _home.Show();

            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            _home.LayoutForViewport(safeArea, 408f);
            Assert.That(_home.HeroRectPx,
                Is.EqualTo(HomeLayout.HeroRect(safeArea, 408f, dailyEntryUnlocked: true)),
                "the window rises above all three unlocked routes");
            yield return null;
        }

        private Color ImageColor(string name)
        {
            var found = Find(name);
            Assert.That(found, Is.Not.Null, name + " must exist");
            var image = found.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, name + " must be an Image");
            return image.color;
        }

        private Transform Find(string name)
        {
            foreach (var transform in _home.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform;
            return null;
        }

        private static Transform DirectChild(Transform parent, string name)
        {
            Transform result = null;
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name != name) continue;
                result = parent.GetChild(i);
                count++;
            }
            Assert.That(count, Is.EqualTo(1),
                name + " remains one direct HeroCard child");
            return result;
        }

        private static Rect PaintedRect(RectTransform rect)
        {
            Assert.That(rect, Is.Not.Null);
            return new Rect(rect.anchoredPosition.x - rect.sizeDelta.x * rect.pivot.x,
                rect.anchoredPosition.y - rect.sizeDelta.y * rect.pivot.y,
                rect.sizeDelta.x, rect.sizeDelta.y);
        }

        private static void AssertRect(Rect actual, Rect expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(0.01f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.01f));
        }
    }
}
