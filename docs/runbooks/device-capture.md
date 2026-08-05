# Device capture runbook — CM-C3 criteria 2/4/7 (FrameLog via CM-C3-DEVCAP)

The instrument: a Development-Build-only component that serialises the CM-C2b FrameLog (the one
named clock) to a fixed file on pause, so a human can run scripted fail/retry cycles on a device
and reduce the pull to the same tables the editor leg prints. It ships no evidence itself — CM-C3
criteria 2/4/7 close only when a human attaches the device tables (low + mid tier).

## Protocol (per device tier)
1. Build with **Development Build CHECKED** — this is a *different* APK from CM-C2b criterion 8's
   release build; never conflate the two artifacts.
2. Run **20 complete fail → retry cycles** on the device (a failable level must be in the boot
   path — see the F-DEV-3 note below).
3. Press **Home** once. That fires the pause hook — the single write. Nothing is written during
   play, so no capture I/O can land inside a measured interval.
4. `adb logcat -d | grep DEVCAP_WRITTEN` → prints the absolute path, row count, mark count.
   **Verify this line exists before unplugging** — a crash before Home loses the capture
   (accepted trade, A-DEVCAP-7).
5. Pull, depending on where step 4's path points:
   - external (`/storage/emulated/0/Android/data/com.catmetro.game/files/devcap/framelog.csv`):
     `adb pull <path> framelog.csv`
   - internal (`/data/user/0/com.catmetro.game/files/devcap/framelog.csv` — Unity's default;
     readable because a Development Build is debuggable):
     `adb exec-out run-as com.catmetro.game cat files/devcap/framelog.csv > framelog.csv`
6. `bash scripts/devcap-report.sh framelog.csv` → exactly five lines (`CYCLES=`,
   `CAUSE_MS_TABLE=`, `RETRY_MS_TABLE=`, `CAUSE_P95=`, `RETRY_P95=`) to paste into the CM-C3 PR,
   once per tier. The script exits non-zero under 20 complete cycles — a short capture can never
   be mistaken for evidence.

## Measurement definitions (ratified 2026-08-05, A-DEVCAP-3)
- **Cause interval**: last frame before the first FailureReview frame → the causeVisible=1 frame
  (the first frame on which the causal node is framed AND the banner renders — the identical
  predicate to the editor harness).
- **Retry interval**: last FailureReview frame → first Playing frame. Frame-anchored, not
  touch-anchored (an input timestamp would be a second clock; A-C3-6 forbids it). Worst-case
  under-report vs a touch anchor: ~one frame (~17 ms at 60 Hz) on a 1000 ms budget — noted in
  every artifact that cites these tables.
- **p95**: `ceil(0.95 * n) - 1` over the ascending-sorted table; budgets: cause ≤1500 ms,
  retry <1000 ms, p95 over 20 cycles, per tier.

## Known blocker (F-DEV-3, human call pending)
L001 — the only level in the boot path — can only Win or Halt; it cannot reach FailureReview, so
this protocol cannot run against it. The fail/retry cycles need a dev-only failable-level hook
(options go to the human with the DEVCAP PR). Do not improvise one: the boot path is outside
this contract's scope.
