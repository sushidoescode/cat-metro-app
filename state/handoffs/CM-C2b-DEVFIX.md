# CM-C2b-DEVFIX — companion notes, revision 2 (mechanism comparison, rationale, open questions)

Companion to `CM-C2b-DEVFIX-draft-contract.md` (revision 2). Evidence-first: file:line for every
claim about the repo, and an explicit label wherever a claim is an inference that only a device
(or a single editor run) can settle.

**Recut 2026-08-05 for the human's in-session rulings:** Q-DEVFIX-1 **RESOLVED — restore URP**,
conforming to `docs/adr/0004-toolchain-and-sdk-version-pins.md:37`, **no ADR change**; the
coordinator independently re-verified the finding before ruling. 1%-low **pinned:
mean-of-worst-1%**. Q-DEVFIX-2 (7 Presentation lines) remains a **rider** — consent in flight,
drafted assuming YES. Criteria count went 5 → **6** (URP restoration and its configuration pin are
now criteria 1 and 2; the old frame-policy and vsync criteria folded into one).

---

## 0. What the tree actually says (read before arguing about mechanisms)

| Fact | Evidence |
|---|---|
| Frame-rate policy is absent from shipped code | no `targetFrameRate` anywhere under `unity/Assets/Scripts` (grep, 0 hits); `ARTIFACT.md:57-63` |
| vsync is off on both quality levels; Android boots level 0 (`Mobile`) | `unity/ProjectSettings/QualitySettings.asset:32,85,117-118` |
| The board is 100% runtime-created — 7 `GameObject.CreatePrimitive` sites, all coloured through `Renderer.material.color` | `Presentation/Board/BoardView.cs:65,101,123,132,177` (+ colour writes `:75,85,86,186`); `Presentation/Cameras/CauseCameraController.cs:84,91`; `Presentation/Hud/WavePreview/WavePreviewStrip.cs:33,45,87` |
| The built scene references **no material at all** — one scene root, the `GameRoot` object, no camera, no light, no renderer | `unity/Assets/Scenes/Game.unity:122-138,166-171` |
| Camera and view tree are created at runtime by the composition root | `Bootstrap/GameRoot.cs:105-129` |
| Presentation **cannot** reference Bootstrap — the dependency runs the other way | `CatMetro.Bootstrap.asmdef:4-10` lists `CatMetro.Presentation`; `CatMetro.Presentation.asmdef:4-10` does not list Bootstrap |
| Presentation already loads committed assets through `Resources` | `Presentation/Strings/UiStrings.cs:20-32` |
| Always Included Shaders is the stock 7-entry default list; **no default render pipeline is assigned** | `unity/ProjectSettings/GraphicsSettings.asset:29-36`, `:40` (`m_CustomRenderPipeline: {fileID: 0}`) |
| Both quality levels point at render-pipeline assets **that do not exist in the tree** | `QualitySettings.asset:50` (guid `5e6cbd92…1858`), `:103` (guid `4b83569d…5dfd`); neither guid appears in any other file, and `unity/Assets/` has no `Settings/` folder — not even an orphan `.meta` (`unity/Assets/*` = 11 entries) |
| URP 17.5.0 *is* installed; its global settings + volume profile sit at the `Assets/` root | `unity/Packages/manifest.json:6`; `unity/Assets/UniversalRenderPipelineGlobalSettings.asset`, `unity/Assets/DefaultVolumeProfile.asset` |
| URP's default **material** set is an editor-scoped resource class | `UniversalRenderPipelineGlobalSettings.asset:156-164` (`UniversalRenderPipelineEditorMaterials.m_DefaultMaterial`) |
| URP's own variant stripping is ON | `UniversalRenderPipelineGlobalSettings.asset:20-30` (`m_StripUnusedVariants: 1`, `m_StripRuntimeDebugShaders: 1`) |
| Colour space is already **Linear** — ADR-0004:37's "linear colour" is satisfied today | `unity/ProjectSettings/ProjectSettings.asset:50` (`m_ActiveColorSpace: 1`) |
| Optimized frame pacing OFF; Android ships Vulkan + GLES3 | `ProjectSettings.asset:72` (`androidUseSwappy: 0`), `:542` (`m_APIs`) |
| ADR pins the render pipeline as URP | `docs/adr/0004-toolchain-and-sdk-version-pins.md:37` |

