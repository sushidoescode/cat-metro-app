// Scripted Android App Bundle entry point. Editor-only assembly: never enters the player.
// Google Play requires an AAB for a new app, so this path selects the bundle format explicitly
// rather than inheriting the Unity editor's persistent Library state.
//
// Signing is NOT this file's business. A human configures the upload key in Unity; the
// tracked Player Settings can retain its local path and alias and must be sanitized after
// the build. This builder only READS whether custom signing is configured and REPORTS which
// signing state produced the artifact. It never reads or writes key material or credentials,
// and it never assigns any Player Settings field — the wrapper test refuses both classes of
// edit on the comment-stripped source.
//
// Fail-closed posture: a store bundle is release-only (a set CM_DEV_BUILD is refused,
// not ignored), and a DEBUG-SIGNED bundle is refused unless CM_ALLOW_DEBUG_SIGNING=1
// explicitly opts into a pipeline-proof build — a debug .aab is never uploadable and
// must never exit 0 by default.
using System.IO;
using System.Collections.Generic;
using CatMetro.Bootstrap;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CatMetroCliAabBuild
{
    public static void BuildAndroidAab()
    {
        string outPath = System.Environment.GetEnvironmentVariable("CM_AAB_OUT");
        if (string.IsNullOrWhiteSpace(outPath))
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
        try
        {
            outPath = Path.GetFullPath(outPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=bad-output-path detail=" + e.GetType().Name);
            EditorApplication.Exit(1);
            return;
        }
        if (Path.GetExtension(outPath) != ".aab")
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=extension-not-aab out=" + outPath);
            EditorApplication.Exit(1);
            return;
        }

        string[] campaignIds = GameRoot.LevelBand;
        string campaignDirectory = Path.Combine(
            Application.dataPath, "StreamingAssets/content/levels");
        var uniqueCampaignIds = new HashSet<string>(System.StringComparer.Ordinal);
        if (campaignIds == null || campaignIds.Length == 0)
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=campaign-band-empty");
            EditorApplication.Exit(1);
            return;
        }
        foreach (string campaignId in campaignIds)
        {
            bool validId = campaignId != null
                && campaignId.Length == 4
                && campaignId[0] == 'L'
                && char.IsDigit(campaignId[1])
                && char.IsDigit(campaignId[2])
                && char.IsDigit(campaignId[3]);
            if (!validId || !uniqueCampaignIds.Add(campaignId))
            {
                Debug.LogError("CLI_AAB_RESULT Failed reason=campaign-band-invalid id="
                    + (campaignId ?? "<null>"));
                EditorApplication.Exit(1);
                return;
            }
            string stagedLevel = Path.Combine(campaignDirectory, campaignId + ".json");
            if (!File.Exists(stagedLevel))
            {
                Debug.LogError("CLI_AAB_RESULT Failed reason=campaign-level-not-staged id="
                    + campaignId + " path=" + stagedLevel);
                EditorApplication.Exit(1);
                return;
            }
        }
        string campaignIdsCsv = string.Join(",", campaignIds);

        string signing = PlayerSettings.Android.useCustomKeystore ? "custom" : "debug";
        bool allowDebug =
            System.Environment.GetEnvironmentVariable("CM_ALLOW_DEBUG_SIGNING") == "1";
        if (signing == "debug" && !allowDebug)
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=debug-signing-refused — a debug "
                + "keystore bundle is never uploadable to Play; configure the upload "
                + "keystore in Player Settings, sanitize the tracked settings diff, and rebuild, or "
                + "set CM_ALLOW_DEBUG_SIGNING=1 for an explicit pipeline-proof build");
            EditorApplication.Exit(1);
            return;
        }

        string outDir = Path.GetDirectoryName(outPath);
        if (string.IsNullOrEmpty(outDir))
        {
            Debug.LogError("CLI_AAB_RESULT Failed reason=no-output-directory out=" + outPath);
            EditorApplication.Exit(1);
            return;
        }
        Directory.CreateDirectory(outDir);

        bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        int exitCode;
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
            // size= is Unity's report.summary.totalSize metric, NOT the artifact's
            // on-disk byte count — never build a check on that field.
            Debug.Log("CLI_AAB_RESULT " + report.summary.result
                + " signing=" + signing
                + " campaignLevels=" + campaignIds.Length
                + " campaignIds=" + campaignIdsCsv
                + " size=" + report.summary.totalSize
                + " errors=" + report.summary.totalErrors
                + " out=" + outPath);
            exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
        }
        finally
        {
            // Restore whatever state the human's editor session had — this runs before
            // process death because the result Exit sits below this block (F-3).
            EditorUserBuildSettings.buildAppBundle = previousAppBundle;
        }
        EditorApplication.Exit(exitCode);
    }
}
