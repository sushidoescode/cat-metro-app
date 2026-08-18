# GLB-DECIMATION — frozen contract and approved design

- **Status:** FROZEN before implementation
- **Frozen against:** `origin/main` at
  `3115ebdddd23f3d7eb6836c2670f6dfc2d0a6fb4`
- **Date:** 2026-08-15
- **Owner:** Cat Metro takeover coordination session
- **Branch:** `task/GLB-DECIMATION`
- **Contract rule:** this document is the branch's first commit. Changes to an
  acceptance criterion require a recorded human amendment; implementation may
  clarify mechanics but may not silently weaken the contract.

## Human directive and approval

The governing 2026-08-15 directive makes decimation the blocker to shipping:
generated candidates are roughly 1.43M triangles each against a 30k vendor
request, and the requested result is approximately 5k–20k triangles per asset,
followed by before/after silhouette renders through
`scripts/glb-silhouette.py` that are actually inspected.

The human selected the category policy in-session:

> I'd go with the 15k cats / 10k props

The human then approved the proposed Blender-headless design in-session:

> I approve!

## Measured input facts

The paid generation queue completed before freeze. All 15 entries in
`docs/design/assets/CAT-MANIFEST.json` have a valid local GLB and matching paid-
tier provenance sidecar under the gitignored
`unity/Assets/Art/Generated/incoming/` directory.

- 15 source GLBs total **855,215,420 bytes (815.6 MiB)**.
- Meshy candidates measure approximately 1.92M–1.99M triangles each.
- Tripo candidates measure approximately 1.43M–1.49M triangles each.
- Every current source has one mesh, one primitive, one material, and embedded
  texture images.
- Blender **5.1.2** is already installed at `/opt/homebrew/bin/blender` and was
  verified with `--background --version`.
- `gltfpack` / meshoptimizer are not installed.
- The pre-decimation 15-asset silhouette lineup was rendered and inspected at
  `.catshots/decimation-before-20260815/lineup-grid.png` in the main checkout.
  Geometry is coherent; inconsistent Tripo display plinths are visible but are
  deliberately outside this contract.

## Objective

Add a fail-closed, offline, reproducible-enough development pipeline that uses
Blender 5.1.2 in background mode to create Unity-importable, texture-preserving
GLB derivatives of the 15 local generated candidates without modifying their
source files. Produce and inspect before/after silhouette evidence for every
candidate.

## Selected approach

### Blender headless, one isolated process per asset

The public entry point will be a Python-stdlib orchestration script. It invokes
Blender as an argument vector, never through a shell, with at least:

```text
blender --background --factory-startup --python <driver> -- <arguments>
```

The Blender-side driver imports one GLB, applies collapse decimation to mesh
objects, exports one ordinary binary glTF, and exits. One Blender process per
asset releases the very large source mesh before the next candidate starts and
prevents scene state from leaking between candidates. No GUI process is used.

Blender becomes a pinned offline development dependency, not a Unity package,
runtime library, network service, or shipped component. A proposed ADR-0012
must record the 5.1.2 pin, GPL tool/output boundary, upgrade trigger, offline
posture, and verification duties. Because ADRs are a human merge floor, the PR
cannot be agent-merged even if every technical gate is green.

ADR-0012 is intentional even though `origin/main` currently ends at ADR-0010:
open PR #65 already reserves ADR-0011 for Polyfork asset licensing and custody.

### Rejected alternatives

1. **gltfpack / meshoptimizer:** attractive for speed and dedicated glTF
   simplification, but absent locally; adoption adds a dependency, and Unity's
   support for its compression extension is not proven in this project.
2. **Unity ModelImporter compression/optimization alone:** useful later for
   storage precision and vertex ordering, but it does not reduce source
   triangle count enough to satisfy this contract.
3. **A custom decimator:** avoids Blender but adds a high-risk geometry
   algorithm that this project has no reason to own.

