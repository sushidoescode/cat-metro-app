using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
        public IEnumerator Home_PaintsTheWarmTabletopPalette()
        {
            CreateShown();
            yield return null;

            Assert.That(_home.BackgroundColor, Is.EqualTo(Palette.WarmPaper),
                "Home has a full-bleed warm-paper background (was transparent)");
            Assert.That(_home.TitleColor, Is.EqualTo(Palette.InkNavy),
                "the title is ink navy, not the old pure white");
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
        public IEnumerator RouteCard_UsesTheHeroStage_PaletteMarkers_AndLabelledPrimaryCta()
        {
            CreateShown();
            yield return null;

            Assert.That(Find("HeroCard"), Is.Not.Null,
                "Home supplies one visually dominant route-card stage");
            Assert.That(Find("DepotPlatform"), Is.Not.Null,
                "the stage contains a depot platform, not empty silhouette blocks");
            Assert.That(Find("RailNorth"), Is.Not.Null,
                "the stage contains geometric rail structure");
            Assert.That(Find("Sleeper03"), Is.Not.Null,
                "the rail has repeated cream sleepers");

            var label = Find("PlayLabel");
            Assert.That(label, Is.Not.Null, "the primary action is labelled");
            Assert.That(label.GetComponent<TMP_Text>().text,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("intro.play")));

            Assert.That(Find("ParkedDistrictA").GetComponent<UnityEngine.UI.Image>(), Is.Not.Null);
            Assert.That(Find("ParkedDistrictB").GetComponent<UnityEngine.UI.Image>(), Is.Not.Null);
            Assert.That(Find("ParkedDistrictC").GetComponent<UnityEngine.UI.Image>(), Is.Not.Null,
                "cat-wire retains all three exact Image holder nodes");
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
            Assert.That(_home.HeroRectPx.y, Is.EqualTo(329.2f).Within(0.01f));
            Assert.That(_home.HeroRectPx.width, Is.EqualTo(815f).Within(0.01f));
            Assert.That(_home.HeroRectPx.height, Is.EqualTo(1399.8f).Within(0.01f));
            Assert.That(_home.PrimaryLabelText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("intro.play")));
            Assert.That(_home.MarkerCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[]
            {
                Palette.SignalRed, Palette.HarborBlue, Palette.TabbyYellow,
            }, _home.MarkerColors);
            yield return null;
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
            Assert.That(count, Is.EqualTo(1), name + " remains one direct HeroCard child");
            return result;
        }
    }
}
