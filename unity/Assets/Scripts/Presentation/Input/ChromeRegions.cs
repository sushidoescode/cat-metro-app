using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Input
{
    // Presentation-only feedback attached to a resolved chrome region. WoodTap is the default
    // for every existing button registration; None is reserved for consuming surfaces such as a
    // modal blocker that are intentionally not controls.
    public enum ChromeFeedback : byte
    {
        None = 0,
        WoodTap = 1,
    }

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
            public ChromeFeedback Feedback;
            public RectTransform Visual;
            public Graphic Face;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private long _seq;

        public int Count => _entries.Count;
        public event Action StackedModalChanged;
        public bool HasStackedModal
        {
            get
            {
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].Priority >= StackedModalPriority) return true;
                return false;
            }
        }

        public bool ContainsRegistration(long registration)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Seq == registration) return true;
            return false;
        }
        // The chip factory supplies the painted root after registering its tap region.
        public void BindVisual(string id, RectTransform target, Graphic face = null)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == id)
                {
                    var entry = _entries[i];
                    entry.Visual = target;
                    entry.Face = face != null ? face : target != null ? target.GetComponent<Graphic>() : null;
                    _entries[i] = entry;
                    return;
                }
            throw new ArgumentException("unregistered region '" + id + "'");
        }

        // Diagnostic query for lifecycle tests and device self-tests. Counts cannot prove that
        // a particular painted target was removed once a screen gains another legitimate entry.
        public bool IsRegistered(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == id) return true;
            return false;
        }

        public void Register(string id, Func<Rect> screenRect, Action onTap, int priority)
        {
            Register(id, screenRect, onTap, priority, ChromeFeedback.WoodTap);
        }

        // Feedback is presentation metadata only. It never participates in hit resolution or
        // action ordering, so adding it cannot change which region consumes a tap.
        public void Register(string id, Func<Rect> screenRect, Action onTap, int priority,
            ChromeFeedback feedback)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("region id is required");
            if (screenRect == null) throw new ArgumentException("screenRect provider is required");
            if (onTap == null) throw new ArgumentException("onTap action is required");
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == id)
                    throw new ArgumentException(
                        "duplicate region id '" + id + "' — a wiring defect, never a silent replace");
            bool hadModal = HasStackedModal;
            _entries.Add(new Entry
            {
                Id = id,
                ScreenRect = screenRect,
                OnTap = onTap,
                Priority = priority,
                Seq = _seq++,
                Feedback = feedback,
            });
            if (!hadModal && HasStackedModal) StackedModalChanged?.Invoke();
        }

        public bool Unregister(string id)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    bool wasModal = _entries[i].Priority >= StackedModalPriority;
                    _entries.RemoveAt(i);
                    if (wasModal && !HasStackedModal) StackedModalChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        // Highest priority containing the point wins; ties go to the EARLIEST registration.
        // The rect provider is consulted per call — regions track their live layout.
        public bool TryResolve(Vector2 screenPos, out Action onTap) =>
            TryResolve(screenPos, out onTap, out _);

        // API-compatible metadata overload: existing action-only callers retain their exact
        // behavior, while TapInput can suppress feedback for a consuming non-control region.
        public bool TryResolve(Vector2 screenPos, out Action onTap,
            out ChromeFeedback feedback) => TryResolve(screenPos, out onTap, out feedback, out _, out _);

        public bool TryResolve(Vector2 screenPos, out Action onTap,
            out ChromeFeedback feedback, out RectTransform visual, out Graphic face)
            => TryResolve(screenPos, out onTap, out feedback, out visual, out face, out _);

        public bool TryResolve(Vector2 screenPos, out Action onTap,
            out ChromeFeedback feedback, out RectTransform visual, out Graphic face,
            out long registration)
        {
            onTap = null;
            feedback = ChromeFeedback.None;
            visual = null;
            face = null;
            registration = -1;
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
                    feedback = _entries[i].Feedback;
                    visual = _entries[i].Visual;
                    face = _entries[i].Face;
                    registration = _entries[i].Seq;
                }
            }
            return found;
        }
    }
}
