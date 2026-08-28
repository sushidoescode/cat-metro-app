# Analytics transport and exact outbound-data declaration

**Owners:** analytics lane, TASK 2 (Apple privacy labels), TASK 3 (Play data safety),
TASK 11 (rewarded ads), and the RevenueCat commerce lane

**Last verified against code:** 2026-08-26

**Release rule:** the submitted binary and both store forms must agree with this document.

## Current checked-in state

`unity/Assets/Resources/Config/analytics_transport.json` is checked in with `enabled: false` and
an empty public project token. In that state `GameAnalyticsRuntime.CreateProduction()` returns a
no-op before creating an analytics identifier, opening the analytics profile or queue, evaluating
the remote flag, or making a network request. The current checked-in build therefore sends
**zero data from this gameplay analytics lane**.

The rest of this document is the exact behavior when a human inserts the public project token and
sets `enabled: true` for a release. TASK 2 and TASK 3 must inspect the resource in the actual
submitted build and declare the state that really ships.

## Transport decision for the five-week deadline

Cat Metro sends its allowlisted gameplay events directly to PostHog's hosted HTTPS ingestion API.
It does **not** ship the PostHog Unity SDK. Direct requests reuse Cat Metro's existing durable
queue, so there is one durable event owner, one retry identity, and no SDK singleton, hidden queue,
autocapture, session replay, exception capture, automatic device enrichment, or person-property
state.

This is smaller than operating a first-party analytics service and narrower than adding Firebase
or Unity Analytics. RevenueCat alone cannot report level starts, completions, and app returns.
PostHog supplies hosted event analysis while the client owns only two small request shapes. This
direct transport also avoids the reviewed Unity SDK behavior that could flush its private queue
before a fresh remote kill-switch value resolved.

RevenueCat remains authoritative for paywall and store-validated purchase facts. This gameplay
transport does not duplicate them, and no dashboard is being built for Shipaton.

## Exact data that leaves the device when enabled

The configured collector is currently `https://eu.i.posthog.com`. Configuration accepts only the
official EU or US PostHog collector; if the region changes, the store declarations must change
with it. All application bodies below are UTF-8 JSON over HTTPS.

### Request 1: remote kill switch

Before any gameplay batch may leave, the client sends:

`POST <host>/flags/?v=2`

The JSON object has exactly these fields:

| JSON field | Exact value or meaning |
| --- | --- |
| `api_key` | Public PostHog project token embedded in the resource config; never a personal/secret API key |
| `distinct_id` | Lowercase 32-hex random app-install GUID described below |
| `geoip_disable` | Boolean `true`, top-level |
| `flag_keys_to_evaluate[0]` | Configured key, currently `cat-metro-analytics-enabled` |

It sends no `person_properties`, group properties, groups, anonymous-ID alias, feature-flag
evaluation properties, device properties, locale, timezone, or account fields. Application code
explicitly sets `Content-Type: application/json`, `Accept: application/json`, and
`User-Agent: cat-metro-analytics/1`. That is not a claim that these are the only headers on the
wire: Unity's native networking layer may add `Content-Length`, `Accept-Encoding`, and
`X-Unity-Version`, and may resolve or replace `User-Agent` differently by platform. The submitted
iOS and Android builds need packet capture before anyone makes an exact on-wire header claim.
See Unity's
[request-header documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.SetRequestHeader.html).

The receiving network and PostHog necessarily observe the source IP and ordinary TLS/connection
metadata even though no IP address is placed in JSON. Top-level `geoip_disable: true` asks the
flags service not to perform GeoIP enrichment; it cannot hide the network source IP.

