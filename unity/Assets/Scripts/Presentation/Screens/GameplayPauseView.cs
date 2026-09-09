using System;
using CatMetro.Presentation.Fx;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Screens
{
    // GameRoot owns the stack, session and Home navigation; this view owns paint and taps.
    public sealed class GameplayPauseView : MonoBehaviour
    {
        private ChromeRegions _regions;
        private Func<bool> _canOpen;
        private Action _open, _resume, _home;
        private BoardFx _fx;
        private ChromeChip _pin, _resumeChip, _homeChip;
        private RectTransform _sheet, _ticket;
        private Image _shade, _paper;
        private TMP_Text _title, _body;
        private Rect _entryPx, _resumePx, _homePx, _safe;
        private float _dpi = -1f;
        private bool _shown, _closing, _entryRegistered, _sheetRegistered;
        public bool IsVisible => _shown;
        public Rect EntryRectPx => _entryPx;

        public static GameplayPauseView Create(Transform owner, Camera camera,
            ChromeRegions regions, Func<bool> canOpen, Func<bool> motionOff,
            Action open, Action resume, Action home)
        {
            var go = new GameObject("PauseCanvas", typeof(Canvas));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 125;
            var view = go.AddComponent<GameplayPauseView>();
            view._regions = regions;
            view._canOpen = canOpen;
            view._open = open;
            view._resume = resume;
            view._home = home;
            view._fx = BoardFx.GetOrCreate(go.transform, motionOff);
            view._pin = ChromeChip.PaintPrimary(go.transform, default, "",
                Palette.InkNavy, HudShapeSprites.Pause);
            view._pin.Root.name = "PausePin";
            view._sheet = MakeRect(go.transform, "PauseSheet");
            Stretch(view._sheet);
            view._shade = MakeImage(view._sheet, "Shade", null, Palette.WithAlpha(Palette.DepotNavy, .48f));
            Stretch(view._shade.rectTransform);
            view._ticket = MakeRect(view._sheet, "PauseTicket");
            view._paper = MakeImage(view._ticket, "Paper", HudShapeSprites.RoundedSquare, Palette.CreamCard);
            Stretch(view._paper.rectTransform);
            view._title = MakeText(view._ticket, "Title", Strings.UiStrings.Get("pause.title"), .73f, .94f);
            view._body = MakeText(view._ticket, "Body", Strings.UiStrings.Get("pause.body"), .58f, .74f);
            view._resumeChip = ChromeChip.PaintPrimary(view._ticket, default,
                Strings.UiStrings.Get("pause.resume"), Palette.TicketOrange);
            view._homeChip = ChromeChip.PaintPrimary(view._ticket, default,
                Strings.UiStrings.Get("pause.home"), Palette.InkNavy);
            view.LayoutForViewport(Screen.safeArea, Screen.dpi);
            view._sheet.gameObject.SetActive(false);
            view.RefreshEntry();
            return view;
        }

        public static Rect EntryRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi), side = 48f * px;
            var capsule = WavePreviewStrip.CapsuleRect(safeArea, dpi);
            return new Rect(safeArea.xMax - 16f * px - side,
                capsule.center.y - side * .5f, side, side);
        }

        public void Show()
        {
            if (_shown) return;
            _shown = true;
            _closing = false;
            _sheet.gameObject.SetActive(true);
            RefreshEntry();
            RegisterSheet();
            _fx.Tween(_ticket, .18f, p =>
            {
                if (!_shown) return;
                float eased = Mathf.SmoothStep(0f, 1f, p);
                _ticket.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, eased);
                _shade.color = Palette.WithAlpha(Palette.DepotNavy, .48f * eased);
            });
        }

        private void Resume()
        {
            if (!_shown || _closing) return;
            _fx.Finish(_ticket);
            _closing = true;
            _fx.Tween(_ticket, .16f, p =>
            {
                if (!_shown) return;
                float eased = Mathf.SmoothStep(0f, 1f, p);
                _ticket.localScale = Vector3.one * Mathf.Lerp(1f, .96f, eased);
                _shade.color = Palette.WithAlpha(Palette.DepotNavy, .48f * (1f - eased));
                if (p >= 1f) { Hide(); _resume?.Invoke(); }
            });
        }

        public void Hide()
        {
            _shown = _closing = false;
            _fx?.Finish(_ticket);
            UnregisterSheet();
            if (_sheet != null) _sheet.gameObject.SetActive(false);
            RefreshEntry();
        }

        private void Update()
        {
            if (_safe != Screen.safeArea || _dpi != Screen.dpi)
                LayoutForViewport(Screen.safeArea, Screen.dpi);
            RefreshEntry();
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _safe = safeArea; _dpi = dpi;
            float px = HudBands.PxPerDp(dpi);
            _entryPx = EntryRect(safeArea, dpi);
            _pin.LayoutFace(_entryPx, dpi);
            float width = Mathf.Min(320f * px, safeArea.width - 40f * px);
            float height = Mathf.Min(286f * px, safeArea.height * .64f);
            var ticket = new Rect(safeArea.center - new Vector2(width, height) * .5f,
                new Vector2(width, height));
            _ticket.anchorMin = _ticket.anchorMax = Vector2.zero;
            _ticket.pivot = new Vector2(.5f, .5f);
            _ticket.anchoredPosition = ticket.center;
            _ticket.sizeDelta = ticket.size;
            _paper.pixelsPerUnitMultiplier = .9f / px;
            TypeScale.Apply(_title, TypeScale.Title, dpi);
            TypeScale.Apply(_body, 16f, dpi, body: true);
            float chipHeight = Mathf.Min(60f * px, height * .23f);
            var resume = new Rect(20f * px, height * .32f, width - 40f * px, chipHeight);
            var home = new Rect(20f * px, height * .06f, width - 40f * px, chipHeight);
            _resumeChip.LayoutFace(resume, dpi);
            _homeChip.LayoutFace(home, dpi);
            _resumePx = new Rect(ticket.position + resume.position, resume.size);
            _homePx = new Rect(ticket.position + home.position, home.size);
        }

        private void RefreshEntry()
        {
            if (_pin == null) return;
            bool visible = isActiveAndEnabled && !_shown && _canOpen != null && _canOpen();
            _pin.Root.gameObject.SetActive(visible);
            if (visible && !_entryRegistered)
            {
                _regions.Register("game.pause", () => _entryPx,
                    () => { if (_canOpen()) _open?.Invoke(); }, ChromeRegions.ParentPriority);
                _entryRegistered = true;
            }
            else if (!visible) UnregisterEntry();
        }

        private void RegisterSheet()
        {
            if (_sheetRegistered || !_shown || !isActiveAndEnabled) return;
            _regions.Register("pause.resume", () => _resumePx, Resume, ChromeRegions.StackedModalPriority);
            _regions.Register("pause.home", () => _homePx,
                () => { if (!_closing) _home?.Invoke(); }, ChromeRegions.StackedModalPriority);
            // Same-priority controls register before their shade, so only empty space is
            // absorbed by the blocker. Parent/results regions remain strictly below.
            _regions.Register("pause.blocker", () => new Rect(0, 0, Screen.width, Screen.height),
                () => { }, ChromeRegions.StackedModalPriority, ChromeFeedback.None);
            _sheetRegistered = true;
        }

        private void UnregisterEntry()
        {
            if (!_entryRegistered) return;
            _regions.Unregister("game.pause");
            _entryRegistered = false;
        }

        private void UnregisterSheet()
        {
            if (!_sheetRegistered) return;
            _regions.Unregister("pause.resume");
            _regions.Unregister("pause.home");
            _regions.Unregister("pause.blocker");
            _sheetRegistered = false;
        }

        private void OnDisable() { UnregisterEntry(); UnregisterSheet(); }
        private void OnEnable() { RefreshEntry(); RegisterSheet(); }
        private void OnDestroy() { UnregisterEntry(); UnregisterSheet(); }

        private static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var rect = MakeRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced;
            image.material = UiChromeMaterial.Shared; image.color = color; image.raycastTarget = false;
            return image;
        }
        private static TMP_Text MakeText(Transform parent, string name, string value, float low, float high)
        {
            var rect = MakeRect(parent, name);
            rect.anchorMin = new Vector2(.07f, low); rect.anchorMax = new Vector2(.93f, high);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.alignment = TextAlignmentOptions.Center;
            text.color = Palette.InkNavy; text.raycastTarget = false;
            return text;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
