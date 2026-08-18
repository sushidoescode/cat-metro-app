# ADR-0012: Blender 5.1.2 headless for generated-GLB decimation

- **Status:** Proposed — human merge required; design approved 2026-08-15 in `state/handoffs/GLB-DECIMATION-frozen-contract.md`
- **Date:** 2026-08-15
- **Relates:** ADR-0004, ADR-0008, `state/handoffs/GLB-DECIMATION-frozen-contract.md`

## Context

The 15 paid generated-asset candidates are valid local GLBs, but each currently measures roughly
1.43–1.99 million triangles. That is far above both the vendor request and the human-approved range
of 5,000–20,000 triangles per asset. Before any generated candidate can be curated into the tracked
Unity asset tree, Cat Metro needs a deterministic-enough, fail-closed development tool that reduces
geometry while preserving the ordinary embedded GLB material, texture, UV, orientation, scale, and
silhouette boundaries in the frozen contract.

Blender is already available locally and exposes the needed import, collapse-decimation, and binary
glTF export surfaces. Pinning that surface is a new development dependency and tool boundary, so the
decision must precede tests or behavior implementation under the constitution. The human approved
the Blender-headless design and the category budgets on 2026-08-15; this ADR remains Proposed because
ADR approval and merge are human-only.

## Decision

We will use **Blender 5.1.2**, build **`ec6e62d40fa9`**, with its bundled official
**`io_scene_gltf2` 5.1.20** importer/exporter as a pinned, offline development tool for generated-GLB
decimation. The orchestrator launches Blender without a shell, in background mode with factory
settings and auto-execution disabled, using an argument vector equivalent to:

```text
blender --background --factory-startup --disable-autoexec --python <driver> -- <arguments>
```

Each asset runs in one fresh Blender process so large mesh state is released and cannot leak into the
next asset. The driver imports one source GLB, applies collapse decimation to mesh objects, and exports
one ordinary binary glTF (`.glb`). It does not use a GUI, contact a network service, or execute scripts
embedded in input assets.

Blender is not a Unity package, runtime library, importer, shipped application component, or player
dependency. It is required only for the local real-asset generation leg. The resulting GLBs are
ordinary Unity-importable asset data with embedded buffers and textures. The derivative extension
allowlist is the human-approved empty set, **`[]`**: `extensionsUsed` and `extensionsRequired` must be
absent or empty, so no Draco, meshopt, or other mesh-compression/runtime decoder is introduced.

Sources remain byte-unchanged under the ignored local incoming tree. Derivatives and their provenance
sidecars remain ignored local files under
`unity/Assets/Art/Generated/incoming/decimated/`; this decision does not authorize promotion into the
tracked Unity tree.

Triangle budgets are fixed by manifest category and are checked from post-export GLB geometry:

| Category | Target | Accepted band |
|---|---:|---:|
| `cat` | **15,000 triangles** | **13,500–15,000** |
| `prop` | **10,000 triangles** | **9,000–10,000** |

Every derivative must also remain inside the global human range of 5,000–20,000 triangles. A category
cannot borrow another category's budget, and a source already at or below its target requires an
explicit curation decision instead of silent re-export.

Metrics and structural checks do not decide visual acceptability. Before any derivative is accepted,
a human reviews same-size, same-yaw before/after silhouettes for all 15 assets, with separate close
inspection of every ambiguous appendage or silhouette regression. Code-green alone is insufficient.

## Alternatives seriously considered

- **gltfpack / meshoptimizer.** This is a fast, purpose-built glTF optimization path, but it is not
  installed in the approved environment. Adoption would add another dependency, and Cat Metro has
  not proved that Unity can consume its compression-extension output without a runtime decoder. It
  loses to the already-available Blender path and the empty derivative extension allowlist.
- **Unity importer settings alone.** ModelImporter optimization and compression may later reduce
  storage precision or reorder vertices, but they do not reduce the source triangle count enough to
  meet this contract. They also move the concern into the runtime asset-import boundary instead of
  producing an independently inspectable ordinary GLB.
- **A custom decimator.** A project-owned geometry algorithm could remove the external tool pin, but
  it would add substantial correctness, preservation, performance, and maintenance risk for a solved
  operation. Cat Metro has no product requirement to own that algorithm.

