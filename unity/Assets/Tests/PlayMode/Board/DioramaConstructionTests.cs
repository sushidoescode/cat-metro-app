using System.Collections;
using System.Linq;
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
                AssertNamed(commuter.transform, "cat:ears");
                AssertNamed(commuter.transform, "cat:face");
                AssertNamed(commuter.transform, "contact-shadow");
            }
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
