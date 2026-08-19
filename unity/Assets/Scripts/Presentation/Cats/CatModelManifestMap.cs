using CatMetro.Domain;

namespace CatMetro.Presentation.Cats
{
    // CM-CATS-WIRE AC1/AC2: the CLOSED, case-sensitive mapping from a surface key to a
    // generated-cat manifest id, plus the frozen presentation budgets. Every row here is
    // contract text (state/handoffs/CM-CATS-WIRE-frozen-contract.md) — a changed row is a
    // contract amendment, not a code edit.
    //
    // Closed means closed: an unknown colour code, an unknown district name, or an id outside
    // the eight selected rows returns null, and null means "use this slot's existing visual."
    // It never means "pick a nearby cat" — a wrong cat on a colour-coded board is a GAMEPLAY
    // defect, not a cosmetic one (A3: CatColor stays the authority for board selection).
    //
    // The filenames name PR #94's decimated derivatives. They are documentation of WHICH bytes
    // a promoted reference must point at; nothing here loads a file, and nothing here may grow
    // into a runtime path (ADR-0007: direct references, no Resources/Addressables/parser).
    public static class CatModelManifestMap
    {
        // --- surface keys -----------------------------------------------------------------
        // The three non-interactive Home silhouettes. The interactive pins are deliberately
        // outside this map (contract "Exact surfaces"): they are not grey placeholders.
        public const string DistrictA = "ParkedDistrictA";
        public const string DistrictB = "ParkedDistrictB";
        public const string DistrictC = "ParkedDistrictC";

        // --- frozen budgets ---------------------------------------------------------------
        // AC2. Each surface owns its own ceiling; the combined figure is the sum, not a
        // separate allowance either surface may borrow against.
        public const int BoardInstanceLimit = 9;
        public const int HomeInstanceLimit = 3;
        public const int CombinedInstanceLimit = BoardInstanceLimit + HomeInstanceLimit;
        public const int PerModelTriangleLimit = 15000;
        public const int CombinedTriangleLimit = CombinedInstanceLimit * PerModelTriangleLimit;
        // Selected COMPRESSED source payload, not runtime memory (A5). The 2026-08-17
        // derivatives measure 17,434,232 bytes against this 20 MiB ceiling; that measurement is
        // evidence the ceiling holds, never an exact-byte pin.
        public const long SelectedSourceByteLimit = 20L * 1024L * 1024L;

        // --- the closed map ---------------------------------------------------------------

        // Board selection reads CatColor and nothing else (A3). Wild is mapped because the
        // domain reserves the code; an unmapped or future code falls back rather than
        // borrowing another colour's cat.
        public static string BoardManifestId(byte colorCode)
        {
            switch (colorCode)
            {
                case CatColor.Red: return "cat-red-tabby";
                case CatColor.Blue: return "cat-blue-siamese";
                case CatColor.Yellow: return "cat-yellow-longhair";
                case CatColor.Green: return "cat-green-shorthair";
                case CatColor.Wild: return "cat-wild-alley";
                default: return null; // CatColor.None and every unknown code
            }
        }

        // A null or unknown district name is a normal miss, not an argument error: callers walk
        // real scene node names and must never throw on one they do not recognise.
        public static string HomeManifestId(string districtName)
        {
            switch (districtName)
            {
                case DistrictA: return "cat-red-tabby-sitting";
                case DistrictB: return "cat-blue-siamese-loaf";
                case DistrictC: return "cat-conductor";
                default: return null;
            }
        }

        // The eight selected derivatives, spelled out rather than composed, so the table reads
        // as the frozen table it is. `cat-yellow-longhair-wave` is deliberately absent: it is
        // outside this slice (its exclusion is contract, so it stays excluded even now that the
        // asset's detached fragment has been corrected — widening the slice is an amendment).
        // C# string switch is ordinal, so "CAT-RED-TABBY" misses: manifest ids are
        // case-sensitive.
        public static string DerivativeFilename(string manifestId)
        {
            switch (manifestId)
            {
                case "cat-red-tabby": return "cat-red-tabby.glb";
                case "cat-blue-siamese": return "cat-blue-siamese.glb";
                case "cat-yellow-longhair": return "cat-yellow-longhair.glb";
                case "cat-green-shorthair": return "cat-green-shorthair.glb";
                case "cat-wild-alley": return "cat-wild-alley.glb";
                case "cat-red-tabby-sitting": return "cat-red-tabby-sitting.glb";
                case "cat-blue-siamese-loaf": return "cat-blue-siamese-loaf.glb";
                case "cat-conductor": return "cat-conductor.glb";
                default: return null;
            }
        }

        // The catalog's admission test: an id nothing in the closed map names can never be
        // resolved, however it reached the catalog.
        public static bool IsSelected(string manifestId)
        {
            return DerivativeFilename(manifestId) != null;
        }
    }
}
