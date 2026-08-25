using UnityEngine;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Hud.WavePreview
{
    // HUD-WAVE: cheap procedural sprites for the wave-preview capsule. Everything the capsule
    // draws is generated here at runtime from arithmetic — no imported art, no per-instance
    // materials. Images bind UiChromeMaterial.Shared and vary only by SPRITE, so the whole
    // capsule stays inside the repo's "no ad-hoc material instances" rule while still being
    // able to show a disc, a triangle and a rounded capsule.
    //
    // Textures are cached statically and flagged HideAndDontSave: a PlayMode test that unloads
    // its scene between cases must not leave the next case holding a destroyed texture.
    // Coverage is supersampled 4x4 per texel, which is what keeps a 40px face from reading as
    // a jagged blob on a phone.
    public static class HudShapeSprites
    {
        private const int Supersample = 4;

        private static Sprite _disc;
        private static Sprite _capsule;
        private static Sprite _roundedSquare;
        private static Sprite _triangle;
        private static Sprite _hexagon;
        private static Sprite _waveBand;

        // A filled circle. Cat heads, eyes, and the circle destination badge.
        public static Sprite Disc => _disc != null ? _disc
            : (_disc = Build("HudDisc", 64, 64, InsideDisc, Vector4.zero));

        // The capsule background: a rounded rectangle sliced so it stretches to any width
        // without smearing its corners. Border must exceed the corner radius or the slice
        // clips into the curve.
        public static Sprite Capsule => _capsule != null ? _capsule
            : (_capsule = Build("HudCapsule", 96, 96, InsideCapsule, new Vector4(46, 46, 46, 46)));

        // The square destination badge — rounded, so it reads as a toy tile rather than a
        // rectangle of colour, and stays distinguishable from the disc at badge size.
        public static Sprite RoundedSquare => _roundedSquare != null ? _roundedSquare
            : (_roundedSquare = Build("HudRoundedSquare", 64, 64, InsideRoundedSquare,
                new Vector4(18, 18, 18, 18)));

        // Upward triangle. Both the triangle destination badge and (rotated) the cat ears.
        public static Sprite Triangle => _triangle != null ? _triangle
            : (_triangle = Build("HudTriangle", 64, 64, InsideTriangle, Vector4.zero));

        // Flat-top hexagon — the fourth destination shape.
        public static Sprite Hexagon => _hexagon != null ? _hexagon
            : (_hexagon = Build("HudHexagon", 64, 64, InsideHexagon, Vector4.zero));

        // The decorative wave running through the capsule, straight from the target art: a
        // sine-topped band filling the lower part of the tile. Tiles horizontally, so the
        // sprite is wide and the Image repeats it.
        public static Sprite WaveBand => _waveBand != null ? _waveBand
            : (_waveBand = Build("HudWaveBand", 256, 64, InsideWaveBand, Vector4.zero));

        public static Sprite ForShape(DestinationShape shape)
        {
            switch (shape)
            {
                case DestinationShape.Square: return RoundedSquare;
                case DestinationShape.Triangle: return Triangle;
                case DestinationShape.Hexagon: return Hexagon;
                default: return Disc;
            }
        }

        // --- coverage predicates, all in normalised 0..1 tile space ---

        private static bool InsideDisc(float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            return dx * dx + dy * dy <= 0.25f;
        }

        private static bool InsideCapsule(float x, float y) => InsideRounded(x, y, 0.48f);

        private static bool InsideRoundedSquare(float x, float y) => InsideRounded(x, y, 0.22f);

        // Rounded rectangle, standard rounded-box SDF: push the point into the inset core box,
        // then everything outside that core is inside iff it is within `radius` of it.
        private static bool InsideRounded(float x, float y, float radius)
        {
            float qx = Mathf.Max(Mathf.Abs(x - 0.5f) - (0.5f - radius), 0f);
            float qy = Mathf.Max(Mathf.Abs(y - 0.5f) - (0.5f - radius), 0f);
            return qx * qx + qy * qy <= radius * radius;
        }

        // Upward triangle inset slightly so the apex is not clipped by the tile edge.
        private static bool InsideTriangle(float x, float y)
        {
            const float pad = 0.06f;
            float top = 1f - pad, bottom = pad;
            if (y < bottom || y > top) return false;
            float t = (y - bottom) / (top - bottom);          // 0 at base, 1 at apex
            float halfWidth = 0.5f * (1f - t) * (1f - 2f * pad);
            return Mathf.Abs(x - 0.5f) <= halfWidth;
        }

        // Regular hexagon, flat top and bottom, points left and right. A regular hexagon is the
        // intersection of three slabs, one per pair of opposite edges: |p . n| <= apothem for
        // the three edge normals at 30, 90 and 150 degrees.
        private static bool InsideHexagon(float x, float y)
        {
            const float r = 0.95f;                 // circumradius in half-tile units
            const float cos30 = 0.8660254f;
            float apothem = cos30 * r;
            float px = (x - 0.5f) * 2f;
            float py = (y - 0.5f) * 2f;
            return Mathf.Abs(py) <= apothem
                && Mathf.Abs(cos30 * px + 0.5f * py) <= apothem
                && Mathf.Abs(cos30 * px - 0.5f * py) <= apothem;
        }

        // Alpha below a sine curve; two full periods across the tile so a tiled Image reads as
        // a continuous wave with no seam.
        private static bool InsideWaveBand(float x, float y)
        {
            float curve = 0.62f + 0.17f * Mathf.Sin(x * 2f * Mathf.PI * 2f);
            return y <= curve;
        }

        private static Sprite Build(string name, int width, int height,
            System.Func<float, float, bool> inside, Vector4 border)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                // The wave band tiles; the shapes must not bleed their edge across the sprite.
                wrapMode = name == "HudWaveBand" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[width * height];
            float inv = 1f / Supersample;
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            float u = (px + (sx + 0.5f) * inv) / width;
                            float v = (py + (sy + 0.5f) * inv) / height;
                            if (inside(u, v)) hits++;
                        }
                    }
                    // White RGB so Image.color tints it directly to a Palette token.
                    byte a = (byte)Mathf.RoundToInt(
                        255f * hits / (Supersample * Supersample));
                    pixels[py * width + px] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
