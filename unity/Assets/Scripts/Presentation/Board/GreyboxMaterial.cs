namespace CatMetro.Presentation.Board
{
    // CM-C2b-DEVFIX criterion 5: the one shared greybox material provider — cached
    // Resources load in UiStrings' shape. On a failed load it logs the error LOUDLY and
    // callers receive null (the editor tests catch a missing asset long before a device
    // does; review F-7 records this honestly rather than claiming a fallback that isn't
    // there). Every runtime-created renderer binds Shared before any colour write, so
    // nothing in the build depends on the engine's default material (the shader that gets
    // stripped on device).
    public static class GreyboxMaterial
    {
        private static UnityEngine.Material _shared;

        public static UnityEngine.Material Shared
        {
            get
            {
                if (_shared == null)
                {
                    _shared = UnityEngine.Resources.Load<UnityEngine.Material>("Materials/Greybox");
                    if (_shared == null)
                        UnityEngine.Debug.LogError(
                            "GreyboxMaterial: Materials/Greybox missing from Resources — " +
                            "runtime primitives would fall back to the strippable engine default");
                }
                return _shared;
            }
        }

        public static UnityEngine.Material CreateTinted(string name, UnityEngine.Color color)
        {
            var basis = Shared;
            if (basis == null) return null;
            var material = new UnityEngine.Material(basis) { name = name };
            material.color = color;
            return material;
        }
    }
}
