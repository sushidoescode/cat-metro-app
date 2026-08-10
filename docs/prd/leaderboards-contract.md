# Cat Metro global leaderboards — frozen Lane 4 contract draft

- **Status:** Draft; frozen as the first commit on `docs/monetization-amendment`, then corrected in a
  visibly named review-fixes commit. No dependency or implementation is approved until the human
  approves ADR-0010.
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
   premium currency, forced ads, paid Gold, or paid global rank; the existing named local rewind and
   economy-reward exceptions remain bounded exactly as specified.
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
- **Feature posture:** no build may contain PGS until ADR-0010 is human-approved and the dependency,
  Console, privacy, and Data Safety gates are complete. A PGS-capable build then has a separate local
  `leaderboard` switch for dark UI/calls; that player-writable switch is not an approval boundary.

## 3. Non-negotiable invariants

1. Campaign, Daily Line, and District Cup remain playable without PGS authentication or network.
2. Global rank grants no tickets, cosmetics, entitlement, progression, medal, offer eligibility,
   ad eligibility, or purchase benefit. Local rules remain the only reward authority.
3. A purchase, entitlement, ad watch, equipped cosmetic, or DLC ownership can never increase a
   submitted score or make an otherwise ineligible run eligible.
4. A leaderboard call never blocks Boot, Home, a level start, a result, save load, or local reward
   grant. Failure degrades to local results plus a passive retry affordance.
5. Only a completed run reproduced by the shipped deterministic Domain may enter the submission
   outbox. Bounded canonical replay evidence is retained and revalidated from an untrusted save
   immediately before every network attempt. This catches ordinary corruption/save edits; it is not
   claimed as authoritative anti-cheat.
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

The Play Console generates the actual resource IDs. `CatMetro.Application` owns only the logical
`BoardKey` and eligibility. Human-created IDs live in a baked, reviewed provider manifest owned by
`CatMetro.Integrations.PlayGames`, which maps `(BoardKey, destinationRevision)` to the exact resource.
No resource ID enters an application DTO, and no code guesses or constructs one from player input. A
missing manifest entry removes the Global Board affordance for that date/round and has no effect on
play.

### Season-1 inventory

PGS documents a maximum of 70 leaderboards per game. The candidate Season-1 envelope is **56
consecutive Daily resources + 8 Cup resources = 64**, leaving six slots for corrections. It is an
envelope, not permission to create resources. Before creation, the human signs one exact manifest
containing:

- a Monday local-date start and the 56 exact Daily `dateKey` values through the eighth Sunday;
- the eight exact Monday-through-Sunday Cup round IDs intersecting that epoch;
- one frozen scoring, simulation, Daily-generator, Cup-content, and tag-format revision; and
- every logical key, opaque `destinationRevision`, generated resource ID, ordering, unit, score bound,
  localization, and publication state.

If launch cannot align to that Monday boundary, global boards wait for the next signed epoch; local
Daily/Cup play does not wait. A ruleset or content change during the epoch requires a new physical
resource/destination revision and explicit old-client behavior; incompatible clients fail local-only.
Before Season 2, the human must verify in Play Console whether retiring/deleting Season-1 resources
releases quota. If it does not, dated Daily global boards end at the inventory boundary; the
no-own-backend ruling wins over silent resource reuse or mixed-board rankings.

Published resources are never repurposed for another date or round, because PGS scores cannot be
reset after publication. A correction uses one of the six reserve resources and a new opaque
`destinationRevision`; the old resource is hidden, never relabeled. The new destination starts a new
delivery state and requeues the retained eligible result after replay validation.

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

- Two gates are mandatory. Immutable build capability `pgs_leaderboards_compiled=false` excludes the
  Unity plugin, its Maven artifacts, generated manifest/resources, initialization, authentication,
  and network flow. Only a build that has cleared the signed dependency, exact Console-manifest,
  credential, privacy, and Data Safety **first-inclusion** gates may set it true. That enables the
  internal device evidence required before any production-capable release. Inside such a
  capable build, the player-writable runtime flag `leaderboard=false` removes Cat Metro board UI,
  adapter construction/calls, and outbox writes, but cannot promise that the included PGS v2 platform
  layer makes no launch authentication attempt. Editing the save can never enable an absent build
  capability.
- PGS v2 performs platform authentication at game launch when included. The implementation sets
  `com.google.android.gms.games.SUPPRESS_GAME_PROFILE_CREATION=true` only to suppress automatic
  profile-creation UI; it is not described as suppressing authentication or network access and must
  not block gameplay.
- The first explicit **Join global boards** tap requests any needed authentication/profile creation.
  Dismissal is final for that session; no nag, commerce substitution, or lost local result follows.
- Authenticated players see **Global board** on eligible Daily/Cup results and in the mode's history
  view. Unauthenticated, offline, missing-resource, or flag-OFF states show no rank.
