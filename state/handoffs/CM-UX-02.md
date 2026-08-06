# CM-UX-02 — build-loop handoff note (session 2026-08-05/06, UX lane)

**Contract:** `state/handoffs/CM-UX-02-frozen-contract.md` (frozen in #31 after a 2-round,
23-finding review). Copied VERBATIM below per the forge-build freeze rule.

## Gate & sequencing record

Red started ONLY after `git log` showed CM-C2b-DEVFIX (#30, 20126ac) on main (stop condition 1;
the #29 ratified ordering). Post-DEVFIX baseline verified first: EditMode 353/353, PlayMode
38/38 (DEVFIX +5 — the re-derived criterion-9 base), CM-UX-01 pins green on the new base
(stop condition 6 discharged).

## TDD evidence

- **Red (02c0e2e):** EditMode 372 total / **15** red at the committed tree (the 16th — the
  vocab guard catching 'lose' inside 'closes' in this slice's own prose — was red in the
  pre-reword run `ux02-red-em.xml` and fixed before commit; R1-L10 correction); PlayMode 46 /
  6 red — 5 for the criterion's own reason, and the real-halt leg red on the halt's loud log
  rather than the missing-veil assert (R1-L10: recorded honestly, not "all right-reason").
- **Green (ace6c09):** EditMode **372/372** · PlayMode **46 (45 live + the env-gated capture
  rig)**. Post-R1 fixes: **EM 375/375 · PM 47/47** (adds the UiChrome pairing test, the P9P
  inert pin, a fourth realistic-dpi floor row, and the H1 painted-rect joint test). The
  re-captured CTA frame is full-width in a Screen-matched frame; the batch host's landscape
  aspect (640x480, measured) makes the board overlap the band compositionally — an artifact of the host
  aspect, not the layout law; the device-aspect eyeball stays with the TG sitting.
- **Visual verification (#33 standing rule, RE-EVIDENCED per R1-M1):** the first CTA capture
  was NON-PROBATIVE — the 720x1280 RT disagreed with batchmode Screen (640x480, measured in R2), so the
  ScreenSpaceCamera canvas rendered at RT size while the view laid out from Screen: the chip
  measured 88.9% width / 9.7% height in-frame. Root cause fixed (Screen-matched RT), frames
  re-captured and re-eyeballed. The veil frame was probative in both rounds (relative anchors);
  neither frame shows magenta — genuine proof the UiChrome.mat binding + TMP SDF path render
  on the URP baseline. Queued for the human's TG-5 sitting.
- **TMP import footprint (criterion-1 honesty number, R1 measurement):** 4.1 MB on disk total,
  **2.3 MB under `Assets/TextMesh Pro/Resources/**`** (force-included in builds); the AAB
  impact measurement stays with the device lane (stop condition 7, >5 MB threshold).

## Criterion → check map

1 TMP/UGUI render-only → PlayMode whitelist walk + decoy Button control; harness statics
  unmodified · 2 transitions → Playing→FailureReview→Playing + Won rows, pre-flip controls ·
3 CTA → LOCKED copy via key; safe-area thumb band live-bound at the view; EditMode
  tappable-rect table (dpi INJECTED PER ROW: zero-inset + 48px-nav + nav+cutout at 160 dpi +
  a Pixel-9-Pro-class row at 486 dpi) **plus the R1-H1 joint test — the live PaintedRectPx
  equals ThumbBand(safeArea) and the live tappable rect clears the floor**; render-only
  (Regions.Count with decoy 0→+1→0) · 4 REAL halt via GameRoot.Launch()+timescale (the
  GreyboxTests recipe) with the exact CM-UX-07 delegate shape; vocab guard over csv value +
  slice sources (exemptions argued in the guard header) · 5 motion-off structural guarantee
  ONLY (zero animation components over the whole ChromeRoot + decoy Animator; **the frozen
  contract's 5(b) parity assert was REMOVED in the R1 fixes as provably tautological — nothing
  in this chrome reads MotionOff and there is no motion to toggle; a named-check removal under
  hard rule 5, carried as a human line on the PR**); TMP glyph-geometry proxy · 6 csv
  byte-pins incl. U+2014 round-trip; P-4 literal guard ·
7 zero registrations (decoy-proven counter) · 8 both divergence zones pinned numerically ·
9 zero existing-test edits; suites green; Packages untouched (verified each commit).

## G-4 worked example (criterion 8, numbers)

Pixel-9-Pro-class row (raw H=2856 px, bottom inset b=146, top inset t=146; scaled table row
uses 640/48/32): inert-chip overhang = max(0, 0.75·146 − 0.25·146) = **73 px** of painted chip
above the raw band top; over-consuming zone = **146 px** of raw band below the safe area that
still retries. Both pinned by `ChromeGeometryTests`; reconciliation stays its own reviewed
line (CM-UX-07, per #27 R2-N1).

## Import shim provenance (untracked, deleted after its one run)

Per the CM-C2b criterion-8 precedent — full text:

```csharp
// UNTRACKED import-tooling shim for CM-UX-02's TMP essentials import (human-approved Q-6,
// 2026-08-05). Editor-only; never enters the player; deleted after the one import run.
using UnityEditor;
using UnityEngine;
public static class CatMetroTmpImport
{
    private static int _updates;
    public static void ImportEssentials()
    {
        const string pkg = "/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/"
            + "Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/"
            + "TMP Essential Resources.unitypackage";
        AssetDatabase.importPackageCompleted += name => { AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool ok = AssetDatabase.IsValidFolder("Assets/TextMesh Pro");
            Debug.Log("TMP_IMPORT_DONE valid=" + ok); EditorApplication.Exit(ok ? 0 : 1); };
        AssetDatabase.importPackageFailed += (name, err) => {
            Debug.LogError("TMP_IMPORT_FAILED " + err); EditorApplication.Exit(1); };
        EditorApplication.update += () => { if (++_updates > 3000) {
            Debug.LogError("TMP_IMPORT_TIMEOUT");
            EditorApplication.Exit(AssetDatabase.IsValidFolder("Assets/TextMesh Pro") ? 0 : 1); } };
        AssetDatabase.ImportPackage(pkg, false);
    }
}
```

Run log: `TMP_IMPORT_DONE valid=True`, exit 0. Incident during cleanup: the shim's folder
removal deleted the TRACKED `Assets/Editor.meta`; restored via `git checkout` with
`SceneBootstrapper.cs` intact before any commit (working-tree only, zero history impact).

## Scope notes for the reviewer

- `Assets/Resources/Materials/UiChrome.mat` (+ `UiChromeMaterial.cs` provider) was not in the
  frozen in-scope list by name: it is the F-DEV-2 lesson applied to chrome (a runtime canvas
  must not depend on a strippable engine default; the mat is hand-authored YAML binding builtin
  UI/Default fileID 10770, force-included via Resources — the GreyboxMaterial pattern). Judged
  in-scope as Presentation-rendering infrastructure; flagged here, not hidden.
- The vocabulary guard carries exemption (c): itself + UiCsvDisciplineTests are excluded from
  their own scan with the reason recorded in the guard header (they must name tokens / quote
  merged LOCKED keys).
- Forward obligations restated: CM-UX-07 binds `BoardInputActive` in the SAME PR that attaches
  the controller; first registrar is CM-UX-06 (R1-F3 law); two-text-stacks interim recorded
  for the TG sitting; **R2-NEW-4: `PaintedRectPx` is self-reported by the view — when
  CM-UX-07 takes the band-reconciliation line, add a RectTransform world-corners read-back
  assert so a sizeDelta drift cannot hide behind the reported rect** (bounded today by the
  measured capture).

---

## Frozen contract (verbatim copy)

# CONTRACT CM-UX-02 — Fail/halt visibility: chrome controller, rendered Try-again CTA, halt veil, TMP/UGUI foundation

**Tranche:** UX tranche-1 slice 2 (`docs/ux/ux-layer-decompose.md` §2). **Revised per review
round 1 (20 findings, 4 blocking — R1-F1..F20 below refer to that round).**
**DEPENDS-ON (both hard):** (1) CM-UX-01 merged (#28); (2) **CM-C2b-DEVFIX merged (#30 in
flight)** — #29's ratification sequences DEVFIX's Presentation lines before UX-lane code, and
TMP's URP shadergraph variants import cleaner on the restored-URP tree. **Red phase does not
start until `git log` shows DEVFIX on main** (mechanical check). The freeze itself is docs-only.
**Human decisions incorporated (in-session 2026-08-05, verbatim record in
`SESSION-HANDOFF-ux.md`):** Q-6 = **import TMP + UGUI now** (ADR-0007 honored directly);
Q-1 = land the DRAFT halt copy now, voice-pass at TG-5; Q-2 = **answered YES** — the halt
restart escape lands as CM-UX-07's wiring line (no longer an open gate; this slice still ships
no affordance); Q-3 does not touch this slice.

### Goal

The two measured fail-visibility gaps close at the component level: FailureReview renders a
LOCKED **Try again** CTA in the thumb band (CM-C3 review-N2 debt), and Halted renders a visible,
semantics-neutral veil instead of nothing (F-DEV-4) — driven by a screen-state-bound chrome
controller, on ADR-0007's UGUI+TMP stack, with zero new input surfaces and zero registered
regions. Composition-root attachment is CM-UX-07's; behaviors are proven by direct construction
over `GameRoot.Launch()`/`GameRoot.LaunchWith(fixture)` as each check names (R1-F20).

### Live-wiring & anti-vacuity rule (binds every criterion — R1-F2, inherited from CM-UX-01)

Any test asserting on live objects drives REAL wiring (`GameRoot.Launch`/`LaunchWith`), never a
bare component. **Every negative or absence assertion carries a positive control in the same
fixture that demonstrates the assertion is able to fail** (a decoy registration, a decoy banned
component, a pre-transition state check). A criterion whose check cannot fail is a defect.

### Spec reference

`docs/prd/ux-flows.md` S-03 (layout intent; `Try again` **[LOCKED]**; *hit-testable from frame
1* — satisfied by TapInput's band, independent of rendering), §1.1 (band law, 48dp) ·
`docs/prd/PRD.md` CM-R16.2, CM-R07.4 (interactive rect defined on the SAFE AREA), CM-R03.2 ·
`docs/adr/0007-*` (UGUI+TMP per Q-6) · `docs/ux/ux-layer-decompose.md` P-1..P-7, G-3/G-4, §6 ·
CM-UX-01 laws: resolution order (band → regions → discs; in-band FailureReview regions are dead
code), `HudBands` safe-area math, R1-F3 (registrars `Unregister` in `OnDestroy`), R2-N2 (first
`MeetsMinTargetPx` call site gets review attention) · Q-B/NEW-Q4 OPEN — nothing here decides
halt semantics · the real-halt recipe: `unity/Assets/Tests/PlayMode/Board/GreyboxTests.cs:151-170`.

### Acceptance criteria (9)

1. **TMP/UGUI foundation, render-only by construction.** TMP Essential Resources imported
   **as shipped inside the built-in `com.unity.ugui` of the pinned editor** (R1-F14: ~45 assets
   under `Assets/TextMesh Pro/**` — SDF shaders + cginc/hlsl includes, URP AND HDRP
   shadergraph variants, LiberationSans font + SDF asset, default style sheet, EmojiOne sprite
   atlas, line-breaking tables; **kept as-imported, no pruning**. Build-footprint honesty
   (R2-N3a): the shadergraph variants sit OUTSIDE `Resources/` and stay out of the build
   unreferenced, but `Assets/TextMesh Pro/Resources/**` (TMP Settings, LiberationSans SDF +
   materials, **the EmojiOne atlas**, style sheet, line-breaking tables) is FORCE-INCLUDED by
   Unity's Resources rule and WILL ship — the guardrail is stop condition 7's measured >5 MB
   threshold, and the AAB-size proof stays with the device lane's build legs). Chrome renders
   on a screen-space `Canvas` whose full component tree contains ONLY render-side types — no
   `EventSystem`-family object, no `Selectable` subclass, no raycaster-driven interactivity
   under the chrome root; harness criterion-2 statics stay green UNMODIFIED — **zero banned
   tokens in the slice's sources AND prose/comments; sweep before commit** (the P-1 landmine). *Check:* one **PlayMode** tree-walk whitelist test over the
   instantiated chrome root (R1-F13: EditMode instantiation of render objects fails on
   material-leak log noise — the CM-UX-01 A-UX2 precedent), **with a positive control: a decoy
   banned component added to a throwaway tree makes the walk fail**; plus the harness leg.
2. **`ScreenChromeController` renders by state — proven on TRANSITIONS (R1-F4).**
   `Attach(Func<string> screenState, ...)`; state map: `FailureReview` → CTA visible, veil
   hidden · `Halted` → veil visible, CTA hidden · `Playing`/`Won` → neither. **Declared
   supersession, bounded (R1-F19):** CM-UX-04 MAY amend the `Won` row to assert its results
   panel and may NOT remove or relax the other three rows. *Check:* PlayMode TRANSITION tests —
   attach while `Playing`, assert nothing renders (the pre-flip positive control), flip the
   state source, pump one frame, assert the mapped render; cover Playing→FailureReview,
   Playing→Halted (via criterion 4's REAL halt), and back-to-Playing hides. Renders appear
   **within one pumped frame** of the source changing (P-6 language; no same-frame claim).
3. **The CTA is the LOCKED string, at the floor ON ITS TAPPABLE RECT, render-only.**
   `retry.cta` resolves "Try again" via `UiStrings` (key-only). The chip is full-width inside
   `HudBands.ThumbBand(safeArea)` with the live `Screen.safeArea`/`Screen.dpi` binding
   (A-UX1-5 lands here; live reads exist ONLY on the runtime path). **The 48dp floor is
   asserted on the EFFECTIVE tappable rect (R1-F1; helper semantics per R2-N1):**
   `MeetsMinTargetPx(TappableRect(chipRect, rawRetryBand), dpi)` — `TappableRect` is a pure
   test-side/pure-class helper computing the axis-aligned overlap via max/min (UnityEngine has
   NO `Rect.Intersect` — verified against the assembly); **disjoint rects return a zero-size
   rect, which fails the floor by construction**. `rawRetryBand` is the merged band
   (`bottom 0.25 * raw height`, `TapInput.cs:81`). The EditMode table **injects dpi per row**
   (no `Screen.*` read in any EditMode leg — the CM-UX-01 injected-inputs law; `PxPerDp`'s
   dpi-0 fallback would otherwise make the same table flip green/red across hosts), covering
   zero-inset AND a gesture-nav+cutout case (R2-N2: first real `MeetsMinTargetPx` call site —
   reviewer attention on the dpi argument). **Painted-overhang bound (R2 residual on F1):**
   the chip's painted rect may exceed its tappable rect ONLY by the criterion-8 divergence
   height (`max(0, 0.75b − 0.25t)`), asserted per table row — the overhang is bounded and
   pinned, not silent, until CM-UX-07 reconciles.
   The chip registers NOTHING with `ChromeRegions` (CM-UX-01 law: the band's own `RetryTapped`
   IS the action). CTA rendering appears within one pumped frame of FailureReview;
   **hit-testability from frame 1 is TapInput's merged band behavior, already pinned — not a
   render claim (R1-F11)**. *Check:* the EditMode intersection table; PlayMode — CTA renders on
   the frame after FailureReview begins, band tap retries exactly as pinned; the decoy-region
   positive control from the binding rule.
4. **The veil renders on the REAL halt, neutrally, inertly (R1-F3).** The PlayMode check drives
   the merged halt for real: `GameRoot.Launch()` on L001, tap nothing, accelerated timescale,
   until `ScreenState == "Halted"` (the `GreyboxTests.cs:151-170` recipe), with the controller
   attached as `() => root.ScreenState` — the SAME delegate shape CM-UX-07 will bind, so a
   vocabulary mismatch fails HERE, not on device. On halt: full-board scrim + `halt.notice`
   ("Signal fault — the line stopped", DRAFT, Q-1) render; the veil registers zero regions
   (absence assert made live by the decoy control) and offers no affordance (Q-2's escape is
   CM-UX-07's). **Vocabulary guard (R1-F5; exemption list argued day-one per R2-N2):** an
   EditMode test asserts the csv value AND every new source file in this slice (component +
   test sources, identifiers included) contain none of the tokens `fail`, `lost`, `lose`,
   `loss`, `game over`, `crash` (case-insensitive; extensible), **with a shipped exemption
   list (the CM-UX-03 precedent):** (a) the merged screen-state vocabulary — the literal
   `FailureReview` (criterion 2's state map REQUIRES it: the state strings live in Bootstrap,
   which `CatMetro.Presentation` does not reference, so the controller must carry the literal);
   (b) the NUnit surface (`Assert.Fail`, `LogAssert`, test names derived from state names).
   What stays banned: player-facing copy and any NEW identifier naming the HALT as a failure.
   `fault` is deliberately NOT in the list: the approved copy attributes fault to
   the SYSTEM ("Signal fault"), which is the Q-B-neutral posture — recorded here so no later
   slice re-adds the token and breaks the approved string.
5. **Greyscale + motion-off legibility, mechanisms named (R1-F12).** Both views carry text +
   shape (never color-only state). *Checks:* (a) PlayMode motion-OFF: views render with zero
   `Animator`/`Animation`/tween components under the chrome root — **positive control: a decoy
   Animator added to a throwaway child makes the assert fail**; (b) motion-ON renders the same
   information set (visibility parity assert between the two runs); (c) the greyscale claim is
   a STRUCTURAL PROXY ONLY — an EditMode assert that each view exposes non-empty text plus a
   shape element; **the reviewer-signed greyscale checklist (A11Y-GLOBAL-2's actual evidence
   form) stays with the batched TG sitting and is NOT claimed by this slice** (P-5 discipline).
6. **Strings discipline, byte-pinned (R1-F16).** `ui.csv` gains exactly two rows:
   `retry.cta,Try again` (LOCKED) and `halt.notice,Signal fault — the line stopped` (DRAFT,
   pinned as these bytes — the em dash is U+2014, the FIRST non-ASCII byte in the table).
   *Check:* EditMode — the 5 base rows byte-identical to their frozen values; the two new
   values resolve through `UiStrings.Get` round-trip byte-exact (guards a BOM/re-encode); the
   P-4 literal guard (neither value appears as a literal in any Presentation component source).
7. **Registry lifetime law honored — and the absence assert can fail (R1-F2).** This slice
   registers no regions; the first registrar is CM-UX-06 (inherits R1-F3's
   Unregister-in-OnDestroy law). *Check:* in the same live fixture, a decoy registration moves
   `Regions.Count` 0 → 1 → 0 around the assertion window, proving the counter observes the
   registry the chrome actually shares.
8. **Band-divergence zones — BOTH pinned numerically (R1-F17; R2 residual).** An EditMode test
   computes, for the criterion-3 inset table, BOTH divergence zones between
   `HudBands.ThumbBand(safeArea)` and the raw band, asserting each expected height per row:
   (a) the **inert-chip overhang** above the raw band top (`max(0, 0.75*bottomInset −
   0.25*topInset)`) and (b) the **over-consuming zone** below the safe area (`bottomInset` —
   raw-band pixels outside the safe area that still retry). It also asserts criterion 3's
   tappable-floor + painted-overhang invariants for the shipped chip layout at every row.
   The slice handoff records the Pixel-9-Pro worked example (numbers). Reconciliation stays
   its own reviewed contract/line (R2-N1 of #27), never a composition-only edit.
9. **Zero behavior drift + combined-tree green.** Suites green at the rebase base's
   **re-derived** counts — **the re-derive is CERTAIN, not conditional (R1-F15): stop
   condition 1 guarantees DEVFIX (+5 PlayMode tests) merges first**; delta over the derived
   base = this slice's tests only; zero existing-test edits; `bash scripts/test.sh` with ALL
   wrappers green — **count re-derived at the DEVFIX base, never a frozen number (R2-N3b: 11
   wrappers exist today after DEVCAP's `devcap.test.sh`; the old "10/10" was stale on
   arrival)**; no GameRoot/Bootstrap/Content/Domain/scripts/wrapper edits; **no
   `unity/Packages/**` change of ANY kind — manifest or lock (R1-F10): an incidental
   `packages-lock.json` rewrite during the TMP import is a dependency-shaped diff and reverts
   before commit** (the CM-UX-01 F6 lesson, now a criterion). *Check:* headless runs + harness
   + diff surface.

### Forward obligations (recorded here so implementers inherit them)

- **CM-UX-07 MUST bind `BoardInputActive = () => ScreenState == "Playing"` in the SAME PR that
  attaches this controller (R1-F9).** G-3's acceptance rationale ("the desync is invisible
  until chrome makes the states legible") EXPIRES with this slice: a wired build that attaches
  the veil without binding the gate shows a halt veil over a board that still flips levers.
  The veil is inert by construction and blocks nothing — the gate is the blocker.
- **Interim two-stack note for the TG sitting (R1 angle-3):** until the BannerView
  back-migration (tranche-2), FailureReview renders BOTH the legacy TextMesh fail banner and
  the TMP chrome — the human will see two text stacks at the TG-1/TG-5 sitting; owner: the
  tranche-2 migration contract.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Presentation/Hud/ScreenChromeController.cs` ·
`unity/Assets/Scripts/Presentation/Hud/RetryCtaView.cs` ·
`unity/Assets/Scripts/Presentation/Hud/HaltVeilView.cs` (names indicative) ·
`unity/Assets/TextMesh Pro/**` (the as-shipped essentials import) · reference additions to
`CatMetro.Presentation`, `CatMetro.Tests.EditMode`, AND `CatMetro.Tests.PlayMode` asmdefs for
`UnityEngine.UI`/`Unity.TextMeshPro` as compilation requires (R1-F18) · **append-only**
`ui.csv` rows · new tests under `unity/Assets/Tests/EditMode/Presentation/**` and
`unity/Assets/Tests/PlayMode/**` · `state/handoffs/CM-UX-02.md` · the single
`state/PROJECT_STATE.md` merged-PR row (R1-F18).

**Explicit non-goals:** no GameRoot/Bootstrap edit, no controller attachment (CM-UX-07); no
restart escape or any Halted affordance; no results panel (CM-UX-04); no region registrations;
no TapInput/ChromeRegions/HudBands edits; no `unity/Packages/**` change of any kind (R1-F10);
no blame chip, ghost replay, rewind chip, or commerce surface (`PRD.md:208`); no edits to
existing ui.csv rows; no Halted semantics (Q-B/NEW-Q4); no gate-wrapper edits; no scene edits
(`Game.unity` untouched — A-UX2-3).

### Assumptions

- **A-UX2-1** TMP Essential Resources ship inside the BUILT-IN `com.unity.ugui` of editor
  6000.3.16f1 (manifest pins 2.5.0; the resolved builtin reports 2.0.0 — R1-F10 wording note):
  the import adds ~45 ASSETS, zero `.cs`, zero package adds. **Verified empirically by the
  round-1 reviewer against the pinned editor.** If red discovers otherwise → stop condition 3.
- **A-UX2-2** TMP's SDF UI shaders render screen-space canvas text on the DEVFIX-restored URP
  baseline; a PlayMode renderability proxy (generated mesh vertex count > 0) stands in for
  device proof; device magenta-checks remain the device lane's.
- **A-UX2-3** The chrome canvas is created by the controller itself; no scene edit.
- **A-UX2-4** `Screen.safeArea`/`Screen.dpi` reads live in the VIEW layer, injected into pure
  `HudBands` at call sites (A-UX1-5). Batchmode safe area equals the raw screen; inset behavior
  is covered by EditMode injection.
- **A-UX2-5 (rewritten per R1-F3):** the REAL Halted transition is deterministically reachable
  in PlayMode today — `GameRoot.Launch()` on L001, no taps, accelerated timescale
  (`GreyboxTests.cs:151-170`, merged and green). Criterion 4 uses it; no constructed state.

### Stop conditions

Defaults (AGENTS.md) plus:
1. DEVFIX (#30) not on main when red would start → **stop and wait** (ratified sequencing).
2. Any criterion appears to require an `EventSystem`, `Selectable`, raycaster interactivity, or
   a second input-package consumer → stop; gate evolution needs the human.
3. TMP essentials require a NEW package/manifest pin, or ANY `unity/Packages/**` file changes
   for any reason → stop (dependency = ADR = human).
4. The veil cannot be made legible without banned vocabulary or a Halted affordance → stop.
5. Criterion-2 harness statics fail for any reason other than this slice's own prose → stop;
   never edit the wrapper.
6. The device session merges a Presentation edit mid-slice → stop, rebase, re-verify the
   CM-UX-01 pins before continuing.
7. The TMP import measurably adds **> 5 MB** to the built Android artifact (measured, not
   guessed, when the device lane next builds) → stop and report numbers (R1-F14 tightening of
   the old "gross, not marginal" wording).
