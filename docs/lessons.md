# Lessons ledger — every human catch becomes a machine catch

The ratchet: any defect class caught by a **human** (or found the expensive way) gets a row here. At the **second occurrence** of the same class, a permanent check MUST be proposed — a lint rule, hook pattern, CI step, contract-template line, or benchmark round — and the row moves to `enforced` with a link. `/forge-review` appends and increments; `/forge-retro` promotes and verifies that `enforced` entries still actually fire.

Row statuses: `observed` (1 occurrence) → `check-proposed` (2+, proposal open) → `enforced` (check live, linked) → `retired` (class extinct or check removed, with reason).

| Date | Defect class | Caught by | Occurrences | Status | Permanent check (link) |
|---|---|---|---|---|---|
| 2026-07-12 | Doer self-waives "load-bearing" ambiguity and implements a guess | Benchmark trap 02 (blind run) | 1 | enforced | Objective load-bearing test in implementer charter + forge-build; re-run verified |
| 2026-07-12 | Benchmark/eval fixtures leak into product lint/test globs (fail-by-design tests go red in CI) | Project CI (dogfood) | 1 | enforced | forge-init step 4 exclusion rule + benchmark README warning |
| 2026-07-12 | Audit fixtures self-disclose (sealed notes/titles readable by the agent under test) | Audit preparation (human) | 1 | enforced | Blinding rules in benchmark README; sealed notes quarantined to keys/ |
| 2026-07-12 | Config asserted from secondary sources fails the real validator (plugin manifest) | First real install (human) | 1 | enforced | COMPAT field-verified section + kit-ci manifest validation |

<!-- Template row:
| YYYY-MM-DD | <class, described so a recurrence is recognizable> | <human review / CI / incident / audit> | N | observed | — |
-->
