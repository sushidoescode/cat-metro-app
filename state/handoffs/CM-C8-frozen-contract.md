# CONTRACT CM-C8 — Analytics offline queue: bounded, ordered, lossy-but-visible, metrics-only

**Roadmap:** D13 (`docs/plan/data/roadmap_56_days.csv:15` — "Typed analytics wrapper (single choke
point; unknown event names assert in dev builds); **offline event queue**").
**DEPENDS-ON:** **CM-C7 merged** — it supplies `dotnet/CatMetro.Application/`, the header/atomic-write
helper, and the `QUEUE_*` rows in `config/runtime_bounds.json` that this contract's criteria read.

### Goal

The Domain/Application half of the offline analytics queue: a bounded, ordered, crash-safe,
metrics-only queue behind `IAnalytics`, with per-event idempotency, drop-oldest overflow accounting and
flush-trigger semantics — all as pure logic, with **no SDK and no engine**.

### Spec reference

`docs/prd/PRD.md` CM-R43.4(a)–(d) (`:687-691`) — read (a) precisely: the MUST test enqueues **exactly
`QUEUE_MAX_EVENTS`** and the 500-event/24 h instance is the *smoke* variant (ADR-0006:222-227) ·
`docs/adr/0006-...` §4 (`QUEUE_*` rows and their rationales, `:182-186,222-245`), §5 (`:269-289` —
`analytics_queue.dat`: path, `"CMQU"` header, same write helper, **non-transactional with respect to
the ledger**, lossy-by-design, **excluded from auto-backup unconditionally**) ·
`docs/adr/0003-...:42,75-77` (`IAnalytics`/`IDiagnostics` declared in Services; SDK types live only in
`Integrations.*`) · `docs/architecture/overview.md:245-249` (the `IAnalytics` signature) ·
`docs/security/threat-model.md:211` (**M-21** — see **Q-U**) · `docs/prd/risks.md` RK-31/RK-32.

### Acceptance criteria (12)

1. **File shape reuses CM-C7's helper.** `analytics_queue.dat` sits beside `save.dat` under
   `IStorageRoot.SaveDirectory` and uses the **same 16-byte header** with magic `"CMQU"`, the same CRC
   check, the same temp+`File.Replace` write path, and the same reject-and-restart-empty behaviour on
   header/CRC failure (ADR-0006:277-279). *Check:* one case asserting the magic and header offsets; one
   asserting a corrupted queue file restarts empty and reports `queue_dropped` with the lost count; one
   asserting the write path is CM-C7's helper (no second implementation — a grep assertion).
2. **Every bound is read from `config/runtime_bounds.json`, never hard-coded.**
   `QUEUE_MAX_EVENTS`, `QUEUE_MAX_BYTES`, `QUEUE_EVENT_MAX_BYTES`, `QUEUE_FLUSH_HIGH_WATER`,
   `QUEUE_FLUSH_TRIGGER` are read at construction. *Check:* one case asserting each constant's live
   value equals the file's row; one grep asserting the five literals appear in no source file.
3. **No-loss / in-order / no-duplicate at the cap.** With **exactly `QUEUE_MAX_EVENTS`** events
   enqueued and the transport unavailable, all of them flush **in enqueue order** with **zero
   duplicates** on reconnect, verified by the per-event idempotency id (CM-R43.4(a),
   `docs/prd/PRD.md:688`). *Check:* one criterion-instance case at `QUEUE_MAX_EVENTS`; one **smoke**
   case at 500 (the ADR-0006:224-227 reading); both asserting order equality and a duplicate count of 0.
4. **Overflow drops oldest-first and says so, on both limbs.** Exceeding `QUEUE_MAX_EVENTS` **or**
   `QUEUE_MAX_BYTES` drops **oldest-first** and emits the named counter `queue_dropped` carrying the
   dropped count (CM-R43.4(b); ADR-0006:263-266; RK-32). *Check:* two cases — the count limb (normal
   events beyond the cap) and the byte limb (`QUEUE_EVENT_MAX_BYTES`-sized events beyond the byte cap)
   — each asserting the surviving set is the newest N, the dropped count is exact, and
   `queue_dropped` fired once per overflow event.
