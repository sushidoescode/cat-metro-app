# Cat Metro global leaderboards — frozen Lane 4 contract draft

- **Status:** Draft; frozen as the first commit on `docs/monetization-amendment`. No dependency or
  implementation is approved until the human approves ADR-0010.
- **Date:** 2026-08-09
- **Decision owner:** human
- **Implementation owner:** unassigned; this lane is documentation only
- **Relates:** `docs/plan/specs/liveops_spec.md` §§1–2, `docs/plan/specs/product_spec.md` §§18–19,
  `docs/prd/PRD.md` CM-R47/CM-R48/U-6, ADR-0002, ADR-0003, ADR-0006, ADR-0007, and proposed ADR-0010

## 1. Frozen Lane 4 scope

The 2026-08-09 human ruling accepts two directions:

1. expand Model B into a deep, fair-core catalog of cosmetic microtransactions, optional rewarded
   ads, and one-time DLC district packs; and
2. add global Daily Line and District Cup boards through Google Play Games Services (PGS), with no
   Cat Metro backend.

This lane delivers exactly four documentation artifacts:

- an amendment appended to `docs/plan/specs/monetization_spec.md`;
- one proposed ADR for the PGS Unity dependency and leaderboard boundary;
- this leaderboard contract draft; and
- one art-reference note for the two 2026-08-09 concept renders.

No code, package manifest, content, test, state, existing ADR, architecture overview, price, SKU, or
Play Console object is changed by this lane. The first three later artifacts must cite this frozen
contract. Review corrections may amend it only in a later, visibly named commit; the first-commit
blob remains the review anchor.

### Lane acceptance criteria

1. The monetization amendment specifies cat skins, train liveries, seasonal themes, one-time DLC
   district packs following the Night Harbor pattern, expanded rewarded placements, and a
   RevenueCat Experiments plan. It preserves the fair core: no energy, loot boxes, subscriptions,
   premium currency, forced ads, or paid gameplay power.
2. Every proposed product ID and every proposed reference price is visibly marked **PENDING HUMAN
   SIGNATURE**. The amendment does not silently overwrite the locked v1 catalog.
3. ADR-0010 names the exact proposed PGS Unity dependency/version, license, isolation boundary,
   authentication posture, data flow, operational limits, and unpin trigger. Its status remains
   Proposed until the human signs it.
4. This contract defines Daily Line and District Cup global-board eligibility, score ownership,
   offline/auth failure behavior, anti-pay-to-win rules, tamper limits, and testable absence rules.
5. The art-reference note identifies both local source files by filename, dimensions, and SHA-256;
   calls the Gemini render the implementation reference and the ChatGPT render a secondary
   mood/material reference; and keeps `product_spec.md` palette values and color-plus-symbol rules
   authoritative.
6. Every artifact states the hard tripwire: a human-authored commit must change `state/mode` to
   `production` before any monetization code (billing, IAP, ads, payments, or equivalent paths)
   merges anywhere. These docs do not satisfy or bypass that gate.

## 2. Authority and conflict posture

The leaderboard feature adds comparison; it does not redefine either game mode.

- **Daily Line authority:** `liveops_spec.md` §1. It remains one deterministic board per **local
  dateKey**, playable offline, with one scoring play and unlimited marked practice. This contract
  does not adopt `product_spec.md` §18's older UTC-date wording.
- **District Cup authority:** `liveops_spec.md` §2. It remains three weekly routes, static
  solver-calibrated medals, a participation livery, and the existing rewind-capped-at-Silver local
  rule. This contract does not choose PRD NEW-Q23's static-versus-percentile branch; global PGS rank
  is a separate, rewardless display and works with either eventual local medal ruling.
- **No-backend meaning:** Cat Metro operates no account, score, or content server. PGS is the sole
  remote score store and identity surface. RevenueCat never stores or resolves leaderboard scores.
- **Feature posture:** `leaderboard` remains OFF until ADR-0010 is human-approved and the future
  implementation passes its dependency, device, privacy, and failure-mode gates.

## 3. Non-negotiable invariants

1. Campaign, Daily Line, and District Cup remain playable without PGS authentication or network.
2. Global rank grants no tickets, cosmetics, entitlement, progression, medal, offer eligibility,
   ad eligibility, or purchase benefit. Local rules remain the only reward authority.
3. A purchase, entitlement, ad watch, equipped cosmetic, or DLC ownership can never increase a
   submitted score or make an otherwise ineligible run eligible.
4. A leaderboard call never blocks Boot, Home, a level start, a result, save load, or local reward
   grant. Failure degrades to local results plus a passive retry affordance.
5. Only a completed run reproduced by the shipped deterministic Domain may enter the submission
   outbox. The replay check detects ordinary corruption; it is not claimed as anti-cheat.
6. PGS player identity, display names, and ranks never enter analytics, crash reports, OneSignal,
   RevenueCat, the save, or share-card images.
7. No custom rank service, Cloud Function, Firebase score table, server OAuth exchange, or admin
   score writer exists. Adding one requires a new ADR and threat-model update.

