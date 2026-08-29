# Handoff — GLB decimation (Codex session ran out of weekly quota mid-flight)

Reconstructed from the repo, not from the dead session's memory. Every SHA below was verified
with `git` on 2026-08-16. **Read `HANDOFF-2026-08-15.md` too** — it carries the older repo-wide
state and the operating traps.

---

## 1. BOTTOM LINE

**The hard part is done and it worked.** All 15 assets were regenerated and decimated:

| | source | output |
|---|---|---|
| triangles (all 15) | **25,352,000** | **199,998** (−99.2%) |
| per cat | ~1.4–2.0 M | **15,000** |
| per prop | ~1.4–2.0 M | **10,000** |
| bytes | 990 MB (`incoming/`) | **24 MB** (`incoming/decimated/`) |

15 derivatives + 15 sidecars exist on disk at
`unity/Assets/Art/Generated/incoming/decimated/`. Outputs are 0.6–2.9 MB each.

**What is NOT done:** three hardening branches are mid-review with concrete, already-diagnosed
blockers. Nothing is pushed, and there is no PR.

---

## 2. GROUND TRUTH (verified 2026-08-16)

- `origin/main` = **`3115ebd`** — CM-BOOT-HOME (#91) merged, so the shipped launch screen,
  the menu, the rotation lock, the asset pipeline and the generation fixes (#93) are all in.
- **⚠️ ALL DECIMATION WORK IS LOCAL. Nothing is pushed. `git branch -r 'origin/task/GLB*'`
  returns nothing, and there is no open decimation PR.** It exists only on this machine, in
  local branches and ~31 worktrees under `/private/tmp/catmetro-glb-*`. Treat the machine as
  the single point of failure until it is pushed.
- Only open PR is **#65** (ART-DIORAMA, `DIRTY`) — old, unrelated, pre-existing.

### The integration branch (the good stuff)
**`task/GLB-DECIMATION` @ `005215d`** — 62 commits ahead of main, **19,453 insertions / 17
files**, working tree clean:

```
docs/adr/0012-blender-headless-glb-decimation.md      153
docs/design/assets/DECIMATION.md                      790
docs/design/assets/GLB-DECIMATION-EVIDENCE.md         396
docs/design/assets/GLB-DECIMATION-METRICS.json      1,941
docs/design/assets/PIPELINE.md                         14
docs/superpowers/plans/2026-08-15-glb-decimation.md   895
scripts/blender_decimate.py                           315
scripts/decimate-assets.py                          1,200
scripts/glb-silhouette.py                             186
scripts/glb_metrics.py                              1,431
state/PROJECT_STATE.md                                  1
state/handoffs/GLB-DECIMATION-frozen-contract.md      270
tests/assets/fake_blender.py                          295
tests/assets/glb-decimation-pipeline.test.sh        8,396
tests/assets/glb-metrics.test.sh                    1,730
tests/assets/glb-silhouette.test.sh                 1,155
tests/assets/glb_fixture.py                           285
```
**Metrics hardening is already integrated here** (verified: the coordinator's `glb_metrics.py`
carries the default-UV-set requirement). `task/GLB-METRICS-HARDENING-GREEN` @ `caaf531` is an
older lineage and is behind the coordinator on tests — do not re-merge it blindly.

---

## 3. THE THREE PENDING BRANCHES — exact outstanding findings

All three descend from `005215d`. **None is approved for integration.**

### A. `task/GLB-PIPELINE-HARDENING-GREEN` @ `7550eb9` (+1 commit) — **REJECTED**
Single-file production change to `scripts/decimate-assets.py` (+682/−159). Independent review
found real blockers; **do not integrate this commit as-is**:
1. **Validation-to-promotion race** — can publish a corrupted GLB or a forged/secret-bearing
   sidecar and still exit 0. (Most serious; reproduced by the reviewer.)
2. **Post-promotion source-custody failure** (reproduced).
3. **Diagnostic leakage.**
Second reviewer (round 1b) added four more:
4. Persistent force-cleanup can **return failure after publishing the new pair** and leave one
   backup behind. The frozen contract says: a failed normal run may leave neither final; a
   failed forced replacement must restore the exact old pair; a successful force must remove
   both backups. So "success with residue" cannot be waved through.
5. **Maximum-length names don't reserve room for transaction suffixes.**
6. **Fake-test environment variables are over-forwarded** to children.
7. Several **post-preflight hash/version reads use broader or unbounded limits.**

### B. `task/GLB-PIPELINE-REVIEW-RED` @ `a4a3684` (+4 commits) — test-only, MID-CORRECTION
Freezes the seven blockers above as failing tests. 31 cases (Slice L = 16, Slice M = 15) that
fail against `7550eb9` while A–K stay green. **The oracle reviewer then rejected four of the
test oracles themselves** — these were being corrected when the quota ran out:
1. Zero-length peek/mmap can still read data.
2. Post-hoc child-output length checks don't bound memory.
3. Several cases freeze private temp/backup names (should not).
4. The UV mapper can be hardcoded to one mesh/primitive/set.
**Decided boundaries (already pinned, do not re-litigate):** child streams use the existing
**1 MiB** metadata envelope; public diagnostics stay capped at **512 bytes**; later hashes/copies
keep each file class's existing **1 MiB / 2 MiB / 64 MiB / 128 MiB** boundary; transaction-safe
filename limit is **208 bytes**.

### C. `task/GLB-SILHOUETTE-HARDENING-GREEN-R1` @ `060504f` (+4) — GREEN, one finding open
History is correctly ordered (reviewed tests → rejected impl → fix). The corrected renderer
derives pixels from validated indexed surface geometry, uses atomic output promotion, rejects
linked/special outputs, counts scene-instanced work before inspection, checks degeneracy in
world space, and bounds parser/cleanup failures. **One open reviewer finding:** repeated
primitives can share a large POSITION accessor — 15 M position decodes while the index-reference
counter sees only 750, so the cap doesn't bound the expensive path.
**Fix already decided, apply it:** keep the **8,000,000** ceiling but count **both selected index
references AND selected POSITION values**. Worst real asset ≈ **6.98 M** combined, so no real cat
is excluded (real max index-refs alone was 5,956,374).

---

## 4. NOT STARTED

- Regenerate / re-inspect derivatives and update the exact evidence **if output or metrics
  change** after the hardening lands.
- Full exact-head gates, security + fresh-context reviews, **open the human-gated PR**, await CI.
- **Source-art / plinth curation** — some models sit on a display base disc and some don't
  (siamese yes, tabby no). Strip all or keep all; mixed reads as a bug. Taste call for the human.
- **Wire approved cats into Board/Home** (they are still grey rectangles in game).
- **Generated-asset licence ADR** — still owed; nothing ships in the Play binary without it.
  All sidecars carry `plan_tier: paid`.

---

## 5. TRAPS SPECIFIC TO THIS WORK

- **An automated content filter repeatedly killed agents** mid-report when they described the
  redaction / secret-leakage case. Filesystem work survived; the fix was to hand the branch to a
  fresh minimal-context agent and use neutral wording. Expect this and plan for it.
- **~31 GLB worktrees** under `/private/tmp/catmetro-glb-*`. Two carry uncommitted one-off
  Editor scripts that exist nowhere else: `DevfixUrpSetup.cs` (wt-devfix) and `SpikeUrpSetup.cs`
  (wt-spike-urp) — commit, record, or discard; human call.
- Repo-wide gates rewrite `dotnet/CatMetro.DailyTools/packages.lock.json` via `dotnet restore`.
  It is **not** part of this task — restore it before committing, never `git commit -a`.
- CI is ~2 h (≈3 h concurrent). `mktemp` returns empty under the repo sandbox. Unity
  `-runTests` must not get `-quit`. Never touch the physical Pixel `2G0YC5ZF7Z056Q`.
