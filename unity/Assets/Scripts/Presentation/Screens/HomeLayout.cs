using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Presentation.Screens
{
    public static class HomeLayout
    {
        public const float PinSideDp = 60f;
        public const float RingMarginDp = 8f;
        public const float DailyPinSideDp = 60f;
        public const float DailyPinGapDp = 8f;
        public const float SideInsetDp = 20f;
        public const float CtaBottomInsetDp = 16f;
        public const float HeaderHeightDp = 84f;
        public const float ContentGapDp = 16f;
        public const float AudioToggleWidthDp = 72f;
        public const float AudioToggleHeightDp = 52f;
        public const float AudioToggleInsetDp = 16f;
        public const float HeaderControlGapDp = 8f;
        public const float TitleShadowXDp = 3f;
        public const float TitleShadowYDp = 5f;

        public static Rect PinRect(Rect safeArea, float dpi) =>
            PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked: false);

        public static Rect PrimaryPinRect(Rect safeArea, float dpi, bool dailyEntryUnlocked)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float gap = DailyPinGapDp * pxPerDp;
            var below = dailyEntryUnlocked
                ? DailyPinRect(safeArea, dpi)
                : WardrobePinRect(safeArea, dpi);
            return new Rect(below.x, below.yMax + gap, below.width,
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
            var wardrobe = WardrobePinRect(safeArea, dpi);
            return new Rect(wardrobe.x, wardrobe.yMax + gap, wardrobe.width,
                DailyPinSideDp * pxPerDp);
        }

        public static Rect WardrobePinRect(Rect safeArea, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * pxPerDp;
            float bottom = CtaBottomInsetDp * pxPerDp;
            return new Rect(safeArea.x + inset, safeArea.y + bottom,
                safeArea.width - inset * 2f, PinSideDp * pxPerDp);
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
            return new Rect(safeArea.x + inset,
                safeArea.yMax - inset - height, width, height);
        }

        public static Rect TitleRect(Rect safeArea, float dpi,
            bool audioToggleVisible, bool reminderGearVisible)
        {
            var header = HeaderRect(safeArea, dpi);
            float pxPerDp = HudBands.PxPerDp(dpi);
            float gap = HeaderControlGapDp * pxPerDp;
            float xMin = header.xMin;
            float xMax = header.xMax;

            if (audioToggleVisible)
                xMin = Mathf.Max(xMin, AudioToggleRect(safeArea, dpi).xMax + gap);
            if (reminderGearVisible)
            {
                // The carved plaque keeps its 3dp right/down toy shadow. Reserve that painted
                // lip as well as the face so the visible sign still clears the gear by 8dp.
                float shadowDx = TitleShadowXDp * pxPerDp;
                xMax = Mathf.Min(xMax,
                    DailyReminderLayout.GearRect(safeArea, dpi).xMin - gap - shadowDx);
            }

            if (xMax < xMin)
            {
                float center = Mathf.Clamp((xMin + xMax) * 0.5f,
                    header.xMin, header.xMax);
                xMin = center;
                xMax = center;
            }

            return new Rect(xMin, header.y, xMax - xMin, header.height);
        }

        public static Rect TitleShadowRect(Rect titlePlaque, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            return new Rect(titlePlaque.x + TitleShadowXDp * pxPerDp,
                titlePlaque.y - TitleShadowYDp * pxPerDp,
                titlePlaque.width, titlePlaque.height);
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
