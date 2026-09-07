using CatMetro.Presentation.Cosmetics;
using UnityEngine;

namespace CatMetro.Presentation.Cats
{
    /// <summary>Home's facing and diagnostic identity on the shared profile rig mount.</summary>
    public sealed class HomeProfileRigView : ProfileRigMount
    {
        public const float HomeFacingYaw = -20f;

        public static HomeProfileRigView Create(RectTransform holder,
            CosmeticPortraitView portrait, CatModelCatalog catalog)
            => CreateMount<HomeProfileRigView>(holder, portrait, catalog, HomeFacingYaw,
                "HOME_RIG", "HomeProfileRigMount");

        // Home intentionally keeps the original portrait tree when admission fails, so this
        // warning must happen at the call site, before a mount component can exist.
        public static void ReportUnavailable(CatModelCatalog catalog)
            => Debug.LogWarning("HOME_RIG fallback branch=5 admitted="
                + (catalog?.AdmittedEntryCount ?? 0) + " reason="
                + (catalog?.RejectionReason ?? "missing catalog"));
    }
}
