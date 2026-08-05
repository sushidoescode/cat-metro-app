# CM-C3-DEVCAP — status log + contract anchors

Status: contract frozen 2026-08-04 (`CM-C3-DEVCAP-frozen-contract.md`), build loop starting on
`task/CM-C3-DEVCAP-frame-capture`. Sections 1–2 below are the architect's anchor/gate findings the
contract is pinned to, read at `main @ fcb3e83` (post-#24); sections 3–4 are the open questions as
they stood at freeze. Build-loop events append at the bottom.

## 1. FrameLog facts the contract is anchored to

`unity/Assets/Scripts/Presentation/Diagnostics/FrameLog.cs`

| fact | file:line |
|---|---|
| Header names the ONE clock: `UnityEngine.Time.realtimeSinceStartupAsDouble`, "rendered as whole milliseconds", "one record per rendered frame, exactly four fields" | `FrameLog.cs:6-9` |
| `FrameRecord` fields — `FrameIndex:int`, `MonotonicMs:long`, `SimTick:int`, `ScreenState:string`, all `readonly`, ctor-set | `FrameLog.cs:10-22` |
| `FrameLog : MonoBehaviour`; unbounded `List<FrameRecord> _records`; public `IReadOnlyList<FrameRecord> Records` | `FrameLog.cs:24-31` |
| Injected sources `SimTickSource` / `ScreenStateSource` (wired by the composition root) | `FrameLog.cs:28-29` |
| Recording happens in `LateUpdate`; `MonotonicMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0)`; `FrameIndex = Time.frameCount` | `FrameLog.cs:33-40` |

Wiring / CM-C2b criterion-3 seam:

| fact | file:line |
|---|---|
| `FrameLog` added in `GameRoot.Wire()`; `SimTickSource = () => Session.State.Tick`; `ScreenStateSource = () => ScreenState` | `unity/Assets/Scripts/Bootstrap/GameRoot.cs:126-128` |
| `GameRoot.ScreenState` public, default `"Playing"` | `GameRoot.cs:37` |
| Fail transition sets `ScreenState = "FailureReview"`, then frames the causal node and shows the banner **in the same `Update`** | `GameRoot.cs:190-214` |
| Retry sets `ScreenState = "Playing"` in the same frame as the tap handler | `GameRoot.cs:146-159` |
| `SEAM_LOADED` log-line precedent for a greppable device marker | `GameRoot.cs:101` |
| CM-C2b criterion 3's law test (one record/frame, four fields, non-decreasing clock) | `unity/Assets/Tests/PlayMode/Board/GreyboxTests.cs:179-198` |
| Contract text pinning "exactly `frameIndex:int, monotonicMs:long, simTick:int, screenState:string`" | `state/handoffs/CM-C2b-frozen-contract.md:37-48` |

Editor measurement protocol the device leg must reproduce
(`unity/Assets/Tests/PlayMode/Board/FailureTests.cs`):

| fact | file:line |
|---|---|
| Budget harness, 20 iterations, `[Values(true,false)] motionOff` | `FailureTests.cs:220-231` |
| cause start = `recs[^1].MonotonicMs` at the moment `FailureReview` is observed | `FailureTests.cs:244-245` |
| cause end = last record once `CauseCam.IsFramed && Banner.Visible` | `FailureTests.cs:246-252` |
| retry start = last record before `HandleTapAtScreen`; retry end = next record, asserted `ScreenState == "Playing"` | `FailureTests.cs:254-260` |
| p95 index = `ceil(0.95 * n) - 1` over the sorted list | `FailureTests.cs:264-266` |
| Output line format `CAUSE_MS_TABLE=` / `RETRY_MS_TABLE=` / `CAUSE_P95=… RETRY_P95=…` | `FailureTests.cs:267-269` |
| Predicate sources: `IsFramed => !_panning`; `PAN_DURATION_MS = 400.0`; `Visible` | `Presentation/Cameras/CauseCameraController.cs:26,15`; `Presentation/Hud/BannerView.cs:14` |
| Editor-leg values already banked: `CAUSE_P95=1ms, RETRY_P95=1ms` | `state/handoffs/CM-C3.md:62-63` |

**Consequence:** the only quantity the four existing columns cannot express is *cause-visible*
(under motion-on it is `PAN_DURATION_MS` after the state flip, and `IsFramed` is also `true` at rest,
so it cannot be inferred from `screenState` alone). Fail-start, retry-start and retry-end are all
derivable from `screenState` transitions and reproduce the editor protocol exactly. Hence: **one**
added CSV column, zero added `FrameRecord` fields.

## 2. Grep-gate findings (the landmine, resolved)

| gate | file:line | shape | effect on this contract |
|---|---|---|---|
| storage-path ban (CM-C7 crit 13) | `tests/save/save.test.sh:29-30` | `grep -rEn --include='*.cs' --exclude-dir=Bootstrap '\b(persistentDataPath\|temporaryCachePath)\b' unity/Assets/Scripts` → fail | **comments are NOT stripped** here (contrast `save.test.sh:45`, which does `sed 's\|//.*\|\|'` for the `Flush` check). Naming the token anywhere outside Bootstrap — even in prose — fails the suite. |
| same ban, second copy (CM-C2b crit 9) | `tests/unity/editmode.test.sh:53-54` | identical pattern, `--exclude-dir=Bootstrap` | duplicated enforcement; both must stay green untouched |
| **resolution** | — | `grep --exclude-dir=Bootstrap` prunes the entire subtree | `unity/Assets/Scripts/Bootstrap/DevCapture/**` is legitimately inside the permitted region — **no gate needs changing, no exemption needed** |
| conditional-compilation ban | `tests/save/save.test.sh:31-33` | pattern is only `#if UNITY_ANDROID`, `--exclude-dir=Bootstrap`; **message is broader**: "criterion 13: conditional compilation outside Bootstrap" | `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` does not match the pattern, and living inside Bootstrap satisfies the message's intent too. Do not introduce the guard anywhere else. |
| UnityEngine ban | `tests/save/save.test.sh:27-28` | `--exclude-dir=Bootstrap --exclude-dir=Presentation` | Bootstrap may name `UnityEngine` — fine |
| second-input-surface ban | `tests/unity/editmode.test.sh:74-75` | `EventSystems\|IPointerDownHandler\|IDragHandler\|IBeginDragHandler\|Touchscreen\|EnhancedTouch\|OnMouse` over **Presentation AND Bootstrap** | **kills the "debug gesture" trigger option outright.** Drove the design to `OnApplicationPause(true)`. (Note: `Keyboard.current`/`InputSystem` in Bootstrap would technically evade both this and `:72-73`'s Presentation-only file count — that would be routing around the gate's intent and is named as stop condition 5.) |
| one-input-consumer count | `tests/unity/editmode.test.sh:72-73` | exactly 1 Presentation file naming `UnityEngine.InputSystem` | untouched — capture adds nothing to Presentation |
| streaming plain-file-read ban | `tests/unity/editmode.test.sh:57-63` | arms only if some Bootstrap file names `streamingAssetsPath`; `$sfile` is unquoted → multi-file | capture must never name `streamingAssetsPath` (A-DEVCAP-10) |
| `Simulation.Step` reach ban | `tests/unity/editmode.test.sh:67-68` | over Presentation+Bootstrap | trivially satisfied |
| solver-reference guard | `scripts/check.sh:118-125` | `CatMetro.Domain.Solver` banned in Application/Presentation/Bootstrap | trivially satisfied |
| `TypeNameHandling` single-site | `scripts/check.sh:89` (`tnh_scan unity/Assets/Scripts`) | whole Scripts tree | reason the artifact is **CSV, not JSON** — no Newtonsoft, no new dependency |
| gesture-handler count test | `unity/Assets/Tests/PlayMode/Board/GreyboxTests.cs:64-66` | counts `TapInput` components only | adding a Bootstrap MonoBehaviour is **safe** (verified: it is not a generic MonoBehaviour census) |
| camera census | `unity/Assets/Tests/PlayMode/Board/FailureTests.cs:215-217` | counts `Camera` only | safe |
| `IStorageRoot` shape pin | `unity/Assets/Scripts/Services/Save/IStorageRoot.cs:4,7-11` | comment: "Exactly two properties (CM-C7 criterion 13)" | **do not** add a third property for the capture directory — hence the injectable `OutputDirectory` field on the component instead |
| existing persistentDataPath residents | `unity/Assets/Scripts/Application/Save/SaveStore.cs:45` (`save.dat`), `unity/Assets/Scripts/Application/Analytics/AnalyticsQueue.cs:68` (`analytics_queue.dat`) | both at `SaveDirectory` root, composed via `IStorageRoot` | `devcap/framelog.csv` in a **new subdirectory** collides with neither; no gate enumerates the directory |
| the one legal `persistentDataPath` site today | `unity/Assets/Scripts/Bootstrap/EngineStorageRoot.cs:10` | `IStorageRoot.SaveDirectory` | the capture composes its own path in Bootstrap rather than widening this seam |
| negative-fixture style precedents | `tests/fixtures/retry-bad/Banned.cs:1-8`, `tests/save/save.test.sh:38-40`, `tests/unity/failure.test.sh:12-14,28-29` | "prove the pattern fires" | criterion 4's `tests/fixtures/devcap-bad/` follows it verbatim |
| `DEVELOPMENT_BUILD` / `UNITY_EDITOR` token count in repo today | **zero matches** anywhere | — | the guard-token gate starts from a clean baseline; any hit outside `DevCapture/**` is by definition new |
| asmdef reality | `unity/Assets/Tests/EditMode/CatMetro.Tests.EditMode.asmdef:4-12` (no `CatMetro.Presentation`) vs `…/PlayMode/CatMetro.Tests.PlayMode.asmdef:4-13` (has it) | — | **all** new tests must be PlayMode-assembly; an EditMode test cannot name `FrameRecord`. No asmdef edit needed. |
| wrapper discovery | `scripts/test.sh:10-25`; `scripts/check.sh:21-27` (bash -n over `scripts` + `tests`) | — | `tests/unity/devcap.test.sh` + `scripts/devcap-report.sh` are auto-covered |

## 3. Open questions needing a human call

1. **[Highest] A-DEVCAP-3 — retry-interval start definition.** The device leg measures *last
   `FailureReview` frame → first `Playing` frame*, not *OS touch-event → first `Playing` frame*,
   because that is byte-for-byte the editor protocol (`FailureTests.cs:254-260`) and because an
   input-side timestamp would be a second clock (A-C3-6 forbids it). Bias: under-reports by up to
   one frame (~17 ms at 60 Hz) vs a touch-anchored measurement; ≤1.7% of the 1000 ms budget.
   **Human must accept this as the definition of CM-C3 criterion 7's device leg, or amend criterion
   7.** There is no third option that keeps one clock.
2. **Device tiers.** `docs/prd/ux-flows.md:287` requires **low and mid tier**; the only device on
   hand is a Pixel 9 Pro (`SESSION-HANDOFF-device-testing.md:41-42` already flags this). This
   contract does not solve it — the human either borrows/emulates or records the deviation. Worth
   deciding *before* the capture session so it isn't run twice.
3. **Is the `strings <release-apk>` check a merge gate or device-session evidence?** Drafted as
   device-session evidence (criterion 4 leg B) so this contract can merge without a device. If the
   human wants zero-footprint proven *at merge*, criterion 4 needs a third leg and the PR blocks on
   an IL2CPP release build (10–20 min, human-run — agents do not run builds).
4. **20 cycles in one sitting, single flush.** A-DEVCAP-7 accepts losing the capture if the app is
   killed before Home is pressed. If the human would rather not risk a 10-minute session, the
   alternative is a periodic flush — which can inflate the very cause interval it measures (see
   A-DEVCAP-7's rejection note). Recommend keeping single-flush; flagging because it is the human's
   time at stake, not the agent's.
5. **Contract id / filing.** Working id `CM-C3-DEVCAP` is not in `state/backlog.md`. Human call:
   register it as a backlog row (e.g. `CM-C3.1`) before freezing, or accept a handoff-only contract.
   Prior contracts were all frozen *from* backlog rows.
6. **Runbook location.** `docs/runbooks/device-capture.md` is new; `docs/runbooks/` exists per
   AGENTS.md's layout. Confirm this is the right home rather than appending to
   `state/handoffs/SESSION-HANDOFF-device-testing.md` (which is a session artifact, not a runbook).

## 4. Things deliberately NOT in the contract (and why)

- A second `FrameLog` field / a `FrameLogV2` — CM-C2b criterion 3 pins four fields; stop condition 1.
- An in-app p95 computation or on-screen readout — the reducer is an offline shell script, which is
  testable against a golden fixture and cannot perturb the device run.
- A `.github/**` CI job for the device leg — Q-V, human-owned `.github/**`.
- Any use of `IStorageRoot` — pinned at two properties; the component takes an injectable
  `OutputDirectory` instead, which is also what makes criterion 3 testable without writing into the
  editor's real `persistentDataPath`.

- 2026-08-05 red milestone: PlayMode tests (criteria 1/2/3 + wiring + default-dir) pin the API; guarded stubs keep the suite compiling; wrapper red at criterion-5 fail-closed (reducer unwritten); criterion-5 fixture derivation lives in the CSV header comments per contract. Scanner fixture proof fires (>=2 violations). Fixture generation asserted expected p95s independently (154/76).

- 2026-08-05 green milestone: reducer via local-executor lane (1 turn, check-verified); C# capture in-session ([DefaultExecutionOrder(1000)] pins the mark after the log record; UnityEngine.Application qualified — bare Application resolves to CatMetro.Application in this namespace). Wrapper OK (1-static,4,5); filtered PlayMode 5/5. Wrapper diff switched to temp-file (process substitution breaks under sandboxed shells; no gate weakened). Full-suite run next.

- 2026-08-05 review round 1 (PR #26): NOT-mergeable verdict — B1 reducer counted Won/Halted-interrupted runs (18-real-as-20 shape proven), B2 fixture distractors inert, M1 exec-order unpinned, M2 cause read-point bias undisclosed, M3 strings-scan evidence predates the code, L1-L5. ALL fixed: strict adjacent-Playing rule + dup-mark hard error in the reducer; fixtures regenerated with FR-adjacent interrupted runs (live skip proof); scanner rejects else-arms + inverted guards (fixture shapes added); DefaultExecutionOrder pinned by test (6/6); read-point bias disclosed in runbook; PR criterion-4 row corrected to DEFERRED (re-scan on post-merge release APK at the DEVFIX re-measure build). Stop-condition-7 attestation: reviewer-measured RETRY_P95=1 both legs, CAUSE_P95=0 (motion-off) / 33 (motion-on, PAN_DURATION/timeScale intrinsic) — no regression; baseline to re-record per parameterisation.
