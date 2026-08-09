# ADR-0010: Google Play Games Services for rewardless global leaderboards

- **Status:** Accepted — human signature recorded 2026-08-09 against reviewed proposal
  `bbbe79325b9ee14474e2ba1b76218d0b53021ac8`
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

We will use the official **Google Play Games plugin for Unity v2.2.0**, release commit
`c6f19addceb9a87489c5f1fb0d50bb4bef1e9c7a`, for optional, rewardless global Daily Line and
District Cup leaderboards on Android, plus one minimal project-owned Android
`submitScoreImmediate` bridge because the stock wrapper exposes only fire-and-forget submission. The
Unity wrapper is Apache-2.0 licensed. We will not operate a score backend, request server auth, or
adopt any other PGS feature under this ADR.

This is an exact pin, not “latest.” The approved candidate artifact is
`GooglePlayGamesPlugin-2.2.0.unitypackage`, expected SHA-256
`72b79902fc19647dea38eb9b5a150aead33826fb8720363ac14c4fbb71e3c273`. A mismatch fails before
import. The release raises the plugin's minimum Android API to 24; Cat Metro's ADR-0004 minSdk 25
remains the stricter floor. Its dependency XML directly resolves
`com.google.android.gms:play-services-games-v2:22.0.0` and
`com.google.android.gms:play-services-nearby:18.5.0`. Nearby is not a permitted product feature, but
the unmodified wrapper brings its binary/manifest surface; signature therefore accepts only a
clean-import SCA, manifest, permission, terms/license, and Data Safety audit that finds that surface
acceptable. Removing or changing Nearby requires a separately pinned, reviewed artifact rather than
an ad-hoc post-import edit. Exactly one EDM4U 1.2.188 copy must remain.

This accepted ADR explicitly amends ADR-0004's “exactly these versions” set. The future
implementation PR must add the wrapper artifact/hash/full commit and both direct Maven coordinates to
ADR-0004 and its machine-readable `config/pins.json` source before any production build contains PGS.
The Apache-2.0 claim applies only to the Unity wrapper; resolved Google Play services binaries remain
subject to their applicable Google Android SDK/API terms and recorded third-party notices.

### 1. Architecture amendment and isolation boundary

ADR-0003 is amended by one SDK-specific assembly and one engine-free
service interface:

| Assembly/interface | Owns | May reference |
|---|---|---|
| `CatMetro.Services.ILeaderboards` + POCO DTOs | authentication state, logical `BoardKey`, opaque destination revision, immediate-submit/show requests and typed local states | Domain/Content POCOs only; no PGS, Java, or Unity types |
| `CatMetro.Integrations.PlayGames` | provider manifest; `(BoardKey, destinationRevision)` → exact PGS resource mapping; sole adapter to the PGS Unity API and approved immediate-submit bridge | `CatMetro.Services`, PGS plugin, project-owned Java bridge, Unity |
| `CatMetro.Application` | score eligibility, hostile-save replay validation, logical board selection, bounded durable evidence/delivery state, retry/CAS policy | existing inward dependencies + `ILeaderboards`; never PGS |
| `CatMetro.Bootstrap` | immutable build-capability enforcement, runtime dark switch, adapter construction, and lifecycle forwarding | existing graph + `CatMetro.Integrations.PlayGames` only in a capable build |
| `CatMetro.Presentation` | local-only/Join/Global-board affordances using application state | Application/Services; never PGS |

No PGS type, resource ID, namespace, callback, status code, player identifier, or display name may
escape `CatMetro.Integrations.PlayGames`/Bootstrap. Static checks fail any PGS namespace elsewhere.
Application uses typed local states such as authenticated, cancelled, offline, missing-resource,
accepted, not-accepted, and invalid-local-evidence; it never branches on vendor integer/string codes.
An opaque destination revision is not a resource ID and reveals no provider identifier.

