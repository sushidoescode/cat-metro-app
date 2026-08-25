using UnityEngine;
using CatMetro.Presentation.Hud.WavePreview;

namespace CatMetro.Presentation.Theme
{
    // HUD-WAVE: the line vocabulary in one place — what a cat colour looks like, what SHAPE
    // stands for it, and what LETTER stands for it.
    //
    // Why three carriers. The board already runs what BoardView calls "the triple coding":
    // it tints a station with the line colour and stamps the first letter of the colour name
    // on it (BoardView.cs — `stationAccept[...].Substring(0, 1).ToUpperInvariant()`), and
    // BoardPropDecorator picks the plate PRIMITIVE off that letter (`label.text == "R"` gives
    // a cylinder, anything else a cube). So the board's shape rule today is really only
    // "red is a circle, everything else is a square" — enough for a two-line level, not a
    // vocabulary. This extends it to four lines while keeping both cases the board already
    // paints, so a red cat's HUD badge and its station badge agree, and so does blue's.
    //
    // Colour alone never carries destination identity anywhere in this HUD: every face badge
    // shows shape AND letter as well, which is the whole point for a red/green viewer.
    //
    // Glyph derivation is deliberately the SAME expression BoardView uses rather than a second
    // switch, so the HUD and the board cannot drift apart when a fifth line is authored.
    //
    // ---------------------------------------------------------------------------------------
    // KNOWN GAP — the BOARD's station shapes, not this file's. Not fixed here on purpose: it
    // belongs to the props lane. Stated precisely so whoever picks it up does not have to
    // rediscover it.
    //
    //   FILE:       unity/Assets/Scripts/Presentation/Props/BoardPropDecorator.cs
    //   EXPRESSION: bool isRedCircle = label.text == "R";
    //               ... GameObject.CreatePrimitive(isRedCircle
    //                       ? PrimitiveType.Cylinder : PrimitiveType.Cube)
    //
    // That is a BINARY rule: red gets a cylinder, EVERY other line gets a cube. It reads as a
    // shape vocabulary only while a level has at most two colours. All 17 authored levels use
    // red/blue/yellow today, and a feat/level-variety lane is introducing GREEN — at which
    // point a green station and a blue station are BOTH cubes and only colour separates them.
    // That is a real colourblind-safety hole, and it is the board (the thing the player is
    // actually looking at), not the HUD.
    //
    // The fix is for that expression to grow toward ShapeOf below — one vocabulary, four
    // distinct shapes, keeping the two cases it already paints (red -> circle/cylinder,
    // blue -> square/cube) so nothing a player has learned changes. The HUD is already there.
    // ---------------------------------------------------------------------------------------
    public static class CatLine
    {
        public static Color ColorOf(string name)
        {
            switch (name)
            {
                case "red": return Palette.SignalRed;
                case "blue": return Palette.HarborBlue;
                case "yellow": return Palette.TabbyYellow;
                case "green": return Palette.GardenGreen;
                // Domain.CatColor is Red/Blue/Yellow/Green — there is no fifth line to map.
                default: return Color.magenta; // BoardView's convention: a loud content bug
            }
        }

        public static DestinationShape ShapeOf(string name)
        {
            switch (name)
            {
                case "red": return DestinationShape.Circle;    // board: cylinder plate
                case "blue": return DestinationShape.Square;   // board: cube plate
                case "yellow": return DestinationShape.Triangle;
                case "green": return DestinationShape.Hexagon;
                // Every line gets its OWN shape — a shared shape would put identity back on
                // colour alone, which is exactly what the badge exists to avoid.
                default: return DestinationShape.Circle;
            }
        }

        // The board's own glyph rule, not a copy of its results.
        public static string GlyphOf(string name)
        {
            return string.IsNullOrEmpty(name)
                ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }
    }
}
