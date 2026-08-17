using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    // CM-CATS-WIRE AC1/AC2, phase-1 RED. Tiny in-memory prefabs exercise the future
    // direct-reference seam; no ignored/generated model is needed for this suite to compile.
    public sealed class CatModelWiringTests
    {
        private const string CatalogTypeName =
            "CatMetro.Presentation.Cats.CatModelCatalog";
        private const string InstanceTypeName =
            "CatMetro.Presentation.Cats.CatModelInstance";

        private readonly List<UnityEngine.Object> _owned =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            // Roots and prefab templates first, then the shared mesh/material assets they
            // reference. This order keeps cleanup itself from producing missing-asset noise.
            foreach (var go in _owned.OfType<GameObject>())
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            foreach (var value in _owned.Where(value => !(value is GameObject)))
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            _owned.Clear();
        }

        private static Type RequirePresentationType(string fullName, string diagnostic)
        {
            var type = typeof(BoardView).Assembly.GetType(fullName, false);
            Assert.That(type, Is.Not.Null, "CM-CATS-WIRE expected RED: " + diagnostic);
            return type;
        }

        private static Type RequireCatalogType()
        {
            return RequirePresentationType(CatalogTypeName,
                "CatModelCatalog seam is missing");
        }

        private static Type RequireInstanceType()
        {
            return RequirePresentationType(InstanceTypeName,
                "CatModelInstance read-back seam is missing");
        }

        private GameObject Own(GameObject value)
        {
            _owned.Add(value);
            return value;
        }

        private UnityEngine.Object Own(UnityEngine.Object value)
        {
            _owned.Add(value);
            return value;
        }

        private Component MakeCatalog(GameObject root)
        {
            var go = new GameObject("CatModelCatalog-Test");
            go.transform.SetParent(root.transform, false);
            return go.AddComponent(RequireCatalogType());
        }

        private GameObject MakePrefab(string manifestId)
        {
            var prefab = Own(new GameObject("prefab:" + manifestId));
            var mesh = (Mesh)Own(new Mesh { name = "mesh:" + manifestId });
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            var filter = prefab.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = prefab.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null, "fixture needs a built-in render shader");
            renderer.sharedMaterial = (Material)Own(new Material(shader)
            {
                name = "material:" + manifestId,
            });
            prefab.SetActive(false); // asset-like template; spawned copies must be activated
            return prefab;
        }

        private static void Register(Component catalog, string manifestId,
            GameObject prefab, int triangles = 15000, long sourceBytes = 1000L)
        {
            var method = catalog.GetType().GetMethod("RegisterForTests",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null,
                "CatModelCatalog.RegisterForTests is the in-memory direct-reference test seam");
            method.Invoke(catalog,
                new object[] { manifestId, prefab, triangles, sourceBytes });
        }

        private static T Read<T>(object target, string name)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance;
            var type = target.GetType();
            var property = type.GetProperty(name, flags);
            object value;
            if (property != null) value = property.GetValue(target, null);
            else
            {
                var field = type.GetField(name, flags);
                Assert.That(field, Is.Not.Null,
                    type.FullName + "." + name + " read-back is missing");
                value = field.GetValue(target);
            }
            return (T)value;
        }

        private static Component Marker(GameObject root, Type markerType)
        {
            var matches = root.GetComponentsInChildren<Component>(true)
                .Where(markerType.IsInstanceOfType).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1),
                root.name + " must carry exactly one CatModelInstance marker");
            return matches[0];
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static ImportedLevel ImportBudgetFixture()
        {
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(@"{
  ""schemaVersion"": 2, ""id"": ""T-CATS"", ""name"": ""Cat budget fixture"", ""seed"": 77,
  ""meta"": { ""band"": ""alternation"", ""difficultyTarget"": 0.1,
    ""mechanics"": [""switch""], ""newMechanic"": null,
    ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9, ""queueCapacity"": 8 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 },
      { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 12 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": 12 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 12 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 12 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"",
    ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": [
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"",
      ""count"": 8, ""spacingTicks"": 1 },
    { ""tick"": 40, ""sourceNode"": ""SRC"", ""color"": ""red"",
      ""count"": 8, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 16, ""timeLimitTicks"": 300,
    ""perfectMaxSwitches"": 1, ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}"));
            Assert.That(imported.Ok, Is.True,
                "the cat budget fixture must import: " + imported.Error);
            return imported.Value;
        }

        private static void PutLiveTrains(GameSession session, int count)
        {
            var colors = new[]
            {
                CatColor.Red, CatColor.Blue, CatColor.Yellow,
                CatColor.Green, CatColor.Wild,
            };
            Assert.That(session.State.Trains.Length, Is.GreaterThanOrEqualTo(count),
                "fixture must expose enough bounded train slots");
            for (int i = 0; i < count; i++)
            {
                session.State.Trains[i] = new TrainSlot
                {
                    Id = (short)(i + 1),
                    Color = colors[i % colors.Length],
                    EdgeId = 0,
                    ProgressTicks = 1,
                    NodeId = 0,
                    State = TrainState.OnEdge,
                };
            }
        }

        private static string ExpectedBoardId(byte color)
        {
            if (color == CatColor.Red) return "cat-red-tabby";
            if (color == CatColor.Blue) return "cat-blue-siamese";
            if (color == CatColor.Yellow) return "cat-yellow-longhair";
            if (color == CatColor.Green) return "cat-green-shorthair";
            if (color == CatColor.Wild) return "cat-wild-alley";
            return null;
        }

        private static void AssertSafeModel(GameObject modelRoot)
        {
            Assert.That(modelRoot.GetComponentsInChildren<Collider>(true).Length,
                Is.EqualTo(0), "cat model adds no physics input surface");
            Assert.That(modelRoot.GetComponentsInChildren<Rigidbody>(true).Length,
                Is.EqualTo(0));
            Assert.That(modelRoot.GetComponentsInChildren<Selectable>(true).Length,
                Is.EqualTo(0), "cat model adds no UGUI input surface");
            Assert.That(modelRoot.GetComponentsInChildren<GraphicRaycaster>(true).Length,
                Is.EqualTo(0));
            Assert.That(modelRoot.GetComponentsInChildren<Animator>(true).Length,
                Is.EqualTo(0));
            Assert.That(modelRoot.GetComponentsInChildren<Animation>(true).Length,
                Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Board_MapsLiveCats_CapsAtNine_AndSharesPrefabAssets()
        {
            var root = Own(new GameObject("BoardCats-TestRoot"));
            var catalog = MakeCatalog(root);
            var markerType = RequireInstanceType();
            foreach (var id in new[]
            {
                "cat-red-tabby", "cat-blue-siamese", "cat-yellow-longhair",
                "cat-green-shorthair", "cat-wild-alley",
            })
                Register(catalog, id, MakePrefab(id));

            var level = ImportBudgetFixture();
            var session = new GameSession(level);
            var board = BoardView.Build(level, root.transform, session);
            PutLiveTrains(session, 12);
            board.UpdateFrom(session);
            yield return null;

            var trainRoots = board.GetComponentsInChildren<BoardElementId>(true)
                .Where(element => element.Kind == "train")
                .OrderBy(element => element.Id).ToArray();
            Assert.That(trainRoots.Length, Is.EqualTo(12),
                "all live trains remain represented, including over-budget fallbacks");

            var modelMarkers = new List<Component>();
            var fallbackMarkers = new List<Component>();
            foreach (var trainRoot in trainRoots)
            {
                var marker = Marker(trainRoot.gameObject, markerType);
                bool fallback = Read<bool>(marker, "UsesFallback");
                if (fallback) fallbackMarkers.Add(marker);
                else
                {
                    modelMarkers.Add(marker);
                    int slot = int.Parse(trainRoot.Id.Substring("train-".Length));
                    Assert.That(Read<string>(marker, "ManifestId"),
                        Is.EqualTo(ExpectedBoardId(session.State.Trains[slot].Color)),
                        "the visual map reads CatColor for train slot " + slot);
                    Assert.That(Read<int>(marker, "TriangleCount"),
                        Is.EqualTo(15000));
                    AssertSafeModel(marker.gameObject);
                }
            }

            Assert.That(modelMarkers.Count, Is.EqualTo(9),
                "BoardView's model cap is inclusive at nine");
            Assert.That(fallbackMarkers.Count, Is.EqualTo(3),
                "over-cap live trains remain visible as ordinary capsule fallbacks");
            Assert.That(Read<int>(catalog, "ActiveModelInstanceCount"), Is.EqualTo(9));
            Assert.That(Read<int>(catalog, "ActiveTriangleCount"), Is.EqualTo(135000));
            Assert.That(Read<long>(catalog, "UniqueSourceBytes"), Is.EqualTo(5000L),
                "five registered sources are counted once, not once per instance");

            foreach (var group in modelMarkers.GroupBy(m => Read<string>(m, "ManifestId")))
            {
                var filters = group.Select(m =>
                    m.GetComponentInChildren<MeshFilter>(true)).ToArray();
                var renderers = group.Select(m =>
                    m.GetComponentInChildren<MeshRenderer>(true)).ToArray();
                Assert.That(filters.All(filter => filter != null), Is.True);
                Assert.That(renderers.All(renderer => renderer != null), Is.True);
                Assert.That(filters.Select(filter => filter.sharedMesh).Distinct().Count(),
                    Is.EqualTo(1), group.Key + " instances share one mesh");
                Assert.That(renderers.Select(renderer => renderer.sharedMaterial).Distinct().Count(),
                    Is.EqualTo(1), group.Key + " instances share one material");
            }

        }

        [UnityTest]
        public IEnumerator SafetyOracleControl_RejectsAnImportedCollider()
        {
            var decoy = Own(new GameObject("cat-safety-oracle-decoy"));
            Assert.DoesNotThrow(() => AssertSafeModel(decoy),
                "the render-only control passes the component wall");
            decoy.AddComponent<BoxCollider>();
            Assert.Throws<AssertionException>(() => AssertSafeModel(decoy),
                "the same oracle rejects a physics input surface");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Board_WithNoCatalog_UsesTheCurrentCapsuleQuietly()
        {
            var markerType = RequireInstanceType();
            var root = Own(new GameObject("BoardFallback-TestRoot"));
            var level = ImportBudgetFixture();
            var session = new GameSession(level);
            var board = BoardView.Build(level, root.transform, session);
            PutLiveTrains(session, 1);

            Assert.DoesNotThrow(() => board.UpdateFrom(session),
                "an asset-free clean clone is a normal runtime state");
            yield return null;

            var train = board.GetComponentsInChildren<BoardElementId>(true)
                .Single(element => element.Kind == "train");
            var marker = Marker(train.gameObject, markerType);
            Assert.That(Read<bool>(marker, "UsesFallback"), Is.True);
            Assert.That(Read<string>(marker, "ManifestId"), Is.EqualTo("cat-red-tabby"),
                "fallback still records which closed mapping was unavailable");
            Assert.That(train.GetComponent<Renderer>(), Is.Not.Null,
                "the existing capsule renderer remains present");
            Assert.That(train.transform.localScale, Is.EqualTo(Vector3.one * 0.35f),
                "the ordinary capsule fallback keeps its current scale");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Home_PartialCatalog_ReplacesOnlyResolvedDistricts_AndPreservesPins()
        {
            var root = Own(new GameObject("HomeCats-TestRoot"));
            var catalog = MakeCatalog(root);
            var markerType = RequireInstanceType();
            Register(catalog, "cat-red-tabby-sitting",
                MakePrefab("cat-red-tabby-sitting"));

            var canvasGo = new GameObject("HomeCats-TestCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var regions = new ChromeRegions();
            var home = HomeScreenView.Create(canvas.transform);
            home.Attach(regions, () => false);
            Assert.DoesNotThrow(home.Show,
                "a partial catalog must never become an all-or-nothing Home failure");
            yield return null;

            var a = Find(home.transform, "ParkedDistrictA");
            var b = Find(home.transform, "ParkedDistrictB");
            var c = Find(home.transform, "ParkedDistrictC");
            Assert.That(a, Is.Not.Null); Assert.That(b, Is.Not.Null); Assert.That(c, Is.Not.Null);

            Assert.That(a.GetComponent<Image>().enabled, Is.False,
                "the available cat replaces only A's fallback paint");
            var aMarker = Marker(a.gameObject, markerType);
            Assert.That(Read<bool>(aMarker, "UsesFallback"), Is.False);
            Assert.That(Read<string>(aMarker, "ManifestId"),
                Is.EqualTo("cat-red-tabby-sitting"));
            AssertSafeModel(aMarker.gameObject);

            foreach (var missing in new[] { b, c })
            {
                Assert.That(missing.GetComponent<Image>().enabled, Is.True,
                    missing.name + " keeps its existing silhouette Image");
                var marker = Marker(missing.gameObject, markerType);
                Assert.That(Read<bool>(marker, "UsesFallback"), Is.True);
            }

            Assert.That(home.PinTransform.GetComponent<Image>().enabled, Is.True,
                "the interactive L001 pin is outside this visual replacement");
            Assert.That(home.RingVisible, Is.True);
            Assert.That(regions.Count, Is.EqualTo(1),
                "partial cat art does not add or remove an input region");
            Assert.That(Read<int>(catalog, "ActiveModelInstanceCount"), Is.EqualTo(1));
            Assert.That(Read<int>(catalog, "ActiveTriangleCount"), Is.EqualTo(15000));
            Assert.That(Read<long>(catalog, "UniqueSourceBytes"), Is.EqualTo(1000L));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Home_FullCatalog_UsesTheThreeFrozenModels_WithinItsOwnCap()
        {
            var root = Own(new GameObject("HomeFullCats-TestRoot"));
            var catalog = MakeCatalog(root);
            var markerType = RequireInstanceType();
            var expected = new Dictionary<string, string>
            {
                { "ParkedDistrictA", "cat-red-tabby-sitting" },
                { "ParkedDistrictB", "cat-blue-siamese-loaf" },
                { "ParkedDistrictC", "cat-conductor" },
            };
            foreach (var id in expected.Values)
                Register(catalog, id, MakePrefab(id));

            var canvasGo = new GameObject("HomeFullCats-TestCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var home = HomeScreenView.Create(canvas.transform);
            home.Attach(new ChromeRegions(), () => true);
            home.Show();
            yield return null;

            foreach (var row in expected)
            {
                var slot = Find(home.transform, row.Key);
                Assert.That(slot.GetComponent<Image>().enabled, Is.False);
                var marker = Marker(slot.gameObject, markerType);
                Assert.That(Read<bool>(marker, "UsesFallback"), Is.False);
                Assert.That(Read<string>(marker, "ManifestId"), Is.EqualTo(row.Value));
                AssertSafeModel(marker.gameObject);
            }

            Assert.That(Read<int>(catalog, "ActiveModelInstanceCount"), Is.EqualTo(3));
            Assert.That(Read<int>(catalog, "ActiveTriangleCount"), Is.EqualTo(45000));
            Assert.That(Read<long>(catalog, "UniqueSourceBytes"), Is.EqualTo(3000L));
            Assert.That(home.PinScale, Is.EqualTo(1f),
                "motion-off still pins the excluded L001 affordance at rest");
        }
    }
}
