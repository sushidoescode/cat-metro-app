# Cat Metro — Shipaton 2026 deliverables

Produced 31 Jul 2026. Every external claim was independently re-verified against primary
sources on that date (Official Rules, RevenueCat/OneSignal/Google/Unity docs + GitHub, live
Play listings, registry RDAP). Facts carry their source and vintage; where no credible current
benchmark exists, that is stated rather than filled with an invented number.

> [!IMPORTANT]
> **OneSignal supersession — 27 Aug 2026.** For every Daily reminder, push-permission,
> OneSignal tag, Journey, FCM, APNs, and notification evidence decision, the authoritative product
> and runtime design is
> [`docs/superpowers/specs/2026-08-27-onesignal-daily-reminders-design.md`](../superpowers/specs/2026-08-27-onesignal-daily-reminders-design.md)
> and the authoritative human setup procedure is
> [`docs/runbooks/onesignal-push-setup.md`](../runbooks/onesignal-push-setup.md). They supersede the
> July multi-campaign material and the general read-order rules below for this subject. Any remaining
> July reference to three Journeys, streak/lapse/help campaigns, local notification backups,
> purchase or event pushes, extra OneSignal tags, or exact-minute delivery is historical and is not
> shipped behavior or an instruction.

## Read in this order

0. **`EXECUTION_PLAN.md`** — START HERE if you are executing: the amended plan of record
   (post-audit deltas, scope, Phase-0 amendment pass, Day-1 critical path, session protocol).
   `AUDIT_FINDINGS.md` (31 Jul adversarial audit) is its evidence base. Until the Phase-0
   amendment pass lands, EXECUTION_PLAN.md supersedes conflicting statements in the files below.
1. **`FINAL_REPORT.md`** — the full 27-section report: verdict, corrections to the prior
   research, verified fact ledger, concept decision with scoring, and pointers into everything else.
2. **`DECISIONS_BRIEF.md`** — the locked decisions every other file was drafted against.
   If anything ever conflicts, this file wins. Update it first, then propagate.
3. Then whichever spec you're about to execute.

A shareable web version of the report is published as a private Artifact (link in the session).

## Specs (`specs/`)

| File | What it's for |
|---|---|
| `product_spec.md` | Game design spec: rules, first-session walkthrough, emotion curve, 30-level progression, 100-level framework, difficulty model, juice priorities, accessibility, cut list, vertical-slice acceptance test |
| `monetization_spec.md` | Three models compared + chosen (B), full purchase-journey map, paywall copy, formal subscription rejection, HAMM narrative |
| `revenuecat_implementation.md` | Version pins, dashboard setup runbook, wrapper architecture, purchase/restore state machine, 22-row test matrix, failure matrix, 3 risk-path code skeletons |
| `onesignal_retention.md` | Approved one-Journey Daily Line contract, two-tag boundary, earned permission flow, exact copy/data, and human evidence obligations |
| `liveops_spec.md` | Daily Line pipeline, weekly District Cup, 5-week live-ops calendar, feature flags and kill-switches |
| `growth_aso_plan.md` | Positioning, competitive grid, full store listing, 30-day content calendar, video concepts, outreach templates, capture workflow |
| `submission_script.md` | Devpost narrative, award-by-award positioning, 2-minute demo storyboard + VO script, judge instructions, final-48h checklist |
| `architecture.md` | Unity technical architecture: assemblies, deterministic sim, scene map, lifecycle rules, performance budgets, integration risk zones |

## Data (`data/`)

Machine-readable appendices. All CSVs parse with standard readers; seven files begin with `#`
policy/assumption comment lines — skip them or pass `comment='#'`. Single comment line:
`ad_placement_map.csv`, `device_test_matrix.csv`, `economy_sources_and_sinks.csv`,
`google_play_checklist.csv`, `revenue_scenarios.csv`. Multi-line comment notes:
`revenuecat_configuration.csv` (3 lines) and `paywall_experiments.csv` (6 lines) — naive
"skip the first line" parsing fails on those two.

