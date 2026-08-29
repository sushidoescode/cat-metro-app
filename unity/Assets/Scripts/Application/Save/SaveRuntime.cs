using System;

namespace CatMetro.Application.Save
{
    // The bootstrap-published save store lets optional systems subscribe without adding a
    // GameRoot dependency. The exact store instance is retained so every consumer shares its
    // durable state and filesystem lifetime.
    public static class SaveRuntime
    {
        public static SaveStore Current { get; private set; }
        public static bool IsInstalled => Current != null;

        public static event Action<SaveStore> Installed;

        public static void Install(SaveStore store)
        {
            if (store == null || ReferenceEquals(Current, store)) return;
            Current = store;
            Installed?.Invoke(store);
        }

        // EditMode/PlayMode tests can share a domain; clear both state and callbacks so a prior
        // bootstrap cannot leak through the next test or a Unity domain reload.
        public static void ResetForTests()
        {
            Current = null;
            Installed = null;
        }
    }
}
