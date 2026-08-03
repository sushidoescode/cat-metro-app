# Live-Ops Spec — Cat Metro

Status: draft v1, 31 Jul 2026. Governed by `deliverables/DECISIONS_BRIEF.md` (locked).
Event window: Jul 31 2026 8:00am PDT – Sep 30 2026 11:45pm PDT (Official Rules, verified 2026-07-31).
Public 1.0 target: Aug 24–28 2026. This spec covers everything that runs after 1.0 ships.

Design principles (from the locked brief):
1. Offline-first, no remote content service at launch. Every live-ops beat must work from data baked into the build, with OneSignal + RevenueCat Offerings as the only remote levers.
2. Fair by design: no forced ads, no energy, no pay-to-win; streaks never gate content (a break costs at most 150 tickets of gift escalation, and a free saver exists).
3. Solo-dev sustainable: every recurring beat has an explicit hours budget; anything over budget gets Cut.
4. Everything ships behind a feature flag (architecture.md flags section) so the commercial beta can carry systems dark.

---

## 1. Daily Line — seed system (P0)

The Daily Line is one shared board per calendar date, unlocked after Level 7 (brief, locked).
Same seed for all players. No backend. Deterministic on-device generation.

### 1.1 Seed derivation (same-seed-for-all)

```
dateKey  = player's LOCAL calendar date, formatted as the ISO string "YYYY-MM-DD"
           (the string itself is timezone-free — every player on the same calendar
           date produces the identical string, which is what makes codes global)
seed(k)  = lower 32 bits of SHA-256("CM-DAILY-1|" + dateKey + "|" + k)   // k = salt, default 0
```

- **Rollover: local midnight.** The daily "day" is the player's local calendar date. This is the player-friendly choice (streak deadlines land at *their* midnight, matching `streak_risk` copy in `notification_copy.csv`).
- **Global comparability: guaranteed by the derivation, not the clock.** Because the seed is a pure function of the ISO date string (the same function everyone runs), a player in Tokyo and a player in São Paulo who both play "2026-08-24" get byte-identical boards even though they play up to ~26 hours apart. Share codes embed the date, so a code is playable and comparable worldwide for as long as that local date exists somewhere on Earth — and replayable after via the challenge deep link (`challenge_opened` in `analytics_event_taxonomy.csv` already supports seed-carrying links).
- Why not UTC-midnight rollover: it dumps the rollover at 5pm–9am local for the Americas/Asia, breaks the "before midnight" streak copy already locked in `notification_copy.csv`, and buys nothing — comparability comes from the date-string function above.
- DST/timezone edges: streak increments are keyed to consecutive `dateKey` values, not elapsed hours; a timezone change that repeats or skips a local date can never *reset* a streak, only at worst delay an increment by one day (`streak_changed` QA row already mandates the timezone/DST test).
- Clock cheating: best-effort only (no server). Guard: if local date moves backwards or jumps >2 days against a monotonic-clock estimate, the day still plays but `rank_bucket` display and share-card generation are suppressed for that day. Never punish — some jumps are real travel.

### 1.2 Generation + validation pipeline

Generator runs on-device from `seed(k)` using launch-mechanics-only parameters (switch, queue, second-source, wildcard — brief: cooldown/gates are post-launch bands). Weekday difficulty curve:

| Weekday | difficultyTarget | Nodes | Switches | Waves | Mechanic pool |
|---|---|---|---|---|---|
| Mon | 0.35 | 6–8 | 2 | 4–5 | switch, queue |
| Tue | 0.45 | 7–9 | 2–3 | 5–6 | + second-source |
| Wed | 0.50 | 8–10 | 3 | 5–6 | + wildcard |
| Thu | 0.55 | 8–10 | 3 | 6–7 | full pool |
| Fri | 0.60 | 9–11 | 3–4 | 6–7 | full pool |
| Sat | 0.75 | 10–12 | 4 | 7–8 | full pool ("Saturday Express") |
| Sun | 0.55 | 8–10 | 3 | 6 | full pool |

All generated boards must satisfy schema v2 constraints (`level_schema.json`): `minActionWindowTicks ≥ 6`, solver-verified solvable, 3-star achievable within band slack.

**Validation is done ahead of time in CI, not trusted at runtime.** Because generation is deterministic, CI can pre-play the future:

- CI job `validate-dailies` (runs on every content/sim PR + nightly): executes generator + solver (the exact `CatMetro.Domain` step function — architecture.md, no parallel implementation) for the next 90 dates.
  `dotnet run --project tools/DailyValidator -- --from 2026-08-24 --days 90 --out Assets/StreamingAssets/daily_overrides.json`
- If `seed(0)` for a date fails validation (unsolvable, degenerate, action window too tight, solver timeout), the tool increments salt k = 1, 2, … until a board passes, and writes `{ "2026-09-13": 2 }` style entries into `daily_overrides.json`, which ships in the build.
- Runtime rule: look up the date in `daily_overrides.json`; if present use that k, else k = 0. **Fallback for dates beyond the baked table** (players who never update): the device runs the same salt-increment validation loop locally (bounded to 250 ms budget; solver at beam width 1k). Same algorithm ⇒ same k ⇒ same board for everyone, even off the table.
- Sim/schema changes invalidate history: any change to the Domain step function bumps the generator version string ("CM-DAILY-1" → "CM-DAILY-2") *only at a version boundary where old clients are force-messaged to update* — otherwise old and new clients would play different boards on the same date. Until the event ends: **do not bump**. Freeze the daily generator constant through Sep 30.
- `daily_started`/`daily_completed` events carry `seed` + `local_date` (taxonomy rows 14–15); the two-device same-seed QA check in the taxonomy is the release gate.

### 1.3 Daily rewards + streak

- First daily completion of the day: 100 tickets (brief economy, locked). `daily_gift_double` rewarded placement can double the separate daily gift (1/day cap, brief).
- Streak = consecutive `dateKey` completions. Cosmetic only: 3/7/14/30-day badge liveries. Never gates content or currency beyond the badge (brief: streak saver = rewarded/free only, IAP Cut).
- Streak save: one rewarded-ad `streak_saver` per day (brief cap). P1: one automatic free "depot pass" repair per rolling 30 days for streaks ≥ 7 — free, no ad, no IAP; ships in the Sep content update, not 1.0.
- Share code grammar (feeds growth plan §12): `CM-<YYMMDD>-<score>` e.g. `CM-260824-3120`, deep link `catmetro://daily?d=2026-08-24&b=3120` (and `https://catmetro.io/d/260824?b=3120` once App Links are set up). Invalid/expired codes fall back to Home (taxonomy row `challenge_opened`).

**Decision:** Local-midnight rollover; seed derived purely from the ISO date string with CI-baked salt overrides; generator constant frozen through Sep 30.
**Evidence:** Determinism stack already locked (brief: seeded PCG32, solver-validated JSON, command log; verified 2026-07-31); taxonomy rows daily_started/daily_completed/streak_changed already specify seed+local_date+DST QA; notification copy already promises "before midnight" local framing.
**Action:** Build `tools/DailyValidator` in Week 2 (Aug 8–14) alongside the campaign solver; wire `daily_overrides.json` into the build; add the 90-date CI job before the Aug 21 commercial beta.
**Risk:** A Domain-sim bug fixed mid-event changes board outcomes for updated clients → split boards on one date.
**Fallback:** Ship the sim fix but pin the daily generator to the old code path via a compile-time shim until Oct 1; if a specific date's board is broken for everyone, push a OneSignal message + IAM declaring a "free ride day" (daily auto-credits participation) — flag `daily_enabled` can dark the mode entirely in a hotfix build.

---

## 2. Weekly District Cup (P0 framework, first live Week 5)

Async weekly event. No real-time multiplayer, no server leaderboard (flag `leaderboard` OFF at launch — architecture.md). District Cup is the *recurring container*; themed weeks (Neon Nights, Commuter Rescue) are District Cup rounds with a skin — one system, many weeks, solo-dev cheap.

### 2.1 Format

- Runs Mon 17:00 local → Sun 23:59 local (matches `event_start` trigger in `onesignal_journeys.csv`).
- Content: 3 special routes per round (generator+validator drafts, hand-tuned; see §5 cadence). Eligibility: highest_level ≥ 8 (journey CSV eligibility, keeps it post-tutorial).
- Scoring: personal best per route; medal tiers Bronze/Silver/Gold from **static solver-calibrated thresholds baked per round** (Gold = solver-95% score, Silver = 75%, Bronze = clear). No percentile ranks at launch — we have no backend to compute them honestly; static tiers are honest and offline-safe.
- Reward: **participation cosmetic** — finish all 3 routes at any medal = that round's livery (e.g. Neon Nights train livery). Gold on all 3 = same livery + gold trim variant. Nothing else. No currency multipliers, no gameplay power.
- **No pay-to-win, mechanically:** rewinds (free, rewarded, or purchased `cm_rewind_5`/`cm_rewind_20`) are usable in Cup routes, but a run that used any rewind caps at Silver. Purchases can therefore never buy Gold. Themes/All Access are cosmetic by definition. This one rule is the entire anti-P2W policy and it is enforceable in the Domain layer (rewind usage is in the command log).
- Messaging: `event_start` + `event_ending` from the journey CSV run as **one-off scheduled sends**, not Journeys (see §6). `event_joined`/`event_completed` analytics rows already exist.

