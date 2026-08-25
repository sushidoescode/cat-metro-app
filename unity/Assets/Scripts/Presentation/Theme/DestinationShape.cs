namespace CatMetro.Presentation.Theme
{
    // STATION-BADGE: the shape half of the line vocabulary, living beside Palette's colour
    // half because it is the same KIND of fact — a per-line brand token, not a rendering
    // detail. Two realisers key off it and neither owns it: the HUD rasterises it into a UGUI
    // sprite (HudShapeSprites.ForShape) and the board extrudes it into a station plate
    // (DestinationShapeMesh.ForShape). CatLine.ShapeOf is the only place a line NAME becomes
    // one of these values.
    //
    // MERGE NOTE for feat/hud-wave. That branch declares this same enum inside
    // Hud/WavePreview/HudShapeSprites.cs, in namespace CatMetro.Presentation.Hud.WavePreview.
    // When the two branches land together the resolution is a two-line edit to that file:
    // DELETE its `public enum DestinationShape { ... }` block and add
    // `using CatMetro.Presentation.Theme;`. Nothing else changes — CatLine's public API is
    // identical on both branches. The enum moves because the board cannot reasonably reach
    // into the HUD's private sprite factory to name a shape, and Theme is the one namespace
    // the board, the props layer and the HUD already all depend on.
    public enum DestinationShape
    {
        Circle,
        Square,
        Triangle,
        Hexagon
    }
}
