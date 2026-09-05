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
//
// DEBUG SIGNING (2026-09-05): the same inherited-state trap, one layer over. A human GUI
// session that configures the upload keystore for an .aab leaves
// PlayerSettings.Android.useCustomKeystore=true in the tracked ProjectSettings.asset. Batch
// mode has no keystore-password seam (deliberately — passwords are human-only), so the next
// CLI dev build died in PrepareForBuild with "Unable to sign the application; please provide
// passwords!". A dev APK is debug-signed by contract (scripts/build-apk.sh header), so this
// entry point declares that itself: force the toggle false, restore it in the same finally.
// It never reads or writes keystore paths, aliases or passwords.
//
// ANDROID DEPENDENCIES (2026-09-05): the External Dependency Manager (EDM4U) resolves the
// RevenueCat / OneSignal / LevelPlay native libraries only from an interactive editor session;
// a -executeMethod batch build never triggers it, so Gradle compiled without them and javac
// died with "package com.revenuecat.purchases does not exist". The builder now calls
// GooglePlayServices.PlayServicesResolver.ResolveSync(true) by reflection before BuildPlayer
// (reflection so this Editor assembly has no compile-time dependency on EDM4U) and logs which
// Gradle templates Unity will see, with their importer types, as CLI_BUILD_TEMPLATE lines.
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

        LogGradleTemplateState();
        if (!ResolveAndroidDependencies())
        {
            Debug.LogError("CLI_BUILD_RESULT Failed reason=android-dependency-resolution");
            EditorApplication.Exit(1);
            return;
        }

        bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool previousCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        int exitCode;
        try
        {
            // Explicit, never inherited: the persisted Library value is not trustworthy.
            EditorUserBuildSettings.buildAppBundle = false;
            // Explicit, never inherited: a dev APK is debug-signed; the GUI's upload-keystore
            // toggle has no batch password and would abort the build before compiling.
            PlayerSettings.Android.useCustomKeystore = false;
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
                + " signing=debug"
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
            PlayerSettings.Android.useCustomKeystore = previousCustomKeystore;
        }
        EditorApplication.Exit(exitCode);
    }

    // Evidence, not control: a custom template Unity silently ignores looks identical to one
    // that was never enabled. Recording existence + importer per template makes the next
    // "why are my dependencies missing" diagnosis a grep instead of an afternoon.
    private static void LogGradleTemplateState()
    {
        foreach (var path in new[]
        {
            "Assets/Plugins/Android/mainTemplate.gradle",
            "Assets/Plugins/Android/gradleTemplate.properties",
            "Assets/Plugins/Android/settingsTemplate.gradle",
            "Assets/Plugins/Android/launcherTemplate.gradle",
        })
        {
            var importer = AssetImporter.GetAtPath(path);
            Debug.Log("CLI_BUILD_TEMPLATE path=" + path
                + " exists=" + File.Exists(path)
                + " importer=" + (importer != null ? importer.GetType().Name : "none"));
        }
    }

    // Returns false only when EDM4U is present and reports a failed resolution. A project
    // without EDM4U resolves nothing and proceeds; that is the pre-2026-09-05 behaviour.
    private static bool ResolveAndroidDependencies()
    {
        System.Type resolver = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try { resolver = assembly.GetType("GooglePlayServices.PlayServicesResolver"); }
            catch (System.Exception) { resolver = null; }
            if (resolver != null) break;
        }
        if (resolver == null)
        {
            Debug.Log("CLI_BUILD_RESOLVE edm4u=absent");
            return true;
        }
        var resolveSync = resolver.GetMethod("ResolveSync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null, new[] { typeof(bool) }, null);
        if (resolveSync == null)
        {
            Debug.LogWarning("CLI_BUILD_RESOLVE edm4u=present resolveSync=missing");
            return true;
        }
        object result;
        try
        {
            result = resolveSync.Invoke(null, new object[] { true });
        }
        catch (System.Exception ex)
        {
            Debug.LogError("CLI_BUILD_RESOLVE edm4u=present ok=False error=" + ex.GetBaseException().Message);
            return false;
        }
        bool ok = !(result is bool b) || b;
        Debug.Log("CLI_BUILD_RESOLVE edm4u=present forced=True ok=" + ok);
        return ok;
    }
}
