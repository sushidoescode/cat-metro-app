---
paths:
  - "unity/**"
---

# Unity gotchas

Loads only when you touch `unity/`, so it doesn't tax every other session.

**The board is greybox.** `BoardView` draws levels out of `GameObject.CreatePrimitive` — spheres,
cubes and stretched quads. There is no track mesh, no wooden board, no lighting rig. That is the
main reason the game doesn't look like `docs/LOOK.md` yet.

**`CatModelCatalog` holds direct prefab references** — no `Resources.Load`, no Addressables. A
scene with no catalog has no cats, by design. Merging cat-wiring code alone still ships
placeholders; the catalog has to be authored into the scene.

**`FindFor` resolves through `anchor.root`,** and `Game.unity` has one root, `GameRoot`. A
catalog anywhere else returns null for both surfaces, silently.

**Admission is silent.** A `Collider`, `Rigidbody`, `Selectable`, `GraphicRaycaster`, `Animator`
or `Animation` anywhere in a prefab's hierarchy gets it rejected with no log. Unity adds an
Animator on FBX import depending on the Rig setting — set `animationType = None`,
`importAnimation = false`, `addCollider = false`.

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

**Facing is per-asset data.** `Entry.FacingYaw` is added to each surface's own turn (board −22°,
Home −20°). The generated set doesn't agree on a forward axis: the sitting Home poses and the
standing board cats face opposite ways, so one shared value can't be right for all of them.

**Known console noise, not a regression:** `Can't add component because class
'CapsuleCollider'/'MeshCollider' doesn't exist!` — physics modules are stripped and the colliders
are unused. Android also swallows the first touch after focus; that's platform behaviour.