## Interfaces and data flow

The exact filenames may be refined in the implementation plan, but the design
has three responsibilities with explicit boundaries:

1. **Pure GLB inspection (Python standard library):** validate GLB header and
   JSON chunk; report mesh, primitive, vertex, triangle, material, image,
   texture-coordinate, extension, and bounds facts. It never mutates a file.
2. **Blender driver:** import, simplify, export. It knows Blender APIs but does
   not read the manifest, source sidecar, credentials, or repository policy.
3. **Batch orchestrator:** read the manifest, select the category budget,
   validate source custody, launch Blender, validate the candidate, atomically
   promote it inside the local derivative directory, and write provenance.

Default paths:

```text
input:  unity/Assets/Art/Generated/incoming/<manifest out>
output: unity/Assets/Art/Generated/incoming/decimated/<manifest out>
proof:  unity/Assets/Art/Generated/incoming/decimated/<manifest out>.json
```

The output remains beneath the already-gitignored `incoming/` tree. This PR
does not promote or commit any generated mesh. The later asset-license ADR and
curation contract own promotion into the tracked Unity tree.

The orchestrator must accept explicit manifest/input/output paths so the clean
worktree implementation can process the main checkout's local ignored sources
without copying 815.6 MiB into git or into the worktree.

## Triangle policy

- `kind == "cat"`: target **15,000 triangles**; accepted band
  **13,500–15,000**.
- `kind == "prop"`: target **10,000 triangles**; accepted band
  **9,000–10,000**.
- Every derivative must remain inside the global human range
  **5,000–20,000 triangles**.
- No category may silently borrow another category's budget.
- A source already at or below its category target is not re-exported silently;
  the tool reports that it is already within budget and requires an explicit
  curation decision.

The modifier ratio derives from measured source triangles, not vertex count or
file size. The exporter produces triangles; the post-export GLB inspection is
the authority for acceptance.

## Preservation requirements

For each current candidate, the derivative must:

- remain a valid GLB with no external buffer or texture dependency;
- retain UV coordinates, the material, and embedded texture images;
- retain the same scene orientation and scale, with normalized bounds agreeing
  within a documented tight tolerance;
- add no animation, camera, light, script, runtime importer, or mesh-compression
  extension;
- preserve an intelligible silhouette and intentional appendages at the fixed
  evidence angles; and
- import as ordinary asset data rather than executable/editor code.

The pipeline does not promise pixel-identical textures or topology. It promises
the reviewed material/texture boundary, bounded geometry, and visible form.

## Derivative provenance

Each accepted GLB gets an atomically-written JSON sidecar, schema version 1,
containing at least:

- source filename, source SHA-256, and source-sidecar SHA-256;
- original service, task id, generation timestamp, plan tier, prompt, and note;
- derivative filename and SHA-256;
- Blender name, exact version, operation (`collapse-decimate`), and UTC run
  timestamp;
- category, requested target, source/output triangles and vertices;
- source/output material and embedded-image counts; and
- source/output bounds used by the preservation check.

No credential, bearer header, signed download URL, `.env` content, or account
response may enter an argument, sidecar, log, test fixture, render, or diff.

## Failure and overwrite behavior

The pipeline fails closed before Blender when the manifest is malformed, kind
is unsupported, a path escapes its allowed root, a source or sidecar is absent,
the source magic is wrong, the source SHA disagrees with provenance, the tier
is not `paid`, or Blender is missing/wrong-versioned.

It fails closed after Blender when export fails, the candidate is malformed,
the triangle band is missed, required UV/material/image data disappears,
bounds drift, an external URI or unsupported extension appears, or provenance
cannot be written. A failure leaves neither a final derivative nor a final
sidecar. Temporary files live in the destination directory so successful
promotion can use `os.replace` atomically.

Existing derivatives are refused by default. A deliberate `--force` path may
replace an existing derivative only after a new candidate and new sidecar both
pass all checks; it may never modify the source candidate.

