# CAT METRO — VENTURE CRITIQUE

**Status:** ADVISORY input to the human hypothesis gate · **Written:** 2026-08-02 (D2 of 56) ·
**Author:** venture-critic (agent) · **Presented alongside:** the PRD draft, same meeting.

**What this is:** the case against, argued as hard as the evidence allows. It decides nothing.
It feeds `docs/prd/hypothesis.md` §kill-criteria and the risk register. Objections that age
badly get retired on the record at `/forge-retro`; objections that confirm get escalated.

**Scope discipline.** The 23-agent audit of 2026-07-31 (`docs/plan/AUDIT_FINDINGS.md`)
re-verified 113 of ~116 external claims against primary sources. I did **not** re-litigate its
covered ground (schedule Monte Carlo, revenue arithmetic, concept selection, pricing, Unity
pin, file integrity, Devpost rules). I re-checked three of its perishable pins and attacked
what it did not cover. Where the idea survives, I say so.

**Perishable pins re-checked today (all still hold — these are retirements, not objections):**

| Pin | Status 2026-08-02 | Source |
|---|---|---|
| AdMob Unity broken on AGP9 → Unity 6000.3.16f1 pin is forced | **Still open**, opened 2026-06-07, no fix, no maintainer timeline | [googleads-mobile-unity #4212](https://github.com/googleads/googleads-mobile-unity/issues/4212) (fetched 2026-08-02) |
| purchases-unity 9.7.0 pin (weekly cadence — drift risk) | **9.7.0 still latest**; no re-pin needed at project start | [purchases-unity releases](https://github.com/RevenueCat/purchases-unity/releases) (fetched 2026-08-02) |
| Devpost prize table (audit flagged it might change after Aug 1) | **Confirmed:** Grand $100k; #BuildInPublic 30/20/10; OneSignal 25/15/5; most others 15/10/5; pool "$685,000+"; deadline Sep 30 11:45pm PDT | [revenuecat-shipaton-2026.devpost.com](https://revenuecat-shipaton-2026.devpost.com/) (fetched 2026-08-02) |

---

## 1. STRONGEST OBJECTIONS, RANKED

One fatal objection outweighs ten cosmetic ones. V-1 is the one that matters. Everything below
it is real but subordinate.

---

### V-1 — The critical path has already slipped, and today (D2) is being spent on planning · **CRITICAL** · Category: execution / market timing

**The failure mechanism.** Every date in this plan is a function of one variable: the day the
Play closed-test clock starts. That clock is per-tester and *trailing* — a tester who joins
late restarts their own 14 days (`EXECUTION_PLAN.md:449-451`), so slip cannot be recovered by
adding people later. As of end of D2 the clock has not started.

**Evidence (in-repo, dated, not disputable):**

- Roadmap D1 (2026-08-01) acceptance criterion: *"Play accepts the AAB; >=8 of 12 invites
  accepted by end of day"* (`data/roadmap_56_days.csv:2`).
- Roadmap D2 (2026-08-02) acceptance criterion: *"12/12 testers opted in; replay-hash test
  green in CI"* (`data/roadmap_56_days.csv:3`).
- `state/PROJECT_STATE.md:8` (2026-08-02): *"phase: discovery → sprint prep … no app code
  scaffolded yet"*. Active-tasks table: empty. Recently-done: forge substrate, docs drop,
  rulesets — no Play Console, no Unity project, no AAB, no testers.
- `docs/plan/DAY1_RUNBOOK.md` was generated **on D2** and its own end-of-day checklist
  (lines 147-155) is entirely unchecked, including the line it labels *"THE number"*:
  `[ ] 12/12 testers opted in (pool >=16 invited)`.
- **D-1 — the two-minute Play Console check for a pre-Nov-13-2023 account creation date, which
  the plan says "determines today's entire critical path" and could delete the 12-tester/14-day
  gate outright** (`EXECUTION_PLAN.md:217`) — is still open on D2. This is the single
  highest-value 120 seconds in the entire plan and it is unspent.
- The audit's whole probability distribution is **conditioned on** an Aug 1–2 clock start:
  *"The single most schedule-critical action in the entire plan is starting the Play closed
  test with 12+ opted-in testers today or tomorrow"* (`EXECUTION_PLAN.md:65-67`); P50 Sep 1–2 /
  P80 Sep 12–16 derive from it (`AUDIT_FINDINGS.md:206-216`). Slip the input, slide the whole
  distribution right, day for day.
- The slip has a published dollar gradient: a Sep 10 launch keeps ~$142 of the $253 base case;
  Sep 20 keeps ~$73 (`AUDIT_FINDINGS.md:212-214`).

**Derived tripwire (arithmetic, not opinion).** Let T = clock-start date. The chain that
absorbs *one* rejection cycle is: clock completes T+14 → apply → rejection lands ~T+21 → the
trailing-14-day criterion re-satisfies → re-apply → grant ~T+28 → submit-on-grant → first-release
review → live ~T+35. For "live" to land on or before the plan's own **latest-viable Sep 19**
(`EXECUTION_PLAN.md:27`), **T ≤ Aug 15**. For bare Sep 30 eligibility, T ≤ Aug 26.
→ **A clock start after Aug 15 leaves literally zero rejection capacity against the
latest-viable launch date**, and rejection is the plan's own #1 unmodelled risk
(`AUDIT_FINDINGS.md:58-69`).

**Compounding factor — the process is eating the resource it depends on.** `EXECUTION_PLAN.md`
§9.0 rule 1 states the mitigation explicitly: *"The console critical path never waits for forge
ceremony."* On D2 it waited. All nine D-decisions remain PENDING in the PRD draft §0, and D-1
and D-6 (tester roster) are prerequisites for the console actions. The rule is correct; it was
not followed on the first day it applied.

**Trigger to watch:** daily opt-in headcount, starting now. **Mitigation owner:** HUMAN
(console actions are human-only by `EXECUTION_PLAN.md` §8.4 and cannot be delegated).

**Honest caveat:** if D-1 comes back "account created before Nov 13 2023," this objection
mostly evaporates — the 12/14 gate does not apply and the critical path collapses to review
times only. That is why the check is the highest-EV action available and why this objection is
cheap to retire.

---

### V-2 — The D7 fun gate is under-powered at n=12 *and* biased toward false-GREEN by its own recruitment instrument · **HIGH** · Category: product risk / decision quality

The audit repaired the gate's *validity* (one fail rule, replay-of-a-won-level metric, stress
boards, outside confirmer). It never computed the gate's **power**. I did.

**(a) The operating characteristic is shallow.** Metric (i) — "≥6/12 testers open the app
unprompted on a second calendar day" — is a binomial test at n=12. Failing metric (i) **alone**
triggers RED, i.e. execute the Plan-B runbook and discard the concept
(`EXECUTION_PLAN.md:134-141`). Exact binomial P(X ≥ 6) for X ~ Bin(12, p):

| True unprompted-return rate p | P(gate passes metric (i)) |
|---|---|
| 0.30 | **11.8%** |
| 0.35 | **21.3%** |
| 0.50 | **61.3%** |
| 0.65 | **91.5%** |

*(My arithmetic on the plan's own stated n and threshold; reproducible with any binomial
calculator. Not a benchmark, not a forecast.)*

Read the middle row: a build whose players genuinely come back half the time **fails the
concept-kill metric 39% of the time**. The gate cannot separate p=0.35 from p=0.50 — the very
distinction on which the pivot decision rests. Adding the other three metrics does not fix
this; it makes YELLOW (2 of 4) more likely still, and the four metrics are positively
correlated (they are computed on the same 12 people in the same 3 days), so the joint error
rate is worse than independent multiplication would suggest.

**(b) The sample is prompted, in writing, to do the thing the metric calls "unprompted."**
The recruitment email drafted on D2 tells every tester: *"Stay opted in through at least Aug 31
… and open it a couple of times a week — daily if you're feeling generous. **Google actually
grades tester engagement.**"* (`DAY1_RUNBOOK.md:53-55`). Metric (i) then measures whether they
"open the app **unprompted** on a second calendar day" (`EXECUTION_PLAN.md:137`). The
instrument that recruits the sample is the instrument that contaminates the measurement, and
the contamination is *directional*: it pushes toward passing. Combine (a) and (b) and the gate
is **more likely to green-light a boring game than to red-light a good one** — the exact
inverse of what a kill-gate is for. Add that the pool is the developer's personal network
(D-6), i.e. people with a social reason to open a friend's app.

**(c) The two Critical-impact risks in the register share one root cause and one sample of 12
people.** `data/risk_register.csv` lists R-01 (production-access delay, Critical) and R-02
(fun-gate failure, Critical) as separate rows with separate early-warning signals and separate
contingencies. They are not independent. If the 12 testers under-engage, you get **both** a RED
fun gate **and** a rejected production-access application, from the same cause, in the same
week. Google's own page confirms the shared dependency: rejection reasons explicitly include
*"your testers not being engaged with your app"*
([support.google.com/googleplay/android-developer/answer/14151465](https://support.google.com/googleplay/android-developer/answer/14151465),
fetched 2026-08-02). Nothing in the register models this correlation.

**Trigger:** metric-(i) tally at D7; and any tester whose opt-in-to-first-open gap exceeds 48h
during D3–D7. **Mitigation owner:** HUMAN (the gate metrics are LOCKED; changing the
interpretation is a human gate decision, not an agent edit).

---

### V-3 — The production-access application will be filed for an app whose monetization layer does not exist yet · **HIGH** · Category: platform dependency

**The failure mechanism.** Google's application asks for *"whether testers exercised all app
features"* and *"whether usage patterns aligned with expected production-user behavior,"* plus a
summary of feedback and engagement
([answer/14151465](https://support.google.com/googleplay/android-developer/answer/14151465),
fetched 2026-08-02). Review is *"usually 7 days or less, but may occasionally take longer"* and
an unsuccessful applicant *"may be required to continue testing"* — the audit-verified +14 days
per cycle.

The plan files that application on **D15 (Aug 15)**, the day the clock completes
(`EXECUTION_PLAN.md` §7; `data/roadmap_56_days.csv:16` — *"Assemble production-access
application answers (testing learnings + changes made)"*). At that moment the build contains:
core sim, 20 levels, tutorial, accessibility. It does **not** contain placements/Paywalls
(D16), the 6 SKUs (D17), the 5 ad surfaces (D18), the 3 journeys (D19), or the store listing
(D20). Every commerce, ads and messaging feature — the entire monetization surface the store
listing will declare — is built **after** the application is filed.

So the honest answer to "did your testers exercise all app features" is *no, because most
features did not exist during the test window*, and the plan has no drafted answer for that
question anywhere. This is a **content** rejection risk, distinct from the base-rate rejection
risk the audit already modelled. It is not fixed by having 12 opted-in testers.

**Corroborating (INTERESTED-PARTY sources — commercial tester-recruitment vendors, treat as
directional only, not primary):** [testerscommunity.com](https://www.testerscommunity.com/google-play-production-access-rejected)
and [primetestlab.com](https://primetestlab.com/blog/google-play-app-rejection-rate-2026)
(both accessed 2026-08-02) claim 2026-era grading now weighs measured in-app engagement time,
recommend 20–25 testers rather than 12, 3+ releases across the 14 days, and 250+ character
answers on every form question. These vendors sell tester services and have every incentive to
overstate the bar — but the *direction* is consistent with Google's own "graded application"
language, and the plan's roadmap does comfortably clear the "3+ releases" bar (builds ship
almost daily). The actionable residue is: **draft the application answers now, not on D14**,
and be honest about feature coverage rather than surprised by the question.

**Trigger:** the drafted application answers not existing by D10 (Aug 10). **Owner:** HUMAN
drafts (attestations about testing are a human statement), agent can assemble the evidence pack.

---

### V-4 — Nothing in the plan gets installs from 100 to 3,000, and the plan says so itself · **HIGH** · Category: distribution / GTM

The audit verified that the revenue model is arithmetically perfect *given* its install anchor.
Nobody attacked the anchor. It does not survive contact.

**The arithmetic.** `data/revenue_scenarios.csv:3` states the organic install figures are
**anchors**, held identical across all three budget tiers, with no derivation: "800 / 3000 /
12000 organic anchors." The base case (3,000) with the modelled 13% store-listing CVR
back-solves to **23,077 listing views over the 37-day window ≈ 624 views/day** from a listing
with zero ranking history, no featuring, and no pre-registration (pre-registration is a binding
NON-GOAL, `EXECUTION_PLAN.md:207`).

**The plan's own estimate of its reachable audience contradicts it.** `specs/growth_aso_plan.md:342`,
arguing against pre-registration: *"at $0 budget with **no audience on Aug 1**, we would be
pre-registering the same **~50 people** we can simply message on launch day."* That is a 60×
gap between the audience the growth plan admits it has and the audience the revenue model
assumes, with no named mechanism carrying the difference at a demonstrated rate.

**The window shrank and the anchor didn't.** The model's 37 days assume an Aug 25 launch. The
LOCKED planning basis is P50 Sep 1–2 (~29 days) and P80 Sep 12–16 (~15–19 days). The same 3,000
anchor at P80 requires ~167 installs/day and ~1,280 listing views/day. Nobody re-derived it.

**The chosen ASO lane is already occupied.** The growth plan picks "train puzzle" over "cat
game" and concedes the trade: *"'Train puzzle' is a smaller total addressable query pool than
'cat game' — we deliberately trade reach for rankability"* (`growth_aso_plan.md:46`). But Cat
Train Tycoon (~547K installs) already ranks #1 for most cat+train queries — the audit's one
CHANGED fact-check finding (`AUDIT_FINDINGS.md:176`). Rankability against a 547K-install
incumbent in the identical lane is asserted, not established.

**The honest scoping.** The **first 100 users are fine**: 18–20 testers + the ~50 messageable
people + Shipaton Discord + Reddit + launch-day BIP clears 100 comfortably. The objection is
narrower and sharper: **installs 100 → 3,000 have no mechanism with a demonstrated rate**, and
every revenue-flavoured award argument (HAMM's "drive real revenue," the Grand revenue
shortlist) sits on top of that gap. At the conservative 800-install row the model yields
**$26.35 net across ~6 payers** — which still clears Shipaton's eligibility bar (≥1 real
RevenueCat purchase) but supports no revenue-based award case whatsoever.

**Trigger:** cumulative organic installs at launch + 10 days. **Owner:** HUMAN (the response is
a positioning decision — re-weight toward craft-judged categories — not a build change).

---

### V-5 — The plan's differentiation is concentrated in artifacts the judging funnel cannot see · **MEDIUM-HIGH** · Category: moat / competition

**The mechanism.** The plan's own Aug-1 verification of the official judging guide
(`EXECUTION_PLAN.md:97-103`, sourced to shipaton.com/blog/how-we-judge-shipaton) establishes
that ≥2 prescreeners score each targeted category **1–5 from the first 2 minutes of video plus
the text form only**, that category-specific questions gate which categories you are judged in,
and that ~100 apps reach the final round.

Now look at where the 412 hours go (`EXECUTION_PLAN.md` §3.1): a deterministic fixed-tick sim
with replay-hash CI, an 11-stage solver validation pipeline, a SHA-256 consumable ledger with
never-trimmed dedupe, refund revocation, a 45-event taxonomy, a deep-link router across
cold/warm/killed. Every one of those is invisible in 2 minutes of video. They are real
engineering and they are *the right engineering* — but as **competitive differentiation inside
this event** they are dark matter.

**Field size, measured today:** 10,966 registered participants on the Devpost page
([fetched 2026-08-02](https://revenuecat-shipaton-2026.devpost.com/)) versus 10,270 recorded on
Aug 1 (`EXECUTION_PLAN.md:83`) — ~700 in one day, on day 2 of a 61-day window, against ~50
prize slots. The marginal prescreen point is bought with video legibility and category-question
copy, and those are scheduled at **W7 (Sep 12–25)** — after the freeze, at the end of the most
fatigued stretch, in a plan whose own R-15 already names *"zero submission-video drafts by
Sep 15"* as an early-warning signal.

**Note what this objection is not.** It is not "the sim work is theater." The audit already
adjudicated that (`AUDIT_FINDINGS.md:424-426`) and I agree with it: the sim/solver pipeline is
the content factory and the capture rig. The objection is about **sequencing and hedging** —
the one channel that decides the award outcome is scheduled last and has no earlier proof point.

**Trigger:** existence of a ≥60s on-device gameplay cut and drafted category-question answers.
**Owner:** PROCESS (a roadmap re-sequencing decision at the human gate).

---

### V-6 — Ad serving outside the US has no consent (CMP/UMP) requirement anywhere in the plan · **MEDIUM** · Category: regulatory / platform dependency

**Not a stop-and-flag legal blocker.** I looked for one and did not find one. This is a
compliance gap with a revenue and policy consequence, not an illegality.

**The mechanism.** Serving personalized ads to users in the EEA, UK or Switzerland requires a
Google-certified CMP integrated with the IAB TCF; without one, eligible serving is limited to
Limited Ads
([support.google.com/admob/answer/13554116](https://support.google.com/admob/answer/13554116),
requirement in force since 2024-01-16; accessed 2026-08-02). Unity publishers implement this via
the UMP SDK
([developers.google.com/admob/unity/privacy/gdpr](https://developers.google.com/admob/unity/privacy/gdpr)).

The plan ships **broad country availability from the first production release with no staged
rollout** (PRD CM-R50.9, from `data/google_play_checklist.csv:30`) and five rewarded AdMob
surfaces. A repo-wide grep for `UMP|CMP|consent|GDPR|TCF` returns: one privacy-policy draft
sentence (`docs/plan/web/privacy/index.html:73`, "consent choices shown in your region"), one
product-spec aside (`specs/product_spec.md:297`, "no GDPR wall beyond the required consent"),
and nothing else. **Zero of the PRD's 57 requirements mention it. It has no acceptance
criterion, no SDK line in the pinned dependency list (`EXECUTION_PLAN.md:18-22`), and no
roadmap day.** The ad requirements CM-R34…CM-R37 have no consent gate at all.

**Concrete failure scenario:** ads go live at launch; EEA/UK traffic serves Limited Ads or
nothing; measured fill and eCPM come in below the modelled 82–94% / $6.50–13.00 band; the
Catvertising evidence pack (per-placement AdTracker charts) is built on degraded numbers; and
the Play Data Safety declaration — which CM-R45.2 promises to verify against a device proxy
capture — is being written against an ad stack whose consent posture is undefined.

**Cheap mitigation exists and preserves eligibility:** the rules only require US accessibility.
Restricting initial country availability to the US (or shipping with `ads_enabled=false`, which
CM-R33 already fully specifies) removes the exposure at zero engineering cost.

**Trigger:** D18 (ad-surface build day, Aug 18) with no consent flow in the build.
**Owner:** ARCHITECT to scope; HUMAN to decide country availability.

---

### V-7 — Lifecycle ceremony is an uncosted tax on the only resource that cannot be parallelised, owned by the person who also authors the process · **MEDIUM** · Category: execution / process risk · **JUDGMENT CALL**

**The mechanism.** The 412-hour estimate (`AUDIT_FINDINGS.md:429`) was computed against the
roadmap's engineering tasks. It contains no line item for the forge lifecycle. That lifecycle
is now enforced server-side: `state/PROJECT_STATE.md:19` records the wall as LIVE and states
*"ALL main-bound work now goes branch → PR → green CI → squash self-merge (human)."* On top:
one task contract per PR, TDD for Domain code, fresh-context review subagents, ADR per gate,
PROJECT_STATE.md updated every session, immutable paths.

Each of those is individually defensible. Collectively they convert a solo build into a stream
of **human-in-the-loop merge interrupts** on the one resource with no slack — the same person
who must also do every Play Console click, every device test, every payment, every BIP post,
and the four-platform social cadence in `growth_aso_plan.md` §12 (1 X post/day + 4–5 TikToks/wk
+ 3 Reels/wk + Shorts + 15 min/day Discord), none of which is obviously inside the 412 hours.

**The conflict of objectives, stated plainly.** The developer authors the toolkit this repo
runs on. That makes the project a dogfood exercise as well as a product deadline — a real,
legitimate second payoff (see Steelman §4.2), and simultaneously a standing incentive to run
*more* ceremony precisely when the calendar argues for less. The plan already anticipates this
(`EXECUTION_PLAN.md` §9.0 rule 1, sprint mode as the named fallback lever). D2's actual
behaviour suggests the anticipation is not self-executing.

**Trigger:** contract-start-to-merge latency > 24h twice in one week; or any day where console
critical-path items go untouched while lifecycle work proceeds. **Owner:** PROCESS + HUMAN.

---

### V-8 — The AI-capacity assumption has no tripwire in the units that actually bind · **LOW-MEDIUM** · Category: unit economics of execution

`state/PROJECT_STATE.md:8`: *"Monthly agent budget: $0 API — subscription capacity only (Claude
Max + Codex Pro/Max + local model). Stop-and-rethink trigger: >40% of budget in any week."*
With a $0 budget, "40% of budget" is undefined. The constraint that actually binds is
subscription rate limits, and there is no tripwire expressed in those units.

**I want to be honest that this objection is weaker than it looks.** Secondary reporting (vendor
blogs, not Anthropic primary — [truefoundry](https://www.truefoundry.com/blog/claude-code-limits-explained),
[explainx](https://www.explainx.ai/blog/claude-usage-limits-2026-timeline-explained), both
accessed 2026-08-02) describes a dual-layer scheme: a 5-hour rolling window plus a weekly cap on
active compute hours, shared across Claude Code, chat and Cowork, with Max tiers quoted around
140–280 h/week (5x) and 240–480 h/week (20x) of Claude Code. Against a human working 57–59 h/week,
that envelope is probably comfortable. The residual risks are the **shared bucket** (heavy chat
use in the same week silently reduces build capacity) and the fact that the plan's own hedge —
local-model execution — is explicitly gated behind "≥3 contracts merge clean" and limited to
pure-C# Domain work (`EXECUTION_PLAN.md` §9.0 rule 3), i.e. it is not available in weeks 1–3
when the load peaks.

**Trigger:** any weekly-limit warning, or any day where agent capacity rather than human
decision-making is the reason a roadmap row didn't close. **Owner:** HUMAN (restate the
stop-and-rethink trigger in capacity units, not dollars).

---

### V-9 — Unit economics: the business is structurally sound and structurally tiny, which makes every metric-driven artifact in the plan decoration · **LOW severity, HIGH decision-relevance**

Cost to serve is genuinely near zero and that is a real strength: client-only, no backend, Play
auto-backup + RC restore instead of accounts, RevenueCat free under $2,500 MTR
(`AUDIT_FINDINGS.md:170`), OneSignal Growth free for 3 months via the Ship Kit perk
(`EXECUTION_PLAN.md:39-40`). Cash at risk is ~$75–120 (Play $25 + domains ~$50).

Willingness to pay is modelled at 0.70/1.40/2.60% conversion against $6.99, giving the audit's
own base case of **~42 payers / $262 gross / $253.51 net** over 37 days
(`AUDIT_FINDINGS.md:115, :289`).

**The venture-layer arithmetic nobody in this corpus has written down:**

| Term | Value | Source |
|---|---|---|
| Hard cash cost | ~$100 | DAY1_RUNBOOK.md:22,106-108 |
| Founder time | 412 h / 56 days | AUDIT_FINDINGS.md:429 |
| Product revenue, base, 37-day window | $253.51 net | AUDIT_FINDINGS.md:115 |
| …at the locked P50/P80 launch dates | ~$142 (Sep 10) / ~$73 (Sep 20) | AUDIT_FINDINGS.md:212-214 |
| P(win ≥1 category) | ~19% | AUDIT_FINDINGS.md:399 (audit's estimate) |
| P(Grand) | ~0.5% | AUDIT_FINDINGS.md:400 (audit's estimate) |
| Prize tiers | $100k Grand; 30/20/10 BIP; 25/15/5 OneSignal; 15/10/5 most | devpost, fetched 2026-08-02 |
| Conditional mean prize given a win | ~$10,000 — **JUDGMENT CALL**, weighted toward 3rd places; nobody has published this distribution | mine |

→ EV ≈ (0.19 × $10,000) + (0.005 × $100,000) + ~$150–250 revenue − ~$100 ≈ **~$2,450**, or
**~$6 per founder-hour**.

**Why this matters, and it is not "therefore don't."** It is a *ranking rule*. At ~$6/hour,
any hour that does not raise P(launch inside window) or P(prescreen score) is worth less than
minimum wage. That arithmetic is the strongest available argument for three specific
decisions the human is about to make: take D-9 (buffer days) **yes**; take the §3.2 cut lines
**early rather than late**; and never trade sleep for levels 36–40 — which is exactly what
`EXECUTION_PLAN.md:559` already says, now with a number behind it.

**Trigger:** none — this is a standing prior for every scope decision. **Owner:** HUMAN.

---

### Candidate objections I examined and am NOT raising (recorded so they are not re-raised)

- **"A big publisher is entering the niche."** Bus Jam Meow (DoubleUGames) was the audit's
  missed-comp finding. I checked the live listing: the localized store view shows on the order
  of ~1,000 downloads
  ([play.google.com](https://play.google.com/store/apps/details?id=com.doubleugames.ng.grp2.bjm),
  accessed 2026-08-02 — install band only, not a verified global figure). It is a match-3, not a
  routing puzzle. **Not a competitive threat inside this window.** The real ASO adjacency is Cat
  Train Tycoon, folded into V-4.
- **Incumbent feature-release risk.** No incumbent will respond to a 3,000-install launch. The
  category does not apply here and manufacturing it would be noise.
- **Concept selection.** Independently re-scored by the audit against two purpose-built
  challengers; Cat Metro wins on externally verified axes (`AUDIT_FINDINGS.md:245-258`). I
  have no new evidence and will not re-litigate a settled question.
- **Unity pin / SDK version drift.** Re-checked today; both pins hold (see header table).
- **Competitive convergence inside Shipaton.** The Devpost project gallery is effectively empty
  on day 2 — there is nothing to measure yet. The plan's Monday convergence-watch
  (`EXECUTION_PLAN.md` §8.7b) is the right instrument; leave it running.

---

## 2. PRE-MORTEM — "It is 1 October 2026. Cat Metro missed the window, or shipped and won nothing."

Three *distinct* causes. Each with the indicator that shows up first, and when to look.

### Cause A — "We launched on September 17 and there was nothing left to measure."

**The story.** The clock started Aug 6 instead of Aug 1 because D-1/D-6 took four days to
answer and testers trickled in. Application filed Aug 20. Rejected Aug 27 ("testers not
sufficiently engaged" — the same 12 friends who also failed metric (i) at D7 and triggered a
48-hour YELLOW surgery). Re-applied Sep 3, granted Sep 12, live Sep 17. Thirteen revenue days.
The price experiment never ran, District Cup round 1 never happened, v1.1 never shipped, and
the cohort tables in the Devpost submission had denominators in the low hundreds. The app is
eligible, polished, and evidentially thin.

**Leading indicator:** the daily tester opt-in headcount. Not "12 invited" — **12 opted in,
continuously**. **Look:** every single day from now until production access is *granted*
(not until application). The first reading that matters is end of day 2026-08-05.

**Second indicator:** the gap between opt-in date and first `app_open` per tester. If that
exceeds 48h for more than 3 testers by D6, both V-2 and V-3 are live simultaneously.

---

### Cause B — "The gate said green, and it was wrong."

**The story.** Eleven of twelve testers — friends, personally recruited, told in writing that
Google grades their engagement — opened the app on a second day. Metric (i): pass. The gate
went green on D7 and nobody looked at the loop again. Forty-nine days of content, commerce, ads
and messaging were built on top of a core loop whose true unprompted return rate among
strangers was ~30%. The app launched, D1 retention came in under the 20% conservative floor,
the funnel leaked at L1–L5, and the honest BIP posts documented a well-engineered game that
players did not come back to. Nothing in the plan after D7 was capable of catching this,
because every subsequent gate measures build completeness, not desire.

**Leading indicator:** the composition of metric (i) — specifically, how many of the "unprompted"
opens fall within 24h of *any* message from the developer (invite, build-drop notice, thank-you,
BIP post the tester follows). **Look:** at the D7 tally itself, before ADR-0007 is written.
If more than half the qualifying opens are message-adjacent, the metric measured politeness.

**Second indicator, later and cheaper than it sounds:** the first 50 *non-tester* installs'
day-2 return rate, available within 72h of public launch. That is the first unbiased read the
project will ever get. If it lands below ~20%, Cause B already happened and the correct
response is a loop fix in v1.1, not more content.

---

### Cause C — "We built the best-instrumented app nobody watched for two minutes."

**The story.** Everything shipped. Deterministic sim, 30 solver-validated levels, six SKUs, a
dedupe ledger with a property test, 45 events, three journeys on a $19 plan, 56 of 56 BIP posts.
The submission went live Sep 15. Two prescreeners spent 120 seconds each on a video assembled in
the last fatigued week from whatever captures existed, against a field that had grown past
1,500 entries. It scored a 3. The app never reached the ~100 that get a real look, so the
RevenueCat advocate never downloaded it, and the entire evidence pack — the AdTracker charts,
the journey canvas, the honest cohort tables with denominators — was never opened by a human.

**Leading indicator:** the existence, on disk, of a ≥60-second on-device gameplay cut that a
stranger would watch to the end. **Look:** 2026-09-05 (D36), ten days earlier than R-15's
Sep 15 check — because if it doesn't exist on Sep 5 the correct response is to cut content
scope, and after Sep 11 (feature freeze, D42) there is no content scope left to cut.

**Second indicator:** drafted answers to the category-specific question for every targeted
category. An empty answer means not judged in that category (`EXECUTION_PLAN.md:168-169`).
Zero drafted answers on Sep 5 is the same signal, in text form.

---

## 3. PROPOSED KILL-CRITERIA (measurable, dated — for the human's hypothesis doc)

Phrased for `docs/prd/hypothesis.md`. These are *proposals*. Several touch LOCKED decisions and
therefore only the human gate can adopt them.

| # | Proposed tripwire | Derived from | Note |
|---|---|---|---|
| **KC-1** | *"We re-baseline the entire schedule to the P80 branch if 12/12 testers are not opted in and a seed AAB is not live on the closed track by end of **2026-08-05**."* | V-1 | 3 days of grace on a 2-day-old slip. |
| **KC-2** | *"We accept that we have **zero rejection capacity** against the latest-viable Sep 19 launch if the clock has not started by **2026-08-15**, and from that date the rejection branch is re-planned as the base case, not the contingency."* | V-1 (T+35 chain) | Derived arithmetic, shown in V-1. |
| **KC-3** | *"We answer D-1 (Play account creation date) by **end of 2026-08-03**."* | V-1 | Two minutes. It can delete KC-1 and KC-2 entirely. |
| **KC-4** | *"A RED on metric (i) alone does not execute Plan B until it is reproduced on a second, independent tester cohort by **2026-08-11 (D11)** — because at n=12 the metric passes a p=0.50 build only 61% of the time."* | V-2 | **Touches the LOCKED pre-registered gate.** Adopting this requires a human decision and — since the gate is pre-registered publicly in BIP post 1 — the confirmation rule must be published *in that same post*, before data exists, or not at all. |
| **KC-5** | *"We classify every qualifying metric-(i) open as message-adjacent or not, and report both numbers publicly. If >50% are message-adjacent, the gate result is reported as UNRESOLVED rather than PASS."* | V-2(b) | Costs nothing; preserves the locked metrics verbatim; fixes the directional bias. |
| **KC-6** | *"Drafted production-access application answers — including an explicit, honest statement of which features testers could and could not exercise — exist by **2026-08-10 (D10)**, or D14 is treated as at-risk."* | V-3 | Human-authored (it is an attestation). |
| **KC-7** | *"If cumulative organic installs are below **300 by launch + 10 days**, the 3,000-install base case is declared void, every downstream revenue claim is restated with the measured number, and award positioning re-weights to the craft-judged categories."* | V-4 | 300 ≈ 30% of the ~103/day base pace. **Threshold is a JUDGMENT CALL** — the human should set it. |
| **KC-8** | *"If no ≥60s on-device gameplay cut and no drafted category-question answers exist by **2026-09-05 (D36)**, we cut content scope (§3.2 cut-line steps 3+) and spend the recovered days on the submission funnel."* | V-5, Cause C | Pulls R-15's check 10 days earlier, before the D42 freeze removes the option. |
| **KC-9** | *"If AdMob ads are to serve outside the US, a Google-certified CMP/UMP flow is in the build and acceptance-tested before the first production release. If it is not present by **D18 (2026-08-18)**, we restrict initial country availability to the US or ship `ads_enabled=false`."* | V-6 | Both fallbacks preserve Shipaton eligibility (US access is the rules requirement). |
| **KC-10** | *"We restate the stop-and-rethink trigger in capacity units (rate-limit warnings / blocked agent-hours per week), not dollars, by **2026-08-09**. A $0 budget makes '>40% of budget' unmeasurable."* | V-8 | Housekeeping, but it is the only tripwire on a real constraint. |
| **KC-11** | *"If contract-start-to-merge latency exceeds 24h twice in one week, or any day passes with untouched console critical-path items while lifecycle work proceeds, we record it as a process cost and re-price ceremony at the next gate."* | V-7 | Measurable; also produces the forge-kit dogfood finding, so it pays for itself twice. |

---

## 4. STEELMAN — the two strongest points FOR this idea

### 4.1 The strategy correctly identifies that this event rewards honesty and legibility, and the plan is genuinely differentiated on exactly that axis.

This is not "build a great game and hope." The audit's clean-check list is the tell: product
IDs, prices, entitlement IDs, offering composition, placement wiring, the rewind rule, and all
five ad-cap sets are **identical across every file**; `example_levels.json` passes real ajv
draft-2020 validation; all 14 CSVs are structurally clean; 45 events exist exactly as claimed
(`AUDIT_FINDINGS.md:339-351`). Layer on a **publicly pre-registered kill-gate published before
data exists** (`EXECUTION_PLAN.md:136`), a standing rule that no published claim may be
falsifiable from the project's own files, and 56 daily posts with denominators and vintages —
and you have a #BuildInPublic entry ($30k first tier) and a HAMM entry that are hard to fake and
hard to match. OneSignal's third criterion is verbatim *"Resourcefulness and creativity"*
(`AUDIT_FINDINGS.md:165`), and "three journeys and six message steps on a $19 plan, with the
cap arithmetic published" is a better fit for that sentence than most well-funded entries will
manage. The plan aimed at the categories that reward its actual strengths. That is the correct
read of the event, and my objections V-4 and V-5 are about *reach and legibility*, not about
whether the entry deserves to win.

### 4.2 The downside is genuinely bounded and the residual assets survive every failure mode.

Cash at risk is ~$100. Cost to serve is ~$0 by design — no backend, no accounts, no infra bill
that outlives the window. And the assets that survive a total award whiff are real: a
deterministic fixed-tick sim with a solver and an 11-stage content-validation pipeline is a
reusable content factory; the forge-kit dogfood produces upstream findings whether or not Cat
Metro wins anything; and the concept itself survived independent adversarial re-scoring against
two purpose-built challengers (7.05 vs 6.25 / 6.65 / 6.30, `AUDIT_FINDINGS.md:249-257`). A ~19%
shot at a ≥$5,000 prize plus a portfolio artifact plus a validated toolkit, for ~$100 and eight
weeks, is not an irrational allocation. **Every objection in §1 is about sequencing and
tripwires — none of them argues the bet should not be taken.**

---

## 5. WHAT WOULD CHANGE MY MIND

| Objection | The specific evidence that retires it |
|---|---|
| **V-1** (clock slip) | Either (a) D-1 returns "account created before Nov 13 2023" — the 12/14 gate does not apply and the mechanism is void; or (b) a Play Console screenshot showing 12/12 opted in with a seed AAB live on the closed track, dated on or before 2026-08-05. Nothing else. Invitations sent ≠ testers opted in. |
| **V-2(a)** (gate power) | A pre-registered decision rule that survives the arithmetic — e.g. a published confirmation step (KC-4) or an explicit human statement that a 39% false-RED rate at p=0.50 is accepted with eyes open. The number is not disputable; only the response to it is. |
| **V-2(b)** (contaminated metric) | A message-adjacency breakdown of the D7 metric-(i) opens (KC-5). If <25% of qualifying opens are message-adjacent, the contamination is immaterial and I withdraw the bias claim. |
| **V-2(c)** (correlated risks) | A risk-register edit that links R-01 and R-02 to a shared root cause with a single early-warning signal. Cosmetic to write, and it makes the correlation visible at the gate where it matters. |
| **V-3** (application content) | Drafted answers to the production-access questions — specifically to "did testers exercise all app features" — reviewed against `answer/14151465` before D14. If the honest answer turns out to be adequate, this objection dies on the spot. Alternatively: any primary Google source stating feature coverage is not weighted. |
| **V-4** (install anchor) | A named acquisition mechanism with a *measured* rate — e.g. an actual BIP follower count and observed click-through by ~Aug 20, or a Reddit/Discord post with a measured install conversion. One real data point beats the anchor. Failing that, a restatement of the base case at the conservative 800-install row, which the model already contains and which changes no award argument that matters. |
| **V-5** (invisible differentiation) | A ≥60-second on-device gameplay cut existing before Sep 5 that a stranger watches to the end, plus one drafted category-question answer per targeted category. Both are checkable artifacts, not opinions. |
| **V-6** (consent gap) | Either a UMP/CMP flow with an acceptance criterion in the requirements set, or a recorded decision to restrict initial availability to the US. Either retires it completely. |
| **V-7** (ceremony tax) | Two weeks of merge-latency data showing contract-to-merge under 24h with no console-path days lost. If the ceremony is cheap in practice, this was a theoretical worry and should be retired on the record at `/forge-retro`. |
| **V-8** (AI capacity) | A tripwire expressed in capacity units. That is the whole ask; I do not expect the limit itself to bind. |
| **V-9** (EV / $6 per hour) | Nothing — the arithmetic is what it is, and it is not an argument against proceeding. It retires only if the human states a different objective function (portfolio, toolkit validation, the enjoyment of the thing), in which case the *ranking rule* it implies should still be adopted. |

---

## 6. ESCALATIONS

**Legal / regulatory blockers found: none.** I looked specifically for a stop-and-flag and did
not find one. The consent gap (V-6) is a compliance *gap* with cheap mitigations, not an
illegality. Two items remain open from the audit's own unverifiable list and are unchanged: the
USPTO/EUIPO trademark screens for "Cat Metro" and "Meowtro" (`AUDIT_FINDINGS.md:565-567`), and
the promo-codes × purchases-unity coexistence test. Neither is mine to close.

**One tail risk worth naming because it has no branch anywhere.** The venture is single
platform, single store, single developer account, with a package id that becomes permanent at
first upload. There is a rejection branch (`EXECUTION_PLAN.md:130-133`) but no **suspension**
branch. The audit recorded that a self-identified Play reviewer warns tester-exchange patterns
can flag accounts by association (`AUDIT_FINDINGS.md:447-450`) — the plan's mitigation is
"recruit from personal network," which is correct and sufficient. But if an account-level
enforcement action arrives on, say, Aug 20, the window ends with no recovery path. Probability
low; impact total; cost of naming it: one row in the risk register.

---

*Prepared 2026-08-02 by the venture-critic for the hypothesis gate. This document recommends
nothing and decides nothing. The decision to proceed, amend, or stop is the human's, and it is
on the never-delegate list. Revisit at `/forge-retro`: retire V-8 and V-7 if they age badly;
escalate V-1, V-2 and V-4 to the kill-criteria review if they confirm.*
