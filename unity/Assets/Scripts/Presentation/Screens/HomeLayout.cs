using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Presentation.Screens
{
    public static class HomeLayout
    {
        public const float PinSideDp = 64f;
        public const float RingMarginDp = 8f;
        public const float DailyPinSideDp = 52f;
        public const float DailyPinGapDp = 8f;
        public const float SideInsetDp = 20f;
        public const float CtaBottomInsetDp = 12f;
        public const float BottomBreathDp = 24f;
        public const float HeaderHeightDp = 130f;
        public const float ContentGapDp = 16f;
        public const float AudioToggleWidthDp = 44f;
        public const float AudioToggleHeightDp = 44f;
        public const float AudioToggleInsetDp = 16f;
        public const float HeaderControlGapDp = 8f;
        public const float TitleShadowXDp = 3f;
        public const float TitleShadowYDp = 5f;
        public const float ShadowMarginDp = 8f;

        public static Rect PinRect(Rect safeArea, float dpi) =>
            PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked: false);

        public static Rect PrimaryPinRect(Rect safeArea, float dpi, bool dailyEntryUnlocked)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float gap = DailyPinGapDp * pxPerDp;
            var below = DailyPinRect(safeArea, dpi);
            return new Rect(below.x, below.yMax + gap,
                safeArea.width - 2f * SideInsetDp * pxPerDp,
                PinSideDp * pxPerDp);
        }

        public static Rect RingRect(Rect safeArea, float dpi)
            => RingRect(safeArea, dpi, dailyEntryUnlocked: false);

        public static Rect RingRect(Rect safeArea, float dpi, bool dailyEntryUnlocked)
        {
            float margin = RingMarginDp * HudBands.PxPerDp(dpi);
            var pin = PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked);
            return new Rect(pin.x - margin, pin.y - margin,
                pin.width + margin * 2f, pin.height + margin * 2f);
        }

        public static Rect DailyPinRect(Rect safeArea, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float gap = DailyPinGapDp * pxPerDp;
            float inset = SideInsetDp * pxPerDp;
            float bottom = (CtaBottomInsetDp + BottomBreathDp) * pxPerDp;
            return new Rect(safeArea.x + inset, safeArea.y + bottom,
                (safeArea.width - inset * 2f - gap) * 0.5f,
                DailyPinSideDp * pxPerDp);
        }

        public static Rect WardrobePinRect(Rect safeArea, float dpi)
        {
            var daily = DailyPinRect(safeArea, dpi);
            return new Rect(daily.xMax + DailyPinGapDp * HudBands.PxPerDp(dpi),
                daily.y, daily.width, daily.height);
        }

        public static Rect HeaderRect(Rect safeArea, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * pxPerDp;
            float height = HeaderHeightDp * pxPerDp;
            return new Rect(safeArea.x + inset, safeArea.yMax - height,
                safeArea.width - inset * 2f, height);
        }

        public static Rect AudioToggleRect(Rect safeArea, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float inset = AudioToggleInsetDp * pxPerDp;
            float width = Mathf.Min(AudioToggleWidthDp * pxPerDp,
                Mathf.Max(0f, safeArea.width - inset * 2f));
            float height = Mathf.Min(AudioToggleHeightDp * pxPerDp,
                Mathf.Max(0f, safeArea.height - inset * 2f));
            return new Rect(safeArea.xMax - inset - width,
                safeArea.yMax - inset - height, width, height);
        }

        public static Rect AudioToggleHitRect(Rect safeArea, float dpi)
        {
            var paint = AudioToggleRect(safeArea, dpi);
            float extra = 2f * HudBands.PxPerDp(dpi);
            return new Rect(paint.x - extra, paint.y - extra,
                paint.width + extra * 2f, paint.height + extra * 2f);
        }

        public static Rect ReminderGearRect(Rect safeArea, float dpi)
        {
            var right = DailyReminderLayout.GearRect(safeArea, dpi);
            return new Rect(safeArea.xMin + safeArea.xMax - right.xMax,
                right.y, right.width, right.height);
        }

        public static Rect TitleRect(Rect safeArea, float dpi,
            bool audioToggleVisible, bool reminderGearVisible)
        {
            var header = HeaderRect(safeArea, dpi);
            float pxPerDp = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * pxPerDp;
            float controlWidth = Mathf.Max(
                audioToggleVisible ? AudioToggleRect(safeArea, dpi).width : 0f,
                reminderGearVisible ? ReminderGearRect(safeArea, dpi).width : 0f);
            if (controlWidth > 0f)
                inset = Mathf.Max(inset, controlWidth
                    + (AudioToggleInsetDp + HeaderControlGapDp + TitleShadowXDp + ShadowMarginDp) * pxPerDp);
            float width = Mathf.Max(0f, safeArea.width - inset * 2f);
            return new Rect(safeArea.center.x - width * 0.5f, header.y, width, header.height);
        }

        public static Rect TitleShadowRect(Rect titlePlaque, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float margin = ShadowMarginDp * pxPerDp;
            return new Rect(titlePlaque.x + TitleShadowXDp * pxPerDp - margin,
                titlePlaque.y - TitleShadowYDp * pxPerDp - margin,
                titlePlaque.width + margin * 2f, titlePlaque.height + margin * 2f);
        }

        public static Rect HeroRect(Rect safeArea, float dpi)
            => HeroRect(safeArea, dpi, dailyEntryUnlocked: false);

        public static Rect HeroRect(Rect safeArea, float dpi, bool dailyEntryUnlocked)
        {
            float gap = ContentGapDp * HudBands.PxPerDp(dpi);
            var cta = PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked);
            var header = HeaderRect(safeArea, dpi);
            float yMin = cta.yMax + gap;
            float yMax = header.yMin - gap;
            return new Rect(cta.x, yMin, cta.width, Mathf.Max(0f, yMax - yMin));
        }
    }
}
