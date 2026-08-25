namespace CatMetro.Presentation.Theme
{
    // STATION-BADGE: the shape half of the line vocabulary, living beside Palette's colour
    // half because it is the same KIND of fact — a per-line brand token, not a rendering
    // detail. Two realisers key off it and neither owns it: the HUD rasterises it into a UGUI
    // sprite (HudShapeSprites.ForShape) and the board extrudes it into a station plate
    // (DestinationShapeMesh.ForShape). CatLine.ShapeOf is the only place a line NAME becomes
    // one of these values.
    //
    // The members are CONFORMANCE to docs/design/assets/CAT-MANIFEST.json, not a free choice.
    // Each cat model was generated carrying a badge on its chest and a matching tag on its
    // collar, and those bytes are pinned by the licensing record — so where art and code
    // disagree, the code moves. Green is diamond because the green shorthair wears a diamond;
    // it was hexagon here until a manifest read caught it, which would have shipped a green
    // cat wearing a diamond walking to a hexagonal plate. VocabularyMatchesTheCatManifest
    // pins the whole table so that cannot recur silently.
    //
    // Star is the odd one: it belongs to the WILD cat, which is not a destination at all — a
    // wild cat auto-accepts at whatever berth it reaches, so no station ever wears a star.
    // It lives here because the HUD face badge needs it. DestinationShapeMesh.ForShape(Star)
    // THROWS rather than returning geometry, because a star is concave and the extruder fans
    // from vertex 0: it would produce silent garbage, which is the exact failure class this
    // repo has already eaten twice.
    //
    // MERGE NOTE for feat/hud-wave. That branch declares this same enum inside
    // Hud/WavePreview/HudShapeSprites.cs, in namespace CatMetro.Presentation.Hud.WavePreview.
    // When the two branches land together the resolution is a two-line edit to that file:
    // DELETE its `public enum DestinationShape { ... }` block and add
    // `using CatMetro.Presentation.Theme;`. The enum moves because the board cannot reasonably
    // reach into the HUD's private sprite factory to name a shape, and Theme is the one
    // namespace the board, the props layer and the HUD already all depend on.
    //
    // That branch ALSO needs two sprite changes, because its copy of this enum predates the
    // manifest read: its `Hexagon` sprite and `ForShape` case become `Diamond` (a square on
    // its point — its InsideRounded/predicate style makes this a small edit), and it needs a
    // new `Star` sprite plus case for the wild cat's face badge.
    public enum DestinationShape
    {
        Circle,
        Square,
        Triangle,
        Diamond,
        Star
    }
}
