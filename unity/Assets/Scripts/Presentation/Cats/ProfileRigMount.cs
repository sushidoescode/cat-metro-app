using System;
using System.Collections.Generic;
using System.Globalization;
using CatMetro.Presentation.Cosmetics;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CatMetro.Presentation.Cats
{
    /// <summary>
    /// Presentation-only mount for the admitted rig in a profile holder. Every
    /// screen correction lives on wrappers; the instantiated prefab root remains untouched.
    /// </summary>
    public class ProfileRigMount : MonoBehaviour
    {
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
        private float _surfaceFacingYaw;
        private string _logPrefix;
        private string _lastDiagnostic;
        private string _initializationFailure;
        private readonly List<HeadSample> _headSamples = new List<HeadSample>();
        private string _headSampleFailure = "no head-weighted vertices";
        private bool _renderCallbackBound;
        private bool _layingOut;
        private bool _hasLayoutGeometry;
        private LayoutGeometry _layoutGeometry;
        private Rect _measuredHolder;
        private Rect _measuredHead;
        private float _measuredShortSide;
        private float _measuredScale;

        public float TurntableAmplitude { get; set; }
        private float _turntableTime;

        private void Update() => AdvanceTurntable(Time.unscaledDeltaTime);

        protected virtual void OnEnable()
        {
            _hasLayoutGeometry = false;
            if (_layoutCamera != null) SubscribeRenderCallback();
        }

        private void SubscribeRenderCallback()
        {
            if (_renderCallbackBound) return;
            // Subscribe after UGUI's layout pass, including when this is the first UI object.
            _ = CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases += RefreshLayoutBeforeRender;
            _renderCallbackBound = true;
        }

        protected virtual void OnDisable() => UnsubscribeRenderCallback();

        private void UnsubscribeRenderCallback()
        {
            if (!_renderCallbackBound) return;
            Canvas.willRenderCanvases -= RefreshLayoutBeforeRender;
            _renderCallbackBound = false;
        }

        private void RefreshLayoutBeforeRender()
        {
            // Explicit editor layouts can outlive Unity's edit-time lifecycle callbacks.
            if (this == null)
            {
                UnsubscribeRenderCallback();
                return;
            }
            if (_layingOut || !isActiveAndEnabled || _holder == null
                || _layoutCamera == null || PrefabRoot == null) return;
            var geometry = new LayoutGeometry(_holder, _layoutCamera);
            if (!_hasLayoutGeometry || !geometry.Equals(_layoutGeometry))
                RebuildLayout(settleCanvas: false);
        }

        public void AdvanceTurntable(float deltaSeconds)
        {
            if (!isActiveAndEnabled || !Mounted || TurntableAmplitude <= 0f
                || _layoutCamera == null || !float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
                return;
            _turntableTime = (_turntableTime + deltaSeconds) % 12f;
            ApplyPose(Mathf.Min(_holder.rect.width, _holder.rect.height), reportGeometry: false);
        }

        public int FallbackBranch { get; private set; }
        public string FallbackReason { get; private set; } = string.Empty;

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

        public static ProfileRigMount Create(RectTransform holder,
            CosmeticPortraitView portrait, CatModelCatalog catalog, float facingYaw = 0f,
            string logPrefix = "PROFILE_RIG")
            => CreateMount<ProfileRigMount>(holder, portrait, catalog, facingYaw,
                logPrefix, "ProfileRigMount");

        protected static T CreateMount<T>(RectTransform holder,
            CosmeticPortraitView portrait, CatModelCatalog catalog, float facingYaw,
            string logPrefix, string objectName) where T : ProfileRigMount
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(holder, false);
            if (portrait != null)
                go.transform.SetSiblingIndex(portrait.transform.GetSiblingIndex());
            var mountRect = (RectTransform)go.transform;
            Stretch(mountRect);
            T component = go.AddComponent<T>();
            ProfileRigMount view = component;
            view._surfaceFacingYaw = facingYaw;
            view._logPrefix = logPrefix;
            view._holder = holder;
            view._portrait = portrait;
            view.CatalogAdmittedEntryCount = catalog != null
                ? catalog.AdmittedEntryCount
                : 0;

            if (holder == null || portrait == null || catalog == null
                || view.CatalogAdmittedEntryCount != 1)
            {
                view._initializationFailure = catalog?.RejectionReason ?? "missing catalog";
                view.UsePortraitFallback(5, view._initializationFailure);
                return component;
            }

            var fitGo = new GameObject("ProfileRigFit", typeof(RectTransform));
            fitGo.transform.SetParent(mountRect, false);
            view._fit = (RectTransform)fitGo.transform;
            view._fit.anchorMin = view._fit.anchorMax = new Vector2(0.5f, 0.5f);
            view._fit.pivot = new Vector2(0.5f, 0.5f);
            view._fit.sizeDelta = Vector2.zero;
            view._fit.anchoredPosition3D = Vector3.zero;

            var facingGo = new GameObject("ProfileRigFacing");
            facingGo.transform.SetParent(view._fit, false);
            view._facing = facingGo.transform;

            if (!catalog.TryInstantiate(view._facing, out GameObject instance,
                    out view._entry))
            {
                view._initializationFailure = "catalog could not instantiate admitted rig";
                view.UsePortraitFallback(6, view._initializationFailure);
                return component;
            }

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
                view._initializationFailure = "no skinned mesh with a mesh";
                view.UsePortraitFallback(7, view._initializationFailure);
                return component;
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
            view.CacheHeadSamples();
            portrait.PortraitApplied += view.OnPortraitApplied;
            view._portraitSubscribed = true;
            return component;
        }

        public bool Layout(Camera canvasCamera)
        {
            if (_layingOut) return Mounted;
            // Retain the request even while the holder is empty so a later canvas pass can
            // recover without another Home/ Wardrobe state change.
            _layoutCamera = canvasCamera;
            if (isActiveAndEnabled && _layoutCamera != null) SubscribeRenderCallback();
            return RebuildLayout(settleCanvas: true);
        }

        private bool RebuildLayout(bool settleCanvas)
        {
            _layingOut = true;
            try
            {
                if (settleCanvas && _holder != null && _layoutCamera != null)
                    Canvas.ForceUpdateCanvases();
                return LayoutSettledGeometry();
            }
            finally { _layingOut = false; }
        }

        private bool LayoutSettledGeometry()
        {
            _measuredHolder = _holder != null ? _holder.rect : default;
            _measuredShortSide = Mathf.Min(_measuredHolder.width, _measuredHolder.height);
            _measuredScale = _measuredShortSide * HolderFill
                / CatModelCatalog.NormalizedStandingHeight;
            _measuredHead = default;
            _hasLayoutGeometry = _holder != null && _layoutCamera != null;
            if (_hasLayoutGeometry) _layoutGeometry = new LayoutGeometry(_holder, _layoutCamera);
            if (PrefabRoot == null || _holder == null || _portrait == null
                || _fit == null || _facing == null || _layoutCamera == null
                || !UsableDimension(_measuredHolder.width) || !UsableDimension(_measuredHolder.height)
                || _headRoot == null)
            {
                UsePortraitFallback(1, LayoutFailureReason(_layoutCamera));
                return false;
            }
            if (!MatchesSelectedCat())
            {
                UsePortraitFallback(2, "selected cat has no matching rig: " + _portrait.AppliedCatId);
                return false;
            }

            float shortSide = _measuredShortSide;
            float scale = _measuredScale;
            if (!UsableDimension(scale))
            {
                UsePortraitFallback(8, "invalid computed fit scale");
                return false;
            }
            _fit.localScale = new Vector3(scale, scale, scale * CanvasDepthScale);
            _fit.anchoredPosition3D = new Vector3(0f, -0.5f * scale,
                -shortSide * CanvasLift);
            return ApplyPose(shortSide, reportGeometry: true);
        }

        private bool ApplyPose(float shortSide, bool reportGeometry)
        {
            AppliedFacingYaw = _entry.FacingYaw + _surfaceFacingYaw
                + TurntableAmplitude * Mathf.Sin(_turntableTime * Mathf.PI / 6f);
            _facing.localRotation = Quaternion.Euler(0f, AppliedFacingYaw, 0f);

            bool hasHead = TryGetRenderedHeadScreenRect(_layoutCamera, out Rect renderedHead);
            _measuredHead = renderedHead;
            if (!hasHead || !TryAlignPortrait(renderedHead, _layoutCamera, shortSide))
            {
                UsePortraitFallback(3, _headSamples.Count == 0 ? _headSampleFailure
                    : "head bounds or portrait alignment unavailable");
                return false;
            }
            if (renderedHead.width < 1f || renderedHead.height < 1f)
            {
                UsePortraitFallback(8, "head below one screen pixel");
                return false;
            }

            bool becameMounted = !Mounted;
            RenderedHeadScreenRect = renderedHead;
            foreach (SkinnedMeshRenderer skin in _skins)
                if (skin != null && skin.sharedMesh != null) skin.enabled = true;
            _portrait.SetBaseLayerSuppressed(true);
            Mounted = true;
            FallbackBranch = 0;
            FallbackReason = string.Empty;
            if ((becameMounted || reportGeometry) && !string.IsNullOrEmpty(_logPrefix))
                Report(_logPrefix + " mounted=true admitted=" + CatalogAdmittedEntryCount
                    + GeometryDiagnostic(), false);
            return true;
        }

        private static bool UsableDimension(float value) => float.IsFinite(value) && value > 0f;

        private string GeometryDiagnostic() => string.Format(CultureInfo.InvariantCulture,
            " scale={0:F3} shortSide={1:F3} holder={2:F3}x{3:F3} headPx={4:F3}x{5:F3}",
            _measuredScale, _measuredShortSide, _measuredHolder.width, _measuredHolder.height,
            _measuredHead.width, _measuredHead.height);

        private readonly struct LayoutGeometry : IEquatable<LayoutGeometry>
        {
            private readonly Rect _holderRect;
            private readonly Matrix4x4 _holderTransform;
            private readonly Rect _cameraPixels;
            private readonly Matrix4x4 _cameraView;
            private readonly Matrix4x4 _cameraProjection;
            private readonly RenderTexture _target;

            public LayoutGeometry(RectTransform holder, Camera camera)
            {
                _holderRect = holder.rect;
                _holderTransform = holder.localToWorldMatrix;
                _cameraPixels = camera.pixelRect;
                _cameraView = camera.worldToCameraMatrix;
                _cameraProjection = camera.projectionMatrix;
                _target = camera.targetTexture;
            }

            public bool Equals(LayoutGeometry other) => _holderRect.Equals(other._holderRect)
                && _holderTransform.Equals(other._holderTransform)
                && _cameraPixels.Equals(other._cameraPixels)
                && _cameraView.Equals(other._cameraView)
                && _cameraProjection.Equals(other._cameraProjection)
                && _target == other._target;
        }

        private void CacheHeadSamples()
        {
            // Bone membership never changes after sampling the pose. Retain the head indices
            // and reuse each bake buffer; Canvas scaling still needs Unity's live skin projection.
            foreach (SkinnedMeshRenderer skin in _skins)
            {
                if (skin == null || skin.sharedMesh == null || _headRoot == null) continue;
                Mesh source = skin.sharedMesh;
                _headSampleFailure = "no head sample mesh=" + source.name
                    + " readable=" + source.isReadable.ToString().ToLowerInvariant();
                try
                {
                    BoneWeight[] weights = source.boneWeights;
                    Transform[] bones = skin.bones;
                    if (weights.Length != source.vertexCount || bones.Length == 0) continue;
                    var headBones = new bool[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                    {
                        Transform bone = bones[i];
                        headBones[i] = bone != null
                            && (bone == _headRoot || bone.IsChildOf(_headRoot));
                    }
                    var indices = new List<int>();
                    for (int i = 0; i < weights.Length; i++)
                        if (HeadWeight(weights[i], headBones) >= HeadWeightThreshold)
                            indices.Add(i);
                    if (indices.Count > 0)
                        _headSamples.Add(new HeadSample(skin, indices.ToArray()));
                }
                catch (UnityException exception)
                {
                    _headSampleFailure += " error=" + exception.Message;
                }
            }
        }

        private bool TryGetRenderedHeadScreenRect(Camera camera, out Rect result)
        {
            bool initialized = false;
            float xMin = 0f, xMax = 0f, yMin = 0f, yMax = 0f;
            foreach (HeadSample sample in _headSamples)
            {
                if (sample.Skin == null) continue;
                sample.Skin.BakeMesh(sample.Buffer, false);
                sample.Buffer.GetVertices(sample.Vertices);
                foreach (int index in sample.Indices)
                {
                    if (index >= sample.Vertices.Count) continue;
                    Vector3 screen = camera.WorldToScreenPoint(
                        sample.Skin.transform.TransformPoint(sample.Vertices[index]));
                    if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y)
                        || !UsableDimension(screen.z)) continue;
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
            result = initialized ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : default;
            return initialized && result.width > 0f && result.height > 0f;
        }

        private sealed class HeadSample
        {
            public readonly SkinnedMeshRenderer Skin;
            public readonly int[] Indices;
            public readonly Mesh Buffer = new Mesh { name = "ProfileHeadSample" };
            public readonly List<Vector3> Vertices;

            public HeadSample(SkinnedMeshRenderer skin, int[] indices)
            {
                Skin = skin;
                Indices = indices;
                Vertices = new List<Vector3>(skin.sharedMesh.vertexCount);
            }
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

        private string LayoutFailureReason(Camera camera)
        {
            if (!string.IsNullOrEmpty(_initializationFailure)) return _initializationFailure;
            if (camera == null) return "missing canvas camera";
            if (_holder == null) return "missing holder";
            if (_portrait == null) return "missing portrait";
            if (PrefabRoot == null) return "missing prefab clone";
            if (_fit == null || _facing == null) return "missing fit or facing wrapper";
            if (!float.IsFinite(_holder.rect.width) || !float.IsFinite(_holder.rect.height))
                return "non-finite holder rect";
            if (_holder.rect.width <= 0f || _holder.rect.height <= 0f) return "empty holder rect";
            if (_headRoot == null) return "missing head bone at " + CatModelCatalog.HeadDeformerRootPath;
            return "layout input unavailable";
        }

        private void Report(string message, bool warning)
        {
            if (string.IsNullOrEmpty(_logPrefix)) return;
            // Layout is called repeatedly; log state transitions, not one warning per frame.
            if (_lastDiagnostic == message) return;
            _lastDiagnostic = message;
            if (warning) Debug.LogWarning(message, this);
            else Debug.Log(message, this);
        }

        private void UsePortraitFallback(int branch, string reason)
        {
            FallbackBranch = branch;
            FallbackReason = reason;
            Report(_logPrefix + " fallback branch=" + branch + " admitted="
                + CatalogAdmittedEntryCount + " reason=" + reason
                + (_hasLayoutGeometry ? GeometryDiagnostic() : string.Empty), true);
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
                UsePortraitFallback(4, "selected cat has no matching rig: " + _portrait.AppliedCatId);
                return;
            }
            if (_layoutCamera != null) Layout(_layoutCamera);
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeRenderCallback();
            if (_portraitSubscribed && _portrait != null)
                _portrait.PortraitApplied -= OnPortraitApplied;
            _portraitSubscribed = false;
            foreach (HeadSample sample in _headSamples) DestroyMountedInstance(sample.Buffer);
            _headSamples.Clear();
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
