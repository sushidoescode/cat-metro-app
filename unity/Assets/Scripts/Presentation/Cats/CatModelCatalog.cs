using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Cats
{
    // CM-CATS-WIRE AC1/AC2: the presentation-owned, direct-reference cat catalog.
    //
    // ADR-0007 posture, restated as code: the only way a model reaches a surface is a SERIALIZED
    // reference authored into this component. There is no Resources.Load, no Addressables, no
    // runtime glTF parser, no file or network read, and no static asset cache. A build whose
    // scene carries no catalog therefore has no cats — which is the ORDINARY case, not an error
    // (A4): the derivatives are local, ignored files, so a clean clone and a CI runner both boot
    // straight into the existing placeholders.
    //
    // Admission is deliberately strict, because a catalog is the one place a wrong or unsafe
    // asset could enter the frame. An entry is admitted only if every one of these holds:
    //   * its id is one of the eight rows of the closed map;
    //   * it names a real prefab;
    //   * that prefab is render-only (the AC1 component wall, enforced by construction rather
    //     than by stripping components off somebody else's asset);
    //   * its declared triangle count is positive and within the per-model ceiling; and
    //   * admitting it keeps the selected source payload inside the frozen byte budget.
    // A duplicate id admits NEITHER copy and permanently blocks that id: "which of these two is
    // the blue siamese" has no safe answer, and guessing is exactly the arbitrary-cat failure
    // the contract forbids. Every rejection is SILENT and simply leaves that slot on its
    // existing visual (absence is quiet and normal); RejectedEntryCount is the diagnostic.
    public sealed class CatModelCatalog : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Entry
        {
            // Authored in the inspector against the frozen map. Promotion of the real
            // derivatives into referenceable assets is its own human-gated custody work
            // (licence/promotion gate); this component is only the seam that receives them.
            public string ManifestId;
            public GameObject Prefab;
            public int TriangleCount;
            // The SELECTED .glb's compressed size on disk. Hand-declared, never measured: the
            // custody rules forbid this component from reading a file at all, so admission can
            // only check the declared figure against the frozen budget. It is spent against
            // nothing at spawn — a wrong number mis-reports UniqueSourceBytes and can wrongly
            // refuse a later entry, but it can never put a wrong cat on screen. Author it from
            // the decimation metrics record.
            public long SourceBytes;
            // AC2's "readable silhouettes" limb needs one authored knob. The generated set does
            // NOT share a size convention — the board cats stand ~1.9 units tall while two of
            // the three Home cats are normalised to ~1.0 — and the contract forbids deriving
            // placement from a model's bounds. So how big this cat should READ is authored here
            // beside the reference, exactly like the reference itself. 0 means "not authored"
            // and is treated as 1; a serialized float has no other way to spell "unset".
            public float DisplayScale;
        }

        [SerializeField] private Entry[] _authored;

        private Dictionary<string, Entry> _byId;
        private HashSet<string> _blocked;
        private long _uniqueSourceBytes;
        private int _rejectedEntries;

        private struct Live
        {
            public GameObject Instance;
            public int Triangles;
        }

        // The budget is held as the actual live instances, not as a counter. A counter would
        // drift the first time something else destroys a surface — and something else does:
        // Retry and LoadNext both destroy the whole board view and rebuild it. Pruning against
        // reality means a destroyed cat can never keep spending the ceiling, whatever order the
        // teardown happens in.
        private readonly List<Live> _live = new List<Live>();

        private void Prune()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i].Instance == null) _live.RemoveAt(i);
        }

        // Live instances the catalog has handed out and not taken back, across BOTH surfaces.
        public int ActiveModelInstanceCount { get { Prune(); return _live.Count; } }

        // Declared triangles currently spawned. Bounded by CombinedTriangleLimit at Acquire.
        public int ActiveTriangleCount
        {
            get
            {
                Prune();
                int total = 0;
                for (int i = 0; i < _live.Count; i++) total += _live[i].Triangles;
                return total;
            }
        }

        // Source payload counted ONCE per admitted entry. Spawning a second cat from the same
        // entry shares that entry's prefab assets and must not move this number (AC2).
        public long UniqueSourceBytes { get { EnsureIndex(); return _uniqueSourceBytes; } }

        // Authored entries the admission rules turned away. Read-back rather than a log line:
        // an absent or unusable entry is a supported state and must not print an error.
        public int RejectedEntryCount { get { EnsureIndex(); return _rejectedEntries; } }

        public int AdmittedEntryCount { get { EnsureIndex(); return _byId.Count; } }

        // Discovery: the common scene root the board view and the home screen already share.
        // Callers resolve ONCE and cache; there is no global, so two roots (a test fixture and
        // a booted game, say) never see each other's catalog.
        public static CatModelCatalog FindFor(Transform anchor)
        {
            if (anchor == null) return null;
            var root = anchor.root;
            if (root == null) return null;
            return root.GetComponentInChildren<CatModelCatalog>(true);
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, Entry>(System.StringComparer.Ordinal);
            _blocked = new HashSet<string>(System.StringComparer.Ordinal);
            if (_authored == null) return;
            for (int i = 0; i < _authored.Length; i++) Admit(_authored[i]);
        }

        private bool Admit(Entry entry)
        {
            if (entry == null) { _rejectedEntries++; return false; }
            string id = entry.ManifestId;
            if (!CatModelManifestMap.IsSelected(id)) { _rejectedEntries++; return false; }

            if (_blocked.Contains(id)) { _rejectedEntries++; return false; }
            if (_byId.ContainsKey(id))
            {
                // The second copy poisons the first: an ambiguous id resolves to fallback.
                var first = _byId[id];
                _byId.Remove(id);
                _blocked.Add(id);
                _uniqueSourceBytes -= first.SourceBytes;
                _rejectedEntries += 2;
                return false;
            }

            if (entry.Prefab == null) { _rejectedEntries++; return false; }
            if (entry.TriangleCount <= 0
                || entry.TriangleCount > CatModelManifestMap.PerModelTriangleLimit)
            {
                _rejectedEntries++;
                return false;
            }
            if (entry.SourceBytes < 0
                || _uniqueSourceBytes + entry.SourceBytes
                    > CatModelManifestMap.SelectedSourceByteLimit)
            {
                _rejectedEntries++;
                return false;
            }
            if (!IsRenderOnly(entry.Prefab)) { _rejectedEntries++; return false; }

            _byId[id] = entry;
            _uniqueSourceBytes += entry.SourceBytes;
            return true;
        }

        // AC1's component wall. Imported model hierarchies must add no physics body, no UGUI
        // input surface and no animation driver: input stays owned by the existing tap/region
        // paths, and the pulse law says motion is code-driven easing. Checked here, once, over
        // the whole prefab including inactive children — never by deleting components from an
        // asset at spawn time.
        private static bool IsRenderOnly(GameObject prefab)
        {
            return prefab.GetComponentsInChildren<Collider>(true).Length == 0
                && prefab.GetComponentsInChildren<Rigidbody>(true).Length == 0
                && prefab.GetComponentsInChildren<Selectable>(true).Length == 0
                && prefab.GetComponentsInChildren<GraphicRaycaster>(true).Length == 0
                && prefab.GetComponentsInChildren<Animator>(true).Length == 0
                && prefab.GetComponentsInChildren<Animation>(true).Length == 0;
        }

        // The in-memory direct-reference seam the phase-1 suite installs tiny prefabs through,
        // so no test needs a copy of an ignored generated model to exercise this wiring.
        internal bool RegisterForTests(string manifestId, GameObject prefab,
            int triangleCount, long sourceBytes)
        {
            EnsureIndex();
            return Admit(new Entry
            {
                ManifestId = manifestId,
                Prefab = prefab,
                TriangleCount = triangleCount,
                SourceBytes = sourceBytes,
                DisplayScale = 1f,
            });
        }

        private static float SafeDisplayScale(Entry entry)
        {
            return entry.DisplayScale > 0f ? entry.DisplayScale : 1f;
        }

        // Returns null for every unresolvable case — no catalog entry, an id outside the closed
        // map, a blocked duplicate, or a combined ceiling already reached. Null is the caller's
        // instruction to keep the slot's existing visual, and it is never an error.
        // Instantiating from a prefab reference SHARES that prefab's meshes and materials; the
        // caller must not write to `.material` on the result, which would clone them.
        public GameObject Acquire(string manifestId, Transform parent,
            out int triangleCount, out float displayScale)
        {
            triangleCount = 0;
            displayScale = 1f;
            EnsureIndex();
            if (string.IsNullOrEmpty(manifestId)) return null;
            Entry entry;
            if (!_byId.TryGetValue(manifestId, out entry)) return null;
            if (entry.Prefab == null) return null;
            if (ActiveModelInstanceCount >= CatModelManifestMap.CombinedInstanceLimit) return null;
            if (ActiveTriangleCount + entry.TriangleCount
                > CatModelManifestMap.CombinedTriangleLimit) return null;

            var instance = Instantiate(entry.Prefab, parent, false);
            instance.name = CatModelInstance.HolderName + ":" + manifestId;
            instance.SetActive(true);
            _live.Add(new Live { Instance = instance, Triangles = entry.TriangleCount });
            triangleCount = entry.TriangleCount;
            displayScale = SafeDisplayScale(entry);
            return instance;
        }

        // Hands an instance back so its budget is freed for another slot. Used when a bounded
        // train slot is recycled onto a different colour: the old cat must not linger under the
        // new one, and its share of the ceiling must not stay spent.
        public void Release(GameObject instance)
        {
            if (instance == null) return;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i].Instance == instance) _live.RemoveAt(i);
            if (UnityEngine.Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }
    }
}
