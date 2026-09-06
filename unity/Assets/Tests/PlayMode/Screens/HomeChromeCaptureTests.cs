using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests.PlayMode
{
    // Isolated chrome proof: no board, cat catalog, licensed art, or GameRoot. The complete
    // first-frame gate remains UiPhoneCaptureTests in the main asset checkout.
    public sealed class HomeChromeCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureChromeOnly_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_HOME_CHROME_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            var cameraObject = new GameObject("ChromeCaptureCamera");
            var canvasObject = new GameObject("ChromeCaptureCanvas");
            var target = new RenderTexture(917, 2048, 24);
            Texture2D pixels = null;
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette.WarmPaper;
                camera.orthographic = true;
                camera.targetTexture = target;
                camera.aspect = 917f / 2048f;
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                var home = HomeScreenView.Create(canvas.transform);
                var wardrobe = WardrobeScreenView.Create(canvas.transform, null);
                wardrobe.Attach(new ChromeRegions());
                wardrobe.ShowEntry();
                home.Attach(new ChromeRegions(), () => true);
                home.ConfigureAudio(false);
                home.Show();
                yield return null;
                Directory.CreateDirectory(dir);
                foreach (bool unlocked in new[] { false, true })
                {
                    if (unlocked) home.UnlockDaily(12);
                    home.LayoutForViewport(new Rect(0, 64, 917, 1920), 408,
                        new Rect(0, 0, 917, 2048));
                    wardrobe.LayoutForViewport(new Rect(0, 64, 917, 1920), 408);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels = new Texture2D(917, 2048, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                    pixels.Apply();
                    var corner = pixels.GetPixel(5, 5);
                    Assert.That(Mathf.Max(corner.r, corner.g, corner.b), Is.LessThan(0.25f),
                        "even over a cream test background the outer corners read as dark warm wood");
                    File.WriteAllBytes(Path.Combine(dir,
                        unlocked ? "chrome-only-daily.png" : "chrome-only-fresh.png"), pixels.EncodeToPNG());
                    Object.Destroy(pixels);
                    pixels = null;
                }
                Assert.That(home.TitleText, Is.Not.Empty);
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.Destroy(pixels);
                Object.Destroy(canvasObject);
                Object.Destroy(cameraObject);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
