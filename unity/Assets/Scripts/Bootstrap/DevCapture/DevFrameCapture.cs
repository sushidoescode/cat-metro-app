#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Bootstrap.DevCapture
{
    // CM-C3-DEVCAP criteria 2/3: observes the composition root's public state (never edits
    // Presentation, never reads a clock — time enters only as FrameRecord copies), latches one
    // causeVisible mark per FailureReview entry, and writes the CSV exactly once, in
    // OnApplicationPause(true) — the only contamination-free flush point (A-DEVCAP-7). RED STUB.
    public sealed class DevFrameCapture : MonoBehaviour
    {
        private readonly HashSet<int> _marks = new HashSet<int>();

        public string OutputDirectory; // criterion 3: injectable; default set at wire time
        public IReadOnlyCollection<int> CauseVisibleFrames => _marks;

        public void Wire(GameRoot root)
        {
            // red stub: no observation, no default directory
        }

        public void OnApplicationPause(bool paused)
        {
            // red stub: no write
        }
    }
}
#endif
