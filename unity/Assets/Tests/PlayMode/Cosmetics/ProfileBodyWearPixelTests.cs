using System;
using System.Collections;
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
    public sealed class ProfileBodyWearPixelTests
    {
        private const int Width = 917, Height = 2048;
        private static readonly string[] BodyNames =
            { "Coat", "CollarLeft", "CollarRight", "ButtonTop", "ButtonBottom" };
        private GameRoot _root;
        private RenderTexture _target;
        private Func<IStorageRoot> _previousStorage;
        private bool _previousSkip;
        private Storage _storage;
        private string _directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _storage = new Storage();
            _previousStorage = GameRoot.DailyStorageRootOverride;
            _previousSkip = GameRoot.DevSkipShippedHome;
            GameRoot.DailyStorageRootOverride = () => _storage;
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _target = new RenderTexture(Width, Height, 24) { antiAliasing = 1 };
            _target.Create();
            _root.Cam.targetTexture = _target;
            _root.Cam.aspect = Width / (float)Height;
            _root.MotionOffToggle = false;
            _directory = Environment.GetEnvironmentVariable("CM_BODYWEAR_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(_directory)) Directory.CreateDirectory(_directory);
            yield return new WaitForSecondsRealtime(.4f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                _root.Cam.targetTexture = null;
                Object.DestroyImmediate(_root.gameObject);
            }
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            GameRoot.DailyStorageRootOverride = _previousStorage;
            GameRoot.DevSkipShippedHome = _previousSkip;
            _storage?.Dispose();
        }

        [UnityTest]
        public IEnumerator HomeAndWardrobe_CoatLeavesWeightedFaceVisible_AcrossIdleYawAndReopen()
        {
            for (int screen = 0; screen < 2; screen++)
            {
                string name = screen == 0 ? "home" : "wardrobe";
                if (screen == 1)
                {
                    Assert.That(_root.Input.HandleTapAtScreen(_root.Wardrobe.EntryRectPx.center), Is.EqualTo(-3));
                    _root.GetComponent<BoardFx>().Advance(.14f);
                    yield return null;
                    Assert.That(_root.Stack.Current, Is.EqualTo("wardrobe"));
                    _root.Wardrobe.LayoutForViewport(new Rect(0, 64, Width, 1920), 408f);
                }
                else _root.Home.LayoutForViewport(new Rect(0, 64, Width, 1920), 408f,
                    new Rect(0, 0, Width, Height));
                CosmeticPortraitView portrait = screen == 0 ? _root.Home.ProfilePortrait : _root.Wardrobe.LargePortrait;
                ProfileRigMount mount = screen == 0 ? _root.Home.ProfileRig : _root.Wardrobe.ProfileRig;
                var source = PortraitTestSource.WithRealTokens(Snapshot("red_tabby", true));
                portrait.Bind(source);
                Canvas.ForceUpdateCanvases();
                Assert.That(mount, Is.Not.Null);
                Assert.That(mount.Layout(_root.Cam), Is.True, "requires the actual admitted paid rig");
                Assert.That(mount.PrefabRoot.GetComponentInChildren<CatRigPresentation>().AuthoredMotionInstalled, Is.True);
                Assert.That(mount.AnimatorCount, Is.Zero);
                mount.BindMotionOff(() => false);
                // Explicit sampling is synchronous: there is no Update between paired renders.
                foreach (float delta in new[] { 0f, .8f, 2.2f, 6f })
                {
                    mount.AdvanceTurntable(delta);
                    AssertFaceClear(name + "-t" + delta.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                        mount, portrait);
                }
                source.Set(Snapshot("red_tabby", false));
                Assert.That(portrait.OutfitLayerTransform.gameObject.activeSelf, Is.False);
                source.Set(Snapshot("red_tabby", true));
                AssertFaceClear(name + "-reequipped", mount, portrait);
                // A fallback cat must get the exact original flat layout even after paid fitting.
                source.Set(Snapshot("blue_siamese", true));
                Assert.That(mount.Mounted, Is.False);
                AssertLegacyBodyAnchors(portrait);
                source.Set(Snapshot("red_tabby", true));
                mount.gameObject.SetActive(false);
                mount.gameObject.SetActive(true);
                Assert.That(mount.Layout(_root.Cam), Is.True);
                AssertFaceClear(name + "-reopened", mount, portrait);
                mount.BindMotionOff(() => true);
                mount.AdvanceTurntable(0f);
                Color32[] rest = Read();
                mount.AdvanceTurntable(1.2f);
                Assert.That(Read(), Is.EqualTo(rest), "Motion Off freezes the complete profile paint");
            }
        }

        private void AssertFaceClear(string name, ProfileRigMount mount, CosmeticPortraitView portrait)
        {
            Image[] body = portrait.OutfitLayerTransform.GetComponentsInChildren<Image>(true)
                .Where(i => BodyNames.Contains(i.name)).ToArray();
            Assert.That(body.Length, Is.EqualTo(5));
            Canvas.ForceUpdateCanvases();
            Color32[] equipped = Read();
            foreach (Image part in body) part.enabled = false;
            Color32[] hatOnly = Read();
            portrait.OutfitLayerTransform.gameObject.SetActive(false);
            Color32[] plain = Read();
            portrait.OutfitLayerTransform.gameObject.SetActive(true);
            foreach (Image part in body) part.enabled = true;
            Color32[] head = HeadMask(mount, name);
            Save(name + "-equipped", equipped);
            Save(name + "-hat-only", hatOnly);
            Save(name + "-plain", plain);
            Save(name + "-weighted-head", head);
            int faceChanges = Different(equipped, hatOnly, head);
            int bodyChanges = Different(equipped, hatOnly);
            int hatChanges = Different(hatOnly, plain);
            TestContext.Out.WriteLine($"BODYWEAR {name} face_changed={faceChanges} body_changed={bodyChanges} hat_changed={hatChanges}");
            // Verify this exact raster can detect a coat intentionally placed over the face.
            RectTransform coat = body.Single(i => i.name == "Coat").rectTransform;
            Vector3 position = coat.position;
            try
            {
                var headPixels = Enumerable.Range(0, head.Length).Where(i => White(head[i])).ToArray();
                Assert.That(headPixels.Length, Is.GreaterThan(100));
                Vector2 center = new Vector2((float)headPixels.Average(i => i % Width),
                    (float)headPixels.Average(i => i / Width));
                Assert.That(RectTransformUtility.ScreenPointToWorldPointInRectangle(coat,
                    center, _root.Cam, out Vector3 world), Is.True);
                coat.position = world;
                Color32[] forced = Read();
                Save(name + "-forced-occlusion-control", forced);
                Assert.That(Different(forced, hatOnly, head), Is.GreaterThan(20),
                    "moving real coat paint over the weighted face must fail the raster criterion");
            }
            finally { coat.position = position; }
            Assert.That(Read(), Is.EqualTo(equipped), "diagnostic controls restore the exact production paint");
            Assert.That(bodyChanges, Is.GreaterThan(100), "a hidden/tiny coat cannot pass");
            Assert.That(hatChanges, Is.GreaterThan(100), "the independent hat must remain visible");
            Assert.That(faceChanges, Is.Zero, "body paint must leave the actual weighted head, including muzzle, visible");
        }

        private Color32[] HeadMask(ProfileRigMount mount, string name)
        {
            SkinnedMeshRenderer skin = mount.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            Transform head = mount.PrefabRoot.GetComponentInChildren<CatRigPresentation>().HeadTransform;
            Camera camera = _root.Cam;
            int culling = camera.cullingMask, layer = skin.gameObject.layer;
            CameraClearFlags clear = camera.clearFlags;
            Color background = camera.backgroundColor;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            bool post = cameraData.renderPostProcessing;
            Material[] materials = skin.sharedMaterials;
            var block = new MaterialPropertyBlock();
            skin.GetPropertyBlock(block);
            var white = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            white.SetColor("_BaseColor", Color.white);
            try
            {
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                cameraData.renderPostProcessing = false;
                skin.gameObject.layer = 31;
                skin.sharedMaterials = Enumerable.Repeat(white, materials.Length).ToArray();
                skin.SetPropertyBlock(null);
                Color32[] realFull = Read();
                using (var geometry = new WeightedHeadMaskGeometry(skin, head))
                {
                    geometry.Material.SetFloat("_HeadOnly", 0f);
                    Color32[] bakedFull = Read();
                    AssertContoursAgree(realFull, bakedFull);
                    geometry.Material.SetFloat("_HeadOnly", 1f);
                    Color32[] weighted = Read();
                    geometry.SetZeroInfluence(true);
                    Color32[] zero = Read();
                    Save(name + "-zero-weight-control", zero);
                    Assert.That(zero.Count(White), Is.Zero);
                    geometry.SetZeroInfluence(false);
                    Assert.That(Read(), Is.EqualTo(weighted));
                    return weighted;
                }
            }
            finally
            {
                skin.sharedMaterials = materials;
                skin.SetPropertyBlock(block);
                skin.gameObject.layer = layer;
                camera.cullingMask = culling;
                camera.clearFlags = clear;
                camera.backgroundColor = background;
                cameraData.renderPostProcessing = post;
                Object.DestroyImmediate(white);
            }
        }

        private static void AssertContoursAgree(Color32[] first, Color32[] second)
        {
            Assert.That(first.Count(White), Is.GreaterThan(100));
            for (int direction = 0; direction < 2; direction++)
            {
                Color32[] a = direction == 0 ? first : second, b = direction == 0 ? second : first;
                int missing = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (!White(a[i])) continue;
                    int x = i % Width, y = i / Width;
                    bool found = false;
                    for (int dy = -1; dy <= 1 && !found; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (x + dx >= 0 && x + dx < Width && y + dy >= 0 && y + dy < Height
                            && White(b[(y + dy) * Width + x + dx])) { found = true; break; }
                    if (!found) missing++;
                }
                Assert.That(missing, Is.Zero, "full-topology diagnostic must match actual flattened profile skin within one raster pixel");
            }
        }

        private static void AssertLegacyBodyAnchors(CosmeticPortraitView portrait)
        {
            RectTransform coat = portrait.OutfitLayerTransform.GetComponentsInChildren<RectTransform>(true)
                .Single(t => t.name == "Coat");
            Vector3[] corners = new Vector3[4];
            coat.GetWorldCorners(corners);
            RectTransform root = portrait.RootTransform;
            Vector3 min = root.InverseTransformPoint(corners[0]);
            Vector3 max = root.InverseTransformPoint(corners[2]);
            Assert.That((min.x - root.rect.xMin) / root.rect.width, Is.EqualTo(.24f).Within(.0001f));
            Assert.That((max.x - root.rect.xMin) / root.rect.width, Is.EqualTo(.76f).Within(.0001f));
            Assert.That((min.y - root.rect.yMin) / root.rect.height, Is.EqualTo(.06f).Within(.0001f));
            Assert.That((max.y - root.rect.yMin) / root.rect.height, Is.EqualTo(.45f).Within(.0001f));
        }

        private Color32[] Read()
        {
            Canvas.ForceUpdateCanvases();
            _root.Cam.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = _target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
        }

        private void Save(string name, Color32[] pixels)
        {
            if (string.IsNullOrEmpty(_directory)) return;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(_directory, name + ".png"), texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static bool White(Color32 c) => c.r >= 250 && c.g >= 250 && c.b >= 250;
        private static int Different(Color32[] a, Color32[] b, Color32[] mask = null)
        {
            int count = 0;
            for (int i = 0; i < a.Length; i++)
                if ((mask == null || White(mask[i])) && Math.Abs(a[i].r - b[i].r)
                    + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 24) count++;
            return count;
        }
        private static CosmeticPortraitSnapshot Snapshot(string cat, bool coat) =>
            new CosmeticPortraitSnapshot(cat, "cat." + cat, coat ? "outfit.conductor" : "", "", "frame.brass");
        private sealed class Storage : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; } = Path.Combine(Path.GetTempPath(), "cm-bodywear-" + Guid.NewGuid().ToString("N"));
            public string CacheDirectory => SaveDirectory;
            public Storage() => Directory.CreateDirectory(SaveDirectory);
            public void Dispose() { if (Directory.Exists(SaveDirectory)) Directory.Delete(SaveDirectory, true); }
        }
    }
}
