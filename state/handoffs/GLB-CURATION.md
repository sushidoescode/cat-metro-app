# Handoff: GLB-CURATION — source-art curation

- **Date/session:** 2026-08-17–18
- **From → To:** Lane C → post-#94 continuation
- **Branch:** `task/GLB-CURATION`
- **Frozen base:** `origin/task/GLB-DECIMATION` at
  `16e20e3fe9793ecaeb0f7865e66a55521eebf7db`
- **PR status:** HOLD. Do not open the curation PR until a human/orchestrator
  confirms PR #94 has merged.

## Contract

The original frozen contract is
[`state/handoffs/GLB-CURATION-frozen-contract.md`](GLB-CURATION-frozen-contract.md).
It is the branch's first commit (`77d66b4`) and governs the exact two source
changes: remove the loaf display disc, remove only the wave min-Y debris
component, and leave all other source-art choices and 13 derivatives untouched.

The human correction is frozen verbatim in
[`state/handoffs/GLB-CURATION-addendum-frozen-contract.md`](GLB-CURATION-addendum-frozen-contract.md)
at correction commit `fb3df0b`. It supersedes only the original wave predicate:
for the cat wave source, keep only the unique largest connected component and
remove every other component. The completed loaf curation is expressly outside
the correction and must remain byte-identical.

The original two-entry inventory is
`docs/design/assets/GLB-CURATION-MANIFEST.json`; the addendum's exact one-asset
decimation rerun is frozen in
`docs/design/assets/GLB-CURATION-WAVE-MANIFEST.json`. Paid source GLBs,
derivative GLBs, sidecars, and backups remain ignored local artifacts; this lane
does not promote them into tracked Unity content.

## State

Done:

- `77d66b4` freezes the contract before tests or implementation.
- `c7390ba` records the observed honest RED for the absent curation tool.
- `b6434c4` adds the minimal Blender curation driver and transactional
  orchestrator.
- `3c51870` records regenerated metrics and rendered/looked-at evidence.
- `f2e719d`, `2d1fcba`, and `0c5fa63` add RED-first review coverage and harden
  locking, custody validation, durable recovery, and exact-two publication.
- `dad7069` / `101f809` reproduce and close pre-journal interruption residue;
  `6ebbfe2` / `1876eb5` reproduce corrupt backup copies and re-hash both
  completed backup members before journal creation or promotion.
- `049f4d7` / `ff9bd55` normalize both allowed asset journals before any new
  work, pin the exact evidence paths, and correct the mixed local-run dates;
  `d8b8c03` pins the complete evidence checksum manifest by literal SHA-256.
- `fb3df0b` freezes the addendum before correction tests or implementation;
  `b30cef0` records the honest RED against the retained second wave component,
  and `a877fba` records the RED for the absent cat-scoped largest-component rule.
- `3b50591` keeps only the unique largest component for the wave cat while
  accepting only the pinned provider-original or first-pass input pair;
  `7d9b5cf` freezes the exact one-wave decimation manifest.
- At reviewed implementation/evidence head `7aaf680`, both corrected wave files
  contain exactly 1 connected component at the pinned `1e-5` weld distance, the
  derivative remains within the 15k-cat target, and the loaf pair is
  byte-identical to the pre-addendum branch.
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

Provider-delivered originals (untouched):
`curation-backups/GLB-CURATION-2026-08-17-16e20e3/`

Recoverable pre-addendum wave pair:
`curation-backups/GLB-CURATION-WAVE-CORRECTION-2026-08-18-841d4a3/`

| Asset / member | SHA-256 | Result |
|---|---|---|
| loaf source before | `e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3` | backup retained |
| loaf source sidecar before | `ce8ea067634f88ee9fc967ea5a0dbc58df890477d3e1dc1905cc3f77a92dcec4` | backup retained |
| loaf source after | `257e59ebac613e3260bfd1161b228ec2be4aa7024969b4b1a3fec2366ffe0097` | 773,061 triangles |
| loaf source sidecar after | `93fd18c00ec6a1b369bed7849a0bfdb4c00cba5dfe6b16358995998a86bb1f66` | coherent |
| loaf derivative after | `9a7b2ef923f923a78466f18d8bf0cfb82140aebbd30ba3e7cddd3f814fd2953c` | 14,999 triangles |
| loaf derivative sidecar after | `2265679b91ff5feb5ab5ef7a277af6c3abfe1fda43e4dff2eccb5cceacc684e4` | coherent |
| wave source provider-original | `8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c` | provider backup retained |
| wave source sidecar provider-original | `e65414b151fa1dd868e9086c0e274ac61743aef8f8f26bc7bcaa6f49f99c8936` | provider backup retained |
| wave source first pass | `f91ccb7ff9b527ecef168d4285488ff647023fb70875f5403c31db8e2349d99d` | correction backup retained; 1,422,808 triangles |
| wave source sidecar first pass | `bb787a4073833edfd54af3e401cfa00e73b5279592ba2d146b015d3f1ffe90e4` | correction backup retained |
| wave derivative first pass | `2eee06883d024631263485b48da067dd8042f66ef81fc669016731fa5fdaa1ef` | correction backup retained; 14,998 triangles |
| wave derivative sidecar first pass | `b961427de158aba8377e3114cc301d4d144ee38e378df984d8140a31cb3d633e` | correction backup retained |
| wave source corrected | `bf4626c2a41214444a483bde1920c7fd95a06069feca202df860861edb540d64` | 1,383,894 triangles / exactly 1 connected component |
| wave source sidecar corrected | `0bedeeb207fcb02277c7b0b1d0bcf8ec8118d4b0cf2e20abbaa3d85b1a64260f` | coherent |
| wave derivative corrected | `a3c4a363b06064ecc5dc03509c36ddd5ab91200a41314a3c674cd91ef4386696` | 15,000 triangles / exactly 1 connected component |
| wave derivative sidecar corrected | `9c7bd939fc493caa44d0250531e2137c8c848d5b9bbfc62de320e2dbab16317e` | coherent |

