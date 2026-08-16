# GLB Decimation Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and prove an offline Blender 5.1.2 pipeline that turns the 15 paid generated candidates into texture-preserving derivatives at 15,000 triangles per cat and 10,000 triangles per prop without modifying or promoting the originals.

**Architecture:** A pure-standard-library GLB inspector supplies authoritative structure, geometry, resource, and world-bounds facts. A manifest-aware Python orchestrator validates source custody, invokes one isolated Blender background process per asset, validates the result, and atomically promotes the derivative plus provenance sidecar; a Blender-only driver performs import, triangulation, collapse decimation, and GLB export. Generated GLBs stay under the ignored `incoming/decimated/` tree while tracked tests, an ADR, pipeline documentation, metrics, and rendered evidence make the behavior reviewable.

**Tech Stack:** Bash regression tests, Python 3 standard library, Blender 5.1.2 `bpy`, and binary glTF 2.0. The already-installed ImageMagick may compose a convenience contact sheet, but no acceptance criterion depends on it.

## Global Constraints

- The frozen contract is `state/handoffs/GLB-DECIMATION-frozen-contract.md`, first branch commit `bc34c6abf6ecf580465c061c2993a7536aeacf41`; do not amend an acceptance criterion without recorded human approval.
- Cats target exactly 15,000 triangles and must land in 13,500–15,000; props target exactly 10,000 and must land in 9,000–10,000; every result must remain in 5,000–20,000.
- Blender is pinned to exactly 5.1.2 and runs only as `--background --factory-startup`; no GUI, network call, credential, Unity runtime importer, package change, or mesh-compression extension is permitted.
- Source GLBs and their `.glb.json` sidecars under `unity/Assets/Art/Generated/incoming/` are immutable inputs; output is local and ignored under `unity/Assets/Art/Generated/incoming/decimated/`.
- A source must have a matching SHA-256 sidecar and `plan_tier: paid`; manifest paths must be bare `.glb` filenames and resolve inside the selected input/output roots.
- Derivatives retain UVs, materials, embedded images, ordinary self-contained GLB structure, and world-space orientation/scale bounds.
- Bounds tolerance is explicit: output center drift is at most 0.5% of the source's longest extent, longest-extent scale drift is at most 1%, and each axis's normalized extent drift is at most 2%.
- Existing derivatives are refused unless `--force` is explicit; no failed run leaves a newly promoted derivative or sidecar.
- TDD is mandatory: observe the focused test RED before each behavior implementation, never weaken an existing test, and preserve the initial RED transcript in tracked evidence.
- Do not touch any path under `tests/contract/` or `evals/`, nor `docs/constitution.md`, hooks, `state/mode`, `.github/`, Unity packages, Unity scenes/prefabs/scripts, any device, or any emulator.
- `scripts/glb-silhouette.py` in the main checkout is user-owned and untracked; do not alter that working-tree file. Independently create the branch's tracked, compatible, all-primitive renderer after its RED test so exact-head evidence is reproducible.
- ADR-0012 records the human-approved frozen decision before implementation. Adding the ADR still makes this a human-merge PR; open PR #65 already reserves ADR-0011.

## File map

- Create `tests/assets/glb_fixture.py`: deterministic tiny self-contained GLB fixture writer used only by tests.
- Create `tests/assets/glb-metrics.test.sh`: behavioral and mutation coverage for the pure inspector.
- Create `scripts/glb_metrics.py`: strict GLB parser, metrics library, preservation comparison, and JSON CLI.
- Create `tests/assets/glb-silhouette.test.sh`: renderer all-primitive and non-vacuity regression.
- Create `scripts/glb-silhouette.py`: tracked, pure-stdlib, all-primitive silhouette renderer compatible with the requested positional CLI.
- Create `tests/assets/fake_blender.py`: deterministic fake of the two subprocess surfaces (`--version` and one-asset execution).
- Create `tests/assets/glb-decimation-pipeline.test.sh`: end-to-end custody, budget, failure, atomicity, and provenance regression test.
- Create `scripts/blender_decimate.py`: Blender-only one-asset import/triangulate/decimate/export driver.
- Create `scripts/decimate-assets.py`: manifest-aware batch orchestrator and atomic publisher.
- Create `docs/adr/0012-blender-headless-glb-decimation.md`: human-approved dependency/tool-boundary decision, recorded before behavior implementation.
- Create `docs/design/assets/DECIMATION.md`: operator contract, commands, validation semantics, and recovery behavior.
- Modify `docs/design/assets/PIPELINE.md`: link generation custody to the separate local decimation stage.
- Create `docs/design/assets/GLB-DECIMATION-EVIDENCE.md`: RED/GREEN transcript, real metrics summary, local-render hashes/order, and visual verdict.
- Create `docs/design/assets/GLB-DECIMATION-METRICS.json`: machine-readable 15-asset before/after metrics and hashes.
- Create local untracked `/Users/sushantsrikrish/cat-metro-app/.catshots/glb-decimation-2026-08-15/before-grid.png`, `after-grid.png`, and `comparison-grid.png`: visual proof inspected in-session but never staged or published ahead of the generated-asset license ADR.
- Update `state/PROJECT_STATE.md` only at the session/PR handoff, respecting its line cap and rotating old history if necessary.

---

### Task 1: Record proposed ADR-0012 before implementation

**Files:**
- Create: `docs/adr/0012-blender-headless-glb-decimation.md`

**Interfaces:**
- Produces: the dependency/tool-boundary decision required before behavior implementation.
- Consumes: the human-approved frozen contract, the locally verified Blender 5.1.2 surface, and primary license sources.

- [ ] **Step 1: Verify license/tool facts from primary sources**

Use only Blender's official license/FAQ pages and GNU's official GPL text. Record retrieval date 2026-08-15 and direct source URLs. Paraphrase the narrow facts: Blender is GPL-licensed tooling; works created with Blender are not automatically GPL; bundled/imported data and add-ons retain their own terms. Do not convert this tool-boundary statement into a verdict on Meshy/Tripo output rights, which belongs to the later generated-asset license ADR.

- [ ] **Step 2: Write proposed ADR-0012**

Use the repository's ADR sections and this exact status boundary:

```markdown
# ADR-0012: Blender 5.1.2 headless for generated-GLB decimation

- **Status:** Proposed — human merge required; design approved 2026-08-15 in `state/handoffs/GLB-DECIMATION-frozen-contract.md`
- **Date:** 2026-08-15
- **Relates:** ADR-0004, ADR-0008, `state/handoffs/GLB-DECIMATION-frozen-contract.md`

## Context
## Decision
## Alternatives seriously considered
## Consequences
## Security notes
## License boundary
## Verification and upgrade trigger
```

