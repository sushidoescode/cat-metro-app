# CM-C2b criterion 8 — device artifact (Pixel 9 Pro, session 2026-08-05)

**Verdict: criterion 8 stays OPEN — the frame-budget leg FAILED honestly** (median present
interval 33 ms vs the ≤16.7 ms budget). Two device-only defects diagnosed, both invisible to the
editor suite by construction; fix contract enters the build loop next; the criterion closes (or
fails again) on a re-measure of the fixed build. All other legs PASS.

## Device & build provenance
- Device: Google Pixel 9 Pro (caiman), serial-targeted over USB. **Deviation note (handoff
  requires honesty here): this exceeds the Pixel-6a-class bar the criterion names.** The budget
  miss below is a frame-rate cap, not load, so the over-spec device is not masking anything —
  a 6a-class device would show the same 33 ms wall.
- Android build id: `CP2A.260705.006` (`adb shell getprop ro.build.display.id`); display 120 Hz.
- APK: release IL2CPP/ARM64, Development Build OFF, debug signing, built from `main @ fcb3e83`
  (clean tree) via `BuildPipeline.BuildPlayer` — agent-driven CLI build, human-authorized
  in-session; editor-only untracked shim (full text at bottom; Editor assemblies never enter the
  player). Build log: `CLI_BUILD_RESULT Succeeded dev=False errors=0`.

## Legs
1. **Seam-loaded level proof — PASS.** From the measured process:
   `08-05 00:31:52.841  4751  4823 I Unity   : SEAM_LOADED content/levels/L001.json`
2. **Merged-manifest backup posture (RK-17) — PASS.** From the built APK:
   `aapt dump xmltree … AndroidManifest.xml` → `A: android:allowBackup(0x01010280)=(type 0x12)0x0`
   (allowBackup=false).
3. **60 s frame capture — measured, FAIL vs budget.**
   - *Instrument deviation (recorded):* the handoff's `dumpsys gfxinfo … framestats` is
     structurally empty for this app — Unity 6's GameActivity renders through a SurfaceView/BLAST
     queue, bypassing the HWUI renderer that gfxinfo measures (raw dump attached:
     `gfxinfo-empty-deviation.txt` — 9 UI views, zero game frames). Instrument used instead:
     **SurfaceFlinger timestats** (presented-frame intervals for the game's surface layer),
     enabled for exactly the play window (raw section attached: `timestats-game-layer.txt`).
   - *Window:* fresh process, one full L001 win run + win-banner hold, 1327 presented frames
     (~44 s presented). Composition note: L001's design offers ~8 s of active sim; the remainder
     renders the win-banner scene. (L001 cannot reach FailureReview at all — logged separately as
     a finding for the CM-C3 device legs.)
   - *Results (present-to-present):* totalFrames **1327**, droppedFrames **0**, averageFPS
     **30.304**; histogram `16ms×3, 33ms×1322, 50ms×1, 66ms×1` → **median 33 ms — FAIL**
     (budget ≤16.7 ms); **p99 33 ms — PASS at the boundary** (1%-low budget ≤33.3 ms).
   - *Diagnosis:* a rock-steady 30 fps with zero dropped frames and ~9 ms latch-to-present — this
     is Unity's Android **default frame-rate cap** (`Application.targetFrameRate` unset ⇒ 30 on
     Android), not a performance limit; the repo sets neither `targetFrameRate` nor `vSyncCount`
     anywhere. Enormous headroom is visible in the latch histogram.
   - *Second device-only defect (visual):* every runtime-created primitive renders **magenta**
     on device — the pipeline's lit shader is stripped from the release build because the built
     scene references no material (the entire board is runtime-created), and the editor never
     strips shaders, which is why all 350+ engine-side tests are green. Evidence:
     `magenta-greybox.jpeg` (human's device photo). Board topology, labels, input, win loop all
     correct on device.
4. **Release zero-footprint scan (CM-C3-DEVCAP criterion 4 leg B — ratified in-session as
   device-session evidence) — PASS, proven live.** `strings` over `libil2cpp.so` +
   `global-metadata.dat`: `DEVCAP_WRITTEN` count **0**; positive control `SEAM_LOADED` count
   **1** (the scan demonstrably reads real binary strings).

## Findings routed onward
- **F-DEV-1 (fix contract next):** no explicit frame-rate policy → Android caps at 30 fps.
- **F-DEV-2 (same contract):** shader stripped for runtime-only materials → magenta board.
- **F-DEV-3 (CM-C3 device legs blocker):** L001 can only Win or Halt — it cannot reach
  FailureReview, so the 20 fail/retry device cycles need a dev-only failable-level hook
  (human call pending).
- **F-DEV-4 (UX evidence for open Q-B/NEW-Q4):** the halt-at-pinned-boundary posture presents as
  a silent freeze on device — indistinguishable from a hang for a player.

## Build shim provenance (untracked, editor-only, deleted at session end)
```csharp
// UNTRACKED build-tooling shim for the CM-C2b criterion-8 device session (agent-driven
// Build And Run, human-authorized in-session 2026-08-05). Editor-only assembly: never enters
// the player. Full text recorded in the criterion-8 artifact; deleted after the session.
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
            + " size=" + report.summary.totalSize
            + " errors=" + report.summary.totalErrors
            + " out=" + outPath);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
```
