using System.Linq;
using CatMetro.Content;
using CatMetro.Presentation.Props;
using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    /// <summary>
    /// Builds the finite, decorative tabletop beneath an authored board. Gameplay remains in
    /// the board's XY plane; thickness extends away from the camera along positive Z.
    /// </summary>
    public static class BoardSurface
    {
        // The wood beyond the outermost node, per edge. It used to be one 1.05 constant. It
        // is three now because the three edges cost completely different amounts of frame,
        // and because the slab is the ONE part of the diorama the safe-frame law does not
        // govern — RuntimeSceneRigTests asserts node markers and BoardPropInstance renderers
        // and never touches BoardBody, and BoardSceneLook.FitCamera excludes BoardBody from
        // the width fit. So growing the slab moves board fill without moving the camera.
        //
        // Under the 38/-32/-4 tilt, one unit of margin buys, in world units of screen:
        //   local X (MarginSide): width +2*|M00| = 1.7375   height +2*|M10| = 0.1099
        //   local Y (MarginFar) : width +2*|M01| = 0.5326   height +2*|M11| = 1.5722
        // The portrait frame is 2.23:1 and the toy is ~1:1, so the whole 26.5% -> 70% gap is
        // vertical: on the r6 render the slab spans 8.27 of the frame's 17.67 world units
        // tall while already overhanging it horizontally. Local Y is therefore the efficient
        // axis, at 2.95x the screen height per unit of frame width spent.
        //
        // MarginSide 2.05. Measured sweep of clipped slab coverage on L001 at the post-split
        // orthographicSize 7.919, holding MarginFar 3.05:
        //   1.05 -> 39.4%   1.30 -> 41.9%   1.55 -> 44.2%   1.80 -> 46.3%
        //   2.05 -> 48.1%   2.30 -> 49.6%   2.80 -> 52.0%
        // The returns flatten but never stop, so the choice is not the knee — it is the
        // first value that clears BOTH frame edges with room to spare. At 1.80 the slab's
        // far corner lands 0.17 units past the right edge, which is 20px and inside the
        // uncertainty on where the camera centres; at 2.05 it is 0.39 units and unambiguous,
        // so the board bleeds left AND right as target-01's does rather than bleeding left
        // and merely touching right. There is no upper law here — losing the side rims off
        // frame is what the target does — but the vertical rims stay in frame, and those are
        // the ones that make the toy read as a finite object.
        //
        // Losing the horizontal rims is also what makes the desk's own defocus falloff
        // matter more than it did: see DefocusVeil, which continues that falloff past the
        // slab into the top and bottom of the frame.
        //
        // MarginFar 3.05, capped by the height fit, not by taste. requiredForHeight takes
        // over from requiredForWidth when frameBounds.size.y exceeds size/0.69079; on the
        // tallest authored levels (L012, L015) that ratio reaches 0.971 at 3.05 and 0.999 at
        // 3.55. 3.05 keeps ~3% of headroom on the worst level. If it ever did bind, the
        // failure is graceful — the camera pulls back and fill drops, no law is breached.
        //
        // MarginNear 1.05, unchanged, and this one is a hard wall rather than a choice.
        // BoardPropDecorator seats the desk clutter at (node minY - 1.4) on the DESK contact
        // plane at z 1.38, which is BEHIND the board's wood face at z 0.35. A near margin of
        // 1.4 or more would bury the mug inside the slab, and that file belongs to another
        // lane right now. 1.05 leaves 0.35 units of clearance. Raising this is the single
        // most valuable follow-up once the prop lane can move the clutter: target-01's own
        // big wood band is at its NEAR edge, running off the bottom of frame.
        private const float MarginSide = 2.05f;
        private const float MarginFar = 3.05f;
        private const float MarginNear = 1.05f;
        private const float WoodFront = 0.35f;
        private const float WoodDepth = 0.70f;
        private const float RimWidth = 0.24f;
        private const float RimDepth = 0.10f;
        private const float DeskFront = 1.38f;
        private const float DeskDepth = 0.50f;
        // Sized so the desk's camera-facing face still contains the whole frame after the
        // board tilt swings it: the tilt maps the desk's +Z stand-off into ~1.0 units of
        // downward screen shift and foreshortens its local Y by cos(38 deg), so the slab has
        // to overhang the frame by much more than the frame's own size. Solved against the
        // largest level (L008) at the widest supported portrait aspect (4:3) and the pre-fit
        // (larger) frame with 10% safety: needs 33.9. 38 keeps ~4 units of slack. The old 20
        // left a wedge of background across the top of the frame — measured at 52px of 2048
        // on the 2026-08-25 r2 render, exactly where this face fell short.
        private const float DeskOverscan = 38f;
        // The grain sheet is mapped to a fixed world span instead of stretching with the slab
        // above, so growing the slab cannot dilute the planks or push the warmth falloff off
        // screen. Beyond the sheet, Clamp repeats its darkest, coolest edge texel — which is
        // exactly what the far desk should be.
        private const float DeskSheetSpan = 26f;
        // World units per repeat of the board's own grain sheet. Picked so the board's figure
        // is finer than the furniture it sits on: 8 grain bands per 4.5 units is 0.5625 units
        // a band against the desk's 26/27 = 0.96, so the toy reads as a smaller, closer piece
        // of wood rather than a chip off the same plank. Unlike the desk sheet this one
        // repeats, so the pitch is a fixed world size on every level instead of stretching
        // with the board — which is what planks actually are. The band count has a hard
        // constraint on top of taste; see WoodGrain.
        private const float BoardSheetSpan = 4.5f;

        // The playable surface, solved from pixels rather than taste. Sampling the 2026-08-25
        // r2 render against docs/reference/target-01-tabletop.png: our board interior rendered
        // (176,103,53), r/b 3.32, luma 119; the target's is (194,122,84), r/b 2.31, luma 139 —
        // ours was both darker and far more orange, which is why navy rail on it read as line
        // art on brown instead of track on a pale bed. Inverting this rig's measured linear
        // light transfer (r 1.035, g 0.642, b 0.337 — the amber key does the warming) for the
        // target's colour gave a desaturated albedo near (0.78, 0.63, 0.57), on the reasoning
        // that the key supplies the warmth and an already-orange albedo compounds into
        // terracotta.
        //
        // That value was right FOR THAT LIGHT and is superseded: BoardSceneLook's rig has been
        // rebalanced so cool colours can render cool, which took the amber out of the key. The
        // warmth has to live in the albedo now, which is how target-01 does it — dividing its
        // board out by its own illuminant gives roughly (207, 144, 101).
        //
        // Solved, not tasted. rendered = S * (albedo_linear * grain_linear + 0.0254), so
        // choosing the render fixes the albedo:
        //     albedo = ((rendered / S) - 0.0254) / grain_linear
        // grain_linear is the sheet's median texel, 0.9529 sRGB = 0.8963 linear.
        //
        // Round 1 held the board where it already was. Round 2 aims it at target-01 instead,
        // now that slot 6 has measured the real game camera: the board rendered
        // (201, 129, 87) against target-01's (202, 134, 89), so it was ~4 luminance units
        // shy, mostly in green. Solving against the round-2 illuminant — the round-1 value
        // corrected by the (0.946, 1.020, 1.011) that the cream ballast asked for, see
        // BoardSceneLook — gives (0.823, 0.555, 0.374), predicted to render (202, 134, 89)
        // exactly. Contrast against the CreamCard ballast stays at 0.328, well over the
        // 0.262 floor.
        private static readonly Color WarmWood = new Color(0.823f, 0.555f, 0.374f);
        // Walnut for the room-scale desk. Calibrated against the 2026-08-25 slot render:
        // the amber key plus warm ambient multiply channel ratios by roughly (1.15 r/g,
        // 1.84 r/b), so a red-leaning albedo (the old 0.55/0.36/0.22, r/b 2.5) rendered as
        // burnt-orange terracotta (measured r/b ~4.6 vs the target desk's ~2.5). To land on
        // target-01's rich brown the albedo must be a desaturated walnut and let the light
        // supply the warmth. The DeskGrain sheet multiplies this toward ~1.05x at the board
        // and ~0.46x cooler at the frame corners.
        // Re-solved through the same inversion as WarmWood, for the same reason: the rig's
        // amber is gone, so the walnut has to be walnut in the albedo. rendered stays at the
        // measured (120, 74, 46); grain_linear is the desk sheet's median near the board
        // (0.8225 * 1.05 lum = 0.8636 sRGB). The desk's own warm-centre/cool-edge falloff
        // survives untouched, and now reads more clearly than it did under a light that was
        // already staining the whole slab amber.
        private static readonly Color WarmDesk = new Color(0.495f, 0.299f, 0.137f);
        private static Mesh _cubeMesh;
        private static Texture2D _woodGrain;
        private static Texture2D _deskGrain;

        public static Transform Build(ImportedLevel level, Transform parent)
        {
            var nodes = level.Dto.Nodes.ToArray();
            if (nodes.Length == 0) return null;

            float minX = nodes.Min(node => node.X);
            float maxX = nodes.Max(node => node.X);
            float minY = nodes.Min(node => node.Y);
            float maxY = nodes.Max(node => node.Y);
            float width = maxX - minX + MarginSide * 2f;
            float height = maxY - minY + MarginNear + MarginFar;
            // `center` stays the AUTHORED node centre. Two things depend on that and must not
            // follow the slab: the desk's radial warmth pool (its lamp light belongs over the
            // play area, not over the slab's new centre of mass) and the two contact-plane
            // markers BoardPropDecorator reads, whose x/y are unused but whose identity as
            // "the middle of the level" is the thing the prop lane reasons about.
            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            // The slab is no longer symmetric about the play area: it reaches MarginFar away
            // from the camera and only MarginNear toward it, so its own centre sits half the
            // difference behind the node centre.
            var bodyCenter = new Vector2(center.x, minY - MarginNear + height * 0.5f);

            var body = new GameObject("BoardBody").transform;
            body.SetParent(parent, false);

            // LOOK step 5 needs desk dressing to stand outside the raised board. This larger,
            // rearward slab is the room-scale desk; the board remains the finite rimmed toy.
            var desk = new GameObject("DeskSurface").transform;
            desk.SetParent(parent, false);
            // One sheet, mapped once over DeskSheetSpan world units centred on the board (not
            // stretched across the whole slab): the desk needs broad plank grain plus a radial
            // warmth falloff, and a repeating tile cannot carry falloff. Outside that span the
            // Clamp wrap holds the sheet's dark edge, so the slab can overhang as far as the
            // frame needs without thinning the grain or dragging the vignette off screen.
            float deskWidth = width + DeskOverscan;
            float deskHeight = height + DeskOverscan;
            var deskST = new Vector4(deskWidth / DeskSheetSpan, deskHeight / DeskSheetSpan,
                0f, 0f);
            deskST.z = 0.5f - 0.5f * deskST.x;
            deskST.w = 0.5f - 0.5f * deskST.y;
            CreatePart("DeskTop", desk,
                new Vector3(center.x, center.y - 0.15f, DeskFront + DeskDepth * 0.5f),
                new Vector3(deskWidth, deskHeight, DeskDepth), WarmDesk,
                grain: DeskGrain(), grainST: deskST);

            // Scene/prop seam: generated FBXs were floored before export, so their holder
            // origin is their feet. Publish the exact camera-facing wood plane before the
            // decorator runs; this keeps them seated as the slab depth changes.
            var contactPlane = new GameObject(BoardPropDecorator.ContactPlaneMarkerName).transform;
            contactPlane.SetParent(parent, false);
            contactPlane.localPosition = new Vector3(center.x, center.y, WoodFront);
            var deskContactPlane = new GameObject(
                BoardPropDecorator.DeskContactPlaneMarkerName).transform;
            deskContactPlane.SetParent(parent, false);
            deskContactPlane.localPosition = new Vector3(center.x, center.y, DeskFront);

            CreatePart("NavyBase", body,
                new Vector3(bodyCenter.x, bodyCenter.y, 1.18f),
                new Vector3(width + 0.24f, height + 0.24f, 0.34f), Palette.InkNavy);
            CreatePart("WoodTop", body,
                new Vector3(bodyCenter.x, bodyCenter.y, WoodFront + WoodDepth * 0.5f),
                new Vector3(width, height, WoodDepth), WarmWood,
                grain: WoodGrain(),
                grainST: new Vector4(width / BoardSheetSpan, height / BoardSheetSpan, 0f, 0f));

            var rim = new GameObject("CreamRim").transform;
            rim.SetParent(body, false);
            float rimZ = WoodFront - RimDepth * 0.5f - 0.01f;
            CreatePart("Top", rim,
                new Vector3(bodyCenter.x, bodyCenter.y + height * 0.5f - RimWidth * 0.5f, rimZ),
                new Vector3(width, RimWidth, RimDepth), Palette.CreamCard);
            CreatePart("Bottom", rim,
                new Vector3(bodyCenter.x, bodyCenter.y - height * 0.5f + RimWidth * 0.5f, rimZ),
                new Vector3(width, RimWidth, RimDepth), Palette.CreamCard);
            CreatePart("Left", rim,
                new Vector3(bodyCenter.x - width * 0.5f + RimWidth * 0.5f, bodyCenter.y, rimZ),
                new Vector3(RimWidth, height - RimWidth * 2f, RimDepth), Palette.CreamCard);
            CreatePart("Right", rim,
                new Vector3(bodyCenter.x + width * 0.5f - RimWidth * 0.5f, bodyCenter.y, rimZ),
                new Vector3(RimWidth, height - RimWidth * 2f, RimDepth), Palette.CreamCard);
            return body;
        }

        // One primitive site and one explicit project-material bind keep every runtime renderer
        // on the project's URP shader. A property block supplies each tint without allocating
        // renderer-local material instances across Retry and LoadNext rebuilds.
        private static Transform CreatePart(string name, Transform parent, Vector3 position,
            Vector3 scale, Color color, Texture2D grain = null, Vector4? grainST = null)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var filter = part.AddComponent<MeshFilter>();
            if (_cubeMesh == null)
                _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            filter.sharedMesh = _cubeMesh;
            var renderer = part.AddComponent<MeshRenderer>();
            var basis = GreyboxMaterial.Shared;
            if (basis != null)
            {
                renderer.sharedMaterial = basis;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                if (grain != null)
                {
                    Vector4 st = grainST ?? new Vector4(1f, 1f, 0f, 0f);
                    properties.SetTexture("_BaseMap", grain);
                    properties.SetTexture("_MainTex", grain);
                    properties.SetVector("_BaseMap_ST", st);
                    properties.SetVector("_MainTex_ST", st);
                }
                renderer.SetPropertyBlock(properties);
            }
            return part.transform;
        }

        // The board's own grain, at board scale. Same procedural-sheet technique as DeskGrain
        // — one CPU-built greyscale sheet multiplying the albedo — retuned for the smaller,
        // nearer object. The sheet it replaces was a single ten-cycle sine spanning 6 world
        // units at 10% amplitude, which is why the WoodTop read as a smooth gradient while
        // target-01's board is visibly grained.
        //
        // Four things changed, all of them about reading as wood rather than about strength:
        //   1. Pitch. 8 grain bands per BoardSheetSpan = 0.5625 world units a band, against
        //      the desk's 0.96. At the fitted ortho size the board's local +X projects at
        //      ~101 px per unit on a 917px frame (0.8705 foreshortening x 115.7
        //      px/world-unit, measured off the 2026-08-25 r3 render), so a band is ~57px and
        //      a grain valley pair ~13px. Fine figure, not corduroy.
        //      The count MUST divide `size` exactly, and that is not cosmetic. It was 9,
        //      giving 256/9 = 28.4 texels a band: every interior band boundary then falls
        //      BETWEEN texels, so no texel reaches the seam notch's bottom — except the one
        //      at u = 0, where `across` is exactly 0 and `seam` hits its maximum. That made
        //      the wrap column the single darkest seam in the sheet and put a visible line
        //      across the board once per tile. Measured: wrap step 0.0980 against a worst
        //      interior plank seam of 0.0617, i.e. 1.59x. At 8 bands every boundary is
        //      texel-aligned and identical, and the wrap step falls to 0.0344 — 1.11x the
        //      average interior step and 0.44x the worst plank seam. Note this is the
        //      opposite of DeskGrain's "odd plank count on purpose": that one is Clamped and
        //      mapped once, so it never wraps and only cares about where its centre probe
        //      lands. Do not copy its rationale here.
        //   2. Structure. Narrow cubed-falloff valleys and thin plank seams instead of a
        //      smooth sine. Sharp features read as grain at far lower amplitude than a slow
        //      wave does, which is how this stays subtle enough for the pale ballast ribbon
        //      to keep sitting on top of it rather than fighting it.
        //   3. Tileable. Every term is period-1 in u and v — integer-frequency sines, and a
        //      wrapped value noise in place of Mathf.PerlinNoise, which has no period. The
        //      desk sheet gets away with Perlin only because it is Clamped and mapped once.
        //      Being period-1 is necessary but NOT sufficient: see the texel-alignment note
        //      under 1, which is what actually bit. Verify a change here by measuring the
        //      wrap step, not by reasoning about periodicity — and measure it in float32.
        //      `hash` is Mathf.Abs(Mathf.Sin(band * 12.9898f) * 43758.547f) % 1f, and at a
        //      magnitude near 43758 a float32 resolves ~0.004, so the fraction it keeps is
        //      nothing like the double-precision one. Band 7 hashes to 0.988 in float32 and
        //      0.166 in float64 — a 15.6 radian difference in the ripple phase it feeds.
        //   4. Normalised to a 0.99 ceiling. The sheet may only darken, so it cannot close
        //      the gap that separates the CreamCard ballast from the board — see
        //      BoardLookTests.BoardGrain_NeverErodesTheBallastContrast for the arithmetic.
        // Mean texel lands at 0.9369 against the old sheet's 0.9375, so the board's average
        // brightness — and with it the measured colour solve above — survives the change;
        // the median rises to 0.9529 because the darkening is now concentrated in thin lines.
        // Both figures are float32, matching what Unity actually builds.
        // Stays CPU-readable (one 256px copy, ~196KB) so the look tests can pin the ceiling.
        private static Texture2D WoodGrain()
        {
            if (_woodGrain != null) return _woodGrain;
            const int size = 256;
            const float bands = 8f;
            _woodGrain = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "Cat Metro Board Grain",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float bandU = u * bands;
                    int band = Mathf.FloorToInt(bandU);
                    float hash = Mathf.Abs(Mathf.Sin(band * 12.9898f) * 43758.547f) % 1f;
                    float across = bandU - band;
                    float edgeTexels = Mathf.Min(across, 1f - across) * (size / bands);
                    float seam = Mathf.Clamp01(1f - edgeTexels / 1.4f);

                    // Wander the grain along the band so the lines are wood, not pinstripe.
                    // Both terms wrap, so the phase they feed the ripple stays seamless.
                    float wobble = (TileNoise(u, v, 4) - 0.5f) * 2.2f
                        + 0.55f * Mathf.Sin(v * Mathf.PI * 2f + hash * 19f);
                    // 36 cycles across the sheet: an integer count, so u wraps cleanly.
                    float ripple = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 72f
                        + hash * 19f + wobble);
                    float valley = (1f - ripple) * (1f - ripple) * (1f - ripple);
                    float fleck = TileNoise(u, v, 32);
                    float value = 0.99f - 0.110f * valley - 0.014f * fleck - 0.016f * hash;
                    value *= 1f - seam * 0.09f;
                    byte channel = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
                    pixels[y * size + x] = new Color32(channel, channel, channel, 255);
                }
            }
            _woodGrain.SetPixels32(pixels);
            _woodGrain.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return _woodGrain;
        }

        // Wrapped value noise on an integer lattice, using the same hash idiom the band
        // variation uses. Exists because the board sheet repeats and Mathf.PerlinNoise does
        // not tile: a Perlin term would put a hard discontinuity across the board wherever
        // the sheet wraps.
        private static float TileNoise(float u, float v, int cells)
        {
            float x = u * cells;
            float y = v * cells;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = LatticeHash(x0, y0, cells);
            float b = LatticeHash(x0 + 1, y0, cells);
            float c = LatticeHash(x0, y0 + 1, cells);
            float d = LatticeHash(x0 + 1, y0 + 1, cells);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float LatticeHash(int x, int y, int cells)
        {
            int wx = ((x % cells) + cells) % cells;
            int wy = ((y % cells) + cells) % cells;
            return Mathf.Abs(Mathf.Sin(wx * 12.9898f + wy * 78.233f) * 43758.547f) % 1f;
        }

        // The room-scale desk sheet. Four jobs in one texture, all judged against
        // docs/reference/target-01-tabletop.png at phone scale (the sheet spans ~40 world
        // units; the portrait frame shows ~6.3, so one texel covers ~11 screen px):
        //   1. Planks. 27 boards across the sheet (~1.5 world units each) with soft dark
        //      seams ~2 texels wide (~20 screen px) and per-plank phase/value variation,
        //      so the desk reads as built furniture, not a colour field. The first sheet
        //      had no plank structure and read as smooth terracotta in the slot render.
        //      Odd plank count on purpose: it keeps u=0.5 mid-plank, so the look test's
        //      centre probe never lands on a seam.
        //   2. Grain. Narrow dark valleys (~0.6 world units apart, cubed-falloff profile)
        //      against broad light plank bodies — the first sheet's low-amplitude sine
        //      washed out to faint streaks.
        //   3. Radial warmth falloff — bright warm wood at the board fading to a darker,
        //      cooler edge, the target's lamp-pool vignette.
        //   4. Fake depth of field — grain and seam contrast fade with radius, so the desk
        //      softens toward the frame edges. True URP DoF post needs a Volume + depth
        //      texture and meaningful mobile cost on this ortho rig; baking the defocus
        //      into the one sheet costs nothing per frame. Tradeoff: the falloff is static
        //      (it cannot react to lighting changes) and covers only the desk, which
        //      overscans well past the portrait frame anyway.
        // Stays CPU-readable (one 512px copy) so the look tests can pin the falloff law.
        private static Texture2D DeskGrain()
        {
            if (_deskGrain != null) return _deskGrain;
            const int size = 512;
            const float planks = 27f;
            _deskGrain = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "Cat Metro Desk Grain",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float dx = u - 0.5f;
                    float dy = v - 0.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy) * 1.41421f;
                    float fall = Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01((radius - 0.22f) / 0.72f));
                    float focus = 1f - 0.75f * fall;

                    float plankU = u * planks;
                    int plank = Mathf.FloorToInt(plankU);
                    float hash = Mathf.Abs(Mathf.Sin(plank * 12.9898f) * 43758.547f) % 1f;
                    float acrossPlank = plankU - plank;
                    float edgeTexels = Mathf.Min(acrossPlank, 1f - acrossPlank)
                        * (size / planks);
                    float seam = Mathf.Clamp01(1f - edgeTexels / 1.2f);
                    float seamMul = 1f - seam * 0.32f * (1f - 0.7f * fall);

                    float bend = (Mathf.PerlinNoise(u * 2.2f, v * 2.2f) - 0.5f) * 3f
                        + 0.8f * Mathf.Sin(v * Mathf.PI * 3f);
                    float band = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 64f
                        + hash * 17f + bend + 0.9f * Mathf.Sin(v * Mathf.PI * 2.3f));
                    float valley = (1f - band) * (1f - band) * (1f - band);
                    float streak = Mathf.PerlinNoise(u * 40f, v * 3.1f);
                    float value = (0.78f + (band * 0.14f - valley * 0.22f
                        + (streak - 0.5f) * 0.10f + (hash - 0.5f) * 0.08f) * focus)
                        * seamMul;

                    float lum = Mathf.Lerp(1.05f, 0.46f, fall);
                    float warm = Mathf.Lerp(1.02f, 0.90f, fall);
                    float cool = Mathf.Lerp(0.97f, 1.06f, fall);
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(value * lum * warm) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(value * lum) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(value * lum * cool) * 255f),
                        255);
                }
            }
            _deskGrain.SetPixels32(pixels);
            _deskGrain.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return _deskGrain;
        }
    }
}
