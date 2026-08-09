# ADR-0010: Google Play Games Services for rewardless global leaderboards

- **Status:** Proposed — human sponsorship recorded 2026-08-09; exact dependency and architecture
  decision remain **PENDING HUMAN SIGNATURE** below
- **Date:** 2026-08-09
- **Relates:** ADR-0002 (deterministic replay), ADR-0003 (assembly isolation), ADR-0004 (dependency
  pins), ADR-0006 (durable save), ADR-0007 (feature flags), ADR-0009 (CI),
  `docs/prd/leaderboards-contract.md` (frozen behavioral contract)

## Context

Daily Line and District Cup already exist as offline-first, deterministic modes. Their local results,
streaks, medals, participation liveries, and rewards do not need a server. The 2026-08-09 human ruling
adds optional global comparison for both modes and explicitly rejects a Cat Metro-operated backend.

The platform decision cannot be left to an implementer. A leaderboard provider introduces identity,
network traffic, Play Console resources, Android manifest/dependency changes, cheating limits, service
availability, quota, moderation, and a new vendor namespace. It also collides with two existing
architecture contracts:

- ADR-0003 fixes the integration assemblies and allows only Bootstrap to reference them; it does not
  name a leaderboard boundary.
- ADR-0004 fixes the complete Android SDK/EDM4U set; it does not contain Play Games Services (PGS).

PGS is materially different from an owned backend. Google owns authentication, public score storage,
rank computation, native leaderboard UI, and entry-management tools. Cat Metro remains responsible for
deciding which local runs are eligible, choosing the exact board, bounding scores, retrying safely, and
being honest that a client-only game cannot provide authoritative anti-cheat.

## Decision

We will use the official **Google Play Games plugin for Unity v2.2.0**, release commit `c6f19ad`, for
optional, rewardless global Daily Line and District Cup leaderboards on Android. The dependency is
Apache-2.0 licensed. We will not operate a score backend, request server auth, or adopt any other PGS
feature under this ADR.

This is an exact pin, not “latest.” The release raises the plugin's minimum Android API to 24; Cat
Metro's ADR-0004 minSdk 25 remains the stricter floor. Before import, the implementation PR records the
downloaded release artifact name and SHA-256, the full resolved Maven tree, license/SCA result, generated
manifest diff, and whether the artifact uses a bundled or shared EDM4U. Exactly one EDM4U 1.2.188 copy
must remain.

### 1. Architecture amendment and isolation boundary

ADR-0003 is amended, once this ADR is approved, by one SDK-specific assembly and one engine-free
service interface:

| Assembly/interface | Owns | May reference |
|---|---|---|
| `CatMetro.Services.ILeaderboards` + POCO DTOs | authentication state, board key, submit/show request and typed result contracts | Domain/Content POCOs only; no PGS or Unity types |
| `CatMetro.Integrations.PlayGames` | the sole adapter from `ILeaderboards` to the PGS Unity API | `CatMetro.Services`, PGS plugin, Unity |
| `CatMetro.Application` | score eligibility, deterministic replay check, board-manifest lookup, bounded durable outbox, retry policy | existing inward dependencies + `ILeaderboards`; never PGS |
| `CatMetro.Bootstrap` | feature-flagged adapter construction and lifecycle forwarding | existing graph + `CatMetro.Integrations.PlayGames` |
| `CatMetro.Presentation` | local-only/Join/Global-board affordances using application state | Application/Services; never PGS |

No PGS type, resource ID, namespace, callback, status code, player identifier, or display name may
escape `CatMetro.Integrations.PlayGames`/Bootstrap. Static checks fail any PGS namespace elsewhere.
Application uses typed local results such as authenticated, cancelled, offline, missing-resource,
retriable, and permanent-failure; it never branches on vendor integer/string codes.

The addition explicitly changes ADR-0003's effectively permanent assembly list. That cost is accepted
because merging PGS into Analytics, Ads, or a generic integration assembly would enlarge the trust and
failure blast radius. No code may create the assembly until this ADR is approved.

### 2. Product boundary

The frozen contract at `docs/prd/leaderboards-contract.md` is normative. In summary:

- Local Daily Line and District Cup remain fully playable, scoreable, rewardable, and reviewable with
  the leaderboard flag OFF, no PGS profile, no network, or a total PGS outage.
- Global rank is public bragging-rights display only. It grants no reward, entitlement, progression,
  medal, cosmetic, ticket, price, cohort, offer, ad, or winner status.
