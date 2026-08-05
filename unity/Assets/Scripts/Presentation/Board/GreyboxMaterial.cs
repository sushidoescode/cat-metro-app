namespace CatMetro.Presentation.Board
{
    // CM-C2b-DEVFIX criterion 5: the one shared greybox material provider — mirrors
    // UiStrings' cached-Resources-load shape (loud error, never a silent null path). Every
    // runtime-created renderer binds Shared before any colour write, so nothing in the build
    // depends on the engine's default material (the shader that gets stripped on device).
    // RED STUB.
    public static class GreyboxMaterial
    {
        public static UnityEngine.Material Shared
        {
            get { return null; } // red stub: no material committed yet
        }
    }
}
