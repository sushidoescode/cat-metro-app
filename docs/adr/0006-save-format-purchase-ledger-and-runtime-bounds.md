# ADR-0006: Save format, purchase ledger, and the `[ARCH]` runtime bounds

- **Status:** Proposed (ratifies `docs/plan/specs/architecture.md:17`; **sets the `[ARCH]` constants the PRD delegates to the architect**; contains one requirement conflict that needs a human choice)
- **Date:** 2026-08-02
- **Relates:** ADR-0002 (no snapshots to persist), ADR-0003 (`ISave` boundary), ADR-0008 (content hash), ADR-0009 (CI).

## Context

The save is where every irreversible thing in this product lives: purchases, ticket and rewind
balances, the dedupe set that makes grants exactly-once, the entitlement cache, progress, and the
breadcrumbs that survive process death. `docs/plan/specs/architecture.md:17` settles the shape
("Versioned JSON + binary header, atomic write (temp+rename), migration table, durable purchase
ledger"). CM-R05 (`docs/prd/PRD.md:157-174`) turns it into seven invariants SI-1…SI-7 plus a
kill-during-write test, a migration test and a low-storage soak. CM-R27
(`docs/prd/PRD.md:454-468`) requires dedupe-hash insert, audit entry and balance increment in **one**
temp+rename write, with the analytics event emitted only *after* the durable write.

Three numbers were explicitly delegated to this ADR. `SAVE_MAX_BYTES` "declared in the same config
file as the queue bounds `[ARCH]`" (`docs/prd/PRD.md:174`) and the offline-queue bounds
`QUEUE_MAX_EVENTS` / `QUEUE_MAX_BYTES` / `QUEUE_FLUSH_TRIGGER` (`[ARCH: NEW-Q19]`,
`docs/prd/PRD.md:687-691`). Two risk rows are addressed to the architect by name: **RK-19** (the
dedupe key lacks domain separation, `docs/prd/risks.md:82`) and **RK-20** (the never-trimmed dedupe
set is unbounded inside an atomic write, `docs/prd/risks.md:83`). A third, **RK-17**
(`docs/prd/risks.md:80`), turns out to conflict with CM-R27.4 — see §Open conflict.

## Decision

### 1. File format

One file, `save.dat`, written atomically: **binary header + UTF-8 JSON payload.**

```
offset size field
0      4    magic            ASCII "CMSV"
4      2    formatVersion    uint16, little-endian   (header layout version; starts at 1)
6      2    saveVersion      uint16, little-endian   (payload schema version; starts at 1)
8      4    payloadLength    uint32, little-endian
12     4    payloadCrc32     uint32, little-endian   (CRC-32/IEEE over the payload bytes)
16     …    payload          UTF-8 JSON, no BOM
```

Rationale for each field, because each earns its place: `magic` distinguishes a save from a truncated
write or an unrelated file; `formatVersion` lets the *header* evolve without guessing; `saveVersion`
drives the migration table without parsing the payload first; `payloadLength` + `payloadCrc32`
make SI-1 ("parses under the shipped schema") checkable *before* handing bytes to a JSON parser,
which matters because parsing is the likeliest boot-path crash source (RK-34).

**Atomic write:** serialize → write `save.dat.tmp` → **`FileStream.Flush(flushToDisk: true)` on the
temp file** (this is the one call that forces the file's own data to stable storage from managed
code) → close → **`File.Replace(source: save.dat.tmp, destination: save.dat, destinationBackupFileName: save.dat.bak)`**,
which performs the rename and produces the `.bak` in one API call. Never write in place.

**Directory-entry durability is not claimed.** There is no directory-`fsync` equivalent in managed
.NET, and Unity/IL2CPP on Android exposes none either; reaching for one means inventing an API or
adding JNI, and neither is authorised here. The consequence is honest and bounded: a power loss in
the window between `File.Replace` returning and the filesystem committing the directory entry can
leave the *old* `save.dat` visible. That is exactly the case the `.bak` fallback below already
covers — the loss ceiling is one commit, never a corrupt file, because the file contents themselves
were flushed before the replace. Do not add a JNI `fsync` helper to close this; it is not worth a
platform-interop surface (ADR-0007 keeps haptics as the *only* hand-written interop).

**On load:** if `save.dat` fails
magic/length/CRC, fall back to `save.dat.bak` (the previous good file, produced by `File.Replace`); if
both fail, start a new save and emit `error_caught(domain=save_corrupt)` — never crash (crash-free
≥99.5% is never-cut, `docs/plan/EXECUTION_PLAN.md:200`). A stale `.tmp` on boot is deleted, which is
what makes SI-6 true.

**Migration:** `MigrationTable` is an ordered list of `(from, to, Func<JObject, JObject>)` applied in
sequence from the file's `saveVersion` to the build's. **Downgrade is refused, not attempted**: a file
whose `saveVersion` exceeds the build's is left untouched, the app starts with an in-memory default
profile in a read-only mode, and `save_migrated(from,to,success=false)` is logged (CM-R05.3,
`docs/prd/PRD.md:172`). A migration step never deletes a key it does not understand.

### 2. Payload schema (v1) — **IRREVERSIBLE, human ADR gate required**

```jsonc
{
  "saveVersion": 1,
  "contentHash": "…",              // ADR-0008; which level corpus this save was produced against
  "profile":     { "createdAtUtc": 0, "lastSeenAtUtc": 0, "sessionCount": 0 },
  "progress":    { "levels": [ { "id": "L001", "stars": 0, "bestScore": 0, "clears": 0 } ],
                   "districtsUnlocked": 1, "tutorialDone": false },
  "daily":       { "lastDateKey": "", "streakDays": 0, "playedKeys": [] },
  "economy":     { "tickets": 0, "rewindBalance": 0, "freeRewindDateKey": "" },
  "caps":        { "dateKey": "",                           // ad/rewind daily caps — durable, never in memory (RK-24)
                   "counters": { "rewind_failure": 0, "double_tickets": 0, "daily_gift_double": 0,
                                 "streak_saver": 0, "theme_rental": 0 } },
  "ledger":      { "keyScheme": "cm-ledger-v1",
                   "dedupe": [ "…32 hex…" ],
                   "audit":  [ { "txnHash": "", "productId": "", "qty": 0, "grantedAtUtc": 0 } ] },
  "entitlements":{ "appUserId": "", "active": [ ], "fetchedAtUtc": 0 },
  "flags":       { "ads_enabled": true, "paywall_placements": true, "daily_enabled": true,
                   "weekly_event": true, "share_card": true, "leaderboard": false },  // ADR-0007
  "breadcrumbs": { "screenStack": [ ],                      // process-death recovery
                   "purchase": null },                      // or { productId, placement, startedAtUtc, state }
  "settings":    { "haptics": true, "motion": true, "audio": true, "equippedThemeId": "" }
}
```

The **key set and nesting are an irreversible contract** the moment a tester's device holds a
v1 file: from then on every change is a migration step that must be written, tested and shipped.
This block requires an explicit human decision at the ADR gate. Because it is irreversible, **no
sub-object is left as an unexplained `{ }` for the first implementer to fill in** — every key below
is either enumerated here or explicitly marked open:

- **`caps.counters`** — exactly the **five locked ad surfaces**, verbatim as named at
  `docs/plan/EXECUTION_PLAN.md:34-37`: `rewind_failure`, `double_tickets`, `daily_gift_double`,
  `streak_saver`, `theme_rental`. Each value is the count consumed **today**, reset when
  `caps.dateKey` rolls over; a no-fill fallback consumes a slot exactly as a completed watch does
  (RK-24). The *limits* are economy values and live in `config/economy_defaults.json`, not here —
  only the consumed counts are persisted.
  **OPEN (not an architect call):** `rewind_failure` is capped **2/session** *and* 5/day
  (`docs/plan/EXECUTION_PLAN.md:35`). A daily counter alone cannot express the per-session half, and
  a "session" does not survive process death. Whether v1 carries a sibling `caps.sessionCounters`
  (durable, reset on `app_open` after the 30-minute gap of CM-R43.6, `docs/prd/PRD.md:693`) or the
  per-session cap is accepted as in-memory-only and therefore resettable by a task-kill is a
  **human decision at this gate** — and it is irreversible in the same way the rest of the block is.
- **`flags`** — exactly the **six flag keys** already fixed at ADR-0007 §Feature flags
  (`docs/adr/0007-presentation-and-runtime-baseline.md:48-49`), persisted so a runtime override
  survives a restart; a missing or unparseable value reads as the compile-time default and never
  throws (ADR-0007 §Security notes, "flags must fail closed"). The defaults shown are illustrative of
  *type*, not of launch posture — `ads_enabled`'s launch value is gated on NEW-Q45.
  **OPEN:** `paywall_placements` is listed by ADR-0007 among boolean kill switches but names a
  *set*. Whether it is a `bool` (paywalls on/off) or an array of enabled `PlacementId`s changes the
  persisted type and therefore the migration surface. Marked open, not guessed.
- **`breadcrumbs.purchase`** — `null` when no purchase is in flight, otherwise exactly four fields:
  `productId` (our SKU string, never a store token), `placement` (the `PlacementId` the flow started
  from), `startedAtUtc` (epoch ms, `IClock`), and `state`. This is the record that makes ADR-0007's
  "a purchase in flight during pause is **never** granted from memory on resume" checkable — on
  resume the reconciler reads this breadcrumb and re-derives truth from RC `CustomerInfo` + the
  ledger.
  **OPEN:** the `state` value set. It must mirror the purchase lifecycle the pinned purchases-unity
  9.7.0 actually exposes, and **RK-39 records that no RC API signature in the corpus is verified**
  (`docs/prd/risks.md:139`; A-07/A-08, `docs/prd/PRD.md:947-948`). Enumerating it here would be
  inventing an SDK API, which is forbidden (`docs/plan/EXECUTION_PLAN.md:480-481`). The implementer
  reads the pinned package source first and the enum lands as an **ADR-0006 amendment before the
  first grant**, not as a code choice.

`profile`, `progress`, `daily`, `economy`, `ledger`, `entitlements` and `settings` are fully
enumerated above and carry no open sub-shape.

### 3. Consumable ledger

- `ConsumableLedger.TryGrant(transactionId, productId)` is the **only** function that may increase
  `economy.rewindBalance` from a purchase (`docs/plan/specs/revenuecat_implementation.md:355-387`).
- **Dedupe key — RK-19 fix, adopted:**
  `key = lowercase-hex( first 16 bytes of SHA-256( "cm-ledger-v1|" + productId + "|" + transactionId ) )`.
  The versioned prefix gives domain separation *and* a migration path if the scheme ever changes; the
  `productId` component removes the cross-product collision RK-19 describes. **32 lowercase hex chars**,
  as CM-R27 requires (`docs/prd/PRD.md:456`). The raw transaction id never leaves the device and never
  enters analytics or crash payloads. **This changes a shipped key format, so it is cheapest strictly
  before the first grant ever lands — i.e. now.** `ledger.keyScheme` is persisted so a future scheme
  change can be detected rather than silently mis-deduped.
- **Order of operations, non-negotiable:** compute key → check dedupe → mutate in-memory state →
  **one** atomic write containing dedupe insert + audit entry + balance → *then* emit
  `purchase_completed`. A fault before the write produces neither event nor balance change
  (CM-R27.3/27.4).
- **RK-20 bound, adopted:** the dedupe set is capped at `LEDGER_DEDUPE_MAX_ENTRIES` and **never
  trimmed**. On reaching the cap, `TryGrant` **refuses** further grants, returns 0 and logs
  `error_caught(domain=ledger_capacity)` — loud and refundable, rather than silently trimming (which
  would re-open double-granting). The audit list stays FIFO-capped at `LEDGER_AUDIT_MAX_ENTRIES`
  (CM-R27.5).
- **Scope of the guarantee, stated once so no published claim overstates it (RK-21):** exactly-once
  grant *from the store*. **Not** tamper resistance. The payload is plaintext.

### 4. The `[ARCH]` constants — single source `config/runtime_bounds.json`

The PRD's constant convention (`docs/prd/PRD.md:88`) requires one named config file that tests read.
**This file is that source; the values below are normative and the file must match this ADR.**

```jsonc
{
  "schemaVersion": 1,

  "SAVE_MAX_BYTES":            524288,   // 512 KiB
  "SAVE_PAUSE_BUDGET_MS":      50,

  "LEDGER_DEDUPE_MAX_ENTRIES": 5000,
  "LEDGER_AUDIT_MAX_ENTRIES":  200,
  "LEDGER_KEY_SCHEME":         "cm-ledger-v1",

  "QUEUE_MAX_EVENTS":          2000,
  "QUEUE_MAX_BYTES":           1048576,  // 1 MiB — backstop only; see rationale
  "QUEUE_EVENT_MAX_BYTES":     512,
  "QUEUE_FLUSH_HIGH_WATER":    64,
  "QUEUE_FLUSH_TRIGGER":       ["network_reachable", "app_foreground", "app_pause", "high_water"],

  "ATTRIBUTION_MAX_RESIMS":    24,       // ADR-0002 §9 owns this row

  "CONTENT_MAX_FILE_BYTES":    262144,   // ADR-0008 owns these three rows
  "CONTENT_MAX_JSON_DEPTH":    16,
  "CONTENT_BOUNDS_PROFILE":    "level-schema-v2"
}
```

One-line rationales (each number is derived, not chosen):

- **`SAVE_MAX_BYTES = 524288`** — modelled worst case is ~221 KB (dedupe 5000 × ~35 B = 175 KB; audit
  200 × ~120 B = 24 KB; **70 levels of progress ≈ 5.6 KB**; a year of daily keys ≈ 15 KB; entitlement
  cache, flags, breadcrumbs, settings ≈ 2 KB), so 512 KiB is ~2.4× the worst case and ~20× the
  expected steady state, which makes the CM-R05.5 soak assertion a real regression detector rather
  than a rubber stamp.
  **Basis of the 70, stated once because this model is load-bearing** (other documents quote smaller
  level counts for *other* purposes and none of them is this one): `progress.levels` is sized at the
  **post-event ceiling, not at 1.0**. At 1.0 the corpus is **40** — 30 campaign levels (6 districts ×
  5) plus the 10-level Night Harbor bonus district L901-L910; post-launch the campaign grows 31-35,
  36-40 and **41-60 post-event** (`docs/plan/EXECUTION_PLAN.md:41-43`), so the campaign ceiling is 60
  and 60 + 10 Night Harbor = **70 progress rows**, which is the number a save must still fit under
  after every planned content drop. The **30-board dated backup pool** (ADR-0008) is deliberately
  *not* in this figure: backup-pool boards are dailies, and a played daily costs one `dateKey` string
  in `daily.playedKeys` — already counted in the "year of daily keys ≈ 15 KB" line — not a
  `progress.levels` row. ADR-0002's "40 shipped levels + 30 pre-validated dailies" and ADR-0007's
  "~50 levels of JSON" describe the *shipped artifact*, which is a different quantity from the
  *lifetime progress rows* modelled here.
- **`SAVE_PAUSE_BUDGET_MS = 50`** — restates the existing lifecycle budget
  (`docs/plan/specs/architecture.md:80`) in the machine-readable file so the flush test can assert it.
- **`LEDGER_DEDUPE_MAX_ENTRIES = 5000`** — matches the bound RK-20 itself proposes
  (`docs/prd/risks.md:83`); at 6 SKUs it is orders of magnitude beyond any legitimate buyer, so
  reaching it means something is wrong and refusing is the correct answer.
- **`LEDGER_AUDIT_MAX_ENTRIES = 200`** — restates the already-specified FIFO cap
  (`docs/plan/data/entitlement_map.json:343-347`, CM-R27.5); it is here so one file holds the bounds.
- **`QUEUE_MAX_EVENTS = 2000` — this is the cap that binds.** ≈130 fully offline levels at
  ~15 events/level, i.e. days of offline play. **Read CM-R43.4(a) precisely
  (`docs/prd/PRD.md:688`): the MUST test enqueues exactly `QUEUE_MAX_EVENTS` events and requires all
  of them to flush in order with zero duplicates; the 500-events/24 h instance is the *smoke*
  variant of the same test, not the criterion instance.** An earlier draft of this ADR had it
  backwards and sized the byte cap against the 500-event figure.
- **`QUEUE_MAX_BYTES = 1048576` (1 MiB) — a backstop, deliberately *not* co-binding.** It is set at
  ≥ `QUEUE_MAX_EVENTS × QUEUE_EVENT_MAX_BYTES` (2000 × 512 = 1 024 000 B ≤ 1 048 576 B), so **a
  queue holding exactly `QUEUE_MAX_EVENTS` events cannot trip the byte cap even if every one of them
  is a maximum-size event.** That is a correctness requirement, not tidiness: with the two caps tuned
  to bind at the same point, the CM-R43.4(a) no-loss run — which sits *at* `QUEUE_MAX_EVENTS` — would
  drop oldest-first the moment the average event exceeded ~128 B, and the MUST would fail against the
  bounds rather than against the code. The byte cap now catches only the case the event cap cannot:
  events larger than expected in aggregate, on a device where disk, not count, is the scarce
  resource. 1 MiB remains negligible against the ≤50 MB free-space floor of the CM-R05.4 low-storage
  soak. **[ERRATUM 2026-08-05, from the CM-C8 review round (finding S8): the previous sentence
  here claimed CM-R43.4(b) keeps "two reachable limbs (… exceed the bytes with
  `QUEUE_EVENT_MAX_BYTES`-sized ones)" — which the paragraph's own arithmetic disproves: at
  2000 × 512 = 1 024 000 ≤ 1 048 576 the COUNT cap always binds first, so the byte limb is
  unreachable at shipped bounds by design. The byte-limb overflow test therefore runs under
  SYNTHETIC bounds (as CM-C8's suite does), and the shipped byte cap remains exactly what the
  bullet says it is: a backstop. ratified by the human in-session 2026-08-05; recorded by the phases-6-10 agent]**
- **`QUEUE_EVENT_MAX_BYTES = 512`** — a single event that cannot be serialized under this is a bug
  (no free text is permitted in the taxonomy, RK-30); it is dropped with `queue_dropped` rather than
  poisoning the queue.
- **`QUEUE_FLUSH_HIGH_WATER = 64`** — flush early enough that a crash loses little, rarely enough
  that flushing is not per-event.
- **`QUEUE_FLUSH_TRIGGER`** — exactly these four and **no timer**, so CM-R43.4(c)'s negative test
  ("fires on the trigger and not otherwise") is decidable.
- **`ATTRIBUTION_MAX_RESIMS = 24`** — the hard ceiling on re-simulations the cause-first attribution
  pass (ADR-0002 §9) may run before it stops and renders the ambiguous branch. It is **pinned here,
  in the file the tests read, rather than deferred to the implementer**, because the naive bound is
  not affordable: the candidate set is the distinct routing decisions in the trailing 24-tick window
  (CM-R15's A-23 predicate, `docs/prd/PRD.md:306`), whose *theoretical* ceiling is
  `C_max = switches × 24 = 10 × 24 = 240` (`docs/plan/data/level_schema.json:81`), and each candidate
  costs a re-run of up to `timeLimitTicks = 4000` ticks
  (`docs/plan/data/level_schema.json:125`) — ≈9.6 × 10⁵ tick-steps against a 3 s window
  (`docs/prd/PRD.md:311`). 24 is one candidate per tick of the window, evaluated **newest-first**
  (nearest the failure tick), which is the right order because the causal decision is by construction
  the *last* routing decision affecting the causal cat (`docs/prd/PRD.md:304`). Worst case becomes
  24 × 4000 ≈ 9.6 × 10⁴ tick-steps — an order of magnitude below the naive ceiling, on an
  allocation-free integer loop. The CM-R15.3 fixtures (`AMB-01`…`03` and the three unambiguous ones,
  `docs/prd/PRD.md:310`) are small boards far under this cap, so all six asserted branches are
  unaffected. The implementer still measures wall time at the vertical slice; a *lower* value is an
  ordinary ADR amendment, but the bound is no longer an open number.

**Queue behaviour that goes with the numbers** (CM-R43.4, RK-32 `docs/prd/risks.md:116`): drop
**oldest-first** on overflow and emit a `queue_dropped` counter carrying the dropped count — visible
loss, never silent; a per-event idempotency id so flush retries dedupe rather than inflating published
numbers; and the queue is **metrics-only** — a static check asserts no entitlement, ledger or cap
state is ever written through it.

### 5. The offline queue file — `analytics_queue.dat`

§1's "one file, `save.dat`" is the rule for **transactional** state. The offline analytics queue is
the one other file this architecture persists, and leaving it unnamed made the §Open conflict
inventory below incomplete. It is specified here so the file list is closed:

| Property | Value |
|---|---|
| **Path** | `analytics_queue.dat`, sibling of `save.dat` under `IStorageRoot.SaveDirectory` (ADR-0003) |
| **Format** | the same 16-byte header as §1 (magic `"CMQU"`, `formatVersion`, `queueVersion`, `payloadLength`, `payloadCrc32`) + UTF-8 JSON array of queued events, each carrying its idempotency id. Same header helper, same CRC check, same reject-and-restart-empty behaviour on failure |
| **Write path** | the same temp+`File.Replace` helper as `save.dat`, so a kill during a queue write cannot corrupt it either |
| **Transactionality** | **explicitly non-transactional with respect to the ledger.** The queue write and the `save.dat` write are two separate atomic writes and are *never* combined. This is intentional: CM-R27.3 requires the analytics event to be emitted only *after* the durable ledger write, so a crash in the gap must lose the **event**, never the **grant**. The ordering is therefore always `save.dat` commit → enqueue → (later) flush |
| **Loss posture** | lossy by design and visibly so. Corruption, cap overflow and a lost tail all degrade to `queue_dropped` with a count. No product decision ever reads this file |
| **Backup posture** | **excluded from Play auto-backup under every option below**, unconditionally |
| **Bounds** | `QUEUE_MAX_EVENTS` / `QUEUE_MAX_BYTES` / `QUEUE_EVENT_MAX_BYTES` above; it does **not** count against `SAVE_MAX_BYTES` |

The unconditional backup exclusion is not a preference: a restored queue would re-emit events
recorded on a *different* install after a reinstall, which is precisely what CM-R43.7 forbids
(`first_open` fires exactly once, "including after reinstall with backup off",
`docs/prd/PRD.md:694`), and would inflate published numbers in violation of CM-R56.3/56.4. Since the
queue carries no entitlement, ledger or cap state, excluding it costs nothing a player would notice.

## Open conflict — RESOLVED 2026-08-04/05 (human decision: BACKUP OFF)

**[RESOLUTION: the human decided "backup off" in-session 2026-08-04 (recorded in
`state/handoffs/CM-C2b.md`; implemented by CM-C2b criterion 11 — `android:allowBackup="false"`
in the custom launcher manifest, `useCustomLauncherManifest: 1`, no backup-rules XML). With
auto-backup off entirely, the conflict below DISSOLVES: nothing is backed up, so the
RK-17 exclusion set (entitlement cache, ledger, dedupe set — and ADR §5's unconditional
`analytics_queue.dat` exclusion) holds a fortiori while CM-R27.4's one-atomic-write law is
untouched. The merged-manifest proof rides the first device build's artifact. ratified by the human in-session 2026-08-05; recorded by the phases-6-10 agent]**

The original conflict statement, kept for the record:

**RK-17 (`docs/prd/risks.md:80`) says the entitlement cache, the consumable ledger and the dedupe set
must be excluded from Play auto-backup. CM-R27.4 says the dedupe insert, audit entry and balance must
land in ONE atomic write. Android's backup rules exclude *files*, not JSON keys. These cannot both
hold for a single-file save.**

**Complete file inventory this decision applies to** (§5 closes a gap an earlier draft left — the
analysis below previously reasoned as if only `save.dat` existed):

| File | Contents | Backup posture |
|---|---|---|
| `save.dat` | everything in §2 — the file the options below disagree about | **the decision** |
| `save.dat.bak` | previous good `save.dat`, produced by `File.Replace` | must follow `save.dat` — a rule excluding one but not the other reopens the same RK-17 exploit through the `.bak` |
| `save.dat.tmp` | transient; deleted on boot (SI-6) | excluded — never a durable artifact |
| `analytics_queue.dat` | metrics only (§5) | **excluded unconditionally**, under all three options |

So the choice below is genuinely about the `save.dat` + `save.dat.bak` pair, and nothing else.
Options, with real costs:

- **(A) One file, `android:allowBackup="false"` (architect's recommendation).** Keeps the
  one-atomic-write invariant exactly as CM-R27.4 states it and closes both RK-17 exploit paths
  (entitlement-cache transplant; buy → back up → spend → restore) outright, with a single manifest
  attribute. **Cost: reinstall or device transfer loses local progress** — entitlements come back via
  RC Restore, tickets/rewinds/stars do not. That is a player-visible product decision, which is why
  it is a human call and not an architect call. It is consistent with cloud save already being a
  declared NON-GOAL (`docs/prd/PRD.md:931`).
- **(B) Two files** — `save.dat` (progress/settings, backed up) + `wallet.dat` (ledger, entitlement
  cache, caps, excluded). Preserves progress transfer. Cost: two atomic writes, so a crash between
  them leaves a mixed pair; that needs a written reconciliation rule (wallet authoritative for
  economy, progress self-heals) and it weakens CM-R05.1's "either the complete previous version or
  the complete new version" from a fact to a per-file fact. Note this makes the durable set **four**
  files (`save.dat`, `save.dat.bak`, `wallet.dat`, `wallet.dat.bak`) plus `analytics_queue.dat`, and
  the backup rules must enumerate all of them — the `.bak` siblings are the easiest to forget and the
  ones that reopen RK-17 if forgotten.
- **(C) One file, backed up, with mitigations** — bind the entitlement cache to the RC `appUserId` it
  was fetched under and discard on mismatch (kills path A of RK-17); accept path B (restore-after-spend
  refunds consumables) as a known, cheap, self-limiting exploit. Cost: a knowingly-shipped free-consumable
  path, and a Data Safety/backup story that has to be told honestly.

**The architect's recommendation is (A)** on the constitution's sizing rule: it is one attribute, it
removes an entire risk row, and the capability it sacrifices was never a requirement. **But it is not
the architect's decision to make** — it changes what happens to a real player's progress. Escalated.

## Alternatives seriously considered

- **Binary/`BinaryFormatter`/MessagePack payload instead of JSON.** Real advantages: smaller, faster,
  and JSON's number handling is a genuine migration hazard. Lost because the save must be
  human-inspectable during a 12-tester closed beta where "what does this player's ledger actually
  say?" is a support question we will ask weekly, and because
  `BinaryFormatter`-class deserialization is a remote-code-execution pattern we will not introduce
  on any path (RK-34). Size is not a constraint here: `SAVE_MAX_BYTES` is 512 KiB.
- **`PlayerPrefs` for anything durable.** Real advantage: one line of code and it already exists.
  Lost outright: no atomicity, no versioning, no multi-key transaction — and RK-24
  (`docs/prd/risks.md:94`) requires ad/rewind caps to be on the durable ledger precisely because
  PlayerPrefs/memory caps are trivially defeated and lost on process death.
- **SQLite (or any embedded DB) for the ledger.** Real advantages: real transactions, real
  durability, no hand-rolled atomic write, and bounded growth without a hand-written cap. Genuinely
  the "correct" engineering answer. Lost on the sizing rule and on blast radius: it is a new native
  dependency (and therefore a new 16 KB page-size audit target, `docs/plan/specs/architecture.md:111`),
  a new supply-chain row, and a new failure mode on the boot path — to manage a few hundred KB of
  state whose entire concurrency model is "one process, one writer, one thread". Revisit only if the
  ledger ever gains a second writer.
- **Cloud save / RC-hosted state.** Lost: a declared NON-GOAL (`docs/prd/PRD.md:931`), and any server
  component re-opens the entire threat-model class (`docs/prd/risks.md:147`).
- **Trim the dedupe set (LRU) instead of refusing at the cap.** Real advantage: no user-visible
  failure mode and no unbounded structure. Lost because trimming re-opens double-granting for exactly
  the transactions most likely to be redelivered, converting a loud, refundable, ~never-reached error
  into a silent correctness bug.
- **Encrypt or sign the save.** Real advantage: raises the bar on the plaintext-edit path. Lost: the
  key ships in the client, so it buys obfuscation, not integrity — and RK-21 already establishes that
  the right answer is to never let a tampered balance reach a surface with marginal cost, not DRM.

## Consequences

**Easier.** One durable-write path for everything transactional, so "did the grant survive?" and
"did progress survive?" have the same answer and the same test harness. Every `[ARCH]` number has one
home that both the tests and the runtime read (ADR-0009 adds a CI check asserting the
`config/runtime_bounds.json` copy in StreamingAssets is byte-identical to the authored one). The
CM-R05 kill-during-write and migration tests run in the fast `dotnet` leg (ADR-0005) because none of
this needs an engine.

**Harder.** Every payload key added after v1 costs a migration step and a migration test, forever.
The refuse-at-cap ledger policy needs a real player-facing message, not a silent no-op. The
`[ARCH]` numbers now have the authority of a contract: changing one is an ADR amendment, not a tweak.

**Locked in — declare irreversible, human ADR gate:**
1. **Save payload schema shape** (§2) — the moment a tester holds a v1 file.
2. **Ledger dedupe key scheme** (`cm-ledger-v1|productId|transactionId`, 16-byte SHA-256 prefix) —
   changing it after the first grant means either re-granting or under-crediting real buyers.
3. **The binary header layout** — 16 bytes, little-endian, CRC-32 over payload.
4. **The auto-backup decision** (§Open conflict) — it is written into the Android manifest and the
   Data Safety declaration.

## Security notes

- **Threat model deltas:** the save is a local, plaintext, player-writable trust boundary. Everything
  read from it is *our own* data from an untrusted store: the loader validates magic, length and CRC
  before parsing, applies the same depth/size bounds as content (ADR-0008), and treats every field as
  range-checked input. A hostile save must not be able to crash the boot path — falling back to
  `.bak` then to defaults is the required behaviour.
- **RK-19 closed** by the domain-separated key. **RK-20 closed** by cap + refuse + loud error.
  **RK-21** is a wording control, restated above and binding on every published claim (CM-R56.4).
  **RK-17 is *not* closed** — it is escalated as the open conflict above and must be answered before
  the save format merges ("the backup rules must land **with** the save format, not after",
  `docs/prd/risks.md:80`).
- **RK-24 dependency** (`docs/prd/risks.md:94`): ad and rewind daily caps live in `caps` **in this
  file** — never PlayerPrefs, never memory — and are keyed on a date key cross-checked against the
  monotonic clock, mirroring CM-R11.6. The `streak_saver` no-fill fallback must consume the same cap
  slot as a completed watch, written through this same ledger.
- **RK-31/RK-32:** the offline queue is a separate, metrics-only file — **`analytics_queue.dat`,
  fully specified in §5** — with its own bounds; it never carries entitlement, ledger or cap state,
  it is excluded from auto-backup unconditionally, and its contents are player-editable — so any
  published number derived from it ships with the provenance statement CM-R56.3 requires.
- **PII:** no email, no username, no raw transaction id, no price ever enters this file's analytics-
  or third-party-bound fields. `entitlements.appUserId` is the RC anonymous id and is the only
  identifier present.