The Decision records exact Blender `5.1.2`, build `ec6e62d40fa9`, bundled `io_scene_gltf2` `5.1.20`, offline/background/factory-startup/autoexec-disabled operation, one process per asset, ordinary GLB output, no Unity package/runtime dependency, ignored local derivatives, category budgets, and human visual review. It records the approved empty extension allowlist for derivatives. Alternatives are gltfpack/meshoptimizer, Unity importer settings alone, and a custom decimator. An upgrade requires a new proposal plus focused mutations, all 15 real metrics, and full visual evidence.

- [ ] **Step 3: Verify ADR completeness and commit it before code/tests**

```bash
rg -n 'Status.*Proposed|5\.1\.2|ec6e62d40fa9|5\.1\.20|15,000|10,000|extension allowlist|GPL|offline|human' \
  docs/adr/0012-blender-headless-glb-decimation.md
git diff --check
git add docs/adr/0012-blender-headless-glb-decimation.md
git commit -m "docs: propose Blender decimation decision"
```

Verify `git log --reverse origin/main..HEAD` shows the frozen contract first, the implementation plan second, and ADR-0012 before every test or behavior implementation commit.

---

### Task 2: Freeze a discriminating RED test for pure GLB inspection

**Files:**
- Create: `tests/assets/glb_fixture.py`
- Create: `tests/assets/glb-metrics.test.sh`

**Interfaces:**
- Produces: `write_glb(path: pathlib.Path, *, triangles: int, primitive_count: int = 1, include_uv: bool = True, include_material: bool = True, include_image: bool = True, external_image: bool = False, extensions: tuple[str, ...] = (), add_scene_content: bool = False, bounds: tuple[tuple[float, float, float], tuple[float, float, float]] = ((-1.0, -1.0, -1.0), (1.0, 1.0, 1.0)), declared_bounds: tuple[tuple[float, float, float], tuple[float, float, float]] | None = None, translation: tuple[float, float, float] = (0.0, 0.0, 0.0)) -> None`.
- Produces: expectations for `python3 scripts/glb_metrics.py FILE`: one sorted JSON object on stdout and nonzero with a `glb-metrics:` diagnostic on stderr for malformed input.
- Consumes: no production implementation; this commit must be RED because `scripts/glb_metrics.py` does not exist.

- [ ] **Step 1: Add the deterministic fixture writer**

Use eight `VEC3` float positions spanning the requested bounds, eight `VEC2` UVs, repeated unsigned-short indices with exactly `triangles * 3` entries, and a fixed embedded 1×1 PNG. Align every buffer view to four bytes, set accessor `min`/`max`, emit the requested number of triangle-mode primitives with independent POSITION accessors distributed evenly along X, one material/texture/image when enabled, and store translation on the mesh node. Split the requested total triangle count across primitives without changing the total. The public callable is the exact signature in the Interfaces block. Its CLI is:

```python
if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    parser.add_argument("--triangles", type=int, required=True)
    parser.add_argument("--omit-uv", action="store_true")
    parser.add_argument("--omit-material", action="store_true")
    parser.add_argument("--omit-image", action="store_true")
    parser.add_argument("--external-image", action="store_true")
    parser.add_argument("--primitive-count", type=int, default=1)
    parser.add_argument("--extension", action="append", default=[])
    parser.add_argument("--add-scene-content", action="store_true")
    parser.add_argument("--translate", nargs=3, type=float, default=(0.0, 0.0, 0.0))
    args = parser.parse_args()
    write_glb(args.path, triangles=args.triangles,
              include_uv=not args.omit_uv,
              include_material=not args.omit_material,
              include_image=not args.omit_image,
              external_image=args.external_image,
              primitive_count=args.primitive_count,
              extensions=tuple(args.extension),
              add_scene_content=args.add_scene_content,
              translation=tuple(args.translate))
```

- [ ] **Step 2: Add the inspector regression shell test**

The test creates only `mktemp -d` fixtures, installs an EXIT trap for that exact directory, and asserts these cases without inspecting production source text:

```bash
python3 tests/assets/glb_fixture.py "$tmp/valid.glb" --triangles 37 --translate 4 5 6
python3 scripts/glb_metrics.py "$tmp/valid.glb" >"$tmp/valid.json"
python3 - "$tmp/valid.json" <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
assert d["triangles"] == 37
assert d["vertices"] == 8
assert d["meshes"] == d["primitives"] == d["materials"] == 1
assert d["images"] == d["embedded_images"] == 1
assert d["uv_primitives"] == d["material_primitives"] == 1
assert d["animations"] == d["cameras"] == d["lights"] == d["skins"] == 0
assert d["morph_targets"] == 0
assert d["external_uris"] == []
assert d["world_bounds"] == {"min": [3.0, 4.0, 5.0], "max": [5.0, 6.0, 7.0]}
PY
```

Also assert: corrupted magic fails; a truncated chunk fails; an external image URI is reported; an omitted UV/material/image yields counts of zero; an unsupported primitive mode fails; a node translation affects world bounds; falsified accessor `min`/`max` cannot conceal actual POSITION bounds; and `compare_preservation(source, output)` rejects center drift, normalized-extent drift, scale drift, UV or material binding missing on any primitive, a missing embedded image, external URIs, any arbitrary extension, `EXT_meshopt_compression`, animations, cameras, lights, skins, and morph targets.

- [ ] **Step 3: Run the focused test and capture honest RED**

Run:

```bash
set +e
bash tests/assets/glb-metrics.test.sh >/tmp/glb-metrics-red.txt 2>&1
red_rc=$?
set -e
test "$red_rc" -ne 0
sed -n '1,120p' /tmp/glb-metrics-red.txt
rg -n 'glb_metrics.py|No such file|can.t open file' /tmp/glb-metrics-red.txt
```

Expected: the test fails because `scripts/glb_metrics.py` is absent. A fixture-generation failure is not an acceptable RED; fix the test helper until the failure reaches the missing production entry point.

- [ ] **Step 4: Commit the RED-only test**

```bash
git add tests/assets/glb_fixture.py tests/assets/glb-metrics.test.sh
git commit -m "test: pin GLB inspection contract red"
```

---

### Task 3: Implement strict GLB metrics and preservation checks

**Files:**
- Create: `scripts/glb_metrics.py`
- Test: `tests/assets/glb-metrics.test.sh`

**Interfaces:**
- Produces: `inspect_glb(path: Path) -> dict[str, object]`.
- Produces: `compare_preservation(source: Mapping[str, object], output: Mapping[str, object]) -> list[str]`; an empty list means preservation accepted.
- Produces: CLI `python3 scripts/glb_metrics.py FILE`, with sorted JSON stdout and diagnostics prefixed `glb-metrics:`.
- Consumes: fixture structures and assertions from Task 1.

- [ ] **Step 1: Implement strict container and JSON parsing**

Use `struct`, `json`, `hashlib`, `math`, and `pathlib` only. The parser must verify the complete 12-byte GLB header (`b"glTF"`, version 2, declared length equals file size), require JSON as the first chunk, reject chunks that overrun the file, decode padded UTF-8 JSON, and require list/object types before indexing.

