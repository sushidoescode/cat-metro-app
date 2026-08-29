using UnityEngine;

namespace CatMetro.Presentation.Props
{
    // Render-only identity used for deterministic placement/capture checks. Deliberately not a
    // BoardElementId: scenery must never participate in simulation or input inventories.
    [DisallowMultipleComponent]
    public sealed class BoardPropInstance : MonoBehaviour
    {
        public string AssetId { get; internal set; }
        public string Role { get; internal set; }
        public string AnchorId { get; internal set; }

        // Read by the camera's horizontal fit and by the safe-frame law, which must agree.
        // See PropRole for why the split exists and what each side of it costs.
        public bool IsDecorative => PropRole.IsDecorative(Role);
    }
}
