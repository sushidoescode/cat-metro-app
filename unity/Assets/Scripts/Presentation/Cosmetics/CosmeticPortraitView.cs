using System.Collections.Generic;
using CatMetro.Services.Cosmetics;
using UnityEngine;

namespace CatMetro.Presentation.Cosmetics
{
    /// <summary>
    /// Typed, reusable portrait mount. It owns only presentation and follows the bound source's
    /// authoritative snapshot across rebind, disable/enable, and destruction.
    /// </summary>
    public sealed class CosmeticPortraitView : MonoBehaviour
    {
        private ICosmeticPortraitSource _source;
        private bool _subscribed;
        private bool _followsCurrentPortrait;
        private bool _baseLayerSuppressed;
        private RectTransform _bodyWear;
        private Vector2 _bodyWearCenterOffset;
        private readonly List<FramePart> _frameParts = new List<FramePart>();
        private readonly Vector3[] _bodyCorners = new Vector3[4];

        public RectTransform RootTransform { get; private set; }
        public RectTransform BaseLayerTransform { get; private set; }
        public RectTransform OutfitLayerTransform { get; private set; }
        public RectTransform AccessoryLayerTransform { get; private set; }
        public RectTransform FrameLayerTransform { get; private set; }
        public string AppliedCatId { get; private set; } = string.Empty;
        public string AppliedOutfitAssetId { get; private set; } = string.Empty;
        public string AppliedAccessoryAssetId { get; private set; } = string.Empty;
        public string AppliedFrameAssetId { get; private set; } = string.Empty;
        internal event System.Action PortraitApplied;

        public static CosmeticPortraitView Create(Transform parent,
            ICosmeticPortraitSource source, string name = "CosmeticPortrait")
        {
            var view = CreateLayers(parent, name);
            view._followsCurrentPortrait = true;
            view.Bind(source);
            return view;
        }

        /// <summary>
        /// Creates a resolver-backed portrait that changes only through ApplySnapshot. Card
        /// previews use this path so profile events and OnEnable cannot replace their preview.
        /// </summary>
        public static CosmeticPortraitView CreateStaticSnapshot(Transform parent,
            ICosmeticPortraitSource source, string name = "CosmeticPortrait")
        {
            var view = CreateLayers(parent, name);
            view._source = source;
            view._followsCurrentPortrait = false;
            view.ClearAll();
            return view;
        }

        private static CosmeticPortraitView CreateLayers(Transform parent, string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "CosmeticPortrait" : name,
                typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CosmeticPortraitView>();
            view.RootTransform = (RectTransform)go.transform;
            Stretch(view.RootTransform);

            view.BaseLayerTransform = MakeLayer(go.transform, "BaseLayer");
            view.OutfitLayerTransform = MakeLayer(go.transform, "OutfitLayer");
            view.AccessoryLayerTransform = MakeLayer(go.transform, "AccessoryLayer");
            view.FrameLayerTransform = MakeLayer(go.transform, "FrameLayer");
            return view;
        }

        public void Bind(ICosmeticPortraitSource source)
        {
            _followsCurrentPortrait = true;
            if (!ReferenceEquals(_source, source))
            {
                Unsubscribe();
                _source = source;
            }

            if (isActiveAndEnabled) Subscribe();
            if (_source != null) ApplySnapshot(_source.CurrentPortrait);
            else ClearAll();
        }

