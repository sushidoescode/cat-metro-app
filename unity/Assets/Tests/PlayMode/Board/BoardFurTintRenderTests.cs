using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardFurTintRenderTests
    {
        private GameObject _host;
        private GameRoot _root;
        private Mesh _mesh;
        private Material _source;
        private Texture2D _atlas;
        private bool _previousDevSkip;
        private static readonly byte[] Routes =
            { CatColor.Red, CatColor.Blue, CatColor.Yellow, CatColor.Green, CatColor.Wild };

        [SetUp]
        public void SetUp() => _previousDevSkip = GameRoot.DevSkipShippedHome;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            if (_mesh != null) Object.DestroyImmediate(_mesh);
            if (_source != null) Object.DestroyImmediate(_source);
            if (_atlas != null) Object.DestroyImmediate(_atlas);
            GameRoot.DevSkipShippedHome = _previousDevSkip;
        }

        [UnityTest]
        public IEnumerator RenderedSwatches_ProtectCreamNavyAndPinkAndKeepStripeContrast()
        {
            Camera camera = BuildSwatches();
            var fur = BoardFurTint.TryInstall(_host.transform.Find("Swatches").gameObject);
            Assert.That(fur, Is.Not.Null, "the real Resources shader must compile and bind");
            Assert.That(Resources.Load<Shader>(BoardFurTint.ShaderResourcePath).isSupported, Is.True,
                "GPU checks require the actual fur shader, including its installed URP includes");
            var target = new RenderTexture(600, 100, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            target.Create();
            camera.targetTexture = target;
            try
            {
                yield return null;
                fur.SetPreview(0f, false);
                Color32[] natural = Read(camera, target);
                fur.SetPreview(1f, true);
                Color32[] mask = Read(camera, target);
                for (int patch = 0; patch < 4; patch++)
                    Assert.That(Swatch(mask, patch).maxColorComponent, Is.LessThan(.02f),
                        "cream, shaded cream, navy and pink must be protected in the GPU mask: " + patch);
                for (int patch = 4; patch < 6; patch++)
                    Assert.That(Swatch(mask, patch).r, Is.GreaterThan(.9f),
                        "positive control must actually recolour bright fur and dark stripes: " + patch);
                fur.SetPreview(1f, false);
                foreach (byte route in Routes)
                {
                    fur.Apply(CatLine.ColorOf(route));
                    Color32[] coloured = Read(camera, target);
                    for (int patch = 0; patch < 4; patch++)
                        Assert.That(Vector4.Distance(Swatch(coloured, patch), Swatch(natural, patch)),
                            Is.LessThan(.01f), CatLine.NameOfCode(route) + "/protected patch=" + patch);
                    Color coat = Swatch(coloured, 4), stripe = Swatch(coloured, 5);
                    Assert.That(Vector4.Distance(coat, Swatch(natural, 4)), Is.GreaterThan(.04f),
                        "positive control must visibly change the coat for " + CatLine.NameOfCode(route));
                    Assert.That(coat.maxColorComponent - stripe.maxColorComponent, Is.GreaterThan(.12f),
                        "the dark stripe must retain visible contrast");
                    AssertRouteHue(coat, route);
                }
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CaptureRealBoard_AllRoutesNaturalAndMask_WhenRequested()
        {
            string directory = System.Environment.GetEnvironmentVariable("CM_FUR_TINT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            GameRoot.DevSkipShippedHome = true;
            _root = GameRoot.Launch();
            _root.enabled = false;
            _root.MotionOffToggle = true;
            _root.View.StopAmbient();
            _root.View.Fx.enabled = false;
            yield return null;
            _root.Session.State.Trains[0] = new TrainSlot { Id = 1, Color = CatColor.Red,
                EdgeId = 1, ProgressTicks = 6, NodeId = 1, State = TrainState.OnEdge };
            _root.View.UpdateFrom(_root.Session, 0f);
            _root.View.UpdateFrom(_root.Session, 1f);
            var train = _root.View.transform.Find("train:0").GetComponent<ToyTrainView>();
            Assert.That(train.RigAdmitted, Is.True, train.RigFallbackReason);
            var fur = train.GetComponentInChildren<BoardFurTint>(true);
            Assert.That(fur, Is.Not.Null, "no caption may claim a masked rig while rendering fallback");
            var skin = train.GetComponentInChildren<SkinnedMeshRenderer>(true);
            foreach (Animator animator in _root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (ParticleSystem particles in _root.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Camera camera = _root.Cam;
            RenderTexture oldTarget = camera.targetTexture;
            Vector3 oldPosition = camera.transform.position;
            float oldOrtho = camera.orthographicSize;
            var target = new RenderTexture(917, 2048, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            target.Create();
            camera.targetTexture = target;
            var checks = new List<string>();
            bool previousForceMatrices = skin.forceMatrixRecalculationPerRender;
            try
            {
                // Route reuse can resample the rig between manual renders in one update.
                // Refresh actual GPU skin matrices for every comparison, as in the seat probe.
                skin.forceMatrixRecalculationPerRender = true;
                yield return null;
                BoardSceneLook.FitCamera(camera, _root.View);
                _root.Preview.Refresh();
                _root.Preview.LayoutForViewport(new Rect(0, 64, 917, 1920), 408f);
                Canvas.ForceUpdateCanvases();
                Vector3 boardPosition = camera.transform.position;
                float boardOrtho = camera.orthographicSize;
                Vector3 closePosition = new Vector3(skin.bounds.center.x, skin.bounds.center.y,
                    camera.transform.position.z);
                const float closeOrtho = .70f;
                int tick = _root.Session.State.Tick;
                Vector3 anchor = train.transform.position;

                fur.SetPreview(0f, false);
                SavePair(camera, target, directory, "natural-control", boardPosition, boardOrtho,
                    closePosition, closeOrtho, out Color32[] natural);

                // A stock-Lit source-material control catches accidental lighting or texture
                // changes in the wrapper, independently of its zero-strength arithmetic.
                var sourceSkin = Resources.Load<GameObject>(CatModelCatalog.ResourcePath)
                    .GetComponentInChildren<SkinnedMeshRenderer>(true);
                Material[] mountedMaterials = skin.sharedMaterials;
                var mountedProperties = new MaterialPropertyBlock();
                skin.GetPropertyBlock(mountedProperties);
                int stockDifferent;
                try
                {
                    skin.sharedMaterials = sourceSkin.sharedMaterials;
                    skin.SetPropertyBlock(null);
                    SavePair(camera, target, directory, "stock-lit-control", boardPosition, boardOrtho,
                        closePosition, closeOrtho, out Color32[] stock);
                    stockDifferent = CountDifferent(stock, natural, 3);
                    checks.Add("stock_lit_different_pixels_gt3=" + stockDifferent);
                }
                finally
                {
                    skin.sharedMaterials = mountedMaterials;
                    skin.SetPropertyBlock(mountedProperties);
                }
                Color32[] silhouette = ReadSkinSilhouette(camera, target, skin, _root.gameObject);
                fur.SetPreview(1f, true);
                SavePair(camera, target, directory, "mask-debug", boardPosition, boardOrtho,
                    closePosition, closeOrtho, out Color32[] mask);
                int[] cream = ProtectedPixels(natural, silhouette, 0);
                int[] navy = ProtectedPixels(natural, silhouette, 1);
                int[] pink = ProtectedPixels(natural, silhouette, 2);
                checks.Add("protected_cream=" + cream.Length + " navy=" + navy.Length + " pink=" + pink.Length);
                checks.Add(pink.Length > 0 ? "real_pink_face_view=observed" :
                    "real_pink_face_view=not_observed;named_GPU_pink_swatch_is_the_only_pink_control");
                // Select visibly warm coat from the natural photograph, independently of
                // the shader mask. An all-black paid-atlas mask must fail even if the
                // procedural swatches and all unchanged-face assertions pass.
                int[] warmCoat = WarmCoatPixels(natural, silhouette);
                int[] positiveCoat = warmCoat.Where(index => mask[index].r > 200
                    && mask[index].g > 200 && mask[index].b > 200).ToArray();
                checks.Add("natural_warm_coat=" + warmCoat.Length
                    + " real_mask_positive_warm_coat=" + positiveCoat.Length);
                var routeCoat = new List<(byte Route, int Changed, Color Mean)>();
                var protectedSets = new[] { cream, navy, pink };
                foreach (byte route in Routes)
                {
                    // Use the production session-to-view binding, including occupant reuse.
                    TrainSlot slot = _root.Session.State.Trains[0];
                    slot.Id = (short)(100 + route);
                    slot.Color = route;
                    _root.Session.State.Trains[0] = slot;
                    _root.View.UpdateFrom(_root.Session, 1f);
                    fur.SetPreview(1f, false);
                    SavePair(camera, target, directory, CatLine.NameOfCode(route), boardPosition, boardOrtho,
                        closePosition, closeOrtho, out Color32[] coloured);
                    int coatChanged = positiveCoat.Count(index => ChannelDelta(natural[index], coloured[index]) > 8);
                    Color meanCoat = Color.clear;
                    foreach (int index in positiveCoat) meanCoat += (Color)coloured[index];
                    if (positiveCoat.Length > 0) meanCoat /= positiveCoat.Length;
                    routeCoat.Add((route, coatChanged, meanCoat));
                    checks.Add(CatLine.NameOfCode(route) + "/coat_changed_gt8=" + coatChanged
                        + "/mean_coat=" + meanCoat.ToString("F4"));
                    for (int group = 0; group < protectedSets.Length; group++)
                    {
                        int[] indices = protectedSets[group];
                        int changed = indices.Count(index => ChannelDelta(natural[index], coloured[index]) > 3);
                        checks.Add(CatLine.NameOfCode(route) + "/group=" + group + "/changed_gt3=" + changed);
                    }
                    Assert.That(_root.Session.State.Tick, Is.EqualTo(tick));
                    Assert.That(Vector3.Distance(train.transform.position, anchor), Is.LessThan(.00001f));
                    Assert.That(train.CatTint, Is.EqualTo(CatLine.ColorOf(route)));
                }
                File.WriteAllLines(Path.Combine(directory, "readback.txt"), checks);
                TestContext.Out.WriteLine("FUR_CAPTURE " + string.Join(" ", checks));
                // Save every broken-state render before these semantic assertions. A colour
                // classifier on the natural image is independent of the shader's atlas mask.
                Assert.That(stockDifferent, Is.LessThan(20),
                    "zero-strength fur shader must retain stock URP lighting and atlas appearance");
                Assert.That(cream.Length, Is.GreaterThan(100), "real cream regions must be present");
                Assert.That(navy.Length, Is.GreaterThan(100), "real navy eye/nose regions must be present");
                Assert.That(warmCoat.Length, Is.GreaterThan(100), "the actual photograph must expose warm fur");
                Assert.That(positiveCoat.Length, Is.GreaterThan(100),
                    "the actual paid-atlas mask must select visible warm fur; all-black masks cannot pass");
                foreach (var result in routeCoat)
                {
                    Assert.That(result.Changed, Is.GreaterThan(positiveCoat.Length / 2),
                        CatLine.NameOfCode(result.Route) + " must visibly recolour the majority of strongly masked coat pixels");
                    AssertRouteHue(result.Mean, result.Route);
                }
                foreach (int[] indices in protectedSets)
                    Assert.That(indices.Count(index => mask[index].r > 12), Is.Zero,
                        "natural cream/navy/pink pixels must stay black in the actual GPU mask");
                foreach (string check in checks.Where(line => line.Contains("/changed_gt3=")))
                    Assert.That(check.EndsWith("=0"), Is.True, check);
            }
            finally
            {
                skin.forceMatrixRecalculationPerRender = previousForceMatrices;
                fur.SetPreview(1f, false);
                camera.targetTexture = oldTarget;
                camera.transform.position = oldPosition;
                camera.orthographicSize = oldOrtho;
                target.Release();
                Object.DestroyImmediate(target);
                if (checks.Count > 0) File.WriteAllLines(Path.Combine(directory, "readback.txt"), checks);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private Camera BuildSwatches()
        {
            _host = new GameObject("fur-render-fixture");
            var cameraGo = new GameObject("fur-camera");
            cameraGo.transform.SetParent(_host.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.localPosition = new Vector3(0, 0, -5);
            camera.orthographic = true;
            camera.orthographicSize = .5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            var light = new GameObject("neutral key").AddComponent<Light>();
            light.transform.SetParent(_host.transform, false);
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.cullingMask = 1 << 30;
            var quad = new GameObject("Swatches") { layer = 30 };
            quad.transform.SetParent(_host.transform, false);
            _mesh = new Mesh { vertices = new[] { new Vector3(-3,-.5f,0), new Vector3(3,-.5f,0),
                new Vector3(3,.5f,0), new Vector3(-3,.5f,0) },
                uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) },
                triangles = new[] { 0,2,1,0,3,2 } };
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            quad.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _atlas = new Texture2D(6, 1, TextureFormat.RGB24, false, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _atlas.SetPixels32(new[] { new Color32(218,198,137,255), new Color32(215,186,126,255),
                new Color32(22,29,62,255), new Color32(211,148,115,255),
                new Color32(218,145,53,255), new Color32(143,75,20,255) });
            _atlas.Apply(false, false);
            _source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _source.SetTexture("_BaseMap", _atlas);
            _source.SetColor("_BaseColor", Color.white);
            _source.SetFloat("_Smoothness", 0f);
            quad.AddComponent<MeshRenderer>().sharedMaterial = _source;
            return camera;
        }

        private static Color Swatch(Color32[] pixels, int patch) => pixels[50 * 600 + patch * 100 + 50];
        private static void AssertRouteHue(Color colour, byte route)
        {
            if (route == CatColor.Red) Assert.That(colour.r, Is.GreaterThan(Mathf.Max(colour.g, colour.b)));
            if (route == CatColor.Blue) Assert.That(colour.b, Is.GreaterThan(Mathf.Max(colour.r, colour.g)));
            if (route == CatColor.Green) Assert.That(colour.g, Is.GreaterThan(Mathf.Max(colour.r, colour.b)));
            if (route == CatColor.Yellow) Assert.That(Mathf.Min(colour.r, colour.g), Is.GreaterThan(colour.b));
            if (route == CatColor.Wild) Assert.That(Mathf.Min(colour.r, colour.b), Is.GreaterThan(colour.g));
        }

        private static Color32[] Read(Camera camera, RenderTexture target)
        {
            RenderTexture old = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels = CaptureRig.ReadRgb24(target);
                return pixels.GetPixels32();
            }
            finally
            {
                RenderTexture.active = old;
                if (pixels != null) Object.DestroyImmediate(pixels);
            }
        }

        private static void SavePair(Camera camera, RenderTexture target, string directory, string name,
            Vector3 boardPosition, float boardOrtho, Vector3 closePosition, float closeOrtho,
            out Color32[] close)
        {
            camera.transform.position = boardPosition;
            camera.orthographicSize = boardOrtho;
            Save(Path.Combine(directory, name + "-board.png"), Read(camera, target), target);
            camera.transform.position = closePosition;
            camera.orthographicSize = closeOrtho;
            Canvas[] canvases = camera.transform.root.GetComponentsInChildren<Canvas>(true);
            bool[] enabled = canvases.Select(canvas => canvas.enabled).ToArray();
            try
            {
                foreach (Canvas canvas in canvases) canvas.enabled = false;
                close = Read(camera, target);
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i];
            }
            Save(Path.Combine(directory, name + "-close.png"), close, target);
        }

        private static void Save(string path, Color32[] colours, RenderTexture target)
        {
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
            try
            {
                pixels.SetPixels32(colours);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, CaptureRig.EncodeOpaqueSrgbPng(pixels));
            }
            finally { Object.DestroyImmediate(pixels); }
        }

        private static Color32[] ReadSkinSilhouette(Camera camera, RenderTexture target, Renderer skin,
            GameObject sceneRoot)
        {
            Renderer[] renderers = sceneRoot.GetComponentsInChildren<Renderer>(true);
            Material[][] materials = renderers.Select(renderer => renderer.sharedMaterials).ToArray();
            bool[] hadProperties = renderers.Select(renderer => renderer.HasPropertyBlock()).ToArray();
            var properties = renderers.Select(renderer =>
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                return block;
            }).ToArray();
            Canvas[] canvases = sceneRoot.GetComponentsInChildren<Canvas>(true);
            bool[] enabled = canvases.Select(canvas => canvas.enabled).ToArray();
            CameraClearFlags oldClear = camera.clearFlags;
            Color oldBackground = camera.backgroundColor;
            var white = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var black = new Material(white);
            white.SetColor("_BaseColor", Color.white);
            black.SetColor("_BaseColor", Color.black);
            Color32[] silhouette;
            try
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterials = Enumerable.Repeat(renderers[i] == skin ? white : black,
                        materials[i].Length).ToArray();
                    renderers[i].SetPropertyBlock(null);
                }
                foreach (Canvas canvas in canvases) canvas.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                silhouette = Read(camera, target);
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterials = materials[i];
                    renderers[i].SetPropertyBlock(hadProperties[i] ? properties[i] : null);
                }
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i];
                camera.clearFlags = oldClear;
                camera.backgroundColor = oldBackground;
                Object.DestroyImmediate(white);
                Object.DestroyImmediate(black);
            }
            for (int i = 0; i < renderers.Length; i++)
                Assert.That(renderers[i].HasPropertyBlock(), Is.EqualTo(hadProperties[i]),
                    "silhouette control must restore the original block presence: " + renderers[i].name);
            return silhouette;
        }

        private static int[] ProtectedPixels(Color32[] natural, Color32[] silhouette, int region)
        {
            var indices = new List<int>();
            for (int i = 0; i < natural.Length; i++)
            {
                if (silhouette[i].r < 250 || silhouette[i].g < 250 || silhouette[i].b < 250) continue;
                Color c = natural[i];
                bool protect = region == 0 ? c.r > .2f && c.g > .80f * c.r && c.b > .50f * c.r
                    && c.r > c.g && c.g > c.b
                    : region == 1 ? c.maxColorComponent < .35f && c.b > 1.25f * c.r && c.b > 1.1f * c.g
                    : c.r > .2f && c.r > 1.15f * c.g && c.r < 1.65f * c.g
                        && c.b > .74f * c.g && c.b < 1.05f * c.g;
                if (protect) indices.Add(i);
            }
            return indices.ToArray();
        }

        private static int[] WarmCoatPixels(Color32[] natural, Color32[] silhouette)
        {
            var indices = new List<int>();
            for (int i = 0; i < natural.Length; i++)
            {
                if (silhouette[i].r < 250 || silhouette[i].g < 250 || silhouette[i].b < 250) continue;
                Color c = natural[i];
                // Broad gamma-space photograph classifier, not the production linear-space
                // mask arithmetic. Excludes cream, navy and pink; includes orange fur and
                // sufficiently lit dark stripes. Owner reviews the actual debug image too.
                if (c.r > .2f && c.g > .4f * c.r && c.g < .85f * c.r && c.b < .6f * c.g)
                    indices.Add(i);
            }
            return indices.ToArray();
        }

        private static int ChannelDelta(Color32 a, Color32 b) => Mathf.Max(Mathf.Abs(a.r - b.r),
            Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
        private static int CountDifferent(Color32[] a, Color32[] b, int tolerance) =>
            Enumerable.Range(0, a.Length).Count(i => ChannelDelta(a[i], b[i]) > tolerance);
    }
}