### 2.2 Round lifecycle (solo-dev runbook)

Tue–Wed (week prior): generate 12 candidate routes, solver-validate, pick 3, hand-tune (~4h).
Thu: livery variant (palette/decal swap on base train, ~2h), copy for 2 sends + 1 IAM (~1h).
Fri: batch-validate, merge, include in the scheduled weekly build (builds already 2×/week from Week 3 — architecture.md CI).
Mon 17:00 local: round goes live via **date-window check baked in content** (`startsAt`/`endsAt` in the event JSON) — no remote flip needed; `weekly_event` flag is the kill-switch.
Because rounds ship in builds, two rounds are always baked ahead (N+1 finished, N+2 draft) so one sick week never breaks the cadence.

**Decision:** District Cup = async 3-route weekly with static medal tiers, participation livery, rewind-capped-at-Silver anti-P2W rule; themed weeks are Cup rounds.
**Evidence:** Brief locks "weekly mini-event from Week 5 (District Cup, async score, participation cosmetic)"; comps show event-skin cadence works at low cost (Cats & Soup 42.7M installs on a cosmetics spine — verified 2026-07-31); no-backend constraint is locked in architecture.md.
**Action:** Build the event container (JSON date-window + medal thresholds + livery grant) in Week 3 (Aug 15–21) behind `weekly_event`; bake Neon Nights + Commuter Rescue content into the 1.0 build itself so the first two rounds need zero updates.
**Risk:** Static Gold thresholds mis-calibrated (too easy = worthless, too hard = discouraging).
**Fallback:** Thresholds live in content JSON — a Thursday build can retune the *next* round; for a live round, an IAM apologizes and the participation livery (the real reward) is unaffected by thresholds.

---

## 3. Five-week live-ops calendar (Aug 24 – Sep 30)

Aug 24 is a Monday; all weeks run Mon–Sun. Submission freeze Sep 26–30 (brief schedule spine).

| Week | Dates | Theme | Content beats | Monetization beat | Messaging (see §6) | Build-in-public beat |
|---|---|---|---|---|---|---|
| W1 Launch Week | Mon Aug 24 – Sun Aug 30 | "Opening Day" | 1.0 live (30 levels, Daily Line from day 1); no event — let the core breathe; hotfix window Thu Aug 27 | Default offering `launch_v1` live at all 5 placements (post_level_5, theme_preview, bonus_district, shop, rewind_failure); no discounts | Journeys 1–3 activate as cohorts qualify; launch announcement is store+social only (no push list yet) | Launch day numbers thread; D1 retention post Fri Aug 28 |
| W2 Neon Nights | Mon Aug 31 – Sun Sep 6 | Night-city Cup round #1 | 3 neon-lit routes (baked in 1.0); Neon Nights livery; the Week-5 build ships levels 31–35 (cooldown mechanic enters, per brief bands); cross-sell moment for `cm_theme_neon` $2.99 via theme_preview placement (preview only — event routes render in neon for everyone) | Sequential offering test A begins Sep 1: `post_level_5` shows All Access at $6.99 (baseline week; $4.99 comparison is W3 — Experiments is Pro-gated per brief, sequential is the fallback) | Scheduled send Mon 17:00 `event_start`; Sun 11:00 `event_ending` (progress ≥30% segment) | "First event week, real numbers" post; Devpost draft started |
| W3 Commuter Rescue | Mon Sep 7 – Sun Sep 13 | Rescue-cats Cup round #2 | 3 routes with heavy wildcard-commuter use; Rescue livery; v1.1 content patch Wed Sep 9 (live by Sep 11): levels 36–40 + depot-pass streak repair (P1) — levels 31–35 already shipped in the Week-5 build (one content schedule, roadmap version) | Sequential offering test B: `post_level_5` at $4.99 (Sep 8–14, the second equal 7-day cell); pick winner by paywall CVR × net revenue Sep 15 | event sends ×2; `new_content` one-off send Thu Sep 10 for the level patch | Pricing A/B results post (HAMM evidence); mid-event metrics |
| W4 District Cup Championship | Mon Sep 14 – Sun Sep 20 | Flagship round: best-of remix routes | 3 remix routes of W2/W3 favorites (by completion data); Championship gold livery; share-code push: "beat my Cup score" cards | Winning price locked; `shop` placement gets Supporter Pack spotlight card (shop-only, never interrupts — brief) | event sends ×2; IAM for share-card feature | "How the Cup works with no backend" technical post |
| W5 Founders Wrap | Mon Sep 21 – Wed Sep 30 | 10-day "Founding Riders" wrap | Founders participation badge for anyone who plays any 3 days during the wrap; no new routes (freeze); Devpost video capture week; **Sep 26–30 submission freeze** (brief) | No changes after Sep 24 — stable revenue reporting through window close Sep 30 11:45pm PDT | One `new_content`-slot scheduled send Mon Sep 21 ("Founding Riders week"); all experiments stopped | Devpost submission + wrap-up thread; full revenue transparency post |

