// iOS Xcode-project post-processing (IOS-RELEASE, 2026-08-25). Editor-only assembly:
// never enters the player.
//
// This runs on EVERY iOS build — the scripted one in CatMetroCliIosBuild and a build
// started by hand from the Editor's Build Settings window alike. That is deliberate. The
// one thing this file writes is a declaration that App Store Connect otherwise asks you
// for interactively on every single upload, and an interactive question is exactly what
// you do not want standing between a finished archive and a submission at 2am.
//
// THE WHOLE FILE IS BEHIND `#if UNITY_IOS`, because `UnityEditor.iOS.Xcode` ships inside
// the iOS Build Support module (PlaybackEngines/iOSSupport). A machine without that module
// installed cannot select iOS as the active build target, so UNITY_IOS is never defined
// there and this file compiles to nothing — editor compilation stays green either way.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class CatMetroIosPostProcess
{
    // ---------------------------------------------------------------------------------
    // EXPORT COMPLIANCE — read this before changing it, it is a legal declaration.
    //
    // US export regulations require every App Store binary to declare whether it uses
    // encryption. `ITSAppUsesNonExemptEncryption = false` asserts that the app uses no
    // encryption beyond what is exempt — which, per Apple's own guidance, includes
    // encryption provided by the operating system (HTTPS/TLS via URLSession or the
    // equivalent) and does not require an ERN or a year-end self-classification report.
    //
    // That assertion matches the dependencies inspected on 2026-08-25: no bundled
    // proprietary crypto, custom cipher, VPN, end-to-end-encrypted messaging, or other
    // non-exempt cryptography was found. Roadmap SDKs are NOT part of that evidence. Re-audit
    // the final archive after adding RevenueCat, ads, notifications, analytics, crash tooling,
    // or any other SDK; an SDK name or an HTTPS endpoint is not proof of its implementation.
    //
    // FLIP THIS TO true, and then go and read Apple's export-compliance documentation
    // properly, if any of that stops being true — in particular if we ever ship our own
    // encryption of user data at rest, or a cloud-save path that encrypts payloads
    // ourselves rather than relying on TLS. Getting this wrong is not a rejection, it is
    // a false statement to a US regulator.
    // ---------------------------------------------------------------------------------
    const bool UsesNonExemptEncryption = false;

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        if (!File.Exists(plistPath))
        {
            // Keep the generated project available for inspection rather than replacing the
            // original build result with a postprocess failure. The final-binary determination
            // remains a human release gate; missing this key is not evidence for either answer.
            Debug.LogWarning("CM_IOS_POSTPROCESS skipped reason=no-Info.plist path=" + plistPath
                + " — App Store Connect will ask for the export-compliance declaration "
                + "interactively on upload instead. Determine the answer against the final "
                + "binary and SDK set; no automatic answer is safe when this step was skipped.");
            return;
        }

        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", UsesNonExemptEncryption);
        plist.WriteToFile(plistPath);

        // Logged on every build so the declaration is auditable from the build log rather
        // than only from this source file.
        Debug.Log("CM_IOS_POSTPROCESS ITSAppUsesNonExemptEncryption="
            + (UsesNonExemptEncryption ? "true" : "false") + " plist=" + plistPath);
    }
}
#endif