        public void ApplySnapshot(CosmeticPortraitSnapshot snapshot)
        {
            if (_source == null)
            {
                ClearAll();
                return;
            }

            ResetBodyWear();
            ResetFrameLayout();
            ApplyLayer(BaseLayerTransform, snapshot.BaseAssetId, out var baseAssetId);
            AppliedCatId = string.IsNullOrEmpty(baseAssetId)
                ? string.Empty
                : snapshot.CatId ?? string.Empty;
            if (_baseLayerSuppressed) BaseLayerTransform.gameObject.SetActive(false);
            ApplyLayer(OutfitLayerTransform, snapshot.OutfitAssetId,
                out var outfitAssetId);
            ApplyLayer(AccessoryLayerTransform, snapshot.AccessoryAssetId,
                out var accessoryAssetId);
            ApplyLayer(FrameLayerTransform, snapshot.FrameAssetId,
                out var frameAssetId);
            _bodyWear = CosmeticPortraitPainter.FindBodyWear(OutfitLayerTransform);
            AppliedOutfitAssetId = outfitAssetId;
            AppliedAccessoryAssetId = accessoryAssetId;
            AppliedFrameAssetId = frameAssetId;
            PortraitApplied?.Invoke();
        }

        internal bool HasBodyWear => _bodyWear != null
            && OutfitLayerTransform.gameObject.activeSelf;

        internal bool FitBodyWear(Rect regionInPortrait)
        {
            if (!HasBodyWear || regionInPortrait.width <= 0f || regionInPortrait.height <= 0f)
                return false;
            ResetBodyWear();
            // Measure every rotated corner, including both collars, before scaling the group.
            // The resulting rectangle bounds all five pieces, not just the coat capsule.
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < _bodyWear.childCount; i++)
            {
                if (!(_bodyWear.GetChild(i) is RectTransform part)) continue;
                part.GetWorldCorners(_bodyCorners);
                foreach (Vector3 world in _bodyCorners)
                {
                    Vector2 local = _bodyWear.InverseTransformPoint(world);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }
            Vector2 size = max - min;
            if (!float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x <= 0f || size.y <= 0f)
                return false;
            Vector2 scale = new Vector2(regionInPortrait.width / size.x, regionInPortrait.height / size.y);
            _bodyWear.localScale = new Vector3(scale.x, scale.y, 1f);
            _bodyWearCenterOffset = Vector2.Scale((min + max) * .5f, scale);
            MoveBodyWearCenter(regionInPortrait.center);
            return true;
        }

        internal void MoveBodyWearCenter(Vector2 centerInPortrait)
        {
            if (HasBodyWear) _bodyWear.anchoredPosition = centerInPortrait - _bodyWearCenterOffset;
        }

        internal void ResetBodyWear()
        {
            if (_bodyWear == null) return;
            Stretch(_bodyWear);
            _bodyWear.localScale = Vector3.one;
            _bodyWear.localRotation = Quaternion.identity;
            _bodyWear.anchoredPosition3D = Vector3.zero;
            _bodyWearCenterOffset = Vector2.zero;
        }

        internal bool HasFrame => FrameLayerTransform.gameObject.activeSelf
            && !string.IsNullOrEmpty(AppliedFrameAssetId);

        internal bool TryGetHeadWearScreenRect(Camera camera, out Rect result)
        {
            Transform hat = OutfitLayerTransform.Find("outfit.conductor/HeadWear");
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            if (hat != null && hat.gameObject.activeInHierarchy)
                for (int i = 0; i < hat.childCount; i++)
                {
                    if (!(hat.GetChild(i) is RectTransform part) || !part.gameObject.activeSelf) continue;
                    part.GetWorldCorners(_bodyCorners);
                    foreach (Vector3 world in _bodyCorners)
                    {
                        Vector2 screen = camera.WorldToScreenPoint(world);
                        min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
                    }
                }
            result = max.x > min.x && max.y > min.y
                ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
            return result.width > 0f && result.height > 0f;
        }

