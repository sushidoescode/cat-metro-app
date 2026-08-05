namespace CatMetro.Bootstrap
{
    // CM-C2b-DEVFIX criterion 3: the boot frame-rate policy — SHIPPED, never dev-guarded.
    // vsync stays 0 so the target governs (a vsync count of 1 on a 120 Hz panel presents
    // 120 fps and busts thermals); TARGET_FPS comes from criterion 8's own median budget.
    // Engine types fully qualified: inside this namespace the bare identifier Application
    // binds to the project's own CatMetro.Application (A-DEVFIX-5).
    public static class FramePolicy
    {
        public const int TARGET_FPS = 60;

        public static void Apply()
        {
            if (UnityEngine.QualitySettings.vSyncCount != 0)
                UnityEngine.QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = TARGET_FPS;
        }
    }
}
