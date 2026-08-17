# GLB-CURATION — frozen contract

- **Status:** FROZEN before tests or implementation
- **Frozen against:** `origin/task/GLB-DECIMATION` at
  `16e20e3fe9793ecaeb0f7865e66a55521eebf7db`
- **Date:** 2026-08-17
- **Owner:** Lane C — source-art curation
- **Branch:** `task/GLB-CURATION`
- **Stacking rule:** build and push this branch now, but do not open its PR until
  PR #94 has merged. Then rebase with
  `git rebase --onto origin/main 16e20e3 task/GLB-CURATION`, rerun every gate at
  the rebased head, and only then open the PR against `main`.
- **Contract rule:** this document is the branch's first commit. Acceptance
  criteria or geometric boundaries may change only through a recorded human
  amendment. Tests and implementation may not silently weaken them.

## Human ruling and evidentiary class

The governing 2026-08-17 ruling was relayed by the orchestrator session rather
than recorded in a human-authored repo commit or GitHub comment. It is therefore
H-1-class evidence: authoritative for this lane's execution, but not independent
human attestation.

The taste-final ruling is **uniform NO-plinth**:

1. remove the display-base disc from `cat-blue-siamese-loaf`;
2. remove the detached generation-debris component adjacent to
   `cat-yellow-longhair-wave`'s feet; and
3. leave the other 13 assets byte-identical.

This contract executes that ruling. It does not reopen strip-versus-keep or
change any other source-art feature.

## Objective

Add a fail-closed, offline Blender 5.1.2 source-curation step for exactly the two
ruled Tripo GLBs. It must stage curated source/sidecar pairs, validate the exact
geometry selected, preserve recoverable originals, transactionally promote the
pair, and then run the existing decimation pipeline for exactly those two
assets. Update the two invalidated evidence/metrics records, pin the other 13
derivatives, and commit rendered evidence that this session actually views.

The paid source GLBs, source sidecars, regenerated derivatives, derivative
sidecars, and recoverable backups stay in the gitignored local asset tree. They
are not licensed or promoted to tracked Unity assets by this contract.

## Measured input anchor

All measurements below were made read-only from the local ignored inputs while
code and documentation ground truth came from the exact fetched origin base.

| Asset | Source SHA-256 | Existing derivative SHA-256 |
|---|---|---|
| `cat-blue-siamese-loaf` | `e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3` | `cc1ff113257d48994a94cfdff52554236034e3e6455d402de195461b8c8fc236` |
| `cat-yellow-longhair-wave` | `8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c` | `4e20de09cee1dcfa383bb708608f03b5f8c1aa78ca4a510a3064f435f5f87a27` |

Blender imports use the already-reviewed seam-safe settings
`merge_vertices=True` and `import_shading="SMOOTH"`. Blender world Z is the
imported form of glTF's Y-up axis; all criteria below are stated in glTF Y even
when the driver evaluates Blender world Z.

### Loaf plinth discriminator

The source glTF Y bounds are
`[-0.4485988914966583, 0.4485988914966583]`, giving height
`0.8971977829933167`. The geometric cut is exact:

```text
cutoff = min_y + 0.08 * (max_y - min_y)
delete vertices where y < cutoff
```

The resulting cutoff is `-0.37682306885719297`. The selected min-Y slab spans
100% of the original X width and Z depth. After it is removed, the retained cat
spans about 74% of the original width and 54% of the original depth. The sharp
footprint contraction between the 7% and 8% height bands is the objective
display-disc boundary. A read-only trial removed `654,714` of the source's
`1,427,775` triangles and retained `773,061`; material, embedded images, UV
binding, pose, paws, and tail remained visibly intact. The production driver
must independently re-measure and enforce these facts rather than trusting the
trial output.

The driver must fail closed unless the candidate slab spans at least 95% of
both original horizontal axes and the retained footprint is below 80% of both
axes. Those guards make the 8% cut specific to an extending plinth, not a
generic instruction to amputate any model's lowest vertices.

### Wave fragment discriminator

After seam-safe import the source has three connected components:

| Rank by triangles | Triangles | Thinnest bbox span / full max span | Location |
|---:|---:|---:|---|
| 1 | 1,383,894 | `> 0.79` | main body |
| 2 | 71,282 | `0.06539922752069884` | touches global min-Y; foot debris |
| 3 | 38,914 | `0.09807603740185485` | does not touch the min-Y band |

The exact selection rule is:

```text
component_thinnest_bbox_span / full_asset_max_span < 0.07
and component_min_y <= full_min_y + 0.01 * full_y_height
```

