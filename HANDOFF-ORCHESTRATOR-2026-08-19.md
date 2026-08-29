# Orchestrator handoff — 2026-08-19

For the incoming orchestrator chat. Supersedes `HANDOFF-ORCHESTRATOR-2026-08-17.md`.
Ground truth from origin and from the machine — never from this file. Verify with
`gh pr list`, `git log origin/main`, and `adb devices -l` before trusting anything below.

---

## 0. ⚠️ READ THIS FIRST — the device map in every older handoff is WRONG

Every prior handoff, plus two TRACKED files on main, say *"the physical Pixel
`2G0YC5ZF7Z056Q`"*. **That is false and it is a safety problem.** Verified with
`adb devices -l` on 2026-08-19:

| serial | what it ACTUALLY is | product/model |
|---|---|---|
| `48121FDAP006X4` | **Pixel 9 Pro** — the Android phone | `caiman` / `Pixel_9_Pro` |
| `2G0YC5ZF7Z056Q` | **Quest 3** — an XR headset, NOT a phone | (not attached 08-19) |
| `emulator-5554` | **Pico OS6 emulator** — XR, NOT an Android phone image | `swan_arm64` |

The standing "never touch `2G0YC5ZF7Z056Q`" rule was therefore protecting a headset while
naming it a Pixel, and "scope all adb to `emulator-5554`" pointed Android work at a Pico XR
emulator. The outgoing session propagated this error into four lane briefs before catching it.

**Rules, corrected:**
- The Android phone is **`48121FDAP006X4`**. Install and capture there, and only with the
  human's explicit word.
- `2G0YC5ZF7Z056Q` (Quest 3) and `emulator-5554` (Pico OS6) are **not this project's devices**.
  Never push an Android phone build at either.
- **Always run `adb devices -l` and read the `model:`/`product:` fields before any adb command.**
  Do not copy a serial out of a handoff — that is exactly how this happened.

Files still carrying the wrong label (untracked lane briefs plus tracked docs):
`HANDOFF-2026-08-15.md`, `HANDOFF-GLB-DECIMATION-2026-08-16.md`,
`HANDOFF-ORCHESTRATOR-2026-08-17.md`, `HANDOFF-LANE-{C,D,E}-*.md`,
`docs/runbooks/emulator-selftest.md` (**tracked**),
`state/handoffs/EMU-RIG-frozen-contract.md` (**tracked, frozen — do not edit; supersede it in
`state/PROJECT_STATE.md` instead**). Correcting the tracked runbook is owed work.

---

## 1. STATE

**main = `ef333eb`.** Three merges landed 2026-08-18/19 after a two-day stall:

| PR | what | commit |
|---|---|---|
| #94 | GLB-DECIMATION — 25.3M→200k tris, 990MB→24MB, pipeline + ADR-0012 | `1b2ea7d` |
| #98 | GLB-CURATION — loaf plinth stripped, wave reduced to its largest component | `0387ccb` |
| #99 | orchestrator records + `.mcp.json` bearer-token ignore | `ef333eb` |

Closed, not merged: **#65** ART-DIORAMA (stale, salvage record on the PR) · **#97** CI-SPEEDUP
(round-2 review blocked it on cache-identity and concurrent-write safety in a memoisation layer
around the test harness — a false-green risk that was judged not worth the wall-clock).

### Open PRs — both blocked on humans, not machines

**#96 `task/GEN-ASSET-LICENSE-ADR` @ `76295cc`** — ADR-0013, the generated-asset licence gate.
CI green. Draft. **Nothing ships to Play until this is signed.** Reviewed twice (the cap is
spent). The orchestrator took custody after Lane B went inactive and re-pinned all eight
manifest values for the two curated assets, repaired a corrupted release gate 2, and added a
curated-source clause. Round 2 then caught the orchestrator's own errors — both edits had been
understated — now corrected with measured numbers.
**Human must decide before signing:** may Cat Metro geometry-edit a provider-delivered source at
all (priced at **45.9%** of the loaf's provider geometry removed through connected mesh, and
**7.375%** of the wave across two components and two passes)? Was the uniform-no-plinth ruling
actually made in the terms executed — the only record is an agent relay? Plus the A5 public-repo
ratification and the Meshy/Tripo checkboxes already listed on the PR. If the answer is yes, §1's
allowed-modification list needs amending too: it does not currently authorise deleting geometry.
**Signing and merging an ADR are both human-only.**

**#95 `task/CM-CATS-WIRE` @ `89e26f7`** — the cat wiring. Code-green, CI pass, reviewed
(18 agents; 13 findings, 12 refuted). **One evidence-file row blocks it**: `ARTIFACT.md`
describes frame 03 as L011 campaign play when it is a staged forced-colour bench — L011 has only
red and blue waves, and green/wild appear in no committed level. Not a gameplay defect; the
document is wrong. Needs the disclosure plus, ideally, a rendered nine-red-tabby frame (the
crowding case that actually ships has never been looked at). Then it is agent-mergeable.

---

## 2. THE APK — built, installed, awaiting the human's eyes

`scratchpad/CatMetro-cats-dev.apk` · 49 MB · sha256
`c454a4f4c39e703dc2067d72c7ae08786ac7bc7fe1de8537b0fe1e7cc030451d` · **installed on
`48121FDAP006X4` 2026-08-19 22:34**. `com.catmetro.game` 0.1.0, arm64-v8a, debug-signed.

