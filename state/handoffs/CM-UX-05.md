# CM-UX-05 — Hint chip + attempt counter — slice handoff

**Status:** built, awaiting review. **Branch:** `task/CM-UX-05-hint-attempts` (anchor ca13801,
CM-UX-02 merged). **Contract:** `state/handoffs/CM-UX-05-frozen-contract.md` (frozen at anchor,
first commit on the branch). Wrapper baseline at anchor: N = 12; this slice adds no wrapper.

## What shipped

- `Presentation/Hud/HintAttemptCounter.cs` — pure C#, edge-triggered FailureReview-entry
  counter; Halted/Won-blind; `Reset()` = the per-level attempt-run seam.
- `Presentation/Hud/HintChipView.cs` — TMP/UGUI render-only chip; pure
  `ChipRect(safeArea, dpi)` law (full safe-area width, bottom edge = safe thumb-band top,
  exactly 48dp high at the injected dpi); live Screen reads only in the view, cached on
  safeArea+dpi (R1-L7); UiChromeMaterial binding; A11Y-S01-5 hooks
  (`LiveRegionPoliteness == "polite"`, `AccessibilityLabel` = resolved copy).
- `Presentation/Hud/HintChipController.cs` — `Attach(Func<string>)` (the CM-UX-07 delegate
  shape), polls in Update; visibility law `Count >= 2 AND state ∈ {Playing, FailureReview}`;
  own canvas at sortingOrder 90 (below the chrome canvas 100 — the veil overlays if ever
  co-visible); `ResetForNewLevel()` seam.
- `ui.csv` +1 appended row: `hint.tutorial,Tap the flashing switch` (**DRAFT** — TG-5 voice
  sitting before any device exposure).
- Tests: 18 EditMode + 8 PlayMode, all by direct construction over `GameRoot.LaunchWith`
  (NEEDS-WIRING posture: zero Bootstrap/GameRoot edits).

## Evidence

- RED 6b31971: EM 393 total / 14 failing (over 375 base), PM 60 total / 8 failing (over 52
  base) — every failure right-reason (skeleton zeros / missing csv row). Green-on-arrival by
  design and labeled so: floor negative control, literal-guard pair, live-region hook pin.
- GREEN eb6695d: EM 393/393, PM 60/60, `check.sh` OK. Full `test.sh` run recorded in the PR.
- **Declared existing-test edit (exactly one):** `UiCsvDisciplineTests` row bound 7→8 via its
  own R1-L6 evolution comment; rows 0–6 pins and both merged value asserts untouched.
- **Visual (#33 rule):** uncommitted probe (deleted before commit) rendered Screen-matched
  frames over the URP baseline, committed under `evals/results/ux/cm-ux-05/`:
  `one-entry-no-chip` (board only — hidden by default), `chip-over-cta` (spruce chip
  "Tap the flashing switch" directly above the navy "Try again" band, no overlap),
  `chip-during-play` (chip persists alone after the CTA hides).

## Honesty clauses (standing)

- **No L001 claim:** L001 cannot reach FailureReview (F-DEV-3); the chip is device-reachable
  only on L002/L003 until F-DEV-3 or Q-B resolves.
- Halted edges are never counted and never render the chip (Q-B/NEW-Q4 undecided).
- `tutorial_step.retries` (CM-R13.5 analytics leg) deferred — Application-layer/device lane.
- v1 registers **no** input target: A11Y-S01-4 honored dimensionally; a tappable hint is
  future work behind its own contract.

## For the TG eyeball sitting (§4)

- Chip copy (`hint.tutorial`) is DRAFT; placement is DRAFT.
- Observed in the captures: on the greybox camera framing the chip band overlaps the two
  station squares at the board's bottom edge. Whether the board rect should shrink above the
  chip, or the chip ride elsewhere, is exactly the placement eyeball's call — recorded, not
  silently absorbed.

## Forward obligations (CM-UX-07 inherits)

- Attach line: `hint = root.AddComponent<HintChipController>(); hint.Attach(() => root.ScreenState);`
- Reset line: call `hint.ResetForNewLevel()` wherever a NEW level loads (LoadNext, when it
  exists); the L001-retry loop must NOT reset — accumulation is the mechanic.
- Rebase note (A-UX5-4): sibling append-slices (CM-UX-04/06) each raise the
  `UiCsvDisciplineTests` bound by their own row count on their own branches — whoever merges
  second re-derives the bound; the hint row pin is index-tolerant on purpose.
