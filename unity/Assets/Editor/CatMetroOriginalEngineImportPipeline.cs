using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CatMetro.EditorTools
{
    // Imports only the original, reproducible engine study. No generated/licensed
    // source folder is read or rewritten, and this tool never saves a scene.
    public static class CatMetroOriginalEngineImportPipeline
    {
        public const string AssetRoot = "Assets/Art/Original/ToyEngine";
        public const string PrefabPath = "Assets/Resources/CatMetroOriginal/ToyEngine.prefab";
        private static readonly string[] RuntimeParts = { "EngineBody", "RunningGear" };

        [MenuItem("Cat Metro/Art/Build Original Toy Engine")]
        public static void Build()
        {
            if (UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("Build the original engine outside Play Mode.");
            string recipeRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "..", "..", "docs", "design", "assets", "original-toy-engine"));
            string fbxSource = Path.Combine(recipeRoot, "Models", "original-toy-engine.fbx");
            string textureSource = Path.Combine(recipeRoot, "Textures", "original-toy-atlas.png");
            BuildFromSources(fbxSource, textureSource, AssetRoot, PrefabPath);
        }

        private static void BuildFromSources(string fbxSource, string textureSource,
            string assetRoot, string prefabPath)
        {
            if (!File.Exists(fbxSource) || !File.Exists(textureSource))
                throw new FileNotFoundException("Generate the original engine runtime study first.");
            string sourceFbx = assetRoot + "/Source/original-toy-engine.fbx";
            string texturePath = assetRoot + "/original-toy-atlas.png";
            string materialPath = assetRoot + "/OriginalToyEngine.mat";
            // Runtime materials reference the installed atlas by GUID. Decode the candidate
            // outside AssetDatabase so a rejected size or geometry cannot replace that texture.
            Texture2D candidateAtlas = LoadCandidateAtlas(textureSource);
            Material material = null;
            GameObject instance = null, root = null;
            var temporaryMeshes = new List<Mesh>();
            try
            {
                EnsureFolder(assetRoot + "/Source");
                EnsureFolder(assetRoot + "/Meshes");
                EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
                CopyIfChanged(fbxSource, sourceFbx);
                AssetDatabase.ImportAsset(sourceFbx, ImportAssetOptions.ForceSynchronousImport);
                ConfigureModelImporter(sourceFbx);
                GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(sourceFbx);
                if (imported == null) throw new InvalidDataException("Original engine FBX did not import.");
                material = BuildMaterial(candidateAtlas);
                instance = UnityEngine.Object.Instantiate(imported);
                root = new GameObject("OriginalToyEngine");
                var filters = instance.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != 2)
                    throw new InvalidDataException("Original engine FBX must have exactly two mesh parts.");
                foreach (string name in RuntimeParts)
                {
                    var candidates = filters.Where(filter => filter.name == name).ToArray();
                    if (candidates.Length != 1 || candidates[0].sharedMesh == null)
                        throw new InvalidDataException("Expected one original engine mesh named " + name);
                    MeshFilter source = candidates[0];
                    Mesh mesh = BakeMeshInWorldSpace(source, name);
                    temporaryMeshes.Add(mesh);
                    var part = new GameObject(name);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                // Validate the candidate before replacing meshes/material/atlas used by
                // an existing working prefab. Runtime retains all steam/vehicle anchors.
                Validate(root, candidateAtlas);
                // Only a validated candidate may replace the shared runtime texture.
                CopyIfChanged(textureSource, texturePath);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTextureImporter(texturePath);
                Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (atlas == null || atlas.width != 1024 || atlas.height != 512)
                    throw new InvalidDataException("Original engine runtime atlas import failed.");
                material.SetTexture("_BaseMap", atlas);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", atlas);
                material = SaveAsset(material, materialPath);
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    filter.sharedMesh = SaveAsset(filter.sharedMesh,
                        assetRoot + "/Meshes/" + filter.name + ".asset");
                    filter.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null) throw new InvalidDataException("Original engine prefab was not saved.");
                AssetDatabase.SaveAssets();
                int triangles = root.GetComponentsInChildren<MeshFilter>()
                    .Sum(filter => filter.sharedMesh.triangles.Length / 3);
                Debug.Log("ORIGINAL_ENGINE_IMPORT PASS prefab=" + prefabPath
                    + " meshes=2 materials=1 triangles=" + triangles
                    + " atlas=" + texturePath + " atlas_sha256=" + FileSha256(textureSource)
                    + " fbx_sha256=" + FileSha256(fbxSource));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(instance);
                foreach (Mesh mesh in temporaryMeshes)
                    if (mesh != null && !EditorUtility.IsPersistent(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                if (material != null && !EditorUtility.IsPersistent(material)) UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(candidateAtlas);
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

        private static void ConfigureModelImporter(string sourceFbx)
        {
            var model = AssetImporter.GetAtPath(sourceFbx) as ModelImporter;
            if (model == null) throw new InvalidDataException("Original engine ModelImporter missing.");
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
        }

        private static void ConfigureTextureImporter(string texturePath)
        {
            var texture = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (texture == null) throw new InvalidDataException("Original engine TextureImporter missing.");
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

        private static Texture2D LoadCandidateAtlas(string source)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                { name = "OriginalToyEngineCandidateAtlas" };
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(source))
                    || texture.width != 1024 || texture.height != 512)
                    throw new InvalidDataException("Original engine atlas must be 1024x512.");
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static Material BuildMaterial(Texture2D atlas)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidDataException("URP/Lit shader missing.");
            var material = new Material(shader) { name = "OriginalToyEngine" };
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
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void Validate(GameObject root, Texture2D atlas)
        {
            var catalog = new CatMetro.Presentation.Props.EngineModelCatalog(root);
            if (catalog.AdmittedEntryCount != 1)
                throw new InvalidDataException(catalog.RejectionReason);
            Mesh body = root.transform.Find("EngineBody").GetComponent<MeshFilter>().sharedMesh;
            Mesh gear = root.transform.Find("RunningGear").GetComponent<MeshFilter>().sharedMesh;
            if (body.triangles.Length != 3904 * 3 || gear.triangles.Length != 3584 * 3
                || body.uv.Length != body.vertexCount || gear.uv.Length != gear.vertexCount)
                throw new InvalidDataException("Original engine topology/UV data differs from the verified source.");
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
                if (renderer.sharedMaterial.GetTexture("_BaseMap") != atlas)
                    throw new InvalidDataException("Original engine did not bind the candidate atlas.");
            // Independent triangle rays in actual imported Y-up coordinates. Cab windows
            // must be open, while the roof and funnel cup must remain real surfaces.
            Vector3[] vertices = body.vertices;
            int[] triangles = body.triangles;
            foreach (float height in new[] { .232f, .270f })
                if (TryRay(vertices, triangles, new Vector3(-.126f, height, -.20f),
                        Vector3.forward, out float distance, out _) && distance <= .40f)
                    throw new InvalidDataException("Original engine cab side window is obstructed.");
            if (!TryRay(vertices, triangles, new Vector3(.150f, .40f, 0f), Vector3.down,
                    out float funnelDistance, out _) || Mathf.Abs(.40f - funnelDistance - .352f) > .0001f)
                throw new InvalidDataException("Original engine funnel inset is missing or capped.");
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
