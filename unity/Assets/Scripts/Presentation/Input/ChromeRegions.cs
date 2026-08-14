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
        // CM-LOADNEXT D-1 + #46-F9 absorption: the documented registration-priority ladder —
        // explicit values are the law (A-UX1-3), never a registration-order accident. A parent
        // screen registers at ParentPriority; an urgent full-screen escape (the halt region) at
        // HaltEscapePriority; a standalone modal (ResultsPanel) at ModalPriority; a
        // ScreenStack-hosted modal (LevelIntroSheet) at StackedModalPriority — one tier above
        // ModalPriority because its ScreensCanvas paints ABOVE ResultsPanel's canvas
        // (sortingOrder 120 vs 110, GameRoot.cs): the tap law matches the paint law, so
        // whichever modal is visually on top also wins the tap, by priority, never by which one
        // happened to register first (state/handoffs/CM-UX-07-delta-audit.md D-1).
        // CM-BOOT-HOME criterion 4 (the priority-debt fix, formerly a KNOWN EXCEPTION here):
        // HomeScreenView's OWN pin (the L001 entry) is the one case where "the tap law matches
        // the paint law" used to be FALSE — its ScreensCanvas (120) paints ABOVE ResultsPanel's
        // canvas (110) exactly like a modal would, yet the pin registered at the lower
        // ParentPriority (0) under the separate modal-over-parent law, so ResultsPanel (10)
        // could outrank a visually-on-top Home pin. Dev-only and unreachable before this
        // contract (state/handoffs/CM-LOADNEXT-frozen-contract.md's Known-debt entry); CM-BOOT-
        // HOME promotes Home to the shipped default, so this would have shipped as a live tap
        // bug the day two tap-eligible regions actually coexisted at the same screen point.
        // Fixed via HomeScreenPriority, below.
        public const int ParentPriority = 0;
        public const int HaltEscapePriority = 5;
        public const int ModalPriority = 10;
        // CM-BOOT-HOME criterion 4: HomeScreenView's L001 pin sits BETWEEN the two modal tiers —
        // strictly above ModalPriority(10) so a visually-on-top Home pin can never lose a tap
        // tie-break to ResultsPanel (the debt this criterion exists to fix), and strictly BELOW
        // StackedModalPriority so a ScreenStack-hosted modal pushed OVER Home still wins the
        // overlap. That second half is load-bearing and was learned the hard way: an earlier
        // revision of this fix put the pin at the TOP of the ladder (StackedModalPriority + 1)
        // on the reasoning that Home and LevelIntroSheet are "mutually exclusive by construction"
        // (LevelSelected hides Home before showing Intro). Production does behave that way, but
        // ScreenCoRegistrationTests pins the LAW for the co-registered case — and with the pin
        // on top, the Home pin stole the sheet's Play chip: playRequested was 0, i.e. THE GAME
        // WAS NOT STARTABLE FROM THE SHEET. The stacked modal must outrank the parent screen's
        // pin, always. StackedModalPriority therefore moves up one tier to preserve that.
        public const int HomeScreenPriority = ModalPriority + 1;        // 11
        public const int StackedModalPriority = HomeScreenPriority + 1; // 12

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
