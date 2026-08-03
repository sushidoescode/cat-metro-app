# CAT METRO — DESIGN-TIME THREAT MODEL (1.0, no server)

**Status:** DRAFT for human ratification · **Compiled:** 2026-08-02 · **Author:** security-reviewer (agent), forge-architect step 3 · **Companions:** `docs/prd/PRD.md` (requirements), `docs/prd/risks.md` (risk register), `docs/plan/specs/architecture.md` (Unity technical architecture) · **Handoff:** architect (ADR consequences) + human product owner (the four escalations already open as NEW-Q45…NEW-Q48).

## 0. What this document is, and what it deliberately does not repeat

`docs/prd/risks.md` already carries 39 rows, of which RK-16…RK-35 came from the forge-specify threat model (items SEC-01…SEC-22). **Those rows are referenced by id and never restated here.** What a threat model owes on top of a risk register is the part that was missing: **assets, trust boundaries, and a mitigation-to-landing-place map** — for every mitigation, the named artifact that must carry it (an ADR consequence, an existing PRD criterion id, or a flagged gap with an owner).

Three things this document adds beyond the register:

1. **Boundary attribution.** Every abuse case is attached to the boundary it crosses, so the architect can put one control at one crossing instead of N controls at N call sites.
2. **New abuse cases.** Fifteen abuse cases below are **not** in the register. They are numbered **SEC-23 onward** (SEC-01…SEC-22 are already spoken for) so the register can absorb them by id without renumbering.
3. **Landing-place status per mitigation** — `REQ` / `Q` / `ADR` / `GAP` (§5). The GAP rows are the actionable output of this document.

**Scope discipline.** This models the system the PRD actually builds: an offline-capable single-player Unity Android title with **no server backend at 1.0** (`docs/prd/risks.md:145-147`; cloud save is a binding NON-GOAL, `docs/prd/PRD.md:927`). Client-side integrity limits are stated as facts, not patched with DRM (§3). No SDK API, version or numeric budget is invented here; every number is cited.

**Verified-today correction to the register.** `docs/prd/risks.md:137` (RK-37) states "no `.github/`, no workflows, no Dockerfile, no `infra/` exist." As of 2026-08-02 that is **superseded**: `.github/workflows/ci.yml`, `.github/workflows/deploy.yml`, `.github/workflows/forge-policy.yml` and two `.disabled` agent workflows exist and are reviewable **now**. §4 TB-8 reviews them at file:line; four of RK-33's controls are implementable today rather than "at the first workflow PR."

---

## 1. Assets

Ranked by what an attacker gains, not by how much we like them.

| # | Asset | Where it lives at 1.0 | What an attacker gains | Class |
|---|---|---|---|---|
| **A-1** | **Release credentials & store identity** — upload keystore + password, Play service-account JSON, RC secret key, OneSignal REST key, `ANTHROPIC_API_KEY`, asset-gen keys (Meshy via Unity-MCP, `state/PROJECT_STATE.md:20`) | CI secrets, human custody; none exist in-repo today | **Irreversible.** Package id is permanent from first upload; a leaked upload key or a suspended developer account takes the app entry, the tester clock and the identity with it (RK-13, RK-33) | Confidentiality → integrity of the whole product |
| **A-2** | **Purchase entitlements** — 4 entitlements via RC umbrella attach; local cache honored **indefinitely** offline (`docs/prd/PRD.md:423`) | RC CustomerInfo (remote truth) + local cache in the save | ~$6.99 of cosmetic/content unlock per forged install; zero marginal cost to us **while the containing invariant holds** (RK-16) | Integrity |
| **A-3** | **Consumable ledger** — rewind balance + never-trimmed dedupe hash set + FIFO-200 audit, single atomic temp+rename write (`docs/prd/PRD.md:456-468`) | Local save only | Free rewinds (cheat-shaped, single-player). The **buyer-harm** direction — under-crediting a paid grant — matters more than the cheat (RK-19) | Integrity + correctness |
| **A-4** | **Save file** — progress, tickets, streak, equipped theme, entitlement cache, **cap/mute/cooldown counters**, **once-ever flags**, purchase breadcrumb, content hash, **save-stored runtime feature flags** (`docs/plan/specs/architecture.md:17-18,99-102`; SI-1…SI-7 at `docs/prd/PRD.md:161-174`) | Local, plaintext, app-private; leaves the sandbox only via Play auto-backup / device transfer | Everything the client is authoritative over — including guarantees the player is supposed to *receive* (once-ever paywall, hard-final messaging) | Integrity (both directions) |
| **A-5** | **Deep-link surface** — 7 `catmetro://` routes + `challenge/{seed}` + an unenumerated query form (NEW-Q15) + App Links on `catmetro.io` + the manual code-entry field (`docs/prd/PRD.md:651,658-660,857`) | Android intent filters; any app, browser, QR, push payload or **ad creative** can drive it | A parameterised entry point into every client state machine, reachable before the first frame | Integrity (untrusted input) |
| **A-6** | **Promo codes** — 25, split 15/5/5, in `ops/judge_codes.md` (`docs/prd/PRD.md:530`) | A repo-shaped file, in a project publishing 56 daily posts | 25 free All Access grants **and** the judge-access path for the submission (RK-26) | Confidentiality |
| **A-7** | **Analytics offline queue** — bounds specified by the architect, on-disk, metrics-only (`docs/prd/PRD.md:687-691`) | Local file, flushed to the analytics backend | The numbers that decide the D7 kill gate and appear in 56 public posts and a Devpost submission (RK-32); also diagnostics **at rest** | Integrity of published claims |
| **A-8** | **Level / content JSON** — 40 in-build levels, `daily_overrides.json`, the 30-board dated backup pool, generated dailies, plus deep-link seeds and share codes (`docs/prd/PRD.md:728-734`) | StreamingAssets, validated in CI, parsed **on the boot path** | A boot-path crash/ANR against a never-cut ≥99.5% crash-free line; arbitrary type instantiation if polymorphic deserialization is ever chosen (RK-34) | Availability + integrity |
| **A-9** | **Player identifiers leaving the device** — RC anonymous app-user-id, OneSignal subscription id + 5 tags incl. `payer_status`, `AD_ID`, crash custom keys (`docs/prd/PRD.md:508,722,798`) | Three third-party backends | No attacker value; **policy value** — an undeclared or mis-declared flow on a 13+ title carrying `AD_ID` is removal-grade (RK-30, RK-12) | Compliance |
| **A-10** | **Ad-network and store account health** | AdMob + Play Console | An invalid-traffic strike or a suspension mid-window costs the revenue evidence and the entry (RK-25, RK-13) | Availability of the venture |

