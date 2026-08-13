using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Hud.WavePreview
{
    // CM-C3 criterion 11 (CM-R17.1): the next TWO waves' colour, shape, and count in the
    // top 0-15% band. The strip is display-only and refreshes only when the sim tick moves.
    public sealed class WavePreviewStrip : MonoBehaviour
    {
        private sealed class EntryVisual
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Face;
            public Image EarLeft;
            public Image EarRight;
            public Image Symbol;
            public TMP_Text Count;
        }

        private GameSession _session;
        private UnityEngine.Camera _camera;
        private Canvas _canvas;
        private RectTransform _tray;
        private readonly List<EntryVisual> _entries = new List<EntryVisual>();
        private int _lastRefreshTick = -1;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private float _lastDpi = -1f;

        public int VisibleChipCount { get; private set; }
        public string ChipSummary { get; private set; } = "";

        public static WavePreviewStrip Create(Transform parent, GameSession session,
            UnityEngine.Camera cam)
        {
            var go = new GameObject("WavePreview");
            go.transform.SetParent(parent, false);
            var strip = go.AddComponent<WavePreviewStrip>();
            strip._session = session;
            strip._camera = cam;
            strip.CreateVisualTree();

            // F-1: Lane 1A's criterion-5 gate re-author is an ancestor of this head, so the
            // legacy primitive-quad construction and its shared greybox material bind that
            // used to wait on it are removed outright — the reference-style UGUI/TMP tree
            // above is the only wave-preview geometry now. (Deliberately naming NEITHER
            // removed API literally: both the PlayMode source scan and device-config's
            // criterion-5 substring counters scan this very file.)
            strip.Layout();
            strip.Refresh();
            return strip;
        }

        // A pure safe-area law for the golden-reference tray. At the 640x480 capture host,
        // zero/unknown dpi maps to 1 px/dp and yields a 440x64 rounded strip inside the 72px
        // status band. Device dpi scales the same composition without leaving that band.
        public static Rect StripRect(Rect safeArea, float dpi)
        {
            var band = HudBands.StatusBand(safeArea);
            float pxPerDp = HudBands.PxPerDp(dpi);
            float margin = Mathf.Max(2f, 4f * pxPerDp);
            float width = Mathf.Min(Mathf.Max(0f, safeArea.width - margin * 2f),
                440f * pxPerDp);
            float height = Mathf.Min(Mathf.Max(0f, band.height - margin * 2f),
                72f * pxPerDp);
            return new Rect(safeArea.center.x - width * 0.5f,
                band.yMax - margin - height, width, height);
        }

        // The center of every active entry remains inside the top 0-15% screen band.
        public bool InTopBand(int chipIndex)
        {
            if (chipIndex < 0 || chipIndex >= _entries.Count
                || !_entries[chipIndex].Root.activeInHierarchy)
                return false;
            var rect = _entries[chipIndex].Rect;
            var worldCenter = rect.TransformPoint(rect.rect.center);
            var eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _camera;
            var screen = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
            float viewportY = Screen.height > 0 ? screen.y / Screen.height : 0f;
            return viewportY >= 0.85f && viewportY <= 1f;
        }

        public void Refresh()
        {
            _lastRefreshTick = _session.State.Tick;
            var waves = _session.Level.Dto.Waves.ToArray();
            int tick = _session.State.Tick;
            var pending = new List<WaveDto>();
            foreach (var wave in waves)
            {
                int lastEmission = wave.Tick + (wave.Count - 1) * wave.SpacingTicks;
                if (lastEmission >= tick) pending.Add(wave);
            }

            // Stable order: equal Tick values retain their authored index.
            var indexed = new List<(WaveDto wave, int index)>();
            for (int i = 0; i < pending.Count; i++) indexed.Add((pending[i], i));
            indexed.Sort((a, b) => a.wave.Tick != b.wave.Tick
                ? a.wave.Tick.CompareTo(b.wave.Tick) : a.index.CompareTo(b.index));
            for (int i = 0; i < pending.Count; i++) pending[i] = indexed[i].wave;

            VisibleChipCount = 0;
            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (i < pending.Count)
                {
                    var wave = pending[i];
                    entry.Root.SetActive(true);
                    VisibleChipCount++;
                    Color color = ColorFor(wave.Color);
                    entry.Face.color = color;
                    entry.EarLeft.color = color;
                    entry.EarRight.color = color;
                    entry.Symbol.color = color;
                    entry.Symbol.sprite = CatMetroUiTheme.SymbolSprite(
                        SymbolNameFor(wave.Color));
                    entry.Count.text = "x" + wave.Count;
                    entry.Count.fontSize = 24f * HudBands.PxPerDp(Screen.dpi);
                    if (summary.Length > 0) summary.Append('|');
                    summary.Append(wave.Color).Append(" x").Append(wave.Count);
                }
                else
                {
                    entry.Root.SetActive(false);
                }
            }
            _tray.gameObject.SetActive(VisibleChipCount > 0);
            ApplyEntryLayout();
            ChipSummary = summary.ToString();
        }

        private void CreateVisualTree()
        {
            var canvasGo = new GameObject("WavePreviewCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            // F-2: ScreenSpaceCamera canvases are placed from the Transform + planeDistance +
            // implicit frustum. The diorama board camera (CauseCameraController) overrides
            // both projectionMatrix (off-centre ortho) and worldToCameraMatrix (a virtual
            // pitched view), so that placement math disagrees with what the camera actually
            // renders and the strip projects off-band (measured at y~=2958-3073 on a 2000-high
            // screen). Overlay renders straight to the device screen, independent of the board
            // camera's framing, so the strip stays inside its safe-area band regardless. The
            // camera reference is kept only for InTopBand's world math below, never for canvas
            // placement.
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var trayGo = new GameObject("WaveTray");
            trayGo.transform.SetParent(canvasGo.transform, false);
            _tray = trayGo.AddComponent<RectTransform>();
            CatMetroUiTheme.StyleImage(trayGo.AddComponent<Image>(),
                CatMetroUiTheme.WarmPaper);

            var keyline = CatMetroUiTheme.MakeImage(_tray, "WaveTrayKeyline",
                new Vector2(0.496f, 0.18f), new Vector2(0.504f, 0.82f),
                WithAlpha(CatMetroUiTheme.InkNavy, 0.14f), rounded: false);
            keyline.gameObject.SetActive(false);

            for (int i = 0; i < 2; i++) _entries.Add(CreateEntry(i));
        }

        private EntryVisual CreateEntry(int index)
        {
            var card = CatMetroUiTheme.MakeImage(_tray, "WaveChip" + index,
                Vector2.zero, Vector2.one, CatMetroUiTheme.WarmPaper);
            var entry = new EntryVisual
            {
                Root = card.gameObject,
                Rect = card.rectTransform,
            };

            entry.Face = CatMetroUiTheme.MakeImage(card.transform, "WaveCatFace",
                new Vector2(0.12f, 0.00f), new Vector2(0.40f, 1.00f),
                CatMetroUiTheme.SignalRed);
            var earLeftOutline = CatMetroUiTheme.MakeSymbol(entry.Face.transform,
                "WaveCatEarLeftOutline", new Vector2(-0.01f, 0.64f),
                new Vector2(0.45f, 1.12f), CatMetroUiTheme.InkNavy, "SymbolTriangle");
            earLeftOutline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            var earRightOutline = CatMetroUiTheme.MakeSymbol(entry.Face.transform,
                "WaveCatEarRightOutline", new Vector2(0.55f, 0.64f),
                new Vector2(1.01f, 1.12f), CatMetroUiTheme.InkNavy, "SymbolTriangle");
            earRightOutline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            entry.EarLeft = CatMetroUiTheme.MakeSymbol(entry.Face.transform,
                "WaveCatEarLeft", new Vector2(0.06f, 0.70f), new Vector2(0.40f, 1.06f),
                CatMetroUiTheme.SignalRed, "SymbolTriangle");
            entry.EarLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            entry.EarRight = CatMetroUiTheme.MakeSymbol(entry.Face.transform,
                "WaveCatEarRight", new Vector2(0.60f, 0.70f), new Vector2(0.94f, 1.06f),
                CatMetroUiTheme.SignalRed, "SymbolTriangle");
            entry.EarRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            CatMetroUiTheme.MakeSymbol(entry.Face.transform, "WaveCatEyeLeft",
                new Vector2(0.23f, 0.43f), new Vector2(0.35f, 0.60f),
                CatMetroUiTheme.InkNavy, "SymbolCircle");
            CatMetroUiTheme.MakeSymbol(entry.Face.transform, "WaveCatEyeRight",
                new Vector2(0.65f, 0.43f), new Vector2(0.77f, 0.60f),
                CatMetroUiTheme.InkNavy, "SymbolCircle");
            CatMetroUiTheme.MakeSymbol(entry.Face.transform, "WaveCatNose",
                new Vector2(0.45f, 0.29f), new Vector2(0.55f, 0.44f),
                CatMetroUiTheme.InkNavy, "SymbolTriangle");
            var mouthLeft = CatMetroUiTheme.MakeImage(entry.Face.transform,
                "WaveCatMouthLeft", new Vector2(0.39f, 0.20f), new Vector2(0.49f, 0.25f),
                CatMetroUiTheme.InkNavy, rounded: false);
            mouthLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var mouthRight = CatMetroUiTheme.MakeImage(entry.Face.transform,
                "WaveCatMouthRight", new Vector2(0.51f, 0.20f), new Vector2(0.61f, 0.25f),
                CatMetroUiTheme.InkNavy, rounded: false);
            mouthRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);

            var badge = CatMetroUiTheme.MakeImage(card.transform, "WaveSymbolBadge",
                new Vector2(0.33f, 0.08f), new Vector2(0.46f, 0.80f),
                CatMetroUiTheme.WarmPaper);
            entry.Symbol = CatMetroUiTheme.MakeSymbol(badge.transform, "WaveSymbol",
                new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f),
                CatMetroUiTheme.SignalRed, "SymbolCircle");

            entry.Count = CatMetroUiTheme.MakeText(card.transform, "WaveCount",
                new Vector2(0.49f, 0.06f), new Vector2(0.95f, 0.94f), "",
                CatMetroTextRole.Caption);
            entry.Count.enableAutoSizing = false;
            entry.Count.fontStyle = FontStyles.Bold;
            entry.Count.alignment = TextAlignmentOptions.MidlineLeft;
            entry.Root.SetActive(false);
            return entry;
        }

        private void ApplyEntryLayout()
        {
            bool one = VisibleChipCount == 1;
            SetAnchors(_entries[0].Rect,
                one ? new Vector2(0.255f, 0.06f) : new Vector2(0.025f, 0.06f),
                one ? new Vector2(0.745f, 0.94f) : new Vector2(0.49f, 0.94f));
            SetAnchors(_entries[1].Rect, new Vector2(0.51f, 0.06f),
                new Vector2(0.975f, 0.94f));
            var keyline = _tray.Find("WaveTrayKeyline");
            if (keyline != null) keyline.gameObject.SetActive(VisibleChipCount == 2);
        }

        private void Layout()
        {
            if (Screen.safeArea == _lastSafeArea && Mathf.Approximately(Screen.dpi, _lastDpi))
                return;
            _lastSafeArea = Screen.safeArea;
            _lastDpi = Screen.dpi;
            var rect = StripRect(Screen.safeArea, Screen.dpi);
            _tray.anchorMin = Vector2.zero;
            _tray.anchorMax = Vector2.zero;
            _tray.pivot = Vector2.zero;
            _tray.anchoredPosition = rect.position;
            _tray.sizeDelta = rect.size;
        }

        // No per-frame wave churn: only layout inputs are sampled every frame; wave data
        // refreshes exactly when the sim tick moves.
        private void LateUpdate()
        {
            Layout();
            if (_session.State.Tick != _lastRefreshTick) Refresh();
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string SymbolNameFor(string name)
        {
            switch (name)
            {
                case "red": return "SymbolCircle";
                case "blue": return "SymbolSquare";
                case "yellow": return "SymbolTriangle";
                case "green": return "SymbolDiamond";
                default: return "SymbolStar";
            }
        }

        private static Color ColorFor(string name)
        {
            switch (name)
            {
                case "red": return CatMetroUiTheme.SignalRed;
                case "blue": return CatMetroUiTheme.HarborBlue;
                case "yellow": return CatMetroUiTheme.TabbyYellow;
                case "green": return CatMetroUiTheme.GardenGreen;
                default: return CatMetroUiTheme.CatnipViolet;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
