using UnityEngine;

namespace CatMetro.Integrations
{
    // Explicit opt-in for the shipped Android binary, shared by runtime guards and the export
    // callback. Configuring a provider does not implicitly add its native SDK to the build.
    public static class AndroidOptionalSdkProfile
    {
        public const bool IncludeOneSignal = false;
        public const bool IncludeLevelPlay = false;

        public static bool AllowOneSignalRuntime =>
            UnityEngine.Application.platform != RuntimePlatform.Android || IncludeOneSignal;

        public static bool AllowLevelPlayRuntime =>
            UnityEngine.Application.platform != RuntimePlatform.Android || IncludeLevelPlay;
    }
}