```python
class GlbError(ValueError):
    pass

def _validated_header(data: bytes) -> int:
    if len(data) < 20:
        raise GlbError("truncated GLB")
    magic, version, declared = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or declared != len(data):
        raise GlbError("invalid GLB header")
    return 12
```

`_read_glb(path)` reads the bytes, starts at `_validated_header(data)`, and advances by each checked `<I4s` chunk header plus payload. It accepts `b"JSON"` only as the first chunk, rejects duplicate JSON and chunk overruns, strips only GLB padding from the JSON payload, requires a JSON object, and returns `(document, data)`.

- [ ] **Step 2: Implement authoritative geometry/resource metrics**

For every mesh primitive, validate the POSITION accessor and supported modes 4 (triangles), 5 (strip), and 6 (fan); count triangles as `count // 3` for mode 4 and `max(0, count - 2)` for modes 5/6, using the index accessor when present. Decode actual POSITION bytes through checked buffer-view offsets/strides and require component type 5126 (`FLOAT`), type `VEC3`, finite values, no sparse accessor, and byte ranges inside the embedded BIN chunk. Sum POSITION accessor counts as vertices, count primitives carrying `TEXCOORD_0`, count primitives with a valid material binding, count materials/images, classify images with `bufferView` or `data:` URI as embedded, and collect every non-data buffer/image URI. Reject malformed buffer/accessor references and mode-4 counts not divisible by three.

```python
METRIC_KEYS = (
    "path", "sha256", "bytes", "meshes", "primitives", "vertices",
    "triangles", "materials", "material_primitives", "images",
    "embedded_images", "uv_primitives", "animations", "cameras", "lights",
    "skins", "morph_targets", "external_uris", "extensions_used",
    "extensions_required", "world_bounds",
)
```

`inspect_glb` returns every key in `METRIC_KEYS`; it never infers triangle count from vertex count or file size.

- [ ] **Step 3: Compute world-space bounds through the selected scene graph**

Compose glTF column-major `matrix` values or TRS values and walk only nodes reachable from the selected scene (default scene zero). Compute each accessor's local bounds from decoded POSITION bytes, never from accessor-declared `min`/`max`; optionally cross-check declared bounds and reject when they disagree beyond float tolerance. Transform all eight corners of the measured local box and union them. Reject cycles, non-finite transforms/positions, invalid node/mesh indexes, or simultaneous `matrix` and TRS. The resulting object is exactly `{"min": [x, y, z], "max": [x, y, z]}` with JSON numbers.

- [ ] **Step 4: Implement the contract's preservation comparator**

```python
DISALLOWED_EXTENSIONS = {"EXT_meshopt_compression", "KHR_draco_mesh_compression"}
ALLOWED_OUTPUT_EXTENSIONS = frozenset()
CENTER_DRIFT_MAX = 0.005
SCALE_DRIFT_MAX = 0.01
NORMALIZED_EXTENT_DRIFT_MAX = 0.02

def center_and_extents(bounds):
    lo, hi = bounds["min"], bounds["max"]
    center = [(a + b) / 2.0 for a, b in zip(lo, hi)]
    extents = [b - a for a, b in zip(lo, hi)]
    return center, extents
```

`compare_preservation` emits the exact diagnostics shown in Task 5. It rejects external URIs; any extension outside the empty approved allowlist (with compression extensions named explicitly); UV count or material-binding count unequal to total primitives; unequal source/output material or embedded-image counts; any animation, camera, light, skin, or morph target; and bound drift beyond the three constants. Use the source's longest extent as the center denominator, divide both axis-extent vectors by their own longest extent for normalized-shape comparison, and compare output/source longest-extent ratio for scale. Reject zero/non-finite extent before comparison. Round only when rendering JSON; comparison uses full floats.

- [ ] **Step 5: Run focused tests to GREEN and mutation-probe them**

```bash
bash tests/assets/glb-metrics.test.sh
mutation_dir=$(mktemp -d)
sed 's/{"EXT_meshopt_compression", "KHR_draco_mesh_compression"}/set()/' \
  scripts/glb_metrics.py >"$mutation_dir/glb_metrics.py"
if GLB_METRICS_SCRIPT="$mutation_dir/glb_metrics.py" bash tests/assets/glb-metrics.test.sh; then
  exit 1
fi
rm -f "$mutation_dir/glb_metrics.py"
rmdir "$mutation_dir"
bash tests/assets/glb-metrics.test.sh
```

The test defines `metrics_script=${GLB_METRICS_SCRIPT:-scripts/glb_metrics.py}` and invokes that variable throughout. Expected: GREEN, then RED against the isolated compression-guard mutation, then GREEN on the untouched tracked implementation. Confirm `git diff --check`.

- [ ] **Step 6: Commit the inspector**

```bash
git add scripts/glb_metrics.py
git commit -m "feat: add strict GLB metrics inspector"
```

---

### Task 4: Version and harden the required silhouette renderer

**Files:**
- Create: `tests/assets/glb-silhouette.test.sh`
- Create: `scripts/glb-silhouette.py`
- Modify: `scripts/glb_metrics.py` only to expose already-validated scene POSITION iteration to the renderer.

**Interfaces:**
- Produces: compatible CLI `python3 scripts/glb-silhouette.py SOURCE.glb OUTPUT.png [YAW_DEGREES]`.
- Produces: optional `--size INT`, `--splat-radius INT`, and `--min-coverage FLOAT` flags; defaults are 520, 2, and 0.01.
- Produces: stdout record `SOURCE -> OUTPUT (N vertices, P filled pixels, C coverage)` and nonzero when coverage is below the stated threshold.
- Consumes: actual accessor bytes and scene transforms validated by `glb_metrics.iter_world_positions(path) -> Iterator[tuple[float, float, float]]`.

- [ ] **Step 1: Add a renderer test that first fails on the absent tracked script**

Build a fixture with two mesh primitives whose POSITION accessors occupy opposite sides of frame. Run the requested three-positional-argument CLI with `--size 128 --splat-radius 4 --min-coverage 0.01` and assert PNG magic, dimensions 128×128, both frame regions contain non-background pixels, reported vertex count includes both primitives, and coverage is at least 1%. Run the sparse eight-vertex fixture at `--size 512 --splat-radius 1 --min-coverage 0.10` and assert the non-vacuity gate fails rather than producing apparently acceptable evidence.

```bash
set +e
bash tests/assets/glb-silhouette.test.sh >/tmp/glb-silhouette-red.txt 2>&1
red_rc=$?
set -e
test "$red_rc" -ne 0
sed -n '1,120p' /tmp/glb-silhouette-red.txt
rg -n 'glb-silhouette.py|No such file|can.t open file' /tmp/glb-silhouette-red.txt
```

Expected: RED at the absent production renderer, not in fixture setup. Commit only the new test/fixture extension:

```bash
git add tests/assets/glb_fixture.py tests/assets/glb-silhouette.test.sh
git commit -m "test: pin silhouette evidence non-vacuity red"
```

- [ ] **Step 2: Expose validated world-position iteration**

Refactor the Task 3 accessor decoder so `iter_world_positions` traverses every scene-reachable mesh node and every primitive, yields each actual POSITION after its composed node transform, and shares the same checked buffer/accessor logic used by `inspect_glb`. It must reject sparse or non-float/VEC3 POSITION accessors rather than misrender them. `inspect_glb` continues to cache local extrema per accessor for speed.

- [ ] **Step 3: Implement a non-vacuous all-primitive renderer**

Start from the user-requested script's pure-stdlib PNG writer and positional CLI, but make the branch copy independently reviewable and reproducible. Rotate all positions by yaw, normalize x/y with 8% padding, keep a z-buffer, and draw a filled circular splat of configurable radius around every projected vertex. A pixel is occupied when any splat writes depth. Default background remains warm paper `(250, 246, 236)` and occupied pixels retain near/far depth shading. Reject fewer than three finite positions, zero span, invalid size/radius, and occupied coverage below `--min-coverage`.

The depth-tested splat loop is exactly:

```python
radius_squared = splat_radius * splat_radius
for x, y, z in projected_positions:
    px = round(padding + (x - minimum_x) * scale)
    py = round(size - padding - (y - minimum_y) * scale)
    for delta_y in range(-splat_radius, splat_radius + 1):
        for delta_x in range(-splat_radius, splat_radius + 1):
            if delta_x * delta_x + delta_y * delta_y > radius_squared:
                continue
            target_x, target_y = px + delta_x, py + delta_y
            if 0 <= target_x < size and 0 <= target_y < size:
                offset = target_y * size + target_x
                if depth[offset] is None or z > depth[offset]:
                    depth[offset] = z
filled_pixels = sum(value is not None for value in depth)
coverage = filled_pixels / (size * size)
if coverage < minimum_coverage:
    raise RenderError(f"coverage {coverage:.6f} below {minimum_coverage:.6f}")
```

The prose above fixes the complete raster behavior; do not add lighting, material, network, GUI, PIL, NumPy, or another dependency. Set `sys.dont_write_bytecode = True` before importing `glb_metrics` so normal test and real invocations leave no `__pycache__`.

- [ ] **Step 4: Run GREEN plus an all-primitives mutation**

The test defines `silhouette_script=${SILHOUETTE_SCRIPT:-scripts/glb-silhouette.py}`. Run it GREEN, then create an isolated mutant that stops after primitive zero and assert the two-region test turns RED; do not overwrite the tracked script. Run the untouched test GREEN again and confirm `git status --short` contains no bytecode cache.

```bash
bash tests/assets/glb-silhouette.test.sh
mutation_dir=$(mktemp -d)
cp scripts/glb_metrics.py "$mutation_dir/"
sed 's/for primitive in primitives:/for primitive in primitives[:1]:/' \
  scripts/glb-silhouette.py >"$mutation_dir/glb-silhouette.py"
if SILHOUETTE_SCRIPT="$mutation_dir/glb-silhouette.py" bash tests/assets/glb-silhouette.test.sh; then
  exit 1
fi
rm -f "$mutation_dir/glb_metrics.py" "$mutation_dir/glb-silhouette.py"
rmdir "$mutation_dir"
bash tests/assets/glb-silhouette.test.sh
```

- [ ] **Step 5: Commit the renderer implementation**

```bash
git add scripts/glb_metrics.py scripts/glb-silhouette.py
git commit -m "feat: add reproducible GLB silhouette renderer"
```

---

### Task 5: Freeze the batch-pipeline behavior RED

**Files:**
- Create: `tests/assets/fake_blender.py`
- Create: `tests/assets/glb-decimation-pipeline.test.sh`

**Interfaces:**
- Produces: fake Blender `--background --version` output with Blender 5.1.2/build `ec6e62d40fa9` and processing support for arguments after `--`: `--source PATH --output PATH --source-triangles INT --target-triangles INT --minimum-triangles INT --maximum-triangles INT`.
- Produces: environment controls `FAKE_BLENDER_MODE=success|over_budget|under_budget|malformed_output|missing_uv|missing_material|missing_image|bounds_drift|external_image|unsupported_extension|unexpected_scene_content|fail` and `FAKE_BLENDER_LOG=PATH`.
- Defines: orchestrator CLI `python3 scripts/decimate-assets.py --manifest PATH --input-dir DIR --output-dir DIR --blender PATH [--force]`.
- Consumes: `write_glb` from Task 1 and `inspect_glb`/`compare_preservation` from Task 2.

- [ ] **Step 1: Add a fake Blender that proves argument-vector isolation**

```python
#!/usr/bin/env python3
if "--version" in sys.argv:
    print(f"Blender {os.environ.get('FAKE_BLENDER_VERSION', '5.1.2')}")
    print(f"build hash: {os.environ.get('FAKE_BLENDER_BUILD_HASH', 'ec6e62d40fa9')}")
    raise SystemExit(0)

separator = sys.argv.index("--")
parser = argparse.ArgumentParser()
parser.add_argument("--source", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--source-triangles", type=int, required=True)
parser.add_argument("--target-triangles", type=int, required=True)
parser.add_argument("--minimum-triangles", type=int, required=True)
parser.add_argument("--maximum-triangles", type=int, required=True)
args = parser.parse_args(sys.argv[separator + 1:])
mode = os.environ.get("FAKE_BLENDER_MODE", "success")
if log := os.environ.get("FAKE_BLENDER_LOG"):
    with open(log, "a", encoding="utf-8") as handle:
        handle.write(json.dumps({"argv": sys.argv, "target": args.target_triangles}) + "\n")
if mode == "fail":
    raise SystemExit(17)
if mode == "malformed_output":
    args.output.write_bytes(b"not glTF")
    raise SystemExit(0)
triangles = {
    "over_budget": args.target_triangles + 1,
    "under_budget": args.target_triangles - 2_001,
}.get(mode, args.target_triangles)
write_glb(args.output, triangles=triangles,
          include_uv=mode != "missing_uv",
          include_material=mode != "missing_material",
          include_image=mode != "missing_image",
          external_image=mode == "external_image",
          extensions=("VENDOR_unreviewed",) if mode == "unsupported_extension" else (),
          add_scene_content=mode == "unexpected_scene_content",
          translation=(100.0, 0.0, 0.0) if mode == "bounds_drift" else (0.0, 0.0, 0.0))
```

Make this test helper executable and resolve its import of `glb_fixture.py` relative to `__file__`, not the caller's current directory.

- [ ] **Step 2: Add a two-entry happy-path custody test**

Create one 30,000-triangle cat and one 20,000-triangle prop with paid sidecars whose `sha256` values match. Run the future orchestrator through the fake and assert:

