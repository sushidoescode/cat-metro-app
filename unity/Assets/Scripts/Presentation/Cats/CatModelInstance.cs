using UnityEngine;

namespace CatMetro.Presentation.Cats
{
    // CM-CATS-WIRE AC1: the per-slot identity/read-back component. Exactly one of these sits
    // under every wired surface — a board train root and each mapped Home district — whether or
    // not a model resolved. That is the point: "no cat here" is a RECORDED state carrying the
    // manifest id that was wanted, not an absence you have to infer from a missing object.
    //
    // It is a marker and a read-back seam. It owns no policy: the surfaces decide what to show,
    // and the catalog decides what may be spawned at all.
    public sealed class CatModelInstance : MonoBehaviour
    {
        // Avoids every substring on the Home session-1 structural tripwire's banned list.
        public const string HolderName = "CatModel";

        // The manifest id the closed map named for this slot. Populated even when the model is
        // unavailable, so a fallback frame still says WHICH cat was missing.
        public string ManifestId { get; private set; }

        // True whenever this slot is showing its pre-existing visual: no catalog, no entry for
        // this id, a surface already at its ceiling, or an unmapped key. Every slot in a clean
        // clone is here, and that is a correct build, not a degraded one.
        public bool UsesFallback { get; private set; }

        // The catalog's declared triangle count for the resolved model; zero on fallback.
        public int TriangleCount { get; private set; }

        // The authored size multiplier for this entry (see CatModelCatalog.Entry). Kept so a
        // surface that re-lays-out — Home, on a resolution change — can re-derive its fit
        // without asking the catalog again.
        public float DisplayScale { get; private set; }

        // The asset's authored facing correction, in degrees of yaw. Kept for read-back and
        // diagnostics; it is applied once at spawn, not re-applied per layout.
        public float FacingYaw { get; private set; }

        // The spawned instance, or null on fallback. Held so a slot whose mapping changes can
        // hand the old instance back to the catalog instead of leaking it.
        public GameObject Model { get; private set; }

        // The holder is always a CHILD, never the surface object itself: a board train root is a
        // CreatePrimitive capsule and therefore carries a collider, and AC1's component-safety
        // wall is asserted over the marker's own subtree.
        //
        // Two shapes because the two surfaces live in different spaces. The board holder is a
        // plain Transform under world geometry. The Home holder is a zero-size RectTransform
        // pinned to its district's centre, so the district's own rect — never the model's
        // geometry — is what the fit is computed from.
        internal static CatModelInstance CreateHolder(Transform parent, bool asRect)
        {
            var go = new GameObject(HolderName);
            if (asRect)
            {
                var rect = go.AddComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition3D = Vector3.zero;
            }
            else
            {
                go.transform.SetParent(parent, false);
            }
            return go.AddComponent<CatModelInstance>();
        }

        internal void RecordFallback(string manifestId)
        {
            ManifestId = manifestId;
            UsesFallback = true;
            TriangleCount = 0;
            DisplayScale = 1f;
            FacingYaw = 0f;
            Model = null;
            transform.localScale = Vector3.one;
        }

        internal void RecordModel(string manifestId, GameObject model,
            CatModelCatalog.Placement placement)
        {
            ManifestId = manifestId;
            UsesFallback = false;
            TriangleCount = placement.TriangleCount;
            DisplayScale = placement.DisplayScale;
            FacingYaw = placement.FacingYaw;
            Model = model;
        }
    }
}
