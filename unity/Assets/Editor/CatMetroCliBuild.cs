// Committed CLI build entry point for Android APK builds, driven by scripts/build-apk.sh
// via -executeMethod CatMetroCliBuild.BuildAndroid (no -quit; the method exits the editor
// itself via EditorApplication.Exit). Editor-only assembly: never enters the player.
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
            + " size=" + report.summary.totalSize
            + " errors=" + report.summary.totalErrors
            + " out=" + outPath);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
