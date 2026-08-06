using System;
using System.Collections.Generic;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criteria 1+2: ADR-0007's navigation — screens push/pop on a stack inside Home
    // and Game, never scene loads. Pure C# (no engine objects): the serialization SHAPE matches
    // ADR-0006 `breadcrumbs.screenStack` (an ordered string array, bottom -> top; empty stack
    // -> empty array, the schema's [] default) so a process death lands the player back where
    // they were — but save I/O itself stays Application-layer and is DEFERRED; nothing in this
    // class reads or writes a file. TryPop on empty returns false (PC-3: on the first Home
    // there is nothing to pop — back exits normally, and that exit is the OS's, not ours).
    public sealed class ScreenStack
    {
        private readonly List<string> _screens = new List<string>();

        public int Count => _screens.Count;

        public string Current => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;

        public void Push(string screenId)
        {
            if (string.IsNullOrEmpty(screenId))
                throw new ArgumentException(
                    "screen id is required — a null/empty push is a wiring defect, never silent");
            _screens.Add(screenId);
        }

        public bool TryPop(out string screenId)
        {
            if (_screens.Count == 0)
            {
                screenId = null;
                return false;
            }
            screenId = _screens[_screens.Count - 1];
            _screens.RemoveAt(_screens.Count - 1);
            return true;
        }

        // A SNAPSHOT in the ADR-0006 shape — callers get a copy, never internal state.
        public string[] ToBreadcrumb()
        {
            return _screens.ToArray();
        }

        // Restore REPLACES contents, atomically: a corrupt entry rejects the whole breadcrumb
        // and leaves the prior stack intact (never a half-restored navigation state).
        public void RestoreFrom(IEnumerable<string> entries)
        {
            if (entries == null)
                throw new ArgumentException("breadcrumb entries are required");
            var restored = new List<string>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry))
                    throw new ArgumentException(
                        "a null/empty breadcrumb entry is corrupt data — rejected atomically");
                restored.Add(entry);
            }
            _screens.Clear();
            _screens.AddRange(restored);
        }
    }
}
