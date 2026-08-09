using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    public enum DioramaMeshKind : byte
    {
        Cube,
        Sphere,
        Cylinder,
        Capsule,
        RoundedBox,
        Quad,
    }

    public static class DioramaMeshFactory
    {
        private static readonly Dictionary<DioramaMeshKind, Mesh> BuiltinMeshes =
            new Dictionary<DioramaMeshKind, Mesh>();
        private static readonly Dictionary<string, Mesh> SymbolMeshes =
            new Dictionary<string, Mesh>();

        public static GameObject Create(
            Transform parent,
            string name,
            DioramaMeshKind kind,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Attach(go, kind, material);
            return go;
        }

        public static void Attach(GameObject go, DioramaMeshKind kind, Material material)
        {
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFor(kind);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public static GameObject CreateSymbol(
            Transform parent,
            string name,
            LineIdentity identity,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = SymbolMesh(identity.SymbolId);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var symbol = go.AddComponent<LineSymbolMesh>();
            symbol.SymbolId = identity.SymbolId;
            return go;
        }

        private static Mesh MeshFor(DioramaMeshKind kind)
        {
            if (BuiltinMeshes.TryGetValue(kind, out var mesh) && mesh != null) return mesh;
            string resource;
            switch (kind)
            {
                case DioramaMeshKind.Cube: resource = "Cube.fbx"; break;
                case DioramaMeshKind.Sphere: resource = "Sphere.fbx"; break;
                case DioramaMeshKind.Cylinder: resource = "Cylinder.fbx"; break;
                case DioramaMeshKind.Capsule: resource = "Capsule.fbx"; break;
                case DioramaMeshKind.RoundedBox:
                    mesh = MakeRoundedBox();
                    BuiltinMeshes[kind] = mesh;
                    return mesh;
                case DioramaMeshKind.Quad: resource = "Quad.fbx"; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
            mesh = Resources.GetBuiltinResource<Mesh>(resource);
            if (mesh == null)
                throw new System.InvalidOperationException("Missing built-in mesh " + resource);
            BuiltinMeshes[kind] = mesh;
            return mesh;
        }

        private static Mesh MakeRoundedBox()
        {
            const float half = 0.5f;
            const float radius = 0.12f;
            const float core = half - radius;
            float[] grid = { -half, -core, core, half };
            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var triangles = new List<int>(324);

            AddRoundedFace(Vector3.forward, Vector3.right, Vector3.up,
                grid, core, radius, vertices, normals, triangles);
            AddRoundedFace(Vector3.back, Vector3.left, Vector3.up,
                grid, core, radius, vertices, normals, triangles);
            AddRoundedFace(Vector3.right, Vector3.back, Vector3.up,
                grid, core, radius, vertices, normals, triangles);
            AddRoundedFace(Vector3.left, Vector3.forward, Vector3.up,
                grid, core, radius, vertices, normals, triangles);
            AddRoundedFace(Vector3.up, Vector3.right, Vector3.back,
                grid, core, radius, vertices, normals, triangles);
            AddRoundedFace(Vector3.down, Vector3.right, Vector3.forward,
                grid, core, radius, vertices, normals, triangles);

            var mesh = new Mesh
            {
                name = "RoundedBox12",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddRoundedFace(
            Vector3 outward,
            Vector3 across,
            Vector3 upward,
            float[] grid,
            float core,
            float radius,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            int start = vertices.Count;
            for (int y = 0; y < grid.Length; y++)
            {
                for (int x = 0; x < grid.Length; x++)
                {
                    Vector3 outer = outward * 0.5f + across * grid[x] + upward * grid[y];
                    Vector3 inner = new Vector3(
                        Mathf.Clamp(outer.x, -core, core),
                        Mathf.Clamp(outer.y, -core, core),
                        Mathf.Clamp(outer.z, -core, core));
                    Vector3 normal = (outer - inner).normalized;
                    vertices.Add(inner + normal * radius);
                    normals.Add(normal);
                }
            }
            for (int y = 0; y < grid.Length - 1; y++)
            {
                for (int x = 0; x < grid.Length - 1; x++)
                {
                    int a = start + y * grid.Length + x;
                    int b = a + 1;
                    int d = a + grid.Length;
                    int c = d + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
            }
        }

        private static Mesh SymbolMesh(string symbolId)
        {
            if (SymbolMeshes.TryGetValue(symbolId, out var mesh) && mesh != null) return mesh;
            switch (symbolId)
            {
                case "circle": mesh = MakeDisc(18, false); break;
                case "square": mesh = MakeDisc(4, false, 45f); break;
                case "triangle": mesh = MakeDisc(3, false); break;
                case "diamond": mesh = MakeDisc(4, false); break;
                case "star": mesh = MakeDisc(5, true); break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(symbolId), symbolId,
                        "Unknown line symbol mesh");
            }
            mesh.name = "LineSymbol/" + symbolId;
            SymbolMeshes[symbolId] = mesh;
            return mesh;
        }

        private static Mesh MakeDisc(int points, bool star, float degreesOffset = 90f)
        {
            int rimCount = star ? points * 2 : points;
            var vertices = new Vector3[rimCount + 1];
            var normals = new Vector3[rimCount + 1];
            var triangles = new int[rimCount * 3];
            vertices[0] = Vector3.zero;
            normals[0] = Vector3.back;
            for (int i = 0; i < rimCount; i++)
            {
                float radius = star && (i & 1) == 1 ? 0.45f : 1f;
                float angle = (degreesOffset - i * 360f / rimCount) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius, 0f);
                normals[i + 1] = Vector3.back;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = ((i + 1) % rimCount) + 1;
            }
            var mesh = new Mesh
            {
                vertices = vertices,
                normals = normals,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
