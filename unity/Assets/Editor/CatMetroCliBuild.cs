// UNTRACKED build-tooling shim for the CM-C2b criterion-8 device session (agent-driven
// Build And Run, human-authorized in-session 2026-08-05). Editor-only assembly: never enters
// the player. Full text recorded in the criterion-8 artifact; deleted after the session.
//
// STORE-RELEASE (2026-08-25): the app-bundle flag is now set EXPLICITLY, not inherited.
// EditorUserBuildSettings.buildAppBundle persists in unity/Library across sessions, so once
// CatMetroCliAabBuild (or a human clicking "Build App Bundle") has set it true, this APK path
// silently emitted an .aab with a .apk filename — adb rejects it and the error names neither
// cause. Forcing it false here and restoring in a finally makes each entry point declare its
// own artifact kind, so neither builder can be poisoned by the other's leftover state.
// The extension gate refuses the inverse mistake (an .aab path handed to the APK builder).
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CatMetroCliBuild
{
    public static void BuildAndroid()
    {
        string outPath = System.Environment.GetEnvironmentVariable("CM_APK_OUT");
        bool dev = System.Environment.GetEnvironmentVariable("CM_DEV_BUILD") == "1";
        if (string.IsNullOrEmpty(outPath))
        {
            Debug.LogError("CLI_BUILD_RESULT Failed reason=no-CM_APK_OUT");
            EditorApplication.Exit(1);
            return;
        }
        if (Path.GetExtension(outPath) != ".apk")
        {
            Debug.LogError("CLI_BUILD_RESULT Failed reason=extension-not-apk out=" + outPath
                + " — this entry point only emits APKs; use CatMetroCliAabBuild.BuildAndroidAab "
                + "(scripts/build-aab.sh) for a Play upload bundle");
            EditorApplication.Exit(1);
            return;
        }

        bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        int exitCode;
        try
        {
            // Explicit, never inherited: the persisted Library value is not trustworthy.
            EditorUserBuildSettings.buildAppBundle = false;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("CLI_BUILD_RESULT " + report.summary.result
                + " dev=" + dev
                + " appBundle=False"
                + " size=" + report.summary.totalSize
                + " errors=" + report.summary.totalErrors
                + " out=" + outPath);
            exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
        }
        finally
        {
            // Restore the human's editor state; runs before process death because the
            // result Exit sits below this block.
            EditorUserBuildSettings.buildAppBundle = previousAppBundle;
        }
        EditorApplication.Exit(exitCode);
    }
}
