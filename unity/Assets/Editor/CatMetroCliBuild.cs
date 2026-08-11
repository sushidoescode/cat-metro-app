using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Stable editor-only command used by the local/CI Android evidence loop. It never enters the
// player and deliberately fails closed when its one output seam is malformed.
public static class CatMetroCliBuild
{
    private static readonly string[] PolyforkModels =
    {
        "polyfork_tram_track_tile_f3c69a.fbx",
        "polyfork_train_engine_180979.fbx",
        "polyfork_log_cabin_4fac3b.fbx",
        "polyfork_young_pine_0d7695.fbx",
        "polyfork_wooden_fence_section_5f04b7.fbx",
        "polyfork_wooden_bench_661da4.fbx",
        "polyfork_sandwich_board_sign_cb5e7c.fbx",
        "polyfork_street_lamp_29f365.fbx",
        "polyfork_coffee_cup_90be67.fbx",
    };

    public static void BuildAndroid()
    {
        string requestedPath = Environment.GetEnvironmentVariable("CM_APK_OUT");
        bool development = Environment.GetEnvironmentVariable("CM_DEV_BUILD") == "1";
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            Fail("no-CM_APK_OUT");
            return;
        }

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(requestedPath);
        }
        catch (Exception exception)
        {
            Fail("invalid-output-path-" + exception.GetType().Name);
            return;
        }

        if (!string.Equals(Path.GetExtension(outputPath), ".apk",
                StringComparison.OrdinalIgnoreCase))
        {
            Fail("output-must-be-apk");
            return;
        }

        string parent = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(parent))
        {
            Fail("output-parent-missing");
            return;
        }
        if (!RequirePolyforkLocalCustody())
        {
            Fail("polyfork-local-custody-missing");
            return;
        }
        Directory.CreateDirectory(parent);

        // A prior interactive Editor session must not silently turn this APK contract into AAB.
        EditorUserBuildSettings.buildAppBundle = false;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Game.unity" },
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = development ? BuildOptions.Development : BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("CLI_BUILD_RESULT " + report.summary.result
            + " dev=" + development
            + " size=" + report.summary.totalSize
            + " errors=" + report.summary.totalErrors
            + " out=" + outputPath);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static bool RequirePolyforkLocalCustody()
    {
        string modelRoot = Path.Combine(Application.dataPath, "Art", "Polyfork", "Models");
        foreach (string model in PolyforkModels)
        {
            string fbx = Path.Combine(modelRoot, model);
            if (!File.Exists(fbx) || !File.Exists(fbx + ".meta")) return false;
        }
        return true;
    }

    private static void Fail(string reason)
    {
        Debug.LogError("CLI_BUILD_RESULT Failed reason=" + reason);
        EditorApplication.Exit(1);
    }
}
