namespace CatMetro.Presentation.Hud
{
    // CM-UX-02: the one shared UI material provider — the GreyboxMaterial pattern applied to
    // chrome (the F-DEV-2 lesson: a runtime-created canvas must never depend on a strippable
    // engine default; a Resources-loaded material force-includes its shader in the build).
    // On a missing asset it logs LOUDLY and callers receive null — the editor suite catches
    // that long before a device does.
    public static class UiChromeMaterial
    {
        private static UnityEngine.Material _shared;

        public static UnityEngine.Material Shared
        {
            get
            {
                if (_shared == null)
                {
                    _shared = UnityEngine.Resources.Load<UnityEngine.Material>("Materials/UiChrome");
                    if (_shared == null)
                        UnityEngine.Debug.LogError(
                            "UiChromeMaterial: Materials/UiChrome missing from Resources — "
                            + "runtime chrome would depend on a strippable engine default");
                }
                return _shared;
            }
        }
    }
}
