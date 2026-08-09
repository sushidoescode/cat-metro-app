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

            BuildEnvironment();

            for (int i = 0; i < nodes.Length; i++)
            {
                nodeIndex[nodes[i].Id] = i;
                _nodeIds[i] = nodes[i].Id;
                _nodePos[i] = new Vector3(nodes[i].X, nodes[i].Y, 0f);
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
                    BuildStation(prim, LineIdentity.ForName(stationAccept[nodes[i].Id]));
                }
                else if (kind == "source") BuildDepot(prim);
                else BuildJunction(prim);
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
                var prim = new GameObject("edge:" + edges[i].Id);
                prim.name = "edge:" + edges[i].Id;
                prim.transform.SetParent(transform, false);
                prim.transform.localPosition = (a + b) * 0.5f + new Vector3(0f, 0f, 0.2f);
                prim.transform.up = (b - a).normalized;
                var id = prim.AddComponent<BoardElementId>();
                id.Id = edges[i].Id; id.Kind = "edge";
                BuildTrack(prim.transform, edges[i].Id, (b - a).magnitude);
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
                disc.transform.localPosition = _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.4f);
                disc.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
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

                var arm = new GameObject("arm");
                DioramaMeshFactory.Attach(arm, DioramaMeshKind.Cube,
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
                        _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.35f);
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
            var surface = Shape(transform, "desk:surface", DioramaMeshKind.Cube,
                DioramaPalette.Material("desk-warm-wood", DioramaPalette.TicketOrange),
                new Vector3(3f, 5.5f, 0.85f), new Vector3(7.2f, 15f, 0.18f));
            surface.transform.localRotation = Quaternion.Euler(0f, 0f, -1.25f);

            Shape(transform, "desk:bevel", DioramaMeshKind.Cube,
                DioramaPalette.Material("desk-bevel", DioramaPalette.DepotNavy),
                new Vector3(3f, 12.65f, 0.72f), new Vector3(7.45f, 0.2f, 0.25f));

            // A few broad, low-contrast inlays make the surface read as a wooden desk while
            // staying inside the authoritative product palette.
            for (int i = 0; i < 5; i++)
            {
                Shape(transform, "desk:grain-" + i, DioramaMeshKind.Cube,
                    DioramaPalette.Material("desk-grain", DioramaPalette.CreamCard),
                    new Vector3(3f, -0.5f + i * 3.05f, 0.73f),
                    new Vector3(6.7f, 0.035f, 0.04f));
            }

            var tree = new GameObject("prop:tree");
            tree.transform.SetParent(transform, false);
            tree.transform.localPosition = new Vector3(0.15f, 8.35f, 0.28f);
            Shape(tree.transform, "tree:shadow", DioramaMeshKind.Sphere,
                DioramaPalette.Material("contact-shadow", DioramaPalette.DepotNavy),
                new Vector3(0.12f, -0.58f, 0.2f), new Vector3(0.72f, 0.2f, 0.05f));
            Shape(tree.transform, "tree:trunk", DioramaMeshKind.Cube,
                DioramaPalette.Material("tree-trunk", DioramaPalette.TicketOrange),
                new Vector3(0f, -0.38f, 0f), new Vector3(0.22f, 0.72f, 0.14f));
            Shape(tree.transform, "tree:crown-low", DioramaMeshKind.Sphere,
                DioramaPalette.Material("tree-teal", DioramaPalette.MetroTeal),
                new Vector3(0f, 0.12f, -0.02f), new Vector3(0.95f, 1.12f, 0.16f));
            Shape(tree.transform, "tree:crown-high", DioramaMeshKind.Sphere,
                DioramaPalette.Material("tree-teal", DioramaPalette.MetroTeal),
                new Vector3(0.1f, 0.72f, -0.04f), new Vector3(0.68f, 0.9f, 0.16f));

            var fence = new GameObject("prop:fence");
            fence.transform.SetParent(transform, false);
            fence.transform.localPosition = new Vector3(5.75f, 8.9f, 0.28f);
            for (int i = 0; i < 3; i++)
                Shape(fence.transform, "fence:post-" + i, DioramaMeshKind.Cube,
                    DioramaPalette.Material("fence-cream", DioramaPalette.CreamCard),
                    new Vector3((i - 1) * 0.48f, 0f, 0f), new Vector3(0.1f, 1.15f, 0.12f));
            for (int i = 0; i < 2; i++)
                Shape(fence.transform, "fence:rail-" + i, DioramaMeshKind.Cube,
                    DioramaPalette.Material("fence-navy", DioramaPalette.InkNavy),
                    new Vector3(0f, -0.3f + i * 0.58f, -0.02f),
                    new Vector3(1.15f, 0.09f, 0.13f));

            var cup = new GameObject("prop:desk-cup");
            cup.transform.SetParent(transform, false);
            cup.transform.localPosition = new Vector3(5.85f, 0.35f, 0.18f);
            var cupBody = Shape(cup.transform, "cup:body", DioramaMeshKind.Cylinder,
                DioramaPalette.Material("cup-cream", DioramaPalette.WarmPaper),
                Vector3.zero, new Vector3(0.52f, 0.12f, 0.52f));
            cupBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Shape(cup.transform, "cup:coffee", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cup-coffee", DioramaPalette.DepotNavy),
                new Vector3(0f, 0f, -0.09f), new Vector3(0.38f, 0.38f, 0.04f));
            Shape(cup.transform, "cup:handle", DioramaMeshKind.Cube,
                DioramaPalette.Material("cup-cream", DioramaPalette.WarmPaper),
                new Vector3(0.38f, 0f, 0f), new Vector3(0.24f, 0.12f, 0.12f));
        }

        private static void BuildJunction(GameObject root)
        {
            DioramaMeshFactory.Attach(root, DioramaMeshKind.Cylinder,
                DioramaPalette.Material("junction", DioramaPalette.CreamCard));
            root.transform.localScale = new Vector3(0.34f, 0.07f, 0.34f);
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void BuildDepot(GameObject root)
        {
            string id = root.name.Substring(root.name.IndexOf(':') + 1);
            var depot = new GameObject("depot:" + id);
            depot.transform.SetParent(root.transform, false);
            depot.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            Shape(depot.transform, "depot:shadow", DioramaMeshKind.Sphere,
                DioramaPalette.Material("contact-shadow", DioramaPalette.DepotNavy),
                new Vector3(0.12f, -0.28f, 0.2f), new Vector3(0.96f, 0.24f, 0.05f));
            Shape(depot.transform, "depot:body", DioramaMeshKind.Cube,
                DioramaPalette.Material("depot-body", DioramaPalette.CreamCard),
                Vector3.zero, new Vector3(1.25f, 0.82f, 0.16f));
            var roof = Shape(depot.transform, "depot:roof", DioramaMeshKind.Cube,
                DioramaPalette.Material("depot-roof", DioramaPalette.DepotNavy),
                new Vector3(0f, 0.53f, -0.04f), new Vector3(1.42f, 0.24f, 0.2f));
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            Shape(depot.transform, "depot:door", DioramaMeshKind.Cube,
                DioramaPalette.Material("depot-door", DioramaPalette.MetroTeal),
                new Vector3(0f, -0.18f, -0.12f), new Vector3(0.45f, 0.42f, 0.08f));
            Shape(depot.transform, "depot:lintel", DioramaMeshKind.Cube,
                DioramaPalette.Material("depot-lintel", DioramaPalette.TicketOrange),
                new Vector3(0f, 0.11f, -0.15f), new Vector3(0.68f, 0.1f, 0.06f));
        }

        private static void BuildStation(GameObject root, LineIdentity line)
        {
            DioramaMeshFactory.Attach(root, DioramaMeshKind.Cube,
                DioramaPalette.Material("station-paper", DioramaPalette.WarmPaper));
            root.transform.localScale = new Vector3(1.28f, 0.62f, 0.15f);
            var tag = root.AddComponent<LineVisualTag>();
            tag.Apply(line, LineVisualRole.Station);

            Shape(root.transform, "station:keyline", DioramaMeshKind.Cube,
                DioramaPalette.Material("station-keyline", DioramaPalette.InkNavy),
                new Vector3(0f, -0.62f, 0.2f), new Vector3(1.16f, 0.16f, 0.12f));
            Shape(root.transform, "station:plate", DioramaMeshKind.Cube,
                DioramaPalette.Material("line-" + line.SymbolId, line.Color),
                new Vector3(0f, 0f, -0.58f), new Vector3(0.62f, 0.6f, 0.08f));
            var symbol = DioramaMeshFactory.CreateSymbol(root.transform,
                "station:symbol-" + line.SymbolId, line,
                DioramaPalette.Material("station-symbol", DioramaPalette.WarmPaper));
            symbol.transform.localPosition = new Vector3(0f, 0f, -0.7f);
            symbol.transform.localScale = new Vector3(0.2f, 0.38f, 1f);

            var canopy = Shape(root.transform, "station:canopy", DioramaMeshKind.Cube,
                DioramaPalette.Material("station-canopy", DioramaPalette.CreamCard),
                new Vector3(0f, 0.7f, 0f), new Vector3(1.28f, 0.18f, 0.2f));
            canopy.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        }

        private static void BuildTrack(Transform root, string id, float length)
        {
            Shape(root, "trackbed:" + id, DioramaMeshKind.Cube,
                DioramaPalette.Material("track-cream", DioramaPalette.CreamCard),
                Vector3.zero, new Vector3(0.72f, length, 0.13f));
            Shape(root, "rail-left:" + id, DioramaMeshKind.Cube,
                DioramaPalette.Material("rail-navy", DioramaPalette.InkNavy),
                new Vector3(-0.23f, 0f, -0.12f), new Vector3(0.08f, length, 0.08f));
            Shape(root, "rail-right:" + id, DioramaMeshKind.Cube,
                DioramaPalette.Material("rail-navy", DioramaPalette.InkNavy),
                new Vector3(0.23f, 0f, -0.12f), new Vector3(0.08f, length, 0.08f));
            int tieCount = Mathf.Max(2, Mathf.CeilToInt(length / 0.55f));
            for (int i = 0; i < tieCount; i++)
            {
                float y = tieCount == 1 ? 0f : Mathf.Lerp(-length * 0.45f,
                    length * 0.45f, i / (float)(tieCount - 1));
                Shape(root, "tie:" + id + ":" + i, DioramaMeshKind.Cube,
                    DioramaPalette.Material("rail-navy", DioramaPalette.InkNavy),
                    new Vector3(0f, y, -0.08f), new Vector3(0.58f, 0.08f, 0.07f));
            }
        }

        private GameObject BuildCommuter(int index, byte colorCode)
        {
            LineIdentity line = LineIdentity.For(colorCode);
            var cat = new GameObject("train:" + index);
            cat.transform.SetParent(transform, false);
            DioramaMeshFactory.Attach(cat, DioramaMeshKind.Capsule,
                DioramaPalette.Material("cat-" + line.SymbolId, line.Color));
            var tag = cat.AddComponent<LineVisualTag>();
            tag.Apply(line, LineVisualRole.Commuter);

            Vector3 bodyScale;
            switch (line.SilhouetteId)
            {
                case "round-tabby": bodyScale = new Vector3(0.54f, 0.68f, 0.16f); break;
                case "slim-siamese": bodyScale = new Vector3(0.38f, 0.82f, 0.14f); break;
                case "fluffy-longhair": bodyScale = new Vector3(0.7f, 0.72f, 0.2f); break;
                case "sleek-shorthair": bodyScale = new Vector3(0.44f, 0.74f, 0.13f); break;
                default: bodyScale = new Vector3(0.58f, 0.76f, 0.2f); break;
            }
            cat.transform.localScale = bodyScale;

            Shape(cat.transform, "cat:head", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-" + line.SymbolId, line.Color),
                new Vector3(0f, 0.55f, -0.12f), new Vector3(1.2f, 0.78f, 0.8f));
            var ears = new GameObject("cat:ears");
            ears.transform.SetParent(cat.transform, false);
            ears.transform.localPosition = new Vector3(0f, 1.32f, -0.16f);
            var leftEar = Shape(ears.transform, "ear:left", DioramaMeshKind.Cube,
                DioramaPalette.Material("cat-ear", line.Color),
                new Vector3(-0.38f, 0f, -2f), new Vector3(0.38f, 0.62f, 0.16f));
            leftEar.transform.localRotation = Quaternion.Euler(0f, 0f,
                line.SilhouetteId == "bent-ear-scruffy" ? 42f : -18f);
            var rightEar = Shape(ears.transform, "ear:right", DioramaMeshKind.Cube,
                DioramaPalette.Material("cat-ear", line.Color),
                new Vector3(0.38f, 0f, -2f), new Vector3(0.38f, 0.62f, 0.16f));
            rightEar.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);

            var tail = Shape(cat.transform, "cat:tail", DioramaMeshKind.Capsule,
                DioramaPalette.Material("cat-tail", line.Color),
                new Vector3(0.62f, -0.08f, 0.34f), new Vector3(0.18f, 0.7f, 0.12f));
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, -48f);

            Shape(cat.transform, "cat:belly", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-belly", DioramaPalette.CreamCard),
                new Vector3(0f, -0.14f, -2f), new Vector3(0.8f, 0.76f, 0.16f));

            var face = new GameObject("cat:face");
            face.transform.SetParent(cat.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.58f, -2.2f);
            Shape(face.transform, "eye:left", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-face", DioramaPalette.InkNavy),
                new Vector3(-0.22f, 0.08f, 0f), new Vector3(0.12f, 0.13f, 0.08f));
            Shape(face.transform, "eye:right", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-face", DioramaPalette.InkNavy),
                new Vector3(0.22f, 0.08f, 0f), new Vector3(0.12f, 0.13f, 0.08f));
            Shape(face.transform, "nose", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-nose", DioramaPalette.TicketOrange),
                new Vector3(0f, -0.12f, -0.02f), new Vector3(0.12f, 0.1f, 0.08f));
            for (int i = -1; i <= 1; i += 2)
            {
                var whisker = Shape(face.transform, "whisker:" + i, DioramaMeshKind.Cube,
                    DioramaPalette.Material("cat-face", DioramaPalette.InkNavy),
                    new Vector3(i * 0.3f, -0.14f, 0.02f), new Vector3(0.28f, 0.035f, 0.04f));
                whisker.transform.localRotation = Quaternion.Euler(0f, 0f, i * -9f);
            }

            Shape(cat.transform, "cat:paw-left", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-paws", DioramaPalette.CreamCard),
                new Vector3(-0.3f, -0.62f, -1.8f), new Vector3(0.36f, 0.18f, 0.16f));
            Shape(cat.transform, "cat:paw-right", DioramaMeshKind.Sphere,
                DioramaPalette.Material("cat-paws", DioramaPalette.CreamCard),
                new Vector3(0.3f, -0.62f, -1.8f), new Vector3(0.36f, 0.18f, 0.16f));

            var shadow = Shape(cat.transform, "contact-shadow", DioramaMeshKind.Sphere,
                DioramaPalette.Material("contact-shadow", DioramaPalette.DepotNavy),
                new Vector3(0.12f, -0.66f, 0.78f), new Vector3(0.92f, 0.22f, 0.06f));
            shadow.GetComponent<Renderer>().sortingOrder = -1;

            var badge = Shape(cat.transform, "cat:badge", DioramaMeshKind.Cylinder,
                DioramaPalette.Material("cat-badge", DioramaPalette.CreamCard),
                new Vector3(0f, -0.08f, -2.3f), new Vector3(0.48f, 0.08f, 0.48f));
            badge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var symbol = DioramaMeshFactory.CreateSymbol(cat.transform,
                "cat:symbol-" + line.SymbolId, line,
                DioramaPalette.Material("cat-symbol", DioramaPalette.InkNavy));
            symbol.transform.localPosition = new Vector3(0f, -0.08f, -2.6f);
            symbol.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

            if (line.SilhouetteId == "fluffy-longhair" ||
                line.SilhouetteId == "bent-ear-scruffy")
            {
                Shape(cat.transform, "cat:ruff", DioramaMeshKind.Sphere,
                    DioramaPalette.Material("cat-ruff", line.Color),
                    new Vector3(0f, 0.18f, 0.18f), new Vector3(1.28f, 0.42f, 0.24f));
            }
            return cat;
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
                    go = BuildCommuter(t, trains[t].Color);
                    var id = go.AddComponent<BoardElementId>();
                    id.Id = "train-" + t; id.Kind = "train";
                    _trains[t] = go;
                }
                go.SetActive(true);
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
            return LineIdentity.ForName(name).Color;
        }

        private static Color ColorForCode(byte code)
        {
            return LineIdentity.For(code).Color;
        }
    }
}
