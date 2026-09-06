using TMPro;
using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Presentation.Theme
{
    // Sizes are dp. Pass the same dpi used by the surface's layout (including capture rigs).
    // No component, per-label material instance, or runtime glyph generation is required.
    public static class TypeScale
    {
        public const float Display = 40f;
        public const float Title = 28f;
        public const float Body = 20f;
        public const float Caption = 14f;
        public const float Minimum = 12f;

        private static TMP_FontAsset _displayFont;
        private static TMP_FontAsset _bodyFont;
        private static Material _boardTitle;

        public static TMP_FontAsset DisplayFont => _displayFont != null ? _displayFont
            : (_displayFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Fredoka-SemiBold SDF"));
        public static TMP_FontAsset BodyFont => _bodyFont != null ? _bodyFont
            : (_bodyFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Nunito-Regular SDF"));
        public static Material BoardTitleMaterial => _boardTitle != null ? _boardTitle
            : (_boardTitle = Resources.Load<Material>("Fonts & Materials/Fredoka-SemiBold BoardTitle"));

        public static void Apply(TMP_Text text, float sizeDp, float dpi = 160f,
            bool body = false, float minimumDp = Minimum)
        {
            if (text == null) return;
            text.font = body ? BodyFont : DisplayFont;
            text.fontStyle = FontStyles.Normal; // display is already baked at SemiBold
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(Minimum, Mathf.Max(Minimum, minimumDp) * HudBands.PxPerDp(dpi));
            text.fontSizeMax = Mathf.Max(text.fontSizeMin, sizeDp * HudBands.PxPerDp(dpi));
            text.fontSize = text.fontSizeMax;
        }
    }
}
