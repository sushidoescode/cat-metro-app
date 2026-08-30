using System;
using System.Collections.Generic;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Cosmetics
{
    /// <summary>
    /// Paints the admitted cosmetic renderer tokens from reusable UGUI shapes. Token groups
    /// are built once beneath a layer and then activated as needed, so profile updates do not
    /// create material instances or depend on deferred destruction.
    /// </summary>
    public static class CosmeticPortraitPainter
    {
        private const string RedTabby = "cat.red_tabby";
        private const string BlueSiamese = "cat.blue_siamese";
        private const string YellowLonghair = "cat.yellow_longhair";
        private const string Conductor = "outfit.conductor";
        private const string Brass = "frame.brass";
        private const string Lantern = "frame.lantern";

        public static IReadOnlyCollection<string> SupportedRendererTokens { get; } =
            Array.AsReadOnly(new[]
            {
                RedTabby,
                BlueSiamese,
                YellowLonghair,
                Conductor,
                Brass,
                Lantern,
            });

        internal static bool Paint(RectTransform layer, string rendererToken)
        {
            if (layer == null || !IsSupported(rendererToken))
            {
                Clear(layer);
                return false;
            }

            DeactivateChildren(layer);
            var group = layer.Find(rendererToken) as RectTransform;
            if (group == null)
            {
                group = MakeGroup(layer, rendererToken);
                Build(group, rendererToken);
            }

            group.gameObject.SetActive(true);
            layer.gameObject.SetActive(true);
            return true;
        }

        internal static void Clear(RectTransform layer)
        {
            if (layer == null) return;
            DeactivateChildren(layer);
            layer.gameObject.SetActive(false);
        }

        private static bool IsSupported(string rendererToken)
        {
            switch (rendererToken)
            {
                case RedTabby:
                case BlueSiamese:
                case YellowLonghair:
                case Conductor:
                case Brass:
                case Lantern:
                    return true;
                default:
                    return false;
            }
        }

        private static void Build(RectTransform group, string rendererToken)
        {
            switch (rendererToken)
            {
                case RedTabby:
                    BuildRedTabby(group);
                    break;
                case BlueSiamese:
                    BuildBlueSiamese(group);
                    break;
                case YellowLonghair:
                    BuildYellowLonghair(group);
                    break;
                case Conductor:
                    BuildConductor(group);
                    break;
                case Brass:
                    BuildBrassFrame(group);
                    break;
                case Lantern:
                    BuildLanternFrame(group);
                    break;
            }
        }

        private static void BuildRedTabby(Transform parent)
        {
            Add(parent, "Body", HudShapeSprites.Capsule, Palette.SignalRed,
                V(0.29f, 0.07f), V(0.71f, 0.43f));
            AddEars(parent, Palette.SignalRed);
            Add(parent, "Head", HudShapeSprites.Disc, Palette.SignalRed,
                V(0.18f, 0.28f), V(0.82f, 0.90f));
            AddFace(parent, Palette.InkNavy);
            Add(parent, "StripeCentre", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.485f, 0.73f), V(0.515f, 0.84f));
            Add(parent, "StripeLeft", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.41f, 0.73f), V(0.44f, 0.82f), 14f);
            Add(parent, "StripeRight", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.56f, 0.73f), V(0.59f, 0.82f), -14f);
            AddWhiskers(parent);
        }

        private static void BuildBlueSiamese(Transform parent)
        {
            Add(parent, "Body", HudShapeSprites.Capsule, Palette.HarborBlue,
                V(0.29f, 0.07f), V(0.71f, 0.43f));
            AddEars(parent, Palette.InkNavy);
            Add(parent, "Head", HudShapeSprites.Disc, Palette.HarborBlue,
                V(0.18f, 0.28f), V(0.82f, 0.90f));
            Add(parent, "CreamFaceCentre", HudShapeSprites.Disc, Palette.CreamCard,
                V(0.31f, 0.39f), V(0.69f, 0.75f));
            AddFace(parent, Palette.InkNavy);
            Add(parent, "SiameseMuzzle", HudShapeSprites.Disc, Palette.InkNavy,
                V(0.43f, 0.45f), V(0.57f, 0.57f));
            AddWhiskers(parent);
        }

        private static void BuildYellowLonghair(Transform parent)
        {
            Add(parent, "Body", HudShapeSprites.Capsule, Palette.TabbyYellow,
                V(0.27f, 0.06f), V(0.73f, 0.45f));
            Add(parent, "CheekTuftLeft", HudShapeSprites.Triangle, Palette.TabbyYellow,
                V(0.10f, 0.34f), V(0.34f, 0.60f), 90f);
            Add(parent, "CheekTuftRight", HudShapeSprites.Triangle, Palette.TabbyYellow,
                V(0.66f, 0.34f), V(0.90f, 0.60f), -90f);
            AddEars(parent, Palette.TabbyYellow);
            Add(parent, "Head", HudShapeSprites.Disc, Palette.TabbyYellow,
                V(0.16f, 0.27f), V(0.84f, 0.91f));
            AddFace(parent, Palette.InkNavy);
            Add(parent, "ForeheadMark", HudShapeSprites.Triangle, Palette.InkNavy,
                V(0.46f, 0.71f), V(0.54f, 0.81f), 180f);
            AddWhiskers(parent);
        }

        private static void BuildConductor(Transform parent)
        {
            Add(parent, "Coat", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.24f, 0.06f), V(0.76f, 0.45f));
            Add(parent, "CollarLeft", HudShapeSprites.Triangle, Palette.CreamCard,
                V(0.37f, 0.34f), V(0.51f, 0.49f), -18f);
            Add(parent, "CollarRight", HudShapeSprites.Triangle, Palette.CreamCard,
                V(0.49f, 0.34f), V(0.63f, 0.49f), 18f);
            Add(parent, "ButtonTop", HudShapeSprites.Disc, Palette.TabbyYellow,
                V(0.475f, 0.26f), V(0.525f, 0.31f));
            Add(parent, "ButtonBottom", HudShapeSprites.Disc, Palette.TabbyYellow,
                V(0.475f, 0.16f), V(0.525f, 0.21f));
            Add(parent, "HatBrim", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.20f, 0.76f), V(0.80f, 0.84f));
            Add(parent, "HatCrown", HudShapeSprites.RoundedSquare, Palette.InkNavy,
                V(0.31f, 0.76f), V(0.69f, 0.95f));
            Add(parent, "HatBand", HudShapeSprites.Capsule, Palette.CreamCard,
                V(0.33f, 0.80f), V(0.67f, 0.84f));
            Add(parent, "HatBadge", HudShapeSprites.Disc, Palette.TabbyYellow,
                V(0.455f, 0.84f), V(0.545f, 0.93f));
        }

        private static void BuildBrassFrame(Transform parent)
        {
            AddRails(parent, "Outer", Palette.TabbyYellow, 0.025f, 0.065f);
            AddRails(parent, "Inner", Palette.CreamCard, 0.085f, 0.035f);
            AddCornerNotch(parent, "NotchBottomLeft", V(0.02f, 0.02f));
            AddCornerNotch(parent, "NotchBottomRight", V(0.88f, 0.02f));
            AddCornerNotch(parent, "NotchTopLeft", V(0.02f, 0.88f));
            AddCornerNotch(parent, "NotchTopRight", V(0.88f, 0.88f));
        }

        private static void BuildLanternFrame(Transform parent)
        {
            AddRails(parent, "NavyOuter", Palette.InkNavy, 0.025f, 0.065f);
            AddRails(parent, "TealInner", Palette.MetroTeal, 0.085f, 0.035f);
            AddLantern(parent, "LanternBottomLeft", V(0.015f, 0.015f));
            AddLantern(parent, "LanternBottomRight", V(0.875f, 0.015f));
            AddLantern(parent, "LanternTopLeft", V(0.015f, 0.875f));
            AddLantern(parent, "LanternTopRight", V(0.875f, 0.875f));
        }

        private static void AddEars(Transform parent, Color color)
        {
            Add(parent, "EarLeft", HudShapeSprites.Triangle, color,
                V(0.21f, 0.66f), V(0.43f, 0.94f), 10f);
            Add(parent, "EarRight", HudShapeSprites.Triangle, color,
                V(0.57f, 0.66f), V(0.79f, 0.94f), -10f);
        }

        private static void AddFace(Transform parent, Color color)
        {
            Add(parent, "EyeLeft", HudShapeSprites.Disc, color,
                V(0.35f, 0.56f), V(0.41f, 0.62f));
            Add(parent, "EyeRight", HudShapeSprites.Disc, color,
                V(0.59f, 0.56f), V(0.65f, 0.62f));
            Add(parent, "Muzzle", HudShapeSprites.Disc, color,
                V(0.47f, 0.48f), V(0.53f, 0.54f));
        }

        private static void AddWhiskers(Transform parent)
        {
            Add(parent, "WhiskerLeftUpper", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.23f, 0.49f), V(0.39f, 0.515f), 7f);
            Add(parent, "WhiskerLeftLower", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.23f, 0.45f), V(0.39f, 0.475f), -7f);
            Add(parent, "WhiskerRightUpper", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.61f, 0.49f), V(0.77f, 0.515f), -7f);
            Add(parent, "WhiskerRightLower", HudShapeSprites.Capsule, Palette.InkNavy,
                V(0.61f, 0.45f), V(0.77f, 0.475f), 7f);
        }

        private static void AddRails(Transform parent, string prefix, Color color,
            float inset, float thickness)
        {
            float far = 1f - inset;
            Add(parent, prefix + "Left", HudShapeSprites.Capsule, color,
                V(inset, inset), V(inset + thickness, far));
            Add(parent, prefix + "Right", HudShapeSprites.Capsule, color,
                V(far - thickness, inset), V(far, far));
            Add(parent, prefix + "Bottom", HudShapeSprites.Capsule, color,
                V(inset, inset), V(far, inset + thickness));
            Add(parent, prefix + "Top", HudShapeSprites.Capsule, color,
                V(inset, far - thickness), V(far, far));
        }

        private static void AddCornerNotch(Transform parent, string name, Vector2 min)
        {
            Add(parent, name, HudShapeSprites.RoundedSquare, Palette.TabbyYellow,
                min, min + V(0.10f, 0.10f), 45f);
        }

        private static void AddLantern(Transform parent, string name, Vector2 min)
        {
            Add(parent, name + "Housing", HudShapeSprites.RoundedSquare, Palette.InkNavy,
                min, min + V(0.11f, 0.11f));
            Add(parent, name + "Glow", HudShapeSprites.Disc, Palette.TabbyYellow,
                min + V(0.026f, 0.026f), min + V(0.084f, 0.084f));
        }

        private static RectTransform MakeGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
        }

        private static Image Add(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, float rotation = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.material = UiChromeMaterial.Shared;
            image.raycastTarget = false;
            return image;
        }

        private static void DeactivateChildren(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
                parent.GetChild(i).gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);
    }
}
