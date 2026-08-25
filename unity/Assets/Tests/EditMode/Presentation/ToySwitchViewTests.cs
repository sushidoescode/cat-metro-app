using CatMetro.Presentation.Board;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    // SWITCH-LEVERS: the pure laws of the toy switch (yaw math, lever lean, arrow shape) plus
    // the Build structure, pinned without a scene — the ToyTrackMeshBuilderTests shape.
    public sealed class ToySwitchViewTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
        }

        // --- the pure yaw law: local +Y lands on the routed direction ---
        [Test]
        public void YawDegrees_TakesLocalPlusYOntoTheBoardPlaneDirection()
        {
            var directions = new[]
            {
                new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
                new Vector2(0f, -1f), new Vector2(0.6f, 0.8f), new Vector2(-3f, 4f).normalized,
            };
            foreach (var dir in directions)
            {
                var rotated = Quaternion.Euler(0f, 0f, ToySwitchView.YawDegrees(dir)) * Vector3.up;
                var expected = new Vector3(dir.x, dir.y, 0f);
                Assert.That(Vector3.Distance(rotated, expected), Is.LessThan(1e-4f),
                    "yaw must aim the assembly's +Y at " + dir);
            }
        }

        [Test]
        public void LeverTilt_IsAReadableLean_TowardTheTargetAndOffTheBoard()
        {
            Assert.That(ToySwitchView.LeverTiltDegrees, Is.InRange(20f, 55f),
                "tilted like the concept toy — neither flagpole-upright nor lying flat");
            // pre-yaw the lever rises camera-ward (-Z); the tilt leans it toward local +Y
            var axis = ToySwitchView.LeverLocalRotation * Vector3.back;
            float tilt = ToySwitchView.LeverTiltDegrees * Mathf.Deg2Rad;
            Assert.That(axis.y, Is.EqualTo(Mathf.Sin(tilt)).Within(1e-4f),
                "the lean points at the routed branch");
            Assert.That(axis.z, Is.EqualTo(-Mathf.Cos(tilt)).Within(1e-4f),
                "the lever still stands off the board toward the camera");
            Assert.That(axis.x, Is.EqualTo(0f).Within(1e-4f), "no sideways lean");
        }

        // --- Build structure: base, lever, arrow — palette-bound, inert to input ---
        [Test]
        public void Build_CreatesBaseLeverArrow_PaletteBound_NoCollidersNoIds()
        {
            _host = new GameObject("switch-test-host");
            var view = ToySwitchView.Build("S-test", _host.transform, new Vector3(1f, 2f, -0.4f));

            Assert.That(view.name, Is.EqualTo("switch:S-test"), "the pinned root name");
            Assert.That(view.transform.parent, Is.SameAs(_host.transform));
            Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, -0.4f)));
            Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one),
                "the teach pulse modulates the root scale from a unit baseline");

            var baseRenderer = view.GetComponent<Renderer>();
            Assert.That(baseRenderer, Is.Not.Null, "the ROOT carries the base renderer "
                + "(the teach-ring material comparison reads it)");
            AssertColor(baseRenderer.sharedMaterial.color, Palette.MetroTeal);

            var pivot = view.LeverPivot;
            Assert.That(pivot, Is.Not.Null);
            Assert.That(pivot.localRotation, Is.EqualTo(ToySwitchView.LeverLocalRotation),
                "the lever's lean is the constant toy tilt — direction comes from the yaw");
            AssertColor(pivot.Find("Stem").GetComponent<Renderer>().sharedMaterial.color,
                Palette.CreamCard);
            AssertColor(pivot.Find("Knob").GetComponent<Renderer>().sharedMaterial.color,
                Palette.TicketOrange);
            AssertColor(view.transform.Find("Arrow").GetComponent<Renderer>().sharedMaterial.color,
                Palette.TicketOrange);
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.sharedMaterial.shader,
                    Is.EqualTo(GreyboxMaterial.Shared.shader),
                    renderer.name + " must stay on the committed pipeline shader");

            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty,
                "taps resolve through screen-space discs — the toy must add no collider");
            Assert.That(view.GetComponentsInChildren<BoardElementId>(true), Is.Empty,
                "BoardView owns the switch's ONE BoardElementId; Build adds none");
        }

        [Test]
        public void ArrowMesh_PointsAlongPlusY_FlatWithTheTipOnTheCentreline()
        {
            _host = new GameObject("switch-test-host");
            var view = ToySwitchView.Build("S-test", _host.transform, Vector3.zero);
            var mesh = view.transform.Find("Arrow").GetComponent<MeshFilter>().sharedMesh;

            var vertices = mesh.vertices;
            var tip = vertices[0];
            foreach (var v in vertices)
            {
                if (v.y > tip.y) tip = v;
                Assert.That(v.z, Is.EqualTo(0f).Within(1e-5f), "the arrow is a flat decal");
            }
            Assert.That(tip.y, Is.GreaterThan(0.2f), "the head reaches forward");
            Assert.That(tip.x, Is.EqualTo(0f).Within(1e-5f), "the tip sits on the centreline");
            Assert.That(mesh.bounds.min.y, Is.GreaterThan(-0.4f),
                "the tail stays on the base block");
        }

        [Test]
        public void SetDirection_YawsTheAssembly_SoTheLeverLeansTowardTheBranch()
        {
            _host = new GameObject("switch-test-host");
            var view = ToySwitchView.Build("S-test", _host.transform, Vector3.zero);

            view.SetDirection(new Vector2(1f, 0f));
            var forward = view.transform.localRotation * Vector3.up;
            Assert.That(Vector3.Distance(forward, Vector3.right), Is.LessThan(1e-4f));
            var leverAxis = view.LeverPivot.rotation * Vector3.back;
            Assert.That(leverAxis.x, Is.GreaterThan(0.5f), "the lever leans toward +X");
            Assert.That(leverAxis.z, Is.LessThan(0f), "and still rises off the board");

            view.SetDirection(new Vector2(-1f, 0f));
            leverAxis = view.LeverPivot.rotation * Vector3.back;
            Assert.That(leverAxis.x, Is.LessThan(-0.5f), "the toggle flips the lean");

            var held = view.transform.localRotation;
            view.SetDirection(Vector2.zero);
            Assert.That(view.transform.localRotation, Is.EqualTo(held),
                "a degenerate direction never snaps the toy to a bogus yaw");
        }

        [Test]
        public void Build_Twice_SharesCachedMeshesAndMaterials_NoPerRebuildLeak()
        {
            _host = new GameObject("switch-test-host");
            var first = ToySwitchView.Build("S-a", _host.transform, Vector3.zero);
            var second = ToySwitchView.Build("S-b", _host.transform, Vector3.one);
            Assert.That(second.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(first.GetComponent<MeshFilter>().sharedMesh),
                "one base mesh serves every switch across Retry rebuilds");
            Assert.That(second.GetComponent<Renderer>().sharedMaterial,
                Is.SameAs(first.GetComponent<Renderer>().sharedMaterial),
                "one cached tinted material — the ToyTrackMeshBuilder idiom, no leak");
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f));
        }
    }
}