The addition explicitly changes ADR-0003's effectively permanent assembly list. It also amends
ADR-0007's hand-written Android interop posture: the haptics JNI helper is no longer the only approved
project-owned bridge; this one PGS immediate-submit bridge is the sole additional exception. That cost
is accepted because merging PGS into Analytics, Ads, or a generic integration assembly would enlarge
the trust and failure blast radius. Approval is now recorded. Assembly/bridge code may be authored
only under the frozen contract on a dedicated implementation or security-spike branch; it may not
merge to main or enter any artifact other than the explicitly controlled evidence builds required by
gates 6 and 8 until every applicable gate below is satisfied. No production inclusion or distribution
is authorized by this approval.

The bridge contract is closed and intentionally narrow:

- its only PGS call is
  `LeaderboardsClient.submitScoreImmediate(resourceId, score, scoreTag)` from the already pinned
  `play-services-games-v2:22.0.0`; it adds no Maven artifact or external source;
- the C#/Java boundary carries only a generated request ID, validated resource ID, signed `long`
  score, canonical tag, and completion result—never player identity, auth token, display text, or an
  arbitrary method name;
- it returns `Accepted` only for successful, non-null `Task<ScoreSubmissionData>` completion whose
  leaderboard ID matches the request and whose all-time `ScoreSubmissionResult` is present. A new
  best and an already-stored better best are both accepted outcomes;
- task failure/cancel, auth loss, malformed/mismatched result, timeout, process death, or no callback
  returns/remains `NotAccepted`; Application retains the row and never permanently drops it;
- the stock Unity `SubmitScore`, cached `LoadScores`, and any custom auth/UI/read/achievement path are
  statically unreachable from delivery code; and
- the implementation PR records reviewed Java/C# source hashes and the generated Android diff.
  Adding a method or changing callback semantics requires an ADR amendment and security review.

### 2. Product boundary

The frozen contract at `docs/prd/leaderboards-contract.md` is normative. In summary:

- Local Daily Line and District Cup remain fully playable, scoreable, rewardable, and reviewable with
  PGS absent, the runtime leaderboard flag OFF, no PGS profile, no network, or a total PGS outage.
- Global rank is public bragging-rights display only. It grants no reward, entitlement, progression,
  medal, cosmetic, ticket, price, cohort, offer, ad, or winner status.
- A global-eligible Daily result is the one scoring play for its exact local `dateKey`, with zero
  rewinds and a successful deterministic replay check. Practice runs never submit.
- A global-eligible Cup total is the sum of three clean per-route personal bests for the exact round.
  Any free, rewarded, or purchased rewind keeps its existing local result but excludes that run from
  the global total. Paid convenience can therefore never buy rank.
- Local rewards commit before any network call and are never rolled back by auth or submission state.

PGS score submission is best-score/idempotent from the app's perspective. Application owns bounded
canonical replay evidence and destination-aware delivery state. The pinned wrapper's `SubmitScore`
callback is local dispatch state, **not** server acknowledgement and is never used. Only the approved
bridge's validated successful `Task<ScoreSubmissionData>` from `submitScoreImmediate` may clear
durable state; no cached wrapper read, callback boolean, reward mutation, or premature “submitted”
copy substitutes for it.

### 3. Board-resource topology

PGS supplies daily, weekly, and all-time views for each leaderboard, but its fixed reset windows do not
match Cat Metro's local-date Daily or Monday-17:00-local Cup contract. We will therefore publish one PGS
leaderboard resource per dated Daily and per Cup round and use that resource's **all-time** view.

| Local board key | PGS Console resource | Value | Order |
|---|---|---|---|
| `daily_YYYYMMDD` | exact baked resource ID for that local `dateKey` | final integer score of the sole clean scoring play | larger is better |
| `cup_<round_id>` | exact baked resource ID for that round | sum of the three clean route bests | larger is better |

