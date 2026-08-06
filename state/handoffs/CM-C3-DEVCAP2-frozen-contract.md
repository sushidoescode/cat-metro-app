# CONTRACT CM-C3-DEVCAP2 — Dev-only boot-level override (the demo + measurement hook)

**Status: FROZEN 2026-08-06 at branch anchor (origin/main), branch
`task/CM-C3-DEVCAP2-level-override`.** The mechanism was human-ratified in-session 2026-08-05
("dev-only level override" chosen over shipping a failable level or waiting for UX replay), and
re-prioritized by the human 2026-08-06: the demo build is the point. **Criterion-5(a)
amendment (TimeOut loss) RATIFIED by the human in-session 2026-08-06** — informed by the
reviewer's independent confirmation of the input-independence proof and the F7 thinness note — "I'm looking for the
meatier build and testing an actual level so we can iterate on feedback."

**DEPENDS-ON:** CM-C3-DEVCAP merged (#26 — the guarded `Bootstrap/DevCapture/**` tree, its
clock/guard scanner, and the `devcap` directory convention this contract reuses).

### Goal

A Development-Build-only boot hook: if `<capture dir>/level.json` exists (the same
`persistentDataPath/devcap/` directory the capture already owns), the scene-boot path imports THAT
level instead of the shipped path — so a human can `adb push` any level (a failable demo board,
the 20-cycle measurement fixture) to a dev build without shipping a byte of it. Zero release
footprint, loud provenance, loud fallback.

### Acceptance criteria (5)

1. **Override honored, announced.** With a readable `level.json` in the devcap directory, the
   scene-boot path (`GameRoot.Awake` → `InitializeFromSeam`) imports those bytes through the
   REAL `LevelImporter` and logs one line matching `^DEVCAP_LEVEL_OVERRIDE .+/devcap/level\.json$`
   (SEAM_LOADED precedent) INSTEAD of the `SEAM_LOADED` line — the two lines are mutually
   exclusive per boot, so no artifact can ever mistake an override run for a seam run.
   *Check:* one PlayMode test with an injectable directory (same injection shape as
   `DevFrameCapture.OutputDirectory`): write a valid fixture level, boot via the scene path
   (`Awake` self-init — use the factory-suppression flag the tests already rely on, or drive
   `Awake` equivalently), assert the booted `Session.Level` matches the override (level id), and
   LogAssert the override line.
2. **Invalid override falls back LOUDLY.** An unreadable/unimportable `level.json` logs one
   error line matching `^DEVCAP_LEVEL_OVERRIDE_INVALID ` and boots the NORMAL shipped path
   (`SEAM_LOADED` fires); never a crash, hang, or silent half-boot.
   *Check:* one PlayMode test with garbage bytes in the injected directory.
3. **Absent override changes nothing.** No file → byte-identical behavior to today: seam boot,
   `SEAM_LOADED`, no override log. *Check:* one PlayMode test asserting the negative.
4. **Zero release footprint.** All new code lives in `unity/Assets/Scripts/Bootstrap/DevCapture/`
   (whole-file dev guard — the EXISTING `tests/unity/devcap.test.sh` scanner already enforces
   wrap + reference-guarding + clock-token ban over that tree with fixture-proven gates; this
   contract adds NO new scanner). GameRoot gains ≤5 added lines, all inside the existing
   `#if DEVELOPMENT_BUILD || UNITY_EDITOR` discipline, references at guard depth ≥1 — guarded BY CONSTRUCTION; the merged scanner's
   reference rule does not yet name `DevLevelOverride` (review F2; SYM extension filed as a
   follow-up in the status log — the wrapper is frozen by #26 and may not be edited here). The release strings scan (`DEVCAP_LEVEL_OVERRIDE` count 0 vs a dev-build
   positive control) rides the next release-APK scan alongside the deferred `DEVCAP_WRITTEN` leg.
   *Check:* `bash tests/unity/devcap.test.sh` green (the scanner sweeps the new file
   automatically); `git diff --name-only` in the PR proves the file table.
5. **A human-playable DEMO level exists in the test tree, solver-witnessed, never shipped.**
   `tests/fixtures/devcap/demo-level.json`: all-red cats, TWO red-accepting stations (the
   misroute/halt boundary is unreachable by construction — every route is color-legal), waves
   tuned so that (a) inactive play fails **[AMENDED at red, 2026-08-06 — see status log: via `TimeOut`; the sim's one-release-per-mouth-per-tick semantics (`Simulation.cs:12-24`) make QueueOverflow input-independent in single-source topologies, so the clock is the player-skill loss; burst waves keep the recoverable overload-ring drama]** (the fail/retry loop demonstrably
   fires — cause camera, banner, one-tap retry), and (b) active switching can win.
   *Check (both halves mechanical):* one PlayMode test boots it via the override seam and runs
   TWO legs — no-input to `Failed(TimeOut)`/`FailureReview` **[same amendment]**, and a scripted switching
   sequence to `Won` (the sequence may come from the solver's optimal log; if the solver deems
   it unsolvable, re-tune the level, never the test). Plus a wrapper leg asserting the demo file
   imports clean through the validator's runtime importer path. **Visual leg (standing user
   directive):** capture real frames of the demo sequence (boot → flood → FailureReview with
   ring + banner → retry → Playing) via an uncommitted probe, LOOK at them, and attach the
   description + PNG paths to the PR — code-green alone does not close this criterion.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Bootstrap/DevCapture/DevLevelOverride.cs` (+.meta) ·
≤5 guarded lines in `unity/Assets/Scripts/Bootstrap/GameRoot.cs` ·
`unity/Assets/Tests/PlayMode/Diagnostics/DevLevelOverrideTests.cs` (+.meta) ·
`tests/fixtures/devcap/demo-level.json` · an appended labelled block in
`tests/unity/devcap.test.sh` is **NOT permitted** (that wrapper is frozen by CM-C3-DEVCAP's
merge — instead the demo-import leg lives in this contract's own PlayMode tests) ·
`state/handoffs/CM-C3-DEVCAP2.md` status log · PROJECT_STATE +1 line on merge only.

**Non-goals / forbidden:** no edits to `FrameLogCsv.cs`/`DevFrameCapture.cs`/`devcap-report.sh`
(merged, frozen) · no scene edit, no Presentation edit, no Domain/Content/importer edit · no
shipped level (the demo lives in `tests/fixtures/`, reaches the device only via `adb push`) ·
no new dependency · no second input surface · no clock tokens under DevCapture (the scanner
fires) · storage-path APIs stay Bootstrap-only · no immutable paths.

### Assumptions

- **A-DEVCAP2-1.** The override reads plain `File.ReadAllBytes` — legitimate in Bootstrap
  (`persistentDataPath` is not the streaming path; the web-request rule guards
  `streamingAssetsPath` only, which this file never names — the scanner + editmode gates prove).
- **A-DEVCAP2-2.** Two red stations make `PlatformOverflow`/misroute unreachable by construction
  — the demo cannot collide with the Q-J/NEW-Q4 pins.
- **A-DEVCAP2-3.** The demo's fightability claim is mechanical (criterion 5's two legs +
  solver witness), and its FUN is the human's to judge on device — feedback iterates the level
  file, which is an adb-push away, no rebuild needed. That fast iteration loop is the point.

### Stop conditions

Defaults plus: (1) criterion 5(b) unreachable after 3 tuning attempts → ship the demo as
fail-only with the limitation named in the PR (the retry-loop demo still works; fightability
iterates with the human) — never weaken the test to pass. (2) Any need to edit the frozen
DevCapture files or wrapper → stop. (3) Any need for a scene/Presentation change → stop.
