# CM-UX-04 — adoption handoff note (UX lane, 2026-08-06)

## Provenance (cross-lane adoption, human-ruled)

Built by the DEVICE session (contract 465789a → red 5a9b0b6 → green 0e527dd, anchored at
ca13801) inside the UX lane's ownership grant. Surfaced; **human ruling 2026-08-06: UX lane
adopts the branch as-is.** Adoption branch `task/CM-UX-04-adopted`, PR #39. The lane's
independently-drafted contract is RETIRED unexecuted — the adopted contract's standalone-panel
design (A-UX4-1) is structurally superior (the controller's merged Won-renders-neither row
stays untouched, so CM-UX-07's controller attach cannot accidentally activate the panel; Q-3's
hold is architecture, not procedure).

## Contract

`state/handoffs/CM-UX-04-frozen-contract.md` (the device session's freeze, first-commit-anchored
— the #36-ruled-sufficient mechanism; verified byte-unchanged across their commits during
adoption triage).

## What the adoption added (gaps in the adopted work, closed here)

1. **The #33 visual evidence was never produced** (their contract cites the rule; their capture
   rig existed, Screen-matched and correctly disarmed — but was never run armed). Adoption ran
   it: `evals/results/ux/cm-ux-04/cm-ux-04-results.png` — a REAL Won board (the merged
   "All cats home!" banner visible: the recorded two-text-stacks interim), the panel's
   full-width `Next` chip in the thumb band, exactly one CTA, structurally-empty footer.
   Session-eyeballed.
2. **This handoff note** (theirs was absent).
3. **The append-append merge resolution** vs main after CM-UX-05 (#38) merged first:
   `hint.tutorial` row 7, `results.next` row 8 (merge order governs); `UiCsvDisciplineTests`
   composes both declared amendments (count 5+4, row-8 pin, honest method rename) with the
   resolution documented in-test. Their key-based `ResultsStringsTests` needed no change.

## Evidence

Resolved adoption tree (main @ c510e57 merged in): **EditMode 730/730 · PlayMode 70/70**
(the EM jump is CM-C9's 329 taxonomy tests — verified unique, zero duplicates). **Final
combined tree over #36's rings (993b96f): EditMode 730/730 · PlayMode 78/78** — the
chip/panel/rings interplay run; the adoption reviewer independently reconciled every count
delta to exactly this slice's tests (F10 correction: the earlier 70/70 was the pre-#36 point).

## Forward obligations (accumulated, restated for CM-UX-07 / LoadNext)

- Panel activation is the LoadNext contract's single line (Q-3); nothing attaches it sooner.
- CM-UX-07: `BoardInputActive` bind · `MotionOffSource` bind + Retry() rebind ·
  `ScreenChromeController` attach · the human-approved halt escape · the batched TG sitting
  (now also: results-panel weight per TG-4, the CM-UX-05 chip copy + placement, the tinted
  teach ring feel, and #39 review F13: the footer container sits ABOVE the primary CTA —
  the sitting must not inherit that inversion unexamined).
- **Lane-wide pattern debts from the #39 adoption review (owners: CM-UX-07 / the TMP
  migration contract):** F4 — every chrome view's `PaintedRectPx` is self-reported (cached
  layout input, never a RectTransform read-back); a `CanvasScaler` would silently divorce
  painted from registered; CM-UX-07 adds world-corners read-back asserts. F5 — the TMP
  renderability proxy (`textInfo.characterCount > 0`) proves parsing, not glyph geometry;
  upgrade to `isVisible`/vertex-count asserts lane-wide. F11 — the CM-UX-02 vocabulary
  guard's fixed file list is three slices behind `Hud/`; a lane decision on enrolment is due.
  F15 — the panel's full-screen scrim does not block board taps until `BoardInputActive`
  binds; the attach and the bind MUST land together (already the standing CM-UX-07 law).
  R2-3 — host DEACTIVATION (vs destruction) leaves the region registered while IsVisible
  honestly reads false; `OnDisable` unregister joins the CM-UX-07 obligations.

## F2 provenance record (R2-5 — state lives in the repo, constitution principle 7)

The #39 review flagged the worktree's modified `.claude/settings.json` (its ask-array emptied,
removing the `gh pr merge`/`git push`/`git checkout`/`git restore`/`gh repo` ask-gates). The UX
lane's session record: the deletion was performed by the HUMAN via the `/permissions` command
in-session on 2026-08-06 (the command output enumerated exactly those five deleted rules); no
agent touched the file, and it appears in no commit (verified across the full PR range by the
round-2 reviewer). **PENDING: the human's own one-line confirmation, which also releases the
#39 merge** — the reviewer correctly held that an agent-relayed report cannot unflag a
review-flagged human judgment. If the human instead wants the ask-gates restored, `/permissions`
is the tool.