5. **An oversize single event is dropped, not queued.** An event that cannot serialise under
   `QUEUE_EVENT_MAX_BYTES` is dropped with `queue_dropped` and never enters the queue
   (ADR-0006:239-241). *Check:* one case asserting the queue length is unchanged and the counter
   incremented.
6. **Flush fires on exactly the four triggers and on no timer.** `QUEUE_FLUSH_TRIGGER` is exactly
   `["network_reachable","app_foreground","app_pause","high_water"]`, plus the `QUEUE_FLUSH_HIGH_WATER`
   threshold; a **negative test** asserts no flush occurs from elapsed time alone
   (CM-R43.4(c); ADR-0006:242-245 — "exactly these four and **no timer**, so the negative test is
   decidable"). *Check:* four positive cases (one per trigger) + one negative case advancing a
   simulated tick source with no trigger and asserting zero flushes.
7. **Metrics-only, statically.** A `[CI]` check asserts no entitlement, ledger or cap type can be
   written through the queue: zero references to the CM-C7 ledger/entitlement/caps types from
   `unity/Assets/Scripts/Application/Analytics/**`, and the enqueue signature accepts only the
   analytics event type (CM-R43.4(d); ADR-0006:266-267). *Check:* one grep assertion with a negative
   fixture + one reflection case over the public enqueue surface.
8. **Idempotency id survives process restart and flush retry.** Each event carries an id generated at
   **enqueue** time and persisted with the event; reloading the queue file from disk and re-flushing
   after a simulated process death produces the **same ids**, so a retried flush dedupes instead of
   inflating counts. The derivation is **deterministic and reproducible in a test** (not
   `Guid.NewGuid`). *Check:* one case asserting id stability across a save/load cycle; one asserting a
   double flush of the same batch dedupes to one delivery per id; one asserting id uniqueness across
   `QUEUE_MAX_EVENTS` enqueues.
9. **M-21's backup limb — deviation recorded, not met (Q-U).** `docs/security/threat-model.md:211`
   requires an idempotency id "that survives a **backup restore**"; ADR-0006:282,285-289 excludes
   `analytics_queue.dat` from auto-backup **unconditionally**, which makes the restore path
   unreachable rather than idempotent. **This contract implements the four reachable limbs (criteria
   2–8) and records the backup limb as satisfied-by-exclusion, not by the id.** The artifact that makes
   exclusion true is the Android manifest / backup-rules XML — **not in this contract** (Q-G) — and the
   exclusion *set* depends on the unresolved RK-17 decision (ADR-0006 §Open conflict).
   *Check:* the PR carries a written deviation note naming M-21, ADR-0006 §5, Q-G and RK-17; one test
   asserts the queue's persisted record contains the idempotency id (so a future backup-aware design
   has the field it needs). **The criterion fails if the deviation note is absent**, not if the limb is
   unmet.
10. **`IAnalytics` is a Services interface and no SDK is touched.** `IAnalytics` is declared in
    `CatMetro.Services` with the signature at `overview.md:245-249`
    (`void Log(in AnalyticsEvent e)`, `void SetUserProperty(UserPropertyKey, string)`,
    `int QueuedEventCount`); zero SDK namespaces (`Firebase`, `OneSignalSDK`, `GoogleMobileAds`,
    `RevenueCat`) and zero `UnityEngine` appear anywhere this contract writes (ADR-0003:61-64).
    *Check:* one interface-shape case + one grep assertion with a negative fixture.
