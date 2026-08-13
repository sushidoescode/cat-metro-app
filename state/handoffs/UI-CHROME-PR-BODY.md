# UI-CHROME PR body draft

**Do not open from this snapshot.** First require Lane 1A PR #65 on `main`, rebase this branch,
land the separately committed WavePreview primitive-path removal, and replace every bracketed
post-#65 placeholder below with exact evidence. Open as a draft while device/TG evidence remains
pending; do not mark ready or request HC-25 until every merge blocker is discharged.

Suggested title: `UI-CHROME: branded screens, readable wave cats, and presentation stingers`

---

## What changed

- adds one product-spec presentation vocabulary: the exact 12-color palette, rounded chrome
  geometry, shared CatMetro Sans TMP roles, and reusable color+shape symbols
- restyles Home with a tracked Cat Metro title treatment, cat/rail mark, three distinct parked
  district silhouettes, and the existing single L001 pin
- restyles LevelIntro as a navy-staged paper route card with separated route motif and orange
  Play CTA
- unifies warning/win banner, retry, hint, and halt chrome under the same paper/navy/teal/orange
  system without adding player-facing copy
- turns ResultsPanel into a deliberate completion card with route/cat motif, visible confetti,
  one Next CTA, and the existing structurally empty footer
- replaces the bare wave chips visually with one cream tray containing upcoming cat faces,
  redundant symbol badges, and fitted 24dp-equivalent counts
- adds one small Presentation-local audio manager with exactly three prewarmed sources and
  original generated tap, warning, and win PCM stingers

## Why

Lane 1B removes the remaining greybox UI presentation while preserving the existing simulation,
screen stack, localization, safe-area, render-only chrome, and single-input-surface laws. The
wave strip directly repairs the recorded 640x480 editor-host illegibility observation through
color + cat face + symbol + count rather than color alone.

## Ordered dependency and intentionally RED assertions

The ground-truth parallel-push handoff assigns Lane 1A the criterion-5 primitive/material gate
re-author and requires it to land before Lane 1B removes WavePreviewStrip's remaining
`GameObject.CreatePrimitive` path.

Current pre-#65 snapshot:

- the accepted visual-only strip commit retains exactly one primitive source site; its legacy
  renderer is disabled and never painted
- full PlayMode is **154/156**; the only failures are
  `LiveWaveChip_IsUguiTmp_FittedReadableAndSymbolCoded` at its zero-Renderer assertion and
  `WavePreviewSource_ContainsNoPrimitiveConstruction`
- both assertions remain enabled and intentionally RED by ordering; they are not skipped,
  ignored, disabled, or softened to manufacture a clean count

Post-#65 update required before opening:

- [ ] rebase onto the landed Lane 1A commit and record its criterion-5 gate re-author OID
- [ ] remove the legacy primitive path in a separate Lane 1B commit
- [ ] show the same two assertions GREEN and replace the 154/156 snapshot with the final full
  PlayMode count

## Validation

Pre-#65 evidence snapshot at Lane 1B `124b8a3`:

- full Unity EditMode: **824/824**
- full Unity PlayMode: **154/156**, with only the two ordered failures named above
- wave visual + existing behavior: **2/2**
- failure/device breadth: **17/17**
- `bash scripts/check.sh`: **PASS**
- `bash tests/unity/failure.test.sh`: **PASS**
- symbol-node mutation: **RED 0/1**, missing `WaveSymbol`; reverted byte-clean
- count-size mutation: **RED 0/1**, 23dp below the 24dp/38.25px floor; reverted byte-clean

Final post-#65 gates:

- [ ] full Unity EditMode: `[count/count]`
- [ ] full Unity PlayMode: `[count/count]`
- [ ] `bash scripts/check.sh`: `[PASS]`
- [ ] `bash scripts/test.sh`: `[N/N PASS]`
- [ ] `bash scripts/build.sh`: `[PASS]`
- [ ] `ui.csv` byte comparison and forbidden-path audit: `[PASS]`
- [ ] dev APK: `[filename, source OID, SHA-256, debuggable/ABI/SDK facts]`

## Rendered and asset evidence

- [Frozen contract and full TDD/status trail](https://github.com/sushidoescode/cat-metro-app/blob/art/ui-chrome-pass/state/handoffs/UI-CHROME-frozen-contract.md)
- [Wave strip capture method, inspection, mutations, and boundary](https://github.com/sushidoescode/cat-metro-app/blob/art/ui-chrome-pass/evals/results/ux/ui-chrome-pass/playing-wave-preview/ARTIFACT.md)
- [Inspected 640x480 wave frame](https://github.com/sushidoescode/cat-metro-app/blob/art/ui-chrome-pass/evals/results/ux/ui-chrome-pass/playing-wave-preview/cm-ui-wave-reference-640x480.png)
- [Stinger generation provenance](https://github.com/sushidoescode/cat-metro-app/blob/art/ui-chrome-pass/unity/Assets/UI/Audio/PROVENANCE.md)
- [ ] link the final six-frame real-scene pack: Home, LevelIntro, Playing/wave, first warning,
  second warning + hint, and Won/Results
- [ ] link the combined post-#65 device-resolution capture and scoped logcat

**Host caveat:** every current Lane 1B render is the recorded 640x480 batch-editor host scale.
Those frames prove editor-host composition only. They are not device-resolution/device-DPI
evidence; the final device leg rides the combined post-#65 build.

## Risk gate and review

Pre-#65 trusted-base classifier snapshot:

- protected base: `9be8f9595df75a7ec1a859fcd75dd7bbf1eb8fb8`
- Lane 1B head: `124b8a31a299d79d2b468c656b3bd7b64e3df2e5`
- verdict: **RISKY**, exit 2
- rule: `fail-closed.inspection-limit`
- explanation: changed `CatMetroSans SDF.asset` exceeds the hard 1 MiB inspected-blob cap
- vector: `change_class=unclassifiable`, `blast_radius=unknown`,
  `reversibility=unknown`, `security_review_required=true`

This snapshot does not authorize the merge gate after rebasing. Required before ready:

- [ ] rerun the trusted-base classifier against the protected post-#65 `main` OID
- [ ] complete exactly one independent correctness review round required by the RISKY verdict
- [ ] complete the independently routed security review required by the vector
- [ ] record every finding, fix/disposition, and reviewer verdict here

## Scope and boundaries

- no scene, ProjectSettings, URP/lighting, Board, Cameras, Input, Diagnostics, Domain, Content,
  Bootstrap, package, or Greybox material changes
- `ui.csv` remains byte-identical; no new player-facing copy
- runtime chrome remains render-only; existing ChromeRegions/Input seams own every hit
- Lane 1A and Lane 1B coordinate the strip only through the human-provided reference and chat;
  neither lane edits the other's files

## Draft/merge blockers

- [ ] Lane 1A PR #65 merged and Lane 1B rebased
- [ ] separate collider/primitive-path commit complete with the two deferred assertions green
- [ ] final source, Unity, wrapper, and build gates green
- [ ] final real-scene frame pack inspected and committed
- [ ] combined post-#65 device-resolution evidence attached
- [ ] required independent correctness/security review converged
- [ ] human TG review accepted
- [ ] fresh in-session HC-25 merge word received only after all prior blockers are discharged

This PR must not arm or complete a merge without that fresh HC-25 word.
