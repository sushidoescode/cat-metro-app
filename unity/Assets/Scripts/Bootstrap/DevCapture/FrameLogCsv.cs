#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CatMetro.Presentation.Diagnostics;

namespace CatMetro.Bootstrap.DevCapture
{
    // CM-C3-DEVCAP criterion 1: a PURE serialiser of the existing FrameLog — columns 1-4 are the
    // FrameRecord fields copied verbatim (no arithmetic, no rounding, no re-basing; A-DEVCAP-1
    // carries A-C3-6's single-clock law), column 5 marks the causeVisible frames. The devcap
    // wrapper's clock-token grep is what keeps this file from ever growing a clock of its own.
    public static class FrameLogCsv
    {
        public static string ToCsv(
            IReadOnlyList<FrameRecord> records, ISet<int> causeVisibleFrames)
        {
            var sb = new StringBuilder();
            sb.Append("frameIndex,monotonicMs,simTick,screenState,causeVisible\n");
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                sb.Append(r.FrameIndex).Append(',')
                  .Append(r.MonotonicMs).Append(',')
                  .Append(r.SimTick).Append(',')
                  .Append(Quote(r.ScreenState)).Append(',')
                  .Append(causeVisibleFrames.Contains(r.FrameIndex) ? '1' : '0')
                  .Append('\n');
            }
            return sb.ToString();
        }

        // CSV-quoted only if the state contains ',' or '"' (quotes doubled). No shipped screen
        // state does; the pure test pins the pathological case anyway.
        private static string Quote(string s)
        {
            if (s == null) return "";
            if (s.IndexOf(',') < 0 && s.IndexOf('"') < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
#endif