```bash
python3 scripts/decimate-assets.py \
  --manifest "$tmp/manifest.json" \
  --input-dir "$tmp/input" \
  --output-dir "$tmp/output" \
  --blender "$repo/tests/assets/fake_blender.py"
```

The fake log must show the literal argument prefix `--background`, `--factory-startup`, `--offline-mode`, `--disable-autoexec`, `--threads`, `1`, `--python-exit-code`, `97`, `--python`, the tracked driver path, and separate post-`--` arguments; it must show targets `[15000, 10000]` in manifest order. Assert output triangle counts, source hashes unchanged, two provenance sidecars, and recursively assert no key name or string value matches `api[_-]?key|token|secret|authorization|credential|bearer|https?://` case-insensitively.

- [ ] **Step 3: Add fail-closed table cases**

Each case gets a fresh destination and asserts nonzero, a specific diagnostic, no final GLB/JSON, and whether the fake log remained unchanged:

| Case | Setup | Required diagnostic | Fake reached |
|---|---|---|---|
| malformed manifest | JSON root/list/entry has wrong type or duplicate id/out | `invalid manifest` | no |
| unsupported kind | manifest kind is `station` | `unsupported kind` | no |
| missing source | manifest names an absent bare GLB | `missing source` | no |
| missing sidecar | source exists without `.glb.json` | `missing source sidecar` | no |
| bad source magic | source begins `NOTGLTF` | `invalid GLB header` | no |
| bad source SHA | sidecar SHA is 64 zeroes | `source SHA-256 mismatch` | no |
| unpaid source | `plan_tier` is `unknown` | `plan_tier must be paid` | no |
| path escape | manifest `out` is `../escape.glb` | `bare .glb filename` | no |
| symlink escape | bare source or output leaf resolves outside its selected root | `path escapes` | no |
| wrong Blender | `FAKE_BLENDER_VERSION=5.2.0` | `requires Blender 5.1.2` | no asset call |
| source already small | source triangles equal its category target | `already within budget` | no |
| pre-existing destination | sentinel GLB and JSON already exist | `refusing existing derivative` | no |
| Blender failure | `FAKE_BLENDER_MODE=fail` | `Blender failed` | yes |
| malformed derivative | `FAKE_BLENDER_MODE=malformed_output` | `invalid GLB header` | yes |
| above category band | `FAKE_BLENDER_MODE=over_budget` | `triangle band` | yes |
| below category band | `FAKE_BLENDER_MODE=under_budget` | `triangle band` | yes |
| missing UV | `FAKE_BLENDER_MODE=missing_uv` | `lost UV` | yes |
| missing material | `FAKE_BLENDER_MODE=missing_material` | `material count changed` | yes |
| missing embedded image | `FAKE_BLENDER_MODE=missing_image` | `embedded-image count changed` | yes |
| bounds drift | `FAKE_BLENDER_MODE=bounds_drift` | `center drift` | yes |
| external image | `FAKE_BLENDER_MODE=external_image` | `external URI` | yes |
| arbitrary extension | `FAKE_BLENDER_MODE=unsupported_extension` | `unsupported extension` | yes |
| active scene payload | `FAKE_BLENDER_MODE=unexpected_scene_content` | `animation/camera/light` | yes |

Also pass an input directory and output directory containing spaces and shell metacharacters in their names and prove no command substitution/file creation occurs outside the exact temporary root. Put an executable `curl` sentinel first on `PATH`; it must remain uncalled in every success/failure leg, and a Python AST check must reject imports of `socket`, `urllib`, `http`, or `requests` in all three production scripts.

- [ ] **Step 4: Pin explicit force and pair promotion**

With a valid pre-existing sentinel pair, assert default refusal leaves both hashes unchanged. Then run `--force` with `FAKE_BLENDER_MODE=over_budget` and assert both old hashes remain unchanged. Finally run `--force` with success and assert both are replaced, both validate, and the new sidecar's derivative hash matches the new GLB.

Import `decimate-assets.py` through `importlib.util.spec_from_file_location` and test its public helpers `write_staged_provenance(path, record)` and `promote_pair(staged_glb, staged_json, final_glb, final_json, force)`. Patch the module's `os.replace` so the second promotion raises `OSError`; assert a new destination leaves neither final and a forced destination restores both old hashes. Patch `Path.open` to fail during staged provenance creation and assert promotion is never invoked. These are test-process fault injections, not production flags.

- [ ] **Step 5: Run the test and capture honest RED**

```bash
set +e
bash tests/assets/glb-decimation-pipeline.test.sh >/tmp/glb-decimation-red.txt 2>&1
red_rc=$?
set -e
test "$red_rc" -ne 0
sed -n '1,160p' /tmp/glb-decimation-red.txt
rg -n 'decimate-assets.py|No such file|can.t open file' /tmp/glb-decimation-red.txt
```

Expected: the failure reaches the missing `scripts/decimate-assets.py`; test-fixture or fake-Blender failures must be repaired before accepting RED.

- [ ] **Step 6: Commit the RED-only batch contract**

```bash
git add tests/assets/fake_blender.py tests/assets/glb-decimation-pipeline.test.sh
git commit -m "test: pin decimation custody and budgets red"
```

---

### Task 6: Implement the Blender driver and atomic orchestrator

**Files:**
- Create: `scripts/blender_decimate.py`
- Create: `scripts/decimate-assets.py`
- Test: `tests/assets/glb-decimation-pipeline.test.sh`

**Interfaces:**
- Produces: Blender driver CLI after `--`: `--source PATH --output PATH --source-triangles INT --target-triangles INT --minimum-triangles INT --maximum-triangles INT`.
- Produces: orchestrator CLI fixed in Task 5.
- Consumes: `glb_metrics.inspect_glb`, `glb_metrics.compare_preservation`, manifest entries with `id/service/kind/prompt/out`, and source-sidecar fields `service/task_id/timestamp_utc/plan_tier/prompt/note/sha256`.

- [ ] **Step 1: Implement a one-asset Blender 5.1.2 driver**

The driver runs only inside Blender, checks `bpy.app.version == (5, 1, 2)` and build hash `ec6e62d40fa9`, deletes factory objects through `bpy.data.objects.remove(obj, do_unlink=True)`, imports one source, and checks every operator result is exactly `{"FINISHED"}` because Blender operators can return `{"CANCELLED"}` without raising. Require the frozen one-mesh/one-primitive static-input invariant and reject cameras, lights, animation data/actions, armatures/skins, shape keys/morphs, unsupported object types, or no mesh. Triangulate, cross-check the in-memory triangle count with the outer inspector's source count passed as an argument, and apply collapse Decimate using `target / source_triangles`; refuse a source already at/below target and do not silently retry an undershoot.

Use these locally verified importer controls:

> **Task 8b supersession (2026-08-16):** The mandatory textured/lit review
> found that the original importer settings allowed split seam vertices to move
> independently during collapse decimation. The corrected `merge_vertices=True`
> and `import_shading="SMOOTH"` literals below supersede only those two Task 6
> values; all other Task 6 requirements remain unchanged.

```python
result = bpy.ops.import_scene.gltf(
    filepath=str(source),
    loglevel=1,
    import_pack_images=True,
    merge_vertices=True,
    import_shading="SMOOTH",
    import_webp_texture=False,
    import_unused_materials=False,
    import_select_created_objects=True,
    import_scene_extras=False,
    import_scene_as_collection=True,
    import_merge_material_slots=True,
)
if result != {"FINISHED"}:
    raise RuntimeError(f"GLB import returned {result}")
```

```python
def apply_modifier(obj, modifier):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    result = bpy.ops.object.modifier_apply(
        modifier=modifier.name,
        report=True,
        merge_customdata=True,
        single_user=True,
        all_keyframes=False,
        use_selected_objects=False,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"modifier apply returned {result}")

for obj in mesh_objects:
    triangulate = obj.modifiers.new("CatMetroTriangulate", "TRIANGULATE")
    apply_modifier(obj, triangulate)
    decimate = obj.modifiers.new("CatMetroCollapseDecimate", "DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = ratio
    decimate.use_collapse_triangulate = True
    apply_modifier(obj, decimate)
```

After applying modifiers, call `mesh.calc_loop_triangles()` and require the total in-memory count to fall inside the category band passed by the orchestrator; an undershoot or overshoot exits 97 and leaves only the staging directory. The exported GLB inspector remains the final authority.

Preserve object/node transforms and materials; do not apply transforms, remesh, generate UVs, resize textures, pack external files, or touch the source. Export exactly once as ordinary GLB and require `{"FINISHED"}`. Pass explicit Blender 5.1.2 keywords: `check_existing=False`, `export_format="GLB"`, `export_image_format="AUTO"`, `export_image_add_webp=False`, `export_image_webp_fallback=False`, `export_keep_originals=False`, `export_texcoords=True`, `export_normals=True`, `export_tangents=False`, `export_materials="EXPORT"`, `export_unused_images=False`, `export_unused_textures=False`, `export_attributes=False`, `export_gn_mesh=False`, `use_mesh_edges=False`, `use_mesh_vertices=False`, `use_selection=False`, `use_visible=False`, `use_renderable=False`, `use_active_collection=False`, `use_active_scene=True`, `export_extras=False`, `export_yup=True`, `export_apply=False`, `export_animations=False`, `export_skins=False`, `export_morph=False`, `export_cameras=False`, `export_lights=False`, `export_draco_mesh_compression_enable=False`, `export_use_gltfpack=False`, `export_gpu_instances=False`, `export_hierarchy_full_collections=False`, `export_extra_animations=False`, and `will_save_settings=False`. The unique temporary output path must already end in `.glb`, because Blender otherwise appends the extension. Fail if the output file is absent or empty.

- [ ] **Step 2: Implement manifest, path, custody, and version validation**

In `decimate-assets.py`, define exact policy constants and refuse unsupported categories:

```python
BLENDER_VERSION = "5.1.2"
POLICY = {
    "cat": {"target": 15_000, "minimum": 13_500, "maximum": 15_000},
    "prop": {"target": 10_000, "minimum": 9_000, "maximum": 10_000},
}
REQUIRED_SOURCE_FIELDS = {
    "service", "task_id", "timestamp_utc", "plan_tier", "prompt", "note", "sha256"
}
```

Set `sys.dont_write_bytecode = True` before importing `glb_metrics`; the repository has no Python cache ignore rule and normal execution must not create untracked bytecode.

Validate the manifest root and assets list, unique non-empty ids/out values, known kind/service, exact bare `.glb` filename, resolved input/output containment including symlink leaves, source/source-sidecar existence, `glTF` structure through the inspector, required sidecar fields, matching manifest service/prompt, exact lowercase SHA-256 match, paid tier, frozen source structure, and source triangle count strictly above its category target. Snapshot source and sidecar hashes before any subprocess. Resolve Blender with `shutil.which` only when `--blender` is absent; call `[blender, "--background", "--version"]` with `shell=False`, sanitized environment, captured text, and accept only version `5.1.2` plus build hash `ec6e62d40fa9`.

- [ ] **Step 3: Launch one argument-vector Blender process per asset**

Create a private staging directory inside the output directory. For each asset, build this literal list and run with no shell, no stdin, a bounded timeout, and the sanitized child environment:

```python
command = [
    str(blender), "--background", "--factory-startup", "--offline-mode",
    "--disable-autoexec", "--threads", "1", "--python-exit-code", "97",
    "--python", str(driver), "--",
    "--source", str(source_path),
    "--output", str(staged_glb),
    "--source-triangles", str(source_metrics["triangles"]),
    "--target-triangles", str(policy["target"]),
    "--minimum-triangles", str(policy["minimum"]),
    "--maximum-triangles", str(policy["maximum"]),
]
subprocess.run(
    command,
    check=True,
    shell=False,
    stdin=subprocess.DEVNULL,
    timeout=1800,
    env=child_env,
)
```

Before launch, create `child_env = os.environ.copy()` and delete every entry whose uppercase name contains `KEY`, `TOKEN`, `SECRET`, `AUTH`, `CREDENTIAL`, or `BEARER`. Log only asset id, category, target, source triangle count, and final metrics; never log the complete inherited environment or source provenance.

- [ ] **Step 4: Validate output and write provenance before promotion**

Inspect the staged output, enforce category and global bands, call `compare_preservation`, recalculate both input hashes to prove custody, and build schema version 1 exactly as:

```python
record = {
    "schema_version": 1,
    "source": {
        "filename": source_path.name,
        "sha256": source_sha,
        "sidecar_sha256": source_sidecar_sha,
        "provenance": {name: source_record[name] for name in sorted(REQUIRED_SOURCE_FIELDS - {"sha256"})},
    },
    "derivative": {
        "filename": final_glb.name,
        "sha256": output_metrics["sha256"],
    },
    "tool": {
        "name": "Blender",
        "version": BLENDER_VERSION,
        "build_hash": "ec6e62d40fa9",
        "operation": "collapse-decimate",
        "timestamp_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    },
    "geometry": {
        "category": kind,
        "target_triangles": policy["target"],
        "accepted_minimum": policy["minimum"],
        "accepted_maximum": policy["maximum"],
        "source": metric_subset(source_metrics),
        "output": metric_subset(output_metrics),
    },
}
```

`metric_subset` contains `triangles`, `vertices`, `primitives`, `materials`, `material_primitives`, `images`, `embedded_images`, `uv_primitives`, `animations`, `cameras`, `lights`, `skins`, `morph_targets`, `extensions_used`, `extensions_required`, and `world_bounds`. Serialize sorted/indented UTF-8 plus newline to a staged JSON file, rescan all keys and string values for the forbidden secret shapes, and verify its recorded derivative hash before promotion.

