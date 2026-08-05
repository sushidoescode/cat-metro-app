# CONTRACT CM-C2b — Greybox board: render, input, win/fail, manifest, 60 fps (Unity side)

**Roadmap:** D3 engineering, `docs/plan/data/roadmap_56_days.csv:4`.
**DEPENDS-ON:** CM-C2a merged ✓ (#8). **Q-G RESOLVED (recut 2026-08-04):** the scaffold merged as
#19 (`e586712`) with every pin verified — 6000.3.16f1, IL2CPP, ARM64, URP, Input System, minSdk 25 /
targetSdk 36, `com.catmetro.game`, created in place; asmdefs per ADR-0003/0005; the EditMode suite
runs 324/324 in-engine with the replay hash byte-identical to the dotnet leg (the prep note's
"replay-hash parity EditMode leg" is therefore DONE at scaffold; the `unity-editmode` CI job stays
Q-V human). **Keystore + Play App Signing remain open human items** (`docs/plan/EXECUTION_PLAN.md:439`;
ADR-0004:36) — release-era, not a greybox blocker. **minSdk anywhere is 25** — the roadmap's 24 is
superseded by AMD-08 (`docs/plan/EXECUTION_PLAN.md:349-350`).

### Goal

L001, loaded through CM-C2a's importer, renders as a greybox board and is playable to a win and to an
overflow fail — with taps driving the shipped Domain through the command log and Presentation only
interpolating.

### Spec reference

`docs/prd/PRD.md` CM-R07.1/.3 · CM-R20.1 (≥48dp) · CM-R51.1 (`docs/prd/PRD.md:810`) · CM-R52 (perf) ·
`docs/prd/ux-flows.md` S-02 (`:148-198`) · `docs/adr/0003-*` (Presentation/Application/Bootstrap rows) ·
`docs/adr/0007-*` (UGUI+TMP, screen stack, Input System, no Addressables) ·
`docs/architecture/overview.md` §3 (`:119-149` tap → command → Step → snapshot → interpolate), §7.

### Acceptance criteria (11) — 1–8 from tranche 1 unchanged in substance; 9–11 added by the
### 2026-08-04 recut (state/handoffs/CM-C2b-C3-prep.md) to absorb what landed since tranche 2

1. **Greybox render fidelity.** Loading L001 instantiates exactly one view object per authored board
   element (4 nodes incl. 1 source and 2 stations, 3 edges, 1 switch), each carrying the authored id
   and positioned at the authored grid coordinate. *Check:* an EditMode/PlayMode test enumerating the
   scene and comparing the id set and coordinates to the DTO.
2. **Tap targets ≥48dp and one gesture handler.** On the 360×640dp reference frame
   (`docs/prd/ux-flows.md:32`), every interactive element's effective hit rect is ≥48dp (CM-R20.1); the
   Game scene registers **exactly one** gesture handler and zero drag/pinch/long-press-to-aim handlers
   (CM-R07.1). *Check:* two automated UI tests (enumerate-and-measure; enumerate-and-assert-count).
3. **Tap → command → tick, and the frame log exists.** A tap on the junction (a) changes the lever's
   visual state on the first rendered frame after tap-down, and (b) appends exactly one
   `ToggleSwitchCommand` to `CommandLog` (`unity/Assets/Scripts/Domain/Commands.cs:8-18,38`), applied at
   the next tick boundary (CM-R07.3; `docs/architecture/overview.md:129-137`). Two taps in one tick
   produce two entries in receipt order. **This criterion also delivers the instrumented frame log**
   CM-C3 criteria 2, 4 and 7 measure against:
   `unity/Assets/Scripts/Presentation/Diagnostics/FrameLog.cs`, one record per rendered frame with
   exactly `frameIndex:int`, `monotonicMs:long`, `simTick:int`, `screenState:string`; `monotonicMs`
   comes from **one** clock source, named in the file header and in every artifact citing the log.
   *Check:* a PlayMode test asserting log contents and applied tick; a frame-log assertion for the
   lever state; one test asserting a record per frame with all four fields populated and `monotonicMs`
   non-decreasing.
4. **Win by routing the authored cat count.** Playing L001 with the correct route delivers
   `win.deliveries` cats and the run ends `Won`, with the LOCKED banner string `"All cats home!"`
   (`docs/prd/ux-flows.md:188`) read from `unity/Assets/Resources/Strings/ui.csv`, **which this
   contract creates** (CM-C3 appends rows). Zero literal UI strings in components. **The asserted
   number is the level file's own `win.deliveries` (2 as authored), not the roadmap's "10" — Q-E.**
   *Check:* one PlayMode test asserting outcome, banner text, and that the text resolved through
   `ui.csv`.
5. **Fail by overflow — queue overflow only (Q-J).** A scripted fixture board whose **node queue**
   reaches `queueCapacity` and is not cleared within the 16-tick Overload window ends the run with
   `Failed(QueueOverflow)` (`Simulation.cs:131-151`), and the fail state renders (banner present, board
   still visible). *Check:* one PlayMode test asserting outcome + banner presence.
   **Deferred, not met:** roadmap D3's "fail by platform overflow" (`roadmap_56_days.csv:4`) is
   unmeetable while NEW-Q4/Q-J are open (`Outcomes.cs:40-42`). The PR records it as deferred and cites
   Q-J; it re-opens as a follow-up contract when Q-J lands.
6. **Presentation never simulates.** A static check asserts zero calls to `Simulation.Step` outside
   `CatMetro.Application` and the test assemblies; a unit test on the interpolator asserts that at a
   60 Hz render rate against an 8 tps sim the interpolation factor stays in `[0,1)`, increases
   monotonically between snapshots, and resets exactly once per tick (ADR-0002 §1;
   `docs/architecture/overview.md:147`). *Check:* grep assertion + one NUnit case in `EditMode/Pure/`.
7. **Manifest compliance.** The generated Android manifest declares `minSdkVersion=25` and
   `targetSdkVersion=36`; a check fails the build on any lower value (CM-R51.1, `docs/prd/PRD.md:810`;
   AMD-08). *Check:* a build-step assertion over the merged manifest, output pasted in the PR.
8. **60 fps on a Pixel-6a-class device — HUMAN-VERIFIED.** *An agent cannot run this.* On a
   Pixel-6a-class device, an IL2CPP/ARM64 release build playing L001 for **60 continuous seconds**
   records **median frame time ≤16.7 ms and 1%-low ≤33.3 ms** via `adb shell dumpsys gfxinfo <pkg>
   framestats` or the Unity profiler. The run artifact (device model, build id, raw frametime table,
   both figures) is attached to the PR. **The criterion fails if the artifact is absent**, not merely
   if the numbers miss (`roadmap_56_days.csv:4`; CM-R52).
9. **Bootstrap seams land (recut).** `CatMetro.Bootstrap` (asmdef per ADR-0003's added 10th row —
   the ONLY assembly that may name the engine's persistent data path) implements **`IStorageRoot`**
   (persistent + cache paths; CM-C7's `SaveStore` becomes constructible on device) and
   **`IContentSource`** (StreamingAssets reads via an Android-capable engine API — ADR-0008:53-56:
   the APK keeps StreamingAssets where plain file reads cannot reach on device; editor tests prove
   the seam, the device leg rides criterion 8's session). *Check:* an EditMode/PlayMode test loads
   L001 THROUGH the Bootstrap `IContentSource` into the importer and renders it (criterion 1 runs on
   this path, not a test shim); one test constructs `SaveStore` AND `AnalyticsQueue` against the
   Bootstrap `IStorageRoot` and commits+reloads each; `scripts/check.sh`'s runtime-tree guard stays
   green (Bootstrap must not reference solver types); a STATIC assertion that the Bootstrap content
   reader routes StreamingAssets through `UnityWebRequest` (zero plain-file reads of the
   streamingAssets path outside an explicitly editor-only branch — evaluator D4: an editor-passing
   `File.ReadAll` impl fails on device); and a grep over `unity/Assets/Scripts/**` EXCLUDING
   `Bootstrap/**` asserting zero references to the engine's persistent/cache path APIs (evaluator
   D5: Presentation is the first engine-referencing assembly where the ADR-0003 invariant becomes
   violable — the grep is appended to a discovered wrapper or `scripts/check.sh`). Criterion 8's
   device artifact additionally records that the played L001 was loaded through the Bootstrap seam
   (one log line in the run artifact).
10. **StreamingAssets ships the corpus + bounds, byte-identical (recut — closes Q-Y).**
    the staged set is **exactly the merged corpus — ALL of `content/levels/*.json` (L001–L005
    today)** — byte-identical file-for-file, and `unity/Assets/StreamingAssets/config/runtime_bounds.json`
    is a byte-identical copy of `config/runtime_bounds.json` (ADR-0009:33's `ci` clause, deferred by
    CM-C7's Q-Y note, becomes satisfiable HERE). The "no levels beyond L001" non-goal means no NEW
    authored levels — staging the already-merged corpus is this criterion's copy, not authoring.
    *Check:* a SET-EQUALITY assertion (filename sets equal in both directions — evaluator D8: pairwise
    identity alone is omission-blind) plus per-file byte-identity, in a discovered wrapper that FAILS
    on drift in either direction; the CI wiring of ADR-0009:33 itself stays Q-V (human `.github/**`).
    **Deviation recorded (evaluator D9):** ADR-0008:57-62's `ContentSync` editor tooling (the
    prevention half) needs a `CatMetro.Editor` assembly that does not exist; this criterion ships the
    copies + the drift GATE, and `ContentSync` is the named follow-up riding the first CatMetro.Editor
    contract.
11. **RK-17 backup posture — implement the human's decision, or stop loudly (recut).** ADR-0006
    §Open conflict (`:291-333`) leaves the posture open and `docs/prd/risks.md:80` (quoted at
    ADR-0006:394-396) requires it to land WITH the save format; the save format merged (#16), so
    **RK-17 is now PAST DUE — a human decision is required during this contract's window.** If decided:
    the manifest/backup-rules artifact implements it, and `analytics_queue.dat` + its transient `.tmp`
    are excluded UNCONDITIONALLY (ADR-0006 §5; Q-U/M-21's satisfied-by-exclusion deviation in
    `state/handoffs/CM-C8.md`). If still undecided at build time: criterion 7 ships the SDK-version
    assertions only, and the PR names RK-17 as the open release-gate blocker — never a silent default.
    *Check (conditional — evaluator D6):* **decided branch** — the merged-manifest assertion covers
    the chosen posture AND a grep asserts the queue file (+ its transient `.tmp`) is named in the
    committed backup-rules XML. **Undecided branch** — a grep asserts NO `android:allowBackup`
    attribute and NO backup-rules XML is committed anywhere, and the PR carries the named-blocker
    note **stating plainly that criterion 8's device session then runs under Android's platform
    default (backup-ON) — RK-17's exploit posture — as a knowingly-accepted, PR-named exposure
    (evaluator D7). The human decides RK-17 either before the device session or by accepting that
    named exposure; an agent never picks the posture.**

### Scope boundary

**In scope:** the paths in the ownership table for CM-C2b, plus the PlayMode/EditMode harness
wrapper(s) under `tests/unity/` (e.g. `tests/unity/editmode.test.sh`, ADR-0005:93) — **except**
`tests/unity/failure.test.sh`, which is CM-C3's — plus registration-only appends.

**Explicit non-goals:**
- **No polish, no art pass, no audio, no haptics, no VFX** — greybox primitives only.
- **No fail/retry loop, no cause camera, no next-wave preview HUD, no results chrome** — CM-C3.
- **No scoring/chain/star UI** (pin NEW-Q5). **No solver, no validator** (CM-C4/C5).
- **No levels beyond L001**, no daily, no Night Harbor.
- **No SDK, no commerce, no ads, no analytics-taxonomy work** — `**/billing/**`, `**/iap/**`, `**/ads/**`
  are monetization tripwires requiring `state/mode=production` first (AGENTS.md; `state/PROJECT_STATE.md:10`).
  (Recut note: constructing CM-C7's `SaveStore`/CM-C8's queue against Bootstrap seams is criterion 9's
  wiring, not new save/analytics behaviour — neither type gains a line of code here.)
- **No edits to CM-C1's Domain sources or CM-C2a's importer**; **no `Compile Include` append**.
- **No daily DEVICE limbs (recut):** the 250 ms on-device salt loop, ≤200 ms boot validation, the
  30-board backup pool (CM-R46.3/.4) **and CM-C6 criterion 7's handed-off two-device same-seed check
  (roadmap D12; evaluator D10)** stay OUT — all gated on **Q-S** (no board generator exists; the
  shipped `IBoardFactory` stub is fixed-board by design) plus a backup-pool content contract and a
  two-device session. Recut them when Q-S lands; do not fake them against the stub.
- **No writes to immutable paths** (AGENTS.md hard rule 1). **No schema change.**

### Assumptions

- **A-C2b-1** SATISFIED at recut: #19's scaffold matches Q-G exactly (verified in the PR evidence);
  the stop now guards regressions only.
- **A-C2b-2** `IContentSource` (declared by CM-C2a) is implemented in `CatMetro.Bootstrap`
  (ADR-0003:71-74; overview.md:211). CM-C2b implements only what L001 loading needs.
- **A-C2b-3** The greybox uses colour **plus symbol** placeholders from the start, because colour-alone
  encoding is a merge-gate failure later (CM-R21.1); full triple-coding art is out of scope.

### Stop conditions

Defaults apply. Plus:
1. The Unity scaffold is missing, differs from Q-G, or removed/moved `unity/Assets/Scripts/Domain/**`
   or `.../Content/**` → stop.
2. Any Domain or importer behaviour change is needed to make a render or win/fail criterion pass →
   stop; that is a CM-C1/CM-C2a amendment and re-opens the golden.
3. A criterion cannot be met without touching a monetization path or an SDK → stop.
4. The 60 fps criterion cannot be evidenced because no device is available → stop and hand criterion 8
   to the human as explicitly open; do **not** mark it met from an editor measurement.

---

