using UnityEngine;
using CatMetro.Presentation.Input;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criteria 6/7: the minimal LevelIntro sheet — the piece that makes the game
    // explain itself before play. Level name + goal line (intro.goal template with {count}
    // substituted — the component receives the KEY plus injected data, never a literal:
    // the BannerView substitution precedent) + an explicit full-width Play CTA in the
    // safe-area thumb band (S-05's spec'd interaction; the §1.1 primary-CTA law).
    // Tap-anywhere dismissal is NOT built — the Play chip is the sheet's ONLY registered
    // region, so a tap outside it does nothing by construction. Render-only (P-1): the hit
    // routes through the injected ChromeRegions; R1-F3 lifetime law honored (unregister on
    // Hide AND OnDestroy). Star thresholds / best score stay deferred (decompose §5).
    public sealed class LevelIntroSheet : MonoBehaviour
    {
        public System.Action PlayRequested;

        public string NameText => ""; // RED stub
        public string GoalText => ""; // RED stub
        public string PlayText => ""; // RED stub
        public Rect PlayChipRectPx => default; // RED stub
        public bool IsVisible => gameObject.activeSelf;

        public static LevelIntroSheet Create(Transform canvasParent)
        {
            var go = new GameObject("LevelIntroSheet");
            go.transform.SetParent(canvasParent, false);
            return go.AddComponent<LevelIntroSheet>(); // RED stub
        }

        public void Attach(ChromeRegions regions)
        {
            // RED stub
        }

        public void Show(string levelName, int deliveries)
        {
            // RED stub
        }

        public void Hide()
        {
            // RED stub
        }
    }
}