The provider manifest is authored and reviewed inside `CatMetro.Integrations.PlayGames`; it never
constructs a Console resource ID from player input and never exports one. Missing entries fail closed
to local-only. PGS ordering cannot be changed after publication, so a human verifies
larger-is-better, localized points, and score limits before publishing any resource.

PGS documents a maximum of 70 leaderboards per game. The candidate Season-1 envelope is 56 dated
Daily resources + 8 Cup resources = 64, leaving six correction resources. Before any resource is
created, the human signs an exact Monday-through-eighth-Sunday date epoch, eight exact Cup IDs, every
logical key/resource/revision mapping, and one scoring/simulation/generator/content/tag revision for
the entire epoch. If release cannot align to the signed Monday, global boards wait; local play does
not. A ruleset correction consumes a new resource/revision and incompatible old clients fail
local-only. Published boards are never reused because their scores cannot be reset. Before Season 2,
the human verifies whether retiring/deleting resources restores quota. If quota cannot support the
next season, global dated Daily boards stop; Cat Metro does not silently mix dates or introduce an
owned backend.

### 4. Authentication and native UI

There are two separate gates:

1. immutable build capability `pgs_leaderboards_compiled=false` means the artifact contains no PGS
   plugin, Maven libraries, generated manifest/resources, initialization, authentication, or PGS
   network flow; the capability may become true only after the signed dependency, exact Console
   manifest/credentials, privacy, and Data Safety first-inclusion gates are complete, so the resulting
   internal build can gather device evidence before production; and
2. within a human-approved PGS-capable build, the save-backed runtime flag `leaderboard=false` removes
   Cat Metro's board UI, adapter construction/calls, and outbox mutation.

The save is player-writable, so gate 2 may disable but can never authorize gate 1. PGS v2 performs
platform authentication at game launch when included; therefore a capable build does **not** promise
“runtime flag OFF means no auth attempt.” Its identity flow must already be approved and disclosed.
The implementation sets `com.google.android.gms.games.SUPPRESS_GAME_PROFILE_CREATION=true` only to
suppress automatic profile-creation UI, not authentication or network access.

Profile creation/authentication for a new participant begins only after an explicit **Join global
boards** tap. Cancel means local-only for that session, with no nag or commerce replacement. No auth
result can delay Home, level start, result presentation, local save, or local reward.

Cat Metro requests Google's native leaderboard UI for the exact resource and all-time span. The
pinned wrapper does not force the public collection in native UI, so Google owns the initial
collection/selector and Cat Metro makes no “public opens first” promise. It does not download or
render other players' names, avatars, or profiles in Cat Metro UI. The app may request the public
collection when reading the authenticated player's own rank/count for local display and, after a
separately approved analytics amendment, derive the coarse `rank_bucket` described in the contract.
That display read may be cached/stale and is never delivery acknowledgement; its exact pinned call
shape must pass the device spike. Exact rank and identity are never logged.

### 5. Allowed PGS surface and data flow

Allowed:

- Android platform authentication needed for PGS leaderboards;
- immediately submit a bounded numeric score and non-authoritative canonical tag to one
  manifest-allowed board through the approved bridge and consume only validated Task completion;
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
resource ID, numeric score, and a canonical tag. Daily tags are
`v1-d-{YYYYMMDD}-{simToken}-{generatorToken}-{replayHash16}`; Cup tags are
`v1-c-{roundToken}-{simToken}-{generatorToken}-{replayHash16}`. Date is eight digits; signed
round aliases are 1–16 characters; signed sim/generator tokens are 1–8 each; replay hash is exactly
16 lowercase hex; variables use delimiter-free RFC 3986 unreserved ASCII `[A-Za-z0-9._~]` only. The
maximum generated length is 56,
and the adapter independently rejects any tag over PGS's 64-character bound. It contains no Cat
Metro free text or separate player identifier.
The exact production data flow, retention wording, consent/age posture, privacy policy, and Play Data
Safety declarations require human review before the feature turns ON.

