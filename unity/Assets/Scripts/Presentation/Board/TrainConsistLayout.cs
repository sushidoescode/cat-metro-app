namespace CatMetro.Presentation.Board
{
    // LOOK step 6 consist math, kept pure so EditMode tests pin the edge-boundary law without
    // a scene. The head rides an edge at headDistance; a trailing vehicle sits `offset`
    // arc-length units behind it. Presentation remembers ONE edge of history per train (the
    // sim exposes only the current EdgeId, so the view records each edge as the head leaves
    // it); an offset that crosses the current edge's start continues near the END of that
    // remembered edge, and one that falls off known history entirely clamps to the nearest
    // known start. Trade-off, written down: with a carriage offset of ~0.5 world units against
    // authored edges several units long, needing history deeper than one edge is a spawn-frame
    // or junction-instant case, and a clamp there reads as the carriage bunching into the node
    // and pulling out — the same thing a real toy train does.
    public static class TrainConsistLayout
    {
        public readonly struct Sample
        {
            public Sample(bool onPreviousEdge, float distance, bool clamped)
            {
                OnPreviousEdge = onPreviousEdge;
                Distance = distance;
                Clamped = clamped;
            }

            public bool OnPreviousEdge { get; } // which edge Distance is measured along
            public float Distance { get; }      // arc-length from that edge's start
            public bool Clamped { get; }        // the offset fell off known history
        }

        // previousEdgeLength < 0 means "no history" (a freshly spawned or slot-reused train).
        public static Sample ResolveBehind(float headDistance, float offset,
            float currentEdgeLength, float previousEdgeLength)
        {
            if (currentEdgeLength < 0f) currentEdgeLength = 0f;
            if (headDistance < 0f) headDistance = 0f;
            if (headDistance > currentEdgeLength) headDistance = currentEdgeLength;
            if (offset < 0f) offset = 0f;

            float trailing = headDistance - offset;
            if (trailing >= 0f) return new Sample(false, trailing, false);
            if (previousEdgeLength >= 0f)
            {
                float distance = previousEdgeLength + trailing; // trailing < 0 here
                if (distance >= 0f) return new Sample(true, distance, false);
                return new Sample(true, 0f, true);
            }
            return new Sample(false, 0f, true);
        }
    }
}
