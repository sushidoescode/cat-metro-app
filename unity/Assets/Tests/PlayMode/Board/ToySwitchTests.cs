using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Theme;
using CatMetro.Presentation.Fx;

namespace CatMetro.Tests.PlayMode
{
    // SWITCH-LEVERS: the LOOK.md switch through the real seam (GameRoot.Launch, L001) — a
    // chunky orange lever on a teal base with a direction arrow, exactly as tappable as the
    // greybox disc it replaces. Mirrors the BoardLookTests shape.
    public sealed class ToySwitchTests
    {
        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            GameRoot.DevSkipShippedHome = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameRoot.DevSkipShippedHome = false;
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _root = null;
        }

        private static Transform FindByName(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator Switch_IsAToyLever_BaseLeverArrowPresent_PaletteBound()
        {
            _root = GameRoot.Launch();
            yield return null;

            var sw = FindByName(_root.View.gameObject, "switch:S1");
            Assert.That(sw, Is.Not.Null, "the pinned root name survives the restyle");
            Assert.That(sw.GetComponent<ToySwitchView>(), Is.Not.Null);

            // two-tone base: MetroTeal walls first (the teach-ring comparison reads
            // sharedMaterial), the lighter token-derived top face second
            var baseMaterials = sw.GetComponent<Renderer>().sharedMaterials;
            Assert.That(baseMaterials.Length, Is.EqualTo(2), "walls + lighter top face");
            AssertColor(baseMaterials[0].color, Palette.MetroTeal, "teal base walls");
            AssertColor(baseMaterials[1].color, ToySwitchView.BaseTopColor,
                "lighter teal base top");
            AssertColor(FindByName(sw.gameObject, "Stem").GetComponent<Renderer>()
                .sharedMaterial.color, ToySwitchView.StemWoodColor, "wooden dowel stem");
            AssertColor(FindByName(sw.gameObject, "Knob").GetComponent<Renderer>()
                .sharedMaterial.color, Palette.TicketOrange, "orange lever head");
            AssertColor(FindByName(sw.gameObject, "Arrow").GetComponent<Renderer>()
                .sharedMaterial.color, Palette.TicketOrange, "orange direction arrow");
            foreach (var renderer in sw.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material.shader, Is.EqualTo(GreyboxMaterial.Shared.shader),
                        renderer.name + " stays on the committed pipeline shader");

            Assert.That(sw.GetComponentsInChildren<Collider>(true), Is.Empty,
                "input is screen-space disc resolution — the toy adds no collider surface");
            var ids = sw.GetComponentsInChildren<BoardElementId>(true);
            Assert.That(ids.Length, Is.EqualTo(1),
                "exactly the one BoardElementId the greybox switch carried");
            Assert.That(ids[0].gameObject, Is.SameAs(sw.gameObject));
            Assert.That(ids[0].Id, Is.EqualTo("S1"));
            Assert.That(ids[0].Kind, Is.EqualTo("switch"));
        }

        [UnityTest]
        public IEnumerator TapTarget_Preserved_SameCenter_Same48dpLaw()
        {
            _root = GameRoot.Launch();
            yield return null;

            // the toy anchors on the tap center: same board-plane XY as SwitchWorldPos
            var sw = FindByName(_root.View.gameObject, "switch:S1");
            var swLocal = _root.View.transform.InverseTransformPoint(sw.position);
            var anchorLocal = _root.View.transform.InverseTransformPoint(
                _root.View.SwitchWorldPos(0));
            Assert.That(swLocal.x, Is.EqualTo(anchorLocal.x).Within(1e-3f));
            Assert.That(swLocal.y, Is.EqualTo(anchorLocal.y).Within(1e-3f),
                "the visual root never drifts off the input anchor");

            // the 48dp behavioural law, re-proven after the restyle (same or better target)
            float pxPerDp = Screen.dpi > 0f ? Screen.dpi / 160f : 1f;
            Vector2 center = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            int entries = _root.Session.Log.Entries.Count;
            Assert.That(_root.Input.HandleTapAtScreen(center + new Vector2(23f * pxPerDp, 0f)),
                Is.EqualTo(0), "23dp off-center still hits");
            Assert.That(_root.Input.HandleTapAtScreen(center + new Vector2(25f * pxPerDp, 0f)),
                Is.EqualTo(-1), "25dp off-center still misses");
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(entries + 1),
                "one hit, no phantom command");
        }

