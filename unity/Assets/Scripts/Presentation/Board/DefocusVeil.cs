using CatMetro.Presentation.Theme;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatMetro.Presentation.Board
{
    /// <summary>
    /// The stand-in for depth of field. Target-01's near foreground is strongly blurred and
    /// its far edge falls off, and that bokeh is most of what makes the toy read as a real
    /// object on a real desk. We cannot have the real thing here, for reasons that are worth
    /// writing down because they are structural rather than budgetary:
    ///
    ///   1. Assets/Settings/CatMetro_Renderer.asset has `postProcessData: {fileID: 0}`. That
    ///      field is what supplies URP with the shaders for every post-processing pass; with
    ///      it null URP builds no PostProcessPasses at all. A Volume carrying a DepthOfField
    ///      override would be authored, serialised, evaluated — and silently do nothing. That
    ///      is exactly the failure class AGENTS.md warns about (a material that passes every
    ///      test and still renders as a grey ghost), so it is the last thing to reach for.
    ///   2. Assets/Settings/CatMetro_URP.asset has `m_RequireDepthTexture: 0`. DoF needs
    ///      _CameraDepthTexture, so turning it on adds a depth prepass or copy for every
    ///      camera in the game, not just this one.
    ///   3. `m_IntermediateTextureMode: 0` is Auto, and enabling post-processing forces an
    ///      intermediate colour target. On a tiled mobile GPU that trades a straight-to-
    ///      backbuffer path for a full-screen resolve plus blit every frame.
    ///   4. URP's Depth of Field is written for a perspective projection. Bokeh's circle of
    ///      confusion is solved from focal length, aperture and focus distance, and the
    ///      Gaussian path reads LinearEyeDepth with _ZBufferParams, which is the perspective
    ///      reciprocal mapping. BoardSceneLook.FitCamera sets `camera.orthographic = true`
    ///      with an identity rotation. Even fully wired, the CoC would be wrong.
    ///
    ///   (Related, and worth knowing before anyone budgets against MSAA: the pipeline asset
    ///   has `m_MSAA: 1`, i.e. one sample. MSAA is OFF. The `camera.allowMSAA = true` in
    ///   FitCamera is inert until that asset changes.)
    ///
    /// So this fakes it, in the one place a fake is honest: everything the veil touches is
    /// desk. It is a four-quad frame band parented to the camera, with a hole cut for the
    /// diorama, carrying one procedural RGBA sheet. Two cues, one draw call, no post stack,
    /// no depth texture:
    ///
    ///   * a falloff into a cool, dark edge (Palette.DepotNavy) at the top and bottom of the
    ///     frame, which is where the empty desk actually is — the fit leaves 86% of the frame
    ///     width to gameplay but only ~63% of its height, so the vertical is the only axis
    ///     with room for a vignette to read at all;
    ///   * an out-of-focus foreground lobe in the bottom-left (Palette.CreamCard), rising off
    ///     the bottom edge where target-01 puts its coffee cup. Its edge is a cubed smoothstep
    ///     several hundred pixels wide, which is what a defocused silhouette looks like and
    ///     what no amount of geometry will give you on a sharp orthographic camera.
    ///
    /// GAMEPLAY LEGIBILITY IS STRUCTURAL HERE, not a tuning choice. The hole is cut at the
    /// exact half-width the width fit solves the gameplay content into
    /// (size * TargetPortraitAspect * SafeWidth * 1.05) and at the measured half-height of
    /// frameBounds plus a pad, so the board and every piece on it lie inside a region the
    /// mesh does not cover. There are no triangles over the diorama at all — not transparent
    /// ones, none.
    ///
    /// The veil is a child of the CAMERA and never of BoardView, which keeps it out of both
    /// unions in FitCamera (it would otherwise feed its own size back into the fit) and out
    /// of every renderer sweep in RuntimeSceneRigTests.
    /// DefocusVeil_IsAnAuthoredTransparentMaterialOutsideTheBoard pins that so a later
    /// refactor cannot quietly move it.
    ///
    /// KNOWN AND NOT FIXED HERE. Being a camera child means the bands pan with
    /// CauseCameraController.FrameNode, which recentres the camera on a single node without
    /// changing orthographicSize. The hole is solved for the REST pose, so during a cause
    /// frame a renderer near the far or near edge of the board can drift into a band. It is a
    /// soft tint and not an occlusion — the inner third of the band is under 2% alpha by the
    /// cubed ramp, so the worst case is roughly a 13% darkening at one edge of the board while
    /// the cause ring is already the thing being looked at — and it lasts only as long as the
    /// framing does. The fix is to hide the veil while the camera is framed, which means
    /// reaching into a file another lane owns, so it is recorded rather than done. Enlarging
    /// the hole by the maximum pan distance is not the cheap alternative it looks like: the
    /// pan is bounded by the board's own extent and would consume the entire band.
    /// </summary>
    public static class DefocusVeil
    {
        public const string VeilName = "Diorama Defocus Veil";
        public const string MaterialResourcePath = "Materials/Veil";

        // Sheet resolution. 128 is plenty: every feature on it is a several-hundred-pixel
        // gradient by construction, so the texture is upsampled ~4x and bilinear filtering
        // is doing the work. Kept CPU-readable so the look tests can pin the ramp law.
        public const int SheetSize = 128;

        // The band's u is split into two halves that differ only by the foreground lobe, so
        // one sheet serves all four quads: the bottom band samples the lobe half, the other
        // three sample the plain half. Insets keep bilinear filtering off the seam at 0.5.
        public const float LobeHalfMin = 0.02f;
        public const float LobeHalfMax = 0.48f;
        public const float PlainHalfMin = 0.52f;
        public const float PlainHalfMax = 0.98f;

        // Peak opacity of the edge falloff, reached at the outer frame edge. 0.5 darkens the
        // far desk by half toward DepotNavy, which is the same direction and roughly the same
        // strength DeskGrain's own radial falloff already runs (lum 1.05 -> 0.46, warm -> cool
        // over its outer 72%); the veil continues that law past where the desk sheet's Clamp
        // stops varying, and unlike the sheet it also covers whatever props sit out there.
        public const float EdgeAlpha = 0.5f;
        // The foreground lobe. Peak 0.8 rather than 1.0 on purpose: a defocused near object
        // is not only soft-edged, it is translucent at its edge because the lens integrates
        // over it. A fully opaque blob reads as a decal.
        public const float LobeAlpha = 0.8f;
        // Lobe centre and radii in sheet space, with v = 0 at the hole and v = 1 at the outer
        // frame edge. The centre sits BELOW the frame (v 1.10) so only the lobe's top arc is
        // on screen, which is how target-01 frames its cup. u 0.10 within [0.02, 0.48] puts
        // it (0.10 - 0.02) / 0.46 = 17.4% across the bottom band from the left; target-01's
        // cup centre is ~15% across.
        public const float LobeU = 0.10f;
        public const float LobeV = 1.10f;
        public const float LobeRadiusU = 0.078f;
        public const float LobeRadiusV = 0.85f;

        // How far in front of the camera the band sits. Anything past the near clip (0.1)
        // and short of the diorama works; the camera is orthographic, so this changes
        // nothing about the veil's size on screen.
        private const float VeilDepth = 0.5f;
        // Clearance between the measured top/bottom of the diorama and the hole's edge, so
        // the falloff never starts on a rim, a tree or a station roof.
        private const float HolePad = 0.35f;

        private static Texture2D _sheet;
        private static Material _material;

        /// <summary>
        /// Rebuilds the veil for a fitted camera. `contentHalfHeight` is the half-height of
        /// FitCamera's frameBounds about the camera — the real vertical extent of the toy,
        /// not the SafeHeight bound, so the hole is only as large as it has to be.
        /// </summary>
        public static Transform Apply(Camera camera, float orthographicSize,
            float contentHalfHeight)
        {
            if (camera == null) return null;

            float outerHalfWidth = orthographicSize * 0.75f;  // 4:3, the widest we support
            float outerHalfHeight = orthographicSize;
            float holeHalfHeight = contentHalfHeight + HolePad;

            var existing = camera.transform.Find(VeilName);
            // No vertical room means no veil: the falloff would land on the toy. Tear any
            // previous one down rather than leaving a stale band from the last level.
            if (holeHalfHeight >= outerHalfHeight)
            {
                if (existing != null)
                {
                    if (Application.isPlaying) Object.Destroy(existing.gameObject);
                    else Object.DestroyImmediate(existing.gameObject);
                }
                return null;
            }

            var material = SharedMaterial();
            if (material == null) return null;

            Transform veil = existing;
            if (veil == null)
            {
                var go = new GameObject(VeilName);
                veil = go.transform;
                veil.SetParent(camera.transform, false);
                go.AddComponent<MeshFilter>();
                var created = go.AddComponent<MeshRenderer>();
                created.shadowCastingMode = ShadowCastingMode.Off;
                created.receiveShadows = false;
                created.lightProbeUsage = LightProbeUsage.Off;
                created.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.AddComponent<GeneratedVeilMeshOwner>();
            }
            veil.localPosition = new Vector3(0f, 0f, VeilDepth);
            veil.localRotation = Quaternion.identity;
            veil.localScale = Vector3.one;

            // The mesh belongs to this veil, not to the class: PlayMode tests stand several
            // GameRoots up at once, and a static mesh would have the first teardown destroy
            // the geometry the others are still drawing.
            var owner = veil.GetComponent<GeneratedVeilMeshOwner>();
            var filter = veil.GetComponent<MeshFilter>();
            // The bands span 4:3 so they always reach the frame edge, but the sheet is laid
            // out against the camera's REAL aspect so the foreground lobe lands where it is
            // meant to on the actual phone. A stale or odd aspect only slides the lobe; it
            // cannot uncover a frame edge, which is the failure that would be visible.
            float aspect = camera.aspect;
            // Not Mathf.Clamp alone: NaN fails every comparison inside it and would sail
            // through into the UV solve, and a camera with no surface yet reports 0.
            if (!(aspect > 0.35f)) aspect = 917f / 2048f;
            else if (aspect > 0.75f) aspect = 0.75f;
            float visibleHalfWidth = orthographicSize * aspect;
            owner.Mesh = BuildMesh(owner.Mesh, outerHalfWidth, outerHalfHeight,
                holeHalfHeight, visibleHalfWidth);
            filter.sharedMesh = owner.Mesh;

            var renderer = veil.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            var sheet = Sheet();
            properties.SetTexture("_BaseMap", sheet);
            properties.SetTexture("_MainTex", sheet);
            properties.SetTexture("_EmissionMap", sheet);
            // Albedo black so the lit path contributes nothing and the veil's colour is the
            // sheet's own emission. The alpha the blend uses is still _BaseColor.a * map.a,
            // so the ramp survives; what does not survive is any dependence on the key light,
            // which another lane is actively recalibrating. A defocused desk should not
            // change colour because the key moved 3 degrees.
            properties.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f));
            properties.SetColor("_Color", new Color(0f, 0f, 0f, 1f));
            properties.SetColor("_EmissionColor", Color.white);
            renderer.SetPropertyBlock(properties);
            return veil;
        }

        public static Material SharedMaterial()
        {
            if (_material == null)
            {
                _material = Resources.Load<Material>(MaterialResourcePath);
                if (_material == null)
                    Debug.LogError("DefocusVeil: Resources/" + MaterialResourcePath
                        + " missing — the veil needs an AUTHORED transparent URP/Lit material. "
                        + "A runtime-built one would work in the editor and be stripped on "
                        + "device, because no build-time material would reference the "
                        + "_SURFACE_TYPE_TRANSPARENT variant.");
            }
            return _material;
        }

        // TWO quads: a top band and a bottom band, spanning the full 4:3 width. The centre is
        // not covered by any triangle, so the diorama pays nothing — no overdraw, no blend,
        // no chance of the veil tinting a station roof.
        //
        // There are deliberately NO side bands. The width fit puts gameplay across 86.4% of
        // the frame by construction, so a side band could only occupy the outer ~2.4% of the
        // viewport — 22px on the pinned 917px frame — and the falloff would have to complete
        // inside that or read as a hard dark line. Ramped normally it computes to an alpha of
        // about 1e-6 there, i.e. two invisible full-width quads of pure overdraw. The vertical
        // is where the frame actually has room (the toy uses ~63% of the height), which is
        // also where target-01 puts its falloff: the near desk and its cup at the bottom, the
        // far desk fading at the top.
        private static Mesh BuildMesh(Mesh mesh, float ow, float oh, float hh, float visibleHw)
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "Cat Metro Defocus Veil" };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            var vertices = new Vector3[8];
            var uvs = new Vector2[8];
            var triangles = new int[12];
            int v = 0, t = 0;

            // Both bands run their own v from the hole outward, so one ramp serves both. u is
            // laid out so that the VISIBLE frame [-visibleHw, +visibleHw] covers the sheet's
            // half, and the 4:3 overhang runs off the ends where Clamp holds the plain edge.
            AddQuad(vertices, uvs, triangles, ref v, ref t,
                new Vector3(-ow, hh, 0f), new Vector3(ow, hh, 0f),
                new Vector3(ow, oh, 0f), new Vector3(-ow, oh, 0f),
                PlainHalfMin, PlainHalfMax, ow, visibleHw);
            AddQuad(vertices, uvs, triangles, ref v, ref t,
                new Vector3(ow, -hh, 0f), new Vector3(-ow, -hh, 0f),
                new Vector3(-ow, -oh, 0f), new Vector3(ow, -oh, 0f),
                LobeHalfMax, LobeHalfMin, ow, visibleHw);

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            // The camera looks down +Z and the quads face it, so every normal is -Z. Stated
            // rather than recalculated: RecalculateNormals on a flat fan can flip on a
            // degenerate band, and the lit path is switched off by the black albedo anyway.
            var normals = new Vector3[8];
            for (int i = 0; i < normals.Length; i++) normals[i] = new Vector3(0f, 0f, -1f);
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        // inner0/inner1 sit on the hole edge (v = 0); outer1/outer0 on the frame edge (v = 1).
        // u0/u1 are the sheet columns the VISIBLE frame maps to; the band is `ow` wide and the
        // frame `visibleHw`, so the ends overshoot that range and Clamp holds the edge column.
        private static void AddQuad(Vector3[] vertices, Vector2[] uvs, int[] triangles,
            ref int v, ref int t, Vector3 inner0, Vector3 inner1, Vector3 outer1,
            Vector3 outer0, float u0, float u1, float ow, float visibleHw)
        {
            float over = ow / Mathf.Max(visibleHw, 0.0001f);
            float mid = (u0 + u1) * 0.5f;
            float lo = mid + (u0 - mid) * over;
            float hi = mid + (u1 - mid) * over;
            int b = v;
            vertices[v] = inner0; uvs[v++] = new Vector2(lo, 0f);
            vertices[v] = inner1; uvs[v++] = new Vector2(hi, 0f);
            vertices[v] = outer1; uvs[v++] = new Vector2(hi, 1f);
            vertices[v] = outer0; uvs[v++] = new Vector2(lo, 1f);
            // Winding, checked against Unity's own Quad rather than reasoned about, because
            // reasoning about it gets the sign wrong. The built-in Quad has normals (0,0,-1)
            // — it faces a camera that looks down +Z, which is exactly our case — vertices
            // (-.5,-.5) (.5,.5) (.5,-.5) (-.5,.5) and triangles 0,1,2 / 0,3,1. Its first
            // triangle's XY cross, (b-a) x (c-a), is NEGATIVE. So a front face here is a
            // negative cross, and that is what these two produce. The bottom band's vertices
            // arrive in reverse x order so both bands land on the same sign.
            // Veil.mat sets _Cull 0 as well: a back-facing veil is invisible while every test
            // that checks geometry and clearance still passes, and that is not a failure mode
            // worth leaving one sign error away.
            triangles[t++] = b; triangles[t++] = b + 2; triangles[t++] = b + 1;
            triangles[t++] = b; triangles[t++] = b + 3; triangles[t++] = b + 2;
        }

        /// <summary>
        /// The sheet. Alpha carries the falloff and the lobe; RGB carries which of the two
        /// Palette tokens is showing there. Built in float32 throughout, deliberately — a
        /// lane already lost a round verifying procedural sheet maths in float64 and finding
        /// values Unity never produces.
        /// </summary>
        public static Texture2D Sheet()
        {
            if (_sheet != null) return _sheet;
            _sheet = new Texture2D(SheetSize, SheetSize, TextureFormat.RGBA32, true)
            {
                name = "Cat Metro Defocus Veil Sheet",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[SheetSize * SheetSize];
            for (int y = 0; y < SheetSize; y++)
            {
                float v = (y + 0.5f) / SheetSize;
                for (int x = 0; x < SheetSize; x++)
                {
                    float u = (x + 0.5f) / SheetSize;
                    pixels[y * SheetSize + x] = Texel(u, v);
                }
            }
            _sheet.SetPixels32(pixels);
            _sheet.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return _sheet;
        }

        /// <summary>One sheet texel, exposed so the look tests can assert the law directly.</summary>
        public static Color32 Texel(float u, float v)
        {
            // Cubed smoothstep: the falloff has to be imperceptible where it meets the hole,
            // or the veil announces itself as a hard-edged rectangle around the toy. A plain
            // smoothstep still has non-zero curvature at 0; cubing it flattens the first
            // ~30% of the band to under 2% opacity.
            float s = v * v * (3f - 2f * v);
            float edge = EdgeAlpha * s * s * s;

            float lobe = 0f;
            if (u < 0.5f)
            {
                float du = (u - LobeU) / LobeRadiusU;
                float dv = (v - LobeV) / LobeRadiusV;
                float d = Mathf.Sqrt(du * du + dv * dv);
                float k = Mathf.Clamp01(1f - d);
                // Two smoothsteps deep. One is a defocused edge; two is a defocused edge
                // that also has no visible contour line where it reaches zero, which is what
                // separates bokeh from an airbrush.
                k = k * k * (3f - 2f * k);
                lobe = LobeAlpha * k * k * (3f - 2f * k);
            }

            // Composite the lobe OVER the falloff rather than adding them. Adding clamps —
            // at the lobe's peak, 0.8 + 0.5 saturates at 1.0 and the cream share collapses to
            // lobe/1.0 = 0.8, so the mug's core rendered as a navy-cream mud instead of
            // cream. Verified in float32: "over" puts the core at alpha 0.90 and share 0.889.
            float alpha = lobe + edge * (1f - lobe);
            // Ordered dither at half a code value. An 8-bit alpha ramp stretched over ~300
            // screen pixels bands visibly; this is below the quantisation step, so it costs
            // nothing but breaks the contours.
            float dither = (((x2(u) ^ x2(v)) & 1) - 0.5f) * (0.5f / 255f);
            alpha = Mathf.Clamp01(alpha + dither);

            float share = alpha > 0.0001f ? Mathf.Clamp01(lobe / alpha) : 0f;
            Color tint = Color.Lerp(Palette.DepotNavy, Palette.CreamCard, share);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(tint.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(tint.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(tint.b) * 255f),
                (byte)Mathf.RoundToInt(alpha * 255f));
        }

        private static int x2(float t) => Mathf.FloorToInt(t * SheetSize);
    }

    /// <summary>Owns the generated veil mesh, in ToyTrackMeshBuilder's shape.</summary>
    [ExecuteAlways]
    internal sealed class GeneratedVeilMeshOwner : MonoBehaviour
    {
        public Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh == null) return;
            if (Application.IsPlaying(gameObject)) Destroy(Mesh);
            else DestroyImmediate(Mesh);
            Mesh = null;
        }
    }
}
