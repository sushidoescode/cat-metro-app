using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Cats;
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
        private bool[] _sourceNode;
        private int[] _edgeFrom;
        private int[] _edgeTo;
        private int[] _edgeTravel;
        private TrackSplineGraph _trackPaths;
        private int[][] _switchRouteTargetNode; // per switch, per route: target node index
        private int[] _switchNode;
        private ToySwitchView[] _switchView;
        private readonly Dictionary<int, ToyTrainView> _trains = new Dictionary<int, ToyTrainView>();
        private CatPresentationTrack[] _catTracks;
        private int[] _catOccupantGenerations;
        private int[] _sourcePlatformLanes;
        private TrainSlot[] _currentTrainSlots;
        private TrainSlot[] _previousTrainSlots;
        private int[] _currentSessionOccupantGenerations;
        private int[] _previousSessionOccupantGenerations;
        private int[] _currentSessionDeliveryGenerations;
        private int[] _previousSessionDeliveryGenerations;
        private int _previousDeliveryCount;
        private bool _hasPresentationSnapshot;

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

        /// <summary>
        /// Pure presentation derivation: delivery is a counter advance paired with this slot
        /// changing from live to empty. Both slots are caller-owned value snapshots.
        /// </summary>
        public static bool DeliveryAdvancedForPresentation(TrainSlot previous, TrainSlot current,
            int previousDeliveryCount, int currentDeliveryCount) =>
            IsLive(previous) && !IsLive(current) && currentDeliveryCount > previousDeliveryCount;

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
            _sourceNode = new bool[nodes.Length];

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
                _sourceNode[i] = sourceIds.Contains(nodes[i].Id);
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
            _switchView = new ToySwitchView[switches.Length];
            _switchRouteTargetNode = new int[switches.Length][];
            for (int s = 0; s < switches.Length; s++)
            {
                _switchNode[s] = nodeIndex[switches[s].NodeId];
                var routes = switches[s].Routes.ToArray();
                _switchRouteTargetNode[s] = new int[routes.Length];
                for (int r = 0; r < routes.Length; r++)
                    _switchRouteTargetNode[s][r] = _edgeTo[edgeIndex[routes[r]]];

                // SWITCH-LEVERS: the toy assembly (teal base, tilted orange lever, arrow)
                // replaces the disc + arm. Its root keeps the disc's exact contract: the
                // "switch:{id}" name, the one BoardElementId (added HERE, unchanged), a root
                // renderer for the teach-ring comparison, and the same anchor position — the
                // tap target is SwitchWorldPos, which never moved.
                var toySwitch = ToySwitchView.Build(switches[s].Id, transform,
                    _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.4f));
                var switchGo = toySwitch.gameObject;
                var id = switchGo.AddComponent<BoardElementId>();
                id.Id = switches[s].Id; id.Kind = "switch";
                _switchView[s] = toySwitch;

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
                        _teachDiscBaseScale = switchGo.transform.localScale;
                    }
                    // Human ruling 2026-08-06 (#36 review finding 2): the ring carries the
                    // motion-off information, so it must READ as a ring — a distinct darker
                    // tint (the chrome ink-navy), one static cached instance of the greybox
                    // shader (same pipeline, no new Resources entry, no per-retry leak).
                    // SWITCH-LEVERS 2026-08-25: the solid cylinder read as a heavy dark puck
                    // under the toy, so it is now a true ANNULUS — which is what "must READ as
                    // a ring" asked for — sized to leave a wood gap around the base. The tint
                    // the ruling named is unchanged. Every CM-UX-03 pin holds: one transform,
                    // one renderer, static, its own material, the greybox shader.
                    _teachRing[s] = ToySwitchView.BuildTeachRing(switches[s].Id, transform,
                        _nodePos[_switchNode[s]] + new Vector3(0f, 0f, -0.35f),
                        TeachRingMaterial());
                    _teachDisc[s] = switchGo.transform;
                }
            }
            RefreshSwitches();
        }

        private static Material _teachRingMat; // one cached tinted instance per domain

        // The DEVFIX criterion-5 static gate counts CreatePrimitive calls against
        // GreyboxMaterial binds one-to-one. The ring is no longer a primitive, so this helper
        // no longer names the provider directly either — CreateTinted keeps the pairing
        // balanced and yields the same cached copy of the greybox material (identical shader
        // for the gate's live shader-equality walk, distinct tint for the ring).
        private static Material TeachRingMaterial()
        {
            if (_teachRingMat == null)
                _teachRingMat = GreyboxMaterial.CreateTinted(
                    "Teach Ring — Ink Navy", Palette.InkNavy); // the tint the ruling named
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
            for (int s = 0; s < _switchView.Length; s++)
            {
                // Board-local math: node positions all live in this view's XY plane, so the
                // toy's yaw is a pure local rotation — correct under any parent transform.
                var dir = _nodePos[_switchRouteTargetNode[s][CommittedRoute(s)]]
                    - _nodePos[_switchNode[s]];
                _switchView[s].SetDirection(new Vector2(dir.x, dir.y).normalized);
            }
        }

        public void UpdateFrom(GameSession session) => UpdateFrom(session, Time.unscaledTime);

        /// <summary>
        /// Explicit visual-time seam for deterministic presentation tests. Runtime callers use
        /// the one-argument overload, which always supplies <see cref="Time.unscaledTime"/>.
        /// </summary>
        public void UpdateFrom(GameSession session, float visualTime)
        {
            RefreshSwitches();
            UpdateTeach(session);
            float alpha = (float)session.Alpha;
            bool motionOff = MotionOffSource != null && MotionOffSource();
            // Copy first: presentation tracks must never hold a simulation slot reference or
            // mutate the session while deriving a delivery transition.
            var sourceTrains = session.State.Trains;
            EnsureCatTracks(sourceTrains.Length);
            var trains = _currentTrainSlots;
            for (int t = 0; t < sourceTrains.Length; t++)
            {
                trains[t] = sourceTrains[t];
                _currentSessionOccupantGenerations[t] = session.TrainOccupantGeneration(t);
                _currentSessionDeliveryGenerations[t] = session.TrainDeliveryGeneration(t);
            }
            int deliveries = session.State.Deliveries;
            bool deliveryCounterAdvanced = _hasPresentationSnapshot
                && deliveries > _previousDeliveryCount;
            for (int t = 0; t < trains.Length; t++)
            {
                TrainSlot previous = _hasPresentationSnapshot && t < _previousTrainSlots.Length
                    ? _previousTrainSlots[t] : default;
                bool previousLive = _hasPresentationSnapshot && IsLive(previous);
                bool live = IsLive(trains[t]);
                bool sessionDeliveryAdvanced = _hasPresentationSnapshot
                    && _currentSessionDeliveryGenerations[t]
                        != _previousSessionDeliveryGenerations[t];
                bool singleSessionDelivery = sessionDeliveryAdvanced
                    && _currentSessionDeliveryGenerations[t]
                        == NextGeneration(_previousSessionDeliveryGenerations[t]);
                bool displayedOccupantDelivered = previousLive && singleSessionDelivery;
                // Two complete lifecycles can collapse inside one hitch. Likewise, a delivery
                // that starts from an already-empty rendered snapshot belongs to an unseen cat,
                // not any older departure still lingering. Only one delivery of the occupant
                // visible in the prior snapshot may move and animate that retained renderer.
                bool deliveryAdvanced = displayedOccupantDelivered
                    || (!sessionDeliveryAdvanced && deliveryCounterAdvanced
                        && DeliveryAdvancedForPresentation(previous, trains[t],
                            _previousDeliveryCount, deliveries));
                bool sessionOccupantChanged = _hasPresentationSnapshot
                    && _currentSessionOccupantGenerations[t]
                        != _previousSessionOccupantGenerations[t];
                bool newOccupant = live
                    && (!_hasPresentationSnapshot || !previousLive || sessionOccupantChanged);
                if (newOccupant) _catOccupantGenerations[t] = NextGeneration(
                    _catOccupantGenerations[t]);

                // GameSession observes every authoritative step, so its read-only generation
                // catches same-colour refills even when a render hitch collapses delivery,
                // empty-slot and refill snapshots. Unrelated live slots retain their generation.
                bool waitingOnSourcePlatform = live
                    && session.TrainOccupantGeneration(t) > 0
                    && trains[t].State == TrainState.AtNode
                    && trains[t].NodeId >= 0 && trains[t].NodeId < _sourceNode.Length
                    && _sourceNode[trains[t].NodeId];
                _catTracks[t].Observe(trains[t], _catOccupantGenerations[t],
                    deliveryAdvanced, visualTime, waitingOnSourcePlatform);
                bool usesSourcePlatformLane = live
                    && !_catTracks[t].MovingToPlatform
                    && _catTracks[t].PlatformBlend > 0f;
                if (!usesSourcePlatformLane)
                    _sourcePlatformLanes[t] = -1;
                else if (newOccupant || _sourcePlatformLanes[t] < 0)
                {
                    _sourcePlatformLanes[t] = -1;
                    _sourcePlatformLanes[t] = AllocateSourcePlatformLane();
                }
                if (!live)
                {
                    if (_trains.TryGetValue(t, out var dead))
                    {
                        if (deliveryAdvanced && displayedOccupantDelivered)
                        {
                            int deliveryNode = session.TrainDeliveryNode(t);
                            if (deliveryNode >= 0 && deliveryNode < _nodePos.Length)
                                dead.PlaceAtNode(_trackPaths, deliveryNode,
                                    _nodePos[deliveryNode]);
                        }
                        // Exact GameSession delivery metadata places a delivered consist at the
                        // station even when rendering skipped the arrival step. Manually-driven
                        // presentation fixtures fall back to their last root. Either pose is
                        // retained only for departure; motion-off hides it in the same frame.
                        bool retainDeparture = !motionOff
                            && _catTracks[t].State != CatPresentationState.Hidden;
                        if (!retainDeparture)
                        {
                            // Cancel, rather than merely hide, so re-enabling motion cannot
                            // resume an old departure sequence from its elapsed timestamp.
                            bool cancelDeparture = motionOff
                                && _catTracks[t].State != CatPresentationState.Hidden;
                            bool needsHideReset = dead.gameObject.activeSelf || cancelDeparture;
                            if (cancelDeparture) _catTracks[t] = new CatPresentationTrack();
                            if (needsHideReset)
                            {
                                dead.ApplyPresentation(CatPresentationState.Hidden, visualTime, true);
                                dead.gameObject.SetActive(false);
                            }
                        }
                        else
                        {
                            dead.gameObject.SetActive(true);
                            dead.ApplyPresentation(_catTracks[t].State,
                                _catTracks[t].PlatformBlend,
                                _catTracks[t].MovingToPlatform, visualTime, false,
                                _catTracks[t].PlatformBlendSpeed);
                        }
                    }
                    continue;
                }
                if (!_trains.TryGetValue(t, out var consist) || consist == null)
                {
                    // LOOK step 6: a train renders as a toy consist (ToyTrainView) — engine +
                    // carriage + seated cat — instead of a capsule. The root alone carries the
                    // "train" inventory id; everything under it is decoration (no
                    // BoardElementId, no collider), and its localPosition keeps the capsule's
                    // exact head-anchor contract on the shared spline.
                    consist = ToyTrainView.Create(transform, "train:" + t, _edgeFrom, _edgeTo);
                    var id = consist.gameObject.AddComponent<BoardElementId>();
                    id.Id = "train-" + t; id.Kind = "train";
                    _trains[t] = consist;
                }
                consist.gameObject.SetActive(true);
                // The CODE, not a resolved Color: the consist paints the cat AND cuts its
                // destination pin from it, and both have to come off the one CatLine vocabulary
                // or the pin's shape and the cat's colour can drift apart.
                consist.SyncSlot(PresentationOccupantKey(t, _catOccupantGenerations[t]),
                    trains[t].Color);
                if (trains[t].State == CatMetro.Domain.TrainState.OnEdge)
                {
                    int e = trains[t].EdgeId;
                    float progress = Mathf.Min(1f, (trains[t].ProgressTicks + alpha) / _edgeTravel[e]);
                    consist.PlaceOnEdge(_trackPaths, e, _trackPaths.Path(e).Length * progress);
                }
                else
                {
                    consist.PlaceAtNode(_trackPaths, trains[t].NodeId, _nodePos[trains[t].NodeId]);
                }
                // New riders need a hitch-proof source endpoint. Waiting cats reapply their
                // stable presentation lane; once released, they retain that stored endpoint
                // for the boarding walk. Lanes outlive FIFO-rank changes until the cat reaches
                // its seat, so an older waiter and same-tick newcomer cannot coincide.
                if (newOccupant || waitingOnSourcePlatform)
                {
                    int spawnNode = session.TrainOccupantSpawnNode(t);
                    int spawnEdge = session.TrainOccupantSpawnEdge(t);
                    if (spawnNode >= 0 && spawnNode < _nodePos.Length)
                    {
                        Vector3 tangent = spawnEdge >= 0 && spawnEdge < _edgeFrom.Length
                            ? _trackPaths.Path(spawnEdge).TangentDistanceFraction(0f)
                            : Vector3.right;
                        consist.SetSourcePlatformAnchor(_nodePos[spawnNode],
                            new Vector3(tangent.y, -tangent.x, 0f),
                            _sourcePlatformLanes[t]);
                    }
                }
                // Always place from the copied simulation snapshot before applying visual-only
                // cat transforms. No bob/head motion can feed back into spline placement.
                consist.ApplyPresentation(_catTracks[t].State,
                    _catTracks[t].PlatformBlend, _catTracks[t].MovingToPlatform,
                    visualTime, motionOff, _catTracks[t].PlatformBlendSpeed);
            }
            for (int t = 0; t < trains.Length; t++)
            {
                _previousTrainSlots[t] = trains[t];
                _previousSessionOccupantGenerations[t] =
                    _currentSessionOccupantGenerations[t];
                _previousSessionDeliveryGenerations[t] =
                    _currentSessionDeliveryGenerations[t];
            }
            _previousDeliveryCount = deliveries;
            _hasPresentationSnapshot = true;
        }

        private void EnsureCatTracks(int count)
        {
            if (_catTracks != null && _catTracks.Length == count) return;
            var replacement = new CatPresentationTrack[count];
            var replacementGenerations = new int[count];
            var replacementLanes = new int[count];
            for (int i = 0; i < replacementLanes.Length; i++) replacementLanes[i] = -1;
            int existing = _catTracks == null ? 0 : Mathf.Min(_catTracks.Length, count);
            for (int i = 0; i < existing; i++)
            {
                replacement[i] = _catTracks[i];
                replacementGenerations[i] = _catOccupantGenerations[i];
                replacementLanes[i] = _sourcePlatformLanes[i];
            }
            for (int i = existing; i < count; i++) replacement[i] = new CatPresentationTrack();
            _catTracks = replacement;
            _catOccupantGenerations = replacementGenerations;
            _sourcePlatformLanes = replacementLanes;
            _currentTrainSlots = new TrainSlot[count];
            _previousTrainSlots = new TrainSlot[count];
            _currentSessionOccupantGenerations = new int[count];
            _previousSessionOccupantGenerations = new int[count];
            _currentSessionDeliveryGenerations = new int[count];
            _previousSessionDeliveryGenerations = new int[count];
            _hasPresentationSnapshot = false;
        }

        private static int NextGeneration(int current)
        {
            int next = unchecked(current + 1);
            return next > 0 ? next : 1;
        }

        private static long PresentationOccupantKey(int slotIndex, int generation) =>
            ((long)(uint)(slotIndex + 1) << 32) | (uint)generation;

        private int AllocateSourcePlatformLane()
        {
            for (int candidate = 0; candidate < _sourcePlatformLanes.Length; candidate++)
            {
                bool used = false;
                for (int t = 0; t < _sourcePlatformLanes.Length; t++)
                    if (_sourcePlatformLanes[t] == candidate)
                    {
                        used = true;
                        break;
                    }
                if (!used) return candidate;
            }
            return _sourcePlatformLanes.Length;
        }

        private static bool IsLive(TrainSlot slot) =>
            slot.Id != 0 && slot.State != TrainState.None;

        // STATION-BADGE: one colour decision, not two. This was a private duplicate of
        // CatLine.ColorOf — same four cases, same magenta fallback — and the duplication was
        // load-bearing in a way nothing revealed: the station badge's PRIMARY plate inherits
        // THIS material, so editing CatLine's fallback alone would have left an unmapped berth
        // rendering as a plausible red station from here. Delegating makes the vocabulary the
        // single source for colour the way CatLine.ShapeOf already is for shape.
        private static Color ColorFor(string name) => CatLine.ColorOf(name);

        // The byte-keyed half of the same story. This was the LAST colour table outside the
        // vocabulary, and the one that would have bitten: a fifth line lands in CatLine, the
        // station plate and the HUD badge pick it up for free, and trains of that colour go on
        // rendering magenta with nothing to catch it. Routed through the vocabulary too, so
        // adding a line is one edit in CatLine and every surface follows.
        private static Color ColorForCode(byte code) => CatLine.ColorOf(code);
    }
}
