# CONTRACT CM-C7 — Save v1: header + payload, atomic write, migration, ledger dedupe, `[ARCH]` bounds

**Roadmap:** D6 (`docs/plan/data/roadmap_56_days.csv:7` — "Save v1 (versioned JSON + atomic
temp-then-rename); save and analytics flush in OnApplicationPause within the 50 ms budget").
**DEPENDS-ON:** CM-C1 (merged) and **CM-C2a merged** (`dotnet/CatMetro.Services/` project skeleton).
**Blocked on:** **CM-C2a's merge only** — no human, no licence, no Unity. ADR-0006 §Consequences
(`:369-371`) puts the kill-during-write and migration tests in the fast `dotnet` leg precisely because
"none of this needs an engine". **CM-C7 is therefore not a root of the dependency graph**; it is the
first contract that unblocks when CM-C2a lands.

### Goal

The engine-free half of the save system: the 16-byte header, the v1 JSON payload, the atomic
temp+`File.Replace` write behind `IStorageRoot`, the migration table, the domain-separated purchase
ledger **as a data structure**, and the authored `config/runtime_bounds.json` that every other contract
reads.

### Spec reference

`docs/prd/PRD.md` CM-R05.1 (kill-during-write, SI-1…SI-7 at `:161-168`), CM-R05.3 (migration +
downgrade refusal), CM-R05.5 (`SAVE_MAX_BYTES`) · CM-R27.3–.5 (dedupe insert + audit + balance in one
write; FIFO audit cap) ·
`docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md` §1 (`:28-72` header, atomic write,
`.bak` fallback, migration), §2 (`:74-140` payload v1 — **IRREVERSIBLE**, with three OPEN sub-shapes),
§3 (`:142-164` ledger + the RK-19 dedupe key), §4 (`:166-261` the `[ARCH]` constants, verbatim) ·
`docs/adr/0003-...:78-105` (`IStorageRoot`: the seam that keeps `CatMetro.Application` engine-free;
`ISave` declared in Services, implemented in Application) · `docs/architecture/overview.md:224-238`
(the `ISave` / `IStorageRoot` signatures) · `docs/adr/0005-...:112` (save round-trip/migration/ledger
dedupe run in the dotnet leg).

### Acceptance criteria (15)

1. **Header layout, byte-exact.** `save.dat` begins with the 16-byte header of ADR-0006:32-40 —
   `magic "CMSV"` (4), `formatVersion` uint16 LE (2), `saveVersion` uint16 LE (2), `payloadLength`
   uint32 LE (4), `payloadCrc32` uint32 LE (4, CRC-32/IEEE over the payload) — followed by UTF-8 JSON
   with **no BOM**. *Check:* one NUnit case asserting each field's offset, width and endianness on a
   written file, and one asserting byte 16 onward parses as BOM-free UTF-8 JSON.
2. **Payload v1 key set, exactly ADR-0006 §2.** The serialised payload's top-level keys are exactly
   `saveVersion, contentHash, profile, progress, daily, economy, caps, ledger, entitlements, flags,
   breadcrumbs, settings`, with the enumerated sub-shapes: `caps.counters` = the **five** locked ad
   surfaces (`rewind_failure, double_tickets, daily_gift_double, streak_saver, theme_rental`,
   ADR-0006:106-110), `flags` = the **six** ADR-0007 keys (`:118-122`), `ledger` =
   `{keyScheme, dedupe[], audit[]}`. *Check:* one case asserting the exact top-level key set (no more,
   no fewer) and one per enumerated sub-object.
3. **The three OPEN sub-shapes are absent, not guessed.** `caps.sessionCounters`,
   a typed `flags.paywall_placements` beyond `bool`, and any `breadcrumbs.purchase.state` **enum** are
   **not** introduced (ADR-0006:112-137; `overview.md:462`; RK-39 forbids inventing an RC API).
   `breadcrumbs.purchase` is `null` or exactly `{productId, placement, startedAtUtc, state}` with
   `state` carried as an **opaque string round-tripped untouched**. *Check:* three cases asserting each
   open shape is absent/opaque; one case asserting an **unknown key** in a loaded payload round-trips
   unchanged (ADR-0006:72 "A migration step never deletes a key it does not understand").
