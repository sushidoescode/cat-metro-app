using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CatMetro.EditorTools
{
    // Imports only the original, reproducible open carriage study. No generated/licensed
    // source folder is read or rewritten, and this tool never saves a scene.
    public static class CatMetroOriginalCarriageImportPipeline
    {
        public const string AssetRoot = "Assets/Art/Original/OpenCarriage";
        public const string PrefabPath = "Assets/Resources/CatMetroOriginal/OpenCarriage.prefab";
        private const string SourceFbx = AssetRoot + "/Source/original-open-carriage.fbx";
        private const string TexturePath = AssetRoot + "/original-toy-atlas.png";
        private const string MaterialPath = AssetRoot + "/OriginalOpenCarriage.mat";
        private static readonly string[] RuntimeParts = { "OpenShell", "Undercarriage" };

        [MenuItem("Cat Metro/Art/Build Original Open Carriage")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("Build the original carriage outside Play Mode.");
            string recipeRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "..", "..", "docs", "design", "assets", "original-open-carriage"));
            string fbxSource = Path.Combine(recipeRoot, "Models", "original-open-carriage.fbx");
            string textureSource = Path.Combine(recipeRoot, "Textures", "original-toy-atlas.png");
            if (!File.Exists(fbxSource) || !File.Exists(textureSource))
                throw new FileNotFoundException("Generate the original carriage runtime study first: " + recipeRoot);

            EnsureFolder(AssetRoot + "/Source");
            EnsureFolder(AssetRoot + "/Meshes");
            EnsureFolder("Assets/Resources/CatMetroOriginal");
            CopyIfChanged(fbxSource, SourceFbx);
            CopyIfChanged(textureSource, TexturePath);
            AssetDatabase.ImportAsset(SourceFbx, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporters();
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (atlas == null || atlas.width != 1024 || atlas.height != 512)
                throw new InvalidDataException("Original carriage atlas must be 1024x512.");
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            if (imported == null) throw new InvalidDataException("Original carriage FBX did not import.");
            Material material = BuildMaterial(atlas);

            var instance = UnityEngine.Object.Instantiate(imported);
            var root = new GameObject("OriginalOpenCarriage");
            var temporaryMeshes = new List<Mesh>();
            try
            {
                var filters = instance.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != RuntimeParts.Length)
                    throw new InvalidDataException("Original carriage FBX must contain only OpenShell and Undercarriage.");
                foreach (string name in RuntimeParts)
                {
                    var candidates = filters.Where(filter => filter.name == name).ToArray();
                    if (candidates.Length != 1 || candidates[0].sharedMesh == null)
                        throw new InvalidDataException("Expected one original carriage mesh named " + name);
                    MeshFilter source = candidates[0];
                    Mesh mesh = BakeMeshInWorldSpace(source, name);
                    temporaryMeshes.Add(mesh);
                    var part = new GameObject(name);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                // Keep the authored shell whole: teal body, cream rim and neutral floor
                // use different regions of the same atlas, with no global colour tint.
                // Validate temporary geometry before replacing a previously built prefab.
                Validate(root, atlas);
                material = SaveAsset(material, MaterialPath);
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    filter.sharedMesh = SaveAsset(filter.sharedMesh,
                        AssetRoot + "/Meshes/" + filter.name + ".asset");
                    filter.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidDataException("Original carriage prefab was not saved.");
                AssetDatabase.SaveAssets();
                int triangles = root.GetComponentsInChildren<MeshFilter>()
                    .Sum(filter => filter.sharedMesh.triangles.Length / 3);
                Debug.Log("ORIGINAL_CARRIAGE_IMPORT PASS prefab=" + PrefabPath
                    + " meshes=2 materials=1 triangles=" + triangles
                    + " size_y_up=(0.520,0.165,0.540) floor_y=0.085 rim_y=0.165 cavity_rays=21"
                    + " atlas=" + TexturePath + " atlas_sha256=" + FileSha256(textureSource)
                    + " fbx_sha256=" + FileSha256(fbxSource)
                    + " proof=docs/design/assets/original-open-carriage/EXPORT-VERIFICATION.json");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(instance);
                foreach (Mesh mesh in temporaryMeshes)
                    if (mesh != null && !EditorUtility.IsPersistent(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                if (material != null && !EditorUtility.IsPersistent(material)) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        public static void BuildAndExit()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureImporters()
        {
            var model = AssetImporter.GetAtPath(SourceFbx) as ModelImporter;
            if (model == null) throw new InvalidDataException("Original carriage ModelImporter missing.");
            model.globalScale = 1f;
            model.useFileScale = true;
            model.importAnimation = false;
            model.importBlendShapes = false;
            model.importCameras = false;
            model.importLights = false;
            model.addCollider = false;
            model.isReadable = true;
            model.meshCompression = ModelImporterMeshCompression.Off;
            model.importNormals = ModelImporterNormals.Import;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.SaveAndReimport();

            var texture = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (texture == null) throw new InvalidDataException("Original carriage TextureImporter missing.");
            texture.textureType = TextureImporterType.Default;
            texture.sRGBTexture = true;
            texture.mipmapEnabled = true;
            texture.alphaSource = TextureImporterAlphaSource.None;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.maxTextureSize = 1024;
            texture.anisoLevel = 2;
            texture.SaveAndReimport();
        }

        private static Material BuildMaterial(Texture2D atlas)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidDataException("URP/Lit shader missing.");
            var material = new Material(shader) { name = "OriginalOpenCarriage" };
            material.SetTexture("_BaseMap", atlas);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .28f);
            material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", atlas);
            return material;
        }

        private static Mesh BakeMeshInWorldSpace(MeshFilter source, string name)
        {
            // Flatten the FBX import hierarchy into Y-up mesh bytes. Prefab roots and
            // children remain identity; the owner will calibrate board-local placement.
            Mesh mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            mesh.name = name;
            Matrix4x4 matrix = source.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;
            Vector3[] normals = mesh.normals;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            mesh.normals = normals;
            if (matrix.determinant < 0f)
            {
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int swap = triangles[i];
                    triangles[i] = triangles[i + 2];
                    triangles[i + 2] = swap;
                }
                mesh.triangles = triangles;
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void Validate(GameObject root, Texture2D atlas)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length != 2 || renderers.Any(renderer => renderer.sharedMaterials.Length != 1
                || renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != "Universal Render Pipeline/Lit"
                || renderer.sharedMaterial.GetTexture("_BaseMap") != atlas
                || renderer.sharedMaterial.GetColor("_BaseColor") != Color.white)
                || renderers[0].sharedMaterial != renderers[1].sharedMaterial)
                throw new InvalidDataException("Original carriage requires exactly two URP/Lit renderers with explicit BaseMap.");
            Bounds combined = renderers[0].bounds;
            foreach (Renderer renderer in renderers) combined.Encapsulate(renderer.bounds);
            var expected = new Vector3(.520f, .165f, .540f);
            if (Vector3.Distance(combined.size, expected) > .0002f || Mathf.Abs(combined.min.y) > .0001f)
                throw new InvalidDataException("Original carriage axis/unit mismatch: " + combined);
            Mesh shell = root.transform.Find("OpenShell").GetComponent<MeshFilter>().sharedMesh;
            Mesh chassis = root.transform.Find("Undercarriage").GetComponent<MeshFilter>().sharedMesh;
            if (shell.triangles.Length != 700 * 3 || chassis.triangles.Length != 2644 * 3
                || shell.subMeshCount != 1 || chassis.subMeshCount != 1
                || shell.uv.Length != shell.vertexCount || chassis.uv.Length != chassis.vertexCount)
                throw new InvalidDataException("Original carriage topology/UV budget differs from the verified prototype.");
            ValidateCavity(shell);
            foreach (Transform part in root.GetComponentsInChildren<Transform>())
                if (part.localPosition != Vector3.zero || part.localRotation != Quaternion.identity
                    || part.localScale != Vector3.one)
                    throw new InvalidDataException("Original carriage prefab transforms must be identity.");
        }

        private static void ValidateCavity(Mesh mesh)
        {
            // Query actual imported triangles in conventional Unity +Y-up coordinates.
            // These are independent of the Blender recipe's BVH implementation.
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            foreach (float x in new[] { -.15f, 0f, .15f })
                foreach (float z in new[] { -.13f, 0f, .13f })
                    RequireTop(vertices, triangles, new Vector3(x, .30f, z), .085f, "floor");
            foreach (Vector3 origin in new[] { new Vector3(.245f, .30f, 0f),
                new Vector3(-.245f, .30f, 0f), new Vector3(0f, .30f, .225f),
                new Vector3(0f, .30f, -.225f) })
                RequireTop(vertices, triangles, origin, .165f, "rim");
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                if (!TryRay(vertices, triangles, new Vector3(0f, .12f, 0f), direction,
                        out float distance, out Vector3 normal) || distance < .20f || distance > .24f
                    || Vector3.Dot(normal, direction) > -.98f)
                    throw new InvalidDataException("Original carriage inner wall missing or reversed: " + direction);
                if (TryRay(vertices, triangles, new Vector3(0f, .170f, 0f), direction,
                        out _, out _))
                    throw new InvalidDataException("Original carriage opening is obstructed above the rim.");
            }
        }

        private static void RequireTop(Vector3[] vertices, int[] triangles, Vector3 origin,
            float height, string label)
        {
            if (!TryRay(vertices, triangles, origin, Vector3.down, out float distance, out Vector3 normal)
                || Mathf.Abs(origin.y - distance - height) > .0001f || normal.y < .99f)
                throw new InvalidDataException("Original carriage " + label + " is missing, capped or reversed at " + origin);
        }

        private static bool TryRay(Vector3[] vertices, int[] triangles, Vector3 origin,
            Vector3 direction, out float nearest, out Vector3 normal)
        {
            nearest = float.PositiveInfinity;
            normal = Vector3.zero;
            // Two-sided Moller-Trumbore intersection; winding is checked separately.
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], edge1 = vertices[triangles[i + 1]] - a,
                    edge2 = vertices[triangles[i + 2]] - a;
                Vector3 p = Vector3.Cross(direction, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < 1e-9f) continue;
                float inverse = 1f / determinant;
                Vector3 offset = origin - a;
                float u = Vector3.Dot(offset, p) * inverse;
                if (u < -1e-6f || u > 1f + 1e-6f) continue;
                Vector3 q = Vector3.Cross(offset, edge1);
                float v = Vector3.Dot(direction, q) * inverse;
                if (v < -1e-6f || u + v > 1f + 1e-6f) continue;
                float distance = Vector3.Dot(edge2, q) * inverse;
                if (distance < 0f || distance >= nearest) continue;
                nearest = distance;
                normal = Vector3.Cross(edge1, edge2).normalized;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private static T SaveAsset<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(value, existing);
                UnityEngine.Object.DestroyImmediate(value);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static void EnsureFolder(string path)
        {
            string parent = "Assets";
            foreach (string segment in path.Split('/').Skip(1))
            {
                string next = parent + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, segment);
                parent = next;
            }
        }

        private static void CopyIfChanged(string source, string assetPath)
        {
            string destination = Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring("Assets/".Length));
            if (!File.Exists(destination) || FileSha256(source) != FileSha256(destination))
                File.Copy(source, destination, true);
        }

        private static string FileSha256(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
