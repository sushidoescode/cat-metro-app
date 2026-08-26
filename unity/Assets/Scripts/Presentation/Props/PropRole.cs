namespace CatMetro.Presentation.Props
{
    /// <summary>
    /// The gameplay/decorative split for props, in one place because two unrelated callers
    /// have to agree on it: the camera's horizontal fit (BoardSceneLook.FitCamera) and the
    /// safe-frame law (RuntimeSceneRigTests). If those two ever disagree the fit will solve
    /// for one set of renderers and the law will assert on another, which is the shape of
    /// bug that produced the furnished-board signpost failure.
    ///
    /// The role strings are authored by BoardPropDecorator.Decorate and are the only handle
    /// on a prop's purpose that survives instantiation — BoardPropInstance deliberately is
    /// not a BoardElementId, so scenery never enters a simulation or input inventory.
    /// </summary>
    public static class PropRole
    {
        // Gameplay: these stand in for a board element the player must read and act on.
        // The kiosk IS the station (SuppressReplacedStationArchitecture hides the primitive
        // behind it) and the depot IS the source (SuppressReplacedSourceVisual likewise), so
        // if either leaves the frame the player loses a thing the level asks them to use.
        public const string StationKiosk = "station-kiosk";
        public const string Depot = "depot";

        // Decorative: scenery. Nothing here is referenced by a rule, a tap target, or a
        // wave. The parked engine is the only borderline one — it is a second, static toy
        // loco parked beside the source and is never the train the player routes.
        public const string PerimeterTrees = "perimeter-trees";
        public const string DeskClutter = "desk-clutter";
        public const string ParkedEngine = "parked-engine";

        public static bool IsDecorative(string role) =>
            role == PerimeterTrees || role == DeskClutter || role == ParkedEngine;

        public static bool IsGameplay(string role) =>
            role == StationKiosk || role == Depot;
    }
}
