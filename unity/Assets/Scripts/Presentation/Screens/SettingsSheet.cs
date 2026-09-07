using System;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Strings;
using CatMetro.Presentation.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Screens
{
    public enum SettingsChannel { Sound, Music, Haptics, ReduceMotion }

    public sealed class SettingsSheet : MonoBehaviour
    {
        private const int BlockerPriority = ChromeRegions.StackedModalPriority + 10;
        private static readonly string[] Keys = { "settings.sound", "settings.music", "settings.haptics", "settings.motion" };
        private readonly bool[] _values = new bool[4];
        private readonly Rect[] _rows = new Rect[4];
        private readonly ChromeChip[] _pills = new ChromeChip[5];
        private ChromeRegions _regions;
        private Rect _safeArea;
        private float _dpi;
        private bool _shown, _registered, _daily, _restoring;
        private RectTransform _card;
        private TMP_Text _title, _close, _restore, _status;

        public Action<SettingsChannel, bool> Changed;
        public Action CloseRequested, ReminderRequested, RestoreRequested;
        public bool IsVisible => _shown && gameObject.activeSelf;
        public bool ReminderVisible => _daily;
        public Rect CardRectPx { get; private set; }
        public Rect CloseRectPx { get; private set; }
        public Rect ReminderRectPx { get; private set; }
        public Rect RestoreRectPx { get; private set; }

        public static SettingsSheet Create(Transform parent)
        {
            var go = new GameObject("SettingsSheet", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var sheet = go.AddComponent<SettingsSheet>();
            MakeImage(go.transform, "SettingsScrim", Palette.WithAlpha(Palette.DepotNavy, .7f), false);
            sheet._card = MakeImage(go.transform, "SettingsCard", Palette.CreamCard, true).rectTransform;
            sheet._title = MakeText(go.transform, "SettingsTitle", UiStrings.Get("settings.title"));
            sheet._close = MakeText(go.transform, "SettingsClose", UiStrings.Get("reminder.settings.close"));
            sheet._restore = MakeText(go.transform, "SettingsRestore", UiStrings.Get("wardrobe.restore"));
            sheet._status = MakeText(go.transform, "SettingsStatus", "");
            for (int i = 0; i < sheet._pills.Length; i++)
            {
                sheet._pills[i] = ChromeChip.PaintPrimary(go.transform,
                    new Rect(0, 0, 360, 56), "", Palette.MetroTeal, null, 160f);
                sheet._pills[i].Root.name = "SettingsRow" + i;
            }
            sheet.Hide();
            return sheet;
        }

        public void Attach(ChromeRegions regions)
        {
            Unregister(); _regions = regions;
            if (IsVisible) Register();
        }
        public void Configure(bool sound, bool music, bool haptics, bool reduceMotion, bool daily)
        {
            _values[0] = sound; _values[1] = music; _values[2] = haptics; _values[3] = reduceMotion;
            _daily = daily;
            if (_pills[0] == null) return;
            for (int i = 0; i < _values.Length; i++)
            {
                _pills[i].SetLabel(UiStrings.Get(Keys[i]) + "   " + UiStrings.Get(
                    _values[i] ? "reminder.settings.on" : "reminder.settings.off"));
                _pills[i].SetRingColour(_values[i] ? Palette.MetroTeal
                    : Color.Lerp(Palette.CreamCard, Palette.InkNavy, .3f));
            }
            _pills[4].SetLabel(UiStrings.Get("reminder.settings.title"));
            _pills[4].SetRingColour(Color.Lerp(Palette.CreamCard, Palette.InkNavy, .3f));
            _pills[4].Root.gameObject.SetActive(_daily);
            if (IsVisible)
            {
                LayoutForViewport(_safeArea, _dpi);
                Unregister(); Register();
            }
        }
        public bool Value(SettingsChannel channel) => _values[(int)channel];
        public Rect RowRectPx(SettingsChannel channel) => _rows[(int)channel];

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _safeArea = safeArea; _dpi = dpi;
            float p = HudBands.PxPerDp(dpi);
            int count = _daily ? 5 : 4;
            float height = (count * 56f + (count - 1) * 12f + 160f) * p;
            // Small landscape windows scale the sheet; portrait keeps 56 dp rows.
            if (height > safeArea.height - 16f * p) p *= Mathf.Max(.1f, (safeArea.height - 16f * p) / height);
            height = (count * 56f + (count - 1) * 12f + 160f) * p;
            float width = Mathf.Min(360f * p, safeArea.width - 24f * p);
            CardRectPx = new Rect(safeArea.center.x - width / 2, safeArea.center.y - height / 2, width, height);
            ApplyPx(_card, CardRectPx);
            ApplyPx(_title.rectTransform, new Rect(CardRectPx.x + 18f * p, CardRectPx.yMax - 66f * p, width - 106f * p, 44f * p));
            _title.alignment = TextAlignmentOptions.MidlineLeft;
            CloseRectPx = new Rect(CardRectPx.xMax - 82f * p, CardRectPx.yMax - 66f * p, 68f * p, 44f * p);
            ApplyPx(_close.rectTransform, CloseRectPx);
            float layoutDpi = p * HudBands.FallbackDpi;
            for (int i = 0; i < count; i++)
            {
                var row = new Rect(CardRectPx.x + 18f * p, CardRectPx.yMax - (140f + i * 68f) * p,
                    width - 36f * p, 56f * p);
                if (i < 4) _rows[i] = row; else ReminderRectPx = row;
                TypeScale.Apply(_pills[i].Label, TypeScale.Body, layoutDpi);
                _pills[i].LayoutFace(row, layoutDpi);
            }
            if (!_daily) ReminderRectPx = default;
            RestoreRectPx = new Rect(CardRectPx.x + 18f * p, CardRectPx.y + 28f * p, width - 36f * p, 40f * p);
            ApplyPx(_restore.rectTransform, RestoreRectPx);
            ApplyPx(_status.rectTransform, new Rect(CardRectPx.x + 12f * p, CardRectPx.y + 4f * p, width - 24f * p, 24f * p));
            TypeScale.Apply(_title, TypeScale.Title, layoutDpi);
            TypeScale.Apply(_close, TypeScale.Caption, layoutDpi);
            TypeScale.Apply(_restore, TypeScale.Caption, layoutDpi, body: true);
            TypeScale.Apply(_status, TypeScale.Minimum, layoutDpi, body: true);
        }

        public void Show()
        {
            _shown = true;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            Register();
        }
        public void Hide() { _shown = false; Unregister(); gameObject.SetActive(false); }
        public void SetStatus(string text, bool? restoring = null)
        {
            if (restoring.HasValue) _restoring = restoring.Value;
            _status.text = text ?? "";
            _restore.text = UiStrings.Get(_restoring ? "wardrobe.restore.running" : "wardrobe.restore");
        }
        private void Restore()
        {
            if (_restoring) return;
            SetStatus(UiStrings.Get("wardrobe.status.restoring"), true);
            RestoreRequested?.Invoke();
        }
        private void Register()
        {
            if (_registered || !_shown || _regions == null || !isActiveAndEnabled) return;
            _regions.Register("settings.blocker", () => new Rect(0, 0, Screen.width, Screen.height),
                () => { }, BlockerPriority, ChromeFeedback.None);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                _regions.Register("settings.row." + i, () => _rows[index],
                    () => Changed?.Invoke((SettingsChannel)index, !_values[index]), BlockerPriority + 1);
            }
            _regions.Register("settings.close", () => CloseRectPx, () => CloseRequested?.Invoke(), BlockerPriority + 1);
            _regions.Register("settings.restore", () => RestoreRectPx, Restore, BlockerPriority + 1);
            if (_daily) _regions.Register("settings.reminder", () => ReminderRectPx,
                () => ReminderRequested?.Invoke(), BlockerPriority + 1);
            _registered = true;
        }
        private void Unregister()
        {
            if (_regions == null) return;
            foreach (string id in new[] { "blocker", "close", "restore", "reminder", "row.0", "row.1", "row.2", "row.3" })
                _regions.Unregister("settings." + id);
            _registered = false;
        }
        private void Update()
        {
            if (_shown && (_safeArea != Screen.safeArea || !Mathf.Approximately(_dpi, Screen.dpi)))
                LayoutForViewport(Screen.safeArea, Screen.dpi);
        }
        private void OnEnable() { if (_shown) Register(); }
        private void OnDisable() => Unregister();
        private void OnDestroy() => Unregister();

        private static TMP_Text MakeText(Transform parent, string name, string value)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var text = go.AddComponent<TextMeshProUGUI>();
            go.transform.SetParent(parent, false);
            text.text = value; text.color = Palette.InkNavy;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false; text.raycastTarget = false;
            return text;
        }
        private static Image MakeImage(Transform parent, string name, Color color, bool rounded)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            if (rounded && UiChromeMaterial.Shared != null) image.material = UiChromeMaterial.Shared;
            Stretch(image.rectTransform); return image;
        }
        private static void ApplyPx(RectTransform rect, Rect px)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = px.center; rect.sizeDelta = px.size;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
