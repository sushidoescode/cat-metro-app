using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Hud
{
    public enum CatMetroTextRole
    {
        Display,
        Heading,
        Body,
        Cta,
        Caption,
    }

    // UI-CHROME criterion 1: one code-visible vocabulary for every runtime-built view.
    // Values are the authoritative product-spec bytes; views choose roles, never variants.
    public static class CatMetroUiTheme
    {
        public static Color CreamCard => Hex(0xF2, 0xEA, 0xD9);
        public static Color WarmPaper => Hex(0xFA, 0xF6, 0xEC);
        public static Color InkNavy => Hex(0x22, 0x30, 0x4A);
        public static Color DepotNavy => Hex(0x13, 0x1C, 0x30);
        public static Color MetroTeal => Hex(0x3B, 0xAF, 0xA8);
        public static Color TicketOrange => Hex(0xF0, 0x8A, 0x3C);
        public static Color SignalRed => Hex(0xE1, 0x5A, 0x47);
        public static Color HarborBlue => Hex(0x3E, 0x7C, 0xC9);
        public static Color TabbyYellow => Hex(0xEF, 0xC1, 0x3D);
        public static Color GardenGreen => Hex(0x4F, 0xA3, 0x6A);
        public static Color CatnipViolet => Hex(0xA0, 0x6B, 0xD8);
        public static Color AlarmCoral => Hex(0xD9, 0x3A, 0x2B);

        private static TMP_FontAsset _font;
        private static Sprite _rounded;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.Load<TMP_FontAsset>(
                        "CatMetroUI/Fonts/CatMetroSans SDF");
                return _font;
            }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null)
                    _rounded = Resources.Load<Sprite>(
                        "CatMetroUI/Textures/RoundedRect");
                return _rounded;
            }
        }

        public static Sprite SymbolSprite(string resourceName)
        {
            return Resources.Load<Sprite>("CatMetroUI/Textures/" + resourceName);
        }

        public static void StyleText(TMP_Text text, CatMetroTextRole role,
            Color? color = null)
        {
            if (Font != null) text.font = Font;
            text.color = color ?? InkNavy;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;

            switch (role)
            {
                case CatMetroTextRole.Display:
                    text.fontSizeMin = 32f;
                    text.fontSizeMax = 54f;
                    text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    text.characterSpacing = 8f;
                    break;
                case CatMetroTextRole.Heading:
                    text.fontSizeMin = 28f;
                    text.fontSizeMax = 48f;
                    text.fontStyle = FontStyles.Bold;
                    text.characterSpacing = 1f;
                    break;
                case CatMetroTextRole.Body:
                    text.fontSizeMin = 22f;
                    text.fontSizeMax = 34f;
                    break;
                case CatMetroTextRole.Cta:
                    text.fontSizeMin = 24f;
                    text.fontSizeMax = 42f;
                    text.fontStyle = FontStyles.Bold;
                    text.characterSpacing = 2f;
                    break;
                default:
                    text.fontSizeMin = 18f;
                    text.fontSizeMax = 30f;
                    text.fontStyle = FontStyles.Bold;
                    break;
            }
        }

        public static Image StyleImage(Image image, Color color, bool rounded = true)
        {
            var material = UiChromeMaterial.Shared;
            if (material != null) image.material = material;
            image.color = color;
            image.raycastTarget = false;
            if (rounded && RoundedSprite != null)
            {
                image.sprite = RoundedSprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public static RectTransform Stretch(GameObject go, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image MakeImage(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color, bool rounded = true)
        {
            var go = new GameObject(name);
            Stretch(go, parent, anchorMin, anchorMax);
            return StyleImage(go.AddComponent<Image>(), color, rounded);
        }

        public static Image MakeSymbol(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color, string resourceName)
        {
            var image = MakeImage(parent, name, anchorMin, anchorMax, color,
                rounded: false);
            image.sprite = SymbolSprite(resourceName);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        public static TMP_Text MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string value,
            CatMetroTextRole role, Color? color = null)
        {
            var go = new GameObject(name);
            Stretch(go, parent, anchorMin, anchorMax);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.alignment = TextAlignmentOptions.Center;
            StyleText(text, role, color);
            return text;
        }

        private static Color Hex(byte r, byte g, byte b)
        {
            return new Color32(r, g, b, 0xFF);
        }
    }
}
