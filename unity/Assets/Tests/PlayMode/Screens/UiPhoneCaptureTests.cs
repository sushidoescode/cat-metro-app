using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Bootstrap;

namespace CatMetro.Tests.PlayMode
{
    // Opt-in visual evidence for LOOK step 7. The armed rig insists on the reference phone
    // frame so a convenient landscape Game view cannot silently stand in for the shipped UI.
    public sealed class UiPhoneCaptureTests
    {
        private const int CaptureWidth = 917;
        private const int CaptureHeight = 2048;
        private const float CaptureDpi = 408f;
        private static readonly Rect CaptureSafeArea = new Rect(0f, 64f, 917f, 1920f);
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_ShippedHome_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
            yield return null;
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);
            ApplyPhoneLayout(_root.Home);
            ApplyPhoneLayout(_root.Wardrobe);
            Capture(dir, "step-7-home.png");
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_FailureBanner_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
            _root.Home.Hide();
            _root.Banner.ShowKey("fail.banner.timeout");
            yield return null;
            yield return null;
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("The last train left the depot"));
            Assert.That(_root.Banner.Visible, Is.True);
            ApplyPhoneLayout(_root.Banner);
            Capture(dir, "step-7-failure.png");
        }

        // HUD-WAVE: the wave-preview capsule over the live board. Skips Home, because the
        // shipped Home covers the HUD and holds the sim at tick 0 — the capsule is only worth
        // photographing with a real upcoming queue behind it.
        [UnityTest]
        public IEnumerator CaptureEvidence_WavePreviewCapsule_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            GameRoot.DevSkipShippedHome = true;
            _root = GameRoot.Launch();
            yield return null;
            yield return null;
            Assert.That(_root.Preview.FaceCount, Is.GreaterThan(0),
                "precondition: L001 has upcoming cats to draw as faces");
            ApplyPhoneLayout(_root.Preview);
            Capture(dir, "step-7-wave-preview.png");
        }

        [UnityTest]
        public IEnumerator FailureBanner_LayoutsInside917x2048SafeArea_WithoutTextOverflow()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
            _root.Home.Hide();
            _root.Banner.ShowKey("fail.banner.timeout");
            _root.Banner.LayoutForViewport(CaptureSafeArea, CaptureDpi);
            Canvas.ForceUpdateCanvases();
            _root.Banner.TextTransform.GetComponent<TMPro.TMP_Text>().ForceMeshUpdate();

            Assert.That(_root.Banner.PaintedRectPx.xMin,
                Is.GreaterThan(CaptureSafeArea.xMin),
                "the banner keeps a horizontal safe-area inset on the reference phone");
            Assert.That(_root.Banner.PaintedRectPx.xMax,
                Is.LessThan(CaptureSafeArea.xMax),
                "the banner keeps a horizontal safe-area inset on the reference phone");
            Assert.That(_root.Banner.TextTransform, Is.Not.Null,
                "the responsive banner exposes its real TMP transform for layout inspection");
            Assert.That(_root.Banner.IsTextOverflowing, Is.False,
                "The last train left the depot must fit at 917x2048");
        }

        [UnityTest]
        public IEnumerator ShippedHome_DioramaWindow_PreservesTickZeroBoardPixels_WhileFramePaintsAboveIt()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
            yield return null;

            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                "Home must hold the already-loaded board at tick 0");

            Camera camera = _root.Cam;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24,
                RenderTextureFormat.ARGB32);
            target.Create();

            try
            {
                camera.targetTexture = target;
                // ScreenSpaceCamera must observe the phone target before projected UI corners
                // are sampled. Home remains visible, so both yields keep the session at tick 0.
                yield return null;
                ApplyPhoneLayout(_root.Home);
                Canvas.ForceUpdateCanvases();
                yield return null;
                ApplyPhoneLayout(_root.Home);
                Canvas.ForceUpdateCanvases();

                Assert.That(_root.Session.State.Tick, Is.EqualTo(0));
                RectTransform window = FindRequiredRect(
                    _root.Home.transform, "DioramaWindow");
                RectTransform frame = FindRequiredRect(
                    _root.Home.transform, "DioramaFrameTop");
                Assert.That(window.GetComponent<Graphic>(), Is.Null,
                    "the aperture is geometry-free, not a clear-looking Image");
                var frameImage = frame.GetComponent<Image>();
                Assert.That(frameImage, Is.Not.Null,
                    "the named frame is real rendered geometry");
                Assert.That(frameImage.color.a, Is.GreaterThanOrEqualTo(0.95f));

                RectInt aperturePatch = InsetAndClamp(
                    ProjectedScreenRect(window, camera), 0.40f, 0.25f,
                    CaptureWidth, CaptureHeight);
                RectInt framePatch = InsetAndClamp(
                    ProjectedScreenRect(frame, camera), 0.20f, 0.20f,
                    CaptureWidth, CaptureHeight);
                Assert.That(aperturePatch.width, Is.GreaterThanOrEqualTo(32));
                Assert.That(aperturePatch.height, Is.GreaterThanOrEqualTo(32));
                Assert.That(framePatch.width, Is.GreaterThanOrEqualTo(32));
                Assert.That(framePatch.height, Is.GreaterThanOrEqualTo(4));

                // The stack still owns Home while its renderer is hidden. No frame is yielded
                // between these reads, so board state and presentation are identical samples.
                _root.Home.Hide();
                Color32[] boardOnly = ReadFrame(camera, target);
                _root.Home.Show();
                ApplyPhoneLayout(_root.Home);
                Canvas.ForceUpdateCanvases();
                Color32[] withHome = ReadFrame(camera, target);

                Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                    "the comparison must use the same paused board tick");
                Assert.That(RgbSpatialStdDev(boardOnly, aperturePatch, CaptureWidth),
                    Is.GreaterThan(6f / 255f),
                    "the aperture contains varied board-world pixels, not camera clear");
                Assert.That(MeanRgbDelta(boardOnly, withHome,
                        aperturePatch, CaptureWidth), Is.LessThanOrEqualTo(2f / 255f),
                    "Home preserves board pixels through the transparent aperture");
                Assert.That(MeanRgbDelta(boardOnly, withHome,
                        framePatch, CaptureWidth), Is.GreaterThan(8f / 255f),
                    "the opaque frame composites visibly above the board");
                Assert.That(ChangedFraction(boardOnly, withHome, framePatch,
                        CaptureWidth, minimumPerChannelDelta: 4), Is.GreaterThan(0.45f),
                    "most frame pixels replace board pixels, ruling out wrong sorting");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
            }
        }

        private static void ApplyPhoneLayout(Component view)
        {
            // The old views have no injectable viewport seam; once a responsive view lands,
            // this invokes its real layout law against the phone-safe rect before rendering.
            var method = view.GetType().GetMethod("LayoutForViewport",
                BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                method.Invoke(view, new object[] { CaptureSafeArea, CaptureDpi });
        }

        private static RectTransform FindRequiredRect(Transform root, string name)
        {
            RectTransform found = null;
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name != name) continue;
                found = rect;
                break;
            }
            Assert.That(found, Is.Not.Null, name + " must exist in shipped Home");
            return found;
        }

        private static Rect ProjectedScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float xMin = first.x, xMax = first.x, yMin = first.y, yMax = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static RectInt InsetAndClamp(Rect rect, float horizontalInset,
            float verticalInset, int width, int height)
        {
            int xMin = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(
                rect.xMin, rect.xMax, horizontalInset)), 0, width - 1);
            int xMax = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(
                rect.xMax, rect.xMin, horizontalInset)), xMin + 1, width);
            int yMin = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(
                rect.yMin, rect.yMax, verticalInset)), 0, height - 1);
            int yMax = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(
                rect.yMax, rect.yMin, verticalInset)), yMin + 1, height);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Color32[] ReadFrame(Camera camera, RenderTexture target)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height,
                    TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Object.Destroy(texture);
            }
        }

        private static float MeanRgbDelta(Color32[] first, Color32[] second,
            RectInt sample, int imageWidth)
        {
            double total = 0d;
            int count = 0;
            for (int y = sample.yMin; y < sample.yMax; y++)
            {
                int row = y * imageWidth;
                for (int x = sample.xMin; x < sample.xMax; x++)
                {
                    Color32 a = first[row + x];
                    Color32 b = second[row + x];
                    total += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
                        + Mathf.Abs(a.b - b.b);
                    count++;
                }
            }
            return (float)(total / (count * 3d * 255d));
        }

        private static float ChangedFraction(Color32[] first, Color32[] second,
            RectInt sample, int imageWidth, int minimumPerChannelDelta)
        {
            int changed = 0;
            int count = 0;
            int minimumTotalDelta = minimumPerChannelDelta * 3;
            for (int y = sample.yMin; y < sample.yMax; y++)
            {
                int row = y * imageWidth;
                for (int x = sample.xMin; x < sample.xMax; x++)
                {
                    Color32 a = first[row + x];
                    Color32 b = second[row + x];
                    int delta = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
                        + Mathf.Abs(a.b - b.b);
                    if (delta > minimumTotalDelta) changed++;
                    count++;
                }
            }
            return changed / (float)count;
        }

        private static float RgbSpatialStdDev(Color32[] pixels, RectInt sample,
            int imageWidth)
        {
            double r = 0d, g = 0d, b = 0d;
            double r2 = 0d, g2 = 0d, b2 = 0d;
            int count = 0;
            for (int y = sample.yMin; y < sample.yMax; y++)
            {
                int row = y * imageWidth;
                for (int x = sample.xMin; x < sample.xMax; x++)
                {
                    Color32 pixel = pixels[row + x];
                    r += pixel.r; g += pixel.g; b += pixel.b;
                    r2 += pixel.r * pixel.r;
                    g2 += pixel.g * pixel.g;
                    b2 += pixel.b * pixel.b;
                    count++;
                }
            }
            double inv = 1d / count;
            double meanR = r * inv, meanG = g * inv, meanB = b * inv;
            double variance = ((r2 * inv - meanR * meanR)
                + (g2 * inv - meanG * meanG)
                + (b2 * inv - meanB * meanB)) / 3d;
            return (float)(System.Math.Sqrt(System.Math.Max(0d, variance)) / 255d);
        }

        private void Capture(string dir, string name)
        {
            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            _root.Cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            _root.Cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
            tex.Apply();
            _root.Cam.targetTexture = null;
            RenderTexture.active = null;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }
    }
}
