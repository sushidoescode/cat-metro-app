using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

        private static void ApplyPhoneLayout(Component view)
        {
            // The old views have no injectable viewport seam; once a responsive view lands,
            // this invokes its real layout law against the phone-safe rect before rendering.
            var method = view.GetType().GetMethod("LayoutForViewport",
                BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                method.Invoke(view, new object[] { CaptureSafeArea, CaptureDpi });
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
