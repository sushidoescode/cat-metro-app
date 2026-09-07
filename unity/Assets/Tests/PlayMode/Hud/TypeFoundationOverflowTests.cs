using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Theme;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class TypeFoundationOverflowTests
    {
        private const int Width = 917;
        private const int Height = 2048;
        private const float Dpi = 408f;
        private static readonly Rect SafeArea = new Rect(0, 64, Width, 1920);
        private GameRoot _root;
        private RenderTexture _target;
        private FreshStorage _storage;
        private float _previousTimeScale;

        [UnityTest]
        public IEnumerator FreshHome_Intro_L001Hud_AndWon_HaveNoTmpOverflow()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _storage = new FreshStorage();
            GameRoot.DailyStorageRootOverride = () => _storage;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true;
            _target = new RenderTexture(Width, Height, 24);
            _target.Create();
            _root.Cam.targetTexture = _target;
            _root.Cam.aspect = (float)Width / Height;
            _root.Cam.cullingMask = 1 << 31;
            _root.Cam.clearFlags = CameraClearFlags.SolidColor;
            _root.Cam.backgroundColor = Palette.InkNavy;
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true))
                foreach (var child in canvas.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
            yield return null;
            yield return null;

            var report = new OverflowReport();
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"));
            Sample("fresh-home", report);

            _root.Home.LevelSelected.Invoke();
            yield return null;
            Assert.That(_root.Intro.IsVisible, Is.True);
            Sample("intro", report);

            _root.Intro.PlayRequested.Invoke();
            yield return null;
            Assert.That(_root.Preview.IsVisible, Is.True);
            Assert.That(_root.Preview.FaceCount, Is.GreaterThan(0));
            Sample("l001-hud", report);

            // L001 starts on the blue route; its single red passenger wins after one flip.
            Assert.That(_root.Session.EnqueueToggle(0), Is.True);
            _root.Session.AdvanceMs(200 * TickInterpolator.TICK_MS);
            yield return null;
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            Assert.That(_root.GetComponent<ResultsPanel>().IsVisible, Is.True);
            Sample("won", report);

            string json = JsonUtility.ToJson(report, true);
            TestContext.Progress.WriteLine(json);
            string dir = Environment.GetEnvironmentVariable("CM_TYPE_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir))
                File.WriteAllText(Path.Combine(dir, "tmp-overflow.json"), json);
            var overflowing = report.surfaces.SelectMany(surface => surface.labels
                .Where(label => label.overflow)
                .Select(label => surface.name + "/" + label.path + " text=" + label.text));
            Assert.That(overflowing, Is.Empty, "every active TMP label must fit its live rect");
        }

        private void Sample(string name, OverflowReport report)
        {
            // Use the same public viewport seams as the phone capture rig; do not resize text
            // rectangles or alter fonts to make the enumeration pass.
            foreach (var view in _root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var method = view.GetType().GetMethod("LayoutForViewport",
                    BindingFlags.Public | BindingFlags.Instance);
                if (method == null) continue;
                method.Invoke(view, method.GetParameters().Length == 3
                    ? new object[] { SafeArea, Dpi, new Rect(0, 0, Width, Height) }
                    : new object[] { SafeArea, Dpi });
            }
            Canvas.ForceUpdateCanvases();
            var labels = _root.GetComponentsInChildren<TMP_Text>(false)
                .Where(label => label.isActiveAndEnabled)
                .OrderBy(label => HierarchyPath(label.transform)).ToArray();
            Assert.That(labels, Is.Not.Empty, name + " must enumerate real active labels");
            var surface = new Surface { name = name };
            foreach (var label in labels)
            {
                label.ForceMeshUpdate();
                surface.labels.Add(new LabelReadback {
                    path = HierarchyPath(label.transform), text = label.text,
                    font = label.font.name, sizePx = label.fontSize,
                    minimumPx = label.fontSizeMin, maximumPx = label.fontSizeMax,
                    rectWidthPx = label.rectTransform.rect.width,
                    rectHeightPx = label.rectTransform.rect.height,
                    overflow = label.isTextOverflowing,
                });
            }
            report.surfaces.Add(surface);
            string dir = Environment.GetEnvironmentVariable("CM_TYPE_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir)) CaptureUiOnly(dir, name);
        }

        private void CaptureUiOnly(string dir, string name)
        {
            // These proofs work in a clean worktree: paint only Canvas UI, never a substitute
            // board or licensed-model render. Full Home/board captures still require main.
            var camera = _root.Cam;
            var previousActive = RenderTexture.active;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = _target;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, "type-" + name + "-ui-only.png"),
                    pixels.EncodeToPNG());
                Assert.That(pixels.GetPixels32().Count(pixel =>
                    pixel.r > 200 && pixel.g > 200 && pixel.b > 180), Is.GreaterThan(24),
                    name + " must contain painted cream/white UI, not just camera clear");
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.Destroy(pixels);
            }
        }

        private static string HierarchyPath(Transform node)
        {
            string path = node.name;
            while (node.parent != null)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }
            return path;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                _root.Cam.targetTexture = null;
                Object.DestroyImmediate(_root.gameObject);
            }
            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
            }
            Time.timeScale = _previousTimeScale;
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.DevSkipShippedHome = false;
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            _storage?.Dispose();
        }

        [Serializable]
        private sealed class OverflowReport
        {
            public int width = Width, height = Height;
            public float dpi = Dpi;
            public int editorScreenWidth = Screen.width, editorScreenHeight = Screen.height;
            public List<Surface> surfaces = new List<Surface>();
        }

        [Serializable]
        private sealed class Surface
        {
            public string name;
            public List<LabelReadback> labels = new List<LabelReadback>();
        }

        [Serializable]
        private sealed class LabelReadback
        {
            public string path, text, font;
            public float sizePx, minimumPx, maximumPx, rectWidthPx, rectHeightPx;
            public bool overflow;
        }

        private sealed class FreshStorage : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; } = Path.Combine(Path.GetTempPath(),
                "cm-type-overflow-" + Guid.NewGuid().ToString("N"));
            public string CacheDirectory => SaveDirectory;
            public FreshStorage() { Directory.CreateDirectory(SaveDirectory); }
            public void Dispose() { if (Directory.Exists(SaveDirectory)) Directory.Delete(SaveDirectory, true); }
        }
    }
}
