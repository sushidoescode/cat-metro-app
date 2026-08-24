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
        private static readonly Color WarmDesk = new Color(0.40f, 0.26f, 0.18f);
        private static Mesh _cubeMesh;
        private static Texture2D _woodGrain;

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
            CreatePart("DeskTop", desk,
                new Vector3(center.x, center.y - 0.15f, DeskFront + DeskDepth * 0.5f),
                new Vector3(width + DeskOverscan, height + DeskOverscan, DeskDepth), WarmDesk,
                woodGrain: true);

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
                new Vector3(width, height, WoodDepth), WarmWood, woodGrain: true);

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
            Vector3 scale, Color color, bool woodGrain = false)
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
                if (woodGrain)
                {
                    properties.SetTexture("_BaseMap", WoodGrain());
                    properties.SetTexture("_MainTex", WoodGrain());
                    properties.SetVector("_BaseMap_ST",
                        new Vector4(scale.x / 6f, scale.y / 6f, 0f, 0f));
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
    }
}
