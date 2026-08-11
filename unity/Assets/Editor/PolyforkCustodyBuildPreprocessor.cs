using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace CatMetro.Editor
{
    // Covers GUI Build/Build And Run and any direct BuildPipeline caller. The flow token prevents
    // accidental alternate entry and sequences the canonical path; it is not authentication or
    // proof that scripts/build.sh ran because any process under the licensed owner's UID can forge
    // it. Release evidence pairs the shell-gate transcript with the same final-head build record.
    public sealed class PolyforkCustodyBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            ConsumeCanonicalBuildFlowToken();
            PolyforkLocalCustody.RequireExact();
        }

        public static void RequireCanonicalBuildFlowTokenPresent()
        {
            ValidateBuildFlowToken();
        }

        public static void ConsumeCanonicalBuildFlowToken()
        {
            string fullPath = ValidateBuildFlowToken();
            File.Delete(fullPath);
            if (File.Exists(fullPath))
                throw new BuildFailedException("Build-flow token was not consumed");
        }

        private static string ValidateBuildFlowToken()
        {
            string path = Environment.GetEnvironmentVariable(
                "CM_POLYFORK_BUILD_FLOW_TOKEN");
            string nonce = Environment.GetEnvironmentVariable(
                "CM_POLYFORK_BUILD_FLOW_NONCE");
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(nonce))
                throw new BuildFailedException(
                    "Android builds must run through scripts/build.sh licensed-local profile");

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    "Invalid build-flow token path: " + exception.GetType().Name);
            }
            if (!File.Exists(fullPath)
                || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0
                || !string.Equals(File.ReadAllText(fullPath).Trim(), nonce,
                    StringComparison.Ordinal))
                throw new BuildFailedException("Invalid build-flow token");
            return fullPath;
        }
    }
}