        internal bool FitFrame(Rect outerInPortrait, Rect openingInPortrait)
        {
            ResetFrameLayout();
            if (!HasFrame || outerInPortrait.width <= 0f || outerInPortrait.height <= 0f
                || openingInPortrait.width <= 0f || openingInPortrait.height <= 0f
                || openingInPortrait.xMin <= outerInPortrait.xMin || openingInPortrait.xMax >= outerInPortrait.xMax
                || openingInPortrait.yMin <= outerInPortrait.yMin || openingInPortrait.yMax >= outerInPortrait.yMax)
                return false;
            Transform token = null;
            for (int i = 0; i < FrameLayerTransform.childCount; i++)
                if (FrameLayerTransform.GetChild(i).gameObject.activeSelf) token = FrameLayerTransform.GetChild(i);
            if (token == null || (token.name != "frame.brass" && token.name != "frame.lantern")) return false;
            for (int i = 0; i < token.childCount; i++)
                if (token.GetChild(i) is RectTransform part) _frameParts.Add(new FramePart(part));
            FrameLayerTransform.anchorMin = FrameLayerTransform.anchorMax = new Vector2(.5f, .5f);
            FrameLayerTransform.sizeDelta = outerInPortrait.size;
            MoveFrameCenter(outerInPortrait.center);
            // Use one physical border width on every side. Independent x/y compression
            // makes the tall rails broad capsules while the horizontal rails disappear.
            float border = Mathf.Min(openingInPortrait.xMin - outerInPortrait.xMin,
                outerInPortrait.xMax - openingInPortrait.xMax,
                openingInPortrait.yMin - outerInPortrait.yMin,
                outerInPortrait.yMax - openingInPortrait.yMax);
            Vector2 min = new Vector2(border / outerInPortrait.width, border / outerInPortrait.height);
            Vector2 max = Vector2.one - min;
            foreach (FramePart part in _frameParts) part.Fit(min, max, outerInPortrait.size);
            return true;
        }

        internal void MoveFrameCenter(Vector2 centerInPortrait) =>
            FrameLayerTransform.anchoredPosition = centerInPortrait;

        internal void ResetFrameLayout()
        {
            foreach (FramePart part in _frameParts) part.Restore();
            _frameParts.Clear();
            if (FrameLayerTransform == null) return;
            Stretch(FrameLayerTransform);
            FrameLayerTransform.localScale = Vector3.one;
            FrameLayerTransform.localRotation = Quaternion.identity;
            FrameLayerTransform.anchoredPosition3D = Vector3.zero;
        }

        private readonly struct FramePart
        {
            private readonly RectTransform _part;
            private readonly Vector2 _min, _max, _size, _cornerEnvelope;
            private readonly Vector3 _position, _scale;
            private readonly Quaternion _rotation;

            public FramePart(RectTransform part)
            {
                _part = part; _min = part.anchorMin; _max = part.anchorMax;
                _size = part.sizeDelta; _position = part.anchoredPosition3D;
                _scale = part.localScale; _rotation = part.localRotation;
                _cornerEnvelope = _max - _min;
                if (part.name.EndsWith("Glow", System.StringComparison.Ordinal)
                    && part.parent.Find(part.name.Substring(0, part.name.Length - 4) + "Housing") is RectTransform housing)
                    _cornerEnvelope = housing.anchorMax - housing.anchorMin;
            }

            public void Restore()
            {
                if (_part == null) return;
                _part.anchorMin = _min; _part.anchorMax = _max;
                _part.sizeDelta = _size; _part.anchoredPosition3D = _position;
                _part.localScale = _scale; _part.localRotation = _rotation;
            }

            public void Fit(Vector2 openingMin, Vector2 openingMax, Vector2 outerSize)
            {
                Vector2 center = (_min + _max) * .5f;
                bool corner = _part.name.StartsWith("Notch", System.StringComparison.Ordinal)
                    || _part.name.StartsWith("Lantern", System.StringComparison.Ordinal);
                if (!corner)
                {
                    _part.anchorMin = Map(_min, openingMin, openingMax);
                    _part.anchorMax = Map(_max, openingMin, openingMax);
                    _part.offsetMin = _part.offsetMax = Vector2.zero;
                    return;
                }
                // Keep corner decorations proportional. Uniformly fit every rotated corner
                // inside its strip; stretching a wide diamond would reach back into the cat.
                Vector2 mapped = Map(center, openingMin, openingMax);
                Vector2 size = Vector2.Scale(_max - _min, outerSize);
                Vector2 point = Vector2.Scale(mapped, outerSize);
                float roomX = center.x < .5f
                    ? Mathf.Min(point.x, openingMin.x * outerSize.x - point.x)
                    : Mathf.Min(outerSize.x - point.x, point.x - openingMax.x * outerSize.x);
                float roomY = center.y < .5f
                    ? Mathf.Min(point.y, openingMin.y * outerSize.y - point.y)
                    : Mathf.Min(outerSize.y - point.y, point.y - openingMax.y * outerSize.y);
                Vector3 right = _rotation * Vector3.right, up = _rotation * Vector3.up;
                // A lantern's glow shares the housing's scale, retaining its original inset.
                Vector2 envelope = Vector2.Scale(_cornerEnvelope, outerSize);
                float halfWidth = (Mathf.Abs(right.x) * envelope.x + Mathf.Abs(up.x) * envelope.y) * .5f;
                float halfHeight = (Mathf.Abs(right.y) * envelope.x + Mathf.Abs(up.y) * envelope.y) * .5f;
                float scale = Mathf.Min(roomX / halfWidth, roomY / halfHeight);
                _part.anchorMin = _part.anchorMax = mapped;
                _part.sizeDelta = size;
                _part.anchoredPosition3D = Vector3.zero;
                _part.localScale = Vector3.one * scale;
            }

            // Both original frame styles have all rail thickness inside these strips.
            // Their center span stretches while the edge strips use the measured clearance.
            private static Vector2 Map(Vector2 point, Vector2 min, Vector2 max) =>
                new Vector2(Map(point.x, min.x, max.x), Map(point.y, min.y, max.y));
            private static float Map(float point, float min, float max) => point < .15f
                ? point / .15f * min : point > .85f
                ? max + (point - .85f) / .15f * (1f - max)
                : Mathf.Lerp(min, max, (point - .15f) / .70f);
        }

