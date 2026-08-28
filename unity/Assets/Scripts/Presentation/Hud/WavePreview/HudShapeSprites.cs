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
        private static Sprite _diamond;
        private static Sprite _star;
        private static Sprite _waveBand;
        private static Sprite _trophy;
        private static Sprite _people;

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

        // Diamond — a square on its point. Conformance to CAT-MANIFEST.json: the green
        // shorthair wears a diamond. This was a hexagon until a manifest read caught it.
        public static Sprite Diamond => _diamond != null ? _diamond
            : (_diamond = Build("HudDiamond", 64, 64, InsideDiamond, Vector4.zero));

        // Five-pointed star — the WILD cat's badge. Concave, which is why the board's mesh
        // extruder refuses it (it fans from vertex 0); a coverage rasteriser has no such
        // constraint, so the HUD is the one surface that can actually draw it.
        public static Sprite Star => _star != null ? _star
            : (_star = Build("HudStar", 64, 64, InsideStar, Vector4.zero));

        // The decorative wave running through the capsule, straight from the target art: a
        // sine-topped band filling the lower part of the tile. Tiles horizontally, so the
        // sprite is wide and the Image repeats it.
        public static Sprite WaveBand => _waveBand != null ? _waveBand
            : (_waveBand = Build("HudWaveBand", 256, 64, InsideWaveBand, Vector4.zero));

        // The DELIVERIES counter's glyph. target-01 draws a trophy here, not a coloured dot —
        // a dot says "some number of something", a trophy says "how far through the win
        // condition you are" without a legend. Measured off the target it gets ~42px on a
        // 917x2048 phone (counter row 57.6px x the 0.72 mark fraction), which is what the
        // proportions below are drawn for: the cup/stem/base SILHOUETTE carries the identity
        // and survives any downscale, while the handles are a 4.2px secondary cue.
        public static Sprite Trophy => _trophy != null ? _trophy
            : (_trophy = Build("HudTrophy", 64, 64, InsideTrophy, Vector4.zero));

        // The RIDERS counter's glyph: three figures, one forward and two behind. Reads as a
        // crowd rather than as three resolvable people — at 42px the heads sit ~2px apart, so
        // the group silhouette is the signal and that is exactly how the target art draws it.
        public static Sprite People => _people != null ? _people
            : (_people = Build("HudPeople", 64, 64, InsidePeople, Vector4.zero));

        public static Sprite ForShape(DestinationShape shape)
        {
            switch (shape)
            {
                case DestinationShape.Square: return RoundedSquare;
                case DestinationShape.Triangle: return Triangle;
                case DestinationShape.Diamond: return Diamond;
                case DestinationShape.Star: return Star;
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

        // Diamond: the L1 (taxicab) ball, which IS a square rotated 45 degrees. Kept SHARP
        // while the square sprite is rounded — at the ~44px a face badge gets on a phone, a
        // 45-degree rotation alone is a weak signal, so the crisp-points-versus-soft-corners
        // contrast is doing real work to keep the two apart.
        private static bool InsideDiamond(float x, float y)
        {
            return Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f) <= 0.47f;
        }

        // Five-pointed star by point-in-polygon against its ten vertices. A star is CONCAVE,
        // so the half-plane intersections used for the convex shapes above cannot express it;
        // a crossing-number test is exact regardless of convexity. The inner radius is a
        // deliberate 0.50 of the outer rather than the 0.382 of a true pentagram — a thin
        // star closes up into a blob at badge size.
        private static readonly Vector2[] StarPoly = BuildStar(0.47f, 0.235f);

        private static Vector2[] BuildStar(float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? outer : inner;
                // +PI/2, not -PI/2: these are TEXTURE coordinates, where v increases UPWARD,
                // so -PI/2 starts at the BOTTOM and yields an inverted star — a different
                // symbol entirely, and one that reads as a bug. Verified by rasterising.
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f; // start at the top point
                pts[i] = new Vector2(0.5f + r * Mathf.Cos(a), 0.5f + r * Mathf.Sin(a));
            }
            return pts;
        }

        private static bool InsideStar(float x, float y) => InsidePolygon(x, y, StarPoly);

        // Crossing number: count edges the upward ray from (x,y) crosses. Odd means inside.
        private static bool InsidePolygon(float x, float y, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > y) == (poly[j].y > y)) continue;
                float t = (y - poly[i].y) / (poly[j].y - poly[i].y);
                if (x < poly[i].x + t * (poly[j].x - poly[i].x)) inside = !inside;
            }
            return inside;
        }

        // Alpha below a sine curve; two full periods across the tile so a tiled Image reads as
        // a continuous wave with no seam.
        private static bool InsideWaveBand(float x, float y)
        {
            float curve = 0.62f + 0.17f * Mathf.Sin(x * 2f * Mathf.PI * 2f);
            return y <= curve;
        }

        // A trophy, assembled from four stacked slabs plus two handle arcs. Coordinates are
        // TEXTURE space, so y increases UPWARD — the same convention the star's comment above
        // warns about, and the same one that silently inverts a shape if you assume otherwise.
        private static bool InsideTrophy(float x, float y)
        {
            float dx = Mathf.Abs(x - 0.5f);

            if (y >= 0.06f && y <= 0.17f && dx <= 0.25f) return true;   // foot
            if (y > 0.17f && y <= 0.36f && dx <= 0.075f) return true;   // stem
            if (y > 0.36f && y <= 0.43f && dx <= 0.17f) return true;    // plinth under the bowl

            // Handles: an annulus arc on each side. The half-plane test keeps only the OUTER
            // part of the ring, so the inner half does not print a second line across the bowl.
            for (int s = -1; s <= 1; s += 2)
            {
                float hx = 0.5f + s * 0.285f;
                const float hy = 0.68f;
                float d = Mathf.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy));
                if (d >= 0.105f && d <= 0.205f && (x - hx) * s >= -0.06f && y <= 0.86f)
                    return true;
            }

            // Bowl: a cup that tapers to a rounded bottom, with the rim flaring straight so the
            // lip reads as a lip rather than as the top of a cone.
            if (y > 0.43f && y <= 0.94f)
            {
                float t = (y - 0.43f) / (0.94f - 0.43f);
                float half = y > 0.86f ? 0.31f
                    : 0.31f * Mathf.Sqrt(Mathf.Max(0f, t * 0.85f + 0.15f));
                if (dx <= half) return true;
            }
            return false;
        }

        // Three figures — a head disc over a shoulder arch, the centre one larger and forward.
        private static bool InsidePeople(float x, float y)
        {
            return Figure(x, y, 0.170f, 0.685f, 0.140f, 0.175f, 0.05f, 0.49f)
                || Figure(x, y, 0.830f, 0.685f, 0.140f, 0.175f, 0.05f, 0.49f)
                || Figure(x, y, 0.500f, 0.790f, 0.170f, 0.225f, 0.03f, 0.59f);
        }

        // One figure: a head disc, plus a body whose half-width is widest at the SHOULDERS
        // (bottom, t = 0) and narrows toward the neck (top, t = 1).
        private static bool Figure(float x, float y, float cx, float headY, float headR,
            float bodyHalf, float bodyBottom, float bodyTop)
        {
            float hx = x - cx, hy = y - headY;
            if (hx * hx + hy * hy <= headR * headR) return true;
            if (y < bodyBottom || y > bodyTop) return false;
            float t = (y - bodyBottom) / (bodyTop - bodyBottom);
            return Mathf.Abs(x - cx) <= bodyHalf * (1f - 0.45f * t * t);
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
