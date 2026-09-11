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
    public sealed class ToyCarriagePresentationTests
    {
        private GameObject _host, _prefab;
        private Material _material;
        private Texture2D _atlas;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private TrackSplineGraph _paths;
        private readonly int[] _from = { 0, 1, 2 }, _to = { 1, 2, 3 };

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("carriage-comparison-host");
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
        public void MissingCarriageResource_KeepsVisibleFallbackAndFunctionalPassenger()
        {
            var view = Create(CarriageModelCatalog.Empty);
            view.SyncSlot(42, 2, DestinationShape.Triangle);
            view.PlaceOnEdge(_paths, 0, 1.5f);
            view.ApplyPresentation(CatPresentationState.RideIdle, .7f, true);
            Assert.That(view.OriginalCarriageAdmitted, Is.False);
            Assert.That(view.CarriageFallbackReason, Is.Not.Empty);
            Transform carriage = view.transform.Find("Carriage");
            foreach (string name in new[] { "Body", "Chassis" })
            {
                var renderer = carriage.Find(name).GetComponent<MeshRenderer>();
                Assert.That(renderer.enabled && renderer.gameObject.activeInHierarchy, Is.True);
            }
            Assert.That(carriage.Find("OriginalCarriage"), Is.Null);
            Assert.That(carriage.Find("Cat").gameObject.activeSelf, Is.True);
            Assert.That(carriage.Find("Pin").gameObject.activeSelf, Is.True);
            Assert.That(view.PinShape, Is.EqualTo(DestinationShape.Triangle));
            Assert.That(view.transform.localPosition.z, Is.EqualTo(-.2f).Within(.00001f));
        }

        [Test]
        public void OriginalCarriage_PreservesCurvedTravelPassengerPinAndSlotReuse()
        {
            BuildFixture();
            var catalog = new CarriageModelCatalog(_prefab);
            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1), catalog.RejectionReason);
            var fallback = Create(CarriageModelCatalog.Empty);
            var original = Create(catalog);
            Assert.That(original.OriginalCarriageAdmitted, Is.True, original.CarriageFallbackReason);
            Assert.That(original.transform.Find("Carriage/Body"), Is.Null,
                "A hidden legacy slab must not fill the authored cavity or masquerade as the rendered body.");
            Assert.That(original.transform.Find("Carriage/Chassis"), Is.Null);
            foreach (var view in new[] { fallback, original })
            {
                view.SyncSlot(42, 2, DestinationShape.Triangle);
                view.SetTokenFlags(false, true);
            }
            foreach (var step in new[] { (0, 2.8f), (1, .1f), (1, 1.7f), (2, .2f) })
            {
                foreach (var view in new[] { fallback, original })
                {
                    view.PlaceOnEdge(_paths, step.Item1, step.Item2);
                    view.ApplyPresentation(CatPresentationState.RideIdle, .7f, false);
                }
                AssertSameAnchorsAndPassenger(fallback, original);
            }
            foreach (var view in new[] { fallback, original }) view.ShowRejection(.7f);
            AssertSameAnchorsAndPassenger(fallback, original);
            Assert.That(original.transform.Find("Carriage/Pin/Rejected").gameObject.activeSelf, Is.True);
            Assert.That(original.transform.Find("Carriage/Pin/Rejected/Symbol").GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(fallback.transform.Find("Carriage/Pin/Rejected/Symbol").GetComponent<MeshFilter>().sharedMesh));
            foreach (var view in new[] { fallback, original })
            {
                view.SyncSlot(99, 1, DestinationShape.Square);
                view.SetTokenFlags(true, false);
                view.PlaceAtNode(_paths, 3, new Vector3(6, 3, 0));
                view.ApplyPresentation(CatPresentationState.RideIdle, 1.2f, true);
            }
            AssertSameAnchorsAndPassenger(fallback, original);
            Assert.That(original.PresentationOccupantKey, Is.EqualTo(99));
            Assert.That(original.PinShape, Is.EqualTo(DestinationShape.Square));
            Assert.That(original.transform.Find("Carriage/Pin/Rejected").gameObject.activeSelf, Is.False,
                "slot reuse must clear the prior passenger's rejection cue");
            Assert.That(original.CatTint, Is.EqualTo(fallback.CatTint));
            Assert.That(original.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(original.GetComponentsInChildren<BoardElementId>(true), Is.Empty,
                "The owner adds the train inventory id; original decoration must add none.");
            var renderers = original.transform.Find("Carriage/OriginalCarriage")
                .GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(2));
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.SameAs(_material));
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(_atlas));
                Assert.That(renderer.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(Color.white));
                Assert.That(renderer.HasPropertyBlock(), Is.False,
                    "Changing the passenger line must not repaint the cream/teal carriage atlas.");
            }
        }

        [Test]
        public void OriginalCarriage_UsesActualMeshBoundsAtBoardRailHeight()
        {
            BuildFixture();
            var view = Create(new CarriageModelCatalog(_prefab));
            view.PlaceOnEdge(_paths, 1, 1.4f);
            Transform carriage = view.transform.Find("Carriage");
            Transform model = carriage.Find("OriginalCarriage");
            Bounds bounds = MeshBoundsRelativeTo(model, carriage);
            Assert.That(bounds.size.x, Is.EqualTo(.520f).Within(.0001f));
            Assert.That(bounds.size.y, Is.EqualTo(.540f).Within(.0001f));
            Assert.That(bounds.min.z, Is.EqualTo(.070f).Within(.0001f), "rim above the seat");
            Assert.That(bounds.max.z, Is.EqualTo(.235f).Within(.0001f), "wheel bottom at the rail crown");
            MeshFilter shell = model.Find("OpenShell").GetComponent<MeshFilter>();
            Vector3[] points = shell.sharedMesh.vertices.Select(vertex =>
                carriage.InverseTransformPoint(shell.transform.TransformPoint(vertex))).ToArray();
            Assert.That(points.Any(point => Mathf.Abs(point.x) < .24f && Mathf.Abs(point.y) < .22f
                && Mathf.Abs(point.z - .150f) < .0001f), Is.True, "actual recessed floor vertices");
            Assert.That(Vector3.Dot(model.right, carriage.right), Is.GreaterThan(.9999f),
                "+X remains vehicle forward through the board-up adapter");
        }

        [Test]
        public void DeliveredPassenger_HidesOriginalVehicleButKeepsRiderAndPin()
        {
            BuildFixture();
            var view = Create(new CarriageModelCatalog(_prefab));
            view.SyncSlot(42, 2, DestinationShape.Triangle);
            view.ApplyPresentation(CatPresentationState.RideIdle, .7f, true);
            Transform cat = view.transform.Find("Carriage/Cat");
            Transform pin = view.transform.Find("Carriage/Pin");
            view.PrepareDeliveredPassenger(new Vector3(2, 3, -.2f));
            Assert.That(view.transform.Find("Carriage/OriginalCarriage").gameObject.activeSelf, Is.False);
            Assert.That(view.transform.Find("Engine").gameObject.activeSelf, Is.False);
            Assert.That(cat.gameObject.activeSelf && pin.gameObject.activeSelf, Is.True);
            Assert.That(view.PinShape, Is.EqualTo(DestinationShape.Triangle));
            Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(2, 3, -.2f)));
            Assert.That(_prefab.activeSelf, Is.True, "delivery must not deactivate the shared source");
        }

        [TestCase("missing-part")]
        [TestCase("unbound-atlas")]
        [TestCase("collider")]
        [TestCase("wrong-axis")]
        public void MalformedOriginalCarriage_UsesCompleteFallback(string defect)
        {
            BuildFixture();
            if (defect == "missing-part") Object.DestroyImmediate(_prefab.transform.Find("OpenShell").gameObject);
            if (defect == "unbound-atlas") _material.SetTexture("_BaseMap", null);
            if (defect == "collider") _prefab.AddComponent<BoxCollider>();
            if (defect == "wrong-axis") _prefab.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var catalog = new CarriageModelCatalog(_prefab);
            var view = Create(catalog);
            Assert.That(catalog.AdmittedEntryCount, Is.Zero, defect);
            Assert.That(catalog.RejectionReason, Is.Not.Empty);
            Assert.That(view.OriginalCarriageAdmitted, Is.False);
            Assert.That(view.transform.Find("Carriage/Body").GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(view.transform.Find("Carriage/Chassis").GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(view.transform.Find("Carriage/OriginalCarriage"), Is.Null);
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void DefaultCreation_UsesInstalledOriginalResourceOrCompleteFallback()
        {
            GameObject resource = Resources.Load<GameObject>(CarriageModelCatalog.ResourcePath);
            var view = ToyTrainView.Create(_host.transform, "resource-carriage", _from, _to,
                new CatModelCatalog(null));
            if (resource == null)
            {
                Assert.That(view.OriginalCarriageAdmitted, Is.False);
                Assert.That(view.transform.Find("Carriage/Body"), Is.Not.Null);
                return;
            }
            Assert.That(view.OriginalCarriageAdmitted, Is.True, view.CarriageFallbackReason);
            Assert.That(view.transform.Find("Carriage/Body"), Is.Null);
            var installed = resource.GetComponentsInChildren<MeshFilter>();
            var mounted = view.transform.Find("Carriage/OriginalCarriage").GetComponentsInChildren<MeshFilter>();
            Assert.That(mounted.Select(filter => filter.sharedMesh),
                Is.EquivalentTo(installed.Select(filter => filter.sharedMesh)), "use shared imported meshes");
        }

        private ToyTrainView Create(CarriageModelCatalog carriage) => ToyTrainView.Create(
            _host.transform, "carriage-test", _from, _to, new CatModelCatalog(null), carriage);

        private static void AssertSameAnchorsAndPassenger(ToyTrainView a, ToyTrainView b)
        {
            foreach (string path in new[] { "", "Engine", "Carriage", "Carriage/Cat", "Carriage/Pin" })
            {
                Transform ta = path == "" ? a.transform : a.transform.Find(path);
                Transform tb = path == "" ? b.transform : b.transform.Find(path);
                Assert.That(Vector3.Distance(ta.position, tb.position), Is.LessThan(.00001f), path);
                Assert.That(Quaternion.Angle(ta.rotation, tb.rotation), Is.LessThan(.001f), path);
                Assert.That(ta.localScale, Is.EqualTo(tb.localScale), path);
            }
            foreach (Transform ta in a.transform.Find("Carriage/Cat").GetComponentsInChildren<Transform>(true))
            {
                Transform tb = b.transform.Find("Carriage/Cat").GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == ta.name);
                Assert.That(ta.localPosition, Is.EqualTo(tb.localPosition), ta.name);
                Assert.That(ta.localRotation, Is.EqualTo(tb.localRotation), ta.name);
                Assert.That(ta.localScale, Is.EqualTo(tb.localScale), ta.name);
            }
            Assert.That(a.PinShape, Is.EqualTo(b.PinShape));
            Assert.That(a.CatTint, Is.EqualTo(b.CatTint));
            Assert.That(a.PresentationState, Is.EqualTo(b.PresentationState));
            Assert.That(a.PresentationOccupantKey, Is.EqualTo(b.PresentationOccupantKey));
            var marksA = a.GetComponentInChildren<PassengerStatusMarks>(true);
            var marksB = b.GetComponentInChildren<PassengerStatusMarks>(true);
            Assert.That(marksA.StrayVisible, Is.EqualTo(marksB.StrayVisible));
            Assert.That(marksA.ExpressVisible, Is.EqualTo(marksB.ExpressVisible));
            Assert.That(a.transform.Find("Carriage/Pin/Symbol").GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(b.transform.Find("Carriage/Pin/Symbol").GetComponent<MeshFilter>().sharedMesh));
        }

        private static Bounds MeshBoundsRelativeTo(Transform model, Transform parent)
        {
            var points = model.GetComponentsInChildren<MeshFilter>().SelectMany(filter =>
                filter.sharedMesh.vertices.Select(vertex => parent.InverseTransformPoint(
                    filter.transform.TransformPoint(vertex)))).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) bounds.Encapsulate(point);
            return bounds;
        }

        private void BuildFixture()
        {
            _prefab = new GameObject("Synthetic original-carriage admission fixture");
            _atlas = new Texture2D(1024, 512);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "the fixture requires the shipped URP shader");
            _material = new Material(shader);
            _material.SetTexture("_BaseMap", _atlas);
            _material.SetColor("_BaseColor", Color.white);
            // A truly open five-box shell is a small test fixture, not claimed as the art.
            AddPart("OpenShell", new[] {
                (new Vector3(0,.075f,0), new Vector3(.46f,.02f,.42f)),
                (new Vector3(-.245f,.125f,0), new Vector3(.03f,.08f,.48f)),
                (new Vector3(.245f,.125f,0), new Vector3(.03f,.08f,.48f)),
                (new Vector3(0,.125f,-.225f), new Vector3(.46f,.08f,.03f)),
                (new Vector3(0,.125f,.225f), new Vector3(.46f,.08f,.03f)) });
            AddPart("Undercarriage", new[] {
                (new Vector3(0,.044f,0), new Vector3(.458f,.04f,.414f)),
                (new Vector3(-.16f,.054f,-.243f), new Vector3(.108f,.108f,.054f)),
                (new Vector3(.16f,.054f,-.243f), new Vector3(.108f,.108f,.054f)),
                (new Vector3(-.16f,.054f,.243f), new Vector3(.108f,.108f,.054f)),
                (new Vector3(.16f,.054f,.243f), new Vector3(.108f,.108f,.054f)) });
        }

        private void AddPart(string name, (Vector3 position, Vector3 size)[] boxes)
        {
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var mesh = new Mesh { name = "Synthetic " + name };
            mesh.CombineMeshes(boxes.Select(box => new CombineInstance { mesh = cube,
                transform = Matrix4x4.TRS(box.position, Quaternion.identity,
                    new Vector3(box.size.x / cube.bounds.size.x, box.size.y / cube.bounds.size.y,
                        box.size.z / cube.bounds.size.z)) }).ToArray());
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            var part = new GameObject(name);
            part.transform.SetParent(_prefab.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = _material;
        }
    }
}
