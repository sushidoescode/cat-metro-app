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
            // two-tone base (2026-08-25 render review): MetroTeal walls stay sharedMaterial[0]
            // — the instance the teach-ring comparison reads — with the lighter top on [1]
            Assert.That(baseRenderer.sharedMaterials.Length, Is.EqualTo(2));
            AssertColor(baseRenderer.sharedMaterials[0].color, Palette.MetroTeal);
            AssertColor(baseRenderer.sharedMaterials[1].color, ToySwitchView.BaseTopColor);
            AssertColor(baseRenderer.sharedMaterial.color, Palette.MetroTeal);

            var pivot = view.LeverPivot;
            Assert.That(pivot, Is.Not.Null);
            Assert.That(pivot.localRotation, Is.EqualTo(ToySwitchView.LeverLocalRotation),
                "the lever's lean is the constant toy tilt — direction comes from the yaw");
            var stem = pivot.Find("Stem");
            AssertColor(stem.GetComponent<Renderer>().sharedMaterial.color,
                ToySwitchView.StemWoodColor);
            Assert.That(stem.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null,
                "the dowel uses the builtin cylinder — a null here means the resource name "
                + "drifted and the lever would render as a floating knob");
            AssertColor(pivot.Find("Knob").GetComponent<Renderer>().sharedMaterial.color,
                Palette.TicketOrange);
            AssertColor(view.transform.Find("Arrow").GetComponent<Renderer>().sharedMaterial.color,
                Palette.TicketOrange);
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material.shader, Is.EqualTo(GreyboxMaterial.Shared.shader),
                        renderer.name + " must stay on the committed pipeline shader");

            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty,
                "taps resolve through screen-space discs — the toy must add no collider");
            Assert.That(view.GetComponentsInChildren<BoardElementId>(true), Is.Empty,
                "BoardView owns the switch's ONE BoardElementId; Build adds none");
        }

        [Test]
        public void ArrowMesh_PointsAlongPlusY_AsAClosedSolidTile()
        {
            _host = new GameObject("switch-test-host");
            var view = ToySwitchView.Build("S-test", _host.transform, Vector3.zero);
            var mesh = view.transform.Find("Arrow").GetComponent<MeshFilter>().sharedMesh;

            var vertices = mesh.vertices;
            var tip = vertices[0];
            foreach (var v in vertices)
                if (v.y > tip.y) tip = v;
            Assert.That(tip.y, Is.GreaterThan(0.3f), "the head reaches forward, phone-bold");
            Assert.That(tip.x, Is.EqualTo(0f).Within(1e-5f), "the tip sits on the centreline");
            Assert.That(mesh.bounds.min.y, Is.GreaterThan(-0.4f),
                "the tail stays on the base block");
            // 2026-08-25 render review: the flat single-sided decal was culled invisible; the
            // arrow is now a thin closed solid no winding or mirrored transform can hide.
            Assert.That(mesh.bounds.size.z,
                Is.EqualTo(ToySwitchView.ArrowThickness).Within(1e-5f),
                "the arrow is an extruded tile, not a single-sided decal");
            Assert.That(mesh.triangles.Length, Is.EqualTo(60),
                "front + back + one wall quad per outline edge — a closed solid");
        }

        // The regression pin the first cut lacked (2026-08-25 render review: every
        // camera-facing triangle was backface-culled): the camera looks from -Z, so the base
        // top face and the arrow front face must carry normals pointing at it.
        [Test]
        public void Meshes_CameraFacingFaces_ActuallyFaceTheCamera()
        {
            _host = new GameObject("switch-test-host");
            var view = ToySwitchView.Build("S-test", _host.transform, Vector3.zero);

            var baseMesh = view.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(baseMesh.subMeshCount, Is.EqualTo(2),
                "walls submesh + the lighter top-face submesh");
            var baseNormals = baseMesh.normals;
            for (int i = 0; i < 8; i++) // vertices 0..7 are the split top face
                Assert.That(baseNormals[i].z, Is.LessThan(-0.9f),
                    "base top-face vertex " + i + " must face the camera (-Z), not be culled");

            var arrowMesh = view.transform.Find("Arrow").GetComponent<MeshFilter>().sharedMesh;
            var arrowNormals = arrowMesh.normals;
            for (int i = 0; i < 7; i++) // vertices 0..6 are the split front face
                Assert.That(arrowNormals[i].z, Is.LessThan(-0.9f),
                    "arrow front-face vertex " + i + " must face the camera (-Z), not be culled");
        }

        // 2026-08-25 render review: the teach ring was a SOLID cylinder and read as a heavy
        // dark puck. It is now a true annulus. Every CM-UX-03 pin is re-proven here at the
        // geometry level — one transform, one renderer, no children (the band-gate test counts
        // onboarding as exactly alternation + 2 of each).
        [Test]
        public void BuildTeachRing_IsAHollowAnnulus_OneRendererNoChildren()
        {
            _host = new GameObject("switch-test-host");
            var material = GreyboxMaterial.CreateTinted("ring-test", Palette.InkNavy);
            try
            {
                var ring = ToySwitchView.BuildTeachRing("S-test", _host.transform,
                    new Vector3(0f, 0f, -0.35f), material);

                Assert.That(ring.name, Is.EqualTo("teachring:S-test"),
                    "the name the RingCount probe matches");
                Assert.That(ring.parent, Is.SameAs(_host.transform));
                Assert.That(ring.childCount, Is.Zero, "one transform, exactly");
                Assert.That(ring.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1),
                    "one renderer, exactly");
                Assert.That(ring.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material),
                    "the ring keeps its own material — distinct from the base, same shader");
                Assert.That(ring.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(ring.GetComponentsInChildren<BoardElementId>(true), Is.Empty,
                    "the affordance stays out of the authored gameplay inventory");
                Assert.That(ring.GetComponent<Animator>(), Is.Null);
                Assert.That(ring.GetComponent<Animation>(), Is.Null);

                // the hole is real: no vertex anywhere near the centre
                var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
                foreach (var v in mesh.vertices)
                {
                    float radius = new Vector2(v.x, v.y).magnitude;
                    Assert.That(radius,
                        Is.InRange(ToySwitchView.TeachRingInnerRadius - 1e-4f,
                            ToySwitchView.TeachRingOuterRadius + 1e-4f),
                        "every vertex sits in the band — a solid disc would fill the centre");
                }
                foreach (var n in mesh.normals)
                    Assert.That(n.z, Is.LessThan(-0.9f),
                        "the ring faces the camera (-Z) — the winding law of this toy");
            }
            finally { Object.DestroyImmediate(material); }
        }

        // The wood gap that keeps the ring from reading as a puck: the hole must clear the
        // base's widest footprint, whichever way the switch is routed.
        [Test]
        public void TeachRingHole_ClearsTheBasesWidestFootprint_LeavingAWoodGap()
        {
            _host = new GameObject("switch-test-host");
            var view = ToySwitchView.Build("S-test", _host.transform, Vector3.zero);
            float widest = 0f;
            foreach (var v in view.GetComponent<MeshFilter>().sharedMesh.vertices)
                widest = Mathf.Max(widest, new Vector2(v.x, v.y).magnitude);

            Assert.That(ToySwitchView.TeachRingInnerRadius, Is.GreaterThan(widest + 0.02f),
                "board wood shows between the toy and its ring at every routing angle");
            Assert.That(ToySwitchView.TeachRingOuterRadius,
                Is.GreaterThan(ToySwitchView.TeachRingInnerRadius),
                "a band with real width still reads from across the board");
        }

        [Test]
        public void BaseTopColor_IsTheTokenDerivedLighterTeal()
        {
            var expected = Color.Lerp(Palette.MetroTeal, Palette.WarmPaper, 0.25f);
            AssertColor(ToySwitchView.BaseTopColor, expected);
            // intent, not just formula: lighter than the walls so the raked key cannot crush
            // the face the player reads (2026-08-25 render review finding 2)
            float walls = Palette.MetroTeal.r + Palette.MetroTeal.g + Palette.MetroTeal.b;
            float top = ToySwitchView.BaseTopColor.r + ToySwitchView.BaseTopColor.g
                + ToySwitchView.BaseTopColor.b;
            Assert.That(top, Is.GreaterThan(walls + 0.1f), "the top face reads lighter");
        }

        [Test]
        public void StemWoodColor_IsATokenDerivedWarmDowelTone()
        {
            var expected = Color.Lerp(Palette.TicketOrange, Palette.InkNavy, 0.32f);
            AssertColor(ToySwitchView.StemWoodColor, expected);
            // intent: a warm wood, not the cream bar that read grey in the 2026-08-25 render
            Assert.That(ToySwitchView.StemWoodColor.r,
                Is.GreaterThan(ToySwitchView.StemWoodColor.b + 0.2f), "warm, not neutral");
            Assert.That(ToySwitchView.StemWoodColor.r,
                Is.LessThan(Palette.CreamCard.r), "darker than the cream it replaced");
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
