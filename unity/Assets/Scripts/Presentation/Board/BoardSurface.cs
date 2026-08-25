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
        private const float Margin = 1.05f;
        private const float WoodFront = 0.35f;
        private const float WoodDepth = 0.70f;
        private const float RimWidth = 0.24f;
        private const float RimDepth = 0.10f;
        private const float DeskFront = 1.38f;
        private const float DeskDepth = 0.50f;
        private const float DeskOverscan = 20f;

        // Muted walnut: warm enough to read as a toy tabletop without competing with line hues.
        private static readonly Color WarmWood = new Color(0.68f, 0.50f, 0.34f);
        // Mid walnut for the room-scale desk. The DeskGrain sheet multiplies this toward
        // ~1.05x at the board and ~0.5x cooler at the frame corners, so the flat constant
        // needs to sit at the *lit* desk tone, not the vignetted edge tone.
        private static readonly Color WarmDesk = new Color(0.55f, 0.36f, 0.22f);
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
            float width = maxX - minX + Margin * 2f;
            float height = maxY - minY + Margin * 2f;
            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

            var body = new GameObject("BoardBody").transform;
            body.SetParent(parent, false);

            // LOOK step 5 needs desk dressing to stand outside the raised board. This larger,
            // rearward slab is the room-scale desk; the board remains the finite rimmed toy.
            var desk = new GameObject("DeskSurface").transform;
            desk.SetParent(parent, false);
            // One untiled sheet (not the board's repeating tile): the desk needs broad plank
            // grain plus a radial warmth falloff, and a repeating tile cannot carry falloff.
            CreatePart("DeskTop", desk,
                new Vector3(center.x, center.y - 0.15f, DeskFront + DeskDepth * 0.5f),
                new Vector3(width + DeskOverscan, height + DeskOverscan, DeskDepth), WarmDesk,
                grain: DeskGrain(), grainTiling: Vector2.one);

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
                new Vector3(center.x, center.y, 1.18f),
                new Vector3(width + 0.24f, height + 0.24f, 0.34f), Palette.InkNavy);
            CreatePart("WoodTop", body,
                new Vector3(center.x, center.y, WoodFront + WoodDepth * 0.5f),
                new Vector3(width, height, WoodDepth), WarmWood,
                grain: WoodGrain(), grainTiling: new Vector2(width / 6f, height / 6f));

            var rim = new GameObject("CreamRim").transform;
            rim.SetParent(body, false);
            float rimZ = WoodFront - RimDepth * 0.5f - 0.01f;
            CreatePart("Top", rim,
                new Vector3(center.x, center.y + height * 0.5f - RimWidth * 0.5f, rimZ),
                new Vector3(width, RimWidth, RimDepth), Palette.CreamCard);
            CreatePart("Bottom", rim,
                new Vector3(center.x, center.y - height * 0.5f + RimWidth * 0.5f, rimZ),
                new Vector3(width, RimWidth, RimDepth), Palette.CreamCard);
            CreatePart("Left", rim,
                new Vector3(center.x - width * 0.5f + RimWidth * 0.5f, center.y, rimZ),
                new Vector3(RimWidth, height - RimWidth * 2f, RimDepth), Palette.CreamCard);
            CreatePart("Right", rim,
                new Vector3(center.x + width * 0.5f - RimWidth * 0.5f, center.y, rimZ),
                new Vector3(RimWidth, height - RimWidth * 2f, RimDepth), Palette.CreamCard);
            return body;
        }

        // One primitive site and one explicit project-material bind keep every runtime renderer
        // on the project's URP shader. A property block supplies each tint without allocating
        // renderer-local material instances across Retry and LoadNext rebuilds.
        private static Transform CreatePart(string name, Transform parent, Vector3 position,
            Vector3 scale, Color color, Texture2D grain = null, Vector2? grainTiling = null)
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
                    Vector2 tiling = grainTiling ?? Vector2.one;
                    properties.SetTexture("_BaseMap", grain);
                    properties.SetTexture("_MainTex", grain);
                    properties.SetVector("_BaseMap_ST",
                        new Vector4(tiling.x, tiling.y, 0f, 0f));
                }
                renderer.SetPropertyBlock(properties);
            }
            return part.transform;
        }

        private static Texture2D WoodGrain()
        {
            if (_woodGrain != null) return _woodGrain;
            const int size = 128;
            _woodGrain = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "Cat Metro Subtle Wood Grain",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                float bend = 0.035f * Mathf.Sin(v * Mathf.PI * 4f);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float broad = 0.5f + 0.5f * Mathf.Sin(
                        (u + bend) * Mathf.PI * 10f
                        + 0.45f * Mathf.Sin(v * Mathf.PI * 2f));
                    float noise = Mathf.PerlinNoise(u * 8f, v * 1.4f);
                    float value = Mathf.Clamp01(0.90f + broad * 0.075f
                        + (noise - 0.5f) * 0.025f);
                    byte channel = (byte)Mathf.RoundToInt(value * 255f);
                    pixels[y * size + x] = new Color32(channel, channel, channel, 255);
                }
            }
            _woodGrain.SetPixels32(pixels);
            _woodGrain.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return _woodGrain;
        }

        // The room-scale desk sheet. Three jobs in one texture, all judged against
        // docs/reference/target-01-tabletop.png:
        //   1. Plank grain ~5x broader than the board tile, so the surround reads as a real
        //      desk rather than a flat colour field.
        //   2. Radial warmth falloff — bright warm wood at the board fading to a darker,
        //      cooler edge, the target's lamp-pool vignette.
        //   3. Fake depth of field — grain contrast fades with radius, so the desk softens
        //      toward the frame edges. True URP DoF post needs a Volume + depth texture and
        //      meaningful mobile cost on this ortho rig; baking the defocus into the one
        //      sheet costs nothing per frame. Tradeoff: the falloff is static (it cannot
        //      react to lighting changes) and covers only the desk, which overscans well
        //      past the portrait frame anyway.
        // Stays CPU-readable (one 512px copy) so the look tests can pin the falloff law.
        private static Texture2D DeskGrain()
        {
            if (_deskGrain != null) return _deskGrain;
            const int size = 512;
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
                    float bend = 0.045f * Mathf.Sin(v * Mathf.PI * 3f)
                        + 0.06f * (Mathf.PerlinNoise(u * 2.2f, v * 2.2f) - 0.5f);
                    float band = 0.5f + 0.5f * Mathf.Sin((u + bend) * Mathf.PI * 13f
                        + 0.9f * Mathf.Sin(v * Mathf.PI * 2.3f));
                    band = Mathf.Pow(band, 1.6f);
                    float streak = Mathf.PerlinNoise(u * 46f, v * 3.1f);
                    float value = 0.74f
                        + (band * 0.22f + (streak - 0.5f) * 0.10f) * focus;
                    float lum = Mathf.Lerp(1.05f, 0.50f, fall);
                    float warm = Mathf.Lerp(1.04f, 0.90f, fall);
                    float cool = Mathf.Lerp(0.96f, 1.06f, fall);
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