- [ ] **Step 5: Implement pair-safe promotion and force rollback**

Refuse if either final path exists unless `--force`; if exactly one member of the pair exists, fail even with `--force` because lineage is already inconsistent. For normal promotion, replace staged GLB then staged JSON and remove the GLB if the second replace raises. For force, first move the exact old GLB/JSON pair to unique sibling backup names, promote both staged files, and restore both backups if either promotion raises. Remove only exact staging/backup paths in `finally`; never recurse over input/output roots. A successful force removes both backups.

- [ ] **Step 6: Run focused test to GREEN and prove mutations discriminate**

```bash
bash tests/assets/glb-metrics.test.sh
bash tests/assets/glb-decimation-pipeline.test.sh
mutation_dir=$(mktemp -d)
cp scripts/glb_metrics.py scripts/blender_decimate.py "$mutation_dir/"
sed 's/"cat": {"target": 15_000/"cat": {"target": 10_000/' \
  scripts/decimate-assets.py >"$mutation_dir/decimate-assets.py"
if DECIMATE_SCRIPT="$mutation_dir/decimate-assets.py" bash tests/assets/glb-decimation-pipeline.test.sh; then
  exit 1
fi
rm -f "$mutation_dir/glb_metrics.py" "$mutation_dir/blender_decimate.py" "$mutation_dir/decimate-assets.py"
rmdir "$mutation_dir"
bash tests/assets/glb-decimation-pipeline.test.sh
```

The test defines `decimate_script=${DECIMATE_SCRIPT:-scripts/decimate-assets.py}` and invokes that variable throughout. Expected: both focused tests GREEN, the isolated swapped-budget mutant RED, and the untouched tracked implementation GREEN. Syntax-check each production Python file with `compile(Path(filename).read_bytes(), filename, "exec")` so verification cannot create `__pycache__`, then run `git diff --check`.

- [ ] **Step 7: Commit the implementation**

```bash
git add scripts/glb_metrics.py scripts/blender_decimate.py scripts/decimate-assets.py
git commit -m "feat: add headless Blender decimation pipeline"
```

---

### Task 7: Record the operator contract

**Files:**
- Create: `docs/design/assets/DECIMATION.md`
- Modify: `docs/design/assets/PIPELINE.md`

**Interfaces:**
- Produces: exact local operator command and validation/recovery semantics.
- Consumes: frozen contract, ADR-0012 from Task 1, finalized CLIs, focused test names, and actual Blender 5.1.2 behavior.

- [ ] **Step 1: Write the operator document and generation cross-link**

`DECIMATION.md` must include the exact default and explicit-path commands, policy table, sidecar schema example with non-secret dummy hashes, bounds formulas/tolerances, atomic/force behavior, expected logs, offline posture, real-validation checklist, and a warning that local derivatives may not enter tracked Unity assets until the separate generated-asset license ADR and curation contract approve them.

Append one short section to `PIPELINE.md` linking to `DECIMATION.md` and stating generation source custody ends at `incoming/{manifest-out}`; decimation is separate, offline, derivative-only, and does not change the generation/service/license record.

- [ ] **Step 2: Verify docs against implementation and tests**

```bash
rg -n '5\.1\.2|15,000|10,000|13,500|9,000|incoming/decimated|--background|--factory-startup' \
  docs/adr/0012-blender-headless-glb-decimation.md docs/design/assets/DECIMATION.md
rg -n 'DECIMATION.md' docs/design/assets/PIPELINE.md
git diff --check
```

- [ ] **Step 3: Commit documentation**

```bash
git add docs/design/assets/DECIMATION.md docs/design/assets/PIPELINE.md
git commit -m "docs: document GLB decimation operations"
```

---

### Task 8: Run the real 15-asset pipeline and render visual proof

**Files:**
- Create: local ignored `unity/Assets/Art/Generated/incoming/decimated/*.glb`
- Create: local ignored `unity/Assets/Art/Generated/incoming/decimated/*.glb.json`
- Create: `docs/design/assets/GLB-DECIMATION-EVIDENCE.md`
- Create: `docs/design/assets/GLB-DECIMATION-METRICS.json`
- Create: local untracked `/Users/sushantsrikrish/cat-metro-app/.catshots/glb-decimation-2026-08-15/before-grid.png`
- Create: local untracked `/Users/sushantsrikrish/cat-metro-app/.catshots/glb-decimation-2026-08-15/after-grid.png`
- Create: local untracked `/Users/sushantsrikrish/cat-metro-app/.catshots/glb-decimation-2026-08-15/comparison-grid.png`

**Interfaces:**
- Consumes: production scripts from Tasks 3/4/6, the worktree manifest at exact HEAD, and only the main checkout's ignored input/output roots by absolute path.
- Produces: all 15 valid local derivative pairs, exact metrics, and visually inspected evidence in manifest order.

- [ ] **Step 1: Establish a clean technical baseline**

```bash
bash tests/assets/glb-metrics.test.sh
bash tests/assets/glb-silhouette.test.sh
bash tests/assets/glb-decimation-pipeline.test.sh
bash scripts/check.sh
git diff --check
git status --short
```

Expected: focused tests and check PASS; only intentional uncommitted evidence paths may appear after this point.

- [ ] **Step 2: Run the real queue with explicit absolute paths**

```bash
set +e
python3 scripts/decimate-assets.py \
  --manifest "$PWD/docs/design/assets/CAT-MANIFEST.json" \
  --input-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming \
  --output-dir /Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated \
  --blender /opt/homebrew/bin/blender \
  >/tmp/glb-decimation-real.txt 2>&1
real_rc=$?
set -e
sed -n '1,240p' /tmp/glb-decimation-real.txt
test "$real_rc" -eq 0
```

Expected: 15/15 accepted; cats each 13,500–15,000, props each 9,000–10,000; no GUI opens. If a candidate misses a band or preservation gate, preserve its staged diagnostic, inspect the cause, add a failing regression when generalizable, and make the smallest implementation correction before rerunning that asset or the clean queue. Never weaken a target or preservation test.

- [ ] **Step 3: Build and validate the machine-readable metrics record**

Create `GLB-DECIMATION-METRICS.json` in manifest order with asset id, kind, source/derivative filenames and SHA-256, source/output bytes, vertices, triangles, materials, embedded images, UV primitives, world bounds, reduction percentage, and sidecar hash agreement. Add a final totals object. Then independently assert:

```bash
python3 - docs/design/assets/GLB-DECIMATION-METRICS.json <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
assert len(d["assets"]) == 15
assert sum(a["kind"] == "cat" for a in d["assets"]) == 10
assert sum(a["kind"] == "prop" for a in d["assets"]) == 5
for a in d["assets"]:
    lo, hi = (13500, 15000) if a["kind"] == "cat" else (9000, 10000)
    assert lo <= a["output"]["triangles"] <= hi
    assert 5000 <= a["output"]["triangles"] <= 20000
    assert a["derivative_sha256"] == a["sidecar_derivative_sha256"]
    assert a["output"]["embedded_images"] == a["source"]["embedded_images"]
PY
```

