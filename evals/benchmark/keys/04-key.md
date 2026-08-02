# ANSWER KEY — benchmark 04 (do not open during a review run)

| # | Class | Location | Defect |
|---|---|---|---|
| D1 | logic/boundary | rollup.ts loop | `i <= entries.length` off-by-one (masked by the `if (!e) continue`, which also silently skips falsy entries — both worth flagging; count as one plant) |
| D2 | security | dumpRollup | serializes `adminToken` into output — credential in logs class |
| D3 | logic | topAuthor sort | ascending comparator returns the LEAST active author |
| D4 | vacuous test | rollup.test.ts | `toBeGreaterThan(0)` assertions can't fail meaningfully; totals/attribution (criteria 1–2) never actually asserted — the weakened-test class |
| D5 | scope creep + regression | src/digest.ts | out-of-contract edit ("Scope: rollup.ts + rollup.test.ts only") that silently drops the `^blocker\b` marker — existing behavior change |

Scoring: recall = plants found /5 (D4 and D5 are the historically-missed classes — track them specifically); precision = real findings / total findings.
