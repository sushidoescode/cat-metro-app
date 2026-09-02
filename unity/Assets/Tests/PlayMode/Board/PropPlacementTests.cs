using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Props;

namespace CatMetro.Tests.PlayMode
{
    public sealed class PropPlacementTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();
        private readonly List<Material> _ownedMaterials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _owned)
                if (go != null) Object.DestroyImmediate(go);
            _owned.Clear();
            foreach (var material in _ownedMaterials)
                if (material != null) Object.DestroyImmediate(material);
            _ownedMaterials.Clear();
        }

        [Test]
        public void Catalog_AdmitsOnlyKnownRenderOnlyPrefabs()
        {
            var safe = RenderPrefab("safe");
            var unsafePrefab = RenderPrefab("unsafe");
            unsafePrefab.AddComponent<BoxCollider>();

            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, safe),
                Entry(PropModelCatalog.StationKioskId, safe),
                Entry(PropModelCatalog.TreesId, safe),
                Entry(PropModelCatalog.DeskClutterId, safe),
                Entry(PropModelCatalog.ToyEngineId, safe),
                Entry("prop-not-authored", safe),
            });

            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(5));
            Assert.That(catalog.RejectedEntryCount, Is.EqualTo(1));
            Assert.That(catalog.TryGet(PropModelCatalog.DeskClutterId, out var clutter), Is.True);
            Assert.That(clutter.Prefab, Is.SameAs(safe));
            Assert.That(catalog.TryGet("prop-not-authored", out _), Is.False);

            var unsafeCatalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, unsafePrefab),
            });
            Assert.That(unsafeCatalog.AdmittedEntryCount, Is.Zero,
                "component admission—not duplicate ordering—rejects colliders");
            Assert.That(unsafeCatalog.RejectedEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void Catalog_RejectsAtlaslessMaterialBeforeItCanHideTheFallback()
        {
            var atlasless = RenderPrefab("atlasless", bindAtlas: false);
            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.StationKioskId, atlasless),
            });

            Assert.That(catalog.AdmittedEntryCount, Is.Zero);
            Assert.That(catalog.RejectedEntryCount, Is.EqualTo(1));
            Assert.That(catalog.TryGet(PropModelCatalog.StationKioskId, out _), Is.False,
                "a grey/atlasless kiosk must leave the project-owned station visible");
        }

        [Test]
        public void Decorate_L001_AddsBuildingsAndScenery_WithoutChangingAuthoredInventory()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var catalog = CompleteCatalog();
            var authoredBefore = view.GetComponentsInChildren<BoardElementId>(true)
                .Select(x => x.Kind + ":" + x.Id).OrderBy(x => x).ToArray();

            var root = BoardPropDecorator.Decorate(level, view.transform, catalog);

            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.parent, Is.SameAs(view.transform));
            var props = root.GetComponentsInChildren<BoardPropInstance>(true);
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.DepotShedId), Is.EqualTo(1));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.StationKioskId), Is.EqualTo(2));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.TreesId), Is.EqualTo(1));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.DeskClutterId), Is.EqualTo(1));
            Assert.That(props.Count(x => x.AssetId == PropModelCatalog.ToyEngineId), Is.EqualTo(1));
            Assert.That(props.Single(x => x.AssetId == PropModelCatalog.DepotShedId).AnchorId,
                Is.EqualTo("SRC"));
            var stationIds = level.Dto.Stations.ToArray()
                .Select(x => x.NodeId).OrderBy(x => x).ToArray();
            Assert.That(props.Where(x => x.AssetId == PropModelCatalog.StationKioskId)
                .Select(x => x.AnchorId).OrderBy(x => x).ToArray(),
                Is.EqualTo(stationIds));
            Assert.That(props.All(x => x.GetComponent<BoardElementId>() == null), Is.True,
                "decorative art must never enter the authored gameplay inventory");
            Assert.That(view.GetComponentsInChildren<BoardElementId>(true)
                .Select(x => x.Kind + ":" + x.Id).OrderBy(x => x).ToArray(),
                Is.EqualTo(authoredBefore));

            foreach (var prop in props)
            {
                var model = prop.transform.GetChild(0);
                Assert.That(Vector3.Distance(model.localRotation * Vector3.up, Vector3.back),
                    Is.LessThan(0.001f), "FBX Y-up must stand out of the board's XY plane");
            }
        }

        [Test]
        public void PartialCatalog_IsAQuietFallback_AndStationCodingRemainsProjectOwned()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var stations = view.GetComponentsInChildren<BoardElementId>(true)
                .Where(x => x.Kind == "station").OrderBy(x => x.Id).ToArray();
            var codingBefore = stations.Select(StationVisualSignature).ToArray();
            Assert.That(codingBefore[0], Is.Not.EqualTo(codingBefore[1]),
                "precondition: blue and red stations have distinct project-owned coding");
            var sourceVisuals = view.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Kind == "source").GetComponentsInChildren<Renderer>(true);
            Assert.That(sourceVisuals.Any(x => x.enabled), Is.True,
                "precondition: the fallback source visual is visible");
            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, RenderPrefab("depot")),
                Entry(PropModelCatalog.StationKioskId, RenderPrefab("kiosk")),
            });

            Assert.DoesNotThrow(() => BoardPropDecorator.Decorate(level, view.transform, catalog));
            Assert.That(view.GetComponentsInChildren<BoardPropInstance>(true).Length, Is.EqualTo(3));
            Assert.That(stations.Select(StationVisualSignature).ToArray(), Is.EqualTo(codingBefore),
                "neutral kiosks cannot mutate the project's line colors or symbols");
            foreach (var station in stations)
            {
                Assert.That(station.GetComponent<Renderer>().enabled, Is.False,
                    "the fallback cube must not stack through the admitted kiosk");
                var badge = station.transform.Find("station:plate-generated");
                Assert.That(badge, Is.Not.Null,
                    "the project supplies a colored badge; the baked blue sign is never semantic");
                Assert.That(badge.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(badge.localPosition.y, Is.LessThan(-0.5f),
                    "the shape badge sits in front of the kiosk instead of merging into its roof");
                Assert.That(station.transform.Find("station:keyline-generated"), Is.Not.Null,
                    "a cream keyline keeps the badge silhouette distinct from the colored roof");
                Assert.That(station.GetComponentsInChildren<TextMesh>(true)
                        .All(x => x.GetComponent<Renderer>().enabled), Is.True,
                    "the project-owned line symbols stay visible over neutral kiosks");
            }
            Assert.That(stations.Select(x => x.transform.Find("station:plate-generated")
                        .GetComponent<MeshFilter>().sharedMesh.vertexCount).Distinct().Count(),
                Is.EqualTo(2), "red circle and blue square must remain distinct without color");

            var kiosks = view.GetComponentsInChildren<BoardPropInstance>(true)
                .Where(x => x.AssetId == PropModelCatalog.StationKioskId)
                .ToDictionary(x => x.AnchorId);
            foreach (var kiosk in kiosks.Values)
            {
                var woodBase = kiosk.transform.Find("station:wood-base");
                var lineRoof = kiosk.transform.Find("station:line-roof");
                Assert.That(woodBase, Is.Not.Null,
                    "each kiosk stands on a raised project-owned wooden platform");
                Assert.That(lineRoof, Is.Not.Null,
                    "each kiosk has a project-owned line-coloured roof cap");
                Color wood = PropertyColor(woodBase.GetComponent<Renderer>());
                Assert.That(wood.r, Is.GreaterThan(wood.g));
                Assert.That(wood.g, Is.GreaterThan(wood.b));
            }
            string redStationId = level.Dto.Stations.ToArray().Single(x =>
                x.Accepts.ToArray().Contains("red")).NodeId;
            string blueStationId = level.Dto.Stations.ToArray().Single(x =>
                x.Accepts.ToArray().Contains("blue")).NodeId;
            Color redRoof = PropertyColor(kiosks[redStationId].transform
                .Find("station:line-roof").GetComponent<Renderer>());
            Color blueRoof = PropertyColor(kiosks[blueStationId].transform
                .Find("station:line-roof").GetComponent<Renderer>());
            Assert.That(redRoof.r, Is.GreaterThan(redRoof.b), "RED roof carries the red line");
            Assert.That(blueRoof.b, Is.GreaterThan(blueRoof.r), "BLU roof carries the blue line");
            Assert.That(sourceVisuals.All(x => !x.enabled), Is.True,
                "an admitted shed replaces the source placeholder without deleting its gameplay root");
        }

        [Test]
        public void Decorate_PreservesEveryChildOfMultipartScenery()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var sevenPartClutter = MultipartRenderPrefab("seven-part-clutter", 7);
            var catalog = new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DeskClutterId, sevenPartClutter),
            });

            var root = BoardPropDecorator.Decorate(level, view.transform, catalog);
            var clutter = root.GetComponentsInChildren<BoardPropInstance>(true).Single();

            Assert.That(clutter.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(7),
                "props keep all authored parts; there is no largest-component cleanup");
        }

        [Test]
        public void Decorate_UsesFinalAnchorSpace_WhenSceneLaneNestsNodeVisuals()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            var red = view.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Id == "RED" && x.Kind == "station");
            var visualLayout = new GameObject("Final node layout").transform;
            visualLayout.SetParent(view.transform, false);
            visualLayout.localPosition = new Vector3(1.2f, -0.7f, 0f);
            visualLayout.localRotation = Quaternion.Euler(0f, 0f, 8f);
            red.transform.SetParent(visualLayout, true);
            var finalAnchor = view.transform.InverseTransformPoint(red.transform.position);
            finalAnchor.z = BoardPropDecorator.ResolveContactPlaneLocalZ(view.transform);

            var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog());
            var redKiosk = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.StationKioskId && x.AnchorId == "RED");

            Assert.That(Vector3.Distance(redKiosk.transform.localPosition,
                    finalAnchor + new Vector3(0f, 0.42f, 0f)),
                Is.LessThan(0.001f),
                "placement follows the scene lane's final visual anchor, not a nested localPosition");
        }

        [Test]
        public void Decorate_UsesTheBoardViewsPresentationPositionSeam()
        {
            var level = ImportLevel("L001");
            var view = BuildBoard(level);
            Vector3 PresentationPosition(string id)
            {
                var node = level.Dto.Nodes.ToArray().Single(x => x.Id == id);
                return new Vector3(node.X, 5f + (node.Y - 5f) * 1.4f, 0.245f);
            }

            var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog(),
                PresentationPosition);
            var sourceDepot = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.DepotShedId);

            Assert.That(Vector3.Distance(sourceDepot.transform.localPosition,
                    PresentationPosition("SRC") + new Vector3(0f, 0.62f, 0f)),
                Is.LessThan(0.001f));
            var trees = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.TreesId);
            Assert.That(trees.transform.localPosition.y, Is.GreaterThan(7f),
                "scenery bounds use presentation positions too, never raw DTO coordinates");
            float presentedMinX = level.Dto.Nodes.ToArray()
                .Min(x => PresentationPosition(x.Id).x);
            Assert.That(trees.transform.localPosition.x,
                Is.GreaterThanOrEqualTo(presentedMinX - 0.2f),
                "tree plinths stay supported inside the raised board rim");
            var clutter = root.GetComponentsInChildren<BoardPropInstance>(true)
                .Single(x => x.AssetId == PropModelCatalog.DeskClutterId);
            Assert.That(root.GetComponentsInChildren<BoardPropInstance>(true)
                    .Where(x => x != clutter)
                    .All(x => Mathf.Abs(x.transform.localPosition.z - 0.245f) < 0.001f),
                Is.True, "board props land on the scene lane's board contact plane");
            Assert.That(clutter.transform.localPosition.z,
                Is.EqualTo(BoardPropDecorator.ResolveDeskContactPlaneLocalZ(view.transform))
                    .Within(0.001f),
                "desk clutter uses its supporting desk plane, not the raised board plane");
            float presentedMinY = level.Dto.Nodes.ToArray()
                .Min(x => PresentationPosition(x.Id).y);
            Assert.That(clutter.transform.localPosition.y,
                Is.LessThanOrEqualTo(presentedMinY - 1.35f),
                "desk clutter stays wholly outside the playable board footprint");
        }

        [Test]
        public void ContactPlane_UsesExplicitMarkerThenDioramaSurfaceFace()
        {
            var board = Own(new GameObject("contact-plane-board"));
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _owned.Add(surface);
            surface.name = BoardPropDecorator.DioramaSurfaceName;
            surface.transform.SetParent(board.transform, false);
            surface.transform.localPosition = new Vector3(3f, 6.665f, 0.43f);
            surface.transform.localScale = new Vector3(6.55f, 20.93f, 0.34f);

            Assert.That(BoardPropDecorator.ResolveContactPlaneLocalZ(board.transform),
                Is.EqualTo(0.26f).Within(0.001f),
                "without a marker, use the camera-facing mesh face of the diorama surface");

            var marker = new GameObject(BoardPropDecorator.ContactPlaneMarkerName);
            _owned.Add(marker);
            marker.transform.SetParent(board.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0f, 0.245f);

            Assert.That(BoardPropDecorator.ResolveContactPlaneLocalZ(board.transform),
                Is.EqualTo(0.245f).Within(0.001f),
                "the scene lane can publish its exact contact plane explicitly");
        }

        [Test]
        public void BoardViewBuild_WithInjectedCatalog_AlwaysRunsTheIntegrationHook()
        {
            var level = ImportLevel("L001");
            var host = Own(new GameObject("hook-board-host"));
            var view = BoardView.Build(level, host.transform, new GameSession(level),
                CompleteCatalog());

            var props = view.GetComponentsInChildren<BoardPropInstance>(true);
            Assert.That(props.Length, Is.EqualTo(6));
            var marker = view.transform.Find(BoardPropDecorator.ContactPlaneMarkerName);
            Assert.That(marker, Is.Not.Null,
                "the scene lane publishes its tabletop contact plane before decoration");
            var deskMarker = view.transform.Find(BoardPropDecorator.DeskContactPlaneMarkerName);
            Assert.That(deskMarker, Is.Not.Null,
                "the desk dressing has a support plane outside the raised board");
            Assert.That(deskMarker.localPosition.z, Is.GreaterThan(marker.localPosition.z));
            Assert.That(props.Where(x => x.AssetId != PropModelCatalog.DeskClutterId)
                    .All(x => Mathf.Abs(x.transform.localPosition.z - marker.localPosition.z)
                        < 0.001f), Is.True,
                "board prop feet sit on BoardSurface's camera-facing face");
            Assert.That(props.Single(x => x.AssetId == PropModelCatalog.DeskClutterId)
                    .transform.localPosition.z,
                Is.EqualTo(deskMarker.localPosition.z).Within(0.001f));
        }

        [Test]
        public void BoardAndDeskSurfaces_ReuseTheCommittedMaterial()
        {
            var view = BuildBoard(ImportLevel("L001"));
            var boardBody = view.transform.Find("BoardBody");
            var desk = view.transform.Find("DeskSurface");
            Assert.That(boardBody, Is.Not.Null);
            Assert.That(desk, Is.Not.Null);

            var renderers = boardBody.GetComponentsInChildren<Renderer>(true)
                .Concat(desk.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(renderers.All(x => x.sharedMaterial == GreyboxMaterial.Shared), Is.True,
                "Retry/LoadNext must not leak one native Material per slab part");
        }

        [Test]
        public void AllAuthoredLevels_ProduceOneStableScenerySet()
        {
            string levelsRoot = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels");
            var levelPaths = Directory.GetFiles(levelsRoot, "L*.json")
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            Assert.That(levelPaths, Is.Not.Empty,
                "the prop-placement corpus assertion must inspect the shipped level artifact");
            foreach (string path in levelPaths)
            {
                string levelId = Path.GetFileNameWithoutExtension(path);
                var level = ImportLevel(levelId);
                var view = BuildBoard(level);
                var root = BoardPropDecorator.Decorate(level, view.transform, CompleteCatalog());
                var props = root.GetComponentsInChildren<BoardPropInstance>(true);

                Assert.That(props.Length,
                    Is.EqualTo(level.Dto.Stations.Length + level.Dto.Sources.Length + 3),
                    level.Dto.Id + " should have one kiosk per station, one depot per source, "
                    + "and exactly one each of trees, clutter, and parked engine");
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void LocalResources_WhenPresent_AreCompleteUrpPrefabsAndWireThroughBoardView()
        {
            var catalog = PropModelCatalog.LoadResources();
            if (catalog.AdmittedEntryCount == 0)
                Assert.Pass("optional paid prop bytes are absent in this licence-neutral checkout");

            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(5),
                "a local install is atomic: all five props or none");
            Assert.That(catalog.RejectedEntryCount, Is.EqualTo(0));
            foreach (var id in new[]
            {
                PropModelCatalog.DepotShedId,
                PropModelCatalog.StationKioskId,
                PropModelCatalog.TreesId,
                PropModelCatalog.DeskClutterId,
                PropModelCatalog.ToyEngineId,
            })
            {
                Assert.That(catalog.TryGet(id, out var entry), Is.True, id);
                foreach (var renderer in entry.Prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, id + " material");
                    Assert.That(renderer.sharedMaterial.shader.name,
                        Is.EqualTo("Universal Render Pipeline/Lit"), id + " shader");
                    Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null,
                        id + " must bind the external baked atlas explicitly");
                }
            }

            var level = ImportLevel("L001");
            var host = Own(new GameObject("local-resource-board-host"));
            var view = BoardView.Build(level, host.transform, new GameSession(level));
            Assert.That(view.GetComponentsInChildren<BoardPropInstance>(true).Length, Is.EqualTo(6));
        }

        private BoardView BuildBoard(ImportedLevel level)
        {
            var host = Own(new GameObject("board-host"));
            return BoardView.Build(level, host.transform, new GameSession(level),
                PropModelCatalog.Empty);
        }

        private PropModelCatalog CompleteCatalog()
        {
            var prefab = RenderPrefab("complete-catalog-model");
            return new PropModelCatalog(new[]
            {
                Entry(PropModelCatalog.DepotShedId, prefab),
                Entry(PropModelCatalog.StationKioskId, prefab),
                Entry(PropModelCatalog.TreesId, prefab),
                Entry(PropModelCatalog.DeskClutterId, prefab),
                Entry(PropModelCatalog.ToyEngineId, prefab),
            });
        }

        private static PropModelCatalog.Entry Entry(string id, GameObject prefab) =>
            new PropModelCatalog.Entry(id, prefab, 1f, 0f, Vector3.zero);

        private static string StationVisualSignature(BoardElementId station)
        {
            var parts = new List<string>();
            foreach (var renderer in station.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (renderer.gameObject.name == "station:keyline-generated") continue;
                var material = renderer.material;
                parts.Add(renderer.GetType().Name + ":" + material.color);
            }
            foreach (var text in station.GetComponentsInChildren<TextMesh>(true))
                if (text.GetComponent<Renderer>().enabled) parts.Add("text:" + text.text);
            parts.Sort();
            return string.Join("|", parts);
        }

        private static Color PropertyColor(Renderer renderer)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            return properties.GetColor("_BaseColor");
        }

        private GameObject RenderPrefab(string name, bool bindAtlas = true)
        {
            var go = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = TestPropMaterial(bindAtlas);
            return go;
        }

        private GameObject MultipartRenderPrefab(string name, int parts)
        {
            var root = Own(new GameObject(name));
            var material = TestPropMaterial(bindAtlas: true);
            for (int i = 0; i < parts; i++)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = "part-" + i;
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = material;
                part.transform.SetParent(root.transform, false);
            }
            return root;
        }

        private Material TestPropMaterial(bool bindAtlas)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "URP/Lit test precondition");
            var material = new Material(shader);
            material.SetTexture("_BaseMap", bindAtlas ? Texture2D.whiteTexture : null);
            _ownedMaterials.Add(material);
            return material;
        }

        private GameObject Own(GameObject go)
        {
            _owned.Add(go);
            return go;
        }

        private static ImportedLevel ImportLevel(string id)
        {
            var path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels", id + ".json");
            var result = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(result.Ok, Is.True, id + " fixture must import: " + result.Error);
            return result.Value;
        }
    }
}