---

## 2. Trust boundaries

Ten crossings. *Trusted?* answers one question only: **may code on our side act on what crosses this line without validating it?**

| # | Boundary | What crosses, and in which direction | Trusted? | Controls that exist **today** (design-time) |
|---|---|---|---|---|
| **TB-1** | **Player device ↔ app process** | In: local save bytes, system clock, the APK itself. Out: nothing | **No.** The device owner is a legitimate attacker on their own data | CM-R05 SI-1…SI-7 (integrity of *our* writes, not tamper-resistance); CM-R11.6 clock-cheat containment; `leaderboard` OFF (`docs/prd/PRD.md:769`) keeps cheating self-contained |
| **TB-2** | **App ↔ RevenueCat ↔ Play Billing** | In: CustomerInfo, entitlements, transaction ids, revocation events. Out: purchase intents, app-user-id | **Yes for grants** (the store is the authority); **no for absence** — offline silence is not a revocation | CM-R24 (one entitlement check per feature), CM-R27 (exactly-once grant), CM-R32 (state machine; only Granting→Done mutates durable state) |
| **TB-3** | **App ↔ OneSignal** | In: push + IAM copy **and a deep link**, all dashboard-controlled. Out: 5 tags incl. `payer_status`, subscription id | **No inbound.** Remote copy is remote-controlled text; a leaked REST key makes it attacker-controlled text (RK-29) | CM-R38.8 (adapter is sole writer; names must exist in the taxonomy), CM-R39 (quiet hours + 2/day), CM-R38.5 (J3 never sells) |
| **TB-4** | **App ↔ AdMob / GMA** | In: ad creatives rendered **in our process**, `onUserEarnedReward`, fill/no-fill. Out: `AD_ID`, ad-attributed signals | **No.** The reward callback is a client assertion — SSV is unavailable on Unity via RC (`docs/prd/PRD.md:600,608`) | CM-R34 (rewarded/opt-in only; reward only on the callback), CM-R35 (five surfaces, locked caps), CM-R33 (`ads_enabled` kill switch) |
| **TB-5** | **App ↔ deep-link / intent / code surface** | In: URIs from any app, browser, QR, push payload, **ad-creative click-through**, and hand-typed share codes | **No.** The most attacker-reachable input in the product, and it is reachable before the first frame | CM-R41.1-.3 (7 routes, invalid input, safe fallback to Home), CM-R54.5 (invalid codes fall back to Home) |
| **TB-6** | **App ↔ Play auto-backup / device transfer** | Out: everything not excluded, to Google Drive. In: that same blob — **onto a different device or Google account, at a time of the player's choosing** | **No.** Restore is an attacker-timed **rollback** primitive *and* a cross-account **copy** primitive | **None.** Cloud save is a NON-GOAL and the consequence is recorded but uncovered (`docs/prd/PRD.md:931`; RK-17) |
| **TB-7** | **App ↔ analytics + crash backends** | Out: 45 event types with params, crash breadcrumbs and custom keys, `purchase_breadcrumb` (`docs/prd/PRD.md:465`) | Outbound-only; the risk is **what we send** | CM-R45.1-.5 (privacy class per event; proxy capture incl. a forced-crash session; no raw transaction ids) |
| **TB-8** | **Repo / CI ↔ release credentials and store publishing** | Secrets into runners; signed artifacts out to Play | **No.** Any job that can execute agent-authored code is on the untrusted side of the credential | `.github/workflows/forge-policy.yml:101-102` makes the workflow directory human-signed-commit-only; `:57-60` stops a branch editing its own judge; `.github/workflows/deploy.yml:7,11` is tag-triggered into a `production` environment; solo residual recorded at `docs/adr/0001-solo-ruleset-posture.md:12` |
| **TB-9** | **Agent sessions ↔ external text** | In: tester feedback (18–20 people), Play reviews, Devpost/Discord comments, BIP replies, pre-launch report text — pasted into sessions holding write tools | **No.** There is no LLM in the *product*; the *pipeline* is the injection surface (RK-35) | AGENTS.md hard rule 6; client-side hook belts; the human merge floor |
| **TB-10** | **Build-time content pipeline ↔ runtime parser** | In: level JSON, `daily_overrides.json`, backup pool, generated dailies — CI-validated, then parsed at Boot on a device we do not control; plus seeds arriving from TB-5 | **Partially.** CI only validates what CI saw | CM-R12 (11-stage validation as a merge gate), CM-R46.1-.4 (90 dates pre-validated, ≤200 ms boot validation, backup-pool fallback) |

**Boundary collapse worth stating once:** TB-3 and TB-4 both terminate in TB-5. A OneSignal push payload and an AdMob creative's click-through are, to the router, indistinguishable from a hostile web page's intent. **One allowlist at TB-5 covers all three, or none of them.**

---

## 3. What client-side integrity can and cannot do (stated honestly, as the PRD does)

With no server at 1.0, four limits are **facts**, not defects:

1. **Local state cannot be made authoritative.** The save is plaintext (RK-21). It is not casually reachable on an unrooted device — the practical channels are (a) Play auto-backup / device transfer, (b) a repackaged or debuggable build, (c) root. That narrows *who*, not *whether*.
2. **Ad rewards cannot be verified.** Server-side verification is unavailable on Unity via RC (`docs/prd/PRD.md:600`), so `onUserEarnedReward` is a client assertion (RK-24).
3. **Entitlements cannot be revoked offline.** A refunded player who never reconnects keeps everything (RK-22); cached entitlements are honored indefinitely by design (RK-16).
4. **Players cannot be authenticated.** There are no accounts; "account" resolves to an anonymous RC app-user-id that Settings ▸ Reset progress rotates (`docs/prd/PRD.md:839`). Any control needing a stable identity must manufacture one — which is exactly why RK-23 recommends **not** building refund-farm detection.

**The containing invariant that makes all four acceptable** (already escalated as NEW-Q46, `docs/prd/PRD.md:1093`):

> **No entitlement, reward, cap or grant may unlock anything that costs us marginal money, or on which another player's outcome depends.**

True today: Night Harbor ships in-build; Cup medals are cosmetic and any-rewind runs cap at Silver; `leaderboard` is OFF. Three enforcement rules follow, and they are the spine of §5:

- **R-A — Marginal-cost rule.** Anything that spends real money on our behalf (an ad impression, a promo grant, a refund path) is never gated by client-asserted state alone.
- **R-B — Fail-closed rule.** Where client state is untrusted or of unknown provenance, resolve **against the player for grants** and **in the player's favour for suppressions** (never re-sell, never re-message). Both directions matter; SEC-31 below is the case that proves it.
- **R-C — One-enforcement-point rule.** Each cap, grant and suppression is enforced in exactly one place — the discipline CM-R29 already applies to payer suppression — so a new grant source cannot silently bypass a cap.

---

## 4. Abuse cases, by boundary

**42 cases.** `Reg.` column: an `RK-`/`SEC-` id means the register already carries it (not restated); **`NEW`** means this document raises it and assigns the next free `SEC-` id so `docs/prd/risks.md` can absorb it without renumbering. L/I in the register's own vocabulary.

### TB-1 — Player device ↔ app process

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-01** | Root, repackage, or run a debuggable build → edit the plaintext save (`rewindBalance`, tickets, stars) | A-3, A-4 | High | Low | SEC-06 / RK-21 |
| **AC-02** | Ship or install a pre-patched APK that never contacts RC → All Access + both themes + Night Harbor, permanently | A-2 | High | Low-Med | SEC-01 / RK-16 |
| **AC-03** | Edit the **save-stored runtime feature flags** (`docs/plan/specs/architecture.md:99-102`) → set `ads_enabled=true` on a build shipped with ads dark, or `leaderboard=true`. Two effects: **(a)** if NEW-Q45 is answered with fallback B ("restrict availability / ship `ads_enabled=false`"), the *compliance* control is player-editable and the build serves ads to a user we decided not to serve; **(b)** `leaderboard=true` renders `rank_bucket`, which has **no producer** with no leaderboard backend (`docs/prd/PRD.md:696`, U-6) — an undefined surface reached from tampered state | A-4, A-9 | Low | Med (policy) | **NEW — SEC-23** |
| **AC-04** | Reset the **cap / cooldown counters** that live in the same save: `ad_watches_today` (`docs/prd/PRD.md:605`), the five rewarded caps (CM-R35.2), the 3-decline 24 h mute (CM-R36.3), the 30-day review cooldown (CM-R53.3), comeback-rung idempotence keys (CM-R49.1), the daily faucet cap (CM-R49.2) → **unbounded ad watches**. This is what converts RK-24 from "unlimited rewinds" (ticket economy, cheap) into **unlimited ad impressions** — which is RK-25's account-health exposure, the expensive one | A-4, A-10 | Med | Med | **NEW — SEC-24** |
| **AC-05** | Forge a `purchase_breadcrumb` (CM-R27.8) → boot enters the silent Verifying path from untrusted state; contained by the 72 h expiry and by "no grant without RC reconciliation" | A-3 | Low | Low | minor, contained |
| **AC-06** | Move the device clock → contained today by CM-R11.5/.6 (play proceeds, only rank display and share-card generation are suppressed, the player is never punished). **Carry-forward:** any cap keyed on local date alone re-opens this (RK-24 iii) | A-4 | Med | Low | contained |