It selects exactly the 71,282-triangle foot-adjacent component. The other
38,914-triangle component is outside the ruled location and size boundary and
must remain. The main body remains one connected component after deletion.

## Untouched derivative hash pins

The following 13 local derivatives must remain byte-identical. The RED-first
test owns these literal pins and, when the explicit local artifact root is
available, hashes the actual files rather than relying only on tracked metrics.

| Asset | Frozen derivative SHA-256 |
|---|---|
| `cat-red-tabby` | `9d6f3e1b0d82f23500779c570943dc2081c6caad7295da7d3fe19c1c50742b59` |
| `cat-blue-siamese` | `44ceea493949fa7ea92bf40c7bc05e64c4b78e3ca0bb4c08b41fa7d788ee17b7` |
| `cat-yellow-longhair` | `36f03503fcbcb918870463222f50d6b17b3c880281ce61f3a15c2cec6963ed3e` |
| `cat-green-shorthair` | `96910d69ad0bfe424c410e0b9df6e137222d858a28322a5276add6228e9186e5` |
| `cat-wild-alley` | `3fa010b59c3b5dccbe0eb54453e8d595736cbafa391a9f08effd9d052738479c` |
| `cat-red-tabby-sitting` | `3ea8e01d78cb058223c74f225e89512efc44f74f638c99133d7720675e8655b6` |
| `cat-green-shorthair-sit` | `a5791a945bac21cfe55e7e4cdbcd5cd3233c11997cd0f449972a12768cca93f8` |
| `cat-conductor` | `3b0bdbe1a0af9377bfde62ebf2b633e694881dc81438f2814e717c4c71ab9e7d` |
| `prop-depot-shed` | `68994c2316e7c0b23252569bfc06cbc1155c29dd41798c8effdbbaba638844b1` |
| `prop-toy-engine` | `f622b390cdf48fccfb382895bef2988df191b523b614e01f03dbd162e052eeaf` |
| `prop-station-kiosk` | `25053fb73009bf004aeeebab4a861bb664c91935b59c059f21d2fc8c9b6f52cf` |
| `prop-trees` | `e34f39de9a0db8f977370d7f0808f44a28b9641a458ada4957f552c62271c0dd` |
| `prop-desk-clutter` | `d0403b93dc3db30ec3f7e0b825ba7b48f4af7b79094c6b262c7bfa2fb268ec4d` |

## Source custody and transaction boundary

- Inputs are exactly the two pinned source GLB/source-sidecar pairs under an
  explicit `--input-dir`; the tool refuses any other ID or source hash.
- The Blender driver reads one pinned source and writes a new staged GLB. It
  never receives `.env`, network credentials, manifest-wide authority, or a
  final source path as output.
- The orchestrator writes a staged source sidecar that preserves original
  service, task ID, generation timestamp, plan tier, and prompt; updates the
  source SHA-256; and appends a deterministic curation statement to the
  existing `note`. It does not introduce a new provenance schema.
- Before promotion, the staged GLB must pass normal GLB structure,
  material/image/UV preservation, exact operation-specific geometry guards,
  and source-sidecar hash agreement.
- Promotion is pair-atomic with rollback: copy the original source and sidecar
  to a newly created explicit backup directory; atomically replace the final
  pair only after both staged members validate; restore both originals if any
  promotion step fails. Backups are never deleted or overwritten by the tool.
- Default behavior refuses an already-curated source. There is no generic
  `--force` over an unknown source hash.
- After source promotion, invoke `scripts/decimate-assets.py` separately for
  each ruled asset (an exact two-entry manifest or equally narrow supported
  selection), using its existing transactional `--force` path. No other
  derivative or sidecar may be rewritten.

## Rendered evidence boundary

Commit evidence under:

```text
evals/results/assets/glb-curation-2026-08-17/
```

The evidence contains:

1. pre/post source silhouettes for the loaf and wave assets;
2. pre/post derivative silhouettes for all 15 manifest assets at identical
   renderer settings, with the 13 untouched pairs byte-identical;
3. contact sheets and a machine-readable SHA-256 inventory; and
4. a short looked-at record naming the exact visible changes and any remaining
   limitation.

All frames use tracked `scripts/glb-silhouette.py`, identical size/yaw, and the
same renderer hash. This session must view the two source pairs, the two changed
derivative pairs, and the complete comparison sheet at original detail. A
green render command without inspection is not evidence.

## Acceptance criteria

1. **AC1 — frozen scope:** this contract is the branch's first commit, records
   the H-1 caveat, exact stacked base, PR hold, geometric equality boundaries,
   source hashes, and untouched hashes.
