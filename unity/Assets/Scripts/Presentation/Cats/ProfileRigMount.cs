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
        private const float StrongHeadWeightThreshold = 0.25f;
        private const float BodyWeightThreshold = 0.5f;
        private const float NecklineGapInHeadHeights = 0.025f;
        private static readonly Rect PortraitHeadGuide =
            Rect.MinMaxRect(0.18f, 0.28f, 0.82f, 0.94f);

        private RectTransform _holder;
        private CosmeticPortraitView _portrait;
        private RectTransform _fit;
        private Transform _facing;
        private Transform _headRoot;
        private SkinnedMeshRenderer[] _skins;
        private Renderer[] _renderers;
        private bool[] _rendererDefaults;
        private bool _visible = true;
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
        private CatRigPresentation _rigPresentation;
        private Func<bool> _motionOffSource;
        private bool _motionSuppressed;
        private float _idleTime;
        private Vector2 _cosmeticHeadCenter;
        private Vector3 _cosmeticBasePosition;
        private Mesh _fitBuffer;
        private readonly List<Vector3> _fitVertices = new List<Vector3>();
        private readonly List<Vector3> _fitPoints = new List<Vector3>();
        private Transform _bodyRoot;
        private Rect _strongHeadScreenRect;
        private Rect _bodyWearScreenRect;
        private Vector2 _bodyAnchorScreen;
        private bool _bodyWearFitted;

        public float TurntableAmplitude { get; set; }
        private float _turntableTime;

        private void Update() => AdvanceTurntable(Time.unscaledDeltaTime);

        protected virtual void OnEnable()
        {
            _hasLayoutGeometry = false;
            if (_layoutCamera != null) SubscribeRenderCallback();
            if (_rigPresentation != null && _rigPresentation.AuthoredMotionInstalled)
            {
                _idleTime = _turntableTime = 0f;
                _rigPresentation.SampleIdle(0f);
                _motionSuppressed = false;
            }
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
            if (_layingOut || !_visible || !isActiveAndEnabled || _holder == null
                || _layoutCamera == null || PrefabRoot == null) return;
            var geometry = new LayoutGeometry(_holder, _layoutCamera);
            if (!_hasLayoutGeometry || !geometry.Equals(_layoutGeometry))
                RebuildLayout(settleCanvas: false);
        }

        public void AdvanceTurntable(float deltaSeconds)
        {
            if (!isActiveAndEnabled || !_visible || !Mounted || _layoutCamera == null)
                return;
            bool authored = _rigPresentation != null && _rigPresentation.AuthoredMotionInstalled;
            if (_motionOffSource?.Invoke() == true)
            {
                if (_motionSuppressed) return;
                _motionSuppressed = true;
                _idleTime = _turntableTime = 0f;
                if (authored) _rigPresentation.SampleIdle(0f);
                if (authored) ApplyAnimatedPose();
                else ApplyPose(Mathf.Min(_holder.rect.width, _holder.rect.height), reportGeometry: false);
                return;
            }
            _motionSuppressed = false;
            if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f) return;
            if (authored)
            {
                _idleTime = Mathf.Repeat(_idleTime + deltaSeconds, _rigPresentation.IdleClip.length);
                _rigPresentation.SampleIdle(_idleTime);
            }
            if (TurntableAmplitude > 0f) _turntableTime = (_turntableTime + deltaSeconds) % 12f;
            if (authored) ApplyAnimatedPose();
            else if (TurntableAmplitude > 0f)
                ApplyPose(Mathf.Min(_holder.rect.width, _holder.rect.height), reportGeometry: false);
        }

        public void BindMotionOff(Func<bool> source)
        {
            _motionOffSource = source;
            _motionSuppressed = false;
            AdvanceTurntable(0f);
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

        // Occluding UI must hide the camera-lifted mesh without discarding its fitted pose.
        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyRendererVisibility();
            if (visible) AdvanceTurntable(0f);
        }

        private void ApplyRendererVisibility()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].enabled = Mounted && _visible && _rendererDefaults[i];
        }

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
            view._renderers = instance.GetComponentsInChildren<Renderer>(true);
            view._rendererDefaults = new bool[view._renderers.Length];
            for (int i = 0; i < view._renderers.Length; i++)
                view._rendererDefaults[i] = view._renderers[i].enabled;
            view.ApplyRendererVisibility();
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
            view._rigPresentation = animator.GetComponent<CatRigPresentation>();
            view._headRoot = view._rigPresentation != null
                ? view._rigPresentation.HeadTransform
                : animator.transform.Find(CatModelCatalog.HeadDeformerRootPath);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Play(animator.GetLayerName(0) + "." + CatModelCatalog.IdleSitClip,
                0, 0f);
            animator.Update(0f);
            view._rigPresentation?.ApplyHeadShape();
            animator.speed = 0f;
            view.SampledPose = CatModelCatalog.IdleSitClip;
            DestroyImmediate(animator);
            view._rigPresentation?.SampleIdle(0f);
            // Paid anatomy was independently localized by deformation/weights. Generic fixtures
            // retain the flat portrait layout; the weak Head_2 joint is never a chest guide.
            view._bodyRoot = view._rigPresentation != null ? view._headRoot.parent : null;
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
            bool authored = _rigPresentation != null && _rigPresentation.AuthoredMotionInstalled;
            if (authored) _rigPresentation.SampleIdle(0f);
            _facing.localRotation = Quaternion.Euler(0f, _entry.FacingYaw + _surfaceFacingYaw, 0f);
            bool fitted;
            try { fitted = TryFitFullSkin(); }
            finally { if (authored) _rigPresentation.SampleIdle(_idleTime); }
            if (!fitted)
            {
                UsePortraitFallback(8, "full skin bounds or holder fit unavailable");
                return false;
            }
            _measuredScale = _fit.localScale.x;
            return ApplyPose(shortSide, reportGeometry: true);
        }

        private bool TryFitFullSkin()
        {
            // The catalog's standing height only seeds a useful projection scale. The
            // sampled sitting pose and enlarged head determine the actual holder fit.
            // Fit only on layout changes: idle/turntable updates keep this wrapper scale.
            if (!TryCacheFullSkinInFit()) return false;
            Rect holder = _holder.rect;
            Vector2 available = holder.size * HolderFill;
            for (int pass = 0; pass < 4; pass++)
            {
                if (!TryMeasureFullSkinInHolder(out Rect skin)) return false;
                float ratio = Mathf.Min(available.x / skin.width, available.y / skin.height);
                if (!UsableDimension(ratio)) return false;
                // First pass may enlarge a small pose. Subsequent passes only correct
                // perspective/depth drift; an already contained pose must not pump up.
                if (pass > 0) ratio = Mathf.Min(1f, ratio);
                _fit.localScale *= ratio;
                if (!TryMeasureFullSkinInHolder(out skin)) return false;
                Vector2 offset = holder.center - skin.center;
                _fit.anchoredPosition3D += new Vector3(offset.x, offset.y, 0f);
                if (Mathf.Abs(ratio - 1f) < .00001f && offset.sqrMagnitude < .0001f) break;
            }
            if (!TryMeasureFullSkinInHolder(out Rect fitted)) return false;
            // Keep the four-percent breathing/turntable margin. A failed projection
            // retains the complete 2D fallback rather than painting outside the holder.
            Vector2 inset = (holder.size - available) * .5f;
            const float tolerance = .5f;
            return fitted.xMin >= holder.xMin + inset.x - tolerance
                && fitted.xMax <= holder.xMax - inset.x + tolerance
                && fitted.yMin >= holder.yMin + inset.y - tolerance
                && fitted.yMax <= holder.yMax - inset.y + tolerance;
        }

        private bool TryCacheFullSkinInFit()
        {
            if (_fitBuffer == null) _fitBuffer = new Mesh { name = "ProfileFullSkinFit" };
            _fitPoints.Clear();
            for (int i = 0; i < _renderers.Length; i++)
            {
                var skin = _renderers[i] as SkinnedMeshRenderer;
                if (skin == null || skin.sharedMesh == null || !_rendererDefaults[i]) continue;
                bool active = true;
                for (Transform node = skin.transform; node != PrefabRoot; node = node.parent)
                    if (!node.gameObject.activeSelf) { active = false; break; }
                if (!active) continue;
                // All vertices, independent of the head-weight mask used by cosmetics.
                // Bake once per layout; wrapper-space points survive each fit correction.
                skin.BakeMesh(_fitBuffer, true);
                _fitBuffer.GetVertices(_fitVertices);
                Matrix4x4 skinToFit = _fit.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                foreach (Vector3 vertex in _fitVertices)
                    _fitPoints.Add(skinToFit.MultiplyPoint3x4(vertex));
            }
            return _fitPoints.Count > 0;
        }

        private bool TryMeasureFullSkinInHolder(out Rect result)
        {
            bool initialized = false;
            Vector2 min = default, max = default;
            foreach (Vector3 vertex in _fitPoints)
            {
                Vector3 screen = _layoutCamera.WorldToScreenPoint(_fit.TransformPoint(vertex));
                if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y)
                    || !UsableDimension(screen.z)
                    || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_holder,
                        screen, _layoutCamera, out Vector2 point))
                { result = default; return false; }
                if (!initialized) { min = max = point; initialized = true; }
                else { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            }
            result = initialized ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
            return initialized && UsableDimension(result.width) && UsableDimension(result.height);
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
            _portrait.SetBaseLayerSuppressed(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_holder,
                renderedHead.center, _layoutCamera, out _cosmeticHeadCenter);
            _cosmeticBasePosition = _portrait.RootTransform.anchoredPosition3D;
            FitBodyWear();
            Mounted = true;
            ApplyRendererVisibility();
            FallbackBranch = 0;
            FallbackReason = string.Empty;
            if ((becameMounted || reportGeometry) && !string.IsNullOrEmpty(_logPrefix))
                Report(_logPrefix + " mounted=true admitted=" + CatalogAdmittedEntryCount
                    + GeometryDiagnostic(), false);
            return true;
        }

        private void ApplyAnimatedPose()
        {
            AppliedFacingYaw = _entry.FacingYaw + _surfaceFacingYaw
                + TurntableAmplitude * Mathf.Sin(_turntableTime * Mathf.PI / 6f);
            _facing.localRotation = Quaternion.Euler(0f, AppliedFacingYaw, 0f);
            if (!TryGetRenderedHeadScreenRect(_layoutCamera, out Rect head)) return;
            RenderedHeadScreenRect = head;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_holder,
                head.center, _layoutCamera, out Vector2 center)) return;
            Vector2 delta = center - _cosmeticHeadCenter;
            // Keep the settled fit and cosmetic size. Breathing/tilt may move the anchor,
            // but must not repeatedly resize the portrait or drive a canvas layout pass.
            _portrait.RootTransform.anchoredPosition3D = _cosmeticBasePosition
                + new Vector3(delta.x, delta.y, 0f);
            FollowBodyWear();
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
                    int bodyBone = _bodyRoot != null ? Array.IndexOf(bones, _bodyRoot) : -1;
                    Transform tail = _bodyRoot != null ? _bodyRoot.Find("bone_9/bone_12") : null;
                    var tailBones = new bool[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                        tailBones[i] = tail != null && bones[i] != null
                            && (bones[i] == tail || bones[i].IsChildOf(tail));
                    var indices = new List<int>();
                    var bodyIndices = new List<int>();
                    var strongHead = new bool[weights.Length];
                    for (int i = 0; i < weights.Length; i++)
                    {
                        float headWeight = HeadWeight(weights[i], headBones);
                        strongHead[i] = headWeight >= StrongHeadWeightThreshold;
                        if (headWeight >= HeadWeightThreshold) indices.Add(i);
                        // Direct torso weighting, not the entire Head_0 subtree (which also owns
                        // the head and legs). The projected hip-to-neck band further excludes paws.
                        // Even weak tail weights identify tail vertices: Head_0 dominates much
                        // of the tail, so a .25 tail cutoff would incorrectly widen the coat.
                        if (bodyBone >= 0 && BoneWeightFor(weights[i], bodyBone) >= BodyWeightThreshold
                            && !strongHead[i] && HeadWeight(weights[i], tailBones) == 0f)
                            bodyIndices.Add(i);
                    }
                    if (indices.Count > 0)
                        _headSamples.Add(new HeadSample(skin, indices.ToArray(), strongHead, bodyIndices.ToArray()));
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
            Vector2 strongMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 strongMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (HeadSample sample in _headSamples)
            {
                if (sample.Skin == null) continue;
                // Include the imported bone hierarchy's scale when baking. The false
                // mode misprojects the flattened rig after a camera-space canvas refit.
                sample.Skin.BakeMesh(sample.Buffer, true);
                sample.Buffer.GetVertices(sample.Vertices);
                foreach (int index in sample.Indices)
                {
                    if (index >= sample.Vertices.Count) continue;
                    Vector3 screen = camera.WorldToScreenPoint(
                        sample.Skin.transform.TransformPoint(sample.Vertices[index]));
                    if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y)
                        || !UsableDimension(screen.z)) continue;
                    if (sample.StrongHead[index])
                    {
                        strongMin = Vector2.Min(strongMin, screen);
                        strongMax = Vector2.Max(strongMax, screen);
                    }
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
            _strongHeadScreenRect = strongMax.x > strongMin.x && strongMax.y > strongMin.y
                ? Rect.MinMaxRect(strongMin.x, strongMin.y, strongMax.x, strongMax.y) : default;
            result = initialized ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : default;
            return initialized && result.width > 0f && result.height > 0f;
        }

        private sealed class HeadSample
        {
            public readonly SkinnedMeshRenderer Skin;
            public readonly int[] Indices;
            public readonly bool[] StrongHead;
            public readonly int[] BodyIndices;
            public readonly Mesh Buffer = new Mesh { name = "ProfileHeadSample" };
            public readonly List<Vector3> Vertices;

            public HeadSample(SkinnedMeshRenderer skin, int[] indices, bool[] strongHead, int[] bodyIndices)
            {
                Skin = skin;
                Indices = indices;
                StrongHead = strongHead;
                BodyIndices = bodyIndices;
                Vertices = new List<Vector3>(skin.sharedMesh.vertexCount);
            }
        }

        private void FitBodyWear()
        {
            _bodyWearFitted = false;
            _portrait.ResetBodyWear();
            if (_bodyRoot == null || !_portrait.HasBodyWear || _strongHeadScreenRect.height <= 0f) return;
            Vector3 anchor = _layoutCamera.WorldToScreenPoint(_bodyRoot.position);
            float top = _strongHeadScreenRect.yMin - _strongHeadScreenRect.height * NecklineGapInHeadHeights;
            float bottom = anchor.y;
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            int count = 0;
            // TryGetRenderedHeadScreenRect already baked these buffers for this settled pose.
            // No extra bake, buffer creation, or full-mesh read is needed for coat placement.
            foreach (HeadSample sample in _headSamples)
            foreach (int index in sample.BodyIndices)
            {
                if (sample.Skin == null || index >= sample.Vertices.Count) continue;
                Vector3 point = _layoutCamera.WorldToScreenPoint(
                    sample.Skin.transform.TransformPoint(sample.Vertices[index]));
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || point.z <= 0f
                    || point.y < bottom || point.y > top) continue;
                left = Mathf.Min(left, point.x);
                right = Mathf.Max(right, point.x);
                count++;
            }
            if (count < 8 || !UsableDimension(right - left) || !UsableDimension(top - bottom)) return;
            _bodyWearScreenRect = Rect.MinMaxRect(left, bottom, right, top);
            RectTransform portrait = _portrait.RootTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(portrait,
                    _bodyWearScreenRect.min, _layoutCamera, out Vector2 min)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(portrait,
                    _bodyWearScreenRect.max, _layoutCamera, out Vector2 max)) return;
            _bodyWearFitted = _portrait.FitBodyWear(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            _bodyAnchorScreen = anchor;
        }

        private void FollowBodyWear()
        {
            if (!_bodyWearFitted || _bodyRoot == null || !_portrait.HasBodyWear) return;
            Vector2 anchor = _layoutCamera.WorldToScreenPoint(_bodyRoot.position);
            Vector2 center = _bodyWearScreenRect.center + anchor - _bodyAnchorScreen;
            // Keep the settled dimensions. A downward head tilt can lower the neckline, but
            // cannot pull a collar into the face; all rotated corners were included in the fit.
            center.y = Mathf.Min(center.y, _strongHeadScreenRect.yMin
                - _strongHeadScreenRect.height * NecklineGapInHeadHeights - _bodyWearScreenRect.height * .5f);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_portrait.RootTransform,
                center, _layoutCamera, out Vector2 local)) _portrait.MoveBodyWearCenter(local);
        }

        private static float BoneWeightFor(BoneWeight weight, int bone) =>
            (weight.boneIndex0 == bone ? weight.weight0 : 0f)
            + (weight.boneIndex1 == bone ? weight.weight1 : 0f)
            + (weight.boneIndex2 == bone ? weight.weight2 : 0f)
            + (weight.boneIndex3 == bone ? weight.weight3 : 0f);

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
            // Large Wardrobe holders can lift the cosmetic plane through the camera's
            // near plane even while the shallower rig remains visible. Retain the requested
            // lift whenever it is safe; the orthographic projection keeps this depth-only
            // correction aligned with the head before the torso fit is measured.
            float minimumDepth = camera.nearClipPlane
                + Mathf.Max(.01f, camera.nearClipPlane * .1f);
            float depth = Vector3.Dot(root.position - camera.transform.position,
                camera.transform.forward);
            if (depth < minimumDepth)
                root.position += camera.transform.forward * (minimumDepth - depth);
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
            _bodyWearFitted = false;
            _portrait?.ResetBodyWear();
            RenderedHeadScreenRect = default;
            ApplyRendererVisibility();
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
            DestroyMountedInstance(_fitBuffer);
            _fitBuffer = null;
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
