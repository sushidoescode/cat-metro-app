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
        private const float BackSideDp = 48f;
        private const float CatSelectorHeightDp = 50f;
        private const float TabsHeightDp = 50f;
        private const float PrimaryHeightDp = 56f;
        private const float RestoreHeightDp = 44f;
        private const float StatusHeightDp = 40f;
        private const float ItemCardHeightDp = 72f;

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
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var cats = CatSelectorRect(safeArea, dpi);
            var tabs = TabsRect(safeArea, dpi);
            float y = tabs.yMax + GapDp * px;
            float yMax = cats.yMin - GapDp * px;
            return new Rect(safeArea.x + inset, y,
                Mathf.Max(0f, safeArea.width - inset * 2f), Mathf.Max(0f, yMax - y));
        }

        public static Rect TabsRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var items = ItemsRect(safeArea, dpi);
            return new Rect(safeArea.x + inset, items.yMax + GapDp * px,
                Mathf.Max(0f, safeArea.width - inset * 2f), TabsHeightDp * px);
        }

        public static Rect ItemsRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            var status = StatusRect(safeArea, dpi);
            float y = status.yMax + GapDp * px;
            float height = 120f * px;
            return new Rect(safeArea.x + inset, y,
                Mathf.Max(0f, safeArea.width - inset * 2f), height);
        }

        public static Rect ItemCardRect(Rect itemsRect, int visibleIndex, int visibleCount,
            float dpi)
        {
            if (visibleIndex < 0 || visibleCount <= 0 || visibleIndex >= visibleCount)
                return default;
            float px = HudBands.PxPerDp(dpi);
            float gap = GapDp * px;
            float available = Mathf.Max(0f, itemsRect.height - gap * (visibleCount - 1));
            float height = Mathf.Min(ItemCardHeightDp * px, available / visibleCount);
            float y = itemsRect.yMax - height - visibleIndex * (height + gap);
            return new Rect(itemsRect.x, y, itemsRect.width, height);
        }

        public static Rect PrimaryActionRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            float inset = SideInsetDp * px;
            return new Rect(safeArea.x + inset, safeArea.y + inset,
                Mathf.Max(0f, safeArea.width - inset * 2f), PrimaryHeightDp * px);
        }

        public static Rect BuyRect(Rect safeArea, float dpi) => PrimaryActionRect(safeArea, dpi);

        public static Rect RestoreRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            var primary = PrimaryActionRect(safeArea, dpi);
            return new Rect(primary.x, primary.yMax + GapDp * px,
                primary.width, RestoreHeightDp * px);
        }

        public static Rect StatusRect(Rect safeArea, float dpi)
        {
            float px = HudBands.PxPerDp(dpi);
            var restore = RestoreRect(safeArea, dpi);
            return new Rect(restore.x, restore.yMax + GapDp * px,
                restore.width, StatusHeightDp * px);
        }
    }
}
