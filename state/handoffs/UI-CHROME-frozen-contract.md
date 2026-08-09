# CONTRACT UI-CHROME — Lane 1B presentation polish and audio stingers

**Lane:** 1B UI-CHROME from `state/handoffs/PARALLEL-PUSH-2026-08-09.md`.
**Branch:** `art/ui-chrome-pass`. **Frozen anchor:** `b528bd3` (the reviewed
`origin/session/parallel-push-launch` tip, because PR #63 is not yet on main).
`origin/main` was re-fetched before this contract commit and read `1a1bf09`.
Wrapper count at freeze: **N=15**. `bash scripts/check.sh` was green on the clean
anchor; the clean-anchor `bash scripts/test.sh` run was started before freeze and its
final result is recorded in the status log below.

## Goal

Replace the shipped greybox chrome with one coherent Cat Metro presentation system:
the product-spec palette, a legible non-nursery TMP hierarchy, a branded Home and
LevelIntro, cause/hint/retry and results surfaces, a readable symbol-coded wave strip,
and small original tap/outcome stingers. Preserve every existing navigation, input,
safe-area, localization, and state-transition law. Nothing in this lane edits or
serializes the scene; every view remains runtime-constructed by its existing seam.

## Binding references

- `state/handoffs/PARALLEL-PUSH-2026-08-09.md` (ground truth, Lane 1B ownership,
  merge order, collider split, HC-25).
- `SESSION-HANDOFF-2026-08-08.md` §Operating notes (real-frame evidence, mutation
  proofs, raw-source copy scan, substring vocabulary scan, 640x480 host caveat).
- `docs/plan/specs/product_spec.md` §§7, 8, 10, 12, 26: authoritative 12-color
  palette; cream/navy/teal/orange chrome; rounded shape language; parked district
  scenery; one session-1 CTA; cause-first outcome chrome; mute-friendly P0 signals.
- `docs/prd/ux-flows.md` S-01/S-02 and A11Y-S01-2/4/7: parked silhouettes, no
  session-1 commerce nodes, shape twins, 48dp floor, color never the sole signal.
- ADR-0007: UGUI + TMP, prewarmed `AudioSource` pool, no FMOD/Wwise, custom CSV
  strings; ADR-0003: all work stays inside `CatMetro.Presentation`.

## Contract restatement

The lane restyles **Home, LevelIntro, all outcome/hint/retry chrome, ResultsPanel,
and WavePreviewStrip**, and adds **tap, warning-outcome, and win stingers** through a
small Presentation-local audio manager. It may edit only the lane-owned product paths,
its directly corresponding tests, this contract/handoff, rendered evidence, and the
single lane row plus contract-named debt entry at merge. `ui.csv` is append-only; this
contract needs no new copy, so its bytes remain unchanged.

## Acceptance criteria (9)

1. **One authoritative visual vocabulary.** A Presentation-local theme exposes all
   12 product-spec colors byte-exact from their hex values and one shared rounded
   chrome sprite. All TMP text created by the five target surfaces uses one committed
   `CatMetro Sans` TMP font asset under `unity/Assets/UI/**`; display, heading, body,
   and CTA styles have named sizes/weights/tracking. The typeface source is the repo's
   already-shipped OFL Liberation Sans, copied with its license rather than adding a
   package or an unlicensed font. The title treatment uses weight, tracking, case, and
   rail/cat geometry; it does not use nursery/bubble ornament. *Checks:* EditMode exact
   palette/font/resource tests; PlayMode tree walk joining every target TMP node to the
   shared font/style helpers; missing-resource mutation turns the resource test red.

2. **Home reads as Cat Metro, not placeholder geometry.** Home paints Warm Paper,
   renders the CSV-resolved title as an Ink Navy display lockup with teal/orange rail
   accents and a non-text cat/metro mark, and renders three visibly distinct parked
   district silhouettes as layered scenery (not buttons or lock icons). The sole L001
   pin keeps its existing 72dp rect, ChromeRegions seam, pulse, raised-ring motion-off
   twin, and session-1 no-commerce tree law. *Checks:* extend the live Home tests with
   exact role colors, title typography, three silhouette groups with distinct child
   signatures, and unchanged one-region/48dp/motion-off laws; decoy controls remain.

3. **LevelIntro is a readable paper route card.** The sheet uses an Ink Navy staging
   scrim, a rounded Warm Paper card, a Metro Teal route accent, Ink Navy name/goal
   hierarchy, and a Ticket Orange full-width Play CTA with Ink Navy label. Existing
   injected name/data and `intro.goal`/`intro.play` keys remain the only copy. Text uses
   autosizing bounds that keep the name, goal, and CTA non-truncated at the supported
   130% presentation scale. The one-region, outside-tap, safe-area, and priority laws
   remain byte-behavioral equivalents. *Checks:* PlayMode style/overflow/font asserts
   plus all existing interaction and ordering tests; a wrong-CTA-color mutation is red.

4. **Cause, hint, retry, and halt chrome share the same system.** `BannerView` becomes
   TMP/UGUI (zero new `TextMesh`) and presents outcome copy on a rounded Warm Paper card;
   warning outcomes carry an Alarm Coral keyline and win carries a Metro Teal keyline,
   with shape/text still carrying meaning when color is removed. Retry is Ticket Orange
   with Ink Navy type; hint is Metro Teal with Warm Paper type and a visible cat-ear
   shape marker; halt stays semantics-neutral with Ink Navy/Warm Paper plus its existing
   text. All strings still resolve by key, and all existing state/visibility/input laws
   remain. *Checks:* PlayMode component/color/font/tree tests and the unmodified raw-copy
   and halt-vocabulary gates; remove-keyline mutation is red.

5. **ResultsPanel is a deliberate finish, without invented score data.** Won keeps one
   and only one registered `Next` CTA and the structurally empty footer. A rounded Warm
   Paper completion card, teal route/cat motif, light orange/teal confetti geometry, and
   Ticket Orange `Next` chip replace the flat full-band placeholder; the existing win
   banner remains the copy carrier, so no score, stars, tickets, or best values are
   fabricated. Safe-area, live progression, co-registration priority, and OnDisable
   laws stay unchanged. *Checks:* extend ResultsPanel tests with exact role colors,
   geometry/font assertions, count==1/footer-empty invariants, and real-Won transition;
   removing the completion card turns its named assert red.

6. **Wave preview is readable and triple-coded at the recorded host size.** Replace the
   two world-space quad/`TextMesh` chips with display-only UGUI/TMP cards in the top 15%
   safe band. Each visible entry shows its authoritative line color **plus** the locked
   symbol (circle/square/triangle/diamond/star) **plus** a numeric count. At the recorded
   640x480 batch host the TMP glyph bounds are non-empty, remain within their card, and
   use at least the contract's 24dp-equivalent body size after band fitting; a rendered
   crop must be human-legible by inspection. Stable authored wave order and tick refresh
   semantics stay unchanged. After Lane 1A's criterion-5 gate re-author lands, this file
   also contains zero `CreatePrimitive` and its tree contains zero Collider. *Checks:*
   new PlayMode wave-style/layout/symbol tests; existing preview behavior tests; delete-
   symbol and font-size mutations each turn a named assert red.

7. **Three original, prewarmed stingers through one small manager.** A new
   `Presentation/Audio/UiAudioManager` owns exactly three prewarmed `AudioSource`s and
   three committed PCM WAV clips under `unity/Assets/UI/**`: tap (short wood/paper tick),
   warning outcome (short descending thud), and win (short ascending major/pentatonic
   flourish). The clips are generated in-session from oscillators/noise, carry an asset
   provenance note with reproducible commands, and include no third-party recording.
   The manager is Presentation-internal, creates at most one instance per view root,
   allocates no source on playback, and is mute-safe: audio never gates a visual state.
   Tap plays on Home/Play/Next seams and on the retry return edge; warning/win play once
   per state-entry edge, never every frame. *Checks:* EditMode clip presence/import and
   manager-shape tests; PlayMode edge/idempotence/single-instance tests using an observable
   last-cue/count seam; repeated-frame and remove-transition mutations are red.

8. **Localization, architecture, and lane boundaries remain intact.** `ui.csv` is
   byte-identical to the frozen anchor (there is no new player-facing copy). No new
   package/dependency, scene, ProjectSettings, URP/lighting, Greybox material,
   Presentation/Board, Presentation/Cameras, Presentation/Input, Presentation/Diagnostics,
   Domain, Content, Bootstrap, or `tests/contract` edit appears. Runtime UI remains
   render-only and all hits still route through the existing ChromeRegions/Input seams.
   *Checks:* byte compare to `b528bd3`, banned-path diff audit, existing static wrappers.

9. **Green, captured, and reviewable.** TDD is red-first for every behavior/style law;
   load-bearing asserts carry named mutation evidence. `bash scripts/check.sh`, full
   EditMode, full PlayMode, `bash scripts/test.sh`, and `bash scripts/build.sh` pass on the
   committed tree. Real-scene Screen-matched captures are committed for Home, LevelIntro,
   Playing/wave strip, first warning outcome, second warning outcome with hint, and Won/
   Results; the session opens and inspects every PNG and records what is visible. A dev APK
   build is attempted after the CLI shim is copied uncommitted from the main checkout, as
   directed by the launch handoff; device capture remains human hardware evidence if no
   Pixel is attached.

## Scope

**Owned product paths:**

- `unity/Assets/Scripts/Presentation/Hud/**`
- `unity/Assets/Scripts/Presentation/Screens/**`
- `unity/Assets/Scripts/Presentation/Strings/**`
- `unity/Assets/Scripts/Presentation/Audio/**` (new)
- `unity/Assets/Resources/Strings/ui.csv` (append-only; unchanged by this contract)
- `unity/Assets/Resources/Materials/UiChrome.mat`
- `unity/Assets/UI/**` (new font/style/texture/audio assets and licenses)

**Direct evidence/test paths:** matching EditMode/PlayMode Presentation tests,
`evals/results/ux/ui-chrome-pass/**`, this contract/handoff, and—only at the merge
bookkeeping step—the lane's one `state/PROJECT_STATE.md` row plus its collider-debt half.

**Forbidden:** `unity/Assets/Scenes/Game.unity`, `unity/ProjectSettings/**`, URP/lighting,
`Presentation/Board/**`, `Presentation/Cameras/**`, `Presentation/Input/**`,
`Presentation/Diagnostics/**`, `Resources/Materials/Greybox.mat`, Domain, Content,
Bootstrap, packages, immutable paths, and every other lane's state row.

## Assumptions (listed, not hidden)

- **A-UI-1 (typography):** Product specs constrain typography negatively (legible,
  premium, not nursery/bubble) but name no typeface. The smallest licensed interpretation
  is the repo's existing OFL Liberation Sans SDF source, renamed only as the local style
  asset and differentiated through hierarchy/tracking. Typeface taste remains part of the
  rendered-frame human gate; changing to another family is not required for this contract.
- **A-UI-2 (audio):** “tap” means chrome CTA/seam taps inside this lane. Switch-disc audio
  would require a forbidden Board/Input edit and is explicitly not inferred.
- **A-UI-3 (results):** Existing Domain output does not expose real star/ticket rollup data
  to this lane; visual polish must not fabricate it. Geometry may celebrate; numbers may not.
- **A-UI-4 (host scale):** 640x480 proves the recorded preview observation is repaired at
  that host. It is not presented as device DPI evidence; the device leg is called out
  separately.
- **A-UI-5 (audio mixer):** This increment supplies the ADR-0007-sized prewarmed source pool
  and one manager. Layered music stems/mixer snapshots remain outside the requested three
  stingers; no second audio framework is introduced.

## Dependencies and stop conditions

1. **Lane 1A ordering is binding:** do not commit the WavePreview `CreatePrimitive`
   removal until Lane 1A's criterion-5 gate re-author is available; rebase on its landed
   main commit (preferred) or obtain the launch document's explicit joint-edit route.
2. If a target view needs a scene, ProjectSettings, Bootstrap, Board, Camera, Input,
   Diagnostics, Domain, Content, package, or immutable-path edit, stop and ask.
3. If a new player-facing phrase becomes necessary, stop and present the exact append-only
   `ui.csv` row; do not smuggle copy into source.
4. If the OFL font cannot be imported as a committed TMP asset without editing outside the
   owned paths, keep the existing TMP font reference and stop before claiming criterion 1.
5. If real-frame capture needs a committed Editor/Diagnostics probe, stop; the probe may be
   uncommitted and deleted, following the prior UX contracts.
6. Three failed hypotheses on one increment trigger the sprint circuit breaker and a single
   unblocking question.

## Status log

- 2026-08-09 — contract frozen at `b528bd3`; `origin/main` re-fetched at `1a1bf09`;
  N=15; clean-anchor `check.sh` green; clean-anchor full suite green: EditMode 821/821,
  PlayMode 137/137, wrappers 15/15.
- 2026-08-09 — RED verified for the right reasons after one test-only compile correction:
  focused EditMode 0/3 (missing theme, font/shape assets, stingers) and focused PlayMode
  0/16 (missing manager/styles/symbol map, legacy TextMesh/primitive preview). Raw NUnit
  results: `/tmp/cm-ui-red-edit.SXvPxI/results.xml` and
  `/tmp/cm-ui-red-play.FWp9bP/results.xml`.
- 2026-08-09 — independent core milestone green: palette/font/sprite/stinger resources
  3/3 and Home/intro/outcome/results/audio PlayMode 9/9; `check.sh`, raw-copy static,
  and the halt-vocabulary source sweep green. WavePreview remains red and untouched while
  its Lane 1A gate-re-author prerequisite is unavailable. PR #63 landed on main as
  `11a3335`; the three contract-first lane commits were rebased onto that squash.
- 2026-08-09 — first Screen-matched 640x480 inspection caught a live composition defect:
  the intro route rail/marker crossed the title, the full-screen scrim inherited card
  corners, and Home used Cream Card rather than the contract's Warm Paper. Visual-regression
  RED is 0/3 at `/tmp/cm-ui-visual-red.q15KY8/results.xml`; the three named failures are
  `HomePaper`, `full-screen staging scrim`, and `teal route rail`.
- 2026-08-09 — visual-regression GREEN is 3/3 at
  `/tmp/cm-ui-visual-green.Fn1PLb/results.xml`. Recapture at
  `/tmp/cm-ui-screen-recapture.sPaUFf/` was opened at original 640x480: Home shows the
  navy tracked title/cat-rail mark, three distinct layered parked districts, and the sole
  orange/navy pin; LevelIntro shows a clear top route band, separated name/goal hierarchy,
  rounded paper card, flat navy staging field, and full-width orange Play chip.
- 2026-08-09 — post-rebase breadth: full EditMode 824/824 at
  `/tmp/cm-ui-editmode-all.wSLz9B/results.xml`; 101/101 affected PlayMode interactions at
  `/tmp/cm-ui-related-play.PkLOvq/results.xml`. Results-frame inspection then found rounded
  ear blocks reading ambiguously and both confetti pieces hidden under the foreground win
  banner. Motif/composition RED is 3/7 with four named failures at
  `/tmp/cm-ui-motif-red.pKeywj/results.xml` (Home cat mark, hint cat marker, completion cat,
  and confetti/banner separation).
- 2026-08-09 — shared triangle silhouettes plus banner-clear confetti are green 7/7 at
  `/tmp/cm-ui-motif-green.lIkVEE/results.xml`; protruding-ear regression went RED 2/4 at
  `/tmp/cm-ui-ears-red.nxe8JG/results.xml` and GREEN 4/4 at
  `/tmp/cm-ui-ears-green.gX56nU/results.xml`. The Results recapture at
  `/tmp/cm-ui-results-ears.lugDOO/cm-ux-04-results.png` was opened at original size: both
  colored confetti pieces are visible beside the completion card, the navy cat ears read
  above the head/route motif, the CSV win banner remains the sole outcome copy, and the
  full-width orange Next CTA remains unobscured.
