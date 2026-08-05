using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatMetro.Presentation.Input
{
    // CM-UX-01 criterion 1: deterministic chrome hit routing. Pure rect math over injected
    // screen-space rects — no input-package reference, no engine objects; TapInput stays the one
    // input consumer and consults this registry AFTER the legacy retry band and BEFORE the
    // board-disc scan (the resolution-order law). Overlaps resolve by highest priority, ties by
    // earliest registration — deterministic by construction (A-UX1-3: priorities are explicit;
    // cross-component registration order is a tie-break, not an API).
    public sealed class ChromeRegions
    {
        private struct Entry
        {
            public string Id;
            public Func<Rect> ScreenRect;
            public Action OnTap;
            public int Priority;
            public long Seq;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private long _seq;

        public int Count => _entries.Count;

        public void Register(string id, Func<Rect> screenRect, Action onTap, int priority)
        {
            // skeleton (red phase): not implemented
        }

        public bool Unregister(string id)
        {
            return false; // skeleton (red phase)
        }

        public bool TryResolve(Vector2 screenPos, out Action onTap)
        {
            onTap = null; // skeleton (red phase)
            return false;
        }
    }
}