### 6. Integrity posture

The client accepts a score into its outbox only after deterministic re-simulation reproduces the
completion and value. It persists bounded canonical replay evidence sufficient to repeat that check,
then treats the deserialized save row as hostile and repeats all replay, board/revision, ruleset,
zero-rewind, score-bound, and tag checks immediately before every adapter call. Per-board lower/upper
limits are derived from ratified scoring constants and authored route bounds. PGS leaderboard tamper
protection is ON for every published resource. A client checksum or HMAC is not represented as trust.

These controls catch corruption and crude abuse; they are not authoritative anti-cheat. A modified
client can bypass local replay because no trusted Cat Metro server attests the run. That is why rank is
rewardless, why the app never calls a player “verified,” and why there is no prize competition based on
PGS rank. Suspicious entries are handled by the human with PGS tools, not by an unreviewed client
blocklist.

### 7. Failure and lifecycle policy

- Network/auth/service errors return local results immediately and leave at most one eligible score per
  board in bounded destination-aware state.
- Retry occurs only on authenticated foreground or explicit result-screen retry, at most four
  destinations per foreground cycle and one in-flight attempt per destination; there is no timer,
  boot loop, exponential background worker, or retry storm.
- Missing/malformed board manifest, out-of-range score, replay mismatch, wrong mode/date/round, and
  rewind-used states fail closed before the adapter.
- Every attempt carries immutable `(boardKey, destinationRevision, score, attemptId)` identity.
  Validated immediate-task acceptance uses compare-and-swap, takes
  `max(confirmedBest, attemptedScore)`, and cannot clear a newer Cup value. A manifest correction
  creates new delivery state and requeues retained evidence.
- Pause/process death cannot duplicate local rewards. A future human-approved ADR-0006 migration owns
  replay-evidence/delivery durability and exact byte bounds; the PGS adapter owns no file.
- Offline/auth-failed UI never shows a stale value as current global rank. A passive status may say the
  eligible score is waiting to submit, without implying success.
- A local runtime switch becomes a next-app-update rollback for the installed base, not an immediate
  remote control. Immediate human Console response may hide or delete an affected resource, but that
  is destructive to native visibility/history and old clients may still target it; the incident log
  must record that tradeoff. Existing local history remains usable.

### 8. Exact dependency pin and unpin trigger

| Item | Decision |
|---|---|
| Package | official Google Play Games plugin for Unity |
| Pin | **v2.2.0**, full release commit **`c6f19addceb9a87489c5f1fb0d50bb4bef1e9c7a`** — **HUMAN-APPROVED 2026-08-09** |
| Artifact | `GooglePlayGamesPlugin-2.2.0.unitypackage`, expected SHA-256 `72b79902fc19647dea38eb9b5a150aead33826fb8720363ac14c4fbb71e3c273` — **HUMAN-APPROVED 2026-08-09** |
| Direct Maven pins | `play-services-games-v2:22.0.0` + `play-services-nearby:18.5.0` — **HUMAN-APPROVED 2026-08-09**; Nearby use is forbidden and its shipped surface must pass audit |
| Project-owned bridge | one reviewed `submitScoreImmediate`-only Java/C# bridge under §1; no added artifact; implementation source hashes become part of the pin — **HUMAN-APPROVED 2026-08-09** |
| License/terms | Unity wrapper: Apache License 2.0 and notice retention. Google Play services binaries: applicable Google Android SDK/API terms and third-party notices; do not label them Apache-2.0. |
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
- **Use the stock Unity `SubmitScore` and let PGS own its offline queue.** Real advantages: no custom
  bridge and simplest integration. Lost because v2.2.0 immediately reports dispatch as success (and
  can callback false then true unauthenticated), so Cat Metro could neither durably acknowledge nor
  honestly report delivery. A full plugin fork was also rejected: the one-method project bridge has a
  smaller review and unpin surface.
