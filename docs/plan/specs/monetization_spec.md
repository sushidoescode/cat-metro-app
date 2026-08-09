# Cat Metro — Monetization Specification

Status: v1.0, 31 Jul 2026. Source of truth: `deliverables/DECISIONS_BRIEF.md` (locked 31 Jul 2026).
Model: **B — Balanced Hybrid (LOCKED)**. Contingency: **A — Minimal Ethical Premium** (trigger in §2.4).
All prices below are US reference prices; the client never renders a hard-coded price string (§7).
Analytics event names in this spec are the canonical names from `deliverables/data/analytics_event_taxonomy.csv`.
Push/IAM copy referenced by row from `deliverables/data/notification_copy.csv`.

Catalog (Google Play product IDs, locked):

| Product ID | Type | US price | Entitlement(s) | Contents | Priority |
|---|---|---|---|---|---|
| `cm_all_access` | non-consumable | $6.99 | `all_access` | Night Harbor bonus district (10 levels), both premium themes, daily free rewind doubled (2/day), permanent removal of ALL non-rewarded ad surfaces, gold conductor badge | P0 |
| `cm_supporter_pack` | non-consumable | $9.99 | `supporter` + `all_access` | Everything in All Access + Founder livery + name-a-cat cameo (local) + supporter badge. Shop only, never interrupts | P1 |
| `cm_theme_sakura` | non-consumable | $2.99 | `theme_sakura` | Sakura Line theme | P1 |
| `cm_theme_neon` | non-consumable | $2.99 | `theme_neon` | Neon Line theme | P1 |
| `cm_rewind_5` | consumable | $1.99 | — (client ledger) | 5 rewinds | P0 |
| `cm_rewind_20` | consumable | $4.99 | — (client ledger) | 20 rewinds | P0 |
| `cm_all_access_499` | non-consumable, **experiment-only SKU** | $4.99 | `all_access` | Identical to `cm_all_access`; exists only so RC Experiments can price-test $4.99 vs $6.99 (Play prices are per-SKU) | P1 |
| Theme bundle | — | — | — | **Cut** (decoy confusion; All Access IS the bundle) | Cut |
| Currency packs, IAP streak savers, chapter packs, audio packs, monthly club, season pass, any subscription | — | — | — | **Cut** per brief §MONETIZATION and §5 below | Cut |