4. **Atomic write, exactly the ADR's three calls.** Commit = serialise → write `save.dat.tmp` →
   `FileStream.Flush(flushToDisk: true)` on the temp file → close →
   `File.Replace(save.dat.tmp, save.dat, save.dat.bak)` (ADR-0006:48-51). Never write in place.
   *Check:* one case asserting `save.dat.bak` exists with the previous contents after a second commit;
   one asserting no `.tmp` remains after a successful commit; one asserting the sequence via an
   injected filesystem seam. **No directory-fsync and no JNI helper is added** (ADR-0006:54-60).
5. **Kill-during-write leaves one complete version — SI-1…SI-7.** After an interrupted write
   (temp written, `File.Replace` not reached), the loaded save is the **complete previous** version;
   after a completed replace it is the **complete new** version; never a partial file. The loaded
   result satisfies **SI-1…SI-7** (`docs/prd/PRD.md:161-168`) against whichever version loaded.
   *Check:* one NUnit case per SI invariant (7) driven through the injected seam, ×2 interruption
   points.
6. **Load fallback chain never throws.** `save.dat` failing magic/length/CRC falls back to
   `save.dat.bak`; both failing starts a fresh save and reports `error_caught(domain=save_corrupt)`; a
   stale `.tmp` on boot is deleted (**SI-6**); nothing on the boot path throws (ADR-0006:62-67;
   `overview.md:300-301`). *Check:* four cases (bad magic, bad length, bad CRC, both files bad), each
   asserting `LoadResult` ∈ `{Ok, RecoveredFromBackup, Fresh, RefusedDowngrade}` and
   `Assert.DoesNotThrow`; one asserting stale-`.tmp` deletion.
7. **Migration table, ordered, with downgrade refused.** `MigrationTable` is an ordered list of
   `(from, to, Func<JObject, JObject>)` applied in sequence from the file's `saveVersion` to the
   build's; a file whose `saveVersion` **exceeds** the build's is left untouched, the app starts in a
   read-only in-memory default profile, and `save_migrated(from,to,success=false)` is logged
   (CM-R05.3; ADR-0006:68-72). *Check:* one v1→v2 migration case with a stub step; one downgrade case
   asserting the file's bytes are unchanged, the profile is read-only, and the event was recorded.
8. **Dedupe key, domain-separated, 32 lowercase hex.**
   `key = lowercase-hex(first 16 bytes of SHA-256("cm-ledger-v1|" + productId + "|" + transactionId))`
   (ADR-0006:146-153; RK-19). `ledger.keyScheme` persists as `"cm-ledger-v1"`.
   *Check:* one case asserting a pinned key for a fixed `(productId, transactionId)` pair; one
   asserting two different `productId`s with the same `transactionId` produce **different** keys (the
   RK-19 collision that the prefix closes); one asserting the output is exactly 32 lowercase hex chars.
9. **`TryGrant` is the only balance-raising path, and the order is non-negotiable.**
   `ConsumableLedger.TryGrant(transactionId, productId)` computes the key → checks dedupe → mutates
   in-memory state → performs **one** atomic write containing dedupe insert + audit entry + balance
   → **then** returns the value the caller may emit an event from. A fault before the write produces
   neither the balance change nor a grantable event (CM-R27.3/27.4; ADR-0006:155-157).
   *Check:* one case asserting a duplicate `transactionId` grants **zero** the second time; one
   asserting the three mutations land in a single `File.Replace`; one fault-injection case asserting
   neither balance nor dedupe changed and no event value was returned.
10. **RK-20 cap: refuse, never trim.** At `LEDGER_DEDUPE_MAX_ENTRIES` `TryGrant` **refuses**, returns 0
    and reports `error_caught(domain=ledger_capacity)`; the dedupe set is **never** trimmed. The audit
    list is FIFO-capped at `LEDGER_AUDIT_MAX_ENTRIES` (ADR-0006:158-162; CM-R27.5).
    *Check:* one case at the cap asserting refusal + the error + an unchanged set; one asserting the
    audit list drops oldest at its cap while the dedupe set does not.
11. **Size ceiling and the pause budget.** `LastCommittedBytes` is exposed and asserted against
    `SAVE_MAX_BYTES`; `TryCommitWithin(budgetMs)` returns false without writing when it cannot finish
    inside the budget, and the budget default is `SAVE_PAUSE_BUDGET_MS`
    (`overview.md:231-232`; ADR-0006:175-176; CM-R05.5). *Check:* one case asserting a
    synthetic over-cap payload is refused before writing; one asserting `TryCommitWithin(0)` writes
    nothing and returns false.
