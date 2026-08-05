#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CatMetro.Bootstrap.DevCapture
{
    // CM-C3-DEVCAP criteria 2/3: observes the composition root's PUBLIC state — never edits
    // Presentation, never reads a clock (time enters this file only as FrameRecord copies) —
    // latches exactly one causeVisible mark per FailureReview entry on the first frame the
    // editor-protocol predicate holds (CauseCam.IsFramed && Banner.Visible, the identical
    // predicate to the editor budget harness), and writes the CSV exactly once, in
    // OnApplicationPause(true): the only contamination-free flush point (A-DEVCAP-7) — no
    // capture I/O can ever land inside a measured interval.
    //
    // Execution order AFTER FrameLog (which records in LateUpdate at default order): the mark
    // must be the frame index of the record the log just appended for THIS frame, so this
    // component's LateUpdate is pushed later in the phase.
    [DefaultExecutionOrder(1000)]
    public sealed class DevFrameCapture : MonoBehaviour
    {
        private readonly HashSet<int> _marks = new HashSet<int>();
        private GameRoot _root;
        private bool _latched;

        public string OutputDirectory; // criterion 3: injectable for tests; defaulted below
        public IReadOnlyCollection<int> CauseVisibleFrames => _marks;

        private void Awake()
        {
            // criterion 3(d): the default holds even on a bare component, before any wiring
            if (string.IsNullOrEmpty(OutputDirectory))
                OutputDirectory = Path.Combine(
                    UnityEngine.Application.persistentDataPath, "devcap");
        }

        public void Wire(GameRoot root)
        {
            _root = root;
        }

        private void LateUpdate()
        {
            if (_root == null || _root.Log == null) return;
            if (_root.ScreenState != "FailureReview")
            {
                _latched = false; // the latch resets on leaving FailureReview: 20 fails, 20 marks
                return;
            }
            if (_latched || _root.CauseCam == null || _root.Banner == null) return;
            if (!_root.CauseCam.IsFramed || !_root.Banner.Visible) return;
            var recs = _root.Log.Records;
            if (recs.Count == 0) return;
            _marks.Add(recs[recs.Count - 1].FrameIndex);
            _latched = true;
        }

        public void OnApplicationPause(bool paused)
        {
            if (!paused || _root == null || _root.Log == null) return;
            Directory.CreateDirectory(OutputDirectory);
            string path = Path.Combine(OutputDirectory, "framelog.csv");
            File.WriteAllText(path, FrameLogCsv.ToCsv(_root.Log.Records, _marks));
            // the runbook's greppable marker (SEAM_LOADED precedent): path, rows, marks
            Debug.Log("DEVCAP_WRITTEN " + path + " rows=" + _root.Log.Records.Count
                + " marks=" + _marks.Count);
        }
    }
}
#endif
