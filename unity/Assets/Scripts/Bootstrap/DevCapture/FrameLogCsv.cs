#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using CatMetro.Presentation.Diagnostics;

namespace CatMetro.Bootstrap.DevCapture
{
    // CM-C3-DEVCAP criterion 1: a PURE serialiser of the existing FrameLog — columns 1-4 are the
    // FrameRecord fields copied verbatim (no arithmetic, no rounding, no re-basing; A-DEVCAP-1
    // carries A-C3-6's single-clock law), column 5 marks the causeVisible frames. RED STUB.
    public static class FrameLogCsv
    {
        public static string ToCsv(
            IReadOnlyList<FrameRecord> records, ISet<int> causeVisibleFrames)
        {
            throw new System.NotImplementedException("CM-C3-DEVCAP red");
        }
    }
}
#endif
