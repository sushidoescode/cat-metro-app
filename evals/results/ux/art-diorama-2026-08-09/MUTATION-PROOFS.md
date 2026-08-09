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
