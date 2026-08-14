#!/usr/bin/env bash
# Android emulator self-test helper (EMU-RIG). Wraps the recipe proven live on
# 2026-08-14: boot the project AVD headless, install a dev APK, drive the game by
# tap, capture framebuffer evidence, and probe rotation via the virtual
# accelerometer. Full trap ledger: docs/runbooks/emulator-selftest.md.
#
# Custody: every adb call is scoped to EMU_SERIAL through aemu(), and only
# emulator-* serials are accepted — physical devices on the same adb server must
# be unreachable from this tool no matter what the caller exports.
set -eu

SDK="${ANDROID_SDK_ROOT:-/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/AndroidPlayer/SDK}"
EMU_SERIAL="${EMU_SERIAL:-emulator-5554}"
AVD="${EMU_AVD:-catmetro-test}"
PKG="${EMU_PKG:-com.catmetro.game}"
ACTIVITY="${EMU_ACTIVITY:-com.unity3d.player.UnityPlayerGameActivity}"

case "$EMU_SERIAL" in
  emulator-*) ;;
  *) echo "emu-selftest: refusing non-emulator serial '$EMU_SERIAL'" >&2; exit 2 ;;
esac

PATH="$SDK/platform-tools:$SDK/emulator:$PATH"

aemu() { adb -s "$EMU_SERIAL" "$@"; }

usage() {
  sed -n 's/^  \([a-z-]*)\) *#/  \1/p' "$0" | tr -d ')'
  exit 1
}

cmd="${1:-}"; shift || true
case "$cmd" in
  boot) # start the AVD headless on EMU_SERIAL's own port (idempotent; bounded wait)
    if aemu shell true 2>/dev/null; then echo "already booted"; exit 0; fi
    port="${EMU_SERIAL#emulator-}"
    case "$port" in *[!0-9]*|'') echo "emu-selftest: cannot derive a port from '$EMU_SERIAL'" >&2; exit 1 ;; esac
    nohup "$SDK/emulator/emulator" -avd "$AVD" -port "$port" -no-window \
      -gpu swiftshader_indirect -no-audio -no-boot-anim >/dev/null 2>&1 &
    tries=0
    until aemu shell true 2>/dev/null; do
      tries=$((tries + 1))
      [ "$tries" -le 60 ] || { echo "emu-selftest: device did not appear within 180s" >&2; exit 1; }
      sleep 3
    done
    tries=0
    until [ "$(aemu shell getprop sys.boot_completed | tr -d '\r')" = "1" ]; do
      tries=$((tries + 1))
      [ "$tries" -le 60 ] || { echo "emu-selftest: boot did not complete within 180s" >&2; exit 1; }
      sleep 3
    done
    echo "booted"
    ;;
  install) # install <apk-path> (replacing any prior install)
    [ -f "${1:-}" ] || { echo "emu-selftest: install needs an apk path" >&2; exit 1; }
    aemu install -r "$1"
    ;;
  launch) # foreground the game activity (warm start)
    aemu shell am start -n "$PKG/$ACTIVITY"
    ;;
  bounce) # HOME then relaunch — clears stolen-focus pause states
    aemu shell input keyevent KEYCODE_HOME
    sleep 2
    aemu shell am start -n "$PKG/$ACTIVITY"
    ;;
  coldstart) # force-stop then launch — the only reset for a stuck orientation
    aemu shell am force-stop "$PKG"
    sleep 2
    aemu shell am start -n "$PKG/$ACTIVITY"
    ;;
  frame) # frame <out.png> — capture the raw framebuffer
    [ -n "${1:-}" ] || { echo "emu-selftest: frame needs an output path" >&2; exit 1; }
    aemu exec-out screencap -p > "$1"
    ;;
  tap) # tap <x> <y> in raw framebuffer coordinates (portrait: 1080x2400); digits only
    [ -n "${2:-}" ] || { echo "emu-selftest: tap needs x and y" >&2; exit 1; }
    case "${1}${2}" in *[!0-9]*) echo "emu-selftest: tap coordinates must be numeric" >&2; exit 1 ;; esac
    aemu shell input tap "$1" "$2"
    ;;
  rotate-landscape) # drive the virtual accelerometer to a landscape pose
    aemu shell settings put system accelerometer_rotation 1
    aemu emu sensor set acceleration 9.81:0:0
    ;;
  rotate-portrait) # portrait pose; a pre-ORIENT-LOCK build additionally needs coldstart
    aemu shell settings put system accelerometer_rotation 1
    aemu emu sensor set acceleration 0:9.81:0
    ;;
  status) # one-line health: serial, boot state, display rotation, foreground app
    boot=$(aemu shell getprop sys.boot_completed 2>/dev/null | tr -d '\r' || echo 0)
    rot=$(aemu shell dumpsys window displays 2>/dev/null \
      | grep -oE 'mDisplayRotation=ROTATION_[0-9]+' | head -1 || true)
    fg=$(aemu shell dumpsys activity activities 2>/dev/null \
      | grep -oE 'topResumedActivity.*' | head -1 || true)
    echo "serial=$EMU_SERIAL boot=$boot $rot $fg"
    ;;
  *) usage ;;
esac
