using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Presentation.Cats;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // CM-C2b criterion 1: one greybox view object per authored board element, carrying the
    // authored id at the authored grid coordinate. Colour PLUS symbol from the start
    // (A-C2b-3: colour-alone encoding is a later merge-gate failure; full art is out of scope).
    // Presentation NEVER simulates (criterion 6): it reads session state + Alpha and places
    // primitives; the lever shows the COMMITTED route (state route + pending toggles) so a tap
    // is visible on its first rendered frame while the sim applies it at the boundary.
    public sealed class BoardView : MonoBehaviour
    {
        // CM-UX-03: the onboarding teach affordance — a static raised ring behind every switch
        // disc plus a scale pulse, band-gated INSIDE Build (survives Retry's rebuild by
        // construction; live via the existing composition call). The affordance marks the
        // VERB SURFACE, never the answer (deriving route correctness is solver territory —
        // contract A-UX3-1). Clears per switch on its first command in the session log; the
        // ring is ALWAYS static (shape carries the information; motion is removable — the
        // MotionOffSource binding is CM-UX-07's, tests bind directly).
        public System.Func<bool> MotionOffSource;

        private Transform[] _teachRing;   // static shape twin — never animated
        private Transform[] _teachDisc;   // the pulsing disc transforms
        private Vector3 _teachDiscBaseScale;
        private bool[] _teachCleared;

        public bool TeachAffordancePresent(int switchIndex)
        {
            return _teachRing != null && _teachRing[switchIndex] != null
                && _teachRing[switchIndex].gameObject.activeSelf;
        }

        private GameSession _session;
        private string[] _nodeIds;
        private Vector3[] _nodePos;
        private int[] _edgeFrom;
        private int[] _edgeTo;
        private int[] _edgeTravel;
        private int[][] _switchRouteTargetNode; // per switch, per route: target node index
        private int[] _switchNode;
        private Transform[] _switchArm;
        private readonly Dictionary<int, GameObject> _trains = new Dictionary<int, GameObject>();

        // CM-CATS-WIRE: one identity holder per bounded train slot, created with the slot's
        // capsule and reused for the view's whole life — the slot pool IS the model pool, so a
        // warm frame allocates nothing. The catalog independently guards the combined twelve;
        // this surface guards its own nine.
        private readonly Dictionary<int, CatModelInstance> _catSlots =
            new Dictionary<int, CatModelInstance>();
        private CatModelCatalog _catalog;

        // Placement CONSTANTS, never measurements: the contract forbids positioning against a
        // model's base or bounds, so the board states the authored convention it assumes rather
        // than reading geometry back at runtime. The board set is authored ~1.9 units tall and
        // origin-centred, and it replaces a capsule 0.7 units tall (2 units inside the 0.35 slot
        // scale), so this holder scale lands a cat at a comparable on-board size.
        private const float CatHolderScale = 1.2f;
        // The board camera is ORTHOGRAPHIC and the cats are unlit, so flattening the view axis
        // is visually free — and it is what lets a cat sit cleanly in front of the node cube it
        // stands on instead of intersecting it. The offset moves the whole slab forward past
        // that cube's front face.
        private const float CatDepthSquash = 0.35f;
        private static readonly Vector3 CatModelOffset = new Vector3(0f, 0f, -1f);
        // A dead-on cat is a weak silhouette; a slight turn reads as a cat at a glance, which is
        // the actual acceptance criterion. Yaw only — the board's read is a flat XY plane.
        private const float CatYawDegrees = -22f;

        public int SwitchCount => _switchNode.Length;
        public string NodeId(int nodeIndex) => _nodeIds[nodeIndex];
        public Vector3 NodeWorldPos(int nodeIndex) => transform.TransformPoint(_nodePos[nodeIndex]);
        public Vector3 SwitchWorldPos(int switchIndex) =>
            transform.TransformPoint(_nodePos[_switchNode[switchIndex]]); // F11: world, not local

        public static BoardView Build(ImportedLevel level, Transform parent, GameSession session)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view._session = session;
            // CM-CATS-WIRE: resolved ONCE, from the scene root this view already shares with
            // the home screen. Null is the ordinary case (A4) — the ignored derivatives are not
            // in a clean clone, so a CI runner and a fresh checkout both boot to capsules.
            view._catalog = CatModelCatalog.FindFor(go.transform);
            view.BuildElements(level);
            return view;
        }

        private void BuildElements(ImportedLevel level)
        {
            var dto = level.Dto;
            var nodes = dto.Nodes.ToArray();
            var edges = dto.Edges.ToArray();
            var nodeIndex = new Dictionary<string, int>();
            _nodePos = new Vector3[nodes.Length];
            _nodeIds = new string[nodes.Length];

            var sourceIds = new HashSet<string>();
            foreach (var s in dto.Sources.ToArray()) sourceIds.Add(s.NodeId);
            var stationAccept = new Dictionary<string, string>();
            foreach (var s in dto.Stations.ToArray())
                stationAccept[s.NodeId] = s.Accepts.Length > 0 ? s.Accepts.Span[0] : "";

            for (int i = 0; i < nodes.Length; i++)
            {
                nodeIndex[nodes[i].Id] = i;
                _nodeIds[i] = nodes[i].Id;
                _nodePos[i] = new Vector3(nodes[i].X, nodes[i].Y, 0f);
                string kind = sourceIds.Contains(nodes[i].Id) ? "source"
                    : stationAccept.ContainsKey(nodes[i].Id) ? "station" : "node";
                var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prim.name = kind + ":" + nodes[i].Id;
                prim.transform.SetParent(transform, false);
                prim.transform.localPosition = _nodePos[i];
                prim.transform.localScale = Vector3.one * 0.6f;
                var id = prim.AddComponent<BoardElementId>();
                id.Id = nodes[i].Id; id.Kind = kind;
                var renderer = prim.GetComponent<Renderer>();
                renderer.sharedMaterial = GreyboxMaterial.Shared;
                if (kind == "station")
                {
                    renderer.material.color = ColorFor(stationAccept[nodes[i].Id]);
                    var symbol = new GameObject("Symbol").AddComponent<TextMesh>();
                    symbol.transform.SetParent(prim.transform, false);
                    symbol.transform.localPosition = new Vector3(0f, 0f, -1f);
                    symbol.characterSize = 0.3f;
                    symbol.anchor = TextAnchor.MiddleCenter;
                    // symbol half of the triple coding: first letter of the accepted colour
                    symbol.text = stationAccept[nodes[i].Id].Length > 0
                        ? stationAccept[nodes[i].Id].Substring(0, 1).ToUpperInvariant() : "?";
                }
                else if (kind == "source") renderer.material.color = new Color(0.25f, 0.25f, 0.25f);
                else renderer.material.color = new Color(0.7f, 0.7f, 0.7f);
            }

            _edgeFrom = new int[edges.Length];
            _edgeTo = new int[edges.Length];
            _edgeTravel = new int[edges.Length];
            var edgeIndex = new Dictionary<string, int>();
            for (int i = 0; i < edges.Length; i++)
            {
                edgeIndex[edges[i].Id] = i;
                _edgeFrom[i] = nodeIndex[edges[i].From];
                _edgeTo[i] = nodeIndex[edges[i].To];
                _edgeTravel[i] = edges[i].TravelTicks;
                var a = _nodePos[_edgeFrom[i]];
                var b = _nodePos[_edgeTo[i]];
                var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prim.GetComponent<Renderer>().sharedMaterial = GreyboxMaterial.Shared;
                prim.name = "edge:" + edges[i].Id;
                prim.transform.SetParent(transform, false);
                prim.transform.localPosition = (a + b) * 0.5f + new Vector3(0f, 0f, 0.2f);
                prim.transform.localScale = new Vector3(0.12f, (b - a).magnitude, 0.12f);
                prim.transform.up = (b - a).normalized;
                var id = prim.AddComponent<BoardElementId>();
                id.Id = edges[i].Id; id.Kind = "edge";
            }

            var switches = dto.Switches.ToArray();
            _switchNode = new int[switches.Length];
            _switchArm = new Transform[switches.Length];
            _switchRouteTargetNode = new int[switches.Length][];
            for (int s = 0; s < switches.Length; s++)
            {
                _switchNode[s] = nodeIndex[switches[s].NodeId];
                var routes = switches[s].Routes.ToArray();
                _switchRouteTargetNode[s] = new int[routes.Length];
                for (int r = 0; r < routes.Length; r++)
                    _switchRouteTargetNode[s][r] = _edgeTo[edgeIndex[routes[r]]];

                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.GetComponent<Renderer>().sharedMaterial = GreyboxMaterial.Shared;
                disc.name = "switch:" + switches[s].Id;
                disc.transform.SetParent(transform, false);
                disc.transform.localPosition = _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.4f);
                disc.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var id = disc.AddComponent<BoardElementId>();
                id.Id = switches[s].Id; id.Kind = "switch";

                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.GetComponent<Renderer>().sharedMaterial = GreyboxMaterial.Shared;
                arm.name = "arm";
                arm.transform.SetParent(disc.transform.parent, false);
                arm.transform.localScale = new Vector3(0.1f, 0.9f, 0.1f);
                _switchArm[s] = arm.transform;

                // CM-UX-03: onboarding-band teach affordance — a STATIC raised ring behind
                // the disc (shape carries the information; no BoardElementId, so the merged
                // render-fidelity inventory is untouched) plus the disc pulse driven in
                // UpdateFrom. Band-gated HERE so Retry's rebuild re-teaches by construction.
                if (level.Dto.Meta.Band == "onboarding")
                {
                    if (_teachRing == null)
                    {
                        _teachRing = new Transform[switches.Length];
                        _teachDisc = new Transform[switches.Length];
                        _teachCleared = new bool[switches.Length];
                        _teachDiscBaseScale = disc.transform.localScale;
                    }
                    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    // Human ruling 2026-08-06 (#36 review finding 2): the ring carries the
                    // motion-off information, so it must READ as a ring — a distinct darker
                    // tint (the chrome ink-navy), one static cached instance of the greybox
                    // shader (same pipeline, no new Resources entry, no per-retry leak).
                    ring.GetComponent<Renderer>().sharedMaterial = TeachRingMaterial();
                    ring.name = "teachring:" + switches[s].Id;
                    ring.transform.SetParent(transform, false);
                    ring.transform.localPosition =
                        _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.35f);
                    ring.transform.localScale = new Vector3(0.8f, 0.04f, 0.8f);
                    ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    _teachRing[s] = ring.transform;
                    _teachDisc[s] = disc.transform;
                }
            }
            RefreshSwitches();
        }

        private static Material _teachRingMat; // one cached tinted instance per domain

        // The DEVFIX criterion-5 static gate counts CreatePrimitive calls against
        // GreyboxMaterial binds one-to-one — this helper deliberately names the provider
        // EXACTLY once (the ring's bind), copying its material so the shader (and the gate's
        // live shader-equality walk) stay identical while the tint differentiates the ring.
        private static Material TeachRingMaterial()
        {
            if (_teachRingMat == null)
            {
                var basis = GreyboxMaterial.Shared;
                if (basis == null) return null; // the provider already logged loudly
                _teachRingMat = new Material(basis);
                _teachRingMat.color = new Color(0.13f, 0.19f, 0.29f); // the chrome ink-navy
            }
            return _teachRingMat;
        }

        // The teach tick: clear keys on ANY command for the switch in the session log (a
        // toggle-back never re-teaches); the pulse reads render-side time only and mutates
        // the cached transform — zero allocation, zero sim contact. Reads the CALLER's
        // session (review F9) so a future multi-session caller cannot key teach state to a
        // stale log.
        private void UpdateTeach(GameSession session)
        {
            if (_teachRing == null) return;
            bool motionOff = MotionOffSource != null && MotionOffSource();
            var entries = session.Log.Entries;
            for (int s = 0; s < _teachRing.Length; s++)
            {
                if (_teachCleared[s]) continue;
                bool commanded = false;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].SwitchId == s) { commanded = true; break; }
                if (commanded)
                {
                    _teachCleared[s] = true;
                    _teachRing[s].gameObject.SetActive(false);
                    _teachDisc[s].localScale = _teachDiscBaseScale;
                    continue;
                }
                _teachDisc[s].localScale = motionOff
                    ? _teachDiscBaseScale
                    : _teachDiscBaseScale * (1f + 0.12f * Mathf.Sin(Time.time * 4f + s));
            }
        }

        // The COMMITTED route: authoritative state plus not-yet-applied toggles (criterion 3a).
        public int CommittedRoute(int switchIndex)
        {
            int routes = _switchRouteTargetNode[switchIndex].Length;
            return (_session.State.SwitchRoutes[switchIndex]
                + _session.PendingToggleCount(switchIndex)) % routes;
        }

        public void RefreshSwitches()
        {
            for (int s = 0; s < _switchArm.Length; s++)
            {
                var origin = transform.TransformPoint(
                    _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.4f)); // F11: world space
                var target = transform.TransformPoint(_nodePos[_switchRouteTargetNode[s][CommittedRoute(s)]]);
                var dir = (target - transform.TransformPoint(_nodePos[_switchNode[s]])).normalized;
                _switchArm[s].position = origin + dir * 0.5f;
                _switchArm[s].up = dir;
            }
        }

        public void UpdateFrom(GameSession session)
        {
            RefreshSwitches();
            UpdateTeach(session);
            float alpha = (float)session.Alpha;
            var trains = session.State.Trains;
            for (int t = 0; t < trains.Length; t++)
            {
                bool live = trains[t].Id != 0 && trains[t].State != CatMetro.Domain.TrainState.None;
                if (!live)
                {
                    if (_trains.TryGetValue(t, out var dead)) dead.SetActive(false);
                    continue;
                }
                if (!_trains.TryGetValue(t, out var go) || go == null)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.GetComponent<Renderer>().sharedMaterial = GreyboxMaterial.Shared;
                    go.name = "train:" + t;
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = Vector3.one * 0.35f;
                    var id = go.AddComponent<BoardElementId>();
                    id.Id = "train-" + t; id.Kind = "train";
                    _trains[t] = go;
                    // The holder is a CHILD of the capsule root: the root is a CreatePrimitive
                    // capsule and therefore carries a collider, and AC1's component wall is
                    // asserted over the marker's own subtree.
                    _catSlots[t] = CatModelInstance.CreateHolder(go.transform, false);
                }
                go.SetActive(true);
                var capsule = go.GetComponent<Renderer>();
                ApplyCatModel(t, capsule, trains[t].Color);
                // The colour cue belongs to whatever is actually visible. Under a resolved cat
                // the capsule renderer is off, so this `.material` write never happens and no
                // per-instance material clone is created (AC2's shared-asset limb).
                if (capsule.enabled)
                    capsule.material.color = ColorForCode(trains[t].Color);
                if (trains[t].State == CatMetro.Domain.TrainState.OnEdge)
                {
                    int e = trains[t].EdgeId;
                    float progress = Mathf.Min(1f, (trains[t].ProgressTicks + alpha) / _edgeTravel[e]);
                    go.transform.localPosition = Vector3.Lerp(
                        _nodePos[_edgeFrom[e]], _nodePos[_edgeTo[e]], progress)
                        + new Vector3(0f, 0f, -0.2f);
                }
                else
                {
                    go.transform.localPosition = _nodePos[trains[t].NodeId] + new Vector3(0f, 0f, -0.2f);
                }
            }
        }

        // CM-CATS-WIRE AC1/AC2, the board half. Every live slot records WHICH cat the closed
        // map named for its colour, whether or not that cat could be shown; a slot that cannot
        // resolve one keeps the exact capsule it had before, quietly. Nothing here reads or
        // writes simulation state (P-6) — CatColor stays the only input (A3).
        //
        // Warm cost is a dictionary lookup and a reference compare. A model is instantiated only
        // when a slot first resolves or when a recycled slot changes colour, never per frame.
        private void ApplyCatModel(int slot, Renderer capsule, byte color)
        {
            CatModelInstance marker;
            if (!_catSlots.TryGetValue(slot, out marker) || marker == null) return;

            string manifestId = CatModelManifestMap.BoardManifestId(color);
            if (marker.Model != null)
            {
                if (marker.ManifestId == manifestId) return; // warm and still correct
                // A bounded slot recycled onto another colour: hand the cat back rather than
                // leave a red tabby standing on a blue train, and free its share of the ceiling.
                if (_catalog != null) _catalog.Release(marker.Model);
                marker.RecordFallback(manifestId);
            }

            if (_catalog != null && BoardModelCount() < CatModelManifestMap.BoardInstanceLimit)
            {
                int triangles;
                float displayScale;
                var model = _catalog.Acquire(manifestId, marker.transform,
                    out triangles, out displayScale);
                if (model != null)
                {
                    // The squash lives on the UNROTATED holder so it stays aligned with the view
                    // axis; the yaw lives on the model. Putting both on one transform would tilt
                    // the flattening axis and skew the silhouette.
                    float side = CatHolderScale * displayScale;
                    marker.transform.localScale =
                        new Vector3(side, side, side * CatDepthSquash);
                    model.transform.localPosition = CatModelOffset;
                    // PRE-multiplied, never assigned: the prefab's own authored orientation is
                    // the asset's business (a model may be authored facing any way), and this
                    // only adds the board's presentation turn on top of it.
                    model.transform.localRotation = Quaternion.Euler(0f, CatYawDegrees, 0f)
                        * model.transform.localRotation;
                    marker.RecordModel(manifestId, model, triangles, displayScale);
                    capsule.enabled = false;
                    return;
                }
            }

            // Over the surface ceiling, no catalog, or no entry for this id: the ordinary
            // capsule, still coloured, still interpolated, still tappable exactly as before.
            marker.RecordFallback(manifestId);
            capsule.enabled = true;
        }

        // COUNTED, not tallied. A running counter drifts the moment something destroys a slot
        // out from under this view, and a drifted ceiling silently costs the board a cat that it
        // is entitled to. The walk is over the bounded train array (a handful of slots), it
        // allocates nothing, and it is reached only when a catalog exists and a slot is not
        // already warm — so a clean clone, the ordinary case, never pays for it at all.
        private int BoardModelCount()
        {
            int live = 0;
            foreach (var marker in _catSlots.Values)
                if (marker != null && marker.Model != null) live++;
            return live;
        }

        // Retry and LoadNext destroy this whole view and build a new one. Handing the cats back
        // here keeps the catalog's bounded budget honest across a rebuild rather than letting
        // each retry quietly spend another nine slots. (The catalog also prunes destroyed
        // instances defensively; this is the prompt, deterministic half of the same law.)
        private void OnDestroy()
        {
            if (_catalog == null) return;
            foreach (var marker in _catSlots.Values)
            {
                if (marker == null || marker.Model == null) continue;
                _catalog.Release(marker.Model);
            }
            _catSlots.Clear();
        }

        private static Color ColorFor(string name)
        {
            switch (name)
            {
                case "red": return new Color(0.85f, 0.2f, 0.2f);
                case "blue": return new Color(0.2f, 0.4f, 0.9f);
                case "yellow": return new Color(0.9f, 0.8f, 0.2f);
                case "green": return new Color(0.2f, 0.75f, 0.3f);
                default: return Color.magenta;
            }
        }

        private static Color ColorForCode(byte code)
        {
            switch (code)
            {
                case CatMetro.Domain.CatColor.Red: return new Color(0.85f, 0.2f, 0.2f);
                case CatMetro.Domain.CatColor.Blue: return new Color(0.2f, 0.4f, 0.9f);
                case CatMetro.Domain.CatColor.Yellow: return new Color(0.9f, 0.8f, 0.2f);
                case CatMetro.Domain.CatColor.Green: return new Color(0.2f, 0.75f, 0.3f);
                default: return Color.magenta;
            }
        }
    }
}
