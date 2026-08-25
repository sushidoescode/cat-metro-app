using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
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

            // World units per grain band: the sheet carries 9 bands and _BaseMap_ST.x maps it
            // across the top's local width, so the pitch is width / (ST.x * 9). The desk's is
            // DeskSheetSpan / 27 = 0.96; finer than that is the whole point of a second sheet.
            Vector4 st = woodProperties.GetVector("_BaseMap_ST");
            float pitch = wood.localScale.x / (st.x * 9f);
            Assert.That(pitch, Is.InRange(0.25f, 0.90f),
                "board grain must be finer than the desk's 0.96-unit planks without "
                + "collapsing into corduroy at phone scale");

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

            // The sheet repeats, so every term in it has to be period-1 or the board shows a
            // hard line wherever the tile wraps. Compare the step across the wrap with the
            // average step between neighbouring columns/rows: a term that does not tile
            // (Mathf.PerlinNoise, as the desk sheet uses) drives this ratio well above 1.
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

            // The separation this branch must not spend. Measured on the Color values
            // themselves with Rec.709 weights: CreamCard sits 0.262 above the board interior,
            // and that gap is what makes the pale ballast ribbon read as track laid on wood
            // rather than as a stripe painted onto it. The grain multiplies the interior, so
            // the case that matters is the BRIGHTEST texel — the one that walks the board
            // closest to the ribbon. Keeping the sheet's ceiling at or below 1 is what makes
            // this arithmetic hold for any tuning of the grain, not just today's.
            float brightest = 0f;
            foreach (var texel in sheet.GetPixels32())
                brightest = Mathf.Max(brightest, texel.r / 255f);
            Assert.That(brightest, Is.LessThanOrEqualTo(1f),
                "a sheet that can brighten the interior would close the gap to the ballast");

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
            Assert.That(key.shadowStrength, Is.InRange(0.4f, 0.8f),
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
