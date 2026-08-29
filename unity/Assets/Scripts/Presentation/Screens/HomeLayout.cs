using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Presentation.Screens
{
    public static class HomeLayout
    {
        public const float PinSideDp = 72f;
        public const float RingMarginDp = 8f;
        public const float DailyPinSideDp = 56f;
        public const float DailyPinGapDp = 16f;
        public const float SideInsetDp = 20f;
        public const float CtaBottomInsetDp = 16f;
        public const float HeaderHeightDp = 84f;
        public const float ContentGapDp = 16f;

        public static Rect PinRect(Rect safeArea, float dpi) =>
            PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked: false);

        public static Rect PrimaryPinRect(Rect safeArea, float dpi, bool dailyEntryUnlocked)
        {
            var cta = CtaRect(safeArea, dpi);
            if (!dailyEntryUnlocked) return cta;

            float pxPerDp = HudBands.PxPerDp(dpi);
            float gap = DailyPinGapDp * pxPerDp;
            float dailySide = DailySide(cta, pxPerDp);
            return new Rect(cta.x, cta.y, cta.width - dailySide - gap, cta.height);
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
            var cta = CtaRect(safeArea, dpi);
            var primary = PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked: true);
            float side = DailySide(cta, pxPerDp);
            float gap = DailyPinGapDp * pxPerDp;
            return new Rect(primary.xMax + gap, cta.center.y - side / 2f, side, side);
        }

        public static Rect HeaderRect(Rect safeArea, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * pxPerDp;
            float height = HeaderHeightDp * pxPerDp;
            return new Rect(safeArea.x + inset, safeArea.yMax - height,
                safeArea.width - inset * 2f, height);
        }

        public static Rect HeroRect(Rect safeArea, float dpi)
        {
            float gap = ContentGapDp * HudBands.PxPerDp(dpi);
            var cta = PinRect(safeArea, dpi);
            var header = HeaderRect(safeArea, dpi);
            float yMin = cta.yMax + gap;
            float yMax = header.yMin - gap;
            return new Rect(cta.x, yMin, cta.width, Mathf.Max(0f, yMax - yMin));
        }

        private static Rect CtaRect(Rect safeArea, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * pxPerDp;
            float height = PinSideDp * pxPerDp;
            float bottom = CtaBottomInsetDp * pxPerDp;
            return new Rect(safeArea.x + inset, safeArea.y + bottom,
                safeArea.width - inset * 2f, height);
        }

        private static float DailySide(Rect cta, float pxPerDp)
        {
            float minPrimary = HudBands.MinTargetDp * pxPerDp;
            float gap = DailyPinGapDp * pxPerDp;
            return Mathf.Min(DailyPinSideDp * pxPerDp,
                Mathf.Max(minPrimary, cta.width - gap - minPrimary));
        }
    }
}
