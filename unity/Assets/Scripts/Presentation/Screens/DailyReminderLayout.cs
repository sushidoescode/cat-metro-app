using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Presentation.Screens
{
    public static class DailyReminderLayout
    {
        public enum SheetMode
        {
            Prompt,
            Settings
        }

        public struct Rects
        {
            public Rect Blocker;
            public Rect Card;
            public Rect Title;
            public Rect Body;
            public Rect Status;
            public Rect On;
            public Rect Off;
            public Rect Morning;
            public Rect Afternoon;
            public Rect Evening;
            public Rect OpenSettings;
            public Rect Accept;
            public Rect Dismiss;
            public Rect Close;
        }

        private const float CardWidthDp = 360f;
        private const float PromptHeightDp = 370f;
        private const float SettingsHeightDp = 430f;
        private const float SettingsCompactHeightDp = 370f;
        private const float SafeInsetDp = 16f;
        private const float CardInsetDp = 16f;
        private const float GapDp = 8f;
        private const float TargetHeightDp = 52f;
        private const float SlotHeightDp = 60f;
        private const float GearSideDp = 52f;
        private const float GearInsetDp = 16f;

        public static Rects Calculate(Rect safeArea, float dpi, SheetMode mode,
            bool showSettingsFallback)
        {
            float px = HudBands.PxPerDp(dpi);
            float safeInset = SafeInsetDp * px;
            float desiredHeightDp = mode == SheetMode.Prompt
                ? PromptHeightDp
                : (showSettingsFallback ? SettingsHeightDp : SettingsCompactHeightDp);
            float width = Mathf.Min(CardWidthDp * px,
                Mathf.Max(0f, safeArea.width - safeInset * 2f));
            float height = Mathf.Min(desiredHeightDp * px,
                Mathf.Max(0f, safeArea.height - safeInset * 2f));
            var card = new Rect(safeArea.center.x - width * 0.5f,
                safeArea.center.y - height * 0.5f, width, height);

            var result = new Rects
            {
                Blocker = safeArea,
                Card = card,
            };
            if (mode == SheetMode.Prompt)
                LayoutPrompt(ref result, px);
            else
                LayoutSettings(ref result, px, showSettingsFallback);
            return result;
        }

        public static Rect GearRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float side = GearSideDp * px;
            float inset = GearInsetDp * px;
            return new Rect(safeArea.xMax - inset - side,
                safeArea.yMax - inset - side, side, side);
        }

        private static void LayoutPrompt(ref Rects result, float px)
        {
            float inset = CardInsetDp * px;
            float gap = GapDp * px;
            float target = TargetHeightDp * px;
            float slotHeight = SlotHeightDp * px;
            float innerWidth = result.Card.width - inset * 2f;
            float half = (innerWidth - gap) * 0.5f;
            float third = (innerWidth - gap * 2f) / 3f;
            float y = result.Card.y + inset;

            result.Dismiss = new Rect(result.Card.x + inset, y, half, target);
            result.Accept = new Rect(result.Dismiss.xMax + gap, y, half, target);
            y += target + gap;
            result.Morning = new Rect(result.Card.x + inset, y, third, slotHeight);
            result.Afternoon = new Rect(result.Morning.xMax + gap, y, third, slotHeight);
            result.Evening = new Rect(result.Afternoon.xMax + gap, y, third, slotHeight);
            y += slotHeight + gap;
            result.Body = new Rect(result.Card.x + inset, y, innerWidth, 88f * px);
            y += 88f * px + gap;
            result.Title = new Rect(result.Card.x + inset, y, innerWidth,
                Mathf.Max(0f, result.Card.yMax - inset - y));
        }

        private static void LayoutSettings(ref Rects result, float px, bool showFallback)
        {
            float inset = CardInsetDp * px;
            float gap = GapDp * px;
            float target = TargetHeightDp * px;
            float slotHeight = SlotHeightDp * px;
            float innerWidth = result.Card.width - inset * 2f;
            float half = (innerWidth - gap) * 0.5f;
            float third = (innerWidth - gap * 2f) / 3f;
            float y = result.Card.y + inset;

            result.Close = new Rect(result.Card.x + inset, y, innerWidth, target);
            y += target + gap;
            if (showFallback)
            {
                result.OpenSettings = new Rect(result.Card.x + inset, y, innerWidth, target);
                y += target + gap;
            }
            result.Status = new Rect(result.Card.x + inset, y, innerWidth, 44f * px);
            y += 44f * px + gap;
            result.Morning = new Rect(result.Card.x + inset, y, third, slotHeight);
            result.Afternoon = new Rect(result.Morning.xMax + gap, y, third, slotHeight);
            result.Evening = new Rect(result.Afternoon.xMax + gap, y, third, slotHeight);
            y += slotHeight + gap;
            result.Off = new Rect(result.Card.x + inset, y, half, target);
            result.On = new Rect(result.Off.xMax + gap, y, half, target);
            y += target + gap;
            result.Title = new Rect(result.Card.x + inset, y, innerWidth,
                Mathf.Max(0f, result.Card.yMax - inset - y));
        }
    }
}
