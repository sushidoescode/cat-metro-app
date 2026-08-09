using System.Collections.Generic;
using CatMetro.Domain;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    public static class DioramaPalette
    {
        public static readonly Color CreamCard = FromHex("F2EAD9");
        public static readonly Color WarmPaper = FromHex("FAF6EC");
        public static readonly Color InkNavy = FromHex("22304A");
        public static readonly Color DepotNavy = FromHex("131C30");
        public static readonly Color MetroTeal = FromHex("3BAFA8");
        public static readonly Color TicketOrange = FromHex("F08A3C");
        public static readonly Color SignalRed = FromHex("E15A47");
        public static readonly Color HarborBlue = FromHex("3E7CC9");
        public static readonly Color TabbyYellow = FromHex("EFC13D");
        public static readonly Color GardenGreen = FromHex("4FA36A");
        public static readonly Color CatnipViolet = FromHex("A06BD8");
        public static readonly Color AlarmCoral = FromHex("D93A2B");

        private static readonly Dictionary<string, Material> Materials =
            new Dictionary<string, Material>();

        public static Material Material(string name, Color color)
        {
            string key = name + ":" + ColorUtility.ToHtmlStringRGBA(color);
            if (Materials.TryGetValue(key, out var material) && material != null)
                return material;

            var basis = GreyboxMaterial.Shared;
            if (basis == null) return null;
            material = new Material(basis)
            {
                name = "Diorama/" + name,
                color = color,
            };
            material.enableInstancing = true;
            Materials[key] = material;
            return material;
        }

        public static Color LineColor(byte code) => LineIdentity.For(code).Color;

        private static Color FromHex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex, out var color))
                throw new System.ArgumentException("Invalid Cat Metro palette hex: " + hex);
            return color;
        }
    }

    public readonly struct LineIdentity
    {
        public readonly byte ColorCode;
        public readonly Color Color;
        public readonly string SymbolId;
        public readonly string SilhouetteId;

        private LineIdentity(byte colorCode, Color color, string symbolId, string silhouetteId)
        {
            ColorCode = colorCode;
            Color = color;
            SymbolId = symbolId;
            SilhouetteId = silhouetteId;
        }

        public static LineIdentity For(byte code)
        {
            switch (code)
            {
                case CatColor.Red:
                    return new LineIdentity(code, DioramaPalette.SignalRed,
                        "circle", "round-tabby");
                case CatColor.Blue:
                    return new LineIdentity(code, DioramaPalette.HarborBlue,
                        "square", "slim-siamese");
                case CatColor.Yellow:
                    return new LineIdentity(code, DioramaPalette.TabbyYellow,
                        "triangle", "fluffy-longhair");
                case CatColor.Green:
                    return new LineIdentity(code, DioramaPalette.GardenGreen,
                        "diamond", "sleek-shorthair");
                case CatColor.Wild:
                    return new LineIdentity(code, DioramaPalette.CatnipViolet,
                        "star", "bent-ear-scruffy");
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(code), code,
                        "Unknown Cat Metro line colour");
            }
        }

        public static LineIdentity ForName(string name)
        {
            switch (name)
            {
                case "red": return For(CatColor.Red);
                case "blue": return For(CatColor.Blue);
                case "yellow": return For(CatColor.Yellow);
                case "green": return For(CatColor.Green);
                case "wild": return For(CatColor.Wild);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(name), name,
                        "Unknown Cat Metro line colour");
            }
        }
    }
}
