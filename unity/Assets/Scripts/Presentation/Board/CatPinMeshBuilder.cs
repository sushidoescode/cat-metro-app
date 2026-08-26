using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // CAT-PIN: the two flat card meshes a destination pin needs and no other surface has — the
    // rounded-square CARD the symbol sits on, and the five-pointed STAR the wild cat wears.
    //
    // WHY THIS EXISTS RATHER THAN A CALL TO DestinationShapeMesh. That file is the board's
    // shape realiser and it stays the realiser: a pin's circle, square, triangle and diamond
    // all come from it, unchanged, so there is still exactly one place a line NAME becomes a
    // shape (CatLine.ShapeOf) and exactly one place that shape becomes station geometry. But
    // DestinationShapeMesh.Extrude fans its caps from VERTEX 0, which is only a valid
    // triangulation for a CONVEX outline, and ForShape(Star) therefore throws on purpose
    // rather than hand back a self-overlapping tangle. A riding cat CAN be wild
    // (CatColor.Wild = 5, and CatLine.NameOfCode maps it), so the pin has to draw a star and
    // cannot ask that extruder for one.
    //
    // The fix is the triangulation, not the shape. Both outlines here are fanned from the
    // polygon's CENTRE, which is valid for any STAR-SHAPED polygon — one where the centre can
    // see every boundary point. Every convex polygon is star-shaped, and so is a five-pointed
    // star, so this fan is strictly more general than a vertex-0 fan and is what lets a
    // concave outline through without inventing a general-purpose triangulator.
    //
    // WINDING LAW — copied deliberately, not re-derived. This codebase has lost time to
    // backface culling twice. The camera looks from -Z toward +Z, Unity front faces wind
    // CLOCKWISE as the camera sees them, so camera-facing triangles enumerate the CCW outline
    // in REVERSE. Both outlines below are authored counter-clockwise in the card's XY plane
    // (asserted by signed area in CatPinTests, so a future edit that reorders one fails loudly
    // instead of going invisible), the front cap runs them backwards, and the side quads use
    // DestinationShapeMesh's exact index pattern. Every card is a CLOSED solid: a single-sided
    // decal is precisely what disappeared last time.
    //
    // Outlines live in the unit box (-0.5..0.5 on every axis) so a caller's localScale IS the
    // card's world size once divided by mesh.bounds — the lesson ToyTrainView.ScaleForWorldSize
    // was written for. Meshes are cached statically and flagged HideAndDontSave, the
    // DestinationShapeMesh / BoardSurface idiom: these are shared by every train on the board,
    // so per-instance ownership would let the first consist torn down blank every other pin.
    public static class CatPinMeshBuilder
    {
        /// <summary>Card corner radius, as a fraction of the unit box.</summary>
        public const float CornerRadius = 0.1875f;

        /// <summary>The star's inner radius as a fraction of its outer — what sets point length.</summary>
        public const float StarInnerRadius = 0.45f;

        private const int CornerSegments = 8;

        private static Mesh _card;
        private static Mesh _star;

        /// <summary>The white rounded-square card a pin's symbol is mounted on.</summary>
        public static Mesh Card() =>
            _card != null ? _card : (_card = Extrude("Cat pin — card", CardOutline()));

        /// <summary>The wild cat's star badge — concave, so never the convex extruder.</summary>
        public static Mesh StarBadge() =>
            _star != null ? _star : (_star = Extrude("Cat pin — star", StarOutline()));

        // A square with rounded corners, filling the unit box. Counter-clockwise: the four
        // corner arcs run BR -> TR -> TL -> BL, each sweeping 90 degrees in increasing angle,
        // and the straight edges between them are implied by the polygon's own edges.
        private static Vector2[] CardOutline()
        {
            const float h = 0.5f - CornerRadius;
            var centres = new[]
            {
                new Vector2(h, -h),   // bottom-right, sweeping -90 -> 0
                new Vector2(h, h),    // top-right,       0 -> 90
                new Vector2(-h, h),   // top-left,       90 -> 180
                new Vector2(-h, -h),  // bottom-left,   180 -> 270
            };
            var outline = new List<Vector2>(centres.Length * (CornerSegments + 1));
            for (int corner = 0; corner < centres.Length; corner++)
            {
                float start = -90f + 90f * corner;
                for (int step = 0; step <= CornerSegments; step++)
                {
                    float angle = (start + 90f * step / CornerSegments) * Mathf.Deg2Rad;
                    outline.Add(centres[corner]
                        + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CornerRadius);
                }
            }
            return outline.ToArray();
        }

        // Five points, apex UP so the badge reads the same way the HUD draws it, alternating
        // outer and inner radius. Concave — the whole reason this file exists — but star-shaped
        // about its centre, which is exactly the condition the centre fan below needs.
        private static Vector2[] StarOutline()
        {
            var outline = new Vector2[10];
            for (int i = 0; i < outline.Length; i++)
            {
                float angle = (90f + 36f * i) * Mathf.Deg2Rad; // increasing angle == CCW
                float radius = (i % 2 == 0) ? 0.5f : 0.5f * StarInnerRadius;
                outline[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return outline;
        }

        // Star-shaped outline -> closed prism spanning -0.5..0.5 in Z, so one localScale drives
        // every card. The fan runs from the CENTRE, not from vertex 0: that is the single
        // difference from DestinationShapeMesh.Extrude and the only reason a concave star is
        // safe here. Vertices are split per face so RecalculateNormals yields flat facets
        // rather than a smeared average.
        private static Mesh Extrude(string name, Vector2[] outline)
        {
            int n = outline.Length;
            var vertices = new List<Vector3>(2 * (n + 1) + 4 * n);
            vertices.Add(new Vector3(0f, 0f, -0.5f));                    // 0: front centre
            for (int i = 0; i < n; i++)                                  // 1..n: front rim
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -0.5f));
            int back = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, 0.5f));                     // back centre
            for (int i = 0; i < n; i++)                                  // back rim
                vertices.Add(new Vector3(outline[i].x, outline[i].y, 0.5f));

            var triangles = new List<int>(6 * n);
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                // front cap toward the camera: the CCW outline enumerated in REVERSE
                AddTriangle(triangles, 0, 1 + next, 1 + i);
                // back cap away from the camera: forward enumeration faces +Z
                AddTriangle(triangles, back, back + 1 + i, back + 1 + next);
            }
            for (int edge = 0; edge < n; edge++)
            {
                int next = (edge + 1) % n;
                int a = vertices.Count; // per-edge vertices: crisp facet normals
                vertices.Add(new Vector3(outline[edge].x, outline[edge].y, -0.5f));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, -0.5f));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, 0.5f));
                vertices.Add(new Vector3(outline[edge].x, outline[edge].y, 0.5f));
                AddTriangle(triangles, a, a + 2, a + 3);
                AddTriangle(triangles, a, a + 1, a + 2);
            }

            var mesh = new Mesh
            {
                // A PlayMode test that unloads its scene between cases must not leave the next
                // case holding a destroyed prototype.
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
