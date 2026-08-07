# Lessons ledger — every human catch becomes a machine catch

The ratchet: any defect class caught by a **human** (or found the expensive way) gets a row here. At the **second occurrence** of the same class, a permanent check MUST be proposed — a lint rule, hook pattern, CI step, contract-template line, or benchmark round — and the row moves to `enforced` with a link. `/forge-review` appends and increments; `/forge-retro` promotes and verifies that `enforced` entries still actually fire.

Row statuses: `observed` (1 occurrence) → `check-proposed` (2+, proposal open) → `enforced` (check live, linked) → `retired` (class extinct or check removed, with reason).

| Date | Defect class | Caught by | Occurrences | Status | Permanent check (link) |
|---|---|---|---|---|---|
| 2026-07-12 | Doer self-waives "load-bearing" ambiguity and implements a guess | Benchmark trap 02 (blind run) | 1 | enforced | Objective load-bearing test in implementer charter + forge-build; re-run verified |
| 2026-07-12 | Benchmark/eval fixtures leak into product lint/test globs (fail-by-design tests go red in CI) | Project CI (dogfood) | 1 | enforced | forge-init step 4 exclusion rule + benchmark README warning |
| 2026-07-12 | Audit fixtures self-disclose (sealed notes/titles readable by the agent under test) | Audit preparation (human) | 1 | enforced | Blinding rules in benchmark README; sealed notes quarantined to keys/ |
| 2026-07-12 | Config asserted from secondary sources fails the real validator (plugin manifest) | First real install (human) | 1 | enforced | COMPAT field-verified section + kit-ci manifest validation |
| 2026-08-06 | Precondition discipline: a test acts on state it never asserted, or its `precondition:` assert has no red-power (subject and object derived from one shared pure function or literals — cannot fail) | Agent review (#44 F-1; #46 round-1 F1/F3, 3 sites) | 3 | check-proposed | Proposed: CI shape-check over `precondition:`-messaged asserts in unity/Assets/Tests/** — must read SUT state with demonstrated red-power; promotes the D-2 lane law (state/handoffs/CM-UX-07-delta-audit.md) repo-wide (origin: #46 round-1 review) |
| 2026-08-06 | In-slice fix headlined in the PR body ships with no named test — only committed visual evidence protects it | Agent review (#46 round-1 F2) | 1 | observed | — (candidate: contract-template line — every mid-slice fix gets its own failing-first test row) |
| 2026-08-06 | Precondition rationale overclaims sufficiency ("otherwise this test doesn't exercise X" where X is unreachable at that point in the test) | Agent review (#46 round-1 F1) | 1 | observed | — |
| 2026-08-06 | Unregister-on-OnDisable added without matching re-register-on-enable — the phantom-region fix opens its ghost-affordance mirror | Agent review (#46 round-1 F4) | 1 | observed | — |

<!-- Template row:
| YYYY-MM-DD | <class, described so a recurrence is recognizable> | <human review / CI / incident / audit> | N | observed | — |
-->
