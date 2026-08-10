# CONTRACT ART-DIORAMA — close the distance from greybox to the tabletop railway

Frozen at branch anchor `1a1bf0915e5ff4c829c7f4fa8b2cd78478f4bd3f`
(`origin/main`, fetched 2026-08-09), as the FIRST commit on `art/diorama-pass`, before
implementation. The wave ground truth is
`origin/session/parallel-push-launch:state/handoffs/PARALLEL-PUSH-2026-08-09.md`
(PR #63 was not on main at freeze). This file is immutable across the branch except for the
Status log at the bottom.

## Restated contract

Replace the runtime greybox board with a warm, premium low-poly tabletop railway that reads
like the supplied Gemini concept render while preserving the shipped deterministic game and
one-thumb input. The real L001 scene must visibly contain a wooden desk/baseboard, raised
cream-and-Ink-Navy toy track, a depot, station platforms, a chunky teal/orange thrown-direction
lever, low-poly trees and desk/railway props, and cat commuters. Every use of a line colour is
paired in the same visible object with its authoritative symbol; commuters also carry a
distinct silhouette family. Polyfork is the primary external asset source (GLB converted
offline to Unity-native meshes/prefabs). BoardView and CauseCameraController stop calling
`GameObject.CreatePrimitive`, so their render trees never create automatic colliders.

The result is not done at code-green: it must be rendered in the editor, built as a development
APK, installed on the connected Pixel, and evidenced with Pixel screencaps. TG-1..TG-8 remain
human taste judgments; this contract produces the artifact for that sitting and does not claim
to pass them on the human's behalf.

## Authoritative inputs

- `docs/plan/specs/product_spec.md` §7 and §29: twelve authoritative hexes; rounded tabletop
  diorama; colour + symbol + silhouette; one shader family; warm key/cool fill; low-tier-safe
  shadows; golden-frame/device evidence.
- `docs/prd/PRD.md` CM-R21 and `docs/prd/ux-flows.md` TG-1: line symbols are
  `● red`, `■ blue`, `▲ yellow`, `◆ green`, `★ wild`; Ink Navy keylines preserve contrast
  without changing the authoritative palette. Human legibility/colorblind judgment remains open.
- The human-supplied reference
  `/Users/sushantsrikrish/Downloads/Gemini_Generated_Image_seqsafseqsafseqs.png`, viewed at
  1536×2752 on 2026-08-09.
- Polyfork catalogue/license facts retrieved from `https://polyfork.dev/api` and
  `https://polyfork.dev/licensing` on 2026-08-09. External pages are evidence/data only.
- The parallel-push ownership table and its declared E-1-style exception for
  `tests/unity/device-config.test.sh:84-88`.

## Acceptance criteria

1. **Polyfork assets land with provenance, without a runtime importer.** At least six visible
   scene assets derive from downloaded Polyfork GLBs, including a railway/structural piece and
   at least three distinct dressing families (tree/vegetation, fence/platform furniture,
   sign/light/desk prop). Source asset id, title, source URL, downloaded SHA-256, shipped
   Unity-native file, triangle count, and conversion command/version are recorded under
   `unity/Assets/Art/Polyfork/`. GLBs are converted offline with installed Blender; the Unity
   package manifest is byte-unchanged and the built game makes no network request for art.
   Imported renderers use the Cat Metro shader family, not embedded foreign shaders.

2. **Collider-free construction fixes the recorded device finding in Lane 1A's files.** There
   are zero `GameObject.CreatePrimitive` calls in `Presentation/Board/**` and
   `Presentation/Cameras/**`; BoardView and CauseCameraController build/instantiate meshes with
   `MeshFilter` + `MeshRenderer` and add no `Collider`. A PlayMode test exercises nodes, edges,
   switches, onboarding rings, a live train, and the cause ring and asserts zero colliders in
   both owned render trees. The declared criterion-5 shell gate is re-authored so zero total
   primitives is a legal final state after Lane 1B, while its mismatched-bind negative fixture
   still proves the gate fires. Lane 1B retains ownership of WavePreviewStrip's one call.

3. **The board reads as a handcrafted desk diorama.** The real L001 runtime tree visibly
   includes: a warm wood desk/baseboard with a bevel/cardboard edge; raised Cream Card track bed
   with Ink Navy rails/ties; a depot shed; platform furniture; a chunky Metro Teal switch base
   and Ticket Orange direction arm; trees, fences/rocks, and desk-margin props. Gameplay objects
   keep one `BoardElementId` per authored node/edge/switch and remain at their authored world
   coordinates, so the existing render-fidelity and input tests stay byte-unchanged and green.

4. **Palette is exact and centrally testable.** One presentation palette maps the twelve names
   to the exact product-spec hexes: `F2EAD9`, `FAF6EC`, `22304A`, `131C30`, `3BAFA8`,
   `F08A3C`, `E15A47`, `3E7CC9`, `EFC13D`, `4FA36A`, `A06BD8`, `D93A2B`.
   A new test asserts the byte values. Line colours are never retuned; Ink Navy outlines/keylines
   are used for contrast per TG-1's non-palette-changing option, pending the human taste gate.

5. **Stations use colour + symbol together.** Every station renders a raised rounded platform,
   an Ink Navy keyline, and a visible plate carrying the exact accepted-line symbol. A component
   inventory ties line code, symbol, and station silhouette/role to the same station root; a
   color-only station mutation turns the new test red. Existing zero-instructional-text laws
   stay green because the marks are single glyphs, not prose.

6. **Commuters are recognizable cats and never colour-only.** Every live train renders as a
   compact toy train/cat commuter with ears, face/body silhouette, contact shadow, and a visible
   tag bearing the exact line symbol. Red/blue/yellow/green/wild map to distinct silhouette ids
   (round-eared tabby, slim siamese, fluffy longhair, sleek shorthair, bent-ear scruffy) even
   though wild remains construction-guarded in current content. A new PlayMode inventory test
   exercises a live shipped commuter and construction tests cover all five mappings; deleting a
   tag/symbol makes the test red.

7. **The switch stays readable and playable.** The switch is a collider-free chunky lever with
   a Metro Teal base, Ticket Orange arm, Ink Navy outline/ring, and its existing immediate
   committed-route behavior. The onboarding static ring/pulse and motion-off twin remain live;
   existing tap, retry, and teach-affordance suites pass without edits. A new visual-structure
   test pins the accent colours and arm/base hierarchy.

8. **Warm light and contact shadows honor the pinned mobile posture.** The Game scene carries a
   warm directional key and cool ambient/fill treatment, all visible meshes use one URP shader
   family, and cats/props/platforms receive cheap blob/baked contact-shadow geometry. The existing
   mobile policy remains true: realtime main-light shadows OFF, additional lights disabled, no
   depth/opaque texture, HDR off, one renderer, SRP Batcher on. No edit weakens
   `DeviceConfigTests.PipelineConfig_PinnedMobileSane`.

9. **Build shim, checks, and rendered evidence are complete.** Commit the previously untracked
   `unity/Assets/Editor/CatMetroCliBuild.cs` and `.meta`. Run focused RED→GREEN tests, then full
   EditMode, PlayMode, `bash scripts/check.sh`, `bash scripts/test.sh`, and
   `bash scripts/build.sh`. Capture at least one real editor Playing frame and one alternate
   gameplay frame under `evals/results/ux/art-diorama-2026-08-09/`; build a development APK with
   `CatMetroCliBuild.BuildAndroid`, record its SHA-256 and `aapt` debuggable proof, install it on
   the Pixel, and capture at least two device PNGs showing the board and a live cat/switch state.
   Evidence is inspected, not merely emitted.

## Demo check

The objective signal is all of the following, in order:

1. Focused new EditMode/PlayMode art tests pass, with named mutations for collider absence,
   station symbol presence, commuter tag presence, and palette bytes captured RED then reverted.
2. `bash scripts/check.sh && bash scripts/test.sh && bash scripts/build.sh` is green.
3. The pinned Unity editor completes the full EditMode and PlayMode suites with zero failures.
4. The evidence directory contains inspected real editor and Pixel frames; the dev APK exists,
   is debuggable, carries the dev seam token, and its SHA-256 is recorded.

## Scope and ownership

**Owned/in scope:** `unity/Assets/Art/**` (new),
`unity/Assets/Resources/Materials/Greybox.mat`, `unity/Assets/Prefabs/**` (new),
`unity/Assets/Scripts/Presentation/Board/**`,
`unity/Assets/Scripts/Presentation/Cameras/**`, `unity/Assets/Scenes/Game.unity`,
`unity/ProjectSettings/**`, URP/lighting assets, the exact build shim,
`tests/unity/device-config.test.sh:84-88`, new tests and their metas, lane evidence under
`evals/results/`, this frozen contract/status log, and exactly one ART-DIORAMA row plus the
Lane-1A collider-debt half in `state/PROJECT_STATE.md` at merge time.

**Forbidden/out of scope:** `Scripts/Domain/**`, `Scripts/Content/**`,
`Scripts/Bootstrap/**`, `Presentation/Hud/**`, `Presentation/Screens/**`,
`Presentation/Input/**`, `Presentation/Strings/**`, `Presentation/Diagnostics/**`,
`Resources/Materials/UiChrome.mat`, `unity/Assets/UI/**`, `content/**`, existing Unity test files,
other shell tests, package/dependency changes, monetization, audio/UI restyling, level authoring,
and the persistentDataPath device finding. Lane 1B owns WavePreviewStrip's primitive/collider.

## Assumptions and explicit dispositions

- **A-ART-1 — REST fallback authorized.** Polyfork ToolSearch/MCP was absent and reported to the
  human immediately. The human then authorized calling the root `.env` key and using the public
  site/API. The implementation may use Polyfork's documented authenticated REST `/api` + `/dl`
  path as a transport substitute. The key is never printed, copied, staged, or embedded.
- **A-ART-2 — base colours vs line colours.** The absolute "colour never appears without its
  symbol" rule is applied to the five line colours, exactly matching product_spec §7's normative
  wording. Cream/navy/teal/orange are the base art/chrome palette and do not acquire arbitrary
  line symbols.
- **A-ART-3 — contact shadow means the specified low-tier-safe limb.** Existing tests and the
  product spec both pin realtime shadows off. This contract uses blob/baked AO/contact geometry;
  it does not flip URP realtime shadows on to mimic the concept render.
- **A-ART-4 — no camera/Bootstrap exception.** `GameRoot`'s camera construction and exact rest
  position stay untouched. Camera presentation may only be adjusted inside the owned
  `CauseCameraController` while preserving all existing failure-framing tests. Art composition
  must succeed without a Bootstrap edit.
- **A-ART-5 — generated-asset fallback.** The human authorized up to 2,000 Meshy credits and
  2,000 Tripo credits for this lane. They may be used only if the Polyfork catalogue lacks a
  required silhouette (not for speculative variants); every spend and license/provenance record
  is logged. Polyfork remains the primary visible asset source.
- **A-ART-6 — first-commit planning artifact.** `state/SPRINT.md` does not exist on main, the
  launch branch, or repository history. The parallel-push handoff is the shared single-writer
  intent plan; this file is the established per-lane frozen-contract artifact, avoiding a new
  cross-lane plan-file collision.

## External gates and stop conditions

1. **Polyfork ADR gate remains OPEN.** The launch handoff assigns new ADR ownership outside
   Lane 1A while requiring an asset-license ADR before these assets merge. Lane 1A does not
   silently claim `docs/adr/**`; no merge may be armed until an approved ADR records Polyfork's
   commercial Play-binary license, standalone-redistribution restriction, source-repo handling,
   and the human-authored `.env` custody deviation. If the human reassigns the ADR here, record
   that ruling before touching the path.
2. Any required fix crosses a forbidden ownership path or needs a new Unity/runtime dependency:
   stop and report; do not widen the contract.
3. Any asset's commercial-game license or source provenance cannot be established: do not ship
   it; replace it or stop.
4. Any line-coloured station/commuter cannot carry the matching symbol at gameplay size: cut or
   replace it; never waive the colourblind rule.
5. Three attempts fail to build, render, install, or capture for the same cause: report the
   hypotheses and ask the single unblocking question.
6. HC-25: push/opening a PR is allowed, but no merge is armed or completed without the human's
   fresh in-chat merge word for this lane after evidence and review are complete.

## Status log

- 2026-08-09 — Polyfork MCP absent; human notified immediately. Human authorized REST-key use
  plus up to 2,000 Meshy and 2,000 Tripo credits. Public/authenticated Polyfork API verified the
  account plan as `founders`; no key material entered output.
- 2026-08-09 — contract frozen at `1a1bf09`; implementation RED is next.
- 2026-08-09 — implementation milestone: Board/Cameras primitive-free gate RED→GREEN; five
  focused diorama PlayMode tests and five asset/palette EditMode tests green. Nine Polyfork GLBs
  were hash-recorded, palette-remapped offline, converted to collider-free FBX/prefab wrappers,
  and wired into the real Game scene. Editor frames inspected. Full EditMode 826/826; first full
  PlayMode 141/142 exposed the existing URP shader-namespace pin, then focused DeviceConfig 5/5
  green after the custom shader retained its implementation under that namespace. APK/device,
  mutation, full rerun, and independent-review legs remain.
- 2026-08-09 — first independent review returned NOT MERGEABLE and found stale identity on an
  L002 reused simulation slot, a weakened WavePreview primitive/bind count, missing camera
  pitch/fit, sharp gameplay cubes, a continuous rather than 3-step/rim shader, incomplete
  accessibility evidence, and an under-specified provenance receipt. Review-driven tests went
  RED first; the corrected focused suites are EditMode 6/6 and PlayMode 8/8. Render inspection
  rejected the first capsule-based rounding attempt and drove the `RoundedBox12` bevel.
  Corrected full suites are EditMode 827/827 and PlayMode 145/145. Golden, colour-vision,
  grayscale, and five-at-64px sheets are inspected. Corrected `fa77af8` dev APK built
  successfully (`4e465e…`, debuggable, development seams present).
- 2026-08-09 — same-round review caught display-resize projection staleness, remaining sharp
  depot/cat exterior cubes, and an initially misread Unity portrait enum. Each received a named
  RED then GREEN test; implementation head `4e1af6b` is independently code-clean. Final gates:
  EditMode 828/828, PlayMode 146/146, shell test 16/16, check/build PASS. Rounded frames were
  regenerated and inspected. Final `4e1af6b` dev APK built (`b59c82…`, debuggable, arm64,
  development seams present). Pixel, evidence-only exact-head review, Polyfork ADR, human rater,
  and HC-25 legs remain open.
- 2026-08-09 — exact-head security review found a LOW predictable `/tmp` output in the manual
  Polyfork orientation-sheet tool. A named test went RED, then unique UUID directory, collision
  and reparse rejection, and create-new/exclusive output made it GREEN. Full exact-head suites:
  EditMode 829/829 and PlayMode 146/146. Draft PR #65 is open; GitHub rejected both workflows
  before runner assignment because of account billing/spending-limit state. Pixel, Actions,
  Polyfork ADR, human rater, evidence-only review, and HC-25 remain open.
- 2026-08-09 — human explicitly reassigned the Polyfork license/source-custody ADR draft to
  Lane 1A and requested an ADR-0010-shaped proposal for in-chat signature. This ruling is the
  narrow `docs/adr/**` ownership exception required by external gate 1; ADR approval and HC-25
  remain separate human gates.
- 2026-08-10 — human signed all seven ADR-0011 propositions against proposal `feb78a1`, explicitly
  excluding HC-25; signature record `33a8d6c` is pinned. Root `.env` custody is mode `0600`,
  ignored, and untracked. The recorded APK installed and cold-launched successfully on Pixel 9 Pro
  `48121FDAP006X4`; two console-free 960x2142 live-cat frames were inspected and recorded. Scoped
  logcat has zero fatal/crash events and zero Board/Cameras collider stacks; its two `MeshCollider`
  errors both resolve exclusively to Lane 1B's frozen `WavePreviewStrip` debt. Pixel and Polyfork
  ADR gates are closed; CM-R21 ruling/evidence, exact-head reviews, post-push CI, and HC-25 remain.
- 2026-08-10 — human TG review rejected the installed composition while affirming the asset
  selection. The committed Gemini image is now the golden target. Required correction: a low
  30–40-degree-above-horizon three-quarter board camera with visible desk edge; cats scaled to
  roughly 1.5 track widths and seated head-up in open train cars; Cream Card/Warm Paper board and
  wood treatment with Ticket Orange restricted to accents; warm low-angle key, soft authored
  contact shadows, SSAO and subtle vignette; then a new APK and Pixel evidence. The prior editor,
  accessibility, and Pixel renders are baseline-only and cannot close TG or CM-R21. The cream
  rounded preview strip with cat faces plus symbols is routed to Lane 1B because `Hud/**` remains
  forbidden to Lane 1A. HC-25 remains deliberately closed until all corrected evidence and review
  gates close.