- **No global boards.** Real advantages: zero dependency, identity, cheating, quota, or network work;
  the existing local modes already function. Lost because the human explicitly accepted optional
  global comparison and PGS gives it without an owned backend. This remains the feature-flag fallback.

## Consequences

**Easier:** Cat Metro gets Android-native account/rank UI, global comparison, and human moderation
without operating a service. Global comparison remains a replaceable adapter, while Domain and local
mode rules stay deterministic, engine-free, and offline-first.

**Harder:** each season needs pre-created, translated, validated Console resources and quota
accounting. Authentication and Play-distributed test builds enter the device matrix. Data Safety/store
copy gains a Google Games identity/score flow. False confidence in anti-cheat must be actively resisted.
The new asmdef and service interface amend a deliberately stable architecture.

**Lock-in:** published PGS resource IDs, score ordering, and stored scores are hard to reverse; Android
players' public history stays with the Play Games project. Migrating providers would start new global
boards and require a new ADR. Local scores/rewards remain provider-independent, limiting the damage.

**Spend/license:** the Unity wrapper is Apache-2.0 and has no direct package fee; resolved Google
artifacts have separate applicable terms. Engineering/operations cost comes from Console setup,
device testing, moderation, compliance, and future compatibility work.

**Monetization separation:** PGS implementation is not itself monetization. Nevertheless, it may not be
used as a vehicle to co-land billing/IAP/ad/payment code. Any monetization code anywhere still requires
a prior human-authored `state/mode=production` commit.

## Security notes

1. PGS is a new external identity and score-processing trust boundary. Treat every callback, save row,
   score, resource ID, display field, and status as untrusted data; bound lengths/numbers and map to
   typed DTOs. Specifically, never treat v2.2.0's `Action<bool>` submit callback as server acceptance.
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
   permission, Nearby, native-library, terms/license, and Data Safety review. Generated dependency
   changes are reviewed, not accepted as noise.
8. The runtime save flag is attacker-writable and is never an approval/privacy boundary. Static build
   evidence proves capability-OFF artifacts contain no PGS surface; first inclusion follows approval.
9. The project-owned bridge is a security-sensitive JNI boundary even though it adds no dependency.
   Validate arguments on both sides, bind completion to the generated request ID, tolerate duplicate/
   late/no callbacks, never reflect a method name, and fail closed on every malformed Task result.

## Approval and implementation gates

Approval of this ADR authorizes the exact architectural choice and dependency pin, not an immediate
import. A future implementation contract must provide failing tests for every criterion in the frozen
leaderboard contract and clear all of these gates:

1. human signature below; ADR-0003/ADR-0004/ADR-0007/config pin records updated in the implementation PR;
2. clean throwaway import that rejects an artifact-hash mismatch and records one EDM4U, full resolved
   tree, SCA/terms/licenses, Nearby/permissions/Data Safety, install-script, manifest, ProGuard/R8, and
   native-library/16-KB diffs;
3. human-approved ADR-0006 migration for bounded canonical replay evidence, destination-aware delivery
   and compare-and-swap state, plus full hostile-save/kill-during-write fixtures;
4. scoring constants/max bounds resolved before any Console resource is published;
5. two-gate absence/hostile-save, exact-board/revision, zero-rewind, replay-mismatch, tag-bound, offline,
   auth-cancel, missing-resource, late/duplicate callback, bounded retry, identity-isolation, and
   correction automated tests;
6. an independently security-reviewed spike proves the exact project-owned bridge, successful and
   failed `Task<ScoreSubmissionData>` mapping, existing-better-score outcome, source hashes, stock-
   wrapper unreachability, timeout/process-death retention, and no additional JNI/PGS surface;
7. human-created Play Games project, credentials, exact Monday-aligned 64+6-or-smaller signed resource
   epoch, frozen ruleset, translations, units/order/limits, tamper protection, and tester cleanup;
