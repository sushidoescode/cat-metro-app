# Audit prompt — paste into a fresh Fable 5 session

Run this from `/Users/sushantsrikrish/cat-metro`. Everything below is self-contained.

---

# ROLE

You are an independent adversarial auditor. A previous AI session produced a complete strategy
and execution package for a solo-developed Unity Android game entering **RevenueCat Shipaton
2026**. Your job is to **find what is wrong with it** — not to praise it, summarize it, or
politely extend it.

Assume the previous session was competent but fallible, possibly over-confident, and possibly
anchored on the research package it was asked to audit. Your value is entirely in the errors,
contradictions, unverified claims, and bad judgment calls you surface. A finding of "this is
solid" is only useful if you tried hard to break it first and failed.

# CONTEXT

The user is a solo developer. Today the Shipaton 2026 submission window is open (it opened
Jul 31 2026, 8:00am PDT; it closes Sep 30 2026, 11:45pm PDT). He must ship a brand-new game to
Google Play **inside that window**, integrated with RevenueCat, and submit on Devpost.

He supplied four PDFs of prior research (from GPT-5.6 and other models) recommending a concept
called "Loopline: Cat Metro." The previous session was asked to independently verify that
research, challenge it, and produce a definitive execution plan. It:

- Read all four PDFs and the `Loopline_Technical_Appendix/` directory
- Ran a 10-agent web-research fleet on 31 Jul 2026 to re-verify every load-bearing claim
- **Kept the core concept but renamed it to "Cat Metro"** and made five material changes
- Produced ~34 files of specs, data, and plans in `deliverables/`

# WHAT TO READ (in this order)

**Start here — the conclusions and the locked decisions:**
1. `deliverables/FINAL_REPORT.md` — the 27-section master report (verdict, corrections, fact ledger, concept scoring)
2. `deliverables/DECISIONS_BRIEF.md` — every locked decision; all other files were drafted against this
3. `deliverables/README.md` — index of the package

**The specs (~5,400 lines total):**
4. `deliverables/specs/product_spec.md` — game design, 30-level progression, difficulty model, cut list
5. `deliverables/specs/monetization_spec.md` — three models compared, purchase journey, paywall copy, subscription rejection
6. `deliverables/specs/revenuecat_implementation.md` — version pins, dashboard runbook, state machine, test matrix, code skeletons
7. `deliverables/specs/onesignal_retention.md` — 3-journey architecture under plan constraints
8. `deliverables/specs/liveops_spec.md` — daily seed pipeline, weekly event, 5-week calendar
9. `deliverables/specs/growth_aso_plan.md` — positioning, store listing, content calendar, capture workflow
10. `deliverables/specs/submission_script.md` — Devpost narrative, award positioning, demo storyboard, metrics honesty rules
11. `deliverables/specs/architecture.md` — Unity technical architecture

**The data (14 CSVs, 4 JSON):**
12. `deliverables/data/roadmap_56_days.csv` — day-by-day plan with gates
13. `deliverables/data/monetization_catalog.csv` — 6 live SKUs + 10 cut products
14. `deliverables/data/revenue_scenarios.csv` — 3 budgets × 3 outcomes with formulas
15. `deliverables/data/revenuecat_configuration.csv` — 54-row dashboard runbook
16. `deliverables/data/entitlement_map.json`, `offering_and_placement_map.json`
17. `deliverables/data/analytics_event_taxonomy.csv`, `experiment_backlog.csv`, `paywall_experiments.csv`
18. `deliverables/data/ad_placement_map.csv`, `economy_sources_and_sinks.csv`
19. `deliverables/data/onesignal_journeys.csv`, `notification_copy.csv`
20. `deliverables/data/risk_register.csv`, `google_play_checklist.csv`, `device_test_matrix.csv`
21. `deliverables/data/level_schema.json`, `example_levels.json`
22. `deliverables/data/github_issue_backlog.md` — 49 issues (CM-001…CM-049)
23. `deliverables/agents/agent_system_prompts.md` — the 10-agent build fleet

**The original research being audited (treat as superseded but check the audit was fair):**
24. `Shipaton_2026_Winning_Game_Blueprint.pdf` (82pp — the main prior recommendation)
25. `Shipaton 2026 Game Strategy.pdf` (64pp — the earlier "ChronoRoute" recommendation)
26. `RevenueCat Shipaton 2026 Hackathon and Mobile Game Strategy.pdf`
27. `I'm going to be participating in the this hackatho.pdf`
28. `Loopline_Technical_Appendix/` (schema, validator, prompts, 56-day plan, source matrix)

PDFs: extract with `pdftotext -layout <file> <out>.txt` (pdftotext is installed).

Working notes showing the audit trail: `deliverables/_working_claim_inventory.md` and
`_working_concept_analysis.md` (the latter was deliberately written *before* verification
results returned, to avoid anchoring — check whether that actually worked).

# YOUR TASKS

## Task 1 — Re-verify the load-bearing facts independently