**Why a rig was needed at all** (this was not obvious and cost real time to find):
- `scripts/build.sh` **builds nothing** — it is a stub. The only Android path is the *untracked*
  `unity/Assets/Editor/CatMetroCliBuild.cs` shim, which exists on no ref.
- Unity has **no glTF importer**, so `.glb` cannot enter the project. Models must go
  GLB → Blender → FBX.
- `CatModelCatalog` holds **direct prefab references** — no `Resources.Load`. A scene with no
  catalog has no cats, by design. **Merging #95 alone still ships placeholders.**

Rig lives in `scratchpad/wt-apk` (worktree at `89e26f7`), built by
`scratchpad/build-cat-apk.sh`. It is deliberately **local-only and uncommitted** — the models are
gitignored, so catalog references would dangle anywhere else. Converted FBX + extracted textures
are in `scratchpad/final8/`. Rebuild is one command and much faster now the Library is warm.

**Known likely defect in the current APK:** `FacingYaw` was set to `-90` for all eight cats, but
the catalog's own note says the conductor and the standing board cats face *opposite* the two
sitting Home poses. Expect the two sitting Home cats to face the player and the other six to show
their backs. `CatWireBuild.cs` now splits the two families and reads `CM_YAW_STANDING`
(default 0) and `CM_YAW_SITTING` (default −90) from the environment, so a corrected build needs
no code edit. Surfaces add their own base turn: board `−22°`, Home `−20°`.

**Unverified:** colour. URP must map the FBX diffuse texture to `_BaseMap`. Textures are
confirmed embedded in all eight FBX; nobody has seen them in-engine. If cats render grey, the
base-colour JPEGs in `scratchpad/final8/` are the fallback for hand-authored materials.

**Not in the build, by contract:** the five generated props, the interactive Home pins, and 7 of
the 15 models (only the frozen 8-row map is wired).

---

## 3. STRUCTURAL BLIND SPOTS — green means less than it looks

Both found this week, both still open, both need human-gated `.github` work:

1. **CI checks out shallow.** `actions/checkout@v5` with no `fetch-depth` ⇒ depth-1, so every
   history-dependent test passes **vacuously**. This is how main went red on full clones while
   CI reported green: #94's squash orphaned a declared production base, and only a real full
   clone could see it. Fix is `fetch-depth: 0`.
2. **CI never compiles C#.** `scripts/test.sh` discovers only `tests/**/*.test.sh`, and the
   `unity-editmode` job the harness names does not exist. Unity code lands verified by no machine
   but its author's — #95's reviewers could not run its suites at all.

**Standing lesson from #94:** an ancestry-pinned document must name a commit that survives a
squash — a **mainline** commit, never a branch commit.

**Cross-lane sequencing rule, learned by causing it:** a hash-pinned manifest is pinned AFTER the
curation that mutates its assets. Whoever pins bytes must touch them last. Two lanes were briefed
without either being told the other existed, and 8 of 60 pins were false within 24 minutes.

---

## 4. CUSTODY RISK — one directory holds the whole licensing chain

`unity/Assets/Art/Generated/incoming/curation-backups/` (gitignored, machine-local, no backup)
holds the only surviving **provider-delivered** bytes for the two curated assets. Verified intact
2026-08-19: loaf source `e3015351…`, wave source `8d7190fd…`. ADR-0013's own reproduction anchor
exists in **no ref**. If that directory is lost the curation is unreproducible and the ADR's
source pins unverifiable. Meshy deletes non-Enterprise API output 3 days after generation, so
provider-side re-acquisition is already gone. **Human call: back up the paid-tier source set.**

---

## 5. OPERATING NOTES

- **Review cap: two rounds per artifact**, then findings become named follow-up debt and the
  human decides. This session held it; the previous one's unbounded recursion is why nothing
  merged for two days.
- **Anything visual must be rendered and LOOKED AT.** This caught six defects this week that
  green tests missed — the unstripped wave fragment, two cats facing away, and a near-miss where
  a largest-component rule would have deleted two of three trees and both toy-engine wheels.
- CI is ~2h45m regardless of diff size. **Batch branch updates**; cancel superseded runs when a
  double-push starts two concurrent ones.
- `rg` does not exist on the runner and is a shell function locally — never put it in a test;
  use `grep -E`. `grep -q` (BRE) is not a safe substitute where a pattern uses `|`, `(` or `+`.
- `mktemp` returns empty under the sandbox. Unity `-runTests` must not get `-quit`. Never
  `git commit -a` (Unity drifts 5 settings files; `dotnet restore` rewrites a lock file).
- PreToolUse hooks scan command **prose** — a command merely naming an immutable path can be
  denied. Use `--body-file`; report denials rather than rephrasing around them.
- Unity cannot run in the agent sandbox (cold Library needs `packages.unity.com`; the editor
  writes outside the allowlist). A subagent was correctly blocked for trying to disable the
  sandbox — the human must run the build, or authorise it explicitly and specifically.
- Never read `.env`. Never run any Play upload.

---

## 6. WHAT THE HUMAN OWES

1. **Look at the installed APK** and report which cats face wrong and whether they are coloured.
2. **ADR-0013's signature** and the curated-source decision — the last gate before shipping art.
3. **Lane D's evidence row** on #95 (needs a chat with capacity).
4. **The daily-leg gating ruling**: 90-date leg on every PR, nightly only, or nightly + label.
   The latter two need a `.github` change.
5. **Durable backup** of the paid-tier source set.
6. Ratify or flag the census merge-records on #94/#98/#99.
