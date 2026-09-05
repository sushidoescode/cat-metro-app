using System;
using CatMetro.Presentation.Cosmetics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Presentation.Cats
{
    /// <summary>
    /// Presentation-only adapter for the admitted board rig in Home's profile holder. Every
    /// screen correction lives on wrappers; the instantiated prefab root remains untouched.
    /// </summary>
    public sealed class HomeProfileRigView : MonoBehaviour
    {
        public const float HomeFacingYaw = -20f;
        private const float HolderFill = 0.92f;
        private const float CanvasDepthScale = 0.06f;
        private const float CanvasLift = 0.12f;
        private const float CosmeticLift = 0.20f;
        private const float HeadWeightThreshold = 0.05f;
        private static readonly Rect PortraitHeadGuide =
            Rect.MinMaxRect(0.18f, 0.28f, 0.82f, 0.94f);

        private RectTransform _holder;
        private CosmeticPortraitView _portrait;
        private RectTransform _fit;
        private Transform _facing;
        private Transform _headRoot;
        private SkinnedMeshRenderer[] _skins;
        private CatModelCatalog.Entry _entry;
        private Camera _layoutCamera;
        private bool _portraitSubscribed;
        private bool _changingRepresentation;

        public int CatalogAdmittedEntryCount { get; private set; }
        public int SkinnedMeshRendererCount { get; private set; }
        public Transform PrefabRoot { get; private set; }
        public int AnimatorCount => PrefabRoot != null
            ? PrefabRoot.GetComponentsInChildren<Animator>(true).Length
            : 0;
        public string SampledPose { get; private set; } = string.Empty;
        public float AppliedFacingYaw { get; private set; }
        public Rect RenderedHeadScreenRect { get; private set; }
        public bool Mounted { get; private set; }

        public static HomeProfileRigView Create(RectTransform holder,
            CosmeticPortraitView portrait, CatModelCatalog catalog)
        {
            var go = new GameObject("HomeProfileRigMount", typeof(RectTransform));
            go.transform.SetParent(holder, false);
            if (portrait != null)
                go.transform.SetSiblingIndex(portrait.transform.GetSiblingIndex());
            var mountRect = (RectTransform)go.transform;
            Stretch(mountRect);
            var view = go.AddComponent<HomeProfileRigView>();
            view._holder = holder;
            view._portrait = portrait;
            view.CatalogAdmittedEntryCount = catalog != null
                ? catalog.AdmittedEntryCount
                : 0;

            if (holder == null || portrait == null || catalog == null
                || view.CatalogAdmittedEntryCount != 1)
                return view;

            var fitGo = new GameObject("HomeRigFit", typeof(RectTransform));
            fitGo.transform.SetParent(mountRect, false);
            view._fit = (RectTransform)fitGo.transform;
            view._fit.anchorMin = view._fit.anchorMax = new Vector2(0.5f, 0.5f);
            view._fit.pivot = new Vector2(0.5f, 0.5f);
            view._fit.sizeDelta = Vector2.zero;
            view._fit.anchoredPosition3D = Vector3.zero;

            var facingGo = new GameObject("HomeRigFacing");
            facingGo.transform.SetParent(view._fit, false);
            view._facing = facingGo.transform;

            if (!catalog.TryInstantiate(view._facing, out GameObject instance,
                    out view._entry))
                return view;

            view.PrefabRoot = instance.transform;
            view._skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int validSkins = 0;
            foreach (SkinnedMeshRenderer skin in view._skins)
            {
                if (skin == null || skin.sharedMesh == null) continue;
                validSkins++;
                skin.enabled = false;
            }
            view.SkinnedMeshRendererCount = validSkins;
            if (validSkins == 0)
            {
                DestroyMountedInstance(instance);
                view.PrefabRoot = null;
                return view;
            }

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            // Imported bone paths are controller-relative. The admitted prefab deliberately
            // keeps its identity root and ModelCorrection wrapper above that Animator.
            view._headRoot = animator.transform.Find(CatModelCatalog.HeadDeformerRootPath);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Play(animator.GetLayerName(0) + "." + CatModelCatalog.IdleSitClip,
                0, 0f);
            animator.Update(0f);
            animator.speed = 0f;
            view.SampledPose = CatModelCatalog.IdleSitClip;
            DestroyImmediate(animator);
            portrait.PortraitApplied += view.OnPortraitApplied;
            view._portraitSubscribed = true;
            return view;
        }

        public bool Layout(Camera canvasCamera)
        {
            if (PrefabRoot == null || _holder == null || _portrait == null
                || _fit == null || _facing == null || canvasCamera == null
                || _holder.rect.width <= 0f || _holder.rect.height <= 0f
                || _headRoot == null)
            {
                UsePortraitFallback();
                return false;
            }
            _layoutCamera = canvasCamera;
            if (!MatchesSelectedCat())
            {
                UsePortraitFallback();
                return false;
            }

            float shortSide = Mathf.Min(_holder.rect.width, _holder.rect.height);
            float scale = shortSide * HolderFill / CatModelCatalog.NormalizedStandingHeight;
            _fit.localScale = new Vector3(scale, scale, scale * CanvasDepthScale);
            _fit.anchoredPosition3D = new Vector3(0f, -0.5f * scale,
                -shortSide * CanvasLift);
            AppliedFacingYaw = _entry.FacingYaw + HomeFacingYaw;
            _facing.localRotation = Quaternion.Euler(0f, AppliedFacingYaw, 0f);

            Canvas.ForceUpdateCanvases();
            if (!TryGetRenderedHeadScreenRect(canvasCamera, out Rect renderedHead)
                || !TryAlignPortrait(renderedHead, canvasCamera, shortSide))
            {
                UsePortraitFallback();
                return false;
            }

            RenderedHeadScreenRect = renderedHead;
            foreach (SkinnedMeshRenderer skin in _skins)
                if (skin != null && skin.sharedMesh != null) skin.enabled = true;
            _portrait.SetBaseLayerSuppressed(true);
            Mounted = true;
            return true;
        }

        private bool TryGetRenderedHeadScreenRect(Camera camera, out Rect result)
        {
            bool initialized = false;
            float xMin = 0f, xMax = 0f, yMin = 0f, yMax = 0f;
            foreach (SkinnedMeshRenderer skin in _skins)
            {
                if (skin == null || skin.sharedMesh == null) continue;
                Mesh source = skin.sharedMesh;
                BoneWeight[] weights = source.boneWeights;
                Transform[] bones = skin.bones;
                if (weights == null || weights.Length != source.vertexCount
                    || bones == null || bones.Length == 0)
                    continue;

                var headBones = new bool[bones.Length];
                bool hasHeadBone = false;
                for (int i = 0; i < bones.Length; i++)
                {
                    Transform bone = bones[i];
                    bool belongsToHead = bone != null
                        && (bone == _headRoot || bone.IsChildOf(_headRoot));
                    headBones[i] = belongsToHead;
                    hasHeadBone |= belongsToHead;
                }
                if (!hasHeadBone) continue;

                var baked = new Mesh { name = "HomeProfileHeadSample" };
                try
                {
                    skin.BakeMesh(baked, false);
                    Vector3[] vertices = baked.vertices;
                    int count = Mathf.Min(vertices.Length, weights.Length);
                    for (int i = 0; i < count; i++)
                    {
                        if (HeadWeight(weights[i], headBones) < HeadWeightThreshold)
                            continue;
                        Vector3 screen = camera.WorldToScreenPoint(
                            skin.transform.TransformPoint(vertices[i]));
                        if (screen.z <= 0f) continue;
                        if (!initialized)
                        {
                            xMin = xMax = screen.x;
                            yMin = yMax = screen.y;
                            initialized = true;
                        }
                        else
                        {
                            xMin = Mathf.Min(xMin, screen.x);
                            xMax = Mathf.Max(xMax, screen.x);
                            yMin = Mathf.Min(yMin, screen.y);
                            yMax = Mathf.Max(yMax, screen.y);
                        }
                    }
                }
                finally
                {
                    DestroyImmediate(baked);
                }
            }

            result = initialized
                ? Rect.MinMaxRect(xMin, yMin, xMax, yMax)
                : default;
            return initialized && result.width > 0f && result.height > 0f;
        }

        private bool TryAlignPortrait(Rect headScreenRect, Camera camera, float shortSide)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_holder,
                    headScreenRect.min, camera, out Vector2 localMin)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_holder,
                    headScreenRect.max, camera, out Vector2 localMax))
                return false;

            float width = (localMax.x - localMin.x) / PortraitHeadGuide.width;
            float height = (localMax.y - localMin.y) / PortraitHeadGuide.height;
            if (!float.IsFinite(width) || !float.IsFinite(height)
                || width <= 0f || height <= 0f)
                return false;

            float xMin = localMin.x - width * PortraitHeadGuide.xMin;
            float yMin = localMin.y - height * PortraitHeadGuide.yMin;
            RectTransform root = _portrait.RootTransform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(width, height);
            root.anchoredPosition3D = new Vector3(xMin + width * 0.5f,
                yMin + height * 0.5f, -shortSide * CosmeticLift);
            return true;
        }

        private void UsePortraitFallback()
        {
            bool ownsChangeGuard = !_changingRepresentation;
            if (ownsChangeGuard) _changingRepresentation = true;
            Mounted = false;
            RenderedHeadScreenRect = default;
            if (_skins != null)
                foreach (SkinnedMeshRenderer skin in _skins)
                    if (skin != null) skin.enabled = false;
            if (_portrait != null)
            {
                _portrait.SetBaseLayerSuppressed(false);
                Stretch(_portrait.RootTransform);
                _portrait.RootTransform.anchoredPosition3D = Vector3.zero;
            }
            if (ownsChangeGuard) _changingRepresentation = false;
        }

        private bool MatchesSelectedCat() => _entry != null
            && (string.IsNullOrEmpty(_entry.CosmeticCatId)
                || string.Equals(_entry.CosmeticCatId, _portrait.AppliedCatId,
                    StringComparison.Ordinal));

        private void OnPortraitApplied()
        {
            if (_changingRepresentation) return;
            if (!MatchesSelectedCat())
            {
                UsePortraitFallback();
                return;
            }
            if (_layoutCamera != null) Layout(_layoutCamera);
        }

        private void OnDestroy()
        {
            if (_portraitSubscribed && _portrait != null)
                _portrait.PortraitApplied -= OnPortraitApplied;
            _portraitSubscribed = false;
        }

        private static float HeadWeight(BoneWeight weight, bool[] headBones)
        {
            float total = 0f;
            if (IsHeadBone(weight.boneIndex0, headBones)) total += weight.weight0;
            if (IsHeadBone(weight.boneIndex1, headBones)) total += weight.weight1;
            if (IsHeadBone(weight.boneIndex2, headBones)) total += weight.weight2;
            if (IsHeadBone(weight.boneIndex3, headBones)) total += weight.weight3;
            return total;
        }

        private static bool IsHeadBone(int index, bool[] headBones) =>
            index >= 0 && index < headBones.Length && headBones[index];

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void DestroyMountedInstance(Object target)
        {
            if (target == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
