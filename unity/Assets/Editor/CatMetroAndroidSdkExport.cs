using System;
using CatMetro.Integrations;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// Runs for both APK and AAB, after resolver-generated Gradle declarations and SDK callbacks.
public sealed class CatMetroAndroidSdkExport : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => int.MaxValue;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        try
        {
            // The vendor has its own RuntimeInitialize callback, independent of our factory.
            // Do not inspect/log app keys; any enabled native-entry flag requires explicit opt-in.
            var settings = Resources.Load<LevelPlayMediationSettings>("LevelPlayMediationSettings");
            CatMetroAndroidSdkExportTransform.ValidateLevelPlayStartup(
                AndroidOptionalSdkProfile.IncludeLevelPlay,
                settings != null && settings.EnableIronsourceSDKInitAPI,
                settings != null && settings.EnableAdapterDebug,
                settings != null && settings.EnableIntegrationHelper,
                settings != null && settings.DeclareAD_IDPermission);
            CatMetroAndroidSdkExportTransform.Apply(path,
                AndroidOptionalSdkProfile.IncludeOneSignal, AndroidOptionalSdkProfile.IncludeLevelPlay);
        }
        catch (InvalidOperationException error)
        {
            throw new BuildFailedException(error.Message);
        }
        Debug.Log("CATMETRO_ANDROID_SDK_PROFILE oneSignal=" + AndroidOptionalSdkProfile.IncludeOneSignal
            + " levelPlay=" + AndroidOptionalSdkProfile.IncludeLevelPlay + " purchases=preserved");
    }
}
