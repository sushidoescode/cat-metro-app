using System.Collections.Generic;
using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // STATION-BADGE: the 3D realisation of the line vocabulary — a DestinationShape becomes
    // the geometry of a station plate. Twin of HudShapeSprites.ForShape, the 2D realisation:
    // same enum in, a different medium out. Neither of them decides WHICH shape a line gets;
    // CatLine.ShapeOf does that once, for both.
    //
    // Circle and Square deliberately return Unity's BUILTIN Cylinder/Cube meshes rather than
    // generated ones. They are byte-for-byte what the board rendered before the vocabulary
    // existed (GameObject.CreatePrimitive uses these same two assets), so red plates and blue
    // plates are unchanged and the captures and PropPlacement pins that hold them still hold.
    // Triangle and Hexagon have no builtin equivalent, so they are extruded here.
    //
    // WINDING LAW — this codebase has lost time to backface culling twice, most recently the
    // 2026-08-25 switch-lever render review where every camera-facing triangle was culled.
    // The camera looks from -Z toward +Z, and Unity front faces wind CLOCKWISE as the camera
    // sees them, so camera-facing triangles enumerate the CCW board-XY outline in REVERSE.
    // (The sleeper builder next door looks like it does the opposite; it doesn't — it maps its
    // footprint.x through the LATERAL axis, which mirrors the polygon on the way out. Copying
    // its fan pattern over a direct XY outline is precisely the mistake that got made.)
    // Vertices are split per face so RecalculateNormals yields flat facets instead of a
    // smeared average, and every plate is a CLOSED solid — a single-sided decal is exactly
    // what went invisible last time.
    //
    // Meshes are cached statically and flagged HideAndDontSave. That is the BoardSurface /
    // HudShapeSprites neighbour idiom (a shared prototype, built once, surviving a scene
    // unload) rather than the ToyTrackMeshBuilder one (a per-edge unique mesh owned by a
    // component that destroys it in OnDestroy). Per-instance ownership would be wrong here and
    // not merely wasteful: these two meshes are shared by every station on the board, so the
    // first station torn down by a Retry would blank the plates of all the others.
    public static class DestinationShapeMesh
    {
        // Half-height of a regular hexagon of circumradius 0.5: sqrt(3) / 4.
        private const float HexApothem = 0.4330127f;

        private static Mesh _cylinder;
        private static Mesh _cube;
        private static Mesh _triangle;
        private static Mesh _hexagon;

        public static Mesh ForShape(DestinationShape shape)
        {
            switch (shape)
            {
                case DestinationShape.Square:
                    return _cube != null ? _cube
                        : (_cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
                case DestinationShape.Triangle:
                    return _triangle != null ? _triangle
                        : (_triangle = Extrude("Station plate — triangle", TriangleOutline()));
                case DestinationShape.Hexagon:
                    return _hexagon != null ? _hexagon
                        : (_hexagon = Extrude("Station plate — hexagon", HexagonOutline()));
                default:
                    return _cylinder != null ? _cylinder
                        : (_cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"));
            }
        }

        // The builtin cylinder's axis runs along Y, so the circle plate — and only it — has to
        // be laid on its face to point at the camera. Everything else is already a prism in
        // the board's XY plane. This is the rotation the red plate has always carried.
        public static Quaternion PlateRotation(DestinationShape shape) =>
            shape == DestinationShape.Circle
                ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;

        // `size` is the plate's width and height in the anchor's local units; `depth` is how
        // thick it stands off the board. The builtin cylinder is 2 units tall before scaling,
        // so its depth halves — which is why the shipped red plate reads (0.9, 0.05, 0.9)
        // against the blue plate's (0.9, 0.9, 0.1) and the two are in fact the same 0.1 thick.
        // The extruded prisms are normalised to the builtin cube's -0.5..0.5 box so they take
        // the cube's scale semantics unchanged.
        public static Vector3 PlateScale(DestinationShape shape, float size, float depth) =>
            shape == DestinationShape.Circle
                ? new Vector3(size, depth * 0.5f, size)
                : new Vector3(size, size, depth);

        // Apex up, inscribed in the unit box — maximally unlike the square beside it.
        private static Vector2[] TriangleOutline() => new[] // counter-clockwise in board XY
        {
            new Vector2(-0.5f, -0.5f),
            new Vector2(0.5f, -0.5f),
            new Vector2(0f, 0.5f),
        };

        // Flat top and bottom, points left and right — the same orientation HudShapeSprites
        // rasterises, so a green cat's badge and a green station's plate read as one shape.
        private static Vector2[] HexagonOutline() => new[] // counter-clockwise in board XY
        {
            new Vector2(0.5f, 0f),
            new Vector2(0.25f, HexApothem),
            new Vector2(-0.25f, HexApothem),
            new Vector2(-0.5f, 0f),
            new Vector2(-0.25f, -HexApothem),
            new Vector2(0.25f, -HexApothem),
        };

        // Convex outline -> closed prism spanning -0.5..0.5 in Z, matching the builtin cube's
        // box so one localScale drives every shape. Convexity is what makes the fan from
        // vertex 0 valid; both outlines above are convex by construction.
        private static Mesh Extrude(string name, Vector2[] outline)
        {
            int n = outline.Length;
            var vertices = new List<Vector3>(2 * n + 4 * n);
            for (int i = 0; i < n; i++) // 0..n-1: front face only (camera side)
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -0.5f));
            for (int i = 0; i < n; i++) // n..2n-1: back face only (toward the board)
                vertices.Add(new Vector3(outline[i].x, outline[i].y, 0.5f));

            var triangles = new List<int>(6 * n);
            for (int i = 1; i < n - 1; i++)
            {
                // front face toward the camera: the CCW outline enumerated in REVERSE
                AddTriangle(triangles, 0, i + 1, i);
                // back face away from the camera: forward enumeration faces +Z
                AddTriangle(triangles, n, n + i, n + i + 1);
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
                name = name,
                // A PlayMode test that unloads its scene between cases must not leave the next
                // case holding a destroyed prototype.
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
