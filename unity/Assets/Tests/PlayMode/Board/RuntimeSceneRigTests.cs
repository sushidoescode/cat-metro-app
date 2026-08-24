using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Props;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.PlayMode
{
    public sealed class RuntimeSceneRigTests
    {
        private const float PhoneAspect = 917f / 2048f;
        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _owned)
                if (go != null) Object.DestroyImmediate(go);
            _owned.Clear();
            var staleRing = GameObject.Find("CauseRing");
            if (staleRing != null) Object.DestroyImmediate(staleRing);
        }

        [Test]
        public void GameRoot_UsesLowIsometricWarmRig_AndFramesWidePropLayout()
        {
            var root = GameRoot.LaunchWith(ImportLevel("L008"));
            _owned.Add(root.gameObject);
            var camera = root.Cam;
            camera.aspect = PhoneAspect;

            Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.identity),
                Is.LessThan(0.01f),
                "the axis-aligned camera preserves input, preview, and cause-frame geometry");
            Assert.That(Quaternion.Angle(root.View.transform.rotation, Quaternion.identity),
                Is.GreaterThan(20f),
                "the complete board is tilted as one low-isometric presentation space");

            var lights = root.GetComponentsInChildren<Light>(true);
            var keys = lights.Where(x => x.name == "Diorama Warm Key").ToArray();
            Assert.That(lights.Length, Is.EqualTo(1),
                "the fill is ambient, never a second per-object light");
            Assert.That(keys.Length, Is.EqualTo(1), "one idempotent scene key");
            var key = keys.Single();
            Assert.That(key.type, Is.EqualTo(LightType.Directional));
            Assert.That(key.color.r, Is.GreaterThan(key.color.b));
            Assert.That(key.shadows, Is.EqualTo(LightShadows.Soft),
                "the body needs soft contact shadows to sit on the desk");
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(UnityEngine.Rendering.AmbientMode.Trilight),
                "a restrained sky/equator/ground fill keeps navy readable without another light");
            Assert.That(GreyboxMaterial.Shared.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"),
                "the key cannot model the board while the shared presentation material is unlit");

            foreach (var node in root.View.GetComponentsInChildren<BoardElementId>(true)
                         .Where(x => x.Kind == "node" || x.Kind == "source"
                             || x.Kind == "station"))
                AssertInside(camera, node.transform.position, node.Id);

            var props = root.View.GetComponentsInChildren<BoardPropInstance>(true);
            var localCatalog = PropModelCatalog.LoadResources();
            if (localCatalog.AdmittedEntryCount == 5)
                Assert.That(props.Length,
                    Is.EqualTo(root.Session.Level.Dto.Stations.Length
                        + root.Session.Level.Dto.Sources.Length + 3),
                    "the licensed local install exercises the full wide prop layout");
            else Assert.That(props.Length, Is.Zero,
                "a licence-neutral checkout uses the primitive fallback atomically");
            foreach (var prop in props)
                foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled) AssertBoundsInside(camera, renderer.bounds, prop.AssetId);

            var deskSurface = root.View.transform.Find("DeskSurface");
            foreach (var renderer in root.View.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled && (deskSurface == null
                        || !renderer.transform.IsChildOf(deskSurface)))
                    AssertBoundsInsideShadowDistance(camera, renderer.bounds, renderer.name);
        }

        [Test]
        public void CauseFrameAndRetry_PreserveTheObliqueRestRig()
        {
            var root = GameRoot.LaunchWith(ImportLevel("L001"));
            _owned.Add(root.gameObject);
            root.Cam.aspect = PhoneAspect;
            Vector3 restPosition = root.Cam.transform.position;
            Quaternion restRotation = root.Cam.transform.rotation;
            float restSize = root.Cam.orthographicSize;

            Vector3 node = root.View.NodeWorldPos(0);
            root.CauseCam.FrameNode("SRC", node, motionOff: true);
            Vector3 framed = root.Cam.WorldToViewportPoint(node);
            Assert.That(framed.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(framed.y, Is.EqualTo(0.5f).Within(0.01f));
            var ring = GameObject.Find("CauseRing");
            Assert.That(ring, Is.Not.Null);
            Assert.That(ring.GetComponentsInChildren<Collider>(true), Is.Empty,
                "failure framing must not request stripped collider classes on Android");
            Assert.That(ring.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            var ringRenderer = ring.GetComponent<Renderer>();
            Assert.That(ringRenderer.shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(ringRenderer.receiveShadows, Is.False,
                "the failure overlay must not participate in the diorama lighting rig");
            Assert.That(Vector3.Angle(ring.transform.up, -root.View.transform.forward),
                Is.LessThan(0.1f), "the cause ring lies on the tilted board, not world XY");

            root.Retry();
            Assert.That(root.Cam.transform.position, Is.EqualTo(restPosition).Within(0.01f));
            Assert.That(Quaternion.Angle(root.Cam.transform.rotation, restRotation),
                Is.LessThan(0.01f));
            Assert.That(root.Cam.orthographicSize, Is.EqualTo(restSize).Within(0.01f));
        }

        [Test]
        public void LoadNext_RefitsAndRecapturesPose_WithoutDuplicatingTheKey()
        {
            var root = GameRoot.LaunchWith(ImportLevel("L007"));
            _owned.Add(root.gameObject);
            root.Cam.aspect = PhoneAspect;
            root.Cam.transform.position = new Vector3(99f, -80f, -10f);
            root.Cam.orthographicSize = 1f;

            root.LoadNext();
            Assert.That(root.CurrentLevelId, Is.EqualTo("L008"));
            Assert.That(root.Cam.transform.position.x, Is.LessThan(20f),
                "the new renderer bounds replace the deliberately corrupted old pose");
            Vector3 l008Position = root.Cam.transform.position;
            float l008Size = root.Cam.orthographicSize;
            Assert.That(root.GetComponentsInChildren<Light>(true)
                .Count(x => x.name == "Diorama Warm Key"), Is.EqualTo(1));

            root.CauseCam.FrameNode("cause", root.View.NodeWorldPos(0), motionOff: true);
            root.Retry();
            Assert.That(root.Cam.transform.position, Is.EqualTo(l008Position).Within(0.01f));
            Assert.That(root.Cam.orthographicSize, Is.EqualTo(l008Size).Within(0.01f));
            Assert.That(root.GetComponentsInChildren<Light>(true)
                .Count(x => x.name == "Diorama Warm Key"), Is.EqualTo(1),
                "Retry and LoadNext reuse the scene key");
        }

        private static void AssertBoundsInside(Camera camera, Bounds bounds, string label)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int mask = 0; mask < 8; mask++)
                AssertInside(camera, new Vector3(
                    (mask & 1) == 0 ? min.x : max.x,
                    (mask & 2) == 0 ? min.y : max.y,
                    (mask & 4) == 0 ? min.z : max.z), label);
        }

        private static void AssertBoundsInsideShadowDistance(Camera camera, Bounds bounds,
            string label)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int mask = 0; mask < 8; mask++)
            {
                var corner = new Vector3(
                    (mask & 1) == 0 ? min.x : max.x,
                    (mask & 2) == 0 ? min.y : max.y,
                    (mask & 4) == 0 ? min.z : max.z);
                float depth = Vector3.Dot(corner - camera.transform.position,
                    camera.transform.forward);
                Assert.That(depth, Is.InRange(camera.nearClipPlane, 24f),
                    label + " outside the 25-unit URP main-light shadow range");
            }
        }

        private static void AssertInside(Camera camera, Vector3 world, string label)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            Assert.That(viewport.z, Is.GreaterThan(0f), label + " behind camera");
            Assert.That(viewport.x, Is.InRange(0.055f, 0.945f),
                label + " outside the portrait horizontal safe frame");
            Assert.That(viewport.y, Is.InRange(0.12f, 0.87f),
                label + " outside the portrait vertical safe frame");
        }

        private static ImportedLevel ImportLevel(string id)
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels", id + ".json");
            var imported = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(imported.Ok, Is.True,
                imported.Ok ? string.Empty : imported.Error.ToString());
            return imported.Value;
        }
    }
}
