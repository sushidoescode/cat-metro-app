using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services;

namespace CatMetro.Presentation.Screens
{
    public sealed class DailyReminderSheet : MonoBehaviour
    {
        private const int BlockerPriority = ChromeRegions.StackedModalPriority + 10;
        private const int ControlPriority = BlockerPriority + 1;
        private const string RegionPrefix = "daily.reminder.";

        public Action Accepted;
        public Action Dismissed;
        public Action<bool> EnabledChanged;
        public Action<DailyReminderSlot> SlotChanged;

        private ChromeRegions _regions;
        private readonly List<string> _registered = new List<string>();
        private bool _shown;
        private bool _enabled;
        private bool _canRequestPermission;
        private bool _providerAvailable;
        private MessagingPermission _permission;
        private DailyReminderSlot _selectedSlot = DailyReminderSlot.Morning;
        private DailyReminderLayout.SheetMode _mode;
        private DailyReminderLayout.Rects _layout;

        private RectTransform _card;
        private TMP_Text _title;
        private TMP_Text _body;
        private TMP_Text _status;
        private TMP_Text _on;
        private TMP_Text _off;
        private TMP_Text _morning;
        private TMP_Text _afternoon;
        private TMP_Text _evening;
        private TMP_Text _openSettings;
        private TMP_Text _accept;
        private TMP_Text _dismiss;
        private TMP_Text _close;
        private Image _onPaint;
        private Image _offPaint;
        private Image _morningPaint;
        private Image _afternoonPaint;
        private Image _eveningPaint;

        public bool IsVisible => gameObject.activeSelf;
        public string TitleText => TextOf(_title);
        public string BodyText => TextOf(_body);
        public string StatusText => TextOf(_status);
        public string OnText => TextOf(_on);
        public string OffText => TextOf(_off);
        public string MorningText => TextOf(_morning);
        public string AfternoonText => TextOf(_afternoon);
        public string EveningText => TextOf(_evening);
        public string OpenSettingsText => TextOf(_openSettings);
        public string AcceptText => TextOf(_accept);
        public string DismissText => TextOf(_dismiss);
        public bool OpenSettingsVisible => _openSettings != null
            && _openSettings.transform.parent.gameObject.activeSelf;
        public DailyReminderSlot SelectedSlot => _selectedSlot;
        public Rect AcceptRectPx => _layout.Accept;
        public Rect DismissRectPx => _layout.Dismiss;
        public Rect OnRectPx => _layout.On;
        public Rect OffRectPx => _layout.Off;
        public Rect MorningRectPx => _layout.Morning;
        public Rect AfternoonRectPx => _layout.Afternoon;
        public Rect EveningRectPx => _layout.Evening;
        public Rect CloseRectPx => _layout.Close;
        public Rect CardRectPx => _layout.Card;
        public RectTransform CardTransform => _card;

        public static DailyReminderSheet Create(Transform parent)
        {
            var go = new GameObject("DailyReminderSheet");
            go.transform.SetParent(parent, false);
            var sheet = go.AddComponent<DailyReminderSheet>();
            Stretch(go.AddComponent<RectTransform>());

            MakeImage(go.transform, "ReminderScrim", Palette.WithAlpha(Palette.DepotNavy, 0.56f));
            var shadow = MakeImage(go.transform, "ReminderCardShadow",
                Palette.WithAlpha(Palette.DepotNavy, 0.34f), true);
            sheet._card = MakeImage(go.transform, "ReminderCard", Palette.WarmPaper, true).rectTransform;

            sheet._title = MakeText(sheet._card, "ReminderTitle", 31f, FontStyles.Bold,
                Palette.InkNavy);
            sheet._body = MakeText(sheet._card, "ReminderBody", 22f, FontStyles.Normal,
                Palette.InkNavy);
            sheet._status = MakeText(sheet._card, "ReminderStatus", 18f, FontStyles.Italic,
                Palette.InkNavy);

            sheet._off = MakeChip(sheet._card, "ReminderOff", "reminder.settings.off",
                out sheet._offPaint);
            sheet._on = MakeChip(sheet._card, "ReminderOn", "reminder.settings.on",
                out sheet._onPaint);
            sheet._morning = MakeChip(sheet._card, "ReminderMorning", "reminder.slot.morning",
                out sheet._morningPaint);
            sheet._afternoon = MakeChip(sheet._card, "ReminderAfternoon", "reminder.slot.afternoon",
                out sheet._afternoonPaint);
            sheet._evening = MakeChip(sheet._card, "ReminderEvening", "reminder.slot.evening",
                out sheet._eveningPaint);
            sheet._openSettings = MakeChip(sheet._card, "ReminderOpenSettings",
                "reminder.settings.open", out _);
            sheet._accept = MakeChip(sheet._card, "ReminderAccept", "reminder.action.accept", out _);
            sheet._dismiss = MakeChip(sheet._card, "ReminderDismiss", "reminder.action.dismiss", out _);
            sheet._close = MakeChip(sheet._card, "ReminderClose", "reminder.settings.close", out _);

            go.SetActive(false);
            return sheet;
        }

