# OneSignal Retention System — Cat Metro

Status: v1 (31 Jul 2026). Conforms to DECISIONS_BRIEF.md (locked 31 Jul 2026).
Companion data: `deliverables/data/onesignal_journeys.csv` (full 13-row retention design),
`deliverables/data/notification_copy.csv` (all copy variants), `deliverables/data/analytics_event_taxonomy.csv`
(tag/event destinations). This spec maps that design onto the **verified 3-journey OneSignal
Growth-plan budget** and defines exactly what runs as a Journey, what runs another way, and
how we prove it for the OneSignal award (Implementation / User value / Resourcefulness).

---

## 1. Verified capability summary (all facts verified 2026-07-31 unless noted)

| Capability | Status on our stack | Consequence |
|---|---|---|
| OneSignal Unity SDK | **5.3.2** pinned (still ships Google EDM4U) | Single EDM4U 1.2.188 instance; custom Gradle templates + Force Resolve required |
| Push, IAM, tags, deep links, channels, outcomes, `Login(external_id)` | ✓ in 5.3.2 | Full stack available in Unity |
| Custom events | ✓ SDK 5.2.0+, **all PAID plans** | Growth plan covers it; Free plan would not |
| Journeys | **Free = 1 active journey / 2 message steps; Growth $19/mo = 3 journeys / 6 message steps** | Hard budget: 3 journeys, 6 total message steps. Design below allocates 2+3+1 |
| Frequency capping | **Enterprise-only** | We enforce caps in-app (adapter-side send gating) + by journey design (waits, re-entry cooldowns, exit rules) |
| Quiet hours | **Not available** on our plan | Use **Time Window** steps in every journey so nothing delivers 21:00–09:00 local |
| Android push credentials | **FCM v1 service-account JSON** required | Firebase project Day 1; upload service-account JSON to OneSignal before any test push |
| RC integration | RC subscriber attribute **$onesignalUserId**; RC writes purchase tags to OneSignal | Payer suppression segments and purchase-triggered messaging without our own server |

Plan decision (locked in brief): **OneSignal Growth, $19/mo**, activated Day 1 (Aug 1) so
custom events and all 3 journeys are testable during the closed test.

- **Decision:** Growth plan; 3 journeys / 6 message steps is the total messaging automation budget; everything else ships via IAM, tags, scheduled sends, and local notifications.
- **Evidence:** Plan gating, custom-event plan requirement, Enterprise-only frequency capping, no quiet hours, FCM v1 requirement all verified 2026-07-31 (DECISIONS_BRIEF "VERIFIED PLATFORM FACTS").
- **Action:** Aug 1: create OneSignal app, upload FCM v1 JSON, subscribe Growth, send first test push to a device before Aug 3.
- **Risk:** Plan limits change or custom-event entry behaves differently than documented at journey-build time.
- **Fallback:** Journey 1 degrades to tag-segment entry + scheduled daily send; Journey 3 degrades to client-side triggered local notification; retention design survives on Free plan at reduced fidelity.

---

## 2. Budget allocation: 13 designed touchpoints → 3 journeys + other channels

`onesignal_journeys.csv` designs 13 retention touchpoints. The Growth budget forces triage.
Allocation (this REPLACES the prior 7-journey design, per brief):

| Touchpoint (CSV row) | Ships as | Message steps used |
|---|---|---|
| daily_challenge + streak_risk | **Journey 1** (ACTIVE) | 2 |
| inactivity_48h + winback_7d + winback_14d | **Journey 2** (ACTIVE) | 3 |
| hard_level_help | **Journey 3** (ACTIVE) | 1 |
| payer_thanks | IAM at next session start | 0 |
| purchase_issue | Local notification (Unity Mobile Notifications), +2h | 0 |
| feedback_request | IAM first; one-off dashboard send to beta segment as fallback | 0 |
| event_start / event_ending | One-off scheduled dashboard sends per event week | 0 |
| new_content | One-off scheduled dashboard send per release | 0 |
| review_coordination | In-app native review API only (never push) — unchanged | 0 |
| streak-expiry backup | Local notification (Unity Mobile Notifications) | 0 |

