// Scripted Android App Bundle entry point (BUILD-AAB, 2026-08-13). Editor-only assembly:
// never enters the player. Closes the Lane 10 runbook's recorded gap ("no scripted AAB
// path exists"): Play accepts only .aab for new apps, and the only committed builder
// (the art branch's CatMetroCliBuild) is deliberately APK-only and custody-gated.
//
// Signing is NOT this file's business. The upload keystore lives in the human's local,
// uncommitted Player Settings state (threat-model row: never in the repo, never
// agent-reachable). This builder only REPORTS which signing state produced the artifact:
// signing=custom means the locally configured upload keystore signed it; signing=debug
// means Unity's debug certificate did — such an AAB is a pipeline proof and is NOT
// uploadable to Play. It never reads or writes keystore paths, aliases, or passwords.
//
// Store bundles are release-only by definition: a set CM_DEV_BUILD is refused loudly
// rather than ignored, so the dev seam of the APK shim cannot leak into a store artifact.
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CatMetroCliAabBuild
{
    public static void BuildAndroidAab()
    {
        string outPath = System.Environment.GetEnvironmentVariable("CM_AAB_OUT");
        if (string.IsNullOrEmpty(outPath))
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=no-CM_AAB_OUT");
            EditorApplication.Exit(1);
            return;
        }
        if (System.Environment.GetEnvironmentVariable("CM_DEV_BUILD") == "1")
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=dev-build-refused-for-store-bundle");
            EditorApplication.Exit(1);
            return;
        }
        outPath = Path.GetFullPath(outPath);
        if (Path.GetExtension(outPath) != ".aab")
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=extension-not-aab out=" + outPath);
            EditorApplication.Exit(1);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        string signing = PlayerSettings.Android.useCustomKeystore ? "custom" : "debug";
        if (signing == "debug")
        {
            Debug.LogWarning("CLI_AAB_SIGNING debug keystore — pipeline proof only, NOT "
                + "uploadable to Play; configure the upload keystore in Player Settings "
                + "(local, never committed) and rebuild");
        }

        bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            EditorUserBuildSettings.buildAppBundle = true;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("CLI_AAB_RESULT " + report.summary.result
                + " signing=" + signing
                + " size=" + report.summary.totalSize
                + " errors=" + report.summary.totalErrors
                + " out=" + outPath);
            EditorApplication.Exit(
                report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        finally
        {
            // The APK shim's inherited-state law, honored in both directions:
            // EditorUserBuildSettings.buildAppBundle = false is the resting state.
            EditorUserBuildSettings.buildAppBundle = false;
            if (previousAppBundle)
            {
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
            }
        }
    }
}