        [UnityTest]
        public IEnumerator LeverAndArrow_SwingPastTheCommittedRoute_ThenSettle()
        {
            Time.timeScale = 0f;
            _root = GameRoot.Launch();
            yield return null;

            var sw = FindByName(_root.View.gameObject, "switch:S1");
            var view = sw.GetComponent<ToySwitchView>();

            Vector2 expectedBefore = RouteDirection(_root.View.CommittedRoute(0));
            AssertAimsAt(sw, view, expectedBefore, "the built lever aims at the initial route");

            int before = _root.View.CommittedRoute(0);
            float initialYaw = sw.localEulerAngles.z;
            var fx = BoardFx.GetOrCreate(_root.View.transform);
            _root.Input.HandleTapAtScreen(
                _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            Assert.That(_root.View.CommittedRoute(0), Is.Not.EqualTo(before));

            Vector2 expectedAfter = RouteDirection(_root.View.CommittedRoute(0));
            Assert.That(Vector2.Distance(expectedBefore, expectedAfter), Is.GreaterThan(0.1f),
                "positive control: L001's two routes genuinely diverge");
            // NO yield between the tap and this assert: the committed lever flips on the tap
            // frame (criterion 3a), exactly as the greybox arm did.
            float targetYaw = ToySwitchView.YawDegrees(expectedAfter);
            float sign = Mathf.Sign(Mathf.DeltaAngle(initialYaw, targetYaw));
            fx.Advance(0.05f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(initialYaw, sw.localEulerAngles.z)), Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(view.LeverPivot.localRotation, ToySwitchView.LeverLocalRotation), Is.GreaterThan(1f));
            Assert.That(sw.localScale.x, Is.GreaterThan(1.05f));
            _root.View.RefreshSwitches(); // the same committed route must not restart its swing
            fx.Advance(0.05f);
            Assert.That(Mathf.DeltaAngle(targetYaw, sw.localEulerAngles.z) * sign, Is.InRange(4f, 8.1f));
            fx.Advance(0.04f);
            AssertAimsAt(sw, view, expectedAfter, "the toy settles to the committed route at 140 ms");
            Assert.That(sw.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator MotionOff_CompletesASwing_AndStopsItsDust()
        {
            Time.timeScale = 0f;
            _root = GameRoot.Launch();
            yield return null;
            var view = _root.View.GetComponentInChildren<ToySwitchView>();
            var fx = BoardFx.GetOrCreate(_root.View.transform, () => _root.MotionOff);
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            fx.Advance(0.05f);
            Assert.That(fx.ActiveTweenCount, Is.GreaterThan(0));
            _root.MotionOffToggle = true;
            fx.Advance(0.01f);
            AssertAimsAt(view.transform, view, RouteDirection(_root.View.CommittedRoute(0)), "reduced motion settles the verb");
            Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
            foreach (var ps in _root.View.GetComponentsInChildren<ParticleSystem>())
                Assert.That(ps.particleCount, Is.Zero);
        }

        // 2026-08-25 render review: the teach affordance was a solid navy cylinder and read as
        // a heavy dark puck under the toy. Through the REAL board seam it must now be a ring
        // with the board's wood showing through, while every CM-UX-03 pin still holds
        // (TeachAffordanceTests owns those; this is the geometry half).
        [UnityTest]
        public IEnumerator TeachRing_IsAnAnnulus_NotAPuckUnderTheToy()
        {
            _root = GameRoot.Launch();
            yield return null;

            var ring = FindByName(_root.View.gameObject, "teachring:S1");
            Assert.That(ring, Is.Not.Null, "L001 is an onboarding board — positive control");
            Assert.That(_root.View.TeachAffordancePresent(0), Is.True,
                "the affordance still teaches after the restyle");

            var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            float closest = float.MaxValue;
            foreach (var v in mesh.vertices)
                closest = Mathf.Min(closest, new Vector2(v.x, v.y).magnitude);
            Assert.That(closest, Is.EqualTo(ToySwitchView.TeachRingInnerRadius).Within(1e-3f),
                "the centre is empty — the board's wood shows through the ring");

            // the base's widest reach stays inside that hole, so the gap never closes
            var sw = FindByName(_root.View.gameObject, "switch:S1");
            float widest = 0f;
            foreach (var v in sw.GetComponent<MeshFilter>().sharedMesh.vertices)
                widest = Mathf.Max(widest, new Vector2(v.x, v.y).magnitude);
            Assert.That(widest, Is.LessThan(ToySwitchView.TeachRingInnerRadius),
                "the toy never overlaps its own ring");
            Assert.That(ring.GetComponentsInChildren<Collider>(true), Is.Empty,
                "decoration must not intercept the switch tap");
        }

        [UnityTest]
        public IEnumerator Teardown_Clean_RebuildReusesTheCachedMaterials()
        {
            _root = GameRoot.Launch();
            yield return null;
            var views = Object.FindObjectsByType<ToySwitchView>(FindObjectsSortMode.None);
            Assert.That(views.Length, Is.EqualTo(1), "one toy per authored switch on L001");
            var material = views[0].GetComponent<Renderer>().sharedMaterial;

            Object.DestroyImmediate(_root.gameObject);
            _root = null;
            Assert.That(Object.FindObjectsByType<ToySwitchView>(FindObjectsSortMode.None),
                Is.Empty, "no stray switch survives the board teardown");

            _root = GameRoot.Launch();
            yield return null;
            var rebuilt = Object.FindObjectsByType<ToySwitchView>(FindObjectsSortMode.None);
            Assert.That(rebuilt.Length, Is.EqualTo(1));
            Assert.That(rebuilt[0].GetComponent<Renderer>().sharedMaterial,
                Is.SameAs(material), "rebuilds reuse the cached tinted material — no leak");
        }

        // Board-local direction from the switch node toward the given route's target node,
        // derived from the authored level file (never from BoardView internals).
        private Vector2 RouteDirection(int routeIndex)
        {
            var dto = _root.Session.Level.Dto;
            var nodes = dto.Nodes.ToArray().ToDictionary(n => n.Id,
                n => new Vector2(n.X * BoardView.GridX, n.Y * BoardView.GridY));
            var edges = dto.Edges.ToArray().ToDictionary(e => e.Id, e => e);
            var sw = dto.Switches.ToArray()[0];
            string routeEdge = sw.Routes.ToArray()[routeIndex];
            return (nodes[edges[routeEdge].To] - nodes[sw.NodeId]).normalized;
        }

        private void AssertAimsAt(Transform sw, ToySwitchView view, Vector2 expected,
            string because)
        {
            // the assembly's +Y aims at the routed target (board-local)
            var forward = sw.localRotation * Vector3.up;
            Assert.That(forward.x, Is.EqualTo(expected.x).Within(1e-3f), because);
            Assert.That(forward.y, Is.EqualTo(expected.y).Within(1e-3f), because);
            Assert.That(forward.z, Is.EqualTo(0f).Within(1e-3f), "yaw only — never off-plane");
            // the tilted lever leans toward that same branch, still rising off the board
            var leverAxis = _root.View.transform.InverseTransformDirection(
                view.LeverPivot.rotation * Vector3.back);
            var lean = new Vector2(leverAxis.x, leverAxis.y).normalized;
            Assert.That(Vector2.Dot(lean, expected), Is.GreaterThan(0.99f),
                because + " — the lean shows where the train will go");
            Assert.That(leverAxis.z, Is.LessThan(0f), "the lever stands toward the camera");
        }

        private static void AssertColor(Color actual, Color expected, string part)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f), part);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f), part);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f), part);
        }
    }
}