12. **`config/runtime_bounds.json` is authored here, verbatim, and cannot drift (Q-T).** The file
    contains **exactly the 15 keys of ADR-0006 §4 (`:171-193`) — `schemaVersion` plus 14 constants** —
    and the enumeration below is the authoritative list (an "exactly N" assertion whose N contradicts
    its own enumeration is not testable; N is 15 and the enumeration is what the test asserts):
    `schemaVersion 1 · SAVE_MAX_BYTES 524288 · SAVE_PAUSE_BUDGET_MS 50 · LEDGER_DEDUPE_MAX_ENTRIES 5000
    · LEDGER_AUDIT_MAX_ENTRIES 200 · LEDGER_KEY_SCHEME "cm-ledger-v1" · QUEUE_MAX_EVENTS 2000 ·
    QUEUE_MAX_BYTES 1048576 · QUEUE_EVENT_MAX_BYTES 512 · QUEUE_FLUSH_HIGH_WATER 64 ·
    QUEUE_FLUSH_TRIGGER ["network_reachable","app_foreground","app_pause","high_water"] ·
    ATTRIBUTION_MAX_RESIMS 24 · CONTENT_MAX_FILE_BYTES 262144 · CONTENT_MAX_JSON_DEPTH 16 ·
    CONTENT_BOUNDS_PROFILE "level-schema-v2"` — **15 keys, counted.**
    **Four drift tests:** (a) the file's key set has exactly those 15 members, no more and no fewer;
    (b) every constant this contract uses equals the file's row (no duplicated literal);
    (c) `CatMetro.Content.ContentBounds`'s `CONTENT_MAX_FILE_BYTES` and `CONTENT_MAX_JSON_DEPTH`
    (CM-C2a criterion 5) equal the file's rows;
    (d) `QUEUE_MAX_BYTES ≥ QUEUE_MAX_EVENTS × QUEUE_EVENT_MAX_BYTES`, the inequality ADR-0006:228-238
    says must hold or CM-R43.4(a) fails against its own bounds. **The `[ARCH]` values are copied, never
    chosen** — a value not in ADR-0006 §4 is stop condition 2.
    **Not delivered here, and the PR must say so (Q-Y):** ADR-0009:33 makes the required `ci` job assert
    `config/runtime_bounds.json` ↔ `unity/Assets/StreamingAssets/config/runtime_bounds.json`
    **byte-identity** (ADR-0008 names the copy step). **CM-C7 owns no `StreamingAssets` path** —
    `.../StreamingAssets/content/**` is CM-C2b's, `.../StreamingAssets/config/**` is unowned by every
    contract in this queue, and no `.meta` file exists anywhere under `unity/` until the Q-G scaffold
    lands. See the non-goal below.
13. **Engine-free, through `IStorageRoot`.** `CatMetro.Application` builds under `netstandard2.1` in
    `dotnet/CatMetro.sln`; `IStorageRoot` is declared in Services with exactly the two properties of
    `overview.md:235-238`; **zero** occurrences of `UnityEngine` or `persistentDataPath` appear outside
    `unity/Assets/Scripts/Bootstrap/**` (ADR-0003:102-105). Tests supply a temp directory; no
    `#if UNITY_ANDROID` exists. *Check:* build exit code + two grep assertions + one case running the
    whole suite against a temp `IStorageRoot`.
14. **Monetization tripwire, stated and checked (Q-T).** The ledger here is a **data structure**: a
    `[CI]` grep asserts zero files under `unity/Assets/Scripts/Application/Save/**` match
    `/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds`, and no path this
    contract creates matches the AGENTS.md risky-path globs `**/billing/**`, `**/iap/**`, `**/ads/**`.
    **`state/mode` is not touched and no monetization surface is constructed.** The PR states this
    explicitly and asks for the human ack Q-T names. *Check:* one grep assertion + one
    `git diff --name-only` review showing no risky-path match.
15. **Harness discovery.** `tests/save/save.test.sh` exits 0 iff `dotnet test` is green;
    `bash scripts/test.sh` prints `PASS tests/save/save.test.sh` and a summary line matching
    `^test: [0-9]+/[0-9]+ passed` **whose two numbers the wrapper compares equal** (the backreference
    form `\1` is not POSIX ERE — see CM-C2a criterion 13).
    *Check:* `bash scripts/test.sh` exits 0 with both lines and the numeric comparison passing.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C7, plus registration-only appends (sln entries ·
test-csproj `ProjectReference` · lock file · `config/pins.json` · `scripts/check.sh` blocks).

