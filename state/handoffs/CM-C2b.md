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
