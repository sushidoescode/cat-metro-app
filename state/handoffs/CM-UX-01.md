# CM-UX-01 — build-loop handoff note (session 2026-08-05, UX lane)

**Contract:** `state/handoffs/CM-UX-01-frozen-contract.md` (frozen in #27). Copied VERBATIM below
per the forge-build freeze rule — review verifies against this copy; any drift from the file is
visible tampering.

## Restatement

TapInput gains (a) a chrome-region registry (`ChromeRegions`, pure C#) consulted AFTER the legacy
retry band and BEFORE the board-disc scan, deterministic (priority, ties by registration order);
(b) a `BoardInputActive` gate delegate (null = active) that, when bound false, skips the disc scan
entirely (no command append, no visual refresh) while band + regions still resolve. `HudBands`
(pure C#) computes the ux-flows §1.1 band law on an INJECTED safe area + dpi. The EditMode test
asmdef gains the `CatMetro.Presentation` reference. Behavior-neutral until consumed: no regions
registered, gate unbound, no views, no strings. Zero edits outside Presentation + Assets/Tests.

## Assumptions (recorded per forge-build step 2 — none load-bearing/unconfirmed)

- Contract A-UX1-1..5 stand as frozen; §6 Q-1..Q-6 remain open human questions, none blocking.
- **Test-surface choice inside the contract's live-wiring rule:** routing tests (criteria 1-3)
  run PLAYMODE over `GameRoot.LaunchWith(fixture)` — `BoardView.Build` touches
  `renderer.material`, which logs material-leak errors under EditMode and would fail tests on
  log noise; the contract's live-wiring rule explicitly permits either surface. Pure classes
  (`ChromeRegions`, `HudBands`) and the criterion-8 reflection pin run EditMode.
- New chrome-consumption return code from `HandleTapAtScreen` is −3 (distinct from −2 retry /
  −1 miss / index); the criterion-3 pin covers the EXISTING codes only, which are unchanged.
- Region resolution precedes the session null-guard (a later Home screen has chrome but no
  session); the gate applies to the disc scan only.
- Duplicate region id on Register throws ArgumentException (determinism; A-UX1-3 explicitness).

## Evidence (criterion → check), filled at green

(pending — see PR table)

---

## Frozen contract (verbatim copy)

# CONTRACT CM-UX-01 — Input foundation: chrome-region registry, board-input gate, band math, test-assembly fix

**Tranche:** UX tranche-1 slice 1 (`docs/ux/ux-layer-decompose.md`).
**DEPENDS-ON:** nothing in flight — NOW-class; base origin/main ≥ 64cb0d8.
**Ownership note:** zero edits to `Bootstrap/**`, `GameRoot.cs`, `Content/**`, `Domain/**`,
`scripts/`, or `tests/` harness wrappers (parallel device session owns them / hooks protect them).
This is the tranche's ONLY TapInput edit — later slices consume, never modify.

### Goal

Chrome becomes possible without a second input surface: `TapInput` gains a chrome-region registry
consulted before board discs and a bindable board-input gate, `HudBands` supplies the band/48dp
layout law every later slice's criteria call, and the EditMode test assembly gains the
`CatMetro.Presentation` reference without which no Presentation component test compiles.
**Behavior-neutral until consumed:** nothing registers a region and the gate delegate stays null
in this slice; the shipped game is pixel- and hash-identical.

### Spec reference

`docs/prd/PRD.md` CM-R07.1/.4 (one gesture handler; interactive chrome bottom 25%; ≥48dp) ·
`docs/prd/ux-flows.md` §1.1 (band law), S-03 (retry stays hit-testable from frame 1) ·
`docs/adr/0007-*` (Input System, one handler; chrome rendering tech pending decompose §6 Q-6) ·
`state/handoffs/SESSION-HANDOFF-ux.md` (one-input-surface gate, resolution (a)) ·
`tests/unity/editmode.test.sh:69-75` (criterion-2 static legs — must pass UNMODIFIED).

### Acceptance criteria (8)

**Live-wiring rule for every routing test (review R1-F1):** any test asserting
`HandleTapAtScreen` routing MUST drive a TapInput wired to a real `GameSession` + `BoardView` +
`Camera` (EditMode-constructed from a fixture level, or PlayMode over `GameRoot.LaunchWith`) —
never a bare component, whose `TapInput.cs:52` null-guard returns −1 for every input and makes
any assertion on −1/no-effect pass vacuously. Every negative assertion ("no toggle happens")
carries a **positive control** in the same fixture (the identical tap WITH the feature
disengaged does toggle), so the test is demonstrably able to fail.

1. **Resolution order is law: retry band → regions → discs.** `ChromeRegions` (pure C#,
   Presentation): `Register(id, Func<Rect> screenRect, Action onTap, int priority)` /
   `Unregister(id)`. The legacy retry band keeps FIRST claim exactly as merged (`TapInput.cs:46-51`
   — criterion 3's pin freezes it); registered regions are consulted next — a tap inside a region
   fires exactly that region's action and never falls through to a disc beneath; overlapping
   regions resolve by highest priority, ties by registration order — deterministic. **Consequence
   for CM-UX-02, stated here so its implementer inherits it:** a chrome element placed INSIDE the
   retry band during FailureReview is render-only — the band's own `RetryTapped` is its action;
   registering a competing region there is dead code by this law. *Check:* red-first tests under
   the live-wiring rule (hit, miss, overlap, priority-tie, unregister) **plus the in-band case:
   a region registered inside the retry band during FailureReview does NOT fire — the band wins.**
2. **Board-input gate.** `TapInput.BoardInputActive` (`Func<bool>`, default null = active): when
   bound and false, the board-disc scan is skipped entirely — no command appended to the session's
   log, no `RefreshSwitches` — while chrome regions and the existing retry band still resolve.
   (Fixes the verified desync: today in `Won`/`Halted`/`FailureReview`-above-band, taps flip lever
   visuals against a stopped sim, `TapInput.cs:52-71`. Binding arrives in CM-UX-07 —
   `() => ScreenState == "Playing"` — so this slice changes no shipped behavior.)
   *Check:* red-first tests under the live-wiring rule: gate false → session command count
   unchanged AND the tapped switch's committed-route visual unchanged; positive control: same tap,
   gate null and gate true → toggles, byte-identical to merged routing.
3. **Retry-band behavior is pinned, then preserved.** With zero regions registered and the gate
   unbound, `HandleTapAtScreen`'s observable behavior (returns −2/−1/index; retry consumption;
   nearest-center, lowest-index ties) is identical to merged behavior. *Check:* a characterization
   test **labeled as a pin (green on arrival, by design — P-7)** written BEFORE the edit against
   live wiring, kept green through it. The retry band stays full-band during FailureReview even
   when regions exist elsewhere on screen. *Check:* red-first test with a decoy region outside
   the band (the in-band case is criterion 1's).
4. **Band/48dp math over the SAFE AREA, all inputs injected.** `HudBands` (pure C#) takes
   `(Rect safeArea, float dpi)` as explicit inputs — the band law is defined on the safe area per
   `ux-flows.md:32` ("% of the safe area", after gesture-nav/cutout/IME insets), NOT the raw
   screen: thumb-band rect (bottom 25% of safe area), status-band rect (top 15%), dp↔px (dpi
   fallback 160 ⇒ pxPerDp 1, matching `TapInput.cs:53`), `MeetsMinTarget(rect, 48dp)`. No
   `Screen.*` reads inside the pure class; the live `Screen.safeArea`/`Screen.dpi` binding lands
   with the first consuming view (CM-UX-02) — assumption A-UX1-5. *Check:* red-first EditMode
   tests: the 360×640dp zero-inset reference table PLUS one inset case (e.g. 48px bottom
   gesture-nav inset shifts the thumb band up accordingly).
5. **EditMode assembly can see Presentation.** `unity/Assets/Tests/EditMode/*.asmdef` gains the
   `CatMetro.Presentation` reference; a new test file constructs a Presentation type and runs.
   No reference cycle; no `UnityEngine.UI`/TMP additions anywhere (decompose §6 Q-6 pending —
   this slice ships no view either way). **Placement fence (review R1-F9):** all new test files
   live OUTSIDE `unity/Assets/Tests/EditMode/Pure/**` — that subtree is linked into the
   UnityEngine-free dotnet host (`CatMetro.Tests.csproj:17`) and `scripts/check.sh:62` bans
   `UnityEngine` there. *Check:* the new EditMode tests compile+run in the headless suite; both
   hosts' existing suites stay green.
6. **Gate legs pass unmodified.** `tests/unity/editmode.test.sh` criterion-2 statics: exactly one
   Presentation file references `UnityEngine.InputSystem`; zero banned gesture tokens across
   Presentation+Bootstrap **including comments/prose in the new files** (sweep before commit).
   *Check:* the harness leg itself, on a committed tree.
7. **Zero behavior drift.** Full existing suites green with **zero modifications to existing
   tests**: the rebase base's recorded counts (334 EditMode + 20 PlayMode at 64cb0d8 — re-derive
   from the actual base if the device session merges first, review R1-F10; the delta over base
   must be this slice's new tests only) and the dotnet host suite. *Check:* headless runs; base
   counts + deltas recorded in the PR per-criterion evidence.
8. **The slice ships no view — proven structurally (labeled a pin, P-7).** `ChromeRegions` and
   `HudBands` are pure C# — neither derives from `UnityEngine.Object`; TapInput remains the
   slice's only touched MonoBehaviour; no code added by this slice constructs a `GameObject` or
   calls `AddComponent`. *Check:* one EditMode reflection assert on the two new types + review of
   the diff surface (this replaces R1's brittle tree-walk inventory: `BoardView.cs:175-184`
   creates train objects lazily inside `UpdateFrom`, so scene inventories are frame-count-
   dependent and every later slice would have to edit such a test — an invitation to weaken it).

### Scope boundary

**In scope:** `unity/Assets/Scripts/Presentation/Input/TapInput.cs` ·
`unity/Assets/Scripts/Presentation/Input/ChromeRegions.cs` (new) ·
`unity/Assets/Scripts/Presentation/Hud/HudBands.cs` (new) · EditMode tests asmdef + new test
files under `unity/Assets/Tests/EditMode/**` · one new PlayMode test file under
`unity/Assets/Tests/PlayMode/**`.

**Explicit non-goals:** no views/chrome rendering (CM-UX-02+); no GameRoot/Bootstrap edit and no
delegate binding (CM-UX-07); no csv rows; no TMP/UGUI imports or references; no gate-wrapper
edits of any kind; no screen-state knowledge inside TapInput (delegates only); no save/settings
I/O; no monetization-adjacent anything (attempt-1 invariant, `PRD.md:208`).

### Assumptions

- **A-UX1-1** The delegate seam (`RetryRegionActive` precedent, wired by the composition root) is
  the sanctioned pattern for `BoardInputActive`; Presentation never reads `ScreenState` directly.
- **A-UX1-2** Chrome rendering technology is the human's open question (decompose §6 Q-6:
  continue the merged TextMesh-greybox precedent, `BannerView.cs:5-7`, vs import TMP now per
  ADR-0007). This slice is unaffected either way — it ships no view and references no UI package.
- **A-UX1-3** Registry priority is an int with explicit values per registration; later slices may
  not rely on registration order across components (tie-break exists for determinism, not as API).
- **A-UX1-4** The pins in criteria 3 and 8 are evidence of preservation, not TDD theater —
  labeled per P-7.
- **A-UX1-5** `HudBands` is pure math over an injected safe area; the live `Screen.safeArea` +
  `Screen.dpi` binding is CM-UX-02's deliverable. **Accepted named debt (review R1-F15):** the
  pinned retry band consumes taps on the RAW bottom 25% (`TapInput.cs:47`) while `HudBands`
  defines the thumb band on the SAFE AREA — on inset devices the two rects diverge, and a tap in
  the divergence zone retries without hitting the rendered chip. CM-UX-02 must document the zone.
  **Reconciliation is its OWN contract (or an explicitly enumerated, separately-reviewed CM-UX-07
  line item) — never a composition-only edit** (review R2-N1): CM-UX-07's thinness criterion and
  this contract's only-TapInput-edit law both forbid a casual `TapInput.cs:47` change; touching
  the band's consumption rect must be reviewed against criterion 3's pin deliberately.

### Stop conditions

Defaults (AGENTS.md) plus:
1. Registry routing cannot preserve byte-identical board-disc behavior (criterion 3 pin breaks) →
   stop; never adjust the pin to the new behavior.
2. Criterion-2 harness legs fail for any reason other than a token in this slice's own new prose
   → stop and report; never edit the wrapper (a wrapper edit is the gate-evolution path and its
   merge is human-gated).
3. The asmdef reference creates a cycle or breaks either host's suite → stop; report the
   dependency shape rather than restructuring assemblies (ADR-0003/0005 territory).
4. Anything requires a Bootstrap/GameRoot/Content/Domain edit → stop; it belongs to CM-UX-07 or
   is out of the lane's ownership entirely.
5. The device session merges a TapInput change mid-slice → stop, rebase, re-verify criterion 3's
   pin against the new base before continuing.