- A global-eligible Daily result is the one scoring play for its exact local `dateKey`, with zero
  rewinds and a successful deterministic replay check. Practice runs never submit.
- A global-eligible Cup total is the sum of three clean per-route personal bests for the exact round.
  Any free, rewarded, or purchased rewind keeps its existing local result but excludes that run from
  the global total. Paid convenience can therefore never buy rank.
- Local rewards commit before any network call and are never rolled back by auth or submission state.

PGS score submission is best-score/idempotent from the app's perspective. Application owns the
bounded outbox and `submittedBest`; the adapter performs one request and returns one typed result. PGS
callbacks never mutate the save or grant a reward directly.

### 3. Board-resource topology

PGS supplies daily, weekly, and all-time views for each leaderboard, but its fixed reset windows do not
match Cat Metro's local-date Daily or Monday-17:00-local Cup contract. We will therefore publish one PGS
leaderboard resource per dated Daily and per Cup round and use that resource's **all-time** view.

| Local board key | PGS Console resource | Value | Order |
|---|---|---|---|
| `daily_YYYYMMDD` | exact baked resource ID for that local `dateKey` | final integer score of the sole clean scoring play | larger is better |
| `cup_<round_id>` | exact baked resource ID for that round | sum of the three clean route bests | larger is better |

The manifest is authored and reviewed; it never constructs a Console resource ID from player input.
Missing entries fail closed to local-only. PGS ordering cannot be changed after publication, so a human
verifies larger-is-better, localized points, and score limits before publishing any resource.

PGS documents a maximum of 70 leaderboards per game. The Season-1 ceiling is 56 dated Daily resources
+ 8 Cup resources = 64, leaving six correction resources. Published boards are never reused for a
different date/round because their scores cannot be reset. Before Season 2, the human verifies whether
retiring/deleting resources restores quota. If quota cannot support the next season, global dated
Daily boards stop; Cat Metro does not silently mix dates or introduce an owned backend.

### 4. Authentication and native UI

`leaderboard=false` means no PGS initialization, auth attempt, UI, call, or save mutation. When the
flag is ON, existing-profile authentication may be attempted without blocking Boot. The implementation
sets `com.google.android.gms.games.SUPPRESS_GAME_PROFILE_CREATION=true`, supported by the official
plugin line, so a player without a PGS profile is not interrupted at startup.

Profile creation/authentication for a new participant begins only after an explicit **Join global
boards** tap. Cancel means local-only for that session, with no nag or commerce replacement. No auth
result can delay Home, level start, result presentation, local save, or local reward.

Cat Metro opens Google's native leaderboard UI for the exact resource and its public/all-time view.
It does not download or render other players' names, avatars, or profiles in Cat Metro UI. This keeps
identity presentation, Unicode handling, blocking/reporting, and public-rank retention on Google's
surface. The app may read the authenticated player's own rank/count only to display it locally and,
after a separately approved analytics amendment, derive the coarse `rank_bucket` described in the
contract. Exact rank and identity are never logged.

### 5. Allowed PGS surface and data flow

Allowed:

- Android platform authentication needed for PGS leaderboards;
- submit a bounded numeric score and a non-authoritative short score tag to one manifest-allowed board;
- open the exact native leaderboard UI;
- read the authenticated player's result/rank for that exact board; and
- human Console moderation, score limits, test accounts, and tamper protection.

Explicitly out of scope:

- achievements, Events, Game Stats, Saved Games, Recall, quests, multiplayer, friends/social lists,
  player search, email/profile/contacts scopes, server auth codes, web OAuth credentials, and service
  accounts;
- a Firebase/Cloud Function/UGS/custom score mirror, webhook, admin writer, or rank cache;
- sending PGS identity or score/rank to RevenueCat, AdMob, OneSignal, Crashlytics, analytics, the
  ordinary save, a share card, or a Cat Metro server; and
- using rank to verify skill, select a winner, or grant anything of value.

The outbound submission contains the PGS-authenticated account context handled by Google, board
resource ID, numeric score, and a short score tag limited to mode, date/round ID, sim/generator
version, and replay-hash prefix. It contains no Cat Metro free text or separate player identifier.
The exact production data flow, retention wording, consent/age posture, privacy policy, and Play Data
Safety declarations require human review before the feature turns ON.

### 6. Integrity posture

