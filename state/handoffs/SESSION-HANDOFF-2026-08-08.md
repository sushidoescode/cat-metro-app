# Session handoff — 2026-08-08 (crash recovery → wave 2 → gameplay loop)

Written at the end of a long session for whoever picks the work up next. **Read `state/PROJECT_STATE.md` first** (mandatory session-start read); this file is the narrative that state file compresses, plus the things only a person can decide.

## Where main stands

`main` at handoff is `ddb8be1` (#57, CM-LOADNEXT). It carries the complete wired UX tranche, the closed gameplay loop, hardened dev seams, and a merge-authority census awaiting ratification. **23 PRs merged this session: #35–#39, #41–#58** (#40 superseded; #59 — this document — was the only PR open at handoff).

**Verify before trusting** any claim below: `git fetch && git log --oneline -30 origin/main` — fetch first and read `origin/main`, not local `main`, which is stale on a cold clone (a reviewer hit exactly that false negative while checking this file). This document was written across the final hours and its early drafts were wrong about what had landed.

**The game today:** boot → L001 board (shipped) or, in a dev build with a `boot.json` file-drop, Home → LevelIntro → Play. Fail shows a cause banner + Try-again, with a hint chip on the second failure. **Win → results panel with a live Next chip that loads the next band level** (CM-LOADNEXT, merged). Greybox art, no audio, no level select.

**`state/PROJECT_STATE.md` is stale at this commit** — it predates #58 and still lists the CM-SEAMS row as unmerged and `main` as `6f5fed5`. It remains the mandatory read; reconcile it against `git log` and expect the next session's first bookkeeping PR to refresh it.

## THE HUMAN'S QUEUE — nothing below can be closed by an agent

1. **CM-C11 L006 ruling (blocks a built branch).** `task/CM-C11-levels-band` (pushed; commit `96657d8`) has L007–L010 authored, validated, solver-witnessed, and staged, with their own criteria green — note the branch's record lists criteria 3, 4, 11 and 12 as *not certified*, so ruling on L006 alone does not turn the branch green — but **L006 cannot satisfy both criterion 2 (byte-faithful to the *authored* anchor) and criterion 4(b) (≥70% retention under the pessimistic NEW-Q4 reading)**. Mechanism: `LevelSolver.CompareWins`' tie-break always picks the earliest safe toggle tick, so any toggle not at tick 0 has zero early-side jitter margin; L006's three non-zero toggles (ticks 32/72/112) compound to 35% pessimistic retention (`wins=7 losses=0 pinned=13`) **on the unmodified authored anchor**. Evidence is one shift-harness (±1..±3 ticks over each optimal-log entry) applied to three levels plus the corpus test's own failure — *not* three independent methods, as an earlier draft implied. Blast radius on the suite: the `dotnet test`-based wrappers plus the new corpus wrapper go red on this one cause (**a reviewer's static derivation says 8 of 15; the branch's own record says the full-suite run was still pending at handoff — treat any specific count as unverified until you run it**).

   **Status of the anchor — the records CONFLICT, and weighing them is yours (two agent sessions have now mis-stated this in opposite directions; do not accept either framing):**
   - *Pointing to "open":* the frozen contract's own CONFLICT-1 section reads "human call, default A", "the human picks; the contract does not", and calls option A "DEFAULT for execution, and the default that stands at freeze". CONFLICT-1 appears in neither the contract's Ratifications table nor PROJECT_STATE's 2026-08-05 ratification batch.
   - *Pointing to "settled":* that same contract carries a **"Freeze-time ratification addendum (human, in-session 2026-08-05/06)"** (`CM-C11-frozen-contract.md:508`) reading "HC-10×HC-14 defaults **CONFIRMED** … the L006 anchor stands as authored", and `CM-C11.md` calls option A "the ratified default".
   - Either way the call is **joint with CM-C5.1/HC-14** per the frozen JOINT NOTE — neither contract may resolve it alone — so if you reopen, expect to touch both.

   Options: (1) re-scope 4(b)'s pessimistic bar to L007–L010, recording L006's 35% as a characteristic of the authored anchor *(the outgoing session's recommendation — but weigh that it is also the only option that unblocks that session's own built branch; the shipped gate uses the optimistic reading, which L006 passes at 100%, and NEW-Q4 is itself unratified)*; (2) a different explicit bar for L006; (3) reopen CONFLICT-1 to option B/C and redesign L006 — **note option B is "free if NEW-Q1 resolves to Q1-A", and NEW-Q1 is also PENDING your answer**, so B may cost less than it appears. **Blast radius of option (1), which the outgoing session did not surface:** under the same pessimistic reading, PROJECT_STATE's PR-#15-F4 risk trigger records L002/L003/L005 reading 65%/75%/60% — **L005 is also below the 70% bar**, so option (1) leaves a recorded risk trigger unresolved. **Option B's other half, for symmetry:** the contract also says that if NEW-Q1 does *not* resolve to Q1-A, option B contradicts `product_spec.md:540`.

   Full reproduction is in `state/handoffs/CM-C11.md` **on that branch only** (`git show origin/task/CM-C11-levels-band:state/handoffs/CM-C11.md`), not on main. **⚠️ That file repeats the "ratified default" error four times** (incl. "not mine to redesign under the current ratified default" — an agent declining options B/C on the strength of a ratification that does not exist). The paragraph above is the corrected statement; the reproduction's *technical* content stands, its *authority* language does not. **That language is a merge blocker on `task/CM-C11-levels-band`** — it must be corrected before that branch lands, whatever the ruling.
2. **Wrap-to-L001 ratification** (rides CM-LOADNEXT): end-of-band currently wraps to L001 (demo-friendly infinite loop). One-line seam (`GameRoot.WrapAtEndOfBand` + `LevelBand`) to change. Flagged, never presented as ratified.
3. **HC-25 merge-authority deviation** — `state/PROJECT_STATE.md` §Blocked. The census is lane-split, self-implicating (it puts its own PRs in the pre-grant class), and survived a HIGH-severity laundering catch in review. **It cannot be ratified as written: its scope is #35–#56, and #57, #58 and #59 have since extended the deviation. The census's own clause requires those appended BEFORE ratification** — that append is the next session's first bookkeeping job. Then ratify or flag; the ratification act should also rotate the ratified span to `state/archive/` (human-authored commit only — agents must not rotate that row).
4. **H-1** — your direct confirmation that the `.claude/settings.json` ask-array removal was your own `/permissions` act. Channel: a human-authored commit or your own GitHub comment cited by URL; an agent-transcribed relay cannot close it.
5. **Criterion-8 disposition** — evidence is on main (`evals/results/device/c2b-crit8-urp/`): game-layer 6,673 intervals / 106.7 s, median bucket 16 ms, mean-of-worst-1% [19.17, 20.17) ms vs the ≤33.3 budget, `present2presentDelta` 6,511/6,513 at 0 ms. Three limbs stay reserved: the **median convention is unpinned** (Q-DEVFIX-4, ~0.03 ms of margin), the **window composition** question (presents ≠ play; L001 ships 6.25 s of active sim against a 60 s clause), and **binary provenance** (no APK hash bound to committed artifacts). Plus the security-R3 question: the sitting produced a black first capture and the numbers; step 2 says a black capture voids that run's numbers, and the within-sitting ordering is unprovable from the artifacts.
6. **R-1 closure** — the device leg is satisfied by #49's frame; the **human-carried editor Play-Mode screenshot** is still owed (`CM-C2b-DEVFIX.md:240/:276`). The committed visual-pass frames are explicitly agent-carried and do **not** satisfy it.
7. **#43 ride-along ratifications** (H2, H3(ii)/(iii), H4, H5-residual, H6) and the **#46 disclosures** (F6 `Home.Hide()` as an undeclared addition to a frozen flow; corner-vs-center tap literalism).
8. **Play the game.** APK v2 is built and verified (dev-fenced seam tokens present in `global-metadata.dat`) but **predates CM-LOADNEXT** — rebuild from current main for the loop. Commands are in "Device loop" below.

## In flight at handoff

- **#57 CM-LOADNEXT — MERGED 2026-08-09.** Its round-1 verdict (MERGEABLE conditional on a fixup, applied within what the reviewer described as an explicit no-second-round sanction) is an **agent-recorded** disposition living in the PR's comments — re-read it there rather than taking this line's word, and note the sanctioned-fixup route is not the repo's documented way to price down a review leg (sprint pricing via `scripts/forge-risk.sh` is — see #48/#50/#53/#58). Treat it as a one-off to be re-examined, not precedent. **General rule for whoever reads this: arming or completing any merge is a merge act, and HC-25 requires a fresh in-session human re-confirmation for your session — you do not inherit the last session's.**
- **`task/CM-C11-levels-band`** — pushed (commit `96657d8`), blocked on item 1 above.
- **#59** — this handoff document itself.

## Operating notes the next session will otherwise re-learn the hard way

- **Standing human rule (2026-08-06, PROJECT_STATE §Decisions): visual verification.** Rendered-frame captures of the real scene are required evidence for anything visual; code-green alone is insufficient. This binds any art/UX work.
- **Sandbox:** Unity batch, `dotnet test` (MSBuild named pipes), `gh`, and `git push` fail sandboxed — retry those unsandboxed per recorded precedent (`SESSION-HANDOFF-phase6-10.md`, `CM-UX-07-frozen-contract.md`). `adb` also fails (smartsocket bind) but has **no in-repo precedent line** — treat its first unsandboxed use in a session as a fresh disclosure, not a settled exception. Everything else runs sandboxed.
- **The review gate is not ceremony.** This session's reviews caught: an unsourced device measurement about to enter the state file; a merge-authority claim laundered by a truncated quote (the elided clause was the failed precondition); a binary-PNG false positive in `check.sh` that would have broken **every** branch's CI (~1 in 4 future evidence images, by byte census); a headline bug fix with zero test coverage; three tautological "preconditions" with no red-power; and a one-frame double-tap level-skip. Run both legs when the classifier says RISKY, and treat reviewer findings as findings.
- **Mutation proofs are the currency.** Every load-bearing assert should have a named mutation that turns it red, captured with the exact message, reverted byte-clean. Reviewers now check this by re-deriving red-power algebraically.
- **Prose landmines are real gates**: `tests/unity/failure.test.sh` scans raw source text for literal UI strings, and the vocabulary guard does substring matching (a comment containing "closes" tripped it via "lose"). `devcap.test.sh` scans dev-capture symbol references — now including both override seams and `#else` arms.
- **C# trap:** inside any `CatMetro.*` namespace, bare `Application` resolves to the `CatMetro.Application` namespace, not `UnityEngine.Application` (recorded CS0118 in `CM-C3-DEVCAP.md:118`). Fully qualify UnityEngine types in Bootstrap/Presentation/test files. (An earlier draft also named `Screen` — no `CatMetro.Screen` exists; that was wrong.)
- **Editor capture host is 640×480** regardless of `-screen-width/-height`; all committed UX evidence uses it. Size/DPI observations from it are host-scale, not device findings.
- **Worktrees:** the treadmill runs one worktree per lane under the session scratchpad. Several stale ones from crashed sessions may still exist — `git worktree list` then `git worktree prune` after verifying nothing uncommitted. `wt-c11`'s work is now pushed (`96657d8`), so pruning it is safe; the branch is what matters, not the worktree.

## Device loop (when the Pixel is attached)

**⚠️ Before you build: `unity/Assets/Editor/CatMetroCliBuild.cs` — the shim `-executeMethod` calls — has NEVER been committed on any ref.** It exists only as an untracked file in the main checkout (and `CM-C10-frozen-contract.md:347`'s claim that it "is committed on main" is false). A fresh `git worktree` will not have it, and Unity will exit with "executeMethod class 'CatMetroCliBuild' could not be found" — which the licensing-noise warning below will make you misdiagnose. Copy it in from the main checkout, or commit it.

```bash
# rebuild from current main first — APK v2 predates the gameplay loop
CM_APK_OUT=<path>.apk CM_DEV_BUILD=1 /Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$(pwd)/unity" -executeMethod CatMetroCliBuild.BuildAndroid -quit -logFile <log>
adb -s <pixel9pro> install -r <path>.apk
# NOTE the outer double quotes: adb shell joins argv without re-quoting, so an
# unquoted form loses the && and the redirect to the DEVICE shell and silently fails.
echo '{"bootToHome": true}' | adb -s <pixel9pro> shell "run-as com.catmetro.game sh -c 'mkdir -p files/devcap && cat > files/devcap/boot.json'"
adb -s <pixel9pro> exec-out screencap -p > frame.png
```
Verify the build independently of the log (licensing noise makes a successful build look failed): `aapt dump badging` for `application-debuggable`, and a strings scan of `global-metadata.dat` for `DEVCAP_BOOT_OVERRIDE`.

## Candidate next contracts (ranked for demo impact)

1. **Level select** — Home's district silhouettes are decorative; nothing reaches L002+ except by winning. Pairs naturally with CM-C11's band.
2. **Back navigation** — `ScreenStack` has no Back consumer; the breadcrumb is recorded but unused.
3. **Art + audio pass** — everything is greybox; TG-1..8 taste gates are still ahead and are human-judged.
4. **The recorded debt rows** in `state/PROJECT_STATE.md` §Known debt, notably: the CI enforcement gaps (the `unity-editmode` job the harness names does not exist, so Unity suites run on exactly one machine; and once the release build path is tracked, CI must assert it never sets `BuildOptions.Development`), the fixture/ladder residuals from #57's review (F4 bad-next-level throw path, F7 D-1 generalization, F8 unpinned ladder values), and the dev-only Results-vs-Home tap collision.
