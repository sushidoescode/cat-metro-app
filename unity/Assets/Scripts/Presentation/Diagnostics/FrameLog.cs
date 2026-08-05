using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Diagnostics
{
    // CM-C2b criterion 3's instrumented frame log — the artifact CM-C3 criteria 2/4/7 measure
    // against. ONE clock source, named here and in every artifact citing this log:
    // UnityEngine.Time.realtimeSinceStartupAsDouble (monotonic within a session), rendered as
    // whole milliseconds. One record per rendered frame, exactly four fields.
    public sealed class FrameRecord
    {
        public readonly int FrameIndex;
        public readonly long MonotonicMs;
        public readonly int SimTick;
        public readonly string ScreenState;

        public FrameRecord(int frameIndex, long monotonicMs, int simTick, string screenState)
        {
            FrameIndex = frameIndex; MonotonicMs = monotonicMs;
            SimTick = simTick; ScreenState = screenState;
        }
    }

    public sealed class FrameLog : MonoBehaviour
    {
        private readonly List<FrameRecord> _records = new List<FrameRecord>();

        public System.Func<int> SimTickSource;       // wired by the composition root
        public System.Func<string> ScreenStateSource;

        public IReadOnlyList<FrameRecord> Records => _records;

        private void LateUpdate()
        {
            _records.Add(new FrameRecord(
                Time.frameCount,
                (long)(Time.realtimeSinceStartupAsDouble * 1000.0),
                SimTickSource != null ? SimTickSource() : -1,
                ScreenStateSource != null ? ScreenStateSource() : ""));
        }
    }
}
