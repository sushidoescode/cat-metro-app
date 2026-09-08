using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Props;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class PortraitBoardFramingTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            GameRoot.DevSkipShippedHome = false;
        }

        [UnityTest]
        public IEnumerator EarlyBoardsFillPortrait_AndCaptureRepresentativeLadderFrames()
        {
            GameRoot.DevSkipShippedHome = true;
            string dir = System.Environment.GetEnvironmentVariable("CM_LADDER_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Assert.That(CatModelCatalog.LoadResources().AdmittedEntryCount, Is.GreaterThan(0),
                    "furnished captures must run from main with the licensed rig installed");
                Assert.That(PropModelCatalog.LoadResources().AdmittedEntryCount, Is.GreaterThan(0));
                Directory.CreateDirectory(dir);
            }
            var failures = new List<string>();
            var metrics = new List<string> { "level,slabHeight,slabMinY,slabMaxY,orthoSize" };
            var captures = new HashSet<int> { 1, 2, 9, 15, 41, 52 };
            foreach (int number in Enumerable.Range(1, 15).Concat(new[] { 41, 52 }))
            {
                string id = "L" + number.ToString("000");
                _root = GameRoot.Launch(GameRoot.LevelPath(id));
                _root.enabled = false;
                var target = new RenderTexture(917, 2048, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                var previousTarget = _root.Cam.targetTexture;
                try
                {
                    _root.Cam.targetTexture = target;
                    yield return null;
                    BoardSceneLook.FitCamera(_root.Cam, _root.View);
                    _root.Preview.Refresh();
                    _root.Preview.LayoutForViewport(new Rect(0, 64, 917, 1920), 408f);
                    Canvas.ForceUpdateCanvases();

                    var slab = _root.View.transform.Find("BoardBody");
                    var ys = slab.GetComponentsInChildren<MeshFilter>()
                        .SelectMany(f => f.sharedMesh.vertices.Select(v =>
                            _root.Cam.WorldToViewportPoint(f.transform.TransformPoint(v)).y)).ToArray();
                    float height = ys.Max() - ys.Min();
                    metrics.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:F6},{2:F6},{3:F6},{4:F6}",
                        id, height, ys.Min(), ys.Max(), _root.Cam.orthographicSize));
                    if (number <= 15 && height < 0.65f)
                        failures.Add(id + " slab height " + height.ToString("P1"));
                    if (ys.Min() < 0.02f || ys.Max() > 0.98f)
                        failures.Add(id + " slab leaves the portrait vertically");
                    if (!string.IsNullOrEmpty(dir) && captures.Contains(number))
                        WriteFrame(_root.Cam, target, Path.Combine(dir, "ladder-" + id + "-tick000.png"), failures);
                    if (!string.IsNullOrEmpty(dir) && number == 1)
                    {
                        _root.Preview.GetComponent<Canvas>().enabled = false;
                        BoardSceneLook.FitCamera(_root.Cam, _root.View, new Rect(.075f, .28f, .85f, .49f));
                        WriteFrame(_root.Cam, target, Path.Combine(dir, "home-L001-window-fit.png"));
                    }
                }
                finally
                {
                    _root.Cam.targetTexture = previousTarget;
                    target.Release();
                    Object.DestroyImmediate(target);
                    Object.DestroyImmediate(_root.gameObject);
                    _root = null;
                }
            }
            TestContext.Out.WriteLine(string.Join("\n", metrics));
            if (!string.IsNullOrEmpty(dir))
                File.WriteAllLines(Path.Combine(dir, "portrait-framing.csv"), metrics);
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
        }

        private static void WriteFrame(Camera camera, RenderTexture target, string path, List<string> failures = null)
        {
            var previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                texture = CaptureRig.ReadRgb24(target);
                File.WriteAllBytes(path, CaptureRig.EncodeOpaqueSrgbPng(texture));
                if (path.EndsWith("ladder-L002-tick000.png"))
                {
                    var rows = new List<string>();
                    foreach (float fraction in new[] { 0.05f, 0.95f })
                    {
                        float luminance = 0f;
                        int y = Mathf.RoundToInt(texture.height * fraction);
                        for (int x = 0; x < texture.width; x++)
                        {
                            Color c = texture.GetPixel(x, y);
                            luminance += .2126f * c.r + .7152f * c.g + .0722f * c.b;
                        }
                        luminance /= texture.width;
                        if (luminance < 0.35f)
                            failures?.Add("L002 desk luminance at " + fraction + " is " + luminance);
                        rows.Add(fraction.ToString(CultureInfo.InvariantCulture) + ","
                            + luminance.ToString(CultureInfo.InvariantCulture));
                    }
                    File.WriteAllLines(Path.ChangeExtension(path, ".desk-luminance.csv"), rows);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }
    }
}
