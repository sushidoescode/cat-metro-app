using System;
using System.Collections.Generic;
using CatMetro.Presentation.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CatMetro.Editor
{
    public static class CatMetroDioramaAuthoring
    {
        private const string ModelRoot = "Assets/Art/Polyfork/Models/";
        private const string MaterialRoot = "Assets/Art/Materials/";
        private const string PrefabRoot = "Assets/Prefabs/Diorama/";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private readonly struct Dressing
        {
            public readonly string Model;
            public readonly string Name;
            public readonly string Material;
            public readonly Color Color;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;
            public readonly float Scale;

            public Dressing(string model, string name, string material, Color color,
                Vector3 position, Vector3 rotation, float scale)
            {
                Model = model;
                Name = name;
                Material = material;
                Color = color;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }

        [MenuItem("Cat Metro/Build Diorama Assets")]
        public static void Build()
        {
            EnsureFolder("Assets/Art", "Materials");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Diorama");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var entries = new[]
            {
                new Dressing("polyfork_log_cabin_4fac3b.fbx", "Polyfork_DepotShed",
                    "CreamCard", DioramaPalette.CreamCard,
                    new Vector3(3f, 10.2f, 0.28f), new Vector3(90f, 0f, 0f), 0.2f),
                new Dressing("polyfork_train_engine_180979.fbx", "Polyfork_ToyEngine",
                    "MetroTeal", DioramaPalette.MetroTeal,
                    new Vector3(4.72f, 9.15f, 0.28f), Vector3.zero, 0.24f),
                new Dressing("polyfork_young_pine_0d7695.fbx", "Polyfork_Pine",
                    "MetroTeal", DioramaPalette.MetroTeal,
                    new Vector3(0.38f, 10.25f, 0.42f), new Vector3(90f, 0f, 0f), 0.52f),
                new Dressing("polyfork_wooden_fence_section_5f04b7.fbx", "Polyfork_Fence",
                    "CreamCard", DioramaPalette.CreamCard,
                    new Vector3(5.38f, 10.75f, 0.42f), new Vector3(90f, 0f, 0f), 0.58f),
                new Dressing("polyfork_wooden_bench_661da4.fbx", "Polyfork_Bench",
                    "InkNavy", DioramaPalette.InkNavy,
                    new Vector3(0.45f, 0.85f, 0.38f), Vector3.zero, 0.68f),
                new Dressing("polyfork_sandwich_board_sign_cb5e7c.fbx", "Polyfork_StationSign",
                    "TicketOrange", DioramaPalette.TicketOrange,
                    new Vector3(5.35f, 3.15f, 0.22f), new Vector3(90f, 0f, 0f), 0.9f),
                new Dressing("polyfork_street_lamp_29f365.fbx", "Polyfork_StreetLamp",
                    "DepotNavy", DioramaPalette.DepotNavy,
                    new Vector3(5.42f, 6.7f, 0.25f), new Vector3(90f, 0f, 0f), 0.56f),
                new Dressing("polyfork_coffee_cup_90be67.fbx", "Polyfork_CoffeeCup",
                    "WarmPaper", DioramaPalette.WarmPaper,
                    new Vector3(0.48f, 3.85f, 0.28f), new Vector3(90f, 0f, 0f), 4.6f),
                new Dressing("polyfork_tram_track_tile_f3c69a.fbx", "Polyfork_TrackTile",
                    "CreamCard", DioramaPalette.CreamCard,
                    new Vector3(0.12f, 6.25f, 0.55f), Vector3.zero, 0.16f),
            };

            var prefabPaths = new Dictionary<string, string>();
            foreach (Dressing entry in entries)
            {
                string modelPath = ModelRoot + entry.Model;
                ConfigureImporter(modelPath);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) throw new InvalidOperationException("Missing model " + modelPath);
                Material material = MaterialAsset(entry.Material, entry.Color);
                string prefabPath = PrefabRoot + entry.Name + ".prefab";
                BuildPrefab(model, entry.Name, material, prefabPath);
                prefabPaths[entry.Name] = prefabPath;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ReplaceRoot(scene, "DioramaSet");
            ReplaceRoot(scene, "WarmKey");

            var set = new GameObject("DioramaSet");
            foreach (Dressing entry in entries)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[entry.Name]);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(set.transform, false);
                instance.transform.localPosition = entry.Position;
                instance.transform.localEulerAngles = entry.Rotation;
                instance.transform.localScale = Vector3.one * entry.Scale;
            }

            var key = new GameObject("WarmKey");
            var light = key.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.77f, 0.58f, 1f);
            light.intensity = 1.18f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            key.transform.rotation = Quaternion.Euler(18f, -24f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = DioramaPalette.WarmPaper;
            RenderSettings.ambientEquatorColor = DioramaPalette.CreamCard;
            RenderSettings.ambientGroundColor = DioramaPalette.InkNavy;
            RenderSettings.ambientIntensity = 0.72f;
            RenderSettings.sun = light;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("CAT_METRO_DIORAMA_AUTHORED prefabs=" + entries.Length);
        }

        [MenuItem("Cat Metro/Capture Diorama Orientation Sheet")]
        public static void CaptureOrientationSheet()
        {
            string[] names =
            {
                "Polyfork_DepotShed", "Polyfork_ToyEngine", "Polyfork_Pine",
                "Polyfork_Fence", "Polyfork_Bench", "Polyfork_StationSign",
                "Polyfork_StreetLamp", "Polyfork_CoffeeCup", "Polyfork_TrackTile",
            };
            Vector3[] rotations =
            {
                Vector3.zero, new Vector3(0f, 90f, 0f),
                new Vector3(90f, 0f, 0f), new Vector3(0f, 0f, 90f),
            };

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            for (int row = 0; row < names.Length; row++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabRoot + names[row] + ".prefab");
                for (int column = 0; column < rotations.Length; column++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.eulerAngles = rotations[column];
                    Bounds bounds = WorldBounds(instance);
                    float fit = Mathf.Min(1.65f / Mathf.Max(0.01f, bounds.size.x),
                        1.35f / Mathf.Max(0.01f, bounds.size.y));
                    instance.transform.localScale = Vector3.one * fit;
                    bounds = WorldBounds(instance);
                    Vector3 target = new Vector3(-3f + column * 2f, (4 - row) * 1.65f, 0f);
                    instance.transform.position += target - bounds.center;
                }
            }

            var cameraObject = new GameObject("OrientationCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DioramaPalette.WarmPaper;
            cameraObject.transform.position = new Vector3(0f, 0f, -12f);

            const int width = 1600;
            const int height = 2600;
            var textureTarget = new RenderTexture(width, height, 24);
            camera.targetTexture = textureTarget;
            camera.Render();
            camera.Render();
            RenderTexture.active = textureTarget;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            const string output = "/tmp/catmetro-polyfork-orientations.png";
            System.IO.File.WriteAllBytes(output, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(textureTarget);
            Debug.Log("CAT_METRO_ORIENTATION_SHEET " + output);
        }

        private static void ConfigureImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static Material MaterialAsset(string name, Color color)
        {
            string path = MaterialRoot + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var basis = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Resources/Materials/Greybox.mat");
            if (basis == null) throw new InvalidOperationException("Greybox material missing");
            if (material == null)
            {
                material = new Material(basis) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = basis.shader;
            // The converter has already quantized every low-poly vertex to the authoritative
            // decor palette. White preserves those exact vertex values; code-built meshes
            // use the same shader with vertex weighting disabled on Greybox.
            material.color = Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_VertexColorWeight"))
                material.SetFloat("_VertexColorWeight", 1f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPrefab(GameObject model, string name, Material material, string path)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                UnityEngine.Object.DestroyImmediate(animator);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                int slots = Mathf.Max(1, renderer.sharedMaterials.Length);
                var materials = new Material[slots];
                for (int i = 0; i < slots; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static void ReplaceRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) UnityEngine.Object.DestroyImmediate(root);
        }

        private static Bounds WorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no renderer");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
