namespace CatMetro.Presentation.Hud
{
    // CM-UX-05 criterion 1: counts FailureReview ENTRIES per level attempt-run — pure C#
    // (no UnityEngine), edge-triggered on the observed state stream. Halted and Won entries
    // are deliberately never counted: a halt is an unexpected runtime boundary, and the
    // decompose pins the counter to FailureReview entries alone. Reset() is the per-level
    // seam — CM-UX-07/level-advance calls it when a new level loads; retries of the SAME
    // level keep accumulating (that accumulation IS the CM-R13.5 mechanic).
    // The state literal is the merged screen-state vocabulary: the strings live in Bootstrap,
    // which this assembly does not reference (the CM-UX-02 criterion-2 precedent).
    public sealed class HintAttemptCounter
    {
        public const string CountedState = "FailureReview";

        public int Count { get; private set; }

        private string _previous;

        // Edge-triggered: an ENTRY is a transition into the counted state, so consecutive
        // polled frames inside one review count once. A first observation that is already
        // the counted state is an entry (attaching mid-review must never read as zero).
        public void Observe(string state)
        {
            if (state == CountedState && _previous != CountedState) Count++;
            _previous = state;
        }

        public void Reset()
        {
            Count = 0;
            _previous = null;
        }
    }
}
