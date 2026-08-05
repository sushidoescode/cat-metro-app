# CONTRACT CM-C2b-DEVFIX — Device config: URP restoration, frame-rate policy, shipped greybox material

**Status: FROZEN 2026-08-05 at branch anchor (post-#26/#27 main), branch
`task/CM-C2b-DEVFIX-device-config`.** Revision 1's Q-DEVFIX-1 is RESOLVED (restore URP).
**Q-DEVFIX-2 consent RATIFIED YES** (human sequencing ack, in-session 2026-08-05: the 7
Presentation bind lines land before the UX lane's chrome work). **R-1 (TextMesh under URP)
CLOSED pre-freeze by the spike/urp-textmesh worktree**: 17,442 glyph pixels rendered under an
active URP asset (PNG evidence in the session scratchpad, coordinator-verified visually); the
magenta probe read 0 in-editor, consistent with the strip-only-on-device diagnosis. Stop
condition 8 remains armed for the real restoration regardless.

**Human rulings folded in (2026-08-05, in-session):**
- **Q-DEVFIX-1 RESOLVED — RESTORE URP.** Conform to `docs/adr/0004-toolchain-and-sdk-version-pins.md:37`;
  **no ADR change**. The coordinator independently verified the finding first: both
  `customRenderPipeline` guids resolve to nothing under `unity/Assets/**`,
  `GraphicsSettings.asset:40` is `m_CustomRenderPipeline: {fileID: 0}`, and no
  `UniversalRenderPipelineAsset` exists in the tree. URP restoration is therefore **first-class
  scope here** (criteria 1-2), not a follow-up.
- **1%-low is PINNED: mean-of-worst-1%** (the stricter of the two readings the artifact recorded,
  `evals/results/device/c2b-crit8/ARTIFACT.md:52-56`). Criterion 6's packet cites it.
- Ratified elsewhere, context only (not this contract): the dev-only failable-level override and
  the Pixel-9-Pro class-deviation recording.

**Branch:** `task/CM-C2b-DEVFIX-device-config` (one contract, one branch).
**DEPENDS-ON:** CM-C2b (#21), CM-C3 (#22), device artifact (#24/#25) merged.
**PARALLEL-SAFETY:** `Bootstrap/DevCapture/**` is in flight on `task/CM-C3-DEVCAP-frame-capture` —
untouchable here. `Presentation/**` is the UX lane's tree (`state/handoffs/SESSION-HANDOFF-ux.md:28-33`);
this contract takes **7 lines** of it — rider Q-DEVFIX-2.
**Origin:** `ARTIFACT.md:94-96` (F-DEV-1, F-DEV-2) plus the render-pipeline finding this contract
now repairs.

### Goal

The device-config defects behind criterion 8 are fixed in **shipped** code and **committed**
assets: the project runs the render pipeline its ADR pins (URP, restored and provably active with
explicitly pinned mobile-sane settings), a boot frame-rate policy lets the target govern
(F-DEV-1), and a committed URP material is bound to every runtime-created renderer so the board
renders in colour on device (F-DEV-2). Each is proven by an editor-runnable check that is RED on
today's `main` and GREEN after. Criterion 8 itself stays **OPEN** and HUMAN-VERIFIED: this
contract ships the fix and hands over a re-measure packet; it never claims a device number.

### Spec reference

`docs/adr/0004-toolchain-and-sdk-version-pins.md:37` (URP, linear colour, Vulkan-first + GLES3 —
the pin being conformed to) · `evals/results/device/c2b-crit8/ARTIFACT.md:57-69` (both diagnoses),
`:85-91` (re-measure protocol), `:43-56` (window miss; the now-pinned 1%-low) ·
`state/handoffs/CM-C2b-frozen-contract.md:71-76` (criterion 8's budgets, HUMAN-VERIFIED) ·
`docs/prd/PRD.md` CM-R52 (perf), CM-R21.1 / A-C2b-3 (colour **plus** symbol) · `docs/adr/0003-*`
(Bootstrap composition root; assembly layering) · `docs/adr/0007-*` (no Addressables — `Resources/`
is the sanctioned inclusion route; precedent `Presentation/Strings/UiStrings.cs:23`).

### Acceptance criteria (6)

1. **URP is restored and provably ACTIVE (RED on today's `main`).**
   Exactly **one** URP pipeline asset and **one** renderer asset are committed under
   `unity/Assets/Settings/` (+ `.meta` for each, + the folder `.meta`), **created through the
   pinned editor** (6000.3.16f1 / URP 17.5.0) — never hand-authored: the asset schema is
   version-specific and a hand-rolled one is a silent-default factory. **`GraphicsSettings` holds
   the single reference** (`m_CustomRenderPipeline` → the committed asset) and **both dangling
   quality-level overrides (`unity/ProjectSettings/QualitySettings.asset:50,103`) are CLEARED to
   `{fileID: 0}`** — one pipeline configuration, one reference, and no level able to drift from
   it, on an Android-only title whose PC level is already excluded from Android (`:113-115`).
   Rationale for clearing rather than repointing is in notes §1b (alternative R1 rejected).
   *Check (RED before):* a PlayMode test asserting `GraphicsSettings.currentRenderPipeline` — the
   call that already resolves any quality-level override — is **non-null**, is a
   `UniversalRenderPipelineAsset`, and its `name` equals the committed asset's name; and that
   `QualitySettings.renderPipeline` is **null**, proving no level overrides the project pipeline.
   Today `currentRenderPipeline` is null, so the test cannot pass without the restoration. Plus
   two fail-closed gates in the new wrapper `tests/unity/device-config.test.sh`: (i) **every**
   `customRenderPipeline:` line in `QualitySettings.asset` is `{fileID: 0}`, and (ii) the
   `m_CustomRenderPipeline` guid in `GraphicsSettings.asset` **resolves to a committed `.meta`
   under `unity/Assets/`** — with a negative fixture carrying an unresolvable guid. *(Gate (ii) is
   what would have caught the original defect; together they are the most durable thing in this
   contract.)*

2. **The pipeline configuration is PINNED, not inherited — no implicit default survives review.**
   The committed assets carry these values, each chosen for a greybox board with **zero lights**
   (`unity/Assets/Scenes/Game.unity:166-171`) on two shipped graphics APIs (Vulkan + GLES3,
   `ProjectSettings.asset:542`):

   | Setting (URP asset) | Pinned | Why |
   |---|---|---|
   | HDR | **off** | no bloom/tonemap; HDR forces a wider colour buffer = tile bandwidth |
   | MSAA | **disabled (1×)** | cubes and quads; MSAA is pure bandwidth here |
   | Render scale | **1.0** | no hidden resolution scaling under a frame budget |
   | Depth texture / Opaque texture | **off / off** | each is an extra pass or a full-screen copy |
   | Main light | **Per Pixel, shadows OFF** | nothing casts; the shadow map is the mobile cost |
   | Additional lights | **Disabled (0 per-object)** | no lights exist |
   | Shadow cascades | **n/a (shadows off)** | — |
   | Colour grading | **LDR** | matches HDR-off; no LUT cost |
   | SRP Batcher | **on** | the one free win |
   | Dynamic batching | **off** | superseded by SRP Batcher |
   | **Use Adaptive Performance** | **off** | **F-DEV-1 interlock:** an active provider may govern frame rate/render scale at runtime and silently defeat criterion 3 |
   | Renderer list | **exactly one**, default index 0 | one configuration, no drift |

   | Setting (renderer asset) | Pinned | Why |
   |---|---|---|
   | Rendering path | **Forward** | Forward+/Deferred buy nothing without lights and cost on tilers |
   | **Renderer Features** | **EMPTY list** | no SSAO, no decals, no full-screen passes |
   | Depth priming | **Disabled** | an extra depth pass over ~50 opaque primitives |
   | Intermediate texture | **Auto** | do not force an offscreen target |

   Colour space is already **Linear** (`ProjectSettings.asset:50`, `m_ActiveColorSpace: 1`) — it
   satisfies ADR-0004:37 and **must not change**. URP's global settings file already resolves
   consistently (`GraphicsSettings.asset:61` → `UniversalRenderPipelineGlobalSettings.asset.meta`
   guid `f181bc04…`; its default volume profile → `DefaultVolumeProfile.asset.meta` guid
   `6a32df83…`), so restoration adds no new global-settings work; the PR states that it verified
   this rather than assuming it. **Render Graph stays ON** (compatibility mode OFF —
   `UniversalRenderPipelineGlobalSettings.asset:205-209`); the obsolete `m_EnableRenderGraph: 0`
   field at `:32` is legacy, and the PR must say which one governs in 17.5.0 so the reviewer is
   not misled by the apparent contradiction.
   *Check:* assert each pinned value through the **typed** asset API where URP exposes it
   publicly (add `Unity.RenderPipelines.Universal.Runtime` to the PlayMode test asmdef); for any
   property URP does not expose, assert it with a grep on the committed YAML in the wrapper. **The
   PR pastes both committed assets in full** so the reviewer diffs intention against accident —
   an unpinned default is a future device surprise, and that is the failure mode this criterion
   exists to prevent.

3. **A boot frame-rate policy ships in Bootstrap, not behind a dev guard, and the vsync posture is
   guarded (F-DEV-1).**
   A shipped `CatMetro.Bootstrap.FramePolicy` exposes `public const int TARGET_FPS = 60` and an
   `Apply()` that forces the engine vsync count to 0 **only if it is not already 0**, then sets
   the engine frame-rate target to `TARGET_FPS`. `GameRoot.Wire` (`Bootstrap/GameRoot.cs:105-129`)
   calls it — one line covering **all three** boot paths (scene-boot `Awake:84-90` →
   `InitializeFromSeam:92-103` → `Wire`; `Launch:48-63`; `LaunchWith:67-82`). No `#if`, no
   editor-only branch, no development-build check anywhere in the file. **URP interplay, stated
   explicitly:** URP introduces no vsync setting of its own — `vSyncCount` remains a
   QualitySettings property and still decides whether the target governs — and criterion 2's
   Adaptive-Performance pin closes the only other runtime frame-rate governor.
   *Check (RED before):* a PlayMode test that **resets the engine target to -1 in `[SetUp]`**
   (a value leaked by an earlier test would otherwise make the assertion vacuous), launches, then
   asserts the target reads `60` and the vsync count reads `0`; today it reads `-1`. Plus wrapper
   gates: (a) the count of `vSyncCount:` lines in the committed `QualitySettings.asset` equals the
   count of `vSyncCount: 0` lines and is ≥1 — **this must still hold after criterion 1 edits that
   file** — (b) `Android: 0` remains under `m_PerPlatformDefaultQuality` (`:117-118`), (c) zero
   `.cs` under `unity/Assets/Scripts` assigns a non-zero vsync count, (d) zero
   conditional-compilation tokens (`#if`, `#elif`, `#else`, `UNITY_EDITOR`, `isDebugBuild`,
   `isEditor`) in the two `.cs` files this contract adds. Negative fixtures prove (a) and (d)
   fire. Setting the vsync count to 1 on the 120 Hz session device would present 120 fps and bust
   thermals — that is why it is banned in code and gated in the asset.

4. **A committed URP greybox material exists and loads through the device path (F-DEV-2, half 1).**
   `unity/Assets/Resources/Materials/Greybox.mat` (+ `.meta`) is committed under a `Resources`
   root — the sanctioned unconditional-inclusion route (no Addressables, ADR-0007) — with shader
   **`Universal Render Pipeline/Unlit`**, now unconditional (criterion 1 makes URP the active
   pipeline). Unlit is required, not preferred: the scene has zero lights, and criterion 2 pins
   main-light shadows off and additional lights disabled, so a lit shader would render
   ambient-only and pay for variants nothing uses. Being a `Resources` asset, it is also what
   keeps URP/Unlit's variants alive under URP's own stripping
   (`UniversalRenderPipelineGlobalSettings.asset:20-30`, `m_StripUnusedVariants: 1` — variants
   survive precisely because an *included material* uses them).
   *Check (RED before):* a PlayMode test loading it through the same path a device build takes —
   `Resources.Load<Material>("Materials/Greybox")` — asserting non-null, `shader != null`, and the
   pairing invariant, now one-directional: **`GraphicsSettings.currentRenderPipeline` is non-null
   AND the shader name starts with `Universal Render Pipeline/`.** (Keeping the pairing assertion
   means a future pipeline change cannot silently re-magenta the board.) Today the load returns
   null. Plus wrapper asserts: `.mat` + `.meta` exist at that exact path, `m_Name: Greybox`, and
   the `m_Shader` line exists and is **not** `{fileID: 0}`.

5. **Every runtime-created renderer binds it, and text survives the pipeline switch (F-DEV-2,
   half 2).**
   All seven `GameObject.CreatePrimitive` sites under `Presentation/**` bind
   `GreyboxMaterial.Shared` to `sharedMaterial` **before** any `.material.color` write, so every
   existing colour path keeps working unchanged (`Renderer.material` instantiates from
   `sharedMaterial`): `Board/BoardView.cs:65,101,123,132,177`,
   `Cameras/CauseCameraController.cs:84`, `Hud/WavePreview/WavePreviewStrip.cs:33`. No public
   signature changes, no test edits, no scene edit. **Second half — the regression the restoration
   could introduce:** labels and banners are `TextMesh` on the built-in font material
   (`BoardView.cs:76-83`, `Hud/BannerView.cs:22`), and they render correctly on device **today,
   under the built-in pipeline** (`ARTIFACT.md:69`).
   *Check (RED before):* one PlayMode test exercising **all four** creation surfaces and proving
   each was exercised before asserting — launch (board), advance ~12 ticks and render a frame
   (assert ≥1 `BoardElementId` of kind `train`), call the public `CauseCam.FrameNode(...)` (assert
   the ring is non-null), read the strip (assert 2 chips) — then assert that **every** `Renderer`
   under the root whose GameObject carries no `TextMesh` has `sharedMaterial != null` and
   `sharedMaterial.shader == Resources.Load<Material>("Materials/Greybox").shader`; today every one
   of them carries the engine default. Separately assert every `TextMesh` renderer still has a
   non-null `sharedMaterial` with a non-null, supported shader. **Honest limit:** that assertion
   proves the text material exists and compiles, **not** that URP draws it — so the PR carries a
   human editor Play-Mode screenshot showing legible labels, and criterion 6's device screenshot
   must show them too. If labels do not render under URP in the editor, stop condition 8 fires.
   Plus a fail-closed static gate:
   `count(GameObject.CreatePrimitive) == count(GreyboxMaterial.Shared)` over
   `unity/Assets/Scripts/Presentation` (7 = 7 today; any future unbound primitive turns it red),
   with the same counting code run over `tests/fixtures/device-config-bad/`, where it must come
   out UNEQUAL, proving the gate is live.

6. **Criterion 8 is handed back with a re-measure packet — it is NOT closed here (HUMAN-VERIFIED).**
   The PR body carries a packet stating: (a) **editor-proven** — URP is active and configured as
   pinned, the boot policy value, the material's existence and pipeline pairing, no runtime
   renderer depends on the engine default; (b) **device-only** — that URP/Unlit survives release
   stripping in an IL2CPP/ARM64 APK, that labels draw, and every frame-time number; (c) the
   re-measure steps from `ARTIFACT.md:85-91`, amended in exactly three ways: **the re-measure runs
   ONLY on the URP build** (the 30 fps / 33 ms baseline was captured on the built-in pipeline and
   is void as a comparison — the pipeline change alters frame cost independently of the cap), a
   **screenshot first** (`adb exec-out screencap -p`: coloured greybox **and** legible labels ⇒
   F-DEV-2 and the URP text risk closed; magenta ⇒ stop before spending a frame capture), and
   **1%-low = mean-of-worst-1%, now pinned by the human** (`ARTIFACT.md:52-56`), computed from a
   raw per-frame table over ≥60 s presented; (d) the decisions still the human's: the window
   composition call (L001 as shipped offers 6.25 s of active sim and cannot loop or reach
   FailureReview — `ARTIFACT.md:43-48`, F-DEV-3), Q-DEVFIX-3 and Q-DEVFIX-4.
   *Check:* a reviewer confirms all four parts are present and the artifact is linked; and a grep
   confirms **no** test under `unity/Assets/Tests/**` asserts a frame-time budget, a device model,
   or an fps figure — an agent-authored device claim is a failed review.

### Scope boundary

**In scope — complete file table.** Nothing outside this table changes.

| Path | Action | Size | Why |
|---|---|---|---|
| `unity/Assets/Settings/` (+ folder `.meta`) | ADD | — | criterion 1 |
| `unity/Assets/Settings/CatMetro_URP.asset` (+ `.meta`) | ADD | editor-generated, then pinned | criteria 1-2 |
| `unity/Assets/Settings/CatMetro_Renderer.asset` (+ `.meta`) | ADD | editor-generated, then pinned | criteria 1-2 |
| `unity/ProjectSettings/GraphicsSettings.asset` | EDIT | **`m_CustomRenderPipeline` only** (`:40`) | criterion 1 — `m_AlwaysIncludedShaders` (`:29-36`) and everything else stay byte-identical |
| `unity/ProjectSettings/QualitySettings.asset` | EDIT | **the two `customRenderPipeline` lines only, cleared to `{fileID: 0}`** (`:50`, `:103`) | criterion 1 — `vSyncCount` (`:32`,`:85`) and `m_PerPlatformDefaultQuality` (`:117-118`) stay byte-identical, and criterion 3's gate proves it |
| `unity/Assets/Scripts/Bootstrap/FramePolicy.cs` (+ `.meta`) | ADD | ~14 lines | criterion 3 |
| `unity/Assets/Scripts/Bootstrap/GameRoot.cs` | EDIT | **+1 line** in `Wire` (`:105-129`) | criterion 3 |
| `unity/Assets/Resources/Materials/Greybox.mat` (+ `.meta`) | ADD | 1 asset | criterion 4 |
| `unity/Assets/Scripts/Presentation/Board/GreyboxMaterial.cs` (+ `.meta`) | ADD | ~16 lines | criterion 5; mirrors `UiStrings.cs:20-32` |
| `unity/Assets/Scripts/Presentation/Board/BoardView.cs` | EDIT | **+5 lines** (after `:72`, `:101`, `:123`, `:132`, `:181`) | criterion 5 — rider Q-DEVFIX-2 |
| `unity/Assets/Scripts/Presentation/Cameras/CauseCameraController.cs` | EDIT | **+1 line** (before `:91`) | criterion 5 — rider Q-DEVFIX-2 |
| `unity/Assets/Scripts/Presentation/Hud/WavePreview/WavePreviewStrip.cs` | EDIT | **+1 line** (after `:45`, before `:47`) | criterion 5 — rider Q-DEVFIX-2 |
| `unity/Assets/Tests/PlayMode/CatMetro.Tests.PlayMode.asmdef` | EDIT | **+1 reference** (`Unity.RenderPipelines.Universal.Runtime`) | criterion 2's typed asserts; URP 17.5.0 is already in `unity/Packages/manifest.json:6` — **no new dependency** |
| `unity/Assets/Tests/PlayMode/Device/DeviceConfigTests.cs` (+ `.meta`) | ADD | ~150 lines | criteria 1-5 — auto-discovered by the existing editor half (`tests/unity/editmode.test.sh:95-106`), which is why **no existing wrapper is edited** |
| `tests/unity/device-config.test.sh` | ADD | ~90 lines | always-on gates; discovered by `scripts/test.sh:18` |
| `tests/fixtures/device-config-bad/` | ADD | 3 small files | negative fixtures: unresolvable pipeline guid, `vSyncCount: 1`, unbound primitive |
| `state/handoffs/CM-C2b-DEVFIX.md` | ADD | handoff/status log | session record |
| `state/PROJECT_STATE.md` | EDIT | 1 appended line, **on merge only** | two sessions append in parallel |

**Explicit non-goals / forbidden edits:**
- **No edits to any existing `tests/unity/*.test.sh` wrapper.** New PlayMode tests are discovered
  by the existing editor half; the new always-on gates live in a new file.
- **No `Bootstrap/DevCapture/**`** (in flight on another branch), and no clock tokens written
  anywhere near it. No `Domain/**`, `Content/**`, `Application/**`, no importer, no schema change.
- **No `unity/Assets/Scenes/Game.unity` edit.** The material reaches the build through
  `Resources/`, which works identically for the scene-boot path and the factory paths; a
  scene-serialized reference would be null in every test.
- **No colour-space change** (`m_ActiveColorSpace: 1` already conforms), **no `androidUseSwappy`
  change** (`ProjectSettings.asset:72`, Q-DEVFIX-3), **no `m_AlwaysIncludedShaders` change** —
  Always Included Shaders is evaluated and rejected (notes §2b), and pruning the stock list risks
  the sprite/UI/text shaders the labels depend on.
- **No second URP asset, no per-tier pipeline variants, no renderer features, no volume/post
  stack, no lights, no art pass.** Unlit changes the greybox from ambient-shaded to flat authored
  colour — a visible change the PR must name; TG-1 does not bite before the art pass (CM-C3 recut,
  evaluator D11).
- **No writes to immutable paths** (`tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `state/trust.json`, `evals/` except `evals/results/`, never
  `evals/results/attested/`). No `.github/**`, no `infra/**`, no monetization paths.
- **No new dependency** (AGENTS.md rule 2): URP 17.5.0 is already in the manifest; the manifest
  does not change. ADR-0004:37 is being **conformed to**, not amended — so no ADR is required for
  the restoration, and the PR says exactly that.
- **No dev-guard on shipped behaviour** anywhere in the diff.

**Live gates this diff runs into (read before writing a line — comments are scanned):**
- `tests/unity/editmode.test.sh:72-75` — exactly ONE Presentation file may name the input package,
  and the token list (`EventSystems`, the pointer/drag handler interface names, the touch APIs,
  the mouse-callback prefix) is banned **in comments too** across Presentation *and* Bootstrap.
- `tests/unity/editmode.test.sh:53-54` — the engine's persistent/cache path APIs may appear only
  under `Bootstrap/**`; do not name them elsewhere, not even in prose or a grep pattern.
- `tests/unity/editmode.test.sh:67-68` — no render-side tree may reach the sim step (aliases too).
- `tests/unity/editmode.test.sh:30-38` — the launcher-manifest / backup-OFF gates read
  `ProjectSettings.asset`; leave `useCustomLauncherManifest: 1` (`:262`) alone.
- `tests/unity/failure.test.sh:9-14,25-29` — no shipped construction of the pinned fail reason; no
  literal UI string (case-insensitive) under `unity/Assets/Scripts`.
- `scripts/check.sh:77` — the JSON type-name setting may be named in exactly one file; never in a
  comment under `unity/Assets/Scripts`.
- `scripts/check.sh:41-42,61-63` — `Tests/EditMode/Pure/**` bans the engine namespace, so **the
  new tests must be PlayMode** (`Tests/PlayMode/Device/`), never `EditMode/Pure/`.
- `scripts/check.sh:31-35` — repo-wide unresolved-token scan; no doubled opening brace followed by
  capitals in the new wrapper (mind the YAML-guid grep patterns).
- `scripts/check.sh:21-27` — every `.sh` under `tests/` is syntax-checked.
- Run `bash scripts/test.sh` only on a committed tree; `git add` by explicit path (the human's
  `.claude/settings.json` is dirty and must never enter the PR).

**Coordination note (parallel lanes):** the UX lane owns `Presentation/**` and has just opened
(`SESSION-HANDOFF-ux.md:67-68`); Bootstrap is a flat deny for that lane until this contract and
CM-C3-DEVCAP merge (`:31-33`). The 7 bind lines must land **before** the UX lane rewrites the
board/camera/HUD files, or one lane rebases. Land this first; tell the UX lane the bind lines and
the pairing gate exist. The restoration also changes what every UX screen renders through — the UX
lane should not start chrome work against the built-in pipeline.

### Assumptions

- **A-DEVFIX-1 (was the blocker; now a ruling).** The tree ships no render-pipeline asset, so the
  runtime falls back to the built-in pipeline today; the human ruled RESTORE URP per ADR-0004:37.
  Criterion 1's assertion is RED on today's `main` by that same finding, independently verified by
  the coordinator. If it is somehow GREEN before any edit, the finding was wrong — stop and report
  before changing anything.
- **A-DEVFIX-2.** Assets under any `Resources` folder are included in the player unconditionally
  and pull their shader (with the variants that material uses) into the build; this is the
  inclusion mechanism, and it is the route `ui.csv` already uses (`UiStrings.cs:23`). No
  Addressables (ADR-0007).
- **A-DEVFIX-3.** `Renderer.material` instantiates from `sharedMaterial`, so binding
  `sharedMaterial` at creation leaves every existing colour write and every existing assertion —
  including `CauseCameraController.RingAlpha` (`:28-29`), which reads back the alpha it stored —
  working unchanged. If an existing test goes red, that is stop condition 3; do not amend the test.
- **A-DEVFIX-4.** `TARGET_FPS = 60` comes from criterion 8's own budget (median ≤16.7 ms). vsync
  stays 0 so the target governs; a vsync count of 1 on a 120 Hz panel would present 120 fps.
- **A-DEVFIX-5.** Inside `namespace CatMetro.Bootstrap` the identifier `Application` binds to the
  project's own `CatMetro.Application` namespace (`GameRoot.cs:2-3`), so the engine's application
  type **must be written fully qualified**; follow `UiStrings.cs:23`'s house style and fully
  qualify engine types in both new files rather than importing the engine namespace.
- **A-DEVFIX-6.** The device re-measure is HUMAN-only (`CM-C2b-frozen-contract.md:71-72`;
  `ARTIFACT.md:8-9`). An agent may not run it, mark it, or infer it.
- **A-DEVFIX-7.** The `.mat` may be hand-authored **if** criterion 4's assertions pass (a wrong
  shader reference yields a null shader and turns the test red — self-proving). The **pipeline and
  renderer assets may not**: they are editor-generated (A-DEVFIX-9), then pinned per criterion 2.
- **A-DEVFIX-8.** URP's global settings and default volume profile already resolve consistently
  (guids verified: `f181bc04…`, `6a32df83…`), so restoration adds no global-settings work. The PR
  records the verification rather than assuming it.
- **A-DEVFIX-9.** Creating the pipeline assets requires one pinned-editor session (6000.3.16f1).
  If that must be agent-driven, it follows the disclosed-shim precedent (`ARTIFACT.md:114-151`):
  untracked, editor-only, full text in the PR, deleted at session end.

### Stop conditions

Defaults apply. Plus:
1. Criterion 1's assertion is GREEN before any edit (i.e. a pipeline is already active) → **stop**;
   the ruling rests on a finding that would then be false.
2. Any criterion appears to need an edit to `Domain/**`, `Content/**`, the importer, an existing
   `tests/unity/*.test.sh` wrapper, the DevCapture tree, or an immutable path → stop.
3. Binding the material needs a public Presentation signature change, a scene edit, or an edit to
   an existing test → stop and report; those ripple into CM-C2b/CM-C3 tests.
4. A device-only fact (un-stripping, label rendering, any frame time) would have to be asserted by
   an agent-runnable test to make a criterion pass → stop; that leg is the human's.
5. Any existing gate (input-surface count, path-API ban, literal-string ban, purity scans,
   manifest/backup gates) goes red because of a new file → stop and report; never edit a wrapper
   to accommodate the diff.
6. Optimized frame pacing, sustained-performance mode, adaptive performance, or a graphics-API
   change appears necessary to meet the median → stop; those are shipped-build ProjectSettings
   decisions for the human (Q-DEVFIX-3).
7. The greybox loses its colour coding under the new material (A-C2b-3: colour **plus** symbol) →
   stop; a uniform board is an information regression, not a rendering fix.
8. **Labels/banners fail to render under URP in the editor** → stop. Routing text through a
   URP-compatible material is ADR-0007's TMP chrome work and belongs to the UX lane, not here.
9. Restoring URP requires editing anything outside this contract's file table — a package manifest
   change, a scene change, a colour-space change, an ADR amendment → stop and report.
10. Q-DEVFIX-2 consent lands as NO → stop before writing criterion 5's binds and re-open the
    mechanism choice with the human (notes §2c is the only alternative, and it ships an
    editor-untestable branch).

---