Cut from this window (explicit): mid-week flash sales (violates fair-play positioning), limited-time IAP (same), any third content update (capacity), any event requiring a server.

**Decision:** Five Mon-anchored weeks, first two event rounds baked into the 1.0 binary, two content updates (Week-5 build: levels 31–35; v1.1 Sep 9, live by Sep 11: levels 36–40), freeze from Sep 24.
**Evidence:** Brief schedule spine (D28 public Aug 24–28; Sep 26–30 freeze); Play production review ≤7d typical (verified 2026-07-31) makes mid-Sep the last safe update slot; 2025 Grand winner calibration (17k users/$30,017 — verified) says steady weeks beat stunt weeks.
**Action:** Content for W2+W3 rounds validated and merged before the Aug 21 commercial-beta gate; W4 remix selection is a 2h data query on Sep 8.
**Risk:** The Sep 9 patch hits a slow Play review and lands mid-event.
**Fallback:** Patch content is additive-only; if review slips past Sep 12, W3's `new_content` send is re-scheduled to align with actual availability (sends are one-off, not journey-locked), and W4 proceeds on baked content regardless.

---

## 4. Comeback mechanics (P0 ladder, P1 extras)

All comeback logic is client-side state + the OneSignal lapse ladder (§6 Journey 2).

| Trigger (local, offline-safe) | Grant | Label |
|---|---|---|
| Return after 48h–7d away | 1 free rewind pre-loaded + "the cats held your spot" board state card (matches `inactivity_48h` copy already locked in notification_copy.csv) | P0 |
| Return after ≥7d | 150 tickets + 2 free rewinds + one free 3-level theme rental token (same rental unit as the rewarded `theme_rental` placement, no ad required) | P0 |
| Return after ≥14d (post `winback_14d`) | Above + "retuned routes" screen: shows their last failed level's fail-rate context (normalizing, mirrors `hard_level_help` variant C tone) | P1 |
| Streak lapsed while away | Streak badge shows "paused" not "lost" for 72h; rewarded `streak_saver` (1/day) or depot pass (P1, §1.3) can restore within that window; after 72h it resets honestly | P0/P1 |
| Missed a full Cup round | Nothing to restore — rounds are participation-based, and the next round is ≤6 days away; IAM says exactly that | P0 (by omission) |

Never: comeback discounts, "we miss you" IAP offers, fake urgency. The `winback_14d` journey row is already marked final-message-then-silence — honor it.

**Decision:** Grants-only comeback ladder (rewinds/tickets/rental), no monetization pressure, streak "paused" grace of 72h.
**Evidence:** Journey CSV rows 4–6 (locked copy is help-toned, zero-pressure); brief positioning ("no forced ads… every level solvable free"); retention medians D7 ~4% current GameAnalytics 2025 data (verified 2026-07-31) mean the lapse ladder is where most users are — burning them with upsells is the comp-store failure mode (Bus Traffic Fever 3.72★, verified).
**Action:** Implement grant table as pure Application-layer rules keyed off `last_seen_at` in Week 3; QA with device-clock jumps per taxonomy QA column.
**Risk:** Grace-period streak logic reads as manipulable ("just wait 71h").
**Fallback:** If abuse shows in `streak_changed(saved)` data, tighten to 24h in the Sep 9 patch — the constant lives in content JSON, not code.

