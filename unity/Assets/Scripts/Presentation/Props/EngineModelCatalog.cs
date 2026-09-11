using UnityEngine;
using UnityEngine.Rendering;

namespace CatMetro.Presentation.Props
{
    // One optional original engine. The importer owns exact topology and cab-window verification;
    // runtime admission checks render-only structure, atlas binding and immutable units.
    public sealed class EngineModelCatalog
    {
        public const string ResourcePath = "CatMetroOriginal/ToyEngine";
        public static EngineModelCatalog Empty { get; } = new EngineModelCatalog(null);
        private readonly GameObject _prefab;
        public int AdmittedEntryCount => _prefab == null ? 0 : 1;
        public string RejectionReason { get; }

        public EngineModelCatalog(GameObject prefab)
        {
            RejectionReason = Validate(prefab);
            if (string.IsNullOrEmpty(RejectionReason)) _prefab = prefab;
        }

        public static EngineModelCatalog LoadResources() =>
            new EngineModelCatalog(Resources.Load<GameObject>(ResourcePath));

        public bool TryGetPrefab(out GameObject prefab)
        {
            prefab = _prefab;
            return prefab != null;
        }

        private static string Validate(GameObject prefab)
        {
            if (prefab == null) return "Original engine resource is absent.";
            if (!prefab.activeSelf || prefab.transform.childCount != 2)
                return "Original engine needs two active named mesh parts.";
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || !(component is Transform || component is MeshFilter
                    || component is MeshRenderer))
                    return "Original engine must contain only static render components.";
                if (component is Transform pose && (!pose.gameObject.activeSelf
                    || pose.localPosition != Vector3.zero || pose.localRotation != Quaternion.identity
                    || pose.localScale != Vector3.one))
                    return "Original engine import transforms must be active and identity.";
            }
            if (prefab.GetComponentsInChildren<MeshRenderer>(true).Length != 2
                || prefab.GetComponentsInChildren<MeshFilter>(true).Length != 2)
                return "Original engine must have exactly two static meshes and renderers.";
            Material shared = null;
            uint indexCount = 0;
            foreach (string name in new[] { "EngineBody", "RunningGear" })
            {
                Transform part = prefab.transform.Find(name);
                if (part == null || part.childCount != 0)
                    return "Original engine is missing a flat named part: " + name;
                MeshFilter filter = part.GetComponent<MeshFilter>();
                MeshRenderer renderer = part.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null || !renderer.enabled)
                    return "Original engine part has no visible mesh: " + name;
                Mesh mesh = filter.sharedMesh;
                if (mesh.vertexCount == 0 || mesh.subMeshCount != 1
                    || mesh.GetTopology(0) != MeshTopology.Triangles
                    || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                    return "Original engine part has incomplete mesh/UV data: " + name;
                uint indices = mesh.GetIndexCount(0);
                if (indices == 0) return "Original engine part has no triangles: " + name;
                indexCount += indices;
                Vector3 size = name == "EngineBody" ? new Vector3(.447f, .291f, .296f)
                    : new Vector3(.458f, .1085f, .299f);
                Vector3 center = name == "EngineBody" ? new Vector3(-.003f, .2245f, 0f)
                    : new Vector3(0f, .05425f, 0f);
                if (!Near(mesh.bounds.size, size) || !Near(mesh.bounds.center, center))
                    return "Original engine part has incorrect axis, size or pivot: " + name;
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length != 1 || materials[0] == null
                    || materials[0].shader == null || materials[0].shader.name != "Universal Render Pipeline/Lit"
                    || !materials[0].HasProperty("_BaseMap")
                    || !(materials[0].GetTexture("_BaseMap") is Texture2D atlas)
                    || atlas.width != 1024 || atlas.height != 512
                    || materials[0].GetColor("_BaseColor") != Color.white)
                    return "Original engine requires its neutral URP/Lit atlas material: " + name;
                if (shared != null && shared != materials[0])
                    return "Original engine parts must share one atlas material.";
                shared = materials[0];
            }
            return indexCount > 10000 * 3 ? "Original engine exceeds its 10000-triangle budget." : string.Empty;
        }

        private static bool Near(Vector3 actual, Vector3 expected) =>
            float.IsFinite(actual.x) && float.IsFinite(actual.y) && float.IsFinite(actual.z)
            && Vector3.Distance(actual, expected) < .0002f;
    }
}
