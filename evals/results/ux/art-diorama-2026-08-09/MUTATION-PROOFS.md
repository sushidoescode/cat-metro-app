# ART-DIORAMA mutation proofs

Baseline: `e61bd08` (`sprint: build Polyfork tabletop diorama`). Each mutation was applied alone,
the named focused test was run, and the edit was reverted with `apply_patch`. Final
`git diff --exit-code` passed before this evidence file was added.

| Mutation | Focused command | Expected RED evidence |
|---|---|---|
| `M-PRIMITIVE-CAMERA` — seed the literal primitive call token in `CauseCameraController` | `bash tests/unity/device-config.test.sh` | exit 1: `Board/Cameras still create 1 stripped-component primitives` |
| `M-BOARD-COLLIDER` — add a `BoxCollider` to the junction | PlayMode `RealL001_HasRequiredDioramaRosterAndColliderFreeOwnedTrees` | exit 2: expected empty, found `node:J1 (UnityEngine.BoxCollider)` |
| `M-STATION-SYMBOL` — corrupt the station `LineSymbolMesh.SymbolId` | PlayMode `StationsAndLiveCommuter_AreTripleCodedWithVisibleSymbolMeshes` | exit 2: `station:RED Expected: True But was: False` |
| `M-COMMUTER-TAG` — mis-tag a live cat as a station | PlayMode `StationsAndLiveCommuter_AreTripleCodedWithVisibleSymbolMeshes` | exit 2: shipped wave produced `0` live cat commuters, expected at least `1` |
| `M-PALETTE-BYTE` — change Signal Red `E15A47` to `E05A47` | EditMode `Palette_RoundTripsToAllTwelveAuthoritativeHexes` | exit 2: 12-entry authoritative palette dictionary mismatch |

Post-revert positive controls: `device-config.test.sh: OK`; `cli-build-shim.test.sh: OK`;
working tree byte-clean at `e61bd08` before this record.

## Independent-review RED proofs

The fresh-context review found defects that the original focused suite did not cover. Tests
were added before each repair:

| Review regression | RED | GREEN |
|---|---|---|
| L002 reuses slot 0 from red to blue | expected blue code `2`, observed stale red code `1` | slot identity change rebuilds colour + square + slim-Siamese together |
| Binding 30-degree camera composition | observed pitch `0`, expected `30` | signed 30-degree pitch, grid-plus-margin projection, existing HUD/input pins green |
| Rounded gameplay exterior | `station:RED` used mesh `Cube` | all inventoried stations, rails, ties, trackbeds, and lever use `RoundedBox12` |
| Toon shader contract | shared shader lacked `_RampThresholds` | three-step ramp plus `_RimStrength` rim term present on the one shader family |
| Polyfork receipt/integrity | exact Blender/acquisition receipt absent | all 9 FBX bytes and imported triangle counts cryptographically checked against provenance |
| Primitive/bind invariant | reviewer showed a second WavePreview token could pass one bind | the same count predicate enforces one-to-one production counts and rejects the bad fixture |

Rendered-frame review also rejected the first capsule substitution because it produced bloated
pill platforms. The tightened `RoundedBox12` assertion went RED against `Capsule.fbx`, then green
with the procedural 12%-radius beveled box. Full final Unity controls: EditMode 827/827 and
PlayMode 145/145.
