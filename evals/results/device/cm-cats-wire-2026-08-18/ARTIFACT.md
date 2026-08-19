# CM-CATS-WIRE phase 2 — rendered evidence (2026-08-18, revised 2026-08-19)

Agent claim (not attested): the generated decimated cats appear on the shipped Home and on the
board through the real boot path, at both surface ceilings, and the asset-free build renders the
existing placeholders with no crash and no error log. Every frame below was rendered and looked
at; what each one shows — including what it shows that is wrong — is written out.

## Provenance

- Branch `task/CM-CATS-WIRE`, contract `state/handoffs/CM-CATS-WIRE-frozen-contract.md` AC2.
- **Device frames (01, 02, 04, 06):** dev APK, AVD `catmetro-test` (pixel_7, arm64-v8a, headless
  SwiftShader), portrait framebuffer 1080x2400, captured with
  `scripts/emu-selftest.sh frame`. Recipe + trap ledger: `docs/runbooks/emulator-selftest.md`.
  **Serial note:** the runbook's default `emulator-5554` was occupied by an unrelated PICO XR
  emulator (AVD `PICO_6.0`) whose VR compositor makes `screencap` return the headset shell, not
  the app. That emulator was left running and was restored to how it was found (app uninstalled,
  `wm size`/`wm density` reset); `catmetro-test` was booted on `emulator-5556` instead and killed
  after capture (twice: 01/02/04 on 2026-08-18, 06 on 2026-08-19 after the facing fix). Every
  adb call was serial-scoped; the physical Pixel was never addressed, including after it
  reappeared on the shared adb server during the second session.
- **Editor frames (03, 05):** the same `GameRoot` boot path in PlayMode, camera rendered to a
  RenderTexture. 03/05 are 1080x1920 rather than Screen-matched because they contain no canvas
  content; the CM-UX-02 Screen-matched-RT rule only binds frames that must lay out UI, and the
  Home frames here come from the device instead.
- **The models are not in this diff and cannot be.** Unity has no glTF importer and this lane may
  not touch `unity/Packages`, so the eight ignored `.glb` derivatives were converted to FBX with
  Blender outside the repo and authored into a local, uncommitted catalog prefab. Nothing under
  the ignored incoming tree was modified, and no GLB byte was rotated or re-exported to fix
  facing — that correction lives in the catalog as data. The catalog these frames used was wired
  into the scene locally and reverted before commit; a committed reference would break every
  other machine and pre-empt the licence/promotion gate.

## Frames (sha256 manifest)

| # | frame | what it shows |
|---|-------|----------------|
| 01 | `50a49f82c065a3731a05729c000e5928d9fadaf54fa08a7af342c01a3c211d9d` 01-home-shipped-three-cats.png | **SUPERSEDED BY 06 — kept as the before-frame of the facing defect.** The shipped Home on device with all three mapped cats resolved: A the sitting red tabby, B the blue siamese loaf, C the conductor. Structurally correct — three cats, no silhouette rectangles, pin and ring untouched — but only the conductor faces the player. A is turned three-quarter away and B is a left-facing profile showing its tail. See 06. |
| 02 | `fa2228687a716f60a53ce2a7dd7b2225e585923b6072011f875cb3024282015c` 02-board-live-cats.png | **Live L001 play on device**: two red tabbies, one leaving the source and one at the blue station. Colour cue is preserved — a `CatColor.Red` train is an orange tabby. The "Signal fault" halt is the pre-existing NEW-Q4 misroute boundary (backlog Q-B criterion 14): the run was left unplayed, so the red cat reached B. Not caused by this diff. |
| 03 | `aafbf56638efd0ea3190b4db84413584bbfb45b4eeea484d3b11339b049fc037` 03-board-nine-cats-at-cap.png | **The board at the nine-cat campaign maximum** (L011, one of the four nine-train levels — A2). Nine cats concurrently, all five mapped colours present and distinguishable: orange tabby, dark slate siamese, yellow tabby, green shorthair, ginger alley cat. Reached after ten `LoadNext` rebuilds, and the catalog still reported exactly 12 live instances / 179,999 triangles — no budget leak across rebuilds. |
| 04 | `1cda92ffd2f13942a73f066a253f4e513f12261680edaecd6c933247c3e23d40` 04-home-no-catalog-fallback.png | **The primary path, on device: the same build with no catalog in the scene.** Home is byte-for-byte the Home that shipped before — three grey silhouette rectangles, title, pin, ring. No crash, no blank slot, no cat-related log line. This is what a clean clone and a CI runner get. |
| 05 | `b1873d05a87d39f3fe80c00539635ae9b4a9c0ec74f4f49e0cdca3c9b037d222` 05-board-no-catalog-fallback.png | **Board fallback**: the ordinary coloured capsules, red and blue, at 0.35 scale on their edges. Unchanged from before this diff. |
| 06 | `7dc0cdc4a0ae30ee7e25c91389d2904ecb2aa3c660d5a2b02960e26a814d7f2b` 06-home-facing-corrected.png | **The shipped Home after the facing fix — the frame that supersedes 01.** All three cats now meet the player's eye: the red tabby shows both eyes, muzzle, stripes and its collar medallion with its tail curled round; the blue siamese shows its face, whiskers and cream chest bib with both front paws forward; the conductor is unchanged. Each keeps the same slight turn, so the row reads as one set rather than three unrelated poses. Pin and ring still untouched. |

