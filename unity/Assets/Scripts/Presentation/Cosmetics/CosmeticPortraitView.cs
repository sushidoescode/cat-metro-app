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

        public RectTransform RootTransform { get; private set; }
        public RectTransform BaseLayerTransform { get; private set; }
        public RectTransform OutfitLayerTransform { get; private set; }
        public RectTransform AccessoryLayerTransform { get; private set; }
        public RectTransform FrameLayerTransform { get; private set; }
        public string AppliedCatId { get; private set; } = string.Empty;
        public string AppliedOutfitAssetId { get; private set; } = string.Empty;
        public string AppliedAccessoryAssetId { get; private set; } = string.Empty;
        public string AppliedFrameAssetId { get; private set; } = string.Empty;

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

            ApplyLayer(BaseLayerTransform, snapshot.BaseAssetId, out var baseAssetId);
            AppliedCatId = string.IsNullOrEmpty(baseAssetId)
                ? string.Empty
                : snapshot.CatId ?? string.Empty;
            ApplyLayer(OutfitLayerTransform, snapshot.OutfitAssetId,
                out var outfitAssetId);
            ApplyLayer(AccessoryLayerTransform, snapshot.AccessoryAssetId,
                out var accessoryAssetId);
            ApplyLayer(FrameLayerTransform, snapshot.FrameAssetId,
                out var frameAssetId);
            AppliedOutfitAssetId = outfitAssetId;
            AppliedAccessoryAssetId = accessoryAssetId;
            AppliedFrameAssetId = frameAssetId;
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
            AppliedCatId = string.Empty;
            AppliedOutfitAssetId = string.Empty;
            AppliedAccessoryAssetId = string.Empty;
            AppliedFrameAssetId = string.Empty;
            CosmeticPortraitPainter.Clear(BaseLayerTransform);
            CosmeticPortraitPainter.Clear(OutfitLayerTransform);
            CosmeticPortraitPainter.Clear(AccessoryLayerTransform);
            CosmeticPortraitPainter.Clear(FrameLayerTransform);
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
