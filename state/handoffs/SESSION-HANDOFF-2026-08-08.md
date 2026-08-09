# Session handoff — 2026-08-08 (crash recovery → wave 2 → gameplay loop)

Written at the end of a long session for whoever picks the work up next. **Read `state/PROJECT_STATE.md` first** (mandatory session-start read); this file is the narrative that state file compresses, plus the things only a person can decide.

## Where main stands

`main` carries the complete wired UX tranche, the closed gameplay loop, ten authored levels' worth of pipeline work (band pending — see below), hardened dev seams, and a merge-authority census awaiting ratification. Twenty-four PRs merged across this session (#35–#58, less #40 which was superseded). Zero open PRs at handoff time except as noted under "In flight".

**The game today:** boot → L001 board (shipped) or, in a dev build with a `boot.json` file-drop, Home → LevelIntro → Play. Fail shows a cause banner + Try-again, with a hint chip on the second failure. **Win now shows the results panel with a live Next chip that loads the next band level** (CM-LOADNEXT). Greybox art, no audio, no level select.

## THE HUMAN'S QUEUE — nothing below can be closed by an agent

1. **CM-C11 L006 ruling (blocks a built branch).** `task/CM-C11-levels-band` has L007–L010 authored, validated, solver-witnessed, staged, all criteria green — but **L006 cannot satisfy both criterion 2 (byte-faithful to the ratified anchor, CONFLICT-1 option A) and criterion 4(b) (≥70% retention under the pessimistic NEW-Q4 reading)**. Empirically reproduced three ways: `LevelSolver.CompareWins`' tie-break always picks the earliest safe toggle tick, so any toggle not at tick 0 has zero early-side jitter margin; L006's three non-zero toggles compound to 35% pessimistic retention **on the unmodified shipped anchor**. Nine of fifteen wrappers are red on this single root cause. Options recorded: (1) re-scope 4(b)'s pessimistic bar to L007–L010 and record L006's 35% as a characteristic of the ratified anchor *(the outgoing session's recommendation — the shipped gate uses the optimistic reading, which L006 passes at 100%, and NEW-Q4 is itself unratified)*; (2) a different explicit bar for L006; (3) reopen CONFLICT-1 to option B/C and redesign L006 (jointly tied to CM-C5.1/HC-14 per the frozen JOINT NOTE). Full reproduction in `state/handoffs/CM-C11.md`.
2. **Wrap-to-L001 ratification** (rides CM-LOADNEXT): end-of-band currently wraps to L001 (demo-friendly infinite loop). One-line seam (`GameRoot.WrapAtEndOfBand` + `LevelBand`) to change. Flagged, never presented as ratified.
3. **HC-25 merge-authority deviation** — `state/PROJECT_STATE.md` §Blocked. The census is lane-split, self-implicating (it puts its own PRs in the pre-grant class), and survived a HIGH-severity laundering catch in review. Ratify or flag; the ratification act should also rotate the ratified span to `state/archive/` (human-authored commit only — agents must not rotate that row).
4. **H-1** — your direct confirmation that the `.claude/settings.json` ask-array removal was your own `/permissions` act. Channel: a human-authored commit or your own GitHub comment cited by URL; an agent-transcribed relay cannot close it.
5. **Criterion-8 disposition** — evidence is on main (`evals/results/device/c2b-crit8-urp/`): game-layer 6,673 intervals / 106.7 s, median bucket 16 ms, mean-of-worst-1% [19.17, 20.17) ms vs the ≤33.3 budget, `present2presentDelta` 6,511/6,513 at 0 ms. Three limbs stay reserved: the **median convention is unpinned** (Q-DEVFIX-4, ~0.03 ms of margin), the **window composition** question (presents ≠ play; L001 ships 6.25 s of active sim against a 60 s clause), and **binary provenance** (no APK hash bound to committed artifacts). Plus the security-R3 question: the sitting produced a black first capture and the numbers; step 2 says a black capture voids that run's numbers, and the within-sitting ordering is unprovable from the artifacts.
6. **R-1 closure** — the device leg is satisfied by #49's frame; the **human-carried editor Play-Mode screenshot** is still owed (`CM-C2b-DEVFIX.md:240/:276`). The committed visual-pass frames are explicitly agent-carried and do **not** satisfy it.
7. **#43 ride-along ratifications** (H2, H3(ii)/(iii), H4, H5-residual, H6) and the **#46 disclosures** (F6 `Home.Hide()` as an undeclared addition to a frozen flow; corner-vs-center tap literalism).
8. **Play the game.** APK v2 is built and verified (dev-fenced seam tokens present in `global-metadata.dat`) but **predates CM-LOADNEXT** — rebuild from current main for the loop. Commands are in "Device loop" below.

## In flight at handoff

- **#57 CM-LOADNEXT** — review round-1 verdict MERGEABLE conditional on a fixup that was applied within the reviewer's explicit sanction (no second round required); armed for auto-merge, CI running at handoff. If it did not land, re-run `gh pr update-branch 57` and confirm auto-merge is still armed.
- **`task/CM-C11-levels-band`** — built, not pushed, blocked on item 1 above. Commit `96657d8` on merge `dd328ae`.

## Operating notes the next session will otherwise re-learn the hard way

- **Sandbox:** Unity batch, `dotnet test` (MSBuild named pipes), `adb`, `gh`, and `git push` all fail sandboxed. Retry those unsandboxed — recorded precedent, not a new escalation. Everything else runs sandboxed.
- **The review gate is not ceremony.** This session's reviews caught: an unsourced device measurement about to enter the state file; a merge-authority claim laundered by a truncated quote (the elided clause was the failed precondition); a binary-PNG false positive in `check.sh` that would have broken **every** branch's CI (~1 in 4 future evidence images, by byte census); a headline bug fix with zero test coverage; three tautological "preconditions" with no red-power; and a one-frame double-tap level-skip. Run both legs when the classifier says RISKY, and treat reviewer findings as findings.
- **Mutation proofs are the currency.** Every load-bearing assert should have a named mutation that turns it red, captured with the exact message, reverted byte-clean. Reviewers now check this by re-deriving red-power algebraically.
- **Prose landmines are real gates**: `tests/unity/failure.test.sh` scans raw source text for literal UI strings, and the vocabulary guard does substring matching (a comment containing "closes" tripped it via "lose"). `devcap.test.sh` scans dev-capture symbol references — now including both override seams and `#else` arms.
- **C# trap:** inside any `CatMetro.*` namespace, bare `Application`/`Screen` resolve to CatMetro types. Fully qualify UnityEngine types in Bootstrap/Presentation/test files.
- **Editor capture host is 640×480** regardless of `-screen-width/-height`; all committed UX evidence uses it. Size/DPI observations from it are host-scale, not device findings.
- **Worktrees:** the treadmill runs one worktree per lane under the session scratchpad. Several stale ones from crashed sessions may still exist — `git worktree list` then `git worktree prune` after verifying nothing uncommitted (this session left `wt-c11` with unpushed work — do not prune that one until item 1 resolves).

## Device loop (when the Pixel is attached)

```bash
# rebuild from current main first — APK v2 predates the gameplay loop
CM_APK_OUT=<path>.apk CM_DEV_BUILD=1 /Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$(pwd)/unity" -executeMethod CatMetroCliBuild.BuildAndroid -quit -logFile <log>
adb -s 48121FDAP006X4 install -r <path>.apk
echo '{"bootToHome": true}' | adb -s 48121FDAP006X4 shell run-as com.catmetro.game sh -c 'mkdir -p files/devcap && cat > files/devcap/boot.json'
adb -s 48121FDAP006X4 exec-out screencap -p > frame.png
```
Verify the build independently of the log (licensing noise makes a successful build look failed): `aapt dump badging` for `application-debuggable`, and a strings scan of `global-metadata.dat` for `DEVCAP_BOOT_OVERRIDE`.

## Candidate next contracts (ranked for demo impact)

1. **Level select** — Home's district silhouettes are decorative; nothing reaches L002+ except by winning. Pairs naturally with CM-C11's band.
2. **Back navigation** — `ScreenStack` has no Back consumer; the breadcrumb is recorded but unused.
3. **Art + audio pass** — everything is greybox; TG-1..8 taste gates are still ahead and are human-judged.
4. **The recorded debt rows** in `state/PROJECT_STATE.md` §Known debt, notably: the CI enforcement gaps (the `unity-editmode` job the harness names does not exist, so Unity suites run on exactly one machine; and once the release build path is tracked, CI must assert it never sets `BuildOptions.Development`), the fixture/ladder residuals from #57's review (F4 bad-next-level throw path, F7 D-1 generalization, F8 unpinned ladder values), and the dev-only Results-vs-Home tap collision.
