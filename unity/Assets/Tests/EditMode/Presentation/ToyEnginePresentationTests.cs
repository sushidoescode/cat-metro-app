using System.Collections.Generic;
using System.Linq;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Props;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class ToyEnginePresentationTests
    {
        private GameObject _host, _prefab;
        private Texture2D _atlas;
        private Material _material;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly int[] _from = { 0, 1, 2 }, _to = { 1, 2, 3 };
        private TrackSplineGraph _paths;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("original-engine-test");
            _host.transform.rotation = BoardSceneLook.BoardTilt;
            _paths = TrackSplineGraph.Build(new[] { Vector3.zero, new Vector3(3, 0, 0),
                new Vector3(3, 3, 0), new Vector3(6, 3, 0) }, _from, _to);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_prefab != null) Object.DestroyImmediate(_prefab);
            if (_material != null) Object.DestroyImmediate(_material);
            if (_atlas != null) Object.DestroyImmediate(_atlas);
            foreach (Mesh mesh in _meshes) if (mesh != null) Object.DestroyImmediate(mesh);
            _meshes.Clear();
        }

        [Test]
        public void MissingOriginalEngineKeepsAllFiveVisiblePrimitivesAndSteamAttachment()
        {
            var view = Create(EngineModelCatalog.Empty);
            Assert.That(view.OriginalEngineAdmitted, Is.False);
            Assert.That(view.EngineFallbackReason, Is.Not.Empty);
            AssertPrimitives(view, true);
            Assert.That(view.transform.Find("Engine/OriginalEngine"), Is.Null);
            Assert.That(view.transform.Find("Engine/Funnel").localPosition,
                Is.EqualTo(new Vector3(.15f, 0f, -.085f)));
        }

        [Test]
        public void OriginalEngineKeepsVehicleSpacingPassengerAndSteamThroughTravelAndReuse()
        {
            BuildFixture();
            var original = Create(new EngineModelCatalog(_prefab));
            var fallback = Create(EngineModelCatalog.Empty);
            Assert.That(original.OriginalEngineAdmitted, Is.True, original.EngineFallbackReason);
            AssertPrimitives(original, false);
            foreach (var step in new[] { (0, 2.8f), (1, .1f), (1, 1.7f), (2, .2f) })
            {
                foreach (var view in new[] { original, fallback })
                {
                    view.SyncSlot(42, 2, DestinationShape.Triangle);
                    view.PlaceOnEdge(_paths, step.Item1, step.Item2);
                    view.ApplyPresentation(CatPresentationState.RideIdle, .7f, false);
                }
                AssertSameAnchors(original, fallback);
            }
            foreach (var view in new[] { original, fallback })
            {
                view.ShowRejection(.7f);
                view.SyncSlot(99, 1, DestinationShape.Square);
                view.PlaceAtNode(_paths, 3, new Vector3(6, 3, 0));
                view.ApplyPresentation(CatPresentationState.RideIdle, 1.2f, true);
            }
            AssertSameAnchors(original, fallback);
            Transform engine = original.transform.Find("Engine");
            Transform model = engine.Find("OriginalEngine");
            Vector3[] vertices = model.GetComponentsInChildren<MeshFilter>().SelectMany(filter =>
                filter.sharedMesh.vertices.Select(v => engine.InverseTransformPoint(filter.transform.TransformPoint(v)))).ToArray();
            // Length, width and height are the ENGINE's own envelope and scale with the consist.
            // The wheel bottom is where it meets the FIXED rails, so it does not; the cab roof
            // height is therefore measured from the rail crown, not from the engine anchor.
            float scale = CatModelCatalog.ConsistScale;
            Assert.That(vertices.Min(v => v.x), Is.GreaterThanOrEqualTo(-.23001f * scale));
            Assert.That(vertices.Max(v => v.x), Is.LessThanOrEqualTo(.23001f * scale));
            Assert.That(vertices.Min(v => v.y), Is.GreaterThanOrEqualTo(-.15001f * scale));
            Assert.That(vertices.Max(v => v.y), Is.LessThanOrEqualTo(.15001f * scale));
            Assert.That(vertices.Min(v => v.z), Is.GreaterThanOrEqualTo(ToyTrainView.RailCrownDepth
                - (ToyTrainView.RailCrownDepth + .13501f) * scale));
            Assert.That(vertices.Max(v => v.z),
                Is.EqualTo(ToyTrainView.RailCrownDepth).Within(.00001f),
                "wheel bottom at the rail crown -- board geometry, unchanged by the consist scale");
            Assert.That(Vector3.Dot(model.right, engine.right), Is.GreaterThan(.99999f));
            Assert.That(original.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(renderer.sharedMaterial, Is.SameAs(_material));
                Assert.That(renderer.HasPropertyBlock(), Is.False, "Passenger line colour must not repaint the engine.");
            }
            original.PrepareDeliveredPassenger(new Vector3(2, 3, -.2f));
            Assert.That(engine.gameObject.activeSelf, Is.False);
            Assert.That(original.transform.Find("Carriage/Cat").gameObject.activeSelf, Is.True);
            Assert.That(_prefab.activeSelf, Is.True);
            Assert.That(_prefab.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [TestCase("missing-part")]
        [TestCase("unbound-atlas")]
        [TestCase("collider")]
        [TestCase("wrong-axis")]
        public void RejectedOriginalEngineKeepsCompleteVisibleFallback(string defect)
        {
            BuildFixture();
            if (defect == "missing-part") Object.DestroyImmediate(_prefab.transform.Find("EngineBody").gameObject);
            if (defect == "unbound-atlas") _material.SetTexture("_BaseMap", null);
            if (defect == "collider") _prefab.AddComponent<BoxCollider>();
            if (defect == "wrong-axis") _prefab.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var view = Create(new EngineModelCatalog(_prefab));
            Assert.That(view.OriginalEngineAdmitted, Is.False, defect);
            Assert.That(view.EngineFallbackReason, Is.Not.Empty);
            AssertPrimitives(view, true);
            Assert.That(view.transform.Find("Engine/OriginalEngine"), Is.Null);
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void DefaultCreationUsesInstalledOriginalMeshesOrCompleteFallback()
        {
            var view = Create(null);
            GameObject resource = Resources.Load<GameObject>(EngineModelCatalog.ResourcePath);
            if (resource == null)
            {
                Assert.That(view.OriginalEngineAdmitted, Is.False);
                AssertPrimitives(view, true);
                return;
            }
            Assert.That(view.OriginalEngineAdmitted, Is.True, view.EngineFallbackReason);
            AssertPrimitives(view, false);
            Assert.That(view.transform.Find("Engine/OriginalEngine").GetComponentsInChildren<MeshFilter>()
                .Select(f => f.sharedMesh), Is.EquivalentTo(resource.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh)));
            Assert.That(resource.GetComponentsInChildren<MeshFilter>().Sum(f => (long)f.sharedMesh.GetIndexCount(0)),
                Is.EqualTo(7488 * 3), "Actual imported original source, not a fixture or a greybox.");
        }

        private ToyTrainView Create(EngineModelCatalog engine) => ToyTrainView.Create(_host.transform,
            "engine-test", _from, _to, new CatModelCatalog(null), CarriageModelCatalog.Empty, engine);

        private static void AssertPrimitives(ToyTrainView view, bool visible)
        {
            foreach (string name in new[] { "Chassis", "Boiler", "Cab", "CabRoof", "Funnel" })
            {
                Transform part = view.transform.Find("Engine/" + name);
                Assert.That(part.gameObject.activeInHierarchy, Is.True, name);
                Assert.That(part.GetComponent<MeshRenderer>().enabled, Is.EqualTo(visible), name);
            }
        }

        private static void AssertSameAnchors(ToyTrainView a, ToyTrainView b)
        {
            foreach (string path in new[] { "", "Engine", "Engine/Funnel", "Carriage", "Carriage/Cat", "Carriage/Pin" })
            {
                Transform ta = path == "" ? a.transform : a.transform.Find(path);
                Transform tb = path == "" ? b.transform : b.transform.Find(path);
                Assert.That(Vector3.Distance(ta.position, tb.position), Is.LessThan(.00001f), path);
                Assert.That(Quaternion.Angle(ta.rotation, tb.rotation), Is.LessThan(.001f), path);
                Assert.That(ta.localScale, Is.EqualTo(tb.localScale), path);
            }
            Assert.That(a.PinShape, Is.EqualTo(b.PinShape));
            Assert.That(a.PresentationOccupantKey, Is.EqualTo(b.PresentationOccupantKey));
            Assert.That(a.CatTint, Is.EqualTo(b.CatTint));
        }

        private void BuildFixture()
        {
            _prefab = new GameObject("Synthetic engine admission fixture");
            _atlas = new Texture2D(1024, 512);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            _material = new Material(shader);
            _material.SetTexture("_BaseMap", _atlas);
            _material.SetColor("_BaseColor", Color.white);
            AddPart("EngineBody", new Vector3(-.003f, .2245f, 0f), new Vector3(.447f, .291f, .296f));
            AddPart("RunningGear", new Vector3(0f, .05425f, 0f), new Vector3(.458f, .1085f, .299f));
        }

        private void AddPart(string name, Vector3 center, Vector3 size)
        {
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var mesh = new Mesh { name = "Synthetic " + name };
            mesh.CombineMeshes(new[] { new CombineInstance { mesh = cube,
                transform = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size.x / cube.bounds.size.x,
                    size.y / cube.bounds.size.y, size.z / cube.bounds.size.z)) } });
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            var part = new GameObject(name);
            part.transform.SetParent(_prefab.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = _material;
        }
    }
}
