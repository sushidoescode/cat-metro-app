using System.Collections.Generic;
using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    public static class ToyTrackMeshBuilder
    {
        private const float SleeperLength = 0.90f;
        private const float SleeperWidth = 0.18f;
        private const float SleeperSpacing = 0.42f;
        private const float SleeperEndInset = 0.58f;
        private const float SleeperFrontZ = 0.16f;
        private const float SleeperBackZ = 0.33f;
        private const float SleeperCorner = 0.035f;

        private const float RailOffset = 0.25f;
        private const float RailWidth = 0.11f;
        private const float RailFrontZ = 0.035f;
        private const float RailShoulderZ = 0.065f;
        private const float RailBackZ = 0.165f;
        private const float RailBevel = 0.025f;
        private const float RailSampleSpacing = 0.16f;

        private static Material _sleeperMaterial;
        private static Material _railMaterial;

        public static GameObject Build(string edgeId, TrackSpline path, Transform parent)
        {
            var vertices = new List<Vector3>(512);
            var sleeperTriangles = new List<int>(768);
            var railTriangles = new List<int>(768);

            AppendSleepers(path, vertices, sleeperTriangles);
            AppendRail(path, -RailOffset, vertices, railTriangles);
            AppendRail(path, RailOffset, vertices, railTriangles);

            var mesh = new Mesh { name = "Toy track " + edgeId };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sleeperTriangles, 0);
            mesh.SetTriangles(railTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var root = new GameObject("edge:" + edgeId);
            root.transform.SetParent(parent, false);
            var id = root.AddComponent<BoardElementId>();
            id.Id = edgeId;
            id.Kind = "edge";
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { SleeperMaterial(), RailMaterial() };
            root.AddComponent<GeneratedTrackMeshOwner>().Mesh = mesh;
            return root;
        }

        private static Material SleeperMaterial()
        {
            if (_sleeperMaterial == null)
                _sleeperMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Track — Cream Sleepers", Palette.CreamCard);
            return _sleeperMaterial;
        }

        private static Material RailMaterial()
        {
            if (_railMaterial == null)
                _railMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Track — Navy Rails", Palette.InkNavy);
            return _railMaterial;
        }

        private static void AppendSleepers(TrackSpline path,
            List<Vector3> vertices, List<int> triangles)
        {
            float usableLength = Mathf.Max(0f, path.Length - 2f * SleeperEndInset);
            int count = Mathf.Max(1, Mathf.FloorToInt(usableLength / SleeperSpacing) + 1);
            for (int sleeper = 0; sleeper < count; sleeper++)
            {
                float distance = count == 1
                    ? path.Length * 0.5f
                    : SleeperEndInset + usableLength * sleeper / (count - 1);
                float fraction = path.Length > 0f ? distance / path.Length : 0f;
                Vector3 centre = path.EvaluateDistanceFraction(fraction);
                Vector3 tangent = path.TangentDistanceFraction(fraction);
                Vector3 lateral = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                AppendChamferedSleeper(centre, tangent, lateral, vertices, triangles);
            }
        }

        private static void AppendChamferedSleeper(Vector3 centre,
            Vector3 tangent, Vector3 lateral,
            List<Vector3> vertices, List<int> triangles)
        {
            float halfLength = SleeperLength * 0.5f;
            float halfWidth = SleeperWidth * 0.5f;
            var footprint = new[]
            {
                new Vector2(-halfLength + SleeperCorner, -halfWidth),
                new Vector2(halfLength - SleeperCorner, -halfWidth),
                new Vector2(halfLength, -halfWidth + SleeperCorner),
                new Vector2(halfLength, halfWidth - SleeperCorner),
                new Vector2(halfLength - SleeperCorner, halfWidth),
                new Vector2(-halfLength + SleeperCorner, halfWidth),
                new Vector2(-halfLength, halfWidth - SleeperCorner),
                new Vector2(-halfLength, -halfWidth + SleeperCorner),
            };

            int start = vertices.Count;
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? SleeperFrontZ : SleeperBackZ;
                for (int i = 0; i < footprint.Length; i++)
                    vertices.Add(centre + lateral * footprint[i].x
                        + tangent * footprint[i].y + Vector3.forward * z);
            }

            for (int i = 1; i < footprint.Length - 1; i++)
            {
                AddTriangle(triangles, start, start + i, start + i + 1);
                AddTriangle(triangles, start + 8, start + 8 + i + 1, start + 8 + i);
            }
            for (int i = 0; i < footprint.Length; i++)
            {
                int next = (i + 1) % footprint.Length;
                AddTriangle(triangles, start + i, start + 8 + i, start + 8 + next);
                AddTriangle(triangles, start + i, start + 8 + next, start + next);
            }
        }

        private static void AppendRail(TrackSpline path, float offset,
            List<Vector3> vertices, List<int> triangles)
        {
            TrackSpline railPath = path.CreateLateralRail(offset);
            int segments = Mathf.Clamp(
                Mathf.CeilToInt(railPath.Length / RailSampleSpacing), 8, 64);
            var crossSection = new[]
            {
                new Vector2(-RailWidth * 0.5f + RailBevel, RailFrontZ),
                new Vector2(RailWidth * 0.5f - RailBevel, RailFrontZ),
                new Vector2(RailWidth * 0.5f, RailShoulderZ),
                new Vector2(RailWidth * 0.5f, RailBackZ),
                new Vector2(-RailWidth * 0.5f, RailBackZ),
                new Vector2(-RailWidth * 0.5f, RailShoulderZ),
            };

            int start = vertices.Count;
            for (int segment = 0; segment <= segments; segment++)
            {
                float fraction = (float)segment / segments;
                Vector3 centre = railPath.EvaluateDistanceFraction(fraction);
                Vector3 tangent = railPath.TangentDistanceFraction(fraction);
                Vector3 lateral = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                for (int i = 0; i < crossSection.Length; i++)
                    vertices.Add(centre + lateral * crossSection[i].x
                        + Vector3.forward * crossSection[i].y);
            }

            int ring = crossSection.Length;
            for (int segment = 0; segment < segments; segment++)
            {
                int a = start + segment * ring;
                int b = a + ring;
                for (int i = 0; i < ring; i++)
                {
                    int next = (i + 1) % ring;
                    AddTriangle(triangles, a + i, b + next, b + i);
                    AddTriangle(triangles, a + i, a + next, b + next);
                }
            }
            for (int i = 1; i < ring - 1; i++)
            {
                AddTriangle(triangles, start, start + i + 1, start + i);
                int end = start + segments * ring;
                AddTriangle(triangles, end, end + i, end + i + 1);
            }
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }

    [ExecuteAlways]
    internal sealed class GeneratedTrackMeshOwner : MonoBehaviour
    {
        public Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh == null) return;
            if (UnityEngine.Application.IsPlaying(gameObject)) Destroy(Mesh);
            else DestroyImmediate(Mesh);
            Mesh = null;
        }
    }
}
