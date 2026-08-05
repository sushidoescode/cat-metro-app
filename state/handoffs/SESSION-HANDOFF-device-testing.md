# SESSION HANDOFF — device testing + tranche 3 (written 2026-08-05, post-#23)

Read order: `state/PROJECT_STATE.md` → this file → the relevant `state/handoffs/CM-*.md`.
Everything through CM-C3 is merged (#14–#23). The game is playable in-engine; the ONLY open
criteria are device legs: CM-C2b criterion 8 and CM-C3 criteria 2/4/7.

## Standing context
- Human decisions already made and recorded: RK-17 backup OFF · Q-K ratified · A-C8-10 cadence
  ratified · ADR-0006 errata ×2 (#23). Merges were human-delegated in the prior session;
  RE-CONFIRM delegation with the human before self-merging in this one.
- Editors installed: pinned 6000.3.16f1 (+ Android modules) among others. Project:
  `/Users/sushantsrikrish/cat-metro-app/unity`. Scene `Assets/Scenes/Game.unity` is the sole
  build scene; GameRoot self-boots through the StreamingAssets seam and logs `SEAM_LOADED`.
- Google Play Console is set up and verified but is NOT needed for local device testing — it
  becomes relevant at internal-testing time, and uploads are HUMAN-ONLY forever (AGENTS.md).
  A locally built APK with Unity's debug signing is exactly right for the device criteria;
  the keystore/Play App Signing item stays parked until release.

## Device test — step-by-step (Pixel 9 Pro over USB)
1. Phone: Settings → About phone → tap "Build number" 7× (Developer options on) →
   System → Developer options → enable "USB debugging". Plug into the Mac with a USB-C cable;
   tap "Allow" on the debugging prompt (check "Always allow").
2. Verify from the Mac: `~/Library/Android/sdk/platform-tools/adb devices` (or the SDK under
   `/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/`)
   — the device must list as `device`, not `unauthorized`.
3. Open the project in Unity Hub with EXACTLY 6000.3.16f1. File → Build Profiles: platform is
   already Android with Game.unity listed. For the criterion-8 RELEASE build: Development
   Build UNCHECKED. Click **Build And Run** (first IL2CPP/ARM64 build takes 10–20 min).
   The game installs and launches on the phone; L001 should render as the greybox board.
4. Criterion 8 artifact (attach to PR #21): play L001 continuously for 60+ seconds, then:
   - `adb shell dumpsys gfxinfo com.catmetro.game framestats > c2b-crit8-framestats.txt`
   - `adb logcat -d | grep SEAM_LOADED` (proves the seam-loaded level)
   - device model + Android build id (`adb shell getprop ro.build.display.id`)
   - merged-manifest proof: `aapt dump xmltree <built>.apk AndroidManifest.xml | grep -i allowBackup`
     (aapt lives under the AndroidPlayer SDK build-tools) — expect allowBackup=false.
   - Budgets: median frame ≤16.7 ms, 1%-low ≤33.3 ms. Pixel 9 Pro exceeds the Pixel-6a-class
     bar; note the class difference honestly in the artifact.
5. CM-C3 criteria 2/4/7 device legs: the agent should first add a small dev-only capture path
   (e.g., a Development Build flag that dumps FrameLog to persistentDataPath for `adb pull`),
   then the human runs 20 fail/retry cycles on low + mid tier per the contract. Do NOT mark
   these met from editor numbers (stop condition 8). A Pixel 9 Pro is neither low nor mid
   tier — borrow/emulate accordingly or record the deviation for the human to accept.

## Agent queue after the device session unblocks nothing (run in parallel)
Tranche-3 decompose per `state/PROJECT_STATE.md`: taxonomy (CM-R43.1-.3), L006–L010,
dead-newMechanic gate, content shipping pipeline, ContentSync (CatMetro.Editor). Same build
loop as before: frozen contract → red → green → fresh-context review round → disposition.
Landmines: project memory + every `state/handoffs/CM-*.md` status log. Never touch immutable
paths; `git add` by explicit path only (the user's `.claude/settings.json` stays out).
