using System;
using System.Collections.Generic;
using CatMetro.Content;
using CatMetro.Presentation.Board;
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
                    SuppressReplacedStationArchitecture(anchors[station.NodeId]);
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

        private static void SuppressReplacedStationArchitecture(Transform stationAnchor)
        {
            // The kiosk supplies the neutral building. Keep only line-owned overlays: the
            // main branch's text symbol and the diorama branch's colored plate/keyline/symbol.
            EnsureProjectOwnedStationPlate(stationAnchor);
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

        private static void EnsureProjectOwnedStationPlate(Transform stationAnchor)
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

            bool isRedCircle = label.text == "R";
            var plate = GameObject.CreatePrimitive(
                isRedCircle ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            plate.name = "station:plate-generated";
            plate.transform.SetParent(stationAnchor, false);
            // Put the shape plaque on the clear board apron in front of the kiosk. Centering it
            // on the line-coloured roof makes the red circle and blue square both read as the
            // same rectangle in the production near-orthographic view.
            plate.transform.localPosition = new Vector3(0f, -1f, -1.35f);
            plate.transform.localRotation = isRedCircle
                ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            plate.transform.localScale = isRedCircle
                ? new Vector3(0.9f, 0.05f, 0.9f)
                : new Vector3(0.9f, 0.9f, 0.1f);
            var plateRenderer = plate.GetComponent<Renderer>();
            var committedMaterial = GreyboxMaterial.Shared;
            plateRenderer.sharedMaterial = fallback.sharedMaterial != null
                ? fallback.sharedMaterial : committedMaterial;
            var collider = plate.GetComponent<Collider>();
            if (collider != null)
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
            }

            var keyline = CreateStationPart("station:keyline-generated", stationAnchor,
                isRedCircle ? PrimitiveType.Cylinder : PrimitiveType.Cube,
                new Vector3(0f, -1f, -1.32f),
                isRedCircle ? new Vector3(1.08f, 0.055f, 1.08f)
                    : new Vector3(1.08f, 1.08f, 0.11f),
                new Color(0.94f, 0.88f, 0.75f));
            keyline.localRotation = isRedCircle
                ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;

            label.transform.localPosition = new Vector3(0f, -1f, -1.44f);
            label.characterSize = 0.5f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
        }
    }
}