The client accepts a score into its outbox only after deterministic re-simulation reproduces the
completion and value. Per-board lower/upper limits are derived from ratified scoring constants and
authored route bounds. PGS leaderboard tamper protection is ON for every published resource.

These controls catch corruption and crude abuse; they are not authoritative anti-cheat. A modified
client can bypass local replay because no trusted Cat Metro server attests the run. That is why rank is
rewardless, why the app never calls a player “verified,” and why there is no prize competition based on
PGS rank. Suspicious entries are handled by the human with PGS tools, not by an unreviewed client
blocklist.

### 7. Failure and lifecycle policy

- Network/auth/service errors return local results immediately and leave at most one eligible score per
  board in the bounded outbox.
- Retry occurs only on authenticated foreground or explicit result-screen retry; there is no timer,
  boot loop, exponential background worker, or retry storm.
- Missing/malformed board manifest, out-of-range score, replay mismatch, wrong mode/date/round, and
  rewind-used states fail closed before the adapter.
- Pause/process death cannot duplicate local rewards. A future human-approved ADR-0006 migration owns
  outbox durability; the PGS adapter owns no file.
- Offline/auth-failed UI never shows a stale value as current global rank. A passive status may say the
  eligible score is waiting to submit, without implying success.
- PGS outage, deprecation, or feature rollback is one flag flip to local-only. Existing local history
  remains usable.

### 8. Exact dependency pin and unpin trigger

| Item | Decision |
|---|---|
| Package | official Google Play Games plugin for Unity |
| Pin | **v2.2.0**, release commit **`c6f19ad`** — **PENDING HUMAN SIGNATURE** |
| License | Apache License 2.0; retain the license/required notices in the shipped third-party notices |
| Direct package fee | none; Google Play/PGS program terms and operational lock-in still apply |
| Android compatibility | plugin minSdk 24; project minSdk 25 / targetSdk 36 remain authoritative |
| Resolver rule | use the project's single EDM4U 1.2.188; duplicate resolver copies fail the import gate |
| Unpin trigger | a security advisory, PGS/plugin deprecation requirement, Play policy change, or proven incompatibility with the pinned Unity/Gradle/AGP/minSdk/targetSdk/16-KB/R8 stack **and** a green clean-import + build + Play-distributed device smoke on the proposed replacement |

An upstream release existing is not an unpin trigger. The candidate must pass the same license/SCA,
dependency-tree, manifest, 16-KB native-library, minified IL2CPP, auth, submit, native-UI, offline, and
rollback evidence as this pin.

## Alternatives seriously considered

- **Own a minimal score backend.** Real advantages: authoritative resource lifecycle, local-date
  windows without 70-resource pressure, custom moderation, and a future server-verification path.
  Lost because it violates the explicit no-own-backend ruling and adds account security, availability,
  abuse, privacy deletion, on-call, and spend obligations far beyond a solo offline-first game.
- **Firebase/Cloud Functions or Unity Gaming Services leaderboards.** Real advantages: managed storage,
  more flexible board cadence, cross-platform expansion, and less Console resource churn. Lost because
  it is still another backend/data processor and dependency, does not use the requested Play-native
  identity/UI, and expands the operational and privacy surface without removing client-trust limits.
- **One rolling PGS leaderboard using its daily/weekly views.** Real advantages: tiny resource count and
  near-zero seasonal Console work. Lost because PGS reset windows do not equal Cat Metro's local-date
  and local-time event windows; players would be ranked against a different competition than the UI
  promises.
- **One PGS resource reused with score tags.** Real advantages: avoids the resource limit and keeps one
  public surface. Lost because tags are not an authorization/filtering boundary and published scores
  cannot be reset; dates/rounds would mix.
- **No global boards.** Real advantages: zero dependency, identity, cheating, quota, or network work;
  the existing local modes already function. Lost because the human explicitly accepted optional
  global comparison and PGS gives it without an owned backend. This remains the feature-flag fallback.

## Consequences

**Easier:** Cat Metro gets Android-native account/rank UI, public leaderboards, and human moderation
without operating a service. Global comparison remains a replaceable adapter, while Domain and local
mode rules stay deterministic, engine-free, and offline-first.

**Harder:** each season needs pre-created, translated, validated Console resources and quota
accounting. Authentication and Play-distributed test builds enter the device matrix. Data Safety/store
copy gains a Google Games identity/score flow. False confidence in anti-cheat must be actively resisted.
The new asmdef and service interface amend a deliberately stable architecture.

