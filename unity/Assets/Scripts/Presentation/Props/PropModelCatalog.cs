using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Props
{
    // Authored props stay optional: Resources admits render-only prefabs, including the
    // original station, while absent entries keep their playable presentation fallback.
    public sealed class PropModelCatalog
    {
        public const string DepotShedId = "prop-depot-shed";
        public const string StationKioskId = "prop-station-kiosk";
        public const string TreesId = "prop-trees";
        public const string DeskClutterId = "prop-desk-clutter";
        public const string ToyEngineId = "prop-toy-engine";

        // LOOK step 5 furnish set, sourced from the Polyfork library. Like the five above,
        // the bytes are machine-local paid-account content; only the ids and presentation
        // corrections live in the repo.
        public const string FenceId = "prop-fence";
        public const string BushId = "prop-bush";
        public const string LampPostId = "prop-lamp-post";
        public const string SignpostId = "prop-signpost";
        public const string TrailSignpostId = "prop-trail-signpost";

        private const string ResourceRoot = "CatMetroProps/";
        public const string OriginalStationResourcePath = "CatMetroOriginal/Station";

        private static readonly HashSet<string> KnownIds = new HashSet<string>
        {
            DepotShedId, StationKioskId, TreesId, DeskClutterId, ToyEngineId,
            FenceId, BushId, LampPostId, SignpostId, TrailSignpostId,
        };

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        public static PropModelCatalog Empty { get; } = new PropModelCatalog(null);

        public int AdmittedEntryCount => _entries.Count;
        public int RejectedEntryCount { get; }

        public sealed class Entry
        {
            public Entry(string assetId, GameObject prefab, float displayScale, float facingYaw,
                Vector3 localOffset)
            {
                AssetId = assetId;
                Prefab = prefab;
                DisplayScale = displayScale;
                FacingYaw = facingYaw;
                LocalOffset = localOffset;
            }

            public string AssetId { get; }
            public GameObject Prefab { get; }
            public float DisplayScale { get; }
            public float FacingYaw { get; }
            public Vector3 LocalOffset { get; }
        }

        public PropModelCatalog(IEnumerable<Entry> entries)
        {
            if (entries == null) return;
            int rejected = 0;
            foreach (var entry in entries)
            {
                if (!CanAdmit(entry) || _entries.ContainsKey(entry.AssetId))
                {
                    rejected++;
                    continue;
                }
                _entries.Add(entry.AssetId, entry);
            }
            RejectedEntryCount = rejected;
        }

        public bool TryGet(string assetId, out Entry entry) =>
            _entries.TryGetValue(assetId, out entry);

        public static PropModelCatalog LoadResources()
        {
            // Per-model presentation corrections belong here; the licensed model bytes remain
            // pinned. The bake exports FBX as Y-up, and BoardPropDecorator supplies the shared
            // Y-up -> board-XY rotation separately.
            var entries = new List<Entry>
            {
                ResourceEntry(DepotShedId, 2.05f, 270f, Vector3.zero),
                StationResourceEntry(),
                ResourceEntry(TreesId, 1.65f, 90f, Vector3.zero),
                ResourceEntry(DeskClutterId, 1.7f, 90f, Vector3.zero),
                ResourceEntry(ToyEngineId, 1.55f, 90f, Vector3.zero),
            };

            // The Polyfork furnish set is a second optional batch: all five or none, so a
            // partially installed machine never shows a half-furnished board. Their FBX metres
            // are real-world (fence = 2 m), so DisplayScale converts metres to board units.
            //
            // FacingYaw, like the five above, is per-asset presentation data. The signposts'
            // plank faces sit at model +-Z and the lantern arm extends model +Z, so these turns
            // choose which face/arm the frontal diorama presents. The lamp
            // is 2.4 m tall with a 0.47 m head: 0.65 tops the 1.45-unit kiosk by ~8% and gives
            // the lantern head a readable 0.3-unit silhouette, against 1.08 units of bare
            // stick at the blind 0.45.
            var furnish = new[]
            {
                ResourceEntry(FenceId, 0.5f, 0f, Vector3.zero),
                ResourceEntry(BushId, 0.8f, 0f, Vector3.zero),
                ResourceEntry(LampPostId, 0.65f, 225f, Vector3.zero),
                ResourceEntry(SignpostId, 0.5f, 45f, Vector3.zero),
                ResourceEntry(TrailSignpostId, 0.5f, 90f, Vector3.zero),
            };
            bool furnishComplete = true;
            foreach (var entry in furnish)
                if (entry.Prefab == null) furnishComplete = false;
            if (furnishComplete) entries.AddRange(furnish);

            return new PropModelCatalog(entries);
        }

        private static Entry ResourceEntry(string assetId, float scale, float yaw, Vector3 offset) =>
            new Entry(assetId, Resources.Load<GameObject>(ResourceRoot + assetId), scale, yaw, offset);

        private static Entry StationResourceEntry()
        {
            var original = Resources.Load<GameObject>(OriginalStationResourcePath);
            // One optional original building replaces the kiosk slot. Other prop slots
            // keep their existing admission/rejection behavior and local provenance.
            // Original FBX front is +Z; 180 degrees faces it toward board -Y after
            // BoardPropDecorator's Y-up-to-board conversion. The 3.05-wide platform
            // becomes 1.281 board units without changing any live badge geometry.
            return CanRenderOriginalStation(original)
                ? new Entry(StationKioskId, original, .42f, 180f, Vector3.zero)
                : ResourceEntry(StationKioskId, 1.45f, 270f, Vector3.zero);
        }

        private static bool CanRenderOriginalStation(GameObject prefab)
        {
            if (prefab == null || !prefab.activeSelf) return false;
            foreach (string name in new[] { "Body", "RoofTint" })
            {
                Transform part = prefab.transform.Find(name);
                if (part == null || !part.gameObject.activeSelf) return false;
                MeshRenderer renderer = part.GetComponent<MeshRenderer>();
                MeshFilter filter = part.GetComponent<MeshFilter>();
                if (renderer == null || !renderer.enabled || filter == null
                    || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0
                    || filter.sharedMesh.subMeshCount == 0)
                    return false;
                bool hasTriangles = false;
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    hasTriangles |= filter.sharedMesh.GetTopology(submesh) == MeshTopology.Triangles
                        && filter.sharedMesh.GetIndexCount(submesh) >= 3;
                if (!hasTriangles) return false;
            }
            return true;
        }

        private static bool CanAdmit(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.AssetId)
                || !KnownIds.Contains(entry.AssetId) || entry.Prefab == null
                || !(entry.DisplayScale > 0f) || float.IsInfinity(entry.DisplayScale)
                || float.IsNaN(entry.DisplayScale))
                return false;

            // These two named parts replace the complete logical station building.
            // A material alone cannot establish that either part is actually visible.
            if (entry.AssetId == StationKioskId
                && (entry.Prefab.transform.Find("Body") != null || entry.Prefab.transform.Find("RoofTint") != null)
                && !CanRenderOriginalStation(entry.Prefab))
                return false;

            var renderers = entry.Prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) return false;
                foreach (var material in materials)
                    if (material == null || material.shader == null
                        || material.shader.name != "Universal Render Pipeline/Lit"
                        || !material.HasProperty("_BaseMap")
                        || material.GetTexture("_BaseMap") == null)
                        return false;
            }
            foreach (var component in entry.Prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) return false;
                if (component is Transform || component is MeshFilter || component is MeshRenderer
                    || component is SkinnedMeshRenderer || component is LODGroup)
                    continue;
                return false;
            }
            return true;
        }
    }
}
