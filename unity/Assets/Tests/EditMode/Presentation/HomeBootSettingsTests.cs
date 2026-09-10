using NUnit.Framework;
using UnityEditor;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests.Presentation
{
    public sealed class HomeBootSettingsTests
    {
        [Test]
        public void BootUsesInkNavy_AndTheCatIcon_WithoutUnitySplash()
        {
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
            Assert.That(PlayerSettings.SplashScreen.showUnityLogo, Is.False);
            Assert.That(PlayerSettings.SplashScreen.backgroundColor, Is.EqualTo(Palette.InkNavy));
            var splash = (UnityEngine.Texture2D)typeof(PlayerSettings.Android)
                .GetProperty("splashScreen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .GetValue(null);
            Assert.That(splash, Is.Not.Null);
            Assert.That(splash.name, Is.EqualTo("cat-metro-icon-foreground-512"));
        }
    }
}
