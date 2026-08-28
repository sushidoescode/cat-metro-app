# Handoff — the art pipeline works now, go build the scene

For a fresh Codex chat. Self-contained. Read `docs/LOOK.md` and open `docs/reference/` before
touching anything visual — the concept art is the spec.

**Forge Kit is gone.** No frozen contracts, no staged approval gates, no review ceremony, no
census. Build the thing, render it, look at it, iterate. If a decision is needed, ask in one line.

---

## The one rule that matters

**Render it and look at it.** Every defect on this project for a week was invisible to tests and
obvious in a picture. In one evening, rendering caught five bugs in a single chain that a full
green test suite reported as healthy:

1. Materials imported as built-in-pipeline Standard in a URP project → grey cats
2. `_BaseMap` never bound, because Unity does not extract an FBX's *embedded* texture → white cats
3. `merge_vertices=True` on glTF import welded UV seams → camouflage cats
4. `merge_vertices=False` when *re-unwrapping* left a triangle soup → one UV island per triangle
5. Underneath all of it: decimation invalidates the provider atlas (see below)

Not one of those was findable by testing. All five took about two minutes each to find by
rendering. **Never batch visual changes — render after each one.**

---

## THE BIG FINDING: decimation requires a re-bake, and nobody did it

The provider atlas is a clean unwrap **at ~2M triangles** (0.7% UV-split across shared edges).
It is packed as many small islands. Decimate to 15k and most islands hold one or two triangles,
so each surviving triangle is huge in 3D while sampling a scrap of texture. Measured: **15–17%
UV-split after decimation, twenty times the source.** The result renders as camouflage.

The decimation work itself was sound — geometry is excellent, silhouettes are right. It was
missing the retopology step that must follow aggressive decimation: **give the low-poly its own
unwrap, then bake colour from the high-poly onto it.**

That is now built and proven:

```
scratchpad/rebake.py         # per-asset: weld -> Smart UV Project -> bake DIFFUSE high->low -> FBX
scratchpad/rebake-all.sh     # runs it over the 8 mapped cats
scratchpad/uv-seams.py       # measures UV fragmentation; how the bug was found
scratchpad/uv-check.py       # UV density check (note: does NOT catch this bug — density
                             # stays constant while islands die. Kept as a cautionary tool.)
```

`merge_vertices` is the trap, and it bites in **both** directions:
- **Preserving** an atlas → `False`. Welding collapses UV seams → camouflage.
- **Re-unwrapping** → `True`. glTF duplicates a vertex at every seam, so an unwelded mesh is a
  triangle soup and Smart UV Project emits one island per face.

Sanity number: a 15k-triangle closed cat welds to **~7,490 vertices**. If you see ~45,000, the
weld did not happen and the unwrap will shatter.

---

## Where things stand

**Working, verified by render:** `cat-conductor` re-baked — clean cream body, orange scarf, teal
cap, no camouflage. The other seven were re-baking as this was written; check
`scratchpad/fbx-baked/` for `<id>_baked.png` per asset, then run
`scratchpad/install-baked-and-render.sh` and **look at the output** before trusting it.

**The APK path works.** `scripts/build-apk.sh` builds a dev APK (~45 min cold). The rig lives in
`scratchpad/wt-apk` (a worktree at `89e26f7`, the cat-wiring branch) with `CatWireBuild.cs`
doing import → prefabs → catalog wiring → build in one pass. An earlier APK installed and ran on
the phone; it just looked bad.

**Open PRs:** `#95` cat wiring (code green, needs one evidence-file correction), `#96` ADR-0013
licensing (green, needs a human signature — not agent work).

---

## What to build next — `docs/LOOK.md` step 2 onward

Step 1 (colour) is essentially done. **Step 2 is the highest-value work available and needs no
new assets:** give the board a body. A wooden tabletop under the level, a warm background instead
of the stock sunset skybox, an isometric camera pitched down, one warm key light with soft
shadows. The current board is white line segments and an orange sphere on a stick against a
default skybox; the same abstract layout will read ten times better with a surface and lighting.

Then: real track geometry with thickness, stations as raised wooden platforms with shape badges,
scenery, and finally cats riding carriages. Full ordering and rationale in `docs/LOOK.md`.

**Unused assets already on disk:** `prop-depot-shed`, `prop-station-kiosk`, `prop-trees`,
`prop-desk-clutter`, `prop-toy-engine` — generated a week ago, excluded by an old contract, never
placed. Note they will need the same re-bake treatment. There is also a paid Polyfork FOUNDERS
account (MCP at `polyfork.dev/mcp`) with low-poly track and scenery that matches this style.

---

## Parallel work that will not collide

- **Scene/lighting** — `unity/Assets/Scripts/Presentation/Board/**`, materials, camera. Highest value.
- **Track geometry** — mesh generation along the level graph. Touches `BoardView` — coordinate
  with the scene lane or take both.
- **Props re-bake + placement** — `scratchpad/rebake.py` over the 5 props, then scene placement.
- **UI cleanup** — dev-console overlay is visible in the build, fail text is clipped off both
  screen edges. Self-contained.

---

## Gotchas (the rest are in `AGENTS.md`)

- **Devices:** run `adb devices -l` and read `model:`. `48121FDAP006X4` = Pixel 9 Pro (target).
  `2G0YC5ZF7Z056Q` = **Quest 3**. `emulator-5554` = **Pico OS6 emulator**. The last two belong to
  other projects — never install there. Older docs called the Quest "the Pixel" for a week.
- **Unity cannot run sandboxed** — cold Library needs the network, editor writes outside the
  allowlist. Same for Blender (segfaults in Metal detection). Run them unsandboxed.
- `CatModelCatalog` rejects prefabs **silently** — assert `AdmittedEntryCount`, never trust the
  screen. It resolves via `anchor.root`, so it must sit on `GameRoot` in `Game.unity`.
- Prefab roots must be identity: `HomeScreenView` never resets `localPosition` and the Home
  holder is scaled ~300x.
- `EditorUserBuildSettings.buildAppBundle` persists in the Library — force it false or you get an
  AAB named `.apk`.
- Unity `-runTests` must not get `-quit`. Never `git commit -a`. `grep -E`, never `rg`.
- The generated art in `unity/Assets/Art/Generated/incoming/` is gitignored and exists on **one
  machine**. `curation-backups/` in there is the only copy of two paid assets' provider originals.
- Never read `.env`. No Play uploads — human only.