---

## 5. Content cadence a solo dev sustains

Weekly recurring budget (hard cap 10 h/week on live-ops content; the rest is engineering/growth):

| Stream | Method | Cadence | Hours/wk |
|---|---|---|---|
| Daily Line | Fully generated + CI-validated (§1.2); human spot-plays tomorrow's board each evening | 7/wk automated | ~1.5 (15 min/day spot-check) |
| District Cup round | generator+validator drafts 12 → pick 3 → hand-tune | 3 routes/wk | ~4 |
| Event livery | Palette/decal swap on base train material | 1/wk | ~2 |
| Send + IAM copy | 2 sends + 1 IAM per round, written from templates in notification_copy.csv | weekly | ~1 |
| Campaign batch | Hand-tuned batches of 5 (levels 31–35 cooldown, Week-5 build; levels 36–40, v1.1 Sep 9 patch) | twice | ~10 one-time per batch |
| Cosmetic ticket-sink variants | 600–1200-ticket earnable variants (brief economy) | 2 shipped in 1.0, +2 in Sep 9 patch | ~3 one-time |

Rules that make this survivable:
- The validator is the content team. Nothing hand-built from scratch except tuning passes; `authoredBy: "generator+validator"` and `"llm+validator"` are first-class in schema v2 (already in level_schema.json).
- Two-rounds-ahead buffer at all times (§2.2). If the buffer is ever empty, the next round auto-falls-back to "Classic Cup" — 3 re-validated remixes of campaign levels 8/14/22 kept permanently on the shelf.
- Cut permanently for the event window: new mechanics beyond the in-window bands 31–40, new districts, narrative content, localization (EN-only, brief), any content requiring new art beyond palette swaps.

**Decision:** ≤10 h/week live-ops content, generator-first with hand-tuning, one campaign patch, permanent fallback round on the shelf.
**Evidence:** Brief locks the subscription rejection for exactly this reason ("no recurring content cadence a solo dev can honestly sustain in 8 weeks"); schema v2 was designed for validated generation (verified in level_schema.json meta.authoredBy).
**Action:** Shelf the "Classic Cup" fallback round before launch week; timebox each Cup tuning session to one Tue evening + one Wed evening.
**Risk:** Hand-tuning quality drops under launch-week pressure.
**Fallback:** Ship the generator's best un-tuned candidates — validator guarantees solvable/fair; players get a slightly flatter week, not a broken one.

---

## 6. OneSignal under the Growth-plan constraint (P0)

Plan: **Growth, $19/mo** (locked in brief). Hard limits (verified 2026-07-31): **3 active journeys, 6 message steps total** (the 2+3+1 design below uses all 6 — zero spare); frequency capping is Enterprise-only → caps enforced by journey design + in-app-written tags; no quiet hours → Time Window steps; FCM v1 service-account JSON required; RC↔OneSignal linked via `$onesignalUserId` and RC purchase tags (brief).

### 6.1 The 3 active Journeys (mapped from `onesignal_journeys.csv`)

| Slot | Journey | CSV rows absorbed | Design (message steps used / 6) |
|---|---|---|---|
| J1 `daily_streak` | Daily Line + streak risk combined | `daily_challenge` (P0) + `streak_risk` (P0) | Entry: custom event `daily_unlocked` tag true. Branch A (no streak risk): Wait Until learned play window (default 10:00 local) → Time Window 09:00–21:00 → **Msg 1** daily_challenge A/B/C. Branch B (tag `streak_days>=3` AND daily not done, evening): Wait Until 6h before local midnight → Time Window 09:00–21:00 → **Msg 2** streak_risk A/B/C. Exit: `daily_completed` custom event or app_open. Steps used: 2/6. |
| J2 `lapse_ladder` | 48h → 7d → 14d in one journey | `inactivity_48h` (P1) + `winback_7d` (P1) + `winback_14d` (P1) | Entry: no `app_open` for 48h (last_active). **Msg 1** inactivity_48h (afternoon Time Window) → Wait Until 7d-inactive → exit-check → **Msg 2** winback_7d (evening) → Wait Until 14d-inactive AND Msg 2 unopened → **Msg 3** winback_14d (final, journey re-entry OFF permanently after this send). Exit at any step: app_open. Steps used: 3/6 (no spare — the 2+3+1 design uses the full 6-step budget). |
| J3 `hard_level_help` | Stuck-player rescue | `hard_level_help` (P1) | Entry: custom event `level_failed`, forwarded to OneSignal only on the second failure of the same `level_id` within 60 min with no completion in between — the ×2 filter lives client-side in the adapter, per onesignal_retention.md §5; no separate derived stuck-level event exists in the taxonomy (OneSignal custom events are on all paid plans, verified). Wait 45 min → exit if `level_completed` → Time Window 09:00–21:00 → **Msg 1** hard_level_help A/B/C with free rewind flag. One entry per level ever (tag `helped_levels` list written by app). Steps used: 1/6. |

