using System;
using System.Collections.Generic;
using CatMetro.Content;

namespace CatMetro.Presentation.Hud.WavePreview
{
    // One cat that has not been emitted yet, as the HUD needs to draw it.
    public readonly struct UpcomingCat
    {
        public readonly string Color;
        public readonly int Tick;      // the tick this cat is emitted on
        public readonly int WaveIndex; // authored wave it belongs to (tie-break + debugging)

        public UpcomingCat(string color, int tick, int waveIndex)
        {
            Color = color; Tick = tick; WaveIndex = waveIndex;
        }
    }

    // HUD-WAVE: the read-only projection from authored waves to the queue of individual cats
    // the preview capsule paints as faces.
    //
    // The session exposes waves, not cats: a WaveDto is (Tick, SourceNode, Color, Count,
    // SpacingTicks), so the old strip could only say "red x2". But the emission schedule is
    // fully determined by that DTO — wave w emits its i-th cat at w.Tick + i * w.SpacingTicks —
    // so the per-cat queue the target art shows is DERIVABLE here in presentation without
    // reaching into the Domain or asking the session for anything new. Pure static math on
    // injected inputs (the HudBands idiom): no Unity types, no session handle, no Screen reads.
    //
    // Ordering is stable: emission tick, then authored wave index, then index within the wave.
    // Two waves scheduled on the same tick therefore keep their authoring order forever, which
    // is what makes a face-order assertion in a test meaningful.
    public static class UpcomingCats
    {
        // Cats whose emission tick has NOT passed, soonest first, capped at `max`.
        // `max` <= 0 returns an empty list. Matches the old strip's pending rule: a cat is
        // still upcoming while its emission tick is >= the current tick.
        public static List<UpcomingCat> Next(ReadOnlyMemory<WaveDto> waves, int tick, int max)
        {
            var queue = new List<UpcomingCat>();
            if (max <= 0) return queue;
            AppendAll(waves, tick, queue);
            Sort(queue);
            if (queue.Count > max) queue.RemoveRange(max, queue.Count - max);
            return queue;
        }

        // Every cat still to come, ignoring the display cap — the count the overflow chip and
        // the "cats still to arrive" counter both need.
        public static int RemainingCount(ReadOnlyMemory<WaveDto> waves, int tick)
        {
            int n = 0;
            var span = waves.Span;
            for (int w = 0; w < span.Length; w++)
            {
                var wave = span[w];
                if (wave == null) continue;
                for (int i = 0; i < wave.Count; i++)
                    if (wave.Tick + i * wave.SpacingTicks >= tick) n++;
            }
            return n;
        }

        private static void AppendAll(ReadOnlyMemory<WaveDto> waves, int tick,
            List<UpcomingCat> into)
        {
            var span = waves.Span;
            for (int w = 0; w < span.Length; w++)
            {
                var wave = span[w];
                if (wave == null) continue;
                for (int i = 0; i < wave.Count; i++)
                {
                    int emission = wave.Tick + i * wave.SpacingTicks;
                    if (emission >= tick) into.Add(new UpcomingCat(wave.Color, emission, w));
                }
            }
        }

        // List.Sort is introsort — NOT stable — so the tie-break is written out explicitly
        // rather than left to the algorithm. Cats from one wave are appended in emission order,
        // so within a wave the tick comparison already orders them; the wave index settles two
        // different waves that share a tick.
        private static void Sort(List<UpcomingCat> queue)
        {
            queue.Sort((a, b) => a.Tick != b.Tick
                ? a.Tick.CompareTo(b.Tick)
                : a.WaveIndex.CompareTo(b.WaveIndex));
        }
    }
}
