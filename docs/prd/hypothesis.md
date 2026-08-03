# Cat Metro — Hypothesis

> Extracted from docs/prd/PRD.md (canonical); edits go there.

## 1. HYPOTHESIS

### 1.1 User
Primary: casual puzzle players 13+ on mid-tier Android who play in 1–5 minute pockets, hold the phone in one hand with sound off, and actively dislike forced ads (specs/product_spec.md:97-104). Secondary: the cat-content audience. Tertiary: transit/systems enthusiasts (specs/product_spec.md:97-102). Design ceiling, verbatim from the spec: *"my commute, one thumb, phone in one hand, sound off by default"* (specs/product_spec.md:104).

### 1.2 Problem
The one-thumb puzzle shelf on Play is dominated by monetization models this audience distrusts (energy/lives, interstitials, loot boxes). The product problem: **is there a routing puzzle that is legible in 6 seconds, replayable within 90 seconds, and honest enough that a player will pay $6.99 once instead of being farmed?**

> **UNVERIFIED / not re-verified in this session:** the "market whitespace" claim is recorded as CONFIRMED by the in-repo audit (AUDIT_FINDINGS.md:20-23) dated 2026-07-31. I did not re-fetch competitor listings today; PRD mode is conversion, not discovery. Any external market claim republished from these files must carry its own source + vintage.

### 1.3 Bet
A pure-C#, fixed-tick, deterministic routing sim (8 ticks/s) with exactly four mechanics at 1.0 — switch, queue capacity, second source, wildcard commuter (EXECUTION_PLAN.md:182-183) — plus a fairness posture that is *mechanically enforced, not merely claimed*: every level solver-proven solvable free (specs/product_spec.md:613-626), zero forced ads (specs/monetization_spec.md:29,33-39), a single scripted paywall exposure per install (specs/monetization_spec.md:127-143), and no offer inside a frustration moment (rewind sheet gated at attempt ≥2 / progress ≥40% / safe tick exists — EXECUTION_PLAN.md:149-151).

Positioning line, verbatim and load-bearing for store listing, paywalls and Devpost: *"Fair by design: no forced ads, no energy, no loot boxes, every level solvable free."* (specs/monetization_spec.md:29).

### 1.4 Evidence summary
- **Independent adversarial audit, 23 agents, 2026-07-31:** 10 agents re-fetched every load-bearing external claim against live primary sources; 7 audited the files mechanically (real ajv JSON-Schema validation, CSV lint, every revenue/economy formula recomputed); 6 attacked self-declared soft spots with computed evidence (AUDIT_FINDINGS.md:4-12).
- **Result: of ~116 externally checkable claims, 113 re-verified CONFIRMED against primary sources** — including every plan-breaking fact (revenue-ranked Grand shortlist, 12-tester/14-day rule, API 36 / Billing 8 deadlines, 15% fee, SDK versions and gating, the AGP9 landmine, the market whitespace). Tally: 113 CONFIRMED / 1 CHANGED / 2 UNVERIFIABLE (AUDIT_FINDINGS.md:20-23, :179).
- **Verdict, verbatim: "AMEND, THEN EXECUTE. Do not rethink the concept; do not ship the package as written."** (AUDIT_FINDINGS.md:18). What did not survive: the schedule as printed, the fun gate as operationalized, and internal consistency (~45 cross-document defects catalogued at AUDIT_FINDINGS.md:297-337).
- **Clean checks worth recording:** product IDs, prices, entitlement IDs, offering composition, placement IDs and wiring, the rewind free-daily rule, and all five ad-cap sets are **identical across every file**; `example_levels.json` passes real ajv draft-2020 validation; all 14 CSVs structurally clean; 45 events exactly as claimed (AUDIT_FINDINGS.md:339-351). The commerce spine is the strongest-evidenced part of this plan.
- **Independent venture critique (2026-08-02, `docs/prd/venture-critique.md`):** nine ranked objections, none of which argues the bet should not be taken; all nine are carried as risks (`docs/prd/risks.md` RK-01…RK-15) and, where they require a human call, as open questions (§5.4). Its own summary: *"Every objection in §1 is about sequencing and tripwires — none of them argues the bet should not be taken."*
- **There are zero real user datapoints yet.** No interviews, no playtests, no telemetry exist for Cat Metro as of 2026-08-02. Every retention/conversion number in the corpus is a self-set target or an external benchmark, and is labeled as such in-source (specs/monetization_spec.md:119,530; data/paywall_experiments.csv:4). **No user feedback is simulated in this document.** The first real user evidence arrives at the D7 fun gate.

### 1.5 Kill criteria (pre-registered, published before data exists)
Pre-registered publicly in BIP post 1/56, **before data exists** (EXECUTION_PLAN.md:136). Metrics verbatim (EXECUTION_PLAN.md:137-141):

1. ≥6/12 testers open the app unprompted on a second calendar day during D5–D7, pushes disabled;
2. ≥4/12 replay an already-**won** level (`level_started` with attempt>1 on a completed level — excludes fail-retries by construction);
3. median session ≥3 levels;
4. quit-without-retry after failure <50%.

**YELLOW** (2 of 4 missed) = 48 h mechanic surgery + re-gate D9. **RED** (3+ of 4, or metric (i) alone) = execute the Plan-B runbook (EXECUTION_PLAN.md:134-136). A named outside person confirms the tally before ADR-0007 is written (EXECUTION_PLAN.md:140-141).

> **Two properties of this gate are now on the record and are human calls, not agent edits (NEW-Q38):** its **power** (exact binomial at the plan's own n=12 / ≥6: pass rates 11.8 / 21.3 / 61.3 / 91.5% at true return rates p = 0.30 / 0.35 / 0.50 / 0.65 — a genuinely 50%-return build fails 39% of the time) and its **contamination** (`DAY1_RUNBOOK.md:53-55` tells every tester in writing to open the app regularly, while metric (i) measures *unprompted* opens; the bias is directional toward passing). Source: `docs/prd/venture-critique.md` V-2, arithmetic reproducible with any binomial calculator; risks RK-06/RK-07. **The gate is pre-registered publicly: any change to it must appear in BIP post 1 before data exists (CM-R56.2), or not at all.**

**Plan B is a runbook, not a vibe:** Meowmelon merge-drop in the **same Play app entry and package** (preserves the tester clock), listing renamed, rewind SKUs deleted (4-SKU catalog), new public target Sep 3–8. Honest framing: ~50% of sunk build effort, 100% of accounts/pipeline/SDK integrations, ~0% of content/design deliverables survive (EXECUTION_PLAN.md:142-145). **There is no suspension branch anywhere** — only a rejection branch (risks RK-13, NEW-Q44b).

**Graduation criterion (separate, process-level, human decision 2026-08-02):** `state/mode` flips to `production` via a human-authored commit **before any monetization code (billing/IAP/ads/payments) merges** (state/PROJECT_STATE.md:10). Those path globs are already risky-path tripwires (AGENTS.md "Risky paths"). This gate binds CM-R23…CM-R37.

---
