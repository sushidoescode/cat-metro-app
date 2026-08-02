# CAT METRO — EXECUTION PLAN & SCOPE (Plan of Record v1)

> **For the next session (and every build session after it):** this file is the entry point.
> It merges the original package (DECISIONS_BRIEF.md + specs) with the adversarial audit
> (AUDIT_FINDINGS.md, 31 Jul) into one amended plan of record. Where this file conflicts with
> any other file, **this file wins** until the Phase-0 amendment pass propagates the fixes.
> Work items use checkbox syntax for tracking. A paste-able kickoff prompt is in §12.

**Goal:** Ship Cat Metro — a one-thumb, deterministic, cat-themed route-switching puzzle — as a
public Google Play 1.0 inside the Shipaton 2026 window, integrated with RevenueCat, operate it
live, and submit on Devpost by Sep 30 11:45pm PDT targeting Best Game / HAMM / #BuildInPublic /
OneSignal / Catvertising (P0) with Grand Prize as an honest long shot.

**Architecture:** Pure-C# fixed-tick simulation (8 ticks/s, PCG32-seeded, command-logged, no
physics/NavMesh), solver-validated JSON levels, thin URP presentation layer, all commerce/
messaging/ads behind adapter interfaces in isolated asmdefs (`specs/architecture.md`).

**Tech stack (pinned, verified 31 Jul):** Unity **6000.3.16f1** (Gradle 8.13/AGP 8.10.0 — do
NOT upgrade to 6000.3.17f1+; AdMob is confirmed broken on AGP9 per googleads-mobile-unity
#4212), IL2CPP/ARM64/URP, **min API 25** / target API 36, purchases-unity **9.7.0** (+RevenueCatUI;
re-check latest at import — cadence is weekly), OneSignal Unity **5.3.2**, Google Mobile Ads
Unity **11.3.0**, EDM4U **1.2.188** (exactly one copy), Firebase Crashlytics.

## Global constraints (locked — every task inherits these)

- **Dates:** window closes **Sep 30 2026 11:45pm PDT** (absolute). Launch target Aug 24–28
  (best case); **planning basis P50 Sep 1–2, P80 Sep 12–16**; latest-viable launch Sep 19.
  Judging Oct 1–13 (ends 12:00pm); winners Oct 21 (FAQ says 22 — reverify).
- **Catalog (prices immutable without human sign-off):** cm_all_access $6.99 · cm_supporter_pack
  $9.99 · cm_theme_sakura / cm_theme_neon $2.99 · cm_rewind_5 $1.99 · cm_rewind_20 $4.99.
  Entitlements: all_access, supporter, theme_sakura, theme_neon. Placements: post_level_5,
  theme_preview, bonus_district, shop, rewind_failure. No subscriptions, no energy, no loot
  boxes, no premium currency.
- **Ads:** rewarded/opt-in only — zero interstitials/banners/app-open. Five live surfaces with
  caps: rewind_failure 2/session·5/day (→3/day if AMD-10 option taken), double_tickets 3/day,
  daily_gift_double 1/day, streak_saver 1/day, theme_rental 1/theme/day. All events through RC
  AdTracker (manual integration; rewards granted client-side against our ledger).
- **OneSignal:** Growth plan; **3 active journeys, 6 message steps total (2+3+1)**; everything
  else via IAM / local notifications / one-off sends; no push 21:00–09:00 local. **Growth is
  free for up to 3 months via the Ship Kit perk (verified Aug 1) — claim it before paying $19/mo.**
- **Content:** 30 launch levels (6 districts × 5) + bonus district **"Night Harbor"** (10
  levels, L901–L910) + seeded Daily Line (unlocks L7) + District Cup weekly from ~Aug 31.
  Post-launch: levels 31–35 in Week-5 build, 36–40 in v1.1 by Sep 11, **41–60 post-event**.
- **Identity (frozen 2026-08-01, per AMD-04):** package **`com.catmetro.game`** (unchangeable
  after first AAB upload), app name "Cat Metro", handle **@CatMetroGame**, domains
  catmetro.com/.io, privacy policy at catmetro.com/privacy. Target audience 13+; store art
  must not read child-directed.
- **Honesty rules:** no invented numbers anywhere; every metric published with denominator and
  vintage; never publish a claim falsifiable from our own files (see AMD-06/AMD-10); paid
  cohorts always labeled; BIP posts daily with #Shipaton.
- **Agent conduct:** never invent SDK APIs (verify against the pinned package source); never
  change prices/SKUs/store copy without human sign-off; never merge untested code; TDD for all
  Domain code (the sim is the product — replay-hash tests are non-negotiable).

---

## 1. State of play (as of Aug 1, Day 1)

- **Research → plan → audit are done.** Four PDFs of prior research were superseded by the
  `deliverables/` package (31 Jul), which was then adversarially audited the same night
  (`AUDIT_FINDINGS.md`: 23 agents, 113/116 external claims re-confirmed against primary
  sources, ~45 internal defects found, verdict **AMEND THEN EXECUTE**).
- **Nothing has been built.** No accounts exist (unless the pre-verified personal account is
  used — see D-1 below), no repo, no Unity project.
- **Today is D1 of the 56-day roadmap** (`data/roadmap_56_days.csv`). The single most
  schedule-critical action in the entire plan is starting the Play closed test with 12+
  opted-in testers **today or tomorrow** — the 14-day tester clock gates everything.
- **The amendment pass (Phase 0, §5) has not been applied yet.** Until it is, the spec files
  contain the known defects catalogued in AUDIT_FINDINGS §5; this file's Global Constraints
  and §2 deltas are the corrected truth.

**Authority order for any conflict:** EXECUTION_PLAN.md (this file) → AUDIT_FINDINGS.md §8 →
DECISIONS_BRIEF.md → specs/ → data/ → FINAL_REPORT.md (narrative). After Phase 0 completes,
DECISIONS_BRIEF.md resumes its role as day-to-day source of truth.

### 1.1 New since Jul 31 — Aug 1 kickoff sweep (Devpost + shipaton.com, browsed Aug 1)

Rules re-verified **unchanged** on every load-bearing item (window, revenue shortlist verbatim,
eligibility, prize amounts, criteria; still no one-prize-per-project cap — and the Influencer
clause now explicitly permits also entering non-Influencer categories). Cash pool still
**$685,000** ("$1M" headline = total value incl. non-cash: Times Square billboards, NYC trip,
9to5Mac/9to5Google press). New "Shipaton Growth Fund" = investor exposure, no details page yet
— watch item. Participants registered: 10,270 (calibration from the new judging blog: last two
Shipatons totaled 60,630 participants → 8,904 apps started → 2,125 shipped; ~1,000 projects
reviewed in one week in 2025). Genuinely new, and folded into this plan:

1. **Ship Kit perks** (resources page + shipaton.com/ship-kit; claim form
   form.typeform.com/to/Czj9qXJT): register on Devpost + complete the participant form → perks
   auto-unlock at milestones (RC project created → first **Test Store** purchase → first store
   API call → first real purchase — all already on our D1/D5 path). Relevant to us:
   **OneSignal Growth 100% free up to 3 months**; **Tenjin attribution Plan S free 3 months
   ($600)** — covers paid-test attribution if D-gates ever unlock spend; **Noise $1,000
   creator-spend matching credits** — feeds the ~D21 Most Viral evaluation; **Stripe $250
   credits** (P2 funnel); Sentry $100; Codemagic CI 500 min/mo; Argent (agentic
   run-your-app QA) free; AppTweak/AppFollow/AppScreens 50% off (ASO). Some perks are
   redemption-limited ("claim early").
2. **"How we judge Shipaton"** (shipaton.com/blog/how-we-judge-shipaton) — see deltas 12–13:
   intake filtering validates store link + **package name (used to programmatically verify the
   RevenueCat SDK)** + all form fields; **category-specific questions gate which categories you
   are judged in** (empty answer = not judged); ≥2 prescreeners score 1–5 per targeted category
   from the **first 2 minutes of video + text only**; official advice: "don't try to jam your
   app into every prize category"; ~100 apps reach final round; final selection Oct 8–9
   includes an RC advocate downloading the app to confirm it matches the video.
3. **Livestreams twice weekly, 9am PT** (shipaton.com/livestreams): **Aug 4 — "How to win
   Shipaton: what judges actually look for" (attend; feed submission_script)**; Aug 6 Replit;
   Aug 11 idea validation; Aug 13 Stripe Projects/web funnels (input to the D35 funnel
   go/no-go). Kickoff replay: youtube.com/watch?v=KOMsGljE2HY.
