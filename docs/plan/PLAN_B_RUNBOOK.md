# PLAN B RUNBOOK — Meowmelon pivot (execute only on a RED fun-gate result)

One page, pre-written so the decision never requires peak energy. Source of truth: EXECUTION_PLAN §2.4–2.5;
concept scoring FINAL_REPORT §6 (C4 "Meowmelon", cat merge-drop, score 7.10).

## Trigger — the one fail rule (identical in FINAL_REPORT §20, roadmap D7 GATE row, risk_register R-02)

YELLOW (2 of 4 metrics missed) = 48h mechanic surgery + re-gate D9; RED (3+ of 4, or metric (i) alone) = execute the Plan-B runbook (PLAN_B_RUNBOOK.md).

Gate metrics (pre-registered publicly in BIP post 1, before data exists): (i) ≥6/12 testers open the app
unprompted on a second calendar day during D5–D7, pushes disabled; (ii) ≥4/12 replay an already-**won**
level (`level_started` with attempt>1 on a completed level — excludes fail-retries by construction);
(iii) median session ≥3 levels; (iv) quit-without-retry after failure <50%. A named outside person
confirms the tally before ADR-0007 is written. This runbook executes only after that confirmed tally.

## The pivot, step by step

1. **Same Play app entry, same package.** Meowmelon ships **in the same Play app entry and package
   (`com.catmetro.game`)** — this is the load-bearing condition: it preserves the 12-tester/14-day
   closed-test clock, the account verifications, and every review already in flight. Never create a new
   app entry; a new entry restarts the tester clock and kills the schedule.
2. **Keep every tester opted in.** The closed test continues uninterrupted through the pivot; builds keep
   flowing to the same cohort.
3. **Rename the listing.** New title/description/assets land as a store-listing update on the existing
   entry (identity constants — package, handles, domains — stay frozen per Global Constraints).
4. **Delete the rewind SKUs.** `cm_rewind_5` and `cm_rewind_20` go (no rewind mechanic in a merge-drop);
   the catalog becomes **4 SKUs**: cm_all_access $6.99, cm_supporter_pack $9.99, cm_theme_sakura $2.99,
   cm_theme_neon $2.99. Entitlements unchanged (all_access, supporter, theme_sakura, theme_neon). Prices
   remain immutable without human sign-off.
5. **New public target: Sep 3–8** (latest-viable launch Sep 19 and the Sep 30 11:45pm PDT submission
   deadline are unchanged).
6. **Record it.** ADR-0007 documents the RED tally (with the outside confirmer named) and the pivot
   decision; the same-day BIP post reports it honestly.

## Honest framing (use these numbers, never the old ones)

**~50% of sunk build effort, 100% of accounts/pipeline/SDK integrations, ~0% of content/design
deliverables survive.** Do not claim "80% of the stack" or "ships even faster" — the audit retired both.
What survives: Play/RevenueCat/OneSignal/AdMob/Firebase accounts, repo + CI + build pipeline, the closed-test
cohort and clock, SDK integrations and adapters. What dies: all 30+10 levels, solver + level schema content,
the diorama art direction, the route-switching design docs.

## What this runbook must never do

No new package or app entry; no price/SKU changes beyond the named rewind deletions without human
sign-off; no silent pivot — the BIP cadence and honesty rules (denominators, vintages) apply to the
pivot announcement like any other post.