8. Play-distributed, correctly signed device smoke for capability absent/present, runtime flag OFF/ON,
   existing profile, no profile, cancel, offline immediate-task failure/later retry, score update, native UI
   collection behavior, reinstall, and next-update rollback on the low-tier device and Pixel;
9. privacy policy, Data Safety, store copy, age/consent, support, and moderation checklist approved
   **before the first PGS-capable artifact**; and
10. runtime leaderboard surface OFF by default until all remaining evidence is attached.

### Human signature

- [x] I approve PGS as the sole remote leaderboard/identity surface, with no Cat Metro backend.
- [x] I approve wrapper **v2.2.0**, full commit/artifact/SHA above, its two direct Maven pins including
      audited-but-unused Nearby, the scoped license/terms posture, and the named unpin trigger.
- [x] I approve the new `CatMetro.Integrations.PlayGames` assembly, `ILeaderboards` boundary, and sole
      project-owned `submitScoreImmediate` bridge exception amending ADR-0007.
- [x] I approve the two-gate build/runtime posture and acknowledge that PGS inclusion may authenticate
      at launch even while Cat Metro's runtime surface is dark.
- [x] I approve the Monday-aligned 64-resource envelope; exact dates, Cup IDs, revisions, and Console
      IDs still require a separate signed manifest before creation.
- [x] I approve validated successful `submitScoreImmediate` Task completion as acknowledgement; the
      stock wrapper callback and cached reads never acknowledge delivery.
- [x] I accept rewardless, best-effort client integrity; PGS rank will never award value.

- **Signed by:** Cat Metro product owner (human, in-session; agent-recorded)
- **Signature statement:** “MERGE. I SIGN §8.10 AND ADR-0010.”
- **Signed at (absolute date/time):** 2026-08-09 15:40:29 PDT (-0700), recording time
- **Signed proposal head:** `bbbe79325b9ee14474e2ba1b76218d0b53021ac8`
- **Signature record commit:** `122b6e0d07ce49d1f63b95026c0e1b02205d03e0`

## Official sources checked 2026-08-09

- [PGS Unity plugin overview](https://developer.android.com/games/pgs/unity/overview)
- [PGS Unity setup and authentication](https://developer.android.com/games/pgs/unity/unity-start)
- [PGS leaderboards concepts, limits, reset windows, and publication behavior](https://developer.android.com/games/pgs/leaderboards)
- [PGS leaderboards in Unity](https://developer.android.com/games/pgs/unity/leaderboards)
- [Official plugin v2.2.0 release](https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v2.2.0)
- [Official v2.2.0 Apache-2.0 license](https://github.com/playgameservices/play-games-plugin-for-unity/blob/v2.2.0/LICENSE)
- [Pinned v2.2.0 `SubmitScore` source](https://github.com/playgameservices/play-games-plugin-for-unity/blob/c6f19addceb9a87489c5f1fb0d50bb4bef1e9c7a/Assets/Public/GooglePlayGames/com.google.play.games/Runtime/Scripts/Platforms/Android/AndroidClient.cs#L1054-L1080)
- [Pinned v2.2.0 cached score-load source](https://github.com/playgameservices/play-games-plugin-for-unity/blob/c6f19addceb9a87489c5f1fb0d50bb4bef1e9c7a/Assets/Public/GooglePlayGames/com.google.play.games/Runtime/Scripts/Platforms/Android/AndroidClient.cs#L885-L914)
- [Pinned direct Maven dependencies](https://github.com/playgameservices/play-games-plugin-for-unity/blob/c6f19addceb9a87489c5f1fb0d50bb4bef1e9c7a/Assets/Public/GooglePlayGames/com.google.play.games/Editor/GooglePlayGamesPluginDependencies.xml#L4-L15)
- [Android `LeaderboardsClient` API](https://developers.google.com/android/reference/com/google/android/gms/games/LeaderboardsClient)