No curation transaction journal or staging residue remained after the final
local recovery checks.

## Evidence

Rendered evidence and the looked-at record are under
`evals/results/assets/glb-curation-2026-08-17/`. Lane C viewed the source
comparison, changed-derivative comparison, and complete 15-asset comparison at
original detail, plus the dedicated correction source-before/source-after,
derivative-before/derivative-after, and four-panel comparison. The detached
two-lobed blob visible left of the pre-addendum cat is gone in both corrected
views; ears, face, raised paw, torso, feet, and curled tail remain intact. The
loaf and the other 13 assets remain unchanged. All 44 PNGs are inventoried in
the literal-hash-pinned `SHA256SUMS`.

The strongest local verification reconstructs the provider-original pair and
the pre-addendum first-pass pair in separate scratch roots, runs the real
curation tool with Blender 5.1.2, and obtains the current corrected source and
sidecar byte-for-byte from either accepted input. An isolated run of the
committed one-wave manifest produced exactly one GLB and one sidecar; its GLB
was byte-identical to the retained corrected derivative and the sidecar differed
only by its fresh tool timestamp.

Review round 1 returned NOT MERGEABLE. Its concrete findings were addressed
RED-first: source-root locking and final anchors, pinned source sidecars,
texture-payload/material-binding equality, degenerate/duplicate geometry
checks, a durable prepared/committed recovery journal, `BaseException` rollback
coverage, exact-two committed inventory, production/failure/concurrency tests,
literal custody/evidence pins, corrected wave denominator measurements, and
the present state/handoff record.

Review round 2 found and closed four final blocker classes: failed setup before
journal durability now removes unusable backup/journal/stage residue; completed
backup members are hash-verified before publication; both ruled asset journals
are normalized before either asset can start; and evidence custody now combines
39 exact paths, actual-byte checks, and a literal hash of `SHA256SUMS`. The date
record now distinguishes the 13 historical queue members from the two August 17
local refreshes. At reviewed implementation/evidence head `d8b8c03`, the
independent reviewer reran the strongest real-Blender reconstruction and found
both source pairs byte-identical, then passed all focused GLB suites, the local
60-member custody check, and the branch diff check. The only later branch change
is this state/handoff closure; exact-head full gates remain the pre-push exit
step.

The addendum review independently verified the honest RED history, exact
one-component source/derivative geometry, both source reconstruction paths,
the one-wave decimation rerun, loaf and 13-asset byte identity, both backup
inventories, and the looked-at 44-image record at `7aaf680`. Its sole finding
was that `state/PROJECT_STATE.md` and this handoff still presented the
superseded first-pass wave as final. The docs oracle added immediately after
`7aaf680` proves that stale state RED and permanently pins the corrected hashes,
counts, component terminal, backup custody, evidence inventory, and reviewed
implementation/evidence head. This state/handoff/lesson update closes that
finding; the held-open independent recheck at the resulting doc-only head is
the pre-push review gate.

## Decisions and risks

- Pair publication is serialized by an input-directory lock and is recoverable
  across interruptions through a durable journal. A subsequent invocation
  normalizes either a prepared or committed journal before new work.
- The addendum supersedes the first-pass wave predicate: for this cat asset,
  every connected component except the unique largest is debris. The selector
  is cat-scoped and does not silently extend that judgment to props.
- Evidence is H-1-class, not independent human attestation. The generated-art
  licensing and tracked-asset promotion boundaries remain deferred.
- Follow-up debt — **source-root pathname/inode re-anchor:** the advisory lock
  pins the opened source-directory inode, while the curation body still follows
  the directory pathname. A non-cooperating actor could rename and recreate that
  path and acquire a second inode lock. Literal final/source anchors constrain
  this outside normal same-tool concurrency; hardening it is a separate contract.
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