- Cat Metro requests the exact resource and all-time span. Its own coarse rank read uses PGS's public
  collection. The pinned Unity native-UI bridge does not force a public default, so Google's UI owns
  and may expose its collection selector; Cat Metro does not promise which collection opens first.
  Cat Metro does not download or render other players' names in its UI. This keeps Unicode name
  rendering and identity retention inside Google's surface.
- Only the platform-auth scopes needed by PGS leaderboards are allowed. Email, profile, contacts,
  friends, Saved Games, achievements, Events, Recall, and server-side access codes are out of scope.
- Store/Data Safety copy must disclose the PGS identity and leaderboard data flow before the first
  ON build. No claim describes global rank as anonymous, verified, or cheat-proof.

## 7. Submission outbox and failure behavior

Global comparison is network-optional, not online-only play. A future ADR-0006 amendment must add a
byte-bounded `leaderboards` object to the versioned save before implementation. Conceptually:

```text
leaderboards
  eligible[boardKey] = { score, scoreTag, rulesetRevision, replayEvidence }
  delivery[boardKey] = {
    destinationRevision, pendingScore, confirmedBest,
    attemptId, attemptState, attempts, createdAtUtc
  }
```

Rules:

1. `eligible` retains the bounded canonical initial state/seed, signed board/content hashes, simulation
   and generator revisions, and complete bounded command log needed to reproduce the score. Cup
   evidence contains all three contributing clean routes. The future ADR-0006 amendment signs exact
   byte/count bounds; until then implementation is blocked.
2. Treat every deserialized row as hostile. Immediately before every adapter call, resolve the current
   signed manifest revision, reproduce the result, and re-check board/date/round, zero rewinds, score
   bounds, content/ruleset revision, and tag grammar. Invalid or obsolete evidence never reaches PGS.
   A client-held checksum or HMAC is not an authority claim.
3. One delivery row exists per logical board. A higher eligible Cup total replaces a lower pending
   total. Daily's eligible value is immutable. A manifest correction changes `destinationRevision`,
   resets `confirmedBest` for that destination, and requeues from retained eligible evidence.
4. The stock v2.2.0 Unity `SubmitScore` path is prohibited for durable delivery because it wraps
   fire-and-forget Android `submitScore`, can immediately callback true, and can emit false then true
   while unauthenticated. ADR-0010 instead approves one project-owned, security-reviewed Android bridge
   whose only PGS operation is `LeaderboardsClient.submitScoreImmediate(resourceId, score, scoreTag)`.
5. The bridge returns `Accepted` only when its Android `Task<ScoreSubmissionData>` completes
   successfully, the result is non-null/parseable, its leaderboard ID equals the requested exact
   resource, and its all-time `ScoreSubmissionResult` is present. This covers either a newly stored
   best or a previously stored better score. Task failure/cancel, auth loss, malformed/mismatched data,
   process death, timeout, or no callback is **not accepted** and leaves the row pending; none is
   converted into a permanent drop. No cached wrapper read is used as acknowledgement.
6. Every attempt has an immutable `(boardKey, destinationRevision, score, attemptId)` token. On
   immediate-task acceptance, set `confirmedBest=max(existing, attemptedScore)` and clear pending only with a
   compare-and-swap proving the current row still matches that token or is not newer. Duplicate,
   contradictory, and late callbacks cannot clear or downgrade newer state.
7. Retry runs only on authenticated foreground or an explicit result-screen retry: at most
   four destinations per foreground cycle and one in-flight attempt per destination. There is no
   timer, Boot retry loop, or unbounded batch. One passive status line per session may say a score is
   waiting; it never claims upload success before immediate-task acceptance.
8. The outbox/evidence set is capped at the 64 signed Season-1 logical boards. Reaching the cap refuses
   new global submissions loudly but never touches local results or rewards. No separate file is
   introduced; ADR-0006's durable-file inventory remains closed.

The diagnostic `scoreTag` has one canonical ASCII grammar:

```text
Daily: v1-d-{YYYYMMDD}-{simToken}-{generatorToken}-{replayHash16}
Cup:   v1-c-{roundToken}-{simToken}-{generatorToken}-{replayHash16}
```

`YYYYMMDD` is eight digits; `roundToken` is the signed manifest alias at 1–16 characters;
`simToken` and `generatorToken` are signed 1–8-character tokens; and `replayHash16` is exactly 16
lowercase hex characters. Variable tokens use only the delimiter-free RFC 3986 unreserved subset
`[A-Za-z0-9._~]`; `-` is the delimiter. The generated maximum is 56 characters, and the adapter
independently rejects any tag over PGS's 64-character limit. Tags contain no player identifier and
are never a filtering, authorization, or verification mechanism.

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

