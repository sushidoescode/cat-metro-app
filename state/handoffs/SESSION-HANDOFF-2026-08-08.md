# Session handoff — 2026-08-08 (crash recovery → wave 2 → gameplay loop)

Written at the end of a long session for whoever picks the work up next. **Read `state/PROJECT_STATE.md` first** (mandatory session-start read); this file is the narrative that state file compresses, plus the things only a person can decide.

## Where main stands

`main` at handoff is `d1550f2` (#58, CM-SEAMS). It carries the complete wired UX tranche, hardened dev seams, and a merge-authority census awaiting ratification. **22 PRs merged this session: #35–#39, #41–#56, #58** (#40 superseded; #57 and #59 were still open at handoff — check `gh pr list` for their real state before trusting any claim below).

**Verify before trusting:** this file was written while #57 was in CI. If `git log --oneline main | grep CM-LOADNEXT` finds nothing, the loop below is NOT on main.

**The game today:** boot → L001 board (shipped) or, in a dev build with a `boot.json` file-drop, Home → LevelIntro → Play. Fail shows a cause banner + Try-again, with a hint chip on the second failure. **Win → results panel with a live Next chip that loads the next band level — ships with CM-LOADNEXT (#57), which was mid-CI at handoff, not yet in `main`'s ancestry.** Greybox art, no audio, no level select.

**`state/PROJECT_STATE.md` is stale at this commit** — it predates #58 and still lists the CM-SEAMS row as unmerged and `main` as `6f5fed5`. It remains the mandatory read; reconcile it against `git log` and expect the next session's first bookkeeping PR to refresh it.

## THE HUMAN'S QUEUE — nothing below can be closed by an agent

1. **CM-C11 L006 ruling (blocks a built branch).** `task/CM-C11-levels-band` (pushed; commit `96657d8`) has L007–L010 authored, validated, solver-witnessed, staged, all criteria green — but **L006 cannot satisfy both criterion 2 (byte-faithful to the *authored* anchor) and criterion 4(b) (≥70% retention under the pessimistic NEW-Q4 reading)**. Empirically reproduced three ways: `LevelSolver.CompareWins`' tie-break always picks the earliest safe toggle tick, so any toggle not at tick 0 has zero early-side jitter margin; L006's three non-zero toggles compound to 35% pessimistic retention **on the unmodified authored anchor**. Nine of fifteen wrappers are red on this single root cause.

   **Status of the anchor, stated precisely (do not let anyone tell you otherwise):** CONFLICT-1 is an **open human call** — the frozen contract's own words are "human call, default A", "the human picks; the contract does not", and option A is "DEFAULT for execution, and the default that stands at freeze". **It is NOT ratified**; it is absent from the contract's Ratifications table and from PROJECT_STATE's 2026-08-05 ratification batch. Reopening to B or C costs nothing in standing — those options were never closed. The call is joint with CM-C5.1/HC-14 per the frozen JOINT NOTE, and neither contract may resolve it alone.

   Options: (1) re-scope 4(b)'s pessimistic bar to L007–L010, recording L006's 35% as a characteristic of the authored anchor *(the outgoing session's recommendation — but weigh that it is also the only option that unblocks that session's own built branch; the shipped gate uses the optimistic reading, which L006 passes at 100%, and NEW-Q4 is itself unratified)*; (2) a different explicit bar for L006; (3) reopen CONFLICT-1 to option B/C and redesign L006 — **note option B is "free if NEW-Q1 resolves to Q1-A", and NEW-Q1 is also PENDING your answer**, so B may cost less than it appears. **Blast radius of option (1), which the outgoing session did not surface:** under the same pessimistic reading, PROJECT_STATE's PR-#15-F4 risk trigger records L002/L003/L005 reading 65%/75%/60% — **L005 is also below the 70% bar**, so option (1) leaves a recorded risk trigger unresolved. Full reproduction is in `state/handoffs/CM-C11.md` **on that branch only** (`git show origin/task/CM-C11-levels-band:state/handoffs/CM-C11.md`), not on main.
2. **Wrap-to-L001 ratification** (rides CM-LOADNEXT): end-of-band currently wraps to L001 (demo-friendly infinite loop). One-line seam (`GameRoot.WrapAtEndOfBand` + `LevelBand`) to change. Flagged, never presented as ratified.
3. **HC-25 merge-authority deviation** — `state/PROJECT_STATE.md` §Blocked. The census is lane-split, self-implicating (it puts its own PRs in the pre-grant class), and survived a HIGH-severity laundering catch in review. **It cannot be ratified as written: its scope is #35–#56, and #57, #58 and #59 have since extended the deviation. The census's own clause requires those appended BEFORE ratification** — that append is the next session's first bookkeeping job. Then ratify or flag; the ratification act should also rotate the ratified span to `state/archive/` (human-authored commit only — agents must not rotate that row).
4. **H-1** — your direct confirmation that the `.claude/settings.json` ask-array removal was your own `/permissions` act. Channel: a human-authored commit or your own GitHub comment cited by URL; an agent-transcribed relay cannot close it.
5. **Criterion-8 disposition** — evidence is on main (`evals/results/device/c2b-crit8-urp/`): game-layer 6,673 intervals / 106.7 s, median bucket 16 ms, mean-of-worst-1% [19.17, 20.17) ms vs the ≤33.3 budget, `present2presentDelta` 6,511/6,513 at 0 ms. Three limbs stay reserved: the **median convention is unpinned** (Q-DEVFIX-4, ~0.03 ms of margin), the **window composition** question (presents ≠ play; L001 ships 6.25 s of active sim against a 60 s clause), and **binary provenance** (no APK hash bound to committed artifacts). Plus the security-R3 question: the sitting produced a black first capture and the numbers; step 2 says a black capture voids that run's numbers, and the within-sitting ordering is unprovable from the artifacts.
6. **R-1 closure** — the device leg is satisfied by #49's frame; the **human-carried editor Play-Mode screenshot** is still owed (`CM-C2b-DEVFIX.md:240/:276`). The committed visual-pass frames are explicitly agent-carried and do **not** satisfy it.
7. **#43 ride-along ratifications** (H2, H3(ii)/(iii), H4, H5-residual, H6) and the **#46 disclosures** (F6 `Home.Hide()` as an undeclared addition to a frozen flow; corner-vs-center tap literalism).
8. **Play the game.** APK v2 is built and verified (dev-fenced seam tokens present in `global-metadata.dat`) but **predates CM-LOADNEXT** — rebuild from current main for the loop. Commands are in "Device loop" below.

## In flight at handoff

- **#57 CM-LOADNEXT** — armed for auto-merge with CI running at handoff. Its round-1 verdict (MERGEABLE conditional on a fixup, applied within what the reviewer described as an explicit no-second-round sanction) is an **agent-recorded** disposition living in the PR's comments — re-read it there rather than taking this line's word. **If it did not land, completing that merge is a merge act: HC-25 requires a fresh in-session human re-confirmation before a new session arms or completes it.** Note also that the sanctioned-fixup route is not the repo's documented way to price down a review leg — sprint pricing via `scripts/forge-risk.sh` is (see #48/#50/#53/#58) — so treat it as a one-off to be re-examined, not precedent.
- **`task/CM-C11-levels-band`** — pushed (commit `96657d8`), blocked on item 1 above.
- **#59** — this handoff document itself.

## Operating notes the next session will otherwise re-learn the hard way

- **Standing human rule (2026-08-06, PROJECT_STATE §Decisions): visual verification.** Rendered-frame captures of the real scene are required evidence for anything visual; code-green alone is insufficient. This binds any art/UX work.
- **Sandbox:** Unity batch, `dotnet test` (MSBuild named pipes), `gh`, and `git push` fail sandboxed — retry those unsandboxed per recorded precedent (`SESSION-HANDOFF-phase6-10.md`, `CM-UX-07-frozen-contract.md`). `adb` also fails (smartsocket bind) but has **no in-repo precedent line** — treat its first unsandboxed use in a session as a fresh disclosure, not a settled exception. Everything else runs sandboxed.
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
adb -s <pixel9pro> install -r <path>.apk
echo '{"bootToHome": true}' | adb -s <pixel9pro> shell run-as com.catmetro.game sh -c 'mkdir -p files/devcap && cat > files/devcap/boot.json'
adb -s <pixel9pro> exec-out screencap -p > frame.png
```
Verify the build independently of the log (licensing noise makes a successful build look failed): `aapt dump badging` for `application-debuggable`, and a strings scan of `global-metadata.dat` for `DEVCAP_BOOT_OVERRIDE`.

## Candidate next contracts (ranked for demo impact)

1. **Level select** — Home's district silhouettes are decorative; nothing reaches L002+ except by winning. Pairs naturally with CM-C11's band.
2. **Back navigation** — `ScreenStack` has no Back consumer; the breadcrumb is recorded but unused.
3. **Art + audio pass** — everything is greybox; TG-1..8 taste gates are still ahead and are human-judged.
4. **The recorded debt rows** in `state/PROJECT_STATE.md` §Known debt, notably: the CI enforcement gaps (the `unity-editmode` job the harness names does not exist, so Unity suites run on exactly one machine; and once the release build path is tracked, CI must assert it never sets `BuildOptions.Development`), the fixture/ladder residuals from #57's review (F4 bad-next-level throw path, F7 D-1 generalization, F8 unpinned ladder values), and the dev-only Results-vs-Home tap collision.
