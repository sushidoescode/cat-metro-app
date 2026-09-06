using System;
using System.Collections;
using System.IO;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class SettingsSheetCaptureTests
    {
        [UnityTest]
        public IEnumerator CaptureEvidence_SettingsSheet_WhenRequested()
        {
            string dir = Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) { Assert.Pass("settings capture disarmed"); yield break; }
            // Isolated UI only: no cats, props, or placeholder board. Licensed-art composition
            // captures still run from main with its admitted rig, as required by AGENTS.md.
            var host = new GameObject("SettingsCapture");
            var camera = host.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.DepotNavy;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(host.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var sheet = SettingsSheet.Create(canvasGo.transform);
            sheet.Attach(new ChromeRegions());
            var target = new RenderTexture(917, 2048, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            target.Create(); camera.targetTexture = target;
            camera.aspect = 917f / 2048;
            try
            {
                Directory.CreateDirectory(dir);
                foreach (bool daily in new[] { false, true })
                {
                    sheet.Configure(!daily, true, !daily, daily, daily);
                    sheet.Show();
                    yield return null;
                    sheet.LayoutForViewport(new Rect(0, 64, 917, 1920), 408);
                    Canvas.ForceUpdateCanvases();
                    camera.Render(); RenderTexture.active = target;
                    var pixels = new Texture2D(917, 2048, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(dir, daily ? "settings-daily.png" : "settings-default.png"), pixels.EncodeToPNG());
                    Object.DestroyImmediate(pixels);
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(host);
            }
        }
    }
}