1. **Two-gate absence:** with `pgs_leaderboards_compiled=false`, the built artifact contains no PGS
   plugin/Maven/manifest/resource surface and device proxy capture shows no PGS auth/network. In an
   approved capable build with `leaderboard=false`, Cat Metro constructs no adapter, board UI, PGS API
   call, outbox write, or `rank_bucket`; platform-level authentication remains disclosed. A hostile
   save that sets `leaderboard=true` cannot enable a capability-OFF build. L001, Daily, and Cup still
   complete normally in both cases.
2. **Boot independence:** auth pending/failure/exception cannot delay Home or throw on Boot.
3. **Exact-board mapping:** two adjacent `dateKey` values and two Cup round ids resolve to four
   distinct baked resource IDs; missing keys fail closed to local-only.
4. **Daily eligibility:** practice, rewind, suspicious-clock, failed, and replay-mismatch fixtures
   produce zero submissions; one clean scoring completion produces exactly one.
5. **Cup anti-P2W:** a purchased-, rewarded-, and free-rewind fixture each remains locally valid but
   contributes zero to global total; three clean personal bests submit their exact sum.
6. **Outbox/immediate-submit durability:** the stock wrapper is statically unreachable; offline task
   failure, unauthenticated/malformed/duplicate bridge callbacks, process death around every
   save/submit/task-completion boundary, a lower attempt
   completing after a higher Cup best, and a destination correction preserve the newest row and no
   duplicate local reward. Only validated `submitScoreImmediate` Task success confirms delivery;
   retries obey the four-destination/one-in-flight bounds.
7. **Identity isolation:** static checks find PGS SDK namespaces only in
   `CatMetro.Integrations.PlayGames`/Bootstrap and no player id/name in analytics, save, diagnostics,
   messaging, purchases, or share-card DTOs.
8. **Hostile-save/tag tests:** forged score/evidence/destination rows fail before the adapter; canonical
   maximum tags pass and illegal characters fail. Independent raw SDK-bound fixtures pass the length
   guard at 63 and 64 URI-safe characters and fail at 65; noncanonical tags still fail the application
   grammar before dispatch.
9. **Dependency/bridge gate:** exact wrapper artifact/hash/full commit, direct Games v2 and Nearby
   Maven pins, terms/license/SCA and Nearby manifest/Data Safety audit, one EDM4U resolution,
   minSdk/targetSdk, 16-KB page-size audit, and R8-minified build all pass. The project-owned bridge's
   reviewed source/hash exposes only immediate submit; Play-distributed auth, offline/failure,
   immediate-task success, existing-better-score, process-death, and open-board smokes pass.
10. **Console gate (human evidence):** exact Monday-aligned 56-Daily/eight-Cup manifest + six reserves,
    or a smaller explicitly signed epoch; one frozen ruleset; correct ordering/units/limits; tamper
    protection ON; test accounts removed before publication; game-service project published alongside
    the app.

## 11. Implementation blockers retained intentionally

- Human approval of ADR-0010 and its dependency pin.
- A security-reviewed implementation spike of ADR-0010's exact project-owned
  `submitScoreImmediate` bridge, with its source hash and generated Android diff recorded. Any extra
  PGS operation, wrapper/fork, or failure to prove Task semantics keeps the build capability OFF and
  requires an ADR amendment.
- Human resolution/implementation of the scoring constants and maximum-score derivation.
- ADR-0006 amendment for canonical replay evidence, destination-aware delivery/CAS state, and the
  exact byte bound, plus expansion of ad-cap counters from the separate monetization amendment.
- Analytics taxonomy amendment for any leaderboard events or `rank_bucket` producer.
- Data Safety/store-copy review for PGS identity and public score display.
- A human-created Play Games Services project, credentials, leaderboard resources, translations,
  icons, score limits, test accounts, exact signed epoch manifest, and publication.

No blocker above weakens the local Daily Line or District Cup. If any remains open, the feature flag
stays OFF and the game ships without global rank.

## 12. Source notes

Official Google documentation checked 2026-08-09:

- [Google Play Games plugin for Unity](https://developer.android.com/games/pgs/unity/overview)
- [PGS leaderboard concepts and limits](https://developer.android.com/games/pgs/leaderboards)
- [Leaderboards in Unity games](https://developer.android.com/games/pgs/unity/leaderboards)
- [Unity setup and authentication](https://developer.android.com/games/pgs/unity/unity-start)
- [Android `LeaderboardsClient` submission/read APIs and score-tag bound](https://developers.google.com/android/reference/com/google/android/gms/games/LeaderboardsClient)
- [Pinned v2.2.0 `SubmitScore` implementation](https://github.com/playgameservices/play-games-plugin-for-unity/blob/c6f19addceb9a87489c5f1fb0d50bb4bef1e9c7a/Assets/Public/GooglePlayGames/com.google.play.games/Runtime/Scripts/Platforms/Android/AndroidClient.cs#L1054-L1080)
- [Official plugin releases](https://github.com/playgameservices/play-games-plugin-for-unity/releases)
