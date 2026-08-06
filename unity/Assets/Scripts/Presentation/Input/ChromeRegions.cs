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
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("region id is required");
            if (screenRect == null) throw new ArgumentException("screenRect provider is required");
            if (onTap == null) throw new ArgumentException("onTap action is required");
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == id)
                    throw new ArgumentException(
                        "duplicate region id '" + id + "' — a wiring defect, never a silent replace");
            _entries.Add(new Entry
            {
                Id = id, ScreenRect = screenRect, OnTap = onTap, Priority = priority, Seq = _seq++
            });
        }

        public bool Unregister(string id)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    _entries.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        // Highest priority containing the point wins; ties go to the EARLIEST registration.
        // The rect provider is consulted per call — regions track their live layout.
        public bool TryResolve(Vector2 screenPos, out Action onTap)
        {
            onTap = null;
            int bestPriority = 0;
            long bestSeq = 0;
            bool found = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].ScreenRect().Contains(screenPos)) continue;
                if (!found
                    || _entries[i].Priority > bestPriority
                    || (_entries[i].Priority == bestPriority && _entries[i].Seq < bestSeq))
                {
                    found = true;
                    bestPriority = _entries[i].Priority;
                    bestSeq = _entries[i].Seq;
                    onTap = _entries[i].OnTap;
                }
            }
            return found;
        }
    }
}