11. **Non-transactional with the save, by design and by test.** The queue write and the `save.dat`
    write are **two separate atomic writes, never combined**, and the ordering is always
    `save.dat` commit → enqueue → (later) flush, so a crash in the gap loses the **event**, never the
    **grant** (ADR-0006:280; CM-R27.3). *Check:* one case asserting a fault between the two writes
    leaves the grant durable and the event absent; one asserting no code path writes both files in one
    operation (grep + a seam assertion).
12. **Harness discovery.** `tests/analytics/queue.test.sh` exits 0 iff `dotnet test` is green;
    `bash scripts/test.sh` prints `PASS tests/analytics/queue.test.sh` and a summary line matching
    `^test: [0-9]+/[0-9]+ passed` **whose two numbers the wrapper compares equal** (the backreference
    form `\1` is not POSIX ERE — see CM-C2a criterion 13).
    *Check:* `bash scripts/test.sh` exits 0 with both lines and the numeric comparison passing.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C8, plus registration-only appends.

**Explicit non-goals:**
- **No SDK adapter, no `CatMetro.Integrations.Analytics`, no Firebase, no Crashlytics, no OneSignal.**
- **No 45-event taxonomy, no typed event constructors, no required-param tests** (CM-R43.1/.2/.3 are a
  separate contract — this one is the **queue**, not the taxonomy).
- **No sampling, no session logic, no `first_open`/`app_open` semantics** (CM-R43.5/.6/.7).
- **No Android manifest, no backup-rules XML** (Q-G, RK-17 — criterion 9's deviation).
- **No `IDiagnostics` scrubber** (RK-31 — a separate contract).
- **No edits to CM-C7's save code**; the queue reuses its helper, it does not modify it. Needing a
  change there is a stop condition.
- **No writes to immutable paths** (AGENTS.md hard rule 1).

### Assumptions

- **A-C8-1** `CatMetro.Application` and `CatMetro.Services` exist from CM-C7/CM-C2a; CM-C8 adds files
  under paths it owns and edits no csproj (link-glob mechanism).
- **A-C8-2** The idempotency id derivation is analyst-shaped: deterministic per `(enqueue ordinal,
  event payload hash)` so it is reproducible in a test and stable across a reload. **No source
  specifies it**; the *properties* (stable, unique, persisted) are the criteria, the derivation is the
  implementer's and is recorded in the file header.
- **A-C8-3** The queue's "transport" is an injected seam in tests; **no real transport exists in this
  contract**, so "flush" means "hand the batch to the seam and mark it delivered on ack".
- **A-C8-4** M-21's backup limb is out of reach here (**Q-U**) and is recorded, not silently dropped.
- **A-C8-5** The queue's persisted records are serialised through **`CatMetro.Content`'s settings
  factory** (CM-C2a criterion 4, e.g. `ContentJson.Settings`, `TypeNameHandling = None`); CM-C8
  **constructs no `JsonSerializerSettings` of its own** — ADR-0003 permits `Application` → `Content`,
  and CM-C2a's `check.sh` block fails on any `TypeNameHandling` match outside that one file path, which
  CM-C8 may not edit.

### Stop conditions

Defaults apply. Plus:
1. Any criterion appears to need an SDK, a network client or a real transport → stop.
2. Any entitlement, ledger or cap value looks like it belongs in an event → **stop**; that breaks
   CM-R43.4(d) and ADR-0006:266-267 outright.
3. Any need to combine the queue write with the `save.dat` write "for atomicity" → **stop**; that
   inverts CM-R27.3 and would let a crash lose a grant.
4. Any need to change CM-C7's header/write helper → stop; that is a CM-C7 amendment.
5. Making M-21's backup limb true appears to require a manifest or backup-rules change → stop and cite
   Q-U/Q-G/RK-17; do **not** claim the limb met.
6. A `QUEUE_*` value looks wrong for a test to pass → stop; the values are ADR-0006 §4's and changing
   one is an ADR amendment (ADR-0006:374-375).
7. Anything requires `state/mode=production` or touches a monetization path → stop.
