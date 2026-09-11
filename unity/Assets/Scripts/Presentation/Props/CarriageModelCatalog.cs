using UnityEngine;
using UnityEngine.Rendering;

namespace CatMetro.Presentation.Props
{
    // One optional original asset. The importer owns cavity/topology verification;
    // runtime admission checks render-only structure, atlas binding and immutable units.
    public sealed class CarriageModelCatalog
    {
        public const string ResourcePath = "CatMetroOriginal/OpenCarriage";
        public static CarriageModelCatalog Empty { get; } = new CarriageModelCatalog(null);
        private readonly GameObject _prefab;
        public int AdmittedEntryCount => _prefab == null ? 0 : 1;
        public string RejectionReason { get; }

        public CarriageModelCatalog(GameObject prefab)
        {
            RejectionReason = Validate(prefab);
            if (string.IsNullOrEmpty(RejectionReason)) _prefab = prefab;
        }

        public static CarriageModelCatalog LoadResources() =>
            new CarriageModelCatalog(Resources.Load<GameObject>(ResourcePath));

        public bool TryGetPrefab(out GameObject prefab)
        {
            prefab = _prefab;
            return prefab != null;
        }

        private static string Validate(GameObject prefab)
        {
            if (prefab == null) return "Original carriage resource is absent.";
            if (!prefab.activeSelf || prefab.transform.childCount != 2)
                return "Original carriage needs two active named mesh parts.";
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || !(component is Transform || component is MeshFilter
                    || component is MeshRenderer))
                    return "Original carriage must contain only static render components.";
                if (component is Transform pose && (!pose.gameObject.activeSelf
                    || pose.localPosition != Vector3.zero || pose.localRotation != Quaternion.identity
                    || pose.localScale != Vector3.one))
                    return "Original carriage import transforms must be active and identity.";
            }
            if (prefab.GetComponentsInChildren<MeshRenderer>(true).Length != 2
                || prefab.GetComponentsInChildren<MeshFilter>(true).Length != 2)
                return "Original carriage must have exactly two static meshes and renderers.";
            Material shared = null;
            uint indexCount = 0;
            foreach (string name in new[] { "OpenShell", "Undercarriage" })
            {
                Transform part = prefab.transform.Find(name);
                if (part == null || part.childCount != 0)
                    return "Original carriage is missing a flat named part: " + name;
                MeshFilter filter = part.GetComponent<MeshFilter>();
                MeshRenderer renderer = part.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null || !renderer.enabled)
                    return "Original carriage part has no visible mesh: " + name;
                Mesh mesh = filter.sharedMesh;
                if (mesh.vertexCount == 0 || mesh.subMeshCount != 1
                    || mesh.GetTopology(0) != MeshTopology.Triangles
                    || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                    return "Original carriage part has incomplete mesh/UV data: " + name;
                uint indices = mesh.GetIndexCount(0);
                if (indices == 0) return "Original carriage part has no triangles: " + name;
                indexCount += indices;
                Vector3 size = name == "OpenShell" ? new Vector3(.520f, .100f, .480f)
                    : new Vector3(.458f, .108f, .540f);
                Vector3 center = name == "OpenShell" ? new Vector3(0f, .115f, 0f)
                    : new Vector3(0f, .054f, 0f);
                if (!Near(mesh.bounds.size, size) || !Near(mesh.bounds.center, center))
                    return "Original carriage part has incorrect axis, size or pivot: " + name;
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length != 1 || materials[0] == null
                    || materials[0].shader == null || materials[0].shader.name != "Universal Render Pipeline/Lit"
                    || !materials[0].HasProperty("_BaseMap")
                    || !(materials[0].GetTexture("_BaseMap") is Texture2D atlas)
                    || atlas.width != 1024 || atlas.height != 512
                    || materials[0].GetColor("_BaseColor") != Color.white)
                    return "Original carriage requires its neutral URP/Lit atlas material: " + name;
                if (shared != null && shared != materials[0])
                    return "Original carriage parts must share one atlas material.";
                shared = materials[0];
            }
            return indexCount > 4000 * 3 ? "Original carriage exceeds its 4000-triangle budget." : string.Empty;
        }

        private static bool Near(Vector3 actual, Vector3 expected) =>
            float.IsFinite(actual.x) && float.IsFinite(actual.y) && float.IsFinite(actual.z)
            && Vector3.Distance(actual, expected) < .0002f;
    }
}
