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
        public IEnumerator LiveCommuter_RidesInOpenCarAtToyTrackScale()
        {
            _root = GameRoot.Launch();
            yield return null;
            _root.Session.AdvanceMs(12 * TickInterpolator.TICK_MS);
            _root.View.UpdateFrom(_root.Session);
            yield return null;

            var commuter = _root.View.GetComponentsInChildren<LineVisualTag>(true)
                .Single(x => x.Role == LineVisualRole.Commuter).transform;
            var floor = AssertNamed(commuter, "train:car-floor");
            var left = AssertNamed(commuter, "train:car-side-left");
            AssertNamed(commuter, "train:car-side-right");
            AssertNamed(commuter, "train:car-end-front");
            AssertNamed(commuter, "train:car-end-back");
            Assert.That(commuter.GetComponentsInChildren<Transform>(true)
                    .Any(x => x.name == "train:car-roof"), Is.False,
                "the reference train is open-top, not a closed carriage");

            Bounds visual = RenderBounds(commuter);
            const float trackWidth = 0.72f;
            Assert.That(visual.size.y, Is.InRange(trackWidth, trackWidth * 1.5f),
                "the complete car/cat toy must be about 1.5 track widths tall");
            Assert.That(visual.size.x, Is.LessThanOrEqualTo(trackWidth * 1.3f),
                "a diagonal car's world AABB stays near one track gauge");

            var head = AssertNamed(commuter, "cat:head").GetComponent<Renderer>();
            var wall = left.GetComponent<Renderer>();
            float carWidth = floor.GetComponent<Renderer>().bounds.size.x;
            Assert.That(head.bounds.size.x, Is.InRange(carWidth * 0.4f, carWidth * 0.8f),
                "the cat is a passenger peeking from the car, never larger than the car");
            Assert.That(head.transform.localPosition.z,
                Is.LessThan(wall.transform.localPosition.z - 0.15f),
                "the cat head must physically protrude toward the camera above the open wall");
            Assert.That(_root.Cam.WorldToViewportPoint(head.bounds.center).y,
                Is.GreaterThan(_root.Cam.WorldToViewportPoint(wall.bounds.center).y),
                "the protruding head must read above the wall in the shipped camera");
            Assert.That(Mathf.Abs(head.transform.localPosition.x), Is.LessThan(0.1f));
            Assert.That(Mathf.Abs(head.transform.localPosition.y), Is.LessThan(0.3f));
            Assert.That(floor.GetComponent<Renderer>(), Is.Not.Null);
            foreach (var featureName in new[] { "eye:left", "eye:right", "nose" })
            {
                var feature = AssertNamed(commuter, featureName).GetComponent<Renderer>();
                Assert.That(feature.bounds.center.z, Is.LessThan(head.bounds.min.z),
                    featureName + " must sit in front of the head surface and remain visible");
            }

            var shadow = AssertNamed(commuter, "contact-shadow");
            Assert.That(shadow.GetComponent<MeshFilter>().sharedMesh.name,
                Is.EqualTo("SoftShadowDisc"));
            Assert.That(shadow.GetComponent<Renderer>().sharedMaterial.color.a,
                Is.InRange(0.08f, 0.35f));
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
        public IEnumerator Camera_UsesLowThreeQuarterFramingAndShowsTheDeskEdge()
        {
            _root = GameRoot.Launch();
            yield return null;
            _root.Cam.aspect = 900f / 2000f;
            _root.CauseCam.ApplyDioramaFraming(_root.Cam.aspect);

            float pitchFromTopDown = Mathf.Abs(Mathf.DeltaAngle(
                _root.Cam.transform.eulerAngles.x, 0f));
            float elevationAboveHorizon = 90f - pitchFromTopDown;
            Assert.That(elevationAboveHorizon, Is.InRange(30f, 40f),
                "the board must read from a low three-quarter tabletop view");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_root.Cam.transform.eulerAngles.y, 0f)),
                Is.LessThan(0.1f));
            Assert.That(_root.Cam.orthographic, Is.True,
                "the owned camera keeps the orthographic gameplay lock, but pitches strongly");

            var lowerLeft = _root.Cam.WorldToViewportPoint(new Vector3(-0.5f, -0.5f, 0f));
            var upperRight = _root.Cam.WorldToViewportPoint(new Vector3(6.5f, 10.5f, 0f));
            Assert.That(lowerLeft.z, Is.GreaterThan(0f));
            Assert.That(upperRight.z, Is.GreaterThan(0f));
            Assert.That(lowerLeft.x, Is.InRange(-0.001f, 1.001f));
            Assert.That(lowerLeft.y, Is.InRange(-0.001f, 1.001f));
            Assert.That(upperRight.x, Is.InRange(-0.001f, 1.001f));
            Assert.That(upperRight.y, Is.InRange(-0.001f, 1.001f));
            Assert.That(lowerLeft.x, Is.LessThan(0.08f), "board fills the portrait width");
            Assert.That(upperRight.x, Is.GreaterThan(0.92f), "board fills the portrait width");

            var sourceAnchor = _root.View.NodeWorldPos(0);
            var redAnchor = _root.View.NodeWorldPos(2);
            Assert.That(AssertNamed(_root.View.transform, "source:SRC").position.y,
                Is.EqualTo(9f).Within(0.001f),
                "the authored node root remains at its exact DTO coordinate");
            Assert.That(sourceAnchor.y - redAnchor.y, Is.GreaterThan(10.5f),
                "visual anchors expand the sparse tutorial topology along the tabletop");
            float occupiedViewportHeight = Mathf.Abs(
                _root.Cam.WorldToViewportPoint(sourceAnchor).y
                - _root.Cam.WorldToViewportPoint(redAnchor).y);
            Assert.That(occupiedViewportHeight, Is.GreaterThan(0.38f),
                "the playable route must occupy the portrait composition, not its middle third");

            var deskEdge = AssertNamed(_root.View.transform, "desk:front-edge");
            var deskViewport = _root.Cam.WorldToViewportPoint(deskEdge.position);
            Assert.That(deskViewport.z, Is.GreaterThan(_root.Cam.nearClipPlane),
                "the foreground desk is physically in front of the virtual tabletop camera");
            Assert.That(GeometryUtility.TestPlanesAABB(
                    GeometryUtility.CalculateFrustumPlanes(_root.Cam),
                    new Bounds(deskEdge.position, Vector3.one * 0.1f)), Is.True,
                "the off-axis tabletop clip retains the near desk apron");
            Assert.That(deskViewport.x, Is.InRange(0f, 1f));
            Assert.That(deskViewport.y, Is.InRange(0.05f, 0.35f),
                "the desk apron belongs visibly at the bottom of the frame");
            Vector3 foregroundPoint = _root.View.transform.TransformPoint(
                new Vector3(3f, -4f, 0.65f));
            var foregroundDesk = _root.Cam.WorldToViewportPoint(foregroundPoint);
            Assert.That(GeometryUtility.TestPlanesAABB(
                    GeometryUtility.CalculateFrustumPlanes(_root.Cam),
                    new Bounds(foregroundPoint, Vector3.one * 0.1f)), Is.True,
                "the virtual tabletop view must retain the foreground desk");
            Assert.That(foregroundDesk.z, Is.GreaterThan(_root.Cam.nearClipPlane));
            Assert.That(foregroundDesk.y, Is.InRange(0f, 0.3f));

            var cup = AssertNamed(_root.View.transform, "prop:desk-cup");
            var cupViewport = _root.Cam.WorldToViewportPoint(cup.position);
            Assert.That(cupViewport.z, Is.GreaterThan(_root.Cam.nearClipPlane));
            Assert.That(GeometryUtility.TestPlanesAABB(
                    GeometryUtility.CalculateFrustumPlanes(_root.Cam),
                    new Bounds(cup.position, Vector3.one * 0.2f)), Is.True,
                "the foreground cup must remain in the shipped frame like the reference");
            Assert.That(cupViewport.x, Is.InRange(0.72f, 1f),
                "the procedural cup stays opposite the imported foreground cup");
            Assert.That(cupViewport.y, Is.InRange(0.04f, 0.22f));
        }

        [UnityTest]
        public IEnumerator Camera_ReappliesAutoFitWhenTheDisplayAspectChanges()
        {
            _root = GameRoot.Launch();
            yield return null;

            var before = _root.Cam.projectionMatrix;
            float rotatedAspect = _root.Cam.aspect < 1f ? 2f : 0.5f;
            _root.Cam.aspect = rotatedAspect;
            yield return null;

            Assert.That(_root.Cam.projectionMatrix.m00,
                Is.Not.EqualTo(before.m00).Within(0.0001f),
                "rotation/multi-window resize must refresh the custom projection");
            var corners = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 10.5f, 0f),
                new Vector3(6.5f, -0.5f, 0f),
                new Vector3(6.5f, 10.5f, 0f),
            };
            foreach (var corner in corners)
            {
                var viewport = _root.Cam.WorldToViewportPoint(corner);
                Assert.That(viewport.z, Is.GreaterThan(0f));
                Assert.That(viewport.x, Is.InRange(-0.001f, 1.001f));
                Assert.That(viewport.y, Is.InRange(-0.001f, 1.001f));
            }
        }

        [UnityTest]
        public IEnumerator GameplayDiorama_HasNoSharpCubeExteriorMesh()
        {
            _root = GameRoot.Launch();
            yield return null;

            var allMeshes = _root.View.GetComponentsInChildren<MeshFilter>(true);
            var candidates = allMeshes
                .Where(x => x.name == "arm"
                    || x.name.StartsWith("trackbed:")
                    || x.name.StartsWith("rail-")
                    || x.name.StartsWith("tie:")
                    || x.name.StartsWith("depot:") && x.name != "depot:shadow"
                    || x.name.StartsWith("ear:")
                    || x.name.StartsWith("whisker:")
                    || x.name.StartsWith("station:") &&
                        x.GetComponent<LineSymbolMesh>() == null)
                .ToArray();
            Assert.That(candidates.Length, Is.GreaterThan(10),
                "positive control: gameplay platform/track/lever meshes were inventoried");
            foreach (var candidate in candidates)
                Assert.That(candidate.sharedMesh.name, Is.EqualTo("RoundedBox12"),
                    candidate.name + " must use the 12%-plus beveled box, not a sharp cube/pill");
            Assert.That(allMeshes.Where(x => x.sharedMesh != null && x.sharedMesh.name == "Cube"),
                Is.Empty, "the complete procedural diorama must not retain a sharp cube mesh");
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
        public IEnumerator DeskPaletteAndSoftContactShadows_MatchTheGoldenHierarchy()
        {
            _root = GameRoot.Launch();
            yield return null;

            var board = _root.View.transform;
            Assert.That(Html(AssertNamed(board, "desk:surface").GetComponent<Renderer>()
                .sharedMaterial.color), Is.EqualTo("FAF6EC"));
            Assert.That(Html(AssertNamed(board, "desk:table").GetComponent<Renderer>()
                .sharedMaterial.color), Is.EqualTo("F2EAD9"));
            AssertNamed(board, "desk:front-edge");
            Assert.That(board.GetComponentsInChildren<Transform>(true)
                .Count(x => x.name.StartsWith("desk:grain-")), Is.GreaterThanOrEqualTo(7),
                "the desktop needs enough restrained inlay to read as wood on a phone");

            var orange = board.GetComponentsInChildren<Renderer>(true)
                .Where(x => Html(x.sharedMaterial.color) == "F08A3C")
                .Select(x => x.name).Distinct().OrderBy(x => x).ToArray();
            Assert.That(orange.All(x => x == "arm" || x == "depot:lintel"), Is.True,
                "Ticket Orange is reserved for switch/station trim, never a large surface");
            var grain = board.GetComponentsInChildren<Renderer>(true)
                .Where(x => x.name.StartsWith("desk:grain-")).ToArray();
            Assert.That(grain.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(grain.All(x => Html(x.sharedMaterial.color) == "22304A"), Is.True,
                "wood grain is low-alpha ink shading; orange stays an accent");
            Assert.That(grain.All(x => x.sharedMaterial.color.a >= 0.18f), Is.True,
                "the palette-safe grain must remain visible at phone scale");
            Assert.That(grain.All(x => x.transform.localScale.y < 5f), Is.True,
                "wood grain stays irregular and segmented, never a full-frame grid line");

            var shadowRoots = board.Cast<Transform>().Where(x =>
                x.name.StartsWith("prop:") || x.name.StartsWith("source:")
                || x.name.StartsWith("station:") || x.name.StartsWith("switch:"))
                .ToArray();
            Assert.That(shadowRoots.Length, Is.GreaterThanOrEqualTo(7));
            foreach (var root in shadowRoots)
            {
                var shadow = root.GetComponentsInChildren<Transform>(true)
                    .SingleOrDefault(x => x.name == "contact-shadow");
                Assert.That(shadow, Is.Not.Null, root.name + " has no authored contact shadow");
                Assert.That(shadow.GetComponent<MeshFilter>().sharedMesh.name,
                    Is.EqualTo("SoftShadowDisc"), root.name);
                Assert.That(shadow.GetComponent<Renderer>().sharedMaterial.color.a,
                    Is.InRange(0.08f, 0.35f), root.name);
            }
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
            var dressingShadows = set.GetComponentsInChildren<Transform>(true)
                .Where(x => x.name == "contact-shadow").ToArray();
            Assert.That(dressingShadows.Length, Is.EqualTo(9),
                "every visible Polyfork dressing receives its own soft contact shadow");
            foreach (var shadow in dressingShadows)
            {
                Assert.That(shadow.GetComponent<MeshFilter>().sharedMesh.name,
                    Is.EqualTo("SoftShadowDisc"));
                Assert.That(shadow.GetComponent<Renderer>().sharedMaterial.color.a,
                    Is.InRange(0.08f, 0.35f));
            }

            var key = roots.Single(x => x.name == "WarmKey").GetComponent<Light>();
            Assert.That(key, Is.Not.Null);
            Assert.That(key.type, Is.EqualTo(LightType.Directional));
            Assert.That(key.shadows, Is.EqualTo(LightShadows.None),
                "contact shadows are authored blob geometry, never realtime shadow maps");
            float keyElevation = Mathf.Asin(Mathf.Abs(Vector3.Dot(
                key.transform.forward.normalized, Vector3.forward))) * Mathf.Rad2Deg;
            Assert.That(keyElevation, Is.InRange(25f, 40f),
                "the warm key must rake across the tabletop from a low afternoon angle");

            var post = roots.SingleOrDefault(x => x.name == "DioramaPost");
            Assert.That(post, Is.Not.Null, "the real Game scene carries the subtle vignette");
            Assert.That(post.GetComponents<Component>()
                .Any(x => x != null && x.GetType().Name == "Volume"), Is.True);

            var sceneRoot = roots.Select(x => x.GetComponent<GameRoot>())
                .Single(x => x != null);
            var cameraData = sceneRoot.Cam.GetComponents<Component>()
                .SingleOrDefault(x => x != null
                    && x.GetType().Name == "UniversalAdditionalCameraData");
            Assert.That(cameraData, Is.Not.Null,
                "the dynamically constructed gameplay camera needs URP post-processing data");
            var property = cameraData.GetType().GetProperty("renderPostProcessing");
            Assert.That(property, Is.Not.Null);
            Assert.That((bool)property.GetValue(cameraData), Is.True,
                "the dynamically constructed gameplay camera must render the vignette");

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

        private static Bounds RenderBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), root.name);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static string Html(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