Rewind definition (locked): rewind to last safe decision tick; never required to finish any level; 1 free/day for every player; extras via rewarded ad (2/session, 5/day) or packs. All Access doubles the daily free rewind to 2/day (this is the concrete benefit behind the brief's "daily free rewind" line item).

---

## 1. Principles — the Fair-by-Design manifesto

Positioning line (locked, used verbatim in store listing, paywalls, and Devpost): **"Fair by design: no forced ads, no energy, no loot boxes, every level solvable free."**

Every principle below is anchored to verified comp evidence (all verified 2026-07-31), not taste:

1. **No forced ads, ever, at launch.** No interstitials, no banners, no app-open ads. Ads exist only where the player explicitly asks for a benefit (rewarded, player-initiated). Evidence: Arrows–Puzzle Escape reached 103.6M installs/12 months at 4.83★ but its loudest review theme is "ad every other level" backlash; Bus Traffic Fever's forced 30s ads correlate with a 3.72★ rating despite 15.4M installs in 5 months. The single loudest complaint across every F2P comp is forced interruption — we simply do not build the surface.
2. **Every level is solvable free.** Rewind is a convenience (skip the redo), never a requirement. No difficulty tuned to sell. Evidence: Mini Metro (no ads/IAP, 4.63★, 3.6M installs) proves the genre audience pays for fairness with ratings and word of mouth.
3. **Gentle price ceiling, one "complete edition."** The catalog tops out at $9.99 and the honest recommendation is $6.99 once. Evidence: Neko Atsume holds 4.78★ at 13.6M installs with IAP capped at $3.49 — gentle works in cat games; Railbound's $4.99 upfront price capped it at 211K installs despite an Apple Design Award — so we are free with a paid completion, not paid upfront.
4. **No dark patterns.** No fake countdowns, no fabricated "sale ends" timers, no preselected checkboxes, no shrinking/delayed/deceptive close buttons, no confirm-shaming ("No thanks, I hate cats" is banned copy), no pay-to-win, no energy, no loot boxes, no gacha odds.
5. **Never sell into frustration.** No offer of any kind after a first failure on a level. Rewind surfaces appear from attempt 2 and lead with free options. (Same discipline as the verified Play in-app review guidance: never prompt after failure.)
6. **Payers are done being sold to.** Owning `all_access` suppresses every system-initiated paywall permanently (§3.11). The only things a payer ever sees offered are consumable rewinds (useful) and the Supporter Pack (framed as a tip jar, shop only).
7. **Transparent money handling.** Restore always one tap away; refunds handled without shaming and with progress preserved (§3.14); "You were not charged" stated explicitly on failures (§3.12).
8. **The manifesto is the marketing.** "No forced ads" appears in the store listing's first paragraph and the paywall trust line. It attacks the loudest complaint in every F2P comp while avoiding premium's install ceiling — this is the strategy the verified data supports, and it is simultaneously the Catvertising narrative ("ads only when the player asks").

**Decision:** Fair-by-design is a hard constraint set, not a tone; every surface in §3–4 is checked against principles 1–7 before ship.
**Evidence:** Verified 2026-07-31 comp data — Arrows 4.83★/ad backlash, Bus Traffic Fever 3.72★/forced ads, Mini Metro 4.63★/no-IAP ceiling, Railbound 211K premium cap, Neko Atsume 4.78★/gentle IAP.
**Action:** Encode principles as a pre-merge checklist in the UI PR template; positioning line into store listing draft and paywall trust line this week (D1–D7).
**Risk:** Fairness constraints cap short-window revenue vs aggressive F2P, and Grand Prize shortlist = window revenue.
**Fallback:** Accept the cap — target awards (Best Game/HAMM/Catvertising) explicitly reward monetization *fit*, not maximization; revenue upside comes from conversion quality and the $6.99 anchor, not surface count.

---

## 2. Model comparison — A vs B vs C

### 2.1 The three candidates

**Model A — Minimal Ethical Premium.** Free 30-level campaign + Daily Line; single $6.99 All Access unlock (bonus district + themes + badge); themes also à la carte; **zero ad SDK**; rewinds = 1 free/day + purchasable packs only (no rewarded refill, theme rental cut). Simplest possible build, purest story, smallest revenue machine.

**Model B — Balanced Hybrid (CHOSEN, LOCKED).** Catalog above + player-initiated rewarded ads on five surfaces (rewind_failure 2/session·5/day, double_tickets 3/day, daily_gift_double 1/day, streak_saver 1/day, theme_rental 3 levels·1/theme/day) via AdMob with RC AdTracker on every ad event. Free + gentle IAP + optional rewarded + explicit "no forced ads" positioning.

**Model C — Live-Ops Stretch (REJECTED).** Model B + weekly content drops as paid chapter packs, a season-pass/subscription track, currency packs, and capped interstitials to fund it. Rejected: a solo dev cannot honestly sustain the content cadence in 8 weeks (see §5), interstitials torch the positioning and rating, and the build cost lands exactly on the launch-critical path.

### 2.2 Scorecard

Scoring 1–5 where **5 is always best for us** (for risk/complexity/burden rows, 5 = lowest risk/complexity/burden).

| Criterion | A: Minimal Ethical Premium | B: Balanced Hybrid | C: Live-Ops Stretch |
|---|---|---|---|
| Player fairness | 5 | 5 | 3 |
| Revenue potential | 2 | 4 | 5 |
| Conversion | 2 | 4 | 4 |
| Retention impact | 3 | 4 | 5 |
| Rating risk (5 = lowest) | 5 | 4 | 2 |
| Dev complexity (5 = simplest) | 5 | 3 | 1 |
| Live-ops burden (5 = lightest) | 5 | 4 | 1 |
| RevenueCat alignment | 2 | 5 | 5 |
| HAMM narrative | 2 | 5 | 4 |
| Catvertising narrative | 1 | 5 | 3 |
| 8-week feasibility | 5 | 4 | 1 |
| **TOTAL** | **37** | **47** | **34** |

Scoring rationale, one line each:
- **A** is maximally fair and shippable but monetizes one moment (the unlock), has no ads story at all (Catvertising 1), and gives HAMM judges a single-product paywall to look at.
- **B** keeps every fairness property (rewarded-only ads are opt-in by construction), adds five conversion surfaces + an ad revenue stream, exercises RC Offerings/Placements/Paywalls/AdTracker/Experiments — the full HAMM story — and "ads only when the player asks" IS the Catvertising entry. Cost: AdMob/EDM4U integration risk and ad-ops.
- **C** wins raw revenue/retention on paper but fails 8-week feasibility (1), triples live-ops burden, reintroduces the forced-ad complaint we built the brand against, and requires the subscription we formally reject in §5.

### 2.3 Recommendation

**Model B**, exactly as locked in the brief. All five rewarded surfaces are P0/P1 as follows: rewind_failure **P0**; double_tickets **P1**; daily_gift_double **P1**; streak_saver **P1** (mechanic exists Week 2+, surface ships with Daily Line); theme_rental **P1**. Interstitials/banners/app-open: **Cut** at launch.

### 2.4 Contingency — Model A "premium-lean"

**Trigger (hard gate):** RevenueCat Ads public-beta access not granted by **D10 (Aug 10)** OR AdMob app/ad-unit approval not working end-to-end on device by **D14 (Aug 14)** smoke gate. (Fallback ad SDK AppLovin MAX 8.6.4 is attempted first if the failure is AdMob-specific; if MAX also fails the D14 gate, contingency fires.)
**What changes:** ship Model A — same catalog and prices, `ads_enabled` feature flag OFF (flag already specced in `architecture.md`), rewind economy = 1 free/day (+1 for All Access) + packs, theme rental and the four other rewarded surfaces dark. Ads arrive in a post-launch update if/when access lands; Catvertising entry is dropped from the award slate and effort shifts to HAMM + OneSignal.
**What does NOT change:** paywall placements, offerings, pricing, copy, suppression rules — the entire §3/§4 spec is ad-agnostic except the explicitly ad-marked rows.

**Decision:** Model B locked; Model A is the wired-in contingency behind the `ads_enabled` flag; Model C rejected for content burden.
**Evidence:** Brief §MONETIZATION (locked); scorecard totals 47/37/34; RC Ads is public beta requiring access grant (verified 2026-07-31); AdMob Unity 11.3.0 + EDM4U pitfalls verified.
**Action:** Request RC Ads beta access Day 1 (Aug 1); AdMob account + ad units Day 1; device spike of rewarded flow by D10; go/no-go recorded at D14 gate.
**Risk:** Beta access timing is outside our control; a late grant (Aug 11–20) tempts scope creep into launch week.
**Fallback:** Hard rule — if not green by D14, ads ship post-launch only; no ad code merges after D21 regardless.

---

## 3. Purchase-journey map — first launch → Day 30

### 3.0 Global rules (apply to every moment below)

- **RC Placements (locked five):** `post_level_5`, `theme_preview`, `bonus_district`, `shop`, `rewind_failure`. Every commerce surface resolves its offering through exactly one of these. Placement→offering mapping:

| Placement | Offering (default) | Experiment variant | Renderer |
|---|---|---|---|
| `post_level_5` | `ofr_core` (packages: `all_access`) | `ofr_core_b` (`cm_all_access_499`) | RC Paywalls v2 (device-tested; custom fallback per brief — 3 open Android crash issues #745/#736/#732) |
| `theme_preview` | `ofr_themes` (packages: `theme_sakura`, `theme_neon`, `all_access` cross-line) | — | Custom bottom sheet |
| `bonus_district` | `ofr_core` | `ofr_core_b` | Custom full-screen |
| `shop` | `ofr_shop` + full catalog via Offerings.All | price variant flows through | Custom shop screen |
| `rewind_failure` | `ofr_rewind` (packages: `rewind_5`, `rewind_20`) | — | Custom bottom sheet |

- **Entitlement attach:** `cm_supporter_pack` attaches to BOTH `supporter` and `all_access` entitlements in RC, so one `all_access` check gates all premium content.
- **Global frequency guard:** max ONE system-initiated commerce surface per session; never two commerce modals back-to-back; never any offer within 60s of a failure unless the player tapped the rewind chip themselves.
- **Payer suppression matrix (P0):** `all_access` or `supporter` active → suppress moments 3.1, 3.5 card, 3.6 lock (district is simply unlocked), theme upsells (owned = "Owned ✓"), All Access cross-sells inside sheets. Still shown: rewind packs (only valid offer to a payer) and the Supporter card in shop (reframed, §3.11). Refund-revoked users: additionally suppress ALL system-initiated paywalls for 30 days (§3.14).
- **Success-metric honesty:** no credible public puzzle ARPDAU/opt-in%/refund benchmarks exist (verified 2026-07-31) — targets below are self-set from adjacent verified data (casual D90 IAP ARPU $1.34/ARPPU $7.26, AppsFlyer 2026; 2025 Shipaton grand-winner calibration 17k users/$30,017/1,750 payers) and will be re-baselined at D+14 of live data.

**Day 0→30 arc at a glance:** D0 install → L1–L5 with zero commerce UI → 3.1 fires once at L5 win → L7 unlocks Daily Line + rewarded surfaces go live → D2–7: 3.2/3.4/3.5 as played → D7+: streaks, 3.9 → inactivity: 3.10 ladder (48h/7d/14d) → throughout: 3.6/3.7 player-initiated, 3.11–3.14 as events occur.

---

### 3.1 First exposure — post-L5 celebration paywall (P0)

| Field | Spec |
|---|---|
| Trigger | `level_completed` for L5, first completion only, after the results celebration finishes (score counted, stars landed), before return to map |
| Player state | ~15–25 min in, 5 straight wins, tutorial done, has earned tickets, has NOT necessarily failed anything; peak honeymoon |
| Screen | Full-screen RC Paywalls v2, celebratory template; confetti continuity from results screen |
| Offer | `cm_all_access` $6.99 only (Supporter is shop-only per brief; no decoys) |
| Exact copy | Full copy block in §4.1 ("One ticket. Every line.") |
| Visual treatment | Night Harbor diorama hero render at top; benefit list with icons; close "✕" top-left, ≥48dp, full opacity from first frame; no countdown, nothing preselected |
| Dismissal | ✕, Android back gesture, and "Keep playing free" text button all close instantly; no exit-intent counter-offer |
| Frequency cap | Once per install, ever (this moment) |
| Cooldown | After dismissal, no system-initiated commerce surface until at least the next session |
| Eligibility | L5 first-win AND no entitlements AND no prior purchase of any SKU |
| Suppression | Payers, refund-suppressed (§3.14), and anyone who bought a theme/rewind pack before L5 (they found the shop; don't interrupt) |
| RC offering/placement | Placement `post_level_5` → `ofr_core` (experiment: `ofr_core_b` at $4.99) |
| Analytics | `paywall_viewed{placement:post_level_5, offering_id, paywall_variant}` → `paywall_dismissed` / `purchase_started` → `purchase_completed`/`purchase_failed`; RC impression auto-tracked (verify parity per taxonomy QA) |
| Success metric | View→purchase ≥1.5% by D+14 of data; median view duration ≥6s (people actually read it); D1 retention of viewers not lower than non-viewers (guardrail) |
| Player-experience risk | Interrupting the honeymoon reads as an ad. Mitigations: fires once ever, celebratory not gating, closes in one tap, and L5 is a natural chapter boundary (District 1 complete) |

### 3.2 Post-value theme preview (P1)

| Field | Spec |
|---|---|
| Trigger | Player taps a locked theme swatch (map header, settings, or results-screen theme chip) — player-initiated only; system never opens it |
| Player state | Curious, mid-session, has seen default cream/navy/teal/orange board; typically L6+ |
| Screen | Bottom sheet over the LIVE board — the board behind the sheet actually re-skins to the previewed theme while the sheet is open (real preview, not a screenshot) |
| Offer | Tapped theme at $2.99; theme_rental rewarded ad ("try for 3 levels"); one-line All Access cross-sell |
| Exact copy | §4.2 |
| Visual treatment | Sheet covers ≤45% of screen so the re-skinned board is the hero; swatch toggle Sakura/Neon inside the sheet |
| Dismissal | Swipe down, ✕, back gesture; board reverts to owned theme instantly |
| Frequency cap | None on opening (player-initiated); theme_rental capped 1/theme/day (locked) |
| Cooldown | Rental expiry (3 levels) shows a passive "That was the Sakura Line" toast with a shop link — max 1 such toast/day |
| Eligibility | Theme not owned; rental row hidden when `ads_enabled` off or daily rental used |
| Suppression | `all_access`/`supporter` → both themes owned, swatches never show lock; owned single theme shows "Owned ✓" |
| RC offering/placement | Placement `theme_preview` → `ofr_themes` |
| Analytics | `paywall_viewed{placement:theme_preview}`, `ad_offer_viewed{placement:theme_rental}`, `rewarded_ad_*`, `purchase_*`, `cosmetic_unlocked{method:iap}`, `cosmetic_equipped` |
| Success metric | Preview→(purchase OR rental) ≥25%; rental→purchase within 7d ≥8% (self-set; re-baseline D+14) |
| Player-experience risk | Rental expiry could feel like a take-away. Mitigation: expiry is silent revert + one gentle toast, never a modal, never mid-level |

### 3.3 First failure — NO offer (P0 rule, enforced)

| Field | Spec |
|---|---|
| Trigger | First `level_failed` on any level (attempt 1) |
| Player state | Frustrated, possibly confused; the single worst moment to monetize |
| Screen | FailureReview only: cause-first failure camera ("The orange platform overflowed — watch where it started"), next-wave preview, big "Try again" (instant retry <1s per brief) |
| Offer | **NONE. No rewind chip, no ad offer, no shop badge animation. This row exists to make the absence testable.** |
| Exact copy | Failure screen: "The {color} platform overflowed. Watch where the jam started." CTA: "Try again" · secondary: "Back to map" |
| Visual treatment | Replay scrub of the failure cause; zero commerce affordances rendered |
| Dismissal | n/a |
| Frequency cap | Applies on attempt 1 of EVERY level, not just the first level ever |
| Cooldown | Global 60s no-offer window after any failure (see §3.0) |
| Eligibility | attempt == 1 |
| Suppression | n/a — rule applies to everyone including payers (an owned-rewind chip would still be selling the habit) |
| RC offering/placement | None by design |
| Analytics | `level_failed{level_id, attempt:1, fail_reason, progress_pct}` only |
| Success metric | Attempt-1 retry rate ≥80%; QA smoke asserts no `paywall_viewed`/`ad_offer_viewed` can fire with attempt==1 |
| Player-experience risk | None — this is risk removal. Dev risk is regression; covered by an EditMode test on the offer-eligibility rule |

### 3.4 Eligible failure, attempt 2+ — rewind sheet (P0)

| Field | Spec |
|---|---|
| Trigger | `level_failed` with attempt ≥2 on the same level AND progress_pct ≥40% (a near-miss worth saving); FailureReview then shows an inline "⏪ Rewind" chip next to "Try again"; the SHEET opens only when the player taps the chip |
| Player state | Invested (2+ attempts), knows the board, lost late; a rewind has genuine utility |
| Screen | Bottom sheet over FailureReview (placement `rewind_failure`) |
| Offer | Ordered: (1) today's free rewind → (2) owned balance → (3) rewarded ad → divider → (4) `cm_rewind_5` $1.99 / `cm_rewind_20` $4.99. Purchase is always below the fold of free options (locked: "purchase secondary") |
| Exact copy | §4.3 |
| Visual treatment | Free/owned/ad rows styled identically (no visual thumb on the scale); pack rows smaller, under "Need more?"; footer fairness line always visible |
| Dismissal | Swipe down, ✕, back, or "No thanks, retry from start" — retry is never gated on the sheet |
| Frequency cap | Chip: always available when eligible. Sheet auto-behavior: none (opens only on tap). Rewarded rewind: 2/session, 5/day (locked) |
| Cooldown | After a purchase in this sheet, sheet suppresses pack rows for 24h (they have inventory; don't restock-sell) |
| Eligibility | attempt ≥2, progress ≥40%, a safe decision tick exists to rewind to |
| Suppression | Payers see the same sheet minus the All Access cross-line; refund-suppressed users see free/owned/ad rows only for 30d |
| RC offering/placement | Placement `rewind_failure` → `ofr_rewind`; consumable fulfillment via our durable ledger + RC webhooks (brief: fulfillment ledger ours) |
| Analytics | `paywall_viewed{placement:rewind_failure}`, `rewind_used{source:free\|purchased\|rewarded, balance_after}`, `ad_offer_viewed/declined`, `rewarded_ad_started/completed/failed`, `purchase_*` |
| Success metric | Chip-tap→rewind-used ≥60%; rewind-pack attach rate among players with ≥10 failures ≥4%; ledger reconciliation = 0 mismatches (hard gate) |
| Player-experience risk | Any purchase UI near failure can read as monetized difficulty. Mitigations: player-initiated sheet, free options first, footer states the level is solvable without it, and solver-validated levels make that footer TRUE |

### 3.5 Chapter (district) complete (P1)

| Field | Spec |
|---|---|
| Trigger | 5th level of a district completed, districts 2–6 only (District 1's completion = L5 = moment 3.1; never both) |
| Player state | Achievement high; natural pause point |
| Screen | District-complete celebration screen; below the "Continue" button, a passive card — NOT a modal |
| Offer | Card links to shop; no product rendered inline |
| Exact copy | Card: "District {n} complete — your metro is growing. Everything Cat Metro sells lives in one small shop." CTA: "Visit shop" · card dismiss: "✕" |
| Visual treatment | Card at 60% visual weight of the Continue button; no badge animation, no price on the card |
| Dismissal | ✕ hides this card for all future districts (persisted `shop_card_optout` flag); Continue always primary |
| Frequency cap | Max 1 card per district completion; max 5 lifetime |
| Cooldown | Not shown if any commerce surface already appeared this session (global rule §3.0) |
| Eligibility | Non-payer, `shop_card_optout` false |
| Suppression | Payers and refund-suppressed never see the card |
| RC offering/placement | Card → shop screen → placement `shop` |
| Analytics | `iam_viewed{message_id:district_shop_card}` equivalent local event, then standard `paywall_viewed{placement:shop}` if opened |
| Success metric | Card→shop open ≥10%; opt-out rate <30% (higher means the card annoys) |
| Player-experience risk | Cheapening a celebration. Mitigation: passive card, one ✕ kills it forever |

### 3.6 Bonus district lock — Night Harbor (P0)

| Field | Spec |
|---|---|
| Trigger | Player taps the "Night Harbor" district on the map (visible but marked "All Access" from first map view — aspirational, not hidden) |
| Player state | Self-selected high intent; may be any progression depth |
| Screen | Custom full-screen unlock view: district hero art, the 10 level nodes ghosted with names visible, All Access benefit list |
| Offer | `cm_all_access` $6.99 |
| Exact copy | §4.1 variant: headline "Night Harbor runs on All Access"; body: "10 handcrafted night-shift levels — plus both themes, a doubled daily rewind, and a permanent ad-free guarantee. One purchase, yours forever." CTA: "Unlock All Access — {localized_price}" · secondary: "Back to the map" |
| Visual treatment | Same layout grammar as §4.1 with district art as hero; ghosted level names create concrete desire ("Last Train Home", "Foghorn Junction" etc. from level content) |
| Dismissal | ✕, back, secondary button — instant |
| Frequency cap | None (purely player-initiated); the map tile itself never pulses or badges |
| Cooldown | n/a |
| Eligibility | Non-payer taps the tile |
| Suppression | `all_access` → tile is simply unlocked; refund-suppressed users still get this view on tap (it's player-initiated) but with no urgency styling — same as everyone |
| RC offering/placement | Placement `bonus_district` → `ofr_core` (experiment variant flows through) |
| Analytics | `paywall_viewed{placement:bonus_district, trigger:map_tap}`, `purchase_*` |
| Success metric | Highest-intent surface: view→purchase ≥6% (self-set); Night Harbor tap rate among D7 retained ≥40% |
| Player-experience risk | Locked content on the map can read as paywall-in-your-face. Mitigation: tile is static, honest ("All Access"), and the free campaign is 30 levels — the locked 10 are clearly the bonus, not the game |

### 3.7 Repeated-play shop (P0)

| Field | Spec |
|---|---|
| Trigger | Player opens the Shop tab from Home (always available, never badged except ≤1× per genuine content change) |
| Player state | Browsing intent, zero pressure context |
| Screen | Single scroll screen, order: (1) All Access hero card → (2) themes row (with preview links to 3.2) → (3) rewind packs row → (4) Supporter Pack card (bottom) → (5) footer: "Restore purchases" + "Manage" links |
| Offer | Full catalog |
| Exact copy | All Access card = §4.1 condensed (headline + 3 benefits + CTA). Themes row: "Sakura Line — {price}" / "Neon Line — {price}" / "Preview". Rewind rows: "5 rewinds — {price}" / "20 rewinds — {price} · best per rewind". Supporter card: §4.4. Footer trust line: "Fair by design: no forced ads, no energy, no loot boxes." |
| Visual treatment | Calm, catalog-like; prices rendered once per item from store data; "best per rewind" is a factual per-unit claim, the only comparative badge allowed |
| Dismissal | Back/tab-away |
| Frequency cap | Shop icon badge: max 1 per content change, clears on open, never pulses |
| Cooldown | n/a |
| Eligibility | Everyone |
| Suppression | Owned items → "Owned ✓" (never hidden — payers can see what they own); payer shop = rewind packs + reframed Supporter card (§3.11) |
| RC offering/placement | Placement `shop` → `ofr_shop` + Offerings.All for full catalog |
| Analytics | `paywall_viewed{placement:shop, trigger:tab}`, per-item `purchase_started/completed/failed`, `restore_started/completed` |
| Success metric | Shop-open rate ≥15% of WAU; shop-origin share of revenue ≥30% by D30 (healthy = people buy unprompted) |
| Player-experience risk | Minimal; a shop tab is expected. Risk is neglect — stale shop reads as abandoned. Weekly copy pass scheduled with the mini-event from Week 5 |

### 3.8 Ad-fatigued player (P1)

| Field | Spec |
|---|---|
| Trigger | Either (a) daily rewarded-rewind cap reached (5/day) or session cap (2/session), or (b) 3 consecutive `ad_offer_declined` across any placements, or (c) `rewarded_ad_failed` (no-fill) |
| Player state | (a) heavy engaged user out of free refills; (b) player telling us they don't want ads; (c) victim of network conditions |
| Screen | Same surfaces as 3.2/3.4, degraded gracefully — never a new surface |
| Offer | (a) rewind sheet reorders: owned balance first, then packs with cap explainer; ad row hidden. (b) ad rows hidden everywhere for 24h (respect the signal). (c) ad row hidden this session; nothing substituted |
| Exact copy | (a) "Out of ad rewinds for today — they refresh at midnight. Packs never expire." (c) toast: "Ads aren't loading right now — your free rewind is unaffected." |
| Visual treatment | Rows disappear cleanly; no grayed-out broken buttons; no upsell animation replaces a hidden ad row |
| Dismissal | Standard sheet dismissal |
| Frequency cap | Cap-explainer line max 1 render/day; the 24h decline-mute resets on any player-initiated ad tap |
| Cooldown | Caps reset midnight local; per brief, streak/timezone edge cases follow `streak_changed` DST test discipline |
| Eligibility | As triggers above |
| Suppression | Payers already see no All Access cross-sell; this moment never becomes a paywall push — packs simply hold their normal shop position |
| RC offering/placement | `rewind_failure` (reordered variant); no new placement |
| Analytics | `ad_offer_viewed{eligibility_reason}`, `ad_offer_declined`, `rewarded_ad_failed{error_code}`, then standard sheet events |
| Success metric | Zero "broken ad button" reviews; pack conversion among capped users ≥2× baseline (validates the reorder is service, not pressure) |
| Player-experience risk | Hiding the ad row can look like a bait-to-paid switch. Mitigation: the cap explainer states the refresh time, and free rewind + full retry always remain |

### 3.9 Returning player, D2–D7 (P1)

| Field | Spec |
|---|---|
| Trigger | `app_open` with install_age 2–7 days, ≥24h since last session, non-payer, ≥8 levels played |
| Player state | Coming back voluntarily; retention moment, NOT a monetization moment |
| Screen | After their next `level_completed` (never at app open), a OneSignal IAM |
| Offer | None. Gift only: daily gift called out (30–80 tickets; first daily 100 per locked economy) + Daily Line pointer |
| Exact copy | IAM: "Welcome back — your daily gift is at the depot, and today's Daily Line takes about a minute." CTA: "Collect" · dismiss: "Later" |
| Visual treatment | Small top banner IAM, auto-dismiss 6s |
| Dismissal | Tap-through or auto |
| Frequency cap | Max 1 per 7 days per player |
| Cooldown | Not shown in a session where any commerce surface appeared |
| Eligibility | As trigger; requires `push_soft_prompt` independence (IAM works without push permission) |
| Suppression | Payers get the same IAM (it's a gift, not an offer) minus any shop reference |
| RC offering/placement | None — deliberate: the moment builds the habit that makes 3.7 work |
| Analytics | `iam_viewed{message_id:welcome_back}`, `ticket_earned{source:gift}`, `daily_started` |
| Success metric | IAM→daily_started ≥35%; D7 retention of recipients ≥ control (guardrail: the IAM must not annoy) |
| Player-experience risk | Even a gift IAM interrupts. Mitigation: post-win timing, 6s auto-dismiss, 7-day cap |

### 3.10 Lapsed player — 48h/7d/14d ladder (P1)

| Field | Spec |
|---|---|
| Trigger | OneSignal Journey 2 "Lapse ladder" (single journey, 48h→7d→14d branches — locked Growth-plan design; see `onesignal_journeys.csv`) |
| Player state | Gone. Every message must be worth its interruption |
| Screen | Push (copy rows `inactivity_48h` A–C, `winback_7d` A–C, `winback_14d` A–C in `notification_copy.csv`) → deep link → Home |
| Offer | 14d branch only: winback_14d variant C promises "a free premium-theme trial" → implemented as ONE theme_rental grant (3 levels, no ad required), auto-armed on next session, flagged `lapse_rental_used` once per lapse cycle. 48h/7d branches: content/help only, zero commerce |
| Exact copy | Push copy is locked in `notification_copy.csv` (variants A/B/C per step); re-entry toast for 14d returners: "Your free Sakura Line trial is on — next 3 levels." |
| Visual treatment | Re-entry session is commerce-silent: no paywalls, no shop badge, no IAM beyond the trial toast |
| Dismissal | Standard push behavior; 14d step is explicitly final ("no more reminders after this" — honored in code: journey exits, `winback_optout` tag set) |
| Frequency cap | One ladder traversal per lapse; ladder re-arms only after 7 consecutive active days |
| Cooldown | 48h commerce-silence window after any winback re-entry |
| Eligibility | Non-payer for winback offers; push permission granted |
| Suppression | Payers exit the ladder at entry check (payer_status tag from RC integration) — a payer who lapses gets the `payer_thanks`-toned content variant (service, never selling); refund-suppressed users get content-only variants |
| RC offering/placement | None directly; if the returned player later opens shop, standard `shop` placement. Trial theme's expiry toast links to `theme_preview` |
| Analytics | `notification_opened{journey_id:lapse_ladder}`, `app_open{notification_campaign_id}`, `cosmetic_equipped` (trial), `paywall_viewed` only if player navigates there |
| Success metric | 7d-branch open rate ≥6%, 14d ≥4%; returned-player D7 re-retention ≥15%; trial→theme purchase ≥5% of trial users (self-set) |
| Player-experience risk | Winback push is the classic annoyance. Mitigation: hard-final 14d message (kept promise), one ladder per lapse, gift-led not discount-led |

### 3.11 Existing payer suppression (P0)

| Field | Spec |
|---|---|
| Trigger | `entitlement_changed{change:granted}` for `all_access` or `supporter` (RC CustomerInfo is source of truth; cached offline per offline-first decision) |
| Player state | They paid. They are done being sold to |
| Screen | Global behavior change, not a screen |
| Offer | Permanently suppressed: 3.1, 3.5 card, 3.6 lock (unlocked), theme upsells, All Access cross-lines in every sheet. Still offered: rewind packs (3.4/3.7, genuinely useful), Supporter card in shop reframed for `all_access` owners |
| Exact copy | Supporter card for All Access owners: "You already own everything in this pack except the Founder extras. Supporter Pack is our tip jar — only buy it as a thank-you. Includes Founder livery, a named cat cameo, and the supporter badge." CTA: "Support the depot — {localized_price}" |
| Visual treatment | Owned items show "Owned ✓" in shop (visible, not hidden); gold conductor badge renders on profile/results |
| Dismissal | n/a |
| Frequency cap | `payer_thanks` push (copy rows A/B) sent ONCE via one-off scheduled send within 24h of first purchase; no further lifecycle selling |
| Cooldown | n/a |
| Eligibility | Any active premium entitlement |
| Suppression | This IS the suppression spec; enforced in one place — an `OfferEligibilityService` in CatMetro.Application (single choke point, unit-tested) |
| RC offering/placement | Placements still resolve (RC-side targeting can vary offering for payers if Pro-gated Targeting available; else client-side gate — brief: Targeting Pro-gated, verify plan) |
| Analytics | `entitlement_changed`, `paywall_viewed` MUST NOT fire for suppressed surfaces (QA asserts); OneSignal tag `payer_status` written by RC integration (locked) |
| Success metric | Zero suppressed-surface impressions for payers in analytics (hard gate); supporter attach among All Access owners ≥3% (tip-jar honesty means low is fine) |
| Player-experience risk | Over-suppression could hide restore/manage paths. Mitigation: shop, restore, and Customer Center always reachable |

### 3.12 Failed purchase (P0)

| Field | Spec |
|---|---|
| Trigger | `purchase_failed{user_cancelled}` or store error from RC purchase callback |
| Player state | Cancelled: changed their mind — respect it. Error: possibly worried they were charged |
| Screen | Cancelled: return silently to origin screen, zero UI. Error: inline toast on origin screen |
| Offer | NONE. No retry modal, no "wait! 10% off" — banned pattern |
| Exact copy | Error toast: "The store couldn't complete that — you were not charged." Pending (Play pending-purchase state): "Your purchase is pending with Google Play. It unlocks the moment it completes." Push (only if a non-cancel incomplete persists >24h, copy rows `purchase_issue` A/B): "Your purchase didn't complete / No charge went through…" |
| Visual treatment | Toast 4s, no modal; pending state shows a quiet "Pending" chip on the shop item |
| Dismissal | Auto |
| Frequency cap | purchase_issue push: max 1 per incident; never for user_cancelled |
| Cooldown | Cancelled purchase → that surface does not re-present itself this session |
| Eligibility | As trigger |
| Suppression | n/a |
| RC offering/placement | Same placement as origin; RC handles Billing 8 pending purchases — grant only on confirmed completion, reconciled against our ledger on resume (architecture lifecycle rule) |
| Analytics | `purchase_failed{error_domain, error_code, user_cancelled}` per error class incl. offline (taxonomy QA) |
| Success metric | Error-class purchase recovery within 48h ≥25%; zero double-grants on pending completion (ledger gate) |
| Player-experience risk | Payment anxiety. The explicit "you were not charged" line is the mitigation — factual reassurance, not sales recovery |

### 3.13 Restored purchase (P0)

| Field | Spec |
|---|---|
| Trigger | Player taps "Restore purchases" (shop footer, settings, and every paywall footer) — required path for reinstalls/new devices |
| Player state | Likely a payer on a new device, possibly frustrated their content is "missing" |
| Screen | Progress spinner → result state on the same screen |
| Offer | None |
| Exact copy | Success: "Restored: All Access ✓" (lists each entitlement restored). None found: "No purchases found on this Google account. Bought Cat Metro under a different account? Switch accounts in the Play Store app and restore again." Help link → Customer Center |
| Visual treatment | Result inline; restored items flip to "Owned ✓" immediately; suppression matrix (§3.11) applies same-frame |
| Dismissal | Standard |
| Frequency cap | None — restore is always free to run |
| Cooldown | None |
| Eligibility | Everyone |
| Suppression | n/a |
| RC offering/placement | `Purchases.RestorePurchases` → CustomerInfo refresh; RevenueCatUI Customer Center for self-serve issues (device-test heavily per brief crash caveat; custom help screen fallback) |
| Analytics | `restore_started`, `restore_completed{entitlements_restored_count}`, `entitlement_changed{change:granted, source:restore}`; fresh-reinstall QA per taxonomy |
| Success metric | Restore success rate ≥95% of attempts with a prior purchase on the account; restore-related 1★ reviews = 0 |
| Player-experience risk | Account-mismatch confusion is the top premium-game support ticket class. The "different account" copy pre-answers it |

### 3.14 Refund / revocation (P0)

| Field | Spec |
|---|---|
| Trigger | `entitlement_changed{change:revoked}` from RC CustomerInfo delta (RC dashboard refund test is the QA procedure per taxonomy) |
| Player state | Asked for money back — treat as a valid decision, part company gracefully |
| Screen | State change applies at next session boundary or next Home visit — NEVER mid-level. One quiet IAM afterward |
| Offer | None for 30 days: ALL system-initiated paywalls suppressed (3.1 if unfired, 3.5, cross-sells); shop and player-initiated surfaces (3.6 on tap, 3.2 on tap) remain, styled normally |
| Exact copy | IAM: "Your refund was processed. Your Night Harbor progress is saved if you ever come back." No CTA beyond "OK" |
| Visual treatment | Themes revert to default on next Home load; Night Harbor relocks with progress and stars retained (repurchase resumes exactly where they left off); badge removed without ceremony |
| Dismissal | Standard IAM |
| Frequency cap | 1 IAM per revocation event |
| Cooldown | 30-day system-paywall suppression window, then normal non-payer rules resume (except 3.1 which remains once-ever) |
| Eligibility | As trigger |
| Suppression | Consumed rewinds are NOT clawed back on rewind-pack refunds (goodwill; ledger annotates the refund so repeat refund-farming is visible — >2 refunded consumable purchases → rewind packs hidden for that account, free/rewarded rows unaffected) |
| RC offering/placement | RC webhooks recommended for consumable refund detection (brief); entitlement revocation is automatic via CustomerInfo |
| Analytics | `entitlement_changed{change:revoked, source:refund}`, `payer_status` user property downgraded, OneSignal tag updated (exits payer journeys) |
| Success metric | Refund rate <2% of transactions (no credible genre benchmark exists — self-set, re-baseline at D+30); zero mid-level content removals (hard gate) |
| Player-experience risk | Relocking content feels punitive. Mitigations: progress retained, no shaming copy, session-boundary timing |

**Decision:** Fourteen moments, five locked RC placements, one global eligibility service; system-initiated exposure is front-loaded into exactly one moment (3.1) and everything else is player-initiated, milestone-passive, or service.
**Evidence:** Brief-locked exposure list (post-L5 celebratory, theme preview, bonus lock, shop always, eligible-failure sheet, never after first failure); comp evidence that interruption is the #1 complaint (verified 2026-07-31); taxonomy CSV defines every event referenced.
**Action:** Build `OfferEligibilityService` + suppression matrix as Week-2 code with EditMode tests; wire all five placements against RC dashboard offerings by D14 SDK-spike gate; QA scripts from the per-moment analytics rows.
**Risk:** Paywalls v2 Android crash issues (#745/#736/#732) hit moment 3.1 — our single highest-traffic surface.
**Fallback:** Custom Unity paywall for `post_level_5` is specced in §4.1 as a text wireframe precisely so it can be built in a day; flag-switchable per brief.

---

## 4. Paywall copy — full text + wireframes

Anti-dark-pattern rules (apply to every surface, enforced in review checklist): no countdown timers of any kind (we run no time-limited sales at launch, so none can be real); nothing preselected; close/back always active from first frame at ≥48dp; decline buttons are neutral ("Keep playing free", "Maybe later", "Not now") — never self-deprecating; every price shown is the store-rendered localized price string; "best per rewind" is the only comparative claim and it is arithmetically true.

### 4.1 All Access paywall (placement `post_level_5`; variant for `bonus_district` in §3.6)

Text wireframe, top to bottom:
1. **Close** "✕" top-left, ≥48dp, visible immediately.
2. **Hero** (top 35%): Night Harbor diorama render — night-lit station, cats on the platform, sakura + neon theme swatches peeking at the edges. No text overlay on the hero.
3. **Headline** (H1, centered): `One ticket. Every line.`
4. **Sub-head** (1 line): `All Access is the complete Cat Metro — one purchase, yours forever.`
5. **Benefit list** (4 rows, icon + text):
   - 🌃 `Night Harbor — a bonus district of 10 handcrafted levels`
   - 🎨 `Both premium themes: Sakura Line + Neon Line`
   - ⏪ `Daily free rewind doubled — 2 every day`
   - 🥇 `Gold conductor badge on your profile`
6. **Price row**: `{localized_price} · one-time purchase` (US reference $6.99; string from store, §7).
7. **Primary CTA** (full-width): `Unlock All Access — {localized_price}`
8. **Secondary CTA** (text button, same tap-target height, directly below): `Keep playing free`
9. **Disclosure lines** (small, always visible without scrolling on a 16:9 phone):
   - `One-time payment. Not a subscription. No recurring charges.`
   - `Every level outside Night Harbor is free and fully solvable without paying.`
   - `Purchases restore on any device with your Google account.` → tappable → Restore
10. **Trust line** (footer, brand voice): `Fair by design: no forced ads, no energy, no loot boxes. Cat Metro has no forced ads today — All Access makes that a permanent promise.` (The former "Ad-free, guaranteed forever" benefit row is demoted here: it sold removal of ad surfaces the free game does not have.)

Behavior notes: this exact layout is replicated in RC Paywalls v2 template config AND as the custom Unity fallback prefab (crash contingency, §3 fallback). The $4.99 experiment variant changes ONLY the price row and CTA price token (offering `ofr_core_b`); copy is otherwise identical so the test isolates price.

### 4.2 Theme preview sheet (placement `theme_preview`)

Text wireframe (bottom sheet, ≤45% height; live board re-skin behind it):
1. **Grabber** + swatch toggle: `[● Sakura Line] [○ Neon Line]` — toggling re-skins the live board behind the sheet.
2. **Headline**: `Ride the {theme_name}` (e.g., "Ride the Sakura Line").
3. **Body** (1 line): `The whole board repaints — stations, trains, sky. You're previewing it right now, behind this card.`
4. **Action rows** (equal visual weight):
   - `Unlock {theme_name} — {localized_price}` (US ref $2.99)
   - `▶ Try it free for 3 levels` (rewarded ad; row hidden if `ads_enabled` off, daily rental used, or no fill)
5. **Cross-line** (small text): `Both themes are included in All Access — {aa_localized_price}` → taps through to §4.1 view (counts as player-initiated).
6. **Dismiss**: swipe-down / `Maybe later` text button.
Owned state: action rows replaced by `Owned ✓ — Equip`.

### 4.3 Rewind sheet (placement `rewind_failure`)

Text wireframe (bottom sheet over FailureReview; opens only from the ⏪ chip, attempt ≥2):
1. **Headline**: `Rewind to your last safe switch?`
2. **Context line**: `Back to just before the {color} jam — your earlier moves stay made.`
3. **Option rows**, strict order, identical styling:
   - `Use today's free rewind (1 left)` — All Access owners see `(2 left)`
   - `Use a rewind — {owned_count} owned` (hidden at 0)
   - `▶ Watch an ad for a rewind ({n} left today)` (caps: 2/session, 5/day; hidden when capped/no-fill/`ads_enabled` off)
4. **Divider**: `Need more?`
5. **Pack rows** (visually secondary, smaller):
   - `5 rewinds — {localized_price}` (US ref $1.99)
   - `20 rewinds — {localized_price} · best per rewind` (US ref $4.99)
6. **Footer fairness line** (always visible): `Every level is solvable without rewinds — this just saves the redo.`
7. **Dismiss**: `No thanks, retry from start` / swipe-down / back.
State variants per §3.8 (caps hit → ad row hidden + explainer line; post-purchase → pack rows suppressed 24h).

### 4.4 Supporter Pack card (shop only, bottom position)

Text wireframe (card in shop scroll, never a modal):
1. **Card art**: Founder livery train on a golden track.
2. **Headline**: `Supporter Pack`
3. **Body**: `Our tip jar, with perks. Everything in All Access, plus the Founder livery, a cat named by you riding every train, and the supporter badge.`
4. **Honesty line** (always shown): `If you just want the content, All Access is the better buy. This one's for people who want to keep the depot lights on.`
5. **CTA**: `Support the depot — {localized_price}` (US ref $9.99).
For `all_access` owners the body/honesty lines swap to the reframed copy in §3.11 (explicit overlap disclosure).
Name-a-cat flow post-purchase: local text field, 12-char limit, profanity filter, stored locally (no server, per offline-first).

**Decision:** Copy above is final draft v1 for build; RC Paywalls v2 carries §4.1, custom UI carries 4.2–4.4.
**Evidence:** Brief-locked catalog/prices/exposure rules; comp review evidence that trust language differentiates (Mini Metro's fairness halo, Arrows' backlash); notification copy voice already established in `notification_copy.csv` (honest, transit-flavored, zero-pressure).
**Action:** Load §4.1 into RC dashboard paywall template + build fallback prefab by D14; string table keys `pw_*` added to the CSV-driven localization table (EN-only launch, structure ready per architecture).
**Risk:** Copy length overflows small screens (720p floor) pushing disclosures below the fold — a fairness fail.
**Fallback:** Disclosure block is layout-priority: benefits list collapses to 4 rows (badge line drops) before disclosures ever scroll; verified on the low-tier device in the test matrix.

---

## 5. Subscription decision — formal record

**Status: REJECTED** for launch and for the entire event window (locked in brief; recorded formally here).

Reasons, in order of weight:
1. **No honest recurring value.** A subscription sells a promise of ongoing delivery. A solo dev shipping a 1.0 on Aug 24–28 cannot honestly sustain a weekly/monthly content cadence during the window — the brief's own content plan is 30 launch levels + a seeded Daily Line + one weekly mini-event from Week 5. Selling a subscription against that pipeline would be the exact "unfair" pattern this game brands against.
2. **Genre fit.** One-time premium converts better for calm puzzle games: the verified poles (Mini Metro $0.99 one-time, 4.63★; Railbound $4.99 one-time; Neko Atsume ≤$3.49 one-shots at 4.78★) are all non-recurring. No comp in our verified set succeeds on subscription.
3. **Operational cost.** Subscriptions add Play subscription declarations, grace/hold/pause states, cancellation flows, churn messaging, and proration edge cases — weeks of solo-dev effort that produce zero content and compete with the launch-critical path.
4. **Trust positioning.** "Fair by design" + auto-renewing charge is a contradiction judges and reviewers will notice; a $6.99 complete edition is the credibility play.
5. **Award alignment.** HAMM rewards "thoughtful pricing and packaging" — a deliberate, documented subscription REJECTION with this reasoning is itself packaging thoughtfulness (§6 uses it).

**Revisit condition (post-event only):** if the weekly District Cup mini-event ships ≥8 consecutive weeks with stable participation AND D30 cohort retention holds ≥1.5× the GameAnalytics 2025 median (~0.7%, verified vintage-labeled), a "Metro Pass" may be evaluated — as an addition, never replacing the one-time catalog, and never gating existing content.

**Decision:** No subscription SKU, no subscription code paths, no "sub-ready" scaffolding at launch.
**Evidence:** Brief §MONETIZATION item 7 + subscription decision paragraph (locked); verified comp pricing above; 8-week schedule spine leaves no cadence capacity.
**Action:** Remove subscription from all screens/copy/Play Console setup; record this section as the canonical answer to "why no sub?" for Devpost and #BuildInPublic posts.
**Risk:** Leaving recurring revenue on the table lowers window revenue vs hybrid-sub competitors chasing the Grand Prize shortlist.
**Fallback:** None needed at launch; the revisit condition above is the only path back, and it opens after the event window closes.

---

## 6. HAMM award narrative

HAMM criteria (verified 2026-07-31): "smartest use of RevenueCat to drive real revenue… well-crafted paywall, thoughtful pricing and packaging, strong conversion." Our submission maps one-to-one:

**Well-crafted paywall.**
- RC Paywalls v2 renders the flagship `post_level_5` paywall (§4.1) — remote-editable copy/layout without app updates, device-tested against the three known Android crash issues, with a same-layout custom fallback wired behind a flag (we show judges the risk management, not just the happy path).
- The paywall is deliberately anti-dark-pattern (no countdowns, no preselects, equal-weight decline) and we say so on the paywall itself — the trust line converts fairness into a selling point.
- The rewind sheet demonstrates paywall craft in the hard case: monetizing failure WITHOUT monetizing frustration (free options first, attempt-1 embargo, solvable-free footer).

**Thoughtful pricing and packaging.**
- A clean five-point ladder: $1.99 → $2.99 → $4.99 → $6.99 → $9.99, each price a different JOB (small consumable / cosmetic / large consumable / complete edition / tip jar). The theme bundle was cut on decoy-confusion grounds — All Access IS the bundle; that cut is part of the story.
- A documented price DECISION with reasoning: All Access raised $4.99→$6.99 — raised because (a) downside is bounded (~$40 net base-case) with a 28.6% conversion-loss cushion, (b) $4.99 breaks the ladder — the everything-tier would price below its own two themes ($5.98) and tie cm_rewind_20, the decoy-confusion grounds on which the theme bundle was cut, and (c) the verified $7.26 casual D90 ARPPU shape supports a ~$7 completion price. The Grand-shortlist revenue argument is immaterial at our scale. Then stress-tested directionally (pre-registered as non-significant at our scale) via experiment (below).
- A formal, reasoned subscription rejection (§5) shows packaging discipline: knowing what NOT to sell.
- Supporter Pack packaging honesty (overlap disclosure to existing owners, §3.11) as an example of packaging that respects the customer.

**Strong conversion (the catalog → placements → experiments arc).**
- **Catalog:** 6 live SKUs, 4 entitlements, consumable ledger with RC webhooks — the full product-type spread on one small game.
- **Placements:** all five RC Placements (`post_level_5`, `theme_preview`, `bonus_district`, `shop`, `rewind_failure`) resolve offerings server-side, so every surface is remotely retargetable; per-placement funnels instrumented via the taxonomy (`paywall_viewed → purchase_started → purchase_completed` with placement + offering_id on every event).
- **Experiments:** the $6.99 vs $4.99 All Access price test runs via RC Experiments if the project plan allows (Pro/Enterprise-gated — verified; plan check is a D1 task), else the pre-declared fallback: sequential offering swaps through Placements with cohort-split readouts by install week. Either way, judges see a hypothesis → test → decision loop inside the window. Scale caveat (pre-registered): at base-case traffic PW01 sees ~300–490 paywall views per arm against the ~7,700 needed for significance — roughly 6 vs 8 purchases per arm — so the readout is directional, decided on summed arm revenue, and never claimed as significant.
- **Evidence pack for judging (judges may judge from text/images/video alone — verified):** RC dashboard screenshots of offerings/placements/paywall config, the funnel numbers per placement, the experiment readout, and this spec's §3 map as the design document. Target headline numbers: view→purchase ≥1.5% on `post_level_5`, ≥6% on `bonus_district`, payer rate benchmarked against the 2025 grand-winner calibration (1,750 payers/17k users ≈ 10% — verified) as aspiration, with honest self-set targets labeled as such.

**Decision:** The HAMM entry is written as "the fair-by-design revenue machine": full RC surface area (Offerings, Packages, Entitlements, Placements, Paywalls v2, Customer Center, AdTracker, Experiments-or-fallback) on a game whose monetization users publicly don't hate.
**Evidence:** HAMM criteria text (verified 2026-07-31); RC Unity capability verification in brief (Placements 6.9.0+, Paywalls v2 via RevenueCatUI, Experiments plan-gated); 2025 calibration data.
**Action:** D1 — verify RC project plan for Experiments/Targeting; Week 2 — placements live in dashboard; Sep — run price experiment ≥14 days; Sep 26–30 — assemble evidence pack into Devpost.
**Risk:** Experiments plan gating discovered late kills the A/B story mid-window.
**Fallback:** Pre-declared sequential-offering test via Placements (already specced) — weaker statistically, still a complete hypothesis→decision narrative; the write-up discloses the method honestly.

---

## 7. Price localization

Approach: **store-templated prices, locally round numbers, zero hard-coded strings.**

1. **Play Console pricing templates.** One template per tier, created D1 alongside product setup: `tpl_cm_199`, `tpl_cm_299`, `tpl_cm_499`, `tpl_cm_699`, `tpl_cm_999`. Each product links to its tier template so a price change is one edit, not six, and the $4.99 experiment SKU (`cm_all_access_499`) simply links `tpl_cm_499`.
2. **Round local pricing.** Start from Play's per-market exchange conversion, then hand-adjust the top expected markets (US, IN, BR, MX, ID, PH, GB, DE, CA, AU) to local psychological round points (e.g., All Access lands at ₹575-class → set ₹549; R$ round to R$34.90-class; never ship a converted price like ₹581.43). Play's tax-inclusive display rules apply automatically per market; templates store the tax-inclusive consumer price where required.
3. **No deep launch discounts in low-ARPU markets** during the event window — the Grand Prize shortlist keys on total reported revenue (verified), so regional price experiments are a post-window activity; note this explicitly in the experiment backlog.
4. **Client renders store truth only.** Unity UI always displays `StoreProduct.PriceString` from the RevenueCat offering (which carries Play's localized, currency-formatted price). The literal strings "$6.99" etc. appear nowhere in the app binary or paywall templates — every price token in §4 is `{localized_price}`. RC Paywalls v2 variables handle this natively; custom UI reads the same StoreProduct.
5. **Analytics without PII or currency noise.** Events log `price_local_bucket` (from the taxonomy) — USD-normalized bucket via RC's price + currency fields — never raw local price strings.
6. **Consistency check in CI:** a smoke test fails the build if any `pw_*`/shop string contains a currency symbol or a digit-dot-digit price pattern (regex gate), enforcing rule 4 mechanically.

**Decision:** Five Play pricing templates + hand-rounded top-10 markets + RC-rendered price strings everywhere; no in-binary prices.
**Evidence:** Brief locks US prices and the 15% effective Play fee for models (verified Jun 30 2026 fee structure); Play promo codes verified working for one-time products (judge access unaffected by pricing setup); RC Offerings verified as our only remote-config surface (architecture decision).
**Action:** D1–D2 create templates + link SKUs during Play Console product setup; add the CI regex gate with the first shop UI PR; review top-10 market rounding before the D24–26 production submission.
**Risk:** Hand-rounding across markets drifts tier relationships (a rounded ₹549 All Access vs rounded ₹149 rewind pack can distort the ladder's perceived gaps).
**Fallback:** If drift is detected in any market, revert that market to Play's auto-converted template price — correctness of the ladder beats prettiness of any single price point.

---

## 8. Amendment draft — deep catalog, DLC districts, rewarded expansion, and Experiments

- **Drafted:** 2026-08-09
- **Status:** Direction and the exact product identifiers, prices, inventory, caps, and packaging
below were **HUMAN-SIGNED 2026-08-09** at §8.10. That signature approves candidate commercial values
but is deliberately **non-executable**: the v1 catalog and five rewarded placements above
remain authoritative until one later coordinated supersession commit satisfies §8.7 after the
human-authored production-mode tripwire.
- **Frozen contract:** `docs/prd/leaderboards-contract.md`
- **Award intent:** deepen the HAMM evidence from one flagship paywall into an honest
catalog → placement → experiment loop while keeping the Catvertising promise literal.

> **Production tripwire — no exception:** before any billing, IAP, ad, payment, catalog adapter,
> purchase UI, reward-grant, or equivalent monetization code merges anywhere, a human-authored commit
> must change `state/mode` to `production`. This documentation amendment does not flip, satisfy, or
> bypass that gate.

### 8.1 Fair-core law survives the expansion

The accepted direction changes catalog depth, not the game's bargain with the player:

1. The 30-level campaign, Daily Line, District Cup, progression, every mechanic, and every required
   route remain free. Paid districts are optional side content, never a bridge between free districts.
2. There is no energy, loot box, randomized paid reward, subscription, premium currency, ticket pack,
   paid streak saver, forced ad, banner, interstitial, app-open ad, or paid gameplay stat.
3. Every durable item is a one-time, permanently restorable non-consumable. “Seasonal” describes its
   art and merchandising window, not ownership expiry; an owned seasonal item remains usable forever.
4. Paid cat skins cannot alter the destination color, symbol tag, silhouette class, hitbox, animation
   timing, queue footprint, or any other gameplay channel. Paid liveries/themes cannot change track,
   signal, queue, switch, hazard, or preview legibility. `product_spec.md` §7's art/readability rules
   and §17's collection contract remain authoritative; color is always paired with symbol and
   silhouette under every cosmetic.
5. Marmalade, Slate Night, Mint Line, star-milestone badges, streak badges/liveries, every District Cup
   participation/gold-trim livery, and the Founder livery keep their existing earned/exclusive sources.
   A paid look may share a mood; it may not duplicate or replace an earned reward.
6. Purchases, trials, and ad watches never improve, confer, or restore simulation score, Gold
   eligibility, or global leaderboard eligibility/rank, and never modify scoring rules, generator
   seeds, route/content difficulty, or matchmaking. Global boards are rewardless and accept only
   zero-rewind runs per the frozen leaderboard contract; rewind use may reduce eligibility as below.
7. The named existing local exceptions remain literal and bounded: any free, rewarded, or purchased
   rewind may support a local Cup completion but caps that run at Silver and still permits the existing
   participation livery; `double_tickets` and `daily_gift_double` change only their named local economy
   rewards. No cosmetic or district trial changes a score, medal, progression reward, or unlock.
8. A purchase may only reduce commerce/ad pressure through owned-item hiding, payer suppression, or
   an ad-removal promise; it never increases offer/ad eligibility, frequency, urgency, or targeting.
   System-initiated commerce stays capped at one surface per session and is suppressed while **any**
   purchase grant or durable successful-purchase history that has not been fully refunded/revoked
   exists; exhausting a consumable never reclassifies its buyer as a prospect. After every source is
   refunded/revoked, the existing 30-day transition applies and `post_level_5` never rearms. Owners
   browse unowned items only through player-initiated Shop/preview taps.

### 8.2 Human-signed candidate catalog — not activated

Every identifier and US reference price in the following tables is a proposal, not permission to
create a Play Console or RevenueCat object. The client always renders Play's localized store price,
never the literals below. Product IDs are lowercase, permanent, and never recycled once created.

#### Carry-forward and playable-content products

| Candidate product ID — **HUMAN-SIGNED 2026-08-09** | Type | Candidate US reference — **HUMAN-SIGNED 2026-08-09** | Durable grant | Packaging ruling |
|---|---|---|---|---|
| `cm_all_access` — **HUMAN-SIGNED 2026-08-09** | non-consumable | **$6.99 — HUMAN-SIGNED 2026-08-09** | `all_access`; every signed `district_<slug>`; `theme_sakura`; `theme_neon` | Every paid **playable district**, current and future; Sakura + Neon; doubled daily free rewind; gold badge; existing ad-removal promise. It does not silently include later standalone cosmetics. Each new district entitlement is dashboard-attached to this product before that district ships. |
| `cm_supporter_pack` — **HUMAN-SIGNED 2026-08-09** | non-consumable | **$9.99 — HUMAN-SIGNED 2026-08-09** | `supporter` plus every grant attached to `cm_all_access` | Existing Founder extras + All Access. Shop-only tip jar; no new gameplay value. |
| `cm_district_night_harbor` — **HUMAN-SIGNED 2026-08-09** | non-consumable | **$2.99 — HUMAN-SIGNED 2026-08-09** | `district_night_harbor` | Night Harbor L901–L910 à la carte; RevenueCat also attaches this same entitlement to every All Access product. Existing progress survives revocation/restore. |
| `cm_district_<signed_slug>` pattern — **HUMAN-SIGNED 2026-08-09** | non-consumable | **$2.99 per 10-level pack — HUMAN-SIGNED 2026-08-09** | `district_<signed_slug>` | Template for a separately specified optional district. This string is not a creatable placeholder SKU; each concrete slug/content contract returns for its own human signature. |
| `cm_rewind_5` — **HUMAN-SIGNED 2026-08-09** | consumable | **$1.99 — HUMAN-SIGNED 2026-08-09** | existing local ledger +5 | Carry-forward convenience; never required and globally rank-ineligible if used. |
| `cm_rewind_20` — **HUMAN-SIGNED 2026-08-09** | consumable | **$4.99 — HUMAN-SIGNED 2026-08-09** | existing local ledger +20 | Carry-forward convenience; never required and globally rank-ineligible if used. |
| `cm_all_access_499` — **HUMAN-SIGNED 2026-08-09** | inactive experiment-only non-consumable | **$4.99 — HUMAN-SIGNED 2026-08-09** | exact same entitlement attachments as `cm_all_access` | Identical grant to `cm_all_access`; absent from Shop except while signed experiment PW01 is live. |

“All Access” now means **every playable line**, not every decorative item. Before this amendment can
activate, §4.1 copy must replace “the complete Cat Metro” with “every playable line in Cat Metro” and
must name Sakura + Neon as the included cosmetic pair. That copy correction prevents a later cosmetic
release from making an earlier purchase claim false.

#### Cat-skin wave 1

Each skin is a material/accessory overlay on the existing destination-readable cat. A skin can add
clothing, texture, and secondary motion; it cannot cover the line symbol or replace the silhouette.

| Candidate product ID — **HUMAN-SIGNED 2026-08-09** | Candidate display name | Candidate US reference — **HUMAN-SIGNED 2026-08-09** | Durable grant |
|---|---|---|---|
| `cm_cat_skin_raincoat` — **HUMAN-SIGNED 2026-08-09** | Rainy-Day Rider | **$0.99 — HUMAN-SIGNED 2026-08-09** | `cat_skin_raincoat` |
| `cm_cat_skin_stationmaster` — **HUMAN-SIGNED 2026-08-09** | Stationmaster | **$0.99 — HUMAN-SIGNED 2026-08-09** | `cat_skin_stationmaster` |
| `cm_cat_skin_sailor` — **HUMAN-SIGNED 2026-08-09** | Harbor Sailor | **$0.99 — HUMAN-SIGNED 2026-08-09** | `cat_skin_sailor` |
| `cm_cat_skin_gardener` — **HUMAN-SIGNED 2026-08-09** | Garden Helper | **$0.99 — HUMAN-SIGNED 2026-08-09** | `cat_skin_gardener` |
| `cm_cat_skin_night_shift` — **HUMAN-SIGNED 2026-08-09** | Night-Shift Knit | **$0.99 — HUMAN-SIGNED 2026-08-09** | `cat_skin_night_shift` |
| `cm_cat_skin_festival` — **HUMAN-SIGNED 2026-08-09** | Festival Bells | **$0.99 — HUMAN-SIGNED 2026-08-09** | `cat_skin_festival` |

#### Train-livery wave 1

These are standalone train paint/decal treatments. They do not include the existing Founder or
earned Cup liveries and cannot imitate their badges or trim.

| Candidate product ID — **HUMAN-SIGNED 2026-08-09** | Candidate display name | Candidate US reference — **HUMAN-SIGNED 2026-08-09** | Durable grant |
|---|---|---|---|
| `cm_livery_brass_line` — **HUMAN-SIGNED 2026-08-09** | Brass Line | **$1.99 — HUMAN-SIGNED 2026-08-09** | `livery_brass_line` |
| `cm_livery_harbor_fog` — **HUMAN-SIGNED 2026-08-09** | Harbor Fog | **$1.99 — HUMAN-SIGNED 2026-08-09** | `livery_harbor_fog` |
| `cm_livery_garden_party` — **HUMAN-SIGNED 2026-08-09** | Garden Party | **$1.99 — HUMAN-SIGNED 2026-08-09** | `livery_garden_party` |
| `cm_livery_midnight_express` — **HUMAN-SIGNED 2026-08-09** | Midnight Express | **$1.99 — HUMAN-SIGNED 2026-08-09** | `livery_midnight_express` |

#### Permanent seasonal-theme wave

Sakura and Neon carry forward. Harvest and Snowbell are proposed additions; neither is promised for a
date until a separate art/content contract passes. Seasonal shop featuring may end, but ownership and
use do not.

| Candidate product ID — **HUMAN-SIGNED 2026-08-09** | Candidate display name | Candidate US reference — **HUMAN-SIGNED 2026-08-09** | Durable grant |
|---|---|---|---|
| `cm_theme_sakura` — **HUMAN-SIGNED 2026-08-09** | Sakura Line | **$2.99 — HUMAN-SIGNED 2026-08-09** | `theme_sakura`; included in `all_access` |
| `cm_theme_neon` — **HUMAN-SIGNED 2026-08-09** | Neon Line | **$2.99 — HUMAN-SIGNED 2026-08-09** | `theme_neon`; included in `all_access` |
| `cm_theme_harvest` — **HUMAN-SIGNED 2026-08-09** | Harvest Line | **$2.99 — HUMAN-SIGNED 2026-08-09** | `theme_harvest` |
| `cm_theme_snowbell` — **HUMAN-SIGNED 2026-08-09** | Snowbell Line | **$2.99 — HUMAN-SIGNED 2026-08-09** | `theme_snowbell` |

There is no random “mystery skin,” premium-currency price, rotating ownership deadline, starter pack,
cosmetic club, theme bundle, season pass, or bulk bundle. A curated visual collection may be an RC
Offering, but every contained product keeps its own exact store price and purchase button.

The six `$0.99` candidates require a new Play pricing template
`tpl_cm_099` — **HUMAN-SIGNED 2026-08-09**. It follows §7's localization/store-truth rules exactly;
its ID and every linked regional price remain non-executable until the coordinated supersession. The
existing `$1.99/$2.99/$4.99/$6.99/$9.99` templates remain candidates for their matching rows.

### 8.3 RevenueCat catalog and commerce-placement map

RevenueCat CustomerInfo remains the durable access truth; a local cache supports offline use and
Restore. The expanded dashboard shape is:

| Commerce placement | Default offering | Contents | Trigger law |
|---|---|---|---|
| existing `post_level_5` | amended `ofr_core` | All Access primary + visible Night Harbor-only option because the hero names that district | once/install, system-initiated; both localized prices visible without scrolling |
| existing `bonus_district` | `ofr_districts` | Night Harbor à la carte + All Access comparison | player taps a locked district |
| existing `theme_preview` | `ofr_themes` | selected theme + other permanent themes + honest All Access inclusion line | player taps an unowned theme |
| new `cat_preview` | `ofr_cat_skins` | selected cat skin first, then the wave | player taps an unowned skin |
| new `livery_preview` | `ofr_liveries` | selected train livery first, then the wave | player taps an unowned livery |
| existing `shop` | `ofr_shop` plus the signed catalog manifest | every active item, grouped by playable content / cats / trains / themes / rewinds / Supporter | player opens Shop |
| existing `rewind_failure` | existing `ofr_rewind` | rewind packs only | unchanged attempt-2+ player tap |

The app resolves the current Offering for the exact Placement and handles “No Offering” as no UI.
Dashboard ordering may change merchandising, never entitlements or eligibility. A signed catalog
manifest is the allowlist: a dashboard product absent from that manifest cannot render or grant.
Likewise, a manifest item missing from the returned Offering shows “Unavailable,” not a hard-coded
fallback price or an alternate product.

The client performs **one entitlement check per feature**, preserving CM-R24. For district
`<slug>`, it checks only `district_<slug>`. RevenueCat attaches that entitlement to both the matching
à-la-carte product and every All Access product (`cm_all_access`, `cm_all_access_499`, and
`cm_supporter_pack`); the client never evaluates `all_access || district_<slug>`. It likewise checks
only the exact item entitlement for a cosmetic. Ownership is never inferred from display name,
package position, season, or another entitlement.

#### Ownership, offer, restore, and refund matrix

| Observed store/CustomerInfo state | Feature access | System commerce | Player-initiated presentation and required copy |
|---|---|---|---|
| no purchase | exact active entitlements only | existing once/install `post_level_5` may qualify | Every Night Harbor-led sheet, including `post_level_5`, shows both **“Night Harbor only — `{localized_price}`”** and **“All playable lines — `{localized_price}`”** without scrolling |
| owns one district only | its single `district_<slug>` remains active | all system paywalls/lifecycle selling suppressed as payer | Shop and unowned previews remain available; All Access comparison says **“You already own {district}. All Access does not credit or prorate earlier purchases.”** before confirmation |
| owns one cosmetic/theme only | that exact item remains active | all system paywalls/lifecycle selling suppressed as payer | owned item reads `Owned`; no trial/ad row for it; other Shop/preview surfaces are player-initiated only |
| bought a consumable only, including after its balance reaches zero | durable ledger remains purchase history; only the purchased quantity is spendable | `post_level_5` never arms and lifecycle/system selling is suppressed as payer | rewind rows and every unowned item remain reachable only after a player opens the relevant sheet/Shop; no “you are out” interrupt |
| owns All Access | every dashboard-attached district + Sakura + Neon | all system paywalls and district/theme cross-sells suppressed | Shop may show standalone cat/livery/Harvest/Snowbell and rewinds only after a player opens it; no “complete collection” claim |
| owns Supporter | Supporter + every All Access attachment | same suppression as All Access | Founder extras read `Owned`; standalone cosmetics remain honest separate purchases |
| owns both à-la-carte district/theme and an umbrella product | the shared feature entitlement remains active while either qualifying product remains valid | payer suppression remains | before the umbrella purchase, show owned-component/no-credit copy; never imply a refund or prorated price |
| one overlapping product is refunded/revoked | shared entitlement stays active from the other valid product | payer suppression remains if any valid purchase/grant remains | no relock toast and no progress mutation; CustomerInfo source set is re-resolved, not inferred locally |
| every product granting the feature is refunded/revoked | entitlement becomes inactive at the existing next-Home/session boundary | existing 30-day refund suppression applies | paid content relocks, cosmetics revert, and all progress/equipment history is retained for later repurchase/Restore |
| reinstall/Restore | CustomerInfo reconstructs every active attachment, including umbrella overlaps | suppression applies in the same frame | result lists exact restored feature names once; consumable rewind balances are not called restorable |

The same overlap matrix applies when a future district entitlement is attached to an already-sold All
Access product. Before that district activates, sandbox/device evidence must prove an existing owner
receives the new `district_<slug>` attachment after CustomerInfo refresh/Restore and that refunding
either overlapping product cannot revoke the other source.

#### Judge-access promo matrix — **HUMAN-SIGNED 2026-08-09**

No hidden universal entitlement or bargain-price master SKU is introduced. Instead, the candidate
judge mechanism is a private packet of **15 Play one-time-product promo codes per recipient —
HUMAN-SIGNED 2026-08-09**:

- one `cm_supporter_pack` code (which covers All Access, every signed district, Sakura, Neon, and
  Founder extras);
- one code for each of the six cat-skin SKUs and four livery SKUs;
- one each for `cm_theme_harvest` and `cm_theme_snowbell`; and
- one each for `cm_rewind_5` and `cm_rewind_20`, subject to Play Console proof that promo redemption
  supports those consumable one-time products and grants each exactly once.

The candidate issue count is **25 private packets / 375 codes — HUMAN-SIGNED 2026-08-09; Play
Console quota/expiry verification still required** (15 judge, five press, five spare). If quota or consumable promo
behavior cannot be proven, the matrix returns for human re-scope; the submission must not claim full
premium access. Codes and redemption URLs live only in the human-controlled secret store and the
judge-only Devpost field—never in this repository, screenshots, logs, analytics, or build-in-public
material. A clean Play-distributed device test redeems one complete packet, proves every durable
entitlement and both consumable grants, and records only redacted evidence. This proposal explicitly
replaces CM-R31's obsolete “one `cm_all_access` code unlocks everything” and repo-local
`ops/judge_codes.md` storage when the coordinated supersession activates.

### 8.4 Expanded opt-in rewarded placements

The five v1 placements stay intact. Three cosmetic/content trials deepen the opt-in ladder without
creating power. Exact new limits below are **HUMAN-SIGNED 2026-08-09** and do not amend ADR-0006's
closed five-counter save schema by themselves.

| AdTracker placement | Status | Reward | Per-placement cap | Absence / expiry law |
|---|---|---|---|---|
| `rewind_failure` | existing | one rewind at eligible attempt-2+ failure | existing 2/session, 5/day | unchanged; the resulting run is globally rank-ineligible |
| `double_tickets` | existing | existing ticket double | existing 3/day | never changes score or unlock requirement |
| `daily_gift_double` | existing | existing gift double | existing 1/day | never required for streak |
| `streak_saver` | existing | existing free/rewarded streak repair | existing 1/day | never sold for money |
| `theme_rental` | existing | selected paid theme for 3 completed levels | existing 1/theme/day | silent revert + at most one passive toast/day |
| `cat_skin_trial` | **new** | selected paid cat skin for 3 completed campaign/practice levels | **1 total earned skin lease per local date across all skins — HUMAN-SIGNED 2026-08-09** | no Daily/Cup/share-card/global-rank use; silent revert |
| `livery_trial` | **new** | selected paid livery for 3 completed campaign/practice levels | **1 total earned livery lease per local date across all liveries — HUMAN-SIGNED 2026-08-09** | no Daily/Cup/share-card/global-rank use; silent revert |
| `district_guest_route` | **new** | one designated practice-only showcase route in a locked paid district | **1 district/day and 1/session — HUMAN-SIGNED 2026-08-09** | zero tickets, stars, progress, medal, Daily/Cup result, or global rank; full retry always free |

The new rows appear only inside a player-opened preview. There is no automatic “watch now” modal, ad
wall, timer, pulsing badge, failure substitution, or ad prompt before L7. Three consecutive declines
still mute every ad row for 24 hours. Trial expiry never interrupts a level.

The cap/state proposal is exact enough to review and was **HUMAN-SIGNED 2026-08-09**:

1. Daily state is keyed by local `dateKey`. Exact scalar keys are `rewind_failure`, `double_tickets`,
   `daily_gift_double`, `streak_saver`, `cat_skin_trial`, `livery_trial`, and
   `district_guest_route`; each new trial key counts 0/1 across every selectable item in its class.
   Existing per-theme law is represented separately as bounded
   `themeRentalByItem[signedThemeId]=0|1`, replacing the inadequate scalar v1 `theme_rental` counter
   during migration. Unknown item IDs fail closed. Local-date rollback protection follows the
   existing durable cap law.
2. A session is the existing CM-R43.6 session: foreground after a background gap of at least 30
   minutes creates a new durable session ID; shorter task-kill/relaunch gaps retain it. Durable
   session counters cover existing `rewind_failure` and new `district_guest_route`, preventing a
   task-kill cap reset.
3. Decline consumes no daily/session cap and grants nothing; it advances the existing consecutive-
   decline mute state. A load/no-fill failure consumes that placement's applicable daily and session
   slot exactly as the locked ADR-0006 law requires, hides the row for the session, grants nothing,
   and never substitutes a purchase prompt.
4. A completed ad consumes the cap and creates its reward exactly once under a durable callback-dedupe
   key; duplicate/late callbacks are no-ops. Cap consumption and reward/lease creation commit in one
   save transaction. Kill before that commit grants nothing and leaves no half-created lease; kill
   after it restores the same lease once. Offline cannot start a new watch but does not revoke an
   already committed lease.
5. Active theme/cat/livery leases are keyed by class and store exact signed `itemId`, `remaining=3`,
   grant `dateKey`, and dedupe key. Only completed campaign or explicitly marked local-practice levels
   decrement them. Daily, Cup, failed, abandoned, and replayed completions do not. Date rollover does
   not erase an earned lease; it expires only after its third eligible completion, then reverts at the
   next safe Home/result boundary. A class with an active lease shows no second trial row, so a later
   watch can never replace or stack it.
6. `district_guest_route` stores exact signed district/route IDs, grant `dateKey`, session ID, dedupe
   key, and state. It permits unlimited retries of that one practice route until the first successful
   completion or local-date rollover, whichever comes first; task death during an attempt returns to
   an available retry. It never writes campaign/Daily/Cup progress, stars, tickets, medals, rewards,
   share eligibility, or leaderboard evidence.

AdMob remains the locked provider. RevenueCat AdTracker receives the SDK callback sequence (loaded,
displayed, opened, impression-level revenue, and failed-to-load) with the exact placement above. The
game's existing completion callback remains the reward authority; an AdTracker revenue event is never
treated as proof that a reward was earned. RevenueCat's ad monetization API is currently beta and
experimental, so beta access, the exact pinned purchases-unity API, AdMob impression-level revenue,
event parity, and dashboard/chart arrival all remain on-device spike gates. RevenueCat's separate
server-verified ad-reward beta is not adopted by this amendment.

Implementation requires a human-approved ADR-0006 save-schema amendment for the exact seven scalar
daily keys, bounded per-theme map, two durable session counters/session identity, class-keyed active
leases, the guest-route lease, and callback dedupe; exact byte/count bounds; migration fixtures from
v1; and cap rollback/process-death tests. Until those land, the three new placements fail closed OFF
even if dashboard configuration exists.

### 8.5 RevenueCat Experiments plan

RevenueCat Experiments is based on Offerings, can vary price/product mix/paywall presentation, supports
placement-specific variants, and is currently a Pro/Enterprise feature. The human verifies the Cat
Metro project plan and the pinned Unity SDK behavior before enrollment. If unavailable, the declared
fallback is a sequential Offering swap by install-week cohort, explicitly labeled non-randomized.

Run only one experiment at a time for this traffic level. The candidate enrollment mode is **new and
existing eligible production users at activation**, using 2 variants for these tests; §8.10 does not
sign that enrollment scope. It requires a later human activation signature alongside the numeric stop
thresholds and sample/decision rule before launch, and the chosen RevenueCat setup is exported/
screenshot before launch. Assignment is sticky only per RC App User ID:
Cat Metro's anonymous IDs are not person-level, and reset/reinstall may create a new ID and assignment.
Reports and player copy make no cross-device/person-stickiness claim.

RevenueCat evaluates Experiment enrollment before Targeting, so neither a subscriber attribute nor an
owner Targeting rule is claimed to prevent assignment. Internal/license artifacts use disjoint signed
app-version values excluded by the Experiment's supported enrollment criteria, and production
experiments activate only after those QA runs. Any sandbox account or tested-grant owner that still
qualifies may be **assigned**; `OfferEligibilityService` prevents an ineligible surface from rendering,
so it remains not exposed and is excluded from the valid-exposure denominator. The non-PII
`cm_test_channel=internal|license|production` attribute is an analysis label, not an enrollment gate.
Sandbox proof must show variant assignment is possible while sandbox purchases stay absent from
production proceeds, and all assigned-but-not-exposed/test rows are separately labeled. If the exact
plan cannot enforce the app-version criterion or produce that audit, the experiment does not start.

For every custom surface (`bonus_district`, `theme_preview`, `cat_preview`, `livery_preview`, and
`shop`), the adapter makes **exactly one**
`Purchases.TrackCustomPaywallImpression(new Purchases.CustomPaywallImpressionParams(paywallId,
actualOffering))` call only after the actual Offering/paywall is successfully visible. This is the
documented Unity shape, but its exact namespace/signature is still compiled against pinned
purchases-unity 9.7.0 before implementation. Recompose,
background/foreground, failed render, cached placeholder, and duplicate callback produce no second
impression. RevenueCatUI's automatic impression and custom tracking are mutually exclusive. A parity
test compares app `paywall_viewed` counts with RC exposed-user counts by placement/variant and blocks a
test with missing or double impressions.

Each experiment runs at least 14 complete days and two weekends unless a pre-registered safety stop
fires. The readout always publishes enrolled users, valid exposed users, purchases, gross proceeds,
refunds, realized revenue per eligible viewer, excluded tester/sandbox counts, and
confidence/uncertainty; an underpowered result is called directional, never “a winner.”

| Order | Experiment | Placement | Control | Treatment | Primary question |
|---|---|---|---|---|---|
| 1 | `PW01` | `post_level_5` | `cm_all_access` at **$6.99 — HUMAN-SIGNED 2026-08-09** | identical grant via `cm_all_access_499` at **$4.99 — HUMAN-SIGNED 2026-08-09** | Does higher conversion at the lower price outweigh lower proceeds per purchase? Night Harbor-only remains the same visible secondary choice in both arms. |
| 2 | `DLC01` | `bonus_district` | All Access hero, Night Harbor à-la-carte secondary | Night Harbor à-la-carte hero, All Access comparison secondary | Which honest packaging best serves high-intent district taps without hiding either choice? |
| 3 | `CAT01` | `cat_preview` | selected skin only above the fold | selected skin + two fixed related skins, identical prices | Does a small curated choice improve purchase rate without choice overload? |
| 4 | `SHOP01` | `shop` | category grid | one signed seasonal collection first, same products/prices | Does visual curation improve durable-cosmetic revenue without increasing dismissals? |

Each test changes one named variable. It may not change close-button prominence, free-content copy,
first-failure embargo, system frequency, ad caps, reward size, rank eligibility, ownership duration,
refund/restore access, or any fair-core invariant. Ad-placement expansion is evaluated observationally
by placement-level opt-in, completion, no-fill, revenue, next-session retention, and decline-mute rates;
RC Experiments does not become permission to test more ad pressure.

Safety guardrails are purchase failures, refund/revocation rate, entitlement mismatch, paywall
dismissal, D1/D7 retention, support complaints, and the hard zero counts for suppressed payer surfaces
and attempt-1 commerce. Before activation, the human records numeric stop thresholds and the minimum
sample/decision rule in the dashboard notes or experiment ledger. A wrong price, wrong entitlement,
missing disclosure, or payer-suppression breach stops the arm immediately regardless of sample size.

### 8.6 Award evidence contract

This amendment targets two Shipaton award stories without changing product truth:

- **HAMM:** show the signed catalog/entitlement map, each Placement's Offering, the remote paywall,
  restored purchase, AdTracker placement revenue, and the pre-registered experiment plus honest
  result. “Strong conversion” is demonstrated with denominators and realized revenue, never a lone
  percentage or significance claim the sample cannot support.
- **Catvertising:** show a player deliberately selecting a rewarded trial, the immediate benefit, the
  visible daily cap, and a clean no-fill/decline path. “Ads only when the player asks” remains literal;
  banners, interstitials, app-open ads, and forced ads must have zero configured units and zero events.

Screenshots/video can be judged alone, so every dashboard capture must include the project/app name,
placement or experiment identifier, state/date, and variant labels. Revenue figures use RevenueCat for
near-real-time unified analysis but reconcile final ad revenue against AdMob because network
post-processing can differ. Test transactions and internal testers are visibly excluded, and the
judge packet in §8.3 proves every premium SKU class is reachable without publishing a secret.

### 8.7 Required downstream amendments before implementation

This docs-only lane intentionally leaves current executable contracts untouched. Signing §8.10 does
**not** supersede a clause or activate a dashboard object. Activation requires all of the following in
order:

1. a human-authored commit changes `state/mode` to `production` before any monetization code;
2. the human has signed every candidate value, cap, promo inventory, and experiment choice here;
3. content/art, privacy/consent/Data Safety, migrations, dashboard manifests, localized price
   templates, and test contracts are complete; and
4. one coordinated, reviewable supersession commit updates every authority row below together. That
   commit names an activation date/build and changes old assertions atomically; partial supersession
   is invalid and v1 remains authoritative.

| Current authority | Required replacement in the coordinated commit |
|---|---|
| this spec preamble/catalog, §§1, 3, 4, 6, and 7 | replace six-SKU/four-entitlement/five-placement/“complete edition”/five-tier claims; add deep catalog, one-entitlement district attachments, honest Night Harbor standalone copy, owner matrix, 8 rewarded rows, experiment instrumentation, judge packet, and signed `tpl_cm_099` |
| PRD CM-R23 | exact signed SKU inventory, types, active/inactive status, prices/templates, and no-hard-coded-price checks |
| PRD CM-R24 | expanded entitlement graph with exactly one entitlement check per district/item and umbrella dashboard attachments; overlap/restore/refund matrix |
| PRD CM-R25 | seven commerce placements, exact fallback/allowlist law, and one-impression semantics for every custom surface |
| PRD CM-R26 | “every playable line” post-L5 copy, standalone Night Harbor disclosure where relevant, and unchanged once-ever/decline law |
| PRD CM-R27 | exact product/promo fulfillment matrix and existing durable exactly-once ledger semantics; no weakening |
| PRD CM-R28 | itemized restore for all durable entitlements, umbrella overlaps, existing-owner future-district attachment, and consumable non-restore copy |
| PRD CM-R29 | any-purchase payer classification, owner-state × placement suppression, player-initiated unowned browsing, and no-proration disclosure |
| PRD CM-R30 / CM-R30-D | per-item/overlap revocation rules, retained progress/equipment, 30-day suppression, and unchanged deferred anti-farming posture |
| PRD CM-R31 | replace one All Access code and repo-local code storage with the signed 15-code packet, exact packet count, quota/expiry proof, clean-device matrix, and human secret-store handling |
| PRD CM-R32 | parameterize the unchanged purchase state machine/error law across every signed SKU and promo redemption path |
| PRD CM-R33 | Model-A contingency and `ads_enabled` absence cover all eight placements; existing D10/D14 gate remains |
| PRD CM-R34 | rewarded-only/decline/no-forced-ad invariants enumerate all eight rows and retain consent decision NEW-Q45 |
| PRD CM-R35 | replace “exactly five” with the exact eight-row cap, session, lease, no-fill, task-kill, and expiry contract from §8.4 |
| PRD CM-R36 | 3-decline/24-hour durable mute applies across all eight rows; acceptance clears the sequence as already specified |
| PRD CM-R37 | lifecycle/grant-once/AdTracker parity applies to all eight rows and the exact privacy field inventory in §8.8 |
| PRD CM-R50 | listing still says no forced ads; disclose optional paid districts/cosmetics, rewarded trials, GMA/RC data flow, and never say All Access contains every cosmetic |
| `product_spec.md` §§4, 12, 16–20, 28 | replace “everything premium/complete edition,” All-Access-only Night Harbor, 15-item catalog, five-placement, launch-scope, promo/judge, and no-server leaderboard wording; preserve free 30-level route and earned items |
| `liveops_spec.md` §§1–3 and 7 | retain local Daily/Cup reward laws, add rewardless PGS global-board distinction, eight-placement merchandising/caps, and accurate next-update/runtime versus build-capability switches |
| `docs/plan/data/**` and `config/**` | atomically update catalog, entitlement graph, Offering/Placement map, experiment ledger, ad map, economy, localization (including `tpl_cm_099`), analytics taxonomy, privacy/Data Safety inventory, test matrix, and signed allowlists |
| ADR-0006 | migrate exact daily/session counters, session identity, active trial/guest leases, callback dedupe, and any item cache with byte/count bounds and rollback/process-death fixtures |
| store/ops/submission artifacts | update listing/paywall copy, support/refund/restore playbooks, private judge redemption guide, accessibility/performance evidence, and Console product/ad/experiment checklists |

ADR-0003/ADR-0004 need amendment only if the already-pinned RevenueCat/Ads dependency surface changes;
this amendment itself adds none. Analytics/privacy amendments must distinguish transaction-required
product/entitlement IDs from preview/equip choices and enumerate all vendor recipients before ON.

No dashboard object should be activated ahead of the matching signed manifest. No product may be sold
before its asset/content is in the same production build or has a truthful dated-delivery disclosure.
Every durable item gets purchase, pending, cancel, offline cache, restore, overlap-refund, duplicate
callback, account mismatch, and reinstall tests. Every cosmetic gets silhouette-at-64px and
deutan/protan/tritan checks on the low-tier device under the CM-R21 five-rater protocol. Every paid
district gets the same accessibility and performance evidence as a free district.

### 8.8 Dependency and privacy boundary

This amendment adds no dependency. It continues to use the pinned RevenueCat + RevenueCatUI and AdMob
paths already governed by ADR-0003/ADR-0004. Any SDK update needed for current AdTracker behavior is a
new dependency-pin amendment, not an implementation convenience.

Transaction-required product/entitlement IDs necessarily reach RevenueCat and can reveal which paid
cosmetic was purchased; Offering/Placement/paywall and anonymous RC App User ID linkage also reach RC
for fulfillment, attribution, experiment assignment, fraud/support, and revenue analysis. The
non-PII `cm_test_channel` distribution attribute also reaches RC as a QA/reporting label; it is not an
enrollment gate. Previewed or trial-selected cosmetic choices, cat names, PGS identity, leaderboard
score/rank, and free-form text do **not** go to RevenueCat, AdMob, or ad diagnostics. Equipped/purchased
cosmetic IDs do enter the existing analytics/OneSignal flows below; the supersession must disclose or
remove them rather than promise otherwise.

| Recipient | Exact amended-flow fields | Purpose | Retention/deletion/consent gate before ON |
|---|---|---|---|
| RevenueCat | anonymous RC App User ID; product/entitlement, Offering, Placement, paywall, localized price/currency, purchase/restore/refund state, transaction token/hash fields supported by the pin; `cm_test_channel`; AdTracker fields below | fulfillment, Restore/support, attribution, experiments, fraud and revenue/ad analysis | record the exact dashboard/DPA retention setting and customer-deletion procedure; Reset/Restore behavior and privacy copy must match; commerce and experiment processing reviewed before activation |
| GameAnalytics (`analytics`) | `cosmetic_unlocked(item_id,method)`; `cosmetic_equipped(item_id,previous_item_id,active_theme)`; ad placement/network/unit/reward/error/revenue fields; paywall Placement/Offering/variant/trigger/duration; purchase product/Offering/Placement/price bucket/hashed transaction/error; restore counts/product list; entitlement ID/change/source and payer/ltv aggregates | product balance, funnel/ad performance, failure diagnosis and aggregate award evidence | the coordinated commit records the exact dashboard retention days and tested user-deletion/export path; no raw store token, PGS identity, cat name, or free text; consent/opt-out and Data Safety follow the signed analytics privacy contract |
| OneSignal | existing `preferred_theme=item_id`; `purchase_completed` custom-event fields (`product_id`, `offering_id`, `placement`, `price_local_bucket`, `txn_id_hash`); `payer_status`; and RC-integration tags `app_user_id`, `period_type`, `purchased_at`, `expiration_at`, `store`, `environment`, `last_event_type`, `product_id`, `entitlement_ids`, `active_subscription`, `subscription_status`, `grace_period_expiration_at` when emitted | payer suppression, service messaging, optional theme personalization, and purchase-state integration | exact tag allowlist and vendor retention are signed in the privacy/taxonomy amendment; Reset clears tags and support routes deletion through OneSignal's user-deletion API; notification permission is not misrepresented as consent for unrelated processing |
| Google Mobile Ads / mediation | automatic baseline and ad-request/impression fields described below | ad delivery, measurement, fraud prevention and diagnostics | ads remain OFF until NEW-Q45, manifest, consent, Data Safety, vendor-retention/deletion disclosures and production proxy evidence agree |

The coordinated supersession must make an explicit minimization choice for `preferred_theme` and the
OneSignal transaction fields: keep the exact disclosed allowlist above, or delete those destinations
from the taxonomy/integration. Silence is not a third option. No new skin/livery preview or equip field
may reach OneSignal without another signed taxonomy/privacy amendment.

For each enabled ad impression the manual AdTracker adapter sends RevenueCat the RC App User ID context,
ad network, mediator, format, exact placement, ad-unit ID, impression ID, exact/estimated
`revenueMicros`, currency, precision, and load/display/open/impression/failure codes. “Coarse revenue”
is not an adequate disclosure. When the pinned Google Mobile Ads SDK operates, Google's published
baseline says it automatically collects/shares device IP address (which may derive approximate
location), app launch/tap and video-view interactions, diagnostics such as crash/app-launch/hang data,
and device/account identifiers including Advertising ID and App Set ID.

Under the current CM-R50 candidate the merged manifest contains `AD_ID`, so Advertising ID is treated
as collected whenever the OS supplies it. Removing that permission is the only accepted manifest
control for Advertising ID and requires a new ad/config/listing test; it does not suppress IP address,
App Set ID, interaction, or diagnostic collection. No Cat Metro manifest toggle is assumed to suppress
those other baseline fields: keeping the SDK/ad request OFF is the only complete local suppression.
UMP/consent/request flags may govern storage, personalization, or processing, but are not claimed to
stop a baseline field unless the exact pinned documentation plus proxy capture proves it. The
production inventory names recipient, exact field, purpose, collection/sharing status,
vendor-controlled retention/deletion link, consent/config control, and user-facing disclosure for
Google, RevenueCat, GameAnalytics, and OneSignal.

Before ads or commerce turn ON, the human re-reviews Play Data Safety, privacy policy, age/consent and
NEW-Q45 posture, deletion/support text, RC tester attribute, and vendor retention against an HTTPS
proxy capture of the exact signed production build plus the generated manifest/dependency report.
Unexpected fields or endpoints keep the affected capability OFF; a document claim never overrides
observed runtime behavior.

### 8.9 Rollout and rollback

1. Human signs §8.10; this approves candidates but changes no executable authority or dashboard state.
2. A separate human-authored commit flips `state/mode` to `production` before any monetization code.
3. §8.7's coordinated supersession, migrations, privacy/consent decisions, and signed manifests land;
   exact Play/RC objects remain draft/inactive until that gate is review-green.
4. Ship the signed catalog manifest with all new products inactive and all new ad placements OFF.
5. License-test one product per type, then the full restore/refund/overlap matrix; device-test every rewarded
   placement including no-fill, decline, cap, task-kill, offline, and expired-trial branches.
6. Activate one cosmetic collection at a time through signed dashboard evidence. District content
   activates only with its validated routes. Start an Experiment only after the baseline is stable.

Rollback is remote deactivation plus client fail-closed behavior. Deactivation never revokes an owned
durable item. A broken product is hidden from new purchase while existing owners retain access and
Restore. A broken ad placement is disabled without substituting commerce. A failed experiment returns
all eligible players to the signed control Offering.

### 8.10 Human signature gate

The direction and exact commercial values in §8 are human-signed but remain non-executable until
§8.7's later activation gates are satisfied:

- [x] I approve every concrete product ID and US reference price in §8.2.
- [x] I approve `tpl_cm_099` and the `$0.99` US reference; every linked regional price remains deferred
      to a separately reviewed, human-signed activation manifest.
- [x] I approve the `cm_district_<signed_slug>` naming/$2.99 pattern and the All Access packaging rule.
- [x] I approve the three new placement identifiers, rewards, and candidate caps in §8.4.
- [x] I approve the experiment identifiers/order/variants in §8.5, including PW01's second Play SKU.
- [x] I approve the 15-code-per-recipient / 25-packet judge-access proposal in §8.3, subject to exact
      Play quota/expiry and consumable-redemption proof; no code secret may enter the repository.
- [x] I confirm no energy, loot boxes, subscriptions, premium currency, forced ads, paid Gold, or paid
      global rank; I accept only the named existing local rewind/economy exceptions in §8.1.
- [x] I acknowledge that a separate human-authored `state/mode=production` commit is required before
      any monetization implementation can merge, and that this signature alone activates nothing;
      §8.7's coordinated supersession is also mandatory.

- **Signed by:** Cat Metro product owner (human, in-session; agent-recorded)
- **Signature statement:** “MERGE. I SIGN §8.10 AND ADR-0010.”
- **Signed at (absolute date/time):** 2026-08-09 15:40:29 PDT (-0700), recording time
- **Signed proposal head:** `bbbe79325b9ee14474e2ba1b76218d0b53021ac8`
- **Signature record commit:** _filled by the immediate metadata-only successor after this record lands_

### 8.11 Sources checked for this amendment

- [RevenueCat Offerings](https://www.revenuecat.com/docs/offerings/overview)
- [RevenueCat Experiments overview](https://www.revenuecat.com/docs/tools/experiments-v1/experiments-overview-v1)
- [RevenueCat Experiments by Placement](https://www.revenuecat.com/docs/tools/targeting/placements)
- [RevenueCat custom paywall impression tracking](https://www.revenuecat.com/docs/getting-started/tracking-custom-paywall-impressions)
- [RevenueCat ad monetization](https://www.revenuecat.com/docs/getting-started/ad-monetization)
- [RevenueCat Unity/manual AdTracker integration](https://www.revenuecat.com/docs/ad-monetization/manual-integration)
- [RevenueCat Ads charts and reconciliation caveat](https://www.revenuecat.com/docs/dashboard-and-metrics/charts/ads)
- [Google Mobile Ads Play Data Safety disclosure](https://developers.google.com/admob/android/privacy/play-data-disclosure)