The previous session claims these were verified against primary sources on 31 Jul 2026. **Re-fetch
the primary sources yourself.** Do not trust the summary. Flag anything stale, wrong, or
mis-stated. Note that dates may have moved since — the rules say they are "subject to change at
the sole discretion of Sponsor."

Priority order (if any of these is wrong, the plan breaks):

1. Submission window Jul 31 – Sep 30 2026; judging Oct 1–13; winners Oct 21 (a FAQ reportedly says Oct 22)
2. **Grand Prize shortlist is built from total revenue reported in RevenueCat** — this drove the pricing strategy
3. First public store release must occur inside the window; updates ineligible; app must be reachable from the US
4. Prize table: Grand $100k · #BuildInPublic $30k/$20k/$10k · OneSignal $25k/$15k/$5k · Best Game / HAMM / Catvertising / Design $15k/$10k/$5k · **Samsung non-cash**
5. Verbatim judging criteria for Best Game, HAMM, Catvertising, OneSignal, #BuildInPublic
6. Whether one project can be *awarded* in multiple categories (the session found no cap but couldn't confirm positively)
7. Google Play: **12 testers / 14 continuous days** for new personal accounts; production-access review time; first-release review time
8. Target API 36 required from Aug 31 2026; Billing Library 8 from same date; 16 KB page size mandatory
9. `purchases-unity` current version and its bundled Play Billing version
10. RevenueCat Unity: Paywalls v2 + Customer Center support; **Experiments is Pro/Enterprise plan-gated**; Placements minimum version; Test Store
11. **"RevenueCat Ads" is a public-beta tracking layer over an existing ad SDK, not an ad server**; AdTracker exists in Unity ≥9.1.0; server-verified ad rewards are NOT available in Unity; the AdMob convenience module is not Unity-compatible
12. OneSignal: Unity SDK version; **Growth plan ($19/mo) = 3 active journeys / 6 message steps**; custom events on paid plans only; frequency capping Enterprise-only; **no quiet-hours feature**
13. Unity 6.3 LTS patch levels and the claim that **6000.3.17f1+ ships Gradle 9 / AGP 9.0** and breaks unverified SDKs
14. Google Play fee structure for US/UK/EEA after Jun 30 2026 (claimed 10% service + 5% billing = 15% effective)
15. Play one-time promo codes work for one-time (non-subscription) in-app products — this is the judge-access mechanism
16. Market claim: **no cat-themed metro/route-switching puzzle exists on Google Play**; store data for Mini Metro, Railbound, Arrows – Puzzle Escape, Bus Traffic Fever, Neko Atsume, Cats & Soup
17. Retention benchmarks: the claim that widely-quoted puzzle figures (D1 31.85% / D7 12.18% / D30 5.35%) are 2022-vintage and outdated, and that current medians are ~22% / ~4% / ~0.7%

For each: state CONFIRMED / CHANGED / WRONG / UNVERIFIABLE, with the URL and date you checked.

## Task 2 — Attack the known soft spots

The previous session flagged these as its own weakest points. Attack them hardest.

**A. The Catvertising strategy may be a self-own.** The plan enters a category explicitly about
ad monetization ("clever placements, smart integration with the rest of your revenue stack") with
a game that has **zero interstitials, zero banners, zero app-open ads** — only five opt-in
rewarded surfaces. The thesis is "the cleverest placement is the one you refuse to build." Is
that a genuinely competitive entry, or does it read as an app that barely monetizes with ads
submitting to an ads award? Would a judge reward the restraint or ignore it? Should this
category be dropped in favor of concentrating effort?

**B. Is the Aug 24–28 launch actually achievable?** The critical path is: closed test with 12
testers for 14 continuous days (starting Aug 1–2) → production-access application → review →
first-release review. Verify the real-world variance on those review times. If a solo dev loses
even a few days, does the whole plan collapse? Is the contingency adequate? Note that RevenueCat's
own Codelabs prep guide reportedly suggests starting closed testing as late as Sep 1.

**C. Is the core loop actually fun?** The game is "tap a two-state junction switch to route
color-coded trains." The plan calls sim watchability its biggest product risk and gates it at
Day 7. Is a Day-7 gate with 5 testers a real gate or theater? Is the Plan B (a cat merge-drop /
Suika-like) actually a viable pivot at Day 7, or is it wishful thinking about reusing "80% of
the stack"?

**D. Are the revenue scenarios credible?** Check the arithmetic in `revenue_scenarios.csv` — every
row claims to be reproducible from its own inputs via the `formula_notes` column. Re-derive at
least three rows. Are the assumed conversion rates, eCPM blends, and DAU derivations defensible,
or quietly optimistic? The model concludes $500 of paid spend returns ~$33 incremental revenue
(6.7% ROAS) — is that conclusion right, and if so is the plan's advice consistent with it?

**E. Is the concept decision anchored?** The previous session inherited a recommendation for this
exact concept and kept it. It scored six concepts with weights it chose itself (Cat Metro 8.08,
merge-drop 7.10, ChronoRoute 6.70, daily-deduction 6.60, idle-café 6.19, CineCraft 5.61). Re-run
that judgment independently. Would different-but-defensible weights flip the ranking? Is there a
seventh concept nobody considered that beats all six? Be specific.

**F. Is $6.99 right?** It was raised from the prior plan's $4.99 on the reasoning that the Grand
Prize shortlist is revenue-ranked. Does that reasoning hold, given the same plan admits the
realistic organic base case is ~$254 net revenue — nowhere near Grand Prize contention? Is the
price optimizing for a prize it cannot win, at the cost of conversion in prizes it can?

**G. Unity version pin.** The plan pins 6000.3.16f1 to avoid AGP 9. Verify that this version
genuinely supports 16 KB page sizes and target API 36, and that pinning below current doesn't
create a worse problem than it solves.

**H. Ethics vs. commerce.** The monetization is deliberately generous (no interstitials, no
energy, no loot boxes, no subscription). Is it *too* generous to produce the revenue the Grand
Prize criterion rewards? Where is the line being crossed in either direction?

## Task 3 — Internal consistency audit

The deliverables were written in parallel by many agents. The previous session found and fixed
three cross-document conflicts (offering IDs, package types, ragged CSV rows). **Find the ones it
missed.** Specifically check:

- Product IDs, prices, entitlement names, offering IDs, and placement IDs agree across `monetization_catalog.csv`, `revenuecat_configuration.csv`, `entitlement_map.json`, `offering_and_placement_map.json`, `monetization_spec.md`, `revenuecat_implementation.md`, and `submission_script.md`
- Every event name referenced anywhere exists in `analytics_event_taxonomy.csv`
- Every experiment ID is unique across `experiment_backlog.csv` and `paywall_experiments.csv`
- Dates in `roadmap_56_days.csv` match the gates described in `FINAL_REPORT.md` and `github_issue_backlog.md`
- The OneSignal message-step budget genuinely sums to ≤6 across the 3 journeys
- The ad placements in `ad_placement_map.csv` match those in `monetization_spec.md` and the caps in `onesignal_journeys.csv` don't conflict
- The level counts (30 launch / 40 with bonus district / 60 post-launch) are consistent everywhere
- `example_levels.json` actually validates against `level_schema.json` (run a real JSON Schema validation)
- No file still uses the retired name "Loopline" other than deliberately (rename history, schema lineage, one build-in-public post)

## Task 4 — Completeness against the original brief

The original request specified 14 phases and 27 required output sections, plus a list of ~23
machine-readable appendices. Check `FINAL_REPORT.md` against that structure. What was skipped,
thinned, or quietly dropped? Was anything dropped for good reason, or just missed?

## Task 5 — Does the plan actually make sense?

Step back from the details. Answer plainly:

1. If this developer executes this plan exactly, what is the realistic outcome? Give a probability distribution over: ships nothing / ships late & unpolished / ships on time & wins nothing / wins a category / wins Grand Prize.
2. What is the single highest-leverage change that would improve those odds?
3. What in this plan is *scope theater* — work that looks impressive in a document but won't move any outcome? Name it and say what to cut.
4. Is a solo developer with AI assistance actually capable of this volume of work in 8 weeks? Sanity-check the hour estimates in `roadmap_56_days.csv` against the deliverable list.
5. Is there anything here that would embarrass the developer publicly, violate a platform policy, or jeopardize eligibility?

# RULES

- **Verify, don't trust.** Fetch primary sources. Quote them with URLs and dates.
- **Never invent** market numbers, SDK capabilities, judging rules, benchmarks, or prices. If you cannot verify something, say "unverifiable" — do not fill the gap.
- Distinguish confirmed fact / reasonable inference / estimate / unresolved assumption, and assign High / Medium / Low confidence to each finding.
- Do not soften findings to be agreeable. If the plan is wrong, say so directly and say what to do instead.
- Equally: do not manufacture disagreement to look rigorous. If something is genuinely sound after you attacked it, say that plainly and move on.
- Prefer specific, actionable corrections over general criticism. "The D28 launch is optimistic" is useless; "production-access review averaged N days per <source>, so the Aug 15 application lands Aug 22–29 and the launch window must shift to X" is useful.

# OUTPUT

Write your audit to `deliverables/AUDIT_FINDINGS.md` and summarize it in chat. Structure:

1. **Verdict** — is this plan executable as written? Ship it, amend it, or rethink it?
2. **Critical findings** — anything that breaks the plan or risks eligibility (ranked by severity)
3. **Fact-check results table** — the 17 items from Task 1, with status, source URL, and date
4. **Soft-spot verdicts** — your ruling on each of A–H from Task 2
5. **Internal inconsistencies found** — file, line, what's wrong, what it should be
6. **Completeness gaps** — what the original brief asked for that isn't there
7. **Outcome assessment** — your answers to Task 5
8. **The 10 changes you would make**, in priority order, each with the specific edit
9. **What you could not verify** — and exactly what the developer must check manually
