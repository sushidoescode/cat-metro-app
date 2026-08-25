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
        // Every DESTINATION line the game has. Exposed so callers and tests can ENUMERATE the
        // vocabulary rather than hard-code a list beside it that silently goes stale.
        //
        // Wild is deliberately NOT here, and the distinction is load-bearing rather than
        // pedantic. A wild cat auto-accepts at whatever berth it reaches, so it has no
        // destination — no station ever advertises "wild", and LevelImporter pins wild out of
        // station accepts entirely. Putting it in this list would make the shape tests demand
        // a wild station plate and tempt someone into authoring a berth that cannot exist.
        private static readonly string[] LineNames = { "red", "blue", "yellow", "green" };

        // The same names PLUS wild, in Domain.CatColor order, because a CAT can be wild even
        // though a STATION cannot. This is the code-keyed table; LineNames is the semantic one.
        // Keep this mirroring CatColor 1..5 exactly — NameOfCode indexes it directly.
        private static readonly string[] CodeNames = { "red", "blue", "yellow", "green", "wild" };

        public static IReadOnlyList<string> Names => LineNames;

        // The byte-keyed door into the SAME table, for the surfaces that hold a Domain colour
        // code rather than a name — trains and cats. Domain.CatColor is 1-based with None at 0
        // (LevelGraph.cs:7-15) and CodeNames is in precisely that order, so this is an INDEX,
        // not a second table, which is the entire point of it existing here. Anything off the
        // end — None at 0, anything past Wild — falls through to the unknown answer, so an
        // unsupported code goes loud instead of quietly picking a line.
        //
        // The 1-based alignment is load-bearing and deliberately NOT re-encoded as a switch;
        // PropPlacementTests pins it against the real CatColor constants so a reorder fails
        // there instead of painting trains magenta.
        public static string NameOfCode(byte code) =>
            code >= 1 && code <= CodeNames.Length ? CodeNames[code - 1] : "";

        public static Color ColorOf(byte code) => ColorOf(NameOfCode(code));

        public static Color ColorOf(string name)
        {
            switch (name)
            {
                case "red": return Palette.SignalRed;
                case "blue": return Palette.HarborBlue;
                case "yellow": return Palette.TabbyYellow;
                case "green": return Palette.GardenGreen;
                // Not a line — a wild CAT's coat. Conformance to CAT-MANIFEST.json, which
                // generated cat-wild-alley in catnip violet (A06BD8) with a star badge; those
                // bytes are paid for and pinned, so this value is a record, not a choice.
                // Violet would read as a fifth line on its own; the star is what says
                // "goes anywhere", which is why the shape channel carries the real signal.
                case "wild": return Palette.CatnipViolet;
                default: return Color.magenta; // BoardView's convention: a loud content bug
            }
        }

        // THE shape decision. Nothing else in the codebase may hold one.
        public static DestinationShape ShapeOf(string name)
        {
            switch (name)
            {
                // Every case here is CONFORMANCE to docs/design/assets/CAT-MANIFEST.json — the
                // badge each cat model was generated wearing. Those bytes are pinned by the
                // licensing record, so art wins and this table follows.
                case "red": return DestinationShape.Circle;    // board: builtin cylinder plate
                case "blue": return DestinationShape.Square;   // board: builtin cube plate
                case "yellow": return DestinationShape.Triangle;
                // Diamond, NOT hexagon. This said hexagon until a manifest read caught it,
                // which would have shipped a green cat wearing a diamond badge walking to a
                // hexagonal plate — a mismatch in the one channel this vocabulary exists to
                // make trustworthy. No green level had shipped yet, so nothing had shown it.
                case "green": return DestinationShape.Diamond;
                // Wild is not a destination; this is its CAT badge, for the HUD face only.
                // DestinationShapeMesh.ForShape(Star) throws — no station wears a star.
                case "wild": return DestinationShape.Star;
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