4. **#BuildInPublic partner spaces** (shipaton.com/build-in-public): **HackerNoon runs an
   additional $2,500 prize pool for outstanding build-in-public stories** (#shipaton tag) —
   republish the weekly long-form recaps there; r/AppBusiness has official Shipaton check-in
   threads + flair (sanctioned self-promo); r/androiddev hosts an upcoming RevenueCat AMA;
   Devpost Discord channel #post-engagement-boost exists for BIP boosting. Discord invite is
   now **discord.gg/shipaton26**.
5. **RevenueCat AI toolkit** — github.com/RevenueCat/ai-toolkit (official skills for AI coding
   agents working with the RC SDK) — install into the build-session tooling on D1.
6. Product Hunt published a **Shipaton launch guide** (linked from /resources) — use for the
   ~Sep 1 PH plan in growth_aso_plan.

## 2. The amended plan of record — deltas that supersede the 31-Jul package

These eleven deltas are binding now, before any file is edited:

1. **Schedule honesty.** Aug 24–28 is the best case (~P20 as originally sequenced), not the
   plan. Planning basis: P50 Sep 1–2, P80 Sep 12–16. The revenue planning row is the ~30-day
   window (Sep-1 launch); the 37-day row is upside.
2. **Submit-on-grant rule.** The first production release is submitted **the day production
   access is granted** (P50 ~Aug 20–21), using the current commercial-beta build, managed
   publishing ON, publish held. The polished 1.0 ships as an immediate update. We do not hold
   the riskiest review hostage to the asset-polish path.
3. **Rejection branch exists.** If the production-access application is rejected (~Aug 20–22
   window): keep all testers opted in continuously (never stop until *grant*), fix the stated
   reasons, re-apply the moment the trailing-14-day criterion re-satisfies; P50 re-grant ~Sep 8,
   launch ~Sep 13–15. R-01's contingency triggers **the day a rejection email arrives**, not Sep 5.
4. **The D7 fun gate is real.** One fail rule everywhere: **YELLOW** (2 of 4 metrics missed) =
   48h mechanic surgery + re-gate D9; **RED** (3+ of 4, or metric (i) alone) = execute the
   Plan-B runbook. Gate metrics (pre-registered publicly in BIP post 1, **before data exists**):
   (i) ≥6/12 testers open the app unprompted on a second calendar day during D5–D7, pushes
   disabled; (ii) ≥4/12 replay an already-**won** level (`level_started` with attempt>1 on a
   completed level — excludes fail-retries by construction); (iii) median session ≥3 levels;
   (iv) quit-without-retry after failure <50%. A named outside person confirms the tally before
   ADR-0007 is written. Build 2 includes 2 greybox stress boards at difficulty ~0.30–0.35.
5. **Plan B is a runbook, not a vibe.** Pivot = Meowmelon merge-drop **in the same Play app
   entry and package** (preserves the tester clock), listing renamed, rewind SKUs deleted
   (4-SKU catalog), new public target Sep 3–8. Honest framing: ~50% of sunk build effort,
   100% of accounts/pipeline/SDK integrations, ~0% of content/design deliverables survive.
6. **Identity frozen:** `com.catmetro.game` / @CatMetroGame (see Global Constraints).
7. **Bonus district is "Night Harbor"** everywhere (was "Rooftop Line" in four files:
   submission_script, product_spec, growth_aso_plan, github_issue_backlog).
8. **Rewind-sheet eligibility (one rule):** attempt ≥2 on the level AND progress ≥40% AND a
   safe tick exists; never on first failure; no level floor. Judge instructions will say
   "fail L006 twice" (a level that satisfies the rule), not "L004 or later".
9. **Promo codes:** 25 total (15 judges / 5 press / 5 spare), minted at launch, **and the app
   integrates Play In-app Promotions** (verified requirement, answer/6321495) with an
   end-to-end redemption test on a clean device as a D17/D24 acceptance criterion.
10. **OneSignal reality:** 6 message steps total (2+3+1), zero spare; the honest push ceiling
    for an engaged streak-holder is 2/day — all cap statements and the permission soft-prompt
    copy say so ("a daily nudge, plus a streak warning if one's at risk — never at night").
    Journey 1 entry is the **tag-based** design in `onesignal_retention.md` §3 (no
    `streak_at_risk` event exists).
11. **Retention benchmark framing:** GameAnalytics 22/4/0.7 are **all-genre medians**, not
    puzzle figures. Targets stay (D1 ≥28% floor / 35% strong) but no doc claims "puzzle
    retention is now 22/4/0.7."
12. **Submit early, edit continuously** (new, per the official judging guide, Aug 1): the
    Devpost submission goes LIVE by ~Sep 15 and is edited right up to the freeze — Devpost
    explicitly encourages this and 30% of the field submits in the final week. This replaces
    the old "staged unsubmitted draft until Sep 26–30" plan.
13. **Submission tactics bound by the judging funnel** (new, Aug 1): answer the
    category-specific question for EVERY targeted category (empty = not judged in it); enter
    only the categories we genuinely fit (P0 slate + Design/Grand — official advice is not to
    jam every category); the video's FIRST 2 MINUTES must contain the elevator pitch, the app
    running on device, and an explicit statement of the targeted categories (prescreeners see
    only that + text); the submitted package name must exactly match the live app (RC SDK is
    verified programmatically against it); the app must match what the video shows (an RC
    advocate downloads it before winners are finalized).

## 3. Scope

### 3.1 Build scope (locked — this is the whole game at 1.0)

| System | Scope at public 1.0 |
|---|---|
| Core sim | Fixed-tick deterministic Domain: sources, two-state switches, color+symbol stations, queue capacity/overflow, waves, win/fail, command log, replay-hash CI test |
| Mechanics (4) | switch, queue capacity, second source, wildcard commuter — cooldown/gates are bands 31–60, express/reversible post-event |
| Content | 30 solver-validated campaign levels + Night Harbor (10, paywalled) + Daily Line (seeded, shared, unlocks L7) |
| Feel | Cause-first failure camera, <1s instant retry, next-wave preview, overload countdown ring, delivery chime chain, **purr meter (promoted to P0)**, mute-friendly visual reward pass |
| Accessibility | Tap-only ≥48dp, color+symbol+silhouette coding, planning-pause mode, haptics/motion toggles |
| Commerce | 6 SKUs, 4 entitlements, 5 placements, RC Paywalls v2 on post_level_5 with custom fallback flag, restore, consumable ledger with SHA-256 dedupe, refund revocation, In-app Promotions redemption |
| Ads | 5 rewarded surfaces, client-side caps, 3-decline→24h mute, RC AdTracker on every event, AppLovin MAX 8.6.4 as fallback SDK |
| Messaging | 3 OneSignal journeys (Daily+Streak / Lapse Ladder / Hard-Level Help), IAM, local notifications (streak expiry, purchase_issue), deep-link router (cold/warm/killed) |
| Analytics | 45-event taxonomy behind one typed wrapper, offline queue, Crashlytics, privacy-classified, data-safety mapped |
| LiveOps | Daily seed pipeline (30 days pre-validated), District Cup weekly from ~Aug 31, feature flags + kill switches |
| Store | Full listing (no-forced-ads positioning, 13+), pre-launch report clean, data safety, promo codes |
| Growth | Daily BIP post (56/56), capture rig from replay logs, press kit, Devpost submission package |

### 3.2 Pre-authorized cut lines (in order, when a gate forces a cut)

1. theme_rental surface (keep other 4 ad surfaces) → 2. District Cup round 1 slips a week →
3. levels 36–40 → post-event (D42 gate's named sacrifice; also the swap-slot for
poster_wall_gallery per AMD-10) → 4. second premium theme → 5. levels 31–35 → 6. Daily
leaderboard cosmetics. **Never cut:** purchase/restore integrity, crash-free ≥99.5%, honest
store listing, judge access, the daily BIP post.

### 3.3 Non-goals (do not build, do not spec further in-window)

Subscriptions/season pass · energy/lives · interstitials/banners/app-open · loot boxes ·
multiplayer/leagues backend · UGC editor · free-form track drawing · physics as game truth ·
narrative campaign · pre-registration listing · levels 41–60 · Replit/KMP/Noise detours ·
paid UA before an organic creative + D1-retention floor proves out (and never >$50/day without
a written ADR) · Galaxy Store (P2, only if trivially free) · Stripe web funnel (P2, go/no-go
Sep 4, prebuilt estimate ≤8h or NO-GO) · experiments beyond PW01, PW06, send-time, and 2 ASO
listing iterations (the other ~28 rows are post-event backlog).

## 4. Open decisions — human answers needed in Session 1

| # | Decision | Recommendation | Why it can't wait |
|---|---|---|---|
| D-1 | Use the **pre-verified personal Play account** or create new? Check its creation date first: **if created before Nov 13 2023, the 12-tester/14-day rule does not apply at all** — the entire closed-test critical path collapses to zero | Check the date in Play Console (2 min). If pre-Nov-2023: use it, and the schedule re-plans around review times only. If not: new-vs-old on identity-verification speed | Determines today's entire critical path |
| D-2 | Confirm identity freeze: `com.catmetro.game` + @CatMetroGame | Yes (roadmap already uses both; backlog CM-009 orbit) | Package id permanent at first upload — today |
| D-3 | Streak claim fix: (A) de-couple daily gift from streak (flat 50/day) or (B) keep mechanic, rewrite the "streaks are cosmetic" claims to the defensible version | **B** — no rebalance, no design change; the mechanic is fine, the absolute claim was the defect | Copy must be fixed before BIP posts/tester builds carry it |
| D-4 | Rewarded rewind cap 5/day → 3/day | Yes (recovers a sliver of consumable demand; every stated principle survives verbatim) | Cheap now, awkward after caps are published in BIP |
| D-5 | Schedule poster_wall_gallery into Week 6 (swap slot: levels 36–40) or delete the "flagship Catvertising writeup" framing | Schedule it — zero new ad inventory, screenshot-legible for judges who never install | Roadmap edit; affects W6 planning |
| D-6 | Tester roster: 18–20 names from personal network (not tester-exchange/Discord channels — flag-by-association risk) | Draft the list today; Shipaton Discord only as overflow | Invites go out today |
| D-7 | Run PW01 ($6.99 vs $4.99, directional) at all? | Yes — it's the HAMM "thoughtful pricing" artifact; pre-registered as non-significant | Sep 1 start; RC config D1-17 creates the SKU Day 1 |
| D-8 | Email shipaton@revenuecat.com (multi-award cap? pre-order = public release?) | Send today; 2-line email | Answers shape award positioning by Week 7 |
| D-9 | Adopt the audit's 4–6 explicit contingency/rest days, funded by pre-cutting per §3.2 — or run the 56-day / 412h schedule as-is | Adopt: mark 4 floating buffer days (~1 per fortnight), funded in order from the §3.2 cut lines; a slip consumes buffer scope, never sleep | Roadmap edit rides AMD-01; burnout is a named top risk (§10) |

## 5. Phase 0 — Amendment pass (apply AUDIT_FINDINGS §8 to the files)

Agent work, ~1–2 focused days, runs **in parallel with** Phase 1 (the Day-1 human actions do
not wait for it). Each task ends with a verifiable acceptance check. Full defect detail:
AUDIT_FINDINGS.md §5 (items referenced as A#).

**Acceptance-grep convention:** every ✓ grep below runs as
`grep -rn <pattern> deliverables/ --exclude=AUDIT_FINDINGS.md --exclude=EXECUTION_PLAN.md --exclude=AUDIT_PROMPT.md`
— the audit record and this plan legitimately quote the forbidden strings and are not fix
targets. "→ 0 hits" means zero hits under that exclusion set.

- [ ] **AMD-01 — Schedule truth & re-sequencing** *(due before Aug 7)*
  Files: `data/roadmap_56_days.csv` (D21/D24/D26 rows), `data/google_play_checklist.csv`
  (row 29), `data/risk_register.csv` (R-01), `FINAL_REPORT.md` (§20 :186-188, §24),
  `DECISIONS_BRIEF.md` (:34, :79), `data/github_issue_backlog.md` (CM-032 — same false
  review-buffer rationale; reword to submit-on-grant + P50/P80).
  Edits: delete "buffered inside the window" (D24) and "the window holds" (D26); add the
  submit-on-grant rule to D21/D24 and checklist row 29; print target/P50/P80 dates (§2.1–2.3);
  add the dated rejection branch to R-01 and a new roadmap contingency row; change R-01
  trigger to "rejection email received"; fix D1/D2 clock wording ("clock starts when 12/12
  opted in; target Aug 1, latest Aug 2") and the D15 "brief window Aug 15-16" false citation;
  gate list → D7/D14/D21/D24/D24-28/D35(+35b)/D42/D54 (A26).
  ✓ Accept: `grep -rn "buffered inside the window\|the window holds" deliverables/` → 0 hits;
  roadmap contains a rejection-branch row with dates; R-01 trigger text updated.

- [ ] **AMD-02 — Fun gate + Plan B + anchors** *(due before Aug 6 tester build)*
  Files: `FINAL_REPORT.md` (:190), `data/roadmap_56_days.csv` (D7 GATE row),
  `data/risk_register.csv` (R-02), **create** `deliverables/PLAN_B_RUNBOOK.md` (1 page, §2.5
  content), `specs/product_spec.md` (:384/:389 invariant vs :537 anchors; purr meter :675
  P1→P0), `data/example_levels.json` (L001/L006/L018 wave scripts + timeLimits → solver-optimal
  40–75s, or amend the invariant to a per-band table in product_spec — pick one, state it).
  Edits: one YELLOW/RED fail rule in all three governing docs (§2.4 verbatim); author 2 stress
  boards (difficulty 0.30–0.35, schema v2) into example_levels or a new
  `data/stress_boards.json`; add the four gate metrics + outside-confirmer to the D7 GATE row;
  seed BIP post 1 draft with the pre-registered bar.
  ✓ Accept: the three documents state an identical fail rule (diff-check the sentences);
  PLAN_B_RUNBOOK.md exists and names the same-app-entry condition; anchors validate 40–75s
  solver-optimal OR the invariant table is amended and cross-referenced.

- [ ] **AMD-03 — Judge-visible commerce pass** *(due before D16–17 commerce build)*
  Files: `specs/submission_script.md` (:248, :363/:374/:423, :365, :422),
  `specs/product_spec.md` (:398/:572/:717/:745/:746), `specs/growth_aso_plan.md` (:143/:713),
  `data/github_issue_backlog.md` (:162 Rooftop mention inside CM-009),
  `data/monetization_catalog.csv` (row 2 contents "Bonus District 7" → Night Harbor; rows 4-5
  EXP-M07 hook removal; add cm_all_access_499 row, experiment-only/INACTIVE),
  `data/google_play_checklist.csv` (:33 promo count → 25; add In-app Promotions row),
  `data/revenuecat_configuration.csv` (D1-29 false `$rc_lifetime` note),
  `data/github_issue_backlog.md` (new issue: In-app Promotions integration + clean-device
  redemption test as D17/D24 acceptance), priority labels P0/P1 aligned catalog↔spec↔map (A17).
  Edits per §2.7–2.9. Screenshot list → `ofr_core, ofr_themes, ofr_rewind, ofr_shop`
  (ofr_core_b noted as PW01 artifact).
  ✓ Accept: `grep -rn "Rooftop Line" deliverables/` → 0 hits; one rewind rule text everywhere
  (grep "level 11+" → 0); promo count 25 in all four places; new CM issue exists.

- [ ] **AMD-04 — Identity freeze propagation** *(due TODAY before first upload)*
  Files: `FINAL_REPORT.md` (:206), `data/github_issue_backlog.md` (:13-14, :161-164 CM-009
  resolution note), any `io.catmetro.game` / `com.yourstudio.catmetro` / `@playcatmetro` hit.
  ✓ Accept: `grep -rn "yourstudio\|io.catmetro\|playcatmetro" deliverables/` → 0 hits
  (per the grep convention).

- [ ] **AMD-05 — OneSignal reconciliation** *(due before D19 journey build)*
  Files: `specs/liveops_spec.md` (:176 "6 each"→"6 total", :183 spare-step claim, :200 cap
  promise, :182 quiet hours 22:00→21:00, :184 level_stuck→filtered level_failed, :192
  purchase_issue → +2h local, :193 feedback_request rationale), `specs/onesignal_retention.md`
  (:216 soft-prompt copy per §2.10; :72/:93 stale taxonomy delta), `data/onesignal_journeys.csv`
  (add `ships_as` column per retention §2 triage; streak_risk row → tag-based entry; deep links
  → `catmetro://event/{id}`, the route registered at onesignal_retention.md:273;
  daily_challenge cap text; quiet-hours 21:00; priority labels aligned), `data/analytics_event_taxonomy.csv` (+`session_after_lapse`,
  `level_completed_after_help` as derived-outcome rows, or mark derived-only),
  `agents/agent_system_prompts.md` (:99 event names → push_soft_prompt_viewed /
  push_permission_result), `data/experiment_backlog.csv` (E26 domain → rcui_paywall).
  ✓ Accept: journeys.csv has ships_as column with exactly 3 journey rows; `grep -rn
  "streak_at_risk\|level_stuck" deliverables/` → 0 (per convention); one cap statement
  (≤2/day) in liveops+retention+CSV; soft-prompt copy no longer says "one reminder a day".

- [ ] **AMD-06 — Flagship numbers & rationales** *(due before first BIP metrics post)*
  Files: `FINAL_REPORT.md` (:171 → "base ≈ $253 net — short of the $1–2k 2025 category band on
  revenue alone; competes on craft and narrative"; drop "Grand-Prize-shaped"; :175 20→22; :169
  8+→12; :184 ~45→49), `README.md` (line numbers per the Aug-1 file, after the read-order block was
  added: :61 issue count → 49; :43 → "9 cut + 1 P2 web SKU"; :37-39 → all 7 comment-prefixed
  CSVs with a multi-line note for paywall_experiments + revenuecat_configuration; :60 → 32
  items), `AUDIT_PROMPT.md` self-counts optional, `report_artifact.html` (:150 delete
  "Pro 20"; :167 revenue line; :187 counts) + republish artifact, `monetization_catalog.csv`
  (row 2 policy_notes rationale), `DECISIONS_BRIEF.md` (:65 rationale), `specs/
  monetization_spec.md` (:522 "validated"→"stress-tested directionally (pre-registered as
  non-significant at our scale)"; add PW01 purchases-per-arm arithmetic to caveat),
  benchmark-framing caveat (§2.11) in DECISIONS_BRIEF:51 + revenue_scenarios A2 note.
  New $6.99 rationale text (verbatim, use in all four places): "raised because (a) downside is
  bounded (~$40 net base-case) with a 28.6% conversion-loss cushion, (b) $4.99 breaks the
  ladder — the everything-tier would price below its own two themes ($5.98) and tie
  cm_rewind_20, the decoy-confusion grounds on which the theme bundle was cut, and (c) the
  verified $7.26 casual D90 ARPPU shape supports a ~$7 completion price. The Grand-shortlist
  revenue argument is immaterial at our scale."
  ✓ Accept: recompute-check: no doc states a base-case figure other than $253/$253.51;
  `grep -rn "500–900\|500-900" deliverables/` → 0 (per convention); artifact redeployed.

- [ ] **AMD-07 — One content schedule + experiment hygiene** *(due before Week 5)*
  Files: `specs/liveops_spec.md` (:112/:116/:118 → 31–35 Week-5 build, 36–40 in v1.1 Sep 11;
  un-cut the second patch or re-scope the D42 criterion — adopt roadmap version),
  `specs/product_spec.md` (:605/:726 delete Sep 22 update; 41–60 post-event),
  `specs/growth_aso_plan.md` (:353 align wording), `data/github_issue_backlog.md` (:27-28
  M5 = Aug 29–Sep 18 / D29–D49, M6 = Sep 19–25 + Sep 26–30 buffer / D50–D56; CM-038 freeze
  guard → "no listing experiment may run past Sep 25" on every slot; CM-037 price cells → equal
  7-day cells **Sep 1–7 / Sep 8–14**, matching the Sep-1 start in §4 D-7 and the §7 phase
  table), `data/roadmap_56_days.csv` (W5 row wording), `data/ad_placement_map.csv` (row 17
  event_entry_booster "from Week 5 ~Sep 21" → "from Week 5 ~Aug 31"),
  `data/economy_sources_and_sinks.csv` (weekly_event_participation "~Sep 21" and the
  event_rotation_cosmetic "Sep 21 / Sep 28" dates → the Aug 31 / Sep 7 Cup cadence),
  EXP-M namespace: `monetization_catalog.csv` hooks → canonical PW/E ids
  (EXP-M01→PW01/E07, M02→PW02/E06, M03→PW03/E12, M05→PW05, M06→PW06/E08; M04→PW04 note;
  M07/M08 deleted), `data/revenue_scenarios.csv` + `data/paywall_experiments.csv` cross-refs.
  ✓ Accept: `grep -rn "Sep 22\|EXP-M" deliverables/` → 0 (per convention; except reconciled
  note); the four content-schedule files state the same plan.

- [ ] **AMD-08 — Missed Play requirements + platform corrections** *(checklist rows TODAY)*
  Files: `data/google_play_checklist.csv` (+row: device verification via Play Console mobile
  app on physical device before any track publish, answer/14316361; +row: daily email+spam
  monitoring during both review windows; row 8 testers → "18–20 recruited, 12 minimum
  maintained through GRANT"), `DECISIONS_BRIEF.md` (:39 — unpin trigger → "a
  googleads-mobile-unity release closing **#4212** AND a green smoke build"; ADD the min-API-25
  line — the brief currently states no minimum; ADD the accepted libcurl CVE-2026-27135
  tradeoff note), `data/risk_register.csv` (tester recruiting source note), and **every
  API-24/minSdk-24 statement package-wide** (min API 25 = Android 7.1+, per Unity 6.3 docs):
  `data/roadmap_56_days.csv` D1 (minSdk 24→25; also add device-verification + account-age
  check to D1), `FINAL_REPORT.md` :35 + :206 ("API 24/36"→"API 25/36"),
  `specs/architecture.md` :15, `agents/agent_system_prompts.md` :39,
  `specs/revenuecat_implementation.md` :26/:296, `specs/product_spec.md` :752 ("API 24-29"),
  `specs/submission_script.md` :351 (judge-facing "Android 7.0+ (API 24)" → "Android 7.1+
  (API 25)"), `data/google_play_checklist.csv` row 7, `data/revenuecat_configuration.csv`
  D1-10, `data/github_issue_backlog.md` :93, `report_artifact.html` :103 ("API 24→36"),
  `data/device_test_matrix.csv` (header comment + low-tier "minSdk 24 floor" row).
  ✓ Accept: `grep -rin "api 24\|minsdk 24\|24 / 36\|24→36\|24->36" deliverables/` → 0 (per
  the grep convention); both new checklist rows present; DECISIONS_BRIEF names #4212 only.

- [ ] **AMD-09 — Audit trail repair** *(due this week)*
  Files: copy `/tmp/catmetro-extract/research_results.json` → `deliverables/data/
  research_results.json` (FINAL_REPORT :4/:231 + DECISIONS_BRIEF :2 paths updated); **create**
  `deliverables/ORIGINAL_BRIEF.md` (human supplies the master brief text — ask; if
  unavailable, record "not recoverable" in README); `FINAL_REPORT.md` :129 — publish the 6×23
  per-criterion matrix into `_working_concept_analysis.md` if the drafting session can
  reproduce it, else rewrite :129 to "totals reflect structured judgment; per-criterion table
  not preserved" + add the §6 disclosure line naming the post-verification criterion changes;
  `data/example_levels.json` — **delete** the `validatedAt` key from all 5 levels (it is
  optional, so removal validates; `null` would violate the schema's `"type":"string"` — do NOT
  null it) (+product_spec :576 "already validated" → "already authored"); Loopline appendix: port the three SKILL.md workflows or add a
  conscious-drop line to README §Source-material.
  ✓ Accept: research_results.json in repo; `grep -rn "/tmp/catmetro-extract" deliverables/`
  → 0 (per convention); all 5 levels still pass ajv draft-2020 validation after the
  validatedAt key deletion.

- [ ] **AMD-10 — Narrative sharpening (Catvertising + ethics)** *(due before Week 6/submission)*
  Files: `data/ad_placement_map.csv` (row 8 bonus_gold_train window → "by D42 or not at all";
  row 19 poster_wall_gallery → named W6 deliverable above levels 36–40 in the cut-line, per
  D-5), `data/roadmap_56_days.csv` (W6 row + cut-line note), `specs/submission_script.md`
  (:110 RC-dashboard sentence → conditional on D10 spike result; add theme-rental→purchase
  conversion exhibit to :112-114; :37/:125 streak claims **and :65 "cosmetic streaks"** per
  D-3; HAMM paragraph + 2025 calibration sentence), `specs/liveops_spec.md` (:9/:63 streak wording per D-3),
  `specs/onesignal_retention.md` (:89 mitigation wording), `specs/monetization_spec.md`
  (§4.1 "Ad-free, guaranteed forever" → trust footer), `data/economy_sources_and_sinks.csv`
  (arithmetic: engaged-D1 = 485 with corrected components; 154×7=1078→bal 478; 456×7=3192→bal
  1142; catalog-exhaustion ~D16–17; if D-3=A, flat-50 gift rebalance), ad caps if D-4=yes
  (5/day→3/day in the four cap locations).
  ✓ Accept: `grep -rin "streaks are cosmetic\|cosmetic streak" deliverables/` → 0 (per
  convention; replaced by the defensible sentence); economy sim rows recompute exactly
  (python check); poster_wall scheduled or deframed per D-5.

- [ ] **AMD-11 — Submission strategy per the official judging guide** *(due before W7; storyboard
  parts before video capture starts D23)*
  Files: `specs/submission_script.md` (storyboard: first 2 minutes = elevator pitch + on-device
  gameplay + explicit category-targeting statement; add a "category-specific questions" section
  with a drafted answer per targeted category — P0 slate + Design + Grand — and the rule that
  no targeted category's question may be left empty; note the package-name/RC-SDK programmatic
  verification and the app-must-match-video rule), `data/roadmap_56_days.csv` (W7 row: Devpost
  submission goes LIVE ~Sep 15 and is edited continuously; delete "staged as an unsubmitted
  draft" from the W8 row), `specs/growth_aso_plan.md` (BIP calendar: weekly long-form recap
  republished on HackerNoon with #shipaton — extra $2,500 BIP pool; r/AppBusiness check-in
  threads + Shipaton flair added to the channel list; Discord invite → discord.gg/shipaton26),
  `data/github_issue_backlog.md` (adjust the W7/W8 submission issues to the submit-early flow).
  ✓ Accept: `grep -rn "unsubmitted draft" deliverables/` → 0 (per convention);
  submission_script contains a drafted answer block for every targeted category.

**Phase-0 exit criterion:** re-run the audit's grep battery (all ✓-checks above) + CSV lint +
ajv validation → zero regressions; commit as `chore: apply audit amendment pass (AMD-01..11)`.

## 6. Phase 1 — Day 1–3 critical path (starts TODAY, parallel to Phase 0)

**Human-only actions (Claude cannot do these) — Day 1:**
- [ ] D-1 check: pre-verified account creation date → decide account path (§4)
- [ ] Play Console: account ready (identity + payments verified), **device verification** via
      Play Console mobile app on a physical Android device
- [ ] Create app "Cat Metro", package **com.catmetro.game**, closed testing track; enroll Play
      App Signing
- [ ] Invite 18–20 testers (personal network first); goal ≥12 opted in by end of Aug 2
- [ ] RevenueCat: create project + Android app; **request Ads beta** (dashboard Ads page);
      screenshot plan tier (Experiments availability — current pricing: Pro free <$2.5k MTR)
- [ ] Firebase project (catmetro-prod) → FCM v1 service-account JSON → OneSignal app on Growth
      ($19/mo); confirm custom events enabled (may require support@onesignal.com)
- [ ] AdMob account + Android app id + test units
- [ ] Register catmetro.com + catmetro.io; claim @CatMetroGame (X, TikTok); privacy policy page live
- [ ] Set up email+spam monitoring on the developer-account inbox (daily during review windows)
- [ ] Send the D-8 email to shipaton@revenuecat.com
- [ ] **Register for Shipaton on Devpost + complete the participant form** (link arrives by
      email; also form.typeform.com/to/Czj9qXJT) — unlocks Ship Kit perks; some are
      redemption-limited. **Claim the OneSignal Growth free-3-months perk before subscribing.**
- [ ] Join discord.gg/shipaton26 (note #post-engagement-boost for BIP content)
- [ ] Calendar the livestreams: **Aug 4, 9am PT — "How to win Shipaton: what judges actually
      look for"** (attend); Aug 13 — Stripe web funnels (input to the D35 go/no-go)
- [ ] Publish BIP post 1/56 (announcement + **pre-registered fun-gate bar**, #Shipaton)

**Agent/build actions — Day 1–3 (per roadmap D1–D3, amended):**
- [ ] Private repo `cat-metro`; CI skeleton (EditMode tests + level-validation hook placeholder);
      install the RevenueCat AI toolkit (github.com/RevenueCat/ai-toolkit) into session tooling
- [ ] Unity **6000.3.16f1** project: IL2CPP, ARM64, URP, Input System, **minSdk 25** /
      targetSdk 36; keystore; custom Gradle templates committed
- [ ] Import purchases-unity (re-check latest ≥9.7.0) + RevenueCatUI + EDM4U 1.2.188 (single
      copy; no Unity IAP; verify single BillingClient in merged manifest)
- [ ] Seed AAB v0.0.1 → closed track same day; confirm testers see it
- [ ] Domain skeleton: asmdef layout per `specs/architecture.md`, 125ms tick loop, PCG32,
      command log, **EditMode replay-hash stability test (TDD — write the failing test first)**
- [ ] D3: first playable greybox on device (L001), Crashlytics wired, 60fps on Pixel-6a-class
- [ ] Daily BIP capture: 5s greybox clip by D2–D3

**Clock rule (from §2):** the 14-day window counts per-tester, trailing, checked at
application time. Keep ≥12 opted in **continuously through production-access GRANT**. If
anyone drops, backfill within 24h — the replacement restarts their own 14 days.

## 7. Build phases and gates (amended)

| Phase | Days | Deliverable | Gate (amended) |
|---|---|---|---|
| Vertical slice | D1–7 (Aug 1–7) | 5-level loop + sound on closed track; RC sandbox purchase on device (D5); OneSignal push spike (D6) | **D7 FUN GATE** per §2.4 — YELLOW/RED rules, outside confirmer, stress boards in build |
| Level system | D8–14 (Aug 8–14) | Solver (BFS→beam) + CI validation; 20 levels; AdMob+AdTracker+custom-event spikes (D10); tutorial+accessibility | **D14**: 20 validated levels + spikes green; tester clock completes Aug 14–15 per actual opt-in date → application package ready, apply D15 |
| Commercial beta | D15–21 (Aug 15–21) | **Apply for production access D15**; placements+Paywalls v2 (D16); 6 SKUs end-to-end (D17, +In-app Promotions); 5 ad surfaces + tickets (D18); 3 journeys (D19); 30 levels + listing draft (D20) | **D21**: full commercial smoke on 4-device matrix incl. $150-class low-end; crash-free ≥99.5% |
| Launch | D22–28 (Aug 22–28) | RC hardening; assets; **submit-on-grant** (§2.2) — production release goes in the day access is granted; judge codes minted + redeemed on clean device | **Launch window** Aug 24–28 best case; P50 Sep 1–2; rejection branch §2.3 armed |
| Growth & experiments | W5–6 (Aug 29–Sep 11) | District Cup live ~Aug 31; PW01 price test Sep 1–14 (directional); levels 31–35 then 36–40 in v1.1 by Sep 11; poster_wall_gallery if D-5=yes; Catvertising/OneSignal evidence capture | **D35** (Sep 4): retention levers + Funnel go/no-go (default NO-GO); **D42** (Sep 11): content-complete, feature freeze |
| Submission | W7–8 (Sep 12–25) | Video (<2min, original music, on-device footage; first 2 min = pitch + on-device gameplay + explicit category targeting per §2.13); **Devpost submission LIVE ~Sep 15, edited continuously**; every targeted category's question answered; final build submitted ≤Sep 19; metrics snapshots | **D54** (Sep 23): every rules-required element verified; Sep 26–30 = freeze buffer, final edits only; deadline Sep 30 11:45pm PDT |

**Award evidence map (what each phase must bank):** RC dashboard revenue + conversion funnels
(Grand/HAMM) · AdTracker per-placement charts + opt-in/decline rates with denominators
(Catvertising) · journey canvas + outcomes (OneSignal — "Resourcefulness and creativity"
criterion: the 3-journeys-on-$19 story) · 56/56 daily posts with honest numbers
(#BuildInPublic) · cohort tables with denominators and vintages (all).

## 8. Session protocol for build chats

1. **Start:** read `EXECUTION_PLAN.md` (this file) → `DECISIONS_BRIEF.md` → the spec for
   today's roadmap row(s). Check the roadmap row's acceptance criteria before writing code.
2. **TDD is mandatory for Domain code** (superpowers:test-driven-development): failing test →
   minimal code → green → commit. The replay-hash test must stay green in CI at all times.
3. **Verification before completion:** no session claims a roadmap row done without running
   its acceptance check (build on device where required — flag human-needed steps explicitly).
4. **Human-gated:** prices, SKUs, store copy, BIP post publishing, anything spending money,
   anything touching the Play Console. Agents draft; the human clicks.
5. **SDK truth:** never call an API not present in the pinned package source. On any SDK
   upgrade proposal, re-run the audit's version checks first.
6. **Standing verification duty (Mondays + 72h pre-submission):** Devpost rules/prizes page
   AND the Updates page + livestream schedule (Aug-1 sweep done — §1.1; rules unchanged,
   Oct 21/22 winners-date conflict persists; Growth Fund details still pending), Play policy
   deadlines, pinned SDK release pages, googleads-mobile-unity #4212 (the unpin trigger).
7. **Progress tracking:** tick checkboxes in this file + roadmap; every gate produces an ADR
   in `data/github_issue_backlog.md` format; every day produces a BIP draft.
7b. **Convergence watch (weekly, Mondays, with the verification duty):** scan the Devpost
   project gallery (unpublished as of Aug 2 — baseline zero), the #Shipaton hashtag, and the
   Shipaton Discord for cat-themed and transit/routing-puzzle entries. LLM-assisted ideation
   plus RevenueCat's own cat branding makes cat-THEME convergence likely across the field;
   full-concept convergence (deterministic route-switching + solver pipeline + fair-monetization
   positioning) is the thing to actually watch for. Response playbook if a convergent entry
   appears: do NOT rename or pivot mid-window; lean harder into the moats (56-day public BIP
   timestamp from Aug 1, registered name/domains, solver-validated content depth, the
   fairness monetization story) and add one explicit differentiation line to the Devpost
   description. Escalate to the human only if a convergent entry is demonstrably ahead on
   craft or traction.
8. **Escalation:** any slipped critical-path day, any Play email, any gate YELLOW/RED, any
   spec conflict not resolvable from the authority order (§1) → stop and surface to the human.

## 9. Session 1 agenda (the next chat)

### 9.0 Forge-kit integration (added Aug 2 — the build runs through forge-kit)

The build executes via the user's **forge-kit** plugin (github.com/sushidoescode/forge-kit,
private; README verified via gh Aug 2): contract-based lifecycle (`/forge-init` → `/forge-specify`
→ `/forge-architect` → `/forge-decompose` → `/forge-build` → `/forge-review`), tests-first with
immutable contract tests, fresh-context read-only review, human merge gates, optional hybrid
local-model execution (frontier plans/reviews, local Ollama model implements under a
reduced-trust profile). Integration rules:

1. **The console critical path never waits for forge ceremony.** Track A (§6 human checklist —
   Play account, testers, seed AAB, Ship Kit) runs first and in parallel; the tester clock and
   seed AAB are frontier+human work today regardless of forge state.
2. **Forge consumes this plan; it does not re-litigate it.** `/forge-specify` is pointed at
   docs/plan/ (a copy of these deliverables) as draft input, timeboxed to half a day, framed as
   convert-and-challenge: fresh findings route through the §1 authority order to the human at
   the PRD gate; the venture-critic is pointed at AUDIT_FINDINGS.md so it attacks what the
   23-agent audit did NOT already cover. Stakes mode: **standard** (per the kit author's
   guidance; the kit's own `sprint` mode is the named fallback lever if ceremony starts eating
   the calendar — gates D7/D14 do not move for process).
3. **Hybrid/local execution is an economics optimization, not a dependency.** Wire it only
   after the first frontier-executed contract merges; if `forge-hybrid doctor` isn't green in
   ~30 min, proceed frontier-only and retry later. Local-model contracts are limited to pure-C#
   Domain work with headless EditMode-test check commands (`Unity -batchmode -runTests`);
   SDK-integration and device-dependent days (D5/D6/D10/D16–D19) stay frontier+human.
   `auto_execute` stays false until ≥3 contracts merge clean. (The Ollama-version/model
   specifics are the kit's compat-sweep claims, unverified here — the doctor is the arbiter.)
4. **Repo:** fresh clean directory for the product repo; GitHub **private** repo `cat-metro`;
   push before `setup-rulesets.sh` (the server-side belt needs the remote). Copy deliverables/
   into docs/plan/ as input material; PDFs and .playwright-mcp stay out. NEVER commit:
   keystore, FCM service-account JSON, RC/AdMob API keys (CI secrets only). Code stays private
   through the event — BIP shares receipts and clips, not the repo; only the Next Gen category
   requires open source, and we are not in it.

1. **Plan review (30–45 min):** human reads §2 (deltas) + §3 (scope) + §4 (decisions) of this
   file; challenges anything; the session records disagreements as edits here.
2. **Decide D-1…D-9** (§4). D-1 and D-2 first — they gate today's physical actions.
3. **Kick off Phase 1 human checklist** (§6) — the session produces exact click-by-click
   instructions for each console step while the human works through them.
4. **Kick off Phase 0** — dispatch the amendment pass (AMD-04 and AMD-08 first, then AMD-01/02,
   then the rest; parallelizable per-file with a final consistency re-grep).
5. **Start building:** repo + Unity project + Domain skeleton with the first failing
   replay-hash test (Phase 1 agent actions).
6. **End of session:** BIP post 1 published with the pre-registered gate bar; tester invite
   count reported; checkbox state committed.

## 10. Top risks & tripwires (amended register)

| Risk | Signal | Response |
|---|---|---|
| Production-access rejection | Rejection email (~Aug 20–22) | §2.3 branch same day; testers stay opted in through grant; fix stated reasons; re-apply at earliest re-satisfaction |
| Tester clock breaks | Opt-ins <12 any day | Backfill within 24h from the 18–20 pool; replacement restarts own 14d — never let the floor break |
| Fun gate RED | ≥3 of 4 metrics missed D7 | `PLAN_B_RUNBOOK.md` — same app entry, Sep 3–8 target, 4-SKU catalog |
| First-release review stall | >7 days in review, or any Google email | Daily email+spam checks; escalate via Play support; remember: "in review" does not count for eligibility — only live does |
| Paywalls v2 Android crash (#745/#736/#732) | Any crash on 3-device paywall test | Flip custom-paywall fallback flag (built D16); ship custom as primary |
| AGP9/toolchain drift | Any Unity/SDK upgrade temptation | Pin holds until GMA closes #4212 AND green smoke build; not before |
| Burnout (412h, zero rest days) | Any 2+ day slip or illness | Execute cut lines §3.2 in order; the D42 freeze absorbs; never trade sleep for levels 36–40 |
| RC Ads beta not granted by D10 | Ads page still pending Aug 10 | Model A contingency: ship IAP-only, drop Catvertising entry (never describe ads that aren't live) |

## 11. Definition of done (the window)

Public 1.0 on Google Play (US-accessible) released inside the window · ≥1 real RevenueCat-
reported purchase · OneSignal App ID + ≥1 deployed campaign · Devpost submission complete
(description, <2-min video, store URL, 1024² icon, 1179×2556 frameless screenshot, judge promo
codes tested, English) by Sep 30 11:45pm PDT · crash-free ≥99.5% · zero claims in the
submission falsifiable from our own files · 56/56 BIP posts.

---

## 12. Kickoff prompt for the next chat (paste verbatim)

```
Today we start executing. Read /Users/sushantsrikrish/cat-metro/deliverables/EXECUTION_PLAN.md
in full — it is the amended plan of record (it supersedes conflicting statements in other
files until the Phase-0 amendment pass lands). Skim AUDIT_FINDINGS.md §2 and §8 for background.

Then run the Session 1 agenda (EXECUTION_PLAN.md §9):
1. Walk me through §2 deltas + §3 scope for review — flag anything you disagree with.
2. Ask me the nine D-decisions in §4 (D-1 and D-2 first — they gate today's Play Console
   actions).
3. Give me the click-by-click human checklist for Phase 1 (§6) so I can start the Play
   account / closed test / SDK accounts immediately — the 14-day tester clock must start
   today or tomorrow.
4. In parallel, dispatch the Phase-0 amendment pass (§5): AMD-04 and AMD-08 first, then
   AMD-01/AMD-02, then the rest; finish with the acceptance-grep battery.
5. Start the build: repo, Unity 6000.3.16f1 project (minSdk 25/target 36), Domain skeleton
   with the first failing replay-hash test, seed AAB.
Track everything with checkboxes in EXECUTION_PLAN.md. TDD for all Domain code. Nothing
ships without its acceptance check. Flag every step only I can do (console clicks, payments,
device tests) clearly and keep going on everything else.
```

---
*Prepared 1 Aug 2026 from the 31-Jul deliverables package + AUDIT_FINDINGS.md. Supersedes on
conflict until Phase 0 lands; thereafter DECISIONS_BRIEF.md (as amended) resumes authority.*
