# CM-CATS-WIRE — frozen contract (phase 1: contract + RED only)

- **Status:** FROZEN before tests and implementation
- **Frozen against:** `origin/main` at
  `3115ebdddd23f3d7eb6836c2670f6dfc2d0a6fb4`
- **Date:** 2026-08-17
- **Branch:** `task/CM-CATS-WIRE`
- **Mode:** sprint
- **Phase boundary:** this branch may freeze this contract and add RED tests now.
  Production implementation is blocked until PR #94 (`GLB-DECIMATION`) merges.

This document is the branch's first commit. A changed criterion, mapping, surface,
or equality below is a contract amendment and must be recorded rather than silently
softened. Phase 1 stops with focused tests demonstrably RED for the missing cat-model
seam; it must not add production code or make the tests green.

## Authority and objective

The governing lane brief is `HANDOFF-LANE-D-CATS-WIRE-2026-08-17.md` in the
orchestrator checkout. It directs this lane to freeze and RED-test replacement of
the shipped Home/Board grey placeholders with the generated decimated cats while
treating absent local assets as normal and retaining the current visuals as fallback.

Objective for phase 2: when curated cat prefabs are directly referenced by a local
catalog, show those cats on the board and shipped Home; when the catalog or any one
entry is absent, retain that slot's existing visual without a crash or error log.

## Exact surfaces

Only these presentation visuals change:

1. `CatMetro.Presentation.Board.BoardView`: a live `train:<slot>` visual created in
   `UpdateFrom` uses the mapped standing cat instead of the current capsule.
   Nodes, sources, stations, edges, switches, teach rings, switch arms, simulation,
   interpolation, and `BoardElementId` semantics do not change.
2. `CatMetro.Presentation.Screens.HomeScreenView`: the three non-interactive
   `ParkedDistrictA`, `ParkedDistrictB`, and `ParkedDistrictC` silhouette `Image`s
   use the mapped cat visual instead of the current rounded rectangle.
   `PinL001`, `PinRingL001`, the optional `PinDaily`, their hit rectangles,
   registration priorities, pulse, colors, and callbacks do not change.

The interactive Home pins are deliberately excluded: they are not grey placeholders,
and changing them would mix the visual task with the already-pinned navigation/input
law. Generated props are also excluded.

## Asset map (manifest id to derivative file)

The map is closed and case-sensitive. It uses manifest ids from
`docs/design/assets/CAT-MANIFEST.json`; filenames name PR #94's decimated derivative,
not a runtime path.

| surface key | manifest id | derivative filename |
|---|---|---|
| board `CatColor.Red` | `cat-red-tabby` | `cat-red-tabby.glb` |
| board `CatColor.Blue` | `cat-blue-siamese` | `cat-blue-siamese.glb` |
| board `CatColor.Yellow` | `cat-yellow-longhair` | `cat-yellow-longhair.glb` |
| board `CatColor.Green` | `cat-green-shorthair` | `cat-green-shorthair.glb` |
| board `CatColor.Wild` | `cat-wild-alley` | `cat-wild-alley.glb` |
| Home `ParkedDistrictA` | `cat-red-tabby-sitting` | `cat-red-tabby-sitting.glb` |
| Home `ParkedDistrictB` | `cat-blue-siamese-loaf` | `cat-blue-siamese-loaf.glb` |
| Home `ParkedDistrictC` | `cat-conductor` | `cat-conductor.glb` |

Unknown color codes, unknown Home slot names, duplicate catalog ids, a null prefab,
or an individually missing mapped entry resolve to fallback; none may select an
arbitrary cat.

`cat-yellow-longhair-wave` is not selected by this slice. Its known detached fragment
therefore cannot leak into a required frame. More generally, the pending human plinth
ruling remains open: neither tests nor implementation may assert, strip, require, or
position against a model's display base, child count, exact bounds, or vertex count.

## Runtime seam and custody

Phase 2 introduces Presentation-owned `CatModelManifestMap`, `CatModelCatalog`, and a
small per-instance identity/read-back component. The catalog holds serialized direct
prefab references and is discovered once from the common scene root used by
`BoardView` and `HomeScreenView`. This follows ADR-0007's "Resources-free direct
references" decision:

- no `Resources.Load`, Addressables, runtime GLB parser, file/network read, package,
  or static global asset cache;
- no change to `unity/Packages/**`, `scripts/**`, `docs/adr/**`, Bootstrap, Domain,
  Content, or Application in this lane; and
- tests install tiny in-memory prefab entries through a test-only catalog seam, never
  by copying the ignored real GLBs into the branch.

PR #94 deliberately leaves derivatives in the ignored local `incoming/decimated`
tree. Promotion/direct-reference authoring and the generated-asset licence approval
remain their own human-gated custody work. Phase 2 may begin only after #94 merges;
no generated model may ship or merge as a referenced Play-binary asset before that
licence/promotion gate is satisfied.

## Acceptance criteria

### AC1 — exact mapping, per-slot fallback, and preserved behavior

