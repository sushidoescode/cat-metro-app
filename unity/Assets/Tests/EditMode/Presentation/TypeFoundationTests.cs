using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.Presentation
{
    public sealed class TypeFoundationTests
    {
        [Test]
        public void NewTmpLabel_InheritsFredokaSemiBold()
        {
            var go = new GameObject("DefaultFontProbe", typeof(RectTransform));
            try
            {
                var text = go.AddComponent<TextMeshProUGUI>();
                Assert.That(text.font.faceInfo.familyName, Is.EqualTo("Fredoka"));
                Assert.That(text.font.faceInfo.styleName, Is.EqualTo("SemiBold"));
                Assert.That(text.font, Is.SameAs(TMP_Settings.defaultFontAsset));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("Fredoka-SemiBold SDF", "Fredoka")]
        [TestCase("Nunito-Regular SDF", "Nunito")]
        public void ShippedFonts_AreBakedSingleAtlases_WithLatinCoverageAndFallback(
            string resourceName, string family)
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/" + resourceName);
            Assert.That(font, Is.Not.Null, resourceName);
            Assert.That(font.faceInfo.familyName, Is.EqualTo(family));
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(font.atlasTextures.Length, Is.EqualTo(1));
            Assert.That(font.atlasTextures[0].width, Is.EqualTo(1024));
            Assert.That(font.atlasTextures[0].height, Is.EqualTo(1024));
            Assert.That(font.material.mainTexture, Is.SameAs(font.atlasTextures[0]));
            for (int c = 32; c <= 255; c++)
            {
                if (c >= 127 && c <= 159) continue; // Latin-1 control characters
                Assert.That(font.HasCharacter(c), Is.True, "U+" + c.ToString("X4"));
            }
            Assert.That(font.HasCharacters("Play · Today's Line — £1.99 €2.49…"), Is.True);
            Assert.That(font.fallbackFontAssetTable.Any(f =>
                f != null && f.faceInfo.familyName == "Liberation Sans"), Is.True);
        }

        [Test]
        public void HomeLabels_NeverShrinkBelowTwelve_AndCaptionUsesNunito()
        {
            var go = new GameObject("TypeCanvas", typeof(Canvas));
            try
            {
                var home = HomeScreenView.Create(go.transform, true, 12);
                home.ConfigureAudio(true);
                home.LayoutForViewport(new Rect(0, 64, 917, 1920), 408,
                    new Rect(0, 0, 917, 2048));
                foreach (var text in home.GetComponentsInChildren<TMP_Text>(true))
                    Assert.That(text.fontSizeMin, Is.GreaterThanOrEqualTo(12), text.name);
                var caption = home.GetComponentsInChildren<TMP_Text>(true)
                    .Single(t => t.name == "LifetimeTally");
                Assert.That(caption.font.faceInfo.familyName, Is.EqualTo("Nunito"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void IntroAndHint_UseBodyFaceForInstructions()
        {
            var go = new GameObject("TypeCanvas", typeof(Canvas));
            try
            {
                var intro = LevelIntroSheet.Create(go.transform);
                var goal = intro.GetComponentsInChildren<TMP_Text>(true)
                    .Single(t => t.name == "GoalLine");
                Assert.That(goal.font.faceInfo.familyName, Is.EqualTo("Nunito"));
                var hint = HintChipView.Create(go.transform);
                Assert.That(hint.GetComponentInChildren<TMP_Text>(true).font.faceInfo.familyName,
                    Is.EqualTo("Nunito"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Rebake_ReplacesAnAlreadyLoadedGlyphLookup()
        {
            var baker = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CatMetroFontBaker")).FirstOrDefault(t => t != null);
            Assert.That(baker, Is.Not.Null);
            var replace = baker.GetMethod("ReplaceFontDefinition",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            Assert.That(replace, Is.Not.Null, "the baker refreshes serialized and cached definitions together");
            var source = Object.Instantiate(TMP_Settings.defaultFontAsset);
            var destination = Object.Instantiate(TMP_Settings.defaultFontAsset);
            try
            {
                float expected = source.characterLookupTable['A'].glyph.metrics.horizontalAdvance;
                var oldGlyph = destination.characterLookupTable['A'].glyph;
                var oldMetrics = oldGlyph.metrics;
                oldMetrics.horizontalAdvance = 1f;
                oldGlyph.metrics = oldMetrics;
                Assert.That(destination.characterLookupTable['A'].glyph.metrics.horizontalAdvance,
                    Is.EqualTo(1f), "positive control: this font has a loaded stale lookup");
                replace.Invoke(null, new object[] { source, destination });
                Assert.That(destination.characterLookupTable['A'].glyph.metrics.horizontalAdvance,
                    Is.EqualTo(expected).Within(0.001f));
            }
            finally
            {
                // The clones borrow the shipped atlas/material; TMP destroys those references
                // from OnDestroy, so detach them before releasing these temporary definitions.
                destination.atlasTextures = System.Array.Empty<Texture2D>();
                destination.material = null;
                source.atlasTextures = System.Array.Empty<Texture2D>();
                source.material = null;
                Object.DestroyImmediate(destination);
                Object.DestroyImmediate(source);
            }
        }
    }
}