| File | Contents |
|---|---|
| `monetization_catalog.csv` | 6 live SKUs + 1 experiment-only SKU (`cm_all_access_499`, PW01) + 9 evaluated-and-cut + 1 P2 web SKU, with rationale |
| `revenuecat_configuration.csv` | Ordered dashboard setup: products, entitlements, offerings, placements, paywall, webhooks, Test Store, promo codes |
| `entitlement_map.json` | Entitlements → products → runtime effects → offline/revocation behavior; consumable ledger contract |
| `offering_and_placement_map.json` | Placement → offering → packages → eligibility/caps/suppression → analytics events |
| `paywall_experiments.csv` | 7 monetization experiments (PW01–PW07) with Pro-plan gating fallback |
| `experiment_backlog.csv` | 26 experiments across product/growth/messaging, honest about what is A/B-able at hackathon scale |
| `ad_placement_map.csv` | 5 live rewarded placements + 12 evaluated concepts, scored; no interstitials by design |
| `economy_sources_and_sinks.csv` | Ticket economy with Day 1/7/30 balance simulations for three player profiles |
| `revenue_scenarios.csv` | 3 budgets × 3 outcomes with auditable formulas; scenario ranges, not forecasts |
| `analytics_event_taxonomy.csv` | 45 events with params, user properties, destinations, privacy class, QA procedure |
| `onesignal_journeys.csv` | The single recurring Audience Segment Journey with three local-time branches and one exit/re-entry contract |
| `notification_copy.csv` | The single approved gentle Daily Line message and deep link |
| `level_schema.json` | Strict level schema v2 (adds teaching metadata, accessibility floors, star thresholds) |
| `example_levels.json` | 5 schema-valid levels spanning the launch progression |
| `roadmap_56_days.csv` | Day-by-day plan with gates, acceptance criteria, dependencies, fallbacks, hour estimates |
| `risk_register.csv` | 18 risks with early-warning signals, mitigations, contingencies, owners |
| `device_test_matrix.csv` | Test tiers incl. 16 KB page-size image and foldable |
| `google_play_checklist.csv` | 35 dated compliance items with evidence requirements and source URLs |
| `github_issue_backlog.md` | 6 milestones, 50 issues, plus PR/bug/feature/ADR templates |

## Agents (`agents/`)

`agent_system_prompts.md` — the 10-agent build fleet (Producer, Unity Engineer, Level/Economy
Designer, RevenueCat Engineer, OneSignal Engineer, QA/Release, Art Director, Growth Lead,
adversarial Code Reviewer, Research Auditor) with path fences, merge gates, handoff format,
and escalation rules. Safety properties: agents may not invent SDK APIs, may not change
prices/SKUs/copy without human sign-off, and may not merge untested code.

## Working notes

`_working_claim_inventory.md` (audit targets extracted from the four supplied PDFs) and
`_working_concept_analysis.md` (concept scoring drafted *before* verification returned, to
avoid anchoring on the prior reports' conclusions). Kept for traceability.

## Source material

The original research package remains in the repo root: `Shipaton_2026_Winning_Game_Blueprint.pdf`,
`Shipaton 2026 Game Strategy.pdf`, `RevenueCat Shipaton 2026 Hackathon and Mobile Game Strategy.pdf`,
`I'm going to be participating in the this hackatho.pdf`, and `Loopline_Technical_Appendix/`.
Treat those as superseded wherever they conflict with `DECISIONS_BRIEF.md`. The verification
audit trail (~150 findings with URLs) is committed at `data/research_results.json`.

**Original master brief — not recoverable (audit C9).** The "14 phases / 27 sections /
~23 appendices" master brief ([U01], user-supplied 2026-07-30) exists only as citations: it is
not among the four research PDFs, and exhaustive search of the package and the extract caches
found no copy. `ORIGINAL_BRIEF.md` therefore cannot be created by an agent — open ask
(recorded 2026-08-03): the human author must supply the text. Until it is committed,
completeness against the 14/27/23 structure is unverifiable.

**Consciously dropped from the Loopline appendix** (recorded so the drop is no longer silent):
the three reusable `SKILL.md` agent workflows (launch-content, level-generation, unity-feature)
and the validator's numeric classification thresholds / beam-evaluation weights. The 11-stage
validator pipeline description survives in the specs; the workflow files and constants were not
ported — the Loopline appendix remains authoritative for them until re-derived in-repo.

## Standing verification duty

Re-check the Devpost rules page, the Play policy deadlines, and the pinned SDK release pages
every Monday, and again 72 hours before submission. The rules state dates are "subject to
change at the sole discretion of Sponsor."
