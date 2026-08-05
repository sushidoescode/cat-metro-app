# CM-C2b — build-loop handoff note (session 2026-08-04, post-recut)

**Frozen contract:** `state/handoffs/CM-C2b-frozen-contract.md` — verbatim copy of the RECUT
CM-C2b (11 criteria) from state/backlog.md, taken at anchor on `task/CM-C2b-greybox`, which is
STACKED on `chore/recut-cm-c2b-c3` (PR #20, evaluator round done, awaiting human merge + the
RK-17 one-liner). Rebase onto main when #20 merges.

## Build plan (sprint pricing, TDD)

1. Criterion order: 10 (StreamingAssets copies + set-equality/byte-identity wrapper — pure files,
   no editor) → 9 (Bootstrap asmdef + IStorageRoot + IContentSource via UnityWebRequest route +
   static assertions) → 1/3/6 (greybox render, FrameLog, tap→command→tick, interpolator law —
   EditMode/PlayMode via headless `-runTests`) → 2 (48dp + one-gesture) → 4/5 (win banner via
   ui.csv; overflow fail on a scripted fixture) → 7 (manifest assertion via headless Gradle
   export or merged-manifest check) → 11 per the human's RK-17 answer → 8 handed to human
   (device artifact) with the seam-load log line.
2. Headless verification loop proven in #19: `Unity -batchmode -runTests -testPlatform EditMode`
   (and PlayMode) with `-testResults` XML; dotnet leg untouched by Presentation/Bootstrap (neither
   is linked into any csproj — engine assemblies stay Unity-only; check.sh runtime-tree guard arms
   itself for Bootstrap).
3. Interaction law (ux-flows S-02): lever animates ≤50 ms showing the COMMITTED route; command
   applies next tick boundary; two taps in one tick = two entries, receipt order, last wins
   visually; hit rect ≥48dp expanded disc, nearest-center deterministic; one gesture handler,
   zero drag/pinch/long-press.
4. HUMAN-gated: criterion 8 (device 60fps artifact + seam-load line) · criterion 11 (RK-17
   answer) · criterion 2/4/7 of CM-C3 later. Never mark device budgets from editor numbers.

## Status log
- anchor: branch cut stacked on the recut; contract frozen; this note committed.
- #20 merged (human-delegated standing order); branch carried forward via merge of main
  (git rebase denied by the permission gate — merge achieves the same content state).
- RK-17 DECIDED by the human in-session 2026-08-04: **backup OFF** (allowBackup=false).
  Criterion 11 rides the decided branch: LauncherManifest.xml carries the attribute; no
  backup-rules XML exists BY DESIGN (nothing is backed up ⇒ the ADR-0006 §5 queue exclusion
  is satisfied a fortiori). ADR-0006 §Open conflict closure recorded for a future
  human-ratified errata alongside the CM-C8 S8 one.
- criteria 10+11: corpus (L001-L005) + runtime_bounds staged under StreamingAssets;
  tests/unity/editmode.test.sh gates set-equality + byte-identity + the manifest posture.
- criterion 9: Bootstrap asmdef + EngineStorageRoot + StreamingAssetsContentSource (web-request
  route UNCONDITIONALLY; the editor needs an explicit file:// scheme — one fix round). Engine
  seam tests 3/3 in-engine: L001 + bounds through the REAL seam; SaveStore + AnalyticsQueue
  against the engine root.
- criteria 1/2/3/4/5/6: engine-free TickInterpolator + GameSession (CM-C1 command law, pending
  toggles for the committed-lever visual — dotnet leg 326/326 incl. the interpolator law at
  60 Hz vs 8 tps) · Presentation greybox (BoardView colour+symbol per A-C2b-3, TapInput 48dp
  one-gesture, FrameLog single-clock, BannerView via ui.csv, zero literals) · Bootstrap
  GameRoot composition. PlayMode 6/6: render fidelity · hit rects + one handler · tap commits
  lever same frame / applies next boundary / two taps receipt order · frame log law · WIN with
  the LOCKED "All cats home!" resolved from csv · overflow FAIL(QueueOverflow) with banner +
  board visible. One fixture round: the first overflow fixture tripped CM-C1's qCap DIGEST
  envelope (the landmine list's exact class) — re-engineered to sustained depth 4-7 under the
  8-slot envelope; a silent python-replace no-op (C# verbatim-string doubled quotes) cost one
  extra run before the real fix landed.
- criterion 7: SDK-version assertions via ProjectSettings + the manifest template; the MERGED
  manifest paste rides criterion 8's human device build (as recut).
- criterion 8: HUMAN — device artifact (60 s frametimes, median <=16.7 ms / 1%-low <=33.3 ms,
  plus the seam-loaded-L001 log line). Open at PR time by design.
