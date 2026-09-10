# Original open passenger carriage

A small original geometry prototype for replacing the current solid carriage
body. The teal exterior, chunky cream rim and neutral interior share the original
station study's analytic wood/paint atlas. Four navy wheels with wooden hubs and
a rounded chassis complete the silhouette. No text, currency, imported artwork,
licensed model data or external provider calls are used.

The shell has a continuous recessed floor and four curved walls. It is a closed
solid with an open cavity, not a solid box with a recess drawn on its top. The
mesh cross-section goes from the underside around the outer wall and rim, down
the inner wall, then onto the floor. There is no face spanning the rim opening.

## Files and reproduction

- `Source/original-open-carriage.blend`: editable meshes, packed original atlas
  and preview studio. Mesh roots are identity at the canonical export origin.
- `Models/original-open-carriage.fbx` and `.glb`: only `OpenShell` and
  `Undercarriage`, sharing one atlas material. Studio objects are excluded.
- `Textures/original-toy-atlas.png`: 1024 × 512, original analytic colour/wood
  atlas, matching the accepted station study palette.
- `PROVENANCE.json`: one original asset row, generator hashes, geometry budgets,
  cavity measurements and output hashes.
- `EXPORT-VERIFICATION.json`: independent FBX/GLB re-import checks and the hash
  of the actual FBX round-trip render.
- `REPRODUCTION.json`: a fresh generation's geometry/GLB/atlas comparison.

Run from the repository root with Blender 5.1.2:

```sh
blender --background --factory-startup --threads 6 --python-exit-code 97 \
  --python scripts/blender_original_open_carriage.py -- \
  --output-dir docs/design/assets/original-open-carriage

blender --background --factory-startup --threads 6 --python-exit-code 97 \
  --python scripts/blender_original_open_carriage.py -- \
  --output-dir docs/design/assets/original-open-carriage --verify-existing
```

The recipe reuses only the project's original station recipe helpers. Both
generator hashes are recorded. Use `--skip-renders` for geometry-only
regeneration into a separate output directory. FBX and Blend container bytes
can differ because of file paths and timestamps; GLB, atlas and measured geometry
are checked separately. Blender may leave `.blend1` backups after regeneration;
they are not asset inputs.

## Geometry and placement

Blender coordinates are **+X forward, +Z up**, with wheel bottoms at Z = 0.
The total footprint includes the wheel hubs. The source has no hidden scaling.

| Measurement | Blender units |
| --- | ---: |
| Total length × width × height | 0.520 × 0.540 × 0.165 |
| Body outer length × width | 0.520 × 0.480 |
| Body underside | 0.065 |
| Flat floor height | 0.085 |
| Rim height | 0.165 |
| Cavity depth | 0.080 |
| Floor rounded-outline bounds | 0.430 × 0.390 |
| Wall thickness near upper side | approximately 0.031 |
| Wheel diameter | 0.108 |

The proposed board conversion is Blender `(x, y, z)` to board-local
`(x, -y, .235 - z)`, preserving +X forward. For an ordinary Unity +Y-up import,
rotate -90 degrees about X and translate board-local Z by .235. Check the actual
imported axes before mounting. This would put the floor at board-local Z .150
and the rim at .070; those are prototype targets, not tested runtime placement.

The current `ToyTrainView` body spans board-local Z .135–.235. The new lower
floor and higher rim create room for the passenger and a possible paw contact
surface. **The current rig pose was not changed or validated against this rim.**
The owner must calibrate the seated body, forepaws, tail, destination pin and
station clearances in Unity. The carriage has no new coupling protrusion beyond
the requested footprint.

| Part | Source vertices | Triangles |
| --- | ---: | ---: |
| OpenShell | 352 | 700 |
| Undercarriage | 1,344 | 2,644 |
| Total | 1,696 | 3,344 |

Two mesh objects, one shared material, one atlas. All authored mesh components
have zero non-manifold edges. The shell is one connected closed solid;
undercarriage components are assembled solids, not a Boolean union. Importers
can split vertices at UV or normal seams.

The geometry checks cast nine downward rays inside the cavity to the floor,
four downward rays to the rim, and four horizontal rays at mid-wall height.
The same horizontal rays above the rim encounter nothing. These checks also run
on both re-imported formats. A solid body control is required to fail. Export
checks verify triangles, bounds, atlas binding and the exact PNG embedded in FBX.

## Opened previews

- `01-open-front.png`: front three-quarter, floor and all four walls visible.
- `02-open-rear.png`: opposite three-quarter and second pair of wheels.
- `03-open-top.png`: unobstructed rounded opening and floor.
- `04-low-side.png`: low wall profile, raised rim and wheel/chassis relationship.
- `05-small.png`: the same geometry rendered at 192 × 151.
- `06-fbx-roundtrip.png`: actual re-imported FBX geometry and material binding.

All are actual Blender CPU Cycles renders. There is no cat proxy, painted-over
image or reference artwork in these renders. Reference direction was personally
checked against `gen-ref-v2-board.png`, `gen-ref-v2-moments.png` and the owner's
`seat35-head128-natural-close.png` gameplay probe before generation.

Unity import, URP texture binding, actual passenger placement/occlusion,
gameplay clearances and Android rendering/performance are not verified. This
commit does not alter runtime code or ship a Unity prefab.