        public void Attach(ChromeRegions regions)
        {
            UnregisterRegions();
            _regions = regions;
            if (_shown && isActiveAndEnabled) RegisterRegions();
        }

        public void Configure(bool enabled, DailyReminderSlot slot,
            MessagingPermission permission, bool canRequestPermission, bool providerAvailable)
        {
            _enabled = enabled;
            _selectedSlot = slot ?? DailyReminderSlot.Morning;
            _permission = permission;
            _canRequestPermission = canRequestPermission;
            _providerAvailable = providerAvailable;
            RefreshPaint();
            if (_shown)
            {
                ApplyModeVisibility();
                LayoutForViewport(Screen.safeArea, Screen.dpi);
                RegisterRegions();
            }
        }

        public void ShowPrompt()
        {
            _mode = DailyReminderLayout.SheetMode.Prompt;
            _title.text = Strings.UiStrings.Get("reminder.prompt.title");
            _body.text = Strings.UiStrings.Get("reminder.prompt.body");
            _shown = true;
            gameObject.SetActive(true);
            ApplyModeVisibility();
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            RefreshPaint();
            RegisterRegions();
        }

        public void ShowSettings()
        {
            _mode = DailyReminderLayout.SheetMode.Settings;
            _title.text = Strings.UiStrings.Get("reminder.settings.title");
            _status.text = Strings.UiStrings.Get(StatusKey());
            _shown = true;
            gameObject.SetActive(true);
            ApplyModeVisibility();
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            RefreshPaint();
            RegisterRegions();
        }

        public void Hide()
        {
            _shown = false;
            UnregisterRegions();
            gameObject.SetActive(false);
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            bool fallback = ShowSettingsFallback();
            _layout = DailyReminderLayout.Calculate(safeArea, dpi, _mode, fallback);
            ApplyPx(_card, _layout.Card);
            var shadow = transform.Find("ReminderCardShadow") as RectTransform;
            float shadowOffset = 5f * HudBands.PxPerDp(dpi);
            ApplyPx(shadow, new Rect(_layout.Card.x + shadowOffset,
                _layout.Card.y - shadowOffset, _layout.Card.width, _layout.Card.height));
            ApplyCardLocal(_title.rectTransform, _layout.Title);
            ApplyCardLocal(_body.rectTransform, _layout.Body);
            ApplyCardLocal(_status.rectTransform, _layout.Status);
            ApplyParentCardLocal(_on, _layout.On);
            ApplyParentCardLocal(_off, _layout.Off);
            ApplyParentCardLocal(_morning, _layout.Morning);
            ApplyParentCardLocal(_afternoon, _layout.Afternoon);
            ApplyParentCardLocal(_evening, _layout.Evening);
            ApplyParentCardLocal(_openSettings, _layout.OpenSettings);
            ApplyParentCardLocal(_accept, _layout.Accept);
            ApplyParentCardLocal(_dismiss, _layout.Dismiss);
            ApplyParentCardLocal(_close, _layout.Close);
        }

        private void ApplyModeVisibility()
        {
            bool prompt = _mode == DailyReminderLayout.SheetMode.Prompt;
            _body.gameObject.SetActive(prompt);
            _accept.transform.parent.gameObject.SetActive(prompt);
            _dismiss.transform.parent.gameObject.SetActive(prompt);
            _on.transform.parent.gameObject.SetActive(!prompt);
            _off.transform.parent.gameObject.SetActive(!prompt);
            _status.gameObject.SetActive(!prompt);
            _close.transform.parent.gameObject.SetActive(!prompt);
            _openSettings.transform.parent.gameObject.SetActive(!prompt && ShowSettingsFallback());
            _morning.transform.parent.gameObject.SetActive(true);
            _afternoon.transform.parent.gameObject.SetActive(true);
            _evening.transform.parent.gameObject.SetActive(true);
        }

        private void RefreshPaint()
        {
            PaintChoice(_onPaint, _enabled);
            PaintChoice(_offPaint, !_enabled);
            PaintChoice(_morningPaint, _selectedSlot.Equals(DailyReminderSlot.Morning));
            PaintChoice(_afternoonPaint, _selectedSlot.Equals(DailyReminderSlot.Afternoon));
            PaintChoice(_eveningPaint, _selectedSlot.Equals(DailyReminderSlot.Evening));
            if (_status != null) _status.text = Strings.UiStrings.Get(StatusKey());
        }