At the inspected PostHog server commit, the flag evaluator identifies an anonymous-ID continuity
override as its only database write. Cat Metro sends no such override, so no-Person behavior for
this request is a source-based inference, not a claim proved against the hosted project. The human
check below remains mandatory. See the
[inspected PostHog server source](https://github.com/PostHog/posthog/blob/2deb7870a34a48d4ebd16c3d15ac81b211475a4c/rust/feature-flags/src/flags/flag_matching.rs#L641).

The transport starts in `Unknown`. While the flag is unknown or false it cannot construct or send
a `/batch` request. A failed, timed-out, malformed, or non-boolean flag response remains unknown
and retries after 5, 10, 20, then 30 seconds (30-second cap). A boolean true expires after 15
minutes of continuous monotonic runtime. At expiry the transport returns to unknown and requests
the flag again; both `Tick()` and `TryDeliver()` enforce this, so a gameplay batch cannot race ahead
of refresh because of frame ordering. A false value clears the app-owned event queue and disables
new capture for the rest of that process unless a later fresh true re-enables it. Foreground and
offline-to-online transitions also request a fresh flag value; refresh
immediately returns to unknown and aborts any in-flight gameplay batch without acknowledging it.

Local `enabled: false` is the zero-contact switch. A remote false value cannot avoid the flag
request itself because that request is how the value is learned.

Neither switch is a local erasure control. Local false leaves any earlier profile/queue artifacts
untouched. Remote false clears the event queue after it is observed but retains the local
analytics profile and `distinct_id`; a later true result resumes with that same ID. No user-facing
analytics-data deletion control is added by this lane.

The remote-disabled state itself is process-local and is not persisted. After a relaunch, the
transport again starts unknown while the fresh queue can capture and persist events; unknown still
prevents every gameplay upload. A new false response clears those records. If the operator changes
the flag back to true before that device re-observes false, records captured during that relaunched
unknown period can upload. Use checked-in `enabled: false` when capture itself must stay durably
off across launches; do not describe the remote flag as a persistent local opt-out.

### Request 2: gameplay event batches

Only after a fresh boolean `true` does the client send:

`POST <host>/batch`

The top-level JSON object has exactly:

| JSON field | Exact value or meaning |
| --- | --- |
| `api_key` | Same public PostHog project token |
| `batch` | Ordered array of at most 50 event envelopes |

There is deliberately no top-level `sent_at`. Request-construction time therefore cannot make a
retry's PostHog deduplication tuple differ from the first attempt.

Every event envelope has exactly:

| JSON field | Exact value or meaning |
| --- | --- |
| `uuid` | Stable UUIDv7: its timestamp bits are the original event capture milliseconds; remaining bits are SHA-256-derived from a fixed Cat Metro domain, `distinct_id`, persisted queue ID, ordinal, capture time, and event name |
| `event` | One of the currently produced names in the next table |
| `distinct_id` | Persistent random app-install GUID |
| `timestamp` | Original capture time as UTC ISO-8601; a legacy record without capture time uses Unix epoch (`1970-01-01T00:00:00Z`) deterministically |
| `properties` | Event-specific allowlist plus the three common fields below |

Every event's `properties` object also contains exactly:

| Property | Exact value or meaning |
| --- | --- |
| `cm_event_id` | Random lowercase 16-hex queue ID generated once at enqueue and persisted across retry |
| `$geoip_disable` | Boolean `true` |
| `$process_person_profile` | Boolean `false` |

The final wire builder finds the named taxonomy row, copies only its required and optional fields,
requires all mandatory fields, allows only primitive values, rejects strings longer than 128
characters rather than truncating them, and then adds the three common properties. Undeclared
ordinary fields and all undeclared `$` fields
are removed. An unknown event, overlong value, or record missing a required field produces no
request. Any malformed record among the next attempted prefix of up to 50 blocks that whole prefix,
including valid records before it, and therefore blocks every later delivery until a future valid
migration or quarantine mechanism handles it; no such quarantine ships in this lane. Because no
analytics SDK is initialized, there are no SDK-added device, app, session, or super-properties to
strip later.

### Currently produced event fields

These are the only production producers in this analytics lane:

| Event | Exact event-specific properties that leave | Exact trigger |
| --- | --- | --- |
| `first_open` | `app_version`, `device_tier`, `os_api_level` | First locally analytics-enabled launch whose analytics profile has missing or non-positive `createdAtUtc` |
| `app_open` | `session_id`, `app_version`, `install_age_days`, `build_channel` | Initial analytics start; foreground after at least 30 background minutes; or foreground on a later UTC date |
| `level_started` | `level_id`, `mode`, `attempt`, `difficulty_target`, `from_screen` | Real Intro Play tap, successful Daily admission, Retry, or successful next-level load; every current producer supplies all five fields |
| `level_completed` | `level_id`, `mode`, `attempt`, `duration_s`, `switches_used`, `perfect`, `score`, `stars` | Once on the real simulation win edge |
| `daily_started` | `seed`, `local_date` | Only after the real Daily pipeline admits and loads a board |

Field details that matter to the store declarations:

- `device_tier` is one of `low`, `mid`, `high`, or `unknown`, derived from a coarse RAM bucket.
  Raw RAM is not sent.
- Android `os_api_level` is the integer API level rendered as text. iOS sends only
  `ios-<major>`. No device model, manufacturer, or full OS string is placed in either JSON body;
  the still-unverified native `User-Agent` caveat above remains.
- `session_id` is a new random lowercase 32-hex GUID per analytics session. It is not an account
  identifier and is not reused for a later session.
- `install_age_days` is the whole number of UTC days since locally stored creation time.
- `build_channel` is only `development` or `production`.
- `difficulty_target` is the authored numeric target rendered invariantly as text.
- `mode` is `campaign` or `daily`; attempts are one-based.
- completion duration is fixed-tick simulation time floored to seconds, not wall-clock play time.
- `perfect`, `score`, and `stars` come from current simulation/authored facts. No inferred score is
  manufactured.
- despite the inherited taxonomy name `local_date`, Daily sends the canonical UTC date key used
  to generate that board, not the device locale or timezone.

`app_open` on a later UTC date is the daily-return signal. An app kept continuously foregrounded
across midnight does not manufacture a return. Level N is queried from `level_started.level_id`.
The gameplay funnel is `first_open → first level_started → first level_completed → later
level_started` for the same `distinct_id`.

Daily's real admission path is instrumented and tested, but the shipped Home currently hides its
entry until the separate unlock/save lane lands. Do not claim observable Daily participation until
that entry is reachable in the release build.

## Data explicitly not sent in this lane's application JSON

- name, email address, phone number, login/account ID, contacts, messages, photos, files, or user
  content;
- IDFA, advertising ID, Android advertising ID, vendor ID, hardware serial, or a cross-app ID;
- precise/approximate coordinates, locale, timezone, language, carrier, install referrer, or
  notification/deep-link attribution;
- an IP address inside JSON (the processor still observes source IP as described above);
- device model, manufacturer, raw RAM, full OS version/string, screen dimensions, or build GUID
  as JSON properties; target packet capture must still characterize the native `User-Agent`;
- taps, touch coordinates, arbitrary screens, screenshots, audio, video, console logs, request
  telemetry, crash stacks, or performance traces;
- session replay, autocapture, SDK-added lifecycle events beyond the declared `app_open`, automatic
  exception capture, feature flag exposure events, groups, person properties, `$set`, `$set_once`,
  `identify`, or alias; or
- paywall, purchase, entitlement, price, transaction, or ad-revenue facts from this gameplay
  transport.

This lane does not read ATT state and does not gate a reward behind ATT. It does not add an
analytics consent prompt. The release/legal owner must still apply the intended lawful basis and
regional/store rules to the exact pseudonymous usage data declared here; if a separate consent
gate is required, local analytics must remain disabled until that gate exists.

## Anonymous identifier lifecycle — store-form critical

The `distinct_id` **persists across ordinary app sessions**.

After local analytics is enabled, Cat Metro reads `analyticsInstallId` from its owned
`analytics_profile.dat`. On a first analytics launch, it commits that ID and a positive
`createdAtUtc` together in one small atomic snapshot **before** constructing the transport. If
that commit fails, analytics stays disabled rather than using an ephemeral ID. This closes a
same-ID duplicate-`first_open` window: the first-open marker is durable before the event can be
queued or sent. The app never replaces the ID with an account ID. A corrupt main profile falls
back to its previous atomic backup when one exists; if neither copy is usable, a later enabled
launch safely creates a new profile and ID.

That ordering deliberately favors no duplicate install event over guaranteed install-event
delivery. A process kill or safe initialization failure after the ID/creation marker commits but
before `appSession.Start()` constructs `first_open` permanently loses that install event. A kill
after construction but before the background queue snapshot commits can lose it for the ordinary
queue crash-window reason below. The next successful launch still emits `app_open`, but PostHog's
`first_open` count is therefore an undercounting signal, not an exact installation ledger.

That local profile artifact contains exactly four allowlisted fields:
`analyticsInstallId`, `createdAtUtc`, `lastSeenAtUtc`, and `sessionCount`. The latter three are
local session bookkeeping; only their declared derivatives (`first_open`, `install_age_days`, and
session timing) can affect outbound events. No email, account, device-model, or arbitrary save
payload can enter this artifact.

The identifier is **not guaranteed to reset across every reinstall scenario**:

- Android: the launcher sets `android:allowBackup="false"`, which disables Android cloud backup;
  ordinary uninstall normally removes app-private data, so reinstall normally generates a new
  ID. Android 12+ documentation warns that some manufacturers' device-to-device transfers ignore
  `allowBackup="false"`, so migration can restore the profile and ID. See the
  [Android 12 backup/restore behavior](https://developer.android.com/about/versions/12/behavior-changes-12#backup-restore).
- iOS: ordinary uninstall normally removes the app container and generates a new ID. Offload App
  preserves the data container. An OS, device, or app-data restoration that restores
  `analytics_profile.dat` also restores the identifier. This lane has no separate backup
  exclusion or forced reinstall rotation.
- If local analytics remains disabled, no analytics identifier is created.

The same restore caveat applies to `analytics_queue.dat`: iOS app-data/device restoration, or an
OEM Android device-to-device migration that ignores the backup flag, can restore queued historical
gameplay records. Each record is locally bound to the owning `analyticsInstallId`. A consistent
profile+queue restore remains blocked while the remote flag is unknown/false and can retry after a
fresh true result. A partial restore or profile reset that produces a different ID atomically
discards the mismatched queue before delivery, so historical activity cannot be re-attributed to
the new ID. TASK 2/TASK 3 must account for consistent-restore retention, not assume uninstall is
an unconditional erasure boundary. The queue's local `ownerId` is not an additional wire field;
it equals the already-declared outbound `distinct_id`.

TASK 2 and TASK 3 should therefore treat it as a persistent, app-scoped pseudonymous identifier,
not as an advertising identifier and not as a guaranteed per-install-reset identifier.

## Persistence, batching, offline behavior, and retry truth

Cat Metro's `analytics_queue.dat` is the only durable event queue. Its shipped limits are 2,000
records, 1 MiB for the actual persisted artifact (16-byte header, JSON brackets/commas, and record
bodies), 512 bytes per record body, and a 64-event high-water trigger. It drops oldest records
first at either cap and records an in-memory loss note. Corruption rejects the file and starts
empty without crashing the game. Loading a syntactically valid current-format artifact re-enforces
all three current caps against the real file: noncanonical JSON/outer fields are stripped into the
writer's exact record shape, oversize canonical records are discarded, count/total-byte overflow
is dropped oldest-first, and any canonicalized/bounded result is synchronously written before it
can be delivered. Unsupported queue/profile header versions are rejected rather than interpreted
as the current format.

Production queue records also persist the owner ID described above. Owner binding is part of the
real 512-byte record measurement: the taxonomy test serializes and reads back all 45 canonical
records through the production-owned queue instead of reconstructing a narrower authored shape.

In production, `Log()` clones and bounds the event in memory and schedules a single background
writer; it performs no filesystem or network wait on the Play, Retry, or win callback. Bursts
coalesce to the newest ordered snapshot. The worker uses the existing atomic durable temp-write and
replace seam. Routine `lastSeenAtUtc`/`sessionCount` profile changes use the same background
executor and may lag or be lost on a process kill; neither field is transmitted, cold start still
emits `app_open`, and the install time was already committed with the ID. A background transition
makes a bounded 20 ms drain attempt. Startup reads and the first-time small ID/creation-time commit
occur before transport construction and gameplay, not in an event callback.

This non-blocking design has an honest crash window: a process kill before the background snapshot
commits can lose all events since the prior completed snapshot. A disk failure never escapes into
gameplay; the newest in-memory snapshot retries on the next enqueue, foreground, pause, or
network-reachable trigger.

Network delivery triggers on foreground, pause, offline-to-online, and 64-event high water. A
triggered batch cannot start until the current queue revision is durably committed; after an
acknowledgment, the shorter revision must also commit before the next batch starts. A started
request does not clear the queue. Only an HTTP 2xx callback removes the exact attempted prefix and
persists the shorter state. Offline, timeout, abort, any non-2xx, exception, or process death
retains that durably attempted batch. Requests contain at most 50 events and automatically pump
the next ordered batch after acknowledgment. Network failures, including a synchronous refusal or
exception before the HTTP operation starts, retry after 5/10/20/30 seconds while the remote flag
remains enabled.
`TryDeliver()` itself rejects lifecycle and high-water delivery triggers before the pending delay
expires, so those paths cannot bypass backoff.

A crash after server ingestion but before the shortened queue persists can replay an event. The
event's deterministic UUIDv7, event name, original timestamp, and `distinct_id` remain identical
on retry. Those four values (`uuid` + `event` + `timestamp` + `distinct_id`) are the PostHog
deduplication tuple; `cm_event_id` is also stable for audit/querying. The complete JSON body is
byte-stable for the same transport configuration and ordered records because it contains no
request-time field. Delivery is at-least-once from the app queue, not an exactly-once claim. See
PostHog's [event deduplication documentation](https://posthog.com/docs/data/events#event-deduplication).

Remote false clears the live queue immediately and schedules the empty disk snapshot through the
same non-blocking writer. A process kill before that snapshot commits can leave the older disk copy.
On the next launch, unknown/false still blocks it; if the operator changes the flag back to true
before that device observes false again, those pre-disable records can retry. Keep an emergency
kill flag false long enough for returning devices to observe it and for their empty queue snapshot
to persist. As declared above, that observation does not persist a disabled-capture marker.

## RevenueCat and rewarded-ad boundaries

RevenueCat remains the source of truth for paywall impressions/interactions, purchase conversion,
store validation, entitlements, and revenue. No client commerce event is duplicated into this
PostHog stream. Humans view those facts in RevenueCat's Paywalls, Overview, and Customer views.

Do **not** enable RevenueCat's direct PostHog integration under the current no-person-profiles
posture. RevenueCat's official setup says the associated user appears as a PostHog Person and that
it updates the `rc_subscription_status` person property. Keep the systems separate unless a future
personless integration is verified against its real payload. This repository cannot prove the
RevenueCat project's server-side integration state; the human release owner must inspect that
dashboard and confirm the PostHog integration is disabled:

- [RevenueCat PostHog integration](https://www.revenuecat.com/docs/integrations/third-party-integrations/posthog)
- [RevenueCat Paywall integrations](https://www.revenuecat.com/docs/tools/paywalls/integrations)

TASK 2 and TASK 3 must merge the commerce lane's actual RevenueCat SDK fields into this declaration.
This branch did not inspect or claim those device payloads.

No rewarded-ad producer was manufactured. `GameRoot.Analytics` remains the seam for TASK 11 to bind
from the real ad-network start/completion/failure callbacks using the existing sanctioned
`Events.RewardedAdStarted`, `Events.RewardedAdCompleted`, and `Events.RewardedAdFailed`
constructors. TASK 11 must add its exact outbound fields to this declaration when that callback is
real.

## TASK 2 / TASK 3 store-form handoff

These are the facts the legal lanes must classify under the store definitions current at filing:

| Actual enabled data flow | Apple privacy-label candidate | Play data-safety candidate |
| --- | --- | --- |
| Persistent random `distinct_id`; per-session random `session_id`; random `cm_event_id`; deterministic event `uuid` | Identifiers; TASK 2 decides User ID vs Device ID labels | Device or other IDs |
| Event name and capture `timestamp`; app opens; level/daily starts; attempts; completion duration; switch count; perfect flag; score; stars; level/mode/screen; authored difficulty; Daily seed/date; install-age days | Usage Data → Product Interaction | App activity → App interactions |
| App version, build channel, coarse RAM tier, OS API/major, and native `X-Unity-Version`; native/platform `User-Agent` pending packet capture | Exact raw fields above; TASK 2 chooses the current category | App info/performance or device-data classification; TASK 3 chooses current category |
| Source IP observed by processor while GeoIP enrichment is disabled | Confirm processor retention/use before answering location questions | Confirm processor retention/use before answering location questions |

The remaining transmitted values are the public project token, configured flag key, and the
boolean control fields `geoip_disable`, `$geoip_disable`, and `$process_person_profile`; these
configure processing rather than describe the player. HTTP framing/negotiation includes body
length and accepted encoding. TASK 2/TASK 3 should retain them in the technical inventory even if
the current forms do not assign them a personal-data category.

The data is encrypted in transit, linked only by an app-scoped pseudonymous ID, not used by this
lane for third-party advertising, and not used for cross-company tracking. Store forms still cover
data sent to a processor. Whether a service-provider transfer qualifies for a store "sharing"
exception is a legal/form decision, not something this code declares.

## Human setup and how to see data

No dashboard needs to be built.

1. Create a production PostHog project in the intended EU or US region. Keep sandbox/test data in
   a separate project.
2. Put its **public project token** in `analytics_transport.json`, keep the matching official host,
   set `enabled: true`, and commit that release-config change. Never embed a personal API key.
3. Create the boolean flag `cat-metro-analytics-enabled` and set it true for release traffic. Use
   local `enabled: false`, not merely remote false, for a zero-contact build.
4. Merge this declaration with the real RevenueCat and TASK 11 payload declarations, update the
   privacy policy/store forms, and confirm the selected legal basis before enabling release data.
5. Make a human-run development build. Before ADB, run `adb devices -l` and confirm the target is
   the Pixel 9 Pro; never install on the two unrelated headset/emulator devices.
6. On a fresh install, exercise Home → Intro → Play → win → Next; background/return on a later UTC
   date; offline/reconnect; flag false/true; and process death during an in-flight batch.
7. Inspect a packet capture or mock collector and PostHog raw events. Confirm the two exact request
   bodies, the native-stack headers on each target platform, the stable four-field retry tuple,
   absence of undeclared or automatically added JSON device fields beyond the declared coarse
   fields, no GeoIP enrichment, no Person, and no
   undeclared or SDK-added lifecycle events beyond the declared `app_open`, and no replay or
   exception events. A created Person or GeoIP enrichment is a release blocker because hosted
   reality would differ from this declaration.
8. In PostHog, query `first_open → level_started → level_completed → later level_started`, break
   down by `level_id`, and cohort later-UTC-date `app_open` events for returns.
9. View paywall impressions, conversion, purchases, and revenue in RevenueCat. Inspect the
   RevenueCat project's integration settings and confirm its direct PostHog bridge is disabled
   under the current privacy decision; code review cannot prove dashboard-side state.
10. After TASK 11 binds the real callback, verify rewarded events in raw PostHog events and update
    both store forms from the observed payload.

No store upload was or may be performed by this lane.

## Verification boundary

Verified against local artifacts:

- the real `UnityWebRequest.uploadHandler.data` for the flag and gameplay batch bodies;
- taxonomy allowlisting plus mutation fields such as email, device model, and screen width;
- unknown/malformed event rejection, original/epoch timestamps, and byte-stable retry bodies;
- one queue owner, retain-until-HTTP-2xx behavior, exact-prefix acknowledgment, 50-event ordered
  batching, durable-before-delivery ordering, exact artifact-byte bounds, offline retry,
  late-callback invalidation, background-write coalescing, persisted identity reuse, and
  owner-mismatch discard, including raw-artifact reload; restored count/byte/per-record cap
  mutation, noncanonical-padding/outer-field mutation; and unsupported-header rejection;
- real Home/Intro/Play, simulation win, Next, and Daily admission producers; and
- checked-in fail-closed config and absence of the PostHog Unity package from package/assembly
  manifests.

Not verified here:

- no live PostHog request was sent because the checked-in token is empty and analytics is disabled;
- hosted no-Person/GeoIP behavior still requires the human raw-event check;
- no iOS/Android build, native on-wire header packet capture, process-kill, uninstall, or
  backup/restore experiment ran;
- the separate RevenueCat SDK payload was not inspected, and the dashboard-side RevenueCat →
  PostHog integration-disabled state was not observed;
- no real rewarded callback exists yet, by design; and
- no store upload was run.