### 6.2 Everything else — explicitly NOT Journeys

| CSV row | Served by | Notes |
|---|---|---|
| `event_start`, `event_ending` | **One-off scheduled sends** (2 per Cup week), segment: push-granted + `highest_level>=8` + active-in-14d; event_ending adds `event_joined` AND NOT `event_completed` | Copy from notification_copy.csv rows 17–21; scheduled Friday for the following week during the §2.2 runbook |
| `payer_thanks` | **IAM** on next session start after `entitlement_changed(granted)` + the RC purchase tag | Never a push; copy rows 25–26 |
| `purchase_issue` | **Local notification** scheduled **+2h** after `purchase_failed` where `user_cancelled = false`, canceled if `purchase_completed` arrives first; falls back to an **IAM** at next session if the notification permission is denied (onesignal_retention.md §6, matches journeys CSV) | Copy rows 27–28; states no charge occurred |
| `feedback_request` | **IAM first**; if unseen after 3 sessions, a **one-off dashboard send** to the segment `feedback_pending = true` (onesignal_retention.md §6) | No journey slot — the fallback is a scheduled send, not a journey |
| `new_content` | **One-off scheduled send** per release (Sep 10, Sep 21) | Copy rows 31–32 |
| `review_coordination` | **In-app review API only** — never any notification (CSV row 14 already says so; brief: quota-limited, never after failure) | Unchanged |
| Streak-expiry backup | **Unity Mobile Notifications (local)**: on each daily completion with `streak_days>=3`, schedule a local notification for tomorrow 20:00 local; cancel on app open or daily completion. Fires even if push permission was granted but OneSignal delivery fails; suppressed entirely if J1 Msg 2 already delivered today (app writes `streak_push_sent_today` tag mirror locally). | Brief names this backup explicitly |

### 6.3 In-app frequency-cap enforcement (Enterprise capping unavailable)

Client-maintained tags checked in every journey/send audience: `last_push_open_at`, `pushes_this_week` (app increments on `notification_opened` and on FCM delivered-receipt where available; conservative default: count sends). Global policy: Push ceiling (global, honest): max 2 pushes/day - a daily nudge, plus a streak warning if one's at risk - never at night (nothing delivers 21:00-09:00 local). Scheduled event sends count against the same ceiling — which is why Cup weeks get exactly 2 one-off sends — and local notifications respect the same counter. (Same statement, verbatim, in onesignal_retention.md §2 and onesignal_journeys.csv.)

### 6.4 OneSignal award angle (P0 target, $25k/15k/5k)

Criteria (verified 2026-07-31): Implementation, User value, Resourcefulness — "a single deployed message is sufficient for eligibility," so the bar to clear is trivial and the ceiling is the story: *three journeys, six copy variants, zero guilt mechanics, local-notification failover, and hard self-imposed caps on a plan without capping.* Screenshot every journey canvas + copy table for the Devpost writeup.

**Decision:** J1 daily+streak, J2 lapse ladder, J3 hard-level help as the 3 active Journeys; events/payer/purchase/new-content via scheduled sends + IAM; streak backup via local notifications; self-enforced 2/day push ceiling (§6.3).
**Evidence:** Plan gates verified 2026-07-31 (Free = 1 journey/2 steps; Growth = 3/6; capping Enterprise-only; custom events on all paid plans); brief locks this exact 3-journey replacement of the 7-journey design; all copy already exists in notification_copy.csv.
**Action:** Aug 1: create OneSignal app, upload FCM v1 service-account JSON, wire `Login(external_id)` + RC `$onesignalUserId` attribute. Aug 15–21: build J1–J3 in dashboard against the closed-test cohort; verify `tutorial_completed` custom event arrives <60s (taxonomy QA row). Journeys flip to the production audience at 1.0.
**Risk:** J1's two-branch design exceeds what one Journey canvas can express cleanly, or Wait Until on learned play window misbehaves.
**Fallback:** Degrade J1 to fixed 10:00 local (CSV already lists send-time experiment 10:00 vs learned as the A/B — run fixed first); if branching truly can't fit, streak_risk falls back to the local-notification path (§6.2 last row) which is already built as its backup.

