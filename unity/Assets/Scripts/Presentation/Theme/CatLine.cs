using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Theme
{
    // The line vocabulary in one place — what a cat colour looks like, what SHAPE stands for
    // it, and what LETTER stands for it. Written by feat/hud-wave for the wave-preview
    // capsule; adopted here (feat/station-badges) as the BOARD's source too, which is what
    // makes it a vocabulary rather than a HUD detail.
    //
    // Why three carriers. BoardView calls it "the triple coding": the board tints a station
    // with the line colour, stamps the first letter of the colour name on it, and the prop
    // layer cuts the plate into that line's SHAPE. Colour alone never carries destination
    // identity anywhere in this game — which is the whole point for a red/green viewer.
    //
    // WHY THIS FILE IS THE SINGLE SOURCE. Until 2026-08-25 there was no vocabulary. The board
    // picked its plate geometry with `label.text == "R" ? Cylinder : Cube` — a BINARY rule
    // that means "red is a circle, EVERYTHING else is a square". That reads as a shape channel
    // only while a level has at most two colours; the moment a level authored a GREEN station,
    // green and blue plates were both cubes and only colour separated them. All 17 shipped
    // levels used red/blue/yellow, so nobody had hit it, but feat/level-variety was authoring
    // green destinations straight into it. The letter channel survived either way, so this was
    // triple coding degrading to double — not to colour-only — but it was still the board, the
    // thing the player actually looks at.
    //
    // The fix was not to widen that switch. It was to DELETE it, so the HUD badge and the
    // board plate cannot disagree about what a line looks like. There is now exactly ONE shape
    // decision in the codebase and it is ShapeOf below. Adding a fifth line is a one-case edit
    // here and nowhere else, and both surfaces pick it up for free.
    //
    // Realisation is deliberately NOT here, and is allowed to differ — HudShapeSprites.ForShape
    // rasterises a DestinationShape into a UGUI sprite, DestinationShapeMesh.ForShape extrudes
    // it into a board plate. Different media, one vocabulary: both are keyed by exactly what
    // ShapeOf returns.
    //
    // MERGE NOTE for feat/hud-wave: this file exists on that branch too. The public API is
    // identical (ColorOf / ShapeOf / GlyphOf, same signatures, same results), so its callers
    // compile unchanged against this copy — take THIS one. The .meta GUID is deliberately the
    // same on both branches so Unity never sees two scripts. See DestinationShape.cs for the
    // one companion edit HudShapeSprites.cs needs.
    public static class CatLine
    {
        // Every line the game has. Domain.CatColor is Red/Blue/Yellow/Green — there is no
        // fifth line to map. Exposed so callers and tests can ENUMERATE the vocabulary rather
        // than hard-code a list beside it that silently goes stale when a line is added.
        private static readonly string[] AllNames = { "red", "blue", "yellow", "green" };

        public static IReadOnlyList<string> Names => AllNames;

        // The byte-keyed door into the SAME table, for the surfaces that hold a Domain colour
        // code rather than a name — trains and cats. Domain.CatColor is 1-based with None at 0
        // (LevelGraph.cs:7-15) and AllNames is in precisely that order, so this is an INDEX,
        // not a second table, which is the entire point of it existing here. Anything off the
        // end — None, and the reserved construction-guarded Wild — falls through to the
        // unknown-line answer, so an unsupported code goes loud instead of picking a line.
        //
        // The 1-based alignment is load-bearing and deliberately NOT re-encoded as a switch;
        // PropPlacementTests.ColourHasOneDecisionSite_OnBothKeyTypes pins it against the real
        // CatColor constants so a reorder fails there instead of painting trains magenta.
        public static string NameOfCode(byte code) =>
            code >= 1 && code <= AllNames.Length ? AllNames[code - 1] : "";

        public static Color ColorOf(byte code) => ColorOf(NameOfCode(code));

        public static Color ColorOf(string name)
        {
            switch (name)
            {
                case "red": return Palette.SignalRed;
                case "blue": return Palette.HarborBlue;
                case "yellow": return Palette.TabbyYellow;
                case "green": return Palette.GardenGreen;
                default: return Color.magenta; // BoardView's convention: a loud content bug
            }
        }

        // THE shape decision. Nothing else in the codebase may hold one.
        public static DestinationShape ShapeOf(string name)
        {
            switch (name)
            {
                case "red": return DestinationShape.Circle;    // board: builtin cylinder plate
                case "blue": return DestinationShape.Square;   // board: builtin cube plate
                case "yellow": return DestinationShape.Triangle;
                case "green": return DestinationShape.Hexagon;
                // Every line gets its OWN shape — a shared shape would put identity back on
                // colour alone, which is exactly what the badge exists to avoid. Red and blue
                // keep the two the board already painted, so nothing a player has learned and
                // nothing an existing capture pinned changes.
                //
                // The fallback deliberately does NOT invent a fifth shape, and that is only
                // safe because it never travels alone. An unknown line is a content bug, and
                // it is already unmistakable on both channels that CAN shout: ColorOf returns
                // magenta and the glyph comes out "?". So the pair a player sees is "magenta
                // circle", never "red circle" — a red station wears SignalRed, so the shape
                // channel can never be the thing that makes a bug look like a real
                // destination. LevelImporter rejects an unknown colour outright, so the only
                // value that reaches this line is "" from a station with an empty accepts
                // list. PropPlacementTests.UnknownLine_IsLoudOnTheChannelsThatCanBeLoud pins
                // that pair; do not quietly tidy the magenta away and leave the circle behind.
                default: return DestinationShape.Circle;
            }
        }

        // The board's own glyph rule, not a copy of its results:
        // BoardView stamps `Accepts[0].Substring(0, 1).ToUpperInvariant()`.
        public static string GlyphOf(string name)
        {
            return string.IsNullOrEmpty(name)
                ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }
    }
}
