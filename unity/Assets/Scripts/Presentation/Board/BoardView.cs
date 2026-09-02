using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Presentation.Props;
using CatMetro.Presentation.Theme;
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
        private TrackSplineGraph _trackPaths;
        private int[][] _switchRouteTargetNode; // per switch, per route: target node index
        private int[] _switchNode;
        private Transform[] _switchArm;
        private readonly Dictionary<int, GameObject> _trains = new Dictionary<int, GameObject>();

        public int SwitchCount => _switchNode.Length;
        public string NodeId(int nodeIndex) => _nodeIds[nodeIndex];
        public Vector3 NodeWorldPos(int nodeIndex) => transform.TransformPoint(_nodePos[nodeIndex]);
        public Vector3 PresentationCenterLocal
        {
            get
            {
                if (_nodePos == null || _nodePos.Length == 0) return Vector3.zero;
                float minX = _nodePos[0].x, maxX = _nodePos[0].x;
                float minY = _nodePos[0].y, maxY = _nodePos[0].y;
                for (int i = 1; i < _nodePos.Length; i++)
                {
                    minX = Mathf.Min(minX, _nodePos[i].x);
                    maxX = Mathf.Max(maxX, _nodePos[i].x);
                    minY = Mathf.Min(minY, _nodePos[i].y);
                    maxY = Mathf.Max(maxY, _nodePos[i].y);
                }
                return new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            }
        }
        public Vector3 SwitchWorldPos(int switchIndex) =>
            transform.TransformPoint(_nodePos[_switchNode[switchIndex]]); // F11: world, not local

        public static BoardView Build(ImportedLevel level, Transform parent, GameSession session,
            PropModelCatalog propCatalog = null)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view._session = session;
            view.BuildElements(level);
            BoardSurface.Build(level, view.transform);
            BoardPropDecorator.Decorate(level, view.transform,
                propCatalog ?? PropModelCatalog.LoadResources(), view.PropAnchorLocalPosition);
            return view;
        }

        // The prop lane resolves through NodeWorldPos so the scene lane's visual-node transform
        // remains the single source of truth after its isometric/tabletop pass lands.
        private Vector3 PropAnchorLocalPosition(string nodeId)
        {
            for (int i = 0; i < _nodeIds.Length; i++)
                if (_nodeIds[i] == nodeId)
                {
                    var position = transform.InverseTransformPoint(NodeWorldPos(i));
                    position.z = BoardPropDecorator.ResolveContactPlaneLocalZ(transform);
                    return position;
                }
            throw new System.ArgumentException("unknown prop anchor " + nodeId);
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
            }
            _trackPaths = TrackSplineGraph.Build(_nodePos, _edgeFrom, _edgeTo);
            for (int i = 0; i < edges.Length; i++)
                ToyTrackMeshBuilder.Build(edges[i].Id, _trackPaths.Path(i), transform);

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
                }
                go.SetActive(true);
                go.GetComponent<Renderer>().material.color = ColorForCode(trains[t].Color);
                if (trains[t].State == CatMetro.Domain.TrainState.OnEdge
                    || trains[t].State == CatMetro.Domain.TrainState.OnEdgeReverse)
                {
                    int e = trains[t].EdgeId;
                    float progress = Mathf.Min(1f, (trains[t].ProgressTicks + alpha) / _edgeTravel[e]);
                    if (trains[t].State == CatMetro.Domain.TrainState.OnEdgeReverse)
                        progress = 1f - progress;
                    go.transform.localPosition = _trackPaths.Path(e).EvaluateDistanceFraction(progress)
                        + new Vector3(0f, 0f, -0.2f);
                }
                else
                {
                    go.transform.localPosition = _nodePos[trains[t].NodeId] + new Vector3(0f, 0f, -0.2f);
                }
            }
        }

        private static Color ColorFor(string name)
        {
            switch (name)
            {
                case "red": return Palette.SignalRed;
                case "blue": return Palette.HarborBlue;
                case "yellow": return Palette.TabbyYellow;
                case "green": return Palette.GardenGreen;
                default: return Color.magenta;
            }
        }

        private static Color ColorForCode(byte code)
        {
            switch (code)
            {
                case CatMetro.Domain.CatColor.Red: return Palette.SignalRed;
                case CatMetro.Domain.CatColor.Blue: return Palette.HarborBlue;
                case CatMetro.Domain.CatColor.Yellow: return Palette.TabbyYellow;
                case CatMetro.Domain.CatColor.Green: return Palette.GardenGreen;
                default: return Color.magenta;
            }
        }
    }
}