**Lock-in:** published PGS resource IDs, score ordering, and stored scores are hard to reverse; Android
players' public history stays with the Play Games project. Migrating providers would start new global
boards and require a new ADR. Local scores/rewards remain provider-independent, limiting the damage.

**Spend/license:** the plugin is Apache-2.0 and has no direct package fee. Engineering/operations cost
comes from Console setup, device testing, moderation, compliance, and future compatibility work.

**Monetization separation:** PGS implementation is not itself monetization. Nevertheless, it may not be
used as a vehicle to co-land billing/IAP/ad/payment code. Any monetization code anywhere still requires
a prior human-authored `state/mode=production` commit.

## Security notes

1. PGS is a new external identity and score-processing trust boundary. Treat every callback, score,
   resource ID, display field, and status as untrusted data; bound lengths/numbers and map to typed DTOs.
2. Client replay validation cannot secure a client-controlled score. Never attach monetary or in-game
   value to rank; never describe it as authoritative.
3. A baked allowlist prevents a remote/string injection from selecting an arbitrary leaderboard.
   Console score limits and tamper protection reduce blast radius but do not replace the allowlist.
4. OAuth client identifiers and Android resources may ship as expected public configuration. Client
   secrets, service-account keys, server auth codes, refresh tokens, and web OAuth credentials are
   forbidden from the repository and binary.
5. Native PGS UI contains external player-controlled names/avatars. Cat Metro neither renders nor logs
   them, preventing spoofed UI strings, PII leakage, and share-card contamination in our surface.
6. Authentication cancellation, service outage, malformed callback, and task death are ordinary states,
   not exceptions that may block Boot or corrupt save/reward state.
7. Dependency import requires advisory, transitive dependency, install/editor-script, Android manifest,
   native-library, and license review. Generated dependency changes are reviewed, not accepted as noise.

## Approval and implementation gates

Approval of this ADR authorizes the exact architectural choice and dependency pin, not an immediate
import. A future implementation contract must provide failing tests for every criterion in the frozen
leaderboard contract and clear all of these gates:

1. human signature below; ADR-0003/ADR-0004/config pin records updated in the implementation PR;
2. clean throwaway import with artifact SHA-256, one EDM4U, resolved tree, SCA/license, install-script,
   manifest, ProGuard/R8, and native-library/16-KB diffs reviewed;
3. human-approved ADR-0006 migration for the bounded outbox and full kill-during-write fixtures;
4. scoring constants/max bounds resolved before any Console resource is published;
5. exact-board, zero-rewind, replay-mismatch, offline, auth-cancel, missing-resource, retry, identity-
   isolation, and flag-absence automated tests;
6. human-created Play Games project, credentials, 64+6-or-smaller signed resource inventory,
   translations, units/order/limits, tamper protection, and tester cleanup;
7. Play-distributed, correctly signed device smoke for existing profile, no profile, cancel, offline,
   submit/update/open native UI, reinstall, and rollback on the low-tier device and Pixel;
8. privacy policy, Data Safety, store copy, age/consent, support, and moderation checklist approved; and
9. leaderboard feature OFF by default until all evidence is attached.

### Human signature

- [ ] I approve PGS as the sole remote leaderboard/identity surface, with no Cat Metro backend.
- [ ] I approve official plugin **v2.2.0 / `c6f19ad`** under Apache-2.0 and its named unpin trigger.
- [ ] I approve the new `CatMetro.Integrations.PlayGames` assembly and `ILeaderboards` boundary.
- [ ] I approve the 64-resource Season-1 plan and the no-backend/no-resource-reuse fallback.
- [ ] I accept rewardless, best-effort client integrity; PGS rank will never award value.

- **Signed by:** _PENDING HUMAN SIGNATURE_
- **Signed at (absolute date/time):** _PENDING HUMAN SIGNATURE_
- **Signing commit:** _PENDING HUMAN SIGNATURE_

## Official sources checked 2026-08-09

- [PGS Unity plugin overview](https://developer.android.com/games/pgs/unity/overview)
- [PGS Unity setup and authentication](https://developer.android.com/games/pgs/unity/unity-start)
- [PGS leaderboards concepts, limits, reset windows, and publication behavior](https://developer.android.com/games/pgs/leaderboards)
- [PGS leaderboards in Unity](https://developer.android.com/games/pgs/unity/leaderboards)
- [Official plugin v2.2.0 release](https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v2.2.0)
- [Official v2.2.0 Apache-2.0 license](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.0/LICENSE)