**Status of the pipeline finding: RULED.** The project has no render-pipeline asset in version
control, so the runtime falls back to **Built-in** while ADR-0004:37 pins URP. The human ruled
**restore URP, no ADR change**, and the coordinator re-verified all three legs of the finding
independently. It is now criteria 1-2 of this contract rather than an escalation.

### The magenta: two candidate mechanisms, and why the chosen fix does not depend on which is true

- **M1 — stripped shader (Built-in active, i.e. today).** Runtime primitives get the engine
  default material, whose shader reaches a player only if something in the build references it.
  Nothing does (`Game.unity:122-171` has no material), so it is stripped and the objects fall back
  to the error shader. The editor is always green because the editor never strips.
- **M2 — editor-only default material (URP active, i.e. after criterion 1).** URP resolves the
  primitive default material through an **editor** resource class
  (`UniversalRenderPipelineGlobalSettings.asset:156-164`), so a player gets no pipeline default
  material at all.

Both explain every observation, including the constraining ones: labels/banners render correctly
on device today (`ARTIFACT.md:69`) because the font shader is in the always-included list
(`GraphicsSettings.asset:29-36`), and geometry/input/win-loop are correct. **After criterion 1 the
project is squarely in M2 territory** — which is the more dangerous of the two, because no amount
of shader inclusion helps when the player never gets a material. The chosen fix (commit a material
*and bind it explicitly*) is the only candidate correct under both, and the only one that stays
correct across the pipeline switch.

---

## 1. F-DEV-1 — frame-rate policy: mechanism comparison

| Option | Verdict | Why |
|---|---|---|
| **(1a) Set the engine frame-rate target to 60 in Bootstrap; leave vsync 0** | **CHOSEN** | Shipped code, one seam, test-assertable (`TARGET_FPS`), matches criterion 8's 16.7 ms median budget. `GameRoot.Wire` (`GameRoot.cs:105-129`) is on **all three** boot paths (`Awake:84-90` → `InitializeFromSeam:92-103` → `Wire`; `Launch:48-63`; `LaunchWith:67-82`), so one call line covers the device path and both test paths. |
| (1b) Set the quality-level vsync count to 1 instead | **REJECTED — the trap the artifact's reviewer named** | On the 120 Hz session device, vsync 1 = present every refresh = 120 fps: double the GPU work, thermal throttling inside the 60 s window, and a median of ~8.3 ms achieved for the wrong reason. It also couples pacing to the panel (60/90/120 Hz devices each read differently). Banned in code and gated in the asset (criterion 3). |
| (1c) Derive the target from the display refresh rate at boot | REJECTED | Ties a shipped budget to the device panel, so criterion 8's single median stops meaning one thing; and it re-introduces 120 fps on the very device the human owns. |
| (1d) Enable optimized frame pacing (`androidUseSwappy: 1`, `ProjectSettings.asset:72`) | **DEFERRED to the human (Q-DEVFIX-3)** | Real and relevant: without the Android frame pacer, a 60 fps cap on a 120 Hz panel is paced by Unity's own timer and can present as alternating 8.3/25 ms intervals rather than a clean 16.7 — against a median budget with ~0.03 ms of margin. But it is a shipped-build ProjectSettings change provable only on a device. Recommend holding it as the named fallback if the re-measure's median misses, rather than changing two variables before the first URP re-measure. |
| (1e) Put the policy behind a development-build guard | REJECTED | Explicitly forbidden: the defect *is* release behaviour. Criterion 3 gates zero conditional compilation in the new files. |

**New under URP — the third frame-rate governor.** URP itself has no vsync setting (`vSyncCount`
stays a QualitySettings property), but a URP asset carries a **Use Adaptive Performance** flag, and
`com.unity.modules.adaptiveperformance` is in the module list (`manifest.json:10`). With a provider
active that subsystem can scale render scale *and* frame rate at runtime — silently defeating
criterion 3 on exactly the device the criterion is measured on. Criterion 2 pins it **off**. This
is the clearest example of why the URP asset's defaults had to be pinned rather than inherited.

**Implementation trap.** Inside `namespace CatMetro.Bootstrap`, the bare identifier `Application`
binds to the project's own `CatMetro.Application` namespace (`GameRoot.cs:2-3`), so
`Application.targetFrameRate` does not compile. Write the engine type fully qualified — house style
already does (`UiStrings.cs:23`).

