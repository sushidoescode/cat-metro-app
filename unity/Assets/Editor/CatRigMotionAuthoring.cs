using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CatMetro.Presentation.Cats;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CatMetro.EditorTools
{
    /// <summary>
    /// Authors project-original motion on a disposable admitted rig. The paid FBX, prefab,
    /// controller and original clips are inputs only; all writes have an exact output allowlist.
    /// </summary>
    public static class CatRigMotionAuthoring
    {
        public const string OutputRoot = "Assets/Art/Original/CatMotion/Resources/CatMotion";
        public const string ControllerPath = OutputRoot + "/BoardCatMotionController.controller";
        public const string ProvenancePath = OutputRoot + "/PROVENANCE.json";
        private const string Owner = "CatMetro.ProjectOriginalCatMotion.v1";
        private const string SourceSha256 =
            "9d87464e3954954d5d64e8eb4aee6150a11f9efcdf320a9f82adb96449dca974";
        private const string Body = "Armature/tripo::Root/tripo::Head_0";
        private const string Head = Body + "/tripo::Head_1";
        private const string Tail = Body + "/bone_9/bone_12";
        private const string EarA = Head + "/tripo::Head_2/bone_4";
        private const string EarB = Head + "/tripo::Head_2/tripo::Head_3";
        private const float SampleRate = 60f;
        private static readonly string[] Names =
            { "Cat_IdleSit", "Cat_Ride", "Cat_Board", "Cat_Alight", "Cat_Celebrate" };
        private static readonly float[] Durations = { 3.2f, 1.6f, 0.18f, 0.18f, 0.48f };

        [MenuItem("CatMetro/Cat Rig/Author Original Motion")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author cat motion outside Play Mode.");
            if (HashFile(CatRigImportPipeline.SourcePath) != SourceSha256)
                throw new InvalidDataException("Pinned provider FBX hash differs; no outputs written.");
            PreflightOutputs();
            var entry = new CatModelCatalog.Entry(Resources.Load<GameObject>(CatModelCatalog.ResourcePath),
                CatModelCatalog.ResourceFacingYaw, CatModelCatalog.ResourceCosmeticCatId);
            var catalog = CatModelCatalog.FromEntry(entry);
            if (catalog.AdmittedEntryCount != 1)
                throw new InvalidOperationException("Cat rig is not admitted: " + catalog.RejectionReason);
            if (AssetDatabase.GetAssetPath(entry.Prefab) != CatRigImportPipeline.PrefabPath)
                throw new InvalidOperationException("Resources resolved an unexpected cat prefab.");

            FileReceipt[] protectedInputs = SnapshotInputs();
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null;
            AnimationClip[] clips = Array.Empty<AnimationClip>();
            try
            {
                host = new GameObject("CatMotion disposable authoring host");
                SceneManager.MoveGameObjectToScene(host, scene);
                // Clone the admitted source directly. Runtime catalog instantiation may install
                // these new clips and presentation head scale; neither belongs in the neutral input.
                GameObject clone = Object.Instantiate(entry.Prefab, host.transform, false);
                Animator animator = clone.GetComponentInChildren<Animator>(true);
                AnimationClip[] originals = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
                AnimationClip neutral = originals.Single(c => c.name == CatModelCatalog.IdleSitClip);
                AnimationClip walk = originals.Single(c => c.name == CatModelCatalog.WalkClip);
                if (AssetDatabase.GetAssetPath(walk) != CatRigImportPipeline.SourcePath
                    || AssetDatabase.GetAssetPath(neutral) != CatRigImportPipeline.OutputRoot + "/Cat_IdleSit.anim")
                    throw new InvalidOperationException("Expected original local idle and provider walk inputs.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0f);
                animator.enabled = false;
                clips = GenerateClips(animator, neutral, walk);
                // GenerateClips restores exactly the sampled Unity baseline before returning.
                BonePose[] baseline = Capture(animator.transform);
                EnsureFolder(OutputRoot);
                for (int i = 0; i < clips.Length; i++) clips[i] = SaveClip(clips[i]);
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                    AssetDatabase.SetLabels(controller, new[] { Owner });
                }
                ConfigureController(controller, clips, walk);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
                foreach (AnimationClip clip in clips) AssetDatabase.SaveAssetIfDirty(clip);

                var provenance = new Provenance
                {
                    generator = Owner,
                    generatorSourcePath = "Assets/Editor/CatRigMotionAuthoring.cs",
                    generatorSourceSha256 = HashFile("Assets/Editor/CatRigMotionAuthoring.cs"),
                    authorship = "Project-original rotation curves; existing paid provider walk referenced unchanged.",
                    sourceFbxPath = CatRigImportPipeline.SourcePath,
                    sourceFbxSha256 = SourceSha256,
                    baselineClipPath = AssetDatabase.GetAssetPath(neutral),
                    baselineClipSha256 = HashFile(AssetDatabase.GetAssetPath(neutral)),
                    referenceBasis = "Sample original Cat_IdleSit at t=0 on a disposable admitted Unity prefab clone. "
                        + "Animator space faces -X, up +Y. Premultiply world rotations about Animator +Z, "
                        + "then read local quaternions after parent-first application. Blender +Y rotation maps "
                        + "to this axis; no Blender quaternion or bind-pose values are copied.",
                    anatomy = "Head_0 weighted torso hierarchy; Head_1 weighted head; bone_12 weighted tail; "
                        + "bone_4 and Head_3 weighted ears. Coupled/mislabeled limb chains remain at sampled rest. "
                        + "Celebrate lifts forepaws through a small body rear, not a direct limb-chain rotation.",
                    timing = "60 Hz endpoint-inclusive sampling; IdleSit 3.2 s, Ride 1.6 s loops; "
                        + "Board/Alight 0.18 s smooth transitions; Celebrate 0.48 s returns to the neutral platform pose.",
                    motionParameters = "Idle degrees from source neutral: body 0.65*sin, head -0.35*sin, roll 1.2*sin, "
                        + "tail 45+3*sin; ears +3.5/-2.5 pulse. Ride degrees relative to seat: body 0.8*sin, head -0.5*sin, roll 0.8*sin, "
                        + "tail 2*sin. Celebrate degrees from source neutral: body +3 at 0.10 s, -8 at 0.22 s, 0 at 0.48 s; head -0.5*body, "
                        + "tail 45+12 pulse, head roll +2 pulse, ears +/-2 pulse. Animator +Z is pitch, +X head roll.",
                    seatedAnimatorDegrees = new Vector3(-35f, 32f, 45f),
                    scalePolicy = "No scale curves. Presenter owns head enlargement. Walk scale curves must remain at sampled baseline.",
                    translationPolicy = "Static sampled child positions only; no Animator-root curves, dynamic translations, hops or root motion.",
                    baseline = baseline,
                    clips = clips.Select(DescribeClip).ToArray(),
                    walk = DescribeReference(walk),
                    controller = new FileReceipt { path = ControllerPath, sha256 = HashFile(ControllerPath) },
                    protectedInputs = protectedInputs,
                };
                RequireOwnedPath(ProvenancePath);
                File.WriteAllText(Absolute(ProvenancePath), JsonUtility.ToJson(provenance, true) + "\n",
                    new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ProvenancePath, ImportAssetOptions.ForceSynchronousImport);
                foreach (ClipReceipt receipt in provenance.clips)
                    Debug.Log("CAT_MOTION_CLIP " + JsonUtility.ToJson(receipt));
            }
            finally
            {
                foreach (AnimationClip clip in clips)
                    if (clip != null && !EditorUtility.IsPersistent(clip)) Object.DestroyImmediate(clip);
                if (host != null) Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (FileReceipt input in protectedInputs)
                    if (HashFile(input.path) != input.sha256)
                        throw new InvalidDataException("Protected input changed during authoring: " + input.path);
            }
            Debug.Log("CAT_MOTION_AUTHORING PASS controller=" + ControllerPath + " provenance=" + ProvenancePath);
        }

        private static AnimationClip[] GenerateClips(Animator animator, AnimationClip neutral, AnimationClip walk)
        {
            if (animator == null || EditorUtility.IsPersistent(animator))
                throw new InvalidOperationException("Motion sampling requires a disposable rig instance.");
            neutral.SampleAnimation(animator.gameObject, 0f);
            BonePose[] baseline = Capture(animator.transform);
            foreach (string path in new[] { Body, Head, Tail, EarA, EarB })
                if (!baseline.Any(b => b.path == path))
                    throw new InvalidDataException("Missing calibrated control: " + path);
            ValidateWalkBindings(walk, baseline);
            var clips = new List<AnimationClip>();
            try
            {
                for (int index = 0; index < Names.Length; index++)
                {
                    var clip = new AnimationClip { name = Names[index], frameRate = SampleRate, legacy = false };
                    clips.Add(clip);
                    int steps = Mathf.CeilToInt(Durations[index] * SampleRate);
                    var rotations = baseline.Select(_ => new Quaternion[steps + 1]).ToArray();
                    for (int sample = 0; sample <= steps; sample++)
                    {
                        Restore(animator.transform, baseline);
                        ApplyPose(animator.transform, index, sample / (float)steps);
                        for (int bone = 0; bone < baseline.Length; bone++)
                        {
                            Quaternion q = animator.transform.Find(baseline[bone].path).localRotation;
                            if (sample > 0 && Quaternion.Dot(rotations[bone][sample - 1], q) < 0f)
                                q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                            rotations[bone][sample] = q;
                        }
                    }
                    for (int bone = 0; bone < baseline.Length; bone++)
                    {
                        for (int axis = 0; axis < 3; axis++)
                            SetCurve(clip, baseline[bone].path, "m_LocalPosition." + "xyz"[axis],
                                new[] { baseline[bone].position[axis], baseline[bone].position[axis] }, Durations[index]);
                        for (int axis = 0; axis < 4; axis++)
                        {
                            int component = axis;
                            float[] values = rotations[bone].Select(q => q[component]).ToArray();
                            if (values.All(v => Mathf.Abs(v - values[0]) < 0.0000001f))
                                values = new[] { values[0], values[0] };
                            SetCurve(clip, baseline[bone].path, "m_LocalRotation." + "xyzw"[axis], values, Durations[index]);
                        }
                    }
                    clip.EnsureQuaternionContinuity();
                    AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = index < 2;
                    settings.loopBlend = false;
                    settings.startTime = 0f;
                    settings.stopTime = Durations[index];
                    settings.keepOriginalOrientation = true;
                    settings.keepOriginalPositionXZ = true;
                    settings.keepOriginalPositionY = true;
                    settings.heightFromFeet = false;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    if (clip.hasRootCurves)
                        throw new InvalidDataException("Original clip unexpectedly contains root motion: " + clip.name);
                }
                return clips.ToArray();
            }
            catch
            {
                foreach (AnimationClip clip in clips) Object.DestroyImmediate(clip);
                throw;
            }
            finally { Restore(animator.transform, baseline); }
        }

        private static void ApplyPose(Transform animator, int clip, float phase)
        {
            float seat = clip == 2 ? Smooth(phase) : clip == 3 ? 1f - Smooth(phase) : 1f;
            float body = -35f * seat, head = 32f * seat, tail = 45f * seat;
            float roll = 0f, earA = 0f, earB = 0f;
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            if (clip == 0)
            {
                // The neutral torso/head preserve the weighted chest tuft in profile portraits.
                // Breathing stays small; the carriage seat remains in Ride, Board and Alight.
                body = 0.65f * wave;
                head = -0.35f * wave;
                roll = 1.2f * wave;
                tail += 3f * wave;
                earA = 3.5f * Pulse(phase, 0.58f, 0.7f);
                earB = -2.5f * Pulse(phase, 0.65f, 0.78f);
            }
            else if (clip == 1)
            {
                body += 0.8f * wave;
                head -= 0.5f * wave;
                roll = 0.8f * wave;
                tail += 2f * wave;
            }
            else if (clip == 4)
            {
                // Anticipation at 0.10 s, gentle rear at 0.22 s, settle to the neutral platform pose.
                float rear = phase < 0.1f / 0.48f
                    ? Mathf.Lerp(0f, 3f, Smooth(phase / (0.1f / 0.48f)))
                    : phase < 0.22f / 0.48f
                        ? Mathf.Lerp(3f, -8f, Smooth((phase - 0.1f / 0.48f) / (0.12f / 0.48f)))
                        : Mathf.Lerp(-8f, 0f, Smooth((phase - 0.22f / 0.48f) / (0.26f / 0.48f)));
                body = rear;
                head = -rear * 0.5f;
                tail += 12f * Pulse(phase, 0.14f, 0.92f);
                roll = 2f * Pulse(phase, 0.22f, 0.88f);
                earA = 2f * Pulse(phase, 0.25f, 0.75f);
                earB = -earA;
            }
            Rotate(animator, Body, Vector3.forward, body);
            Rotate(animator, Head, Vector3.forward, head);
            Rotate(animator, Head, Vector3.right, roll);
            Rotate(animator, Tail, Vector3.forward, tail);
            Rotate(animator, EarA, Vector3.forward, earA);
            Rotate(animator, EarB, Vector3.forward, earB);
        }

        private static float Smooth(float value) => Mathf.SmoothStep(0f, 1f, value);
        private static float Pulse(float phase, float start, float end) => phase <= start || phase >= end
            ? 0f : Mathf.Pow(Mathf.Sin(Mathf.PI * (phase - start) / (end - start)), 2f);
        private static void Rotate(Transform animator, string path, Vector3 axis, float angle)
        {
            Transform bone = animator.Find(path);
            bone.rotation = Quaternion.AngleAxis(angle, animator.TransformDirection(axis)) * bone.rotation;
        }
        private static BonePose[] Capture(Transform animator) => animator.GetComponentsInChildren<Transform>(true)
            .Where(t => t != animator)
            .Select(t => new BonePose
            {
                path = AnimationUtility.CalculateTransformPath(t, animator), position = t.localPosition,
                rotation = t.localRotation, scale = t.localScale,
                rotationInAnimator = Quaternion.Inverse(animator.rotation) * t.rotation,
            }).OrderBy(b => b.path, StringComparer.Ordinal).ToArray();
        private static void Restore(Transform animator, BonePose[] baseline)
        {
            foreach (BonePose bone in baseline)
            {
                Transform transform = animator.Find(bone.path);
                transform.localPosition = bone.position;
                transform.localRotation = bone.rotation;
                transform.localScale = bone.scale;
            }
        }
        private static void ValidateWalkBindings(AnimationClip walk, BonePose[] baseline)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(walk))
            {
                if (binding.type != typeof(Transform) || binding.path.Length == 0
                    || !baseline.Any(b => b.path == binding.path))
                    throw new InvalidDataException("Unsupported provider walk binding: " + binding.path + " " + binding.propertyName);
                if (binding.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal))
                {
                    int axis = "xyz".IndexOf(binding.propertyName.Last());
                    float expected = baseline.Single(b => b.path == binding.path).scale[axis];
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(walk, binding);
                    var values = curve.keys.Select(k => k.value).ToList();
                    for (int key = 1; key < curve.length; key++)
                        for (int subdivision = 1; subdivision < 4; subdivision++)
                            values.Add(curve.Evaluate(Mathf.Lerp(curve.keys[key - 1].time,
                                curve.keys[key].time, subdivision / 4f)));
                    float deviation = values.Count == 0 ? 0f : values.Max(v => Mathf.Abs(v - expected));
                    string range = string.Format(CultureInfo.InvariantCulture,
                        "{0} {1}: baseline={2:R} observedMin={3:R} observedMax={4:R} maxDeviation={5:R} tolerance=0.0001",
                        binding.path, binding.propertyName, expected,
                        values.Count == 0 ? expected : values.Min(), values.Count == 0 ? expected : values.Max(), deviation);
                    Debug.Log("CAT_MOTION_WALK_SCALE " + range);
                    if (deviation > 0.0001f)
                        throw new InvalidDataException("Provider walk animates scale; presenter-owned scale cannot safely reset it: " + range);
                }
                else if (!binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)
                    && !binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)
                    && !binding.propertyName.StartsWith("localEulerAngles", StringComparison.Ordinal))
                    throw new InvalidDataException("Unsupported provider walk property: " + binding.propertyName);
            }
            if (AnimationUtility.GetObjectReferenceCurveBindings(walk).Length != 0)
                throw new InvalidDataException("Provider walk has unsupported object reference animation.");
        }
        private static void SetCurve(AnimationClip clip, string path, string property, float[] values, float duration)
        {
            var curve = new AnimationCurve(values.Select((value, index) =>
                new Keyframe(duration * index / (values.Length - 1), value)).ToArray());
            for (int index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static AnimationClip SaveClip(AnimationClip generated)
        {
            string path = OutputRoot + "/" + generated.name + ".anim";
            RequireOwnedPath(path);
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                AssetDatabase.SetLabels(generated, new[] { Owner });
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        private static void ConfigureController(AnimatorController controller, AnimationClip[] clips, AnimationClip walk)
        {
            if (controller.layers.Length != 1)
                throw new InvalidDataException("Owned controller must have one layer.");
            AnimatorControllerLayer layer = controller.layers[0];
            AnimatorStateMachine machine = layer.stateMachine;
            if (machine.stateMachines.Length != 0 || machine.anyStateTransitions.Length != 0
                || machine.entryTransitions.Length != 0 || machine.behaviours.Length != 0
                || machine.states.Any(s => s.state.transitions.Length != 0 || s.state.behaviours.Length != 0))
                throw new InvalidDataException("Owned controller contains unexpected graph additions.");
            AnimationClip[] motions = clips.Concat(new[] { walk }).ToArray();
            if (machine.states.Any(s => !motions.Any(c => c.name == s.state.name))
                || machine.states.GroupBy(s => s.state.name).Any(g => g.Count() != 1))
                throw new InvalidDataException("Owned controller contains unexpected or duplicate states.");
            layer.name = "Base Layer";
            layer.defaultWeight = 1f;
            layer.avatarMask = null;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.syncedLayerIndex = -1;
            layer.iKPass = false;
            controller.layers = new[] { layer };
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (AnimationClip motion in motions)
            {
                AnimatorState state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == motion.name)
                    ?? machine.AddState(motion.name);
                state.motion = motion;
                state.speed = 1f;
                state.speedParameterActive = state.mirrorParameterActive = state.cycleOffsetParameterActive = state.timeParameterActive = false;
                state.mirror = false;
                state.cycleOffset = 0f;
                state.iKOnFeet = false;
                state.writeDefaultValues = true;
                if (motion.name == "Cat_IdleSit") machine.defaultState = state;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(machine);
        }

        private static void RequireOwnedPath(string path)
        {
            if (path != ControllerPath && path != ProvenancePath
                && !Names.Any(name => path == OutputRoot + "/" + name + ".anim"))
                throw new InvalidOperationException("Refusing write outside exact original-motion outputs: " + path);
            string current = UnityEngine.Application.dataPath;
            foreach (string component in path.Substring("Assets/".Length).Split('/'))
            {
                current = Path.Combine(current, component);
                foreach (string candidate in new[] { current, current + ".meta" })
                    if ((File.Exists(candidate) || Directory.Exists(candidate))
                        && (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Refusing linked output path: " + path);
            }
        }
        private static void PreflightOutputs()
        {
            foreach (string path in Names.Select(name => OutputRoot + "/" + name + ".anim")
                .Concat(new[] { ControllerPath, ProvenancePath }))
            {
                RequireOwnedPath(path);
                if (!File.Exists(Absolute(path))) continue;
                if (path == ProvenancePath)
                {
                    if (JsonUtility.FromJson<Provenance>(File.ReadAllText(Absolute(path)))?.generator != Owner)
                        throw new InvalidOperationException("Existing provenance is not owned by this generator.");
                    continue;
                }
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || !AssetDatabase.GetLabels(asset).Contains(Owner)
                    || (path == ControllerPath ? !(asset is AnimatorController) : !(asset is AnimationClip)))
                    throw new InvalidOperationException("Refusing to overwrite an unowned asset: " + path);
            }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static FileReceipt[] SnapshotInputs() => AssetDatabase.GetDependencies(CatRigImportPipeline.PrefabPath, true)
            .Concat(new[] { CatRigImportPipeline.SourcePath })
            .Where(p => p.StartsWith("Assets/Art/Generated/incoming/cat-rig/", StringComparison.Ordinal))
            .SelectMany(p => new[] { p, p + ".meta" }).Distinct()
            .Where(p => File.Exists(Absolute(p))).OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new FileReceipt { path = p, sha256 = HashFile(p) }).ToArray();
        private static string Absolute(string path) => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", path));
        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(Absolute(path)))
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(stream));
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        private static ClipReceipt DescribeClip(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip)
                .OrderBy(b => b.path, StringComparer.Ordinal).ThenBy(b => b.propertyName, StringComparer.Ordinal).ToArray();
            var canonical = new StringBuilder(clip.name).Append('|').Append(clip.length.ToString("R", CultureInfo.InvariantCulture));
            int keys = 0;
            foreach (EditorCurveBinding binding in bindings)
            {
                canonical.Append('\n').Append(binding.path).Append('|').Append(binding.propertyName);
                foreach (Keyframe key in AnimationUtility.GetEditorCurve(clip, binding).keys)
                {
                    keys++;
                    foreach (float value in new[] { key.time, key.value, key.inTangent, key.outTangent })
                        canonical.Append('|').Append(value.ToString("R", CultureInfo.InvariantCulture));
                }
            }
            using (var sha = SHA256.Create()) return new ClipReceipt
            {
                name = clip.name, path = AssetDatabase.GetAssetPath(clip), seconds = clip.length,
                loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime,
                curveCount = bindings.Length, keyCount = keys, fileSha256 = HashFile(AssetDatabase.GetAssetPath(clip)),
                curveSha256 = Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))),
            };
        }
        private static ReferenceReceipt DescribeReference(AnimationClip clip)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long localId);
            return new ReferenceReceipt { name = clip.name, path = AssetDatabase.GetAssetPath(clip), guid = guid, localId = localId };
        }
        [Serializable] private sealed class BonePose
        {
            public string path;
            public Vector3 position, scale;
            public Quaternion rotation, rotationInAnimator;
        }
        [Serializable] private sealed class FileReceipt { public string path, sha256; }
        [Serializable] private sealed class ReferenceReceipt { public string name, path, guid; public long localId; }
        [Serializable] private sealed class ClipReceipt
        {
            public string name, path, fileSha256, curveSha256;
            public float seconds;
            public bool loop;
            public int curveCount, keyCount;
        }
        [Serializable] private sealed class Provenance
        {
            public string generator, generatorSourcePath, generatorSourceSha256, authorship;
            public string sourceFbxPath, sourceFbxSha256, baselineClipPath, baselineClipSha256;
            public string referenceBasis, anatomy, timing, motionParameters, scalePolicy, translationPolicy;
            public Vector3 seatedAnimatorDegrees;
            public BonePose[] baseline;
            public ClipReceipt[] clips;
            public ReferenceReceipt walk;
            public FileReceipt controller;
            public FileReceipt[] protectedInputs;
        }
    }
}
