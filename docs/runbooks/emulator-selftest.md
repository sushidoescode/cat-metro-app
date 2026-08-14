# Emulator self-test runbook (EMU-RIG)

How an agent (or a human) boots an Android emulator, installs a dev build, plays the
game by tap, and captures visual evidence — the loop proven live on 2026-08-14, when
the rig played and won L001 and L002 unassisted. Helper: `scripts/emu-selftest.sh`
(serial-guarded; emulator-only). Sibling doc for editor-side capture:
`docs/runbooks/device-capture.md`.

## One-time setup

1. **Java for sdkmanager**: the SDK's `sdkmanager` silently needs a JDK. Use Unity's:
   `export JAVA_HOME="/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/AndroidPlayer/OpenJDK"`.
   Without it, sdkmanager fails with a misleading "Please visit java.com" message —
   and if you pipe its output through `tail`/`head`, the failure exit code is masked
   (a lesson this repo has now paid for twice; never pipe a gate).
2. **System image**: Apple-silicon hosts need `system-images;android-34;google_apis;arm64-v8a`.
3. **AVD**: `avdmanager create avd -n catmetro-test -d pixel_7 -k <image>`, then cap the
   data partition in `~/.android/avd/catmetro-test.avd/config.ini`:
   `disk.dataPartition.size=3G` (the default balloons past 7GB and has filled this disk).

## Boot / install / drive

- Boot headless: `bash scripts/emu-selftest.sh boot` (flags used: `-no-window -gpu
  swiftshader_indirect -no-audio -no-boot-anim`).
- Install: `bash scripts/emu-selftest.sh install <dev.apk>` · launch: `... launch`.
- Capture: `... frame out.png` · tap: `... tap <x> <y>` · health: `... status`.
- Tap coordinates are RAW framebuffer pixels (portrait 1080x2400). When a viewer
  displays a scaled screenshot, multiply its coordinates back to the raw size before
  tapping.

## Trap ledger (each of these cost real time)

- **SwiftShader first draw is slow**: the game reports "Fully drawn" ~44s after a cold
  boot's first launch. Black screencaps before that are normal — wait, don't debug.
- **OS dialogs steal focus and pause Unity**: a system dialog over the game leaves the
  player in `pauseUnity` limbo after dismissal. Fix: `bounce` (HOME + relaunch).
- **Rotation is driven by the app, not by Settings**: with the pre-ORIENT-LOCK build the
  activity is the orientation source (`fullSensor`), so `settings put system
  user_rotation` and even `cmd window user-rotation lock` do NOTHING. Drive the virtual
  accelerometer instead: `adb emu sensor set acceleration 9.81:0:0` (landscape) /
  `0:9.81:0` (portrait) with `accelerometer_rotation` enabled.
- **Landscape is sticky on the unlocked build**: once rotated, the activity stays
  landscape after the sensor returns to portrait — warm relaunch does not fix it; only
  `force-stop` + relaunch (`coldstart`) does. ORIENT-LOCK (PR #87) removes the whole
  class; its after-proof must show the sensor sweep leaving the display at ROTATION_0.
- **Stale emulator locks**: "another emulator with the same AVD is running" after a
  crash → `pkill -9 qemu-system-aarch64` and delete `~/.android/avd/catmetro-test.avd/*.lock`.
- **The physical Pixel (serial `2G0YC5ZF7Z056Q`) shares the adb server.** It is the
  human's tester device. Nothing in this rig may address it: the helper hard-refuses
  serials that do not start with `emulator-`; keep raw `adb` commands out of self-test
  flows entirely.

## What the first full pass verified (evidence pack)

`evals/results/device/emu-gameplay-pass-2026-08-14/` — frames + ARTIFACT.md manifest:
boot-to-halt, tap-retry, mid-transit switch flip, matched delivery, L001 "All cats
home!" + Next, L002 load with blue cast, mixed-color play, L002 win — plus the
landscape-defect before-frame that motivates ORIENT-LOCK. Known dev-build noise in
frames: six collider errors in the boot console (tracked, art-chain) and the
NEW-Q4 domain-guard halt banner, which is correct behavior for a misrouted cat.

## Open question (human queue)

Wiring `emu-selftest` into CI would need a hosted-runner emulator budget ruling —
recorded, not assumed.
