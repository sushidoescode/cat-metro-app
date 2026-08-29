using System.Collections.Generic;
using CatMetro.Presentation.Props;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatMetro.Presentation.Board
{
    /// <summary>Owns the scene-wide visual treatment shared by every authored level.</summary>
    public static class BoardSceneLook
    {
        public static readonly Color WarmBackground = new Color(0.85f, 0.81f, 0.73f);

        private const string KeyLightName = "Diorama Warm Key";
        private const float TargetPortraitAspect = 9f / 19.5f;
        // These fractions serve the safe-frame law in RuntimeSceneRigTests.AssertInside:
        // viewport x in (0.055, 0.945), y in (0.12, 0.87), asserted at the pinned phone
        // aspect 917/2048 (~0.4478). The fit must assume an aspect before the camera knows
        // its real surface, and TargetPortraitAspect (~0.4615) is wider than the pinned
        // one, which squeezes content outward at assertion time. With the 1.05 pad:
        //   x extremes = 0.5 +/- TargetAspect*SafeWidth / (2*1.05*0.4478) -> [0.068, 0.932]
        //   y extremes = 0.495 +/- SafeHeight / (2*1.05)                  -> [0.133, 0.857]
        // ~0.013 inside the law on every edge. The old 0.93/0.78 put x extremes at 0.9565
        // (outside the law — the furnished-board signpost failure) and passed vertically by
        // only 0.0036. Do not widen these without re-deriving both bands.
        private const float SafeWidth = 0.88f;
        private const float SafeHeight = 0.76f;
        // THE COMPOSITION ALTERNATIVE, isolated here as a one-line flip so a render can
        // compare it: set this to -16f. Reducing the yaw is the other documented way to buy
        // horizontal fill, and it does NOT pay what an earlier lane reported. The mechanism,
        // with the tilt matrix written out (Unity applies Euler ZXY, so M = Ry*Rx*Rz):
        //
        //   yaw -32:  |M00| 0.86874   |M01| 0.26630   |M02| 0.41758
        //   yaw -16:  |M00| 0.97080   |M01| 0.10220   |M02| 0.21720
        //
        // A renderer's world-x half-extent is |M00|ex + |M01|ey + |M02|ez, so lowering the
        // yaw makes the board's long local-Y axis 2.6x cheaper in frame width — but it also
        // makes local X 12% MORE expensive, and the stations sit at the extremes of local X.
        // On L001 those cancel almost exactly: the post-split gameplay contentBounds goes
        // 6.126 -> 6.048, a 1.013x fill, against the 1.116x the law split alone buys.
        // (Reconstructing the pre-split figure the same way gives ~1.10x, not the ~1.23x an
        // earlier lane reported; that number cannot be right for a 4-wide, 7-tall board.)
        // The yaw lever pays on tall, NARROW levels, costs fill on wide ones such as L008,
        // and it moves the key-light incidence law in ApplyLighting, which is calibrated
        // against this exact 38/-32/-4. That is why it is the alternative and not the choice.
        private const float DioramaYaw = -32f;
        // Public because the diorama tilt is the ONLY thing that decides which way a board-local
        // feature faces the (identity-rotated, orthographic) camera. ToyTrainView derives the
        // cat's fixed facing from it rather than hardcoding a yaw that would silently rot if
        // this tilt were ever re-authored.
        public static readonly Quaternion BoardTilt = Quaternion.Euler(38f, DioramaYaw, -4f);

        // Declared below BoardTilt on purpose: feat/cats-on-trains inserts its comment block
        // at exactly that line, and an insertion of our own at the same point turns two
        // compatible edits into a merge conflict.
        //
        // The fit reaches this far in when a level's content is small. Lowered from 7 with
        // the content-driven width fit below: that fit asks for ~20-25% less size than the
        // slab-driven one did, so a floor of 7 would have become the binding constraint on
        // most levels and silently cancelled the change.
        private const float MinOrthoSize = 5f;
        // Widest portrait aspect the desk's near-plane clearance is solved for. 4:3 covers
        // every phone (the pinned one is 917/2048 ~ 0.448) and tablets too. Solving at 16:9
        // left a 4:3 frame's lower corners 0.1-0.3 units behind the near plane; solving here
        // costs ~1.2 units of camera pull-back, which the smaller frame now affords.
        private const float DeskCoverageAspect = 0.75f;
        private const float DeskNearClearance = 1f;

        public static void Apply(Transform owner, Camera camera, BoardView board)
        {
            var environment = owner.GetComponent<BoardSceneEnvironmentScope>();
            if (environment == null)
                environment = owner.gameObject.AddComponent<BoardSceneEnvironmentScope>();
            environment.Capture();
            ApplyBackground(camera);
            TiltAroundAuthoredCenter(board);
            FitCamera(camera, board);
            ApplyLighting(owner);
        }

        public static void ApplyBackground(Camera camera)
        {
            RenderSettings.skybox = null;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WarmBackground;
        }

        private static void TiltAroundAuthoredCenter(BoardView board)
        {
            Vector3 center = board.PresentationCenterLocal;
            board.transform.localRotation = BoardTilt;
            board.transform.localPosition = center - BoardTilt * center;
        }

        private static void FitCamera(Camera camera, BoardView board)
        {
            // Two unions, because the frame owes them different things.
            //   frameBounds   — everything except the desk. Nothing here may leave the frame
            //                   vertically, so the toy's rim still reads as a finite edge.
            //   contentBounds — GAMEPLAY only: the same minus the decorative slab (BoardBody)
            //                   and minus every renderer under a decorative prop. This is
            //                   what the RuntimeSceneRigTests horizontal safe-frame law
            //                   governs after the split described below.
            // The slab is ~1.25x wider on screen than the content it carries, so fitting the
            // slab into the horizontal safe band shrank the whole diorama to satisfy a border
            // no law asks about. Target-01 lets the board run off both edges; so do we now.
            //
            // THE LAW SPLIT, and what it is worth. Measured off
            // .catshots/orchestrator-2026-08-25-r6/rigA-board-train-midedge.png (L001), whose
            // three unclipped rim corners solve the projection exactly at 115.92 px per world
            // unit, cameraX 3.297, cameraY 5.192, orthographicSize 8.834 — reproducing the
            // 115.7 and 8.85 this file already quoted from the r3 render:
            //
            //   contentBounds was world x [-0.120, 6.714], size 6.834.
            //   The RIGHT extreme is the BLU station kiosk: its blue roof's silhouette ends
            //   at world x 6.571 and its AABB reaches 6.714. That is gameplay and it stays.
            //   The LEFT extreme is the perimeter-trees cluster. Its foliage silhouette
            //   begins at world x 0.187 and NOTHING else outside the slab is left of it, so
            //   the 0.307 gap down to -0.120 is that cluster's own AABB inflation (renderer
            //   bounds are world-axis-aligned, and every board child is tilted 38/-32/-4, so
            //   an AABB always overreaches its silhouette).
            //
            // A decorative tree was therefore setting the size of the entire diorama. With it
            // out, the leftmost gameplay renderer is the depot shed, silhouette at world x
            // 0.895; applying the same 0.307 inflation puts its AABB near 0.588, so
            //   contentBounds.size.x 6.834 -> 6.126, and size 8.834 -> 7.919: a 1.116x fill.
            // Board coverage of the frame goes 26.5% -> 32.7% on that alone, and 48.1% once
            // BoardSurface's anisotropic margin (which this split is what pays for) lands.
            // The cat scales only with the camera, so it goes 3.2-3.6% -> 3.57-4.02% of frame
            // width against the target's 5-6%: the slab does nothing for cat legibility, and
            // nothing on the camera side can, because the gameplay band is already full.
            //
            // This WIDENS what may leave frame. Trees, bushes, the desk clutter and the
            // parked engine may now cross the band that stations, sources, node markers,
            // switches and trains still may not. RuntimeSceneRigTests states both halves
            // explicitly: the gameplay band is unchanged and still fails if a node or a
            // station leaves it, and the decorative band is its own, wider, asserted rule
            // rather than an absence of one.
            Bounds frameBounds = default, contentBounds = default;
            bool foundFrame = false, foundContent = false;
            var deskSurface = board.transform.Find("DeskSurface");
            var slab = board.transform.Find("BoardBody");
            var decorative = new List<Transform>();
            foreach (var prop in board.GetComponentsInChildren<BoardPropInstance>(true))
                if (prop.IsDecorative) decorative.Add(prop.transform);
            foreach (var renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                if (!foundFrame) { frameBounds = renderer.bounds; foundFrame = true; }
                else frameBounds.Encapsulate(renderer.bounds);
                if (slab != null && renderer.transform.IsChildOf(slab)) continue;
                if (IsUnderAny(renderer.transform, decorative)) continue;
                if (!foundContent) { contentBounds = renderer.bounds; foundContent = true; }
                else contentBounds.Encapsulate(renderer.bounds);
            }

            if (!foundFrame)
            {
                Vector3 center = board.transform.TransformPoint(board.PresentationCenterLocal);
                frameBounds = new Bounds(center, Vector3.one);
            }
            // A board with no gameplay renderers at all still has to frame something. This
            // is reachable in two ways now, not one: a board with no renderers, and — more
            // realistically — a level whose only non-slab renderers were decorative.
            if (!foundContent) contentBounds = frameBounds;

            float requiredForHeight = frameBounds.size.y * 0.5f / SafeHeight;
            float requiredForWidth = contentBounds.size.x * 0.5f
                / (TargetPortraitAspect * SafeWidth);
            float size = Mathf.Max(MinOrthoSize,
                Mathf.Max(requiredForHeight, requiredForWidth) * 1.05f);
            float safeCenterY = (0.13f + 0.86f) * 0.5f;
            // Centre X on the content the law governs, not on the slab that may be lopsided
            // around it; centre Y on the full frame so the rim stays inside top and bottom.
            //
            // KNOWN, MEASURED, NOT FIXED HERE: the slab bleeds off the LEFT frame edge only,
            // where target-01 bleeds both. It is a consequence of this line, and the fix is
            // not available on the camera side. Measured off the 2026-08-25 r3 render (L001):
            // the rim's two long edges sit 600px apart, which against their 5.186-unit
            // horizontal separation puts the frame at 115.7 px/world-unit and the fit at
            // orthographicSize 8.85. The slab's AABB is 8.137 units = 941px against a 917px
            // frame, so centred it would clear each edge by only 12px. But the board's centre
            // projects to screen x 411 against the frame's 458.5 — contentBounds is offset
            // 0.41 units from the slab it sits on, because props spread asymmetrically. Left
            // overhang becomes 12 + 47.5 = 60px (bleeds, and the rim does clip at x=0 for
            // rows 588-680); right becomes 12 - 47.5, so it falls ~35px short and the rim
            // stops at x=857.
            //
            // Re-centring on the slab is the obvious fix and it breaches the safe-frame law:
            // 0.41 units is 0.0517 of viewport width here, against the 0.013 of slack the
            // derivation above leaves on each edge — 4x over. That law was breached once this
            // week already and SafeWidth/SafeHeight are the fix.
            //
            // What would work is a wider slab, which is free against the width fit now that
            // contentBounds excludes BoardBody. The slab's AABB has to reach
            // 2 * (3.965 + 0.41) = 8.75 units, and d(AABB.x)/d(Margin) = 2.27, so
            // BoardSurface.Margin would go 1.05 -> ~1.32 bare, ~1.56 with headroom. Vertically
            // that is affordable (requiredForHeight * 1.05 reaches 6.05 against the fitted
            // 8.85; it would have to reach 12.8 to take over the fit). Two reasons it is not
            // done here: it widens the toy's wood border by ~50%, which moves overall board
            // fill — reserved for the human — and the 0.41 offset is per-level, so one
            // constant would be an unverifiable guess on the other 16 levels.
            float cameraX = contentBounds.center.x;
            float cameraY = frameBounds.center.y - (safeCenterY - 0.5f) * 2f * size;

            float cameraZ = -10f;
            float farthestZ = frameBounds.max.z;
            foreach (var renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                cameraZ = Mathf.Min(cameraZ, renderer.bounds.min.z - DeskNearClearance);
            }

            // The desk is a huge tilted slab, so its bounding box dips toward the camera far
            // outside the frame. Pulling the camera back to clear that off-screen corner cost
            // real shadow-distance budget (it put the board ~20 units deep) while clearing the
            // *visible* desk by only ~0.06 units at the pinned aspect — accidental, not safe.
            // Solve the near plane against the desk plane where it is actually on screen.
            var deskTop = deskSurface != null ? deskSurface.Find("DeskTop") : null;
            if (deskTop != null)
            {
                Vector3 normal = deskTop.forward;
                if (Mathf.Abs(normal.z) > 0.01f)
                {
                    Vector3 face = deskTop.TransformPoint(new Vector3(0f, 0f, -0.5f));
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float x = cameraX + ((corner & 1) == 0 ? -1f : 1f)
                            * size * DeskCoverageAspect;
                        float y = cameraY + ((corner & 2) == 0 ? -1f : 1f) * size;
                        float z = face.z
                            - (normal.x * (x - face.x) + normal.y * (y - face.y)) / normal.z;
                        cameraZ = Mathf.Min(cameraZ, z - DeskNearClearance);
                    }
                }
            }
            if (deskSurface != null)
                foreach (var renderer in deskSurface.GetComponentsInChildren<Renderer>(true))
                    farthestZ = Mathf.Max(farthestZ, renderer.bounds.max.z);

            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.transform.rotation = Quaternion.identity;
            camera.transform.position = new Vector3(cameraX, cameraY, cameraZ);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(50f, farthestZ - cameraZ + 1f);
            camera.allowHDR = false;
            // Inert today: Assets/Settings/CatMetro_URP.asset has m_MSAA: 1, i.e. one sample.
            // Left set so the camera does not have to be revisited if that asset ever moves.
            camera.allowMSAA = true;

            // Depth-of-field stand-in. Sized from what the fit just solved, so the hole it
            // cuts is exactly the region gameplay can occupy and no triangle ever lands on
            // the diorama. See DefocusVeil for why real URP DoF is unavailable here rather
            // than merely expensive. Half-height is measured off frameBounds about the final
            // camera, taking the larger side so an off-centre toy still clears the band.
            float contentHalfHeight = Mathf.Max(
                Mathf.Abs(frameBounds.max.y - cameraY), Mathf.Abs(cameraY - frameBounds.min.y));
            DefocusVeil.Apply(camera, size, contentHalfHeight);
        }

        private static bool IsUnderAny(Transform candidate, List<Transform> roots)
        {
            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null && candidate.IsChildOf(roots[i])) return true;
            return false;
        }

        private static void ApplyLighting(Transform owner)
        {
            // Cool fill under a warm key. This is the inverse of what stood here — amber sky
            // over cool ground — and the inversion is the fix for a class-level bug, not a
            // taste change.
            //
            // What was wrong. Measured off the 2026-08-25 r3 render by fitting two COPLANAR
            // surfaces of known albedo (the WoodTop and the CreamRim share the board's normal,
            // so albedo is the only variable between them), the whole rig reduces to
            //
            //     rendered_linear = S * (albedo_linear + 0.0254)
            //
            // with S = (1.17, 0.678, 0.354) — blue attenuated to 0.303 of red. The additive
            // 0.0254 is itself proportional to S in all three channels, so it is the light's
            // own colour, not a neutral floor. Consequences, all measured on that render:
            // CreamCard rendered (255, 200, 138) with red CLIPPED and b-r -117; InkNavy rails
            // rendered (57, 52, 51); the MetroTeal switch wedge rendered (121, 130, 94), an
            // olive. target-01 does none of this: its cream reads (243, 226, 197) unclipped
            // with b-r -46, its rails (69, 75, 95) and its navy roofs (52, 58, 80) at b-r +28,
            // its teal (51, 120, 115) at b-r +64. Dividing the target's cream by a CreamCard
            // albedo puts its illuminant near (1.00, 0.91, 0.78): warm, but nowhere near the
            // amber stamp ours was applying. The target's warmth lives in its ALBEDOS — its
            // board divides out to roughly (207, 144, 101) — while its light stays close to
            // neutral. Ours had it backwards, and a light that starved of blue cannot render
            // any cool albedo cool. docs/LOOK.md names navy and teal in the palette, so this
            // was defeating the art direction at the class level, not just on the rails.
            //
            // Why the ambient was the wrong half of it. The board's visible normal is
            // (0.418, 0.616, -0.668) — n.y = 0.616, so every surface the player looks at is
            // upward-facing and draws almost entirely on the SKY band. The cool ground colour
            // was only ever reaching downward faces, which is to say nothing. Cooling the fill
            // means cooling the sky.
            //
            // What this is. Sky and equator go to a cool blue-grey; the ground stays warm,
            // which is physically what it is — bounce off a wooden desk. Intensity rises to
            // 1.15 to hold the fill's share of the total. With the key below, the illuminant
            // becomes S = (1.063, 0.890, 0.738), i.e. blue at 0.694 of red against 0.303.
            // Predicted from the measured transfer: InkNavy renders (59, 62, 74), b-r +15;
            // MetroTeal (77, 170, 151), b-r +74; CreamCard (252, 225, 193), b-r -59 and no
            // longer clipping. The warm mood is not spent — it moves into the wood albedos
            // (re-solved in BoardSurface so the board still renders (204, 130, 87), which is
            // target-01's board to within three units) and into the warm/cool split that a
            // cool fill under a warm key finally makes possible. Before this, lit and shaded
            // faces were both amber, which is the flatness the raking key was fighting alone.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.59f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.47f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.41f, 0.37f, 0.33f);
            RenderSettings.ambientIntensity = 1.15f;

            // What this light cannot fix, from slot 7's measurement of the real boot seam.
            // Both figures below are measured off the art, not assumed: target-01's rails are
            // b-r +27 and its teal b-r +74. Earlier revisions of this note used +25 and +60,
            // and the teal figure in particular was wrong enough to invert its conclusion.
            //
            // Cream and board land on target. The two that do not:
            //
            //   * RAILS are an albedo problem, and this survived every correction. Measured
            //     (51, 59, 73) at luminance 58 against target-01's (70, 76, 97) at 76 — they
            //     need x1.75 in linear. Cream, the one fixed-albedo reference sitting on
            //     target, needs x1.02, so it holds the exposure and no global lift can give
            //     the rails their 75% without blowing the cream out. Their key visibility is
            //     ~0.85, so there is no shadow to lift either (see shadowStrength). Hitting
            //     (70, 76, 97) under this rig needs an albedo near (64, 73, 105), about 2.3x
            //     InkNavy in linear. InkNavy is shared with outlines and text where a dark
            //     navy is correct, so this wants a rail-specific token. The track lane's
            //     RailNavy was right on LEVEL; only the HUE argument for it went away.
            //
            //   * TEAL is a HUE problem local to that object, not a level problem, and not
            //     the palette question an earlier revision of this note claimed. Against the
            //     corrected target its level is already fine — luminance 147 against 155.
            //     What is off is b-r +102 against +74: it needs (x1.41, x1.11, x0.83), more
            //     red and LESS blue. The cause is orientation, not the illuminant: the teal
            //     wedge's effective illuminant divides out 1.41x bluer than the cream's, so
            //     its normal is collecting far more of the cool sky band than the board's
            //     does. A global fill change cannot serve it, because cream (+2) and rails
            //     (+5) both want MORE blue while teal wants 28 points less. Narrowing the
            //     sky-to-ground hue spread in the trilight above would reduce how much a
            //     surface's tilt changes its colour, and that is the lever if this is worth
            //     another pass — but it trades against the warm ground bounce, and nothing
            //     has measured whether the wedge or the trees are the offender.

            var existing = owner.Find(KeyLightName);
            var key = existing == null
                ? new GameObject(KeyLightName).AddComponent<Light>()
                : existing.GetComponent<Light>();
            if (existing == null) key.transform.SetParent(owner, false);
            key.type = LightType.Directional;
            // Warm, but not amber enough to stamp its own hue on every albedo in the game.
            // integration/look-stack's key is (1, 0.82, 0.67) @ 1.05, which Unity linearises
            // to (1, 0.726, 0.407) — blue at 0.41 of red. Combined with a fill that was ALSO
            // warm (see ApplyLighting's note), the illuminant reached the board at
            // (1.17, 0.678, 0.354), blue at 0.303, and no cool albedo could survive it.
            //
            // (Earlier revisions of this comment quoted (1, 0.78, 0.56) @ 1.18 as the
            // baseline. That was this branch's own round-1 value, not the merge base. The
            // transfer function above was fitted from a capture taken under it, which is why
            // its predictions landed but its stated baseline was wrong. Corrected here.)
            //
            // This value linearises to (1, 0.861, 0.612): blue at 0.61 of red, still
            // unmistakably late-afternoon against a cool fill, but it lets a cool albedo stay
            // cool. Intensity 1.05 -> 0.957 because the fill now carries more of the total.
            //
            // Round-2 calibration, from slot 6's measurement of the real game camera. Cream
            // ballast — CreamCard, a fixed albedo on a lit surface, so it pins the illuminant
            // without any albedo of mine in the loop — rendered (249, 224, 196) against
            // target-01's (243, 226, 197), i.e. the rig was 5.4% hot in red and ~2% shy in
            // green and blue. Correcting by (0.946, 1.020, 1.011) and folding it into the key
            // took (1, 0.90, 0.78) @ 1.02 to the values below. Predicted result: cream lands
            // on (243, 226, 197) exactly and the board on (202, 134, 89), both target-01's.
            key.color = new Color(1f, 0.936f, 0.805f);
            key.intensity = 0.957f;
            key.shadows = LightShadows.Soft;
            // A change against integration/look-stack (0.38) that earlier summaries of this
            // branch failed to call out: it went to 0.55 with the raking-key work below.
            //
            // It is 0.45 here for a reason that turned out to be WRONG, and the correction is
            // worth more than the constant. Round 2 read a rail sample of (40, 48, 62), whose
            // effective illuminant divided out to a key visibility of 0.454 — 1 - 0.55 to
            // three decimals — and concluded the rails were fully shadowed with this constant
            // setting how dark. Slot 7 then measured the real boot seam: rails at
            // (51, 59, 73), giving a key visibility of (0.754, 0.845, 0.939). The rails get
            // roughly 85% of the key. They are barely shadowed at all, the 0.454 match was
            // numerology on a superseded sample, and taking 0.55 -> 0.45 correctly moved them
            // by nothing.
            //
            // Kept at 0.45 only because that is the value slot 7 measured and found harmless,
            // so code and last-known-good render agree. It has no remaining justification of
            // its own. If the raking work's deliberate 0.55 is wanted back, this is a
            // one-line revert with no measured downside in either direction — the sampled
            // elements did not respond to it.
            key.shadowStrength = 0.45f;
            key.shadowBias = 0.08f;
            key.shadowNormalBias = 0.25f;
            key.shadowNearPlane = 0.2f;
            // Geometry note, and the second of this branch's two un-flagged rig changes
            // against integration/look-stack (the other is shadowStrength above): the key
            // pose moves Euler(35,-30,2) -> Euler(19,-56,0). That is a raking-light change,
            // not a nudge, and it is the largest single reason the diorama reads differently
            // from the base branch.
            //
            // The board is tilted Euler(38,-32,-4), so its visible surface
            // normal is ~(0.42, 0.62, -0.67). The base pose at Euler(35,-30,2) sat within
            // ~2.5 degrees of that normal — square-on light, which is why the diorama read
            // flat-lit with near-zero shadow length. This pose keeps the light high-right in
            // frame but ~28 degrees off the board normal, so it rakes: shadows stretch to
            // roughly half an object's height toward frame-left and vertical faces split
            // into lit and shaded sides. Renderer positions are untouched, so the 24-unit
            // shadow-distance law in RuntimeSceneRigTests still holds.
            key.transform.localRotation = Quaternion.Euler(19f, -56f, 0f);
        }
    }

    /// <summary>Returns scene-global render settings when the owning game root goes away.</summary>
    internal sealed class BoardSceneEnvironmentScope : MonoBehaviour
    {
        private static int _activeOwners;
        private static bool _hasSnapshot;
        private static Material _skybox;
        private static AmbientMode _ambientMode;
        private static Color _ambientSky;
        private static Color _ambientEquator;
        private static Color _ambientGround;
        private static float _ambientIntensity;
        private bool _ownsLease;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            _activeOwners = 0;
            _hasSnapshot = false;
            _skybox = null;
        }

        public void Capture()
        {
            if (_ownsLease) return;
            if (_activeOwners == 0)
            {
                _skybox = RenderSettings.skybox;
                _ambientMode = RenderSettings.ambientMode;
                _ambientSky = RenderSettings.ambientSkyColor;
                _ambientEquator = RenderSettings.ambientEquatorColor;
                _ambientGround = RenderSettings.ambientGroundColor;
                _ambientIntensity = RenderSettings.ambientIntensity;
                _hasSnapshot = true;
            }
            _activeOwners++;
            _ownsLease = true;
        }

        private void OnDestroy()
        {
            if (!_ownsLease) return;
            _ownsLease = false;
            _activeOwners = Mathf.Max(0, _activeOwners - 1);
            if (_activeOwners != 0 || !_hasSnapshot) return;
            RenderSettings.skybox = _skybox;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.ambientIntensity = _ambientIntensity;
            _hasSnapshot = false;
            _skybox = null;
        }
    }
}
