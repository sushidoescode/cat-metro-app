using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
    }
}