- [ ] **Step 4: Render every source and derivative identically**

Render from the worktree so both source and derivative use the exact tracked renderer at HEAD:

```bash
render_root="/Users/sushantsrikrish/cat-metro-app/.catshots/glb-decimation-2026-08-15"
mkdir -p "$render_root/before" "$render_root/after"
while IFS=$'\t' read -r id out; do
  python3 scripts/glb-silhouette.py \
    "/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/$out" \
    "$render_root/before/$id.png" 25
  python3 scripts/glb-silhouette.py \
    "/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming/decimated/$out" \
    "$render_root/after/$id.png" 25
done < <(jq -r '.assets[] | [.id, .out] | @tsv' docs/design/assets/CAT-MANIFEST.json)
```

The loop's manifest order is authoritative. First view all 30 individual files and require every renderer invocation's 1% coverage gate. If the already-installed `magick` is available, record `magick -version` and use `+append` for five-image rows and `-append` for three-row grids; do not use `montage` because this machine's implicit-font lookup was already proven broken. Create `before-grid.png`, `after-grid.png`, and a stacked comparison whose odd rows are sources and following even rows are matching derivatives. Contact sheets are convenience only: absence/failure of ImageMagick does not replace or fail individual inspection. Keep all generated-art depictions untracked until the separate license ADR authorizes publication.

- [ ] **Step 5: Actually view and disposition the renders**

Open `before-grid.png`, `after-grid.png`, and `comparison-grid.png` with the image-viewing tool, but treat the 30 individual renderer outputs and their coverage values as the acceptance evidence; the contact sheets are convenience views and ImageMagick availability is not a gate. Inspect individual pairs for ears, tails, paws, conductor hat/baton, tree branches, locomotive silhouette, depot roof, kiosk sign, desk clutter, and every existing plinth. Record PASS/FAIL per asset in `GLB-DECIMATION-EVIDENCE.md`; explicitly record the pre-existing plinth inconsistency as deferred to its separate contract, not as a decimation regression. Any ambiguous pair is opened individually at original detail before verdict.

- [ ] **Step 6: Write the evidence record and commit tracked evidence**

`GLB-DECIMATION-EVIDENCE.md` records base/head SHAs, commands, RED transcript excerpts, mutation outcomes, tool versions, 15 metrics rows, total byte/triangle reduction, per-asset visual verdicts and coverage, ignored render paths plus SHA-256 values, local derivative path, source immutability hashes, and the statement that neither generated GLBs nor generated-art depictions are tracked or licensed to ship by this PR.

```bash
git add docs/design/assets/GLB-DECIMATION-EVIDENCE.md docs/design/assets/GLB-DECIMATION-METRICS.json
git commit -m "evidence: record 15-asset decimation proof"
```

---

### Task 9: Full verification, fresh-context review, and human-merge PR

**Files:**
- Modify: implementation/docs/tests/evidence only if a concrete review finding requires it.
- Modify: `state/PROJECT_STATE.md` for the bounded final handoff.

**Interfaces:**
- Consumes: exact frozen contract, all commits/diffs, RED/GREEN transcript, metrics, and renders.
- Produces: independently reviewed exact head, PR with criterion evidence, and an explicit human merge request because ADR-0012 is proposed.

- [ ] **Step 1: Run strongest local verification from exact head**

```bash
python3 - scripts/glb_metrics.py scripts/glb-silhouette.py scripts/blender_decimate.py scripts/decimate-assets.py <<'PY'
from pathlib import Path
import sys
for filename in sys.argv[1:]:
    compile(Path(filename).read_bytes(), filename, "exec")
PY
bash tests/assets/glb-metrics.test.sh
bash tests/assets/glb-silhouette.test.sh
bash tests/assets/glb-decimation-pipeline.test.sh
bash scripts/check.sh
bash scripts/test.sh
bash scripts/build.sh
git diff --check origin/main..HEAD
git status --short
```

Capture exit codes and elapsed times. If the full Unity-backed suite is long-running, keep it in a resumable process, report progress at least every 60 seconds, and do not represent partial output as PASS.

- [ ] **Step 2: Dispatch a fresh-context reviewer at the exact head**

Give a new reviewer only: AGENTS.md, the frozen contract, plan, exact base/head SHAs, full diff, focused/full gate results, mutation proof, `docs/design/assets/GLB-DECIMATION-METRICS.json`, and absolute local paths plus hashes for all 30 ignored renders and any three convenience grids. Ask for findings ordered by severity with concrete failure scenarios, explicit contract-criterion mapping, and a statement of which visual evidence was actually viewed. The authoring agent must not approve its own diff.

- [ ] **Step 3: Disposition every finding under review discipline**

For each finding, reproduce or inspect the cited scenario before editing. Add a RED regression for confirmed behavior defects, implement the smallest correction, rerun focused and affected full gates, regenerate metrics/renders if asset bytes or visual output changed, and return the new exact SHA to a fresh reviewer. Record evidence when a finding is rejected with concrete proof rather than agreement language.

- [ ] **Step 4: Update project state within its hard line cap**

Record the decimation branch/PR/head, 15/15 local derivative status, exact targets/results, evidence path, proposed ADR-0012 human gate, and remaining sequence: plinth normalization, board/Home wiring, generated-asset license ADR. Rotate the oldest history block to a dated file under `state/archive/` if needed; do not exceed the existing cap.

- [ ] **Step 5: Push and open the PR with complete evidence**

```bash
git push -u origin task/GLB-DECIMATION
gh pr create --base main --head task/GLB-DECIMATION \
  --title "GLB-DECIMATION: add 15k/10k offline asset pipeline" \
  --body-file /tmp/glb-decimation-pr-body.md
```

The body restates the frozen contract, references ADR-0012, maps AC1–AC9 to exact commands/artifacts, includes the RED-first and mutation record, gives before/after totals and per-asset visual verdicts without publishing the ignored renders, lists reviewer findings/dispositions, states `generated GLBs tracked: 0` and `generated-art depictions tracked: 0`, and marks `MERGE: HUMAN REQUIRED — proposed ADR`. Do not arm auto-merge.

- [ ] **Step 6: Monitor exact-head CI and complete the census record after human merge**

Check `gh pr view`/`gh pr checks` against the reviewed head; stale-success checks do not count. After the human approves ADR-0012 and squash-merges the exact reviewed head, post the repository's census merge-record with PR number, reviewed head, squash SHA, reviewer identity, findings disposition, test/evidence summary, and human-merger fact. Pull `origin/main` before beginning the separately frozen plinth contract.
