using CatMetro.Presentation.Hud;
using UnityEngine;

namespace CatMetro.Presentation.Screens
{
    public static class WardrobeLayout
    {
        private const float SideInsetDp = 20f;
        private const float GapDp = 12f;
        private const float EntryWidthDp = 156f;
        private const float EntryHeightDp = 52f;
        private const float BackSideDp = 52f;
        private const float BuyHeightDp = 72f;
        private const float RestoreHeightDp = 52f;
        private const float StatusHeightDp = 58f;
        private const float PreviewHeightDp = 172f;
        private const float PreviewHeadingHeightDp = 24f;
        private const float PreviewCardGapDp = 8f;

        public static Rect EntryRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            float width = Mathf.Min(EntryWidthDp * px, Mathf.Max(0f, safeArea.width - inset * 2f));
            float height = EntryHeightDp * px;
            var hero = HomeLayout.HeroRect(safeArea, dpi);
            return new Rect(hero.xMax - width - GapDp * px,
                hero.yMax - height - GapDp * px, width, height);
        }

        public static Rect BackRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float side = BackSideDp * px;
            float inset = SideInsetDp * px;
            return new Rect(safeArea.x + inset, safeArea.yMax - inset - side, side, side);
        }

        public static Rect TitleRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var back = BackRect(safeArea, dpi);
            return new Rect(back.xMax + GapDp * px, back.y,
                Mathf.Max(0f, safeArea.xMax - inset - back.xMax - GapDp * px), back.height);
        }

        public static Rect BuyRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            return new Rect(safeArea.x + inset, safeArea.y + inset,
                Mathf.Max(0f, safeArea.width - inset * 2f), BuyHeightDp * px);
        }

        public static Rect RestoreRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            var buy = BuyRect(safeArea, dpi);
            return new Rect(buy.x, buy.yMax + GapDp * px, buy.width, RestoreHeightDp * px);
        }

        public static Rect StatusRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            var restore = RestoreRect(safeArea, dpi);
            return new Rect(restore.x, restore.yMax + GapDp * px,
                restore.width, StatusHeightDp * px);
        }

        public static Rect PortraitRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var preview = PreviewStripRect(safeArea, dpi);
            var title = TitleRect(safeArea, dpi);
            float y = preview.yMax + GapDp * px;
            float yMax = title.yMin - GapDp * px;
            return new Rect(safeArea.x + inset, y,
                Mathf.Max(0f, safeArea.width - inset * 2f), Mathf.Max(0f, yMax - y));
        }

        public static Rect PreviewStripRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var status = StatusRect(safeArea, dpi);
            return new Rect(safeArea.x + inset, status.yMax + GapDp * px,
                Mathf.Max(0f, safeArea.width - inset * 2f), PreviewHeightDp * px);
        }

        public static Rect PreviewHeadingRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            var strip = PreviewStripRect(safeArea, dpi);
            float height = PreviewHeadingHeightDp * px;
            return new Rect(strip.x, strip.yMax - height, strip.width, height);
        }

        public static Rect PreviewCardRect(Rect safeArea, float dpi, int index)
        {
            float px = HudBands.PxPerDp(dpi);
            var strip = PreviewStripRect(safeArea, dpi);
            float gap = PreviewCardGapDp * px;
            float headingAndGap = (PreviewHeadingHeightDp + PreviewCardGapDp) * px;
            float width = Mathf.Max(0f, (strip.width - gap * 3f) / 4f);
            int boundedIndex = Mathf.Clamp(index, 0, 3);
            return new Rect(strip.x + boundedIndex * (width + gap), strip.y,
                width, Mathf.Max(0f, strip.height - headingAndGap));
        }
    }
}
