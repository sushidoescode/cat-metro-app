using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    public sealed class TrackSplineGraph
    {
        private const float NodeHandleScale = 0.30f;
        private const float MinimumEdgeLength = 0.0001f;

        private readonly TrackSpline[] _paths;

        private TrackSplineGraph(TrackSpline[] paths)
        {
            _paths = paths;
        }

        public int EdgeCount => _paths.Length;
        public TrackSpline Path(int edgeIndex) => _paths[edgeIndex];

        public static TrackSplineGraph Build(Vector3[] nodePositions, int[] edgeFrom, int[] edgeTo)
        {
            if (nodePositions == null) throw new ArgumentNullException(nameof(nodePositions));
            if (edgeFrom == null) throw new ArgumentNullException(nameof(edgeFrom));
            if (edgeTo == null) throw new ArgumentNullException(nameof(edgeTo));
            if (edgeFrom.Length != edgeTo.Length)
                throw new ArgumentException("Track edge endpoint arrays must have equal lengths.");

            var incomingCount = new int[nodePositions.Length];
            var outgoingCount = new int[nodePositions.Length];
            var incomingDirection = new Vector3[nodePositions.Length];
            var outgoingDirection = new Vector3[nodePositions.Length];
            var minimumIncidentLength = new float[nodePositions.Length];
            for (int n = 0; n < minimumIncidentLength.Length; n++)
                minimumIncidentLength[n] = float.PositiveInfinity;

            for (int edge = 0; edge < edgeFrom.Length; edge++)
            {
                int from = edgeFrom[edge];
                int to = edgeTo[edge];
                if ((uint)from >= (uint)nodePositions.Length ||
                    (uint)to >= (uint)nodePositions.Length)
                    throw new ArgumentOutOfRangeException(nameof(edgeFrom),
                        "Track edge references a node outside the authored graph.");

                Vector3 chord = nodePositions[to] - nodePositions[from];
                float length = chord.magnitude;
                if (length < MinimumEdgeLength)
                    throw new ArgumentException("Track edges must connect distinct node positions.");

                Vector3 direction = chord / length;
                outgoingCount[from]++;
                outgoingDirection[from] += direction;
                incomingCount[to]++;
                incomingDirection[to] += direction;
                minimumIncidentLength[from] = Mathf.Min(minimumIncidentLength[from], length);
                minimumIncidentLength[to] = Mathf.Min(minimumIncidentLength[to], length);
            }

            var nodeTangent = new Vector3[nodePositions.Length];
            var nodeHandle = new float[nodePositions.Length];
            for (int node = 0; node < nodePositions.Length; node++)
            {
                nodeTangent[node] = ResolveNodeTangent(
                    incomingCount[node], outgoingCount[node],
                    incomingDirection[node], outgoingDirection[node]);
                nodeHandle[node] = float.IsPositiveInfinity(minimumIncidentLength[node])
                    ? 0f
                    : minimumIncidentLength[node] * NodeHandleScale;
            }

            var paths = new TrackSpline[edgeFrom.Length];
            for (int edge = 0; edge < paths.Length; edge++)
            {
                int from = edgeFrom[edge];
                int to = edgeTo[edge];
                paths[edge] = new TrackSpline(
                    nodePositions[from], nodePositions[to],
                    nodeTangent[from], nodeTangent[to],
                    nodeHandle[from], nodeHandle[to]);
            }
            return new TrackSplineGraph(paths);
        }

        private static Vector3 ResolveNodeTangent(int incomingCount, int outgoingCount,
            Vector3 incomingDirection, Vector3 outgoingDirection)
        {
            Vector3 incoming = NormalizedOrZero(incomingDirection);
            Vector3 outgoing = NormalizedOrZero(outgoingDirection);

            if (incomingCount == 1 && outgoingCount > 1)
                return incoming;
            if (incomingCount > 1 && outgoingCount == 1)
                return outgoing;
            if (incomingCount > 0 && outgoingCount > 0)
            {
                Vector3 through = incoming + outgoing;
                if (through.sqrMagnitude > 0.000001f)
                    return through.normalized;
                return outgoing.sqrMagnitude > 0f ? outgoing : incoming;
            }
            if (outgoingCount > 0) return outgoing;
            return incoming;
        }

        private static Vector3 NormalizedOrZero(Vector3 value) =>
            value.sqrMagnitude > 0.000001f ? value.normalized : Vector3.zero;
    }

    public sealed class TrackSpline
    {
        private const int MinimumSubdivisionDepth = 4;
        private const int MaximumSubdivisionDepth = 10;
        private const float FlatnessTolerance = 0.001f;

        private readonly Vector3 _p0;
        private readonly Vector3 _p1;
        private readonly Vector3 _p2;
        private readonly Vector3 _p3;
        private readonly float[] _sampleT;
        private readonly float[] _sampleDistance;

        internal TrackSpline(Vector3 start, Vector3 end,
            Vector3 startTangent, Vector3 endTangent,
            float startHandle, float endHandle)
            : this(start, start + startTangent * startHandle,
                end - endTangent * endHandle, end)
        {
        }

        private TrackSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _p0 = p0;
            _p1 = p1;
            _p2 = p2;
            _p3 = p3;

            var samples = new List<ArcSample>(32) { new ArcSample(0f, _p0) };
            Subdivide(_p0, _p1, _p2, _p3, 0f, 1f, 0, samples);
            _sampleT = new float[samples.Count];
            _sampleDistance = new float[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                _sampleT[i] = samples[i].T;
                if (i > 0)
                    _sampleDistance[i] = _sampleDistance[i - 1]
                        + Vector3.Distance(samples[i - 1].Position, samples[i].Position);
            }
            Length = _sampleDistance[_sampleDistance.Length - 1];
        }

        public float Length { get; }

        public Vector3 Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return u * u * u * _p0
                + 3f * u * u * t * _p1
                + 3f * u * t * t * _p2
                + t * t * t * _p3;
        }

        public Vector3 Derivative(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 3f * u * u * (_p1 - _p0)
                + 6f * u * t * (_p2 - _p1)
                + 3f * t * t * (_p3 - _p2);
        }

        public Vector3 Tangent(float t)
        {
            Vector3 derivative = Derivative(t);
            return derivative.sqrMagnitude > 0.000001f ? derivative.normalized : Vector3.up;
        }

        public Vector3 EvaluateDistanceFraction(float fraction) =>
            Evaluate(ParameterAtDistanceFraction(fraction));

        public Vector3 TangentDistanceFraction(float fraction) =>
            Tangent(ParameterAtDistanceFraction(fraction));

        internal TrackSpline CreateLateralRail(float offset)
        {
            Vector3 startLateral = Lateral(Tangent(0f));
            Vector3 endLateral = Lateral(Tangent(1f));
            return new TrackSpline(
                _p0 + startLateral * offset,
                _p1 + startLateral * offset,
                _p2 + endLateral * offset,
                _p3 + endLateral * offset);
        }

        private static Vector3 Lateral(Vector3 tangent) =>
            new Vector3(-tangent.y, tangent.x, 0f).normalized;

        private float ParameterAtDistanceFraction(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Length <= 0f || fraction <= 0f) return 0f;
            if (fraction >= 1f) return 1f;

            float target = Length * fraction;
            int upper = Array.BinarySearch(_sampleDistance, target);
            if (upper >= 0) return _sampleT[upper];
            upper = ~upper;
            int lower = upper - 1;
            float span = _sampleDistance[upper] - _sampleDistance[lower];
            float alpha = span > 0f ? (target - _sampleDistance[lower]) / span : 0f;
            return Mathf.Lerp(_sampleT[lower], _sampleT[upper], alpha);
        }

        private static void Subdivide(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
            float t0, float t1, int depth, List<ArcSample> samples)
        {
            float chord = Vector3.Distance(p0, p3);
            float polygon = Vector3.Distance(p0, p1)
                + Vector3.Distance(p1, p2)
                + Vector3.Distance(p2, p3);
            bool flatEnough = depth >= MinimumSubdivisionDepth
                && polygon - chord <= FlatnessTolerance;
            if (flatEnough || depth >= MaximumSubdivisionDepth)
            {
                samples.Add(new ArcSample(t1, p3));
                return;
            }

            Vector3 p01 = (p0 + p1) * 0.5f;
            Vector3 p12 = (p1 + p2) * 0.5f;
            Vector3 p23 = (p2 + p3) * 0.5f;
            Vector3 p012 = (p01 + p12) * 0.5f;
            Vector3 p123 = (p12 + p23) * 0.5f;
            Vector3 midpoint = (p012 + p123) * 0.5f;
            float tm = (t0 + t1) * 0.5f;
            Subdivide(p0, p01, p012, midpoint, t0, tm, depth + 1, samples);
            Subdivide(midpoint, p123, p23, p3, tm, t1, depth + 1, samples);
        }

        private readonly struct ArcSample
        {
            public ArcSample(float t, Vector3 position)
            {
                T = t;
                Position = position;
            }

            public float T { get; }
            public Vector3 Position { get; }
        }
    }
}