Total journey message steps: **6/6**. Priority labels: Journey 1 **P0**, Journey 2 **P1**,
Journey 3 **P1**, IAM/scheduled/local substitutions **P1**, review_coordination **P0** (cheap, high leverage), everything else in the CSV not listed here **Cut for the event window**.

Push ceiling (global, honest): max 2 pushes/day - a daily nudge, plus a streak warning if one's at risk - never at night (nothing delivers 21:00-09:00 local). (Same statement, verbatim, in liveops_spec.md §6.3 and onesignal_journeys.csv.)

- **Decision:** 2+3+1 message-step split; the three journeys are the three highest-frequency, highest-leverage automations (daily habit, lapse recovery, difficulty rescue).
- **Evidence:** Journey rows and KPIs in onesignal_journeys.csv; Growth limits verified 2026-07-31.
- **Action:** Build journeys in dashboard in priority order (J1 by Aug 8, J2 by Aug 15, J3 by Aug 20) so each soaks in closed test before public launch Aug 24–28.
- **Risk:** Six message steps proves too tight if weekly events (Week 5) deserve automation.
- **Fallback:** Weekly-event sends stay manual scheduled sends permanently — they are calendar-known, so automation adds little; never trade a journey slot for them.

---

## 3. Journey 1 — "Daily Line + Streak" (P0, 2 message steps)

Purpose: convert the Daily Line (unlocks at L7) into a daily habit; protect streaks ≥3
without loss-aversion pressure.

**Entry**
- Trigger: custom event **`daily_unlocked`** (fires once at L7). Re-entry: enabled, cooldown 20h, re-entry trigger custom event **`daily_completed`** — every completed daily re-arms tomorrow's cycle.
- Taxonomy: row `daily_unlocked` already lists destinations `analytics, onesignal_tag, onesignal_event` — the adapter emits the matching OneSignal custom event; no CSV change needed (stale delta removed 2026-08-03, AMD-05).
- Eligibility filters on entry: `push_enabled = true` tag, `payer_status` any (payers get dailies too), `daily_opt_in = true` (settings toggle, default on).
- Design property: a player who lapses stops firing `daily_completed`, so Journey 1 self-silences within one cycle and hands off to Journey 2 — no cross-journey collision logic needed.

