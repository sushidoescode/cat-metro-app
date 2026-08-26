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
            // The law is split. A prop that stands in for a board element the player has to
            // read and act on keeps the gameplay band; scenery gets the wider decorative one.
            // See PropRole, and SafeFrameLaw_SplitsGameplayFromDecorativeWithTeeth below for
            // the proof that the gameplay half still bites.
            foreach (var prop in props)
                foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled) continue;
                    if (prop.IsDecorative)
                        AssertBoundsInsideDecorativeBand(camera, renderer.bounds, prop.AssetId);
                    else AssertBoundsInside(camera, renderer.bounds, prop.AssetId);
                }

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

        [Test]
        public void SafeFrameLaw_SplitsGameplayFromDecorativeWithTeeth()
        {
            var root = GameRoot.LaunchWith(ImportLevel("L008"));
            _owned.Add(root.gameObject);
            var camera = root.Cam;
            camera.aspect = PhoneAspect;

            // 1. The gameplay half is unchanged and still bites. A station that leaves the
            //    band must fail, and this proves it against a REAL station's real position
            //    rather than a hypothetical. The push is a FULL frame width, not half: a
            //    station already sitting at the left of the band would still be inside it
            //    after half a frame, and the test would pass by accident on some levels and
            //    fail on others depending on which station came back first.
            var stations = root.View.GetComponentsInChildren<BoardElementId>(true)
                .Where(x => x.Kind == "station").ToArray();
            Assert.That(stations, Is.Not.Empty, "L008 is the wide-prop level and has stations");
            foreach (var station in stations)
                AssertInside(camera, station.transform.position, station.Id);

            var probe = stations[0];
            float frameWidth = 2f * camera.orthographicSize * camera.aspect;
            var escaped = probe.transform.position + new Vector3(frameWidth, 0f, 0f);
            Assert.Throws<AssertionException>(
                () => AssertInside(camera, escaped, probe.Id),
                "the gameplay band must still reject a station that leaves the frame — "
                + "widening what may bleed did not widen this");
            var sunk = probe.transform.position
                + new Vector3(0f, 2f * camera.orthographicSize, 0f);
            Assert.Throws<AssertionException>(
                () => AssertInside(camera, sunk, probe.Id),
                "and it must still reject one that leaves vertically");

            // 2. The decorative half is a rule of its own, not the absence of one. It is
            //    wider horizontally on purpose — target-01 runs its scenery off both side
            //    edges — and barely wider vertically, because the top and bottom of the
            //    frame are where the toy's rim has to keep reading as a finite edge.
            Assert.That(DecorativeMaxX, Is.GreaterThan(0.945f),
                "the decorative band has to be wider than the gameplay one or the split "
                + "bought nothing");
            Assert.That(DecorativeMinY, Is.LessThan(0.12f));
            Assert.That(DecorativeMaxY, Is.GreaterThan(0.87f));
            // The shape of the widening, stated so it cannot drift into "decorative means
            // unconstrained". Horizontally the band leaves the FRAME: target-01 runs its
            // trees and fences off both side edges and so may we. Vertically it does not:
            // the top and bottom of the frame are where the toy's rim has to keep reading as
            // a finite edge, which is the whole reason SafeHeight exists.
            Assert.That(DecorativeMinX, Is.LessThan(0f),
                "scenery may bleed off the side edges");
            Assert.That(DecorativeMaxX, Is.GreaterThan(1f));
            Assert.That(DecorativeMinY, Is.GreaterThan(0f),
                "but nothing decorative may leave the frame vertically");
            Assert.That(DecorativeMaxY, Is.LessThan(1f));

            var props = root.View.GetComponentsInChildren<BoardPropInstance>(true);
            if (PropModelCatalog.LoadResources().AdmittedEntryCount != 5)
            {
                Assert.That(props, Is.Empty,
                    "a licence-neutral checkout has no props to classify");
                return;
            }
            Assert.That(props.Any(x => x.IsDecorative), Is.True,
                "the split is only meaningful if this level actually carries scenery");
            Assert.That(props.Any(x => !x.IsDecorative), Is.True,
                "and only honest if it still carries props the gameplay band governs");
            foreach (var prop in props)
                Assert.That(PropRole.IsDecorative(prop.Role) || PropRole.IsGameplay(prop.Role),
                    Is.True, prop.Role + " is on neither side of the split — a role the "
                    + "decorator emits must land in PropRole or it silently gets the "
                    + "gameplay band by default");

            // 3. Every decorative renderer obeys its own band, and the ones that actually
            //    bleed do so sideways.
            foreach (var prop in props.Where(x => x.IsDecorative))
                foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled)
                        AssertBoundsInsideDecorativeBand(camera, renderer.bounds, prop.AssetId);
        }

        [Test]
        public void DecorativePropsLeaveTheWidthFit_ButNeverTheVerticalFit()
        {
            var root = GameRoot.LaunchWith(ImportLevel("L008"));
            _owned.Add(root.gameObject);
            var camera = root.Cam;
            camera.aspect = PhoneAspect;
            if (PropModelCatalog.LoadResources().AdmittedEntryCount != 5) Assert.Ignore(
                "needs the licensed local prop install");

            // The fit solved its size from gameplay alone, so the union of the GAMEPLAY
            // renderers is what fills the horizontal band — the decorative ones are allowed
            // to be wider than it, and on L001 the perimeter trees are exactly that.
            var deskSurface = root.View.transform.Find("DeskSurface");
            var slab = root.View.transform.Find("BoardBody");
            var decorative = root.View.GetComponentsInChildren<BoardPropInstance>(true)
                .Where(x => x.IsDecorative).Select(x => x.transform).ToArray();
            Bounds gameplay = default, everything = default;
            bool foundGameplay = false, foundAll = false;
            foreach (var renderer in root.View.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                if (!foundAll) { everything = renderer.bounds; foundAll = true; }
                else everything.Encapsulate(renderer.bounds);
                if (slab != null && renderer.transform.IsChildOf(slab)) continue;
                if (decorative.Any(d => renderer.transform.IsChildOf(d))) continue;
                if (!foundGameplay) { gameplay = renderer.bounds; foundGameplay = true; }
                else gameplay.Encapsulate(renderer.bounds);
            }
            Assert.That(foundGameplay, Is.True);
            float half = camera.orthographicSize * camera.aspect;
            float used = gameplay.size.x / (2f * half);
            Assert.That(used, Is.InRange(0.80f, 0.945f),
                "the gameplay union should still be filling the horizontal band — if it "
                + "collapses, the fit stopped being content-driven");

            // Vertically nothing changed: the whole diorama, slab included, still has to sit
            // inside the frame so the toy's rim reads as a finite edge top and bottom.
            foreach (var renderer in root.View.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                Vector3 lo = camera.WorldToViewportPoint(renderer.bounds.min);
                Vector3 hi = camera.WorldToViewportPoint(renderer.bounds.max);
                Assert.That(Mathf.Min(lo.y, hi.y), Is.GreaterThan(-0.02f),
                    renderer.name + " fell off the bottom of the frame");
                Assert.That(Mathf.Max(lo.y, hi.y), Is.LessThan(1.02f),
                    renderer.name + " ran off the top of the frame");
            }
        }

        // The decorative band. Horizontally 0.12 of the frame wider than the gameplay one on
        // each side, because target-01 runs its trees and fences off both edges and a tree
        // that decides the size of the whole diorama is the bug this split fixes. Vertically
        // it is only 0.10/0.11 wider, and deliberately so: SafeHeight exists to keep the
        // toy's rim reading as a finite edge, and scenery sailing off the top or bottom would
        // defeat that just as thoroughly as the slab doing it.
        private const float DecorativeMinX = -0.12f;
        private const float DecorativeMaxX = 1.12f;
        private const float DecorativeMinY = 0.02f;
        private const float DecorativeMaxY = 0.98f;

        private static void AssertBoundsInsideDecorativeBand(Camera camera, Bounds bounds,
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
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                Assert.That(viewport.z, Is.GreaterThan(0f), label + " behind camera");
                Assert.That(viewport.x, Is.InRange(DecorativeMinX, DecorativeMaxX),
                    label + " is decorative and may bleed sideways, but not this far");
                Assert.That(viewport.y, Is.InRange(DecorativeMinY, DecorativeMaxY),
                    label + " is decorative and still may not leave the frame vertically");
            }
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
