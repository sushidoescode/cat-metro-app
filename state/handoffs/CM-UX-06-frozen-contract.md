# CONTRACT CM-UX-06 — ScreenStack + Home greybox + LevelIntro sheet

**Tranche:** UX tranche-1 slice 6 (`docs/ux/ux-layer-decompose.md` §2, CM-UX-06). NEEDS-WIRING ·
gate-untouched. **Base/anchor:** origin/main ca13801 (CM-UX-02 merged, #35). Wrapper count at
anchor: **N=12** (`find tests -name '*.test.sh' | wc -l`) — all later counts are N-relative.
**DEPENDS-ON:** CM-UX-01 (#28) and CM-UX-02 (#35), both merged. CM-UX-03/04/05 are NOT
dependencies — this slice shares no files with them beyond append-only ui.csv rows and the
declared UiCsvDisciplineTests count amendment (its own R1-L6 evolution clause).

### Goal

The player-facing spine of the home→play journey exists at component level: ADR-0007's screen
stack as a pure class whose serialization shape matches ADR-0006 `breadcrumbs.screenStack`, a
greybox Home (one pulsing L001 pin, parked-district silhouettes, session-1 structural law), and
the minimal LevelIntro sheet that makes the game explain itself — level name + goal line +
explicit thumb-band Play CTA before play. Components + direct-construction tests only; the
Home/stack mount behind an explicit launch argument is CM-UX-07's (P-3).

### Binding posture (inherited, decompose §1)

P-1 render-only chrome, hits through CM-UX-01's ChromeRegions (this slice is the FIRST
registrar — inherits R1-F3's Unregister-in-OnDestroy law) · P-2 TMP/UGUI, no new TextMesh ·
P-3 zero Bootstrap/GameRoot edits · P-4 ui.csv append-only + per-slice literal guard · P-5
48dp floors, state never color-alone, motion-off removes easing never information · P-6
injected delegates, no wall clock into the sim · P-7 red-first labeled red-first, pins labeled
pins. Live-wiring/anti-vacuity rule inherited from CM-UX-01/02: every negative or absence
assertion carries a positive control in the same fixture.

### Spec reference

`docs/adr/0007-*` (screen stack inside Home/Game, push/pop, breadcrumb persistence) ·
`docs/adr/0006-*:94` (`breadcrumbs.screenStack: []` — ordered string array) ·
`docs/prd/ux-flows.md` S-01 layout intent (session-1 Home: one pulsing pin, parked scenery,
"No shop entry, no daily entry, no badges rendered in session 1"), S-05 (pin tap → LevelIntro →
Play; thumb-band law §1.1; A11Y-S01-2 raised-ring shape twin; A11Y-S01-4 ≥48dp), TG-3 (neither
Night-Harbor variant built — tile absent in greybox) · CM-UX-01 laws (resolution order, HudBands
safe-area math, injected inputs) · CM-UX-02 precedents (view-layer live Screen reads, whitelist
tree walk in PlayMode per R1-F13, csv byte-pins).

### Acceptance criteria (8)

1. **Pure-C# ScreenStack, ADR-0007 navigation law.** `Push(id)` (rejects null/empty — a wiring
   defect, never silent), `TryPop(out id)` LIFO (false on empty — PC-3: on the first Home there
   is nothing to pop), `Current` (null when empty), `Count`. *Check:* red-first EditMode tests
   (push/pop order, empty-pop, rejection).
2. **Breadcrumb SHAPE matches ADR-0006, save I/O stays deferred.** `ToBreadcrumb()` returns the
   ordered string array (bottom→top; empty stack → empty array, matching the `[]` default);
   `RestoreFrom(entries)` validates entries, replaces contents, and round-trips exactly. NO save
   read/write anywhere in this slice (Application-layer, deferred). Structural pin (P-7):
   ScreenStack is not a `UnityEngine.Object` and the slice's pure types construct no GameObject.
   *Check:* red-first round-trip + validation tests; one labeled-pin reflection assert.
3. **Greybox Home renders the session-1 read.** Title (`home.title` via UiStrings key),
   parked-district silhouettes (render-only), exactly ONE level pin (L001). Pin tappable rect
   ≥48dp: pure `HomeLayout.PinRect(safeArea, dpi)` math (injected inputs; 72dp side, centered
   in the thumb band) with an EditMode table (360×640 reference @160dpi, dpi-0 fallback, one
   bottom-inset case) PLUS a live PlayMode assert on the painted rect. *Check:* EditMode table
   red-first; PlayMode render assert.
4. **Pulse + motion-off shape twin (A11Y-S01-2).** Motion state is an injected `Func<bool>`
   (GameRoot.MotionOff binding is CM-UX-07's). Motion ON: the pin scale pulses (code-driven
   easing, zero Animator/Animation components). Motion OFF: scale locked at 1. The raised-ring
   shape twin renders in BOTH modes — the pulse is easing, the ring is information. *Check:*
   PlayMode — scale varies over sampled frames motion-on, byte-stable motion-off; ring visible
   both modes; positive control (hidden view → no ring).
5. **Session-1 structural law + TG-3, decoy-controlled.** A PlayMode tree walk over the Home
   root asserts NO node whose name matches shop/store/daily/badge/streak/share/notification and
   NO Night-Harbor/All-Access node (neither TG-3 variant is built). Positive control: a decoy
   node makes the walk fail. *Check:* the tree test with decoy.
6. **First registrar, lifetime-lawful, render-only trees.** Home registers exactly one region
   (the pin) and LevelIntro exactly one (the Play chip) with an INJECTED ChromeRegions;
   resolve-at-center fires the `LevelSelected` / `PlayRequested` seam; resolve outside fires
   nothing (tap-anywhere dismissal is NOT built — S-05/decompose law); regions unregister on
   Hide AND OnDestroy (R1-F3). Whitelist tree walk over both roots: render-side types only,
   zero Selectable, zero raycaster, zero animation components — with the CM-UX-02 decoy
   positive controls. Harness criterion-2 statics pass UNMODIFIED; zero banned input-surface
   tokens in sources or prose (sweep before commit). *Check:* PlayMode region/lifetime tests +
   whitelist walks + the harness leg on a committed tree.
7. **LevelIntro explains the level; strings discipline (P-4).** `Show(levelName, deliveries)`
   renders the injected level name, the goal line (`intro.goal` template, `{count}`
   substituted — the component receives the KEY plus data, never a literal), and the Play CTA
   (`intro.play`) full-width in `HudBands.ThumbBand(Screen.safeArea)` (≥48dp floor asserted).
   ui.csv gains exactly THREE rows, byte-pinned, all DRAFT and queued for the TG-5 voice
   sitting: `home.title,Cat Metro` · `intro.play,Play` · `intro.goal,Deliver {count} cats`.
   Rows 0–6 stay byte-identical; the merged UiCsvDisciplineTests count bound (7) rises to 10
   under its OWN declared R1-L6 evolution clause (rows-5/6 pins untouched; my rows pinned in
   this slice's test) — disclosed in the PR, never silent. Literal guard: none of the three
   values appears as a quoted literal in any Presentation component source. TMP renderability
   proxy on the sheet text. *Check:* EditMode csv/byte/round-trip/literal tests red-first;
   PlayMode render assert.
8. **Zero behavior drift.** Full suites green at the re-derived anchor counts (re-derive at
   ca13801, never a frozen number) + this slice's tests only; zero existing-test edits EXCEPT
   the declared criterion-7 count amendment; no GameRoot/Bootstrap/Content/Domain/scripts/
   wrapper/`unity/Packages/**`/scene edits; `.meta` files generated by a `-quit` batch import
   and committed. *Check:* headless EditMode+PlayMode runs + `bash scripts/check.sh` + full
   `bash scripts/test.sh` on a committed tree + diff surface.

### Visual verification (standing rule, #33)

After green: render real frames of Home (motion-on and motion-off) and the LevelIntro sheet via
an UNCOMMITTED capture probe, LOOK at the PNGs, describe what is visible in the PR evidence,
delete the probe before committing.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Presentation/Screens/ScreenStack.cs` ·
`.../Screens/HomeLayout.cs` · `.../Screens/HomeScreenView.cs` · `.../Screens/LevelIntroSheet.cs`
(all new, + .meta) · append-only `ui.csv` rows · the declared UiCsvDisciplineTests count
amendment · new tests under `unity/Assets/Tests/EditMode/Presentation/**` and
`unity/Assets/Tests/PlayMode/Screens/**` · this handoff.

**Explicit non-goals:** no wiring/mount (CM-UX-07: Home/stack behind an explicit launch
argument); no boot-to-Home or LevelIntro tick-0 hold (GameRoot state-machine work — boot-flow
follow-up contract, never the thin wiring PR); no save I/O or stack persistence
(Application-layer); no star thresholds/best score on the sheet (deferred register); no
Night-Harbor tile in either variant (TG-3); no settings/pause/audio surfaces; no
monetization/store/daily/share/streak/notification surface of any kind (the tree law IS the
tripwire); no TapInput/ChromeRegions/HudBands/ChromeGeometry edits; no gate-wrapper edits; no
scene edits; no `unity/Packages/**` change of any kind.

### Assumptions

- **A-UX6-1** The goal line is in-slice per the lane's task contract ("level name + goal line
  before play"); the deferred-register item "LevelIntro best-score/thresholds (save + content
  reads)" defers SAVE-backed data. The sheet receives `(levelName, deliveries)` as plain
  injected data by direct construction; who reads content stays the caller's concern
  (CM-UX-07/boot-flow). Zero-instructional-text posture: level name and a goal count are
  level-meta legibility on a pre-play sheet, not in-run tutorial prose (CM-R13.1's exemption
  spirit); flagged for the TG-5 sitting with the rest of the DRAFT set.
- **A-UX6-2** All three csv values are DRAFT; TG-5 voice-passes them before device exposure.
- **A-UX6-3** The pulse may read `UnityEngine.Time` (merged Presentation precedent:
  `CauseCameraController.cs:72`); it never enters the sim (P-6) and is removed by the injected
  motion-off delegate.
- **A-UX6-4** Screens build under a caller-supplied canvas transform (the RetryCtaView
  pattern); canvas/camera composition is CM-UX-07's. Live `Screen.safeArea`/`Screen.dpi` reads
  exist only on the runtime layout path, injected into pure math (A-UX1-5 law).
- **A-UX6-5** ChromeRegions is injected (tests construct their own; CM-UX-07 hands the views
  `TapInput.Regions`). Region ids: `home.pin.l001`, `intro.play`; priorities explicit
  (A-UX1-3).

### Stop conditions

Defaults (AGENTS.md) plus:
1. Any criterion appears to need an EventSystem-family object, Selectable, raycaster
   interactivity, or a second input consumer → stop; gate evolution is human-gated.
2. Criterion-2 harness statics fail for any reason other than this slice's own prose → stop;
   never edit the wrapper.
3. Anything requires a Bootstrap/GameRoot/Content/Domain/scripts edit → stop.
4. The merged UiCsvDisciplineTests amendment turns out to require more than the declared
   count-raise (e.g. touching rows 0–6 pins) → stop; that is not the R1-L6 path.
5. The device session merges a Presentation edit mid-slice → stop, rebase, re-verify the
   CM-UX-01/02 pins before continuing.

### Status log

- 2026-08-06 — contract frozen at anchor ca13801; N=12 wrappers recorded; red next.
