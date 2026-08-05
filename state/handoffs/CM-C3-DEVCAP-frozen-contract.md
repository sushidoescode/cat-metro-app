# CONTRACT CM-C3-DEVCAP — Dev-only FrameLog device capture (instrument for CM-C3 criteria 2/4/7)

**Status: FROZEN 2026-08-04 at branch anchor `fcb3e83` (main, post-#24), branch
`task/CM-C3-DEVCAP-frame-capture`.** Handoff-only contract (no `state/backlog.md` row — that file is
human-ordered; the omission is flagged to the human in-session). **A-DEVCAP-3 is analyst-authored and
unratified at freeze** — offered to the human this session, A-C3-2/Q-K precedent; overrule route is an
amendment to CM-C3 criterion 7's device-leg definition, priced at criterion 5's reducer + criterion
2's mark rule.
**Roadmap:** none of its own — this is tooling in service of D4 (`docs/plan/data/roadmap_56_days.csv:5`).
**DEPENDS-ON:** CM-C2b merged (#21, the FrameLog) **and** CM-C3 merged (#22, the fail/retry loop).
**Suggested branch:** `task/CM-C3-DEVCAP-frame-capture`.

### Goal

A Development-Build-only capture path that writes the **existing** CM-C2b FrameLog to a fixed,
documented location under `Application.persistentDataPath`, annotated with the one instant the log
cannot express on its own (cause-visible), so a human can run 20 scripted fail/retry cycles on a
low- and a mid-tier device, pull **one** file, and reduce it to the same `CAUSE_MS_TABLE` /
`RETRY_MS_TABLE` / `CAUSE_P95` / `RETRY_P95` lines the editor leg already prints. It introduces **no
second clock source** and leaves **zero footprint** in a non-Development build.

This contract ships the **instrument, not the evidence**. CM-C3 criteria 2, 4 and 7 stay OPEN after
it merges; they close when the human attaches the device tables.

### Spec reference

`state/handoffs/SESSION-HANDOFF-device-testing.md:38-42` (the ask) ·
`state/handoffs/CM-C3-frozen-contract.md:41-47` (criterion 2: cause visible ≤1500 ms, p95 over 20,
HUMAN-VERIFIED device leg), `:59-62` (criterion 7: tap→Playing <1000 ms, p95 over 20), `:109-110`
(**A-C3-6** — the frame log is the single clock source; a second one is a stop), `:126-128`
(stop condition 8: editor numbers never satisfy a device leg) ·
`state/handoffs/CM-C2b-frozen-contract.md:37-48` (criterion 3: the log's **exactly four** fields and
the one named clock) · `unity/Assets/Scripts/Presentation/Diagnostics/FrameLog.cs:6-9,10-22,33-40` ·
`unity/Assets/Tests/PlayMode/Board/FailureTests.cs:220-272` (the editor protocol this must
reproduce) · `docs/prd/ux-flows.md:287` (`A11Y-S03-1`: tap-to-Playing <1.0 s on **mid and low
tier**) · `docs/adr/0003-assembly-isolation-and-dependency-rule.md:105` +
`docs/architecture/overview.md:210` (`CatMetro.Bootstrap` is the **only** assembly that may name
`Application.persistentDataPath`).

### Acceptance criteria (6)

1. **The CSV is a verbatim serialisation of the existing log — no arithmetic, no second clock.**
   A pure function `FrameLogCsv.ToCsv(IReadOnlyList<FrameRecord> records, ISet<int> causeVisibleFrames)`
   emits the header `frameIndex,monotonicMs,simTick,screenState,causeVisible` and exactly one row per
   record **in record order**; columns 1–4 are the `FrameRecord` fields copied with **no arithmetic,
   no rounding, no re-basing** (`FrameLog.cs:10-22`), column 5 is `1` iff the row's `frameIndex` is in
   `causeVisibleFrames`, else `0`. `screenState` is CSV-quoted only if it contains `,` or `"`.
   *Check:* one PlayMode `[Test]` (plain NUnit, no `[UnityTest]`) over a hand-constructed 4-record
   list asserting the exact expected text, **plus** a `tests/unity/devcap.test.sh` grep asserting
   **zero** occurrences of `Time\.|realtimeSinceStartup|DateTime|DateTimeOffset|Stopwatch|Environment\.TickCount|frameCount`
   under `unity/Assets/Scripts/Bootstrap/DevCapture/**`, with `tests/fixtures/devcap-bad/Banned.cs`
   proving the pattern fires. **This grep is A-C3-6's enforcement** — it is the reason the capture
   cannot grow a clock later.

2. **`causeVisible` marks exactly the instant the editor leg measures, once per failure.**
   For each entry into `FailureReview`, **exactly one** frame is marked: the first frame at or after
   the transition on which `GameRoot.CauseCam.IsFramed && GameRoot.Banner.Visible` is true — the
   identical predicate to `FailureTests.cs:247-251` (`CauseCameraController.cs:26`,
   `BannerView.cs:14`). Zero frames are marked while `ScreenState != "FailureReview"`, and the marked
   `frameIndex` **equals** the `FrameRecord.FrameIndex` of the frame on which the predicate first held
   (not the frame before or after). The mark latch resets on leaving `FailureReview`, so 20 failures
   produce 20 marks.
   *Check:* one `[UnityTest]` over the existing scripted-overflow fixture, parameterised
   `[Values(true, false)] bool motionOff` (motion-on exercises the `PAN_DURATION_MS = 400`
   delay — `CauseCameraController.cs:15` — where the mark is genuinely later than the transition;
   motion-off exercises the same-frame cut), asserting: exactly one mark per failure across **two**
   fail→retry cycles, the frame-identity equality, and zero marks outside `FailureReview`.

3. **One fixed location, one write, announced in logcat.**
   The capture writes the whole CSV to `<OutputDirectory>/framelog.csv`, where `OutputDirectory`
   defaults to `Path.Combine(Application.persistentDataPath, "devcap")` (created if absent) and is
   **injectable** for tests. The write happens **only** in `OnApplicationPause(true)` — never on a
   transition, never on a timer, never per frame — so no capture I/O can land inside a measured
   interval. Each write emits exactly one log line matching
   `^DEVCAP_WRITTEN .+/devcap/framelog\.csv rows=[0-9]+ marks=[0-9]+$` (the `SEAM_LOADED` pattern,
   `GameRoot.cs:101`), so the human never guesses the path. Re-pausing overwrites with a superset.
   *Check:* one `[UnityTest]` that (a) drives a full fail→retry cycle with **no** pause and asserts
   the output directory contains no file, (b) invokes the pause hook with `OutputDirectory` set to a
   temp dir and asserts the file exists at `<temp>/framelog.csv` with `rows == Log.Records.Count`,
   (c) asserts `LogAssert` sees the `DEVCAP_WRITTEN` line, and (d) one plain `[Test]` asserting the
   **default** directory equals `Path.Combine(Application.persistentDataPath, "devcap")`.

4. **Zero footprint in a non-Development build, proven by a gate that fires.**
   Every `.cs` file under `unity/Assets/Scripts/Bootstrap/DevCapture/**` is **wholly** wrapped:
   the first line that is neither blank nor a `//` comment is exactly
   `#if DEVELOPMENT_BUILD || UNITY_EDITOR`, the last non-blank line is `#endif`, and directives
   balance. **Every** occurrence of the capture symbols (`DevFrameCapture`, `FrameLogCsv`) elsewhere
   under `unity/Assets/Scripts/**` — i.e. the wiring lines in `GameRoot.cs` — sits at preprocessor
   depth ≥1 inside such a region. Additionally, zero monetization/analytics tokens
   (`/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds|AnalyticsQueue`)
   appear under `DevCapture/**`.
   *Check:* `tests/unity/devcap.test.sh` runs a `python3` preprocessor-depth scan over the real tree
   (fail-closed if the root is missing) and then over `tests/fixtures/devcap-bad/`, which contains
   **two** planted violations — an unwrapped `DevCapture`-shaped file and an unguarded
   `DevFrameCapture` reference — and the wrapper **fails** unless the scan reports ≥2 there. The
   monetization pattern is proven live against the existing `tests/fixtures/save-bad/` (the
   `save.test.sh:38-40` precedent).
   *HUMAN leg (recorded with CM-C2b criterion 8's artifact, **not** a merge gate for this contract):*
   `strings <release-apk-libil2cpp-or-metadata> | grep -c DEVCAP_WRITTEN` on the **release** APK the
   human already builds → `0`.

5. **The reducer prints the editor-format tables from a pulled CSV.**
   `bash scripts/devcap-report.sh <csv>` prints exactly these five lines, in this order:
   `CYCLES=<n>` · `CAUSE_MS_TABLE=<comma-separated ascending>` · `RETRY_MS_TABLE=<…>` ·
   `CAUSE_P95=<n>` · `RETRY_P95=<n>` — using **exactly** the editor definitions
   (`FailureTests.cs:244-266`):
   - **cause interval** = `monotonicMs` of the `causeVisible=1` row **minus** `monotonicMs` of the
     **last row before** the first `FailureReview` row of that entry;
   - **retry interval** = `monotonicMs` of the **first `Playing` row after** a `FailureReview` run
     **minus** `monotonicMs` of the **last `FailureReview` row** of that run;
   - **p95 index** = `ceil(0.95 * n) - 1` over the ascending-sorted list (n=20 → index 18);
   - runs ending in `Won` or `Halted`, and a trailing incomplete run, are **skipped**, not guessed.
   The script exits **non-zero** if either table has fewer than **20** entries, printing the counts —
   a short capture can never be mistaken for evidence (CM-C3 criteria 2/7 are p95 **over 20**).
   *Check:* `tests/unity/devcap.test.sh` diffs the script's output against
   `tests/fixtures/devcap/sample-expected.txt` for `tests/fixtures/devcap/sample-framelog.csv`
   (≥20 complete cycles, values hand-computed in the fixture header comment), and asserts non-zero
   exit + a count line for `tests/fixtures/devcap/sample-short.csv` (19 cycles).

6. **Harness discovery, and the existing gates stay untouched and green.**
   `tests/unity/devcap.test.sh` exits 0 iff criteria 1, 4 and 5's static/fixture checks all hold
   (each labelled; fail-closed on every missing scan root). `bash scripts/test.sh` prints
   `PASS tests/unity/devcap.test.sh` and a summary line matching `^test: [0-9]+/[0-9]+ passed`
   **whose two numbers the wrapper compares equal** (CM-C2a criterion 13 precedent: `\1` is not
   POSIX ERE). The PR evidence shows `git diff --name-only` containing **none** of
   `tests/save/save.test.sh`, `tests/unity/editmode.test.sh`, `tests/unity/failure.test.sh`,
   `unity/Assets/Scripts/Domain/**`, `unity/Assets/Scripts/Content/**`,
   `unity/Assets/Scripts/Presentation/**`, and `bash scripts/check.sh` + `bash scripts/test.sh`
   green with the pinned-editor half run.
   *Check:* the two commands' output pasted in the PR, plus the `git diff --name-only` listing.

### Scope boundary

**In scope — the complete file list (nothing else may appear in the diff):**

| path | role |
|---|---|
| `unity/Assets/Scripts/Bootstrap/DevCapture/FrameLogCsv.cs` | pure serialiser (criterion 1) |
| `unity/Assets/Scripts/Bootstrap/DevCapture/DevFrameCapture.cs` | observer + pause-triggered writer (criteria 2, 3) |
| `unity/Assets/Scripts/Bootstrap/GameRoot.cs` | **≤5 added lines**, all inside `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR`, adding the component in `Wire()` beside the existing `FrameLog` wiring (`GameRoot.cs:126-128`). No other line of this file changes. |
| `unity/Assets/Tests/PlayMode/Diagnostics/DevCaptureTests.cs` | criteria 1, 2, 3 |
| `scripts/devcap-report.sh` | criterion 5 |
| `tests/unity/devcap.test.sh` | criteria 1, 4, 5, 6 |
| `tests/fixtures/devcap/sample-framelog.csv`, `sample-short.csv`, `sample-expected.txt` | criterion 5 |
| `tests/fixtures/devcap-bad/Unguarded.cs`, `tests/fixtures/devcap-bad/Banned.cs` | criteria 1, 4 negative fixtures (never compiled — outside `unity/Assets/`) |
| `docs/runbooks/device-capture.md` | the human's copy-paste protocol (below) |
| `*.meta` for the new files under `unity/Assets/` | Unity-generated |

**Explicit non-goals:**
no new `FrameLog`/`FrameRecord` field (CM-C2b criterion 3 pins **exactly four**) · no edit to
`unity/Assets/Scripts/Presentation/**` of any kind — camera, banner, board, wave preview, input · no
edit to CM-C1 Domain sources, CM-C2a's importer, or CM-C2b board code · no change to CM-C3's retry
path, attribution rule, or screen-state strings · no HUD, overlay, on-screen counter or debug menu ·
no debug **gesture** of any kind (`editmode.test.sh:72-75` bans a second input surface; the pause
hook is the sanctioned trigger) · no analytics event, no `AnalyticsQueue` use, no network, no upload ·
no third `IStorageRoot` property (`IStorageRoot.cs:4` — "Exactly two properties (CM-C7 criterion
13)") · no new dependency, no JSON/Newtonsoft (CSV via `StringBuilder`; `check.sh:89`'s
`TypeNameHandling` scan spans all of `unity/Assets/Scripts`) · no new asmdef and no asmdef edit
(`CatMetro.Tests.PlayMode.asmdef` already references both `Bootstrap` and `Presentation`) ·
no `.github/**` workflow (Q-V, human) · **no claim on CM-C3 criteria 2/4/7** — they stay OPEN ·
no edit to `tests/save/save.test.sh`, `tests/unity/editmode.test.sh`, `tests/unity/failure.test.sh` ·
no writes to immutable paths (`tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
`scripts/git-hooks/`, `state/mode`, `evals/` except `evals/results/`).

**Documented device protocol** (criterion 3's contract with the human, lands in
`docs/runbooks/device-capture.md`):
1. Build with **Development Build CHECKED** (this is a *different* APK from criterion 8's release
   build — do not conflate the two artifacts).
2. Run **20 complete fail→retry cycles** on the device.
3. Press **Home** (fires `OnApplicationPause(true)` → the single write).
4. `adb logcat -d | grep DEVCAP_WRITTEN` → gives the absolute path, row count, mark count.
5. Pull, either form depending on where step 4's path points:
   - external (`/storage/emulated/0/Android/data/com.catmetro.game/files/devcap/framelog.csv`):
     `adb pull <path> framelog.csv`
   - internal (`/data/user/0/com.catmetro.game/files/devcap/framelog.csv`, Unity's default; works
     because a Development Build is debuggable):
     `adb exec-out run-as com.catmetro.game cat files/devcap/framelog.csv > framelog.csv`
6. `bash scripts/devcap-report.sh framelog.csv` → the five lines to paste into CM-C3's PR, once per
   tier (low, mid).

### Assumptions

- **A-DEVCAP-1 (carries A-C3-6).** The FrameLog is the single named clock source:
  `Time.realtimeSinceStartupAsDouble` rendered as whole ms (`FrameLog.cs:6-9,37`). The capture reads
  time **only** as `FrameRecord.MonotonicMs`/`.FrameIndex`/`.SimTick` copied verbatim from
  `FrameLog.Records` (`FrameLog.cs:31`). Criterion 1's grep is what keeps that true under future
  edits. **If the capture ever appears to need its own timestamp, that is stop condition 2.**
- **A-DEVCAP-2.** `causeVisible` is a **fifth CSV column, not a fifth `FrameRecord` field.** The
  serialisation is an annotation of the log, not a change to it — CM-C2b criterion 3's "exactly four
  fields" law and its PlayMode test (`GreyboxTests.cs:179-198`) are untouched.
- **A-DEVCAP-3 (measurement definition — HUMAN CALL WANTED).** The retry interval's start is the
  **last `FailureReview` frame record**, not the OS touch-event timestamp. This is deliberate: it is
  byte-for-byte the editor protocol (`FailureTests.cs:254-260`, which takes the last record before
  calling `HandleTapAtScreen`), so the two legs measure the same quantity and are comparable. The
  cost: on a 60 Hz device it can **under-report by up to one frame (~17 ms)** against a
  touch-event-anchored measurement, because the real tap lands somewhere inside that frame. Against a
  1000 ms budget this is ≤1.7%. **Recorded here, named in the PR, and offered to the human to accept
  or overrule** — overruling it costs a second input-side timestamp, which A-C3-6 forbids, so the
  overrule route is "amend CM-C3's criterion-7 definition", not "add a clock".
- **A-DEVCAP-4 (placement is forced, not chosen).** `Application.persistentDataPath` may be named
  **only** under `unity/Assets/Scripts/Bootstrap/**` — enforced twice, and **both greps read comments
  too** (no `sed 's|//.*||'` stripping, unlike `save.test.sh:45`):
  `tests/save/save.test.sh:29-30` (`--exclude-dir=Bootstrap`, fails "criterion 13: storage-path API
  outside Bootstrap") and `tests/unity/editmode.test.sh:53-54` (fails "criterion 9: engine path API
  outside Bootstrap"). `grep --exclude-dir=Bootstrap` prunes the whole subtree, so
  `Bootstrap/DevCapture/**` is legitimately inside the permitted region. Naming the token — even in a
  comment — in `Presentation/`, `Application/`, `Services/`, `Content/` or `Domain/` fails the suite.
  Consistent with `docs/adr/0003:105` and `docs/architecture/overview.md:210`.
- **A-DEVCAP-5 (no filename collision).** `devcap/framelog.csv` is a **new subdirectory** under
  `persistentDataPath`; the two existing residents live at the root of the same directory —
  `save.dat` (+`.bak`/`.tmp`, `SaveStore.cs:45`) and `analytics_queue.dat`
  (`AnalyticsQueue.cs:68`). No gate enumerates that directory, and neither store lists or globs it,
  so the subdirectory is invisible to CM-C7/CM-C8 behaviour. Nothing in `docs/adr/0006` counts it
  toward any storage bound (it is a dev-build-only file that cannot exist in release).
- **A-DEVCAP-6 (conditional compilation is legal here and only here).** `tests/save/save.test.sh:31-33`
  bans `#if UNITY_ANDROID` outside Bootstrap under the broader message "conditional compilation
  outside Bootstrap". `#if DEVELOPMENT_BUILD || UNITY_EDITOR` inside `Bootstrap/DevCapture/**`
  satisfies both the pattern and the message's intent. The guard must **not** be introduced anywhere
  else in the tree.
- **A-DEVCAP-7 (single flush, accepted data-loss risk).** Writing only at
  `OnApplicationPause(true)` is what guarantees no I/O hitch lands inside a measured interval.
  The cost: an app kill or crash loses the session's capture. Accepted; the mitigation is the
  runbook's step 4 (verify the `DEVCAP_WRITTEN` line before unplugging). A periodic flush was
  considered and rejected — any flush during `Playing` can land on the frame immediately preceding a
  fail transition and inflate the cause interval by its own cost, i.e. the instrument would corrupt
  the measurement it exists to take.
- **A-DEVCAP-8 (memory).** `FrameLog._records` already accumulates unbounded for the session
  (`FrameLog.cs:26,35`) — pre-existing CM-C2b behaviour this contract does not change. 20 cycles
  ≈ 10 min ≈ 36 k records ≈ ~2.5 MB live, serialised to a ~1.5 MB CSV in one `StringBuilder` pass at
  pause time. The capture's **own** state is one `HashSet<int>` of ~20 frame indices.
- **A-DEVCAP-9 (test placement).** All engine-side tests go in the **PlayMode** assembly:
  `CatMetro.Tests.EditMode.asmdef:4-12` does **not** reference `CatMetro.Presentation`, so an
  EditMode test cannot name `FrameRecord`; `CatMetro.Tests.PlayMode.asmdef:4-13` references both
  `Presentation` and `Bootstrap`. Plain `[Test]` methods run in the PlayMode assembly, so criterion
  1's pure test needs no coroutine and no asmdef edit.
- **A-DEVCAP-10 (`streamingAssetsPath` stays out).** `tests/unity/editmode.test.sh:57-63` arms a
  plain-file-read ban over **every** Bootstrap file naming `streamingAssetsPath` (the `$sfile` list
  is unquoted and multi-file). The capture must never name that token, so the new file cannot be
  swept into that check.

### Stop conditions

Defaults apply (AGENTS.md hard rules 3 and 5). Plus:

1. The cause-visible instant cannot be marked without adding a field to `FrameRecord` or editing
   `FrameLog.cs` → **STOP**. CM-C2b criterion 3 pins exactly four fields; a fifth is a CM-C2b
   amendment, not this contract's business.
2. Anything appears to require a timestamp not copied from `FrameLog.Records` — `Time.*`,
   `DateTime`, `Stopwatch`, `Environment.TickCount`, a native call → **STOP and report**. A-C3-6 is
   the whole reason this contract exists in this shape.
3. Any need to add a third `IStorageRoot` property or otherwise edit
   `unity/Assets/Scripts/Services/Save/IStorageRoot.cs` → **STOP** (CM-C7 criterion 13 pins two).
4. Any need to name `persistentDataPath`/`temporaryCachePath` outside
   `unity/Assets/Scripts/Bootstrap/**`, **including inside a comment** → **STOP**; the two gates
   (A-DEVCAP-4) read comments, and weakening either is forbidden — that would be editing a gate to
   pass, which AGENTS.md hard rule 5 rules out.
5. Any need for a debug gesture, key binding, on-screen button or second input surface → **STOP**;
   `editmode.test.sh:72-75` exists precisely to catch this and the pause hook is the sanctioned
   trigger.
6. Any need to edit Domain, the importer, `Presentation/**`, or CM-C3's retry/attribution path to
   make the capture work → **STOP**; the capture observes public state or it does not ship.
7. The editor budget harness `FailureTests.Budgets_CauseVisible1500_RetryUnder1000_P95Over20`
   regresses (`CAUSE_P95`/`RETRY_P95` above their pre-change values, currently 1 ms each per
   `state/handoffs/CM-C3.md:62-63`) → **STOP and report**. Capture tooling may not move the numbers
   it exists to measure.
8. Any analytics event, network call, upload, or monetization token appears anywhere in the diff →
   **STOP immediately** (monetization tripwire, AGENTS.md risky paths; CM-C8 owns analytics and this
   is a file, not an event).
9. It becomes tempting to mark CM-C3 criterion 2, 4 or 7 as met from anything this contract produces
   in the editor → **STOP**; CM-C3 stop condition 8 (`CM-C3-frozen-contract.md:126-128`) is
   unchanged. This contract ships the instrument only.

---
