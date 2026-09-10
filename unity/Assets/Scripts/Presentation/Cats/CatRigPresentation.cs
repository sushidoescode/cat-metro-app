using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Cats
{
    /// <summary>Clone-only motion and head shape for the locally admitted, weighted paid rig.</summary>
    [DisallowMultipleComponent]
    public sealed class CatRigPresentation : MonoBehaviour
    {
        public const string ControllerResourcePath = "CatMotion/BoardCatMotionController";
        public const string OpenCarriageControllerResourcePath = "CatMotion/OpenCarriage/OpenCarriageMotion";
        public const string RideClip = "Cat_Ride";
        public const string WeightedHeadPath = "Armature/tripo::Root/tripo::Head_0/tripo::Head_1";
        public const float HeadScale = 1.28f;
        private static GameObject _verifiedSource;
        private static readonly string[] RequiredClips =
            { "Cat_IdleSit", "Cat_Walk", "Cat_Board", "Cat_Alight", "Cat_Celebrate", "Cat_Ride" };
        private Animator _animator;
        private RuntimeAnimatorController _authoredController;
        private AnimatorOverrideController _openCarriageController;

        public Transform HeadTransform { get; private set; }
        public Vector3 SourceHeadScale { get; private set; }
        public AnimationClip IdleClip { get; private set; }
        public bool AuthoredMotionInstalled => _authoredController != null
            && (_animator == null || _animator.runtimeAnimatorController == _authoredController);
        public bool OpenCarriageMotionInstalled => _openCarriageController != null && AuthoredMotionInstalled;

        // Board creation calls this only after admitting the actual open carriage. Profiles
        // and fallback vehicles retain the shared base controller and its original six clips.
        public bool TryUseOpenCarriageMotion()
        {
            if (OpenCarriageMotionInstalled) return true;
            if (!AuthoredMotionInstalled || _animator == null) return false;
            var candidate = Resources.Load<AnimatorOverrideController>(OpenCarriageControllerResourcePath);
            if (!HasOpenCarriageClips(candidate, _authoredController, out AnimationClip idle)) return false;
            _openCarriageController = candidate;
            _authoredController = candidate;
            IdleClip = idle;
            _animator.runtimeAnimatorController = candidate;
            ApplyHeadShape();
            return true;
        }

        private static bool HasOpenCarriageClips(AnimatorOverrideController candidate,
            RuntimeAnimatorController baseline, out AnimationClip idle)
        {
            idle = null;
            if (candidate == null || baseline == null || candidate.runtimeAnimatorController != baseline
                || !HasExpectedClips(candidate, baseline, out idle)) return false;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            candidate.GetOverrides(overrides);
            if (overrides.Count != RequiredClips.Length) return false;
            foreach (AnimationClip original in baseline.animationClips)
            {
                int matches = 0;
                foreach (var pair in overrides)
                {
                    if (pair.Key != original) continue;
                    matches++;
                    bool replaced = original.name == "Cat_Ride" || original.name == "Cat_Board" || original.name == "Cat_Alight";
                    if (!replaced)
                    {
                        if (pair.Value != null && pair.Value != original) return false;
                        continue;
                    }
                    AnimationClip replacement = Resources.Load<AnimationClip>("CatMotion/OpenCarriage/" + original.name);
                    if (replacement == null || pair.Value != replacement || replacement == original
                        || replacement.name != original.name || replacement.empty || replacement.hasRootCurves
                        || replacement.events.Length != 0 || Mathf.Abs(replacement.length - original.length) > .00001f)
                        return false;
                }
                if (matches != 1) return false;
            }
            return true;
        }

        // Called only after CatModelCatalog has admitted the original prefab. Resource identity
        // plus direct Head_1 skin weights keep synthetic/adapted rigs on their existing path.
        public static CatRigPresentation TryInstall(GameObject instance, GameObject sourcePrefab)
        {
            if (instance == null || instance == sourcePrefab || sourcePrefab == null
                || sourcePrefab != Resources.Load<GameObject>(CatModelCatalog.ResourcePath)) return null;
            Animator sourceAnimator = sourcePrefab.GetComponentInChildren<Animator>(true);
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (sourceAnimator == null || animator == null) return null;
            Transform sourceHead = sourceAnimator.transform.Find(WeightedHeadPath);
            Transform head = animator.transform.Find(WeightedHeadPath);
            if (sourceHead == null || head == null) return null;
            if (_verifiedSource != sourcePrefab)
            {
                if (!HasDirectWeightedHead(sourceAnimator, sourceHead)) return null;
                _verifiedSource = sourcePrefab;
            }

            CatRigPresentation presentation = animator.GetComponent<CatRigPresentation>();
            if (presentation != null)
            {
                presentation.ApplyHeadShape();
                return presentation;
            }
            presentation = animator.gameObject.AddComponent<CatRigPresentation>();
            presentation._animator = animator;
            presentation.HeadTransform = head;
            // Always read the source scale, never a previously enlarged clone scale.
            presentation.SourceHeadScale = sourceHead.localScale;
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(ControllerResourcePath);
            if (HasExpectedClips(controller, sourceAnimator.runtimeAnimatorController, out AnimationClip idle))
            {
                presentation._authoredController = controller;
                presentation.IdleClip = idle;
                animator.runtimeAnimatorController = controller;
            }
            animator.applyRootMotion = false;
            presentation.ApplyHeadShape();
            return presentation;
        }

        private static bool HasDirectWeightedHead(Animator source, Transform head)
        {
            foreach (SkinnedMeshRenderer skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                int bone = Array.IndexOf(skin.bones, head);
                if (bone < 0) continue;
                // The provider import disables Read/Write for players. Its resource identity,
                // exact mesh and 30-bone skeleton retain the offline weight proof there.
                if (!skin.sharedMesh.isReadable)
                    return skin.sharedMesh.name == "tripo_node_699f4d25-5654-463d-b024-d3774811f482"
                        && skin.bones.Length == 30 && bone == 2;
                int weighted = 0;
                foreach (BoneWeight weight in skin.sharedMesh.boneWeights)
                {
                    float influence = (weight.boneIndex0 == bone ? weight.weight0 : 0f)
                        + (weight.boneIndex1 == bone ? weight.weight1 : 0f)
                        + (weight.boneIndex2 == bone ? weight.weight2 : 0f)
                        + (weight.boneIndex3 == bone ? weight.weight3 : 0f);
                    if (influence >= 0.25f) weighted++;
                }
                // The pinned Unity mesh has 4,454 such vertices. A weak ear parent or a
                // small fixture with the same names is not the calibrated anatomical head.
                if (weighted > 1000) return true;
            }
            return false;
        }

        private static bool HasExpectedClips(RuntimeAnimatorController candidate,
            RuntimeAnimatorController original, out AnimationClip idle)
        {
            idle = null;
            if (candidate == null || original == null) return false;
            AnimationClip[] clips = candidate.animationClips;
            if (clips.Length != RequiredClips.Length) return false;
            AnimationClip originalWalk = Array.Find(original.animationClips, c => c != null && c.name == CatModelCatalog.WalkClip);
            foreach (string name in RequiredClips)
            {
                AnimationClip found = null;
                foreach (AnimationClip clip in clips)
                {
                    if (clip == null || clip.name != name) continue;
                    if (found != null || clip.empty || clip.length <= 0f || clip.hasRootCurves) return false;
                    found = clip;
                }
                if (found == null) return false;
                if (name == CatModelCatalog.WalkClip && found != originalWalk) return false;
                if (name == CatModelCatalog.IdleSitClip) idle = found;
            }
            return true;
        }

        public void ApplyHeadShape()
        {
            if (HeadTransform != null) HeadTransform.localScale = SourceHeadScale * HeadScale;
        }

        public void SampleIdle(float seconds)
        {
            if (!AuthoredMotionInstalled || IdleClip == null) return;
            float time = float.IsFinite(seconds) ? Mathf.Repeat(Mathf.Max(0f, seconds), IdleClip.length) : 0f;
            // Profile mounts deliberately remove Animator after their first sample. Sampling
            // transform curves directly keeps that lifecycle and does not recreate a driver.
            IdleClip.SampleAnimation(gameObject, time);
            ApplyHeadShape();
        }

        // Provider walk includes unit scale keys. Reapply the absolute source-derived shape
        // after Animator evaluation, including repeated playback and occupant reuse.
        private void LateUpdate() => ApplyHeadShape();
    }
}
