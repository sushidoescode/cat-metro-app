# CAT METRO — PRD (forge-specify, repair round)

**Status:** DRAFT for human ratification · **Drafted:** 2026-08-02 · **Repaired:** 2026-08-02 (testability-lint repair round) · **Author:** product-analyst (agent) · **Reviewers:** human product owner (judgment) + evaluator (testability lint) · **Handoff on approval:** this PRD + §5 open questions + `docs/prd/risks.md` → architect.

**Companion documents:** `docs/prd/risks.md` (risk register) · `docs/prd/venture-critique.md` (advisory, decides nothing) · `docs/prd/ux-flows.md`.

**Authority order applied throughout:** `docs/plan/EXECUTION_PLAN.md` > `docs/plan/AUDIT_FINDINGS.md` §8 > `docs/plan/DECISIONS_BRIEF.md` > `docs/plan/specs/` > `docs/plan/data/` > `docs/plan/FINAL_REPORT.md`. The Phase-0 amendment pass (EXECUTION_PLAN.md:227) has **not** landed; every spec/data citation below is pre-amendment and is superseded wherever EXECUTION_PLAN or AUDIT_FINDINGS §8 says otherwise. Supersessions are marked **[SUPERSEDES]** inline.

**Citation convention:** paths are relative to `/Users/sushantsrikrish/cat-metro-app/`. Source root: `/Users/sushantsrikrish/cat-metro-app/docs/plan/`.

**What the repair round changed (and only this):**
1. Every criterion the testability lint failed was rewritten to be concretely checkable. Where the expected behaviour depends on an **unanswered human decision**, the criterion is authored **per candidate branch** and marked `[PIN NEW-Qn]`; the candidates are enumerated in §4.1. No agent answered a product question.
2. Where the gap was a **measurement protocol** rather than a product decision (denominators, instruments, thresholds for observation, rubrics), the protocol is authored here and recorded as an **analyst-authored assumption (A-21…A-29) the human may overrule** — never as a silent fill.
3. Requirements that still cannot be made testable stay in **§2.13**, each with the named human action. U-2 was split out as deferred requirement **CM-R30-D** so CM-R30 scores clean on its eight concrete criteria.
4. Open questions surfaced by the venture critique and the design-time threat model were **appended** (§5.4, §5.5). Their *recommendations were not adopted* into requirements.

---

## 0. DECISIONS A HUMAN MUST MAKE BEFORE THIS PRD IS EXECUTABLE

*(full text in §5 — this is the index; nothing below is answered by an agent)*

| # | Blocks | One-line |
|---|---|---|
| D-1 | Whole critical path | Pre-verified personal Play account vs new (pre-Nov-13-2023 account voids the 12-tester/14-day gate) |
| D-2 | CM-R50, CM-R23, CM-R31, all package-id work | Confirm identity freeze `com.catmetro.game` + @CatMetroGame — **now also carries the domain-ownership sub-decision** (NEW-Q27) |
| D-3 | CM-R49, CM-R39, store/BIP copy | Streak claim fix A (flat gift) vs B (rewrite claim) |
| D-4 | CM-R35 caps, BIP copy | Rewarded rewind cap 5/day → 3/day |
| D-5 | Week-6 plan, cut-line step 3 | Schedule `poster_wall_gallery` or delete the "flagship Catvertising" framing |
| D-6 | CM-R12.4, D7 fun gate, CM-R44 | Tester roster (18–20 names) |
| D-7 | CM-R33/PW01 scope | Run PW01 ($6.99 vs $4.99) at all |
| D-8 | Award positioning | Email shipaton@revenuecat.com |
| D-9 | Whole schedule | Adopt 4–6 buffer days funded from the cut lines |
| **NEW-Q1** | **CM-R09, CM-R19, CM-R12, CM-R04** | **Anchor levels L001/L006/L018 (20 / 32.5 / 37.5 s) violate the LOCKED 45–90 s loop invariant. Re-author the anchors, or amend the invariant to a per-band table?** (AUDIT_FINDINGS.md:331, :480-481) |
| **NEW-Q2** | CM-R33, CM-R34, CM-R50.5 listing copy | What does `all_access` actually change about ads, given zero non-rewarded surfaces exist? (google_play_checklist.csv:20 vs entitlement_map.json:63-93) — see U-1 |
| **NEW-Q7** | CM-R09, CM-R10.3, CM-R10.6, CM-R49.2 | The single per-level ticket table (three conflicting sources) **and** the enumerated cosmetic-milestone list that CM-R10.3 tests against |
| **NEW-Q37** | CM-R14, CM-R55, every dated criterion | **Ratify the window basis date** (A-01 is a floating calendar), and decide whether to re-baseline to the P80 branch |
| **NEW-Q45** | CM-R34, CM-R50.9, all ad surfaces | **Consent management: certified CMP/UMP flow in the build, or restrict initial availability / ship `ads_enabled=false`?** No requirement, criterion or question covered this before the repair round. Raised independently by the venture critique (V-6) and the threat model (SEC-16) |
| NEW-Q3…NEW-Q48 | see §5.2–§5.5 | Forty-eight unresolved contradictions, gaps and escalations |

---

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

## 2. REQUIREMENTS

**Reading the table:** every requirement carries an ID, a user story, rationale traced to a quoted or line-cited source, numbered independently-testable acceptance criteria, and a MoSCoW priority. `[CI]` marks a criterion that must be enforced by an automated gate that blocks merge. `[PIN NEW-Qn]` marks a criterion whose expected behaviour is authored **per candidate branch** because the choice is an unanswered human decision — the branches are enumerated in §4.1 and exactly one survives ratification. Criteria naming a test file, config file, artifact or job are naming the *contract*, not an existing artifact — **none of these exist yet**.

**Constant convention (introduced by the repair round):** where a criterion asserts a number that the corpus does not pin, the criterion asserts *the constant*, the test reads the value from a single named config file, and the value itself is either a human decision (§4.1) or an architect decision (flagged `[ARCH]`). This makes the criterion checkable today and the value changeable in one place.

### 2.1 Core sim

---

**CM-R01 — Deterministic fixed-tick simulation with replay-hash CI** · **MUST**
*As a player, I get the same outcome from the same inputs on any device, so that the Daily Line is genuinely the same board for everyone and a failure is always my own read.*
Rationale: "Fixed-tick deterministic sim, 8 ticks/s (125 ms), pure C#, PCG32-seeded, command-logged; presentation interpolates; wall clock never read inside the tick" (specs/product_spec.md:204-206); determinism contract "(levelId, seed, commandLog) → identical outcome on every platform; CI asserts replay hash stability" (specs/product_spec.md:229-230); scope row: "command log, replay-hash CI test" (EXECUTION_PLAN.md:182). Underpins the Daily seed pipeline and Cup anti-P2W enforcement (specs/liveops_spec.md:53,85).

1. `[CI]` EditMode test `ReplayHash_Golden`: replaying a committed `(levelId, seed, commandLog)` triple produces a hash byte-for-byte equal to the checked-in golden; a mismatch fails the build (data/device_test_matrix.csv:9 "Replay hash must equal EditMode golden byte-for-byte"; a red smoke run **blocks merge**).
2. `[CI]` The Domain assembly contains zero references to wall-clock/`DateTime`/`Time.*` inside the tick path — enforced by a static-analysis check in `scripts/check.sh` that fails on any match (specs/product_spec.md:206).
3. Device cross-tier check: the same 3 command logs replayed on low, mid and high tiers produce identical hashes **and** identical tick counts (data/device_test_matrix.csv:4,5; specs/product_spec.md:755 "3 logs × 3 devices identical hashes").
4. High-tier check: at 120 Hz the fixed 125 ms sim tick produces the identical tick count and replay hash as the 60 Hz mid tier (data/device_test_matrix.csv:5).
5. `[CI]` PCG32 is the only RNG reachable from Domain code; a test seeds two independent Domain instances with the same seed and asserts identical emission order over 2000 ticks (specs/product_spec.md:204-206).
6. Zero GC allocations per frame while in Playing state, measured after warm-up on mid and low tiers (specs/product_spec.md:761; data/device_test_matrix.csv:3).

---

**CM-R02 — Authoritative per-tick order shared by solver and game** · **MUST**
*As a level author, the validator's verdict is the game's behaviour, so a level that solves in CI cannot be unsolvable on device.*
Rationale: 8-step tick order at specs/product_spec.md:216-227; "solver and game share this exact function" (specs/product_spec.md:216); validator stage 4 "shares exact Domain step function" (specs/product_spec.md:613-626).

1. `[CI]` The solver binary and the runtime both call the same `Domain.Step` symbol — a build-time test fails if the solver project references any duplicate step implementation (specs/product_spec.md:216).
2. Ordered-effect test suite: one test per step boundary (commands → waves → advance → node arrival → station acceptance → overflow → score/combo → win/time) asserting an observable state delta at that boundary only (specs/product_spec.md:217-227).
3. **Station acceptance, case A — single rejected cat (no oncoming traffic):** a non-matching cat occupies exactly one platform slot for exactly 8 ticks; on tick 9 it enters the incoming edge in reverse, arrives at the previous node, and is re-enqueued for routing; the run's score decreases by exactly 25 and the chain counter is set to 0 at the score/combo boundary. All four post-state facts are asserted in one test (specs/product_spec.md:222).
4. **Station acceptance, case B — rejected cat meets an oncoming cat on the same one-way edge.** `[PIN NEW-Q4]` — one of three authored branches, each with the asserted post-state, exactly one of which survives ratification (§4.1):
   - **B-pass-through:** both cats occupy the edge in the same tick; both continue to their respective destinations; neither is destroyed, delayed or re-routed; no fail reason is raised; the replay hash is stable across repeats.
   - **B-blocked:** the reversing cat does not enter the edge while an oncoming cat occupies it; it holds at the station mouth consuming **no** platform slot; on the first tick the edge is clear it enters and completes the reverse traversal of case A; if the hold causes the station's queue to reach capacity, Overload raises per criterion 5 (no new fail reason).
   - **B-collision:** both cats are removed from the board at the collision tick and the run terminates with an **existing** fail reason (`queue_overflow` or `platform_overflow` as authored); the deliveries counter is decremented accordingly so criterion 6 still holds.
   **Invariant asserted under every branch:** the outcome maps onto win or one of exactly three fail reasons — a fourth reason is out of scope and is blocked by CM-R03.1.
5. Overflow test: a full node queue raises Overload with a 16-tick countdown; clearing space before tick 16 cancels it; not clearing fails with `queue_overflow` (specs/product_spec.md:223-225).
6. Bounce-back invariant test: for every shipped level, `win.deliveries` equals total cats emitted (specs/product_spec.md:241).
7. **Wildcard step-boundary test** `[PIN NEW-Q35]`: the `wild` cat's destination resolution is asserted at a named step boundary — **W-player-assigned:** the wildcard holds an unassigned destination until the player toggles a switch that routes it, at which point the destination is fixed at the commands boundary and appears in the command log; **W-auto-accept:** the wildcard is accepted at the first station it reaches whose acceptance check runs, resolved at the station-acceptance boundary with no command-log entry. Both branches assert replay-hash stability and that the solver takes the identical path (criterion 1). See U-9.

---

**CM-R03 — Exactly three fail reasons with player-legible copy** · **MUST**
*As a player, when I lose I am told which of three things happened, in words about my board.*
Rationale: "Exactly 3 fail reasons: queue_overflow … platform_overflow … time_out"; WIN = "All cats home!" (specs/product_spec.md:251-256).

1. Enum test: `FailReason` has exactly three members; adding a fourth fails a contract test (specs/product_spec.md:251).
2. Each reason renders its authored string with the correct node/station substituted, verified by one test per reason (specs/product_spec.md:252-255).
3. `[CI]` `level_failed` fires with `fail_reason` set to one of the three, plus `progress_pct` (data/analytics_event_taxonomy.csv:10); QA forces each enum value.
4. No level in the shipped set can end in a state that is neither win nor one of the three fail reasons (property test over all 40 levels + 30 pre-validated dailies).

---

**CM-R04 — Scoring, chain bonus, time bonus, Perfect Flow, stars** · **MUST**
*As a player, my score is explained by rules I can see, and 3★ is always actually reachable.*
Rationale: delivery +100 / rejection −25 with chain reset; chain bonus 10 × chain length capped at +50; time bonus `+floor(remainingTicks ÷ 2)`; Perfect Flow +50 flat + `economy.perfectBonus` tickets when zero rejections, zero Overloads, `switchesUsed ≤ win.perfectMaxSwitches`; 1★ any win, 2★/3★ authored thresholds validator-checked solver-achievable (specs/product_spec.md:234-239; data/level_schema.json:121-132).

1. **Parameterized scoring table.** One unit test per row; every constant is read from the single file `config/economy_defaults.json`, and a `[CI]` test asserts the file declares all five constants with no other source in the codebase:

   | Term | Rule asserted | Constant |
   |---|---|---|
   | Delivery | score += `DELIVERY_POINTS` (=100) | `DELIVERY_POINTS` |
   | Rejection | score −= `REJECTION_PENALTY` (=25) **and** chain counter := 0 | `REJECTION_PENALTY` |
   | Chain bonus | at chain step *n*, bonus = 10 × min(*n*, `CHAIN_BONUS_CAP_STEPS`) ⇒ +10/+20/+30/+40/+50 for n=1..5 | `CHAIN_BONUS_CAP_STEPS` (=5) |
   | Time bonus | `floor(remainingTicks / 2)` | — |
   | Perfect Flow | +50 score **and** `PERFECT_BONUS_TICKETS` tickets, iff rejections=0 ∧ overloads=0 ∧ switchesUsed ≤ `PERFECT_MAX_SWITCHES` | `PERFECT_BONUS_TICKETS`, `PERFECT_MAX_SWITCHES` |

   `[PIN NEW-Q5]` two branch-authored rows complete the table: **CHAIN-A (count continues, bonus caps):** at n≥6 the chain *counter* keeps incrementing and the bonus stays +50; CM-R18.2's purr/tail-sync state stays active at chain 6+. **CHAIN-B (count caps):** the chain counter saturates at 5; CM-R18.2's state is asserted at exactly chains 3,4,5. The values of `PERFECT_BONUS_TICKETS` and `PERFECT_MAX_SWITCHES` are the same human decision; per-level overrides in `win.perfectMaxSwitches` / `economy.perfectBonus` remain schema-legal and override the global default (asserted by one test each).
2. `[CI]` Validator stage 7 fails any level whose `stars.three` is not reachable by the solver within band slack (specs/product_spec.md:613-626; data/level_schema.json:129-132).
3. `[CI]` Schema check: `stars.two < stars.three`, both ≥1 (data/level_schema.json:129-132).
4. **Worked-example regression, re-derived post-amendment.** For each of the three anchor levels, a golden test asserts the solver-optimal run's total score equals the value recorded in `tests/golden/anchor_scores.json`. That file is generated **only** from the post-NEW-Q1 authored anchor files by a human-approved amendment commit, and a `[CI]` staleness check fails the build if any anchor level's `meta.validatedAt` is newer than the golden file. The pre-amendment worked numbers at specs/product_spec.md:239 (L001 ≥300, L018 900) are **withdrawn as criteria** and retained only as the pre-amendment record. `[PIN NEW-Q1]`

---

**CM-R05 — Atomic, versioned, process-death-safe save** · **MUST**
*As a player, killing the app mid-anything never costs me progress, tickets, rewinds or an entitlement.*
Rationale: "save v1 atomic/versioned" (specs/product_spec.md:712-715); ledger writes are "ONE atomic temp+rename save write" (specs/revenuecat_implementation.md:357-371); `save_migrated(from_version,to_version,success)` with downgrade-protection test (data/analytics_event_taxonomy.csv:46).

**Save-integrity invariant list (SI) — one definition, referenced by criteria 1, 2 and 4** (analyst-authored, A-22):
- **SI-1** the loaded file parses under the shipped save schema;
- **SI-2** `saveVersion` equals the build's current version;
- **SI-3** ticket balance equals the pre-interruption ledger value;
- **SI-4** rewind balance equals the pre-interruption ledger value;
- **SI-5** the dedupe hash set equals the pre-interruption set exactly (no additions, no losses);
- **SI-6** no temp/partial file remains on disk;
- **SI-7** equipped-theme id and entitlement cache are unchanged from the pre-interruption values.

1. `[CI]` Kill-during-write test (EditMode harness): after an interrupted write, the loaded save is either the complete previous version or the complete new version — never a partial file — and satisfies **SI-1…SI-7** against whichever of the two versions loaded.
2. Device test: `adb kill` during save, during purchase, and during an ad each leave a save satisfying **SI-1…SI-7**; run on mid tier before every production upload (data/device_test_matrix.csv:4).
3. `[CI]` Migration test v1→v2 in CI smoke; downgrade attempt is refused and logged (data/device_test_matrix.csv:9; data/analytics_event_taxonomy.csv:46).
4. **Low-storage soak, low tier:** with **≤50 MB free** on the device, run 30 levels with a save after each; after every save the loaded file satisfies **SI-1…SI-7** against the ledger values recorded immediately before that save. Zero crashes, zero ANRs (data/device_test_matrix.csv:3).
5. The soak records peak save-file size in bytes and fails if it exceeds `SAVE_MAX_BYTES` declared in the same config file as the queue bounds `[ARCH]` — the ceiling exists because CM-R27.5's dedupe set is never trimmed (risks RK-20).

---

### 2.2 Mechanics

**CM-R06 — Exactly four mechanics at 1.0, one new mechanic at a time** · **MUST**
*As a player, each level teaches at most one new idea, and 1.0 never surprises me with a fifth system.*
Rationale: "switch, queue capacity, second source, wildcard commuter — cooldown/gates are bands 31–60, express/reversible post-event" (EXECUTION_PLAN.md:183); `newMechanic` "At most one mechanic may appear here that is not in any earlier level's mechanics list; validator enforces" (data/level_schema.json:21).

1. `[CI]` Validator rejects any level in L001–L030 or L901–L910 whose `meta.mechanics` contains a value outside {switch, queue, second-source, wildcard} (data/level_schema.json:20; specs/product_spec.md:572-574).
2. `[CI]` Validator stage: for each level in play order, at most one mechanic not present in any earlier level (data/level_schema.json:21).
3. `[CI]` Schema-gated flags: `edges[].reversible` only valid with the `reversible` mechanic; `waves[].express` only with `express` (data/level_schema.json:53,117) — a level violating this fails schema validation.
4. Mechanic-introduction map matches the authored table: queue at L004, second-source at L018, wildcard at L021 (specs/product_spec.md:285-286,516-525). The wildcard's *runtime semantics* are pinned by NEW-Q35 (CM-R02.7); this criterion asserts only the introduction point.

---

**CM-R07 — Single-verb tap control, ≤50 ms perceived latency, thumb-zone layout** · **MUST**
*As a one-handed player, one tap is the whole vocabulary and it responds instantly.*
Rationale: "Single verb: tap a junction to toggle its switch… No drag/pinch/hold-to-aim/multi-touch" (specs/product_spec.md:187); "Tap enqueues `ToggleSwitchCommand(switchId, tick)` applied at next tick boundary; lever animates immediately, ≤50 ms perceived latency" (specs/product_spec.md:189); thumb-zone rule (specs/product_spec.md:190).

1. Input-surface test: the Game scene registers exactly one gesture handler; a test asserting absence of drag/pinch/long-press-to-aim handlers fails on regression (specs/product_spec.md:187).
2. **Latency, measured not perceived** (analyst-authored instrument, A-21): latency = (timestamp of the first captured frame in which the lever sprite differs from its pre-tap state) − (tap-down timestamp). Both timestamps come from one clock source recorded in the run artifact; the frame timestamp comes from either a ≥240 fps external capture or the instrumented frame log, and the artifact records which. **p95 ≤ 50 ms over 100 taps on the mid-tier reference device**, with the raw per-tap table retained as evidence (specs/product_spec.md:189).
3. Command ordering test: a tap at any sub-tick time applies at the next tick boundary and appears in the command log in receipt order (specs/product_spec.md:189,217).
4. `[CI]` Layout validator: all interactive UI (retry, pause, rewind sheet) is inside the bottom 25% of the safe area; preview strip has no interactive elements; a switch in the top 15% raises a validator warning (specs/product_spec.md:190).
5. **Fat-finger gate, with a denominator** (analyst-authored protocol, A-21): each of 5 testers runs the **same scripted 200-tap sequence** on a 720p reference device (1000 taps pooled). A **mis-tap** is a tap whose coordinates fall outside the hit zone of the junction the script names for that step, as recorded in the command/input log. Gate: **pooled mis-taps / 1000 taps < 3%** (i.e. fewer than 30). Failure escalates to L-shaped hit-zone splitting, never smaller targets, and the escalation decision is recorded with the tap table (specs/product_spec.md:196-198).
6. `[CI]` Validator rejects junction centers <1.2 grid units apart (specs/product_spec.md:198; static-analysis stage, specs/product_spec.md:613-626).

---

