using NUnit.Framework;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-01 criterion 8 — labeled a PIN per P-7/A-UX1-4: green on arrival, by design. The
    // slice ships no view; this proves it structurally (the R1 tree-walk alternative was
    // frame-count-brittle because BoardView creates train objects lazily inside UpdateFrom).
    public sealed class InputFoundationPinsTests
    {
        [Test]
        public void PIN_ChromeRegionsIsPureCSharp_NeverAnEngineObject()
        {
            Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(typeof(ChromeRegions)),
                Is.False, "ChromeRegions must stay pure C# — a view would be a scope breach");
        }

        [Test]
        public void PIN_HudBandsIsPureStaticMath_NeverAnEngineObject()
        {
            Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(typeof(HudBands)), Is.False);
            Assert.That(typeof(HudBands).IsAbstract && typeof(HudBands).IsSealed, Is.True,
                "static class: band math carries no instance state");
        }
    }
}
