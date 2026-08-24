# Orchestrator handoff — 2026-08-23

For a fresh Fable 5 orchestrator chat. Supersedes `HANDOFF-ORCHESTRATOR-2026-08-19.md`.
Ground truth from the machine, never from this file: `git status`, `gh pr list`, `adb devices -l`.

**Forge Kit is gone.** No frozen contracts, no staged gates, no census, no review ceremony.
Build it, render it, look at it, iterate. Decisions get one sentence, not a document.

---

## 0. FIRST: three lanes' work is uncommitted and intermingled in one tree

This is the thing to handle before anything else.

Three parallel chats (UI, track, props) all wrote **directly into the main checkout**
`/Users/sushantsrikrish/cat-metro-app`. Nothing was committed, branched, or pushed:

- `git status --porcelain` → **103 entries**
- Local `HEAD` = `3115ebd`, but `origin/main` = `ef333eb` — the checkout is **three merges behind**
  (#94 decimation, #98 curation, #99 state) while carrying all the new work
- No new branches, no new PRs. `gh pr list` shows only the older `#95` and `#96`.

Much of that 103 is not lane work: an earlier session ran `git checkout origin/main -- .`, which
staged origin/main's content while `HEAD` stayed behind, so #94's files show as adds. Realigning
`HEAD` without touching the working tree collapses the noise and leaves only real changes:

```
git -C /Users/sushantsrikrish/cat-metro-app reset --mixed ef333eb   # moves HEAD, keeps worktree
git status --porcelain | wc -l                                      # should drop sharply
```

`reset --mixed` does not touch files. Do **not** use `reset --hard` — it is denied by settings
and would destroy every lane's work at once.

**Then separate by area into commits/branches**, because these are independent features that
should not land as one blob:

| area | files |
|---|---|
| UI | `Presentation/Hud/DevelopmentConsoleGuard.cs`, `Hud/BannerView.cs`, `Screens/HomeScreenView.cs`, `Screens/HomeLayout.cs`, `Tests/EditMode/Presentation/HomeLayoutTests.cs` |
| Track | `Board/TrackSplineGraph.cs`, `Board/ToyTrackMeshBuilder.cs`, `Board/BoardView.cs`, `Tests/EditMode/Presentation/{TrackSplineGraph,ToyTrackMeshBuilder}Tests.cs` |
| Props | `Presentation/Props/**`, `Board/BoardSceneLook.cs`, `Board/BoardSurface.cs` |
| Evidence | `.catshots/` — 52 MB of captures, currently untracked. Decide: commit a curated few, or leave out. |

**A fourth lane (scene/lighting) was still running when this was written** and writes into the
same tree and the same `BoardView.cs`. Expect more intermingling; separate after it lands, not
during.

Nothing is at risk of loss — this is the home directory, not `/private/tmp` — but it is one
`reset --hard` away from a very bad day.

---

## 1. What the three completed lanes delivered

Self-reported, with independent verification noted. None ran an APK or device build.

**UI (Chat 1)** — dev-console overlay suppressed on Android with logs intact; failure text now
safe-area-aware TMP sizing that fits phone width; Home rebuilt as a depot-route card with three
cat holders and a wide labelled Play CTA. EditMode 6/6, PlayMode 42/42, `check.sh` pass.
Captures under `/tmp/catmetro-ui-final.1ujmCk/captures/`. **Not device-verified** — the Pixel was
not connected, so the native overlay suppression is test-covered only. Its worktree
`/private/tmp/catmetro-home-route-composition` [`task/HOME-ROUTE-CARD`] is under `/tmp` and the
macOS reaper eats that after ~3 days.

**Track (Chat 2)** — graph-generated cream sleepers with twin navy rails, C1-continuous turnouts,
even-distance traversal, trains following the rendered spline. All 17 levels pass the spline
tests; mesh 5/5; board+train integration 3/3. **PlayMode 181/182** — see the defect below.

**Props (Chat 3)** — all five props re-baked at 2048² with the correct weld/re-unwrap workflow,
multipart geometry preserved (trees 3, clutter 7, engine 3 components — the trap called out in
the previous handoff was avoided). Depot, kiosks, trees, clutter and engine integrated through
`Props/BoardPropDecorator.cs`, with strict URP/atlas admission in `Props/PropModelCatalog.cs`.
Stations now have wooden bases, line-coloured roofs and distinct circle/square badges. Unity
46/46, .NET 857/857. Captures at `.catshots/props/runtime-L001.png`, `runtime-L008.png`.

---

## 2. Known defect, already traced

The 1 PlayMode failure is real and cross-lane. `Props/BoardPropDecorator.cs:255`
`CreateStationPart()` assigns `renderer.sharedMaterial = GreyboxMaterial.Shared`, and the greybox
material is **unlit**. `Props/PropModelCatalog.cs:102` correctly requires
`Universal Render Pipeline/Lit`, so `station:wood-base` fails its own admission rule.

Fix: give station architecture its own lit material rather than reusing the greybox one. Small,
but it touches `GreyboxMaterial`, which the scene lane may also be changing — sequence it after
that lane lands.

---

## 3. The art pipeline is DONE and verified

All eight cats are re-baked, correctly coloured, and captured:
`docs/reference/cats-baked-front.png`, `cats-baked-back.png`, plus
`example-baked-atlas.png` showing what a correct atlas looks like.

**The finding that unblocked it:** aggressive decimation invalidates the provider atlas. It is a
clean unwrap at ~2M triangles (0.7% UV-split) packed as many small islands; at 15k most islands
hold one or two triangles, so each surviving triangle samples a scrap of texture and the model
renders as camouflage (measured 15–17% split, 20× the source). The missing step was retopology:
re-unwrap the low-poly and bake colour from the high-poly. Tooling: `scratchpad/rebake.py`,
`rebake-all.sh`, `uv-seams.py`.

`merge_vertices` is the trap and it bites **both** ways: preserving an atlas → `False` (welding
collapses UV seams → camouflage); re-unwrapping → `True` (glTF duplicates a vertex per seam, so
unwelded is a triangle soup and every face becomes its own island). Sanity number: a 15k-triangle
closed cat welds to **~7,490 vertices**.

Models carry **no animations and no skins** — all static single meshes. The "extra hind legs"
on the standing cats are arms at the sides plus feet on the ground: four limbs, a chibi sitting
pose, confirmed by a ground-slice cluster count of 2. It reads oddly at thumbnail size, which is
a camera-angle problem, not a model problem.

---

## 4. Traps that cost real time

**Render harnesses race.** `CatMaterialCheck.cs` rendered before textures finished importing, so
three consecutive runs produced all-orange, all-dark and all-blue from provably-correct material
data (GUIDs matched one-to-one). Fixed with `ImportAssetOptions.ForceSynchronousImport` plus a
second `cam.Render()`. **If a render looks uniformly wrong, suspect the harness before the assets.**

**Devices — verify, never copy a serial.** Run `adb devices -l` and read `model:`.
`48121FDAP006X4` = Pixel 9 Pro (the target). `2G0YC5ZF7Z056Q` = **Quest 3**.
`emulator-5554` = **Pico OS6 emulator**. Older docs called the Quest "the Pixel" for a week.

**Unity and Blender cannot run sandboxed** — cold Library needs the network, both write outside
the allowlist, Blender segfaults in Metal detection. Run them unsandboxed.

`scripts/build-apk.sh` is the real build path (`scripts/build.sh` is a stub that builds nothing).
Unity `-runTests` must not get `-quit`. Never `git commit -a`. `grep -E`, never `rg`. CI checks
out shallow and never compiles C#, so green there means less than it looks.

---

## 5. Open and owed

- **`#96` ADR-0013 licensing** — green, reviewed twice, **needs a human signature**. Nothing
  generated may ship in a Play binary until it is signed. Agent work is finished on it.
- **`#95` cat wiring** — code green; blocked on one evidence-file row (frame 03 is described as
  campaign play but is a staged forced-colour bench).
- **Nobody has built an APK since the art landed.** The last device build showed grey cats and
  greybox board. A fresh one is the real proof, and it is the fastest way to see whether the
  week's work actually moved the screenshots.
- The generated art in `unity/Assets/Art/Generated/incoming/` is gitignored, one machine only.
  `curation-backups/` there is the only copy of two paid assets' provider originals.

## 6. What to do next

1. Realign `HEAD`, separate the three lanes into commits, get them onto branches.
2. Fix the `station:wood-base` unlit defect → 182/182.
3. Land the scene lane when it finishes; it and the track lane both own `BoardView.cs`.
4. **Build an APK and look at it on the Pixel.** That answers the only question that matters.
5. Then `docs/LOOK.md` step 6 — cats riding carriages, which is what the concept art is selling.
