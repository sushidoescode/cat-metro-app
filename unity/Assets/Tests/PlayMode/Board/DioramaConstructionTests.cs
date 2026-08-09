using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class DioramaConstructionTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            var causeRing = GameObject.Find("CauseRing");
            if (causeRing != null) Object.Destroy(causeRing);
            _root = null;
        }

        [UnityTest]
        public IEnumerator RealL001_HasRequiredDioramaRosterAndColliderFreeOwnedTrees()
        {
            _root = GameRoot.Launch();
            yield return null;

            AssertNamed(_root.View.transform, "desk:surface");
            AssertNamed(_root.View.transform, "desk:bevel");
            AssertNamed(_root.View.transform, "depot:SRC");
            AssertNamed(_root.View.transform, "trackbed:E1");
            AssertNamed(_root.View.transform, "rail-left:E1");
            AssertNamed(_root.View.transform, "rail-right:E1");
            AssertNamed(_root.View.transform, "prop:tree");
            AssertNamed(_root.View.transform, "prop:fence");
            AssertNamed(_root.View.transform, "prop:desk-cup");

            Assert.That(_root.View.GetComponentsInChildren<Collider>(true), Is.Empty,
                "BoardView must never create collider-bearing primitive objects");

            _root.CauseCam.FrameNode("SRC", _root.View.NodeWorldPos(0), motionOff: true);
            yield return null;
            var causeRing = GameObject.Find("CauseRing");
            Assert.That(causeRing, Is.Not.Null, "positive control: cause ring was exercised");
            Assert.That(causeRing.GetComponentsInChildren<Collider>(true), Is.Empty,
                "CauseCameraController must construct the ring without a primitive collider");
        }

        [UnityTest]
        public IEnumerator StationsAndLiveCommuter_AreTripleCodedWithVisibleSymbolMeshes()
        {
            _root = GameRoot.Launch();
            yield return null;

            var stations = _root.View.GetComponentsInChildren<LineVisualTag>(true)
                .Where(x => x.Role == LineVisualRole.Station).ToArray();
            Assert.That(stations.Length, Is.EqualTo(2), "L001 has two authored stations");
            foreach (var station in stations) AssertTripleCoded(station);

            _root.Session.AdvanceMs(12 * TickInterpolator.TICK_MS);
            _root.View.UpdateFrom(_root.Session);
            yield return null;
            var commuters = _root.View.GetComponentsInChildren<LineVisualTag>(true)
                .Where(x => x.Role == LineVisualRole.Commuter).ToArray();
            Assert.That(commuters.Length, Is.GreaterThanOrEqualTo(1),
                "positive control: a shipped wave produced a live cat commuter");
            foreach (var commuter in commuters)
            {
                AssertTripleCoded(commuter);
                var ears = AssertNamed(commuter.transform, "cat:ears");
                Assert.That(ears.GetComponentsInChildren<Renderer>(true).Length,
                    Is.EqualTo(4), "outer and contrasting inner ears carry the cat silhouette");
                AssertNamed(commuter.transform, "cat:face");
                AssertNamed(commuter.transform, "contact-shadow");
            }
        }

        [UnityTest]
        public IEnumerator ReusedSimulationSlot_RebuildsTheCommuterForItsNewLineIdentity()
        {
            _root = GameRoot.Launch("content/levels/L002.json");
            yield return null;

            // L002 starts on blue. Route its two red cats home, wait until both slots are
            // released, then route the later blue wave home. The first blue cat reuses slot 0.
            _root.Input.HandleTapAtScreen(
                _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            _root.Session.AdvanceMs(12 * TickInterpolator.TICK_MS);
            _root.View.UpdateFrom(_root.Session);
            Assert.That(AssertNamed(_root.View.transform, "train:0")
                .GetComponent<LineVisualTag>().ColorCode, Is.EqualTo(CatColor.Red),
                "positive control: slot 0 first acquired the red visual identity");
            _root.Session.AdvanceMs(48 * TickInterpolator.TICK_MS);
            _root.View.UpdateFrom(_root.Session);
            _root.Input.HandleTapAtScreen(
                _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            _root.Session.AdvanceMs(25 * TickInterpolator.TICK_MS);
            _root.View.UpdateFrom(_root.Session);
            yield return null;

            var reused = AssertNamed(_root.View.transform, "train:0")
                .GetComponent<LineVisualTag>();
            Assert.That(reused.gameObject.activeSelf, Is.True,
                "positive control: L002's first blue cat is live in reused slot 0");
            Assert.That(reused.ColorCode, Is.EqualTo(CatColor.Blue));
            Assert.That(reused.SymbolId, Is.EqualTo("square"));
            Assert.That(reused.SilhouetteId, Is.EqualTo("slim-siamese"));
            AssertTripleCoded(reused);
        }

        [UnityTest]
        public IEnumerator Camera_IsPitchedThirtyDegreesAndAutoFitsTheBoardMargin()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(
                _root.Cam.transform.eulerAngles.x, 0f)) - 30f), Is.LessThan(0.1f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_root.Cam.transform.eulerAngles.y, 0f)),
                Is.LessThan(0.1f));

            var lowerLeft = _root.Cam.WorldToViewportPoint(new Vector3(-0.5f, -0.5f, 0f));
            var upperRight = _root.Cam.WorldToViewportPoint(new Vector3(6.5f, 10.5f, 0f));
            Assert.That(lowerLeft.z, Is.GreaterThan(0f));
            Assert.That(upperRight.z, Is.GreaterThan(0f));
            Assert.That(lowerLeft.x, Is.InRange(-0.001f, 1.001f));
            Assert.That(lowerLeft.y, Is.InRange(-0.001f, 1.001f));
            Assert.That(upperRight.x, Is.InRange(-0.001f, 1.001f));
            Assert.That(upperRight.y, Is.InRange(-0.001f, 1.001f));
        }

        [UnityTest]
        public IEnumerator GameplayPlatformsTracksAndLever_HaveNoSharpCubeExteriorMesh()
        {
            _root = GameRoot.Launch();
            yield return null;

            var candidates = _root.View.GetComponentsInChildren<MeshFilter>(true)
                .Where(x => x.name == "arm"
                    || x.name.StartsWith("trackbed:")
                    || x.name.StartsWith("rail-")
                    || x.name.StartsWith("tie:")
                    || x.name.StartsWith("station:") &&
                        x.GetComponent<LineSymbolMesh>() == null)
                .ToArray();
            Assert.That(candidates.Length, Is.GreaterThan(10),
                "positive control: gameplay platform/track/lever meshes were inventoried");
            foreach (var candidate in candidates)
                Assert.That(candidate.sharedMesh.name, Is.EqualTo("RoundedBox12"),
                    candidate.name + " must use the 12%-plus beveled box, not a sharp cube/pill");
        }

        [UnityTest]
        public IEnumerator SwitchVisual_PinsTealBaseOrangeArmAndNavyRing()
        {
            _root = GameRoot.Launch();
            yield return null;

            var switchRoot = AssertNamed(_root.View.transform, "switch:S1");
            var baseRenderer = AssertNamed(switchRoot, "lever-base").GetComponent<Renderer>();
            var armRenderer = AssertNamed(_root.View.transform, "arm").GetComponent<Renderer>();
            var ringRenderer = AssertNamed(switchRoot, "lever-keyline").GetComponent<Renderer>();
            Assert.That(Html(baseRenderer.material.color), Is.EqualTo("3BAFA8"));
            Assert.That(Html(armRenderer.material.color), Is.EqualTo("F08A3C"));
            Assert.That(Html(ringRenderer.material.color), Is.EqualTo("22304A"));

            int before = _root.View.CommittedRoute(0);
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            Assert.That(_root.View.CommittedRoute(0), Is.Not.EqualTo(before),
                "the art wrapper must preserve immediate committed-route feedback");
        }

        [UnityTest]
        public IEnumerator RealGameScene_WiresVisiblePolyforkDressingAndShadowlessWarmKey()
        {
            Scene scene = SceneManager.GetSceneByName("Game");
            bool loadedHere = !scene.isLoaded;
            if (loadedHere)
            {
                yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("Game");
            }
            yield return null;

            var roots = scene.GetRootGameObjects();
            var set = roots.Single(x => x.name == "DioramaSet");
            Assert.That(set.transform.childCount, Is.EqualTo(9));
            Assert.That(set.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(9), "positive control: imported meshes are visible");
            Assert.That(set.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (Transform child in set.transform)
                Assert.That(child.name, Does.StartWith("Polyfork_"));

            var key = roots.Single(x => x.name == "WarmKey").GetComponent<Light>();
            Assert.That(key, Is.Not.Null);
            Assert.That(key.type, Is.EqualTo(LightType.Directional));
            Assert.That(key.shadows, Is.EqualTo(LightShadows.None),
                "contact shadows are authored blob geometry, never realtime shadow maps");

            if (loadedHere) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_RealGameDiorama_WhenRequested()
        {
            string directory = System.Environment.GetEnvironmentVariable("CM_ART_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Pass("capture rig disarmed — set CM_ART_CAPTURE_DIR to emit frames");
                yield break;
            }

            Scene scene = SceneManager.GetSceneByName("Game");
            bool loadedHere = !scene.isLoaded;
            if (loadedHere)
            {
                yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName("Game");
            }
            yield return null;
            yield return null;
            _root = scene.GetRootGameObjects().Select(x => x.GetComponent<GameRoot>())
                .First(x => x != null);

            System.IO.Directory.CreateDirectory(directory);
            Capture(_root.Cam, System.IO.Path.Combine(directory, "editor-diorama-board.png"));

            _root.Session.AdvanceMs(12 * TickInterpolator.TICK_MS);
            _root.View.UpdateFrom(_root.Session);
            yield return null;
            Capture(_root.Cam, System.IO.Path.Combine(directory, "editor-diorama-commuter.png"));

            var accessibilityRoots = CaptureAccessibilityEvidence(directory);
            foreach (var evidenceRoot in accessibilityRoots) Object.Destroy(evidenceRoot);
            yield return null;

            if (loadedHere)
            {
                _root = null;
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static void Capture(Camera camera, string path)
        {
            const int width = 900;
            const int height = 2000;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = rt;
            var causeCamera =
                camera.GetComponent<CatMetro.Presentation.Cameras.CauseCameraController>();
            if (causeCamera != null) causeCamera.ApplyDioramaFraming(width / (float)height);
            // Prime the target once: Metal/URP may compile the newly referenced variant on
            // the first manual render, which is not evidence of a presented editor frame.
            camera.Render();
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.Destroy(texture);
            Object.Destroy(rt);
        }

        private GameObject[] CaptureAccessibilityEvidence(string directory)
        {
            var golden = new GameObject("DioramaGoldenFrame");
            golden.transform.SetParent(_root.transform, false);
            var method = typeof(BoardView).GetMethod("BuildCommuter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            byte[] codes =
            {
                CatColor.Red, CatColor.Blue, CatColor.Yellow, CatColor.Green, CatColor.Wild,
            };
            var cats = new List<GameObject>();
            for (int i = 0; i < codes.Length; i++)
            {
                var cat = (GameObject)method.Invoke(_root.View, new object[] { 100 + i, codes[i] });
                cat.name = "golden-cat:" + LineIdentity.For(codes[i]).SilhouetteId;
                cat.transform.SetParent(golden.transform, false);
                cat.transform.localPosition = new Vector3((i - 2) * 1.1f, 0f, 0f);
                cats.Add(cat);
            }
            SetLayer(golden.transform, 31);

            var cameraObject = new GameObject("DioramaGoldenCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.6f;
            camera.transform.position = new Vector3(0f, 0.4f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DioramaPalette.WarmPaper;
            camera.cullingMask = 1 << 31;

            var originals = cats.SelectMany(x => x.GetComponentsInChildren<Renderer>(true))
                .ToDictionary(x => x, x => x.material.color);
            Capture(camera, System.IO.Path.Combine(directory, "golden-frame-five-lines.png"),
                1600, 900);
            CaptureSimulation(camera, originals, "deutan", SimulateDeutan, directory);
            CaptureSimulation(camera, originals, "protan", SimulateProtan, directory);
            CaptureSimulation(camera, originals, "tritan", SimulateTritan, directory);
            CaptureSimulation(camera, originals, "grayscale", SimulateGrayscale, directory);

            foreach (var pair in originals)
            {
                pair.Key.material.color = DioramaPalette.InkNavy;
                if (pair.Key.name == "contact-shadow") pair.Key.gameObject.SetActive(false);
            }
            foreach (var symbol in cats.SelectMany(
                x => x.GetComponentsInChildren<LineSymbolMesh>(true)))
                symbol.gameObject.SetActive(false);
            for (int i = 0; i < cats.Count; i++)
                cats[i].transform.localPosition = new Vector3((i - 2) * 2f, 0f, 0f);
            camera.orthographicSize = 1.2f;
            Capture(camera, System.IO.Path.Combine(directory, "silhouettes-five-at-64px.png"),
                320, 64);

            return new[] { golden, cameraObject };
        }

        private static void CaptureSimulation(
            Camera camera,
            Dictionary<Renderer, Color> originals,
            string name,
            System.Func<Color, Color> transform,
            string directory)
        {
            foreach (var pair in originals) pair.Key.material.color = transform(pair.Value);
            Capture(camera, System.IO.Path.Combine(directory, "golden-frame-" + name + ".png"),
                1600, 900);
        }

        private static Color SimulateDeutan(Color color) => MatrixColor(color,
            0.367322f, 0.860646f, -0.227968f,
            0.280085f, 0.672501f, 0.047413f,
            -0.011820f, 0.042940f, 0.968881f);

        private static Color SimulateProtan(Color color) => MatrixColor(color,
            0.152286f, 1.052583f, -0.204868f,
            0.114503f, 0.786281f, 0.099216f,
            -0.003882f, -0.048116f, 1.051998f);

        private static Color SimulateTritan(Color color) => MatrixColor(color,
            1.255528f, -0.076749f, -0.178779f,
            -0.078411f, 0.930809f, 0.147602f,
            0.004733f, 0.691367f, 0.303900f);

        private static Color SimulateGrayscale(Color color)
        {
            float value = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            return new Color(value, value, value, color.a);
        }

        private static Color MatrixColor(Color color,
            float rr, float rg, float rb,
            float gr, float gg, float gb,
            float br, float bg, float bb)
        {
            return new Color(
                Mathf.Clamp01(rr * color.r + rg * color.g + rb * color.b),
                Mathf.Clamp01(gr * color.r + gg * color.g + gb * color.b),
                Mathf.Clamp01(br * color.r + bg * color.g + bb * color.b), color.a);
        }

        private static void SetLayer(Transform root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private static void Capture(Camera camera, string path, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.Destroy(texture);
            Object.Destroy(rt);
        }

        private static void AssertTripleCoded(LineVisualTag tag)
        {
            LineIdentity expected = LineIdentity.For(tag.ColorCode);
            Assert.That(tag.SymbolId, Is.EqualTo(expected.SymbolId), tag.name);
            Assert.That(tag.SilhouetteId, Is.EqualTo(expected.SilhouetteId), tag.name);
            Assert.That(Html(tag.LineColor), Is.EqualTo(Html(expected.Color)), tag.name);
            var symbols = tag.GetComponentsInChildren<LineSymbolMesh>(true);
            Assert.That(symbols.Length, Is.GreaterThanOrEqualTo(1),
                tag.name + " has line colour but no visible symbol mesh");
            Assert.That(symbols.Any(x => x.SymbolId == expected.SymbolId), Is.True, tag.name);
            Assert.That(symbols.All(x => x.GetComponent<Renderer>() != null), Is.True, tag.name);
        }

        private static Transform AssertNamed(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            Assert.Fail("required diorama object missing: " + name);
            return null;
        }

        private static string Html(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
