---
paths:
  - "unity/**"
---

# Unity gotchas

Loads only when you touch `unity/`, so it doesn't tax every other session.

**The board is runtime-built, not scene-authored.** `BoardView` owns the simulation geometry;
`BoardSceneLook`, `BoardSurface`, `ToyTrackMeshBuilder`, `ToyTrainView`, and the prop decorator turn
it into the tilted wooden diorama with shaped track and warm lights. Home deliberately reveals the
already-loaded tick-0 board through its transparent window, so an opaque Home backdrop hides real
menu art and breaks the composition.

**`CatModelCatalog` has one optional Resources entry** at `CatRigs/BoardCatRig`; there is no
scene catalog, Addressables lookup, or runtime file/network load. A clean checkout without the
ignored paid dependency keeps the existing placeholders. `AdmittedEntryCount` is the read-back.

**Admission is strict and observable.** The rig must contain exactly one `Animator` with the five
named in-place states, and must contain no `MonoBehaviour`, legacy `Animation`, physics component,
or `StateMachineBehaviour`. The resource prefab keeps that Animator for board playback. Home alone
samples `Cat_IdleSit` and removes the Animator from its mounted clone; admission itself is unchanged.

**`HomeScreenView` never resets a model root's `localPosition`,** and the Home holder is scaled
~300x, so a non-identity prefab root throws the cat off screen. Keep prefab roots at identity and
put corrections on a child.

**Unity has no glTF importer here.** The decimated `.glb` files can't be imported directly —
they go GLB → Blender → FBX. `bake_space_transform=True` with `axis_forward='-Z'`, `axis_up='Y'`
avoids the −89.98° rotation Unity otherwise applies. Blender segfaults under the sandbox (Metal
detection), so it runs unsandboxed too.

**The AAB flag persists.** `CatMetroCliAabBuild` sets `EditorUserBuildSettings.buildAppBundle =
true` and it survives in the Library, so a later APK build can silently emit an AAB named `.apk`.
Force it false.

**Facing is per-entry presentation data.** `Entry.FacingYaw` describes the admitted resource's
camera correction; a surface may add its own turn without rotating the prefab root. The current
resource entry uses 180° for its canonical +Z-forward rig, and Home adds −20° on its wrapper.
`Entry.CosmeticCatId` maps that orange rig to `red_tabby`; unmatched selected cats retain the
complete 2D portrait instead of making one licensed model impersonate another breed.

**Known console noise, not a regression:** `Can't add component because class
'CapsuleCollider'/'MeshCollider' doesn't exist!` — physics modules are stripped and the colliders
are unused. Android also swallows the first touch after focus; that's platform behaviour.
