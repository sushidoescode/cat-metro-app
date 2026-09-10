using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Fx;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class ProfileFramePixelTests
    {
        private const int Width = 917, Height = 2048;

        [UnityTest, Timeout(600000)]
        public IEnumerator HomeAndWardrobe_FrameClearsActualSkinAndHat_AcrossIdleYawAndReopen()
        {
            string directory = Environment.GetEnvironmentVariable("CM_PROFILE_FRAME_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var storage = new ProbeStorage();
            Func<IStorageRoot> previousStorage = GameRoot.DailyStorageRootOverride;
            bool previousSkip = GameRoot.DevSkipShippedHome;
            GameRoot root = null;
            RenderTexture target = null;
            try
            {
                GameRoot.DailyStorageRootOverride = () => storage;
                GameRoot.DevSkipShippedHome = false;
                root = GameRoot.Launch();
                target = CaptureRig.CreateTarget(new CaptureRig.Size(Width, Height));
                root.Cam.targetTexture = target;
                root.Cam.aspect = Width / (float)Height;
                root.MotionOffToggle = false;
                root.AnimatorDurationScale = 1f;
                yield return new WaitForSecondsRealtime(.4f);
                Rect safe = new Rect(0, 64, Width, 1920);
                root.Home.LayoutForViewport(safe, 408f, new Rect(0, 0, Width, Height));
                root.Wardrobe.LayoutForViewport(safe, 408f);
                for (int screen = 0; screen < 2; screen++)
                {
                    string name = screen == 0 ? "home" : "wardrobe";
                    if (screen == 1)
                    {
                        Assert.That(root.Input.HandleTapAtScreen(root.Wardrobe.EntryRectPx.center), Is.EqualTo(-3));
                        root.GetComponent<BoardFx>().Advance(.14f);
                        yield return null;
                        Assert.That(root.Stack.Current, Is.EqualTo("wardrobe"));
                        root.Wardrobe.LayoutForViewport(safe, 408f);
                    }
                    CosmeticPortraitView portrait = screen == 0 ? root.Home.ProfilePortrait : root.Wardrobe.LargePortrait;
                    ProfileRigMount mount = screen == 0 ? root.Home.ProfileRig : root.Wardrobe.ProfileRig;
                    foreach (string frame in new[] { "frame.brass", "frame.lantern" })
                    {
                        var source = PortraitTestSource.WithRealTokens(Snapshot("red_tabby", frame));
                        portrait.Bind(source);
                        Canvas.ForceUpdateCanvases();
                        Assert.That(mount.Layout(root.Cam), Is.True, mount.FallbackReason);
                        Assert.That(mount.PrefabRoot.GetComponentInChildren<CatRigPresentation>().AuthoredMotionInstalled, Is.True);
                        mount.BindMotionOff(() => false);
                        Vector3[] stationary = FrameCorners(portrait);
                        float elapsed = 0f;
                        foreach (float delta in new[] { 0f, .8f, 2.2f, 6f })
                        {
                            elapsed += delta;
                            mount.AdvanceTurntable(delta);
                            Measure(root, target, mount, portrait, directory,
                                name + "-" + frame + "-advance-" + elapsed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), delta == 0f);
                            Vector3[] current = FrameCorners(portrait);
                            for (int i = 0; i < stationary.Length; i++)
                                Assert.That(Vector3.Distance(current[i], stationary[i]), Is.LessThan(.00001f), "frame is stationary while the head turns/breathes");
                        }
                        source.Set(Snapshot("red_tabby", ""));
                        Assert.That(portrait.FrameLayerTransform.gameObject.activeSelf, Is.False);
                        source.Set(Snapshot("red_tabby", frame));
                        Measure(root, target, mount, portrait, directory, name + "-" + frame + "-reequipped", false);
                        foreach (string fallback in new[] { "blue_siamese", "yellow_longhair" })
                        {
                            source.Set(Snapshot(fallback, frame));
                            Assert.That(mount.Mounted, Is.False);
                            AssertLegacyPixels(root.Cam, target, portrait, source);
                        }
                        source.Set(Snapshot("red_tabby", frame));
                        GameObject holder = portrait.transform.parent.gameObject;
                        holder.SetActive(false); holder.SetActive(true);
                        Assert.That(mount.Layout(root.Cam), Is.True);
                        Measure(root, target, mount, portrait, directory, name + "-" + frame + "-reopened", false);
                        mount.BindMotionOff(() => true);
                        mount.AdvanceTurntable(0f);
                        Color32[] rest = Read(root.Cam, target);
                        Rect restRegion = SubjectBounds(root.Cam, portrait);
                        mount.AdvanceTurntable(1.2f);
                        AssertSubjectPaint(root.Cam, portrait, Read(root.Cam, target), rest, restRegion,
                            "Motion Off freezes the full portrait, including frame");
                    }
                }
            }
            finally
            {
                if (root != null) { root.Cam.targetTexture = null; Object.DestroyImmediate(root.gameObject); }
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                GameRoot.DailyStorageRootOverride = previousStorage;
                GameRoot.DevSkipShippedHome = previousSkip;
                storage.Dispose();
            }
        }

        private static CosmeticPortraitSnapshot Snapshot(string cat, string frame) =>
            new CosmeticPortraitSnapshot(cat, "cat." + cat, "outfit.conductor", "", frame);

        private static void Measure(GameRoot root, RenderTexture target, ProfileRigMount mount,
            CosmeticPortraitView portrait, string directory, string name, bool captureControls)
        {
            Camera camera = root.Cam;
            SkinnedMeshRenderer skin = mount.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            RectTransform hat = (RectTransform)portrait.OutfitLayerTransform.Find("outfit.conductor/HeadWear");
            RectTransform frame = portrait.FrameLayerTransform;
            bool refresh = skin.forceMatrixRecalculationPerRender;
            try
            {
                // Multiple manual renders in one frame need the current flattened skin matrix.
                skin.forceMatrixRecalculationPerRender = true;
                Color32[] equipped = Read(camera, target);
                Rect subjectRegion = SubjectBounds(camera, portrait);
                frame.gameObject.SetActive(false);
                Color32[] unframed = Read(camera, target);
                hat.gameObject.SetActive(false);
                Color32[] hatHidden = Read(camera, target);
                hat.gameObject.SetActive(true);
                AssertSubjectPaint(camera, portrait, Read(camera, target), unframed, subjectRegion, "hat control restores exact unframed paint");
                frame.gameObject.SetActive(true);
                AssertSubjectPaint(camera, portrait, Read(camera, target), equipped, subjectRegion, "frame control restores exact paint");
                Color32[] skinMask = ActualSkinMask(camera, target, skin);
                AssertSubjectPaint(camera, portrait, Read(camera, target), equipped, subjectRegion, "skin-mask control restores exact portrait paint");
                bool[] skinPixels = skinMask.Select(White).ToArray();
                bool[] hatPixels = unframed.Select((c, i) => Difference(c, hatHidden[i]) > 24).ToArray();
                Assert.That(skinPixels.Count(p => p), Is.GreaterThan(100));
                Assert.That(hatPixels.Count(p => p), Is.GreaterThan(30), "actual headwear is visible independently of frame/skin masks");
                int visibleFrame = equipped.Where((c, i) => Difference(c, unframed[i]) > 24).Count();
                int overSkin = Overlap(equipped, unframed, skinPixels);
                int overHat = Overlap(equipped, unframed, hatPixels);
                Save(directory, name + "-equipped", equipped);
                Save(directory, name + "-without-frame", unframed);
                if (captureControls)
                {
                    Save(directory, name + "-actual-skin", skinMask);
                    Save(directory, name + "-hat-hidden-control", hatHidden);
                }
                // Move an actual rail over each independent region. The same pixel metric
                // must reject this obvious occluder before accepting the production layout.
                RectTransform rail = frame.GetComponentsInChildren<Image>().First(i => i.name.EndsWith("Top", StringComparison.Ordinal)).rectTransform;
                Vector3 position = rail.localPosition, scale = rail.localScale;
                try
                {
                    ForceRail(rail, camera, PixelBounds(skinPixels).center);
                    Color32[] forcedSkin = Read(camera, target);
                    Assert.That(Overlap(forcedSkin, unframed, skinPixels), Is.GreaterThan(25));
                    Assert.That(SubjectDifferences(equipped, forcedSkin, subjectRegion, out _), Is.GreaterThan(25),
                        "the exact subject metric detects the existing real-rail occlusion control");
                    ForceRail(rail, camera, PixelBounds(hatPixels).center);
                    Color32[] forcedHat = Read(camera, target);
                    Assert.That(Overlap(forcedHat, unframed, hatPixels), Is.GreaterThan(5));
                    Assert.That(SubjectDifferences(equipped, forcedHat, subjectRegion, out _), Is.GreaterThan(5));
                    if (captureControls)
                    {
                        Save(directory, name + "-forced-skin-control", forcedSkin);
                        Save(directory, name + "-forced-hat-control", forcedHat);
                    }
                }
                finally { rail.localPosition = position; rail.localScale = scale; }
                AssertSubjectPaint(camera, portrait, Read(camera, target), equipped, subjectRegion, "forced rail restores exact local transform and paint");
                Transform token = frame.Find(portrait.AppliedFrameAssetId);
                string prefix = portrait.AppliedFrameAssetId == "frame.brass" ? "Outer" : "NavyOuter";
                float verticalStroke = Project(camera, (RectTransform)token.Find(prefix + "Left")).width;
                float horizontalStroke = Project(camera, (RectTransform)token.Find(prefix + "Top")).height;
                Rect frameBounds = Project(camera, frame);
                TestContext.Out.WriteLine($"PROFILE_FRAME {name} visible={visibleFrame} over_skin={overSkin} over_hat={overHat} stroke_px={verticalStroke:R}/{horizontalStroke:R} frame_bounds={frameBounds}");
                Assert.That(verticalStroke, Is.EqualTo(horizontalStroke).Within(.05f), "rails retain a uniform physical stroke");
                Assert.That(horizontalStroke, Is.GreaterThanOrEqualTo(1.5f), "a narrow free strip must not collapse the visible outer rail");
                Assert.That(visibleFrame, Is.GreaterThan(100), "the frame must remain visibly present");
                Assert.That(overSkin, Is.Zero, name + " frame crosses the actual skin projection");
                Assert.That(overHat, Is.Zero, name + " frame crosses the independently visible hat");
                AssertNeighbourClearance(root, portrait, frameBounds);
            }
            finally
            {
                hat.gameObject.SetActive(true); frame.gameObject.SetActive(true);
                skin.forceMatrixRecalculationPerRender = refresh;
            }
        }

        private static void AssertNeighbourClearance(GameRoot root, CosmeticPortraitView portrait, Rect frame)
        {
            if (portrait == root.Home.ProfilePortrait)
            {
                Assert.That(root.Home.HeroRectPx.Contains(frame.min) && root.Home.HeroRectPx.Contains(frame.max),
                    Is.True, "the frame stays in the Home diorama, clear of title and controls");
                foreach (Rect control in new[] { root.Home.PinPaintedRectPx, root.Home.DailyPinPaintedRectPx,
                    root.Home.AudioToggleRectPx, root.Wardrobe.EntryRectPx })
                    Assert.That(frame.Overlaps(control), Is.False, "frame cannot cover Home controls");
                return;
            }
            Transform card = portrait.transform.parent.parent;
            Transform panel = card.parent;
            foreach (string band in new[] { "CatSelectorBand", "TabsBand", "ItemsBand" })
            {
                var rect = panel.Find(band) as RectTransform;
                Assert.That(rect, Is.Not.Null, band);
                Assert.That(frame.Overlaps(Project(root.Cam, rect)), Is.False, "frame cannot cover " + band);
            }
            var plaque = card.Find("PortraitStand/StandPlaque") as RectTransform;
            Assert.That(plaque, Is.Not.Null);
            Assert.That(frame.Overlaps(Project(root.Cam, plaque)), Is.False, "frame cannot cover the name plaque");
            Assert.That(frame.Overlaps(root.Wardrobe.BackRectPx), Is.False);
            Assert.That(frame.Overlaps(root.Wardrobe.PrimaryActionRectPx), Is.False);
        }

        private static void ForceRail(RectTransform rail, Camera camera, Vector2 screen)
        {
            Assert.That(RectTransformUtility.ScreenPointToWorldPointInRectangle(rail, screen, camera, out Vector3 world), Is.True);
            rail.position = world;
            // The positive control stays substantial even when the fitted rail is subpixel thin.
            rail.localScale = new Vector3(1f, 8f, 1f);
        }

        private static void AssertLegacyPixels(Camera camera, RenderTexture target,
            CosmeticPortraitView portrait, PortraitTestSource source)
        {
            Color32[] fallback = Read(camera, target);
            Rect fallbackRegion = SubjectBounds(camera, portrait);
            CosmeticPortraitView legacy = null;
            try
            {
                portrait.gameObject.SetActive(false);
                // A separately painted portrait has never entered the 3D fit path.
                legacy = CosmeticPortraitView.Create(portrait.transform.parent, source, "LegacyFramePixelControl");
                Color32[] independent = Read(camera, target);
                // Include both independently painted layouts so a moved or resized fallback
                // cannot escape the comparison region.
                AssertSubjectPaint(camera, legacy, independent, fallback, fallbackRegion,
                    "fallback preserves independently painted legacy pixels");
            }
            finally
            {
                if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
                portrait.gameObject.SetActive(true);
            }
        }

        private static Rect SubjectBounds(Camera camera, CosmeticPortraitView portrait)
        {
            // This test owns the full portrait, including transparent holder space and every
            // frame corner. It does not own unrelated desk/coffee-cup raster elsewhere in Home.
            return Bounds(portrait.GetComponentsInChildren<Image>()
                .Where(image => image.enabled && image.gameObject.activeInHierarchy)
                .SelectMany(image => Corners(image.rectTransform))
                .Concat(Corners((RectTransform)portrait.transform.parent))
                .Concat(Corners(portrait.FrameLayerTransform))
                .Select(world => (Vector2)camera.WorldToScreenPoint(world)));
        }

        private static void AssertSubjectPaint(Camera camera, CosmeticPortraitView portrait,
            Color32[] actual, Color32[] expected, Rect originalRegion, string message)
        {
            Rect current = SubjectBounds(camera, portrait);
            Rect region = Rect.MinMaxRect(Mathf.Min(originalRegion.xMin, current.xMin),
                Mathf.Min(originalRegion.yMin, current.yMin), Mathf.Max(originalRegion.xMax, current.xMax),
                Mathf.Max(originalRegion.yMax, current.yMax));
            int changed = SubjectDifferences(expected, actual, region, out int first);
            string location = first < 0 ? "none" : (first % Width) + "," + (first / Width);
            Assert.That(changed, Is.Zero, message + "; subject=" + region + "; first changed pixel=" + location);
        }

        private static int SubjectDifferences(Color32[] expected, Color32[] actual, Rect region, out int first)
        {
            Assert.That(expected.Length, Is.EqualTo(Width * Height));
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            int left = Mathf.Clamp(Mathf.FloorToInt(region.xMin), 0, Width);
            int right = Mathf.Clamp(Mathf.CeilToInt(region.xMax), 0, Width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(region.yMin), 0, Height);
            int top = Mathf.Clamp(Mathf.CeilToInt(region.yMax), 0, Height);
            Assert.That(right - left, Is.GreaterThan(0), "subject must intersect the rendered viewport");
            Assert.That(top - bottom, Is.GreaterThan(0), "subject must intersect the rendered viewport");
            int changed = 0; first = -1;
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                {
                    int index = y * Width + x;
                    if (expected[index].Equals(actual[index])) continue;
                    changed++;
                    if (first < 0) first = index;
                }
            return changed;
        }

        private static bool White(Color32 c) => c.r > 250 && c.g > 250 && c.b > 250;
        private static int Difference(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
        private static int Overlap(Color32[] frame, Color32[] plain, bool[] mask) =>
            frame.Where((c, i) => mask[i] && Difference(c, plain[i]) > 24).Count();
        private static Rect PixelBounds(bool[] mask) => Bounds(Enumerable.Range(0, mask.Length)
            .Where(i => mask[i]).Select(i => new Vector2(i % Width + .5f, i / Width + .5f)));
        private static Vector3[] FrameCorners(CosmeticPortraitView portrait) => portrait.FrameLayerTransform
            .GetComponentsInChildren<Image>().SelectMany(i => Corners(i.rectTransform)).ToArray();
        private static Vector3[] Corners(RectTransform rect)
        {
            var result = new Vector3[4]; rect.GetWorldCorners(result); return result;
        }

        private static Color32[] ActualSkinMask(Camera camera, RenderTexture target, SkinnedMeshRenderer skin)
        {
            Material[] materials = skin.sharedMaterials;
            bool hasBlock = skin.HasPropertyBlock(), enabled = skin.enabled;
            var block = new MaterialPropertyBlock(); skin.GetPropertyBlock(block);
            int culling = camera.cullingMask, layer = skin.gameObject.layer;
            CameraClearFlags clear = camera.clearFlags;
            Color background = camera.backgroundColor;
            var data = camera.GetUniversalAdditionalCameraData(); bool post = data.renderPostProcessing;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            var white = new Material(shader);
            white.SetColor("_BaseColor", Color.white);
            try
            {
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; data.renderPostProcessing = false;
                skin.gameObject.layer = 31;
                skin.sharedMaterials = Enumerable.Repeat(white, materials.Length).ToArray();
                skin.SetPropertyBlock(null);
                Color32[] actual = Read(camera, target);
                skin.enabled = false;
                Assert.That(Read(camera, target).Count(c => c.r != 0 || c.g != 0 || c.b != 0), Is.Zero);
                skin.enabled = enabled;
                Assert.That(Read(camera, target), Is.EqualTo(actual));
                return actual;
            }
            finally
            {
                skin.enabled = enabled; skin.sharedMaterials = materials;
                skin.SetPropertyBlock(hasBlock ? block : null); skin.gameObject.layer = layer;
                camera.cullingMask = culling; camera.clearFlags = clear; camera.backgroundColor = background;
                data.renderPostProcessing = post; Object.DestroyImmediate(white);
                Assert.That(skin.HasPropertyBlock(), Is.EqualTo(hasBlock));
            }
        }

        private static Rect Project(Camera camera, RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Bounds(corners.Select(p => (Vector2)camera.WorldToScreenPoint(p)));
        }
        private static Rect Bounds(IEnumerable<Vector2> source)
        {
            Vector2[] points = source.ToArray();
            return points.Length == 0 ? default : Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y),
                points.Max(p => p.x), points.Max(p => p.y));
        }
        private static Color32[] Read(Camera camera, RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, mipChain: false, linear: false);
            try
            {
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply();
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
        }
        private static void Save(string directory, string name, Color32[] pixels)
        {
            if (string.IsNullOrEmpty(directory)) return;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, mipChain: false, linear: false);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), CaptureRig.EncodeOpaqueSrgbPng(texture)); }
            finally { Object.DestroyImmediate(texture); }
        }
        private sealed class ProbeStorage : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; } = Path.Combine(Path.GetTempPath(), "cm-profile-support-" + Guid.NewGuid().ToString("N"));
            public string CacheDirectory => SaveDirectory;
            public ProbeStorage() => Directory.CreateDirectory(SaveDirectory);
            public void Dispose() { if (Directory.Exists(SaveDirectory)) Directory.Delete(SaveDirectory, true); }
        }
    }
}
