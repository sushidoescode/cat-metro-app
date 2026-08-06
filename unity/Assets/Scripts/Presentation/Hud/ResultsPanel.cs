using UnityEngine;
using CatMetro.Presentation.Input;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-04 skeleton — red-phase target (the ChromeGeometry red-first precedent): the
    // public surface only, no behavior. The green commit fills it in. Contract:
    // state/handoffs/CM-UX-04-frozen-contract.md — Won-state results panel, one LOCKED Next
    // CTA hit-routed through the chrome-region registry, structurally-empty footer,
    // NextRequested as a seam ONLY (level advance is Bootstrap-owned; the panel is NOT
    // attached by CM-UX-07 until LoadNext exists — human answer Q-3).
    public sealed class ResultsPanel : MonoBehaviour
    {
        public const string RegionId = "results.next";
        public const int RegionPriority = 10; // explicit per A-UX1-3, never order-reliant

        // The seam: invoked when the registry routes a tap to the CTA region. Null-safe no-op
        // with no subscriber; the panel itself never advances anything.
        public System.Action NextRequested;

        public bool IsVisible => false;
        public GameObject PanelRoot => null;
        public Transform FooterRoot => null;
        public Rect ChipPaintedRectPx => new Rect(0f, 0f, 0f, 0f);
        public string CtaText => "";

        // The chip-rect LAW, pure and injectable (EditMode drives this; the runtime path
        // binds Screen.safeArea into the same function).
        public static Rect ChipRect(Rect safeArea) => new Rect(0f, 0f, 0f, 0f);

        public void Attach(System.Func<string> screenState, ChromeRegions regions) { }
    }
}
