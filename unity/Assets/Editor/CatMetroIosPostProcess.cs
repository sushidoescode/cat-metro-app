// iOS Xcode-project post-processing (IOS-RELEASE, 2026-08-25). Editor-only assembly:
// never enters the player.
//
// This runs on EVERY iOS build — the scripted one in CatMetroCliIosBuild and a build
// started by hand from the Editor's Build Settings window alike. That is deliberate. The
// This callback deliberately leaves export compliance unset. App Store Connect asks the
// human release owner for that declaration, which must be determined from the final archive
// and complete SDK set rather than from a source-only assumption made before the build.
//
// The whole file is behind `#if UNITY_IOS` so this loud release marker only compiles when
// iOS is the selected build target. Machines without iOS Build Support never define it.
#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class CatMetroIosPostProcess
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        // A missing key is intentional: it makes App Store Connect ask the human on upload.
        // Do not replace this marker with true or false until the final archive, RevenueCat,
        // OneSignal, analytics, and every other shipped SDK have been audited by the release
        // owner. This callback exists so the build log makes that unfinished human gate loud.
        Debug.LogWarning("CM_IOS_POSTPROCESS export-compliance=unset human-required project="
            + pathToBuiltProject);
    }
}
#endif
