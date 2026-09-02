using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Presentation.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Hud.WavePreview
{
    // HUD-WAVE: the wave preview as docs/reference/target-01-tabletop.png draws it — a rounded
    // cream capsule across the top of the safe area holding the upcoming cats in order as round
    // colour-coded faces, with the run counters on a row beneath it.
    //
    // What changed and why. The original strip was TWO WORLD-SPACE QUADS parked at viewport
    // y 0.93, one per pending WAVE, labelled "x2" — which is the bare red chip the validation
    // render caught. Two problems: a wave is not a cat, so the player could not see WHICH cats
    // were coming in WHICH order; and world-space overlay geometry sitting in front of the
    // diorama is the thing BoardLookTests has to keep out of the shadow pass. Both go away by
    // moving to a screen-space canvas — the BannerView route. Everything the capsule draws is
    // UGUI (Image / TextMeshProUGUI), which renders through CanvasRenderer, so the strip now
    // contributes ZERO Renderers, no colliders and no material instances at all.
    //
    // Still true, and still enforced: information only, ZERO interactive elements (no collider,
    // no raycast target, no GraphicRaycaster on the canvas), and read-only on game state — the
    // strip only ever READS Level.Dto and State.
    //
    // The legacy read-backs VisibleChipCount / ChipSummary / InTopBand are preserved with their
    // original meanings (pending WAVES, capped at two, and "is the preview in the top band")
    // because FailureTests and DeviceConfigTests pin them; the new per-cat surface is additive.
    public sealed class WavePreviewStrip : MonoBehaviour
    {
        // How many faces fit before the queue collapses into a "+N" tail. Six is what the
        // target's widest capsule (target-02-diorama.png) shows.
        public const int MaxFaces = 6;

        // Capsule geometry as fractions of the SAFE AREA, with dp floors so a short viewport
        // cannot shrink the capsule into illegibility. Measured off target-01: the capsule
        // spans ~80% of the frame width and ~8% of its height, inset ~3% from the top.
        private const float HorizontalInsetFraction = 0.10f;
        private const float HeightFraction = 0.072f;
        private const float TopMarginFraction = 0.028f;
        private const float CounterRowFraction = 0.030f;
        private const float CounterGapFraction = 0.008f;
        private const float MinHeightDp = 48f;
        private const float MinInsetDp = 16f;
        private const float MinTopMarginDp = 12f;

        private const float FaceSizeFraction = 0.62f;  // of capsule height
        private const float FaceGapFraction = 0.28f;   // of face size

        private GameSession _session;
        private UnityEngine.Camera _camera;
        private readonly List<GameObject> _chips = new List<GameObject>();
        private readonly List<TextMesh> _counts = new List<TextMesh>();
        private readonly List<TextMesh> _symbols = new List<TextMesh>();
        private readonly List<Renderer> _renderers = new List<Renderer>(); // review S6: cached
        private TextMesh _flipBudget;
        private int _lastRefreshTick = -1;

        // --- legacy read-backs (pinned by FailureTests / DeviceConfigTests) ---

        // Pending WAVES represented, capped at two. Unchanged meaning.
        public int VisibleChipCount { get; private set; }
        public string ChipSummary { get; private set; } = ""; // "red x2|blue x2" for asserts
        public string FlipSummary => _flipBudget != null && _flipBudget.gameObject.activeSelf
            ? _flipBudget.text : "";

        public static WavePreviewStrip Create(Transform parent, GameSession session,
            UnityEngine.Camera cam)
        {
            var go = new GameObject("WavePreview");
            go.transform.SetParent(parent, false);
            var strip = go.AddComponent<WavePreviewStrip>();
            strip._session = session;
            strip._camera = cam;
            for (int i = 0; i < 2; i++)
            {
                var chip = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var renderer = chip.GetComponent<Renderer>();
                renderer.sharedMaterial = Board.GreyboxMaterial.Shared;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Object.Destroy(chip.GetComponent<Collider>()); // ZERO interactive elements
                chip.name = "chip" + i;
                chip.transform.SetParent(go.transform, false);
                chip.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
                var text = new GameObject("count").AddComponent<TextMesh>();
                text.transform.SetParent(chip.transform, false);
                text.transform.localPosition = new Vector3(0.22f, 0f, -0.01f);
                text.characterSize = 0.2f;
                text.anchor = TextAnchor.MiddleCenter;
                var textRenderer = text.GetComponent<Renderer>();
                textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
                var symbol = new GameObject("cat-token").AddComponent<TextMesh>();
                symbol.transform.SetParent(chip.transform, false);
                symbol.transform.localPosition = new Vector3(-0.24f, 0f, -0.01f);
                symbol.characterSize = 0.24f;
                symbol.anchor = TextAnchor.MiddleCenter;
                symbol.alignment = TextAlignment.Center;
                symbol.color = Theme.Palette.InkNavy;
                var symbolRenderer = symbol.GetComponent<Renderer>();
                symbolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                symbolRenderer.receiveShadows = false;
                strip._chips.Add(chip);
                strip._counts.Add(text);
                strip._symbols.Add(symbol);
                strip._renderers.Add(renderer);
            }

            strip._flipBudget = new GameObject("flip-budget").AddComponent<TextMesh>();
            strip._flipBudget.transform.SetParent(go.transform, false);
            strip._flipBudget.characterSize = 0.15f;
            strip._flipBudget.anchor = TextAnchor.MiddleCenter;
            strip._flipBudget.alignment = TextAlignment.Center;
            strip._flipBudget.color = Theme.Palette.InkNavy;
            var budgetRenderer = strip._flipBudget.GetComponent<Renderer>();
            budgetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            budgetRenderer.receiveShadows = false;
            strip.Refresh();
            strip.Layout();
            return strip;
        }

        private void Build(UnityEngine.Camera cam)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            if (cam != null)
            {
                // Screen Space - Camera so the capsule lands in capture RenderTextures, the way
                // ScreenChromeController does it — an Overlay canvas never reaches a capture.
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = cam;
                _canvas.planeDistance = 1f;
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            // Under BannerView (90), ScreenChromeController (100), results (110) and the
            // screens canvas (120): the HUD is the bottom of the chrome stack, so outcome copy
            // and modals always cover it.
            _canvas.sortingOrder = 80;
            // NO GraphicRaycaster on purpose — the strip is information only.
            _canvasRect = (RectTransform)_canvas.transform;

            // Everything paintable hangs off ONE root so the outcome states can hide the HUD
            // with a single SetActive, while this component keeps ticking.
            _hudRoot = NewRect(transform, "Hud");
            Stretch(_hudRoot);

            _capsule = AddImage(_hudRoot, "Capsule", HudShapeSprites.Capsule);
            _capsule.type = Image.Type.Sliced;   // rounded corners survive any width
            _capsule.color = Palette.WarmPaper;

            // The decorative wave from the target art. A RectMask2D clips to a RECTANGLE, which
            // would show square corners where the capsule is round, so the clip rect is also
            // inset past the corner radius in LayoutForViewport — the band then never reaches
            // the curve and the mask only has to stop the horizontal tiling.
            var waveHost = new GameObject("WaveClip");
            waveHost.transform.SetParent(_capsule.transform, false);
            _waveClip = waveHost.AddComponent<RectTransform>();
            waveHost.AddComponent<RectMask2D>();
            _wave = AddImage(waveHost.transform, "Wave", HudShapeSprites.WaveBand);
            _wave.type = Image.Type.Tiled;
            _wave.color = Palette.CreamCard;
            Stretch(_wave.rectTransform);

            _faceRow = NewRect(_hudRoot, "Faces");
            for (int i = 0; i < MaxFaces; i++)
            {
                var face = CatFaceView.Create(_faceRow, "face" + i);
                face.gameObject.SetActive(false);
                _faces.Add(face);
            }
            _overflow = AddLabel(_faceRow, "Overflow", Palette.InkNavy);
            _overflow.fontStyle = FontStyles.Bold;

            var counters = NewRect(_hudRoot, "Counters");
            // Stretched to the canvas: the counter children are placed in ABSOLUTE canvas
            // pixels, which only resolves correctly if their parent's bottom-left IS the
            // canvas bottom-left. A default RectTransform is a 100x100 box at the centre.
            Stretch(counters);
            // Counter markers still DO NOT reuse the destination shape vocabulary — a triangle
            // or a diamond down here would read as a line badge — but they are no longer bare
            // discs either. They were, and the number FORMAT ("n/m" vs "n") was left to carry
            // the whole distinction: a teal dot and an orange dot, which says "two counts of
            // something" and makes the player learn which is which. The target art names them
            // outright, so these do too: a TROPHY for deliveries against the win condition, a
            // group of PEOPLE for cats currently riding.
            //
            // Both are WarmPaper, not the accent tokens. The counters sit on the diorama with
            // no card behind them, so the glyph and its numeral have to read as one object;
            // colouring the mark and not the number split them into two. The target draws the
            // whole row in one cream, and cream on the dark tabletop is the contrast that works.
            _deliveriesMark = AddImage(counters, "DeliveredMark", HudShapeSprites.Trophy);
            _deliveriesMark.color = Palette.WarmPaper;
            _deliveries = AddLabel(counters, "Delivered", Palette.WarmPaper);
            _ridersMark = AddImage(counters, "RidersMark", HudShapeSprites.People);
            _ridersMark.color = Palette.WarmPaper;
            _riders = AddLabel(counters, "Riders", Palette.WarmPaper);
        }

        // Read-only on game state throughout: Level.Dto and State are only ever READ.
        public void Refresh()
        {
            _lastRefreshTick = _session.State.Tick;
            int tick = _session.State.Tick;
            var waves = _session.Level.Dto.Waves;

            RefreshPendingWaveReadbacks(waves, tick);

            var queue = UpcomingCats.Next(waves, tick, MaxFaces);
            RemainingCats = UpcomingCats.RemainingCount(waves, tick);
            FaceCount = queue.Count;

            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < _faces.Count; i++)
            {
                bool used = i < queue.Count;
                if (_faces[i].gameObject.activeSelf != used) _faces[i].gameObject.SetActive(used);
                if (!used) continue;
                _faces[i].Bind(queue[i].Color);
                if (summary.Length > 0) summary.Append('|');
                summary.Append(queue[i].Color);
            }
            FaceSummary = summary.ToString();

            int hidden = RemainingCats - FaceCount;
            _overflow.text = hidden > 0 ? "+" + hidden : "";

            var win = _session.Level.Dto.Win;
            _deliveries.text = win != null
                ? _session.State.Deliveries + "/" + win.Deliveries
                : _session.State.Deliveries.ToString();
            _riders.text = RidersOnBoard().ToString();

            // Re-place the faces for the queue that just changed. Keep an injected viewport if
            // one is in force; otherwise derive from the canvas, never from raw Screen pixels.
            LayoutForViewport(_lastSafeArea.width > 0f ? _lastSafeArea : CanvasSafeArea(),
                _lastDpi > 0f ? _lastDpi : CanvasDpi());
        }

        // The old per-wave read-backs, computed exactly as before so the pinned assertions keep
        // their original meaning: pending waves (a wave is pending until its final emission
        // tick passes), stable order on Tick with authored index as the tie-break, capped at 2.
        private void RefreshPendingWaveReadbacks(System.ReadOnlyMemory<WaveDto> waves, int tick)
        {
            var pending = new List<WaveDto>();
            var span = waves.Span;
            for (int i = 0; i < span.Length; i++)
            {
                var w = span[i];
                if (w == null) continue;
                int lastEmission = w.Tick + (w.Count - 1) * w.SpacingTicks;
                if (lastEmission >= tick) pending.Add(w);
            }
            var indexed = new List<(WaveDto w, int i)>();
            for (int i = 0; i < pending.Count; i++) indexed.Add((pending[i], i));
            indexed.Sort((a, b) => a.w.Tick != b.w.Tick
                ? a.w.Tick.CompareTo(b.w.Tick) : a.i.CompareTo(b.i));

            var summary = new System.Text.StringBuilder();
            int shown = Mathf.Min(2, indexed.Count);
            for (int i = 0; i < shown; i++)
            {
                if (i < pending.Count)
                {
                    _chips[i].SetActive(true);
                    VisibleChipCount++;
                    var band = _camera.ViewportToWorldPoint(new Vector3(0.35f + 0.3f * i, 0.93f,
                        -_camera.transform.position.z));
                    _chips[i].transform.position = new Vector3(band.x, band.y, -1.5f);
                    _renderers[i].material.color = ColorFor(pending[i].Color);
                    _counts[i].text = "x" + pending[i].Count;
                    _symbols[i].text = TokenGlyph(pending[i]);
                    if (summary.Length > 0) summary.Append('|');
                    summary.Append(pending[i].Color).Append(" x").Append(pending[i].Count);
                }
                else
                {
                    _chips[i].SetActive(false);
                }
            }
            VisibleChipCount = shown;
            ChipSummary = summary.ToString();

            var flipStatus = _session.FlipStatus;
            _flipBudget.gameObject.SetActive(flipStatus.IsBudgeted);
            if (flipStatus.IsBudgeted)
            {
                var band = _camera.ViewportToWorldPoint(new Vector3(0.86f, 0.93f,
                    -_camera.transform.position.z));
                _flipBudget.transform.position = new Vector3(band.x, band.y, -1.5f);
                _flipBudget.text = Strings.UiStrings.Get("hud.flips")
                    .Replace("{used}", flipStatus.Used.ToString())
                    .Replace("{limit}", flipStatus.PerfectMaxSwitches.ToString());
                _flipBudget.color = flipStatus.RemainingToPerfect > 0
                    ? Theme.Palette.InkNavy : Theme.Palette.SignalRed;
            }
        }

        // Cats currently on the board — live train slots. State.Score/Chain are pinned at 0
        // (Domain: "scoring pinned, Q-C"), so this is the only honest "passengers" number the
        // session actually carries.
        private int RidersOnBoard()
        {
            int n = 0;
            var trains = _session.State.Trains;
            for (int i = 0; i < trains.Length; i++)
                if (trains[i].Id != 0 && trains[i].State != CatMetro.Domain.TrainState.None) n++;
            return n;
        }

        // The screen-state seam, mirroring ScreenChromeController.Attach. GameRoot binds it;
        // an unbound strip stays visible, which is what the LaunchWith fixture seam wants.
        public void BindScreenState(System.Func<string> screenState)
        {
            _screenState = screenState;
            ApplyScreenState();
        }

        public bool IsVisible => _hudRoot != null && _hudRoot.gameObject.activeSelf;

        // The pure state law. The wave preview answers "what is coming next", which is a
        // meaningless question once the run is over — so Won and FailureReview hide it
        // OUTRIGHT rather than relying on the banner's sorting order to cover it. Two views
        // edited by different lanes must not depend on each other's z-order to stay correct.
        // Halted keeps the HUD: the run is paused, not finished.
        public static bool VisibleInState(string state) =>
            state != "Won" && state != "FailureReview";

        private void ApplyScreenState()
        {
            if (_hudRoot == null) return;
            bool show = _screenState == null || VisibleInState(_screenState());
            if (_hudRoot.gameObject.activeSelf != show) _hudRoot.gameObject.SetActive(show);
        }

        // The preview sits in the top 0-15% band of the viewport. Originally this asked the
        // camera where a world-space quad was; the capsule has no world position, so it now
        // measures the capsule's own centre against the canvas height — same law, same band,
        // read off the thing that is actually on screen.
        public bool InTopBand(int index)
        {
            if (index < 0 || index >= MaxFaces) return false;
            float canvasHeight = _canvasRect != null ? _canvasRect.rect.height : Screen.height;
            if (canvasHeight <= 0f) return false;
            float centre = _capsulePx.y + _capsulePx.height * 0.5f;
            float vp = centre / canvasHeight;
            return vp >= 0.85f && vp <= 1.0f;
        }

        // Public for the phone-capture rig: the test injects the 917x2048 safe area before it
        // renders, rather than trusting the editor's landscape Game view (the BannerView seam).
        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _capsulePx = CapsuleRect(safeArea, dpi);
            _counterPx = CounterRowRect(safeArea, dpi);
            _lastSafeArea = safeArea;
            _lastDpi = dpi;

            PlacePx((RectTransform)_capsule.transform, _capsulePx);
            // Inset the wave clip by half the capsule height on each side: that is more than
            // the rounded corner ever spans, so the band's square clip edge stays hidden
            // under the straight part of the capsule.
            float capInset = _capsulePx.height * 0.5f;
            _waveClip.anchorMin = Vector2.zero;
            _waveClip.anchorMax = Vector2.one;
            _waveClip.offsetMin = new Vector2(capInset, 0f);
            _waveClip.offsetMax = new Vector2(-capInset, 0f);

            float faceSize = _capsulePx.height * FaceSizeFraction;
            float gap = faceSize * FaceGapFraction;
            PlacePx(_faceRow, _capsulePx);

            // Centre the row of faces (plus the overflow tail) inside the capsule.
            bool hasOverflow = _overflow.text.Length > 0;
            float overflowWidth = hasOverflow ? faceSize * 0.95f : 0f;
            float total = FaceCount > 0 ? FaceCount * faceSize + (FaceCount - 1) * gap : 0f;
            if (hasOverflow) total += gap + overflowWidth;
            float cursor = -total * 0.5f + faceSize * 0.5f;
            for (int i = 0; i < FaceCount; i++)
            {
                case "red": return new Color(0.85f, 0.2f, 0.2f);
                case "blue": return new Color(0.2f, 0.4f, 0.9f);
                case "yellow": return new Color(0.9f, 0.8f, 0.2f);
                case "green": return new Color(0.2f, 0.75f, 0.3f);
                case "wild": return Theme.Palette.CreamCard;
                default: return Color.magenta;
            }
            if (hasOverflow)
            {
                cursor += -faceSize * 0.5f + overflowWidth * 0.5f;
                PlaceCentred((RectTransform)_overflow.transform,
                    new Vector2(cursor, 0f), new Vector2(overflowWidth, faceSize));
                _overflow.fontSizeMax = faceSize * 0.62f;
            }

            LayoutCounters();
        }

        private void LayoutCounters()
        {
            float row = _counterPx.height;
            float mark = row * 0.72f;
            float textWidth = row * 2.6f;
            float gap = row * 0.28f;
            // Two [mark][gap/2][text] pairs with one full gap between them, centred as a group.
            float pair = mark + gap * 0.5f + textWidth;
            float total = pair * 2f + gap;
            float x = _counterPx.x + _counterPx.width * 0.5f - total * 0.5f;
            float centreY = _counterPx.y + row * 0.5f;

            x = PlaceCounter(_deliveriesMark, _deliveries, x, centreY, mark, textWidth, gap, row);
            x += gap;
            PlaceCounter(_ridersMark, _riders, x, centreY, mark, textWidth, gap, row);

            _deliveries.fontSizeMax = row;
            _riders.fontSizeMax = row;
        }

        private static float PlaceCounter(Image mark, TMP_Text label, float x, float centreY,
            float markSize, float textWidth, float gap, float rowHeight)
        {
            PlacePx(mark.rectTransform,
                new Rect(x, centreY - markSize * 0.5f, markSize, markSize));
            x += markSize + gap * 0.5f;
            PlacePx((RectTransform)label.transform,
                new Rect(x, centreY - rowHeight * 0.5f, textWidth, rowHeight));
            label.alignment = TextAlignmentOptions.Left;
            return x + textWidth;
        }

        // LateUpdate, not Update: GameRoot advances the session in its own Update, so
        // refreshing here keeps the capsule from painting a tick-stale queue for a frame.
        private void LateUpdate()
        {
            if (_session != null && _session.State.Tick != _lastRefreshTick) Refresh();
            Layout();
            ApplyScreenState();
        }

        private void Layout()
        {
            var safeArea = CanvasSafeArea();
            float dpi = CanvasDpi();
            if (safeArea == _lastSafeArea && dpi == _lastDpi) return;
            LayoutForViewport(safeArea, dpi);
        }

        // Screen.safeArea is in SCREEN pixels, but a Screen Space - Camera canvas is sized by
        // its CAMERA's pixel rect — and when that camera renders into a RenderTexture the two
        // disagree. Feeding screen pixels into canvas-space anchors then puts the HUD nowhere
        // near where the law says: capture step-2-board.png laid this capsule out against a
        // ~619x489 batchmode screen rect while the canvas was the 917x2048 render target,
        // which dropped the capsule to 75% down the frame at 55% of its width. Read as an edge
        // bug; it is not one — the law is anchored to safeArea.yMax either way. The screen was
        // simply the wrong ruler.
        //
        // So carry the safe area across as FRACTIONS of the screen and apply them to the
        // canvas rect. On device the two rects are identical and this is a no-op; in any
        // render target it puts the capsule where the target art says regardless of whether
        // the capturing rig remembered to inject a viewport.
        private Rect CanvasSafeArea()
        {
            var canvas = _canvasRect != null && _canvasRect.rect.height > 0f
                ? _canvasRect.rect
                : new Rect(0f, 0f, Screen.width, Screen.height);
            float sw = Screen.width, sh = Screen.height;
            if (sw <= 0f || sh <= 0f) return canvas;
            var safe = Screen.safeArea;
            return new Rect(
                canvas.width * (safe.x / sw),
                canvas.height * (safe.y / sh),
                canvas.width * (safe.width / sw),
                canvas.height * (safe.height / sh));
        }

        // The dp floors express a PHYSICAL size, so they have to scale with the same ruler —
        // otherwise a 2048-tall render target gets floors sized for a 489-tall screen.
        private float CanvasDpi()
        {
            float sh = Screen.height;
            if (_canvasRect == null || sh <= 0f || _canvasRect.rect.height <= 0f)
                return Screen.dpi;
            return Screen.dpi * (_canvasRect.rect.height / sh);
        }

        // --- small UGUI helpers ---

        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            var mat = UiChromeMaterial.Shared;
            if (mat != null) image.material = mat;
            image.raycastTarget = false; // ZERO interactive elements
            return image;
        }

        private static TMP_Text AddLabel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "";
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 6f;
            text.fontSizeMax = 40f;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Bottom-left anchored pixel placement — the BannerView convention, so an injected
        // safe-area rect maps straight onto the canvas without a second coordinate system.
        private static void PlacePx(RectTransform rect, Rect px)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(px.x, px.y);
            rect.sizeDelta = new Vector2(px.width, px.height);
        }

        private static void PlaceCentred(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static string TokenGlyph(WaveDto wave)
        {
            string glyph;
            switch (wave.Shape)
            {
                case "square": glyph = "S"; break;
                case "triangle": glyph = "T"; break;
                default: glyph = "O"; break;
            }
            if (wave.Stray) glyph += "!";
            if (wave.Express) glyph += "E";
            return glyph;
        }
    }
}
