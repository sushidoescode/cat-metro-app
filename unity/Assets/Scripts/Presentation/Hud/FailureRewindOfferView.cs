using System;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Strings;
using CatMetro.Presentation.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Hud
{
    // Exists only while the optional offer is actually shown. Retry owns its original band;
    // this separate canvas paints and registers the adjoining 48dp strip above it.
    public sealed class FailureRewindOfferView : MonoBehaviour
    {
        public const string RegionId = "failure.rewind";
        private Canvas _canvas;
        private RectTransform _rect;
        private ChromeRegions _regions;
        private Rect _lastSafeArea;
        private float _lastDpi = -1f;
        public Rect PaintedRectPx { get; private set; }

        public static Rect ChipRect(Rect safeArea, float dpi) =>
            new Rect(safeArea.x, HudBands.ThumbBand(safeArea).yMax, safeArea.width,
                HudBands.MinTargetDp * HudBands.PxPerDp(dpi));

        public static FailureRewindOfferView Create(Transform parent, Camera camera,
            ChromeRegions regions, Action tapped)
        {
            var canvasGo = new GameObject("FailureRewindCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;

            var go = new GameObject("FailureRewindOffer", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var view = go.AddComponent<FailureRewindOfferView>();
            view._canvas = canvas;
            view._rect = go.GetComponent<RectTransform>();
            view._regions = regions;
            var background = go.AddComponent<Image>();
            background.material = UiChromeMaterial.Shared;
            background.color = Palette.CreamCard;
            background.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            var text = label.AddComponent<TextMeshProUGUI>();
            text.text = UiStrings.Get(RegionId);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 30f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 30f;
            text.color = Palette.MetroTeal;
            text.raycastTarget = false;
            view.Layout();
            regions.Register(RegionId, () => view.PaintedRectPx, tapped, ChromeRegions.ParentPriority);
            return view;
        }

        private void LateUpdate() => Layout();

        private void Layout()
        {
            if (_rect == null || (_lastSafeArea == Screen.safeArea && _lastDpi == Screen.dpi)) return;
            _lastSafeArea = Screen.safeArea;
            _lastDpi = Screen.dpi;
            PaintedRectPx = ChipRect(_lastSafeArea, _lastDpi);
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = PaintedRectPx.position;
            _rect.sizeDelta = PaintedRectPx.size;
        }

        public void Dismiss()
        {
            Unregister();
            var canvas = _canvas;
            _canvas = null;
            if (canvas == null) return;
            canvas.gameObject.SetActive(false);
            Destroy(canvas.gameObject);
        }

        private void Unregister()
        {
            _regions?.Unregister(RegionId);
            _regions = null;
        }

        private void OnDisable() => Dismiss();
        private void OnDestroy() => Dismiss();
    }
}
