# Original station runtime variant

One neutral station building, regenerated entirely from the original procedural
recipe. The accepted red/circle station and both track prototypes remain unchanged
in `../original-station-track/`.

```sh
blender --background --factory-startup --threads 6 --python-exit-code 97 \
  --python scripts/blender_original_station_runtime.py -- \
  --output-dir docs/design/assets/original-station-runtime
blender --background --factory-startup --threads 2 --python-exit-code 97 \
  --python scripts/blender_original_station_runtime.py -- \
  --output-dir docs/design/assets/original-station-runtime --verify-existing
```

The FBX/GLB contains four independently named, identity-transform meshes:

| Part | Triangles | Role |
|---|---:|---|
| Body | 6,832 | Cream walls/platform, wood, navy trim, bench and window |
| RoofTint | 504 | Neutral-white atlas region for per-renderer route color |
| BadgePost | 188 | Optional neutral authoring part, omitted from runtime prefab |
| BadgeFace | 476 | Optional cream disc, omitted from runtime prefab |

There is no embedded colored circle. The runtime prefab has **two renderers,
7,336 triangles, one shared material and one 1024×512 atlas**. The existing
`BoardPropDecorator` continues to own the actual cream discs, masts, primary and
secondary destination shapes, placement, and effect target names. Only the
building's `RoofTint` renderer receives the station color. The body keeps its atlas
and the legacy extra box canopy/platform are omitted for this original model.

Both exports were reimported in fresh Blender scenes; all four part names, triangle
counts, atlas bindings, and bounds match. FBX contains the exact atlas PNG bytes.
Roof UVs sample only the neutral swatch. See `EXPORT-VERIFICATION.json` and the
per-asset `PROVENANCE.json`. Both current Blender previews were personally opened;
they show the admitted building with a sample red tint and the neutral authoring
parts. They are not Unity screenshots.

To create the Unity assets, run **Cat Metro → Art → Build Original Station**
outside Play Mode, or the CLI method
`CatMetro.EditorTools.CatMetroOriginalStationImportPipeline.BuildAndExit`.
The importer uses Unity APIs to create folders, mesh/material assets, the prefab,
and their metadata. It copies only this original FBX/atlas into
`Assets/Art/Original/Station/`, explicitly binds URP/Lit `_BaseMap`, and saves
`Assets/Resources/CatMetroOriginal/Station.prefab`. The prefab contains only `Body`
and `RoofTint` at identity transforms. The importer rejects unexpected units or
axis orientation against the 3.05 × 2.222 × 2.17 Y-up bounds.

`PropModelCatalog` prefers this resource for the existing station-kiosk slot at
0.55 scale and 180° yaw. If absent, the legacy resource path and its corrections
are unchanged. No other prop slot's admission/rejection behavior changes.
An original-only checkout admits exactly one prop; a full furnished install still
admits ten because this replaces the station slot.

There is **no generated Unity prefab, mesh, material, or metadata in this candidate
yet**: the owner controls the Unity session. After running the importer, transfer
the complete `Assets/Art/Original/Station/` and
`Assets/Resources/CatMetroOriginal/` trees plus Unity-created folder metadata and
`CatMetroOriginalStationImportPipeline.cs.meta` back into this worktree for review.
Only include newly created parent-folder metadata where the parent is new.

Validation pending with the owner: Unity compilation/import, the four
`OriginalStation_TintsOnlyItsRoof_AndPreservesEveryLiveBadge` cases, existing
PropPlacement/RuntimeSceneRig/BoardFeedback suites, and actual phone-sized board
captures for colors, authored blue-triangle acceptance, multi-accept badges, and
train clearance. Static track prototype pieces are not integrated into the game.
