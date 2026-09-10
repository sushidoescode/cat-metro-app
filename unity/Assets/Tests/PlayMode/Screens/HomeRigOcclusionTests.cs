using System;
using System.Collections;
using System.IO;
using System.Reflection;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Screens;
using CatMetro.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    // These pixel assertions deliberately require the admitted licensed rig. A worktree
    // without incoming art cannot supply evidence for UI drawn over a camera-lifted mesh.
    public sealed class HomeRigOcclusionTests
    {
        private static readonly Rect Safe = new Rect(0, 64, 917, 1920);
        private GameRoot _root;
        private RenderTexture _target;
        private string _storage;
        private float _timeScale;
        private bool _firstPassMounted;
        private string _firstPassGeometry;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            if (CatModelCatalog.LoadResources().AdmittedEntryCount != 1)
                Assert.Ignore("Licensed Home rig required; run in the main checkout.");
            Time.timeScale = 0;
            _storage = Path.Combine(Path.GetTempPath(), "cm-home-occlusion-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_storage);
            GameRoot.DailyStorageRootOverride = () => new Storage(_storage);
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            _root = GameRoot.Launch();
            _root.enabled = false;
            _firstPassMounted = _root.Home.ProfileRig.Mounted;
            _firstPassGeometry = "screen=" + Screen.width + "x" + Screen.height
                + " safe=" + Screen.safeArea + " dpi=" + Screen.dpi
                + " hero=" + _root.Home.HeroRectPx + " fallback=" + _root.Home.ProfileRig.FallbackReason;
            _target = new RenderTexture(917, 2048, 24);
            _target.Create();
            _root.Cam.targetTexture = _target;
            _root.Cam.aspect = 917f / 2048f;
            _root.Home.LayoutForViewport(Safe, 408, new Rect(0, 0, 917, 2048));
            _root.Wardrobe.LayoutForViewport(Safe, 408);
            Canvas.ForceUpdateCanvases();
            Assert.That(_root.Home.ProfileRig.Mounted, Is.True, "phone layout must admit the real rig");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            yield return null;
            if (_target != null) { _target.Release(); Object.Destroy(_target); }
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            CatMetro.Services.Purchases.PurchaseRuntime.ResetForTests();
            CatMetro.Services.Cosmetics.CosmeticRuntime.ResetForTests();
            Time.timeScale = _timeScale;
            if (_storage != null && Directory.Exists(_storage)) Directory.Delete(_storage, true);
        }

        [Test]
        public void NormalBoot_MountsOnTheFirstHomeLayout_WithoutARenderCallback()
        {
            Assert.That(_firstPassMounted, Is.True, _firstPassGeometry);
        }

        [Test]
        public void BootCover_HasZeroRigPixels_AndRestoresTheSameMountAfterReveal()
        {
            var rig = _root.Home.ProfileRig;
            var instance = rig.PrefabRoot;
            int pixels = RigPixels(new Rect(0, 0, 917, 2048), "boot-navy-first-frame");
            Assert.That(pixels, Is.Zero, "no camera-lifted mesh may draw over the boot cover");
            Assert.That(rig.Mounted, Is.True, "hiding renderers retains mount state");
            AdvanceBoot(.15f);
            Assert.That(RigPixels(new Rect(0, 0, 917, 2048), "boot-navy-150ms"), Is.Zero);
            AdvanceBoot(.15f);
            Assert.That(rig.PrefabRoot, Is.SameAs(instance));
            Assert.That(RigPixels(new Rect(0, 0, 917, 2048), "home-restored-after-boot"), Is.GreaterThan(100));
        }

        [Test]
        public void StackedModalOverHome_HasZeroRigPixels_AndCloseRestoresTheSameMount()
        {
            AdvanceBoot(.3f);
            var rig = _root.Home.ProfileRig;
            var instance = rig.PrefabRoot;
            Assert.That(RigPixels(new Rect(0, 0, 917, 2048), "home-before-modal"), Is.GreaterThan(100),
                "positive control: the admitted cat paints before the modal opens");
            var sheet = DailyReminderSheet.Create(_root.Home.transform.parent);
            sheet.Attach(_root.Input.Regions);
            sheet.ShowSettings();
            sheet.LayoutForViewport(Safe, 408);
            Assert.That(sheet.CardRectPx.Overlaps(rig.RenderedHeadScreenRect), Is.True,
                "the real stacked modal must cover the cat in this reproduction");
            Assert.That(RigPixels(sheet.CardRectPx, "modal-over-home"), Is.Zero,
                "zero rig contribution inside the modal card, including its first render");
            Assert.That(rig.Mounted, Is.True);
            sheet.Hide();
            Assert.That(rig.PrefabRoot, Is.SameAs(instance));
            Assert.That(RigPixels(new Rect(0, 0, 917, 2048), "home-restored-after-modal"), Is.GreaterThan(100));
        }

        [Test]
        public void ClosingOneModal_DoesNotRevealTheRigThroughAnotherModalOrBootCover()
        {
            var first = DailyReminderSheet.Create(_root.Home.transform.parent);
            first.Attach(_root.Input.Regions);
            first.ShowSettings();
            _root.Intro.Show("Wake the Lever", 1);
            AdvanceBoot(.3f);
            first.Hide();
            _root.Home.Show();
            _root.Home.LayoutForViewport(Safe, 408, new Rect(0, 0, 917, 2048));
            Assert.That(RigPixels(new Rect(0, 0, 917, 2048), null), Is.Zero,
                "ending boot or closing one modal cannot reveal a rig behind another modal");
            _root.Intro.Hide();
            Assert.That(RigPixels(new Rect(0, 0, 917, 2048), null), Is.GreaterThan(100));
        }

        [Test]
        public void BoardGateProbe_SitsOutsideEveryHomePin_WithTheLicensedBoard()
        {
            AdvanceBoot(.3f);
            var disc = HomeBoardGateProbe.PlaceDiscOutsidePins(_root);
            Assert.That(_root.Input.HandleTapAtScreen(disc), Is.EqualTo(-1));
            Render("home-board-gate-disc");
            Debug.Log("HOME_GATE disc=" + disc + " play=" + _root.Home.PinPaintedRectPx
                + " daily=" + _root.Home.DailyPinPaintedRectPx + " wardrobe=" + _root.Wardrobe.EntryRectPx);
        }

        [Test]
        public void PlayDolly_StillPaintsAllFourFadingSurfacesAtPointTwoSeconds()
        {
            AdvanceBoot(.3f);
            _root.Home.LevelSelected.Invoke();
            _root.Intro.LayoutForViewport(Safe, 408);
            Assert.That(_root.Intro.transform.GetSiblingIndex(),
                Is.GreaterThan(_root.Wardrobe.transform.GetSiblingIndex()),
                "the intro dimmer must cover every suspended Home pin");
            var fx = _root.GetComponent<CatMetro.Presentation.Fx.BoardFx>();
            fx.Advance(1f); // A real player can read the intro for an arbitrary duration.
            Render("intro-before-play");
            _root.Intro.PlayRequested.Invoke();
            Render("dolly-start");
            fx.Advance(.2f);
            Render("dolly-200ms");
            var paint = new[] {
                _root.Home.transform.Find("HeroCard/DioramaFrameTop"),
                _root.Home.transform.Find("PinL001/PlayButtonFace"),
                _root.Home.transform.Find("PinDaily/DailyButtonFace"),
                _root.Wardrobe.transform.Find("WardrobeCapsule/WardrobeButtonFace") };
            foreach (var surface in paint)
            {
                Assert.That(surface, Is.Not.Null);
                Assert.That(surface.gameObject.activeInHierarchy, Is.True, surface.name);
                Assert.That(surface.GetComponent<UnityEngine.UI.Graphic>().canvasRenderer.GetAlpha(),
                    Is.EqualTo(.2f).Within(.01f), surface.name + " after an actual camera render");
            }
            fx.Advance(.05f);
            Render("dolly-250ms");
            foreach (var surface in paint) Assert.That(surface.gameObject.activeInHierarchy, Is.False);
            fx.Advance(.15f);
            Render("play-400ms");
        }

        [TestCase(917, 2048, 408f)]
        [TestCase(600, 1100, 240f)]
        public void ProfileHolder_ContainsTheFullAnimatedSkinAndRaster_WithoutUndoingHeadGrowth(
            int width, int height, float dpi)
        {
            AdvanceBoot(.3f);
            _root.Cam.targetTexture = null;
            _target.Release(); Object.DestroyImmediate(_target);
            _target = new RenderTexture(width, height, 24);
            _target.Create();
            _root.Cam.targetTexture = _target;
            _root.Cam.aspect = (float)width / height;
            var safe = new Rect(0, 32, width, height - 64);
            for (int screen = 0; screen < 2; screen++)
            {
                string name = screen == 0 ? "home" : "wardrobe";
                if (screen == 0)
                {
                    // Home owns the title/header and its two pins; Wardrobe owns the third
                    // Home pin independently. Both must use this capture's viewport/density.
                    _root.Home.LayoutForViewport(safe, dpi, new Rect(0, 0, width, height));
                    _root.Wardrobe.LayoutForViewport(safe, dpi);
                    Assert.That(_root.Wardrobe.EntryRectPx.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
                    Assert.That(_root.Wardrobe.EntryRectPx.xMax, Is.LessThanOrEqualTo(safe.xMax));
                    // This synchronous test has not run a player-loop frame since Launch.
                    // Pump the real HUD lifecycle so its existing Home-state binding observes
                    // the newly composed screen stack before the first manual camera render.
                    typeof(CatMetro.Presentation.Hud.WavePreview.WavePreviewStrip)
                        .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(_root.Preview, null);
                    Assert.That(_root.Preview.IsVisible, Is.False,
                        "gameplay HUD must observe Home before capture, without manual visibility overrides");
                }
                else
                {
                    Assert.That(_root.Input.HandleTapAtScreen(_root.Wardrobe.EntryRectPx.center), Is.EqualTo(-3));
                    var canvas = _root.Wardrobe.GetComponentInParent<Canvas>();
                    float planeDistance = Vector3.Dot(canvas.transform.position - _root.Cam.transform.position,
                        _root.Cam.transform.forward);
                    Assert.That(planeDistance, Is.EqualTo(canvas.planeDistance).Within(.001f),
                        "restoring the play camera must synchronize the canvas before any render");
                    _root.GetComponent<CatMetro.Presentation.Fx.BoardFx>().Advance(.14f);
                    _root.Wardrobe.LayoutForViewport(safe, dpi);
                }
                Canvas.ForceUpdateCanvases();
                var mount = screen == 0 ? _root.Home.ProfileRig : _root.Wardrobe.ProfileRig;
                Assert.That(mount.Layout(_root.Cam), Is.True, mount.FallbackReason);
                var motion = mount.PrefabRoot.GetComponentInChildren<CatRigPresentation>(true);
                Assert.That(motion, Is.Not.Null);
                Assert.That(motion.AuthoredMotionInstalled, Is.True);
                var source = Resources.Load<GameObject>("CatRigs/BoardCatRig");
                Transform sourceHead = source.GetComponentInChildren<Animator>(true).transform
                    .Find(CatRigPresentation.WeightedHeadPath);
                Vector3 grownScale = sourceHead.localScale * 1.28f;
                var fit = mount.PrefabRoot.parent.parent;
                Vector3 scale = fit.localScale, position = fit.localPosition;
                Rect holder = ProfileHolderPixels((RectTransform)mount.transform.parent);
                _root.MotionOffToggle = false;
                _root.AnimatorDurationScale = 1f;
                for (int phase = 0; phase < 3; phase++)
                {
                    if (phase > 0) mount.AdvanceTurntable(phase == 1 ? 3f : 6f);
                    Assert.That(fit.localScale, Is.EqualTo(scale), "idle must not rescale the whole cat");
                    Assert.That(fit.localPosition, Is.EqualTo(position), "idle must not recenter the whole cat");
                    Assert.That(motion.HeadTransform.localScale, Is.EqualTo(grownScale));
                    Rect skin = FullSkinPixels(mount);
                    AssertContained(skin, holder, 1f, name + " full skin phase " + phase);
                    Assert.That(skin.height, Is.GreaterThan(holder.height * .55f), "positive size control");
                    AssertProfileRasterContained(mount, holder, width, height,
                        name + "-fit-phase-" + phase);
                }
                // Compare the head with its unshaped control at the same fitted scale.
                Rect grown = mount.RenderedHeadScreenRect;
                motion.HeadTransform.localScale = sourceHead.localScale;
                typeof(ProfileRigMount).GetMethod("ApplyAnimatedPose", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(mount, null);
                Rect control = mount.RenderedHeadScreenRect;
                motion.ApplyHeadShape();
                typeof(ProfileRigMount).GetMethod("ApplyAnimatedPose", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(mount, null);
                Assert.That(grown.width, Is.GreaterThan(control.width * 1.15f),
                    "the holder correction must retain visibly enlarged head geometry");
            }
        }

        private Rect ProfileHolderPixels(RectTransform holder)
        {
            var corners = new Vector3[4]; holder.GetWorldCorners(corners);
            Vector3 min = _root.Cam.WorldToScreenPoint(corners[0]);
            Vector3 max = _root.Cam.WorldToScreenPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Rect FullSkinPixels(ProfileRigMount mount)
        {
            var buffer = new Mesh();
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            try
            {
                foreach (var skin in mount.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled) continue;
                    skin.BakeMesh(buffer, true);
                    foreach (Vector3 vertex in buffer.vertices)
                    {
                        Vector3 point = _root.Cam.WorldToScreenPoint(skin.transform.TransformPoint(vertex));
                        Assert.That(point.z, Is.GreaterThan(0f));
                        min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                    }
                }
            }
            finally { Object.DestroyImmediate(buffer); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertContained(Rect skin, Rect holder, float tolerance, string message)
        {
            Assert.That(skin.xMin, Is.GreaterThanOrEqualTo(holder.xMin - tolerance), message);
            Assert.That(skin.xMax, Is.LessThanOrEqualTo(holder.xMax + tolerance), message);
            Assert.That(skin.yMin, Is.GreaterThanOrEqualTo(holder.yMin - tolerance), message);
            Assert.That(skin.yMax, Is.LessThanOrEqualTo(holder.yMax + tolerance), message);
        }

        private void AssertProfileRasterContained(ProfileRigMount mount, Rect holder,
            int width, int height, string name)
        {
            Color32[] actual = ProfileRaster(width, height, name);
            mount.PrefabRoot.gameObject.SetActive(false);
            Color32[] without;
            try { without = ProfileRaster(width, height, null); }
            finally { mount.PrefabRoot.gameObject.SetActive(true); }
            int inside = 0, outside = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (Math.Abs(actual[i].r - without[i].r) <= 4
                        && Math.Abs(actual[i].g - without[i].g) <= 4
                        && Math.Abs(actual[i].b - without[i].b) <= 4) continue;
                    if (holder.Contains(new Vector2(x + .5f, y + .5f))) inside++;
                    else outside++;
                }
            TestContext.Out.WriteLine(name + " " + width + "x" + height
                + " inside=" + inside + " outside=" + outside + " holder=" + holder);
            Assert.That(inside, Is.GreaterThan(100), "positive control: actual licensed rig must paint");
            Assert.That(outside, Is.Zero, "no rig pixels may escape the holder or cover selector tiles");
        }

        private Color32[] ProfileRaster(int width, int height, string name)
        {
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                Canvas.ForceUpdateCanvases();
                _root.Cam.Render();
                RenderTexture.active = _target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                string directory = Environment.GetEnvironmentVariable("CM_HOME_REGRESSION_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(directory) && name != null)
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, name + "-" + width + "x" + height + ".png"),
                        CaptureRig.EncodeOpaqueSrgbPng(texture));
                }
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
        }

        private void AdvanceBoot(float seconds) => typeof(GameRoot).GetMethod("AdvanceHomeBootFade",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_root, new object[] { seconds });

        private int RigPixels(Rect rect, string name)
        {
            var rig = _root.Home.ProfileRig;
            Color32[] actual = Render(name);
            rig.PrefabRoot.gameObject.SetActive(false);
            Color32[] withoutRig;
            try { withoutRig = Render(null); }
            finally { rig.PrefabRoot.gameObject.SetActive(true); }
            int count = 0;
            for (int y = Mathf.Max(0, Mathf.CeilToInt(rect.yMin)); y < Mathf.Min(2048, rect.yMax); y++)
                for (int x = Mathf.Max(0, Mathf.CeilToInt(rect.xMin)); x < Mathf.Min(917, rect.xMax); x++)
                {
                    int i = y * 917 + x;
                    if (Math.Abs(actual[i].r - withoutRig[i].r) > 4
                        || Math.Abs(actual[i].g - withoutRig[i].g) > 4
                        || Math.Abs(actual[i].b - withoutRig[i].b) > 4) count++;
                }
            Debug.Log("HOME_OCCLUSION " + name + " rigPixels=" + count + " rect=" + rect);
            return count;
        }

        private Color32[] Render(string name)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(917, 2048, TextureFormat.RGB24, false);
            try
            {
                Canvas.ForceUpdateCanvases();
                _root.Cam.Render();
                RenderTexture.active = _target;
                texture.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                texture.Apply();
                string dir = Environment.GetEnvironmentVariable("CM_HOME_REGRESSION_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(dir) && name != null)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, name + "-917x2048.png"), CaptureRig.EncodeOpaqueSrgbPng(texture));
                }
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
        }

        private sealed class Storage : IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;
            public Storage(string path) => SaveDirectory = path;
        }
    }
}