## Consequences

**Easier:** the pipeline uses an installed, inspectable tool with first-party glTF import/export and a
known collapse-decimation operation. One-process isolation bounds cross-asset state, and ordinary GLB
output avoids a Unity runtime package or decoder.

**Harder:** development machines performing real decimation must provide the stock exact Blender
build that carries the verified bundled importer/exporter. A Blender upgrade is deliberate rather
than automatic. Processing
15 source files totaling roughly 815.6 MiB costs local CPU, memory, disk, and review time, while
human silhouette review remains a required non-automated gate.

**Lock-in and reversibility:** the pipeline is coupled to the pinned Blender command line and Python
API, but derivatives remain standard binary glTF and source files remain unchanged, so a replacement
tool can regenerate them. No generated derivative is committed or shipped by this ADR.

## Security notes

Generated GLBs and their provenance are untrusted input. The orchestrator must validate allowed roots,
source hashes, paid-tier provenance, GLB structure, category, version pins, and destination state
before invoking Blender. It passes arguments as a vector rather than through a shell. Blender runs
with `--factory-startup`, `--disable-autoexec`, and `--background`, once per asset, with no credential,
key, signed URL, account response, or network requirement.

After export, the pipeline fails closed unless the derivative is a valid self-contained GLB whose
geometry, UV, material, embedded-image, bounds, and triangle-budget facts meet the frozen contract.
External URIs and every glTF extension are rejected under the empty extension allowlist. Temporary
candidate and sidecar files stay inside the destination directory and are atomically promoted only
after both pass validation. Existing derivatives are not overwritten without the explicit guarded
force path, and source candidates are never modified.

This boundary makes GLB files asset data, not trusted executable/editor code. It does not authorize a
Unity package, runtime importer, embedded script, camera, light, animation, or network-capable add-on.

## License boundary

Blender is GPL-licensed tooling. Blender's official license page identifies Blender's source as GNU
GPL version 2 or later and binary distributions as compatible under GNU GPL version 3 or later. GNU
GPLv3 section 2 states that output from running GPL-covered software is covered only when the output's
content is itself a covered work. Blender's official FAQ likewise states that an artist's creations
and Blender output data are the artist's property rather than automatically acquiring Blender's GPL.

That tool/output boundary does not erase other rights or terms. Imported or bundled data remains
subject to its pre-existing license and provenance, Blender components retain their applicable
licenses, and published add-ons or Python scripts using Blender's API remain subject to Blender's
stated GPL-compliance requirements. The future Blender-side driver and every third-party input must
therefore be reviewed under their own applicable terms; ordinary GLB output is not automatically GPL
merely because Blender produced it.

This ADR makes no finding about Meshy or Tripo output ownership, commercial use, attribution, plan
tier, or redistribution rights. Those questions belong to the later generated-asset license and
custody ADR before any derivative ships.

Primary sources retrieved **2026-08-15**:

- Blender Foundation, "License": https://www.blender.org/about/license/
- Blender Foundation, "Frequently Asked Questions": https://www.blender.org/support/faq/
- Free Software Foundation, "GNU General Public License, version 3":
  https://www.gnu.org/licenses/gpl-3.0.html

## Verification and upgrade trigger

The local approved surface was verified as Blender **5.1.2**, build **`ec6e62d40fa9`**, with bundled
`io_scene_gltf2` **5.1.20**, while running in background/factory-startup/autoexec-disabled mode. The
implementation fails closed if either reported Blender identity value differs. The add-on version is
a transitively pinned property of the approved stock bundle and is not independently queried; a
modified bundle that retains the same reported application identity is unsupported and is not
claimed to be detectable. Required verification then includes focused tests and mutations,
syntax/static checks, repository gates, post-export inspection, and recorded source/output metrics
for all 15 real assets, followed by complete before/after human visual evidence.

An upstream release existing is not an upgrade trigger. A security advisory, platform incompatibility,
confirmed import/export defect, or inability to install/run the pin may justify change, but an upgrade
requires a **new proposed decision** before implementation. The candidate must repeat the focused
mutations, report all 15 real-asset metrics, and reproduce the full before/after visual evidence for
human inspection. No partial sample or version-only smoke test can unpin this decision.
