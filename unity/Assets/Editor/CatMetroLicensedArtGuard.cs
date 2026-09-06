// LICENSED-ART BUILD GUARD (2026-09-05). The licensed cat rig and props live in the gitignored
// unity/Assets/Art/Generated/incoming/ tree ("one machine only", AGENTS.md). CatModelCatalog and
// PropModelCatalog reject or fall back SILENTLY when a resource is absent, so a build made
// without the install ships a flat placeholder cat with no log line saying why — which is
// exactly what the first Pixel dev build of 2026-09-05 did. This preprocess hook runs for EVERY
// player build, GUI or CLI, and refuses to build unless the licensed resources resolve.
// Override, for a deliberate placeholder build only: set CM_ALLOW_PLACEHOLDER_ART=1 in the
// environment (CLI) or the EditorPrefs key "CatMetro.AllowPlaceholderArt" to true (GUI).
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class CatMetroLicensedArtGuard : IPreprocessBuildWithReport
{
    public const string RigResourcePath = "CatRigs/BoardCatRig";
    // One prop that every furnished board needs: PropModelCatalog.ResourceRoot + assetId.
    public const string ProbePropResourcePath = "CatMetroProps/prop-depot-shed";
    public const string OverrideEnvVar = "CM_ALLOW_PLACEHOLDER_ART";
    public const string OverridePrefKey = "CatMetro.AllowPlaceholderArt";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        bool rig = Resources.Load<GameObject>(RigResourcePath) != null;
        bool prop = Resources.Load<GameObject>(ProbePropResourcePath) != null;
        bool allow = System.Environment.GetEnvironmentVariable(OverrideEnvVar) == "1"
            || EditorPrefs.GetBool(OverridePrefKey, false);
        Debug.Log("CLI_BUILD_ASSETS licensedRig=" + (rig ? "present" : "ABSENT")
            + " licensedProps=" + (prop ? "present" : "ABSENT")
            + " placeholderOverride=" + allow);
        if (rig && prop) return;
        if (allow)
        {
            Debug.LogWarning("CatMetroLicensedArtGuard: building WITH PLACEHOLDER ART by explicit override");
            return;
        }
        throw new BuildFailedException(
            "CatMetroLicensedArtGuard: licensed art is not installed in this checkout ("
            + (rig ? "" : "Resources/" + RigResourcePath + " ")
            + (prop ? "" : "Resources/" + ProbePropResourcePath)
            + "). Install unity/Assets/Art/Generated/incoming/ from the machine-local source "
            + "(HANDOFF-NEXT-ORCHESTRATOR-2026-09-02.md, licensed install) or set "
            + OverrideEnvVar + "=1 for a deliberate placeholder build.");
    }
}
