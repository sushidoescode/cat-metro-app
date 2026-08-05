# CM-C2b criterion 8 — device artifact (Pixel 9 Pro, session 2026-08-05)

**Verdict: criterion 8 stays OPEN — the frame-budget leg FAILED** on the median budget (33 ms vs
≤16.7 ms) and, under the mean-of-worst-1% convention, on the 1%-low budget too (36.8 ms vs
≤33.3 ms; see leg 3 for both readings — the contract does not pin the definition). The capture
window also **missed the contract's "60 continuous seconds" clause** (~44 s presented — named
here as a miss, not a footnote). Two device-only defects diagnosed; fix contract enters the build
loop next. **Disposition is HUMAN-only** (frozen contract: "HUMAN-VERIFIED. An agent cannot run
this.") — this artifact records evidence and defects; it does not close or fail the criterion.

This is revision 2: a fresh-context review round (PR #25, 9 findings) corrected a false
frame-policy claim, added the second 1%-low reading, named the 60 s miss, tightened the active-sim
figure, ruled out the second sanctioned instrument, and made the zero-footprint scan reproducible.

## Device & build provenance
- Device: Google Pixel 9 Pro (caiman), serial-targeted over USB; display 120 Hz. **Deviation note
  (handoff requires honesty here): this exceeds the Pixel-6a-class bar the criterion names.** The
  median miss below is a frame-rate cap, not load — but note the cap-independent caveat in leg 3:
  active-sim time on 6a-class hardware remains unmeasured.
- Android build id: `CP2A.260705.006` (`adb shell getprop ro.build.display.id`).
- APK: release IL2CPP/ARM64, Development Build OFF, debug signing, built from `main @ fcb3e83`
  (clean tree) via `BuildPipeline.BuildPlayer` — agent-driven CLI build, human-authorized
  in-session; editor-only untracked shim (full text at bottom; Editor assemblies never enter the
  player). Build log: `CLI_BUILD_RESULT Succeeded dev=False errors=0`.
  APK SHA-256: `634dfc8c1be8e6720616fc5e191cb72d2283cc7ddbb8093d2edb0782d93fbc6b`
  (reproducible from `main @ fcb3e83` + the disclosed shim).

## Legs
1. **Seam-loaded level proof — PASS.** From the measured process:
   `08-05 00:31:52.841  4751  4823 I Unity   : SEAM_LOADED content/levels/L001.json`
2. **Merged-manifest backup posture (RK-17) — PASS.** From the built APK:
   `aapt dump xmltree … AndroidManifest.xml` → `A: android:allowBackup(0x01010280)=(type 0x12)0x0`
   (allowBackup=false).
3. **Frame capture — measured, FAIL vs budget; window short of contract.**
   - *Instrument deviation (recorded):* the contract sanctions `gfxinfo framestats` **or the Unity
     profiler**. gfxinfo is structurally empty for this app — Unity 6's GameActivity renders
     through a SurfaceView/BLAST queue, bypassing the HWUI renderer gfxinfo measures (raw dump
     attached: `gfxinfo-empty-deviation.txt` — 9 UI views, zero game frames). The Unity profiler
     is also ineligible: it requires a Development Build / autoconnect, and criterion 8 mandates a
     non-development build. Instrument used instead: **SurfaceFlinger timestats** (presented-frame
     intervals for the game's surface layer), enabled for exactly the play window (extract with
     capture provenance attached: `timestats-layers.txt` — game layer + two control layers).
   - *Window — CONTRACT MISS:* the contract demands **60 continuous seconds**; this window is
     1327 presented frames ≈ **43.8 s** (fresh process, one full L001 win run + win-banner hold).
     Composition: L001's design offers **6.25 s of active sim** (win at tick 50, 8 ticks/s); the
     remainder renders the win-banner scene. L001 cannot loop or reach FailureReview (F-DEV-3), so
     a release-build window longer than one run has no active-play source today. The re-measure
     protocol below puts this to the human.
   - *Results (present-to-present):* totalFrames **1327**, droppedFrames **0**, jankyFrames **0**,
     averageFPS **30.304**; histogram `16ms×3, 33ms×1322, 50ms×1, 66ms×1` (1 ms floor-buckets).
     **Median: the 33 ms bucket — FAIL** vs ≤16.7 ms. **1%-low, both readings** (the contract
     does not define the term): p99 lands in the 33 ms bucket, which **straddles** the 33.3 ms
     budget — a floor-bucketed histogram cannot settle that boundary; mean-of-worst-1% (13 frames:
     66+50+11×33 = 479 ms) = **36.8 ms — FAIL** vs ≤33.3 ms. The re-measure must capture a raw
     per-frame table (e.g. SurfaceFlinger `--latency` timestamps) so boundary calls are exact, and
     the human should pin the 1%-low definition before it runs.
   - *Diagnosis:* a steady 30 fps with zero dropped frames and zero janky frames. Frame-rate
     policy in the repo as-built: `Application.targetFrameRate` is never set anywhere;
     `unity/ProjectSettings/QualitySettings.asset` sets `vSyncCount: 0` on the `Mobile` quality
     level, which Android boots into (`m_PerPlatformDefaultQuality: Android: 0`). With vsync off
     and no explicit target, **Unity's Android default target (30 fps) governs** — a cap, not a
     performance limit. Headroom evidence: droppedFrames 0, jankyFrames 0, and post-to-acquire at
     0–3 ms across the window (the app submits frames essentially instantly).
   - *Second device-only defect (visual):* every runtime-created primitive renders **magenta** on
     device — the pipeline's lit shader is stripped from the release build because the built scene
     references no material (the entire board is runtime-created), and the editor never strips
     shaders, which is why all engine-side editor tests are green. Evidence:
     `magenta-greybox.jpeg` (human's device photo). Board topology, labels, input, and the win
     loop are all correct on device.
4. **Release zero-footprint scan (CM-C3-DEVCAP criterion 4 leg B — ratified in-session as
   device-session evidence) — PASS, proven live and reproducible.** Exact procedure against the
   APK identified by the SHA-256 above:
   ```
   unzip -o -q catmetro-c8-release.apk lib/arm64-v8a/libil2cpp.so \
       assets/bin/Data/Managed/Metadata/global-metadata.dat
   { strings lib/arm64-v8a/libil2cpp.so; \
     strings assets/bin/Data/Managed/Metadata/global-metadata.dat; } | grep -c DEVCAP_WRITTEN
   # → 0
   { strings lib/arm64-v8a/libil2cpp.so; \
     strings assets/bin/Data/Managed/Metadata/global-metadata.dat; } | grep -c SEAM_LOADED
   # → 1   (positive control: the scan demonstrably reads real binary strings)
   ```
   (`strings` at its default minimum length 4; both target literals are ≥4 chars.)

## Re-measure protocol (for the human to dispose with the fix contract)
1. Fix contract (F-DEV-1/2) merges; rebuild release from the new main; re-install.
2. Raw per-frame table (SurfaceFlinger `--latency` timestamps), 60+ s presented, budgets computed
   from raw intervals; 1%-low definition pinned by the human beforehand.
3. Composition call (human): accept win+banner-hold composition for the release build (disclosed),
   or wait for a replay/menu affordance (UX lane) so the window holds 60 s of active play, or
   accept a documented deviation. L001-as-shipped cannot supply 60 s of active sim.

## Findings routed onward
- **F-DEV-1 (fix contract next):** frame-rate policy absent (`targetFrameRate` unset,
  `vSyncCount: 0` on the active quality level) → Android runs at the 30 fps default cap.
- **F-DEV-2 (same contract):** shader stripped for runtime-only materials → magenta board.
- **F-DEV-3 (CM-C3 device legs blocker):** L001 can only Win or Halt — both J1 exits terminate at
  a station and a mismatched cat throws at arrival (pinned NEW-Q4), so FailureReview and even
  TimeOut are unreachable; the 20 fail/retry device cycles need a dev-only failable-level hook
  (human call pending). Verified independently by the PR #25 reviewer.
- **F-DEV-4 (UX evidence for open Q-B/NEW-Q4):** the halt-at-pinned-boundary posture presents as
  a silent freeze on device — indistinguishable from a hang for a player.

## Review round (PR #25)
Fresh-context review: 9 findings (1 High). All addressed in revision 2: F-1 false frame-policy
claim corrected (High); F-2 dual 1%-low reading added — leg now FAILS under the conventional
definition; F-3 bucket-boundary indeterminacy named + raw-table re-measure required; F-4 60 s
shortfall named as a contract miss; F-5 active-sim corrected to 6.25 s + 6a-claim scoped; F-6
Unity-profiler ineligibility stated; F-7 scan transcript + APK SHA-256 added; F-8/F-9 fixed in
`SESSION-HANDOFF-ux.md` (gate-evolution carve-out; attested/ + trust.json added to the immutable
list). Nits: headroom citation corrected, timestats extract renamed with provenance header,
active-sim figure tightened.

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
