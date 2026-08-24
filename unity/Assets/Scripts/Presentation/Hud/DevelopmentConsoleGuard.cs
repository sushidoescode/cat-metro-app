using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // Unity enables its native Development Console by default in development players and opens
    // it on the first error. Keep Android dev diagnostics in logcat without letting that native
    // overlay cover the game.
    public static class DevelopmentConsoleGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStartup()
        {
            Apply(UnityEngine.Application.platform, Debug.isDebugBuild);
        }

        public static bool Apply(RuntimePlatform platform, bool isDebugBuild)
        {
            if (platform != RuntimePlatform.Android || !isDebugBuild) return false;
            Debug.developerConsoleEnabled = false;
            return true;
        }
    }
}
