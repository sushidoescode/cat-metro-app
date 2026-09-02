using CatMetro.Presentation.Hud;
using UnityEngine;

namespace CatMetro.Presentation.Screens
{
    public static class WardrobeLayout
    {
        private const float SideInsetDp = 12f;
        private const float GapDp = 8f;
        private const float EntryWidthDp = 156f;
        private const float EntryHeightDp = 52f;
        private const float BackSideDp = 50f;
        private const float CatSelectorHeightDp = 50f;
        private const float TabsHeightDp = 50f;
        private const float ActionHeightDp = 56f;
        private const float StatusHeightDp = 40f;
        private const float ItemRailHeightDp = 112f;
        private const float ItemCardMaxWidthDp = 112f;

        public static Rect EntryRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            float width = Mathf.Min(EntryWidthDp * px,
                Mathf.Max(0f, safeArea.width - inset * 2f));
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

        public static Rect CatSelectorRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var title = TitleRect(safeArea, dpi);
            return new Rect(safeArea.x + inset,
                title.yMin - GapDp * px - CatSelectorHeightDp * px,
                Mathf.Max(0f, safeArea.width - inset * 2f), CatSelectorHeightDp * px);
        }

        public static Rect PortraitRect(Rect safeArea, float dpi)
            => PortraitRect(safeArea, dpi, 1, true);

        public static Rect PortraitRect(Rect safeArea, float dpi, int visibleCount,
            bool hasPrimaryAction)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var cats = CatSelectorRect(safeArea, dpi);
            var tabs = TabsRect(safeArea, dpi, visibleCount, hasPrimaryAction);
            float y = tabs.yMax + GapDp * px;
            float yMax = cats.yMin - GapDp * px;
            return new Rect(safeArea.x + inset, y,
                Mathf.Max(0f, safeArea.width - inset * 2f), Mathf.Max(0f, yMax - y));
        }

        public static Rect TabsRect(Rect safeArea, float dpi)
            => TabsRect(safeArea, dpi, 1, true);

        public static Rect TabsRect(Rect safeArea, float dpi, int visibleCount,
            bool hasPrimaryAction)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var items = ItemsRect(safeArea, dpi, visibleCount, hasPrimaryAction);
            return new Rect(safeArea.x + inset, items.yMax + GapDp * px,
                Mathf.Max(0f, safeArea.width - inset * 2f), TabsHeightDp * px);
        }

        public static Rect ItemsRect(Rect safeArea, float dpi)
            => ItemsRect(safeArea, dpi, 1, true);

        public static Rect ItemsRect(Rect safeArea, float dpi, int visibleCount,
            bool hasPrimaryAction)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var status = StatusRect(safeArea, dpi, hasPrimaryAction);
            float y = status.yMax + GapDp * px;
            return new Rect(safeArea.x + inset, y,
                Mathf.Max(0f, safeArea.width - inset * 2f), ItemRailHeightDp * px);
        }

        public static Rect ItemCardRect(Rect itemsRect, int visibleIndex, int visibleCount,
            float dpi)
        {
            if (visibleIndex < 0 || visibleCount <= 0 || visibleIndex >= visibleCount)
                return default;
            float px = HudBands.PxPerDp(dpi);
            float gap = GapDp * px;
            float available = Mathf.Max(0f, itemsRect.width - gap * (visibleCount - 1));
            float width = Mathf.Min(ItemCardMaxWidthDp * px, available / visibleCount);
            float total = width * visibleCount + gap * (visibleCount - 1);
            float x = itemsRect.x + Mathf.Max(0f, (itemsRect.width - total) * 0.5f)
                + visibleIndex * (width + gap);
            return new Rect(x, itemsRect.y, width, itemsRect.height);
        }

        public static Rect ActionBandRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            return new Rect(safeArea.x + inset, safeArea.y + inset,
                Mathf.Max(0f, safeArea.width - inset * 2f), ActionHeightDp * px);
        }

        public static Rect PrimaryActionRect(Rect safeArea, float dpi)
        {
            float gap = GapDp * HudBands.PxPerDp(dpi);
            var band = ActionBandRect(safeArea, dpi);
            return new Rect(band.x, band.y, Mathf.Max(0f, (band.width - gap) * 0.5f),
                band.height);
        }

        public static Rect BuyRect(Rect safeArea, float dpi) => PrimaryActionRect(safeArea, dpi);

        public static Rect RestoreRect(Rect safeArea, float dpi)
            => RestoreRect(safeArea, dpi, true);

        public static Rect RestoreRect(Rect safeArea, float dpi, bool hasPrimaryAction)
        {
            var band = ActionBandRect(safeArea, dpi);
            if (!hasPrimaryAction) return band;
            float gap = GapDp * HudBands.PxPerDp(dpi);
            var primary = PrimaryActionRect(safeArea, dpi);
            return new Rect(primary.xMax + gap, band.y, primary.width, band.height);
        }

        public static Rect StatusRect(Rect safeArea, float dpi)
            => StatusRect(safeArea, dpi, true);

        public static Rect StatusRect(Rect safeArea, float dpi, bool hasPrimaryAction)
        {
            float px = HudBands.PxPerDp(dpi);
            var band = ActionBandRect(safeArea, dpi);
            return new Rect(band.x, band.yMax + GapDp * px,
                band.width, StatusHeightDp * px);
        }
    }
}
