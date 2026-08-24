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
    }
}