## 4. Board topology

PGS automatically creates daily, weekly, and all-time views for every leaderboard, but its daily
and weekly resets use fixed UTC−7 windows. Those windows do not match Cat Metro's local-date Daily
Line or Monday-17:00-local Cup windows. Comparing different generated boards under one PGS reset
window would be false competition. Therefore Cat Metro uses a distinct PGS resource for every
dated Daily and every Cup round, and reads its **all-time** view.

| Mode | PGS resource key in the baked manifest | Score | Ordering | Lifetime |
|---|---|---|---|---|
| Daily Line | `daily_YYYYMMDD` for the exact local `dateKey` | the existing scoring play's final integer score | larger is better | immutable historical board for that date |
| District Cup | `cup_<round_id>` | sum of the three eligible per-route personal bests | larger is better | immutable historical board for that round |

The Play Console generates the actual resource IDs. Human-created IDs are copied into a baked,
reviewed manifest; game code never guesses or constructs an ID from player input. A missing manifest
entry removes the Global Board affordance for that date/round and has no effect on play.

### Season-1 inventory

PGS documents a maximum of 70 leaderboards per game. Season 1 therefore reserves **56 consecutive
Daily resources + 8 Cup resources = 64**, leaving six slots for correction resources. The exact
Season-1 start date is the first production date on which `leaderboard` ships ON. Before Season 2,
the human must verify in Play Console whether retiring/deleting Season-1 resources releases quota.
If it does not, dated Daily global boards end at the inventory boundary; the no-own-backend ruling
wins over silent resource reuse or mixed-board rankings.

Published resources are never repurposed for another date or round, because PGS scores cannot be
reset after publication. A correction uses one of the six reserve resources and a manifest update;
the old resource is hidden, never relabeled.

## 5. Eligibility and score contracts

### 5.1 Daily Line

A Daily score is globally eligible only when all of the following are true:

- the run is the one scoring play for the exact local `dateKey`; practice runs never submit;
- the run completed successfully under the current, version-pinned Daily generator;
- the command log contains **zero rewinds**;
- the existing monotonic-clock guard did not suppress rank/share for a backward or greater-than-two-
  day jump;
- deterministic local re-simulation reproduces the completion and final score; and
- a baked PGS resource exists for that exact `dateKey`.

The first eligible completion is placed in the outbox. Because later plays are practice by existing
product law, they cannot replace it. Daily rewards and streak progression commit locally before any
PGS call and never roll back on submission failure.

### 5.2 District Cup

A Cup score is globally eligible only when:

- all three routes in the round have an eligible completed run;
- each contributing personal best was completed with **zero rewinds**;
- each run re-simulates to the same completion and score; and
- the exact `cup_<round_id>` resource exists.

The submitted value is `best(route_1) + best(route_2) + best(route_3)`. Improving any clean route
recomputes and resubmits the total; PGS retains the better score. A run using a free, rewarded, or
purchased rewind can still earn the local result allowed by `liveops_spec.md` (at most Silver), but
it never contributes to global rank. Thus paid rewinds cannot buy position even indirectly.

### 5.3 Bounds and formatting

Both boards use PGS numeric `long` scores with a localized `point/points` unit and larger-is-better
ordering. Once published, PGS ordering is immutable; Console creation therefore stays a human gate.
Lower and upper score limits are derived from the ratified scoring constants plus each board's
authored delivery/time bounds. No leaderboard is published while scoring pin NEW-Q5/Q-C or its
maximum-score derivation remains open.

## 6. Authentication, UI, and privacy

- The PGS Unity plugin's automatic connection may run only behind the `leaderboard` flag, but it
  must not block gameplay. The implementation sets
  `com.google.android.gms.games.SUPPRESS_GAME_PROFILE_CREATION=true` so a player without a Games
  profile reaches Home without a profile-creation interruption.
- The first explicit **Join global boards** tap invokes manual PGS authentication/profile creation.
  Dismissal is final for that session; no nag, commerce substitution, or lost local result follows.
- Authenticated players see **Global board** on eligible Daily/Cup results and in the mode's history
  view. Unauthenticated, offline, missing-resource, or flag-OFF states show no rank.
- The app opens the native PGS UI for the exact resource and its all-time/public view. It does not
  download or render player names in Cat Metro UI. This keeps Unicode name rendering and identity
  retention inside Google's surface.
- Only the platform-auth scopes needed by PGS leaderboards are allowed. Email, profile, contacts,
  friends, Saved Games, achievements, Events, Recall, and server-side access codes are out of scope.
- Store/Data Safety copy must disclose the PGS identity and leaderboard data flow before the first
  ON build. No claim describes global rank as anonymous, verified, or cheat-proof.

## 7. Submission outbox and failure behavior

Global comparison is network-optional, not online-only play. A future ADR-0006 amendment must add a
bounded `leaderboards` object to the versioned save before implementation:

```text
leaderboards
  pending[boardKey] = { score, scoreTag, createdAtUtc, attempts }
  submittedBest[boardKey] = score
```

Rules:

1. One pending row per board key; a higher eligible Cup total replaces a lower pending total.
2. Daily has one scoring result, so its row is immutable after creation.
3. Retry only on authenticated app foreground or an explicit result-screen retry; no timer and no
   retry loop on Boot.
4. A successful callback moves the value to `submittedBest`. A failure leaves it pending and shows
   at most one passive status line per session.
5. The outbox is capped at the 64 Season-1 resources. Reaching the cap refuses new global
   submissions loudly but never touches local results or rewards.
6. No separate file is introduced: ADR-0006's durable-file inventory remains closed. No code lands
   until the save migration and tests are approved.

The `scoreTag` is diagnostic metadata only: mode, exact date/round id, sim/generator version, and a
short replay-hash prefix, with no player identifier. PGS score tags are not treated as a filtering
or authorization mechanism.

## 8. Rank display and `rank_bucket`

Native PGS rank is the source of truth for the public board. If a future analytics amendment keeps
`daily_completed.rank_bucket`, the only permitted producer is a successful PGS read for the exact
dated resource:

```text
percentile = 100 * playerRank / approximatePublicScoreCount
bucket     = top_10 | top_25 | top_50 | participant
```

Invalid/missing rank, count < 1, unauthenticated play, or a pending submission produces no bucket;
the client never guesses from local par tables. Exact rank, player id, and display name are never
logged. This definition resolves the former "no producer" shape without making analytics a gameplay
dependency; the taxonomy change itself is outside this lane.

## 9. Integrity and moderation posture

- Enable PGS leaderboard tamper protection before publication and verify the switch in the human
  Console checklist. Enable plausible per-board score limits once the scoring range is ratified.
- Client-side replay validation, score limits, and PGS tamper protection reduce accidental or crude
  abuse; none creates authoritative verification. A modified client can still lie because Cat Metro
  has no trusted score server.
- Consequently rank is bragging-rights display only. It never selects a winner, reward, cohort,
  price, paywall, ad, entitlement, or moderation outcome.
- Suspicious entries are handled with PGS's hide/tamper tools by the human. Cat Metro stores no
  blocklist and exposes no report endpoint.

## 10. Future implementation acceptance criteria

1. **Flag absence:** with `leaderboard=false`, no board UI, auth attempt, PGS API call, outbox write,
   or `rank_bucket` exists; L001, Daily, and Cup still complete normally.
2. **Boot independence:** auth pending/failure/exception cannot delay Home or throw on Boot.
3. **Exact-board mapping:** two adjacent `dateKey` values and two Cup round ids resolve to four
   distinct baked resource IDs; missing keys fail closed to local-only.
4. **Daily eligibility:** practice, rewind, suspicious-clock, failed, and replay-mismatch fixtures
   produce zero submissions; one clean scoring completion produces exactly one.
5. **Cup anti-P2W:** a purchased-, rewarded-, and free-rewind fixture each remains locally valid but
   contributes zero to global total; three clean personal bests submit their exact sum.
6. **Outbox durability:** process death before/after the save commit and before/after the PGS
   callback yields no duplicate local reward and eventually at most one best score per board.
7. **Identity isolation:** static checks find PGS SDK namespaces only in
   `CatMetro.Integrations.PlayGames`/Bootstrap and no player id/name in analytics, save, diagnostics,
   messaging, purchases, or share-card DTOs.
8. **Dependency gate:** exact plugin pin, license/SCA inventory, one EDM4U resolution, minSdk/targetSdk,
   16-KB page-size audit, R8-minified build, and install/auth/submit/open-board smoke all pass on a
   Play-distributed test build signed with the configured certificate.
9. **Console gate (human evidence):** 64 Season-1 resources + six reserves or a smaller explicitly
   signed inventory; correct ordering/units/limits; tamper protection ON; test accounts removed before
   publication; game-service project published alongside the app.

## 11. Implementation blockers retained intentionally

- Human approval of ADR-0010 and its dependency pin.
- Human resolution/implementation of the scoring constants and maximum-score derivation.
- ADR-0006 amendment for the save migration/outbox, plus expansion of ad-cap counters from the
  separate monetization amendment.
- Analytics taxonomy amendment for any leaderboard events or `rank_bucket` producer.
- Data Safety/store-copy review for PGS identity and public score display.
- A human-created Play Games Services project, credentials, leaderboard resources, translations,
  icons, score limits, test accounts, and publication.

No blocker above weakens the local Daily Line or District Cup. If any remains open, the feature flag
stays OFF and the game ships without global rank.

## 12. Source notes

Official Google documentation checked 2026-08-09:

- [Google Play Games plugin for Unity](https://developer.android.com/games/pgs/unity/overview)
- [PGS leaderboard concepts and limits](https://developer.android.com/games/pgs/leaderboards)
- [Leaderboards in Unity games](https://developer.android.com/games/pgs/unity/leaderboards)
- [Unity setup and authentication](https://developer.android.com/games/pgs/unity/unity-start)
- [Official plugin releases](https://github.com/playgameservices/play-games-plugin-for-unity/releases)
