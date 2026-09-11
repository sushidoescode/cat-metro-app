using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Plain C# so the actual export edits can be exercised without launching Unity or Gradle.
public static class CatMetroAndroidSdkExportTransform
{
    private static readonly HashSet<string> OneSignal = new HashSet<string>(StringComparer.Ordinal)
    {
        "com.onesignal:core:5.9.9",
        "com.onesignal:notifications:5.9.9",
        "com.onesignal:in-app-messages:5.9.9"
    };
    private static readonly HashSet<string> LevelPlay = new HashSet<string>(StringComparer.Ordinal)
    {
        "com.unity3d.ads-mediation:mediation-sdk:9.5.0",
        "com.google.android.gms:play-services-ads-identifier:18.1.0"
    };
    private const string RevenueCatIdentifier = "com.google.android.gms:play-services-ads-identifier:17.0.1";
    private const string BridgeDirectory = "src/main/java/com/ironsource/unity/androidbridge";
    private static readonly Regex OptionalCoordinate = new Regex(
        @"com\.onesignal|com\.unity3d\.ads-mediation|com\.ironsource|com\.google\.android\.gms:play-services-ads-identifier");
    private static readonly Regex Declaration = new Regex(
        @"^\s*implementation\s+(?<quote>['""])(?<coordinate>[^'""\s]+)\k<quote>\s*$");

    public static void ValidateLevelPlayStartup(bool includeLevelPlay, bool autoInitialize,
        bool adapterDebug, bool integrationHelper, bool declareAdId)
    {
        if (!includeLevelPlay && (autoInitialize || adapterDebug || integrationHelper || declareAdId))
            throw new InvalidOperationException("Android LevelPlay is excluded but its Resources settings enable native initialization, diagnostics, or AD_ID. Disable those flags or explicitly opt in to LevelPlay.");
    }

    public static void Apply(string unityLibraryPath, bool includeOneSignal, bool includeLevelPlay)
    {
        var library = Path.GetFullPath(unityLibraryPath);
        if (Path.GetFileName(library) != "unityLibrary" || !File.Exists(Path.Combine(library, "build.gradle")))
            throw new InvalidOperationException("Expected the generated unityLibrary Gradle module.");
        var project = Directory.GetParent(library).FullName;
        var modules = Directory.GetDirectories(project)
            .Where(path => File.Exists(Path.Combine(path, "build.gradle"))).ToArray();
        var gradleFiles = Directory.GetFiles(project, "*.gradle")
            .Concat(modules.SelectMany(path => Directory.GetFiles(path, "*.gradle"))).ToArray();
        var edits = new Dictionary<string, string>(StringComparer.Ordinal);

        // Inspect the complete input before writing. Never blindly delete unfamiliar versions,
        // configurations, or multiline declarations after a resolver/SDK change.
        foreach (var path in gradleFiles)
        {
            var original = File.ReadAllText(path);
            var output = new System.Text.StringBuilder();
            int lineNumber = 0;
            foreach (Match line in Regex.Matches(original, @"[^\n]*\n|[^\n]+$"))
            {
                lineNumber++;
                string text = line.Value;
                string code = Regex.Replace(text, @"//[^\r\n]*", "").Trim();
                if (!OptionalCoordinate.IsMatch(code)) { output.Append(text); continue; }
                var declaration = Declaration.Match(code);
                string coordinate = declaration.Success ? declaration.Groups["coordinate"].Value : null;
                if (coordinate == null || (!OneSignal.Contains(coordinate) && !LevelPlay.Contains(coordinate)
                    && coordinate != RevenueCatIdentifier))
                    throw new InvalidOperationException("Unrecognized optional Android dependency at "
                        + Path.GetFileName(path) + ":" + lineNumber + "; review the resolved declaration before exporting.");
                bool remove = OneSignal.Contains(coordinate) ? !includeOneSignal
                    : LevelPlay.Contains(coordinate) && !includeLevelPlay;
                if (!remove) output.Append(text);
            }
            var changed = output.ToString();
            if (changed != original) edits.Add(path, changed);
        }

        string bridge = Path.Combine(library, BridgeDirectory);
        foreach (var module in modules)
        {
            var source = Path.Combine(module, "src");
            if (!Directory.Exists(source)) continue;
            foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".java", StringComparison.Ordinal) || path.EndsWith(".kt", StringComparison.Ordinal)))
            {
                if (!includeLevelPlay && path.StartsWith(bridge + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
                var code = File.ReadAllText(path);
                if ((!includeOneSignal && Regex.IsMatch(code, @"\bcom\.onesignal\b"))
                    || (!includeLevelPlay && Regex.IsMatch(code, @"\bcom\.(?:ironsource|unity3d\.mediation)\b")))
                    throw new InvalidOperationException("A source outside the known optional SDK bridge still references an excluded Android SDK: "
                        + Path.GetFileName(path));
            }
        }

        foreach (var edit in edits) File.WriteAllText(edit.Key, edit.Value);
        if (!includeLevelPlay && Directory.Exists(bridge)) Directory.Delete(bridge, true);
    }
}
