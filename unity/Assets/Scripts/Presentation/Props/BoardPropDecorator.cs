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
                        station.Accepts);
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
                new Vector3(maxX + 0.4f, minY - 0.35f, contactZ));

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

        private static void AddStationArchitecture(Transform kiosk, Transform stationAnchor)
        {
            var fallback = stationAnchor.GetComponent<Renderer>();
            Color lineColor = fallback != null && fallback.sharedMaterial != null
                ? fallback.material.color : Color.magenta;

            var model = kiosk.Find("Model");
            Bounds bounds = LocalRendererBounds(kiosk, model);
            float baseWidth = Mathf.Clamp(bounds.size.x * 1.05f, 0.9f, 2.1f);
            float baseDepth = Mathf.Clamp(bounds.size.y * 1.05f, 0.75f, 1.7f);
            CreateStationPart("station:wood-base", kiosk,
                PrimitiveType.Cube,
                new Vector3(bounds.center.x, bounds.center.y, -0.055f),
                new Vector3(baseWidth, baseDepth, 0.11f),
                new Color(0.64f, 0.43f, 0.29f));

            // The generated kiosk remains neutral licensed artwork. This thin project-owned
            // cap makes the station's route colour unmistakable without editing the pinned FBX
            // or relying on its baked blue sign for gameplay semantics.
            float roofWidth = Mathf.Clamp(bounds.size.x * 0.82f, 0.75f, 1.65f);
            float roofDepth = Mathf.Clamp(bounds.size.y * 0.72f, 0.6f, 1.35f);
            CreateStationPart("station:line-roof", kiosk,
                PrimitiveType.Cube,
                new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 0.035f),
                new Vector3(roofWidth, roofDepth, 0.07f), lineColor);
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

        // One primitive site and one committed-material bind preserves the project's runtime
        // primitive gate; property blocks give each platform its own colour without a material
        // allocation on every Retry/LoadNext rebuild.
        private static Transform CreateStationPart(string name, Transform parent,
            PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
            }
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = GreyboxMaterial.Shared;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
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
            ReadOnlyMemory<string> accepts)
        {
            // The kiosk supplies the neutral building. Keep only line-owned overlays: the
            // main branch's text symbol and the diorama branch's colored plate/keyline/symbol.
            EnsureProjectOwnedStationPlate(stationAnchor, accepts);
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
                if (name.StartsWith("station:plate") || name.StartsWith("station:keyline")
                    || name.StartsWith("station:symbol"))
                    continue;
                renderer.enabled = false;
            }
        }

        // --- the station badge: the only surface that tells a player where a berth goes ---

        // Depths, unchanged from the shipped badge: the cream keyline sits BEHIND the coloured
        // plate and reads as a halo around it, and the glyph sits in front of both. More
        // negative is nearer the camera.
        private const float PlateZ = -1.35f;
        private const float KeylineZ = -1.32f;
        private const float GlyphZ = -1.44f;
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
        // BoardSurface.Margin is only 1.05, so a row clearing it vertically would hang off the
        // cream rim. And at the clearance the keylines actually need, the two cream halos
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

        // The shipped keyline cream, hoisted verbatim rather than swapped for a Palette token.
        // It is deliberately warmer and darker than Palette.CreamCard because the board top IS
        // CreamCard — a keyline in the token would vanish into the surface it has to separate
        // the badge from. Retinting a shipped colour is a palette decision, not a legibility
        // lane's; flagged for whoever owns the palette rather than changed here.
        private static readonly Color KeylineCream = new Color(0.94f, 0.88f, 0.75f);

        private static void EnsureProjectOwnedStationPlate(Transform stationAnchor,
            ReadOnlyMemory<string> accepts)
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
            var keyline = CreatePlateGeometry("station:keyline-generated", stationAnchor,
                shape, new Vector3(0f, PlateY, KeylineZ), KeylineSize, KeylineDepth);
            Tint(keyline, KeylineCream);

            var plate = CreatePlateGeometry("station:plate-generated", stationAnchor,
                shape, new Vector3(0f, PlateY, PlateZ), PlateSize, PlateDepth);
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

            label.transform.localPosition = new Vector3(0f, PlateY, GlyphZ);
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

                var chipKeyline = CreatePlateGeometry("station:keyline-accept-" + (i - 1),
                    stationAnchor, extraShape, new Vector3(x, PlateY, KeylineZ),
                    AcceptKeylineSize, KeylineDepth);
                Tint(chipKeyline, KeylineCream);

                var chip = CreatePlateGeometry("station:plate-accept-" + (i - 1),
                    stationAnchor, extraShape, new Vector3(x, PlateY, PlateZ),
                    AcceptSize, PlateDepth);
                Tint(chip, CatLine.ColorOf(extra));

                AddAcceptGlyph(stationAnchor, label, "station:symbol-accept-" + (i - 1),
                    new Vector3(x, PlateY, GlyphZ), CatLine.GlyphOf(extra));
            }
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
            part.transform.localRotation = DestinationShapeMesh.PlateRotation(shape);
            part.transform.localScale = DestinationShapeMesh.PlateScale(shape, size, depth);
            part.AddComponent<MeshFilter>().sharedMesh = DestinationShapeMesh.ForShape(shape);
            return part.AddComponent<MeshRenderer>();
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