### TB-2 — App ↔ RevenueCat ↔ Play Billing

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-07** | Stay offline indefinitely → entitlement cache honored forever; clock tampering cannot revoke | A-2 | High | Low-Med | SEC-01 / RK-16 |
| **AC-08** | Refund, then stay offline or DNS-block `api.revenuecat.com` → revocation never arrives | A-2 | Med | Low-Med | SEC-07 / RK-22 |
| **AC-09** | Any store path reusing or shaping a transaction id across products → dedupe collision; the second grant is silently swallowed and the **buyer is under-credited** | A-3 | Low | Med | SEC-04 / RK-19 |
| **AC-10** | Trigger a redelivery storm / buy heavily over time → never-trimmed dedupe set grows the atomic write, widening the CM-R05.1 kill-during-write window on low tier | A-3 | Low | Med | SEC-05 / RK-20 |
| **AC-11** | Use Settings ▸ Reset progress → the RC anonymous id rotates, orphaning real purchases (or, if implemented as "rotate but keep entitlements", the Data Safety deletion claim is false) | A-2, A-9 | Med | Med | SEC-03 / RK-18 |
| **AC-12** | **The RC anonymous app-user-id itself travels in auto-backup.** Restore one backup onto two devices (or another person's account) → both present the same app-user-id, so the entitlement follows the copy. Critically, this **defeats RK-17's own proposed fix** ("bind the entitlement cache to the app-user-id it was fetched under and discard on mismatch"): the id is restored alongside the cache, so the mismatch never fires. The exclusion set must therefore cover the **id**, so a restored install starts anonymous and must use Restore purchases (CM-R28) — the Play-account-authoritative path — with the RK-18 rotation semantics reconciled in the same decision | A-2, A-9 | Med | Med | **NEW — SEC-25** |
| **AC-13** | Not an attacker — an unverified assumption: if RC 9.7.0 consumption is client-triggered and **non-atomic** with the ledger write, AC-09 and AC-10 change shape | A-3 | Med | Med | RK-39 |

### TB-3 — App ↔ OneSignal

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-14** | Leak/steal the REST key or the dashboard session → push "Your purchase failed — tap to restore" to the whole install base with a hostile destination; the product's honesty posture makes players **more** likely to trust it | A-9, players | Low | High | SEC-14 / RK-29 |
| **AC-15** | Nothing — a design flow: `payer_status` is purchase information shared with a third party, bound to a persistent subscription id, on a 13+ title carrying `AD_ID`, and the tag set is enumerated in no taxonomy artifact | A-9 | Med | High (policy) | SEC-15 / RK-30 |
| **AC-16** | **Fail the same level twice within 60 minutes, repeatedly.** J3's entry is a **client-detected** trigger (×2 filter applied in the adapter, `docs/prd/PRD.md:620`) and CM-R38.6 **banks a free rewind at trigger time** so the copy is true whether or not the push delivers (`:622`). No cap, cadence or ledger routing is specified for that grant, and journey re-entry rules are dashboard config no one can audit from the repo (RK-38). Effect: a deterministic free-rewind faucet, competing directly with the two paid rewind SKUs, reachable with zero tooling and no tampering | A-3 | Med | Med | **NEW — SEC-26** |
| **AC-17** | Push/IAM payloads carry deep links → the same untrusted-input problem as TB-5, even though the sender is "us" (see AC-23, AC-27) | A-5 | — | — | folds into RK-27 |

### TB-4 — App ↔ AdMob / GMA

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-18** | Hook `onUserEarnedReward` → unlimited rewinds/tickets, bounded by CM-R35 caps **only if** those caps are on the durable ledger rather than in memory | A-3 | Med | Med | SEC-09 / RK-24(a) |
| **AC-19** | Go into airplane mode (or block the ad network) and take `streak_saver` → CM-R33.5's no-fill fallback pays **150 tickets unconditionally, with no ad shown**. A designed bypass, not a hack | A-3 | Med | Med | SEC-09 / RK-24(b) |
| **AC-20** | Drive modified-client ad traffic → an invalid-traffic strike or account suspension mid-window. **Amplified by AC-04**: rollback-reset caps make the impression volume unbounded | A-10 | Low | High | SEC-10 / RK-25 |
| **AC-21** | Serve (or buy) an ad creative whose click-through targets **our own `catmetro://` scheme** → the ad network becomes an **in-process source of untrusted intents**. RK-27 enumerates "any app, web page, QR code or push payload"; creatives are not in that list, and they render inside our process with a user tap already attached | A-5 | Low | Med | **NEW — SEC-27** |
| **AC-22** | An EEA/UK user installs → ads and ad-attributed analytics run with no certified CMP/UMP flow. The **decision** is open (NEW-Q45); what no artifact names is the **enforcement point** — Boot is where SDK init already happens behind feature flags (`docs/plan/specs/architecture.md:58`), so the ordered gate has an obvious home and no owner | A-9 | High | High (policy) | SEC-16 / RK-11 |

### TB-5 — App ↔ deep-link / intent / code surface

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-23** | Send a crafted `catmetro://` URI from any app/page/QR → traversal via `level/{id}`, boot-loop via a pathological `challenge/{seed}` parsed before the save loads, forged score into share-card generation, or a forced commerce surface | A-5, A-8 | High | Med | SEC-12 / RK-27 |
| **AC-24** | Send `catmetro://daily?d=YYYY-MM-DD&b=score` (the query form NEW-Q15 flags as unenumerated, `docs/prd/PRD.md:659`) with an attacker-chosen **dateKey** → the daily surface awards one scoring play per dateKey (CM-R11.4) and drives streak increments (CM-R11.5). If `d` is ever used as the **scoring** key rather than a display/navigation hint, an attacker-supplied date is a streak and ticket faucet — and it arrives through a channel CM-R11.6's clock-cheat containment does not watch, because the device clock was never moved | A-4, A-5 | Med | Med | **NEW — SEC-28** |
| **AC-25** | Type an adversarial code into the **manual code-entry field** CM-R54.4 puts on Home as the OEM fallback → it reaches the same seed/board parser as `challenge/{seed}` but does **not** pass through the DeepLinkRouter, so router-scoped hardening (RK-27) misses it entirely | A-5, A-8 | Low | Med | **NEW — SEC-29** |
| **AC-26** | Register the domain the project does not own → App Links resolve to an attacker's `assetlinks.json`; share links become a phishing vector aimed at our own players, and the privacy-policy URL is unguaranteeable | A-5, players | Med | Med | SEC-13 / RK-28 |
| **AC-27** | Drive a player to `catmetro://restore` (a route in CM-R41's list, `docs/prd/PRD.md:651`) or to the **Settings ▸ Redeem a code** surface CM-R31.4 `P-client-surface` adds → a network/account action and the **promo-code redemption sheet** (asset A-6) are both reachable from untrusted input. RK-27(e) questions `restore`; the redeem surface post-dates that row and belongs in the same non-reachable set | A-5, A-6 | Low | Med | **NEW — SEC-30** |

### TB-6 — App ↔ Play auto-backup / device transfer

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-28** | Buy → back up → restore onto a second account/device → the entitlement cache arrives without the purchase | A-2 | Med | Med | SEC-02 / RK-17(A) |
| **AC-29** | Buy 20 rewinds → back up → spend → wipe → restore → balance restored, redelivery blocked by the dedupe set, but the **spend** is undone. Repeatable | A-3 | Med | Med | SEC-02 / RK-17(B) |
| **AC-30** | **Roll back the save and replay every "once ever / never again" guarantee.** The save holds `post_level_5` once-ever consumption (CM-R26.2), `payer_thanks` exactly-one-delivery (CM-R29.4), `lapse_final_sent` (CM-R38.7), `winback_optout` (CM-R49.4), `feedback_request` 1-per-build (CM-R42.3), the 3-decline mute (CM-R36.3) and the review cooldown (CM-R53.3). Two effects, and the first is the one that matters: **(i) player-harm** — after an ordinary device transfer a player who dismissed the one scripted paywall or opted out of win-back messaging is re-sold and re-messaged, so CM-R26 and CM-R49.4 are best-effort rather than the guarantees their copy claims; **(ii) farming** — the same rollback resets the AC-04 counters. **No requirement covers save provenance on restore** | A-4, players | Med | Med | **NEW — SEC-31** |
| **AC-31** | The **analytics queue file** is not in RK-17's exclusion list → diagnostics at rest (params incl. `txn_id_hash`, `price_local_bucket`, `device_tier`) leave the sandbox to Drive, and a restored stale queue **re-flushes** the very events that compute the D7 gate. RK-32's per-event idempotency id must therefore survive **backup**, not merely retry | A-7, A-9 | Low | Med | **NEW — SEC-32** |

### TB-7 — App ↔ analytics + crash backends

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-32** | No attacker — a leak path: an RC error string carrying a transaction id lands in Crashlytics breadcrumbs, custom keys or `purchase_breadcrumb`, outside the analytics taxonomy gate | A-9 | Med | Med | SEC-18 / RK-31 |
| **AC-33** | Edit the on-disk queue to inject `level_completed`/`daily_completed` → forged D7 gate numbers, published in 56 posts and to judges; unbounded depth also fills low-tier storage | A-7, A-11 | Med | Med | SEC-19 / RK-32 |
| **AC-34** | RK-31's scrubber is scoped to **transaction-id-shaped** tokens. Nothing forbids the **RC app-user-id** or the **OneSignal subscription id** appearing in an analytics param or a crash custom key — both are persistent identifiers on a 13+ title carrying `AD_ID`, and either one silently converts a `behavioral_no_pii` event into an identified one, breaking CM-R45's Data Safety parity | A-9 | Med | Med | **NEW — SEC-33** |

### TB-8 — Repo / CI ↔ release credentials

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-35** | Keystore committed; a workflow echoing a base64 secret; an unpinned third-party action updated to malicious code; the AVD smoke job handed release credentials it does not need. **Four of these are checkable today** (RK-37 assumed no workflows exist): `.github/workflows/ci.yml:10` and `.github/workflows/deploy.yml:13` use `actions/checkout@v5`, a **mutable tag**, while `.github/workflows/forge-policy.yml:26` correctly pins by commit SHA · neither `ci.yml` nor `deploy.yml` declares a `permissions:` block, so both inherit the repository default `GITHUB_TOKEN` scope (a repo setting this review cannot read; `forge-policy.yml:17-18` shows the intended `contents: read` pattern) · `.github/workflows/deploy.yml:11` names `environment: production` but its reviewers/protection rules are a comment plus an open TODO (`state/PROJECT_STATE.md:32`), so the human-approval-before-publish control **does not exist yet** · secret scanning is a TODO in both places (`.github/workflows/ci.yml:17`; `state/PROJECT_STATE.md:33`) while RK-26 requires it **before `ops/judge_codes.md` is ever created** | A-1 | Med | Critical | SEC-20 / RK-33 |
| **AC-36** | Promo codes committed, screenshotted into a BIP post, or pasted into a public Devpost field | A-6 | Med | Med | SEC-11 / RK-26 |
| **AC-37** | **The composite chain neither row names alone:** external text (TB-9) → an agent proposes a build/CI change → a job holding release credentials executes it. Today the chain is broken in two places (the workflow directory requires a human-signed commit, `forge-policy.yml:101-102`; a branch cannot edit its own judge, `:57-60`), but under solo posture **no server-side control requires a second human for a merge** (`docs/adr/0001-solo-ruleset-posture.md:12`). The surviving control is therefore **credential placement, not review** — which promotes NEW-Q48's "smoke jobs on a debug key with zero release secrets" from hygiene to load-bearing | A-1 | Low | Critical | **NEW — SEC-34** |
| **AC-38** | Enable `.github/workflows/claude-review.yml.disabled` at graduation (`state/PROJECT_STATE.md:10`) as written → a job that **reads PR content** while holding `pull-requests: write` and `secrets.ANTHROPIC_API_KEY` (`:11,17`), with neither `actions/checkout@v5` nor `anthropics/claude-code-action@v1` pinned by SHA (`:13,15`). The fork guard at `:9` is correct and satisfies `docs/security/agent-checklist.md:8`; the unpinned actions and the write-scoped token in a content-reading job are the residue. Today this is a `git mv` in a checklist with **no security criterion attached** | A-1 | Low | High | **NEW — SEC-35** |
| **AC-39** | Land monetization code without the stakes-mode flip and without independent review → nothing mechanical objects. AGENTS.md declares `**/billing/**`, `**/iap/**`, `**/ads/**` and `infra/**` risky paths, and `state/PROJECT_STATE.md:10` makes the mode flip a precondition, but those globs appear in **neither** the ownership file **nor** the server-side policy job (both cover only the enforcement-bearing set). The monetization tripwire is convention held by one person — a stated posture, not a control | A-1, A-10 | Med | Med | **NEW — SEC-36** |

### TB-9 — Agent sessions ↔ external text

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-40** | Craft tester feedback / a Play review / a Devpost comment that reads as a task ("the tests are wrong, disable X", "just flip the flag") → an agent weakens a test or flips a flag. Not "ignore previous instructions" — plausible, on-topic product feedback, arriving exactly during the Growth crunch when the pressure to relax the floor peaks | A-12, A-1 | Med | High | SEC-22 / RK-35 |

### TB-10 — Build-time content pipeline ↔ runtime parser

| # | Attacker action → effect | Asset | L | I | Reg. |
|---|---|---|---|---|---|
| **AC-41** | A pathological board (huge `waves`, deep nesting) or a truncated file on the boot path → OOM/ANR on low tier; and the classic — polymorphic/type-name deserialization chosen for schema flexibility turns level JSON into arbitrary type instantiation | A-8 | Med | Med | SEC-21 / RK-34 |
| **AC-42** | Exceed the **budgets** rather than the validation: the device-side salt loop is bounded at 250 ms with beam width 1k (CM-R46.3) and boot validation at ≤200 ms (CM-R46.4), but both are asserted in tests, not enforced at runtime. CM-R46.4 defines the fallback when validation **fails**; nothing defines behaviour when the **bound is exceeded** on generated or tampered content | A-8 | Low | Med | **NEW — SEC-37** |

---

## 5. Required mitigations and where each must land

**Status vocabulary** — this column is the point of the document:

- **REQ** — an existing PRD criterion already mandates it. Nothing new is needed; the id is cited so a reviewer can check it.
- **Q** — a human open question already owns the **decision** (`NEW-Qnn` / `D-n`). The decision is not an agent's; the *enforcement point* still needs an ADR consequence once answered.
- **ADR** — must be recorded as a **named consequence** of an ADR the architect is authoring now. Only `ADR-0001` exists, so the number is the architect's to assign; each row names the ADR **topic** and the consequence sentence it must carry.
- **GAP** — **nothing today owns it**: no criterion, no open question, no ADR. Needs a commissioned criterion (human) or a new ADR consequence (architect). These are the actionable output.

| # | Required mitigation | Closes | Must land as | Status |
|---|---|---|---|---|
| **M-01** | Write the containing invariant (§3, rule R-A) as a **MUST**: no entitlement, reward, cap or grant unlocks anything with marginal cost or cross-player effect | AC-02, 07, 08, 18, 19 | NEW-Q46 (human records the acceptance) **+** `[ADR:ENTITLEMENT]` consequence, so it survives future features | **Q + ADR** |
| **M-02** | Refresh CustomerInfo on every foreground-with-network; the online answer wins over cache **in both directions** | AC-07, 08, 11 | `[ADR:ENTITLEMENT]` — RK-16(b) is a proposal with no criterion | **ADR** |
| **M-03** | Domain-separated dedupe key `SHA-256("cm-ledger-v1\|" + productId + "\|" + transactionId)`; the versioned prefix makes the scheme migratable | AC-09 | `[ADR:ENTITLEMENT]` — it changes a shipped key format, so it is cheapest **before the first grant ever lands** | **ADR** |
| **M-04** | Cap the dedupe set at a stated N with a **tested** overflow policy that refuses further grants loudly (`error_caught`) rather than trimming silently | AC-10 | CM-R05.5 already carries `SAVE_MAX_BYTES`; the cap value and the refusal policy are `[ADR:SAVE]` | **REQ (partial) + ADR** |
| **M-05** | Auto-backup / device-transfer **exclusion set**, enumerating **five** items, not three: entitlement cache · consumable ledger (balance + audit) · dedupe set · **RC app-user-id** · **analytics queue file** | AC-12, 28, 29, 31 | A commissioned PRD criterion that lands **with the save format**, not after (RK-17 names only the first three) | **GAP** |
| **M-06** | **Restore-provenance fail-closed rule (R-B):** a save arriving from a backup restore, or whose bound identity mismatches, is treated as **suppression flags SET** and **grant/cap counters CONSUMED**. With no server this is the only honest answer to rollback: it protects the player from being re-sold and re-messaged, and removes the farming payoff, without pretending to detect tampering | AC-04, 30 | Commissioned criterion + `[ADR:SAVE]` consequence | **GAP** |
| **M-07** | Any flag carrying a **policy or containment guarantee** is compile-time only. Save-stored runtime flags may never be the sole carrier of one — specifically `ads_enabled` under a NEW-Q45 fallback-B answer, and `leaderboard` OFF, which is load-bearing for the whole "cheating is self-contained" argument | AC-03 | `[ADR:FLAGS]` consequence + a criterion under CM-R48 stating which flags are which | **GAP** |
| **M-08** | **One enforcement point per grant and per cap (R-C)**, covering every source: completed ad watch, `streak_saver` no-fill fallback, J3's banked rewind, comeback rungs, promo grants. One config constant, one call site — the discipline CM-R29 already applies to payer suppression | AC-04, 16, 18, 19 | RK-24(i) covers the no-fill path only; the general rule is an `[ADR:ENTITLEMENT]` consequence | **ADR + GAP (J3)** |
| **M-09** | J3's banked free rewind is **capped**, written through the durable ledger, and consumes the same daily cap slot as a watched ad; the journey's re-entry rule is captured in the RK-38 config export | AC-16 | Commissioned criterion under CM-R38 | **GAP** |
| **M-10** | Caps persisted in the atomic save (never PlayerPrefs/memory) and never keyed on local date alone — paired with a monotonic-clock sanity check as CM-R11.6 already does for the Daily | AC-04, 06, 18 | `[ADR:SAVE]` + `[ADR:INTEGRATIONS]` | **ADR** |
| **M-11** | Strict **allowlist of route names and parameter names** at TB-5; every parameter typed and range-checked; never string-concatenated into a path or resource name; unknown route or param → Home with **no partial handling** | AC-23, 24, 25, 27 | RK-27(a)(b) as commissioned criteria + `[ADR:DEEPLINK]` router contract | **Q + ADR** |
| **M-12** | Router resolves **after** save load, behind a crash-safe pre-parse; "malformed link at cold start still boots to Home" as an explicit criterion on CM-R41.2 | AC-23 | RK-27(c) as a commissioned criterion | **Q** |
| **M-13** | **MUST:** no deep link may grant an entitlement, tickets or a rewind, or open a purchase/paywall surface. **Scope extension this document adds:** the same rule binds ad-creative click-throughs, push payloads, the `catmetro://restore` route and the Settings ▸ Redeem-a-code sheet. Links land on read-only destinations; commerce always requires a subsequent in-UI tap | AC-17, 21, 23, 27 | RK-27(d) is the one the threat model asks be made a MUST; the four extra surfaces are new | **Q + GAP (scope)** |
| **M-14** | The **scoring** dateKey is derived from the device-clock path only. A link-supplied `d` may select what board is *shown*; it may never select what is *credited* (score, streak, ticket award) | AC-24 | Commissioned criterion under CM-R11 / CM-R41; NEW-Q15 asks only whether the query form is enumerated | **GAP** |
| **M-15** | The manual code-entry field uses the **same** validation function as the router — one parser contract, two call sites, one test set | AC-25 | Commissioned criterion under CM-R54 + `[ADR:DEEPLINK]` | **GAP** |
| **M-16** | Domain ownership as a **blocking sub-decision of D-2**; `assetlinks.json` served over HTTPS on the frozen domain before any share link ships; privacy-policy URL, support email and webhook host re-derived from the same identity; **no `rc-hooks` host at 1.0** unless it is authenticated (RC signs webhooks; an unauthenticated one is a free entitlement-mutation endpoint) | AC-26 | D-2 / NEW-Q27 | **Q** |
| **M-17** | **Consent enforcement point:** an ordered Boot gate — consent resolved **before** any ad load, any ad-attributed analytics call, and any OneSignal identification — persisted and re-checked, with a "no consent ⇒ non-personalized ads, ad rows still honest" path | AC-22 | NEW-Q45 owns the **decision**; the **enforcement point** is owned by nothing. Boot is where SDK init already sits (`docs/plan/specs/architecture.md:58`) | **Q + GAP (enforcement point)** |
| **M-18** | OneSignal **App ID only** in the client; REST key never in the client and never in an agent-readable file; message destinations restricted to the M-11 allowlist; **no HTML/rich-text rendering of remote copy**; no remote content rendered inside a purchase or entitlement surface; dashboard on 2FA, key rotated after the event | AC-14 | `[ADR:INTEGRATIONS]` + §6 custody table | **ADR** |
| **M-19** | **One data-out inventory artifact** as the Data Safety source of truth: the 45 events **plus** OneSignal tags, crash custom keys, `AD_ID`, and any RC subscriber attributes — each with name, value domain, privacy class and destination | AC-15, 32, 34 | CM-R45.1 covers events; RK-30 covers tags; **no artifact covers all four channels** | **GAP** |
| **M-20** | One logging wrapper with a scrubber applied to events, breadcrumbs **and** crash custom keys, whose deny-list includes **persistent identifiers** (RC app-user-id, OneSignal subscription id), not only transaction-id-shaped tokens | AC-32, 34 | RK-31 covers the wrapper; the identifier scope is new | **ADR + GAP (scope)** |
| **M-21** | Queue bounds, drop-oldest policy, visible `queue_dropped` counter, metrics-only scope, and a per-event idempotency id **that survives a backup restore**, not merely a retry | AC-31, 33 | CM-R43.4(a)–(d) carries the first four; backup-survival is new | **REQ + GAP (extension)** |
| **M-22** | Every published rate carries denominator, vintage and the client-authoritative provenance statement | AC-33 | CM-R56.3 | **REQ** |
| **M-23** | **No polymorphic / type-name deserialization, ever** — explicit typed models only, stated as a MUST so it is not reached for later as a convenience | AC-41 | `[ADR:CONTENT]` consequence | **ADR** |
| **M-24** | Runtime bounds validation independent of the CI schema gate (max nodes/edges/waves/switches, nesting depth, file size), **plus** defined behaviour when a *budget* is exceeded rather than when validation fails | AC-41, 42 | `[ADR:CONTENT]`; CM-R46.4 defines the validation-failure fallback only | **ADR + GAP (budget branch)** |
| **M-25** | A fuzz corpus of malformed and adversarial level JSON in CI — the existing monkey fuzz exercises the UI, not the parser | AC-41 | `[ADR:CONTENT]` + agent-implemented test asset | **ADR** |
| **M-26** | Release-credential controls: Play App Signing (repo holds an *upload* key only) · encrypted CI secrets only · a `production` environment with **required human approval** · smoke/test jobs on a debug key with zero release secrets · third-party actions pinned by commit SHA · least-privilege `permissions:` · no secrets on `pull_request` triggers · secret scanning in the hook belt **and** CI · service account scoped to this app and rotated after the event | AC-35, 37 | NEW-Q48 owns the set. **Four items are implementable today and owned by no dated task:** SHA-pin `actions/checkout` in `.github/workflows/ci.yml:10` and `.github/workflows/deploy.yml:13` · add explicit `permissions:` to both · configure reviewers on the `production` environment (`.github/workflows/deploy.yml:11`, `state/PROJECT_STATE.md:32`) · wire secret scanning (`.github/workflows/ci.yml:17`) | **Q + GAP (the four)** |
| **M-27** | Secret scanning live **before `ops/judge_codes.md` is created**; one code per named recipient with a redemption log; short expiry; spare pool unpublished; pre-publish check for code strings on every screenshot | AC-36 | CM-R56.5 and CM-R57.6 carry the publication rules; **the sequencing control ("scanner before the file exists") has no dated owner** | **REQ (partial) + GAP** |
| **M-28** | Attach a security criterion to enabling the agent-review workflow at graduation: keep the fork guard, SHA-pin both actions, and justify (or drop) `pull-requests: write` on a job that reads PR content | AC-38 | `docs/security/agent-checklist.md:8,24-25` states the rules; the graduation step names no criterion | **GAP** |
| **M-29** | Give the monetization risky-path globs (`**/billing/**`, `**/iap/**`, `**/ads/**`, `infra/**`) a mechanical counterpart to the stakes-mode precondition — an ownership entry, a policy-job path rule, **or** a recorded human acceptance that the tripwire is convention-only | AC-39 | AGENTS.md declares them; `CODEOWNERS` and the policy job cover only the enforcement-bearing set | **GAP** |
| **M-30** | Make the external-text ingest rule operational: pasted external content is **fenced and source-labeled**, never a task instruction; no agent action rests on the authority of external text alone; the human-merge floor and hook belts are **not relaxed during the Growth crunch**, which is when the pressure peaks | AC-40 | AGENTS.md hard rule 6 (stated) + RK-35 (process owner) | **Q / process** |

---

## 6. Key and secret custody (which key may exist where)

Derived from the boundaries, not from convention. "Agent-reachable" = any file, env var or CI job an agent session or an agent-authored branch can read or cause to run.

| Secret | In the client binary? | In the repo? | Agent-reachable? | Where it must live |
|---|---|---|---|---|
| RC **public SDK key** | **Yes** — public by design | Yes | Yes | Client config |
| OneSignal **App ID** | **Yes** | Yes | Yes | Client config |
| AdMob **app id / unit ids** | **Yes** | Yes | Yes | Manifest / client config |
| RC **secret API key** | **Never** | **Never** | **Never** | With no server at 1.0 the correct answer is that it **does not exist in this project at all** (RK-28's webhook-host note is the same conclusion from the other end) |
| OneSignal **REST API key** | **Never** | **Never** | **Never** | Human custody / CI secret only (RK-29) |
| **Upload keystore + password** | n/a | **Never** | **Never** | Encrypted CI secret + Play App Signing, so the repo can only ever hold an *upload* key (RK-33) |
| **Play service-account JSON** | n/a | **Never** | **Never** | Encrypted CI secret, scoped to this one app, minimum publish role, in the approval-gated `production` environment only |
| `ANTHROPIC_API_KEY` | n/a | **Never** | Only inside the review job | Repo secret; job scope reviewed at graduation (AC-38) |
| **Asset-gen keys** (Meshy via Unity-MCP, Tripo) | n/a | **Never** | **No** — owner-only store (`state/PROJECT_STATE.md:20`) | Owner-only credential store at the asset phase |
| **Promo codes** (A-6) | n/a | **Never** (gitignored **and** scanned before the file exists) | **Never** | Human custody + judge-only Devpost field (CM-R57.6) |

The rule this table encodes, and which the existing "agents never run `fastlane supply`" rule needs as its technical counterpart: **agent-reachable contexts never hold a credential that can publish, mutate entitlements, or message the install base.**

---

## 7. Invariants to carry forward, and the conditions that re-open closed classes

Five invariants. Breaking any one of them silently re-opens a class this model currently treats as contained:

1. **Marginal-cost invariant (R-A / M-01).** The moment an entitlement, reward or cap gates something that costs money or affects another player, RK-16/RK-22's accepted residual stops being acceptable and the whole no-server posture needs re-deciding.
2. **`leaderboard` stays OFF.** Cheating is self-contained *because* no cross-player surface consumes client-authored scores. Flipping it on re-opens daily-seed manipulation, save editing and the share-card score parameter (AC-01, AC-23) as **cross-player** integrity problems — and `rank_bucket` has no producer today (`docs/prd/PRD.md:696`).
3. **Cosmetic-only competitive rewards.** Cup medals affect no one else; any-rewind runs cap at Silver, enforced in Domain from the command log.
4. **No server component appears.** The register already states this conditional (`docs/prd/risks.md:147`): a hosted kill-file, the `rc-hooks` webhook host or the Stripe web funnel each re-open the entire web-injection / authn-authz / SSRF class, and each needs a **fresh threat model before code**. This document does not cover them.
5. **No LLM enters the product runtime.** Prompt injection is a *pipeline* risk here (TB-9) precisely because nothing in the shipped app interprets text as instructions. Any in-game generated content, LLM hint system or remote-copy templating changes that answer.

---

## 8. Honesty section

**Attack classes checked at design time and found adequately handled — no new mitigation needed.** Forced-ad and dark-pattern abuse of the player (CM-R19.2, CM-R25.5, CM-R26.4, CM-R32.5, CM-R36 are unusually strong and internally consistent — no exit-intent, no fake urgency, no auto-present) · PII in the share card (no username, no account; the surface is genuinely empty) · raw price and transaction-id handling in **analytics** (CM-R23.5 / CM-R45.4 are correct as written; the crash channel is the gap, AC-32/34) · daily-seed manipulation and clock tampering (CM-R11.5/.6 punish display only, never the player) · pay-to-win in competitive surfaces (command-log rewind detection with cosmetic-only rewards is the right containment with no server) · sub-ready scaffolding and hidden recurring charges (pre-refused in `docs/prd/PRD.md:929`) · classic web injection — SQL, template, SSRF, unsafe HTTP deserialization — **not applicable at 1.0**: no server, no user-supplied URL fetching, no HTML rendering of remote copy **provided M-18 holds**.

**Attack classes that could NOT be assessed from these documents, named rather than waived:**

1. **Runtime configuration** — RevenueCat entitlement attach and Targeting rules, OneSignal journey/segment/re-entry definitions (which AC-16 depends on), AdMob unit and account settings, Play Console App-content answers. Several guarantees rest entirely on them (RK-38). The artifact needed is an **export**, not an opinion.
2. **Dependencies** — SDK versions are pinned but **no advisory, transitive-tree, license or install-script check has been run**; no manifest or lockfile exists (RK-36). Nothing in this document should be read as a dependency clearance.
3. **Deployed infrastructure and repository settings** — the default `GITHUB_TOKEN` permission scope, environment protection rules, branch/ruleset state beyond what `ADR-0001` records, and secret inventory in the GitHub org. AC-35 flags what is visible in the workflow files; the settings behind them are not readable from the repo.
4. **The RevenueCat 9.7.0 API surface** — CustomerInfo refresh semantics, revocation delivery, and consumable consumption ownership are **assumed**, not verified (RK-39, A-07). If consumption is client-triggered and non-atomic with the ledger write, M-03 and M-04 change shape.
5. **Any implementation** — no app code exists. Every statement here is about a design, and **every mitigation is a proposal to be commissioned, not a control anyone has observed.**

**Escalations that remain human-only decisions**, unchanged by this document: NEW-Q45 (consent), NEW-Q46 (accepting the offline-entitlement residual), NEW-Q47 (refund-farm detection — the recommendation remains *do not implement*), NEW-Q48 (release-signing and CI secret controls). **Acceptance of any high-severity residual is recorded by the human in the PRD or an ADR — never assumed by an agent.**