**Steps**
1. **Wait Until** next 10:00 user local time (learned send-time experiment deferred to Week 6; start fixed 10:00).
2. **Time Window** 09:00–21:00 local (our quiet-hours substitute; applies to every message step in every journey).
3. **Branch A:** tag `daily_last_done` == today (client writes `yyyymmdd`) → **exit silently** (already played, never remind).
4. **Message step 1:** `daily_challenge` variants A/B/C from notification_copy.csv (34/33/33 split), deep link `catmetro://daily`. Segment filter on the step: OneSignal native `last_session` > 2 hours ago (suppress if they're mid-session or just played).
5. **Wait Until** 18:00 local same day, Time Window 18:00–21:00.
6. **Branch B:** tag `streak_days` >= 3 AND `daily_last_done` != today → **Message step 2:** `streak_risk` A/B/C, deep link `catmetro://daily`. Else exit.
7. **Exit:** custom event `daily_completed`, or end of day, or push permission revoked.

**Caps (by design, since no platform frequency capping):** max 2 pushes/day from this journey, and step 2 requires streak ≥3 so new players see at most 1/day. Client-side global gate: the adapter refuses to fire re-entry `daily_completed` events to OneSignal more than once per 20h (idempotent tag write instead).

**KPIs:** daily participation rate (daily_started / DAU with daily unlocked), push open rate per variant, streak-save rate (dailies completed 18:00–24:00 after Message 2 / Message 2 sends). Copy variant with best open→complete rate wins by Week 4; losers pruned.

**Harm risk:** MEDIUM on the streak message (loss aversion). Mitigations already in copy: a streak break never gates content and costs at most 150 tickets of gift escalation (the saver is free/rewarded); variant A literally says "Campaign progress is safe either way"; hard 21:00 stop; 1 streak message per day max; streak saver is free/rewarded only (never sold — brief, MONETIZATION item 7).

- **Decision:** Daily journey is event-armed by completion, not clock-armed by segment, so it automatically stops nagging lapsed players.
- **Evidence:** journeys.csv rows daily_challenge + streak_risk; copy rows 2–7; Time Window availability verified 2026-07-31.
- **Action:** (`daily_unlocked` already carries `onesignal_event` in analytics_event_taxonomy.csv — no CSV edit needed.) Build + device-test J1 by Aug 8 (test push → deep link → daily board on cold/warm/killed states).
- **Risk:** Dashboard permits only one entry event (no OR of daily_unlocked/daily_completed).
- **Fallback:** Entry = `daily_completed` alone; bridge the L7→first-daily gap with a one-shot local notification scheduled at daily-unlock + 20h ("Today's Line is ready").

---

## 4. Journey 2 — "Lapse Ladder" (P1, 3 message steps)

Purpose: one journey covering 48h → 7d → 14d lapse rungs with waits + branches, ending in
an **honest final message** that promises silence and keeps the promise.

**Entry**
- Trigger: segment-based — OneSignal native `last_session` > 48 hours AND tag `tutorial_done = true` AND first_session older than 2 days AND tag `lapse_final_sent != true`.
- Payer courtesy: entry also filters `last_purchase` RC-written tag not within 24h (never hit someone who just paid; journeys.csv suppression rule).

**Steps**
1. **Message step 1 (48h):** `inactivity_48h` A/B/C. Deep links per variant: A/C → `catmetro://level/{last_fail_level}` (tag substitution), B → `catmetro://home`. Time Window 15:00–20:00 local. Variant C (`hint_color` tip) only enabled once the `hint_color` tag pipeline is live (client writes it from the last failed level's metadata).
2. **Wait** 5 days. **Exit rule (journey-wide):** any new session exits the journey immediately (user no longer matches; also explicit exit on session). Anyone still here is 7 days lapsed.
3. **Message step 2 (7d):** `winback_7d` A/B/C → `catmetro://home`. Time Window 18:00–21:00. Content-led, zero-pressure copy ("No timer — come back when you have a minute").
4. **Wait** 7 days. **Branch:** Message 2 clicked → exit (they saw it and chose no; respect that). Still lapsed and unclicked →
5. **Message step 3 (14d, FINAL):** `winback_14d` A/B/C → `catmetro://home`. Copy is explicitly final: "If Cat Metro isn't for you, no more reminders after this." Immediately after this step, a journey **tag step** sets `lapse_final_sent = true`.
6. **Exit:** journey complete.

**Keeping the promise:** `lapse_final_sent = true` permanently blocks re-entry past the
14d rung. If the player later returns and completes a level, the client re-opens ONLY the
48h and 7d rungs (clears an auxiliary `lapse_suppress_48h` flag, not `lapse_final_sent`) —
the final "we'll stop" message is sent at most **once per user, ever**. A returning player
who lapses again gets at most 2 messages next time, never the final rung again.

**Caps:** ≤3 pushes per lapse episode across 14 days; nothing else targets lapsed users
(Journey 1 self-silenced, Journey 3 requires an active session to trigger). Worst-case
overlap: one Journey-1 daily reminder inside the first ~24h of a lapse (its last armed
cycle) — accepted and documented.

**KPIs (with honest denominators):** return rate per rung = sessions within 72h of send /
sends at that rung; unsubscribe/uninstall within 24h of each rung; 10% **holdout on this
journey only** (see §10 — lapsed users are the one place a holdout is cheap).

**Harm risk:** LOW. The ladder's whole personality is "we noticed, here's what's new,
we'll stop asking." Never guilt copy, never discount-panic framing (discount framing on
winback_14d variant C is a free theme *trial*, not a price cut — consistent with the
fair-by-design positioning).

- **Decision:** One journey, three rungs, session-exits everywhere, final message sent once per user ever.
- **Evidence:** journeys.csv rows inactivity_48h / winback_7d / winback_14d; copy rows 8–16; segment `last_session` filtering is core OneSignal functionality (verified 2026-07-31).
- **Action:** Build J2 by Aug 15; QA by resetting `last_session` with a test device left idle (or a test segment override); verify tag step writes `lapse_final_sent`.
- **Risk:** Journeys on Growth lack tag-update steps in the builder.
- **Fallback:** Client writes `lapse_final_sent` on next app open when it sees `notification_opened` / receives the 14d campaign_id — and if the user never returns, re-entry cooldown set to 90 days approximates "never again" within the event window.

---

## 5. Journey 3 — "Hard-Level Help" (P1, 1 message step)

Purpose: rescue players stuck on a level. **This journey never sells anything.** It gives
away a rewind and a tip. It exists because "stuck and abandoned" is the #1 silent churn
path in level-based puzzles, and because help-not-sell is the product's stated identity.

**Entry**
- Trigger: custom event **`level_failed`** — but the adapter applies the ×2 filter
  client-side: the event is forwarded to OneSignal only on the **second failure of the
  same level within 60 minutes with no completion in between** (analytics backend still
  receives every raw `level_failed`). This keeps journey config trivial, works on any
  plan, and puts the cap logic in code we control (our in-app answer to Enterprise-only
  frequency capping).
- Client-side emit caps: max 1 forwarded event/day, never for levels ≥40, never for a level already helped (`helped_levels` local set), never within 2h of any other push we initiated (adapter keeps a last-push timestamp from `notification_opened` + local schedule log).
- At the same moment, the client sets local `pending_help_level = {level_id}` and banks **one free rewind** for that level — granted on next attempt **whether or not the push is ever delivered**, so the copy's promise ("Your free rewind is loaded") is always true.

**Steps**
1. **Wait** 45 minutes (inside journeys.csv's 30–60 band — long enough that they've genuinely stopped, short enough the frustration context survives).
2. **Branch:** custom event `level_completed`-equivalent check — implemented as: segment filter `last_session` < 45 min ago → exit silently (they came back on their own).
3. **Message step:** `hard_level_help` A/B/C → `catmetro://level/{level_id}` via tag `last_fail_level`. Time Window 10:00–21:00. Variant C ("{fail_pct}% of players need 3+ tries") only enabled after closed-test data bakes a per-level fail table into the client tags; until then A/B split 50/50.
4. **Exit:** message sent, or session, or 24h elapsed.

**Never-sells guarantees:** copy contains no product, no price, no shop link; the deep link
lands on the level intro, not a paywall; on the assisted attempt the rewind sheet shows
the granted free rewind only — the purchase row (`cm_rewind_5`/`cm_rewind_20`) is hidden
for that attempt (rewind_failure placement suppressed). If they fail again later, normal
sheet order resumes: owned → free daily → rewarded → purchase last (brief, paywall
exposure rules).

**Caps:** 1 per level ever; ≤2/week from this journey (client emit gate); re-entry cooldown 72h.

**KPIs:** level completion within 24h of send vs. players who hit the ×2 trigger while the
journey was dark (pre-launch closed-test baseline is the comparison group — honest about
non-randomization); unsubscribe rate after receipt (this is the canary: if >1% of
recipients unsubscribe, pause the journey).

**Harm risk:** MEDIUM if it ever reads as an upsell — mitigated structurally (no purchase
surface on arrival) not just editorially.

- **Decision:** ×2 filter, caps, and reward-granting all live client-side; OneSignal handles only wait + branch + send.
- **Evidence:** journeys.csv row hard_level_help; copy rows 22–24; custom events verified on 5.2.0+/paid plans 2026-07-31; brief paywall-exposure rule "never after first failure".
- **Action:** Build J3 by Aug 20; QA per taxonomy: force each fail path, verify single forward, verify free rewind grants exactly once (ledger).
- **Risk:** Deep link with tag substitution lands on a stale level (player beat it via another device — no cloud save at launch, so single-device; low).
- **Fallback:** DeepLinkRouter validates level state on arrival; if already completed, route to next uncompleted level with a "you got it!" toast.

---

## 6. What does NOT fit in 3 journeys — and how it ships instead

1. **payer_thanks** (P1): IAM at first session start after `purchase_completed(all_access)` — better than the old 15-min push: the buyer is *in the app*. Copy from notification_copy.csv payer_thanks A/B (restore education, "no ads will interrupt your runs"). One-time, sets `payer_thanked` tag, permanently excluded from any sales messaging by `payer_status` segment filters.
2. **purchase_issue** (P1): **local notification** scheduled +2h on `purchase_failed` where `user_cancelled = false`; canceled if `purchase_completed` arrives first. Copy purchase_issue A/B ("No charge went through"), deep links `catmetro://shop` / `catmetro://restore`. Local delivery needs no plan feature, no server, and fires even if push permission handles remote-push oddly — the notification permission still gates it on Android 13+, so if denied, fall back to an IAM at next session.
3. **feedback_request** (P2): IAM after 3 sessions (beta cohort / first 2 weeks post-launch); if IAM unseen 3 sessions, a **one-off dashboard send** to the segment `feedback_pending = true`. 1 per build, never near a failure moment (journeys.csv row 12).
4. **event_start / event_ending** (P1, from Week 5): **one-off scheduled dashboard sends**, created each Monday: event_start Mon 17:00 local (timezone-based delivery) to segment `highest_level >= 8` AND `last_session < 14d`; event_ending Sun morning to segment `active_event = {event_id}` AND NOT completed. Calendar-known content does not deserve a journey slot. Mechanization option if manual scheduling grates: a GitHub Actions cron calling the OneSignal REST create-notification API with the send template — zero servers, and a genuinely good Resourcefulness exhibit (§12).
5. **new_content** (P2): one-off scheduled send per release, local-afternoon delivery, segment `last_session < 30d`.
6. **review_coordination** (P0): unchanged — in-app Google Play native review API only, never a push, never after failure, quota-respecting (verified in-app review constraints, brief).
7. **Streak-expiry local backup** (P0): **Unity Mobile Notifications** (`com.unity.mobile.notifications`; pin exact 2.x patch in the Week-1 SDK spike alongside the brief's other pins) schedules a local notification at streak-expiry −6h whenever a daily is completed with `streak_days >= 3`, canceled on next daily completion. This backstops Journey 1 against push-delivery failure, OneSignal outage, or OS-level throttling — and if remote AND local are both due, the adapter cancels the local one on `notification_opened` to avoid doubles.

- **Decision:** IAM for in-app moments, local notifications for device-known timers, scheduled sends for calendar-known content; journey slots reserved for behavior-triggered automation only.
- **Evidence:** Growth limits verified 2026-07-31; journeys.csv rows 10–15; brief OneSignal DECISION line.
- **Action:** Implement IMessaging adapter surface for IAM triggers + local schedule/cancel by Aug 14; write the Monday send runbook (segment, template, timezone delivery checkbox) into the ops doc.
- **Risk:** Manual Monday sends get forgotten in launch-week chaos.
- **Fallback:** GitHub Actions cron (item 4) — or skip an event announcement entirely; a missed announcement is strictly better than a mis-targeted one.

---

## 7. Permission flow (Android 13 two-attempt budget)

Android 13+ (API 33) makes POST_NOTIFICATIONS a runtime permission and effectively gives
us **two** system-dialog attempts before the OS stops showing the dialog. We spend them
like scarce currency:

1. **Never at first launch.** No prompt of any kind before the player has felt value.
2. **Soft IAM prompt** after the **first `daily_completed`** (the moment notifications become obviously useful): OneSignal IAM push-prompt template, copy: *"Want tomorrow's Line delivered? A daily nudge, plus a streak warning if one's at risk — never at night."* Buttons: "Yes, remind me" / "Not now". Fires `push_soft_prompt_viewed` (taxonomy row 36; cap 1/build, tag `soft_prompt_seen`).
3. "Yes" → `OneSignal.Notifications.RequestPermissionAsync(fallbackToSettings: false)` — **attempt 1**. Result fires `push_permission_result` (grant/deny paths, taxonomy row 37) and tags `push_enabled`.
4. "Not now" or system-deny → nothing until the **second value moment**: `streak_changed` with `new_streak = 3`. Second soft IAM ("Your 3-day streak can get a nightly heads-up"). Accept → **attempt 2**, still `fallbackToSettings: false`.
5. Both attempts spent or exhausted → the only remaining surface is an explicit row in Settings: "Enable notifications" → `RequestPermissionAsync(fallbackToSettings: true)`, which deep-links to app settings when the dialog can no longer be shown. User-initiated, so it never feels like nagging.
6. **API < 33:** permission is default-granted; skip all prompting, rely on caps to keep trust. Channels registered at init: `daily` (default importance), `help`, `account` — users can mute per-channel from OS settings.

- **Decision:** Two system attempts, each gated behind a soft IAM at a real value moment; settings fallback is user-initiated only.
- **Evidence:** Taxonomy rows 36–38 already model this flow; `requestPermission` fallbackToSettings is in OneSignal Unity 5.x API (SDK verified 2026-07-31); agent prompt rules forbid direct system-API calls outside this flow.
- **Action:** Implement in IMessaging + Integrations.OneSignal by Aug 10; QA grant/deny/settings-later on API 33+ and API 29 devices per taxonomy QA column.
- **Risk:** Soft-prompt acceptance is low because it appears only at L7+ (daily unlock), shrinking the reachable base.
- **Fallback:** Add an earlier optional soft prompt at first `level_completed` with 3 stars — only if closed-test opt-in is <35% by Aug 14; measure before moving it.

---

## 8. Tag / event map (from analytics taxonomy destinations)

Adapter rule (from agent_system_prompts.md): every tag/event sent must exist in the
taxonomy CSV; unknown names are a build error in development. The OneSignal adapter is the
only writer.

**Custom events sent to OneSignal** (taxonomy destination `onesignal_event`):

| Event | Journey use |
|---|---|
| `tutorial_completed` | J2 eligibility tag flip (`tutorial_done`) |
| `daily_unlocked` | J1 entry (taxonomy row already lists `onesignal_event`) |
| `daily_completed` | J1 re-entry + exit; outcome |
| `level_failed` (×2-filtered, client-side) | J3 entry |
| `event_joined` / `event_completed` | scheduled-send segment maintenance |
| `purchase_completed` | payer segments; outcome with value |

**Tags written** (taxonomy destination `onesignal_tag` + user_properties needed for copy substitution):

| Tag | Source | Used by |
|---|---|---|
| `push_enabled`, `soft_prompt_seen` | permission flow | all eligibility |
| `tutorial_done` | tutorial_completed | J2 entry |
| `highest_level` | level_completed | event-send segments |
| `last_fail_level`, `hint_color`, `district` | level_failed + level metadata | J2/J3 copy substitution + deep links |
| `daily_unlocked`, `daily_last_done` | daily flow | J1 branches |
| `streak_days` | streak_changed | J1 branch B, copy substitution |
| `active_event` | event_joined | event_ending segment |
| `preferred_theme` | cosmetic_equipped | future creative personalization (P2) |
| `payer_status` | entitlement_changed (RC source of truth) | payer suppression everywhere |
| `lapse_final_sent`, `feedback_pending`, `daily_opt_in` | client flags | J2 promise, feedback send, J1 eligibility |
| RC-written purchase tags | RevenueCat→OneSignal integration | payer courtesy filters (J2) |

- **Decision:** Copy-substitution variables ride as tags; behavioral triggers ride as custom events; the taxonomy CSV is the single registry.
- **Evidence:** analytics_event_taxonomy.csv destination column (rows 2–39); notification_copy.csv substitution variables.
- **Action:** (Taxonomy CSV already lists `onesignal_event` on `daily_unlocked` — no change needed.) Add `hint_color`/`district`/`fail_pct` to the tag-writer spec in the adapter ticket.
- **Risk:** Tag sprawl / per-user tag limits on Growth plan.
- **Fallback:** Prune P2 tags (`preferred_theme`) first; all journeys above need ≤12 tags.

---

## 9. Deep link routing

- Scheme: **`catmetro://`** registered as an intent-filter on the main activity. Routes: `catmetro://daily`, `catmetro://home`, `catmetro://level/{id}`, `catmetro://event/{id}`, `catmetro://shop`, `catmetro://restore`, `catmetro://feedback`.
- Every notification (remote, local, IAM button) carries the URI in additionalData; the OneSignal click listener hands the raw string to the central **DeepLinkRouter** (per agent_system_prompts.md rule), which validates and routes — **safe fallback to Home** on unknown/invalid/stale targets (e.g. level already completed → next uncompleted level + toast; event expired → Home with "event ended" note).
- Click listener registered in **Boot before scene load** so cold-start clicks are never dropped; router resolves after save load (architecture.md state machine: `Deep link → Home(resolve) → LevelIntro`).
- Every routed open fires `notification_opened` with `campaign_id`, `journey_id`, `deep_link` (taxonomy row 38) and stamps the session's `notification_campaign_id` (row 2) for attribution.
- QA (taxonomy row 38): cold + warm + killed process states route correctly, on the device matrix.

- **Decision:** One scheme, one router, one fallback; no per-feature link handling.
- **Evidence:** architecture.md scene/state machine; agent prompt DeepLinkRouter rule; taxonomy rows 38/41.
- **Action:** PlayMode tests for all 7 routes + invalid input (CatMetro.Tests.PlayMode already lists deep-link routing); device smoke in the Aug 21 commercial beta.
- **Risk:** OEM-specific cold-start timing drops the click payload.
- **Fallback:** OneSignal caches last click; router re-checks pending click on first Home load.

---

## 10. RC ↔ OneSignal identity and measurement

**Identity spine**
1. First launch: generate stable app user id `cm_<uuid>` (device-local, no account).
2. Boot order: RC `Purchases.configure(appUserID: cm_<uuid>)` → `OneSignal.initialize` → `OneSignal.Login("cm_<uuid>")`.
3. When the OneSignal user id is available, set RC subscriber attribute **`$onesignalUserId`** via the purchases-unity API — this connects RC's dashboard integration, and **RC writes purchase tags to OneSignal** from then on (both facts verified 2026-07-31).
4. Result: purchase state flows RC→OneSignal without our own server; support/restore correlate on `cm_<uuid>` across both dashboards.

**Measurement**
- **Session attribution:** `notification_opened` → session tagged `notification_campaign_id`; report "sessions within 2h of send / sends" per journey and per copy variant.
- **Outcomes API:** send `daily_completed` (J1), `session_after_lapse` (J2), `level_completed_after_help` (J3) as outcomes; `purchase_completed` as an outcome with value. (`session_after_lapse` and `level_completed_after_help` are **derived-only OneSignal outcome names** — computed from session timing and `level_completed` relative to sends, never emitted by the client; they are deliberately NOT rows in analytics_event_taxonomy.csv, which stays at 45 client events.) — this puts revenue-per-journey inside OneSignal's own reporting, which is exactly the chart the OneSignal judges can read natively.
- **Holdout thinking at our scale:** at a few hundred DAU, formal holdouts are underpowered everywhere except where they're free. So: **no holdout** on J1/J3 (the retention cost of withholding exceeds the information value; use pre/post + variant comparison and say so honestly); **10% holdout on J2 only** (lapsed users cost nothing to hold out) — report rung-level return rates with n's, no significance theater.
- Every reported rate ships with its denominator (this discipline is also the submission's honesty story — see submission_script.md).

- **Decision:** Single `cm_<uuid>` identity in both SDKs; $onesignalUserId set at boot; outcomes carry the revenue linkage; holdout only where it's free.
- **Evidence:** RC $onesignalUserId attribute + RC purchase-tag writing verified 2026-07-31 (brief); taxonomy rows 2, 31, 38.
- **Action:** Implement identity spine in Boot composition root by Aug 8; verify a sandbox purchase produces RC tags on the OneSignal user; configure 3 outcomes in dashboard by Aug 20.
- **Risk:** RC integration tag names/latency differ from expectation, breaking payer filters at launch.
- **Fallback:** `payer_status` tag written by our own adapter from `entitlement_changed` (taxonomy row 35) is the primary payer filter; RC-written tags are corroboration, not a dependency.

---

## 11. Data Safety, and Unity-specific limitations

**Play Data Safety form implications (must match behavior exactly):**
- Collected: **Device or other IDs** (FCM token, OneSignal id, `cm_<uuid>`) — shared with OneSignal (push) and Google (FCM); **App interactions** (analytics events, taxonomy privacy class behavioral_no_pii); **Purchase history** (RevenueCat, transactional class). Advertising ID is declared under the ads integration (AdMob) — covered in the ads spec, listed here because reviewers read the form as one document.
- Declared handling: encrypted in transit; not sold; data deletion available on request (published support email routes to OneSignal user-deletion API + RC customer deletion). No account creation → Play's account-deletion requirement does not attach, but we still publish the deletion path.
- POST_NOTIFICATIONS permission appears in the manifest — consistent with the store listing's "optional reminders" phrasing. Nothing in our copy or store listing may promise "no notifications."
- 13+ target audience (brief, Families): notification content contains no child-directed appeal.

**Unity 5.3.2 limitations we design around (verified 2026-07-31):**
1. Still ships Google **EDM4U** → exactly one EDM4U copy (1.2.188) across RC/GMA/OneSignal; custom `mainTemplate.gradle` + `gradleTemplate.properties` under version control; Force Resolve in CI (architecture.md risk zone 1–2).
2. Custom events need a **paid plan** — a Free-plan downgrade silently breaks J1/J3 entries; plan status is a launch-checklist item.
3. No frequency capping / quiet hours on Growth → all caps live in journey design + the adapter's send-gating (documented per journey above); Time Windows are mandatory on every message step.
4. ProGuard/R8 keep rules for OneSignal push receivers; test the minified release build in Week 2, not launch week (architecture.md risk zone 4).
5. Notification small icon must be provided (monochrome silhouette of the cat-train mark) or Android renders a default bell — polish item, Aug 18.
6. Server-side send logic beyond journeys requires the REST API — we have no server; the GitHub Actions cron pattern (§6) is the approved shape if needed.

- **Decision:** Data Safety form drafted from this section verbatim; Unity limitations all have named owners in the schedule.
- **Evidence:** Brief platform facts (EDM4U, plan gating, FCM v1) verified 2026-07-31; architecture.md risk zones.
- **Action:** Draft Data Safety form entries alongside the Aug 21 commercial beta; add "OneSignal plan = Growth, custom events firing" to the launch checklist.
- **Risk:** Data Safety mismatch triggers a Play review rejection inside the launch window.
- **Fallback:** Form is reviewed against actual network traffic (device proxy capture) on Aug 20 — before the Aug 24–26 production submission, leaving the buffer days to fix.

---

## 12. OneSignal award narrative (Implementation / User value / Resourcefulness)

Award facts (verified 2026-07-31): $25k/$15k/$5k; "a single deployed message is sufficient
for eligibility"; judged on **Implementation, User value, Resourcefulness**. We are past
eligibility by Week 2; the narrative aims at the criteria:

- **Implementation:** the full surface in one small game — push + IAM + tags + custom events + Time Windows + deep links + outcomes + `Login(external_id)` + the RC `$onesignalUserId` integration, with a typed adapter, a taxonomy-enforced tag registry, and device-tested cold/warm/killed deep-link routing. Exhibits: journey canvas screenshots, outcomes chart with revenue linkage, adapter code excerpt.
- **User value:** messaging that gives before it asks — Journey 3 sends a free rewind and *never sells*; Journey 2 ends with an honest "no more reminders after this" and keeps the promise with a tag; Time Windows enforce sleep hours the plan doesn't provide; a two-attempt Android 13 permission budget spent only at value moments. Exhibit: unsubscribe rate + the copy itself.
- **Resourcefulness — the core story:** *rich retention inside a 3-journey / 6-message-step budget.* A 13-touchpoint retention design (onesignal_journeys.csv) compressed into 2+3+1 message steps by making journeys event-armed and self-silencing; frequency capping (Enterprise-only) rebuilt client-side in the adapter; quiet hours (unavailable) rebuilt with Time Windows; streak protection backstopped by **Unity local notifications** so the system degrades gracefully; purchase-recovery shipped as a +2h local notification needing no plan feature at all; calendar content kept out of journeys via scheduled sends (optionally a GitHub Actions cron hitting the REST API — automation with zero servers). A solo dev on the $19/mo plan getting Enterprise-shaped behavior by design is precisely a Resourcefulness rubric answer.

- **Decision:** Lead the award write-up with the budget-compression story; Implementation and User value are supporting evidence.
- **Evidence:** Award criteria and plan limits verified 2026-07-31 (brief); everything claimed exists in §§3–7 with ship dates inside the window.
- **Action:** Capture journey-canvas + outcomes screenshots the week of Sep 22 (real data, not staged); write the 200-word award paragraph into the Devpost draft (see submission_script.md §2).
- **Risk:** Low send volumes make the outcomes charts look thin to judges.
- **Fallback:** Present rates with denominators and the design artifacts (canvas, copy CSV, caps table) — the rubric rewards the system's craft, not raw volume; "a single deployed message is sufficient" sets the eligibility floor explicitly.
