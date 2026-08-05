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
`docs/adr/0007-*` (Input System, one handler; UGUI arrives later — P-2 TextMesh posture) ·
`state/handoffs/SESSION-HANDOFF-ux.md` (one-input-surface gate, resolution (a)) ·
`tests/unity/editmode.test.sh:66-75` (criterion-2 static legs — must pass UNMODIFIED).

### Acceptance criteria (8)

1. **Registry routes before discs, deterministically.** `ChromeRegions` (pure C#, Presentation):
   `Register(id, Func<Rect> screenRect, Action onTap, int priority)` / `Unregister(id)`.
   `TapInput.HandleTapAtScreen` consults registered regions before the board-disc scan; a tap
   inside a region fires exactly that region's action and never falls through to a disc beneath;
   overlapping regions resolve by highest priority, ties by registration order — deterministic.
   *Check:* red-first EditMode tests driving `HandleTapAtScreen` with stub regions (hit, miss,
   overlap, priority-tie, unregister).
2. **Board-input gate.** `TapInput.BoardInputActive` (`Func<bool>`, default null = active): when
   bound and false, the board-disc scan is skipped entirely — no `EnqueueToggle`, no
   `RefreshSwitches`, return -1 — while chrome regions and the existing retry band still resolve.
   (Fixes the verified desync: today in `Won`/`Halted`/`FailureReview`-above-band, taps flip lever
   visuals against a stopped sim, `TapInput.cs:52-71`. Binding arrives in CM-UX-07 —
   `() => ScreenState == "Playing"` — so this slice changes no shipped behavior.)
   *Check:* red-first EditMode tests (gate false → no toggle, no visual refresh call; gate
   null/true → byte-identical routing to today).
3. **Retry-band behavior is pinned, then preserved.** With zero regions registered and the gate
   unbound, `HandleTapAtScreen`'s observable behavior (returns −2/−1/index; retry consumption;
   nearest-center, lowest-index ties) is identical to merged behavior. *Check:* a characterization
   test **labeled as a pin (green on arrival, by design — P-7)** written BEFORE the edit, kept
   green through it. The retry band keeps precedence over the disc scan and stays full-band during
   FailureReview even when regions exist elsewhere on screen. *Check:* red-first EditMode test
   with a decoy region outside the band.
4. **Band/48dp math with injected metrics.** `HudBands` (pure C#): thumb-band rect (bottom 25%),
   status-band rect (top 15%), dp↔px with **injected** dpi (fallback 160 ⇒ pxPerDp 1, matching
   `TapInput.cs:53`), and `MeetsMinTarget(rect, 48dp)`. Deterministic on the 360×640dp reference
   with dpi injected — no `Screen.*` reads inside the pure class. *Check:* red-first EditMode
   tests including the 360×640 reference-frame table.
5. **EditMode assembly can see Presentation.** `unity/Assets/Tests/EditMode/*.asmdef` gains the
   `CatMetro.Presentation` reference; a new test file constructs a Presentation type and runs.
   No reference cycle; no `UnityEngine.UI`/TMP additions anywhere (P-2). *Check:* the new EditMode
   tests themselves compile+run in the headless suite; both hosts' existing suites stay green.
6. **Gate legs pass unmodified.** `tests/unity/editmode.test.sh` criterion-2 statics: exactly one
   Presentation file references `UnityEngine.InputSystem`; zero banned gesture tokens across
   Presentation+Bootstrap **including comments/prose in the new files** (sweep before commit).
   *Check:* the harness leg itself, on a committed tree.
7. **Zero behavior drift.** Full existing suites green with **zero modifications to existing
   tests**: 334 EditMode + 20 PlayMode (Unity) and the dotnet host suite. *Check:* headless runs;
   numbers recorded in the PR per-criterion evidence.
8. **No new strings, no new csv rows, no rendered output.** The slice ships no view; a tree walk
   of a launched session shows no new renderable object versus base. *Check:* one PlayMode
   assertion on `LaunchWith(fixture)` comparing the child-object inventory against base
   expectations.

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
- **A-UX1-2** TextMesh-greybox posture (decompose P-2) — recorded here so the reviewer sees the
  ADR-0007 UGUI mandate is deferred by posture, not ignored.
- **A-UX1-3** Registry priority is an int with explicit values per registration; later slices may
  not rely on registration order across components (tie-break exists for determinism, not as API).
- **A-UX1-4** The pin in criterion 3 is evidence of preservation, not TDD theater — labeled per
  P-7.

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