**Test trap.** The engine frame-rate target is process-global and survives across PlayMode tests, so
a value leaked by an earlier test makes the assertion vacuous: reset it to `-1` in `[SetUp]`. Same
reason the vsync write is conditional (`if != 0`): an unconditional write can dirty
`QualitySettings.asset` in the editor, and this contract must not produce settings churn beyond the
two lines it declares.

---

## 1b. URP restoration — wiring comparison (new decision, revision 2)

| Option | Verdict | Why |
|---|---|---|
| (R1) Recreate the Unity template's two-tier setup: `Mobile_RPAsset` + `PC_RPAsset` + two renderer assets, each quality level pointing at its own | **REJECTED** | Two configurations to keep in sync on an Android-only title whose PC level is already excluded from Android (`QualitySettings.asset:113-115`). Tier drift is precisely the class of rot that produced this defect — the tree had *two* pipeline references and *zero* assets, and nobody noticed for four merged contracts. |
| (R2) One asset + one renderer; **repoint both quality overrides** at it; also set the GraphicsSettings default | REJECTED (was revision 2's first cut) | Correct, but it leaves **three** references to one asset. Each is a place a future edit can dangle, and the gate has to guid-match all three. More moving parts for no capability. |
| **(R3) One asset + one renderer; GraphicsSettings holds the single reference; both quality overrides CLEARED to `{fileID: 0}`** | **CHOSEN** | One reference, one source of truth. `GraphicsSettings.currentRenderPipeline` already resolves overrides, so the runtime assertion is unchanged. It yields a *stronger and simpler* pair of gates: "every `customRenderPipeline:` line is `{fileID: 0}`" (no level may drift) plus "the one GraphicsSettings guid resolves to a committed `.meta`" (no dangling reference). Adding a low-tier asset later is still a one-line override, and the gate then fails loudly and asks for a contract — which is the correct behaviour. |
| (R4) Restore URP in a separate contract, land F-DEV-1/2 first | **REJECTED by the ruling, and rightly** | The material's shader must pair with the *active* pipeline, so shipping the material first would mean shipping a built-in shader and immediately re-magenta-ing the board on restoration. Worse, criterion 8's re-measure would run twice: once on a pipeline the ADR forbids, once on the real one. One contract, one re-measure. |
| (R5) Amend ADR-0004:37 to say built-in | **REJECTED by the human** | Not chosen; recorded so the decision has a visible loser. The pin exists for the mid-tier Android/toon-flat rationale in ADR-0004 and nothing in the evidence argued against it — the assets were simply lost. |

**Why the assets must be editor-generated, not hand-authored.** A `UniversalRenderPipelineAsset`
is a large, version-specific serialized object (17.5.0 stores much of its settings graph by `rid`
reference — see the shape of `UniversalRenderPipelineGlobalSettings.asset:92-427`). Hand-authoring
it would produce exactly the silent defaults criterion 2 exists to prevent, and any field the hand
author omits takes an engine default the reviewer never sees. Generate through the pinned editor,
then pin the values, then paste both assets in the PR.

**Global-settings consistency, verified (not assumed).** `GraphicsSettings.asset:61` maps the URP
global settings to guid `f181bc04520684291991d4863e8471fd`, which is exactly
`unity/Assets/UniversalRenderPipelineGlobalSettings.asset.meta:2`; the default volume profile at
`UniversalRenderPipelineGlobalSettings.asset:226` is guid `6a32df8376682428595fd3a68d5a0b06`, which
is exactly `unity/Assets/DefaultVolumeProfile.asset.meta:2`. **Both resolve.** So the global
settings file is already consistent and restoration adds no work there — it simply becomes live.
Two things in it the PR must state rather than inherit: the obsolete `m_EnableRenderGraph: 0`
(`:32`) versus the governing `RenderGraphSettings.m_EnableCompatibilityMode: 0` (`:205-209`) — the
latter means **Render Graph ON**, the Unity 6 supported path — and `m_StripUnusedVariants: 1`
(`:23,232`), which is what makes criterion 4's committed material load-bearing: under URP stripping,
an *included material* is what keeps its variants alive.

---

## 2. F-DEV-2 — inclusion mechanism comparison, re-scored under restored URP

### (a) Committed material referenced from the build, **not applied at runtime**
**REJECTED, and now more decisively than in revision 1.** Under M1 this could have worked by
accident (a magnet material dragging its shader into the build so the engine default material finds
a compiled shader). **Under URP it cannot work at all**: the player has no pipeline default material
to repair (`UniversalRenderPipelineGlobalSettings.asset:156-164`). The revision-1 objection also
still stands and is fatal on its own: the editor never strips, so "does the magnet work" has no red
state and no green state — only a device build answers, which violates this contract's
red-before/green-after requirement.

### (b) Always Included Shaders (`GraphicsSettings.asset:29-36`)
**REJECTED, on correctness first and cost second.**
- Correctness: including a shader does not change **which material** a runtime primitive gets.
  Under URP the primitive has no usable material at all, so no entry in this list fixes it.
- Cost, quantified: the list compiles **every variant** of the listed shader for every target
  graphics API — here two (Vulkan + GLES3, `ProjectSettings.asset:542`). `Universal Render
  Pipeline/Lit` is the multi-`multi_compile` monster (lightmaps × directional lightmaps × main/
  additional light shadows × cascades × decals × per-object light data × fog × instancing):
  thousands to tens of thousands of variants, minutes-to-tens-of-minutes of build time and
  multi-MB of shader data — for a board with **zero lights**. `Universal Render Pipeline/Unlit` is
  the opposite (a handful of variants), but always-including an unlit shader nothing references
  fixes nothing.
- Secondary: it edits a project-settings file for a benefit the chosen mechanism already delivers
  as a side effect — a `Resources` material pulls in its own shader with exactly the variants that
  material uses. Pruning the stock 7 entries is separately forbidden: the sprite/UI/text shaders in
  that list are what the `TextMesh` labels ride on.

### (c) Bootstrap-owned post-hoc sweep (walk renderers, swap in the committed material)
**REJECTED — the interesting rejection.** It is the only route that keeps `Presentation/**`
untouched, so it deserved a real attempt. It fails on two independent counts:
1. **Lazily created renderers.** Trains are created inside `BoardView.UpdateFrom`
   (`BoardView.cs:177`), driven every frame from `GameRoot.Update:182` — a one-shot sweep after
   `Wire` provably misses them. A `LateUpdate` sweep does catch them in the same frame, so this
   objection alone is survivable (cost: a per-frame non-alloc `GetComponentsInChildren` over ~50
   renderers).
2. **Colour cannot be carried across the swap — and the failure is device-only.** Colours are
   written into a *material instance* (`BoardView.cs:75,85,86,186`; `CauseCameraController.cs:91`;
   `WavePreviewStrip.cs:87`). Reading a colour back off a material is shader-mediated
   (`Material.color` / `HasProperty` / `GetColor`), and on device that shader is exactly what is
   missing. The colour-preservation branch therefore takes one path in the editor (shader present →
   colours preserved → test green) and possibly another on device (colours lost → a uniform board,
   destroying the colour half of A-C2b-3's colour-plus-symbol coding — stop condition 7). **A fix
   whose only failure mode is invisible to every editor test is the same class of defect as the one
   being fixed.**
   - Sub-variants also rejected: re-assigning `Material.shader` in place still loses colour (the
     colour property names differ between pipelines' shaders — this is why Unity ships a material
     upgrader); `MaterialPropertyBlock` is empty because nothing writes one; re-deriving colours in
     Bootstrap needs per-element colour semantics that live in `BoardView.ColorFor/ColorForCode`
     (`:202-224`).
3. **The "architecturally Bootstrap-owned" version is impossible as stated**: Presentation cannot
   reference Bootstrap (`CatMetro.Presentation.asmdef:4-10` vs `CatMetro.Bootstrap.asmdef:4-10`).
   Any provider Presentation can call must live in Presentation or below. This is why Q-DEVFIX-2
   exists at all.

### (d) Constructor/parameter injection — Bootstrap loads the material and passes it in
**REJECTED (bigger, riskier).** Cleanest layering, but it changes three public signatures
(`BoardView.Build:33`, `CauseCameraController.Wire:31`, `WavePreviewStrip.Create:23`), rippling
into existing CM-C2b/CM-C3 PlayMode tests (`Tests/PlayMode/Board/GreyboxTests.cs`,
`FailureTests.cs`) — i.e. it forces edits to tests this contract must not touch, for zero
behavioural gain over (e).

### (e) **CHOSEN — committed `Resources` material (URP/Unlit) + explicit bind at every creation site**
`unity/Assets/Resources/Materials/Greybox.mat`, loaded through a cached Presentation-side provider
mirroring `UiStrings` (`UiStrings.cs:20-32`), bound to `sharedMaterial` at each of the 7
`CreatePrimitive` sites **before** the first `.material.color` write.

Why it wins:
- **Correct under both M1 and M2, and across the pipeline switch.** It supplies the material *and*
  pulls the shader into the build graph; it never depends on what the engine hands a primitive.
- **Falsifiable in the editor, twice.** `Resources.Load` returns null before the asset exists
  (criterion 4, red→green); every renderer carries the engine default before the binds exist
  (criterion 5, red→green). Both use the lookup path a device build takes.
- **Zero behavioural ripple.** `Renderer.material` instantiates from `sharedMaterial`, so binding
  first leaves every existing colour write and assertion — including
  `CauseCameraController.RingAlpha` (`:28-29`), which reads back the alpha it stored — working
  untouched. No signature changes, no test edits, no scene edit.
- **Cheapest possible shader, and now doubly justified.** `Game.unity:166-171` has no light, and
  criterion 2 pins main-light shadows off / additional lights disabled — a lit shader would render
  ambient-only and pay variant cost for nothing. Unlit also renders `ColorFor`'s authored values
  exactly, which is what the colour-plus-symbol coding wants.
- **It is what keeps URP/Unlit alive under URP's stripping** (`…GlobalSettings.asset:23`,
  `m_StripUnusedVariants: 1`): included material ⇒ surviving variants.
- **Static gate is cheap and fail-closed.** `count(GameObject.CreatePrimitive) ==
  count(GreyboxMaterial.Shared)` over `Presentation/**` (7 = 7 today) turns red the moment anyone —
  including the UX lane — adds an unbound primitive.

**The Presentation touch, exactly (rider Q-DEVFIX-2).** 7 inserted lines across 3 existing files +
1 new file:

| File | Inserted | Anchor |
|---|---|---|
| `Presentation/Board/BoardView.cs` | 5 lines | after `:72` (nodes/stations/sources — the `Renderer` local already exists and the bind must precede `:75/:85/:86`); after `:101` (edge); after `:123` (switch disc); after `:132` (switch arm); inside the train-creation block `:177-184`, before `:186` |
| `Presentation/Cameras/CauseCameraController.cs` | 1 line | inside `ShowRing`'s creation block `:82-92`, before `:91` |
| `Presentation/Hud/WavePreview/WavePreviewStrip.cs` | 1 line | inside the chip loop `:31-46`, after `:45`, before `strip.Refresh()` at `:47` |
| `Presentation/Board/GreyboxMaterial.cs` | new file, ~16 lines | cached `Resources.Load<Material>("Materials/Greybox")` + one loud error on a null load (house convention: `UiStrings.cs:17`'s loud sentinel; `GameRoot.cs:203`'s `error_caught domain=` token) |

Bootstrap's share of F-DEV-2 is **zero lines**; its share of F-DEV-1 is one call line plus one new
file. If consent lands NO, the only remaining option is (c) — and its price is a
colour-preservation branch no editor test can exercise (stop condition 10).

---

## 3. What the editor can prove, and what it cannot

| Claim | Proof | Where |
|---|---|---|
| URP is active and is the committed asset; no quality level overrides it | PlayMode assertion on `GraphicsSettings.currentRenderPipeline` / `QualitySettings.renderPipeline` | criterion 1 |
| No pipeline reference in the project dangles | wrapper gate resolving the guid to a committed `.meta` + negative fixture | criterion 1 |
| Every pinned URP/renderer setting is the pinned value | typed asset API where public, YAML grep otherwise, full asset paste in the PR | criterion 2 |
| The boot policy value is 60 and vsync is 0 | PlayMode assertion after a `[SetUp]` reset | criterion 3 |
| The shipped policy is not dev-guarded | wrapper grep + negative fixture | criterion 3 |
| The committed asset keeps vsync 0 and Android on level 0 **after** criterion 1 edits that file | wrapper grep + negative fixture | criterion 3 |
| A material exists at the runtime path, with a real shader, paired to URP | PlayMode `Resources.Load` through the device path | criterion 4 |
| No runtime renderer depends on the engine default material | PlayMode sweep over all four creation surfaces, each proven exercised | criterion 5 |
| Every future primitive stays bound | fail-closed count gate + fixture | criterion 5 |
| The label material exists and its shader compiles | PlayMode assertion | criterion 5 |
| **URP actually draws the labels** | editor Play-Mode screenshot (human) → device screenshot | criteria 5, 6 |
| **URP/Unlit survives release stripping in an IL2CPP/ARM64 APK** | **device only** | criterion 6 |
| **Median ≤16.7 ms / 1%-low (mean-of-worst-1%) ≤33.3 ms over ≥60 s** | **device only, HUMAN** | `CM-C2b-frozen-contract.md:71-76`; `ARTIFACT.md:85-91` |

Editor-side proxies for un-stripping, with their honest limits: the material lives under
`Resources` (included unconditionally by *rule*, not by measurement) and its `m_Shader` reference is
asserted non-`{fileID: 0}` (catches a mis-authored asset, not a stripping policy). The cheapest
direct device evidence is one screenshot — which is why criterion 6 adds it to the artifact's
protocol rather than inventing a new instrument.

---

## 4. Re-measure wiring (the artifact's protocol, `ARTIFACT.md:85-91`, amended in three ways)

1. Merge this contract; **rebuild release from the new main — on the URP build only.** The 30 fps /
   33 ms / 36.8 ms figures were captured on the built-in pipeline and are **void as a baseline**:
   the pipeline change alters frame cost independently of the frame cap, so nothing from the first
   session may be compared to the second except the seam and manifest legs.
2. **Screenshot first** (`adb exec-out screencap -p`). Coloured greybox **and legible labels** ⇒
   F-DEV-2 and the URP-text risk are closed. Magenta, black, or missing labels ⇒ stop before
   spending a frame capture; the mechanism assumption was wrong and the numbers are worthless.
3. Raw per-frame table (`--latency` timestamps), ≥60 s presented, budgets computed from raw
   intervals. **1%-low = mean-of-worst-1%, now pinned by the human** (`ARTIFACT.md:52-56`) — the
   stricter of the two readings the first artifact recorded, and the one under which the first run
   failed at 36.8 ms.
4. Composition call (still the human's): L001 as shipped offers 6.25 s of active sim and cannot
   loop or reach FailureReview (F-DEV-3), so accept the win+banner-hold composition (disclosed),
   wait for a replay/menu affordance from the UX lane, or record a documented deviation.
5. Disposition is the human's. The PR must not state or imply that criterion 8 passed.

---

## 5. Risk register for the restoration (new in revision 2)

| # | Risk | L | I | Handling |
|---|---|---|---|---|
| R-1 | `TextMesh` labels/banners stop rendering under URP (legacy font material; `BoardView.cs:76-83`, `BannerView.cs:22`). They render correctly **today** under built-in (`ARTIFACT.md:69`), so this is a regression the restoration could introduce. | med | high | Criterion 5 asserts the material/shader exist and compile; an **editor Play-Mode screenshot** in the PR and the device screenshot in criterion 6 are the only real proofs. **Stop condition 8** if they fail — routing text through a URP-compatible material is ADR-0007 TMP chrome work owned by the UX lane. |
| R-2 | Frame cost changes materially under URP (extra blit/intermediate texture), so the median budget behaves differently from the built-in baseline. | high | med | Expected and accepted: criterion 2 pins the settings that drive it (no HDR, no MSAA, render scale 1, no depth/opaque texture, Forward, no renderer features, intermediate texture Auto). The baseline is declared void (§4.1); Q-DEVFIX-3 (frame pacing) is the named fallback. |
| R-3 | An unpinned URP/renderer default becomes a future device surprise. | med | high | Criterion 2's pin table + the full-asset paste in the PR so the reviewer diffs intention vs accident. |
| R-4 | Adaptive Performance silently governs frame rate/render scale, defeating criterion 3. | low | high | Pinned **off** in criterion 2; called out as an F-DEV-1 interlock. |
| R-5 | Skybox background behaves differently under URP (`Game.unity:29` references the built-in default skybox; the camera created at `GameRoot.cs:110-117` uses default clear flags). | low | low | Covered by criterion 6's screenshot. Deliberately **not** fixed here: unlike the primitives' material, the skybox *is* referenced by the scene, so it is in the build graph — inventing a solid-colour background would be scope creep. |
| R-6 | The pipeline assets require a pinned-editor session an agent may not have. | med | med | A-DEVFIX-9: editor-generated, or the disclosed untracked-shim precedent (`ARTIFACT.md:114-151`). |
| R-7 | The UX lane starts chrome work against the built-in pipeline and has to redo it. | med | med | Coordination note: land this contract first and tell the UX lane the pipeline changed under them. |

**Worst risk = R-1.** Throwaway spike, time-boxed **30 minutes**, in a **separate worktree** on a
throwaway branch (`spike/urp-textmesh`, never merged): create the URP asset + renderer, wire
GraphicsSettings, open `Game.unity`, press Play, screenshot. If the labels render, R-1 collapses to
a documented observation and the contract proceeds unchanged; if they do not, the spike has bought
the answer before the contract is frozen and criterion 5's second half becomes a stop, not a
surprise mid-implementation. The spike writes nothing to `main`, ships no code, and its only
artifact is one screenshot pasted into the handoff.

---

## 6. Open questions — status after the rulings

- **Q-DEVFIX-1 — RESOLVED (human, 2026-08-05).** Restore URP; conform to ADR-0004:37; no ADR
  change. Folded in as criteria 1-2. Sequencing consequence recorded: the criterion-8 re-measure
  runs **only** on the URP build.
- **Q-DEVFIX-2 — RIDER, consent in flight (drafted assuming YES).** The 7 lines of
  `Presentation/**` (UX-lane tree, `SESSION-HANDOFF-ux.md:28-30`). If YES: land this contract
  first — the UX lane has not started (`:67-68`). If NO: stop condition 10 fires; the only
  alternative is mechanism (c), whose colour branch no editor test can exercise. There is no third
  option — Presentation cannot reference Bootstrap.
- **Q-DEVFIX-3 — OPEN, scoped out unless overruled.** Optimized frame pacing stays OFF
  (`ProjectSettings.asset:72`); named as the fallback if the URP re-measure's median misses. Decide
  whether to change it now (one more variable in the first URP measurement) or hold it.
- **Q-DEVFIX-4 — PARTIALLY RESOLVED.** 1%-low is pinned (mean-of-worst-1%). Still open: the
  **median** convention. A 60 fps cap yields ~16.67 ms against a **≤16.7 ms** budget — ~0.03 ms of
  margin, so any aggregation or rounding that bins upward turns a working fix into a failed
  criterion. Pin the median definition before the re-measure runs; if the human prefers margin over
  literalism the alternatives are a slightly higher target or an amended budget — the human's call,
  not the agent's.
- **Q-DEVFIX-5 — disclosure, not a gate.** Unlit changes the greybox from ambient-shaded to flat
  authored colour; better for colour coding and cheaper. TG-1 does not bite before the art pass
  (CM-C3 recut, evaluator D11). The PR names it.
- **Observation, explicitly out of scope (route to the UX lane).** The cause ring is an opaque
  1.4-diameter disc placed 0.6 units in front of a 0.6-wide node (`CauseCameraController.cs:89,93`),
  so it **occludes the node it is framing** — today in the editor, and identically after this fix
  (both materials are opaque; the alpha 0.85 at `:91` is stored but never blended). A pre-existing
  CM-C3 visual defect, not a regression introduced here; fixing it needs a transparent material and
  a ring-vs-disc mesh decision.

## 7. Sources consulted for the magenta mechanism (background only — repo evidence is primary)

- Unity Discussions, "URP default materials are magenta / pink":
  https://discussions.unity.com/t/urp-default-materials-are-magenta-pink-and-i-cant-convert-them/879231
- "Unity Shader Graph pink material in player build — shader variant collection and stripping":
  https://gamineai.com/help/unity-shader-graph-pink-material-player-build-shader-variant-collection-stripping-fix

- 2026-08-05 loop record: red 0/5 verified (wrapper fired on the dangling-override defect itself) -> URP assets editor-generated via disclosed shim (full text in PR; deleted at session end) -> green 5/5 + wrapper OK -> full suite 12/12 (EditMode+PlayMode survived the pipeline switch). ProjectSettings edits amended in after a near-miss (caught pre-push). L-3-style honesty: the criterion-2 typed asserts cover the public API; YAML greps cover renderer features + depth priming.