---

## 7. Kill-switches and feature flags

No remote-config service at launch (locked, architecture.md). Levers, by latency:

| # | Lever | Scope | Latency | How |
|---|---|---|---|---|
| 1 | RevenueCat Offerings | Paywalls, prices, which products appear at which placement | Minutes, remote | Swap/emptying the current Offering per placement (post_level_5, theme_preview, bonus_district, shop, rewind_failure); empty offering ⇒ placements render nothing and monetization is OFF without a build |
| 2 | OneSignal dashboard | Pause any journey, cancel scheduled sends, emergency broadcast IAM to all sessions | Minutes, remote | Journeys have per-journey pause; IAM is the only remote "announce a problem" channel we have — pre-write the outage IAM template now |
| 3 | AdMob console | Stop rewarded fill (pause ad units) | ~Hours, remote | App already handles no-fill gracefully (taxonomy `rewarded_ad_failed` QA row); pausing units = rewarded surfaces quietly hide |
| 4 | Baked runtime flags | `daily_enabled`, `weekly_event`, `share_card`, `ads_enabled`, `paywall_placements`, `leaderboard` (OFF) | Next build (2–7d incl. review) | architecture.md flag set; save-stored so a hotfix can flip and persist |
| 5 | Content date windows | Cup rounds start/stop themselves | Zero (pre-baked) | `startsAt`/`endsAt` in event JSON — misbehaving future round can be neutered in the next scheduled build before it ever starts |
| 6 | `daily_overrides.json` | Replace a bad daily seed for updated clients | Next build | §1.2; non-updated clients get the local salt-loop fallback |
| 7 | Play staged rollout | Halt a bad binary | Minutes, remote | Ship 1.0 at 20% → 50% → 100% over 72h; halting rollout is the biggest red button we own |
| 8 | Hosted `ops.json` kill-file (catmetro.io, 24h cache, fail-open) | True remote flags | — | **P1, Sep 9 patch at the earliest — Cut from 1.0** (brief: no remote config at launch); fail-open design so a dead host changes nothing |

Pre-written emergency assets (write during Week 3, store in repo `/ops/emergency/`): outage IAM ("The Daily Line is delayed — today counts as complete for your streak"), refund-wave response macro, rollout-halt checklist, RC offering named `offering_dark` (empty) ready to activate.

Daily ops check (launch week: 2×/day, 15 min; after: 1×/day):
1. Play Console: crash rate (target ≥99.5% crash-free sessions), ANR, ratings, review replies.
2. RevenueCat: revenue, refunds, entitlement errors — RC is revenue source of truth for the Grand Prize shortlist (verified 2026-07-31: shortlist = total revenue reported in RevenueCat during the window).
3. OneSignal: delivery/open rates, journey health, unsubscribe spike check.
4. AdMob: fill rate, eCPM sanity (US rewarded $15–30 band, Tenjin Q2'24 / Appodeal Q4'24 — label vintage, per brief).
5. Analytics: D1 cohort, daily participation, funnel `paywall_viewed→purchase_completed`.

**Decision:** Six baked flags + three remote dashboards + staged rollout = the complete kill-switch surface for 1.0; hosted kill-file is P1.
**Evidence:** Architecture.md locks "no remote config at launch except RevenueCat Offerings"; RC Offerings remote-by-design (verified 2026-07-31); Play staged rollout and ≤7d review timelines verified 2026-07-31.
**Action:** Create `offering_dark` and the emergency IAM template in Week 3; add the ops checklist as a repo markdown checked off daily in the build-in-public log (doubles as #BuildInPublic evidence).
**Risk:** A client-crash bug in a dark-flagged system still crashes (flags gate behavior, not code loading).
**Fallback:** Integration adapters live behind interfaces in isolated asmdefs (architecture.md `CatMetro.Integrations.*`) — a crashing SDK adapter can be no-op'd in a same-day hotfix without touching game code; staged rollout halt contains blast radius meanwhile.
