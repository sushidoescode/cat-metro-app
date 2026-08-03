# GitHub Issue Backlog — Cat Metro

Status: v1.0, 31 Jul 2026. Governed by `deliverables/DECISIONS_BRIEF.md` (locked 31 Jul 2026).
Aligned day-for-day to `deliverables/data/roadmap_56_days.csv`. Specs referenced: `specs/architecture.md`,
`specs/product_spec.md`, `specs/monetization_spec.md`, `specs/onesignal_retention.md`,
`specs/liveops_spec.md`, `specs/revenuecat_implementation.md`, `specs/growth_aso_plan.md`,
`specs/submission_script.md`.

**49 issues across 6 milestones.** IDs below (`CM-001` …) are *backlog* IDs, stable across tools —
GitHub's own issue numbers will differ. Put the backlog ID at the front of every issue title so
dependency references survive.

Repo: `cat-metro` (private until launch). Android package: **`com.catmetro.game`** (FROZEN
2026-08-01 per AMD-04; matches `roadmap_56_days.csv` D1 — it cannot be changed after the first
AAB upload; the CM-009 package-id conflict is resolved). Handle: **@CatMetroGame**.

---

## 1. Milestones

| Milestone | Dates | Roadmap days | Exit gate (from the roadmap CSV) | Issues |
|---|---|---|---|---|
| **M1 — Vertical Slice** | **Aug 1–7** | D1–D7 | **FUN GATE (D7):** ≥5 of 12 testers replay ≥1 level unprompted; median session ≥3 levels; testers explain the goal unaided | CM-001 → CM-009 |
| **M2 — Level System** | **Aug 8–14** | D8–D14 | **LEVEL-SYSTEM GATE (D14):** 20 levels solver-validated in CI; RC sandbox purchase + rewarded ad + push each pass on the current device build; Play Console shows 12 testers continuous for 14 days | CM-010 → CM-018 |
| **M3 — Commercial Beta** | **Aug 15–21** | D15–D21 | **COMMERCIAL-BETA GATE (D21):** 100% smoke pass; purchases/restore/refund-revoke + 5 ad surfaces + 3 journeys + offline campaign all green; crash-free ≥99.5% across beta sessions | CM-019 → CM-028 |
| **M4 — Public 1.0** | **Aug 22–28** | D22–D28 | **STORE-READY GATE (D24)** then **PUBLIC LAUNCH WINDOW (D24–28):** production submission in; Cat Metro 1.0 live on Google Play, accessible from the USA, release date logged | CM-029 → CM-035 |
| **M5 — Growth & Experiments** | **Aug 29 – Sep 18** | D29–D49 | **D35 RETENTION GATE** (D1 ≥22% floor; Daily participation ≥25% of DAU; top-2 levers chosen), **D35b FUNNEL GO/NO-GO** (default NO-GO), **D42 CONTENT-COMPLETE GATE** (feature freeze begins) | CM-036 → CM-043 |
| **M6 — Submission** | **Sep 19–25 + Sep 26–30 buffer** | D50–D56 | **SUBMISSION-READY GATE (D54, Sep 23)** then submitted by **Sep 29 18:00 PDT**; absolute deadline **Sep 30 11:45pm PDT** | CM-044 → CM-049 |

Milestone rule: an issue may only move to a later milestone by an explicit Cut decision recorded in an
ADR. Slipping work silently across a gate is how 8-week schedules die.

---

## 2. Label taxonomy

