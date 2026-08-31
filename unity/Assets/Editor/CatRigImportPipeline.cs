using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatMetro.EditorTools
{
    /// <summary>
    /// Builds the licence-local Tripo cat into the ignored Resources seam consumed by TASK 18.
    /// The provider FBX is never rewritten: every axis, scale, material, and clip-name correction
    /// lives in generated Unity import metadata or beneath the ModelCorrection presentation node.
    /// </summary>
    public static class CatRigImportPipeline
    {
        public const string SourcePath =
            "Assets/Art/Generated/incoming/cat-rig/provider-tripo/candidate-b/" +
            "walk-fbx/tripo-out/cat-metro-walker-b-walk-fbx-6e32e93a/model.fbx";
        public const string OutputRoot =
            "Assets/Art/Generated/incoming/cat-rig/Resources/CatRigs";
        public const string PrefabPath = OutputRoot + "/BoardCatRig.prefab";

        private const string TextureRoot =
            "Assets/Art/Generated/incoming/cat-rig/UnityDerived/Textures";
        private const string BaseMapPath = TextureRoot
            + "/Color_699f4d25-5654-463d-b024-d3774811f482.jpg";
        private const string ControllerPath = OutputRoot + "/BoardCatRig.controller";
        private const string MaterialPath = OutputRoot + "/BoardCatRig.mat";
        private const string WalkClipName = "Cat_Walk";
        private const string FallbackBindingPath = "Armature";
        private const float FallbackFrameRate = 24f;
        private const float FallbackDuration = 1f / FallbackFrameRate;
        private const float TargetHeight = 1f;
        private const string ExpectedSourceSha256 =
            "9d87464e3954954d5d64e8eb4aee6150a11f9efcdf320a9f82adb96449dca974";
        private const string ExpectedBaseMapSha256 =
            "7581075a68dadd5c1f6d89b5b294adf2c311201cb2cf9a09921c4348d05a61a9";

        private static readonly string[] FallbackClipNames =
        {
            "Cat_IdleSit", "Cat_Board", "Cat_Alight", "Cat_Celebrate",
        };

        [MenuItem("CatMetro/Cat Rig/Build Local Import")]
        public static void BuildLocalImport()
        {
            RequireAssetSha256(SourcePath, ExpectedSourceSha256, "provider FBX");
            EnsureAssetFolder(OutputRoot);
            EnsureAssetFolder(TextureRoot);
            ConfigureSourceImporter();

            Texture2D baseMap = ExtractAndConfigureTextures();
            DisableSourceMaterialImport();
            Material material = CreateUrpMaterial(baseMap);
            AnimationClip walk = FindWalkClip();
            AnimatorController controller = CreateController(walk);
            GameObject prefab = CreateNormalizedPrefab(controller, material);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            LogReadback(prefab, walk, baseMap);
        }

        // Unity CLI entry point. Deliberately separate from the menu method so an Editor user
        // does not have their session closed after rebuilding the local licensed asset.
        public static void BuildLocalImportAndExit()
        {
            try
            {
                BuildLocalImport();
                Debug.Log("CAT_RIG_IMPORT_RESULT PASS prefab=" + PrefabPath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("CAT_RIG_IMPORT_RESULT FAIL " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureSourceImporter()
        {
            var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("Missing final Tripo FBX", SourcePath);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.resampleCurves = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            importer.materialImportMode = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath) == null
                ? ModelImporterMaterialImportMode.ImportStandard
                : ModelImporterMaterialImportMode.None;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            ModelImporterClipAnimation[] sourceClips = importer.defaultClipAnimations;
            ModelImporterClipAnimation[] exactProviderTakes =
                (sourceClips ?? Array.Empty<ModelImporterClipAnimation>())
                .Where(clip => clip.takeName == "preset:quadruped:walk")
                .ToArray();
            if (exactProviderTakes.Length != 1)
                throw new InvalidDataException("Expected one exact preset:quadruped:walk take; found "
                    + string.Join("; ", (sourceClips ?? Array.Empty<ModelImporterClipAnimation>())
                        .Select(clip => clip.takeName + " [" + clip.firstFrame + ", "
                            + clip.lastFrame + "]")));

            // Tripo's FBX contains an identical Armature|-prefixed duplicate. Selecting by the
            // provider task's exact preset name keeps one authoritative clip in Unity.
            ModelImporterClipAnimation walk = exactProviderTakes[0];
            walk.name = WalkClipName;
            walk.loopTime = true;
            walk.loopPose = true;
            walk.lockRootPositionXZ = true;
            walk.lockRootHeightY = true;
            walk.lockRootRotation = true;
            walk.keepOriginalPositionXZ = false;
            walk.keepOriginalPositionY = false;
            walk.keepOriginalOrientation = false;
            importer.clipAnimations = new[] { walk };
            importer.SaveAndReimport();
        }

        private static Texture2D ExtractAndConfigureTextures()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(SourcePath);
            string[] existing = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot });
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath) == null)
            {
                if (existing.Length != 0)
                    throw new InvalidDataException(
                        "Texture extraction folder is partial or stale; expected no textures before extraction.");
                if (!importer.ExtractTextures(TextureRoot))
                    throw new InvalidDataException("The FBX contains no extractable colour texture.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => new { Path = path, Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path) })
                .Where(entry => entry.Texture != null)
                .ToArray();
            if (textures.Length == 0)
                throw new InvalidDataException("Texture extraction produced no Texture2D assets.");

            string[] colourPaths = textures.Where(entry => IsColourMap(entry.Path))
                .Select(entry => entry.Path).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (colourPaths.Length != 1 || colourPaths[0] != BaseMapPath)
                throw new InvalidDataException("Expected only the pinned provider colour atlas "
                    + BaseMapPath + "; found: " + string.Join(", ", colourPaths));

            foreach (var entry in textures)
            {
                var textureImporter = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
                if (textureImporter == null) continue;
                string name = Path.GetFileNameWithoutExtension(entry.Path).ToLowerInvariant();
                bool isNormal = name.Contains("normal");
                bool isData = name.Contains("orm") || name.Contains("rough")
                    || name.Contains("metal") || name.Contains("occlusion");
                textureImporter.maxTextureSize = 1024;
                textureImporter.mipmapEnabled = true;
                textureImporter.textureCompression = TextureImporterCompression.Compressed;
                textureImporter.crunchedCompression = false;
                textureImporter.textureType = isNormal
                    ? TextureImporterType.NormalMap : TextureImporterType.Default;
                textureImporter.sRGBTexture = !isNormal && !isData;
                PinMobileTextureBudget(textureImporter, "Android");
                PinMobileTextureBudget(textureImporter, "iPhone");
                textureImporter.SaveAndReimport();
            }

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
            if (baseMap == null)
                throw new InvalidDataException("Could not identify the provider colour atlas.");
            RequireAssetSha256(BaseMapPath, ExpectedBaseMapSha256, "provider colour atlas");
            if (baseMap.width > 1024 || baseMap.height > 1024)
                throw new InvalidDataException("Colour atlas did not downscale to 1024 pixels.");
            return baseMap;
        }

        private static void PinMobileTextureBudget(TextureImporter importer, string platform)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void DisableSourceMaterialImport()
        {
            var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("Missing final Tripo FBX", SourcePath);
            // The generated prefab supplies its one canonical URP material. Leaving FBX material
            // import enabled pulls the otherwise-unused normal map into the recursive build closure.
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
        }

        private static bool IsColourMap(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return name.Contains("color") || name.Contains("colour") || name.Contains("albedo")
                || name.Contains("base") || name.Contains("diffuse");
        }

        private static Material CreateUrpMaterial(Texture2D baseMap)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit shader is unavailable.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var canonical = new Material(shader);
            try
            {
                canonical.name = Path.GetFileNameWithoutExtension(MaterialPath);
                canonical.enableInstancing = true;
                canonical.SetTexture("_BaseMap", baseMap);
                canonical.SetTexture("_MainTex", baseMap);
                canonical.SetColor("_BaseColor", Color.white);
                canonical.SetColor("_Color", Color.white);
                canonical.SetFloat("_Metallic", 0f);
                canonical.SetFloat("_Smoothness", 0.18f);
                // Match URP's first-render validation now, so merely capturing the prefab does
                // not rewrite the material and invalidate the provenance hash afterward.
                canonical.SetOverrideTag("RenderType", "Opaque");
                canonical.SetShaderPassEnabled("MOTIONVECTORS", false);
                // Copy into the existing asset rather than replacing it: stale Lit slots and
                // keywords are cleared while the licensing receipt's material GUID stays stable.
                EditorUtility.CopySerialized(canonical, material);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canonical);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimationClip FindWalkClip()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            if (clips.Length != 1 || clips[0].name != WalkClipName)
                throw new InvalidDataException("Expected one imported " + WalkClipName
                    + " clip, found: " + string.Join(", ", clips.Select(clip => clip.name)));
            return clips[0];
        }

        private static AnimatorController CreateController(AnimationClip walk)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            CanonicalizeControllerShell(controller);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            var expectedNames = new HashSet<string>(FallbackClipNames) { WalkClipName };
            var seenNames = new HashSet<string>();
            Vector3 fallbackBindPosition = ReadFallbackBindPosition();
            foreach (ChildAnimatorState child in stateMachine.states.ToArray())
                if (!expectedNames.Contains(child.state.name) || !seenNames.Add(child.state.name))
                    stateMachine.RemoveState(child.state);
            ClearStateMachine(stateMachine);
            AddOrUpdateState(stateMachine, WalkClipName, walk);
            foreach (string fallbackName in FallbackClipNames)
            {
                string path = OutputRoot + "/" + fallbackName + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, path);
                }
                CanonicalizeFallbackClip(clip, fallbackBindPosition);
                clip.name = fallbackName;
                clip.frameRate = FallbackFrameRate;
                EditorUtility.SetDirty(clip);
                AddOrUpdateState(stateMachine, fallbackName, clip);
            }
            stateMachine.defaultState = stateMachine.states
                .Select(child => child.state)
                .First(state => state.name == "Cat_IdleSit");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CanonicalizeControllerShell(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0 || layers[0].stateMachine == null)
                throw new InvalidDataException("Generated cat controller has no primary state machine.");

            AnimatorStateMachine primary = layers[0].stateMachine;
            AnimatorStateMachine[] staleMachines = layers.Skip(1)
                .Select(layer => layer.stateMachine)
                .Where(machine => machine != null && machine != primary)
                .Distinct().ToArray();
            AnimatorControllerLayer layer = layers[0];
            layer.name = "Base Layer";
            layer.avatarMask = null;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 1f;
            layer.iKPass = false;
            layer.syncedLayerIndex = -1;
            controller.layers = new[] { layer };
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (AnimatorStateMachine stale in staleMachines)
                UnityEngine.Object.DestroyImmediate(stale, true);
        }

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
                stateMachine.RemoveAnyStateTransition(transition);
            foreach (AnimatorTransition transition in stateMachine.entryTransitions.ToArray())
                stateMachine.RemoveEntryTransition(transition);
            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines.ToArray())
            {
                stateMachine.RemoveStateMachine(child.stateMachine);
                UnityEngine.Object.DestroyImmediate(child.stateMachine, true);
            }
            foreach (StateMachineBehaviour behaviour in stateMachine.behaviours.ToArray())
                UnityEngine.Object.DestroyImmediate(behaviour, true);
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions.ToArray())
                    child.state.RemoveTransition(transition);
                foreach (StateMachineBehaviour behaviour in child.state.behaviours.ToArray())
                    UnityEngine.Object.DestroyImmediate(behaviour, true);
            }
        }

        private static Vector3 ReadFallbackBindPosition()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            Animator animator = source == null
                ? null : source.GetComponentInChildren<Animator>(true);
            Transform armature = animator == null
                ? null : animator.transform.Find(FallbackBindingPath);
            if (armature == null)
                throw new InvalidDataException("Imported cat Animator is missing child "
                    + FallbackBindingPath + " required for fallback clip padding.");
            return armature.localPosition;
        }

        private static void CanonicalizeFallbackClip(AnimationClip clip, Vector3 bindPosition)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            // TASK 18 rejects empty or zero-length state clips. One bind-pose keyframe on a
            // real child at t > 0 keeps the pose motionless while satisfying that contract.
            // Unity packs Transform position components into a runtime Vector3 curve, so all
            // three components must be keyed or the missing bind values are sampled as zero.
            SetFallbackPositionCurve(clip, "m_LocalPosition.x", bindPosition.x);
            SetFallbackPositionCurve(clip, "m_LocalPosition.y", bindPosition.y);
            SetFallbackPositionCurve(clip, "m_LocalPosition.z", bindPosition.z);
            clip.legacy = false;
            clip.wrapMode = WrapMode.Default;
        }

        private static void SetFallbackPositionCurve(AnimationClip clip, string property,
            float bindValue)
        {
            var padBinding = EditorCurveBinding.FloatCurve(FallbackBindingPath,
                typeof(Transform), property);
            AnimationUtility.SetEditorCurve(clip, padBinding,
                new AnimationCurve(new Keyframe(FallbackDuration, bindValue)));
        }

        private static void AddOrUpdateState(AnimatorStateMachine stateMachine, string name,
            Motion motion)
        {
            AnimatorState state = stateMachine.states.Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == name)
                ?? stateMachine.AddState(name);
            state.motion = motion;
            state.speed = 1f;
            state.speedParameter = string.Empty;
            state.speedParameterActive = false;
            state.mirror = false;
            state.mirrorParameter = string.Empty;
            state.mirrorParameterActive = false;
            state.cycleOffset = 0f;
            state.cycleOffsetParameter = string.Empty;
            state.cycleOffsetParameterActive = false;
            state.timeParameter = string.Empty;
            state.timeParameterActive = false;
            state.iKOnFeet = false;
            state.tag = string.Empty;
            state.writeDefaultValues = true;
        }

        private static GameObject CreateNormalizedPrefab(AnimatorController controller,
            Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new InvalidDataException("Imported FBX has no GameObject root.");

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject wrapper = null;
            try
            {
                wrapper = new GameObject("BoardCatRig");
                SceneManager.MoveGameObjectToScene(wrapper, previewScene);
                var correction = new GameObject("ModelCorrection");
                SceneManager.MoveGameObjectToScene(correction, previewScene);
                correction.transform.SetParent(wrapper.transform, false);
                // Blender reads the provider source as +X forward, but Unity's FBX conversion
                // exposes the anatomical head along -X. TASK 18 consumes +Z forward; this is
                // the only yaw correction and stays outside the pinned provider model bytes.
                correction.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                model.name = "TripoCatWalker";
                model.transform.SetParent(correction.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                Animator[] animators = model.GetComponentsInChildren<Animator>(true);
                Animator animator = animators.FirstOrDefault();
                if (animator == null) animator = model.AddComponent<Animator>();
                foreach (Animator duplicate in model.GetComponentsInChildren<Animator>(true))
                    if (duplicate != animator) UnityEngine.Object.DestroyImmediate(duplicate);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != 1)
                    throw new InvalidDataException("Expected one skinned renderer, found "
                        + renderers.Length);
                renderers[0].sharedMaterial = material;
                renderers[0].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[0].receiveShadows = true;

                if (!TryGetLocalBounds(wrapper.transform, out Bounds rotatedBounds)
                    || rotatedBounds.size.y <= 1e-6f)
                    throw new InvalidDataException("Could not measure imported cat bounds.");
                correction.transform.localScale = Vector3.one * (TargetHeight / rotatedBounds.size.y);
                if (!TryGetLocalBounds(wrapper.transform, out Bounds scaledBounds))
                    throw new InvalidDataException("Could not measure normalized cat bounds.");
                correction.transform.localPosition = new Vector3(
                    -scaledBounds.center.x, -scaledBounds.min.y, -scaledBounds.center.z);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Prefab save failed.");
                return prefab;
            }
            finally
            {
                if (wrapper != null) UnityEngine.Object.DestroyImmediate(wrapper);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static bool TryGetLocalBounds(Transform root, out Bounds localBounds)
        {
            var meshes = new List<Tuple<Transform, Mesh>>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) meshes.Add(Tuple.Create(filter.transform, filter.sharedMesh));
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null) meshes.Add(Tuple.Create(renderer.transform, renderer.sharedMesh));

            bool initialized = false;
            localBounds = default;
            foreach (Tuple<Transform, Mesh> entry in meshes)
            {
                Vector3 min = entry.Item2.bounds.min;
                Vector3 max = entry.Item2.bounds.max;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 point = new Vector3(x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                    point = root.InverseTransformPoint(entry.Item1.TransformPoint(point));
                    if (!initialized)
                    {
                        localBounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else localBounds.Encapsulate(point);
                }
            }
            return initialized;
        }

        private static void LogReadback(GameObject prefab, AnimationClip walk, Texture2D baseMap)
        {
            SkinnedMeshRenderer renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Mesh mesh = renderer.sharedMesh;
            int triangles = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                triangles += (int)mesh.GetIndexCount(submesh) / 3;
            TryGetLocalBounds(prefab.transform, out Bounds bounds);
            Debug.Log("CAT_RIG_IMPORT_READBACK"
                + " sourceGuid=" + AssetDatabase.AssetPathToGUID(SourcePath)
                + " triangles=" + triangles
                + " vertices=" + mesh.vertexCount
                + " bones=" + renderer.bones.Length
                + " clip=" + walk.name
                + " duration=" + walk.length.ToString("F6")
                + " loop=" + walk.isLooping
                + " rootCurves=" + walk.hasRootCurves
                + " baseMap=" + baseMap.width + "x" + baseMap.height
                + " bounds=" + bounds.size.ToString("F6")
                + " minY=" + bounds.min.y.ToString("F6")
                + " shader=" + renderer.sharedMaterial.shader.name);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", assetPath));
            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void RequireAssetSha256(string assetPath, string expected, string label)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Missing " + label, fullPath);

            string actual;
            using (FileStream stream = File.OpenRead(fullPath))
            using (SHA256 sha256 = SHA256.Create())
                actual = BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidDataException(label + " SHA-256 mismatch: expected "
                    + expected + ", got " + actual);
        }

    }
}