**CM-R08 — Rewind: last safe decision tick, one eligibility rule, never on first failure** · **MUST**
*As a player who just lost a long run, I can undo one decision — and I am never sold that undo while I'm frustrated.*
Rationale (LOCKED, one rule): "attempt ≥2 on the level AND progress ≥40% AND a safe tick exists; never on first failure; no level floor" (EXECUTION_PLAN.md:149-151). **[SUPERSEDES]** specs/product_spec.md:261,482 (partial rule), data/monetization_catalog.csv:6 ("level 11+"), data/ad_placement_map.csv:3 (missing the 40%/safe-tick conditions). Rewind restores a snapshot at the tick before the causal routing decision, computed from the deterministic command log (specs/product_spec.md:482); "Every level is solvable without rewinds — this just saves the redo." (specs/monetization_spec.md:467).

1. `[CI]` EditMode test: on attempt 1 the `rewind_failure` placement is **not fetched at all** — zero paywall and zero ad events (data/offering_and_placement_map.json:274; data/entitlement_map.json:392; specs/monetization_spec.md:202).
2. `[CI]` Eligibility truth table test over {attempt 1|≥2} × {progress <40%|≥40%} × {safe tick exists|not}: the chip renders in exactly one cell (attempt ≥2 ∧ progress ≥40% ∧ safe tick) (EXECUTION_PLAN.md:149-151).
3. The sheet opens **only** on chip tap — never auto-presents (specs/monetization_spec.md:189,198).
4. Restore-correctness test: rewinding to the computed safe tick and replaying the remaining log reproduces a deterministic, hash-stable state (specs/product_spec.md:482 + CM-R01).
5. Spend order rendered top-to-bottom: today's free rewind → owned balance → rewarded ad → divider "Need more?" → 5-pack → 20-pack; purchase rows always below free options (specs/monetization_spec.md:192; data/offering_and_placement_map.json:253-261).
6. Footer string present on every render: "Every level is solvable without rewinds — this just saves the redo." (specs/monetization_spec.md:467).
7. `rewind_used` logs `source ∈ {free, purchased, rewarded}` + `balance_after`; ledger decrements exactly once per use (data/analytics_event_taxonomy.csv:12).
8. Free allowance: 1/day for everyone; `all_access` tops up **to** 2/day at local-midnight refill (not +2) (specs/monetization_spec.md:23; data/entitlement_map.json:75-77). `[PIN NEW-Q6]` — the alternative branch (flat +1, i.e. 2/day only for owners who did not spend) is authored in §4.1.

---
### 2.3 Content

**CM-R09 — 30 campaign levels, 6 districts × 5, authored to the difficulty bands** · **MUST**
*As a player, difficulty climbs on a curve someone measured, not vibes.*
Rationale: "30 solver-validated campaign levels" (EXECUTION_PLAN.md:184); district order Whisker Yard → Harbor Line → Market Cross → Twin Platforms → Catnip Gardens → Midnight Terminus, sequential unlock by completing the previous district with any stars (specs/product_spec.md:398); band table with per-level difficulty + first-attempt targets (specs/product_spec.md:516-525,539-570); `difficultyTarget` is computed from six weighted axes, deviation >0.05 fails CI (specs/product_spec.md:499-511).

1. `[CI]` Content test: exactly 30 campaign levels exist, 5 per district × 6 districts, ids matching `^L[0-9]{3}$` (data/level_schema.json:10).
2. `[CI]` For every level, validator-computed difficulty is within ±0.05 of the authored `meta.difficultyTarget` (specs/product_spec.md:499-511).
3. `[CI]` Band assignment matches the authored table for all 30 levels (specs/product_spec.md:539-570).
4. District unlock test: district N+1 is unreachable until district N has 5 completions at any star count; stars gate nothing mechanically (specs/product_spec.md:397-398).
5. **Time-limit conformance** `[PIN NEW-Q1]` — `[CI]`, one branch survives (§4.1):
   - **Q1-A (re-author the anchors):** for every shipped level, `win.timeLimitSeconds` ∈ [45, 90]; the three anchor levels L001/L006/L018 are re-authored to satisfy it and their re-authored files pass all 11 CM-R12 stages; the validator fails any level outside [45, 90].
   - **Q1-B (per-band table replaces the flat invariant):** the band ranges are committed as `data/difficulty_bands.csv` with an explicit `[minSeconds, maxSeconds]` per band; for every shipped level, `win.timeLimitSeconds` falls inside its own band's authored range; the validator fails any level outside its band range. The three anchors ship exactly as authored in `data/example_levels.json` (specs/product_spec.md:537) and their 20 / 32.5 / 37.5 s limits must fall inside their bands' authored ranges.
   Asserted under **both** branches: **every level's time limit falls inside its authored range, and there is exactly one authored range source in the repo.**
6. **Ticket award schedule** `[PIN NEW-Q7]` — `[CI]`: level *N* awards exactly `T(N)` tickets on first clear, where `T` is the single committed table `data/level_ticket_schedule.csv` covering L001–L030. A content test fails any level whose award differs from the table; a `[CI]` grep asserts no other per-level ticket figure is readable by game code. On ratification this table **[SUPERSEDES]** specs/product_spec.md:299 ("+20 tickets"), specs/product_spec.md:399 ("20–50/level") and data/economy_sources_and_sinks.csv:3 (per-district 20/25/30/35/40/50), which currently conflict three ways.

---

**CM-R10 — Night Harbor (Bonus District 7), L901–L910, All Access content** · **MUST**
*As a paying player I get 10 extra remix levels; as a free player I am never blocked by them.*
Rationale: "Night Harbor (10, paywalled)" (EXECUTION_PLAN.md:184); name is binding everywhere, **[SUPERSEDES]** "Rooftop Line" (EXECUTION_PLAN.md:147-148; specs/product_spec.md:398,572); "nothing a free player needs is here — extra content, not gated progression", remix spread 0.30–0.55, launch mechanics only (specs/product_spec.md:572-574); tile visible and honestly labelled "All Access" from first map view, never pulses/badges (specs/monetization_spec.md:227-243).

1. `[CI]` Grep gate: zero occurrences of "Rooftop Line" in any shipped string table, listing asset, or docs/prd file (EXECUTION_PLAN.md:147-148).
2. `[CI]` Ten levels L901–L910 exist, all using only the four launch mechanics, `difficultyTarget` within 0.30–0.55 (specs/product_spec.md:572-574).
3. **Progression test with an enumerated universe** `[PIN NEW-Q7]`: with `all_access` absent, all 30 campaign levels are completable **and** every entry in the committed cosmetic-milestone list `data/cosmetic_milestones.csv` is reachable. The list is the test universe: each row names the milestone, its ticket cost or unlock condition, and its source. A test that iterates the file and finds any unreachable row fails. (The list does not exist in the corpus today — authoring it is part of NEW-Q7.)
4. Map test: the tile renders with the "All Access" label on first map view, with no pulse/badge animation, for a non-owner (specs/monetization_spec.md:227-243). `[PIN NEW-Q30]` — depot-silhouette vs labelled-tile presentation, branches in §4.1.
5. Owner test: with `all_access` active the tile is simply unlocked and no lock/paywall surface fires (data/entitlement_map.json:63-93,91).
6. **Bonus-district faucet parity, stated as an inequality** (analyst-authored comparison base, A-27) `[PIN NEW-Q7]`: (a) each of the ten Night Harbor first clears awards exactly 40 tickets, 400 total (data/economy_sources_and_sinks.csv:11); and (b) **tickets-per-minute of a solver-optimal Night Harbor first-clear run ≤ tickets-per-minute of a solver-optimal run of the highest-yield campaign level**, both computed from `data/level_ticket_schedule.csv` and the solver-optimal completion times, with the two figures printed in the test output. This replaces the previous unfalsifiable intent clause ("deliberately not a faucet advantage over free play").

---

**CM-R11 — Daily Line: one seeded board per calendar date, unlocked at L7** · **MUST**
*As a returning player, today's board is the same board everyone else is playing, with no server.*
Rationale: "Daily Line (seeded, shared, unlocks L7)" (EXECUTION_PLAN.md:184); seed = lower 32 bits of SHA-256("CM-DAILY-1|" + local ISO dateKey + "|" + k) (specs/liveops_spec.md:22-27); local-midnight rollover, UTC explicitly rejected (specs/liveops_spec.md:29-31); generator version constant "CM-DAILY-1" FROZEN through Sep 30 (specs/liveops_spec.md:57).
> **[CONFLICT]** specs/product_spec.md:447 gives seed = SHA-256("CM-DAILY-" + UTC date). specs/liveops_spec.md:22-31 (local dateKey, "CM-DAILY-1|" prefix, UTC rejected) is later and internally consistent with the streak/DST design → adopted here; recorded as NEW-Q8 for human confirmation because both are pre-amendment.

1. `[CI]` Seed-derivation unit test with fixed vectors: three known dateKeys produce three pinned seed values.
2. Two-device same-date test: two devices in different timezones both complete the same dateKey and log identical `seed` in `daily_started`/`daily_completed` — release gate (specs/liveops_spec.md:58; data/analytics_event_taxonomy.csv:14-15).
3. Unlock test: Daily is unreachable before an L007 win; the L007 win fires `daily_unlocked` exactly once (specs/product_spec.md:448; data/analytics_event_taxonomy.csv:13).
4. Scoring test: one scoring play per dateKey; further plays are marked practice and score 0 tickets (specs/product_spec.md:448; data/economy_sources_and_sinks.csv:5).
5. Streak test: increments key on consecutive dateKeys; a timezone or DST change can delay an increment by one day but can never reset a streak (specs/liveops_spec.md:32; data/analytics_event_taxonomy.csv:16 QA "timezone-change + DST edge").
6. Clock-cheat test: if the local date moves backwards or jumps >2 days vs the monotonic estimate, the day still plays but rank display and share-card generation are suppressed — the player is never punished (specs/liveops_spec.md:33).
7. `[CI]` Generator constant "CM-DAILY-1" is asserted unchanged by a contract test through Sep 30 (specs/liveops_spec.md:57).

---

**CM-R12 — 11-stage level validation pipeline as a merge gate** · **MUST**
*As the solo developer, no unsolvable, trivial, brittle or stale level can reach a player.*
Rationale: 11 mandatory stages — schema, static analysis, lower-bound feasibility, solver, triviality reject, brittleness/accessibility, star check, difficulty ±0.05, novelty, staleness, human playtest (specs/product_spec.md:613-626); beam widths 1k/2.5k/5k then a human witness replay is admissible proof (specs/product_spec.md:632).

1. `[CI]` All 11 stages run in CI on every content PR; any stage failure blocks merge (specs/product_spec.md:613-626).
2. `[CI]` Triviality stage: a zero-input run must NOT win on any level, including L001 (specs/product_spec.md:613-626).
3. `[CI]` Brittleness stage: ±1-tick jitter applied to a winning log retains ≥70% win rate; no solution below `minActionWindowTicks`; onboarding levels use 12–16 ticks (specs/product_spec.md:621; data/level_schema.json:23).
4. Human playtest stage: every level playtested on device; capstones by 3 testers (specs/product_spec.md:613-626) — **depends on D-6**.
5. `[CI]` Staleness stage: a level whose `meta.validatedAt` predates the last sim/schema change fails CI (specs/product_spec.md:613-626; data/level_schema.json:25).
6. `[CI]` Static stage: every station reachable from a source able to emit its colors; no orphan switches; junction spacing ≥1.2 units (specs/product_spec.md:613-626).
> **Defect to carry, not to fix silently (AMD-09):** `validatedAt` is typed `"string"` with no null allowed (data/level_schema.json:25), while AUDIT_FINDINGS.md:523 instructs "set `validatedAt` to null until the validator actually runs". Tooling must **delete the key**, never write null, or amend the schema. → NEW-Q9.

---

**CM-R13 — No-text tutorial trio L001–L003** · **MUST**
*As a first-time player, I learn the whole verb without reading anything.*
Rationale: "3 no-text teaching levels L001–L003; no tutorial text, no hand icons, no modals" (specs/product_spec.md:273-274); L001 switch starts routed WRONG (`initialRoute:1`) so the first tap is the lesson (specs/product_spec.md:278; data/example_levels.json:4-17); exit fires `tutorial_completed` on L003 win (specs/product_spec.md:282; data/analytics_event_taxonomy.csv:6).

1. String-table test: L001–L003 render zero tutorial text, zero hand icons, zero modals (specs/product_spec.md:273-274).
2. `[CI]` Content test: L001 has `initialRoute:1` on its single switch and `minActionWindowTicks:16` (data/example_levels.json:4-17).
3. `tutorial_completed` fires exactly once, on an L003 win, and sets `tutorial_done` (data/analytics_event_taxonomy.csv:6).
4. Fresh-tester run: 5 fresh testers finish L003 with zero verbal help, ≥4/5 within 3:00 (specs/product_spec.md:758) — supplementary check, **not** the D7 gate (see §4 A-06).
5. Hint fallback: after 2 fails on any tutorial level, one 1-line hint chip from the string table appears; `tutorial_step.retries` records it (specs/product_spec.md:289; data/analytics_event_taxonomy.csv:5).
6. Target (measured, not gated at D14): ≥90% of installs complete L003; total tutorial ≤2:15 (specs/product_spec.md:282-283) — self-set target, denominator = installs.

---

**CM-R14 — Post-launch content plan: 31–35 Week 5, 36–40 in v1.1 by Sep 11, 41–60 post-event** · **SHOULD**
Rationale: the single adopted plan (AUDIT_FINDINGS.md:506-510) **[SUPERSEDES]** four incompatible schedules (AUDIT_FINDINGS.md:307), including specs/product_spec.md:589-593,605 and specs/liveops_spec.md:112. Levels 41–60 in-window is a binding NON-GOAL (EXECUTION_PLAN.md:207).

1. **Levels 31–35 introduce the cooldown mechanic and pass all 11 CM-R12 stages by window-basis day 34** (the Week-5 build tag). "Window-basis day *n*" = the ratified basis date + (*n*−1) days; on ratification of NEW-Q37 the derived calendar date is written into the ops log, after which this is an ordinary dated check. Under the unratified Aug-1 basis, day 34 = 2026-09-03. `[PIN NEW-Q37]` (specs/liveops_spec.md:112,158).
2. **Either** levels 36–40 are present in the tagged v1.1 build by **2026-09-11**, **or** a dated cut-line-step-3 decision signed by the human exists in the ops log by 2026-09-11. Both branches are observable artifacts (a build tag, or a signed dated log entry); the criterion fails if neither exists on 2026-09-12 (EXECUTION_PLAN.md:198-199).
3. `[CI]` No level id in 041–060 exists in any in-window build (EXECUTION_PLAN.md:207).

---

### 2.4 Feel

**CM-R15 — Cause-first failure camera** · **MUST**
*As a player who just failed, the game shows me the decision that did it before it shows me anything else.*
Rationale: "cause-first failure camera pans to causing node, ghost-replays final 3 s at 60% speed with causal cat highlighted, one chip e.g. 'This blue cat needed the switch flipped here'" (specs/product_spec.md:258-261); attribution walks the sim log backward to the last routing decision affecting the causal cat; ships in the vertical slice; when ambiguous, the camera shows the node without blame text (specs/product_spec.md:265,338).

**Ambiguity, defined mechanically** (analyst-authored predicate, A-23): attribution is **ambiguous** for a given failure iff, within the 24 ticks (3 s) preceding the failure tick, there exist **≥2 distinct candidate causal routing decisions** such that replaying the command log with that single decision removed averts the failure **independently** for each. Otherwise attribution is **unambiguous** and the single averting decision is the causal node.

1. `[CI]` Deterministic unit-test suite: one test per `fail_reason` × board archetype asserting the identified causal node id (specs/product_spec.md:265,338).
2. Scripted-scenario gate: correct cause in 20/20 scripted scenarios on device (specs/product_spec.md:759-760).
3. **Ambiguity behaviour, over enumerated fixtures:** three scripted **ambiguous** fixtures — `AMB-01` symmetric twin-junction board, `AMB-02` two-source convergence, `AMB-03` queue-capacity double-cause — each satisfy the predicate above (asserted by the removal test itself) and each render: camera framed on the node, **zero** blame chips, zero blame text. Three scripted **unambiguous** fixtures each render exactly one blame chip naming the single averting decision's node. Six fixtures, six asserted branches (specs/product_spec.md:338).
4. Ghost replay runs the final 3 s at 60% speed with the causal cat highlighted (specs/product_spec.md:258-261).
5. CI smoke includes a forced-overflow fail and asserts the cause-first sheet appears (data/device_test_matrix.csv:9).

---

**CM-R16 — Instant retry under 1 second, live during the ghost replay** · **MUST**
Rationale: "instant retry <1 s" (specs/product_spec.md:258); "Retry button live DURING ghost replay; replay skippable by same tap" (specs/product_spec.md:267); retry restores switch states to tick-0 `initialRoute`, not mid-run states (specs/product_spec.md:391).

1. Measured tap-to-playing <1.0 s on mid tier (specs/product_spec.md:759) and <1.0 s on low tier (data/device_test_matrix.csv:3).
2. Test: the retry control is hit-testable from frame 1 of the ghost replay; the same tap skips the replay (specs/product_spec.md:267).
3. State test: after retry, every switch equals its level `initialRoute` (specs/product_spec.md:391; data/level_schema.json:80-91).
4. CI smoke asserts instant retry after a forced fail (data/device_test_matrix.csv:9).

---

**CM-R17 — Next-wave preview and overload countdown ring** · **MUST**
Rationale: preview strip shows the first 2 waves in the Read phase, display-only at top (specs/product_spec.md:375-382,190); overload = 16-tick (2 s) countdown ring + riser (specs/product_spec.md:224,671); L006's teaching goal is preview-strip reading (data/example_levels.json:20).

