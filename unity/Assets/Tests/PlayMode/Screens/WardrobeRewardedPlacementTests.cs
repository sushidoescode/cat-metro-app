using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Screens;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.PlayMode
{
    // Fixed TryOn cards are retired. Catalogue reward behavior is covered through the typed
    // coordinator/route and Wardrobe flow; retain the fixture GUID's phone-size target guard.
    public sealed class WardrobeRewardedPlacementTests
    {
        [Test]
        public void CataloguePrimaryAction_MeetsThe48DpPhoneTarget()
        {
            var target = WardrobeLayout.PrimaryActionRect(new Rect(0f, 64f, 917f, 1920f), 408f);

            Assert.That(HudBands.MeetsMinTargetPx(target, 408f), Is.True);
        }
    }
}
