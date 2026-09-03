using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
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
        public void GameRoot_UsesFrontalWarmRig_AndFramesWidePropLayout()
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
                "the complete board is tilted as one presentation space");
            Vector3 boardFar = root.View.transform.TransformDirection(Vector3.up);
            float frontalSkew = Mathf.Abs(Mathf.Atan2(boardFar.x, boardFar.y) * Mathf.Rad2Deg);
            Assert.That(frontalSkew, Is.LessThan(1f),
                $"the curated framing target is frontal; the board's receding axis is "
                + $"skewed {frontalSkew:F1} degrees on screen");

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
                    "the core licensed install exercises the full wide prop layout");
            else if (localCatalog.AdmittedEntryCount == 10)
                Assert.That(props.Length,
                    Is.EqualTo(root.Session.Level.Dto.Stations.Length * 2
                        + root.Session.Level.Dto.Sources.Length + 11),
                    "the furnished licensed install exercises the full wide prop layout");
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
        public void CauseFrameAndRetry_PreserveTheFrontalRestRig()
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
            // The guard and the predicate have to agree on a point that is genuinely in the
            // scene, or the negative cases below would be proving something the scene is not
            // actually checked against.
            AssertInside(camera, probe.transform.position, probe.Id);
            Assert.That(IsInsideGameplayBand(camera, probe.transform.position), Is.True,
                "the predicate and the guard must read the same band");

            float frameWidth = 2f * camera.orthographicSize * camera.aspect;
            var escaped = probe.transform.position + new Vector3(frameWidth, 0f, 0f);
            Assert.That(IsInsideGameplayBand(camera, escaped), Is.False,
                "the gameplay band must still reject a station that leaves the frame — "
                + "widening what may bleed did not widen this");
            var sunk = probe.transform.position
                + new Vector3(0f, 2f * camera.orthographicSize, 0f);
            Assert.That(IsInsideGameplayBand(camera, sunk), Is.False,
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
            int admittedEntries = PropModelCatalog.LoadResources().AdmittedEntryCount;
            if (admittedEntries != 5 && admittedEntries != 10)
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
        public void EveryAuthoredLevel_KeepsEveryGameplayRendererInsideHorizontalBand()
        {
            int inspected = 0;
            bool mutationProved = false;
            foreach (string levelId in AuthoredLevelIds())
            {
                var root = GameRoot.LaunchWith(ImportLevel(levelId));
                try
                {
                    var camera = root.Cam;
                    camera.aspect = PhoneAspect;
                    Renderer[] gameplay = GameplayRenderers(root.View).ToArray();
                    Assert.That(gameplay.Length, Is.GreaterThan(10),
                        levelId + " must expose a non-vacuous rendered gameplay hierarchy");
                    foreach (var renderer in gameplay)
                    {
                        Assert.That(HorizontalBoundsViolation(camera, renderer.bounds), Is.Null,
                            levelId + "/" + renderer.name + " left x="
                            + $"{GameplayMinX:F3}..{GameplayMaxX:F3}");
                        inspected++;
                    }

                    if (!mutationProved)
                    {
                        Renderer probe = gameplay[0];
                        Vector3 original = probe.transform.position;
                        float frameWidth = 2f * camera.orthographicSize * camera.aspect;
                        try
                        {
                            probe.transform.position += new Vector3(frameWidth, 0f, 0f);
                            Assert.That(HorizontalBoundsViolation(camera, probe.bounds), Is.Not.Null,
                                "a one-frame renderer escape must trip the artifact guard");
                            mutationProved = true;
                        }
                        finally
                        {
                            probe.transform.position = original;
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(root.gameObject);
                }
            }
            Assert.That(inspected, Is.GreaterThan(100),
                "anti-vacuity: the corpus sweep must inspect real gameplay renderers");
            Assert.That(mutationProved, Is.True);
        }

        [Test]
        public void TrainPassengerAndPin_StayInsideBand_AtEveryTickAndEighthTickSample()
        {
            bool meshMutationProved = false;
            bool verticalMeshMutationProved = false;
            bool skinMutationProved = false;
            bool verticalSkinMutationProved = false;
            int inspectedSkinPoses = 0;
            int inspectedPlatformEndpoints = 0;
            foreach (string levelId in AuthoredLevelIds())
            {
                var root = GameRoot.LaunchWith(ImportLevel(levelId));
                var bakedSkin = new Mesh { name = "safe-frame-skinned-passenger" };
                try
                {
                    var camera = root.Cam;
                    camera.aspect = PhoneAspect;
                    var graph = root.Session.Level.Graph;
                    Vector3[] nodePositions = root.Session.Level.Dto.Nodes.ToArray()
                        .Select(node => new Vector3(node.X, node.Y, 0f)).ToArray();
                    TrackSplineGraph paths = TrackSplineGraph.Build(nodePositions,
                        graph.EdgeFrom, graph.EdgeTo);

                    AssertSourcePlatformEndpointsInside(camera, root, bakedSkin,
                        paths, nodePositions, ref inspectedSkinPoses,
                        ref inspectedPlatformEndpoints, levelId);
                    AssertRetainedHeadingEnvelopeAtStations(camera, root, bakedSkin,
                        nodePositions, ref inspectedSkinPoses,
                        ref inspectedPlatformEndpoints, levelId);

                    Assert.That(root.Session.Alpha, Is.EqualTo(0d),
                        "a newly launched session starts on a tick boundary");
                    for (int eighth = 0; eighth < 8; eighth++)
                    {
                        if (eighth > 0)
                            root.Session.AdvanceMs(TickInterpolator.TICK_MS / 8d);
                        Assert.That(root.Session.Alpha,
                            Is.EqualTo(eighth / 8d).Within(0.0000001d));
                        for (int edge = 0; edge < graph.EdgeFrom.Length; edge++)
                        {
                            int[] incoming = Enumerable.Range(0, graph.EdgeFrom.Length)
                                .Where(x => graph.EdgeTo[x] == graph.EdgeFrom[edge]).ToArray();
                            foreach (int history in new[] { -1 }.Concat(incoming))
                            {
                                ClearTrainHistory(root);
                                if (history >= 0)
                                {
                                    PutTrainOnEdge(root, history,
                                        graph.EdgeTravelTicks[history] - 1);
                                    PutTrainAtNode(root, graph.EdgeFrom[edge]);
                                }
                                int travel = graph.EdgeTravelTicks[edge];
                                for (int tick = 0; tick < travel; tick++)
                                {
                                    PutTrainOnEdge(root, edge, tick);
                                    if (graph.EdgeTunnel[edge])
                                    {
                                        var hiddenTrain = root.View
                                            .GetComponentsInChildren<BoardElementId>(true)
                                            .Single(x => x.Kind == "train");
                                        Assert.That(hiddenTrain.gameObject.activeInHierarchy,
                                            Is.False,
                                            $"{levelId}/tunnel edge {edge}/tick {tick} must "
                                            + "hide its consist before safe-frame geometry is read");
                                        continue;
                                    }
                                    AssertSeatedTrainInside(camera, root, bakedSkin,
                                        ref inspectedSkinPoses,
                                        $"{levelId}/edge {edge}/tick {tick}+{eighth}/8"
                                        + $"/history {history}");
                                    AssertHitchReleasedSourceEndpointsInside(camera, root,
                                        bakedSkin, paths, nodePositions,
                                        ref inspectedSkinPoses,
                                        ref inspectedPlatformEndpoints,
                                        $"{levelId}/edge {edge}/tick {tick}+{eighth}/8"
                                        + $"/history {history}");
                                }
                            }
                        }
                    }

                    // Node poses do not interpolate, but the carriage retains the edge it
                    // arrived on. Exercise every real arrival independently of the alpha grid.
                    for (int edge = 0; edge < graph.EdgeFrom.Length; edge++)
                    {
                        int[] incoming = Enumerable.Range(0, graph.EdgeFrom.Length)
                            .Where(x => graph.EdgeTo[x] == graph.EdgeFrom[edge]).ToArray();
                        foreach (int history in new[] { -1 }.Concat(incoming))
                        {
                            ClearTrainHistory(root);
                            if (history >= 0)
                            {
                                PutTrainOnEdge(root, history,
                                    graph.EdgeTravelTicks[history] - 1);
                                PutTrainAtNode(root, graph.EdgeFrom[edge]);
                            }
                            PutTrainOnEdge(root, edge, graph.EdgeTravelTicks[edge] - 1);
                            PutTrainAtNode(root, graph.EdgeTo[edge]);
                            string arrivalLabel =
                                $"{levelId}/arrival edge {edge}/history {history}";
                            if (graph.StationNode.Contains(graph.EdgeTo[edge]))
                                AssertTrainAndDepartureInside(camera, root, bakedSkin,
                                    ref inspectedSkinPoses, arrivalLabel);
                            else
                                AssertSeatedTrainInside(camera, root, bakedSkin,
                                    ref inspectedSkinPoses, arrivalLabel);
                        }
                    }

                    // A catch-up frame may park at a node unrelated to the remembered edge.
                    // Exercise each edge's near-terminal remembered heading at every node; the
                    // separate five-degree station envelope above spans the headings between.
                    for (int prior = 0; prior < graph.EdgeFrom.Length; prior++)
                    {
                        for (int node = 0; node < graph.NodeCount; node++)
                        {
                            ClearTrainHistory(root);
                            PutTrainOnEdge(root, prior, graph.EdgeTravelTicks[prior] - 1);
                            PutTrainAtNode(root, node);
                            string catchUpLabel =
                                $"{levelId}/node {node}/prior edge {prior}";
                            if (graph.StationNode.Contains(node))
                                AssertTrainAndDepartureInside(camera, root, bakedSkin,
                                    ref inspectedSkinPoses, catchUpLabel);
                            else
                                AssertSeatedTrainInside(camera, root, bakedSkin,
                                    ref inspectedSkinPoses, catchUpLabel);
                        }
                    }

                    if (!meshMutationProved || !verticalMeshMutationProved)
                    {
                        Transform probe = TrainTransform(root);
                        Vector3 original = probe.position;
                        float frameWidth = 2f * camera.orthographicSize * camera.aspect;
                        float frameHeight = 2f * camera.orthographicSize;
                        try
                        {
                            probe.position = original + new Vector3(frameWidth, 0f, 0f);
                            Assert.That(GameplayBandViolation(camera, probe, bakedSkin,
                                    ref inspectedSkinPoses),
                                Is.Not.Null,
                                "a horizontal train escape must trip the mesh-vertex guard");
                            meshMutationProved = true;
                            probe.position = original + new Vector3(0f, frameHeight, 0f);
                            Assert.That(GameplayBandViolation(camera, probe, bakedSkin,
                                    ref inspectedSkinPoses),
                                Is.Not.Null,
                                "a vertical train escape must trip the mesh-vertex guard");
                            verticalMeshMutationProved = true;
                        }
                        finally
                        {
                            probe.position = original;
                        }
                    }
                    if (!skinMutationProved || !verticalSkinMutationProved)
                    {
                        Transform probe = TrainTransform(root);
                        SkinnedMeshRenderer admittedSkin = probe
                            .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .FirstOrDefault(x => x.enabled && x.gameObject.activeInHierarchy);
                        if (admittedSkin != null)
                        {
                            MeshRenderer[] staticRenderers = probe
                                .GetComponentsInChildren<MeshRenderer>(true);
                            bool[] rendererStates = staticRenderers
                                .Select(x => x.enabled).ToArray();
                            Vector3 original = probe.position;
                            float frameWidth = 2f * camera.orthographicSize * camera.aspect;
                            float frameHeight = 2f * camera.orthographicSize;
                            try
                            {
                                foreach (MeshRenderer renderer in staticRenderers)
                                    renderer.enabled = false;
                                probe.position = original + new Vector3(frameWidth, 0f, 0f);
                                string violation = GameplayBandViolation(camera, probe,
                                    bakedSkin, ref inspectedSkinPoses);
                                Assert.That(violation, Does.Contain("skinned vertex"),
                                    "with every static renderer suppressed, only the admitted "
                                    + "skin can prove its horizontal negative case");
                                skinMutationProved = true;
                                probe.position = original + new Vector3(0f, frameHeight, 0f);
                                violation = GameplayBandViolation(camera, probe,
                                    bakedSkin, ref inspectedSkinPoses);
                                Assert.That(violation, Does.Contain("skinned vertex"),
                                    "with every static renderer suppressed, only the admitted "
                                    + "skin can prove its vertical negative case");
                                verticalSkinMutationProved = true;
                            }
                            finally
                            {
                                probe.position = original;
                                for (int index = 0; index < staticRenderers.Length; index++)
                                    staticRenderers[index].enabled = rendererStates[index];
                            }
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(bakedSkin);
                    Object.DestroyImmediate(root.gameObject);
                }
            }
            Assert.That(meshMutationProved, Is.True);
            Assert.That(verticalMeshMutationProved, Is.True);
            int admittedRigs = CatModelCatalog.LoadResources().AdmittedEntryCount;
            TestContext.Out.WriteLine("SAFE_FRAME_SKIN_READBACK admitted=" + admittedRigs
                + " inspectedPoses=" + inspectedSkinPoses
                + " platformEndpoints=" + inspectedPlatformEndpoints
                + " animationNormalizedTime=0");
            Assert.That(inspectedPlatformEndpoints, Is.GreaterThan(100),
                "the safe-frame sweep must place production waiting and walking source endpoints");
            if (admittedRigs == 1)
            {
                Assert.That(inspectedSkinPoses, Is.GreaterThan(100),
                    "the spatial safe-frame sweep must inspect the visible licensed skin, "
                    + "not only its hidden fallback MeshFilters");
                Assert.That(skinMutationProved, Is.True,
                    "the admitted-skin predicate needs its own isolated negative control");
                Assert.That(verticalSkinMutationProved, Is.True,
                    "the admitted-skin predicate needs a vertical negative control");
            }
        }

        [Test]
        public void DecorativePropsLeaveTheWidthFit_ButNeverTheVerticalFit()
        {
            var root = GameRoot.LaunchWith(ImportLevel("L008"));
            _owned.Add(root.gameObject);
            var camera = root.Cam;
            camera.aspect = PhoneAspect;
            int admittedEntries = PropModelCatalog.LoadResources().AdmittedEntryCount;
            if (admittedEntries != 5 && admittedEntries != 10)
                Assert.Ignore("needs the licensed local prop install");

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

        // The gameplay band, as four named numbers rather than four literals, so the guard
        // below and the predicate beside it cannot drift apart.
        private const float GameplayMinX = 0.055f;
        private const float GameplayMaxX = 0.945f;
        private const float GameplayMinY = 0.12f;
        private const float GameplayMaxY = 0.87f;

        /// <summary>
        /// The same test the guard applies, as a bool, so the NEGATIVE case can be asserted
        /// directly instead of by catching the guard's own AssertionException.
        ///
        /// Catching an assertion here would couple the negative-case proof to NUnit's assertion
        /// side effects. One proposed rationale was that NUnit 3.6+ records an AssertionResult
        /// before throwing and can therefore fail the test even when Assert.Throws catches the
        /// exception. That later-version behaviour was not verified and is not treated as fact.
        ///
        /// What was verified locally is narrower: Unity's inspected nunit.framework.dll reports
        /// version 3.5.0.0, and RecordAssertion, AssertionResults, RecordTestCompletion,
        /// MultipleAssertLevel and AssertionStatus have zero occurrences in it. Control probes
        /// in the same scan — AssertionException, TestExecutionContext, CurrentResult and
        /// SetResult — are present, so the absence is real rather than a failed search. Those
        /// probes establish only the shipped assembly's surface, not any NUnit 3.6+ behaviour.
        /// Independently of NUnit version, the predicate below is version-proof and tests the
        /// same four constants directly.
        /// </summary>
        private static bool IsInsideGameplayBand(Camera camera, Vector3 world)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return viewport.z > 0f
                && viewport.x >= GameplayMinX && viewport.x <= GameplayMaxX
                && viewport.y >= GameplayMinY && viewport.y <= GameplayMaxY;
        }

        private static void AssertInside(Camera camera, Vector3 world, string label)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            Assert.That(viewport.z, Is.GreaterThan(0f), label + " behind camera");
            Assert.That(viewport.x, Is.InRange(GameplayMinX, GameplayMaxX),
                label + " outside the portrait horizontal safe frame");
            Assert.That(viewport.y, Is.InRange(GameplayMinY, GameplayMaxY),
                label + " outside the portrait vertical safe frame");
        }

        private static IEnumerable<Renderer> GameplayRenderers(BoardView board)
        {
            Transform desk = board.transform.Find("DeskSurface");
            Transform slab = board.transform.Find("BoardBody");
            Transform[] decorative = board.GetComponentsInChildren<BoardPropInstance>(true)
                .Where(x => x.IsDecorative).Select(x => x.transform).ToArray();
            foreach (var renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (desk != null && renderer.transform.IsChildOf(desk)) continue;
                if (slab != null && renderer.transform.IsChildOf(slab)) continue;
                if (decorative.Any(x => renderer.transform.IsChildOf(x))) continue;
                yield return renderer;
            }
        }

        private static string HorizontalBoundsViolation(Camera camera, Bounds bounds)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 viewport = camera.WorldToViewportPoint(world);
                if (viewport.z <= 0f || viewport.x < GameplayMinX
                    || viewport.x > GameplayMaxX)
                    return $"bounds corner {corner} projected to "
                        + $"({viewport.x:F4}, {viewport.y:F4}, {viewport.z:F2})";
            }
            return null;
        }

        private static void AssertTrainInside(Camera camera, GameRoot root, Mesh bakedSkin,
            ref int inspectedSkinPoses, string label) =>
            AssertViewInside(camera,
                TrainTransform(root).GetComponent<ToyTrainView>(), bakedSkin,
                ref inspectedSkinPoses, label);

        private static void AssertViewInside(Camera camera, ToyTrainView view, Mesh bakedSkin,
            ref int inspectedSkinPoses, string label) =>
            Assert.That(GameplayBandViolation(camera, view.transform, bakedSkin,
                ref inspectedSkinPoses), Is.Null,
                label + $" left x={GameplayMinX:F3}..{GameplayMaxX:F3}, "
                + $"y={GameplayMinY:F3}..{GameplayMaxY:F3}");

        private static void AssertTrainAndDepartureInside(Camera camera, GameRoot root,
            Mesh bakedSkin, ref int inspectedSkinPoses, string label)
        {
            ToyTrainView view = TrainTransform(root).GetComponent<ToyTrainView>();
            AssertViewAndDepartureInside(camera, view, bakedSkin,
                ref inspectedSkinPoses, label);
        }

        private static void AssertViewAndDepartureInside(Camera camera, ToyTrainView view,
            Mesh bakedSkin, ref int inspectedSkinPoses, string label)
        {
            AssertViewSeatedInside(camera, view, bakedSkin, ref inspectedSkinPoses, label);
            Animator animator = view.GetComponentInChildren<Animator>(true);
            view.ApplyPresentation(CatPresentationState.Walk, 1f, true, 0f, false, 1f);
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Play("Base Layer." + CatModelCatalog.WalkClip, 0, 0f);
                animator.Update(0f);
                view.ApplyPresentation(CatPresentationState.Walk,
                    1f, true, 0f, false, 1f);
            }
            AssertViewInside(camera, view, bakedSkin, ref inspectedSkinPoses,
                label + "/departure-endpoint");
            view.ApplyPresentation(CatPresentationState.Celebrate,
                1f, true, 0f, false, 1f);
            if (animator != null)
            {
                animator.Play("Base Layer." + CatModelCatalog.CelebrateClip, 0, 0f);
                animator.Update(0f);
                view.ApplyPresentation(CatPresentationState.Celebrate,
                    1f, true, 0f, false, 1f);
            }
            AssertViewInside(camera, view, bakedSkin, ref inspectedSkinPoses,
                label + "/celebrate-endpoint");
        }

        private static void AssertSeatedTrainInside(Camera camera, GameRoot root,
            Mesh bakedSkin, ref int inspectedSkinPoses, string label)
        {
            ToyTrainView view = TrainTransform(root).GetComponent<ToyTrainView>();
            AssertViewSeatedInside(camera, view, bakedSkin,
                ref inspectedSkinPoses, label);
        }

        private static void AssertViewSeatedInside(Camera camera, ToyTrainView view,
            Mesh bakedSkin, ref int inspectedSkinPoses, string label)
        {
            Animator animator = view.GetComponentInChildren<Animator>(true);
            view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Play("Base Layer." + CatModelCatalog.IdleSitClip, 0, 0f);
                animator.Update(0f);
                view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);
            }
            AssertViewInside(camera, view, bakedSkin, ref inspectedSkinPoses, label);
        }

        private static void AssertSourcePlatformEndpointsInside(Camera camera, GameRoot root,
            Mesh bakedSkin, TrackSplineGraph paths, Vector3[] nodePositions,
            ref int inspectedSkinPoses,
            ref int inspectedPlatformEndpoints, string levelId)
        {
            var graph = root.Session.Level.Graph;
            foreach (int sourceNode in graph.SourceNodes)
            {
                int outgoingEdge = Enumerable.Range(0, graph.EdgeFrom.Length)
                    .Single(edge => graph.EdgeFrom[edge] == sourceNode);
                Vector3 tangent = paths.Path(outgoingEdge)
                    .TangentDistanceFraction(0f);
                Vector3 side = new Vector3(tangent.y, -tangent.x, 0f);
                // The allocator is global across train slots and may assign every live slot a
                // distinct lane before a delivery frees one. Exercise the complete imported
                // digest bound, not only the largest queue the current wave timings happen to
                // produce.
                int maximumQueuePosition = Mathf.Max(0, graph.TrainsMax - 1);

                PutTrainAtNode(root, sourceNode);
                ToyTrainView view = TrainTransform(root).GetComponent<ToyTrainView>();
                // The first manual observation itself enters Walk at blend=1, so Cat.position
                // is already a platform endpoint. The consist root's XY is BoardView's exact
                // laid-out source node and avoids applying PlatformSideOffset twice.
                Vector3 nodeBoard = view.transform.localPosition;
                Animator animator = view.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                for (int lane = 0; lane <= maximumQueuePosition; lane++)
                {
                    foreach (CatPresentationState state in new[]
                        {
                            CatPresentationState.WaitingIdle,
                            CatPresentationState.Walk,
                        })
                    {
                        view.SetSourcePlatformAnchor(nodeBoard, side, lane);
                        view.ApplyPresentation(state, 1f, false, 0f, false, 1f);
                        if (animator != null)
                        {
                            string clip = state == CatPresentationState.WaitingIdle
                                ? CatModelCatalog.IdleSitClip
                                : CatModelCatalog.WalkClip;
                            animator.Play("Base Layer." + clip, 0, 0f);
                            animator.Update(0f);
                            view.ApplyPresentation(state, 1f, false, 0f, false, 1f);
                        }
                        AssertTrainInside(camera, root, bakedSkin,
                            ref inspectedSkinPoses,
                            $"{levelId}/source {sourceNode}/lane {lane}/{state}");
                        inspectedPlatformEndpoints++;
                    }
                }
            }
        }

        private static void AssertHitchReleasedSourceEndpointsInside(Camera camera,
            GameRoot root, Mesh bakedSkin, TrackSplineGraph paths,
            Vector3[] nodePositions, ref int inspectedSkinPoses,
            ref int inspectedPlatformEndpoints, string poseLabel)
        {
            var graph = root.Session.Level.Graph;
            ToyTrainView view = TrainTransform(root).GetComponent<ToyTrainView>();
            Animator animator = view.GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (int sourceNode in graph.SourceNodes)
            {
                int outgoingEdge = Enumerable.Range(0, graph.EdgeFrom.Length)
                    .Single(edge => graph.EdgeFrom[edge] == sourceNode);
                Vector3 tangent = paths.Path(outgoingEdge)
                    .TangentDistanceFraction(0f);
                Vector3 side = new Vector3(tangent.y, -tangent.x, 0f);
                int maximumQueuePosition = Mathf.Max(0, graph.TrainsMax - 1);
                for (int lane = 0; lane <= maximumQueuePosition; lane++)
                {
                    view.SetSourcePlatformAnchor(nodePositions[sourceNode], side, lane);
                    view.ApplyPresentation(CatPresentationState.Walk,
                        1f, false, 0f, false, 1f);
                    if (animator != null)
                    {
                        animator.Play("Base Layer." + CatModelCatalog.WalkClip, 0, 0f);
                        animator.Update(0f);
                        view.ApplyPresentation(CatPresentationState.Walk,
                            1f, false, 0f, false, 1f);
                    }
                    AssertTrainInside(camera, root, bakedSkin, ref inspectedSkinPoses,
                        poseLabel + $"/hitch-source {sourceNode}/lane {lane}/Walk");
                    inspectedPlatformEndpoints++;
                }
            }
        }

        private static void AssertRetainedHeadingEnvelopeAtStations(Camera camera,
            GameRoot root, Mesh bakedSkin, Vector3[] nodePositions,
            ref int inspectedSkinPoses,
            ref int inspectedPlatformEndpoints, string levelId)
        {
            var graph = root.Session.Level.Graph;
            var probe = ToyTrainView.Create(root.View.transform,
                "safe-frame-retained-heading-probe", new[] { 0 }, new[] { 1 });
            long occupantKey = 0L;
            try
            {
                foreach (int stationNode in graph.StationNode.Distinct())
                {
                    Vector3 station = new Vector3(
                        nodePositions[stationNode].x, nodePositions[stationNode].y, 0f);
                    for (int heading = 0; heading < 360; heading += 5)
                    {
                        Vector3 direction = Quaternion.Euler(0f, 0f, heading)
                            * Vector3.right * 2f;
                        TrackSplineGraph paths = TrackSplineGraph.Build(
                            new[] { Vector3.zero, direction },
                            new[] { 0 }, new[] { 1 });
                        probe.SyncSlot(++occupantKey, CatColor.Red);
                        probe.PlaceOnEdge(paths, 0, paths.Path(0).Length);
                        // This is ToyTrainView's production foreign-node clamp: the consist
                        // parks at the real station while retaining its last rendered heading.
                        probe.PlaceAtNode(paths, 0, station);
                        AssertViewAndDepartureInside(camera, probe, bakedSkin,
                            ref inspectedSkinPoses,
                            $"{levelId}/station {stationNode}/retained heading {heading}");
                        inspectedPlatformEndpoints += 2;
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(probe.gameObject);
            }
        }

        private static void PutTrainOnEdge(GameRoot root, int edge, int tick)
        {
            ClearOtherTrainSlots(root);
            root.Session.State.Trains[0] = new TrainSlot
            {
                Id = 1,
                Color = CatColor.Red,
                EdgeId = (short)edge,
                ProgressTicks = (short)tick,
                NodeId = (short)root.Session.Level.Graph.EdgeFrom[edge],
                State = TrainState.OnEdge,
            };
            root.View.UpdateFrom(root.Session);
        }

        private static void PutTrainAtNode(GameRoot root, int node)
        {
            ClearOtherTrainSlots(root);
            root.Session.State.Trains[0] = new TrainSlot
            {
                Id = 1,
                Color = CatColor.Red,
                NodeId = (short)node,
                State = TrainState.AtNode,
            };
            root.View.UpdateFrom(root.Session);
        }

        private static void ClearTrainHistory(GameRoot root)
        {
            var graph = root.Session.Level.Graph;
            Assert.That(graph.EdgeFrom.Length, Is.GreaterThan(0));
            Assert.That(graph.NodeCount, Is.GreaterThan(1),
                "the corpus needs a foreign node to exercise the no-history train pose");
            PutTrainOnEdge(root, 0, graph.EdgeTravelTicks[0] - 1);
            int foreignNode = (graph.EdgeTo[0] + 1) % graph.NodeCount;
            PutTrainAtNode(root, foreignNode);
        }

        private static void ClearOtherTrainSlots(GameRoot root)
        {
            for (int i = 1; i < root.Session.State.Trains.Length; i++)
                root.Session.State.Trains[i] = default;
        }

        private static Transform TrainTransform(GameRoot root) =>
            root.View.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Kind == "train" && x.gameObject.activeInHierarchy).transform;

        private static string GameplayBandViolation(Camera camera, Transform train,
            Mesh bakedSkin, ref int inspectedSkinPoses)
        {
            string worstViolation = null;
            float worstOverflow = 0f;
            foreach (var filter in train.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (!filter.gameObject.activeInHierarchy || filter.sharedMesh == null
                    || renderer == null || !renderer.enabled) continue;
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(
                        filter.transform.TransformPoint(vertices[i]));
                    if (viewport.z <= 0f)
                        return $"{filter.name} vertex {i} projected behind camera at "
                            + $"({viewport.x:F4}, {viewport.y:F4}, {viewport.z:F2})";
                    float horizontalOverflow = viewport.x < GameplayMinX
                        ? GameplayMinX - viewport.x
                        : viewport.x > GameplayMaxX ? viewport.x - GameplayMaxX : 0f;
                    float verticalOverflow = viewport.y < GameplayMinY
                        ? GameplayMinY - viewport.y
                        : viewport.y > GameplayMaxY ? viewport.y - GameplayMaxY : 0f;
                    float overflow = Mathf.Max(horizontalOverflow, verticalOverflow);
                    if (overflow > worstOverflow)
                    {
                        worstOverflow = overflow;
                        worstViolation = $"{filter.name} vertex {i} projected to "
                            + $"({viewport.x:F4}, {viewport.y:F4}, {viewport.z:F2})";
                    }
                }
            }
            foreach (var skin in train.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.gameObject.activeInHierarchy || !skin.enabled
                    || skin.sharedMesh == null) continue;
                inspectedSkinPoses++;
                skin.BakeMesh(bakedSkin, true);
                Vector3[] vertices = bakedSkin.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(
                        skin.transform.TransformPoint(vertices[i]));
                    if (viewport.z <= 0f)
                        return $"{skin.name} skinned vertex {i} projected behind camera at "
                            + $"({viewport.x:F4}, {viewport.y:F4}, {viewport.z:F2})";
                    float horizontalOverflow = viewport.x < GameplayMinX
                        ? GameplayMinX - viewport.x
                        : viewport.x > GameplayMaxX ? viewport.x - GameplayMaxX : 0f;
                    float verticalOverflow = viewport.y < GameplayMinY
                        ? GameplayMinY - viewport.y
                        : viewport.y > GameplayMaxY ? viewport.y - GameplayMaxY : 0f;
                    float overflow = Mathf.Max(horizontalOverflow, verticalOverflow);
                    if (overflow > worstOverflow)
                    {
                        worstOverflow = overflow;
                        worstViolation = $"{skin.name} skinned vertex {i} projected to "
                            + $"({viewport.x:F4}, {viewport.y:F4}, {viewport.z:F2})";
                    }
                }
            }
            return worstViolation;
        }

        private static string[] AuthoredLevelIds()
        {
            string staged = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels");
            string authored = Path.Combine(UnityEngine.Application.dataPath,
                "..", "..", "content", "levels");
            Assert.That(Directory.Exists(staged), Is.True,
                "the staged level corpus must exist: " + staged);
            Assert.That(Directory.Exists(authored), Is.True,
                "the authored level corpus must exist: " + authored);
            string[] stagedIds = LevelIdsIn(staged);
            string[] authoredIds = LevelIdsIn(authored);
            Assert.That(stagedIds.Length, Is.GreaterThanOrEqualTo(17),
                "anti-vacuity: this project had at least 17 authored levels");
            Assert.That(stagedIds, Is.EqualTo(authoredIds),
                "staged levels drifted from the authored corpus");
            foreach (string id in stagedIds)
            {
                byte[] stagedBytes = File.ReadAllBytes(Path.Combine(staged, id + ".json"));
                byte[] authoredBytes = File.ReadAllBytes(Path.Combine(authored, id + ".json"));
                Assert.That(stagedBytes, Is.EqualTo(authoredBytes),
                    id + " differs between staged and authored level content");
            }
            return stagedIds;
        }

        private static string[] LevelIdsIn(string directory) =>
            Directory.GetFiles(directory, "L*.json")
                .Where(x => Path.GetExtension(x) == ".json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(x => x, System.StringComparer.Ordinal)
                .ToArray();

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
