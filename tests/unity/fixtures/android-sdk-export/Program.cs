using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static class Program
{
    private const string Core = "dependencies {\n    implementation 'com.revenuecat.purchases:purchases-hybrid-common:[18.32.1]'\n    implementation 'androidx.core:core:1.9.0'\n";
    private const string Optional = "    implementation 'com.onesignal:core:5.9.9' // resolver\n    implementation 'com.onesignal:notifications:5.9.9'\n    implementation 'com.onesignal:in-app-messages:5.9.9'\n    implementation 'com.unity3d.ads-mediation:mediation-sdk:9.5.0'\n    implementation 'com.google.android.gms:play-services-ads-identifier:18.1.0'\n";
    private static readonly List<string> Passed = new List<string>();
    private static string Evidence;

    public static int Main(string[] args)
    {
        Evidence = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "catmetro-sdk-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Evidence);
        try
        {
            Run("android-never-enters-disabled-messaging-sdk", () => {
                UnityEngine.Application.platform = UnityEngine.RuntimePlatform.Android;
                OneSignalSDK.OneSignal.NativeCalls = 0;
                using (var messaging = new CatMetro.Integrations.OneSignal.OneSignalMessaging())
                {
                    messaging.Initialize("11111111-1111-4111-8111-111111111111");
                    Check(!messaging.IsAvailable && !messaging.CanRequestPermission, "disabled messaging appeared available");
                    messaging.PromptAsync(false, default).GetAwaiter().GetResult();
                }
                Check(OneSignalSDK.OneSignal.NativeCalls == 0, "disabled messaging invoked native SDK");
            });
            Run("android-never-constructs-disabled-ad-provider", () => {
                UnityEngine.Application.platform = UnityEngine.RuntimePlatform.Android;
                int constructed = 0;
                CatMetro.Integrations.RewardedAdProviderFactory.Register(config => { constructed++; return null; });
                var config = CatMetro.Integrations.RewardedAdsConfig.Parse("{\"androidAppKey\":\"fixture\",\"androidRewardedAdUnitId\":\"fixture\"}", UnityEngine.RuntimePlatform.Android);
                Check(config.IsConfigured, "configured ad fixture was invalid");
                CatMetro.Integrations.RewardedAdProviderFactory.Create(config);
                Check(constructed == 0, "disabled ad provider was constructed");
                UnityEngine.Application.platform = UnityEngine.RuntimePlatform.IPhonePlayer;
                CatMetro.Integrations.RewardedAdProviderFactory.Create(config);
                Check(constructed == 1, "non-Android factory behavior changed");
            });
            Run("non-android-messaging-remains-available", () => {
                foreach (var platform in new[] { UnityEngine.RuntimePlatform.IPhonePlayer, UnityEngine.RuntimePlatform.OSXEditor })
                {
                    UnityEngine.Application.platform = platform;
                    OneSignalSDK.OneSignal.NativeCalls = 0;
                    using (var messaging = new CatMetro.Integrations.OneSignal.OneSignalMessaging())
                    {
                        messaging.Initialize("11111111-1111-4111-8111-111111111111");
                        Check(messaging.IsAvailable && OneSignalSDK.OneSignal.NativeCalls > 0, "non-Android messaging changed");
                    }
                }
            });
            Run("disabled-preserves-purchases", () => {
                var path = Fixture("disabled");
                Apply(path, false, false);
                Check(File.ReadAllText(Path.Combine(path, "build.gradle")) == Core + "}\n", "purchase/core dependencies changed or optional SDK survived");
                Check(!Directory.Exists(Bridge(path)), "LevelPlay bridge imports survived");
                Check(File.ReadAllText(Path.Combine(path, "src/main/java/game/Keep.java")) == "package game; class Keep {}\n", "unrelated Java changed");
                Check(File.ReadAllText(Path.Combine(path, "build/stale/build.gradle")) == Optional, "incremental output was read or changed");
                var first = Snapshot(path);
                Apply(path, false, false);
                Check(first == Snapshot(path), "second export changed an already filtered project");
                File.WriteAllText(Path.Combine(path, "build.gradle"), Core + Optional + "}\n");
                WriteBridge(path);
                Apply(path, false, false);
                Check(first == Snapshot(path), "regenerated incremental input did not converge");
            });
            Run("explicit-opt-in-preserves-sdk-input", () => {
                var path = Fixture("enabled"); var before = Snapshot(path);
                Apply(path, true, true);
                Check(Snapshot(path) == before, "opt-in changed dependencies or bridges");
            });
            Run("independent-one-signal-opt-in", () => {
                var path = Fixture("messaging"); Apply(path, true, false);
                var text = File.ReadAllText(Path.Combine(path, "build.gradle"));
                Check(text.Contains("com.onesignal:notifications:5.9.9") && !text.Contains("mediation-sdk") && !text.Contains("ads-identifier"), "switches were coupled");
                Check(!Directory.Exists(Bridge(path)), "disabled ad bridge survived");
            });
            Run("independent-levelplay-opt-in", () => {
                var path = Fixture("ads"); Apply(path, false, true);
                var text = File.ReadAllText(Path.Combine(path, "build.gradle"));
                Check(!text.Contains("com.onesignal:") && text.Contains("mediation-sdk:9.5.0") && text.Contains("ads-identifier:18.1.0"), "switches were coupled");
                Check(Directory.Exists(Bridge(path)), "enabled ad bridge removed");
            });
            Run("preserves-revenuecat-identifier-version", () => {
                var path = Fixture("rc-identifier");
                File.AppendAllText(Path.Combine(path, "build.gradle"), "implementation 'com.google.android.gms:play-services-ads-identifier:17.0.1'\n");
                Apply(path, false, false);
                Check(File.ReadAllText(Path.Combine(path, "build.gradle")).Contains("ads-identifier:17.0.1"), "RevenueCat identifier dependency removed");
            });
            foreach (var declaration in new[] {
                "implementation 'com.onesignal:core:6.0.0'", "implementation 'com.onesignal:new-module:5.9.9'",
                "implementation 'com.ironsource:adquality-sdk:9.9.0'", "api 'com.onesignal:core:5.9.9'",
                "implementation(\n'com.onesignal:core:5.9.9'\n)", "implementation 'com.unity3d.ads-mediation:mediation-sdk:9.6.0'",
                "implementation 'com.onesignal:core:5.9.9\")" })
            {
                var current = declaration;
                Run("reject-declaration-" + Passed.Count, () => {
                    var path = Fixture("unknown-" + Passed.Count);
                    File.AppendAllText(Path.Combine(path, "build.gradle"), current + "\n");
                    var before = Snapshot(path);
                    Reject(() => Apply(path, false, false));
                    Check(Snapshot(path) == before, "rejected export mutated files before validation completed");
                });
            }
            Run("reject-optional-import-outside-known-bridge", () => {
                var path = Fixture("foreign-import");
                File.WriteAllText(Path.Combine(path, "src/main/java/game/Keep.java"), "package game;\nimport com.ironsource.mediationsdk.adunit.adapter.utility.AdInfo;\n");
                var before = Snapshot(path); Reject(() => Apply(path, false, false));
                Check(before == Snapshot(path), "unknown import rejection mutated project");
            });
            Run("reject-new-one-signal-native-source", () => {
                var path = Fixture("onesignal-import");
                File.WriteAllText(Path.Combine(path, "src/main/java/game/Keep.java"), "import com.onesignal.OneSignal;\n");
                Reject(() => Apply(path, false, false));
            });
            Run("reject-wrong-export-root", () => Reject(() => Apply(Path.Combine(Evidence, "missing"), false, false)));
            Run("levelplay-startup-flags-cannot-bypass-disabled-profile", () => {
                for (int flag = 0; flag < 4; flag++)
                {
                    int enabled = flag;
                    Reject(() => CatMetroAndroidSdkExportTransform.ValidateLevelPlayStartup(false,
                        enabled == 0, enabled == 1, enabled == 2, enabled == 3));
                }
                CatMetroAndroidSdkExportTransform.ValidateLevelPlayStartup(false, false, false, false, false);
                CatMetroAndroidSdkExportTransform.ValidateLevelPlayStartup(true, true, true, true, true);
            });
            File.WriteAllLines(Path.Combine(Evidence, "passed.txt"), Passed);
            Console.WriteLine("android-sdk-export: PASS " + Passed.Count + " behavioral fixtures; evidence=" + Evidence);
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Evidence, "failure.txt"), error.ToString());
            Console.Error.WriteLine("android-sdk-export: FAIL " + error.Message);
            return 1;
        }
    }

    private static void Run(string name, Action test) { test(); Passed.Add(name); Console.WriteLine("PASS " + name); }
    private static void Check(bool condition, string failure) { if (!condition) throw new Exception(failure); }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("unsafe export was accepted"); }
    private static void Apply(string path, bool messaging, bool ads) => CatMetroAndroidSdkExportTransform.Apply(path, messaging, ads);
    private static string Bridge(string path) => Path.Combine(path, "src/main/java/com/ironsource/unity/androidbridge");
    private static void WriteBridge(string path)
    {
        Directory.CreateDirectory(Bridge(path));
        File.WriteAllText(Path.Combine(Bridge(path), "LevelPlayBridge.java"), "package com.ironsource.unity.androidbridge;\nimport com.unity3d.mediation.LevelPlay;\n");
    }
    private static string Fixture(string name)
    {
        var project = Path.Combine(Evidence, name); var path = Path.Combine(project, "unityLibrary");
        Directory.CreateDirectory(Path.Combine(path, "src/main/java/game"));
        Directory.CreateDirectory(Path.Combine(path, "build/stale"));
        Directory.CreateDirectory(Path.Combine(project, "launcher"));
        File.WriteAllText(Path.Combine(path, "build.gradle"), Core + Optional + "}\n");
        File.WriteAllText(Path.Combine(project, "launcher/build.gradle"), "dependencies { implementation project(':unityLibrary') }\n");
        File.WriteAllText(Path.Combine(path, "src/main/java/game/Keep.java"), "package game; class Keep {}\n");
        File.WriteAllText(Path.Combine(path, "build/stale/build.gradle"), Optional);
        WriteBridge(path); return path;
    }
    private static string Snapshot(string path)
    {
        return string.Join("\n", Directory.GetFiles(Path.GetDirectoryName(path), "*", SearchOption.AllDirectories).OrderBy(x => x)
            .Select(x => x + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x)))));
    }
}
