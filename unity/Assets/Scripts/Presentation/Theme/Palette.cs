using UnityEngine;

namespace CatMetro.Presentation.Theme
{
    // BEAUTIFUL-MENU criterion 1: the product_spec §7 "tabletop diorama" palette ported to
    // code as the single source of brand color. Values are /255 from the spec hex so a
    // PaletteTests hex derivation matches exactly. Plain static class — never a Component,
    // so it is invisible to the HomeScreenTests render-only whitelist. Migrate screens off
    // inline `new Color(...)` literals to these constants over time.
    //
    // "Cream/navy/teal/orange is the LOCKED base palette; line colors are content." (§7)
    public static class Palette
    {
        // base palette (chrome)
        public static readonly Color CreamCard    = new Color(242f / 255f, 234f / 255f, 217f / 255f); // #F2EAD9 board/table
        public static readonly Color WarmPaper    = new Color(250f / 255f, 246f / 255f, 236f / 255f); // #FAF6EC panels/bg
        public static readonly Color InkNavy      = new Color( 34f / 255f,  48f / 255f,  74f / 255f); // #22304A outlines/text
        public static readonly Color DepotNavy    = new Color( 19f / 255f,  28f / 255f,  48f / 255f); // #131C30 shadow/parked
        public static readonly Color MetroTeal    = new Color( 59f / 255f, 175f / 255f, 168f / 255f); // #3BAFA8 accent/success
        public static readonly Color TicketOrange = new Color(240f / 255f, 138f / 255f,  60f / 255f); // #F08A3C CTA

        // line palette (content)
        public static readonly Color SignalRed    = new Color(225f / 255f,  90f / 255f,  71f / 255f); // #E15A47
        public static readonly Color HarborBlue   = new Color( 62f / 255f, 124f / 255f, 201f / 255f); // #3E7CC9
        public static readonly Color TabbyYellow  = new Color(239f / 255f, 193f / 255f,  61f / 255f); // #EFC13D
        public static readonly Color GardenGreen  = new Color( 79f / 255f, 163f / 255f, 106f / 255f); // #4FA36A
        public static readonly Color CatnipViolet = new Color(160f / 255f, 107f / 255f, 216f / 255f); // #A06BD8
        public static readonly Color AlarmCoral   = new Color(217f / 255f,  58f / 255f,  43f / 255f); // #D93A2B

        // Helper for callers that want a translucent tint of a base color.
        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