1. Preview test: at tick 0 the strip displays the next two waves' colors/counts and contains zero interactive elements (specs/product_spec.md:190,377).
2. Ring test: entering Overload renders the ring with exactly 16 ticks of countdown; clearing space cancels it and the ring disappears (specs/product_spec.md:223-224).
3. **Mute legibility, as named visual asserts** (analyst-authored, A-24): with system audio disabled, (a) the overload countdown ring is present with alpha > 0 in every tick of the 16-tick countdown; (b) the audio riser is substituted by a ring pulse rendered at ≥1 pulse per 4 ticks; (c) the preview strip renders wave colors and counts unchanged. The full mute audit is **CM-R18.3** (the draft's reference to "CM-R18.4" was wrong).
4. **L003 scripted near-overload:** the ring fires **exactly once** during the scripted run and remains cancellable for **≥12 of its 16 ticks** from any single tap on either junction — asserted by replaying the scripted log with a single tap injected at each of ticks 1…16 and counting the ticks at which the run still averts the failure (specs/product_spec.md:280).

---

**CM-R18 — Delivery chime chain, purr meter (P0), mute-friendly visual reward pass** · **MUST**
Rationale: purr meter is **promoted to P0** (EXECUTION_PLAN.md:185) **[SUPERSEDES]** specs/product_spec.md:675 (P1). P0 juice set: switch clack + haptic tick, delivery chime (pentatonic ascending per chain step), overload ring + riser, fail thud + cause-camera pan, win rollup, mute-friendly design pass — all critical info visual (specs/product_spec.md:669-674). The chain meter IS the purr meter (specs/product_spec.md:236). "Game must pass D7 fun gate with P0 items only" (specs/product_spec.md:690).

1. Chime test: chain steps 1..5 play ascending pentatonic steps; a rejection resets the chain and the audio step (specs/product_spec.md:234-236,670).
2. Purr-meter test: at chain ≥3 the board hum and tail-sync visual state both activate; the visual state activates identically with audio muted. The chain values at which the state is asserted follow CM-R04.1's branch: **CHAIN-A** asserts 3,4,5 and 6+; **CHAIN-B** asserts 3,4,5 only. `[PIN NEW-Q5]`
3. **Mute audit, as an automated UI test over an inline checklist** — the checklist is the criterion, not a document that does not exist. With system audio off, a scripted playthrough asserts:
   - **MUTE-01** the chain meter renders a distinct, named visual state at each of chains 1, 2, 3, 4, 5 (five distinct state ids, asserted individually);
   - **MUTE-02** the overload countdown ring is visible for the full 16-tick countdown (CM-R17.3a);
   - **MUTE-03** on failure, either exactly one blame chip is rendered or the camera frames the causal node with zero chips, per the CM-R15.3 branch for that fixture;
   - **MUTE-04** the win rollup renders score, best chain, time bonus, Perfect-Flow state and star count, all five present and non-empty;
   - **MUTE-05** the purr/tail-sync visual state is active at chain ≥3 (CM-R18.2).
   The automated test over MUTE-01…MUTE-05 **is the gate**. A reviewer pass over the same five rows is a recorded second check and is explicitly **not** the gate (specs/product_spec.md:674).
4. D7 gate rehearsal: the build entering the fun gate contains only P0 juice items (specs/product_spec.md:690).

---

**CM-R19 — Core-loop invariants: one primary CTA, no offers in phases 1–4, authored loop duration** · **MUST**
Rationale: "Loop invariants: duration 45–90 s; retry <1 s; rewarded offers only inside the fail path per eligibility, never in Phases 1–4; monetization surface confined to Phase-5 fail path + post-win results footer" (specs/product_spec.md:384-387); results screen has exactly one primary CTA (Next) (specs/product_spec.md:382); validator audits solver-optimal time to land 40–75 s (specs/product_spec.md:389).

1. `[CI]` **Solver-optimal time conformance** `[PIN NEW-Q1]` — one branch survives (§4.1):
   - **Q1-A:** for every shipped level, solver-optimal completion time ∈ [40, 75] s; the validator fails any level outside it. The three anchors are re-authored to satisfy it (CM-R09.5 Q1-A).
   - **Q1-B:** for every shipped level, solver-optimal completion time falls inside its band's authored `[minSolverSeconds, maxSolverSeconds]` in `data/difficulty_bands.csv`; the validator fails any level outside its band range.
   Asserted under both branches: **exactly one authored range source exists, and no shipped level sits outside its own authored range.**
2. `[CI]` Offer-placement test: no commerce or ad surface can be constructed while the sim is in phases Read/First-route/Rhythm/Crunch (specs/product_spec.md:384-387).
3. Results-screen test: exactly one primary CTA is rendered (specs/product_spec.md:382).
   *(The draft's criterion 4 — "BLOCKED on NEW-Q1, criterion 1 currently fails for the three anchor levels" — is deleted; the conflict is now carried by the branch authoring in criterion 1 and by NEW-Q1 itself.)*

---
### 2.5 Accessibility

**CM-R20 — Tap targets ≥48dp everywhere, verified on device and in the pre-launch report** · **MUST**
Rationale: "Tap-only ≥48dp" (EXECUTION_PLAN.md:186); hit targets expanded beyond the visual disc; simultaneous-tap disambiguation picks nearest center (specs/product_spec.md:188); "accessibility tap-target findings must be fixed, not dismissed — locked bar ≥48dp" (data/google_play_checklist.csv:28).

1. `[CI]` UI test enumerating every interactive element asserts ≥48dp on the 720p reference (specs/product_spec.md:188).
2. Tablet tier: every interactive element ≥48dp with board+HUD visible and zero scroll (data/device_test_matrix.csv:7).
3. Pre-launch report shows zero unresolved tap-target findings before production submit (data/google_play_checklist.csv:28).
4. Disambiguation test: two overlapping hit zones resolve to the nearest center, deterministically (specs/product_spec.md:188).

---

**CM-R21 — Color + symbol + silhouette triple coding, colorblind pass as a merge gate** · **MUST**
Rationale: "Line colors NEVER appear alone — always paired with symbol (● red, ■ blue, ▲ yellow, ◆ green, ★ wild) AND a distinct cat silhouette per color" (specs/product_spec.md:154-156); colorblind sim pass (deutan/protan/tritan) is a merge gate for palette changes (specs/product_spec.md:179); themes never change the colorblind-safe encoding (specs/monetization_spec.md:15-16); Neon must pass the colorblind check before enabling in `ofr_themes` (data/revenuecat_configuration.csv:18).

**Legibility protocol (one protocol, used by criteria 3 and 6)** — analyst-authored, A-26: renders are produced from the shipped assets; **5 raters**, none of whom authored the assets, are shown the renders **one at a time in randomized order, unprompted** (no legend, no answer list beyond the five line names) and name the line for each. 5 raters × 5 lines = 25 trials per run. **Bar: ≥90% of pooled trials correct (≥23/25).** Any asset below the bar is re-topo'd or cut, and the decision — asset id, trial results, remedy chosen, date, signer — is recorded in the ops log; the criterion fails only if an asset is below the bar **and** no such dated record exists.

1. `[CI]` Asset lint: every line-colored element references both a symbol and a silhouette id; a color-only asset fails the build (specs/product_spec.md:154-156).
2. `[CI]` Palette-change PRs run the deutan/protan/tritan simulation pass; failure blocks merge (specs/product_spec.md:179).
3. **Symbol + silhouette, color removed:** the five line markers rendered at in-game size with color removed (grayscale) and symbol retained pass the legibility protocol above (specs/product_spec.md:770).
4. Theme test: equipping Sakura or Neon leaves symbol and silhouette encoding byte-identical (specs/monetization_spec.md:15-16).
5. Neon is not enabled in `ofr_themes` until its colorblind check passes (data/revenuecat_configuration.csv:18; data/entitlement_map.json:288).
6. **Silhouette-only at 64 px:** the five cat silhouettes rendered at **64 px with color removed and symbols removed** pass the same legibility protocol (specs/product_spec.md:179-181).

---

**CM-R22 — Planning-pause, action-window floor, haptics/motion toggles** · **MUST**
Rationale: "planning-pause mode, haptics/motion toggles" (EXECUTION_PLAN.md:186); tap-and-hold 400 ms freezes the sim, switches remain tappable, release resumes after a 3-2-1 quarter-second countdown (specs/product_spec.md:191); `minActionWindowTicks` floor default 6 (~750 ms) is a hard constraint on top of the difficulty model, not an axis (specs/product_spec.md:511-512; data/level_schema.json:23); planning-pause offered inline after 3 fails (specs/product_spec.md:486).

1. Planning-pause test: 400 ms hold freezes the tick loop; taps during the freeze enqueue commands; release resumes after the 3-2-1 countdown; the resulting run is still replay-hash deterministic (specs/product_spec.md:191 + CM-R01).
2. `[CI]` Validator rejects any level whose only solutions require an action window below `minActionWindowTicks`; schema floor ≥3, product default 6 (data/level_schema.json:23; specs/product_spec.md:511).
3. Settings test: haptics master toggle and motion toggle each persist across restart and suppress their effects when off (specs/product_spec.md:679; EXECUTION_PLAN.md:186).
4. After 3 fails on any level, planning-pause is offered inline exactly once per level (specs/product_spec.md:486).
> **Defect to not propagate:** specs/product_spec.md:191,723 point at "Section 24" for the accessibility set, but §24 is Level Validation; specs/product_spec.md:385 points at "Section 21" for rewarded-offer eligibility, but eligibility is §20. Cross-references are wrong in-source; this PRD cites the real locations.

---

### 2.6 Commerce

**CM-R23 — Exact 6-SKU catalog, ids and prices, no hard-coded price strings** · **MUST**
*As a buyer I see one honest ladder of prices, rendered by the store, in my currency.*
Rationale: catalog LOCKED (EXECUTION_PLAN.md:187 "6 SKUs"; data/monetization_catalog.csv:2-7; data/google_play_checklist.csv:12 "IDs are permanent — a typo means a dead SKU"). Client renders `StoreProduct.PriceString` only; CI regex gate fails the build if any `pw_*`/shop string contains a currency symbol or digit-dot-digit (specs/monetization_spec.md:547-549; data/revenuecat_configuration.csv:41; data/offering_and_placement_map.json:13).

1. `[CI]` Config test: exactly the six ids `cm_all_access, cm_supporter_pack, cm_theme_sakura, cm_theme_neon, cm_rewind_5, cm_rewind_20` exist and are ACTIVE; `cm_all_access_499` exists but is INACTIVE and absent from `ofr_shop` (specs/monetization_spec.md:19; data/entitlement_map.json:54-61; data/paywall_experiments.csv:7). Under Option C (§6) this criterion asserts `cm_theme_neon` INACTIVE instead.
2. `[CI]` Regex gate over all paywall/shop strings: zero currency symbols, zero `\d\.\d\d` literals (specs/monetization_spec.md:547-549).
3. Type test: the two rewind SKUs are consumable; the other four are non-consumable (data/google_play_checklist.csv:12).
4. One sandbox purchase per SKU logged before production submit (data/google_play_checklist.csv:12).
5. Analytics logs `price_local_bucket`, never a raw local price (specs/monetization_spec.md:548; data/analytics_event_taxonomy.csv:31).
6. The 20-pack per-unit badge ("$0.25 vs $0.40") is the only comparative claim allowed and is arithmetically true (data/monetization_catalog.csv:7; data/entitlement_map.json:336).

---

**CM-R24 — Four entitlements with dashboard umbrella attach; one entitlement check per feature** · **MUST**
Rationale: attach map at data/revenuecat_configuration.csv:24-27 and specs/revenuecat_implementation.md:58 — "Umbrella attach means client checks exactly ONE entitlement id per feature — no boolean algebra in game code" (data/entitlement_map.json:13).

1. `[CI]` Static check: no game-code expression combines two entitlement ids with boolean logic (data/entitlement_map.json:13).
2. Entitlement test matrix: buying `cm_supporter_pack` grants `supporter` **and** `all_access` **and** both theme entitlements (data/entitlement_map.json:126-159).
3. Theme ownership derives from the dashboard attach, not client inference (data/entitlement_map.json:63-93).
4. Equipped-theme id is a separate save field; an owned theme stays equipped across offline and reinstall+Restore (data/entitlement_map.json:215-231).
5. Cached non-consumable entitlements are honored **indefinitely** offline; >30 d staleness is diagnostics-only and never revokes; clock tampering cannot revoke (specs/revenuecat_implementation.md:395-447; data/entitlement_map.json:95-103). **The residual this creates is a named human acceptance (NEW-Q46, risks RK-16/RK-22), not an agent-authored control.**

---

**CM-R25 — Placement-first client contract with a written fallback policy** · **MUST**
Rationale: "game code asks by PLACEMENT id only (`GetOfferingForPlacementAsync`); no screen/prefab/presenter may name an offering id" (data/offering_and_placement_map.json:6); wiring at specs/monetization_spec.md:106-114 and data/revenuecat_configuration.csv:34-38; fallback policy at data/revenuecat_configuration.csv:39 and data/offering_and_placement_map.json:138,284.

1. `[CI]` Grep gate: zero occurrences of any `ofr_*` identifier outside `CatMetro.Integrations.RevenueCat` (data/offering_and_placement_map.json:6).
2. Wiring test: the five placements resolve to `ofr_core / ofr_themes / ofr_core / ofr_shop / ofr_rewind` respectively (data/revenuecat_configuration.csv:34-38).
3. Fallback test: with the placement API returning empty, `post_level_5`/`bonus_district`/`shop` fall back to current offering then cached last-good; `theme_preview`/`rewind_failure` fall back to **nothing** (data/revenuecat_configuration.csv:39; data/offering_and_placement_map.json:138,284).
4. `SupporterVisibilityRule` test: the supporter package renders only on the shop screen; `post_level_5` and `bonus_district` bind the all_access package exclusively (data/revenuecat_configuration.csv:29; data/offering_and_placement_map.json:17,75-77).
5. Global rule tests: max ONE system-initiated commerce surface per session; never two commerce modals back-to-back; a 60 s no-offer window after any failure unless the player tapped the rewind chip (specs/monetization_spec.md:117; data/offering_and_placement_map.json:9-10).
6. Offline: each placement serves its cached last-good offering and purchase CTAs fail fast ≤2 s (data/offering_and_placement_map.json:16).

---

**CM-R26 — `post_level_5`: the one scripted paywall exposure, once per install ever** · **MUST**
Rationale: "the ONE scripted paywall exposure — RC Paywalls v2 `post_level_5`, celebratory framing, All Access $6.99 as complete edition, dismiss X immediate and obvious; decline → Harbor Line map reveal, no re-ask" (specs/product_spec.md:304); eligibility and dismissal rules at specs/monetization_spec.md:127-143; RCUI used ONLY for post_level_5 with a permanent per-device disable after 2 crash markers and a first-class custom UGUI renderer taking over (specs/revenuecat_implementation.md:453-510).

1. Trigger test: fires on first L5 completion, after the celebration, before map return; never on a replay of L5 (specs/monetization_spec.md:127-143).
2. `[CI]` Once-ever test: after one presentation (or one dismissal), no re-arm occurs — including after reinstall-with-same-save (data/offering_and_placement_map.json:78-107).
3. Eligibility test: suppressed when any entitlement is active, when any SKU was previously purchased, or when refund-suppressed (specs/monetization_spec.md:127-143).
4. Dismissal test: ✕ is ≥48dp and full opacity on frame 1; back gesture and "Keep playing free" also dismiss; no exit-intent counter-offer is constructible (specs/monetization_spec.md:127-143).
5. Empty-resolution test: if the placement resolves empty, nothing presents and the once-ever moment is consumed silently (data/offering_and_placement_map.json:92).
6. Renderer resilience: 2 armed-marker process deaths or in-process exceptions set `PaywallV2Disabled=true` permanently on that device and the custom paywall takes over with no user-visible error (specs/revenuecat_implementation.md:453-510).
7. Copy test: 3 disclosure lines visible **without scrolling** on 720p 16:9 (specs/monetization_spec.md:419-439).
8. Paywall open-to-render ≤2.0 s and purchase-to-entitlement ≤3.0 s on mid tier (data/device_test_matrix.csv:4).
> Success metrics (measured, not gates): view→purchase ≥1.5% by D+14, median view ≥6 s, fast-dismiss (<2 s) share <70% (specs/monetization_spec.md:127-143) — self-set, denominator = `paywall_viewed` on this placement.

---

**CM-R27 — Consumable ledger: SHA-256 dedupe, exactly-once grant, durable-write-before-event** · **MUST**
*As a buyer of a rewind pack, I am credited exactly once, even if the store calls back three times or the app dies mid-grant.*
Rationale: "`ConsumableLedger.TryGrant(transactionId, productId)` is the ONLY function that may increase rewind balance from a purchase" (specs/revenuecat_implementation.md:355-387); key = first 16 bytes of SHA-256 as 32 lowercase hex chars; raw transaction id never leaves the device (specs/revenuecat_implementation.md:375-383); dedupe set is NEVER trimmed (data/entitlement_map.json:343-347); "Never cut: purchase/restore integrity" (EXECUTION_PLAN.md:200-201).

1. `[CI]` EditMode test: duplicate callback with the same transaction id grants once, logs `error_caught(domain=ledger_dedupe)` and returns 0 (specs/revenuecat_implementation.md:357-371).
2. `[CI]` Unknown-SKU test: quantity lookup returns 0 and no balance change occurs (specs/revenuecat_implementation.md:357-371).
3. `[CI]` Ordering test: `purchase_completed` is emitted only after the durable write; a fault injected before the write produces neither an event nor a balance change (specs/revenuecat_implementation.md:357-371).
4. `[CI]` Atomicity test: dedupe-hash insert, audit entry and balance increment land in ONE temp+rename write (specs/revenuecat_implementation.md:357-371).
5. `[CI]` Never-trim test: after 200 audit entries the audit list is FIFO-capped at 200 while the dedupe HashSet retains all hashes (data/entitlement_map.json:343-347). **Bounding this structure and the resulting save size is risks RK-20 (architect), and CM-R05.5 carries the size assertion.**
6. Device test: duplicate redelivery across 3 relaunches credits +20 exactly once (specs/revenuecat_implementation.md:228-253).
7. Device test: `adb kill` mid-purchase leaves no orphaned consumable; the ledger reconciles from RC CustomerInfo (data/device_test_matrix.csv:4).
8. Breadcrumb test: `purchase_breadcrumb` written on entering Purchasing, cleared at Done/Cancelled/Failed; on boot it drives a silent Verifying; at 72 h it expires with `purchase_failed(error_domain=recovery, error_code=breadcrumb_expired)` (specs/revenuecat_implementation.md:199-212).
9. `[CI]` Main-thread assertion: a dev-build assert on off-main-thread ledger entry is build-blocking (data/entitlement_map.json:363).
10. Hard gate: ledger mismatches = 0 (specs/monetization_spec.md:202).
> **Scope of the guarantee, stated so no published claim overstates it (risks RK-21):** this ledger guarantees **exactly-once grant from the store**. It is explicitly **not** tamper-resistance — the save is plaintext. Any BIP/listing/Devpost text describing it as anti-cheat violates CM-R56.4.

---

**CM-R28 — Restore purchases, one tap from three places, honest copy** · **MUST**
Rationale: "Restore purchases" reachable from shop footer, settings, and every paywall footer (specs/monetization_spec.md:369); copy at specs/monetization_spec.md:373; consumables are NOT restorable and the copy must never imply otherwise (data/entitlement_map.json:401-404).

1. Reachability test: a restore entry point exists in shop footer, settings, and every paywall footer (specs/monetization_spec.md:369).
2. `[CI]` Suppression-matrix test: after a successful restore the payer suppression matrix applies in the same frame (specs/monetization_spec.md:374,380).
3. Copy test: success lists each restored entitlement ("Restored: All Access ✓"); none-found renders the account-switch hint verbatim and logs `restore_completed(entitlements_restored_count=0)` (specs/monetization_spec.md:373; data/entitlement_map.json:401-404).
4. Buy-owned test: `ProductAlreadyPurchased` renders "You already own this — restoring instead" and auto-restore runs once (specs/revenuecat_implementation.md:251,274).
5. Fresh-reinstall restore verified on device (data/analytics_event_taxonomy.csv:34).
6. **Restore success and the review tripwire, separated** (analyst-authored window, A-29): over the **14 days following production launch**, restore success ≥95% of attempts from accounts with a prior purchase, measured as `restore_completed(entitlements_restored_count ≥ 1)` ÷ `restore_started` for those accounts. Store reviews mentioning restore are triaged **weekly** and each is logged with a disposition — they are a **tripwire that opens an investigation**, explicitly **not** an acceptance criterion. (The draft's "zero restore-related 1★ reviews" is withdrawn as a criterion: no observation window, and it requires subjective classification of third-party text.)

---

**CM-R29 — Payer suppression enforced in exactly one place** · **MUST**
*As someone who already paid, I am done being sold to.*
Rationale: "payers are done being sold to" (specs/monetization_spec.md:33-39); suppression list at specs/monetization_spec.md:118,332-333; "Enforced in ONE place: `OfferEligibilityService` (CatMetro.Application), unit-tested; QA hard gate = zero `paywall_viewed` for suppressed surfaces on a payer" (specs/monetization_spec.md:339-342); RC Targeting may only be a redundant server-side belt — the assertion must hold with Targeting off (data/revenuecat_configuration.csv:58).

1. `[CI]` Single-source test: every suppression decision routes through `OfferEligibilityService`; a static check fails on any bypass (specs/monetization_spec.md:339-342).
2. `[CI]` Unit matrix: with `all_access` or `supporter` active, `post_level_5`, district-complete shop card, bonus_district lock, theme upsells and All Access cross-lines are all suppressed; rewind packs and the reframed Supporter card remain (specs/monetization_spec.md:118,332-333).
3. QA hard gate: zero `paywall_viewed` events for suppressed surfaces on a payer account, with RC Targeting **off** (specs/monetization_spec.md:339-342; data/revenuecat_configuration.csv:58).
4. **`payer_thanks`, authored against a named surface** `[PIN NEW-Q10]` — one branch survives (§4.1). Under every branch: **exactly one delivery ever per install** (durable flag, survives process death, cannot re-fire on a second purchase), the copy contains no product, price, shop link or CTA other than dismissal, and no lifecycle selling message is delivered to that install afterwards.
   - **T-local-notification:** a local notification is scheduled at the durable write of the first `purchase_completed` and fires between +0 h and +24 h of it; if the app is opened first, it is cancelled and the thanks is shown in-session instead; asserted single-fire across a kill-and-relaunch.
   - **T-IAM-next-session:** an in-app message presents on the first session start after the first purchase **and** within 24 h of it; if no session starts inside 24 h, it is not shown at all and is logged as expired (also a single, asserted outcome).
   - **T-journey-step:** one of the six locked message steps is allocated to it; CM-R38.1's "exactly 6 steps" audit still passes, and the allocation is recorded in the journey export.
5. An unfired `post_level_5` never re-arms after the player becomes a payer (specs/monetization_spec.md:118).

---

**CM-R30 — Refund and revocation handled honestly and never mid-level** · **MUST**
Rationale: trigger `entitlement_changed{change:revoked}`; applied at next session boundary or next Home visit, NEVER mid-level (hard gate) (specs/monetization_spec.md:389-391; data/entitlement_map.json:105-107); content effects and 30-day system-paywall suppression at data/entitlement_map.json:108-115 and specs/monetization_spec.md:392-397.

1. Device test via RC dashboard refund: revocation applies at the next session boundary; a test that forces revocation mid-level asserts no state change until the boundary (specs/monetization_spec.md:389-391).
2. Content test: Night Harbor relocks while retaining every star/score/completion; repurchase resumes exactly where the player left off (data/entitlement_map.json:108-113).
3. Themes revert silently at next Home load; daily rewind returns to 1/day at next midnight; badge removed with no ceremony (data/entitlement_map.json:108-113).
4. Commerce test: all system-initiated paywalls suppressed 30 days; player-initiated surfaces remain, normally styled, with no urgency/discount/win-back copy constructible (specs/monetization_spec.md:392,397).
5. One quiet IAM with CTA "OK" only, copy verbatim per specs/monetization_spec.md:393.
6. Supporter refund emits four revoked events for one product (data/entitlement_map.json:168).
7. `payer_status` downgraded, OneSignal tag updated, payer journeys exited (data/entitlement_map.json:116). **The tag set is an undeclared third-party data flow until CM-R45 enumerates it (risks RK-30).**
8. Consumed rewinds are not clawed back (data/entitlement_map.json:395-399).
> **Split-out:** the anti-farming clause that previously sat inside this requirement is now **CM-R30-D** (below) and blocks nothing here. CM-R30 scores on criteria 1–8, all concrete.

---

**CM-R30-D — Refund-farming suppression** · **WON'T (deferred out of 1.0 pending NEW-Q47)**
Statement as inherited: ">2 refunded consumable purchases → pack rows hidden for that account permanently" (data/entitlement_map.json:395-399).
**Why it is deferred and not authored:** "account" has no definition under anonymous RC ids with no accounts, and no mechanism by which the client learns of a refund is specified. Any implementation that *creates* an identity (device fingerprint, SSAID/AD_ID-derived id, hardware hash) introduces a persistent identifier, a Data Safety declaration and a 13+ audience exposure to stop a handful of $1.99 refunds (risks RK-23). **No criterion is authored.** The human answers NEW-Q47: (a) do not implement — delete the clause; or (b) implement, in which case it becomes a PII flow requiring a `docs/security/` policy entry and a Data Safety declaration **before any code lands**, and a criterion is commissioned then. See U-2.

---

**CM-R31 — Play In-app Promotions integrated; 25 promo codes; clean-device end-to-end test** · **MUST**
*As a hackathon judge with no purchase intent, one code gives me the full game in under a minute.*
Rationale: LOCKED — "Promo codes: 25 total (15 judges / 5 press / 5 spare), minted at launch, **and the app integrates Play In-app Promotions** (verified requirement, answer/6321495) with an end-to-end redemption test on a clean device as a D17/D24 acceptance criterion" (EXECUTION_PLAN.md:152-154). **[SUPERSEDES]** data/google_play_checklist.csv:33 (15 codes, judges only). Redemption UX: entitlement arrives via CustomerInfo sync with **no** purchase UI, thank-you toast, logs `purchase_completed(price_local_bucket=promo)` (specs/revenuecat_implementation.md:250; data/revenuecat_configuration.csv:55).

1. 25 codes minted once `cm_all_access` is ACTIVE, split 15/5/5, with expiry before Sep 30 (data/revenuecat_configuration.csv:55).
2. Clean-device (factory-reset) end-to-end test: 2 codes redeemed, `all_access` active in RevenueCat, no purchase UI shown, thank-you toast rendered (specs/revenuecat_implementation.md:250,307; data/google_play_checklist.csv:33).
3. `purchase_completed` logs `price_local_bucket=promo` (specs/revenuecat_implementation.md:250).
4. **Client-side redemption capability, authored against a named surface** `[PIN NEW-Q12]`:
   - **P-client-surface (default reading of the LOCKED row):** a named in-app **redeem entry point** exists (Settings ▸ "Redeem a code" row). From a **running** app, tapping it launches the Play promo-code redemption sheet; on a successful redemption the next CustomerInfo sync grants `all_access` with **no purchase UI shown**; verified end-to-end on a factory-reset device with a recorded screen capture. Failure/cancel returns to Settings with no state mutation.
   - **P-console-only:** the human scopes the work as Console code generation only; this criterion is **withdrawn** and CM-R31 rests on criteria 1–3 and 5, which are already concrete. Withdrawal is recorded with a dated note, because EXECUTION_PLAN.md:152-154 currently reads as a locked client requirement.
5. Remaining 23 codes + a step-by-step redemption guide stored in `ops/judge_codes.md` and pasted into Devpost; never posted publicly (data/revenuecat_configuration.csv:55). **Secret-handling is risks RK-26: gitignore + secret-scan rule must exist before the file is created.**
> **UNVERIFIED:** "quarterly quota ~500" is explicitly flagged "reverify in Console" (data/revenuecat_configuration.csv:55) — must not be published as fact (A-12).

---

**CM-R32 — Purchase state machine, failure copy, and the "you were not charged" rule** · **MUST**
Rationale: state machine Idle→Fetching(8 s timeout)→Presenting→Purchasing→{Cancelled|Pending|Verifying}→Granting→Done; only Granting→Done mutates durable state (specs/revenuecat_implementation.md:162-196); purchases never auto-retried; offering fetches auto-retry ×3 (1 s/4 s/10 s) (specs/revenuecat_implementation.md:268-287); error-toast rule verbatim: "The store couldn't complete that — you were not charged."; user-cancel = silent return, no retry modal, no "wait! 10% off" (banned) (specs/monetization_spec.md:351-353).

1. State-machine test: only the Granting→Done transition mutates durable state, and it does so atomically (specs/revenuecat_implementation.md:162-196).
2. Pending test: a slow-card purchase renders "Waiting for Google Play to confirm…" and completes correctly in a later session (specs/revenuecat_implementation.md:162-196).
3. Back-gesture test: from Purchasing onward the sim is paused and the back gesture is swallowed (specs/revenuecat_implementation.md:162-196; specs/product_spec.md:192).
4. Retry-policy test: zero automatic purchase retries; exactly 3 offering-fetch retries at 1/4/10 s (specs/revenuecat_implementation.md:268-287).
5. Copy test: every non-cancel failure renders the "you were not charged" string; user-cancel renders nothing (specs/monetization_spec.md:351-353).
6. **Unknown-error degradation, with the safest path defined** (analyst-authored, A-28): injecting an **unrecognized** RC error code at each mutating call site produces **all four** of: (a) **no durable state mutation** (save bytes unchanged, ledger unchanged, entitlement cache unchanged); (b) the toast "The store couldn't complete that — you were not charged."; (c) the state machine returns to **Idle**; (d) **zero** automatic retries. One test per injection site (specs/revenuecat_implementation.md:268-287).
> **Do not treat RC C# API names as verified:** all skeleton names are "directionally correct, not copy-paste-guaranteed" and the async surface may be callback-only (specs/revenuecat_implementation.md:331-337,515). The architect must verify against the 9.7.0 package source. → NEW-Q13, A-07, risks RK-39.

---

**CM-R33 — Ads contingency: Model A wired, `ads_enabled` flag, hard D10/D14 trigger** · **MUST**
Rationale: contingency trigger — "RC Ads beta not granted by D10 (Aug 10) OR AdMob not working end-to-end on device by D14 (Aug 14); AppLovin MAX 8.6.4 attempted first if AdMob-specific" (specs/monetization_spec.md:90); contingency state = same catalog/prices, `ads_enabled` OFF, rewind economy 1 free/day (+1 All Access) + packs, all five rewarded surfaces dark, paywalls/offerings/copy unchanged (specs/monetization_spec.md:91-92); "No ad code merges after D21 regardless" (specs/monetization_spec.md:98).

1. Flag test: with `ads_enabled=false` every rewarded row is absent from every surface, and no ad SDK call is issued (specs/monetization_spec.md:91-92).
2. Flag test: with `ads_enabled=false` the paywalls, offerings and all copy are byte-identical to the ads-on build (specs/monetization_spec.md:91-92).
3. Economy test under contingency: 1 free rewind/day (2 with All Access) + packs remain the only sources (specs/monetization_spec.md:91-92).
4. Calendar gate: the D10/D14 decision is recorded with evidence; no ad code merges after D21 (specs/monetization_spec.md:90,98).
5. No-fill test: rewarded surfaces hide gracefully with a toast; the `streak_saver` no-fill path pays the 150-ticket fallback (specs/monetization_spec.md:267-283; data/economy_sources_and_sinks.csv:19).
> **Named exposure, carried as a risk not silently patched (risks RK-24 / SEC-09):** the no-fill fallback as written pays out **without an ad ever being shown**, deterministically reachable in airplane mode. Whether it consumes the same daily cap slot and is written through the same durable ledger is an architect design decision the human commissions — no agent-authored criterion changes the economy here.
> **Also carried as a risk, not a criterion (RK-25):** AdMob **account health** is not currently listed as a trigger condition for flipping `ads_enabled=false`. Adding it is a human decision.

---
### 2.7 Ads

**CM-R34 — Rewarded/opt-in only; zero interstitials, banners, app-open** · **MUST**
*As a player, an ad only ever plays because I asked for something.*
Rationale: LOCKED (EXECUTION_PLAN.md:188); "ALL placements rewarded/opt-in only; NO interstitials/banners/app-open" (data/ad_placement_map.csv:1); "no forced ads ever at launch" is the pitch spine (specs/product_spec.md:46-54; specs/monetization_spec.md:29,33-39).

1. `[CI]` Static gate: the build contains zero references to interstitial, banner, or app-open ad formats in any ad SDK adapter (data/ad_placement_map.csv:1).
2. Runtime test: every ad start is preceded by an explicit player tap on an opt-in row; no ad can start from a timer, a level transition, or app foreground (data/ad_placement_map.csv:1).
3. Reward test: reward granted only on the `onUserEarnedReward` callback (data/ad_placement_map.csv:3).
4. **Decline invariants, enumerated** (analyst-authored, A-28): after `ad_offer_declined` on any surface, **all five** hold, one assert each: (a) ticket balance unchanged; (b) rewind balance unchanged; (c) no daily/session cap counter for that surface is consumed; (d) no re-prompt for that surface occurs in the same session; (e) no UI state changes beyond dismissal of the offer sheet (same screen, same scroll position, same board state). This replaces the unbounded universal "never penalizes the player in any observable way" (data/analytics_event_taxonomy.csv:24).
5. Listing/IARC parity: store listing and IARC declare rewarded/opt-in only (data/google_play_checklist.csv:20,22).
> **No consent gate exists in this requirement or anywhere in this PRD.** Serving ads and ad-attributed analytics to EEA/UK users requires a certified CMP/UMP flow. This is **NEW-Q45** (risks RK-11; venture critique V-6; threat model SEC-16) and is a human decision: build the consent flow, or restrict initial availability / ship `ads_enabled=false`. **It blocks CM-R50.9.**

---

**CM-R35 — Exactly five rewarded surfaces with locked caps** · **MUST**
Rationale: LOCKED caps — rewind_failure 2/session, 5/day (**3/day if D-4 is taken**); double_tickets 3/day; daily_gift_double 1/day; streak_saver 1/day; theme_rental 1/theme/day (EXECUTION_PLAN.md:188; data/ad_placement_map.csv:3-7). Cap values in data/ad_placement_map.csv:3 state 5/day flatly — **PENDING D-4**.

1. `[CI]` Config test: exactly five rewarded placements exist, named `rewind_failure, double_tickets, daily_gift_double, streak_saver, theme_rental` (specs/revenuecat_implementation.md:121-123).
2. `[CI]` Cap enforcement tests, one per surface, asserting the row disappears at the cap boundary and reappears at the local-midnight reset (data/ad_placement_map.csv:3-7; data/analytics_event_taxonomy.csv:23).
3. rewind_failure session cap 2 and daily cap **5 (or 3 — D-4)** are read from one config constant, so the D-4 decision is a one-line change; a test asserts the published cap value equals the constant.
4. `double_tickets` offered only on the results screen after a **fresh** win (never a replay), payout ≥20, as a single quiet button, never a popup (data/ad_placement_map.csv:4; data/economy_sources_and_sinks.csv:8).
5. `theme_rental` grants exactly 3 levels, level-counted locally, honored if connectivity drops mid-rental; expiry is a silent revert plus at most one passive toast/day (data/entitlement_map.json:231; specs/monetization_spec.md:145-163).
6. Cap-reached UX: sheet reorders owned-first with the cap explainer "refresh at midnight. Packs never expire." and the ad row hidden (specs/monetization_spec.md:267-283).

---

**CM-R36 — Three declines mute ad rows for 24 h** · **MUST**
*As a player who keeps saying no, the game stops asking.*
Rationale: LOCKED "3-decline → 24h mute" (EXECUTION_PLAN.md:188). **Appears in none of the six retention/commerce data files** — instantiated here from the locked constraint; the nearest in-file statement is "3 consecutive `ad_offer_declined` → ad rows hidden everywhere 24h (reset by any player-initiated ad tap)" (specs/monetization_spec.md:267-283).

1. Behaviour test: 3 consecutive `ad_offer_declined` events hide ad rows on **all five** surfaces for 24 h (specs/monetization_spec.md:267-283).
2. Reset test: any player-initiated ad tap clears the mute state (specs/monetization_spec.md:267-283).
3. Persistence test: the mute survives app restart and process death.
4. Non-consecutive test: decline-decline-accept-decline does not trigger the mute.
5. Copy test: the mute is silent — no explanatory modal, no re-ask prompt.

---

**CM-R37 — Every ad event through RC AdTracker; rewards granted client-side against our ledger** · **MUST**
Rationale: "All events through RC AdTracker; rewards granted client-side against our ledger" (locked; EXECUTION_PLAN.md:188); `IAdTracker` maps 1:1 to RC AdTracker (≥9.1.0) with `AdContext = {placement, network, adUnitId}`; the AdMob convenience module is NOT available for Unity → manual calls; **server-verified ad rewards are NOT available on Unity** → client-side grants with the same grant-once discipline (specs/revenuecat_implementation.md:111-127; data/ad_placement_map.csv:1).

1. `[CI]` Adapter test: each of TrackAdLoaded / Displayed / Opened / Revenue / FailedToLoad is invoked exactly once per corresponding lifecycle event, for all five placements (specs/revenuecat_implementation.md:111-127; data/ad_placement_map.csv:3-7).
2. `[CI]` Grant-once test: a duplicated `onUserEarnedReward` callback grants the reward once (same discipline as CM-R27).
3. Pre-beta safety: AdTracker calls are no-op-safe before RC Ads beta access is granted (specs/revenuecat_implementation.md:111-127).
4. Analytics routing: `rewarded_ad_started/completed/failed` route to `revenuecat_adtracker`; `rewarded_ad_completed` carries reward_type/reward_amount and updates `ad_watches_today` (data/analytics_event_taxonomy.csv:25-27).
5. Kill-app-mid-ad test: reward granted exactly once or not at all — never twice (data/analytics_event_taxonomy.csv:26).
6. Airplane-mode test: ad surface degrades gracefully with the no-fill toast (specs/product_spec.md:766; specs/monetization_spec.md:267-283).
> Note: RC Ads (public beta) is a **tracking layer only — it does not serve ads** (specs/revenuecat_implementation.md:24; data/revenuecat_configuration.csv:44). Client-side granting with no SSV is risks RK-24; whether GMA 11.3.0 rewarded SSV is usable independently of RC AdTracker is an architect question, not an agent-authored criterion.

---

### 2.8 Messaging

**CM-R38 — Exactly 3 OneSignal journeys, 6 message steps total (2+3+1)** · **MUST**
Rationale: LOCKED (EXECUTION_PLAN.md:155-159; specs/onesignal_retention.md:19,54). **[SUPERSEDES]** specs/liveops_spec.md:176 "6 message steps each" (AUDIT_FINDINGS.md:308). Journey 1 entry is **tag-based**; no `streak_at_risk` event exists (EXECUTION_PLAN.md:158-159) — **[SUPERSEDES]** data/onesignal_journeys.csv:3 and data/analytics_event_taxonomy.csv:13.

1. `[CI]` Config audit: exactly 3 active journeys and exactly 6 message steps exist in the exported OneSignal configuration; a 7th step fails the audit (specs/onesignal_retention.md:19,54). If NEW-Q10 resolves to **T-journey-step**, the `payer_thanks` step is one of the six, not a seventh.
2. J1 entry is tag-based (`daily_unlocked` tag), with the streak leg as a branch on `streak_days`/`daily_last_done` inside J1 — not an event named `streak_at_risk` (EXECUTION_PLAN.md:158-159; AUDIT_FINDINGS.md:310).
3. J2 entry is segment-based: `last_session` > 48 h AND `tutorial_done=true` AND first_session older than 2 days AND `lapse_final_sent != true`, excluding payers within 24 h of purchase (specs/onesignal_retention.md:105-106).
4. J3 entry: `level_failed` forwarded only on the **second** failure of the same level within 60 min with no completion between — ×2 filter applied client-side in the adapter, while analytics still receives every raw `level_failed` (specs/onesignal_retention.md:151-156). **[SUPERSEDES]** `level_stuck` (specs/liveops_spec.md:184; AUDIT_FINDINGS.md:322).
5. J3 never sells: no product/price/shop link in copy; the deep link lands on the level intro, not a paywall; on the assisted attempt the rewind sheet shows the granted free rewind only, with purchase rows hidden and `rewind_failure` suppressed (specs/onesignal_retention.md:166-171).
6. J3 banks the free rewind at trigger time so "Your free rewind is loaded" is true whether or not the push delivers (specs/onesignal_retention.md:158).
7. `lapse_final_sent=true` permanently blocks re-entry past the 14 d rung; the final message is sent at most once per user, ever (specs/onesignal_retention.md:116-121).
8. `[CI]` Adapter gate: every tag/event name must exist in the taxonomy CSV; unknown names are a build error in development; the OneSignal adapter is the only writer (specs/onesignal_retention.md:232-234). **The tag set itself is an undeclared data category until CM-R45 enumerates it (risks RK-30).**

---

**CM-R39 — Quiet hours 21:00–09:00 local; honest cap 2/day** · **MUST**
Rationale: LOCKED "No push 21:00-09:00 local. Honest cap statement: max 2/day for an engaged streak-holder" (EXECUTION_PLAN.md:155-157). **[SUPERSEDES]** data/onesignal_journeys.csv:3 ("22:00-09:00"), specs/liveops_spec.md:182 ("09:00–22:00"), and specs/liveops_spec.md:200 ("max 1 push/day, 3/week") — AUDIT_FINDINGS.md:309,321. Frequency capping is Enterprise-only, so caps are enforced in-app plus Time Window steps on every message step (specs/onesignal_retention.md:20-21).

1. `[CI]` Config audit: every one of the 6 message steps carries a Time Window that excludes 21:00–09:00 local (specs/onesignal_retention.md:21).
2. Adapter test: a send attempt inside quiet hours is refused locally even if the dashboard would allow it (specs/onesignal_retention.md:20-21).
3. **Worst-case day budget, as a 7-day simulation** `[PIN NEW-Q14]`: a simulated engaged streak-holder timeline over 7 consecutive local days (daily completed each day, streak ≥3, one purchase failure on day 4, one lapse-adjacent day 6) is replayed through the delivery layer, and the test asserts **≤2 deliveries in every local day**, counting **all** channels: J1 steps, J2/J3 steps, scheduled event sends, and local notifications (CM-R42). The per-channel allocation the simulation uses is the human-authored table committed as `config/message_budget.json`; two candidate allocations are enumerated in §4.1. Additionally, and independent of the allocation: **the adapter refuses any send that would exceed the day budget, whatever its source** — asserted by injecting a 3rd send on a day already at 2 (EXECUTION_PLAN.md:156).
4. Copy test: the soft-prompt and every public cap statement say "a daily nudge, plus a streak warning if one's at risk — never at night" (EXECUTION_PLAN.md:156-157) — **[SUPERSEDES]** "at most one reminder a day" (specs/onesignal_retention.md:216; AUDIT_FINDINGS.md:492-493).

---

**CM-R40 — Notification permission: never at first launch, two-attempt budget, value-moment gated** · **MUST**
Rationale: soft prompt does not appear in session 1 (specs/product_spec.md:308-309); Android 13+ two-attempt budget with attempt 1 after first `daily_completed` and attempt 2 at `streak_changed` new_streak=3; after both, only a Settings row using `fallbackToSettings:true` (specs/onesignal_retention.md:211-219); request after the first Daily Line unlock (post L7) with a plain-language pre-prompt (data/google_play_checklist.csv:26).

1. Session-1 test: no permission prompt and no soft prompt in the first session (specs/product_spec.md:308-309).
2. `[CI]` Cap test: `push_soft_prompt_viewed` fires at most once per build; `soft_prompt_seen` tag set (data/analytics_event_taxonomy.csv:36; specs/onesignal_retention.md:211-219).
3. Attempt-budget test: at most two system permission dialogs ever; subsequent access is a user-initiated Settings row (specs/onesignal_retention.md:211-219).
4. API <33 test: default-granted path skips prompting; channels `daily`, `help`, `account` registered at init (specs/onesignal_retention.md:220).
5. Denial test: denial degrades to local scheduling where possible and never blocks gameplay (data/google_play_checklist.csv:26).
6. `push_permission_result` logs granted/denied and `source_trigger` (data/analytics_event_taxonomy.csv:37).

---

**CM-R41 — Deep-link router: 7 routes, cold/warm/killed, safe fallback** · **MUST**
Rationale: 7 routes `catmetro://daily|home|level/{id}|event/{id}|shop|restore|feedback` (specs/onesignal_retention.md:273); central DeepLinkRouter validates and routes with safe fallback to Home; click listener registered in Boot before scene load; router resolves after save load (specs/onesignal_retention.md:274-275,283).

1. `[CI]` PlayMode tests for all 7 routes plus invalid input (specs/onesignal_retention.md:276-281).
2. Device QA: cold, warm and killed states all route correctly (specs/onesignal_retention.md:276-281; specs/product_spec.md:767).
3. Stale-link test: a completed level routes to the next uncompleted level with a "you got it!" toast; an expired event routes Home with "event ended" (specs/onesignal_retention.md:186-187,274).
4. `notification_opened` fires with `campaign_id`, `journey_id`, `deep_link` and stamps the session (specs/onesignal_retention.md:276).
5. Event deep links use `catmetro://event/{event_id}` — **[SUPERSEDES]** the bare `catmetro://event` in data/onesignal_journeys.csv (AUDIT_FINDINGS.md:325).
6. Challenge links: `catmetro://challenge/{seed}` and App Link `https://catmetro.io/c/{seed}`; invalid/expired seed lands Home gracefully (specs/product_spec.md:697). **The domain is not owned yet and the identity is split .io/.com — risks RK-28, folded into D-2 / NEW-Q27.**
> **NEW-Q15:** the query-parameter share form `catmetro://daily?d=YYYY-MM-DD&b=score` (specs/liveops_spec.md:65) is not enumerated in the route list (specs/onesignal_retention.md:273).
> **Untrusted-input hardening is risks RK-27 (SEC-12), not an agent-authored criterion:** route+parameter allowlisting, typed range checks, crash-safe pre-parse before save load, and the invariant that **no deep link may grant an entitlement/tickets/a rewind or open a purchase surface** are mitigations the human commissions. The last of those is the one the threat model asks be made a MUST.

---

**CM-R42 — Local-notification backups and IAM substitutions** · **SHOULD** (streak-expiry backup: **MUST**, it is the P0 offline leg)
Rationale: streak-expiry local backup is P0 via Unity Mobile Notifications, scheduled when a daily completes with `streak_days>=3`, canceled on next daily completion/app open, suppressed if J1 Msg 2 already delivered today (specs/onesignal_retention.md:199); `purchase_issue` and `feedback_request` substitutions (specs/onesignal_retention.md:194-195).

1. **Streak-expiry backup** `[PIN NEW-Q17]` — one offset survives (§4.1). Under **both** branches: scheduled at the daily completion that leaves `streak_days ≥ 3`; **cancelled** on the next daily completion **or** on app open, whichever is first; **suppressed** if J1 Msg 2 was delivered on the same local day; **exactly one delivery** per scheduling; counts against the CM-R39.3 day budget.
   - **S-2000-local:** fires at 20:00 local on the day **after** the scheduling daily completion (specs/liveops_spec.md:196).
   - **S-expiry-minus-6h:** fires at (streak expiry timestamp − 6 h), computed from the local-midnight rollover rule of CM-R11 (specs/onesignal_retention.md:199).
2. **`purchase_issue`** `[PIN NEW-Q16]` — one mechanism + offset survives (§4.1). Under **all** branches: the copy contains "No charge went through"; it **never** fires when `user_cancelled=true`; **exactly one** delivery per failed purchase; it counts against the CM-R39.3 day budget and obeys quiet hours.
   - **PI-push-2h:** OneSignal push at failure + 2 h (data/onesignal_journeys.csv:11).
   - **PI-local-2h-IAM-fallback:** local notification at failure + 2 h; if permission is denied, an IAM on the next session instead — exactly one of the two delivers, never both (specs/onesignal_retention.md:194).
   - **PI-IAM-then-local-4h:** IAM on the next session; if no session by failure + 4 h, a local notification at +4 h — again exactly one delivers (specs/liveops_spec.md:192).
3. **`feedback_request`** `[PIN NEW-Q18]`: **F-IAM-only** (no push or scheduled send is constructible for this surface — asserted by a static check that the feedback surface has no scheduling call site) or **F-push-permitted** (a scheduled send is permitted and obeys quiet hours + the day budget). Under both: **never delivered in a session in which `level_failed` fired within the preceding 5 minutes**, and **capped at 1 per build** (specs/onesignal_retention.md:195).
> **Correctly left to the architect, and not a blocker for the criteria above:** the Unity Mobile Notifications 2.x patch version is deliberately unpinned ("pin exact 2.x patch in the Week-1 SDK spike", specs/onesignal_retention.md:199). This PRD must not invent one.

---

### 2.9 Analytics

**CM-R43 — 45-event taxonomy behind one typed wrapper with a bounded offline queue** · **MUST**
Rationale: "45-event taxonomy behind one typed wrapper, offline queue, Crashlytics, privacy-classified, data-safety mapped" (EXECUTION_PLAN.md:190) — the **only** source for the wrapper/queue requirement; it appears in none of the data files. Exactly 45 events with required params, destinations and privacy classes (data/analytics_event_taxonomy.csv:2-46).

1. `[CI]` Count test: exactly 45 event types are emittable; adding a 46th without a taxonomy row fails the build (data/analytics_event_taxonomy.csv:2-46). **Taxonomy delta TD-01 (CM-R44.2) adds a required *param* to `level_started`, not an event, so the count stays 45** — unless NEW-Q36 resolves to a separate event, in which case this criterion asserts 46 and the taxonomy row is added in the same change.
2. `[CI]` Typed-wrapper test: no call site constructs an analytics event by raw string; a static check fails on any direct SDK call outside the wrapper (EXECUTION_PLAN.md:190).
3. `[CI]` Required-param test: each event type fails to construct without its required params (data/analytics_event_taxonomy.csv, per-row `required_params`).
4. **Offline queue, with bounds** `[ARCH: NEW-Q19]`. The three constants `QUEUE_MAX_EVENTS`, `QUEUE_MAX_BYTES`, `QUEUE_FLUSH_TRIGGER` are declared in one config file, specified by the architect, and read by the tests:
   (a) **No-loss/no-duplication:** with `QUEUE_MAX_EVENTS` events enqueued and no network for 24 h, **all** enqueued events flush **in order** on reconnect with **zero duplicates**, verified by a per-event idempotency id; the smoke instance of this test uses 500 events / 24 h.
   (b) **Overflow:** enqueueing beyond `QUEUE_MAX_EVENTS` or `QUEUE_MAX_BYTES` drops **oldest-first** and emits a named counter `queue_dropped` carrying the dropped count — so the loss is visible rather than silent (otherwise "no loss" is false by construction and CM-R56.4 is violated; risks RK-32).
   (c) **Flush trigger:** the flush fires on `QUEUE_FLUSH_TRIGGER` (reconnect and/or app foreground as specified) and not otherwise, asserted by a negative test.
   (d) **Scope:** the queue carries **metrics only** — a static check asserts no entitlement, ledger or cap state is written through it.
5. Sampling test: `switch_toggled` at 10% and `perf_sample` at 1%; sampling verified against the replay log (data/analytics_event_taxonomy.csv:8,45).
6. Session test: `app_open` fires on foreground after a 30-minute gap; dedupe verified at 29 m and 31 m (data/analytics_event_taxonomy.csv:2).
7. `first_open` fires exactly once, including after reinstall with backup off (data/analytics_event_taxonomy.csv:3).
8. **`daily_started` QA step, against a truth source that exists:** the device's `seed` for a given dateKey equals the seed the CI `validate-dailies` job printed for that dateKey (CM-R46.1). This **[SUPERSEDES]** the taxonomy's "verify same seed as server-of-truth for date" (data/analytics_event_taxonomy.csv:14) — there is no server. See U-7; pending human confirmation under NEW-Q20.
> **Still untestable as stated → NEW-Q20:** `device_tier` derivation is undefined (now pinned by CM-R52.2's derivation rule once NEW-Q28 is answered); `rank_bucket` has no producer with no leaderboard backend (U-6); `restore_started` has a blank QA procedure (data/analytics_event_taxonomy.csv:33). `fail_reason` enum values are never listed in the CSV but are resolved by CM-R03 from specs/product_spec.md:251.

---

**CM-R44 — The four D7 fun-gate metrics must be computable from shipped instrumentation** · **MUST**
*As the person who has to fire or not fire the kill switch, I can compute all four numbers from the build the testers played.*
Rationale: gate metrics (EXECUTION_PLAN.md:137-141). Metric (ii) requires `level_started` with `attempt>1` **on a completed level**, which requires per-level completion state at event time — the taxonomy defines `level_started(level_id, mode, attempt, difficulty_target)` (data/analytics_event_taxonomy.csv:7) and defines no completion flag.

1. Metric (i): unprompted second-calendar-day opens are computable per tester from `app_open` + `first_open` with pushes disabled (data/analytics_event_taxonomy.csv:2-3).
2. **Metric (ii), against taxonomy delta TD-01** `[PIN NEW-Q36 for the shape]`: `level_started` carries a required completion-state param — `previously_completed: bool` or `completions_before: int` — added to `data/analytics_event_taxonomy.csv` in the same change, with CM-R43.1's count assertion updated accordingly (45 with a param; 46 if the separate-event shape is chosen). Criterion: over a scripted stream in which tester A replays a level they have won and tester B fail-retries a level they have never won, the metric-(ii) query returns **exactly one** qualifying event and it is tester A's. A second scripted stream with zero replays returns zero.
3. Metric (iii): median session length in levels is computable from `level_completed` grouped by session (data/analytics_event_taxonomy.csv:9).
4. Metric (iv): quit-without-retry after failure is computable from `level_failed` → `level_quit` vs `level_started(attempt+1)` (data/analytics_event_taxonomy.csv:10-11).
5. **Dry run before D5:** all four metrics are computed end-to-end on a synthetic event stream **before D5**, with the four numbers printed, so the gate cannot fail for instrumentation reasons. This is the catch-all for any remaining gap.
6. A named outside person confirms the tally before ADR-0007 is written (EXECUTION_PLAN.md:140-141).
> **The gate's power and its contamination are human calls, not instrumentation gaps** — NEW-Q38, risks RK-06/RK-07. If the human adopts a message-adjacency classification (critique KC-5), it needs a data source at gate time and must be published in BIP post 1 first.

---

**CM-R45 — Privacy classification and Data Safety parity** · **MUST**
Rationale: privacy classes `behavioral_no_pii / behavioral_ad / transactional / diagnostic` (data/analytics_event_taxonomy.csv:25-46); "answers must match actual app behavior — a mismatch is a policy violation" (data/google_play_checklist.csv:14); Data Safety form verified against device proxy capture before production submission (specs/onesignal_retention.md:329).

1. `[CI]` Every event row carries a privacy class; an unclassified event fails the build (data/analytics_event_taxonomy.csv).
2. Device proxy capture before production submit shows no data category leaving the device that is not declared — the capture covers **a forced-crash session as well as a happy-path session**, so crash/diagnostic payloads are in scope (specs/onesignal_retention.md:329; data/google_play_checklist.csv:14; risks RK-31).
3. Share card contains no PII and no username (none exists) (specs/product_spec.md:696; data/analytics_event_taxonomy.csv:40).
4. Raw store transaction ids never leave the device; only `txn_id_hash` is logged (specs/revenuecat_implementation.md:375-383).
5. Data Safety re-verified after any SDK change (data/google_play_checklist.csv:34).
> **Gap carried as a risk (RK-30), not silently converted into a criterion:** the OneSignal **tag** set (`payer_status`, `last_session`, `streak_days`, `tutorial_done`, `daily_last_done`) is a third-party data flow that this requirement does not enumerate. Extending the taxonomy artifact to cover tags — name, value domain, privacy class, destination — is a change the human commissions.

---
### 2.10 LiveOps

**CM-R46 — Daily seed pipeline: 90 dates pre-validated in CI, dated backup pool on device** · **MUST**
Rationale: CI job `validate-dailies` runs the generator + the exact `CatMetro.Domain` step function for the next 90 dates on every content/sim PR and nightly (specs/liveops_spec.md:51-54); salt-loop overrides written to `daily_overrides.json` and shipped in the build (specs/liveops_spec.md:55-56); app pre-validates today's + tomorrow's board at boot (solver-lite ≤200 ms) with a fallback to a dated backup pool of 30 hand-validated dailies (specs/product_spec.md:459).

1. `[CI]` `validate-dailies` runs on every PR touching content or sim and nightly; a failing date blocks merge, and the job prints the resolved seed per dateKey (the truth source CM-R43.8 compares against) (specs/liveops_spec.md:51-54).
2. `[CI]` Every generated board satisfies schema v2, `minActionWindowTicks ≥ 6`, solver-verified solvable, 3★ achievable within band slack (specs/liveops_spec.md:49).
3. Salt-loop test: seed(0) failure increments k deterministically; device-side loop is bounded at 250 ms with beam width 1k and produces the same k as CI (specs/liveops_spec.md:55-56).
4. Boot test: today's and tomorrow's boards are validated in ≤200 ms; on failure the dated backup pool serves a board and everyone still shares one board (specs/product_spec.md:459). **Runtime bounds validation against pathological boards is risks RK-34 (architect).**
5. **Weekday difficulty ramp** `[PIN NEW-Q21]`: generated `difficultyTarget` for each weekday equals the authored curve value **±0.05** across the next 90 dates, where the curve is the single committed file `config/daily_weekday_curve.json`. Two candidate curves are enumerated in §4.1 (liveops 0.35…0.75 vs product_spec 0.30…0.55); exactly one is committed. The same question asks whether the curve is a **frozen generator parameter** — if yes, a contract test asserts it unchanged through Sep 30 alongside CM-R11.7's "CM-DAILY-1" assertion.
6. Human spot-play of tomorrow's board each evening (~15 min/day) is a named daily ops task (specs/liveops_spec.md:69,154).

---

**CM-R47 — District Cup, weekly from ~Aug 31, async, no backend, anti-P2W** · **SHOULD**
Rationale: "District Cup weekly from ~Aug 31" (EXECUTION_PLAN.md:191) **[SUPERSEDES]** "from Week 5 ~Sep 21" (data/economy_sources_and_sinks.csv:10,20; data/ad_placement_map.csv:17; AUDIT_FINDINGS.md:326); Mon 17:00 → Sun 23:59 local; eligibility `highest_level >= 8`; medals are static solver-calibrated tiers, no percentile ranks (specs/liveops_spec.md:81-83); "any-rewind run caps at Silver — purchases can never buy Gold; enforceable in Domain layer via command log" (specs/liveops_spec.md:85); `leaderboard` flag OFF (specs/liveops_spec.md:77); cut-line step 2 is "District Cup round 1 slips a week" (EXECUTION_PLAN.md:197).

1. `[CI]` **Anti-P2W, against the chosen ranking model** `[PIN NEW-Q23]`: a Cup run whose command log contains any rewind is capped at Silver, enforced in Domain (specs/liveops_spec.md:85). "Silver" resolves per branch (§4.1):
   - **RK-static (solver-calibrated medals):** Silver = the static score threshold in the round's event JSON, generated from solver output; the criterion additionally asserts that **the first round's par table is produced with no prior telemetry** and is committed with the round.
   - **RK-percentile (prior-week buckets):** Silver = the prior-week percentile boundary. **Note for the human, not a decision:** with a leaderboard backend as a binding NON-GOAL (EXECUTION_PLAN.md:207) there is no cross-player data source to compute percentiles from; this branch requires naming one.
2. Window test: rounds open Mon 17:00 local and close Sun 23:59 local from a `startsAt`/`endsAt` window baked in event JSON — no remote flip (specs/liveops_spec.md:90-94).
3. Eligibility test: players below `highest_level` 8 never see the Cup entry (specs/liveops_spec.md:82).
4. **Reward set** `[PIN NEW-Q22]` — exactly one branch (§4.1). Under **both**: no gameplay power, no purchase interaction, and Gold ×3 adds gold trim.
   - **CR-livery-only:** finishing all 3 routes at any medal grants exactly the round livery and **zero** currency — asserted by a test that the reward payload contains no ticket/currency field (specs/liveops_spec.md:84).
   - **CR-livery-plus-150:** grants the livery **and exactly 150 tickets**, logged as `ticket_earned(source=cup_participation)`, and the grant counts against the CM-R49.2 daily faucet cap (data/economy_sources_and_sinks.csv:10).
5. `weekly_event` flag kills the Cup without a store update (specs/liveops_spec.md:90-94).
6. Two rounds always baked ahead (N+1 finished, N+2 draft); the permanent "Classic Cup" fallback auto-serves if the buffer empties (specs/liveops_spec.md:90-94,163).

---

**CM-R48 — Feature flags and kill switches, ordered by latency** · **MUST**
Rationale: seven levers, ordered by response latency: RC empty offering (minutes) → OneSignal pause/broadcast IAM → AdMob console pause → baked runtime flags (`daily_enabled`, `weekly_event`, `share_card`, `ads_enabled`, `paywall_placements`, `leaderboard` OFF) → content date windows → `daily_overrides.json` → Play staged rollout 20%→50%→100% over 72 h, halt = biggest red button (specs/liveops_spec.md:217-227). The hosted `ops.json` kill-file is **CUT from 1.0** (specs/liveops_spec.md:227).

1. **Per-flag absence table.** For each flag: with the flag OFF the app boots and completes L001, and the named surface is **absent** (one test per row):

   | Flag | Exact surface that must be absent when OFF |
   |---|---|
   | `daily_enabled` | Daily tile absent from Home; `catmetro://daily` routes Home; no `daily_started` emittable |
   | `weekly_event` | Cup entry absent from Home; `catmetro://event/{id}` routes Home with "event ended"; no Cup run constructible |
   | `share_card` | Share CTA absent on Daily results, 3★ win and Cup result; no RenderTexture composite constructed |
   | `ads_enabled` | All five rewarded rows absent; zero ad SDK calls issued (CM-R33.1) |
   | `paywall_placements` | No system-initiated paywall presents; **RC still initializes** (criterion 6) |
   | `leaderboard` | No rank display on the Daily results screen; `rank_bucket` not rendered anywhere |

2. `[CI]` `leaderboard` ships OFF (specs/liveops_spec.md:77,217-227).
3. Empty-offering drill: pointing a placement at `offering_dark` removes the surface within one app session (specs/liveops_spec.md:229).
4. **Structural isolation, asserted on the build rather than on a schedule:** `[CI]` no concrete `CatMetro.Integrations.*` type is referenced from `CatMetro.Domain` or `CatMetro.Application` except through an interface declared outside `Integrations` (asmdef boundaries + static check); **and** a stub implementation of every `Integrations` interface compiles and boots the app to Home with the corresponding flag off. Separately, **a recorded no-op drill** (adapter name, date, elapsed wall time) exists for at least one adapter before the first production upload. *(This replaces "a crashing SDK adapter can be no-op'd in a same-day hotfix", which asserted a schedule capability rather than a property of the build.)* (specs/liveops_spec.md:241-242)
5. Pre-written emergency assets exist in `/ops/emergency/` (outage IAM, refund-wave macro, rollout-halt checklist, `offering_dark`) before the first production upload (specs/liveops_spec.md:229).
6. `paywall_placements` dark still initializes RC — the flag gates presentation, not initialization (specs/revenuecat_implementation.md:130-147).

---

**CM-R49 — Comeback ladder: every path gives, never asks** · **SHOULD**
Rationale: "Every comeback path gives, never asks; purchases only in the rewind sheet's secondary row; grants idempotent per calendar day; grants share one daily cap (max 2× normal daily faucet), ledger-logged via `ticket_earned.source`" (specs/product_spec.md:489-493); 48 h / 7 d / 14 d rungs in ONE journey (specs/product_spec.md:483-485); "Never: comeback discounts, 'we miss you' IAP offers, fake urgency" (specs/liveops_spec.md:138).

1. **Idempotence, against a concrete ladder** `[PIN NEW-Q24]`: applying the rung-*R* grant twice on the same local calendar date credits the authored grant **exactly once** — asserted per rung for all three rungs. The authored grant per rung is the single committed file `config/comeback_ladder.json`; the two candidate ladders (product_spec vs liveops) are enumerated in §4.1 with their exact contents, and exactly one is committed.
2. **Daily faucet cap, with a numeric baseline** `[PIN NEW-Q7 + NEW-Q24]`: total tickets granted on a comeback day ≤ **2 × `DAILY_FAUCET_BASELINE`**, where `DAILY_FAUCET_BASELINE` is declared in `config/economy_defaults.json` and is derived from the authored per-level ticket table (CM-R09.6) as *(the Daily Line award) + (the median campaign-level first-clear award)*. The test reads the constant, computes the day's grants from `ticket_earned` and asserts the inequality; a second test asserts the constant equals the derivation from the committed table, so the two cannot drift.
3. Commerce-silence test: 48 h and 7 d branches present zero commerce; the re-entry session is commerce-silent for 48 h (specs/monetization_spec.md:307-323).
4. 14 d rung is hard-final: `winback_optout` set in code; no further messages ever (specs/monetization_spec.md:307-323; specs/onesignal_retention.md:116-121).
5. **Streak-lapse UX and `streak_saver` eligibility, one window and one floor** `[PIN NEW-Q25]`: the badge reads "paused", not "lost", for exactly `STREAK_PAUSE_WINDOW` and then resets honestly; `streak_saver` is offered only when the lapse is within `STREAK_PAUSE_WINDOW` **and** the pre-lapse streak ≥ `STREAK_SAVER_FLOOR`; a lapse one minute past the window is ineligible and a streak one below the floor is ineligible (two negative tests). Candidates: window **48 h** (data/ad_placement_map.csv:6) or **72 h** (specs/liveops_spec.md:135); floor **≥2** (data/ad_placement_map.csv:6) or **≥3** (specs/onesignal_retention.md:85). The same question fixes the free grace token's accrual window and its stacking rule with `streak_saver` (U-8).
> **NEW-Q26 still open:** if `theme_rental` is cut (cut-line step 1), does the ≥7 d comeback rental token survive? (specs/liveops_spec.md:133 vs EXECUTION_PLAN.md:197)

---

### 2.11 Store

**CM-R50 — Play listing: honest positioning, 13+, no-forced-ads stated plainly** · **MUST**
Rationale: "Full listing (no-forced-ads positioning, 13+)" (EXECUTION_PLAN.md:192); short desc ≤80 / full ≤4000, **both stating "no forced ads" plainly** (data/google_play_checklist.csv:27); target audience 13+ only, no under-13 group; "art must read premium tabletop-diorama, not cartoon-toy — child-appealing art can get a 13+ listing rejected; second-pair-of-eyes asset review required" (data/google_play_checklist.csv:23); listing must say "optional reminders" and may never promise "no notifications" (specs/onesignal_retention.md:314-315).

1. Copy test: short description ≤80 chars and full description ≤4000 chars, both containing an explicit no-forced-ads statement (data/google_play_checklist.csv:27).
2. Asset set complete: icon 512×512 32-bit PNG, feature graphic 1024×500, 2–8 phone screenshots at 1080×1920, 7in/10in tablet screenshots (data/google_play_checklist.csv:27; data/device_test_matrix.csv:7).
3. **13+ declaration with a scored rubric** (analyst-authored rubric contents, A-25): target audience declared 13+ with no under-13 group; **and** a second reviewer scores the art against the committed rubric `docs/prd/art-review-rubric.md`, whose rows enumerate child-directed signals drawn from Play's target-audience/Families policy — e.g. infantile rounded character proportions; primary-colour-only palette; toy/sticker/reward-chart motifs; nursery or bubble typography; anthropomorphic playroom props; cartoon "kid-show" UI ornamentation. The reviewer marks **each row present/absent with a one-line justification**, then signs and dates the artifact. **Submission is blocked while any row is marked present.** The criterion checks: the rubric file exists, every row is marked, the artifact is signed and dated, and no row is marked present. *(This replaces the unscored "signs off that the art does not read child-directed".)*
4. Listing states "optional reminders" and contains no "no notifications" promise (specs/onesignal_retention.md:314-315).
5. Ads declaration = Yes, AD_ID permission present, rewarded/opt-in only, with the listing line "ads only when you ask for them" (data/google_play_checklist.csv:20). **Additionally** `[PIN NEW-Q2]`: the listing contains **no present-tense claim that an entitlement removes an ad surface** (a `[CI]` copy check greps for the superseded sentence); if the human elects forward-binding language instead of deletion (U-1), the exact sentence is recorded in the listing-copy artifact and the copy test asserts it verbatim.
6. Privacy policy live and reachable, with the **identical** URL in the listing and in-app Settings; the host is the one frozen by D-2/NEW-Q27, not assumed (data/google_play_checklist.csv:19; risks RK-28).
7. `[CI]` **Package identity, single-valued:** package id, Unity `applicationIdentifier` and the RevenueCat Android app entry all read the id frozen by D-2 (`com.catmetro.game` per EXECUTION_PLAN.md:146), **and** a repo-wide grep finds zero occurrences of any other package id. Six files currently read the superseded `io.catmetro.game` (data/google_play_checklist.csv:6,9,30; data/device_test_matrix.csv:1,9; specs/revenuecat_implementation.md:53; data/revenuecat_configuration.csv:7; data/entitlement_map.json:6) — the criterion fails until D-2 is answered and they are reconciled. **Blocked on D-2.**
8. All App content sections complete — Play blocks production release while any section is incomplete (data/google_play_checklist.csv:34).
9. Managed publishing ON before submitting 1.0 (data/google_play_checklist.csv:29); US availability from the first production release (data/google_play_checklist.csv:30). **The "broad availability, no staged country rollout" clause is BLOCKED on NEW-Q45** — it contradicts the US-availability line as written, and broad availability with `AD_ID` and five ad surfaces requires the consent posture that does not exist (risks RK-11).
> **Identity cascade (NEW-Q27):** support email is `support@catmetro.io` (data/revenuecat_configuration.csv:43) vs `support@catmetro.com` (data/google_play_checklist.csv:32); webhook host is `rc-hooks.catmetro.io` (data/entitlement_map.json:380-387). Each must be re-derived from the frozen identity, **and domain ownership is a blocking sub-decision of D-2** (risks RK-28).

---

**CM-R51 — Platform compliance: target API 36, Billing 8+, 16 KB pages, min API 25** · **MUST**
Rationale: target API 36 required from Aug 31 2026 (data/google_play_checklist.csv:10); Billing 8+ required from Aug 31 2026, satisfied transitively by purchases-unity 9.7.0 shipping Play Billing 8.3.0; **do NOT install Unity IAP** (duplicate BillingClient) (data/google_play_checklist.csv:11; specs/revenuecat_implementation.md:31); 16 KB page size mandatory for targetSdk 35+ since Nov 2025 (data/google_play_checklist.csv:13); min API **25** (EXECUTION_PLAN.md:20) **[SUPERSEDES]** minSdk 24 in data/device_test_matrix.csv:1, data/google_play_checklist.csv:7, specs/revenuecat_implementation.md:26.

1. `[CI]` Manifest check: `targetSdk=36`, `minSdk=25`; a plugin downgrade fails the build (data/google_play_checklist.csv:10; EXECUTION_PLAN.md:20).
2. `[CI]` Exactly one `billingclient` entry in EDM4U resolved deps after every SDK change; exactly one EDM4U copy at 1.2.188 (data/google_play_checklist.csv:11; specs/onesignal_retention.md:318).
3. `[CI]` `zipalign -c -P 16 -v` exits 0 with no BAD lines; every packaged `.so` has 16384-byte LOAD alignment (data/device_test_matrix.csv:6).
4. 16 KB AVD: app boots and completes L001 with zero `UnsatisfiedLinkError`/SIGSEGV; billing, ads and push init callbacks fire (data/device_test_matrix.csv:6).
5. `[CI]` R8-minified build boots with keep rules for Play Billing, GMA and OneSignal receivers (data/google_play_checklist.csv:7; specs/onesignal_retention.md:318-329).
6. `[CI]` Version pin assertion: Unity 6000.3.16f1 (do NOT move to 6000.3.17f1+ — Gradle 9/AGP 9 breakage), purchases-unity 9.7.0, OneSignal 5.3.2, GMA 11.3.0, EDM4U 1.2.188 (EXECUTION_PLAN.md:20; specs/revenuecat_implementation.md:18-36). **No SCA/advisory/license pass has been run — risks RK-36.**

---

**CM-R52 — Device matrix gates and performance budgets** · **MUST**
Rationale: budgets and per-tier pass criteria at data/device_test_matrix.csv:1,3-9; crash-free ≥99.5% is a never-cut line (EXECUTION_PLAN.md:200-201); emulator rows are correctness-only, never perf evidence (data/device_test_matrix.csv:1).

1. Mid tier (Pixel 6a, primary target): ≤16.6 ms p50 / ≤22 ms p95, cold start ≤3.5 s, ≤120 draw calls, PSS ≤350 MB, AAB ≤60 MB (data/device_test_matrix.csv:4).
2. **Low tier, with the fixture precondition defined.** `device_tier` is derived by a **named committed rule**: a static allowlist keyed on (SoC model, total RAM), with a documented fallback band on total RAM alone for unlisted devices; a unit test asserts the derived tier for three named reference devices, one per tier. Then `[PIN NEW-Q28]`:
   - **T-auto:** on the low-tier reference device the 30 Hz cap **engages automatically at boot** — asserted from the frame-time log within the first 5 s of a cold start with no user action;
   - **T-setting:** the cap is a user setting that **defaults ON** for devices the derivation classifies low, persists across restart, and is asserted ON before measurement.
   Under **both**: at max wave, frame time ≤33.3 ms p50 / ≤40 ms p95, **no frame >66 ms**, cold start ≤5.0 s, and a 30-level pass with 0 crashes and 0 ANRs (data/device_test_matrix.csv:3). The same derivation rule supplies the `device_tier` analytics param (NEW-Q20).
3. High tier: ≤8.3 ms p50 / ≤11 ms p95 at 120 Hz; no UI inside gesture-nav or cutout insets (data/device_test_matrix.csv:5).
4. Tablet: portrait lock honoured on rotate with **no activity restart and no sim state loss** (data/device_test_matrix.csv:7).
5. Crash-free sessions ≥99.5% across the closed-test window, 0 ANRs (data/device_test_matrix.csv:4).
6. `[CI]` CI smoke on GitHub Actions AVD: install, boot, L001 win, forced overflow fail, cause-first sheet, instant retry, replay-hash assertion, save migration v1→v2, `catmetro://daily` deep link, RC Test Store purchase of `cm_all_access` granting `all_access` in a mock harness, 5-min monkey fuzz (2000 events, throttle 200, 0 crashes/ANRs) — **a red smoke run blocks merge** (data/device_test_matrix.csv:9). **This job must run with a debug key and zero release secrets — risks RK-33.**
7. Pre-launch report reviewed for every closed-track upload; any native crash on API 35+ is treated as a 16 KB regression until proven otherwise; the RC build shows zero crashes/ANRs across the crawler set with a written disposition per remaining warning (data/google_play_checklist.csv:28).
> Foldable (P2) sits below the auto-cut line and is skipped outright if it threatens any P0 gate (data/device_test_matrix.csv:8).

---

**CM-R53 — Settings: reset progress, help/refund route, in-app review discipline** · **MUST**
Rationale: no accounts, so account deletion is N/A — but "product requirement: Settings > Reset progress control that clears local save, RC anonymous ID and OneSignal tags" (data/google_play_checklist.csv:24); support + refund route stated in Settings > Help, `refund rate >3% of orders = R-12 tripwire` (data/google_play_checklist.csv:32); in-app review rules: no visible CTA, never after failure, never for a reward, quota may silently no-op, never branch UI on the callback, single call site verified in code review (data/google_play_checklist.csv:25).

1. Reset test: the control clears the local save, rotates the RC anonymous id and clears OneSignal tags; the app returns to a first-open state (data/google_play_checklist.csv:24). **Named consequence carried as a risk, not patched here (RK-18 / SEC-03):** rotating the id orphans real purchases, or — if implemented as "rotate but keep entitlements" — makes the Settings copy and the Data Safety deletion answer false. Which behaviour ships, and the single sentence that Settings copy / Data Safety answer / behaviour must all share, is a human decision.
2. `[CI]` Single-call-site test: exactly one call site invokes the Play in-app review API (data/google_play_checklist.csv:25).
3. **Review trigger, one predicate** `[PIN NEW-Q29]` — exactly one branch (§4.1). Under **both**: the 30-day cooldown is persisted and asserted (a second attempt at day 29 no-ops, at day 31 proceeds); **never** invoked in a session in which `level_failed` fired; **never** invoked in a session in which `paywall_viewed` or `rewarded_ad_started` fired.
   - **RV-district:** invoked on the `level_completed` event of a level whose district thereby becomes complete (data/google_play_checklist.csv:25).
   - **RV-stars:** invoked on `level_completed` with `stars=3` AND `session_count ≥ 5` AND the current session crash-free (data/onesignal_journeys.csv:14).
4. UI never branches on the review callback (data/google_play_checklist.csv:25).
5. Settings > Help states the support email and the Google Play refund route (data/google_play_checklist.csv:32). The email address is the one frozen by NEW-Q27.

---

### 2.12 Growth

**CM-R54 — Share card and challenge links, no PII** · **SHOULD**
Rationale: on-device RenderTexture composite 1080×1350 with wordmark, date/level name, score+stars, streak badge, route-ribbon, theme colors, short link; no PII/username (none exists) (specs/product_spec.md:696); Android share sheet only — no in-app social, friends list, or chat (specs/product_spec.md:698); `share_card` flag exists (specs/liveops_spec.md:217-227).

1. Composite test: output is 1080×1350 PNG containing the required elements and zero PII (specs/product_spec.md:696; data/analytics_event_taxonomy.csv:40).
2. Surfaces test: offered post-Daily (primary), on any 3★ win (secondary), and on a Cup result (specs/product_spec.md:696).
3. Share target test: only the OS share sheet is invoked; no in-app social surface exists (specs/product_spec.md:698).
4. OEM fallback: the card carries a human-readable code ("Route CM-0824") and Home has a manual code-entry field (specs/product_spec.md:706).
5. `challenge_opened` logs `source(link|code)`; invalid/expired codes fall back to Home (data/analytics_event_taxonomy.csv:41). **Score values reaching the card from a deep link are untrusted input — risks RK-27.**
> Target: share rate ≥8% of daily completions (specs/product_spec.md:698-699) — self-set; denominator = daily completions.

---

**CM-R55 — Capture rig: marketing assets reproducible from replay logs** · **SHOULD**
Rationale: "capture rig from replay logs" (EXECUTION_PLAN.md:193); the 6-second ad is a single uninterrupted gameplay capture, "reproducible from a replay file via Capture scene; 3 seed variants by Sep 5" (specs/product_spec.md:60-73); no in-house mp4 replay export — the static card + deterministic replays feed the Capture rig (specs/product_spec.md:783-794).

1. Reproducibility test: the same replay file re-rendered twice produces frame-identical output (depends on CM-R01).
2. The 6-second capture is one uninterrupted take containing tap → flip → delivery chain → near-miss save → logo (specs/product_spec.md:60-73).
3. **Dated deliverable:** 3 seed variants exist on disk by **window-basis day 36** (= 2026-09-05 under the unratified Aug-1 basis; the derived calendar date is written into the ops log on ratification of NEW-Q37). The criterion is evaluated on that date: the three files either exist or they do not. `[PIN NEW-Q37]` (specs/product_spec.md:60-73; the date coincides with the critique's KC-8 checkpoint, risks RK-15.)

---

**CM-R56 — Daily BIP post, 56/56 — never cut** · **MUST**
Rationale: "Daily BIP post (56/56)" (EXECUTION_PLAN.md:193); "Never cut: … the daily BIP post" (EXECUTION_PLAN.md:200-201); BIP post 1 carries the pre-registered fun-gate bar before data exists (EXECUTION_PLAN.md:136).

1. Post count: one post per calendar day of the window, evidenced by post links in the ops log.
2. BIP post 1 contains the four gate metrics verbatim, published before any tester data exists (EXECUTION_PLAN.md:136-141). **Any change to the gate — including a confirmation rule or a message-adjacency reporting rule (NEW-Q38) — must appear in this post or not at all.**
3. Every published rate carries its denominator and vintage (specs/onesignal_retention.md:299; specs/liveops_spec.md:235). Fun-gate figures additionally carry the provenance statement that they come from a client-authoritative event stream with 12 known testers (risks RK-32).
4. No BIP post contains a number that our own files falsify (locked honesty rule; AUDIT_FINDINGS.md:498-505). This rule binds the production-access application answers and the Devpost submission too.
5. No BIP post or screenshot contains a promo-code string (risks RK-26).

---

**CM-R57 — Devpost submission package** · **MUST**
Rationale: submit early, edit continuously — the Devpost submission goes LIVE by ~Sep 15 and is edited to the freeze (EXECUTION_PLAN.md:163-166); submission tactics bound by the judging funnel (EXECUTION_PLAN.md:167-174).

1. The category-specific question is answered for EVERY targeted category (empty = not judged in it) (EXECUTION_PLAN.md:168-169).
2. **Entered categories equal the committed slate:** entries are exactly the P0 slate plus Design and Grand, as enumerated in the committed targeted-category list; **no other category is entered**. Asserted by comparing the live Devpost entry list against that file before the freeze. *(This replaces the undefined "only genuinely-fitting categories".)* (EXECUTION_PLAN.md:169-170)
3. The video's **first 2 minutes** contain the elevator pitch, the app running on device, and an explicit statement of targeted categories (EXECUTION_PLAN.md:170-171).
4. The submitted package name exactly matches the live app (RC SDK is verified programmatically against it) (EXECUTION_PLAN.md:172-173).
5. **Build/video parity, self-observable:** the version code of the build in the submitted Play listing equals the production version code live at the Sep 26–30 freeze, **and** the build shown in the demo video reports that same version code on screen (captured in the video or in a paired screenshot with the same code visible). *(This replaces "an RC advocate downloads it before winners are finalized", which is an uncontrolled third-party action.)* (EXECUTION_PLAN.md:173-174)
6. Promo codes + redemption guide are attached in the free-trial/promo-code field — a judge-only field, never a public one (data/revenuecat_configuration.csv:55; risks RK-26).
7. Submission is live by ~Sep 15 and edited until the Sep 26–30 freeze (EXECUTION_PLAN.md:163-166; specs/liveops_spec.md:114).

---

### 2.13 UNTESTABLE AS STATED — returned to the human for rework

Statements in the corpus that could not be converted into an independently-checkable criterion. They are **not** dropped; each names the human action that unblocks it. The evaluator concurred with all ten.

| # | Statement | Why untestable | Named human action | Source |
|---|---|---|---|---|
| U-1 | "cm_all_access removes all non-rewarded ad surfaces permanently" | Asserts a change to a set that is **empty by design** (zero forced ads) — no observable differs between owner and non-owner | **NEW-Q2:** rewrite as forward-binding language with no present-tense effect (e.g. "if we ever add a non-rewarded ad surface, All Access owners never see it") **or delete it from the listing**. Blocks CM-R50.5 | google_play_checklist.csv:20 vs entitlement_map.json:63-93 |
| U-2 | ">2 refunded consumable purchases → pack rows hidden for that account permanently" | No account identity under anonymous RC ids; no mechanism by which the client learns of a refund | **NEW-Q47:** answer "do not implement" and delete (threat-model recommendation, RK-23), **or** define identity as the RC anonymous app-user-id (noting Settings ▸ Reset progress rotates it) plus CustomerInfo-refresh-on-boot as the learn path — which makes it a PII flow needing a `docs/security/` entry and a Data Safety declaration first. **Already split out as CM-R30-D; blocks nothing** | entitlement_map.json:395-399 |
| U-3 | `payer_thanks` "one-off scheduled send within 24 h of first purchase" | Names no delivery mechanism; there is no server; must fit the locked 6-step budget | **NEW-Q10:** name the surface. Three branches are authored at CM-R29.4 — the human picks one and that criterion becomes ordinary | monetization_spec.md:336 |
| U-4 | Anomaly alerts (>10 packs/day/user; >5 packs/day/user) | Thresholds without an alerting channel, evaluation cadence or recipient — nothing to observe firing | **NEW-Q47:** name the channel and cadence (e.g. a weekly manual query over the analytics export with a written disposition), **or** move the rule to the post-launch backlog | monetization_catalog.csv:6-7; entitlement_map.json:327-338 |
| U-5 | "D7 retention ≥12% (top-10% band)" | Labeled an aspiration in-source; no denominator process; n=12 explicitly too small to support the statistic | **Keep as a labeled aspiration, excluded from acceptance criteria.** The D7 fun gate (CM-R44) remains the only retention-adjacent gate | product_spec.md:362-366, :660-661 |
| U-6 | `rank_bucket` on `daily_completed` | No producer can exist — a leaderboard backend is a binding NON-GOAL | **NEW-Q20:** drop `rank_bucket` from the taxonomy for 1.0 (adjusting the required-param rows), **or** define a purely local derivation and say so in the row | analytics_event_taxonomy.csv:15 |
| U-7 | `daily_started` QA "verify same seed as server-of-truth for date" | Names a truth source that does not exist | **NEW-Q20:** confirm the replacement already authored at CM-R43.8 — the CI `validate-dailies` output for that dateKey (CM-R46.1) | analytics_event_taxonomy.csv:14 |
| U-8 | "free grace token 1/month" for streak repair | Calendar-month vs rolling 30 days unstated; stacking with `streak_saver` undefined | **NEW-Q25:** pick one accrual window and state the stacking rule (e.g. "refills on the 1st of each local calendar month; using `streak_saver` on a lapse consumes that lapse, so the token is not also spent") | product_spec.md:487 vs liveops_spec.md:64 |
| U-9 | Wildcard `wild` runtime semantics | Defined only as "accepted anywhere" — player-assigned vs auto-resolve is unstated, which changes both the sim step and the solver | **NEW-Q35:** specify the resolution rule in the tick order. Both branches are authored at CM-R02.7; the human picks one. Also undercuts CM-R06's fourth mechanic until answered | level_schema.json:59-78 vs product_spec.md:222 |
| U-10 | "10% holdout on J2 only" | Presumes a holdout-assignment mechanism that does not exist on the OneSignal Growth plan | **NEW-Q31 (extended):** implement a client-side deterministic hash-bucket holdout (and add a criterion asserting bucket stability across restarts), **or** drop the holdout and label J2 results uncontrolled | onesignal_retention.md:298 |

---

## 3. NON-GOALS (verbatim, binding)

From `docs/plan/EXECUTION_PLAN.md:203-211`, §3.3 "Non-goals (do not build, do not spec further in-window)":

> Subscriptions/season pass · energy/lives · interstitials/banners/app-open · loot boxes ·
> multiplayer/leagues backend · UGC editor · free-form track drawing · physics as game truth ·
> narrative campaign · pre-registration listing · levels 41–60 · Replit/KMP/Noise detours ·
> paid UA before an organic creative + D1-retention floor proves out (and never >$50/day without
> a written ADR) · Galaxy Store (P2, only if trivially free) · Stripe web funnel (P2, go/no-go
> Sep 4, prebuilt estimate ≤8h or NO-GO) · experiments beyond PW01, PW06, send-time, and 2 ASO
> listing iterations (the other ~28 rows are post-event backlog).

Reinforcing pre-refusals already recorded in the specs (specs/product_spec.md:783-794): level editor/UGC · realtime multiplayer · endless/procedural marathon at launch (the Daily **is** the procedural mode) · free-draw track building · meta city-builder · cat gacha · custom accounts/cloud-save backend (Play auto-backup + RC restore instead) · 3D camera orbit/zoom · story campaign/cutscenes · in-house mp4 replay export.

Commerce-side pre-refusals with rationale on record (specs/monetization_spec.md:20-21,490-507; data/monetization_catalog.csv:8-16): theme bundle (decoy confusion — "All Access IS the bundle") · starter pack · ticket/currency packs · streak-saver IAP (monetizing loss-aversion) · chapter packs · audio pack · cosmetic club monthly · season pass · $19.99 founder variant. **No subscription SKU, no sub code paths, no "sub-ready" scaffolding** (specs/monetization_spec.md:492,503).

> **Consequence worth stating once:** "Play auto-backup + RC restore instead of cloud save" is a NON-GOAL with a security consequence — the entitlement cache and consumable ledger travel in auto-backup unless excluded (risks RK-17). No criterion covers it today.

---
## 4. ASSUMPTIONS REGISTER

Each row is an assumption this PRD rests on. "Falsifier" = the cheapest observation that would prove it wrong. **A-21…A-29 were authored by this agent during the repair round** — they are measurement protocols, not product decisions, and the human may overrule any of them without changing what the requirement means.

| ID | Assumption | Basis | Falsifier | Owner |
|---|---|---|---|---|
| **A-00** | **`state/mode=sprint` is operative.** The mode file (state/mode:5, human-set 2026-08-02, recorded at state/PROJECT_STATE.md:25) is the authority; EXECUTION_PLAN.md:520-522 records "Stakes mode: **standard**" with sprint as "the named fallback lever". **The file wins.** Sprint **prices ceremony** (proportional testing per `evals/mode-policy.json`, agent-review leg priced by `scripts/forge-risk.sh`), it **never moves the enforcement floor**: human merge gate, immutable paths, TDD-for-Domain, and every `[CI]` criterion above stand at full strength. Graduation to `production` is required **before any monetization code merges** (state/PROJECT_STATE.md:10). | state/mode:5; state/PROJECT_STATE.md:10,25; EXECUTION_PLAN.md:520-522; AGENTS.md hard rules 1,7 | A human-authored commit changing state/mode | Human |
| A-01 | All dates in the specs are anchored to the **best-case** Aug 24–28 launch. Planning basis is P50 Sep 1–2, P80 Sep 12–16, with submit-on-grant. **The basis itself is unratified — every dated criterion in this PRD is expressed relative to it (NEW-Q37).** | EXECUTION_PLAN.md:123-129 | Production access granted earlier/later than the P50 band | Human |
| A-02 | The 23-agent audit's 113/116 verification (dated 2026-07-31) still holds on 2026-08-02. **Not re-verified in this session** — but the venture critique re-checked three perishable pins on 2026-08-02 and all three held. | AUDIT_FINDINGS.md:4-12,20-23,179; venture-critique.md header table | Any re-fetch that contradicts a CONFIRMED claim | Human |
| A-03 | The Phase-0 amendment pass has not landed, so every specs/ and data/ citation above is pre-amendment and may still carry a §5 defect. | EXECUTION_PLAN.md:227; AUDIT_FINDINGS.md:297-337 | The amendment PR merging | Agent + human |
| A-04 | RC plan tier (Pro vs free) is unknown until the D1-02 check; it forks Experiments, Targeting redundancy and Funnels. This PRD assumes **neither** and carries both branches. | data/paywall_experiments.csv:2; data/revenuecat_configuration.csv:6 | The Day-1 dashboard check | Human |
| A-05 | RC Ads beta grant latency is unbounded; `ads_enabled` may ship OFF (Model A). CM-R33 covers both states. | specs/monetization_spec.md:90-92; specs/revenuecat_implementation.md:24 | D10 / D14 gate outcome | Human |
| A-06 | The D7 fun gate is the **only** gate definition. The spec's other tester exercises (5-tester beat scoring, "≥5 testers replayed voluntarily", fat-finger 5 testers) survive as supplementary checks, not as the gate. | EXECUTION_PLAN.md:134-141 **[SUPERSEDES]** specs/product_spec.md:196,287,336,758 | — | Human |
| A-07 | RC C# API names in `revenuecat_implementation.md` are "directionally correct, not copy-paste-guaranteed"; the async surface may be callback-only. No SDK API in this PRD is asserted as verified. | specs/revenuecat_implementation.md:331-337,515 | Reading the 9.7.0 package source | Architect |
| A-08 | Purchase consumption mechanics (who calls consume, when) are unresolved between specs/revenuecat_implementation.md:56 and D1-15. **If consumption is client-triggered and non-atomic with the ledger write, risks RK-19/RK-20 change shape.** | specs/revenuecat_implementation.md:56 | 9.7.0 source | Architect |
| A-09 | OneSignal Growth is free for 3 months via the Ship Kit perk. **[SUPERSEDES]** "$19/mo". Custom events require SDK 5.2.0+ **and a paid plan** — a silent downgrade breaks J1/J3. | locked constraints; specs/onesignal_retention.md:18,319 | Plan status check | Human |
| A-10 | Journey-builder capabilities (single entry/re-entry combo for J1; tag-update steps for `lapse_final_sent`) are unverified; both carry client-side fallbacks. | specs/onesignal_retention.md:94-95,139-140 | Building the journeys | Human |
| A-11 | Play fee model = 10% first $1M + 5% billing fee = **15% effective**, verified 2026-07-31; use 15% in every revenue model. | data/google_play_checklist.csv:5 | Console/policy re-check | Human |
| A-12 | Play promo-code quarterly quota "~500" is **UNVERIFIED** and must be re-checked in Console before publication. | data/revenuecat_configuration.csv:55 | Console check | Human |
| A-13 | AdMob rewarded eCPM band $15–30 US carries vintage Tenjin Q2'24 / Appodeal Q4'24 — **external benchmark, must ship with its vintage**, never as our number. | specs/liveops_spec.md:235 | — | Human |
| A-14 | GameAnalytics 22/4/0.7 are **all-genre medians, not puzzle figures**; no doc may claim otherwise. | EXECUTION_PLAN.md:160-162 | — | Human |
| A-15 | The 2025 grand-winner calibration (1,750 payers / 17k users ≈10%) is an **aspiration**, and all self-set targets are labeled as such. | specs/monetization_spec.md:513-536,119,530 | — | Human |
| A-16 | Every experiment at our scale is **DIRECTIONAL, not significant**; write-ups must say so. PW01 needs ~3.5k views/arm for significance vs realistic low hundreds. | data/paywall_experiments.csv:4,7 | — | Human |
| A-17 | Gradle 8.13 / AGP 8.10.0 pin appears only in a data file; consistent with the locked no-AGP9 rule but not independently stated in EXECUTION_PLAN. | data/device_test_matrix.csv:1; EXECUTION_PLAN.md:20 | Reading EXECUTION_PLAN pins | Architect |
| A-18 | 12-tester/14-day closed-test rule applies — **unless D-1 finds the account predates Nov 13 2023, in which case the entire gate is bypassed**. | EXECUTION_PLAN.md:217; AUDIT_FINDINGS.md:511-517 | The 2-minute Console check | Human |
| A-19 | There is no `district` field in the level schema despite district-based unlock being locked; L901–L910 breaks any implicit id-range mapping. | AUDIT_FINDINGS.md:329; data/level_schema.json:6-25 | Schema amendment | Architect |
| A-20 | Priority labels conflict between artifacts (supporter/themes P0 in the catalog vs P1 elsewhere); this PRD uses the EXECUTION_PLAN scope table, which lists all commerce as in-scope at 1.0. | AUDIT_FINDINGS.md:319; EXECUTION_PLAN.md:187 | Human ruling | Human |
| **A-21** | **Input measurement protocols (analyst-authored).** Mis-taps are counted over a **fixed scripted 200-tap sequence per tester, 5 testers, 1000 pooled taps**, with a mis-tap defined as a tap outside the scripted junction's hit zone in the input log (CM-R07.5). Perceived latency is operationalized as **tap-down → first frame in which the lever sprite differs from its pre-tap state**, from a ≥240 fps capture or the instrumented frame log, p95 over 100 taps (CM-R07.2). | Repair round; the corpus gives thresholds without denominators or instruments | The human names a different denominator, tap count or instrument | Human may overrule |
| **A-22** | **Save-integrity invariants SI-1…SI-7 and the ≤50 MB low-storage threshold (analyst-authored).** "Consistent save" and "integrity intact" had no definition; SI-1…SI-7 is the definition all three CM-R05 criteria now share. | Repair round; specs/product_spec.md:712-715; data/device_test_matrix.csv:3 | The human names a different free-space threshold or adds/removes an invariant | Human may overrule |
| **A-23** | **Attribution ambiguity predicate (analyst-authored):** ≥2 distinct candidate causal routing decisions within the 24 ticks preceding the failure whose individual removal each independently averts it; plus the three named ambiguous fixtures (CM-R15.3). | Repair round; specs/product_spec.md:338 gives the behaviour but never defines the trigger | The human defines ambiguity differently, or the architect finds the removal test intractable at 24 ticks | Human may overrule |
| **A-24** | **Mute-legibility checklist MUTE-01…MUTE-05 and the ring-cancellability budget (≥12 of 16 ticks)** (analyst-authored). Replaces "legible with audio disabled" and "enough slack that any tap saves it" (CM-R17.3, CM-R17.4, CM-R18.3). | Repair round; specs/product_spec.md:280,674 | The human adds a P0 signal to the checklist or changes the slack budget | Human may overrule |
| **A-25** | **Child-directed art rubric contents (analyst-authored):** the enumerated signal rows the second reviewer scores present/absent, drawn from Play's target-audience/Families policy, with submission blocked while any row is present (CM-R50.3). | Repair round; data/google_play_checklist.csv:23 requires the sign-off but names no rubric | The human replaces the row set, or Play policy language changes | Human may overrule |
| **A-26** | **Silhouette/symbol legibility protocol (analyst-authored):** 5 raters, unprompted, randomized order, 25 pooled trials, **≥90% correct**, with any failing asset re-topo'd or cut **and the decision recorded** (CM-R21.3, CM-R21.6). | Repair round; specs/product_spec.md:179-181,770 name the bar's subject but no metric or judge | The human changes the rater count or the pass bar | Human may overrule |
| **A-27** | **Faucet-parity comparison base (analyst-authored):** tickets-per-minute of a solver-optimal Night Harbor first-clear ≤ tickets-per-minute of a solver-optimal run of the highest-yield campaign level (CM-R10.6). Replaces an intent clause with an inequality. | Repair round; data/economy_sources_and_sinks.csv:11 | The human names a different parity base (e.g. per-level rather than per-minute) | Human may overrule |
| **A-28** | **"Safest path" and "no penalty", enumerated (analyst-authored):** the four-part unknown-error degradation (CM-R32.6) and the five decline invariants (CM-R34.4). Both replaced unbounded universals. | Repair round; specs/revenuecat_implementation.md:268-287; data/analytics_event_taxonomy.csv:24 | The human adds an observable to either list | Human may overrule |
| **A-29** | **Restore observation window (analyst-authored):** ≥95% restore success measured over the **14 days following production launch** from accounts with a prior purchase, with review mentions triaged **weekly** as a tripwire rather than a criterion (CM-R28.6). | Repair round; specs/monetization_spec.md:382 gives a target with no window | The human sets a different window or cadence | Human may overrule |

### 4.1 PINNED BRANCHES — criteria authored per candidate, exactly one survives ratification

Each row is a criterion that is **fully testable under either branch** but whose branch selection is a human decision. Nothing here is chosen by an agent.

| Pin | Criterion(s) | Candidate branches (verbatim option names used above) |
|---|---|---|
| **NEW-Q1** | CM-R09.5, CM-R19.1, CM-R04.4 | **Q1-A** re-author anchors into the flat 45–90 s / 40–75 s invariant · **Q1-B** commit `data/difficulty_bands.csv` with per-band `[min,max]` ranges and keep the anchors as authored |
| **NEW-Q2** | CM-R50.5 (and U-1) | **listing-delete** the sentence · **listing-forward-binding** a recorded verbatim sentence with no present-tense effect |
| **NEW-Q4** | CM-R02.4 | **B-pass-through** · **B-blocked** · **B-collision** (all three map onto the existing three fail reasons) |
| **NEW-Q5** | CM-R04.1, CM-R18.2 | **CHAIN-A** counter continues past 5, bonus caps at +50 · **CHAIN-B** counter saturates at 5 · plus the values of `PERFECT_BONUS_TICKETS` and `PERFECT_MAX_SWITCHES` |
| **NEW-Q6** | CM-R08.8 | **topup-to-2** (entitlement_map reading) · **flat-plus-1** (product_spec reading) |
| **NEW-Q7** | CM-R09.6, CM-R10.3, CM-R10.6, CM-R49.2 | the single committed `data/level_ticket_schedule.csv` (three conflicting candidates: +20 flat / 20–50 per level / per-district 20·25·30·35·40·50) **and** the enumerated `data/cosmetic_milestones.csv` |
| **NEW-Q10** | CM-R29.4, CM-R38.1 | **T-local-notification** · **T-IAM-next-session** · **T-journey-step** |
| **NEW-Q12** | CM-R31.4 | **P-client-surface** (in-app redeem entry point → Play redemption sheet) · **P-console-only** (criterion withdrawn, recorded) |
| **NEW-Q14** | CM-R39.3 | the committed `config/message_budget.json` allocation: **MB-J1-only** (J1 nudge + J1 streak warning = 2/day; scheduled sends and local notifications displace, never add) · **MB-explicit** (a human-authored per-channel table) |
| **NEW-Q16** | CM-R42.2 | **PI-push-2h** · **PI-local-2h-IAM-fallback** · **PI-IAM-then-local-4h** |
| **NEW-Q17** | CM-R42.1 | **S-2000-local** · **S-expiry-minus-6h** |
| **NEW-Q18** | CM-R42.3 | **F-IAM-only** · **F-push-permitted** |
| **NEW-Q21** | CM-R46.5 | **curve-liveops** (Mon 0.35 … Sat 0.75 … Sun 0.55) · **curve-productspec** (Mon 0.30 … Sat 0.50 … Sun 0.55); plus frozen-parameter yes/no |
| **NEW-Q22** | CM-R47.4 | **CR-livery-only** · **CR-livery-plus-150** |
| **NEW-Q23** | CM-R47.1 | **RK-static** solver-calibrated medals (+ how round 1's par table is bootstrapped with no telemetry) · **RK-percentile** prior-week buckets (**requires naming a cross-player data source, which the NON-GOALs currently forbid**) |
| **NEW-Q24** | CM-R49.1, CM-R49.2 | **ladder-productspec** (doubled gift + 1 rewind / + theme-rental token / 24 h theme trial) · **ladder-liveops** (1 rewind / 150 tickets + 2 rewinds + rental token / "retuned routes" screen) |
| **NEW-Q25** | CM-R49.5 (and U-8) | window **48 h** or **72 h**; floor **≥2** or **≥3**; plus the grace token's accrual window and stacking rule |
| **NEW-Q28** | CM-R52.2 | **T-auto** device-tier detect engages the cap at boot · **T-setting** user setting defaulting ON for low-tier devices; plus the committed `device_tier` derivation rule |
| **NEW-Q29** | CM-R53.3 | **RV-district** · **RV-stars** |
| **NEW-Q30** | CM-R10.4 | **depot-silhouette** · **labelled-Night-Harbor-tile** |
| **NEW-Q35** | CM-R02.7, CM-R06.4 (and U-9) | **W-player-assigned** (resolved at the commands boundary, appears in the command log) · **W-auto-accept** (resolved at the station-acceptance boundary, no command-log entry) |
| **NEW-Q36** | CM-R44.2, CM-R43.1 | **previously_completed: bool** · **completions_before: int** · (a separate event instead, which moves the count to 46) |
| **NEW-Q37** | CM-R14.1, CM-R55.3, every dated criterion | the ratified window-basis date, and whether to re-baseline to the P80 branch |

---
## 5. OPEN QUESTIONS

**Totals: 9 D-decisions + 48 numbered NEW-Q items (NEW-Q44 has sub-part b) = 57 open items.** None is answered by an agent.

### 5.1 D-1 … D-9 — verbatim from EXECUTION_PLAN.md:213-225 · **ALL PENDING HUMAN DECISION**

> | # | Decision | Recommendation | Why it can't wait |
> |---|---|---|---|
> | D-1 | Use the **pre-verified personal Play account** or create new? Check its creation date first: **if created before Nov 13 2023, the 12-tester/14-day rule does not apply at all** — the entire closed-test critical path collapses to zero | Check the date in Play Console (2 min). If pre-Nov-2023: use it, and the schedule re-plans around review times only. If not: new-vs-old on identity-verification speed | Determines today's entire critical path |
> | D-2 | Confirm identity freeze: `com.catmetro.game` + @CatMetroGame | Yes (roadmap already uses both; backlog CM-009 orbit) | Package id permanent at first upload — today |
> | D-3 | Streak claim fix: (A) de-couple daily gift from streak (flat 50/day) or (B) keep mechanic, rewrite the "streaks are cosmetic" claims to the defensible version | **B** — no rebalance, no design change; the mechanic is fine, the absolute claim was the defect | Copy must be fixed before BIP posts/tester builds carry it |
> | D-4 | Rewarded rewind cap 5/day → 3/day | Yes (recovers a sliver of consumable demand; every stated principle survives verbatim) | Cheap now, awkward after caps are published in BIP |
> | D-5 | Schedule poster_wall_gallery into Week 6 (swap slot: levels 36–40) or delete the "flagship Catvertising writeup" framing | Schedule it — zero new ad inventory, screenshot-legible for judges who never install | Roadmap edit; affects W6 planning |
> | D-6 | Tester roster: 18–20 names from personal network (not tester-exchange/Discord channels — flag-by-association risk) | Draft the list today; Shipaton Discord only as overflow | Invites go out today |
> | D-7 | Run PW01 ($6.99 vs $4.99, directional) at all? | Yes — it's the HAMM "thoughtful pricing" artifact; pre-registered as non-significant | Sep 1 start; RC config D1-17 creates the SKU Day 1 |
> | D-8 | Email shipaton@revenuecat.com (multi-award cap? pre-order = public release?) | Send today; 2-line email | Answers shape award positioning by Week 7 |
> | D-9 | Adopt the audit's 4–6 explicit contingency/rest days, funded by pre-cutting per §3.2 — or run the 56-day / 412h schedule as-is | Adopt: mark 4 floating buffer days (~1 per fortnight), funded in order from the §3.2 cut lines; a slip consumes buffer scope, never sleep | Roadmap edit rides AMD-01; burnout is a named top risk (§10) |

**Recommendations above are the plan's own; none has been adopted by an agent. Every one is PENDING HUMAN DECISION.**

Requirements blocked by each: D-1 → CM-R52.7, whole schedule · **D-2 → CM-R50.7, CM-R50.6, CM-R41.6, CM-R23, CM-R31 (now also carrying domain ownership, NEW-Q27 / risks RK-28)** · D-3 → CM-R49, CM-R39.4, store/BIP copy · D-4 → CM-R35.3 · D-5 → CM-R14.2, cut-line step 3 · D-6 → CM-R12.4, CM-R13.4, CM-R44 · D-7 → CM-R23.1, CM-R33 · D-8 → CM-R57 · D-9 → §6 cut-line funding.

### 5.2 NEW-Q1…NEW-Q34 — contradictions and gaps surfaced by the source extracts

| ID | Question | Conflict / gap | Blocks |
|---|---|---|---|
| NEW-Q1 | **Re-author anchors L001/L006/L018 to the 45–90 s loop invariant, or amend the invariant to a per-band table?** | Anchor time limits 20 / 32.5 / 37.5 s vs locked 45–90 s and the validator's 40–75 s solver band (AUDIT_FINDINGS.md:331,480-481; specs/product_spec.md:384,389,537) | CM-R09.5, CM-R19.1, CM-R04.4, CM-R12 |
| NEW-Q2 | What does `all_access` actually change about ads? | google_play_checklist.csv:20 vs entitlement_map.json:63-93 ("removes nothing today") | CM-R50.5, U-1 |
| NEW-Q3 | Is `switches: []` (zero-switch level) legal design? | Required array with no minItems (data/level_schema.json:6-7,80-91) | CM-R12 schema stage |
| NEW-Q4 | Reverse traversal of a one-way edge by a rejected cat: collision, pass-through, or blocked? | specs/product_spec.md:210 vs :222 | CM-R02.4 |
| NEW-Q5 | Does the chain **count** continue past 5, or only the bonus cap? And what are the global defaults for `win.perfectMaxSwitches` / `economy.perfectBonus`? | specs/product_spec.md:236; data/level_schema.json:121-140 | CM-R04.1, CM-R18.2 |
| NEW-Q6 | All Access daily rewinds: 1 or 2 free/day? | specs/product_spec.md:482 vs data/entitlement_map.json:75-77 | CM-R08.8 |
| NEW-Q7 | Per-level ticket schedule L001–L030 **and** the enumerated cosmetic-milestone list | specs/product_spec.md:299 vs :399 vs data/economy_sources_and_sinks.csv:3; no milestone list exists | CM-R09.6, CM-R10.3, CM-R10.6, CM-R49.2 |
| NEW-Q8 | Daily seed: UTC date + "CM-DAILY-" or local dateKey + "CM-DAILY-1\|"? | specs/product_spec.md:447 vs specs/liveops_spec.md:22-31 | CM-R11.1 |
| NEW-Q9 | `validatedAt` when unvalidated: delete the key or amend the schema to allow null? | data/level_schema.json:25 vs AUDIT_FINDINGS.md:523 | CM-R12.5 |
| NEW-Q10 | `payer_thanks` delivery mechanism inside the 3-journey/6-step budget | specs/monetization_spec.md:336 vs specs/onesignal_retention.md:193 | CM-R29.4, CM-R38.1, U-3 |
| NEW-Q11 | *(superseded by NEW-Q47)* Refund-farming detection: what is an "account" and how does the client learn? | data/entitlement_map.json:395-399 | CM-R30-D, U-2 |
| NEW-Q12 | Scope of client-side Play In-app Promotions work beyond Console code generation | EXECUTION_PLAN.md:152-154 vs data/google_play_checklist.csv:33 | CM-R31.4 |
| NEW-Q13 | RC 9.7.0 API shapes (async vs callback; error-class enum) | specs/revenuecat_implementation.md:286,331-337,515 | CM-R32, architect handoff, risks RK-39 |
| NEW-Q14 | Day-budget allocation across J1 + scheduled sends + local notifications under the 2/day honest cap | specs/liveops_spec.md:200 (superseded 1/day) vs specs/onesignal_retention.md:85 | CM-R39.3 |
| NEW-Q15 | Is `catmetro://daily?d=…&b=…` a registered route form? | specs/liveops_spec.md:65 vs specs/onesignal_retention.md:273 | CM-R41, risks RK-27 |
| NEW-Q16 | `purchase_issue` mechanism/timing: +2 h push, +2 h local w/ IAM fallback, or IAM + 4 h local? | data/onesignal_journeys.csv:11 vs specs/onesignal_retention.md:194 vs specs/liveops_spec.md:192 | CM-R42.2 |
| NEW-Q17 | Streak-expiry local backup: "tomorrow 20:00 local" or "expiry −6 h"? | specs/liveops_spec.md:196 vs specs/onesignal_retention.md:199 | CM-R42.1 |
| NEW-Q18 | Is any push/scheduled send permitted for `feedback_request`, or IAM-only? | specs/onesignal_retention.md:195 vs specs/liveops_spec.md:193 | CM-R42.3 |
| NEW-Q19 | Analytics offline queue: depth, byte cap, flush trigger, drop policy | EXECUTION_PLAN.md:190 only; absent from all data files | CM-R43.4 `[ARCH]`, risks RK-32 |
| NEW-Q20 | `device_tier` derivation; `rank_bucket` producer; `restore_started` QA procedure; the `daily_started` truth source | data/analytics_event_taxonomy.csv:14,15,33 | CM-R43.8, CM-R52.2, U-6, U-7 |
| NEW-Q21 | Daily weekday difficulty ramp: liveops values (0.35→0.75) or product_spec values (0.30→0.55)? Frozen generator parameter? | specs/liveops_spec.md:39-47 vs specs/product_spec.md:449 | CM-R46.5 |
| NEW-Q22 | Cup participation: 150 tickets in or out? | specs/liveops_spec.md:84 vs data/economy_sources_and_sinks.csv:10 | CM-R47.4 |
| NEW-Q23 | Cup ranking: solver-calibrated medals or prior-week percentile buckets (+ how is round 1's par table bootstrapped)? | specs/liveops_spec.md:83 vs specs/product_spec.md:467-476 | CM-R47.1 |
| NEW-Q24 | Comeback grant values (48 h / 7 d / 14 d) — two different ladders | specs/product_spec.md:483-485 vs specs/liveops_spec.md:132-134 | CM-R49.1, CM-R49.2 |
| NEW-Q25 | `streak_saver`: 48 h or 72 h window; floor ≥2 or ≥3; **plus the grace token's accrual window and stacking rule** | data/ad_placement_map.csv:6 vs specs/liveops_spec.md:135; specs/onesignal_retention.md:85; U-8 | CM-R49.5 |
| NEW-Q26 | If `theme_rental` is cut (cut-line step 1), does the ≥7 d comeback rental token survive? | specs/liveops_spec.md:133 vs EXECUTION_PLAN.md:197 | §6 Option B |
| NEW-Q27 | Identity cascade: support email (.io vs .com), webhook host `rc-hooks.catmetro.io`, RC app display name, service-account naming — **and domain ownership as a blocking sub-decision of D-2** | data/revenuecat_configuration.csv:43 vs data/google_play_checklist.csv:32; data/entitlement_map.json:380-387; risks RK-28 | CM-R50.6, CM-R50.7, CM-R41.6, CM-R53.5 |
| NEW-Q28 | Low-tier 30 Hz cap: automatic tier detect or user setting? Plus the `device_tier` derivation rule | data/device_test_matrix.csv:3 | CM-R52.2 |
| NEW-Q29 | In-app review trigger: "win on a completed district, 1/30 d" or "3 stars + session_count ≥5 + crash-free"? | data/google_play_checklist.csv:25 vs data/onesignal_journeys.csv:14 | CM-R53.3 |
| NEW-Q30 | Does the Home map show the paywalled bonus district as a "depot silhouette" or as the labelled Night Harbor tile? | specs/product_spec.md:298 vs specs/monetization_spec.md:227-243 | CM-R10.4 |
| NEW-Q31 | Does the launch client read offering metadata (`first_exposure_level`) at all — **and does any client-side experiment-assignment mechanism (the J2 10% holdout) ship at all?** | data/paywall_experiments.csv:8 vs EXECUTION_PLAN.md:210-211; U-10 | CM-R25, U-10 |
| NEW-Q32 | Customer Center is P1 and may slip post-launch, but restore/manage links appear in every paywall footer — what is the fallback help-screen scope? | data/revenuecat_configuration.csv:43; specs/revenuecat_implementation.md:106 | CM-R28.1 |
| NEW-Q33 | Is the W2/W3 sequential $6.99→$4.99 price test the same thing as PW01? What pricing latitude exists Sep 14–24? | specs/liveops_spec.md:111-114 vs data/paywall_experiments.csv:7 | D-7, CM-R23 |
| NEW-Q34 | Under a P50 Sep 1–2 launch, which W1–W5 liveops beats compress or drop (beyond the cut-line order)? | specs/liveops_spec.md:106-114 vs EXECUTION_PLAN.md:123-125 | CM-R47, §6 |

### 5.3 NEW-Q35…NEW-Q36 — raised by the testability repair round

| ID | Question | Why it is a human call | Blocks |
|---|---|---|---|
| NEW-Q35 | **Wildcard `wild` runtime semantics: player-assigned destination at a switch, or auto-accept at the first eligible station?** | It changes the tick order, the solver and the fourth shipped mechanic — a design decision, not a measurement gap | CM-R02.7, CM-R06.4, U-9 |
| NEW-Q36 | **Shape of taxonomy delta TD-01** on `level_started`: `previously_completed: bool`, `completions_before: int`, or a separate event (which moves CM-R43.1's count to 46) | It changes a shipped analytics contract and the "exactly 45 events" claim that is published | CM-R44.2, CM-R43.1, risks RK-09 |

### 5.4 NEW-Q37…NEW-Q44 — surfaced by the venture critique (`docs/prd/venture-critique.md`, 2026-08-02)

**These are appended as questions only. The critique's own recommendations (its KC-1…KC-11) were NOT adopted into any requirement.** The critique decides nothing; it argues the case against as hard as the evidence allows and explicitly states that none of its objections argues the bet should not be taken.

| ID | Question the human must answer | Source objection | Blocks / touches |
|---|---|---|---|
| NEW-Q37 | **Ratify the window-basis date**, and decide whether to re-baseline the schedule to the P80 branch. The plan's own D1/D2 acceptance criteria (12/12 opted in, seed AAB live) are unmet as of D2; the audit's P50/P80 distribution is conditioned on an Aug 1–2 clock start. Derived arithmetic in V-1: with the T+35 one-rejection-cycle chain, **a clock start after Aug 15 leaves zero rejection capacity against the plan's own latest-viable Sep 19 launch**. | V-1 (CRITICAL) | CM-R14.1, CM-R55.3, every dated criterion; risks RK-01, RK-02 |
| NEW-Q38 | **How does the pre-registered D7 gate respond to its own power and contamination?** Exact binomial at n=12/≥6: 11.8 / 21.3 / 61.3 / 91.5% pass at p = 0.30/0.35/0.50/0.65. The recruitment email tells testers in writing to open the app regularly while metric (i) measures *unprompted* opens. Options the human may take: accept the 39% false-RED rate at p=0.50 explicitly on the record; adopt a confirmation step; and/or report message-adjacency alongside the tally. **Whatever is chosen must be published in BIP post 1 before data exists (CM-R56.2), or not at all.** | V-2(a),(b) | CM-R44, CM-R56.2; risks RK-06, RK-07 |
| NEW-Q39 | **Should R-01 (production-access delay) and R-02 (fun-gate failure) be linked in `data/risk_register.csv` as sharing one root cause and one sample of 12 people?** They are currently logged as independent Critical risks; Google's own page lists "testers not being engaged" as a rejection reason, so one roster collapse produces both outcomes in the same week. | V-2(c) | risks RK-08 |
| NEW-Q40 | **When are the production-access application answers drafted, and what is the honest answer to "did your testers exercise all app features"?** The application is filed D15 for a build whose commerce (D16–17), ads (D18) and messaging (D19) layers do not exist yet. | V-3 | risks RK-10; CM-R56.4 binds the answers |
| NEW-Q41 | **Is the 3,000-install anchor restated, and at what threshold is it declared void?** `revenue_scenarios.csv:3` calls it an anchor; it back-solves to ~624 listing views/day against a growth plan that concedes "~50 people" reachable at launch. The critique proposes <300 installs by launch+10 days as the tripwire and flags the threshold itself as a judgment call for the human. | V-4 | risks RK-14; award positioning |
| NEW-Q42 | **Is the submission-funnel work pulled earlier than W7?** The critique asks for a ≥60 s on-device gameplay cut and one drafted category-question answer per targeted category by ~D36, ten days before the D42 freeze removes the option to fund it from content scope. | V-5 | CM-R55.3, CM-R57; risks RK-15 |
| NEW-Q43 | **How is process cost measured, and in what units is the AI-capacity tripwire expressed?** Ceremony is uncosted against the 412 h estimate and is owned by the person who authors the process; the stop-and-rethink trigger is ">40% of budget" against a $0 budget. | V-7, V-8 | risks RK-03, RK-04 |
| NEW-Q44 | **What is the objective function?** EV ≈ ~$6/founder-hour is a *ranking rule*, not an argument to stop — but it ranks differently if the objective is money vs portfolio vs toolkit validation vs enjoyment. Undeclared, the project optimises for whichever the last decision assumed. | V-9 | §6 cut decisions; risks RK-05 |
| NEW-Q44b | **Is a suspension branch written, or is the unbranched tail accepted on the record?** Single platform, single store, single developer account, permanent package id from first upload; there is a rejection branch and no suspension branch anywhere. | §6 escalation | `PLAN_B_RUNBOOK.md`; risks RK-13 |

### 5.5 NEW-Q45…NEW-Q48 — escalations from the design-time threat model (security-reviewer, forge-specify step 6)

**Design-time only; no code exists and nothing was reviewed as an implementation. Its mitigations were NOT adopted into requirements** — they live in `docs/prd/risks.md` (RK-16…RK-39). These four were escalated as explicitly not agent-acceptable.

| ID | Escalation | Why the human, not an agent | Blocks / touches |
|---|---|---|---|
| **NEW-Q45** | **Consent management (CMP/UMP).** CM-R50.9 sets broad availability and CM-R50.5 declares ads with `AD_ID` across five rewarded surfaces; serving ads and ad-attributed analytics to EEA/UK users requires a Google-certified CMP / UMP flow. **There was no requirement, criterion or question covering consent anywhere before this round** — the threat model calls it "the single largest compliance gap in the document" and the venture critique raised it independently. Two clean options, both preserving Shipaton eligibility (US access is the rules requirement): build the consent flow, or restrict initial availability / ship `ads_enabled=false`. | It is a policy exposure and an availability decision, and it changes scope | **CM-R50.9**, CM-R34; risks RK-11 |
| **NEW-Q46** | **Record the acceptance of indefinite offline entitlement honoring and unenforceable offline revocation**, and write the containing invariant as a MUST: *no entitlement ever unlocks anything that costs us money or that another player's outcome depends on*. Accepting the residual is the right call for a no-server $6.99 premium title — but the acceptance must be recorded by the human in the PRD or an ADR, not assumed by an agent. | Accepting a residual risk is a human act | CM-R24.5, CM-R30; risks RK-16, RK-22 |
| **NEW-Q47** | **Refund-farm detection and consumable anomaly alerting: build, defer, or do not implement?** The threat model recommends **do not implement** — any implementation manufactures a persistent identifier (fingerprint / SSAID / AD_ID-derived id) with a Data Safety declaration and a 13+ audience problem, to stop a handful of $1.99 refunds. The same question covers U-4's anomaly thresholds (>10 / >5 packs/day/user), which name no channel, cadence or recipient. | Creating a PII flow is a human decision and needs a `docs/security/` policy entry before code | CM-R30-D, U-2, U-4; risks RK-23 |
| **NEW-Q48** | **The release-signing and CI secret controls, before the first signed build.** Play App Signing so the repo only ever holds an upload key; credentials in encrypted CI secrets only; a release environment with required human approval; smoke jobs on a debug key with zero release secrets; third-party actions pinned by SHA; least-privilege `permissions:`; no secrets on `pull_request`; secret scanning in the pre-commit hook and CI; service account scoped and rotated. **This is the one item where a mistake is irreversible** — a leaked upload key against a permanently-fixed package id. The existing "agents never run `fastlane supply`" rule needs its technical counterpart: agent-reachable contexts never hold the credential. | Hard-blocking for the D15 production-access application; irreversible | CM-R52.6; `.github/**`, `infra/**` risky paths; risks RK-33, RK-37 |

---

## 6. SCOPE CUT PRESENTATION — ratify, don't invent

The human ratifies one of three states. Options B and C are **pre-authorized** by the plan's own cut-line ladder; activating them is a scheduling decision, not a redesign.

### Option A — the plan of record (RECOMMENDED)
Ship the locked 1.0 build scope in full (EXECUTION_PLAN.md:178-193): core sim · 4 mechanics · 30 levels + Night Harbor + Daily Line · full feel P0 set incl. purr meter · accessibility P0s · 6 SKUs / 4 entitlements / 5 placements · 5 rewarded surfaces · 3 journeys / 6 steps · 45-event taxonomy · Daily seed pipeline + District Cup from ~Aug 31 · full listing · daily BIP + capture rig + Devpost package.
Requirements in scope: **all CM-R01…CM-R57** (CM-R30-D remains deferred under NEW-Q47 in every option).
Recommended because the audit's clean-check list shows the commerce spine, schema and event taxonomy are already internally consistent (AUDIT_FINDINGS.md:339-351) — the risk in this plan is calendar, not design. **Note the tension the human is ratifying against:** the venture critique's V-1 argues the calendar has already slipped (NEW-Q37) and V-9's ranking rule argues for taking cuts early rather than late.

### Option B — cut-line steps 1–3 activated
The pre-authorized ladder, in order, verbatim from EXECUTION_PLAN.md:195-201:

> 1. theme_rental surface (keep other 4 ad surfaces) → 2. District Cup round 1 slips a week →
> 3. levels 36–40 → post-event (D42 gate's named sacrifice; also the swap-slot for
> poster_wall_gallery per AMD-10) → 4. second premium theme → 5. levels 31–35 → 6. Daily
> leaderboard cosmetics. **Never cut:** purchase/restore integrity, crash-free ≥99.5%, honest
> store listing, judge access, the daily BIP post.

Effect on requirements: CM-R35.5 drops (theme_rental) and `theme_preview` renders buy-only — **but NEW-Q26 must be answered first** (does the ≥7 d comeback rental token survive?). CM-R47 round 1 slips one week. CM-R14.2 (levels 36–40) moves post-event, satisfied via its cut-line-step-3 branch; if D-5 is taken, `poster_wall_gallery` occupies that slot.
Everything else in Option A holds, including every `[CI]` criterion.

### Option C — cut-line steps 1–5 activated
Adds: second premium theme cut (Neon — data/revenuecat_configuration.csv:18 already gates it on the colorblind check, so this is the cheapest of the deep cuts) and levels 31–35 cut. Effect: `cm_theme_neon` and the `theme_neon` entitlement are not offered (the SKU stays defined but INACTIVE — CM-R23.1 asserts INACTIVE instead of ACTIVE); CM-R14.1 drops; the cooldown mechanic does not ship in-window at all, which makes the D42 gate criterion unsatisfiable as written (AUDIT_FINDINGS.md:307) — **flagged to the human as a knock-on, not resolved here.**

**Invariant across A, B and C — never cut** (EXECUTION_PLAN.md:200-201): purchase/restore integrity (CM-R27, CM-R28, CM-R32), crash-free ≥99.5% (CM-R52.5), honest store listing (CM-R50), judge access (CM-R31), the daily BIP post (CM-R56).

**Funding note:** D-9's recommendation is that the 4–6 buffer days are **funded from this ladder in order** — a slip consumes buffer scope, never sleep (EXECUTION_PLAN.md:225). Ratifying Option A while also adopting D-9 means pre-committing to step 1 the first time a gate slips.

---

## 7. DEFINITION OF DONE FOR THIS DOCUMENT

- [x] Every requirement traced to a file:line or explicitly flagged as an assumption (§4) / open question (§5).
- [x] Every acceptance criterion is product-level and concretely checkable; the deterministic-sim (CM-R01) and purchase/restore-integrity (CM-R27, CM-R28) criteria are `[CI]`-marked and merge-blocking. Where a criterion depended on an unanswered human decision, the criterion is authored per candidate branch and marked `[PIN NEW-Qn]`, with the candidates enumerated in §4.1.
- [x] Criteria that asserted schedules, intents, third-party actions or unbounded universals were replaced with observable properties (CM-R14.2, CM-R48.4, CM-R57.5, CM-R34.4, CM-R10.6, CM-R28.6).
- [x] Requirements that still cannot be made testable are in §2.13 with the specific human action named, not silently dropped; U-2 was split into deferred requirement **CM-R30-D** so CM-R30 scores clean.
- [x] Human-decision list is at the top (§0) and expanded at §5.1–§5.5, not buried; the consent gap (NEW-Q45) is promoted into §0.
- [x] No invented statistics; no simulated user feedback (§1.4 states plainly that zero user datapoints exist); external benchmarks carry vintage; analyst-authored measurement protocols are labeled as assumptions the human may overrule (A-21…A-29).
- [x] Venture-critique recommendations were **not** adopted into requirements — they are open questions (§5.4) and risks (`risks.md`). Security mitigations were **not** adopted into requirements — they are risks plus four named escalations (§5.5).
- [x] Risk register produced as a companion document (`docs/prd/risks.md`), deduplicated against EXECUTION_PLAN §10 rather than repeating it.
- [ ] **Awaiting:** human ratification of §6; answers to D-1…D-9, the §4.1 pins, and NEW-Q1…NEW-Q48; then handoff (PRD + open questions + risks) → architect.






