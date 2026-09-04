using System;

namespace CatMetro.Domain
{
    // Canonical per-train identity byte. Legacy concrete colors remain byte-identical because
    // Round is encoded as shape offset zero and both flags default off.
    public static class CatToken
    {
        public const int ColorMask = 0x07;
        public const int ShapeMask = 0x18;
        public const int StrayMask = 0x20;
        public const int ExpressMask = 0x40;
        private const int ReservedMask = 0x80;
        private const int ShapeShift = 3;

        public static byte Color(byte packed)
        {
            ValidatePacked(packed);
            return (byte)(packed & ColorMask);
        }

        public static byte Shape(byte packed)
        {
            ValidatePacked(packed);
            return (byte)(CatShape.Round + ((packed & ShapeMask) >> ShapeShift));
        }

        public static bool IsStray(byte packed)
        {
            ValidatePacked(packed);
            return (packed & StrayMask) != 0;
        }

        public static bool IsExpress(byte packed)
        {
            ValidatePacked(packed);
            return (packed & ExpressMask) != 0;
        }

        public static byte Pack(byte color, byte shape, bool stray, bool express)
        {
            if (color < CatColor.Red || color > CatColor.Wild)
                throw new ArgumentOutOfRangeException(nameof(color));
            if (!CatShape.IsKnown(shape))
                throw new ArgumentOutOfRangeException(nameof(shape));

            int packed = color | ((shape - CatShape.Round) << ShapeShift);
            if (stray) packed |= StrayMask;
            if (express) packed |= ExpressMask;
            return (byte)packed;
        }

        private static void ValidatePacked(byte packed)
        {
            int color = packed & ColorMask;
            int shapeOffset = (packed & ShapeMask) >> ShapeShift;
            if ((packed & ReservedMask) != 0
                || color > CatColor.Wild
                || shapeOffset > CatShape.Triangle - CatShape.Round
                || (color == CatColor.None && packed != 0))
                throw new ArgumentOutOfRangeException(nameof(packed));
        }
    }
}
