using UnityEngine;
using CatMetro.Presentation.Input;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criteria 3/4/5/6: the greybox session-1 Home. One pulsing L001 pin (72dp,
    // thumb-band-centered via pure HomeLayout math; live Screen reads only here, the
    // RetryCtaView precedent), parked-district silhouettes, csv-keyed title — and NOTHING
    // else: no shop, no daily, no badges (S-01 layout intent; the tree test is the
    // monetization tripwire), and neither TG-3 variant of the bonus-district tile exists.
    // Render-only by law (P-1): the pin's hit routes through the INJECTED ChromeRegions
    // registry — this view is the tranche's first registrar and honors the R1-F3 lifetime
    // law (unregister on Hide AND OnDestroy). Motion posture (P-5, A11Y-S01-2): the pulse is
    // easing and vanishes under the injected motion-off delegate; the raised-ring shape twin
    // renders in BOTH modes — motion never carries the information.
    public sealed class HomeScreenView : MonoBehaviour
    {
        public System.Action LevelSelected;

        public Rect PinPaintedRectPx => default; // RED stub
        public bool RingVisible => false; // RED stub
        public float PinScale => 1f; // RED stub
        public bool IsVisible => gameObject.activeSelf;
        public string TitleText => ""; // RED stub

        public static HomeScreenView Create(Transform canvasParent)
        {
            var go = new GameObject("HomeScreen");
            go.transform.SetParent(canvasParent, false);
            return go.AddComponent<HomeScreenView>(); // RED stub
        }

        public void Attach(ChromeRegions regions, System.Func<bool> motionOff)
        {
            // RED stub
        }

        public void Show()
        {
            // RED stub
        }

        public void Hide()
        {
            // RED stub
        }
    }
}