2. **AC2 — honest RED first:** a new non-immutable asset test is committed and
   observed failing for the missing curation implementation before production
   code. It discriminates both ruled defects and the 13 hash pins. No existing
   test is weakened or deleted.
3. **AC3 — loaf source curation:** only the pinned loaf source can take the
   exact 8% min-Y cut; the 95% selected-footprint and 80% retained-footprint
   guards pass; post-curation structure/material/images/UVs remain valid; and
   rerunning the defect check changes RED to GREEN.
4. **AC4 — wave source curation:** only the pinned wave source can remove the
   component satisfying both strict `< 0.07` thin-span and inclusive `<= 0.01`
   min-Y-location predicates; exactly one 71,282-triangle component is removed;
   the main body remains connected; and the other component remains.
5. **AC5 — custody and failure safety:** both original source pairs are
   recoverable in a unique backup directory; staged candidates never overwrite
   inputs; bad hashes, wrong IDs, unexpected geometry, pre-existing backups,
   partial sidecars, and promotion failures are mutation-tested fail-closed
   with pair rollback and no temp/lock/backup-name residue.
6. **AC6 — exact two-asset decimation:** only the curated loaf and wave sources
   are rerun through the reviewed pipeline. Their derivatives remain within the
   cat band 13,500–15,000 and have coherent schema-1 sidecars. All 13 pinned
   derivatives retain their literal SHA-256 values.
7. **AC7 — evidence records:** only the loaf and wave entries invalidated in
   `GLB-DECIMATION-EVIDENCE.md` and `GLB-DECIMATION-METRICS.json` are updated,
   except mechanically necessary aggregate totals/render hashes. The tracked
   records agree with all local source/sidecar/derivative/sidecar bytes.
8. **AC8 — rendered-and-looked-at:** all required source and derivative frames,
   contact sheets, and hash inventory are committed at the named evidence path;
   the session views them and records the visual disposition. The other 13
   before/after derivative PNG pairs are byte-identical.
9. **AC9 — verification and review:** focused RED/GREEN tests, all existing GLB
   suites, `bash scripts/check.sh`, `bash scripts/test.sh`,
   `bash scripts/build.sh`, and `git diff --check` pass at the exact pushed
   head. Fresh-context review follows sprint risk pricing with an absolute cap
   of two rounds; concrete findings are fixed or recorded as named follow-up
   debt at the cap.

## Assumptions and ruled interpretations

- `cat-yellow-longhair-wave`'s separate rank-3 component is deliberately
  retained because it fails both parts of the ruled foot-fragment predicate.
  This is execution of the location-specific ruling, not a new taste call.
- “Byte-untouched” binds the 13 retained derivative GLBs. Their sources and
  sidecars are also outside this tool's two-entry allowlist and therefore remain
  untouched in practice.
- Source and derivative binaries stay ignored and local; committed hashes,
  metrics, tests, and PNGs are the branch evidence. No generated GLB is added
  forcibly to Git.
- The original generation timestamp remains generation provenance. Curation is
  recorded by extending the existing free-form `note`, avoiding a schema
  change or ADR requirement.
- The exact source hashes above are the only supported pre-curation state.
  Drift is a stop, not a threshold retuning opportunity.

No other load-bearing assumption is open.

## Scope and stop boundaries

Authorized:

- source-curation additions under `scripts/`;
- one new test and supporting non-immutable fixtures under `tests/assets/`;
- the two local curated source/sidecar pairs and their recoverable backups;
- the two local regenerated derivative/sidecar pairs;
- only necessary loaf/wave and aggregate edits in the two GLB evidence docs;
- committed visual evidence under `evals/results/assets/`; and
- task status/handoff updates under `state/`.

Forbidden or deferred:

- any push to `task/GLB-DECIMATION`;
- opening this lane's PR before the human/orchestrator says #94 merged;
- editing the other 13 assets, source prompts, or taste defects;
- tracked Unity asset promotion or Board/Home wiring;
- generated-asset licensing/ADR work;
- Unity scripts, packages, project settings, scenes, prefabs, shaders, runtime
  importers, `.github/**`, `docs/adr/**`, or any immutable path;
- generation, network/API use, `.env` access, spend, emulator/device work,
  Google Play actions, merge, tag, release, or deploy.

## Expected tracked paths

- `state/handoffs/GLB-CURATION-frozen-contract.md`
- source-curation scripts under `scripts/`
- a new test/supporting fixture under `tests/assets/`
- `docs/design/assets/GLB-DECIMATION-EVIDENCE.md`
- `docs/design/assets/GLB-DECIMATION-METRICS.json`
- `evals/results/assets/glb-curation-2026-08-17/**`
- end-of-session `state/PROJECT_STATE.md` and continuation handoff

