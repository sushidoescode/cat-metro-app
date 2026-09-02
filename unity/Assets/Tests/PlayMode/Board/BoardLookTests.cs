using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Props;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardLookTests
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
            GameRoot.DevSkipShippedHome = false;
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator BoardBody_FramesL001WithAVisibleLayeredSlab()
        {
            _root = GameRoot.Launch();
            yield return null;

            var body = _root.View.transform.Find("BoardBody");
            Assert.That(body, Is.Not.Null, "the level needs a finite tabletop silhouette");
            Assert.That(body.Find("WoodTop"), Is.Not.Null, "wood is the playable surface");
            Assert.That(body.Find("CreamRim"), Is.Not.Null, "the pale rim separates top from edge");
            Assert.That(body.Find("NavyBase"), Is.Not.Null, "the dark base makes thickness readable");

            var nodes = _root.Session.Level.Dto.Nodes.ToArray();
            float minX = nodes.Min(n => n.X);
            float maxX = nodes.Max(n => n.X);
            float minY = nodes.Min(n => n.Y);
            float maxY = nodes.Max(n => n.Y);
            var top = body.Find("WoodTop");
            Assert.That(top.localPosition.x - top.localScale.x * 0.5f,
                Is.LessThanOrEqualTo(minX - 0.75f));
            Assert.That(top.localPosition.x + top.localScale.x * 0.5f,
                Is.GreaterThanOrEqualTo(maxX + 0.75f));
            Assert.That(top.localPosition.y - top.localScale.y * 0.5f,
                Is.LessThanOrEqualTo(minY - 0.75f));
            Assert.That(top.localPosition.y + top.localScale.y * 0.5f,
                Is.GreaterThanOrEqualTo(maxY + 0.75f));
        }

        [UnityTest]
        public IEnumerator BoardBody_IsDecorationOnlyAndSitsBehindGameplay()
        {
            _root = GameRoot.Launch();
            yield return null;

            var body = _root.View.transform.Find("BoardBody");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponentsInChildren<Collider>(true), Is.Empty,
                "collider-free mesh parts must not intercept input or trip stripped Android builds");
            Assert.That(body.GetComponentsInChildren<MeshFilter>(true)
                    .All(filter => filter.sharedMesh != null), Is.True,
                "every collider-free board part still needs the built-in cube mesh");
            Assert.That(body.GetComponentsInChildren<BoardElementId>(true), Is.Empty,
                "decoration must stay out of the authored gameplay inventory");
            Assert.That(body.Find("WoodTop").localPosition.z, Is.GreaterThan(0.2f),
                "positive Z is behind the furthest track geometry in this board convention");
        }

        [UnityTest]
        public IEnumerator DeskSurface_ExtendsWellPastTheRaisedBoard()
        {
            _root = GameRoot.Launch();
            yield return null;

            var wood = _root.View.transform.Find("BoardBody/WoodTop");
            var desk = _root.View.transform.Find("DeskSurface/DeskTop");
            Assert.That(desk, Is.Not.Null, "the room-scale tabletop sits behind the toy board");
            Assert.That(desk.localScale.x, Is.GreaterThan(wood.localScale.x + 12f));
            Assert.That(desk.localScale.y, Is.GreaterThan(wood.localScale.y + 12f),
                "the warm desk should continue beyond the portrait frame, not end like a second board");
            var properties = new MaterialPropertyBlock();
            desk.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.That(properties.GetTexture("_BaseMap"), Is.Not.Null,
                "the oversized desk needs subtle continuous grain without geometry seams");

            // Replaces an older pin that required the whole slab to sit in front of the near
            // plane. That was the wrong invariant twice over: it let the slab's far
            // off-screen corner drag the camera backwards through the shadow budget, and it
            // still allowed the background to show at the top of the frame (measured: a 52px
            // wedge on the 2026-08-25 r2 render). What actually matters is the frame.
            AssertDeskCoversTheFrame(_root, 917f / 2048f);
            float gameplayDepth = Vector3.Dot(
                _root.View.transform.TransformPoint(_root.View.PresentationCenterLocal)
                    - _root.Cam.transform.position,
                _root.Cam.transform.forward);
            Assert.That(gameplayDepth, Is.LessThan(24f),
                "the board must stay inside the URP asset's 25-unit main-light shadow distance");
        }

        [UnityTest]
        public IEnumerator DecorativeSlab_DoesNotDriveTheWidthFit()
        {
            _root = GameRoot.Launch();
            yield return null;
            _root.Cam.aspect = 917f / 2048f;

            var slab = _root.View.transform.Find("BoardBody");
            var desk = _root.View.transform.Find("DeskSurface");
            Assert.That(slab, Is.Not.Null);

            Bounds slabBounds = default, content = default;
            bool foundSlab = false, foundContent = false;
            foreach (var renderer in _root.View.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (desk != null && renderer.transform.IsChildOf(desk)) continue;
                if (renderer.transform.IsChildOf(slab))
                {
                    if (!foundSlab) { slabBounds = renderer.bounds; foundSlab = true; }
                    else slabBounds.Encapsulate(renderer.bounds);
                    continue;
                }
                if (!foundContent) { content = renderer.bounds; foundContent = true; }
                else content.Encapsulate(renderer.bounds);
            }
            Assert.That(foundSlab && foundContent, Is.True, "board needs a slab and content");

            if (slabBounds.size.x <= content.size.x + 0.1f)
                Assert.Pass("slab is no wider than its content here — nothing to bleed");

            // Had the slab still been fitted into the horizontal safe band, this is the size
            // it would have forced. The toy is the content; the border around it is scenery,
            // and target-01 runs that border off both edges of the frame.
            float slabDrivenSize = slabBounds.size.x * 0.5f
                / (9f / 19.5f * 0.88f) * 1.05f;
            Assert.That(_root.Cam.orthographicSize, Is.LessThan(slabDrivenSize - 0.01f),
                "the decorative slab must not set the zoom — it cost the diorama real size "
                + "to satisfy a margin no safe-frame law asks about");
        }

        [UnityTest]
        public IEnumerator DeskGrain_IsItsOwnBroadSheet_AndWarmthFallsOffTowardTheEdges()
        {
            _root = GameRoot.Launch();
            yield return null;

            var wood = _root.View.transform.Find("BoardBody/WoodTop").GetComponent<Renderer>();
            var desk = _root.View.transform.Find("DeskSurface/DeskTop").GetComponent<Renderer>();
            var woodProperties = new MaterialPropertyBlock();
            var deskProperties = new MaterialPropertyBlock();
            wood.GetPropertyBlock(woodProperties);
            desk.GetPropertyBlock(deskProperties);

            var deskTexture = deskProperties.GetTexture("_BaseMap") as Texture2D;
            Assert.That(deskTexture, Is.Not.Null);
            Assert.That(deskTexture, Is.Not.SameAs(woodProperties.GetTexture("_BaseMap")),
                "the desk needs its own broader grain, not the board's repeating tile");
            Vector4 st = deskProperties.GetVector("_BaseMap_ST");
            Assert.That(st.x, Is.GreaterThan(1.2f),
                "the sheet maps to a bounded world span rather than stretching with the slab, "
                + "so the slab can overhang the frame without thinning the grain or dragging "
                + "the warmth falloff off screen");
            Assert.That(st.z, Is.EqualTo(0.5f - 0.5f * st.x).Within(0.001f),
                "the warm pool stays centred on the board");
            Assert.That(st.w, Is.EqualTo(0.5f - 0.5f * st.y).Within(0.001f));
            Assert.That(deskTexture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp),
                "clamp is load-bearing: past the sheet it holds the dark cool edge texel, "
                + "which is what the desk beyond the lamp pool should be");

            // Region averages, not single texels: the sheet now carries real grain valleys
            // and plank seams, so one texel is a lottery. The law governs the neighbourhood.
            Color centre = AverageDeskSample(deskTexture, 0.5f, 0.5f);
            Color corner = AverageDeskSample(deskTexture, 0.07f, 0.07f);
            Assert.That(centre.maxColorComponent,
                Is.GreaterThan(corner.maxColorComponent + 0.2f),
                "warmth must pool around the board and fall away toward the desk edges");
            Assert.That(centre.r - centre.b, Is.GreaterThan(corner.r - corner.b),
                "the falloff cools as it darkens, like lamp light leaving the desk");
        }

        private const float PinnedPhoneAspect = 917f / 2048f;

        [UnityTest]
        public IEnumerator BoardBody_MarginIsAnisotropicAndStillClearsTheDeskClutter()
        {
            _root = GameRoot.Launch();
            yield return null;

            var nodes = _root.Session.Level.Dto.Nodes.ToArray();
            float minX = nodes.Min(n => n.X), maxX = nodes.Max(n => n.X);
            float minY = nodes.Min(n => n.Y), maxY = nodes.Max(n => n.Y);
            var top = _root.View.transform.Find("BoardBody/WoodTop");
            Assert.That(top, Is.Not.Null);

            float side = (top.localScale.x - (maxX - minX)) * 0.5f;
            float far = top.localPosition.y + top.localScale.y * 0.5f - maxY;
            float near = minY - (top.localPosition.y - top.localScale.y * 0.5f);

            // The frame is 2.23:1 and the toy is roughly square, so the whole fill gap is
            // vertical. Under the diorama tilt a unit of the board's local Y buys 1.5722 of
            // screen height for 0.5326 of width, while a unit of local X buys 1.7375 of width
            // for 0.1099 of height — local Y is 2.95x the better axis and the margin has to
            // say so. If these three ever collapse back to one number the fill goes with it.
            Assert.That(far, Is.GreaterThan(side + 0.5f),
                "the far margin is the efficient axis and must be the largest");
            Assert.That(side, Is.GreaterThan(near + 0.5f),
                "the side margin is bought with frame the slab does not have to pay for");

            // The near margin is a wall, not a preference. BoardPropDecorator seats the desk
            // clutter at (node minY - 1.4) on the desk contact plane, which is BEHIND the
            // board's wood face — a near margin of 1.4 or more buries the mug inside the slab.
            Assert.That(near, Is.LessThan(1.4f),
                "a near margin at or past 1.4 swallows the desk clutter BoardPropDecorator "
                + "places at minY - 1.4");
            Assert.That(near, Is.GreaterThanOrEqualTo(1.0f),
                "and it still has to be a rim, not a hairline");

            var clutter = _root.View.GetComponentsInChildren<BoardPropInstance>(true)
                .FirstOrDefault(x => x.Role == PropRole.DeskClutter);
            if (clutter != null)
                Assert.That(clutter.transform.localPosition.y,
                    Is.LessThan(top.localPosition.y - top.localScale.y * 0.5f),
                    "the desk clutter has to sit beyond the board's near rim, on the desk");
        }

        [UnityTest]
        public IEnumerator BoardBody_BleedsOffBothSideEdgesAndFillsTheFrame()
        {
            _root = GameRoot.Launch();
            yield return null;
            var camera = _root.Cam;
            camera.aspect = PinnedPhoneAspect;
            var top = _root.View.transform.Find("BoardBody/WoodTop");
            Assert.That(top, Is.Not.Null);

            // The slab's camera-facing face, projected. The tilt makes it a parallelogram, so
            // measure its actual area rather than a bounding box: shoelace over the four
            // corners, in viewport units, which makes the number a fraction of the frame.
            var corners = new Vector2[4];
            var local = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(top.TransformPoint(local[i]));
                corners[i] = new Vector2(viewport.x, viewport.y);
            }
            float area = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                area += a.x * b.y - b.x * a.y;
            }
            area = Mathf.Abs(area) * 0.5f;

            // Measured baseline, off .catshots/orchestrator-2026-08-25-r6 at orthographicSize
            // 8.834: the slab was 37.10 of the frame's 139.77 square world units, 26.5%. The
            // law split plus the anisotropic margin computes to 53.5% unclipped and 48.1%
            // once the side bleed is cut off. 0.45 is a floor under that, well clear of the
            // 0.265 it replaces.
            Assert.That(area, Is.GreaterThan(0.45f),
                "the board's projected area collapsed back toward the 26.5% we started from");

            float minX = corners.Min(c => c.x), maxX = corners.Max(c => c.x);
            float minY = corners.Min(c => c.y), maxY = corners.Max(c => c.y);
            // Target-01 runs its board off the left AND right edges. Ours does now too, and
            // that is the whole point of the slab being outside the safe-frame law.
            Assert.That(minX, Is.LessThan(-0.15f), "the slab must bleed off the left edge");
            Assert.That(maxX, Is.GreaterThan(1.02f), "and off the right edge, not merely touch it");
            // Vertically it must NOT, because that is what keeps the toy reading as a finite
            // object on a desk rather than as a floor.
            Assert.That(minY, Is.GreaterThan(0.02f), "the near rim stays in frame");
            Assert.That(maxY, Is.LessThan(0.98f), "and so does the far rim");
        }

        [UnityTest]
        public IEnumerator DefocusVeil_BandsNeverTouchTheDiorama()
        {
            _root = GameRoot.Launch();
            yield return null;
            var camera = _root.Cam;

            var veil = camera.transform.Find(DefocusVeil.VeilName);
            Assert.That(veil, Is.Not.Null, "the depth-of-field stand-in should have been built");
            var mesh = veil.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.EqualTo(8),
                "two bands, top and bottom — a side band could only occupy the outer 2.4% of "
                + "the viewport and would be invisible full-width overdraw");

            // A back-facing veil renders as nothing at all while every geometry and clearance
            // check below still passes, so the winding gets its own assertion. The reference
            // is Unity's built-in Quad, which faces a camera looking down +Z exactly as ours
            // does: its first triangle's XY cross product is negative, so ours must be too.
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.That(cross, Is.LessThan(0f),
                    "veil triangle " + (i / 3) + " winds away from the camera");
            }
            foreach (var normal in mesh.normals)
                Assert.That(normal.z, Is.LessThan(0f), "the veil faces the camera");

            float bandBottomTop = float.NegativeInfinity;  // top of the LOWER band
            float bandTopBottom = float.PositiveInfinity;  // bottom of the UPPER band
            foreach (var vertex in mesh.vertices)
            {
                float y = veil.TransformPoint(vertex).y;
                if (y < camera.transform.position.y)
                    bandBottomTop = Mathf.Max(bandBottomTop, y);
                else bandTopBottom = Mathf.Min(bandTopBottom, y);
            }
            Assert.That(bandBottomTop, Is.LessThan(bandTopBottom), "the bands must not meet");

            // This is the legibility guarantee, and it is structural rather than a tuning
            // choice: the hole is cut from the measured extent of frameBounds plus a pad, so
            // NOTHING in the diorama — gameplay or scenery, slab included — is under a veil
            // triangle. Not a transparent one. None.
            var deskSurface = _root.View.transform.Find("DeskSurface");
            int sweptRenderers = 0;
            foreach (var renderer in _root.View.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                sweptRenderers++;
                Assert.That(renderer.bounds.min.y, Is.GreaterThan(bandBottomTop),
                    renderer.name + " overlaps the lower defocus band");
                Assert.That(renderer.bounds.max.y, Is.LessThan(bandTopBottom),
                    renderer.name + " overlaps the upper defocus band");
            }
            Assert.That(sweptRenderers, Is.GreaterThan(4), "the sweep found almost nothing to check");
        }

        [UnityTest]
        public IEnumerator DefocusVeil_SheetFadesFromNothingAtTheHoleToACoolEdgeAndACreamLobe()
        {
            _root = GameRoot.Launch();
            yield return null;

            // The falloff, sampled on the plain half of the sheet. A cubed smoothstep, so the
            // first third of the band is under 2% opacity: if the ramp were visible where it
            // meets the hole the veil would announce itself as a rectangle around the toy.
            Assert.That((int)DefocusVeil.Texel(0.75f, 0f).a, Is.LessThanOrEqualTo(1),
                "the veil must be fully transparent where it meets the diorama");
            Assert.That((int)DefocusVeil.Texel(0.75f, 0.3f).a, Is.LessThanOrEqualTo(4),
                "and still invisible a third of the way out");
            Assert.That((int)DefocusVeil.Texel(0.75f, 1f).a, Is.InRange(120, 136),
                "reaching EdgeAlpha 0.5 at the frame edge");
            Assert.That((int)DefocusVeil.Texel(0.75f, 0.6f).a,
                Is.GreaterThan((int)DefocusVeil.Texel(0.75f, 0.4f).a),
                "the ramp is monotonic outward");

            Color edge = DefocusVeil.Texel(0.75f, 1f);
            Assert.That(edge.b - edge.r, Is.GreaterThan(0f),
                "the frame edge cools as it darkens, continuing DeskGrain's own falloff law");
            Assert.That(Vector3.Distance(
                    new Vector3(edge.r, edge.g, edge.b),
                    new Vector3(Palette.DepotNavy.r, Palette.DepotNavy.g, Palette.DepotNavy.b)),
                Is.LessThan(0.05f), "and it is a Palette token, not a hand-mixed grey");

            // The out-of-focus foreground lobe, which is the cue target-01's coffee cup
            // supplies and the one thing a sharp orthographic camera cannot produce.
            Color32 core = DefocusVeil.Texel(DefocusVeil.LobeU, 1f);
            Assert.That((int)core.a, Is.GreaterThan(220),
                "the lobe's core has to occlude the desk to read as a near object");
            Color coreColor = core;
            Assert.That(Vector3.Distance(
                    new Vector3(coreColor.r, coreColor.g, coreColor.b),
                    new Vector3(Palette.CreamCard.r, Palette.CreamCard.g, Palette.CreamCard.b)),
                Is.LessThan(0.18f), "the lobe is cream, like target-01's cup");

            // Soft-edged over hundreds of screen pixels, which is what defocus looks like and
            // what a hard-edged decal does not. Between the core and clear of the lobe the
            // alpha has to fall back to the plain ramp without a contour.
            Assert.That(DefocusVeil.Texel(DefocusVeil.LobeU + DefocusVeil.LobeRadiusU * 0.8f,
                1f).a, Is.LessThan((int)core.a - 40),
                "the lobe must fall off, not stop");
            Assert.That(DefocusVeil.Texel(DefocusVeil.LobeU + DefocusVeil.LobeRadiusU * 1.2f,
                1f).a, Is.InRange(120, 140),
                "and land back on the plain falloff with no step");
            Assert.That((int)DefocusVeil.Texel(0.75f, 1f).a,
                Is.LessThan((int)DefocusVeil.Texel(DefocusVeil.LobeU, 1f).a),
                "the plain half of the sheet carries no lobe");
        }

        [UnityTest]
        public IEnumerator DefocusVeil_IsAnAuthoredTransparentMaterialOutsideTheBoard()
        {
            _root = GameRoot.Launch();
            yield return null;
            var veil = _root.Cam.transform.Find(DefocusVeil.VeilName);
            Assert.That(veil, Is.Not.Null);

            // Not under BoardView, and this is load-bearing twice over: FitCamera unions
            // renderer bounds under the board, so a veil in there would feed its own size
            // back into the fit; and RuntimeSceneRigTests sweeps the same hierarchy, so it
            // would be asked to obey a safe-frame law written for gameplay.
            Assert.That(veil.IsChildOf(_root.View.transform), Is.False,
                "the veil must never live under BoardView");
            Assert.That(veil.parent, Is.EqualTo(_root.Cam.transform));

            var renderer = veil.GetComponent<MeshRenderer>();
            Assert.That(renderer.shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(renderer.receiveShadows, Is.False,
                "a lens effect must not join the diorama's lighting rig");

            // The material is AUTHORED, not built at runtime. A runtime-built transparent
            // URP/Lit works in the editor and is stripped on device, because no build-time
            // material would reference the _SURFACE_TYPE_TRANSPARENT variant — the same class
            // of silent device-only failure AGENTS.md records for URP base colour.
            var material = DefocusVeil.SharedMaterial();
            Assert.That(material, Is.Not.Null,
                "Resources/" + DefocusVeil.MaterialResourcePath + " must exist as an asset");
            Assert.That(material, Is.SameAs(
                Resources.Load<Material>(DefocusVeil.MaterialResourcePath)),
                "the veil must use the loaded asset itself, never a clone of it");
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"),
                "the project has one shader and the veil does not get to add a second");
            Assert.That(material.GetFloat("_Surface"), Is.EqualTo(1f),
                "surface type Transparent");
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(0f));
            Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo(3000),
                "the veil draws after the diorama it sits in front of");
            Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True,
                "the veil's colour is emission so it cannot drift when the key light is "
                + "recalibrated — a defocused desk should not change hue because the key "
                + "moved three degrees");
        }

        /// <summary>
        /// No frame corner may show background instead of desk, and no corner of the desk
        /// that IS on screen may fall behind the near plane. Both are evaluated on the desk's
        /// camera-facing plane: solve where the corner's view ray meets that plane, then ask
        /// the slab's own local space whether the hit is still on the slab (|local| &lt; 0.5).
        /// </summary>
        private static void AssertDeskCoversTheFrame(GameRoot root, float aspect)
        {
            var camera = root.Cam;
            camera.aspect = aspect;
            var desk = root.View.transform.Find("DeskSurface/DeskTop");
            Assert.That(desk, Is.Not.Null);
            Vector3 normal = desk.forward;
            Assert.That(Mathf.Abs(normal.z), Is.GreaterThan(0.01f),
                "an edge-on desk plane cannot be solved for coverage");
            Vector3 face = desk.TransformPoint(new Vector3(0f, 0f, -0.5f));
            Vector3 eye = camera.transform.position;

            for (int corner = 0; corner < 4; corner++)
            {
                float x = eye.x + ((corner & 1) == 0 ? -1f : 1f) * camera.orthographicSize
                    * aspect;
                float y = eye.y + ((corner & 2) == 0 ? -1f : 1f) * camera.orthographicSize;
                float z = face.z
                    - (normal.x * (x - face.x) + normal.y * (y - face.y)) / normal.z;
                Vector3 local = desk.InverseTransformPoint(new Vector3(x, y, z));
                string where = "frame corner " + corner;
                // 0.5 is the slab edge; the design slack computes to ~0.31 at worst, so 0.45
                // catches a real regression without pinning the overscan to a single value.
                Assert.That(Mathf.Abs(local.x), Is.LessThan(0.45f),
                    where + " falls off the desk horizontally — background would show");
                Assert.That(Mathf.Abs(local.y), Is.LessThan(0.45f),
                    where + " falls off the desk vertically — this is the cream band");
                Assert.That(z - (eye.z + camera.nearClipPlane), Is.GreaterThan(0.25f),
                    where + " of the desk is clipped by the camera near plane");
            }
        }

        private static Color AverageDeskSample(Texture2D texture, float u, float v)
        {
            Color sum = Color.clear;
            for (int du = -1; du <= 1; du++)
                for (int dv = -1; dv <= 1; dv++)
                    sum += texture.GetPixelBilinear(u + du * 0.04f, v + dv * 0.04f);
            return sum / 9f;
        }

        [UnityTest]
        public IEnumerator BoardGrain_IsItsOwnFinerSheetThanTheDesk()
        {
            _root = GameRoot.Launch();
            yield return null;

            var wood = _root.View.transform.Find("BoardBody/WoodTop");
            var desk = _root.View.transform.Find("DeskSurface/DeskTop");
            var woodProperties = new MaterialPropertyBlock();
            var deskProperties = new MaterialPropertyBlock();
            wood.GetComponent<Renderer>().GetPropertyBlock(woodProperties);
            desk.GetComponent<Renderer>().GetPropertyBlock(deskProperties);

            var sheet = woodProperties.GetTexture("_BaseMap") as Texture2D;
            Assert.That(sheet, Is.Not.Null,
                "the playable surface needs grain of its own — target-01's board is visibly "
                + "wood, and ours read as a smooth gradient");
            Assert.That(sheet, Is.Not.SameAs(deskProperties.GetTexture("_BaseMap")),
                "the board is a smaller, nearer object than the furniture it sits on");
            Assert.That(sheet.wrapMode, Is.EqualTo(TextureWrapMode.Repeat),
                "the board sheet holds a fixed world pitch by repeating, so a small level and "
                + "a large one get the same size of plank rather than a stretched one");

            const int bands = 8;

            // World units per grain band: the sheet carries `bands` of them and _BaseMap_ST.x
            // maps it across the top's local width, so the pitch is width / (ST.x * bands).
            // The desk's is DeskSheetSpan / 27 = 0.96; finer than that is the whole point of
            // a second sheet.
            Vector4 st = woodProperties.GetVector("_BaseMap_ST");
            float pitch = wood.localScale.x / (st.x * bands);
            Assert.That(pitch, Is.InRange(0.25f, 0.90f),
                "board grain must be finer than the desk's 0.96-unit planks without "
                + "collapsing into corduroy at phone scale");

            // The root cause of the seam this file's wrap check first caught, pinned directly
            // rather than only through its symptom. Texels per band must be whole. It was
            // 256/9 = 28.4, so every interior band boundary fell BETWEEN texels and none of
            // them reached the seam notch's bottom — except the column at u = 0, where
            // `across` is exactly 0 and `seam` saturates. That made the wrap column the single
            // darkest seam in the sheet and drew a line across the board once per tile.
            Assert.That(sheet.width % bands, Is.Zero,
                "texels per band must divide evenly, or the only column landing exactly on a "
                + "seam notch bottom is the one at the tile wrap — measured at 1.59x the "
                + "worst interior plank seam when this was 256/9");

            var texels = sheet.GetPixels32();
            float low = 1f, high = 0f;
            foreach (var texel in texels)
            {
                float value = texel.r / 255f;
                low = Mathf.Min(low, value);
                high = Mathf.Max(high, value);
            }
            // The sheet this replaces spanned 0.075 and read as a gradient; this one spans
            // ~0.196, concentrated in narrow valleys and seams rather than a slow sine.
            Assert.That(high - low, Is.GreaterThan(0.15f),
                "a low-contrast sheet satisfies every structural pin above and still renders "
                + "as the flat gradient this replaces");

            // The sheet repeats, so the board shows a hard line wherever the tile wraps unless
            // the wrap is indistinguishable from an ordinary column step. Compare the step
            // across the wrap with the average step between neighbouring columns/rows.
            //
            // This check earns its keep: it failed on the first build of this sheet and the
            // defect was real, not a misfire. Being period-1 in u — which every term here is —
            // turned out to be necessary but not sufficient; the texel alignment pinned above
            // is what actually bit. Measured on the built sheet: u wraps at 1.11x the average
            // interior step (0.0344 against 0.0311) and 0.44x the worst interior plank seam,
            // v at 0.61x. The broken 256/9 sheet measured 3.12x on u.
            //
            // If you change this sheet, MEASURE the wrap the way this test does; do not
            // reason about it. And measure in float32: `hash` is
            // Mathf.Abs(Mathf.Sin(band * 12.9898f) * 43758.547f) % 1f, and near 43758 a
            // float32 resolves only ~0.004, so a double-precision model of it is a different
            // sheet. Band 7 hashes 0.988 in float32 against 0.166 in float64 — 15.6 radians
            // apart in the ripple phase it feeds. That mismatch is exactly why this failure
            // was reported as passing before the slot ran it.
            int size = sheet.width;
            float seamU = 0f, insideU = 0f, seamV = 0f, insideV = 0f;
            for (int i = 0; i < size; i++)
            {
                seamU += Mathf.Abs(Texel(texels, size, 0, i) - Texel(texels, size, size - 1, i));
                seamV += Mathf.Abs(Texel(texels, size, i, 0) - Texel(texels, size, i, size - 1));
                for (int j = 1; j < size; j++)
                {
                    insideU += Mathf.Abs(Texel(texels, size, j, i) - Texel(texels, size, j - 1, i));
                    insideV += Mathf.Abs(Texel(texels, size, i, j) - Texel(texels, size, i, j - 1));
                }
            }
            seamU /= size;
            seamV /= size;
            insideU /= size * (size - 1);
            insideV /= size * (size - 1);
            Assert.That(seamU, Is.LessThan(insideU * 2f),
                "the tile does not wrap cleanly across u — the board will show a seam");
            Assert.That(seamV, Is.LessThan(insideV * 2f),
                "the tile does not wrap cleanly across v");
        }

        [UnityTest]
        public IEnumerator BoardGrain_NeverErodesTheBallastContrast()
        {
            _root = GameRoot.Launch();
            yield return null;

            var wood = _root.View.transform.Find("BoardBody/WoodTop").GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            wood.GetPropertyBlock(properties);
            Color interior = properties.GetColor("_BaseColor");
            var sheet = properties.GetTexture("_BaseMap") as Texture2D;
            Assert.That(sheet, Is.Not.Null);

            // Guard first: an unbound property block reads back as clear, and a black interior
            // would satisfy the margin below without meaning anything. GreyboxMaterial failing
            // to resolve is exactly the silent-bind failure AGENTS.md warns about.
            Assert.That(Luminance(interior), Is.GreaterThan(0.2f),
                "the board interior never got its base colour — the margin below is vacuous");

            // The separation this branch must not spend. Measured on the Color values
            // themselves with Rec.709 weights: CreamCard sits 0.262 above the board interior,
            // and that gap is what makes the pale ballast ribbon read as track laid on wood
            // rather than as a stripe painted onto it. The grain multiplies the interior, so
            // the case that matters is the BRIGHTEST texel — the one that walks the board
            // closest to the ribbon. A sheet stored as bytes cannot exceed 1.0, so the ceiling
            // is structural: no tuning of the grain can push the board past its own ungrained
            // albedo, and this margin therefore holds for any future sheet, not just today's.
            float brightest = 0f;
            foreach (var texel in sheet.GetPixels32())
                brightest = Mathf.Max(brightest, texel.r / 255f);

            float board = Luminance(interior) * brightest;
            float ballast = Luminance(Palette.CreamCard);
            Assert.That(ballast - board, Is.GreaterThanOrEqualTo(0.262f),
                "worst-case grained board must stay at least the measured 0.262 below the "
                + "CreamCard ballast — that margin is why the track reads as track");
        }

        private static float Texel(Color32[] texels, int size, int x, int y)
            => texels[y * size + x].r / 255f;

        private static float Luminance(Color c)
            => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [UnityTest]
        public IEnumerator KeyLight_RakesTheTiltedBoardLikeLateAfternoon()
        {
            _root = GameRoot.Launch();
            yield return null;

            var key = _root.GetComponentsInChildren<Light>(true)
                .Single(light => light.name == "Diorama Warm Key");
            Vector3 toSource = -key.transform.forward;
            Assert.That(toSource.y, Is.GreaterThan(0.1f), "the key still hangs above the desk");
            Assert.That(toSource.x, Is.GreaterThan(0.2f),
                "late-afternoon light enters from frame right, matching the target renders");

            Vector3 boardNormal = -_root.View.transform.forward;
            float incidence = Vector3.Angle(toSource, boardNormal);
            Assert.That(incidence, Is.InRange(18f, 55f),
                "the key must rake across the tilted board — square-on light flattens every "
                + "face and collapses shadow length to nothing");

            // Moved from "> 0.3". The key must stay warm, but 0.3 was a floor on the very
            // saturation that broke the palette: (1, 0.78, 0.56) linearises to (1, 0.571,
            // 0.274), and with the key carrying ~85% of the illumination that quarter-blue
            // stamped amber onto every albedo in the game — InkNavy rails measured (57, 52,
            // 51) and the MetroTeal wedge (121, 130, 94), an olive. Warmth belongs in the
            // wood albedos and in the split against a cool fill, not in a key saturated
            // enough to overwrite docs/LOOK.md's navy and teal. The band keeps it amber-ish
            // and catches a slide back to either extreme.
            Assert.That(key.color.r - key.color.b, Is.InRange(0.12f, 0.30f),
                "warm key, neither noon white nor amber enough to overwrite the palette");
            // Upper bound restored to 0.8. A previous revision tightened it to 0.5 on the
            // finding that shadowStrength was crushing the cool end of the palette — navy
            // rails measured at luminance 47 with a key visibility of 0.454, apparently
            // 1 - 0.55 exactly. Slot 7 measured the real boot seam and disproved it: the
            // rails read (51, 59, 73) with a key visibility of ~0.85, so they are barely
            // shadowed, the 0.454 was numerology on a superseded sample, and moving the
            // constant moved nothing. The 0.5 ceiling encoded a false belief AND forbade
            // this branch's own deliberate raking value of 0.55, so it is gone. The lower
            // bound stays relaxed to admit integration/look-stack's 0.38.
            Assert.That(key.shadowStrength, Is.InRange(0.35f, 0.8f),
                "shadows must read on the desk yet stay airy");
            // Inverted, deliberately. This pin used to demand warm sky over cool ground, and
            // that ordering was the bug: the board's visible normal has n.y = 0.616, so every
            // surface the player looks at draws on the SKY band and the cool ground reached
            // nothing but downward faces. Cool fill from above, warm bounce off the wooden
            // desk below — which is also what the two actually are.
            Assert.That(RenderSettings.ambientGroundColor.r
                    - RenderSettings.ambientGroundColor.b,
                Is.GreaterThan(RenderSettings.ambientSkyColor.r
                    - RenderSettings.ambientSkyColor.b),
                "cool fill above, warm desk bounce below — a warm sky is what left shaded "
                + "faces with nowhere cool to live");
        }

        [UnityTest]
        public IEnumerator Illuminant_CanRenderACoolAlbedoCool()
        {
            _root = GameRoot.Launch();
            yield return null;

            var key = _root.GetComponentsInChildren<Light>(true)
                .Single(light => light.name == "Diorama Warm Key");

            // Evaluate the rig where it is actually looked at: the board's visible normal.
            // Unity's own ambient probe does the trilight-to-SH work, so this reads the real
            // fill rather than a model of it.
            Vector3 boardNormal = -_root.View.transform.forward;
            var results = new Color[1];
            RenderSettings.ambientProbe.Evaluate(new[] { boardNormal }, results);
            Color fill = results[0];
            Assert.That(fill.maxColorComponent, Is.GreaterThan(0.01f),
                "ambient probe evaluated to black — the trilight settings never reached it, "
                + "and nothing below this line means anything");
            Assert.That(fill.b, Is.GreaterThan(fill.r),
                "the fill is the only cool light in the rig; if it is warm too, a shaded or "
                + "upward-facing surface has nowhere cool to live and every navy in the game "
                + "renders brown");

            // Key irradiance on that same normal, in linear space, plus the fill: this is the
            // S in the transfer measured off the 2026-08-25 r3 render,
            //     rendered_linear = S * (albedo_linear + 0.0254)
            // fitted from two coplanar known albedos (WoodTop and CreamRim share the board's
            // normal). S was (1.17, 0.678, 0.354) — blue at 0.303 of red — which is why no
            // albedo in the colour cube could render as target-01's navy rails (69, 75, 95)
            // or its teal (51, 120, 115). This rig solves to about (1.063, 0.890, 0.738).
            Color keyLinear = key.color.linear;
            float incidence = Mathf.Max(0f, Vector3.Dot(-key.transform.forward, boardNormal));
            float scale = key.intensity * incidence;
            float red = keyLinear.r * scale + fill.r;
            float blue = keyLinear.b * scale + fill.b;
            Assert.That(red, Is.GreaterThan(0.01f), "the key contributes no red at all");
            Assert.That(blue / red, Is.GreaterThan(0.55f),
                "the illuminant must carry enough blue for a cool albedo to survive it — at "
                + "0.303 it could not, and docs/LOOK.md names navy and teal in the palette");
            // Deliberately loose on this side. Whether Evaluate returns the same convention
            // the shader samples (the 1/pi is the usual disagreement) changes the fill's
            // magnitude but not its direction, so a tight bound here would pin a convention
            // rather than the look. The exact pin on warmth is the key's own r - b band in
            // KeyLight_RakesTheTiltedBoardLikeLateAfternoon.
            Assert.That(blue / red, Is.LessThan(0.98f),
                "and it must still be warm: the late-afternoon character is the point");
        }

        [UnityTest]
        public IEnumerator WarmBackground_ReplacesTheDefaultSkybox()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Cam.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(RenderSettings.skybox, Is.Null);
            Assert.That(Vector4.Distance(_root.Cam.backgroundColor,
                    new Color(0.85f, 0.81f, 0.73f)),
                Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator WorldSpacePreview_DoesNotCastIntoTheDiorama()
        {
            _root = GameRoot.Launch();
            yield return null;

            foreach (var renderer in _root.Preview.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(renderer.receiveShadows, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SceneLook_RestoresGlobalEnvironmentWhenRootIsDestroyed()
        {
            Material previousSkybox = RenderSettings.skybox;
            var previousMode = RenderSettings.ambientMode;
            Color previousSky = RenderSettings.ambientSkyColor;
            Color previousEquator = RenderSettings.ambientEquatorColor;
            Color previousGround = RenderSettings.ambientGroundColor;
            float previousIntensity = RenderSettings.ambientIntensity;
            var sentinel = new Material(GreyboxMaterial.Shared.shader);
            var sentinelSky = new Color(0.11f, 0.22f, 0.33f);
            var sentinelEquator = new Color(0.17f, 0.19f, 0.21f);
            var sentinelGround = new Color(0.05f, 0.07f, 0.09f);

            try
            {
                RenderSettings.skybox = sentinel;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientSkyColor = sentinelSky;
                RenderSettings.ambientEquatorColor = sentinelEquator;
                RenderSettings.ambientGroundColor = sentinelGround;
                RenderSettings.ambientIntensity = 0.42f;

                _root = GameRoot.Launch();
                yield return null;
                Assert.That(RenderSettings.skybox, Is.Null);

                var retiringRoot = _root;
                Object.Destroy(retiringRoot.gameObject);
                _root = GameRoot.Launch();
                yield return null;
                Assert.That(retiringRoot == null, Is.True,
                    "the old root is destroyed after the replacement has acquired the look");
                Assert.That(RenderSettings.skybox, Is.Null,
                    "an overlapping live root must retain the diorama environment");
                Assert.That(RenderSettings.ambientMode,
                    Is.EqualTo(UnityEngine.Rendering.AmbientMode.Trilight));

                Object.DestroyImmediate(_root.gameObject);
                _root = null;
                Assert.That(RenderSettings.skybox, Is.SameAs(sentinel));
                Assert.That(RenderSettings.ambientMode,
                    Is.EqualTo(UnityEngine.Rendering.AmbientMode.Flat));
                Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sentinelSky));
                Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(sentinelEquator));
                Assert.That(RenderSettings.ambientGroundColor, Is.EqualTo(sentinelGround));
                Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.42f).Within(0.001f));
            }
            finally
            {
                RenderSettings.skybox = previousSkybox;
                RenderSettings.ambientMode = previousMode;
                RenderSettings.ambientSkyColor = previousSky;
                RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround;
                RenderSettings.ambientIntensity = previousIntensity;
                Object.DestroyImmediate(sentinel);
            }
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_BoardLook_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_BOARD_LOOK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)
                && System.Environment.GetCommandLineArgs().Contains("-cmBoardLookCapture"))
                dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                    "../Library/Captures"));
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_BOARD_LOOK_CAPTURE_DIR to emit a phone frame");
                yield break;
            }

            _root = GameRoot.Launch();
            yield return null;
            yield return null;

            const int width = 917;
            const int height = 2048;
            var rt = new RenderTexture(width, height, 24);
            _root.Cam.targetTexture = rt;
            _root.Preview.Refresh();
            Canvas.ForceUpdateCanvases();
            _root.Cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            _root.Cam.targetTexture = null;
            RenderTexture.active = null;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "step-2-board.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }
    }
}
