using System;
using System.Collections.Generic;
using System.IO;
using CatMetro.Presentation.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CatMetro.Editor
{
    public static class CatMetroDioramaAuthoring
    {
        private const string ModelRoot = "Assets/Art/Polyfork/Models/";
        private const string MaterialRoot = "Assets/Art/Materials/";
        private const string PrefabRoot = "Assets/Prefabs/Diorama/";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string RendererPath = "Assets/Settings/CatMetro_Renderer.asset";
        private const string ShadowMeshPath = "Assets/Art/Meshes/SoftShadowDisc.asset";
        private const string PostProfilePath = "Assets/Art/Settings/CatMetro_TabletopPost.asset";

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
            PolyforkLocalCustody.RequireExact();
            EnsureFolder("Assets/Art", "Materials");
            EnsureFolder("Assets/Art", "Meshes");
            EnsureFolder("Assets/Art", "Settings");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Diorama");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Mesh shadowMesh = SoftShadowMeshAsset();
            Material shadowMaterial = ContactShadowMaterialAsset();
            VolumeProfile postProfile = ConfigureTabletopRendering();

            var entries = new[]
            {
                new Dressing("polyfork_log_cabin_4fac3b.fbx", "Polyfork_DepotShed",
                    "CreamCard", DioramaPalette.CreamCard,
                    new Vector3(0.9f, 8.55f, 0.4f), new Vector3(180f, 0f, 0f), 0.18f),
                new Dressing("polyfork_train_engine_180979.fbx", "Polyfork_ToyEngine",
                    "MetroTeal", DioramaPalette.MetroTeal,
                    new Vector3(5.0f, 8.35f, 0.4f), new Vector3(180f, 0f, -12f), 0.2f),
                new Dressing("polyfork_young_pine_0d7695.fbx", "Polyfork_Pine",
                    "MetroTeal", DioramaPalette.MetroTeal,
                    new Vector3(0.55f, 6.65f, 0.4f), new Vector3(180f, 0f, 0f), 0.46f),
                new Dressing("polyfork_wooden_fence_section_5f04b7.fbx", "Polyfork_Fence",
                    "CreamCard", DioramaPalette.CreamCard,
                    new Vector3(3.0f, 2.9f, 0.4f), new Vector3(180f, 0f, -8f), 0.5f),
                new Dressing("polyfork_wooden_bench_661da4.fbx", "Polyfork_Bench",
                    "InkNavy", DioramaPalette.InkNavy,
                    new Vector3(2.0f, 2.3f, 0.4f), new Vector3(180f, 0f, 10f), 0.56f),
                new Dressing("polyfork_sandwich_board_sign_cb5e7c.fbx", "Polyfork_StationSign",
                    "TicketOrange", DioramaPalette.TicketOrange,
                    new Vector3(5.65f, 2.55f, 0.4f), new Vector3(180f, 0f, 0f), 0.62f),
                new Dressing("polyfork_street_lamp_29f365.fbx", "Polyfork_StreetLamp",
                    "DepotNavy", DioramaPalette.DepotNavy,
                    new Vector3(5.5f, 5.25f, 0.4f), new Vector3(180f, 0f, 0f), 0.42f),
                new Dressing("polyfork_coffee_cup_90be67.fbx", "Polyfork_CoffeeCup",
                    "WarmPaper", DioramaPalette.WarmPaper,
                    new Vector3(0.5f, -2.2f, 0.4f), new Vector3(180f, 0f, 0f), 3.8f),
                new Dressing("polyfork_tram_track_tile_f3c69a.fbx", "Polyfork_TrackTile",
                    "CreamCard", DioramaPalette.CreamCard,
                    new Vector3(5.75f, 6.9f, 0.5f), new Vector3(0f, 0f, 18f), 0.13f),
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
            ReplaceRoot(scene, "DioramaPost");

            var set = new GameObject("DioramaSet");
            foreach (Dressing entry in entries)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[entry.Name]);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(set.transform, false);
                instance.transform.localPosition = BoardView.DioramaPoint(entry.Position);
                instance.transform.localEulerAngles = entry.Rotation;
                instance.transform.localScale = Vector3.one * entry.Scale;
                AddDressingShadow(instance, shadowMesh, shadowMaterial);
            }

            var key = new GameObject("WarmKey");
            var light = key.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.77f, 0.58f, 1f);
            light.intensity = 1.18f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            key.transform.rotation = Quaternion.Euler(58f, -24f, 0f);

            var post = new GameObject("DioramaPost");
            var volume = post.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = postProfile;

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

        private static VolumeProfile ConfigureTabletopRendering()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null) throw new InvalidOperationException("Missing " + RendererPath);
            if (renderer.rendererFeatures.Count != 0)
                throw new InvalidOperationException(
                    "Mobile renderer must remain feature-free; use authored blob/baked AO");

            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (renderer.postProcessData == null)
                throw new InvalidOperationException("URP PostProcessData is unavailable");
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "CatMetro_TabletopPost";
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }
            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            vignette.active = true;
            vignette.color.Override(new Color(0.035f, 0.045f, 0.07f, 1f));
            vignette.center.Override(new Vector2(0.5f, 0.48f));
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.44f);
            vignette.rounded.Override(false);
            EditorUtility.SetDirty(vignette);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static Mesh SoftShadowMeshAsset()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ShadowMeshPath);
            if (mesh != null) return mesh;

            var sourceObject = new GameObject("SoftShadowMeshSource");
            DioramaMeshFactory.Attach(sourceObject, DioramaMeshKind.SoftShadow,
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Resources/Materials/Greybox.mat"));
            mesh = UnityEngine.Object.Instantiate(
                sourceObject.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "SoftShadowDisc";
            UnityEngine.Object.DestroyImmediate(sourceObject);
            AssetDatabase.CreateAsset(mesh, ShadowMeshPath);
            return mesh;
        }

        private static Material ContactShadowMaterialAsset()
        {
            const string path = MaterialRoot + "ContactShadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var basis = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Resources/Materials/Greybox.mat");
            if (basis == null) throw new InvalidOperationException("Greybox material missing");
            if (material == null)
            {
                material = new Material(basis) { name = "ContactShadow" };
                AssetDatabase.CreateAsset(material, path);
            }
            Color tint = DioramaPalette.DepotNavy;
            tint.a = 0.2f;
            material.shader = basis.shader;
            material.color = tint;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_VertexColorWeight"))
                material.SetFloat("_VertexColorWeight", 0f);
            if (material.HasProperty("_VertexAlphaWeight"))
                material.SetFloat("_VertexAlphaWeight", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.enableInstancing = true;
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddDressingShadow(
            GameObject instance, Mesh mesh, Material material)
        {
            Bounds bounds = WorldBounds(instance);
            var shadow = new GameObject("contact-shadow");
            shadow.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shadow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = -10;
            shadow.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0.245f);
            shadow.transform.rotation = Quaternion.identity;
            shadow.transform.localScale = new Vector3(
                Mathf.Clamp(bounds.size.x * 0.9f, 0.24f, 1.2f),
                Mathf.Clamp(bounds.size.y * 0.48f, 0.18f, 0.9f), 1f);
            shadow.transform.SetParent(instance.transform, true);
        }

        [MenuItem("Cat Metro/Configure 900x2000 Evidence Game View")]
        public static void ConfigurePortraitEvidenceGameView()
        {
            const int width = 900;
            const int height = 2000;
            const string label = "Cat Metro Evidence 900x2000";
            const System.Reflection.BindingFlags all =
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic;

            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes", true);
            Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType", true);
            Type groupKindType = editorAssembly.GetType(
                "UnityEditor.GameViewSizeGroupType", true);
            Type gameViewType = editorAssembly.GetType("UnityEditor.GameView", true);
            var gameView = EditorWindow.GetWindow(gameViewType);

            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance", all).GetValue(null, null);
            object currentGroupKind = null;
            foreach (string propertyName in new[]
            {
                "GameViewSizeGroupType", "currentSizeGroupType", "sizeGroupType",
            })
            {
                var property = gameViewType.GetProperty(propertyName, all);
                if (property == null) continue;
                currentGroupKind = property.GetValue(
                    property.GetMethod.IsStatic ? null : gameView, null);
                if (currentGroupKind != null) break;
            }
            if (currentGroupKind == null)
            {
                string fallback = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                    ? "Android" : "Standalone";
                currentGroupKind = Enum.Parse(groupKindType, fallback);
            }
            object group = sizesType.GetMethod("GetGroup", all)
                .Invoke(sizes, new[] { currentGroupKind });
            Type groupType = group.GetType();
            int builtinCount = (int)groupType.GetMethod("GetBuiltinCount", all)
                .Invoke(group, null);
            int customCount = (int)groupType.GetMethod("GetCustomCount", all)
                .Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize", all);

            int selectedIndex = -1;
            for (int i = 0; i < builtinCount + customCount; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                int candidateWidth = (int)sizeType.GetProperty("width", all)
                    .GetValue(size, null);
                int candidateHeight = (int)sizeType.GetProperty("height", all)
                    .GetValue(size, null);
                if (candidateWidth == width && candidateHeight == height)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
                object newSize = Activator.CreateInstance(sizeType,
                    new[] { fixedResolution, (object)width, height, label });
                groupType.GetMethod("AddCustomSize", all)
                    .Invoke(group, new[] { newSize });
                selectedIndex = builtinCount + customCount;
            }

            gameViewType.GetProperty("selectedSizeIndex", all)
                .SetValue(gameView, selectedIndex, null);
            gameView.Repaint();
            Debug.Log("CAT_METRO_EVIDENCE_GAME_VIEW 900x2000 group=" + currentGroupKind
                + " index=" + selectedIndex);
        }

        [MenuItem("Cat Metro/Capture Diorama Orientation Sheet")]
        public static void CaptureOrientationSheet()
        {
            PolyforkLocalCustody.RequireExact();
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

            var prefabs = new GameObject[names.Length];
            for (int row = 0; row < names.Length; row++)
            {
                string prefabPath = PrefabRoot + names[row] + ".prefab";
                prefabs[row] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabs[row] == null)
                    throw new InvalidOperationException("Missing prefab " + prefabPath);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            for (int row = 0; row < names.Length; row++)
            {
                for (int column = 0; column < rotations.Length; column++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[row]);
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
            string output = WritePrivateOrientationSheet(image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(textureTarget);
            Debug.Log("CAT_METRO_ORIENTATION_SHEET " + output);
        }

        private static string WritePrivateOrientationSheet(byte[] png)
        {
            if (png == null) throw new ArgumentNullException(nameof(png));
            string directory = Path.Combine(Path.GetTempPath(),
                "catmetro-polyfork-orientations-" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(directory) || File.Exists(directory))
                throw new IOException("Orientation-sheet temp path already exists");

            var directoryInfo = Directory.CreateDirectory(directory);
            if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Orientation-sheet temp directory is a reparse point");

            string output = Path.Combine(directory, "orientations.png");
            using (var stream = new FileStream(
                output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(png, 0, png.Length);
            return output;
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
