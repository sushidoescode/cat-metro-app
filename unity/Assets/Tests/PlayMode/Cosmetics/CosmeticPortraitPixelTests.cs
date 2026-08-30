using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    public sealed class CosmeticPortraitPixelTests
    {
        private const int CaptureWidth = 917;
        private const int CaptureHeight = 2048;
        private const int PortraitSize = 360;

        private GameObject _cameraHost;
        private GameObject _canvasHost;
        private Camera _camera;
        private RenderTexture _target;
        private PortraitTestSource _source;
        private CosmeticPortraitView _view;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _cameraHost = new GameObject("PortraitPixelCamera");
            _camera = _cameraHost.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.WarmPaper;
            _target = new RenderTexture(CaptureWidth, CaptureHeight, 24,
                RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target;

            _canvasHost = new GameObject("PortraitPixelCanvas", typeof(RectTransform));
            var canvas = _canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            _canvasHost.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            _source = PortraitTestSource.WithRealTokens(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", ""));
            _view = CosmeticPortraitView.Create(canvas.transform, _source, "PixelPortrait");

            // ScreenSpaceCamera must observe the bound RenderTexture for a full frame before
            // phone-space layout is meaningful.
            yield return null;
            LayoutPortrait();
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasHost != null) UnityEngine.Object.DestroyImmediate(_canvasHost);
            if (_cameraHost != null) UnityEngine.Object.DestroyImmediate(_cameraHost);
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.DestroyImmediate(_target);
            }
            _canvasHost = null;
            _cameraHost = null;
            _target = null;
            _view = null;
            _source = null;
        }

        [UnityTest]
        public IEnumerator ThreeRealCats_PaintDistinctColor32PixelsInsideThePortraitRect()
        {
            PixelCrop red = default;
            PixelCrop blue = default;
            PixelCrop yellow = default;
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", ""), value => red = value);
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.blue", "cat.blue_siamese", "", "", ""), value => blue = value);
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.yellow", "cat.yellow_longhair", "", "", ""), value => yellow = value);

            Assert.That(CountDifferent(red.Pixels, blue.Pixels), Is.GreaterThan(8_000));
            Assert.That(CountDifferent(red.Pixels, yellow.Pixels), Is.GreaterThan(8_000));
            Assert.That(CountDifferent(blue.Pixels, yellow.Pixels), Is.GreaterThan(8_000));
            Assert.That(CountNear(red.Pixels, Palette.SignalRed), Is.GreaterThan(8_000));
            Assert.That(CountNear(blue.Pixels, Palette.HarborBlue), Is.GreaterThan(8_000));
            Assert.That(CountNear(yellow.Pixels, Palette.TabbyYellow), Is.GreaterThan(8_000));
        }

        [UnityTest]
        public IEnumerator BrassAndLantern_AddDistinctFramePixelsBeyondAPlainNegativeControl()
        {
            PixelCrop plain = default;
            PixelCrop brass = default;
            PixelCrop lantern = default;
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", ""), value => plain = value);
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", "frame.brass"), value => brass = value);
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", "frame.lantern"), value => lantern = value);

            Assert.That(CountNear(brass.Pixels, Palette.TabbyYellow),
                Is.GreaterThan(CountNear(plain.Pixels, Palette.TabbyYellow) + 1_500));
            Assert.That(CountNear(lantern.Pixels, Palette.MetroTeal),
                Is.GreaterThan(CountNear(plain.Pixels, Palette.MetroTeal) + 1_500));
            Assert.That(CountDifferent(brass.Pixels, lantern.Pixels), Is.GreaterThan(5_000));
        }

        [UnityTest]
        public IEnumerator Conductor_AddsReadableNavyAndBrassPixelsAtPhoneScale()
        {
            PixelCrop plain = default;
            PixelCrop conductor = default;
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "", "", ""), value => plain = value);
            yield return Render(new CosmeticPortraitSnapshot(
                "cat.red", "cat.red_tabby", "outfit.conductor", "", ""),
                value => conductor = value);

            Assert.That(CountNear(conductor.Pixels, Palette.InkNavy),
                Is.GreaterThan(CountNear(plain.Pixels, Palette.InkNavy) + 5_000),
                "the coat and hat remain a broad navy silhouette at phone size");
            Assert.That(CountNear(conductor.Pixels, Palette.TabbyYellow),
                Is.GreaterThan(CountNear(plain.Pixels, Palette.TabbyYellow) + 150),
                "buttons and badge remain readable brass marks, not subpixel decoration");
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_EmitsEveryPortraitStateAndComposedSheet_WhenRequested()
        {
            string directory = Environment.GetEnvironmentVariable("CM_COSMETIC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Pass("capture rig disarmed — set CM_COSMETIC_CAPTURE_DIR to emit portraits");
                yield break;
            }

            var states = new[]
            {
                new CaptureState("portrait-plain.png", new CosmeticPortraitSnapshot(
                    "cat.red", "cat.red_tabby", "", "", "")),
                new CaptureState("portrait-complete-preview.png", new CosmeticPortraitSnapshot(
                    "cat.blue", "cat.blue_siamese", "outfit.conductor", "", "frame.brass")),
                new CaptureState("portrait-purchased.png", new CosmeticPortraitSnapshot(
                    "cat.red", "cat.red_tabby", "outfit.conductor", "", "frame.brass")),
                new CaptureState("portrait-lapsed.png", new CosmeticPortraitSnapshot(
                    "cat.red", "cat.red_tabby", "", "", "")),
                new CaptureState("portrait-restored.png", new CosmeticPortraitSnapshot(
                    "cat.yellow", "cat.yellow_longhair", "outfit.conductor", "", "frame.lantern")),
                new CaptureState("portrait-brass.png", new CosmeticPortraitSnapshot(
                    "cat.red", "cat.red_tabby", "", "", "frame.brass")),
                new CaptureState("portrait-lantern.png", new CosmeticPortraitSnapshot(
                    "cat.red", "cat.red_tabby", "", "", "frame.lantern")),
            };
            var captures = new List<PixelCrop>(states.Length);
            Directory.CreateDirectory(directory);

            foreach (var state in states)
            {
                PixelCrop crop = default;
                yield return Render(state.Snapshot, value => crop = value);
                WritePng(Path.Combine(directory, state.FileName), crop.Width, crop.Height,
                    crop.Pixels);
                captures.Add(crop);
            }

            WriteContactSheet(Path.Combine(directory, "portrait-states.png"), captures);
            foreach (var state in states)
                Assert.That(File.Exists(Path.Combine(directory, state.FileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "portrait-states.png")), Is.True);
        }

        private IEnumerator Render(CosmeticPortraitSnapshot snapshot, Action<PixelCrop> receive)
        {
            _source.Set(snapshot);
            yield return null;
            LayoutPortrait();
            Canvas.ForceUpdateCanvases();
            _camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
            texture.Apply(false, false);
            var full = texture.GetPixels32();
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);

            var corners = new Vector3[4];
            _view.RootTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(_camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(_camera, corners[2]);
            int xMin = Mathf.Clamp(Mathf.FloorToInt(bottomLeft.x), 0, CaptureWidth - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(bottomLeft.y), 0, CaptureHeight - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(topRight.x), xMin + 1, CaptureWidth);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(topRight.y), yMin + 1, CaptureHeight);
            int width = xMax - xMin;
            int height = yMax - yMin;
            var crop = new Color32[width * height];
            for (int y = 0; y < height; y++)
                Array.Copy(full, (yMin + y) * CaptureWidth + xMin,
                    crop, y * width, width);
            receive(new PixelCrop(width, height, crop));
        }

        private void LayoutPortrait()
        {
            var rect = _view.RootTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        }

        private static int CountDifferent(Color32[] left, Color32[] right)
        {
            Assert.That(right.Length, Is.EqualTo(left.Length));
            int count = 0;
            for (int i = 0; i < left.Length; i++)
            {
                int distance = Math.Abs(left[i].r - right[i].r)
                    + Math.Abs(left[i].g - right[i].g)
                    + Math.Abs(left[i].b - right[i].b);
                if (distance > 24) count++;
            }
            return count;
        }

        private static int CountNear(Color32[] pixels, Color color)
        {
            Color32 expected = color;
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 240
                    && Math.Abs(pixels[i].r - expected.r) <= 28
                    && Math.Abs(pixels[i].g - expected.g) <= 28
                    && Math.Abs(pixels[i].b - expected.b) <= 28)
                    count++;
            }
            return count;
        }

        private static void WriteContactSheet(string path, IReadOnlyList<PixelCrop> captures)
        {
            int tileWidth = captures[0].Width;
            int tileHeight = captures[0].Height;
            const int columns = 4;
            int rows = Mathf.CeilToInt(captures.Count / (float)columns);
            int width = tileWidth * columns;
            int height = tileHeight * rows;
            var pixels = new Color32[width * height];
            Color32 background = Palette.DepotNavy;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;
            for (int index = 0; index < captures.Count; index++)
            {
                int x = (index % columns) * tileWidth;
                int y = (rows - 1 - index / columns) * tileHeight;
                for (int row = 0; row < tileHeight; row++)
                    Array.Copy(captures[index].Pixels, row * tileWidth,
                        pixels, (y + row) * width + x, tileWidth);
            }
            WritePng(path, width, height, pixels);
        }

        private static void WritePng(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private readonly struct PixelCrop
        {
            public int Width { get; }
            public int Height { get; }
            public Color32[] Pixels { get; }

            public PixelCrop(int width, int height, Color32[] pixels)
            {
                Width = width;
                Height = height;
                Pixels = pixels;
            }
        }

        private readonly struct CaptureState
        {
            public string FileName { get; }
            public CosmeticPortraitSnapshot Snapshot { get; }

            public CaptureState(string fileName, CosmeticPortraitSnapshot snapshot)
            {
                FileName = fileName;
                Snapshot = snapshot;
            }
        }
    }
}