**type/** — exactly one per issue.

| Label | Meaning |
|---|---|
| `type/feat` | New player-facing or system capability |
| `type/fix` | Defect repair against shipped behavior |
| `type/spike` | Time-boxed investigation with a written outcome; never merges gameplay code |
| `type/chore` | Tooling, config, accounts, dependency, repo hygiene |
| `type/content` | Levels, copy, art, audio, event rounds |
| `type/test` | Test coverage or QA harness work |
| `type/ops` | Store, release, monitoring, live-ops runbook |
| `type/docs` | Specs, ADRs, README, submission text |
| `type/growth` | ASO, marketing, community, press, build-in-public |

**area/** — one or more; mirrors the asmdef layout in `architecture.md`.

`area/domain` · `area/application` · `area/content` · `area/presentation` · `area/services` ·
`area/integrations-rc` · `area/integrations-onesignal` · `area/integrations-ads` ·
`area/integrations-analytics` · `area/editor` · `area/ci` · `area/build` · `area/store` · `area/art` ·
`area/audio` · `area/marketing`

**priority/** — exactly one. Matches the brief's P0/P1/P2/Cut vocabulary.

| Label | Meaning |
|---|---|
| `P0` | Launch-blocking. Cutting it moves the launch date |
| `P1` | Ships at 1.0 if the schedule holds; degrades cleanly if cut |
| `P2` | Post-launch or stretch; never on the critical path |
| `Cut` | Explicitly decided against; kept visible so it is not re-proposed |

**award/** — traceability from work to the award slate (brief §AWARD TARGETING).

`award/best-game` · `award/hamm` · `award/catvertising` · `award/onesignal` · `award/bip` ·
`award/design` · `award/grand`

**status/** and **risk/**

`status/blocked` · `status/needs-device` (cannot be verified in Editor) · `status/needs-review` ·
`status/awaiting-external` (Play review, RC beta access, plan approval) · `risk/high` ·
`risk/policy` (Play policy or event-rules exposure) · `gate` (a scheduled go/no-go, not ordinary work)

**size/** — estimate, not a commitment. `size/S` ≤2h · `size/M` 2–6h · `size/L` 6–12h ·
`size/XL` >12h → **must be split before it can be assigned**.

Conventions: one issue = one merged PR wherever possible; `gate` issues carry the go/no-go criteria in
their body verbatim from the roadmap CSV and close with an ADR link; anything labeled
`status/awaiting-external` gets a daily comment with the current external state so blockage is visible
rather than assumed.

---

## 3. M1 — Vertical Slice (Aug 1–7)

**CM-001 — Bootstrap repo, Unity 6000.3.16f1 project, and CI skeleton**
`type/chore` `area/build` `area/ci` `P0` `size/L`
AC: Private repo `cat-metro` with the `architecture.md` asmdef folder layout; Unity **6000.3.16f1**
project (IL2CPP, ARM64-only, URP, Input System, minSdk 25, targetSdk 36, package `com.catmetro.game`);
GitHub Actions compiles and runs EditMode tests on every PR.
Deps: none. **Blocks everything.** Do **not** upgrade past 6000.3.16f1 (Gradle 9/AGP 9.0 breaks
unverified SDKs until GMA/RC/OneSignal confirm compat — brief).

**CM-002 — Day-1 account and service provisioning**
`type/chore` `area/store` `area/integrations-rc` `area/integrations-onesignal` `area/integrations-ads` `P0` `size/L` `status/awaiting-external`
AC: Play Console app created with a **closed testing track** and 12 tester invites sent (the 14-day
continuous-tester clock starts Aug 1–2); RevenueCat project created, **RevenueCat Ads beta access
requested on the dashboard Ads page**, and the project's plan tier recorded (Experiments/Targeting are
Pro-gated); OneSignal app on the **Growth plan ($19/mo)** with the FCM v1 service-account JSON
uploaded; AdMob account + Android app id; Firebase project `catmetro-prod`.
Deps: none. Blocks CM-005, CM-007, CM-008, CM-018. The tester clock is the longest external
dependency in the whole project — it starts today or the launch date moves.

**CM-003 — Domain core: 8-tick deterministic simulation**
`type/feat` `area/domain` `P0` `size/L`
AC: Pure C# `CatMetro.Domain` with a 125 ms fixed tick, PCG32 seeded RNG as the only RNG, an appended
command log, and `SimulationState` snapshots; an EditMode replay-hash test proves
`(levelId, seed, commandLog) → identical outcome`.
Deps: CM-001. Blocks CM-004, CM-010, CM-013, CM-019. No `UnityEngine` reference in this assembly —
the dependency rule is enforced in review.

**CM-004 — Greybox playable: sources, switches, stations, queues, overflow**
`type/feat` `area/domain` `area/presentation` `area/content` `P0` `size/L` `status/needs-device`
AC: L001 authored in schema v2 JSON, loaded through the Content importer, and playable in Editor and
on a Pixel-6a-class device; win by delivering the required cats, fail by platform overflow; 60 fps on
mid-tier.
Deps: CM-003. Blocks CM-005. If the known Unity 6 URP Android frametime regression appears, drop to a
minimal URP profile and log findings for CM-029.

**CM-005 — Fail/retry loop with cause-first feedback**
`type/feat` `area/presentation` `area/application` `P0` `size/M` `status/needs-device`
AC: Instant retry measured at **<1 s** tap-to-playing with no scene reload; the failure camera pans to
the overflowed platform within **1.5 s** of the fail; next-wave preview HUD is live.
Deps: CM-004. Blocks CM-009 (fun gate). Next-wave preview may slip to M2 if timing is tight — the
sub-second retry may not.

**CM-006 — Android build pipeline + RevenueCat purchase proven on device**
`type/chore` `type/spike` `area/build` `area/integrations-rc` `P0` `size/L` `status/needs-device`
AC: Custom `mainTemplate.gradle` + `gradleTemplate.properties` under version control; keystore created
and Play App Signing enrolled; **16 KB page-size audit** of every native lib passes; purchases-unity
**9.7.0** + EDM4U **1.2.188** installed with exactly one EDM4U copy and a single BillingClient in the
merged manifest; an RC **Test Store** sandbox purchase of `cm_rewind_5` succeeds on device and the
entitlement cache survives airplane mode.
Deps: CM-001, CM-002. Blocks CM-021, CM-022. Do **not** install Unity IAP (duplicate BillingClient
risk — brief).

**CM-007 — Save v1 and Android lifecycle safety**
`type/feat` `area/application` `area/services` `P0` `size/M`
AC: Versioned JSON save with atomic temp-then-rename writes and a migration table; save + analytics
flush completes inside the **50 ms** `OnApplicationPause` budget; a process-death kill during save
loses no committed state.
Deps: CM-003. Blocks CM-023 (the purchase ledger builds on this durability guarantee).

**CM-008 — OneSignal 5.3.2 spike: push on device + identity spine**
`type/spike` `area/integrations-onesignal` `area/services` `P0` `size/M` `status/needs-device`
AC: OneSignal Unity **5.3.2** installed with custom Gradle templates and Force Resolve; a test push is
received on **2 physical devices**; boot order is RC `configure(cm_<uuid>)` → `OneSignal.initialize` →
`OneSignal.Login("cm_<uuid>")`, and the RC subscriber attribute `$onesignalUserId` is set.
Deps: CM-002, CM-006. Blocks CM-026. If a Gradle conflict blocks the build, move the spike to CM-012's
day and ship the tester build without it.

**CM-009 — D7 FUN GATE + spec reconciliations + ADR-0007**
`type/docs` `gate` `P0` `size/S`
AC: Gate criteria met and recorded (≥5 of 12 testers replay ≥1 level unprompted; median session ≥3
levels; testers explain the goal unaided) with an evidence pack and ADR-0007; **and** two spec
conflicts are reconciled in writing before any store asset is built: (a) Android package id —
RESOLVED 2026-08-01 (AMD-04): frozen at `com.catmetro.game` in every deliverable; record it in the ADR; (b)
bonus-district name — RESOLVED 2026-08-03 (AMD-03): **Night Harbor** (L901–L910) everywhere;
`product_spec.md` §22 edited to match `monetization_spec.md` §3.6. Both choices are now applied in the
files; record both in the ADR.
Deps: CM-005 (playtests), CM-001 (package id must be frozen pre-first-AAB). Fallback if the gate
fails: 48h of mechanic surgery (tick speed / queue caps / wave pacing), re-gate on D9. The concept
stays — the brief's verdict is KEEP; redesign pacing, not premise.

---

## 4. M2 — Level System (Aug 8–14)

**CM-010 — Solver v1 (BFS) + Editor solver runner**
`type/feat` `area/domain` `area/editor` `P0` `size/L`
AC: BFS solver for ≤2-switch boards that calls **the exact Domain step function** (no parallel sim);
Editor runner proves L001–L008 solvable and reports minimum switch counts.
Deps: CM-003. Blocks CM-011. Any divergence between solver and game step function is a P0 defect, not
a tuning issue.

**CM-011 — Beam-search solver, batch validator CLI, and the CI content gate**
`type/feat` `type/ci` `area/editor` `area/ci` `P0` `size/L`
AC: Beam search at widths 1k/2.5k/5k; `tools/BatchValidator` CLI; a GitHub Action runs level validation
on every content PR and **fails a deliberately broken level** while passing L001–L012.
Deps: CM-010. Blocks CM-016, CM-027, CM-039. This CI job is the mechanism that makes "every level
solvable free" a true statement rather than a marketing line — it is quoted as such in the paywall
footer, the store listing, and the Devpost submission.

**CM-012 — Rewarded ads end-to-end + RevenueCat AdTracker wiring**
`type/feat` `type/spike` `area/integrations-ads` `area/integrations-rc` `area/services` `P0` `size/L` `status/needs-device` `award/catvertising`
AC: `IAds` interface + `CatMetro.Integrations.Ads` adapter asmdef; Google Mobile Ads Unity **11.3.0**
loads and shows a rewarded test ad on device and the reward callback fires **exactly once**; **AdTracker
manually wired** for all five event types (`TrackAdLoaded`, `TrackAdDisplayed`, `TrackAdOpened`,
`TrackAdRevenue`, `TrackAdFailedToLoad`) and all five are visible in the RC dashboard.
Deps: CM-002, CM-006, CM-008. Blocks CM-025, CM-042. The AdMob convenience module is **not** for Unity —
manual AdTracker integration is the only path. Fallback: spike AppLovin MAX 8.6.4 if GMA init or fill
fails on the device matrix; if both fail by D14, the Model A contingency fires and Catvertising is
dropped from the slate.

**CM-013 — Mechanics 3 and 4: second source and wildcard commuter**
`type/feat` `area/domain` `area/content` `P0` `size/M`
AC: Second-source and wildcard-commuter rules implemented in Domain **and** in the solver; L016–L020
authored against them and validated.
Deps: CM-003, CM-010. Blocks CM-017. Cooldown and gates stay post-launch (bands 31–60, locked) — never
pull them forward to fix pacing.

**CM-014 — Tutorial beats and accessibility mode**
`type/feat` `area/presentation` `area/application` `P0` `size/L`
AC: Tutorial beat system covering L001–L003 with a fresh player clearing them unaided in a supervised
test; planning-pause (hold 400 ms) freezes the sim with switches still tappable; haptics tiers via the
JNI helper, with independent haptics and motion toggles.
Deps: CM-004. Blocks CM-017. Trim to 2 beats if players stall on beat 3.

**CM-015 — Daily Line seeded mode behind `daily_enabled`**
`type/feat` `area/domain` `area/application` `P1` `size/L`
AC: Seed derived as `lower32(SHA-256("CM-DAILY-1|" + dateKey + "|" + k))` from the **local** ISO date
string; unlocks after L7; the same date produces a **byte-identical board on two devices**; the
generator constant is frozen through Sep 30.
Deps: CM-013, CM-011. Blocks CM-026 (Journey 1 entry), CM-036, CM-043. `tools/DailyValidator` pre-plays
90 dates in CI and bakes `daily_overrides.json`; runtime falls back to the same local salt-increment
loop for dates beyond the table.

**CM-016 — Content batch: levels L001–L020 authored and validated**
`type/content` `area/content` `P0` `size/XL — split per district` `award/best-game`
AC: 20 levels authored to schema v2 across Whisker Yard → Twin Platforms, each solver-validated in CI
with 3-star thresholds proven achievable; difficulty curve reviewed against solver stats.
Deps: CM-011, CM-013. Blocks CM-018 (the D14 gate counts these). Split into 4 sub-issues of 5 levels so
no single issue is `size/XL` when assigned. Fallback: ship 17 if three resist validation, and restore
before CM-030.

**CM-017 — Typed analytics choke point, offline queue, and OneSignal custom events**
`type/feat` `area/integrations-analytics` `area/integrations-onesignal` `area/services` `P0` `size/L`
AC: One typed wrapper where an **unknown event name asserts in dev builds**; offline event queue
survives process death; all P0 taxonomy events fire on device; Crashlytics symbol upload runs in CI and
a forced test crash arrives symbolicated; a OneSignal custom event (`level_failed`) is received in the
dashboard.
Deps: CM-007, CM-008, CM-014. Blocks CM-026, CM-042, CM-046. Economy/ads/monetization taxonomy rows
stay dark until their surfaces exist.

**CM-018 — D14 LEVEL-SYSTEM GATE + production-access application package**
`type/docs` `gate` `P0` `size/S` `status/awaiting-external`
AC: 20 levels solver-validated in CI; RC sandbox purchase, rewarded ad, and push each re-verified on
the current build; Play Console shows **12 testers continuous for 14 days**; the production-access
application answers (testing learnings + changes made) are written and ready to submit; ADR-0014 signed.
Deps: CM-006, CM-008, CM-012, CM-016, CM-017. Blocks CM-020. If the clock ran short from late opt-ins,
apply the day it completes — launch still fits Aug 24–28.

---

## 5. M3 — Commercial Beta (Aug 15–21)

**CM-019 — Rewind system via command-log truncation**
`type/feat` `area/domain` `area/application` `P0` `size/M` `award/hamm`
AC: Rewind returns the sim to the last safe decision tick by truncating the command log and replaying
deterministically; an EditMode test proves the rewound run is identical to a fresh run with the same
truncated log; rewind is never required to finish any level.
Deps: CM-003. Blocks CM-023, CM-025.

**CM-020 — Submit the production-access application, content rating, and data safety**
`type/ops` `area/store` `P0` `size/M` `status/awaiting-external` `risk/policy`
AC: Production-access questionnaire submitted (Console shows it in review); IARC content-rating
questionnaire complete with target audience **13+** and no under-13 age group declared; data safety
draft mapped from the taxonomy's `privacy_class` column and reconciled against actual network traffic.
Deps: CM-018. Blocks CM-032. This is the single longest-lead external item after the tester clock — if
Console gates eligibility to Aug 16, submit then.

**CM-021 — RC Placements ×5 + Paywalls v2 on `post_level_5` + custom fallback**
`type/feat` `area/integrations-rc` `area/presentation` `P0` `size/L` `status/needs-device` `risk/high` `award/hamm`
AC: All five placements wired (`post_level_5`, `theme_preview`, `bonus_district`, `shop`,
`rewind_failure`); RC **Paywalls v2** renders `post_level_5` on **3 physical devices with zero
crashes**; the pixel-matched custom Unity fallback is behind a flag and switches instantly.
Deps: CM-006, CM-019. Blocks CM-024, CM-028. Three open Android Paywalls-v2 crash issues
(#745/#736/#732) make this the highest-risk purchase-path item in the project — device-test heavily and
ship the custom paywall as primary if any crash reproduces.

**CM-022 — Play products ×6, pricing templates, and RC catalog**
`type/ops` `area/store` `area/integrations-rc` `P0` `size/M` `award/hamm`
AC: Play products created — `cm_all_access` $6.99, `cm_supporter_pack` $9.99, `cm_theme_sakura` $2.99,
`cm_theme_neon` $2.99 (non-consumable), `cm_rewind_5` $1.99, `cm_rewind_20` $4.99 (consumable) — each
linked to a pricing template (`tpl_cm_199/299/499/699/999`); RC entitlements `all_access`, `supporter`,
`theme_sakura`, `theme_neon` created with `cm_supporter_pack` attached to **both** `supporter` and
`all_access`; default offering `ofr_core` packaged to the placements.
Deps: CM-006, CM-021. Blocks CM-023, CM-038. Also create the experiment SKU `cm_all_access_499`
(Play prices are per-SKU, so a price test needs its own product).

**CM-023 — Purchase, restore, refund-revocation, and the consumable ledger**
`type/feat` `area/application` `area/integrations-rc` `P0` `size/L` `status/needs-device` `award/hamm`
AC: All six SKUs purchase in sandbox on device; restore works on a fresh reinstall; the durable
consumable ledger has double-grant protection verified by killing the app mid-purchase; Billing 8
**pending purchases** grant only on confirmed completion; `entitlement_changed(revoked)` relocks
content at a session boundary with progress and stars retained.
Deps: CM-007, CM-019, CM-022. Blocks CM-028. Zero ledger mismatches is a hard gate, not a target.

**CM-024 — `OfferEligibilityService` + payer suppression matrix**
`type/feat` `type/test` `area/application` `P0` `size/M` `award/hamm`
AC: One choke point decides every commerce-surface impression; EditMode tests prove (a) **no
`paywall_viewed` or `ad_offer_viewed` can fire on attempt 1 of any level**, (b) an `all_access` or
`supporter` holder sees zero system-initiated paywalls, (c) refund-revoked users get 30 days of
system-paywall suppression, (d) max one system-initiated commerce surface per session.
Deps: CM-021, CM-023. Blocks CM-028. This service is where the fair-by-design manifesto becomes
testable code — every rule in `monetization_spec.md` §3.0 lives here or nowhere.

**CM-025 — Five rewarded surfaces with caps + the tickets economy**
`type/feat` `area/application` `area/integrations-ads` `P0/P1 per surface` `size/L` `status/needs-device` `award/catvertising`
AC: `rewind_failure` (2/session, 5/day — **P0**), `double_tickets` (3/day), `daily_gift_double` (1/day),
`streak_saver` (1/day), `theme_rental` (3 levels, 1/theme/day) all grant **exactly once** including a
kill-app-mid-ad case; caps suppress correctly; declining never penalizes; tickets earn 20–50/level, 100
first daily, 30–80 daily gift, with 600–1,200 cosmetic sinks and nothing gameplay-gated.
Deps: CM-012, CM-019. Blocks CM-042. Cut `theme_rental` last if cap logic drags — the other four are
the Catvertising core. Streak saver is rewarded/free only and is **never** sold.

**CM-026 — Three OneSignal journeys, deep-link router, and local-notification backup**
`type/feat` `area/integrations-onesignal` `area/application` `P0` `size/L` `status/needs-device` `award/onesignal`
AC: J1 daily+streak (2 message steps), J2 lapse ladder 48h→7d→14d (3 steps), J3 hard-level help (1 step)
all fire from test events on device; `catmetro://daily|home|level/{id}|event/{id}|shop|restore|feedback`
route correctly from **cold, warm and killed** states; Time Windows block 21:00–09:00 delivery;
client-side caps enforce max 1 push/day and 3/week; Unity Mobile Notifications back up streak expiry.
Deps: CM-008, CM-015, CM-017. Blocks CM-043, CM-044. If a journey misfires, fall back to one-off
scheduled sends — a single deployed message already satisfies award eligibility.

**CM-027 — Content: levels 21–30 + bonus district L901–L910**
`type/content` `area/content` `P0` `size/XL — split by district` `award/best-game`
AC: L021–L030 (Catnip Gardens, Midnight Terminus) and the bonus district L901–L910 authored and
solver-validated in CI; the bonus district uses **launch mechanics only** and gates no progression.
Deps: CM-011, CM-016. Blocks CM-030, CM-032. Split into three sub-issues. Fallback: if the bonus
district is red at D21, it ships in the first Sep update and All Access buyers get it as an automatic
content drop — but the paywall copy must then say "arriving this week" and never sell an undated thing.

**CM-028 — D21 COMMERCIAL-BETA GATE: full smoke on the device matrix**
`type/test` `gate` `P0` `size/M` `status/needs-device`
AC: 100% smoke-checklist pass on 4 devices including a $150-class low-end; purchases, restore,
refund-revoke, all 5 ad surfaces, all 3 journeys, and the offline campaign green; crash-free **≥99.5%**
across beta sessions; build 4 delivered to all 12 testers; ADR-0021 signed.
Deps: CM-021, CM-023, CM-024, CM-025, CM-026. Blocks CM-029. Fallbacks are pre-declared: flip the
paywall to the custom fallback, swap GMA to AppLovin MAX 8.6.4, cut `theme_rental` — **the launch date
does not move.**

---

## 6. M4 — Public 1.0 (Aug 22–28)

**CM-029 — Release-candidate hardening and performance pass**
`type/chore` `area/build` `area/presentation` `P0` `size/L` `status/needs-device`
AC: ≤16.6 ms p50 / ≤22 ms p95 frame time on mid-tier and stable 30 fps on low-end during max wave;
≤350 MB PSS on a 3 GB device; ≤60 MB AAB; R8 keep rules for Billing, GMA and OneSignal receivers
verified on a minified release build; final 16 KB page-size and targetSdk 36 audit; Play pre-launch
report warnings addressed.
Deps: CM-028. Blocks CM-031, CM-032.

**CM-030 — Store and submission asset kit**
`type/growth` `area/art` `area/store` `area/marketing` `P0` `size/L` `award/design`
AC: 1024×1024 icon (Concept A "Conductor"); **6 portrait 1080×1920 screenshots** with the locked
captions; 1024×500 feature graphic; **1179×2556 frameless** screenshot rendered natively (not upscaled);
30-second listing promo video; Unity Recorder 5.1.6 takes banked at 1080×1920 for the sub-2-minute
submission video. Every asset matches spec pixel-exact and passes the internal Families-risk review
(nothing reads as child-directed).
Deps: CM-027, CM-029. Blocks CM-032, CM-045. Full briefs in `growth_aso_plan.md` §4–§7 and §23.

**CM-031 — Store listing copy final**
`type/growth` `area/store` `area/marketing` `P0` `size/M` `risk/policy`
AC: Title `Cat Metro: Train Puzzle` (23 chars), short description option A (72 chars), the 2,338-char
long description, target audience 13+, "Contains ads" and "In-app purchases" labels correct, privacy
policy live at catmetro.com/privacy; a CI regex gate fails the build if any in-app `pw_*`/shop string
contains a currency symbol or a price pattern.
Deps: CM-022, CM-029. Blocks CM-032. Copy is locked in `growth_aso_plan.md` §3 — do not improvise it in
Console.

**CM-032 — D24 STORE-READY GATE: submit the production release**
`type/ops` `gate` `P0` `size/M` `status/awaiting-external`
AC: Production access granted; v1.0.0 AAB built from the release branch and submitted (status **In
review**); listing 100% complete; staged-rollout plan documented with halt criteria (crash-free <99% or
ANR >0.47%); **Play one-time promo codes minted for `cm_all_access` and one redeem-tested on a clean
device**; ADR-0024 signed.
Deps: CM-020, CM-029, CM-030, CM-031. Blocks CM-034. SUBMIT-ON-GRANT: the first production release is
submitted the day production access is granted (P50 ~Aug 20–21), using the current commercial-beta
build, managed publishing ON, publish held — the polished 1.0 ships as an immediate update. First-release
review can take up to 7 days, so launch Aug 24–28 is the best case; planning basis P50 Sep 1–2,
P80 Sep 12–16.

**CM-033 — Launch comms kit: press, Devpost shell, community, Discord**
`type/growth` `area/marketing` `P1` `size/L` `award/bip`
AC: Press kit live at catmetro.com/press (descriptions, fact sheet, 6 screenshots, 3 GIFs, logo pack,
1024² icon, 30s video, differentiation one-pager); 10 named press targets with routes confirmed on
their sites the day of sending; Devpost project shell created with the P0 category mapping; 5 Reddit
drafts written against each sub's current self-promo rules (including the r/incremental_games mod DM);
Discord server created with 5 channels; @CatMetroGame handles claimed.
Deps: CM-030. Blocks CM-034, CM-047. Detail in `growth_aso_plan.md` §10–§15. These are the review-wait
days — do the work that does not touch the binary.

**CM-034 — D27 PUBLIC LAUNCH: release to production and monitor**
`type/ops` `gate` `P0` `size/L` `status/awaiting-external`
AC: Cat Metro 1.0 publicly installable **from the USA**; rollout to 100% with halt criteria armed;
first organic installs visible; the launch push sent and journeys confirmed active on the production
audience; first real purchases and revenue confirmed in the RevenueCat dashboard (the Grand Prize
shortlist's source); vitals checked hourly; release date recorded for Early-and-Effective-Release
judging.
Deps: CM-032, CM-033. Blocks CM-035. Any day Aug 24–28 is on target; later remains eligible but burns
Grand-Prize revenue runway — spend all remaining schedule slack here.

**CM-035 — Day-1 readout, review replies, and the launch-week hotfix loop**
`type/ops` `type/fix` `area/store` `P0` `size/M`
AC: The full funnel flows in analytics (`first_open → tutorial_completed → level_completed(L5) →
paywall_viewed → purchase_completed`) with raw counts; crash-free ≥99.5%; every review replied to;
entitlement grants and restores behaving on production traffic; any crash cluster hotfixed.
Deps: CM-034. Blocks CM-036. The Day-1 numbers post (with denominators) is also the launch-week
#BuildInPublic beat.

---

## 7. M5 — Growth & Experiments (Aug 29 – Sep 18)

**CM-036 — District Cup event container + two baked rounds**
`type/feat` `type/content` `area/application` `area/content` `P1` `size/L`
AC: Event container reads `startsAt`/`endsAt` date windows from content JSON behind the `weekly_event`
flag, awards a participation livery for finishing all 3 routes at any medal, and caps any run that used
a rewind at Silver (the entire anti-P2W rule, enforced in the Domain layer from the command log); Neon
Nights and Commuter Rescue rounds are **baked into the 1.0 binary** so the first two weeks need no
update; the "Classic Cup" fallback round sits permanently on the shelf.
Deps: CM-015, CM-027. Blocks CM-043. Cup weeks start Mon 17:00 local; medal thresholds are
solver-calibrated static values, not percentiles (we have no backend to compute percentiles honestly).

**CM-037 — All Access price experiment: $6.99 vs $4.99**
`type/ops` `area/integrations-rc` `P1` `size/M` `award/hamm`
AC: RC Experiments if the project plan allows, otherwise the pre-declared fallback — sequential
offering swaps (`ofr_core` at $6.99 Sep 1–7, `ofr_core_b` at $4.99 Sep 8–14 — equal 7-day cells) with
cohort-split readouts by install week; both price points serve correctly and **never simultaneously to
the same client**; winner picked Sep 15 on revenue-per-1k-paywall-views; readout written as ADR-0035
with the method disclosed and labeled **directional, not significant**.
Deps: CM-022, CM-034. Blocks CM-046. E07 in `experiment_backlog.csv` is the governing design.

**CM-038 — ASO iterations + Play store-listing experiment queue**
`type/growth` `area/store` `area/marketing` `P1` `size/M` `award/grand`
AC: E16 (icon A vs B) starts once the listing has ~3 days of stable traffic; E17 (screenshot order) and
E18 (short description) queue behind it, **one at a time, 14 days each**; **freeze guard on every
slot: no listing experiment may run past Sep 25**; ASO iteration 1 (Aug 29–Sep 4)
and iteration 2 (Sep 5–11) change one field at a time from Play Console search-term data; every readout
is adopted only above Play's >90% probability-to-beat, and nulls are reported as nulls.
Deps: CM-031, CM-034. Blocks CM-047. Every slot obeys the Sep 25 freeze guard; Slot 3 (E18)
additionally only starts if its variant copy is one we would be happy for a judge to read.

**CM-039 — Levels 31–40 + in-app review flow + v1.1 release**
`type/content` `type/feat` `area/content` `area/presentation` `P1` `size/XL — split` `award/best-game`
AC: L031–L035 (cooldown mechanic enters, band 31–60) and L036–L040 (gates mechanic) authored and
solver-validated; the Play in-app review flow triggers **only** after a 3-star win with
`session_count >= 5`, never after a failure, never inside a purchase or ad flow, quota-aware and
never branching on a result the API does not return; v1.1 submitted in time to be live by **Sep 11**.
Deps: CM-011, CM-035. Blocks CM-043. Ship 31–35 only if 36–40 slip — the D42 content-complete gate
absorbs the cut.

**CM-040 — Catvertising evidence capture**
`type/docs` `area/marketing` `area/integrations-ads` `P0` `size/M` `award/catvertising`
AC: Screen recordings of all five ad surfaces as a player experiences them; opt-in, decline, and
3-consecutive-decline-mute counts **with denominators**; a documented zero-forced-ad-surface claim (a
3-frame win→results→next-level strip proving nothing interstitial exists); AdTracker event and revenue
charts captured from the RC dashboard.
Deps: CM-025, CM-034. Blocks CM-044. If the Model A contingency fired, close this issue as `Cut` and
drop the Catvertising category — do not submit an entry describing ads that are not live.

**CM-041 — OneSignal journey tuning, outcomes, and copy pruning**
`type/ops` `area/integrations-onesignal` `P1` `size/M` `award/onesignal`
AC: Three outcomes configured (`daily_completed`, `session_after_lapse`, `level_completed_after_help`)
plus `purchase_completed` as an outcome **with value**; A/B copy variants read at Week 4 and losers
pruned; the 10% holdout runs on J2 only; any journey whose recipients unsubscribe at >1% is paused;
delivery/open/outcome numbers recorded with send counts.
Deps: CM-026, CM-034. Blocks CM-044.

**CM-042 — Growth execution: Product Hunt, calendar, creators, share loop**
`type/growth` `area/marketing` `P1` `size/L` `award/bip` `award/grand`
AC: Product Hunt launch **Tue Sep 1** with a value-led maker comment carrying week-1 numbers and a
6-hour reply window; the 30-day content calendar (Aug 24–Sep 22) executed with an X post **every
single day**; micro-creator waves of 10 on Aug 25 and Sep 8 with responses logged; the daily share-card
repost ritual and the Saturday Express callout running weekly.
Deps: CM-033, CM-035. Blocks CM-044 (the BIP corpus is the award evidence). Detail in
`growth_aso_plan.md` §12–§21. No upvote solicitation, no incentivized reviews, no share-to-unlock.

**CM-043 — D35 + D35b + D42 gates**
`type/docs` `gate` `P0` `size/S`
AC: **D35 retention gate** — D1 ≥22% floor (target 30%+), Daily Line participation ≥25% of DAU, top-2
levers chosen, ADR-0035. **D35b funnel go/no-go** — GO only if crash-free ≥99.5% **and** P0 award work
is on track **and** RC Funnels is on our plan **and** Stripe is connected **and** the build estimate is
≤8h via Redemption Links; **default NO-GO**, ADR-0035b. **D42 content-complete gate** — 30 launch levels
+ 10 bonus + 31–40 + Cup cadence + both themes live; feature freeze begins, ADR-0042.
Deps: CM-036, CM-037, CM-039, CM-041. Blocks CM-044. If D1 <15%, stop all growth spend (the $0 default
budget holds), run an FTUE surgery sprint, and cut levels 36–40.

---

## 8. M6 — Submission (Sep 19–25 + Sep 26–30 buffer)

**CM-044 — Evidence pack: 32 exhibits**
`type/docs` `area/marketing` `P0` `size/L` `award/hamm` `award/onesignal` `award/catvertising` `award/bip`
AC: All 32 exhibits from `submission_script.md` §4 captured from **live systems** (store, RC dashboard,
AdTracker, OneSignal canvases and delivery stats, Crashlytics crash-free rate, CI runs, cohort table
with n per cohort, BIP index) named `NN_slug_YYYY-MM-DD.png`, with each exhibit captured **twice** a
week apart so a broken final capture still has a dated predecessor.
Deps: CM-040, CM-041, CM-042, CM-043. Blocks CM-046, CM-048. Exhibits 11 (RC revenue) and 22 (OneSignal
delivery) are re-shot on Sep 30 for final numbers.

**CM-045 — The 2-minute demo video**
`type/docs` `area/art` `area/marketing` `P0` `size/L` `award/best-game` `award/design`
AC: Final cut **≤1:59** at 1920×1080 with burned-in captions, gameplay in the very first frame and the
hook landing inside 15 seconds; the 275-word VO recorded and mixed (speech −16 LUFS, game audio ducked
to −22); **zero third-party trademarks and zero third-party music** — every audio stem is ours;
uploaded to YouTube and playback verified on a second device.
Deps: CM-030 (banked takes), CM-044. Blocks CM-048. Storyboard and script are locked in
`submission_script.md` §5. Fallback: the on-screen-text-only cut carries the full argument without VO.

**CM-046 — Devpost submission LIVE early: story + seven award paragraphs + category questions**
`type/docs` `P0` `size/L` `award/best-game` `award/hamm` `award/catvertising` `award/onesignal` `award/bip` `award/design` `award/grand`
AC: The submission goes **LIVE on Devpost ~Sep 15** (submit early, edit continuously — official
judging guide, Aug 1) and is edited to the freeze. The 300-word Story field, all seven award
paragraphs tuned to the **verified** criteria wording, a drafted answer to **every targeted
category's category-specific question** (an empty question = not judged in that category —
`submission_script.md` §2.9), "Built with" list, category selections, and every remaining field
filled — **no field left blank**; every number passes the claims audit (denominator, date range,
vintage) and the overstatement audit (no "significant", no bare D30, no extrapolated LTV, no
attribution language for correlational spikes).
Deps: CM-037, CM-043, CM-044. Blocks CM-048.

**CM-047 — Judge access: fresh codes and testing instructions**
`type/ops` `area/store` `P0` `size/S`
AC: 5 fresh Play one-time promo codes for `cm_all_access` minted, each with an **expiry past Oct 13**
(the end of judging), one redeem-tested on a clean device and then burned; a ≥10-code reserve held; the
testing-instructions block pasted with the **"look at the paywalls before redeeming"** warning first and
the per-surface fastest-route table intact; the restore path re-verified after a reinstall.
Deps: CM-032, CM-034. Blocks CM-048. Redeeming All Access permanently suppresses system-initiated
paywalls — the ordering warning is the difference between a judge seeing our monetization and
concluding it does not exist.

**CM-048 — D54 SUBMISSION-READY GATE, T-72h rules re-check, and submission**
`type/ops` `gate` `P0` `size/L` `risk/policy`
AC: **D54 (Sep 23)** — description, <2 min video, store URL, 1024² icon, 1179×2556 frameless
screenshot, and judge access all verified against the Official Rules; winners-date discrepancy (rules
Oct 21 vs FAQ Oct 22) re-verified; checklist ADR-0054 signed. **T-72h (Sun Sep 27)** — full rules
re-read and diffed against DECISIONS_BRIEF §VERIFIED EVENT FACTS, plus re-verification of the two
criteria texts the brief did not capture (**#BuildInPublic** and **Design**) with the corresponding
paragraphs retuned. The submission has been **live since ~Sep 15** (CM-046, submit-early flow) —
**final edit pass complete by Tue Sep 29 18:00 PDT**, with Sep 30 reserved for the final metrics
refresh and buffer; hard deadline Sep 30 11:45pm PDT. Package-name/video-match audit passes:
submitted package name exactly matches the live app (RC SDK integration is verified
programmatically against it) and the live build matches what the video shows (an RC advocate
downloads it before winners are finalized).
Deps: CM-044, CM-045, CM-046, CM-047. Blocks CM-049. The 44-row timed checklist (items 0–38) lives in
`submission_script.md` §7 and is mirrored to `/submission/checklist.md`.

**CM-049 — Post-submission stability watch through judging (Oct 1–13)**
`type/ops` `P0` `size/M`
AC: App stays live, installable and USA-available; promo codes stay unredeemed and unexpired, replaced
within 24h if used; OneSignal journeys run unattended with no stale event copy scheduled during
judging; RC offerings stable with no experiment mid-flight showing a judge an odd price; crash-free and
ANR inside halt criteria; reviews replied to daily.
Deps: CM-048. Blocks nothing. **Zero feature work.** The only acceptable change during judging is a
crash fix, and even that goes through staged rollout.

**CM-050 — In-app Promotions integration + clean-device promo-code redemption test (added 2026-08-03, AMD-03)**
`type/feat` `area/integrations-rc` `area/store` `P0` `size/M` `status/needs-device`
AC: The app integrates Play **In-app Promotions** — required for one-time-product promo codes to
redeem (answer/6321495; only Active buy options can be promoted); whether purchases-unity coexists
cleanly with the In-app Promotions redemption flow is UNVERIFIED, so the proof is an **end-to-end
promo-code redemption test on a clean device**, run as an acceptance criterion at **D17** (commerce
build) and re-run at **D24** (pre-submit sweep): code redeemed in the Play Store app → `all_access`
entitlement arrives via CustomerInfo sync with no purchase UI → `purchase_completed(price_local_bucket=promo)`
logged. If redemption fails, the judge-access mechanism named in the rules is broken (R-16) and fixing
it becomes the top P0.
Deps: CM-022, CM-023. Blocks CM-047. Cross-ref: google_play_checklist.csv In-app Promotions row;
rc config D2-51.

---

## 9. Appendix A — Pull request template

Save as `.github/pull_request_template.md`.

```markdown
## What
<!-- One sentence. What does this PR change from the player's or the system's point of view? -->

## Backlog issue
Closes CM-___

## Why
<!-- Link the spec section that requires this: DECISIONS_BRIEF / product_spec §_ / monetization_spec §_ / etc.
     If no spec requires it, say so explicitly and justify the scope. -->

## How
<!-- The approach, and any alternative you rejected and why. -->

## Verification
- [ ] EditMode tests pass locally
- [ ] `dotnet`/CI level validation passes (if content touched)
- [ ] Replay-hash determinism test green (if Domain touched)
- [ ] Verified **on a physical device** (required for: purchases, ads, push, deep links, paywalls, perf)
      Device(s) + Android version: ___
- [ ] No new allocations per frame in `Playing` (if Presentation/Domain touched)

## Fair-by-design checklist (required on any commerce, ad, or messaging PR)
- [ ] No offer of any kind can appear after a **first** failure
- [ ] Free / owned / rewarded options render **above** any purchase option
- [ ] No countdown timer, nothing preselected, close control ≥48dp and active from the first frame
- [ ] Decline copy is neutral (no confirm-shaming)
- [ ] Payer suppression respected (`all_access` / `supporter` see no system-initiated paywall)
- [ ] Prices come from `StoreProduct.PriceString` — no price literal anywhere in code, prefab or template
- [ ] Any new analytics event/tag exists in `analytics_event_taxonomy.csv`

## Risk
<!-- What could this break? What is the rollback? Which feature flag gates it? -->

## Screenshots / capture
<!-- Before/after, or a short clip for anything visual. -->
```

---

## 10. Appendix B — Bug report template

Save as `.github/ISSUE_TEMPLATE/bug.yml` (rendered here as markdown for readability).

```markdown
---
name: Bug
about: Something behaves differently from the spec
labels: type/fix
---

## Summary
<!-- One sentence. -->

## Severity
- [ ] S0 — crash, data loss, wrong money (purchase, entitlement, ledger), or a policy violation
- [ ] S1 — a P0 feature is unusable
- [ ] S2 — degraded but workaroundable
- [ ] S3 — cosmetic

## Environment
- Build / version code:
- Device + Android version + RAM tier (low / mid / high):
- Fresh install or upgrade:
- Network state (online / offline / flaky):
- Entitlements held:

## Reproduction
1.
2.
3.
**Frequency:** always / intermittent (___ of ___ attempts)

## Expected vs actual
Expected:
Actual:

## Determinism artifacts (attach whenever the simulation is involved)
- Level id + seed:
- Command log / replay file:
- Replay hash:

## Evidence
<!-- Logcat excerpt, Crashlytics link, screenshot, screen recording. -->

## Spec reference
<!-- Which spec section does the actual behavior contradict? If none, this may be a feature request. -->
```

---

## 11. Appendix C — Feature request template

Save as `.github/ISSUE_TEMPLATE/feature.yml`.

```markdown
---
name: Feature
about: New capability or a change in behavior
labels: type/feat
---

## Problem
<!-- The player or system problem. NOT the solution. -->

## Proposed change
<!-- One paragraph. -->

## Priority claim
- [ ] P0 — launch-blocking; cutting it moves the date
- [ ] P1 — ships at 1.0 if the schedule holds; degrades cleanly if cut
- [ ] P2 — post-launch / stretch
**Justify the claim in one sentence:**

## Which locked decision does this touch?
<!-- Cite DECISIONS_BRIEF or the owning spec. If it CONTRADICTS a locked decision, stop and open an ADR
     instead — locked decisions change through ADRs, never through feature issues. -->

## Scope check (a "no" on any line means this needs splitting or cutting)
- [ ] Fits inside its milestone without displacing a P0
- [ ] Needs no new mechanic beyond the 4 locked launch mechanics
- [ ] Needs no backend, no remote config, and no new SDK
- [ ] Needs no new art beyond palette/decal swaps
- [ ] Estimate is `size/L` or smaller

## Acceptance criteria
1.
2.

## Measurement
<!-- What number tells us this worked, and what is its denominator? -->

## Cost of NOT doing it
<!-- If this is empty, close the issue. -->
```

---

## 12. Appendix D — ADR template

Save as `docs/adr/ADR-NNNN-short-title.md`. Numbering matches the roadmap's named decisions
(ADR-0007 fun gate, ADR-0014 level-system gate, ADR-0021 commercial beta, ADR-0024 store-ready,
ADR-0035 / ADR-0035b, ADR-0042 freeze, ADR-0054 submission-ready).

```markdown
# ADR-NNNN — <short title>

- **Status:** Proposed | Accepted | Superseded by ADR-NNNN | Rejected
- **Date:** YYYY-MM-DD
- **Milestone / roadmap day:** M_ / D__
- **Deciders:** solo dev
- **Related issues:** CM-___, CM-___
- **Supersedes / relates to:** DECISIONS_BRIEF §___, specs/____.md §___

## Context
<!-- The situation and the forces at play. Include the numbers that exist today, each with its
     denominator, and label every external benchmark with its vintage and verification date. -->

## Decision
<!-- One paragraph, active voice, unambiguous. What we are doing, starting when. -->

## Options considered

| Option | Pros | Cons | Cost | Verdict |
|---|---|---|---|---|
| A | | | | |
| B | | | | |
| C (do nothing) | | | | |

## Evidence
<!-- Verified facts with dates ("verified 2026-07-31"), telemetry with denominators, comp data with
     sources. Anything unverified must be labeled as an assumption. -->

## Consequences
**Positive:**
**Negative (state these honestly — an ADR with no downsides is a sales pitch):**
**Neutral / follow-on work:**

## What would change my mind
<!-- A falsifiable condition and the date we check it. An ADR without this is an opinion. -->

## Risk and fallback
- **Risk:**
- **Fallback:**
- **Kill-switch:** <!-- which feature flag, RC offering, OneSignal pause, or staged-rollout halt -->

## Review date
<!-- When we come back and score this decision against reality. -->

## Public log
<!-- The #BuildInPublic post that published this decision, if any. Gate ADRs are always published,
     including the ones that failed. -->
```

---

## 13. Working agreements

1. **One issue, one PR, one merge.** If a PR closes two issues, one of them was mis-scoped.
2. **`size/XL` is not assignable.** Split it first (CM-016, CM-027 and CM-039 are pre-marked for splitting).
3. **Gates are issues.** They get the `gate` label, their criteria pasted verbatim from
   `roadmap_56_days.csv`, and they close with an ADR link — pass or fail.
4. **Device-verified means device-verified.** Anything labeled `status/needs-device` cannot be closed
   from Editor evidence: purchases, ads, push, deep links, paywalls, and performance.
5. **Locked decisions change through ADRs only.** A feature issue may not quietly contradict
   `DECISIONS_BRIEF.md`; it opens an ADR that supersedes the relevant section, or it does not happen.
6. **Every new event or tag exists in the taxonomy first.** Unknown names assert in dev builds by
   design — that is the guard, not a nuisance.
7. **The daily #BuildInPublic post is a standing commitment, not an issue.** It ships even on the worst
   day of the project (CM-042); a bad day is the content.