## Acceptance criteria

1. **AC1 — frozen design and dependency record:** this contract is the first
   branch commit; proposed ADR-0012 records Blender 5.1.2 and is referenced by
   the PR.
2. **AC2 — honest RED first:** a new asset-pipeline regression test is observed
   failing against the contract-only branch before implementation. No existing
   test is weakened or deleted.
3. **AC3 — category budgets:** the queue selects 15,000 for every manifest cat
   and 10,000 for every manifest prop; post-export triangles meet the category
   bands and the global 5k–20k range.
4. **AC4 — custody and atomicity:** sources and source sidecars are never
   modified; path escapes, stale hashes, unearned paid-tier claims, unexpected
   overwrites, and partial outputs are mutation-tested fail-closed.
5. **AC5 — asset preservation:** all 15 real derivatives retain valid embedded
   materials/textures/UVs, compatible ordinary GLB structure, and stable
   orientation/scale bounds.
6. **AC6 — provenance:** every real derivative sidecar contains the lineage,
   hashes, exact tool version, category target, before/after geometry counts,
   preservation counts, and no secret-shaped field or value.
7. **AC7 — no GUI/network/runtime dependency:** real execution uses background
   Blender only, performs no network call, uses no key, changes no Unity package
   manifest, and adds no runtime importer.
8. **AC8 — rendered evidence:** `scripts/glb-silhouette.py` renders every source
   and derivative at identical size/yaw; the session views the complete
   before/after lineup and separately inspects ambiguous regressions. Code-green
   alone is not acceptance.
9. **AC9 — verification:** focused tests, syntax/static checks, repository
   `scripts/check.sh`, `git diff --check`, and the strongest proportionate full
   suite are run from a clean exact head. A fresh-context reviewer receives the
   frozen contract, full diff, RED/GREEN record, real 15-asset metrics, and
   rendered evidence; every finding is dispositioned before the human merge.

## RED-first test design

The new shell test may generate tiny GLB fixtures in a private temporary
directory and use a fake Blender executable to exercise orchestration without a
GUI or Blender dependency in required CI. Its controls must prove that the fake
boundary was actually reached and that category targets, argument separation,
fail-closed setup, atomic promotion, and provenance assertions discriminate.

The pure GLB inspector receives valid and malformed binary fixtures. Mutations
must include at least: removed implementation entry point, swapped cat/prop
target, bad source SHA, output over budget, missing UV/material/image, and a
pre-existing destination. Real Blender execution over all 15 local sources is
the non-CI demonstration leg; it cannot be replaced by the stub test.

## Scope and sequencing

This is one task contract and one PR. It includes only the decimation tooling,
tests, documentation/ADR, local derivative run, metrics, and visual evidence.

Explicitly out of scope:

- removing, adding, or normalizing display plinths;
- choosing final cat/prop placements or wiring Home/board presentation;
- promoting generated GLBs into tracked Unity assets;
- the Meshy/Tripo generated-asset commercial-license ADR and its human
  signature;
- changing the existing asset prompts or spending more generation credits;
- changing Unity packages, project settings, scenes, prefabs, shaders, or game
  code; and
- any physical-device or emulator interaction.

Those remain subsequent, separately frozen contracts in the human's required
order: plinth normalization, board/Home wiring, then the asset-license decision
before any generated derivative ships.

## Expected reviewed paths

- `state/handoffs/GLB-DECIMATION-frozen-contract.md`
- a proposed `docs/adr/0012-*blender*decimation*.md`
- new decimation/GLB-inspection scripts under `scripts/`
- new non-immutable tests under `tests/assets/`
- focused pipeline documentation under `docs/design/assets/`

No file under `tests/contract/`, `evals/`, `docs/constitution.md`, hooks,
`state/mode`, `.github/`, Unity package manifests, or product/runtime source is
authorized by this contract.
