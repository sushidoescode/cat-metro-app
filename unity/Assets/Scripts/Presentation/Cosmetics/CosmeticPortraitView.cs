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