**Explicit non-goals:**
- **No Unity, no `IStorageRoot` implementation, no `persistentDataPath`** — that is Bootstrap's
  (ADR-0003:43; overview.md:210) and needs Q-G.
- **No SDK, no RevenueCat, no purchase flow, no entitlement fetch, no ad, no paywall.** The ledger
  gains balance only through `TryGrant`, whose caller does not exist yet.
- **No `analytics_queue.dat`, no queue behaviour** — CM-C8 (the two files are deliberately
  non-transactional, ADR-0006:280).
- **No Android manifest, no `allowBackup`, no backup-rules XML** — the RK-17 open conflict
  (ADR-0006:291-333) is a **human decision that must land with the save format**; CM-C7 records it as
  unresolved and ships neither posture.
- **No `contentHash` computation** (ADR-0008's catalog pipeline is a later contract); the key exists and
  round-trips.
- **No `unity/Assets/StreamingAssets/config/**`, and therefore no copy step (Q-Y).** The
  `config/runtime_bounds.json` ↔ `unity/Assets/StreamingAssets/config/runtime_bounds.json`
  byte-identity assertion of ADR-0009:33 (ADR-0008's copy step) is **deferred to the content-pipeline
  contract that owns `StreamingAssets`**. **The PR records that this `ci` clause is unsatisfiable until
  then and names the follow-up by name** — it is not silently left to fail. CM-C7 authors only
  `config/runtime_bounds.json`.
- **No economy values** — `config/economy_defaults.json` is a human decision (CM-R04.1) and is not
  authored here.
- **No writes to immutable paths** (AGENTS.md hard rule 1).

### Assumptions

- **A-C7-1** Authorship of `config/runtime_bounds.json` is assigned to this contract by the tranche-2
  decomposition (**Q-T**), resolving tranche 1's CM-C2 stop condition 6. The values are ADR-0006's; the
  assignment is the analyst's and needs human ratification.
- **A-C7-2** The ledger is a data structure, not a monetization surface (**Q-T**). If the human rules
  otherwise, CM-C7 stops until `state/mode` is `production` (`state/PROJECT_STATE.md:10`).
- **A-C7-3** The save's payload is serialised with the CM-C2a-pinned Newtonsoft version and
  `TypeNameHandling = None` (ADR-0006:337-342 rejects `BinaryFormatter`-class deserialization outright).
  **CM-C7 reuses `CatMetro.Content`'s settings factory (CM-C2a criterion 4, e.g. `ContentJson.Settings`)
  and constructs none of its own** — ADR-0003 permits `Application` → `Content`. This is not a style
  preference: CM-C2a's appended `check.sh` block fails on any `TypeNameHandling` match outside that one
  file path, and CM-C7 may not edit an existing block, so a second settings site would be unmergeable.
- **A-C7-4** `saveVersion` starts at 1 and `formatVersion` starts at 1 (ADR-0006:35-36). The v1→v2
  migration test uses a **stub** v2 step; no real v2 schema is invented.
- **A-C7-5** The RK-17 backup decision and the three open payload sub-shapes stay open; shipping v1
  without them is legal because none of the three has a *present* consumer — but **the moment a tester
  device holds a v1 file the payload shape is irreversible** (ADR-0006:100-104,377-379), which is why
  the PR must carry the human ADR gate explicitly.

### Stop conditions

Defaults apply. Plus:
1. Any criterion requires an SDK call, a store type, a purchase flow or an ad → **stop**; that is the
   monetization tripwire and needs `state/mode=production` first.
2. **Any temptation to choose an `[ARCH]` value not present in ADR-0006 §4** → stop; copy or stop.
3. The RK-17 auto-backup decision appears to be needed to make a criterion pass → stop and escalate
   (ADR-0006 §Open conflict; "the backup rules must land **with** the save format").
4. Any of the three OPEN payload sub-shapes appears to need a concrete type → **stop**; enumerating
   `breadcrumbs.purchase.state` would be inventing an RC API, which RK-39/A-07 forbid.
5. Directory-fsync, JNI or a native helper looks necessary for durability → stop (ADR-0006:54-60).
6. Trimming the dedupe set looks like the fix for the cap → **stop**; refuse-at-cap is the decision
   (ADR-0006:356-359) and trimming re-opens double-granting.
7. `config/runtime_bounds.json` authorship is contested by the human (Q-T) → stop and re-cut.

---
