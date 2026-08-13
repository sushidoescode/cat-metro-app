using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace CatMetro.Tests.Presentation
{
    // UI-CHROME criteria 1/7/8. Resource and palette laws stay in EditMode so a missing
    // font, shape, or clip is a fast deterministic defect rather than a frame-eyeball.
    public sealed class UiChromePassResourceTests
    {
        private const string ThemeTypeName =
            "CatMetro.Presentation.Hud.CatMetroUiTheme";

        private static Type ThemeType()
        {
            return typeof(CatMetro.Presentation.Hud.UiChromeMaterial).Assembly
                .GetType(ThemeTypeName);
        }

        [Test]
        public void Theme_ExposesTheTwelveAuthoritativePaletteColors_ByteExact()
        {
            var type = ThemeType();
            Assert.That(type, Is.Not.Null,
                "UI-CHROME RED: the shared CatMetroUiTheme does not exist yet");

            var expected = new Dictionary<string, Color32>
            {
                { "CreamCard", Hex("F2EAD9") },
                { "WarmPaper", Hex("FAF6EC") },
                { "InkNavy", Hex("22304A") },
                { "DepotNavy", Hex("131C30") },
                { "MetroTeal", Hex("3BAFA8") },
                { "TicketOrange", Hex("F08A3C") },
                { "SignalRed", Hex("E15A47") },
                { "HarborBlue", Hex("3E7CC9") },
                { "TabbyYellow", Hex("EFC13D") },
                { "GardenGreen", Hex("4FA36A") },
                { "CatnipViolet", Hex("A06BD8") },
                { "AlarmCoral", Hex("D93A2B") },
            };

            foreach (var pair in expected)
            {
                var property = type.GetProperty(pair.Key,
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(property, Is.Not.Null,
                    "theme is missing palette role " + pair.Key);
                var actual = (Color)property.GetValue(null);
                Assert.That((Color32)actual, Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void UiResources_FontRoundedCardAndFiveSymbols_AreCommittedAndLoadable()
        {
            var font = Resources.Load<TMP_FontAsset>(
                "CatMetroUI/Fonts/CatMetroSans SDF");
            Assert.That(font, Is.Not.Null,
                "UI-CHROME RED: CatMetro Sans TMP asset is not committed under UI/**");
            Assert.That(font.name, Is.EqualTo("CatMetroSans SDF"));

            Assert.That(Resources.Load<Sprite>(
                "CatMetroUI/Textures/RoundedRect"), Is.Not.Null);
            foreach (var shape in new[] { "Circle", "Square", "Triangle", "Diamond", "Star" })
            {
                var sprite = Resources.Load<Sprite>(
                    "CatMetroUI/Textures/Symbol" + shape);
                Assert.That(sprite, Is.Not.Null,
                    "symbol sprite missing: " + shape);
            }

            Assert.That(File.Exists(
                "Assets/UI/Fonts/LiberationSans - OFL.txt"), Is.True,
                "the redistributed typeface must carry its OFL text beside the UI asset");
        }

        [Test]
        public void StingerAssets_AreThreeDistinctNonEmptyPcmClips_WithProvenance()
        {
            var clips = new[]
            {
                Resources.Load<AudioClip>("CatMetroUI/Audio/ui-tap"),
                Resources.Load<AudioClip>("CatMetroUI/Audio/ui-warning"),
                Resources.Load<AudioClip>("CatMetroUI/Audio/ui-win"),
            };

            foreach (var clip in clips)
            {
                Assert.That(clip, Is.Not.Null,
                    "UI-CHROME RED: one or more committed stinger clips are missing");
                Assert.That(clip.samples, Is.GreaterThan(0));
                Assert.That(clip.channels, Is.EqualTo(1),
                    "small UI stingers are mono");
                Assert.That(clip.frequency, Is.EqualTo(44100));
            }
            Assert.That(clips[0].samples, Is.Not.EqualTo(clips[1].samples));
            Assert.That(clips[1].samples, Is.Not.EqualTo(clips[2].samples));
            Assert.That(File.Exists("Assets/UI/Audio/PROVENANCE.md"), Is.True,
                "original synthesis recipes must ship with the clips");
        }

        private static Color32 Hex(string rgb)
        {
            Assert.That(ColorUtility.TryParseHtmlString("#" + rgb, out var color), Is.True);
            return color;
        }
    }
}
