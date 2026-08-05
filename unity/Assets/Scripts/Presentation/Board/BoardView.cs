using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Content;
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
                disc.name = "switch:" + switches[s].Id;
                disc.transform.SetParent(transform, false);
                disc.transform.localPosition = _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.4f);
                disc.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var id = disc.AddComponent<BoardElementId>();
                id.Id = switches[s].Id; id.Kind = "switch";

                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = "arm";
                arm.transform.SetParent(disc.transform.parent, false);
                arm.transform.localScale = new Vector3(0.1f, 0.9f, 0.1f);
                _switchArm[s] = arm.transform;
            }
            RefreshSwitches();
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
                    go.name = "train:" + t;
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = Vector3.one * 0.35f;
                    var id = go.AddComponent<BoardElementId>();
                    id.Id = "train-" + t; id.Kind = "train";
                    _trains[t] = go;
                }
                go.SetActive(true);
                go.GetComponent<Renderer>().material.color = ColorForCode(trains[t].Color);
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
