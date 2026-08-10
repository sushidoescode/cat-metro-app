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
        private Vector3[] _visualNodePos;
        private int[] _edgeFrom;
        private int[] _edgeTo;
        private int[] _edgeTravel;
        private int[][] _switchRouteTargetNode; // per switch, per route: target node index
        private int[] _switchNode;
        private Transform[] _switchArm;
        private readonly Dictionary<int, GameObject> _trains = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, byte> _trainVisualColors =
            new Dictionary<int, byte>();

        public const float DioramaVerticalScale = 1.75f;
        public const float DioramaVerticalCenter = 5f;

        public int SwitchCount => _switchNode.Length;
        public string NodeId(int nodeIndex) => _nodeIds[nodeIndex];
        public Vector3 NodeWorldPos(int nodeIndex) =>
            transform.TransformPoint(_visualNodePos[nodeIndex]);
        public Vector3 SwitchWorldPos(int switchIndex) =>
            transform.TransformPoint(_visualNodePos[_switchNode[switchIndex]]); // F11: world

        // Preserve authored logical roots for the render-fidelity contract, but expand the
        // sparse tutorial graph along the board's long axis. Only visual children, tap anchors,
        // and train interpolation use this tabletop composition transform.
        public static Vector3 DioramaPoint(Vector3 logical)
        {
            logical.y = DioramaVerticalCenter
                + (logical.y - DioramaVerticalCenter) * DioramaVerticalScale;
            return logical;
        }

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
            _visualNodePos = new Vector3[nodes.Length];
            _nodeIds = new string[nodes.Length];

            var sourceIds = new HashSet<string>();
            foreach (var s in dto.Sources.ToArray()) sourceIds.Add(s.NodeId);
            var stationAccept = new Dictionary<string, string>();
            foreach (var s in dto.Stations.ToArray())
                stationAccept[s.NodeId] = s.Accepts.Length > 0 ? s.Accepts.Span[0] : "";

            BuildEnvironment();

            for (int i = 0; i < nodes.Length; i++)
            {
                nodeIndex[nodes[i].Id] = i;
                _nodeIds[i] = nodes[i].Id;
                _nodePos[i] = new Vector3(nodes[i].X, nodes[i].Y, 0f);
                _visualNodePos[i] = DioramaPoint(_nodePos[i]);
                string kind = sourceIds.Contains(nodes[i].Id) ? "source"
                    : stationAccept.ContainsKey(nodes[i].Id) ? "station" : "node";
                var prim = new GameObject(kind + ":" + nodes[i].Id);
                prim.name = kind + ":" + nodes[i].Id;
                prim.transform.SetParent(transform, false);
                prim.transform.localPosition = _nodePos[i];
                var id = prim.AddComponent<BoardElementId>();
                id.Id = nodes[i].Id; id.Kind = kind;
                if (kind == "station")
                {
                    BuildStation(prim, _visualNodePos[i] - _nodePos[i],
                        LineIdentity.ForName(stationAccept[nodes[i].Id]));
                }
                else if (kind == "source")
                    BuildDepot(prim, _visualNodePos[i] - _nodePos[i]);
                else BuildJunction(prim, _visualNodePos[i] - _nodePos[i]);
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
                var visualA = _visualNodePos[_edgeFrom[i]];
                var visualB = _visualNodePos[_edgeTo[i]];
                var prim = new GameObject("edge:" + edges[i].Id);
                prim.name = "edge:" + edges[i].Id;
                prim.transform.SetParent(transform, false);
                prim.transform.localPosition = (a + b) * 0.5f + new Vector3(0f, 0f, 0.2f);
                var id = prim.AddComponent<BoardElementId>();
                id.Id = edges[i].Id; id.Kind = "edge";
                var trackVisual = new GameObject("track-visual:" + edges[i].Id);
                trackVisual.transform.SetParent(prim.transform, false);
                trackVisual.transform.localPosition =
                    (visualA + visualB) * 0.5f + new Vector3(0f, 0f, 0.2f)
                    - prim.transform.localPosition;
                trackVisual.transform.up = (visualB - visualA).normalized;
                BuildTrack(trackVisual.transform, edges[i].Id,
                    (visualB - visualA).magnitude);
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

                var disc = new GameObject("switch:" + switches[s].Id);
                DioramaMeshFactory.Attach(disc, DioramaMeshKind.Cylinder,
                    DioramaPalette.Material("lever-teal", DioramaPalette.MetroTeal));
                disc.name = "switch:" + switches[s].Id;
                disc.transform.SetParent(transform, false);
                disc.transform.localPosition =
                    _visualNodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.4f);
                disc.transform.localScale = new Vector3(0.5f, 0.18f, 0.5f);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var id = disc.AddComponent<BoardElementId>();
                id.Id = switches[s].Id; id.Kind = "switch";

                var baseVisual = DioramaMeshFactory.Create(disc.transform, "lever-base",
                    DioramaMeshKind.Cylinder,
                    DioramaPalette.Material("lever-base", DioramaPalette.MetroTeal));
                baseVisual.transform.localScale = new Vector3(0.82f, 0.7f, 0.82f);
                var keyline = DioramaMeshFactory.Create(disc.transform, "lever-keyline",
                    DioramaMeshKind.Cylinder,
                    DioramaPalette.Material("lever-keyline", DioramaPalette.InkNavy));
                keyline.transform.localPosition = new Vector3(0f, -0.08f, 0f);
                keyline.transform.localScale = new Vector3(1.16f, 0.22f, 1.16f);
                ContactShadow(disc.transform, transform.TransformPoint(
                    _visualNodePos[_switchNode[s]] + new Vector3(0.04f, -0.05f, 0.25f)),
                    new Vector2(0.78f, 0.56f));

                var arm = new GameObject("arm");
                DioramaMeshFactory.Attach(arm, DioramaMeshKind.RoundedBox,
                    DioramaPalette.Material("lever-arm", DioramaPalette.TicketOrange));
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
                    var ring = new GameObject("teachring:" + switches[s].Id);
                    DioramaMeshFactory.Attach(ring, DioramaMeshKind.Cylinder,
                        TeachRingMaterial());
                    // Human ruling 2026-08-06 (#36 review finding 2): the ring carries the
                    // motion-off information, so it must READ as a ring — a distinct darker
                    // tint (the chrome ink-navy), one static cached instance of the greybox
                    // shader (same pipeline, no new Resources entry, no per-retry leak).
                    ring.GetComponent<Renderer>().sharedMaterial = TeachRingMaterial();
                    ring.name = "teachring:" + switches[s].Id;
                    ring.transform.SetParent(transform, false);
                    ring.transform.localPosition =
                        _visualNodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.35f);
                    ring.transform.localScale = new Vector3(0.8f, 0.04f, 0.8f);
                    ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    _teachRing[s] = ring.transform;
                    _teachDisc[s] = disc.transform;
                }
            }
            RefreshSwitches();
        }

        private void BuildEnvironment()
        {
            var table = Shape(transform, "desk:table", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("desk-cream-card", DioramaPalette.CreamCard),
                new Vector3(3f, 5f, 0.73f), new Vector3(12f, 38f, 0.28f));
            table.transform.localRotation = Quaternion.Euler(0f, 0f, -0.8f);

            Shape(transform, "desk:front-edge", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("desk-front-edge", DioramaPalette.CreamCard),
                DioramaPoint(new Vector3(3f, -0.1f, 0.48f)),
                new Vector3(6.7f, 0.48f, 0.55f));

            var surface = Shape(transform, "desk:surface", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("board-warm-paper", DioramaPalette.WarmPaper),
                DioramaPoint(new Vector3(3f, 5.6f, 0.43f)),
                new Vector3(6.55f, 11.3f * DioramaVerticalScale, 0.34f));
            surface.transform.localRotation = Quaternion.Euler(0f, 0f, -0.55f);

            Shape(transform, "desk:bevel", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("desk-bevel", DioramaPalette.DepotNavy),
                DioramaPoint(new Vector3(3f, 11.25f, 0.22f)),
                new Vector3(6.72f, 0.18f, 0.24f));

            // Restrained, semi-transparent navy inlays read as grain only in the exposed desk
            // margin: the closer Cream Card game board naturally depth-occludes their centre.
            Color grain = DioramaPalette.InkNavy;
            grain.a = 0.22f;
            for (int i = 0; i < 15; i++)
            {
                for (int segment = 0; segment < 3; segment++)
                {
                    float width = 2.4f + ((i + segment * 2) % 4) * 0.42f;
                    float x = -1.25f + segment * 4.25f
                        + ((i * 17 + segment * 7) % 9) * 0.09f;
                    var inlay = Shape(transform,
                        "desk:grain-" + i + ":" + segment, DioramaMeshKind.RoundedBox,
                        DioramaPalette.Material("desk-grain", grain),
                        new Vector3(x, -9f + i * 2f + segment * 0.18f, 0.57f),
                        new Vector3(width, 0.035f, 0.025f));
                    inlay.transform.localRotation = Quaternion.Euler(0f, 0f,
                        ((i + segment) % 3 - 1) * 2.5f);
                }
                if ((i & 3) == 1)
                {
                    var knot = Shape(transform, "desk:grain-knot-" + i,
                        DioramaMeshKind.Cylinder,
                        DioramaPalette.Material("desk-grain", grain),
                        new Vector3(0.4f + (i % 3) * 2.7f, -8.8f + i * 2f, 0.555f),
                        new Vector3(0.18f, 0.018f, 0.3f));
                    knot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }
            for (int knot = 0; knot < 2; knot++)
            {
                var foregroundKnot = Shape(transform, "desk:grain-knot-foreground-" + knot,
                    DioramaMeshKind.Cylinder,
                    DioramaPalette.Material("desk-grain", grain),
                    new Vector3(2.15f + knot * 2.35f, -4.45f, 0.555f),
                    new Vector3(0.16f, 0.018f, 0.28f));
                foregroundKnot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            var tree = new GameObject("prop:tree");
            tree.transform.SetParent(transform, false);
            tree.transform.localPosition = DioramaPoint(new Vector3(0.15f, 8.35f, 0.28f));
            ContactShadow(tree.transform, tree.transform.TransformPoint(
                new Vector3(0.1f, -0.54f, -0.03f)), new Vector2(0.82f, 0.34f));
            Shape(tree.transform, "tree:trunk", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("tree-trunk", DioramaPalette.DepotNavy),
                new Vector3(0f, -0.38f, 0f), new Vector3(0.22f, 0.72f, 0.35f));
            Shape(tree.transform, "tree:crown-low", DioramaMeshKind.Sphere,
                DioramaPalette.Material("tree-teal", DioramaPalette.MetroTeal),
                new Vector3(0f, 0.12f, -0.12f), new Vector3(0.95f, 1.12f, 0.62f));
            Shape(tree.transform, "tree:crown-high", DioramaMeshKind.Sphere,
                DioramaPalette.Material("tree-teal", DioramaPalette.MetroTeal),
                new Vector3(0.1f, 0.72f, -0.18f), new Vector3(0.68f, 0.9f, 0.52f));

            var fence = new GameObject("prop:fence");
            fence.transform.SetParent(transform, false);
            fence.transform.localPosition = DioramaPoint(new Vector3(5.75f, 8.9f, 0.28f));
            ContactShadow(fence.transform, fence.transform.TransformPoint(
                new Vector3(0.08f, -0.24f, -0.03f)), new Vector2(1.35f, 0.4f));
            for (int i = 0; i < 3; i++)
                Shape(fence.transform, "fence:post-" + i, DioramaMeshKind.RoundedBox,
                    DioramaPalette.Material("fence-cream", DioramaPalette.CreamCard),
                    new Vector3((i - 1) * 0.48f, 0f, -0.05f), new Vector3(0.1f, 1.15f, 0.22f));
            for (int i = 0; i < 2; i++)
                Shape(fence.transform, "fence:rail-" + i, DioramaMeshKind.RoundedBox,
                    DioramaPalette.Material("fence-navy", DioramaPalette.InkNavy),
                    new Vector3(0f, -0.3f + i * 0.58f, -0.02f),
                    new Vector3(1.15f, 0.09f, 0.22f));

            var cup = new GameObject("prop:desk-cup");
            cup.transform.SetParent(transform, false);
            cup.transform.localPosition = new Vector3(5.55f, -4.2f, 0.3f);
            cup.transform.localScale = Vector3.one * 0.9f;
            ContactShadow(cup.transform, cup.transform.TransformPoint(
                new Vector3(0.05f, -0.08f, -0.03f)), new Vector2(1.08f, 0.58f));
            var cupBody = Shape(cup.transform, "cup:body", DioramaMeshKind.Cylinder,
                DioramaPalette.Material("cup-cream", DioramaPalette.WarmPaper),
                Vector3.zero, new Vector3(0.52f, 0.12f, 0.52f));
            cupBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shape(cup.transform, "cup:coffee", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cup-coffee", DioramaPalette.DepotNavy),
                new Vector3(0f, 0f, -0.09f), new Vector3(0.38f, 0.38f, 0.04f));
            Shape(cup.transform, "cup:handle", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("cup-cream", DioramaPalette.WarmPaper),
                new Vector3(0.38f, 0f, 0f), new Vector3(0.24f, 0.12f, 0.12f));
        }

        private static void BuildJunction(GameObject root, Vector3 visualOffset)
        {
            var visual = new GameObject("junction:visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = visualOffset;
            DioramaMeshFactory.Attach(visual, DioramaMeshKind.Cylinder,
                DioramaPalette.Material("junction", DioramaPalette.CreamCard));
            visual.transform.localScale = new Vector3(0.34f, 0.07f, 0.34f);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void BuildDepot(GameObject root, Vector3 visualOffset)
        {
            string id = root.name.Substring(root.name.IndexOf(':') + 1);
            var depot = new GameObject("depot:" + id);
            depot.transform.SetParent(root.transform, false);
            depot.transform.localPosition = visualOffset + new Vector3(0f, 0.2f, 0f);
            ContactShadow(depot.transform, depot.transform.TransformPoint(
                new Vector3(0.1f, -0.3f, 0.24f)), new Vector2(1.42f, 0.46f));
            Shape(depot.transform, "depot:body", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("depot-body", DioramaPalette.CreamCard),
                Vector3.zero, new Vector3(1.25f, 0.82f, 0.42f));
            var roof = Shape(depot.transform, "depot:roof", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("depot-roof", DioramaPalette.DepotNavy),
                new Vector3(0f, 0.53f, -0.1f), new Vector3(1.42f, 0.24f, 0.46f));
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            Shape(depot.transform, "depot:door", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("depot-door", DioramaPalette.MetroTeal),
                new Vector3(0f, -0.18f, -0.26f), new Vector3(0.45f, 0.42f, 0.1f));
            Shape(depot.transform, "depot:lintel", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("depot-lintel", DioramaPalette.TicketOrange),
                new Vector3(0f, 0.11f, -0.3f), new Vector3(0.68f, 0.1f, 0.07f));
        }

        private static void BuildStation(GameObject root, Vector3 visualOffset, LineIdentity line)
        {
            var visual = new GameObject("station:visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = visualOffset;
            DioramaMeshFactory.Attach(visual, DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("station-paper", DioramaPalette.WarmPaper));
            visual.transform.localScale = new Vector3(1.28f, 0.62f, 0.34f);
            ContactShadow(visual.transform,
                visual.transform.position + new Vector3(0f, -0.18f, 0.25f),
                new Vector2(1.5f, 0.72f));
            var tag = root.AddComponent<LineVisualTag>();
            tag.Apply(line, LineVisualRole.Station);

            Shape(visual.transform, "station:keyline", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("station-keyline", DioramaPalette.InkNavy),
                new Vector3(0f, -0.62f, 0.2f), new Vector3(1.16f, 0.16f, 0.12f));
            Shape(visual.transform, "station:plate", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("line-" + line.SymbolId, line.Color),
                new Vector3(0f, 0f, -0.58f), new Vector3(0.62f, 0.6f, 0.08f));
            var symbol = DioramaMeshFactory.CreateSymbol(visual.transform,
                "station:symbol-" + line.SymbolId, line,
                DioramaPalette.Material("station-symbol", DioramaPalette.WarmPaper));
            symbol.transform.localPosition = new Vector3(0f, 0f, -0.7f);
            symbol.transform.localScale = new Vector3(0.2f, 0.38f, 1f);

            var canopy = Shape(visual.transform, "station:canopy", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("station-canopy", DioramaPalette.CreamCard),
                new Vector3(0f, 0.7f, -0.08f), new Vector3(1.28f, 0.18f, 0.34f));
            canopy.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        }

        private static void BuildTrack(Transform root, string id, float length)
        {
            Shape(root, "trackbed:" + id, DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("track-cream", DioramaPalette.CreamCard),
                Vector3.zero, new Vector3(0.72f, length, 0.22f));
            Shape(root, "rail-left:" + id, DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("rail-navy", DioramaPalette.InkNavy),
                new Vector3(-0.23f, 0f, -0.16f), new Vector3(0.08f, length, 0.14f));
            Shape(root, "rail-right:" + id, DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("rail-navy", DioramaPalette.InkNavy),
                new Vector3(0.23f, 0f, -0.16f), new Vector3(0.08f, length, 0.14f));
            int tieCount = Mathf.Max(2, Mathf.CeilToInt(length / 0.55f));
            for (int i = 0; i < tieCount; i++)
            {
                float y = tieCount == 1 ? 0f : Mathf.Lerp(-length * 0.45f,
                    length * 0.45f, i / (float)(tieCount - 1));
                Shape(root, "tie:" + id + ":" + i, DioramaMeshKind.RoundedBox,
                    DioramaPalette.Material("rail-navy", DioramaPalette.InkNavy),
                    new Vector3(0f, y, -0.13f), new Vector3(0.58f, 0.08f, 0.11f));
            }
        }

        private GameObject BuildCommuter(int index, byte colorCode)
        {
            LineIdentity line = LineIdentity.For(colorCode);
            var train = new GameObject("train:" + index);
            train.transform.SetParent(transform, false);
            var tag = train.AddComponent<LineVisualTag>();
            tag.Apply(line, LineVisualRole.Commuter);

            Shape(train.transform, "train:car-floor", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("car-floor", DioramaPalette.WarmPaper),
                new Vector3(0f, 0f, -0.18f), new Vector3(0.62f, 0.78f, 0.12f));
            Shape(train.transform, "train:car-side-left", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("car-cream", DioramaPalette.CreamCard),
                new Vector3(-0.31f, 0f, -0.35f), new Vector3(0.1f, 0.78f, 0.3f));
            Shape(train.transform, "train:car-side-right", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("car-cream", DioramaPalette.CreamCard),
                new Vector3(0.31f, 0f, -0.35f), new Vector3(0.1f, 0.78f, 0.3f));
            Shape(train.transform, "train:car-end-front", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("car-cream", DioramaPalette.CreamCard),
                new Vector3(0f, -0.4f, -0.35f), new Vector3(0.72f, 0.1f, 0.3f));
            Shape(train.transform, "train:car-end-back", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("car-navy-trim", DioramaPalette.InkNavy),
                new Vector3(0f, 0.4f, -0.35f), new Vector3(0.72f, 0.1f, 0.3f));

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                Shape(train.transform, "train:wheel-" + x + ":" + y, DioramaMeshKind.Sphere,
                    DioramaPalette.Material("car-wheel", DioramaPalette.DepotNavy),
                    new Vector3(x * 0.29f, y * 0.27f, -0.12f),
                    new Vector3(0.14f, 0.14f, 0.1f));

            float headWidth;
            float headHeight;
            float earHeight;
            float earSpread;
            switch (line.SilhouetteId)
            {
                case "round-tabby":
                    headWidth = 0.23f; headHeight = 0.22f; earHeight = 0.15f; earSpread = 0.08f;
                    break;
                case "slim-siamese":
                    headWidth = 0.16f; headHeight = 0.27f; earHeight = 0.22f; earSpread = 0.07f;
                    break;
                case "fluffy-longhair":
                    headWidth = 0.245f; headHeight = 0.23f; earHeight = 0.17f; earSpread = 0.1f;
                    break;
                case "sleek-shorthair":
                    headWidth = 0.19f; headHeight = 0.21f; earHeight = 0.14f; earSpread = 0.075f;
                    break;
                default:
                    headWidth = 0.24f; headHeight = 0.24f; earHeight = 0.2f; earSpread = 0.095f;
                    break;
            }

            Shape(train.transform, "cat:chest", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-" + line.SymbolId, line.Color),
                new Vector3(0f, -0.06f, -0.48f), new Vector3(0.18f, 0.2f, 0.18f));
            Shape(train.transform, "cat:head", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-" + line.SymbolId, line.Color),
                new Vector3(0f, 0.04f, -0.62f),
                new Vector3(headWidth, headHeight, 0.22f));
            var ears = new GameObject("cat:ears");
            ears.transform.SetParent(train.transform, false);
            var leftEar = Shape(ears.transform, "ear:left", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("cat-ear", line.Color),
                new Vector3(-earSpread, 0.18f, -0.91f),
                new Vector3(0.075f, earHeight, 0.07f));
            leftEar.transform.localRotation = Quaternion.Euler(0f, 0f,
                line.SilhouetteId == "bent-ear-scruffy" ? 42f : -18f);
            var rightEar = Shape(ears.transform, "ear:right", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("cat-ear", line.Color),
                new Vector3(earSpread, 0.18f, -0.91f),
                new Vector3(0.075f, earHeight, 0.07f));
            rightEar.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var leftInner = Shape(ears.transform, "ear:left-inner", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("cat-inner-ear", DioramaPalette.CreamCard),
                new Vector3(-earSpread, 0.18f, -0.97f),
                new Vector3(0.035f, earHeight * 0.55f, 0.025f));
            leftInner.transform.localRotation = leftEar.transform.localRotation;
            var rightInner = Shape(ears.transform, "ear:right-inner", DioramaMeshKind.RoundedBox,
                DioramaPalette.Material("cat-inner-ear", DioramaPalette.CreamCard),
                new Vector3(earSpread, 0.18f, -0.97f),
                new Vector3(0.035f, earHeight * 0.55f, 0.025f));
            rightInner.transform.localRotation = rightEar.transform.localRotation;

            var face = new GameObject("cat:face");
            face.transform.SetParent(train.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.02f, -0.88f);
            Shape(face.transform, "eye:left", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-face", DioramaPalette.InkNavy),
                new Vector3(-headWidth * 0.3f, 0.035f, 0f), new Vector3(0.032f, 0.04f, 0.025f));
            Shape(face.transform, "eye:right", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-face", DioramaPalette.InkNavy),
                new Vector3(headWidth * 0.3f, 0.035f, 0f), new Vector3(0.032f, 0.04f, 0.025f));
            Shape(face.transform, "nose", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-nose", DioramaPalette.TicketOrange),
                new Vector3(0f, -0.035f, -0.01f), new Vector3(0.04f, 0.035f, 0.025f));
            for (int i = -1; i <= 1; i += 2)
            {
                var whisker = Shape(face.transform, "whisker:" + i, DioramaMeshKind.RoundedBox,
                    DioramaPalette.Material("cat-face", DioramaPalette.InkNavy),
                    new Vector3(i * headWidth * 0.34f, -0.06f, 0.01f),
                    new Vector3(0.09f, 0.014f, 0.018f));
                whisker.transform.localRotation = Quaternion.Euler(0f, 0f, i * -9f);
            }

            Shape(train.transform, "cat:paw-left", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-paws", DioramaPalette.CreamCard),
                new Vector3(-0.1f, -0.16f, -0.62f), new Vector3(0.07f, 0.04f, 0.03f));
            Shape(train.transform, "cat:paw-right", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-paws", DioramaPalette.CreamCard),
                new Vector3(0.1f, -0.16f, -0.62f), new Vector3(0.07f, 0.04f, 0.03f));

            ContactShadow(train.transform, train.transform.TransformPoint(
                new Vector3(0.04f, -0.02f, 0.45f)), new Vector2(0.86f, 0.62f));

            var badge = Shape(train.transform, "cat:badge", DioramaMeshKind.Cylinder,
                DioramaPalette.Material("cat-badge-" + line.SymbolId, line.Color),
                new Vector3(0f, -0.29f, -0.54f), new Vector3(0.22f, 0.035f, 0.22f));
            badge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var symbol = DioramaMeshFactory.CreateSymbol(train.transform,
                "cat:symbol-" + line.SymbolId, line,
                DioramaPalette.Material("cat-symbol", DioramaPalette.WarmPaper));
            symbol.transform.localPosition = new Vector3(0f, -0.29f, -0.59f);
            symbol.transform.localScale = new Vector3(0.085f, 0.085f, 1f);

            if (line.SilhouetteId == "fluffy-longhair" ||
                line.SilhouetteId == "bent-ear-scruffy")
            {
                Shape(train.transform, "cat:ruff", DioramaMeshKind.Sphere,
                    DioramaPalette.Material("cat-ruff", line.Color),
                    new Vector3(0f, -0.08f, -0.76f),
                    new Vector3(headWidth * 1.25f, 0.13f, 0.15f));
            }
            return train;
        }

        private static GameObject ContactShadow(
            Transform owner, Vector3 worldPosition, Vector2 worldSize)
        {
            Color tint = DioramaPalette.DepotNavy;
            tint.a = 0.32f;
            var shadow = new GameObject("contact-shadow");
            DioramaMeshFactory.Attach(shadow, DioramaMeshKind.SoftShadow,
                DioramaPalette.Material("contact-shadow-soft", tint));
            shadow.transform.position = worldPosition;
            shadow.transform.rotation = Quaternion.identity;
            shadow.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
            shadow.transform.SetParent(owner, true);
            shadow.GetComponent<Renderer>().sortingOrder = -10;
            return shadow;
        }

        private static GameObject Shape(Transform parent, string name, DioramaMeshKind kind,
            Material material, Vector3 position, Vector3 scale)
        {
            var go = DioramaMeshFactory.Create(parent, name, kind, material);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            return go;
        }

        private static Material _teachRingMat; // one cached tinted instance per domain

        private static Material TeachRingMaterial()
        {
            if (_teachRingMat == null)
            {
                _teachRingMat = DioramaPalette.Material("teach-ring", DioramaPalette.InkNavy);
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
                    _visualNodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.4f));
                var target = transform.TransformPoint(
                    _visualNodePos[_switchRouteTargetNode[s][CommittedRoute(s)]]);
                var dir = (target - transform.TransformPoint(
                    _visualNodePos[_switchNode[s]])).normalized;
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
                bool hasVisual = _trains.TryGetValue(t, out var go) && go != null;
                bool identityChanged = hasVisual &&
                    (!_trainVisualColors.TryGetValue(t, out byte visualColor)
                        || visualColor != trains[t].Color);
                if (!hasVisual || identityChanged)
                {
                    if (go != null)
                    {
                        go.SetActive(false);
                        Object.Destroy(go);
                    }
                    go = BuildCommuter(t, trains[t].Color);
                    var id = go.AddComponent<BoardElementId>();
                    id.Id = "train-" + t; id.Kind = "train";
                    _trains[t] = go;
                    _trainVisualColors[t] = trains[t].Color;
                }
                go.SetActive(true);
                if (trains[t].State == CatMetro.Domain.TrainState.OnEdge)
                {
                    int e = trains[t].EdgeId;
                    float progress = Mathf.Min(1f, (trains[t].ProgressTicks + alpha) / _edgeTravel[e]);
                    go.transform.localPosition = Vector3.Lerp(
                        _visualNodePos[_edgeFrom[e]], _visualNodePos[_edgeTo[e]], progress)
                        + new Vector3(0f, 0f, -0.2f);
                    Vector3 direction =
                        _visualNodePos[_edgeTo[e]] - _visualNodePos[_edgeFrom[e]];
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        // Rotate strictly around the tabletop normal. Transform.up's 180-degree
                        // antiparallel case may choose X as its axis, which flips the car's Z
                        // height and buries a peeking cat below the carriage on return tracks.
                        float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                        go.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
                    }
                }
                else
                {
                    go.transform.localPosition =
                        _visualNodePos[trains[t].NodeId] + new Vector3(0f, 0f, -0.2f);
                }
            }
        }

        private static Color ColorFor(string name)
        {
            return LineIdentity.ForName(name).Color;
        }

        private static Color ColorForCode(byte code)
        {
            return LineIdentity.For(code).Color;
        }
    }
}
