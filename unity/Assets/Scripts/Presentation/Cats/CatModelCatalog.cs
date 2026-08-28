using System;
using System.Collections.Generic;
using CatMetro.Presentation.Board;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatMetro.Presentation.Cats
{
    // The real model stays optional: a clean checkout keeps the existing placeholder until an
    // asset meets this strict visual-only contract. Rejection is intentionally observable so a
    // failed import can never look like an unexplained fallback.
    public sealed class CatModelCatalog
    {
        public const string ResourcePath = "CatRigs/BoardCatRig";
        public const string IdleSitClip = "Cat_IdleSit";
        public const string WalkClip = "Cat_Walk";
        public const string BoardClip = "Cat_Board";
        public const string AlightClip = "Cat_Alight";
        public const string CelebrateClip = "Cat_Celebrate";
        public const float NormalizedStandingHeight = 1f;
        public const float PresenterScale = 0.34f;

        private static readonly string[] RequiredClipNames =
        {
            IdleSitClip, WalkClip, BoardClip, AlightClip, CelebrateClip,
        };

        private readonly GameObject _prefab;

        public CatModelCatalog(GameObject prefab)
        {
            if (TryValidate(prefab, out string reason))
            {
                _prefab = prefab;
                RejectionReason = string.Empty;
            }
            else
            {
                RejectionReason = reason;
            }
        }

        public int AdmittedEntryCount => _prefab == null ? 0 : 1;
        public bool RigAdmitted => _prefab != null;
        public string RejectionReason { get; }

        public static CatModelCatalog LoadResources() =>
            new CatModelCatalog(Resources.Load<GameObject>(ResourcePath));

        public bool TryInstantiate(Transform parent, out GameObject instance)
        {
            instance = null;
            if (_prefab == null) return false;

            instance = UnityEngine.Object.Instantiate(_prefab, parent, false);
            var animator = instance.GetComponentInChildren<Animator>(true);
            animator.applyRootMotion = false;
            return true;
        }

        public static string ClipFor(CatPresentationState state)
        {
            switch (state)
            {
                case CatPresentationState.Walk: return WalkClip;
                case CatPresentationState.Board: return BoardClip;
                case CatPresentationState.Alight: return AlightClip;
                case CatPresentationState.Celebrate: return CelebrateClip;
                default: return IdleSitClip;
            }
        }

        public static bool TryValidate(GameObject prefab, out string rejectionReason)
        {
            if (prefab == null)
            {
                rejectionReason = "Missing cat rig at Resources/" + ResourcePath + ".";
                return false;
            }

            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    rejectionReason = "Cat rig has a missing component.";
                    return false;
                }

                if (component is Animation || component is Collider || component is Rigidbody
                    || component is Collider2D || component is Rigidbody2D
                    || component is BoardElementId || component is Selectable
                    || component is BaseRaycaster)
                {
                    rejectionReason = "Cat rig contains forbidden " + component.GetType().Name + ".";
                    return false;
                }
            }

            var animators = prefab.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1)
            {
                rejectionReason = "Cat rig must contain exactly one Animator.";
                return false;
            }
            if (animators[0].applyRootMotion)
            {
                rejectionReason = "Cat rig Animator.applyRootMotion must be false.";
                return false;
            }
            if (animators[0].runtimeAnimatorController == null)
            {
                rejectionReason = "Cat rig Animator is missing its controller.";
                return false;
            }

            var clips = animators[0].runtimeAnimatorController.animationClips;
            foreach (string required in RequiredClipNames)
            {
                AnimationClip clip = Array.Find(clips, candidate => candidate != null && candidate.name == required);
                if (clip == null)
                {
                    rejectionReason = "Cat rig controller is missing clip " + required + ".";
                    return false;
                }
            }
            foreach (AnimationClip clip in clips)
            {
                if (clip != null && clip.hasRootCurves)
                {
                    rejectionReason = "Cat rig clip " + clip.name + " must be in-place.";
                    return false;
                }
            }
            if (!HasRequiredStates(animators[0], out string missingState))
            {
                rejectionReason = "Cat rig controller is missing state " + missingState + ".";
                return false;
            }

            if (prefab.transform.localRotation != Quaternion.identity
                || prefab.transform.localScale != Vector3.one)
            {
                rejectionReason = "Cat rig root must keep +Z forward and +Y up.";
                return false;
            }

            if (!TryGetLocalBounds(prefab.transform, out Bounds bounds))
            {
                rejectionReason = "Cat rig has no renderable mesh bounds.";
                return false;
            }

            if (!Mathf.Approximately(bounds.size.y, NormalizedStandingHeight)
                || !Mathf.Approximately(bounds.min.y, 0f)
                || !Mathf.Approximately(bounds.center.x, 0f)
                || !Mathf.Approximately(bounds.center.z, 0f))
            {
                rejectionReason = "Cat rig must have a ground-centred, one-unit standing pivot.";
                return false;
            }

            rejectionReason = string.Empty;
            return true;
        }

        // Animator.HasState is only reliable on an initialized Animator. Probe the controller
        // through a disposable plain GameObject rather than instantiating the imported prefab,
        // so catalog validation cannot invoke scripts or other behaviour carried by that asset.
        private static bool HasRequiredStates(Animator source, out string missingState)
        {
            var probe = new GameObject("Cat rig state probe");
            try
            {
                var animator = probe.AddComponent<Animator>();
                animator.runtimeAnimatorController = source.runtimeAnimatorController;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0f);

                string layerName = animator.GetLayerName(0);
                foreach (string required in RequiredClipNames)
                {
                    int stateHash = Animator.StringToHash(layerName + "." + required);
                    if (!animator.HasState(0, stateHash))
                    {
                        missingState = required;
                        return false;
                    }
                }
            }
            finally
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(probe);
                else UnityEngine.Object.DestroyImmediate(probe);
            }

            missingState = string.Empty;
            return true;
        }

        private static bool TryGetLocalBounds(Transform root, out Bounds localBounds)
        {
            var meshes = new List<Tuple<Transform, Mesh>>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) meshes.Add(Tuple.Create(filter.transform, filter.sharedMesh));
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skinned.sharedMesh != null) meshes.Add(Tuple.Create(skinned.transform, skinned.sharedMesh));

            if (meshes.Count == 0)
            {
                localBounds = default;
                return false;
            }

            bool initialized = false;
            localBounds = default;
            foreach (var entry in meshes)
            {
                Bounds meshBounds = entry.Item2.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
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
            return true;
        }
    }
}
