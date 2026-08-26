// Scripted iOS entry point (IOS-RELEASE, 2026-08-25). Editor-only assembly: never enters
// the player.
//
// WHAT THIS PRODUCES, AND WHAT IT DOES NOT.
// Unity's iOS target does not emit an installable binary. It emits an *Xcode project*
// directory. Turning that into a .ipa is `xcodebuild archive` + `xcodebuild
// -exportArchive`, and uploading it is Transporter or `xcrun altool`. Those three steps
// are HUMAN-ONLY in this repo — they touch signing identities and a live App Store
// Connect account. This file's job ends the moment the Xcode project exists on disk.
// Runbook: docs/release/ios-release-runbook.md
//
// SIGNING IS NOT THIS FILE'S BUSINESS, same posture as CatMetroCliAabBuild. The Apple
// team ID, certificates and provisioning profiles live in the human's local Xcode
// keychain and Player Settings — never in the repo, never agent-reachable. This builder
// only READS the signing configuration and REPORTS it. It never assigns any Player
// Settings field.
//
// Why this WARNS about a missing team ID rather than refusing, unlike the AAB builder's
// hard refusal on debug signing: a debug-signed .aab is *never* uploadable, so producing
// one and exiting 0 would be a lie. An Xcode project with no team ID is different — it is
// still a perfectly good project, and setting the team once in Xcode's Signing &
// Capabilities tab is the normal, documented workflow. Refusing here would block a step
// that legitimately happens downstream.
//
// The bundle identifier IS a hard refusal, because that one is not recoverable downstream
// without cost: an archive built under the wrong bundle ID cannot be uploaded against the
// App Store Connect record, and you only discover it after a 30-45 minute IL2CPP build
// plus an archive. Catching it in the first second is worth the strictness.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CatMetroCliIosBuild
{
    // Bundle identifiers Unity invents for you when nothing has been set. Building against
    // any of these means Player Settings was never configured for iOS.
    static readonly string[] PlaceholderIdentifiers =
    {
        "com.Company.ProductName",
        "com.unity3d.player",
        "com.DefaultCompany.",
    };

    public static void BuildIos()
    {
        string outPath = Environment.GetEnvironmentVariable("CM_IOS_OUT");
        if (string.IsNullOrWhiteSpace(outPath))
        {
            Debug.LogError("CLI_IOS_RESULT Failed reason=no-CM_IOS_OUT");
            EditorApplication.Exit(1);
            return;
        }

        try
        {
            outPath = Path.GetFullPath(outPath);
        }
        catch (Exception e)
        {
            Debug.LogError("CLI_IOS_RESULT Failed reason=bad-output-path detail=" + e.GetType().Name);
            EditorApplication.Exit(1);
            return;
        }

        // Unity writes an Xcode project *directory* here, not a file. A path carrying a
        // file extension is almost always someone copying the APK/AAB invocation, and the
        // failure mode if we let it through is a directory literally named "CatMetro.ipa"
        // containing an Xcode project — confusing at exactly the wrong moment.
        if (!string.IsNullOrEmpty(Path.GetExtension(outPath)))
        {
            Debug.LogError("CLI_IOS_RESULT Failed reason=output-must-be-a-directory-not-a-file out="
                + outPath + " — Unity emits an Xcode project directory; the .ipa comes later, "
                + "from xcodebuild");
            EditorApplication.Exit(1);
            return;
        }

        string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            Debug.LogError("CLI_IOS_RESULT Failed reason=no-ios-bundle-identifier — set it in "
                + "Player Settings > iOS > Identification > Bundle Identifier, or commit an "
                + "`iPhone:` entry under `applicationIdentifier:` in ProjectSettings.asset. It "
                + "must match the bundle ID registered in App Store Connect exactly.");
            EditorApplication.Exit(1);
            return;
        }

        foreach (string placeholder in PlaceholderIdentifiers)
        {
            if (bundleId.StartsWith(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("CLI_IOS_RESULT Failed reason=placeholder-bundle-identifier id="
                    + bundleId + " — this is Unity's default, not a registered App Store "
                    + "Connect bundle ID. An archive built under it cannot be uploaded.");
                EditorApplication.Exit(1);
                return;
            }
        }

        bool dev = Environment.GetEnvironmentVariable("CM_DEV_BUILD") == "1";

        // Read-only signing report. Never assigned here.
        string teamId = PlayerSettings.iOS.appleDeveloperTeamID;
        bool autoSign = PlayerSettings.iOS.appleEnableAutomaticSigning;
        string signing = string.IsNullOrWhiteSpace(teamId)
            ? "no-team-id"
            : (autoSign ? "automatic" : "manual");
        if (signing == "no-team-id")
        {
            Debug.LogWarning("CLI_IOS_SIGNING no team ID configured — the generated project "
                + "will not archive until you pick a team in Xcode > Signing & Capabilities "
                + "(or set Player Settings > iOS > Signing Team ID). This is expected before "
                + "Apple Developer Program enrolment completes.");
        }

        Directory.CreateDirectory(outPath);

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Game.unity" },
            locationPathName = outPath,
            target = BuildTarget.iOS,
            // Deliberately NOT AcceptExternalModificationsToPlayer. Unity's "append" mode
            // preserves hand edits in the Xcode project across rebuilds, which sounds
            // helpful and is a trap: it also preserves stale generated sources, so a
            // renamed or deleted C# class leaves a duplicate-symbol link error that looks
            // like an engine bug. Always regenerate clean; put every Xcode-side change in
            // CatMetroIosPostProcess.cs so it is reapplied every time.
            options = dev ? BuildOptions.Development : BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);

        // size= is Unity's report.summary.totalSize metric, NOT the artifact's on-disk byte
        // count, and for an iOS build it is especially unrepresentative — the real download
        // size is decided later by App Store thinning. Never build a check on that field.
        Debug.Log("CLI_IOS_RESULT " + report.summary.result
            + " channel=" + (dev ? "dev-never-upload" : "release")
            + " bundleId=" + bundleId
            + " signing=" + signing
            + " minIos=" + PlayerSettings.iOS.targetOSVersionString
            + " size=" + report.summary.totalSize
            + " errors=" + report.summary.totalErrors
            + " out=" + outPath);

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
