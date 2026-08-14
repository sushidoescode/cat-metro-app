using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests.Presentation
{
    // BEAUTIFUL-MENU criterion 1, red-first: the product_spec §7 palette ported to code as
    // named constants. Each expected color is derived INDEPENDENTLY from the spec hex string,
    // so a mistyped channel in Palette.cs fails here rather than silently shipping an
    // off-brand color. (Palette is a plain static class, so it is invisible to the
    // HomeScreenTests component whitelist.)
    public sealed class PaletteTests
    {
        private static Color Hex(string hex)
        {
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        [Test]
        public void BasePalette_MatchesProductSpecSection7Hex()
        {
            Assert.That(Palette.WarmPaper, Is.EqualTo(Hex("FAF6EC")), "Warm Paper");
            Assert.That(Palette.CreamCard, Is.EqualTo(Hex("F2EAD9")), "Cream Card");
            Assert.That(Palette.InkNavy, Is.EqualTo(Hex("22304A")), "Ink Navy");
            Assert.That(Palette.DepotNavy, Is.EqualTo(Hex("131C30")), "Depot Navy");
            Assert.That(Palette.MetroTeal, Is.EqualTo(Hex("3BAFA8")), "Metro Teal");
            Assert.That(Palette.TicketOrange, Is.EqualTo(Hex("F08A3C")), "Ticket Orange");
        }

        [Test]
        public void LinePalette_MatchesProductSpecSection7Hex()
        {
            Assert.That(Palette.SignalRed, Is.EqualTo(Hex("E15A47")), "Signal Red");
            Assert.That(Palette.HarborBlue, Is.EqualTo(Hex("3E7CC9")), "Harbor Blue");
            Assert.That(Palette.TabbyYellow, Is.EqualTo(Hex("EFC13D")), "Tabby Yellow");
            Assert.That(Palette.GardenGreen, Is.EqualTo(Hex("4FA36A")), "Garden Green");
            Assert.That(Palette.CatnipViolet, Is.EqualTo(Hex("A06BD8")), "Catnip Violet");
            Assert.That(Palette.AlarmCoral, Is.EqualTo(Hex("D93A2B")), "Alarm Coral");
        }

        [Test]
        public void EveryConstant_IsFullyOpaque()
        {
            foreach (var c in new[] {
                Palette.WarmPaper, Palette.CreamCard, Palette.InkNavy, Palette.DepotNavy,
                Palette.MetroTeal, Palette.TicketOrange, Palette.SignalRed, Palette.HarborBlue,
                Palette.TabbyYellow, Palette.GardenGreen, Palette.CatnipViolet, Palette.AlarmCoral })
            {
                Assert.That(c.a, Is.EqualTo(1f), "palette constants carry no baked alpha");
            }
        }
    }
}
