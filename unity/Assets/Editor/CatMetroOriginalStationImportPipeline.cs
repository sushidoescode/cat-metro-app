using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CatMetro.EditorTools
{
    // Imports only the original, reproducible station study. No generated/licensed
    // source folder is read or rewritten, and this tool never saves a scene.
    public static class CatMetroOriginalStationImportPipeline
    {
        public const string AssetRoot = "Assets/Art/Original/Station";
        public const string PrefabPath = "Assets/Resources/CatMetroOriginal/Station.prefab";
        private const string SourceFbx = AssetRoot + "/Source/original-station-runtime.fbx";
        private const string TexturePath = AssetRoot + "/original-toy-atlas.png";
        private const string MaterialPath = AssetRoot + "/OriginalStation.mat";
        private static readonly string[] RuntimeParts = { "Body", "RoofTint" };

        [MenuItem("Cat Metro/Art/Build Original Station")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("Build the original station outside Play Mode.");
            string recipeRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "..", "..", "docs", "design", "assets", "original-station-runtime"));
            string fbxSource = Path.Combine(recipeRoot, "Models", "original-station-runtime.fbx");
            string textureSource = Path.Combine(recipeRoot, "Textures", "original-toy-atlas.png");
            if (!File.Exists(fbxSource) || !File.Exists(textureSource))
                throw new FileNotFoundException("Generate the original station runtime study first: " + recipeRoot);

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
                throw new InvalidDataException("Original station atlas must be 1024x512.");
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            if (imported == null) throw new InvalidDataException("Original station FBX did not import.");
            Material material = BuildMaterial(atlas);

            var instance = UnityEngine.Object.Instantiate(imported);
            var root = new GameObject("OriginalStation");
            var temporaryMeshes = new List<Mesh>();
            try
            {
                var filters = instance.GetComponentsInChildren<MeshFilter>(true);
                foreach (string name in RuntimeParts)
                {
                    var candidates = filters.Where(filter => filter.name == name).ToArray();
                    if (candidates.Length != 1 || candidates[0].sharedMesh == null)
                        throw new InvalidDataException("Expected one original station mesh named " + name);
                    MeshFilter source = candidates[0];
                    Mesh mesh = BakeMeshInWorldSpace(source, name);
                    temporaryMeshes.Add(mesh);
                    var part = new GameObject(name);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                // BadgePost and BadgeFace are neutral authoring parts only. The live
                // board already owns cream discs, masts, every accepted shape and the
                // delivery/rejection animation targets; a second sign would be misleading.
                // Reject malformed geometry before overwriting the material and meshes
                // referenced by a previously working station prefab.
                Validate(root, atlas);
                material = SaveAsset(material, MaterialPath);
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    filter.sharedMesh = SaveAsset(filter.sharedMesh,
                        AssetRoot + "/Meshes/" + filter.name + ".asset");
                    filter.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidDataException("Original station prefab was not saved.");
                AssetDatabase.SaveAssets();
                int triangles = root.GetComponentsInChildren<MeshFilter>()
                    .Sum(filter => filter.sharedMesh.triangles.Length / 3);
                Debug.Log("ORIGINAL_STATION_IMPORT PASS prefab=" + PrefabPath
                    + " meshes=2 materials=1 triangles=" + triangles
                    + " atlas=" + TexturePath + " atlas_sha256=" + FileSha256(textureSource)
                    + " fbx_sha256=" + FileSha256(fbxSource));
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
            if (model == null) throw new InvalidDataException("Original station ModelImporter missing.");
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
            if (texture == null) throw new InvalidDataException("Original station TextureImporter missing.");
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
            var material = new Material(shader) { name = "OriginalStation" };
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
            // children remain identity; placement corrections stay in the catalog.
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
            return mesh;
        }

        private static void Validate(GameObject root, Texture2D atlas)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length != 2 || renderers.Any(renderer => renderer.sharedMaterials.Length != 1
                || renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != "Universal Render Pipeline/Lit"
                || renderer.sharedMaterial.GetTexture("_BaseMap") != atlas))
                throw new InvalidDataException("Original station requires exactly two URP/Lit renderers with explicit BaseMap.");
            Bounds combined = renderers[0].bounds;
            foreach (Renderer renderer in renderers) combined.Encapsulate(renderer.bounds);
            var expected = new Vector3(3.05f, 2.222f, 2.17f);
            if (Vector3.Distance(combined.size, expected) > .005f || Mathf.Abs(combined.min.y) > .002f)
                throw new InvalidDataException("Original station axis/unit mismatch: " + combined);
            if (root.transform.Find("RoofTint").GetComponent<Renderer>().bounds.min.y < 1.65f)
                throw new InvalidDataException("Original station roof is not above its platform.");
            foreach (Transform part in root.GetComponentsInChildren<Transform>())
                if (part.localPosition != Vector3.zero || part.localRotation != Quaternion.identity
                    || part.localScale != Vector3.one)
                    throw new InvalidDataException("Original station prefab transforms must be identity.");
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
