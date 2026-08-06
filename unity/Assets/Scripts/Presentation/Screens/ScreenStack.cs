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
        public int Count => 0; // RED stub
        public string Current => null; // RED stub

        public void Push(string screenId)
        {
            // RED stub
        }

        public bool TryPop(out string screenId)
        {
            screenId = null; // RED stub
            return false;
        }

        public string[] ToBreadcrumb()
        {
            return new string[0]; // RED stub
        }

        public void RestoreFrom(IEnumerable<string> entries)
        {
            // RED stub
        }
    }
}