        internal void SetBaseLayerSuppressed(bool suppressed)
        {
            if (_baseLayerSuppressed == suppressed) return;
            _baseLayerSuppressed = suppressed;
            if (suppressed)
            {
                BaseLayerTransform.gameObject.SetActive(false);
                return;
            }

            if (_source != null) ApplySnapshot(_source.CurrentPortrait);
        }

        private void OnEnable()
        {
            if (!_followsCurrentPortrait || _source == null) return;
            Subscribe();
            ApplySnapshot(_source.CurrentPortrait);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void OnSourceChanged()
        {
            if (_source != null) ApplySnapshot(_source.CurrentPortrait);
        }

        private void ApplyLayer(RectTransform layer, string assetId, out string appliedAssetId)
        {
            appliedAssetId = string.Empty;
            if (string.IsNullOrEmpty(assetId)
                || !_source.TryGetPortraitAsset(assetId, out var asset)
                || asset == null
                || !CosmeticPortraitPainter.Paint(layer, asset.RendererToken))
            {
                CosmeticPortraitPainter.Clear(layer);
                return;
            }

            appliedAssetId = assetId;
        }

        private void Subscribe()
        {
            if (_subscribed || _source == null) return;
            _source.Changed += OnSourceChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_source != null) _source.Changed -= OnSourceChanged;
            _subscribed = false;
        }

        private void ClearAll()
        {
            ResetBodyWear();
            ResetFrameLayout();
            AppliedCatId = string.Empty;
            AppliedOutfitAssetId = string.Empty;
            AppliedAccessoryAssetId = string.Empty;
            AppliedFrameAssetId = string.Empty;
            CosmeticPortraitPainter.Clear(BaseLayerTransform);
            CosmeticPortraitPainter.Clear(OutfitLayerTransform);
            CosmeticPortraitPainter.Clear(AccessoryLayerTransform);
            CosmeticPortraitPainter.Clear(FrameLayerTransform);
            PortraitApplied?.Invoke();
        }

        private static RectTransform MakeLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
