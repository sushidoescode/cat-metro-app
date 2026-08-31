using System;
using System.Collections.Generic;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Props
{
    // LOOK steps 4-5: optional authored models surround the live board without replacing any
    // gameplay object. Props are children of BoardView so Retry/LoadNext rebuild them atomically.
    public static class BoardPropDecorator
    {
        public const string RootName = "Props";
        public const string ContactPlaneMarkerName = "prop-contact-plane";
        public const string DeskContactPlaneMarkerName = "desk-prop-contact-plane";
        public const string DioramaSurfaceName = "desk:surface";

        private static readonly Quaternion ModelYUpToBoardXY = Quaternion.Euler(-90f, 0f, 0f);

        public static GameObject Decorate(ImportedLevel level, Transform boardRoot,
            PropModelCatalog catalog) => Decorate(level, boardRoot, catalog, null);

        public static GameObject Decorate(ImportedLevel level, Transform boardRoot,
            PropModelCatalog catalog, Func<string, Vector3> presentationPosition)
        {
            if (level == null || boardRoot == null || catalog == null
                || catalog.AdmittedEntryCount == 0)
                return null;

            var alreadyBuilt = boardRoot.Find(RootName);
            if (alreadyBuilt != null) return alreadyBuilt.gameObject;

            var anchors = new Dictionary<string, Transform>();
            foreach (var element in boardRoot.GetComponentsInChildren<BoardElementId>(true))
            {
                if ((element.Kind == "node" || element.Kind == "source" || element.Kind == "station")
                    && !anchors.ContainsKey(element.Id))
                    anchors.Add(element.Id, element.transform);
            }
            if (anchors.Count == 0) return null;

            GameObject propsRoot = null;
            var dto = level.Dto;

            foreach (var station in dto.Stations.ToArray())
            {
                if (!TryPosition(station.NodeId, boardRoot, anchors, presentationPosition,
                        out var anchorPosition))
                    continue;
                var kiosk = Spawn(ref propsRoot, boardRoot, catalog,
                    PropModelCatalog.StationKioskId,
                    "station-kiosk", station.NodeId,
                    anchorPosition + new Vector3(0f, 0.42f, 0f));
                if (kiosk != null)
                {
                    AddStationArchitecture(kiosk.transform, anchors[station.NodeId]);
                    // The full accept list travels with the anchor: the badge is the only
                    // surface that can tell a player a berth takes more than one line.
                    SuppressReplacedStationArchitecture(anchors[station.NodeId],
                        station.Accepts, boardRoot);
                }
                // LOOK step 5: each platform gets its own lantern, just off the kiosk so the
                // near-orthographic camera reads them as separate objects.
                Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.LampPostId,
                    "station-lamp", station.NodeId,
                    anchorPosition + new Vector3(-0.85f, -0.15f, 0f));
            }

            bool hasFirstSource = false;
            Vector3 firstSourcePosition = Vector3.zero;
            string firstSourceId = "";
            foreach (var source in dto.Sources.ToArray())
            {
                if (!TryPosition(source.NodeId, boardRoot, anchors, presentationPosition,
                        out var anchorPosition))
                    continue;
                if (!hasFirstSource)
                {
                    hasFirstSource = true;
                    firstSourcePosition = anchorPosition;
                    firstSourceId = source.NodeId;
                }
                var depot = Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.DepotShedId,
                    "depot", source.NodeId,
                    anchorPosition + new Vector3(0f, 0.62f, 0f));
                if (depot != null) SuppressReplacedSourceVisual(anchors[source.NodeId]);
            }

            bool firstAnchor = true;
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f, contactZ = 0f;
            foreach (var node in dto.Nodes.ToArray())
            {
                if (!TryPosition(node.Id, boardRoot, anchors, presentationPosition,
                        out var position))
                    continue;
                if (firstAnchor)
                {
                    minX = maxX = position.x;
                    minY = maxY = position.y;
                    contactZ = position.z;
                    firstAnchor = false;
                    continue;
                }
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }
            if (firstAnchor) return propsRoot;

            Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.TreesId,
                "perimeter-trees", "",
                new Vector3(minX - 0.15f, Mathf.Lerp(minY, maxY, 0.66f), contactZ));
            Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.DeskClutterId,
                "desk-clutter", "",
                new Vector3((minX + maxX) * 0.5f, minY - 1.4f,
                    ResolveDeskContactPlaneLocalZ(boardRoot)));
            if (hasFirstSource)
            {
                Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.ToyEngineId,
                    "parked-engine", firstSourceId,
                    firstSourcePosition + new Vector3(1.15f, 0.1f, 0f));
                // A signpost by the depot, like the target art's platform signage.
                Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.SignpostId,
                    "depot-signpost", firstSourceId,
                    firstSourcePosition + new Vector3(-1.05f, -0.2f, 0f));
            }

            // LOOK step 5 furnish: a short split-rail fence run along the south apron and a
            // few bushes around the rim fill the board without touching any gameplay anchor.
            // Deterministic bounds-derived placement keeps Retry/LoadNext rebuilds identical.
            for (int i = 0; i < 3; i++)
            {
                float t = (i + 0.5f) / 3f;
                Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.FenceId,
                    "fence-line", "",
                    new Vector3(Mathf.Lerp(minX, maxX, t), minY - 0.5f, contactZ));
            }
            Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.BushId,
                "rim-bush", "",
                new Vector3(Mathf.Lerp(minX, maxX, 0.2f), maxY + 0.35f, contactZ));
            Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.BushId,
                "rim-bush", "",
                new Vector3(maxX + 0.3f, Mathf.Lerp(minY, maxY, 0.35f), contactZ));
            Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.BushId,
                "rim-bush", "",
                new Vector3(Mathf.Lerp(minX, maxX, 0.8f), minY - 0.3f, contactZ));
            Spawn(ref propsRoot, boardRoot, catalog, PropModelCatalog.TrailSignpostId,
                "trail-signpost", "",
                // The licensed L008 renderer reached viewport x 1.158 at maxX + 0.4 while
                // the decorative safe-frame law ends at 1.12. Moving it 0.6 board units
                // inward preserves the rim-side composition with measured headroom.
                new Vector3(maxX - 0.2f, minY - 0.35f, contactZ));

            return propsRoot;
        }

        private static Vector3 BoardLocalPosition(Transform boardRoot, Transform anchor) =>
            boardRoot.InverseTransformPoint(anchor.position);

        private static bool TryPosition(string nodeId, Transform boardRoot,
            Dictionary<string, Transform> anchors, Func<string, Vector3> presentationPosition,
            out Vector3 position)
        {
            if (!anchors.TryGetValue(nodeId, out var anchor))
            {
                position = Vector3.zero;
                return false;
            }
            if (presentationPosition == null)
            {
                position = BoardLocalPosition(boardRoot, anchor);
                position.z = ResolveContactPlaneLocalZ(boardRoot);
            }
            else position = presentationPosition(nodeId);
            return true;
        }

        public static float ResolveContactPlaneLocalZ(Transform boardRoot)
        {
            if (boardRoot == null) return 0f;

            var marker = boardRoot.Find(ContactPlaneMarkerName);
            if (marker != null) return marker.localPosition.z;

            // art/diorama-pass already owns this mesh. Its camera-facing (-Z) face is the
            // tabletop contact plane, so the prop lane automatically follows that scene
            // without copying its private depth constant.
            var surface = boardRoot.Find(DioramaSurfaceName);
            var filter = surface == null ? null : surface.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return 0f;
            var bounds = filter.sharedMesh.bounds;
            var face = bounds.center;
            face.z = bounds.min.z;
            return boardRoot.InverseTransformPoint(surface.TransformPoint(face)).z;
        }

        public static float ResolveDeskContactPlaneLocalZ(Transform boardRoot)
        {
            if (boardRoot == null) return 0f;
            var marker = boardRoot.Find(DeskContactPlaneMarkerName);
            return marker == null
                ? ResolveContactPlaneLocalZ(boardRoot)
                : marker.localPosition.z;
        }

        private static BoardPropInstance Spawn(ref GameObject propsRoot, Transform boardRoot,
            PropModelCatalog catalog, string assetId, string role, string anchorId,
            Vector3 boardPosition)
        {
            if (!catalog.TryGet(assetId, out var entry)) return null;
            if (propsRoot == null)
            {
                propsRoot = new GameObject(RootName);
                propsRoot.transform.SetParent(boardRoot, false);
            }

            var holder = new GameObject(role + (string.IsNullOrEmpty(anchorId) ? "" : ":" + anchorId));
            holder.transform.SetParent(propsRoot.transform, false);
            holder.transform.localPosition = boardPosition;
            var identity = holder.AddComponent<BoardPropInstance>();
            identity.AssetId = assetId;
            identity.Role = role;
            identity.AnchorId = anchorId;

            var model = UnityEngine.Object.Instantiate(entry.Prefab, holder.transform, false);
            model.name = "Model";
            model.transform.localPosition = entry.LocalOffset;
            model.transform.localRotation = ModelYUpToBoardXY
                * Quaternion.Euler(0f, entry.FacingYaw, 0f);
            model.transform.localScale = Vector3.one * entry.DisplayScale;
            return identity;
        }

        // --- LOOK step 4: the station as a raised wooden platform under a line canopy ---
        //
        // ALL of these are BOARD units (the kiosk holder is unscaled), and every one of them
        // was chosen against a fixed conservative 82 px/unit yardstick rather than by eye.
        // That number is not claimed as today's corpus minimum; camera fit changes with level
        // and licensed catalog. With the frontal Quaternion.Euler(38, 0, 0), board +Z projects
        // vertically at 0.616 screen units per board unit, so a thickness t reads as
        // 0.616 * t * 82 px at the yardstick. The r6 artifact found that detail under roughly
        // 4 px does not read; 0.05 units is therefore the floor and nothing below sits near it.
        private const float PlinthDepth = 0.18f;   // 9.1 px of riser at the 82 px/unit yardstick
        private const float PlinthInset = 0.14f;   // the deck overhangs the plinth all round
        private const float DeckDepth = 0.10f;     // 5.0 px
        private const float PostThickness = 0.09f; // 8.4 px wide (board X and Y both project)
        private const float PostInset = 0.07f;
        private const float CanopyDepth = 0.10f;   // 5.0 px
        private const float CanopyModelClearance = 0.06f;
        private const float MinCanopyRise = 0.45f; // >= 22.7 px of daylight under the roof

        // Toy-wood course: a darker plinth, a paler deck on top of it, darker posts again. The
        // three-value ramp is what makes the platform read as a stack of parts rather than one
        // painted box, and it is the same warm walnut family BoardSurface already uses for the
        // tabletop, so a station cannot be mistaken for a line colour.
        private static readonly Color PlinthWood = new Color(0.64f, 0.43f, 0.29f);
        private static readonly Color DeckWood = new Color(0.86f, 0.74f, 0.56f);
        private static readonly Color PostWood = new Color(0.45f, 0.30f, 0.20f);

        private static void AddStationArchitecture(Transform kiosk, Transform stationAnchor)
        {
            var fallback = stationAnchor.GetComponent<Renderer>();
            Color lineColor = fallback != null && fallback.sharedMaterial != null
                ? fallback.material.color : Color.magenta;

            var model = kiosk.Find("Model");
            Bounds bounds = LocalRendererBounds(kiosk, model);
            float baseWidth = Mathf.Clamp(bounds.size.x * 1.05f, 0.9f, 2.1f);
            float baseDepth = Mathf.Clamp(bounds.size.y * 1.05f, 0.75f, 1.7f);

            // The platform, in two courses. It used to be ONE 0.11-thick box, which is 5.6 px
            // of riser at the conservative yardstick and read as paint on the wood
            // rather than as something standing on it. A plinth carries the height and a deck
            // caps it, overhanging on every side, so the shadow line between the two is what
            // the eye actually reads as "raised" — that line is the whole trick in target-02.
            // The name station:wood-base is unchanged because it is still the course that meets
            // the tabletop, and the existing pins name it.
            CreateStationPart("station:wood-base", kiosk,
                new Vector3(bounds.center.x, bounds.center.y, -PlinthDepth * 0.5f),
                new Vector3(baseWidth - PlinthInset * 2f, baseDepth - PlinthInset * 2f,
                    PlinthDepth),
                PlinthWood);
            float deckTopZ = -(PlinthDepth + DeckDepth); // -Z is toward the camera
            CreateStationPart("station:wood-deck", kiosk,
                new Vector3(bounds.center.x, bounds.center.y, deckTopZ + DeckDepth * 0.5f),
                new Vector3(baseWidth, baseDepth, DeckDepth), DeckWood);

            // The generated kiosk remains neutral licensed artwork. This project-owned canopy
            // makes the station's route colour unmistakable without editing the pinned FBX or
            // relying on its baked blue sign for gameplay semantics.
            //
            // It is now held UP on posts instead of capping the model. Same plate, same colour,
            // same clearance over the artwork — but with daylight under it the station reads as
            // a shelter a cat waits beneath, which is what both reference boards show and what
            // a plate lying on the roofline cannot say. The underside is whichever is higher of
            // "just clear of the kiosk" and "far enough off the deck to see under", so a short
            // generated kiosk still gets a canopy you can see daylight beneath.
            float roofWidth = Mathf.Clamp(bounds.size.x * 0.82f, 0.75f, 1.65f);
            float roofDepth = Mathf.Clamp(bounds.size.y * 0.72f, 0.6f, 1.35f);
            float canopyZ = Mathf.Min(bounds.min.z - CanopyModelClearance,
                deckTopZ - MinCanopyRise);
            CreateStationPart("station:line-roof", kiosk,
                new Vector3(bounds.center.x, bounds.center.y, canopyZ - CanopyDepth * 0.5f),
                new Vector3(roofWidth, roofDepth, CanopyDepth), lineColor);

            // Four posts, at the canopy's corners, spanning exactly deck-top to canopy-
            // underside. Derived rather than authored: a constant height would either float the
            // roof off the posts or bury them in the deck the first time a kiosk's bounds
            // changed, and neither is visible to a test that only checks that parts exist.
            float postX = roofWidth * 0.5f - PostInset;
            float postY = roofDepth * 0.5f - PostInset;
            float postRise = deckTopZ - canopyZ;
            for (int i = 0; i < 4; i++)
                CreateStationPart("station:roof-post-" + i, kiosk,
                    new Vector3(
                        bounds.center.x + ((i & 1) == 0 ? -postX : postX),
                        bounds.center.y + ((i & 2) == 0 ? -postY : postY),
                        (deckTopZ + canopyZ) * 0.5f),
                    new Vector3(PostThickness, PostThickness, postRise), PostWood);
        }

        private static Bounds LocalRendererBounds(Transform holder, Transform model)
        {
            bool found = false;
            Bounds localBounds = default;
            if (model != null)
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds world = renderer.bounds;
                    Vector3 min = world.min;
                    Vector3 max = world.max;
                    for (int mask = 0; mask < 8; mask++)
                    {
                        Vector3 point = holder.InverseTransformPoint(new Vector3(
                            (mask & 1) == 0 ? min.x : max.x,
                            (mask & 2) == 0 ? min.y : max.y,
                            (mask & 4) == 0 ? min.z : max.z));
                        if (!found)
                        {
                            localBounds = new Bounds(point, Vector3.zero);
                            found = true;
                        }
                        else localBounds.Encapsulate(point);
                    }
                }
            return found ? localBounds
                : new Bounds(Vector3.zero, new Vector3(1.2f, 1f, 1.2f));
        }

        // The builtin-cube idiom, the same one BoardSurface.CreatePart and CreatePlateGeometry
        // below already use: a bare GameObject with a MeshFilter and a MeshRenderer. This used
        // to call the engine's primitive factory and then destroy the collider it handed back.
        // Not building one is strictly better than remembering to destroy one — the platform
        // grew from two parts to seven here, and "every one of them destroys its collider" is
        // exactly the kind of invariant that holds until someone adds the eighth. A property
        // block over the one committed material gives each part its colour without allocating
        // a Material per station on every Retry/LoadNext rebuild.
        //
        // NOTE for whoever next reads device-config.test.sh criterion 5. That gate proves "no
        // unbound runtime renderer" by counting CreatePrimitive call sites and requiring the
        // committed-material bind count to equal it. The proxy stopped holding before this lane
        // touched anything — main is 8 and 8, feat/station-badges is already 8 and 13 — because
        // the branch moved to exactly this idiom, and AddComponent<MeshRenderer>() creates a
        // renderer that CreatePrimitive never counted. The real invariant is unchanged and
        // still worth gating; the counting rule is what needs rewriting, and it cannot simply
        // be widened to include the AddComponent form either, because binds now happen inside
        // shared helpers (Tint, BoardSurface.CreatePart) rather than once per site. It also
        // counts occurrences in COMMENTS, which is why neither token is spelled out anywhere
        // in this note. Deliberately NOT patched here: a gate broken by another lane is that
        // lane's to fix, and quietly nudging its numbers is how a measurement bug becomes
        // permanent.
        private static Mesh _cubeMesh;

        private static Transform CreateStationPart(string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Color color)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            if (_cubeMesh == null) _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            part.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GreyboxMaterial.Shared;
            Tint(renderer, color);
            return part.transform;
        }

        private static void SuppressReplacedSourceVisual(Transform sourceAnchor)
        {
            // The logical/id/collider root remains intact. Only its fallback dressing is hidden
            // once the generated shed has been admitted; stations are intentionally untouched
            // because their project-owned line badge must stay visible over a neutral kiosk.
            foreach (var renderer in sourceAnchor.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        private static void SuppressReplacedStationArchitecture(Transform stationAnchor,
            ReadOnlyMemory<string> accepts, Transform boardRoot)
        {
            // The kiosk supplies the neutral building. Keep only line-owned overlays: the
            // main branch's text symbol and the diorama branch's colored plate/keyline/symbol.
            EnsureProjectOwnedStationPlate(stationAnchor, accepts,
                SignFootLocalZ(stationAnchor, boardRoot));
            var renderers = stationAnchor.GetComponentsInChildren<Renderer>(true);
            bool hasLinePlate = false;
            foreach (var renderer in renderers)
                if (renderer.enabled && renderer.gameObject.name.StartsWith("station:plate"))
                {
                    hasLinePlate = true;
                    break;
                }
            if (!hasLinePlate) return;

            foreach (var renderer in renderers)
            {
                if (renderer.GetComponent<TextMesh>() != null) continue;
                string name = renderer.gameObject.name;
                // station:signmast joins the keep-list for a reason worth stating: this sweep
                // hides EVERYTHING it does not recognise, silently. The mast is the post the
                // badge stands on, so forgetting it here would leave a sign hanging in the air
                // with no test able to see it — every existing assertion checks meshes, colours
                // and names, and a disabled renderer passes all three.
                if (name.StartsWith("station:plate") || name.StartsWith("station:keyline")
                    || name.StartsWith("station:symbol") || name.StartsWith("station:signmast"))
                    continue;
                renderer.enabled = false;
            }
        }

        // The tabletop, expressed in the station anchor's own space. A sign post has to REACH
        // the wood, and the anchor is scaled (BoardView builds it at 0.6) and may be nested
        // under whatever final layout the scene lane authors, so the plane cannot be a constant
        // here. This is the same contact-plane seam every other prop already stands on.
        private static float SignFootLocalZ(Transform stationAnchor, Transform boardRoot)
        {
            if (boardRoot == null || stationAnchor == null) return 0f;
            Vector3 inBoard = boardRoot.InverseTransformPoint(stationAnchor.position);
            inBoard.z = ResolveContactPlaneLocalZ(boardRoot);
            return stationAnchor.InverseTransformPoint(boardRoot.TransformPoint(inBoard)).z;
        }

        // --- the station badge: the only surface that tells a player where a berth goes ---

        // STATION-PLATFORM: the badge stands UP on a post now, so "depth" stopped being a board
        // axis and became the sign's own facing axis. The layer order is unchanged and so are
        // its magnitudes — the cream keyline sits 0.03 BEHIND the coloured plate and reads as a
        // halo around it, the glyph sits 0.09 in FRONT of both — but they are measured along
        // the direction the sign faces rather than along board -Z.
        //
        // That substitution is the whole of the risk in standing the badge up, and it is
        // invisible: keeping the shipped z literals would have left three parts that are now
        // COPLANAR in depth separated only VERTICALLY by 0.03, which is a z-fight and a
        // keyline peeking out of the top of its own plate. Nothing that checks meshes, colours,
        // names or scales can see that, which is why the offsets moved axis here rather than
        // staying put and being tuned later.
        private const float PlateZ = -1.35f;                // the sign HEAD's height off the board
        private const float KeylineFacingOffset = -0.03f;   // behind the plate
        private const float GlyphFacingOffset = 0.09f;      // in front of it
        private const float PlateY = -1f;
        private const float PlateSize = 0.9f;
        private const float PlateDepth = 0.1f;
        private const float KeylineSize = 1.08f;
        private const float KeylineDepth = 0.11f;

        // The secondary accept row runs BESIDE the primary plate, on its row and in its Z band,
        // so the badge reads as one strip of destinations the way real platform signage does.
        //
        // Not below it, which is where the first cut put it. Two things rule that out. The
        // primary keyline already reaches local y -1.54 (0.92 board units under the node) and
        // BoardSurface's near rim is only 0.89, so a row clearing it vertically would hang off
        // the cream rim. And at the clearance the keylines actually need, the two cream halos
        // overlapped at an identical Z and z-fought. Sideways, the primary stays at x = 0
        // untouched — which is also why a single-accept station renders exactly as it always
        // has, down to the pixel.
        //
        // AcceptFirstX = primary keyline half (0.54) + chip keyline half (0.24) + a 0.08 gap.
        private const float AcceptFirstX = 0.86f;
        private const float AcceptPitch = 0.52f;
        private const float AcceptSize = 0.4f;
        private const float AcceptKeylineSize = 0.48f;
        private const float AcceptGlyphSize = 0.22f;

        // The post the badge stands on. Anchor-local, and the anchor is at 0.6, so 0.15 here is
        // 0.09 board units — 8.4 px wide at the conservative yardstick, the same slender-but-
        // present read the reference art's sign poles have. The chip masts are thinner (0.11 ->
        // 0.066 board -> 6.1 px) because a chip is a smaller sign and a pole as thick as the
        // primary's would out-weigh the badge it carries. Both stay well clear of the ~4 px
        // floor the r6 render established for board detail.
        private const float MastThickness = 0.15f;
        private const float ChipMastThickness = 0.11f;
        // Pale toy wood: light enough to separate from the navy roof and the warm tabletop
        // both, which is how target-01's platform sign poles read.
        private static readonly Color MastWood = new Color(0.78f, 0.66f, 0.50f);

        /// <summary>
        /// Board-local yaw that turns a +x face toward the camera. Same derivation the cat lane
        /// uses in ToyTrainView.CameraFacingYawDegrees, and deliberately the same shape of
        /// answer: a FIXED board yaw, not a per-frame billboard.
        /// </summary>
        public static float CameraFacingYawDegrees(Quaternion boardTilt)
        {
            // The camera is identity-rotated and orthographic, so it looks along world +z.
            Vector3 viewLocal = Quaternion.Inverse(boardTilt) * Vector3.forward;
            // Face back along it, flattened into the board plane.
            return Mathf.Atan2(-viewLocal.y, -viewLocal.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Turns a board-PARALLEL plate into a sign standing out of the tabletop and facing the
        /// diorama camera. Composed on the left of DestinationShapeMesh.PlateRotation, so the
        /// shape's own orientation is still the vocabulary's and this only stands it up.
        /// </summary>
        public static Quaternion StandingSignRotation(Quaternion boardTilt)
        {
            float yaw = CameraFacingYawDegrees(boardTilt) * Mathf.Deg2Rad;
            Vector3 towardCamera = new Vector3(Mathf.Cos(yaw), Mathf.Sin(yaw), 0f);
            // -Z of the SIGN frame is where every plate's visible face ends up, because
            // PlateRotation is composed on the right to put it there. (Which of a plate's own
            // axes that is varies by shape and is none of this method's business: the prisms
            // and the cube present their -Z, while the circle presents a cylinder CAP along its
            // -Y.) LookRotation aims +Z, so aim it AWAY from the camera. Up is board -Z, out of
            // the tabletop, which is what makes the sign vertical and its glyph upright rather
            // than lying on its side.
            return Quaternion.LookRotation(-towardCamera, Vector3.back);
        }

        /// <summary>
        /// The one rotation every station sign wears. Its face ends up pointing along board
        /// (0, -1, 0) — the SAME direction ToyTrainView's seated cats face, since both
        /// are answering "which way is the camera". Only the model axis differs: a cat presents
        /// its +x face and so carries the bare yaw (-90 degrees in the frontal rig), while a
        /// plate presents its -z face and so needs the standing turn composed in as well.
        /// </summary>
        public static Quaternion StationSignRotation =>
            StandingSignRotation(BoardSceneLook.BoardTilt);

        // The sign's own facing direction in the anchor's space, which is where the three badge
        // layers are stacked. Board Z is no longer "toward the camera" once the badge stands up.
        private static Vector3 SignFacing => StationSignRotation * Vector3.back;

        // The shipped keyline cream, hoisted verbatim rather than swapped for a Palette token.
        // It is deliberately warmer and darker than Palette.CreamCard because the board top IS
        // CreamCard — a keyline in the token would vanish into the surface it has to separate
        // the badge from. Retinting a shipped colour is a palette decision, not a legibility
        // lane's; flagged for whoever owns the palette rather than changed here.
        private static readonly Color KeylineCream = new Color(0.94f, 0.88f, 0.75f);

        private static void EnsureProjectOwnedStationPlate(Transform stationAnchor,
            ReadOnlyMemory<string> accepts, float footLocalZ)
        {
            foreach (var renderer in stationAnchor.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled && renderer.gameObject.name.StartsWith("station:plate"))
                    return;

            // Main's fallback station carries the correct line color and glyph on a cube.
            // Recompose those same project-owned signals as a compact badge so the generated
            // kiosk can replace the cube without letting its fixed blue sign imply a route.
            var fallback = stationAnchor.GetComponent<Renderer>();
            TextMesh label = null;
            foreach (var candidate in stationAnchor.GetComponentsInChildren<TextMesh>(true))
            {
                label = candidate;
                break;
            }
            if (fallback == null || label == null) return;

            var accepted = accepts.Span;
            string primary = accepted.Length > 0 ? accepted[0] : "";
            // STATION-BADGE: the plate's shape comes from the line vocabulary, not from a
            // switch of its own. This used to read `label.text == "R" ? Cylinder : Cube` —
            // "red is a circle, everything else is a square" — which stopped being a shape
            // channel the moment a level authored a third destination colour. CatLine.ShapeOf
            // is the single source; the HUD's badge sprites key off the very same call.
            var shape = CatLine.ShapeOf(primary);

            // Put the shape plaque on the clear board apron in front of the kiosk. Centering it
            // on the line-coloured roof makes the red circle and blue square both read as the
            // same rectangle in the production near-orthographic view.
            //
            // LOOK step 4: it now STANDS on a post there instead of lying on the wood. The
            // plaque's centre has not moved by a millimetre — same x, same y, same height off
            // the board — so the composition, the on-wood fit and the chip row are all
            // untouched; what changed is that its face is perpendicular to the tabletop and
            // yawed at the camera, and that a mast runs from the wood up to its bottom edge.
            //
            // Under the frontal 38-degree pitch, a flat plaque would present cos(38) = 0.788
            // of its face, while the best upright board-plane direction presents sin(38) =
            // 0.616. Standing therefore costs about 22% of projected face area; the deliberate
            // 0.9-unit badge pays that cost in exchange for reading unambiguously as a station
            // sign rather than another symbol painted onto the tabletop.
            var head = new Vector3(0f, PlateY, PlateZ);
            CreateMast("station:signmast-generated", stationAnchor,
                head, PlateSize, MastThickness, footLocalZ);

            var keyline = CreatePlateGeometry("station:keyline-generated", stationAnchor,
                shape, head + SignFacing * KeylineFacingOffset, KeylineSize, KeylineDepth);
            Tint(keyline, KeylineCream);

            var plate = CreatePlateGeometry("station:plate-generated", stationAnchor,
                shape, head, PlateSize, PlateDepth);
            // The primary plate keeps wearing the station's OWN line material, exactly as it
            // did before — that is what makes the fallback cube and the badge the same signal.
            //
            // Note the asymmetry with the chips below, because it decides how each is read
            // back: this plate carries a real per-station material and NO property block, so
            // its colour is sharedMaterial.color. The chips carry GreyboxMaterial.Shared plus
            // a property block, so theirs is only in the block. Reading either the other way
            // returns a plausible wrong colour rather than failing.
            plate.sharedMaterial = fallback.sharedMaterial != null
                ? fallback.sharedMaterial : GreyboxMaterial.Shared;

            label.transform.localPosition = head + SignFacing * GlyphFacingOffset;
            label.transform.localRotation = StationSignRotation;
            label.characterSize = 0.5f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;

            // STATION-BADGE gap 2: a berth that accepts more than one line used to advertise
            // only its first. L009's COOL takes blue AND yellow and badged a bare "B", so the
            // yellow half was unlearnable from the board — the player had to discover it by
            // sending a cat and watching. Every further accepted line now gets its own chip in
            // a row under the plate, carrying the same three channels the primary does: line
            // colour, line shape, line letter. BoardView is untouched; the decorator already
            // held the station DTO, so the whole fix lives in the badge that had the gap.
            for (int i = 1; i < accepted.Length; i++)
            {
                string extra = accepted[i];
                var extraShape = CatLine.ShapeOf(extra);
                float x = AcceptFirstX + (i - 1) * AcceptPitch;
                // The row still runs along board +X, NOT along the standing sign's own width.
                // Board +X projects to screen (0.869, -0.055) — almost exactly horizontal — so
                // the chips read as a level row of signage beside the primary, and every
                // clearance the chip lane worked out in anchor units carries over unchanged.
                var chipHead = new Vector3(x, PlateY, PlateZ);
                CreateMast("station:signmast-accept-" + (i - 1), stationAnchor,
                    chipHead, AcceptSize, ChipMastThickness, footLocalZ);

                var chipKeyline = CreatePlateGeometry("station:keyline-accept-" + (i - 1),
                    stationAnchor, extraShape, chipHead + SignFacing * KeylineFacingOffset,
                    AcceptKeylineSize, KeylineDepth);
                Tint(chipKeyline, KeylineCream);

                var chip = CreatePlateGeometry("station:plate-accept-" + (i - 1),
                    stationAnchor, extraShape, chipHead, AcceptSize, PlateDepth);
                Tint(chip, CatLine.ColorOf(extra));

                AddAcceptGlyph(stationAnchor, label, "station:symbol-accept-" + (i - 1),
                    chipHead + SignFacing * GlyphFacingOffset, CatLine.GlyphOf(extra));
            }
        }

        // The post under a sign. It runs from the tabletop to the BOTTOM EDGE of the plate it
        // carries, which is why it takes the plate's size rather than a height of its own: the
        // sign head is what decides where the mast stops, and a mast with an authored length
        // would either float off the plate or spear through it the moment either moved. A
        // degenerate span (a head at or below the wood) draws nothing rather than an inverted
        // box, because a negative localScale.z MIRRORS the cube and flips its winding — the
        // exact backface-culling failure this codebase has already eaten twice.
        private static void CreateMast(string name, Transform parent, Vector3 head,
            float headSize, float thickness, float footLocalZ)
        {
            float topZ = head.z + headSize * 0.5f; // the sign stands up, so its half-height is +z
            float rise = footLocalZ - topZ;
            if (rise <= 0f) return;
            CreateStationPart(name, parent,
                new Vector3(head.x, head.y, (footLocalZ + topZ) * 0.5f),
                new Vector3(thickness, thickness, rise), MastWood);
        }

        // Builtin-mesh idiom (BoardSurface / CauseCameraController): a bare GameObject with a
        // MeshFilter and MeshRenderer. No CreatePrimitive, and therefore no collider to have
        // to destroy again afterwards. Material binding is left to the caller because the
        // primary plate wears the station's own line material while everything else is
        // GreyboxMaterial.Shared plus a property block.
        private static Renderer CreatePlateGeometry(string name, Transform parent,
            DestinationShape shape, Vector3 localPosition, float size, float depth)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            // Composed, in this order and no other. PlateRotation is the SHAPE's own
            // orientation and still comes wholly from the vocabulary; StationSignRotation is
            // applied on its left, in the parent's frame, and only stands the finished plate
            // up. Swapping the operands would yaw the shape inside its own plane instead —
            // which for the circle plate looks identical and for the triangle silently puts the
            // apex somewhere else, so the order is pinned by a test rather than by this comment.
            part.transform.localRotation =
                StationSignRotation * DestinationShapeMesh.PlateRotation(shape);
            part.transform.localScale = DestinationShapeMesh.PlateScale(shape, size, depth);
            part.AddComponent<MeshFilter>().sharedMesh = DestinationShapeMesh.ForShape(shape);
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GreyboxMaterial.Shared;
            return renderer;
        }

        private static void Tint(Renderer renderer, Color color)
        {
            renderer.sharedMaterial = GreyboxMaterial.Shared;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        // The chip letters copy the primary label's font AND its font material. A TextMesh
        // whose renderer does not carry its font's material draws nothing — and copying rather
        // than allocating keeps the badge inside the no-ad-hoc-materials rule. It also ties
        // the chips to the primary honestly: wherever the station's own letter renders, these
        // render too, and wherever it doesn't, neither do they.
        private static void AddAcceptGlyph(Transform parent, TextMesh model, string name,
            Vector3 localPosition, string glyph)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = StationSignRotation;
            var text = go.AddComponent<TextMesh>();
            text.font = model.font;
            text.text = glyph;
            text.fontSize = model.fontSize;
            text.fontStyle = model.fontStyle;
            text.color = model.color;
            text.characterSize = AcceptGlyphSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            var modelRenderer = model.GetComponent<Renderer>();
            if (modelRenderer != null)
                go.GetComponent<Renderer>().sharedMaterial = modelRenderer.sharedMaterial;
        }
    }
}
