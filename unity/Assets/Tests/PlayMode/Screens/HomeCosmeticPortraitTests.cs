using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    public sealed class HomeCosmeticPortraitTests
    {
        private const int Width = 917;
        private const int Height = 2048;
        private const string CaptureArm = "CM_COSMETIC_HOME_CAPTURE";
        private const string CapturePath = "/tmp/cm-cosmetics-home-task8.png";
        private static readonly Rect PhoneSafeArea = new Rect(0f, 64f, Width, 1920f);

        private GameObject _canvasHost;
        private GameObject _cameraHost;
        private RenderTexture _captureTarget;
        private Canvas _canvas;
        private HomeScreenView _home;

        [TearDown]
        public void TearDown()
        {
            if (_home != null) UnityEngine.Object.DestroyImmediate(_home.gameObject);
            if (_canvasHost != null) UnityEngine.Object.DestroyImmediate(_canvasHost);
            if (_cameraHost != null) UnityEngine.Object.DestroyImmediate(_cameraHost);
            if (_captureTarget != null)
            {
                _captureTarget.Release();
                UnityEngine.Object.DestroyImmediate(_captureTarget);
            }

            _home = null;
            _canvasHost = null;
            _cameraHost = null;
            _captureTarget = null;
            _canvas = null;
        }

        [Test]
        public void PortraitSource_PreservesThreeImageHolders_AndMountsOnlyInsideB()
        {
            var source = new RecordingPortraitSource("red_tabby", "cat.red_tabby");
            CreateCanvas();

            _home = HomeScreenView.Create(_canvas.transform, portraitSource: source);

            var hero = FindRect(_home.transform, "HeroCard");
            var a = DirectChild(hero, "ParkedDistrictA");
            var b = DirectChild(hero, "ParkedDistrictB");
            var c = DirectChild(hero, "ParkedDistrictC");
            Assert.That(a.GetComponent<Image>(), Is.Not.Null);
            Assert.That(b.GetComponent<Image>(), Is.Not.Null);
            Assert.That(c.GetComponent<Image>(), Is.Not.Null);
            Assert.That(a.GetComponent<Image>().color,
                Is.EqualTo(Palette.WithAlpha(Palette.DepotNavy, 0.18f)));
            Assert.That(c.GetComponent<Image>().color,
                Is.EqualTo(Palette.WithAlpha(Palette.DepotNavy, 0.18f)));
            Assert.That(b.GetComponent<Image>().color, Is.EqualTo(Color.clear),
                "only B's fallback paint becomes transparent when the shared portrait is bound");

            var portraits = b.GetComponentsInChildren<CosmeticPortraitView>(true);
            Assert.That(portraits.Length, Is.EqualTo(1));
            Assert.That(portraits[0].name, Is.EqualTo("HomeProfilePortrait"));
            Assert.That(portraits[0].transform.parent, Is.SameAs(b));
            Assert.That(_home.ProfilePortrait, Is.SameAs(portraits[0]));
            Assert.That(_home.ProfilePortraitTransform,
                Is.SameAs(portraits[0].RootTransform));
            AssertStretched(portraits[0].RootTransform);
            Assert.That(a.GetComponentsInChildren<CosmeticPortraitView>(true), Is.Empty);
            Assert.That(c.GetComponentsInChildren<CosmeticPortraitView>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PhoneLayout_UsesActualHolderGeometry_AndPaintsReadableFeatures()
        {
            var source = new RecordingPortraitSource("red_tabby", "cat.red_tabby");
            CreateCaptureCamera();
            CreateCanvas(_cameraHost.GetComponent<Camera>());
            _home = HomeScreenView.Create(_canvas.transform, portraitSource: source);
            _home.Attach(new ChromeRegions(), () => true);
            _home.Show();

            // RenderTexture binding must settle for one frame before screen-space layout.
            yield return null;
            _home.LayoutForViewport(PhoneSafeArea, 408f);
            Canvas.ForceUpdateCanvases();

            var holder = DirectChild(FindRect(_home.transform, "HeroCard"),
                "ParkedDistrictB");
            var portrait = _home.ProfilePortrait;
            Assert.That(portrait, Is.Not.Null);
            var holderRect = ScreenRect(holder);
            var portraitRect = ScreenRect(portrait.RootTransform);

            Assert.That(holderRect.x, Is.EqualTo(597.05f).Within(1f));
            Assert.That(holderRect.y, Is.EqualTo(1043.935f).Within(1f));
            Assert.That(holderRect.width, Is.EqualTo(179.3f).Within(1f));
            Assert.That(holderRect.height, Is.EqualTo(188.55f).Within(1f));
            AssertRectApproximately(portraitRect, holderRect, 0.5f);
            Assert.That(PhoneSafeArea.Contains(holderRect.min), Is.True);
            Assert.That(PhoneSafeArea.Contains(holderRect.max), Is.True);

            var painted = portrait.GetComponentsInChildren<Image>(true)
                .Where(image => image.gameObject.activeInHierarchy && image.color.a > 0.01f)
                .ToArray();
            Assert.That(painted.Length, Is.GreaterThan(8),
                "the shared mount must contain real visible portrait paint");
            var eye = FindRect(portrait.transform, "EyeLeft");
            var eyeRect = ScreenRect(eye);
            var eyeImage = eye.GetComponent<Image>();
            Assert.That(eyeImage, Is.Not.Null);
            Assert.That(eyeImage.gameObject.activeInHierarchy, Is.True);
            Assert.That(eyeImage.color.a, Is.GreaterThan(0.01f));
            Assert.That(eyeImage.color, Is.EqualTo(Palette.InkNavy));
            Assert.That(eyeRect.width, Is.GreaterThanOrEqualTo(8f));
            Assert.That(eyeRect.height, Is.GreaterThanOrEqualTo(8f));

            var pixels = RenderPixels(out var png);
            Assert.That(CountColor(pixels, portraitRect,
                new Color32(225, 90, 71, 255), 28), Is.GreaterThan(3_000),
                "Red Tabby must contribute a readable Signal Red silhouette");
            Assert.That(CountColor(pixels, portraitRect,
                new Color32(34, 48, 74, 255), 28), Is.GreaterThan(40),
                "eyes, muzzle, stripes and whiskers must contribute real Ink Navy pixels");
            Assert.That(CountColor(pixels, eyeRect, (Color32)Palette.InkNavy, 12),
                Is.GreaterThan(20),
                "EyeLeft itself must render Ink Navy pixels inside its measured bounds");

            if (string.Equals(Environment.GetEnvironmentVariable(CaptureArm), "1",
                    StringComparison.Ordinal))
                File.WriteAllBytes(CapturePath, png);
        }

        [UnityTest]
        public IEnumerator HomeHideShowDestroy_BalancesThePortraitSubscription()
        {
            var source = new RecordingPortraitSource("red_tabby", "cat.red_tabby");
            CreateCanvas();
            _home = HomeScreenView.Create(_canvas.transform, portraitSource: source);
            _home.Attach(new ChromeRegions(), () => true);

            _home.Show();
            yield return null;
            Assert.That(source.SubscriberCount, Is.EqualTo(1));
            Assert.That(_home.ProfilePortrait.AppliedCatId, Is.EqualTo("red_tabby"));

            _home.Hide();
            Assert.That(source.SubscriberCount, Is.Zero);

            _home.Show();
            yield return null;
            Assert.That(source.SubscriberCount, Is.EqualTo(1));
            source.SetPortrait("blue_siamese", "cat.blue_siamese");
            Assert.That(_home.ProfilePortrait.AppliedCatId, Is.EqualTo("blue_siamese"));

            UnityEngine.Object.DestroyImmediate(_home.gameObject);
            _home = null;
            Assert.That(source.SubscriberCount, Is.Zero);
            Assert.That(source.AddCalls, Is.EqualTo(source.RemoveCalls),
                "all source subscriptions must be balanced after destruction");
        }

        private void CreateCanvas(Camera camera = null)
        {
            _canvasHost = new GameObject("HomeCosmeticCanvas");
            _canvas = _canvasHost.AddComponent<Canvas>();
            _canvas.renderMode = camera == null
                ? RenderMode.ScreenSpaceOverlay
                : RenderMode.ScreenSpaceCamera;
            if (camera != null)
            {
                _canvas.worldCamera = camera;
                _canvas.planeDistance = 1f;
            }
        }

        private void CreateCaptureCamera()
        {
            _cameraHost = new GameObject("HomeCosmeticCamera");
            var camera = _cameraHost.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.WarmPaper;
            _captureTarget = new RenderTexture(Width, Height, 24,
                RenderTextureFormat.ARGB32);
            camera.targetTexture = _captureTarget;
        }

        private Color32[] RenderPixels(out byte[] png)
        {
            var camera = _cameraHost.GetComponent<Camera>();
            camera.Render();
            var previous = RenderTexture.active;
            Texture2D texture = null;
            png = null;
            try
            {
                RenderTexture.active = _captureTarget;
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                png = texture.EncodeToPNG();
                return pixels;
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                RenderTexture.active = previous;
            }
        }

        private Rect ScreenRect(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private static RectTransform DirectChild(RectTransform parent, string name)
        {
            var matches = new List<RectTransform>();
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name)
                    matches.Add((RectTransform)parent.GetChild(i));
            Assert.That(matches.Count, Is.EqualTo(1),
                name + " must remain one direct HeroCard child");
            return matches[0];
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            var result = root.GetComponentsInChildren<RectTransform>(true)
                .SingleOrDefault(rect => rect.name == name);
            Assert.That(result, Is.Not.Null, "missing RectTransform " + name);
            return result;
        }

        private static void AssertStretched(RectTransform rect)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
        }

        private static void AssertRectApproximately(Rect actual, Rect expected,
            float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(tolerance));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(tolerance));
        }

        private static int CountColor(IReadOnlyList<Color32> pixels, Rect rect,
            Color32 expected, int tolerance)
        {
            int count = 0;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 1, Width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, Height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 1, Height);
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
            {
                var actual = pixels[y * Width + x];
                if (Math.Abs(actual.r - expected.r) <= tolerance
                    && Math.Abs(actual.g - expected.g) <= tolerance
                    && Math.Abs(actual.b - expected.b) <= tolerance)
                    count++;
            }
            return count;
        }

        private sealed class RecordingPortraitSource : ICosmeticPortraitSource
        {
            private Action _changed;
            private CosmeticPortraitSnapshot _portrait;
            private readonly Dictionary<string, CosmeticPortraitAssetDefinition> _assets =
                new Dictionary<string, CosmeticPortraitAssetDefinition>(StringComparer.Ordinal)
                {
                    ["cat.red_tabby"] = new CosmeticPortraitAssetDefinition(
                        "cat.red_tabby", "cat.red_tabby", "test.red"),
                    ["cat.blue_siamese"] = new CosmeticPortraitAssetDefinition(
                        "cat.blue_siamese", "cat.blue_siamese", "test.blue"),
                };

            public int SubscriberCount { get; private set; }
            public int AddCalls { get; private set; }
            public int RemoveCalls { get; private set; }
            public CosmeticPortraitSnapshot CurrentPortrait => _portrait;

            public event Action Changed
            {
                add
                {
                    _changed += value;
                    SubscriberCount++;
                    AddCalls++;
                }
                remove
                {
                    _changed -= value;
                    SubscriberCount--;
                    RemoveCalls++;
                }
            }

            public RecordingPortraitSource(string catId, string assetId)
            {
                _portrait = new CosmeticPortraitSnapshot(catId, assetId, "", "", "");
            }

            public bool TryGetPortraitAsset(string assetId,
                out CosmeticPortraitAssetDefinition asset)
                => _assets.TryGetValue(assetId ?? string.Empty, out asset);

            public void SetPortrait(string catId, string assetId)
            {
                _portrait = new CosmeticPortraitSnapshot(catId, assetId, "", "", "");
                _changed?.Invoke();
            }
        }
    }
}