The red `Can't add component because class 'CapsuleCollider'/'MeshCollider' doesn't exist!` lines
in the dev console of frames 01/02/04 are the pre-existing stripped-physics-module errors from
`GameObject.CreatePrimitive` (state/PROJECT_STATE.md). They appear identically in the no-catalog
build, so they are not this diff's. The cat prefabs carry no colliders at all — the catalog
refuses to admit a prefab that does.

## What rendering caught that green tests did not

1. **The cats came in lying on their sides.** The glTF -> Blender -> FBX path lands the mesh Z-up
   in Unity. Every test was green; the first frame was pancakes seen from above. Fixed in the
   asset authoring step, not in presentation code.
2. **Then two of them faced away from the camera.** With one blanket 180-degree turn the
   conductor and the standing board cats faced front while the two sitting Home cats showed their
   backs — the generated set does not agree on a forward axis. This is why the presentation code
   PRE-multiplies its own presentation turn onto the prefab's authored rotation instead of
   assigning one: facing is the asset's business.
3. **Then, with facing corrected per prefab, two of the three still faced away on Home.** The
   orchestrator caught this one on 01 after I had looked at the same frame and called it good —
   my read was too generous. Two sitting poses are authored a quarter-turn off from the
   conductor, so "leave facing to the asset" produced a mixed Home, and on this project mixed
   reads as a bug. Fixed as CATALOG DATA (`Entry.FacingYaw`), not by touching the GLB bytes:
   PR #96's licence ADR pins a hash manifest to them. The angle was chosen by rendering a sweep
   and looking — at 0 both read as left profiles, at +90 both showed their backs, at -90 both
   face the player.
4. **An opaque mesh left on the Home canvas plane is invisible.** The canvas is ScreenSpaceCamera
   at camera z + 1, UI draws after the opaque pass with ZTest LEqual, and Home paints a full-bleed
   opaque background at exactly that depth. The cats are lifted in front of the plane and their
   depth is flattened to fit the 0.7-unit gap before the near plane — free under an orthographic
   camera with unlit models, and both expressed as fractions of the district rect so they hold at
   any resolution.

## Observations for the queue (not this contract's diff)

1. **A cat standing on a switch node draws over the switch disc.** The cat has to sit in front of
   the node cube it stands on, which puts it in front of the disc too; the capsule it replaced
   sat behind the disc. The teach ring still reads around it. Worth a taste call once a level
   routinely parks cats on interchanges.
2. **`DeviceConfigTests.AllRuntimeRenderers_BindGreybox_TextMeshSurvives` will fail the day a
   catalog ships.** It asserts every runtime renderer under GameRoot binds the Greybox shader; a
   cat's own material does not. It passes today only because no catalog is committed. The
   promotion lane must amend it deliberately, not discover it.
3. **Imported model child node names are unaudited against the Home commerce tripwire.** The
   session-1 walk fails on any GameObject name containing `night`, `ticket`, `access`, and
   friends; a promoted prefab's authored child names have never been checked against that list.