- The closed table above is exposed by the new mapping seam and is pinned in EditMode.
- With a valid catalog entry, each named Board/Home surface contains exactly one
  cat-model instance bearing the mapped manifest id. Board instances remain under the
  original `train:<slot>` root, preserve `BoardElementId(Id="train-<slot>", Kind="train")`,
  follow the same interpolated position/active-state law, and do not tint cloned
  materials at runtime.
- With no catalog, an absent entry, or only a partial catalog, each unresolved Board
  slot uses the current colored capsule and each unresolved Home district keeps its
  current enabled silhouette `Image`. Absence is quiet and normal: no throw, error
  log, blank slot, cross-slot substitution, or all-or-nothing catalog failure.
- A successful Home replacement disables only that district's fallback `Image`.
  Existing `HomeLayout`, title/style, session-1 commerce tripwire, motion, region
  count, and pin behavior remain unchanged.
- Imported model descendants add no `Collider`, `Rigidbody`, `Selectable`,
  `GraphicRaycaster`, `Animator`, or `Animation`. Input remains owned by the existing
  `TapInput`/`ChromeRegions` paths.

Phase 2 has one declared test migration: `HomeScreenTests.HomeTree_IsRenderOnly_*` may
extend its render-only whitelist only with the exact new cat identity/catalog and
`MeshFilter`/`MeshRenderer` types. Its zero-`Selectable`, zero-raycaster, and
zero-animation assertions stay byte-intact, and a new negative control proves a
forbidden component is still detected. This is a named migration for the new visual
representation, not permission to weaken or delete the existing test.

### AC2 — bounded rendering, shared assets, and visual evidence

- `BoardView` renders at most **9** cat-model instances concurrently. Additional live
  train slots use their ordinary capsule fallback until a model slot is available.
- Home renders exactly its **3** mapped district instances when all are available.
  Thus the combined presentation ceiling is **12 visible cat instances** and
  **180,000 triangles** (`12 × 15,000`); neither surface may exceed its own cap.
- Every accepted referenced cat is at most 15,000 triangles. The eight selected
  decimated GLBs together must remain at or below **20 MiB** of source payload
  (`20 × 1024 × 1024` bytes). The 2026-08-17 derivatives measure 17,434,232 bytes;
  this is evidence for the ceiling, not an exact-byte pin.
- Instances share prefab meshes and materials. Spawning a second cat from the same
  entry must not clone a mesh/material or increase the catalog's unique-source byte
  accounting; no model load, hierarchy scan, or allocation occurs per `UpdateFrom`
  after the bounded pool is warm.
- Implementation evidence includes rendered frames from the editor or the scoped
  emulator (`adb -s emulator-5554`): shipped Home with all three cats and a board at
  the current nine-cat campaign maximum. The frames must show readable silhouettes
  and preserved color/symbol cues. Code-green alone is not acceptance. The evidence
  records plinths as observed but does not decide the pending plinth taste ruling.

## Phase-1 RED tests

1. EditMode `CatModelManifestMapTests` resolves the exact eight rows, rejects unknown
   keys, pins the 9/3/12/180,000/20-MiB ceilings, and proves the known 17,434,232-byte
   selected set fits without asserting any exact model bounds or vertex count.
2. PlayMode `CatModelWiringTests` uses generated tiny prefabs to prove available,
   absent, and partial-catalog behavior on the real `BoardView` and
   `HomeScreenView` construction paths; it also proves the component-safety wall,
   per-surface instance caps, and shared mesh/material identity.

The tests use reflection only to reach the not-yet-existing seam, so the Unity test
assemblies still compile and report named assertion failures against current main.
The expected RED is "CatModelManifestMap/CatModelCatalog seam is missing," not a
missing ignored GLB, a fixture import error, an incidental object name, or a pre-existing
suite failure. Test mutation controls demonstrate that a wrong map, silent blank,
over-cap result, or cloned shared asset would be caught.

## Assumptions and derived facts

- A1: The three parked districts are the intended Home placeholders. The L001/Daily
  pins remain the navigation affordances already protected by the Home test suite.
- A2: Nine Board model slots are sufficient for the current campaign: each committed
  L001-L017 level emits at most nine cats. Future content may exceed nine; fallback is
  intentional at the equality boundary rather than permission to raise the budget.
- A3: `CatColor` remains the authority for Board selection. Visual code never derives
  gameplay state or rewrites a train's color.
- A4: A direct-reference catalog can be present only in asset-equipped builds and null
  in clean clones. That nullability is a supported runtime state, not a test exception.
- A5: The 20-MiB number budgets selected compressed source payload, while the shared-
  mesh/material and active-triangle limbs bound runtime duplication. Phase-2 rendered
  and profiler evidence must report actual imported runtime memory; this phase does not
  invent it from GLB compressed bytes.

## Scope and stop conditions

Phase 1 may change only this contract, new tests and their `.meta` files, and the
required session-state row. It must not edit production code, existing tests, assets,
packages, scripts, ADRs, scenes, prefabs, project settings, or immutable paths.

Stop phase 1 after the contract commit, RED-test commit, focused right-reason RED log,
state update, and draft PR. Stop immediately if #94's eventual promoted asset interface
contradicts the direct-reference catalog, if a test needs Bootstrap/scene ownership, or
if making current main RED would require weakening an existing test.
