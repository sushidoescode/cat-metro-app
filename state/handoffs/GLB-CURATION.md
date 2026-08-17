# Handoff: GLB-CURATION — source-art curation

- **Date/session:** 2026-08-17
- **From → To:** Lane C → post-#94 continuation
- **Branch:** `task/GLB-CURATION`
- **Frozen base:** `origin/task/GLB-DECIMATION` at
  `16e20e3fe9793ecaeb0f7865e66a55521eebf7db`
- **PR status:** HOLD. Do not open the curation PR until a human/orchestrator
  confirms PR #94 has merged.

## Contract

The frozen contract is
[`state/handoffs/GLB-CURATION-frozen-contract.md`](GLB-CURATION-frozen-contract.md).
It is the branch's first commit (`77d66b4`) and governs the exact two source
changes: remove the loaf display disc, remove only the wave min-Y debris
component, and leave all other source-art choices and 13 derivatives untouched.

The tracked two-entry rerun inventory is
`docs/design/assets/GLB-CURATION-MANIFEST.json`. Paid source GLBs, derivative
GLBs, sidecars, and backups remain ignored local artifacts; this lane does not
promote them into tracked Unity content.

## State

Done:

- `77d66b4` freezes the contract before tests or implementation.
- `c7390ba` records the observed honest RED for the absent curation tool.
- `b6434c4` adds the minimal Blender curation driver and transactional
  orchestrator.
- `3c51870` records regenerated metrics and rendered/looked-at evidence.
- `f2e719d`, `2d1fcba`, and `0c5fa63` add RED-first review coverage and harden
  locking, custody validation, durable recovery, and exact-two publication.
- Both ignored source pairs are curated and both ignored derivatives were
  regenerated through the reviewed decimator. No tracked GLB was added.
- The 13 unruled derivative GLBs, sidecars, and rendered before/after pairs are
  byte-pinned and unchanged.

Not done by design:

- no PR has been opened;
- no push or mutation was made to `task/GLB-DECIMATION`;
- no Unity wiring, licensing ADR, generation, network/API spend, device work,
  Google Play action, merge, tag, release, or deployment was performed.

## Local custody

Artifact root:
`/Users/sushantsrikrish/cat-metro-app/unity/Assets/Art/Generated/incoming`

Recoverable originals:
`curation-backups/GLB-CURATION-2026-08-17-16e20e3/`

| Asset / member | SHA-256 | Result |
|---|---|---|
| loaf source before | `e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3` | backup retained |
| loaf source sidecar before | `ce8ea067634f88ee9fc967ea5a0dbc58df890477d3e1dc1905cc3f77a92dcec4` | backup retained |
| loaf source after | `257e59ebac613e3260bfd1161b228ec2be4aa7024969b4b1a3fec2366ffe0097` | 773,061 triangles |
| loaf source sidecar after | `93fd18c00ec6a1b369bed7849a0bfdb4c00cba5dfe6b16358995998a86bb1f66` | coherent |
| loaf derivative after | `9a7b2ef923f923a78466f18d8bf0cfb82140aebbd30ba3e7cddd3f814fd2953c` | 14,999 triangles |
| loaf derivative sidecar after | `2265679b91ff5feb5ab5ef7a277af6c3abfe1fda43e4dff2eccb5cceacc684e4` | coherent |
| wave source before | `8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c` | backup retained |
| wave source sidecar before | `e65414b151fa1dd868e9086c0e274ac61743aef8f8f26bc7bcaa6f49f99c8936` | backup retained |
| wave source after | `f91ccb7ff9b527ecef168d4285488ff647023fb70875f5403c31db8e2349d99d` | 1,422,808 triangles |
| wave source sidecar after | `bb787a4073833edfd54af3e401cfa00e73b5279592ba2d146b015d3f1ffe90e4` | coherent |
| wave derivative after | `2eee06883d024631263485b48da067dd8042f66ef81fc669016731fa5fdaa1ef` | 14,998 triangles |
| wave derivative sidecar after | `b961427de158aba8377e3114cc301d4d144ee38e378df984d8140a31cb3d633e` | coherent |

No curation transaction journal or staging residue remained after the final
local recovery checks.

## Evidence

Rendered evidence and the looked-at record are under
`evals/results/assets/glb-curation-2026-08-17/`. Lane C viewed the source
comparison, changed-derivative comparison, and complete 15-asset comparison at
original detail. The loaf disc and the wave min-Y debris are absent; the two
cats remain intact; the wave rank-3 non-min-Y component remains intentionally.
All 39 PNGs are inventoried in `SHA256SUMS`.

The strongest local verification reconstructs both original source pairs from
the backup in scratch space, runs the real curation tool with Blender 5.1.2,
and compares source, source sidecar, backup, and current curated bytes. It
passed for both assets. An isolated run of the committed exact-two manifest
produced exactly two GLBs and two sidecars; both GLBs were byte-identical to the
retained derivatives.

Review round 1 returned NOT MERGEABLE. Its concrete findings were addressed
RED-first: source-root locking and final anchors, pinned source sidecars,
texture-payload/material-binding equality, degenerate/duplicate geometry
checks, a durable prepared/committed recovery journal, `BaseException` rollback
coverage, exact-two committed inventory, production/failure/concurrency tests,
literal custody/evidence pins, corrected wave denominator measurements, and
the present state/handoff record.

## Decisions and risks

- Pair publication is serialized by an input-directory lock and is recoverable
  across interruptions through a durable journal. A subsequent invocation
  normalizes either a prepared or committed journal before new work.
- The wave rank-3 component is not min-Y debris under the frozen predicate and
  remains. Changing that result is a new human taste ruling, not continuation
  work.
- Evidence is H-1-class, not independent human attestation. The generated-art
  licensing and tracked-asset promotion boundaries remain deferred.
- Review is capped at two rounds. Any concrete blocker surviving round 2 must
  be named as follow-up debt; do not silently broaden this lane.

## Next step

After a human/orchestrator confirms PR #94 merged:

```sh
git fetch origin
git rebase --onto origin/main 16e20e3 task/GLB-CURATION
```

Then rerun focused local-artifact verification plus `bash scripts/check.sh`,
`bash scripts/test.sh`, `bash scripts/build.sh`, and `git diff --check` at the
rebased exact head. Only after those gates pass may the curation PR be opened
against `main`. Until then, the remote branch may exist, but the PR must not.