        private void RegisterRegions()
        {
            UnregisterRegions();
            if (!_shown || !isActiveAndEnabled || _regions == null) return;
            Register("blocker", () => _layout.Blocker, () => { }, BlockerPriority,
                ChromeFeedback.None);
            Register("morning", () => _layout.Morning,
                () => SelectSlot(DailyReminderSlot.Morning), ControlPriority);
            Register("afternoon", () => _layout.Afternoon,
                () => SelectSlot(DailyReminderSlot.Afternoon), ControlPriority);
            Register("evening", () => _layout.Evening,
                () => SelectSlot(DailyReminderSlot.Evening), ControlPriority);
            if (_mode == DailyReminderLayout.SheetMode.Prompt)
            {
                Register("accept", () => _layout.Accept, Accept, ControlPriority);
                Register("dismiss", () => _layout.Dismiss, Dismiss, ControlPriority);
            }
            else
            {
                Register("on", () => _layout.On, () => EnabledChanged?.Invoke(true), ControlPriority);
                Register("off", () => _layout.Off, () => EnabledChanged?.Invoke(false), ControlPriority);
                if (ShowSettingsFallback())
                    Register("open-settings", () => _layout.OpenSettings,
                        () => EnabledChanged?.Invoke(true), ControlPriority);
                Register("close", () => _layout.Close, Hide, ControlPriority);
            }
        }

        private void Register(string suffix, Func<Rect> rect, Action action, int priority,
            ChromeFeedback feedback = ChromeFeedback.WoodTap)
        {
            string id = RegionPrefix + suffix;
            _regions.Register(id, rect, action, priority, feedback);
            _registered.Add(id);
        }

        private void UnregisterRegions()
        {
            if (_regions != null)
                for (int i = 0; i < _registered.Count; i++) _regions.Unregister(_registered[i]);
            _registered.Clear();
        }

        private void SelectSlot(DailyReminderSlot slot)
        {
            _selectedSlot = slot;
            RefreshPaint();
            SlotChanged?.Invoke(slot);
        }

        private void Accept()
        {
            Hide();
            Accepted?.Invoke();
        }

        private void Dismiss()
        {
            Hide();
            Dismissed?.Invoke();
        }

        private bool ShowSettingsFallback() => _mode == DailyReminderLayout.SheetMode.Settings
            && _providerAvailable && !_canRequestPermission
            && _permission != MessagingPermission.Authorized;

        private string StatusKey()
        {
            if (!_providerAvailable) return "reminder.status.unavailable";
            if (_permission == MessagingPermission.Authorized) return "reminder.status.authorized";
            if (_permission == MessagingPermission.Denied) return "reminder.status.denied";
            return "reminder.status.unknown";
        }

        private void OnDisable() => UnregisterRegions();

        private void OnEnable()
        {
            if (_shown) RegisterRegions();
        }

        private void OnDestroy() => UnregisterRegions();

        private static void PaintChoice(Image image, bool selected)
        {
            if (image != null)
                image.color = selected ? Palette.MetroTeal : Palette.CreamCard;
        }

        private static string TextOf(TMP_Text text) => text != null ? text.text : "";

        private static Image MakeImage(Transform parent, string name, Color color,
            bool rounded = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            Stretch(rect);
            var image = go.AddComponent<Image>();
            if (rounded)
            {
                var material = UiChromeMaterial.Shared;
                if (material != null) image.material = material;
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text MakeText(Transform parent, string name, float size,
            FontStyles style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(15f, size);
            text.fontSizeMax = size;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_Text MakeChip(Transform parent, string name, string key,
            out Image paint)
        {
            paint = MakeImage(parent, name, Palette.CreamCard, true);
            var text = MakeText(paint.transform, name + "Label", 18f, FontStyles.Bold,
                Palette.InkNavy);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(5f, 4f);
            text.rectTransform.offsetMax = new Vector2(-5f, -4f);
            text.text = Strings.UiStrings.Get(key);
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = 18f;
            return text;
        }

        private void ApplyParentCardLocal(TMP_Text text, Rect rect)
        {
            ApplyCardLocal(text.transform.parent as RectTransform, rect);
        }

        private void ApplyCardLocal(RectTransform rect, Rect screenRect)
        {
            ApplyPx(rect, new Rect(screenRect.x - _layout.Card.x,
                screenRect.y - _layout.Card.y, screenRect.width, screenRect.height));
        }

        private static void ApplyPx(RectTransform rect, Rect px)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = px.center;
            rect.sizeDelta = px.size;
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
