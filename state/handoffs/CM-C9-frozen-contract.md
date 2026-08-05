# CONTRACT CM-C9 — Event taxonomy: the 45-row table, the typed choke point, the closed param wall

**Status:** **FREEZE-READY** (tranche 3, Wave 1). Supersedes `t3-taxonomy-draft-contract.md`; the id is
no longer provisional — **`CM-C9`** was assigned by the human on 2026-08-05 (R4 below), and the
competing `CM-C9` claim from the staging draft moved to **CM-C10** (cross-check C2 closed).
**Roadmap:** D13 (`docs/plan/data/roadmap_56_days.csv:15` — "Typed analytics wrapper (single choke
point; unknown event names assert in dev builds)" and "Remaining P0 taxonomy events implemented
(**economy/ads/monetization rows staged dark until their surfaces exist**)"). CM-C8 took the second
half of that row (the offline queue); this contract takes the first.
**DEPENDS-ON:** **CM-C8 merged** (#18) — it supplies `IAnalytics`/`AnalyticsEvent`
(`unity/Assets/Scripts/Services/Analytics/IAnalytics.cs:10-32`) and the queue that transports what
this contract builds. CM-C8's own non-goal names this contract by requirement id:
"**No 45-event taxonomy, no typed event constructors, no required-param tests** (CM-R43.1/.2/.3 are a
separate contract — this one is the **queue**, not the taxonomy)" (`state/backlog.md:1676-1677`;
`state/handoffs/CM-C8-frozen-contract.md:105-106`).
**Blocked on:** nothing. No Unity, no licence, no device, no human in the loop to start
(pure C#, `dotnet` leg; the Unity host runs the same sources through the existing asmdef).
**Wave:** **Wave 1**, in its own worktree, parallel with **CM-C10** (stager) and **CM-C5.1**
(dead-mechanic gate) and with the in-flight DEVCAP → DEVFIX pair. File-disjoint from all of them; the
only shared file is `state/PROJECT_STATE.md` (one appended line on merge).

---

## Ratifications (human, in-session 2026-08-05) — binding; recorded before freeze

| # | Ratification as given | Effect on this contract |
|---|---|---|
| **R2 (HC-4)** | **Declaring all 45 events, including the dark monetization/ads/economy factories, does NOT trip the AGENTS.md monetization tripwire. TAX keeps the 45-count criteria.** | Open question **H-3 is CLOSED**. Criterion 1 (45 rows), criterion 2 (45 factories) and criterion 9 (staged-dark wall) ship **as drafted, un-narrowed**. The contract still creates **no** path matching `**/billing/**`, `**/iap/**`, `**/ads/**`, adds **zero** call sites, and does **not** touch `state/mode` — criterion 9 is the static proof of exactly that, and it is unchanged. Stop condition 9 stands unchanged: the ratification legalises *declaring*, never *emitting* or *constructing a surface*. |
| **R3 (HC-5)** | **The five-item backlog ownership amendment batch is delegated to the agent** (exact text drafted by the agent). | The `Application/EventTaxonomy/**` carve-out this contract needs is **item 1 of that batch**, drafted at `t3-backlog-amendment.md` (EDIT-1 + EDIT-2). A-C9-2's "analyst-proposed, not taken" becomes "drafted under delegation; applied to `state/backlog.md` by the human/queue owner before or with this PR". |
| **R4 (ids + sequencing)** | **ids: TAX = CM-C9, stager = CM-C10, LVL = CM-C11, DM = CM-C5.1. Wave 1 = TAX + DM + stager in parallel worktrees; LVL is Wave 2, after the stager merges. DEVFIX's 7 Presentation lines precede the UX lane.** | Open question **H-7 is CLOSED**. Title, branch (`task/CM-C9-taxonomy`) and handoff filenames use `CM-C9`. Nothing else in the contract depends on the string. This contract has zero dependency on the content quartet and does not queue behind it. |
| **R1 (HC-1/HC-2)** *(recorded for context; no effect here)* | `scripts/stage-content.sh` (CM-C10) is THE single staging author, with deterministic path-derived guids for staged `.meta` files. ContentSync is **not** frozen now; it will be re-cut later as **assert-only**. | This contract stages nothing and writes no `StreamingAssets` path (A-C9-3 already rejected the runtime-config alternative for exactly this reason). Its own new `.cs.meta` files under `unity/Assets/Scripts/**` and `unity/Assets/Tests/**` are **editor-generated**, not stager output — the stager's two rules cover `content/levels/*.json` and `config/runtime_bounds.json` only (CM-C10 criterion 2). A-C9-8 is unchanged. |
| **Earlier same-session, tranche-3 relevant** | CM-C3's device legs use a **dev-only failable-level override**; **URP is being restored by CM-C2b-DEVFIX**. | Neither touches this contract: it writes no `Presentation/**`, no `Bootstrap/**`, no scene, no `ProjectSettings/**`, and renders nothing. Recorded so no implementer infers a dependency. |

**Cross-check dispositions applied to this file:** C2 (id clash) closed by R4; C6 (wrapper-count
literals) applied as the rule below; matrix row `Application/EventTaxonomy/**` closed by the delegated
batch. No other cross-check finding names this contract.

### Rule (cross-check C6) — wrapper counts are relative, never literal

**No criterion in this contract may state a literal `scripts/test.sh` wrapper count.** Where a count is
asserted it reads: *"`bash scripts/test.sh` is green at **N+1**, where **N** is the wrapper count
printed by `bash scripts/test.sh` on this branch's **rebase baseline** (`git merge-base HEAD main`),
captured and pasted in the handoff note **before** the first new wrapper is added."* A contract that
adds no wrapper asserts **N unchanged**. Rationale: four lanes (CM-C9, CM-C10, CM-C11, DEVCAP,
DEVFIX) are each adding a wrapper in parallel, so any literal is false for whichever lands second —
`state/PROJECT_STATE.md:8` itself already carries two different literals (8/8 and 10/10) from
different days. This contract adds **exactly one** wrapper (`tests/taxonomy/taxonomy.test.sh`), so its
target is **N+1**.

### Revision log vs `t3-taxonomy-draft-contract.md`

1. Title/id: provisional `CM-C9` → assigned `CM-C9` (R4); the staging draft's competing claim is now CM-C10.
2. Ratifications block added (above).
3. H-3 (monetization tripwire) moved from open → **CLOSED by R2**; criteria 1/2/9 explicitly retained at 45.
4. H-7 (id + queue position) moved from open → **CLOSED by R4**; Wave-1 placement stated.
5. A-C9-2 restated: the ownership carve-out is drafted under the delegated batch rather than "proposed in the PR".
6. Criterion 13 gains the explicit **N+1** clause (C6 rule).
7. Open-questions section replaced by an explicit **RIDES-WITH-PR** table (H-1, H-2, H-5, H-6, merge delegation), each with its ADR-gate flag.
8. **No criterion was weakened, renumbered or removed.** 13 criteria in, 13 criteria out.

---

### Goal

The engine-free, SDK-free typed choke point for analytics: the 45-row taxonomy table copied from
`docs/plan/data/analytics_event_taxonomy.csv` and drift-tested against it, 45 typed factories that are
the **only** place an `AnalyticsEvent` is constructed in shipped code, required-param and
closed-param-set enforcement that fails typed rather than throwing, and a static wall proving the
monetization/ad rows are **declared but staged dark** — zero call sites, zero ledger or entitlement
value ever written through the queue. No event is emitted by this contract; no gameplay, UI or
Bootstrap file is touched.

### Spec reference

- `docs/prd/PRD.md:681-696` — **CM-R43** in full. Load-bearing sub-lines:
  - `:684` **CM-R43.1** `[CI]` count test — "exactly 45 event types are emittable; adding a 46th
    without a taxonomy row fails the build"; plus the TD-01/NEW-Q36 caveat ("the count stays 45 —
    unless NEW-Q36 resolves to a separate event").
  - `:685` **CM-R43.2** `[CI]` typed-wrapper test — "no call site constructs an analytics event by
    raw string; a static check fails on any direct SDK call outside the wrapper".
  - `:686` **CM-R43.3** `[CI]` required-param test — "each event type fails to construct without its
    required params (`data/analytics_event_taxonomy.csv`, per-row `required_params`)".
  - `:691` **CM-R43.4(d)** — the queue "carries **metrics only** — a static check asserts no
    entitlement, ledger or cap state is written through it". **This contract must not violate it; see
    stop condition 2 and the CONFLICT block below.**
  - `:715` CM-R45.1's four privacy classes (`behavioral_no_pii / behavioral_ad / transactional /
    diagnostic`) — the column this table carries.
  - `:705`, `:998`, `:1068` — **TD-01 / NEW-Q36**, the unresolved `level_started` completion-state
    param (45 vs 46). **Human-pinned; not an agent choice** (stop condition 1).
  - `:1047` — **NEW-Q20**: `device_tier` derivation, `rank_bucket` producer, `restore_started` QA are
    undefined. The table copies the CSV's silence; it invents no value domain (assumption A-C9-6).
  - `:208` — the attempt-1 invariant (zero paywall/ad **events** on attempt 1). Criterion 9 is the
    static form of "and none of them can fire yet".
- `docs/plan/data/analytics_event_taxonomy.csv:1` (header) and `:2-46` (**the 45 rows — the source of
  truth this contract copies and never edits**). Rows cited by criteria: `:12` `rewind_used`, `:16`
  `streak_changed`, `:19` `ticket_earned`, `:20` `ticket_spent`, `:21` `cosmetic_unlocked`, `:33`
  `restore_started` (**zero required params — the empty case**), `:34` `restore_completed`, `:35`
  `entitlement_changed`, `:41` `challenge_opened`, `:44` `error_caught`, `:45` `perf_sample`.
- `docs/plan/EXECUTION_PLAN.md:190` — the only source for the wrapper requirement ("45-event taxonomy
  behind one typed wrapper").
- `docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md:244-246` —
  `QUEUE_EVENT_MAX_BYTES = 512`: "a single event that cannot be serialized under this is a bug (**no
  free text is permitted in the taxonomy**, RK-30); it is dropped with `queue_dropped`". Criterion 11
  is the taxonomy-side proof that this can never happen. Also `:166-194` (§4 — the file the bound is
  read from, never re-declared), `:268-272` (metrics-only), `:274-288` (§5 — the queue file, whose
  payload shape this contract does **not** change), `:283` (bare JSON array payload).
- `docs/architecture/overview.md:245-249` — the `IAnalytics` signature, annotated "**typed only; no
  raw event strings outside the adapter**". `:213` — ADR-0003's rule that SDK types live in
  `Integrations.*`; that assembly still does not exist and this contract does not create it.
- `unity/Assets/Scripts/Services/Analytics/IAnalytics.cs:5-9` (the shipped comment that assigns the
  taxonomy to this contract), `:10-19` (`AnalyticsEvent(string, JObject)` — the struct this contract
  builds), `:21-25` (`UserPropertyKey` "Empty by design, not by omission" — **left empty here**, see
  non-goals and the RIDES-WITH-PR row H-6).
- `state/handoffs/CM-C8.md:41-42` (A-C8-8 — the recorded notes this contract does **not** convert),
  `:52-55` (**A-C8-11** — "the metrics-only wall is TYPE-level… The typed taxonomy wrapper
  (CM-R43.1-.3) is where value-level enforcement becomes possible"; criteria 4 and 5 are that
  enforcement), `:56-59` (A-C8-12 — the durable dropped-total, **out of scope**, non-goal 6),
  `:29-35` (A-C8-6 — the queue's id derivation; criterion 12 must not perturb it).

---

### CONFLICT — recorded, not resolved by this contract (RIDES-WITH-PR call H-2)

`docs/prd/PRD.md:691` and `docs/adr/0006-...:268-272` say the queue carries **metrics only** and that
"no entitlement, ledger or cap state is ever written through it". The **pinned taxonomy** requires the
opposite of five rows: `rewind_used.balance_after` (`csv:12`), `ticket_earned.balance_after` (`:19`),
`ticket_spent.balance_after` (`:20`), `restore_completed.entitlements_restored_count` (`:34`),
`entitlement_changed.entitlement_id/change` (`:35`) are **required params**. Both are MUST-level and
both are pinned corpus. Two readings, neither picked here:

- **(R1) system-of-record reading** — (d) bans the queue being a *source of truth* for ledger state; a
  metric *copy* of a balance is legal. CM-C8's shipped check is type-level, which is R1 in code
  (A-C8-11, `state/handoffs/CM-C8.md:52-55`).
- **(R2) value-level reading** — no ledger/entitlement-derived *value* may ride an event at all; then
  the five rows must be amended in `docs/plan/data/analytics_event_taxonomy.csv`, which is a
  published-contract change and an ADR-0006 §4 amendment.

**What this contract does under the conflict:** it ships the table **verbatim** (R1-compatible and
R2-compatible, because a declared row is not an emitted value) and adds **zero call sites**, so no
ledger or entitlement value is written through the queue by this diff — criteria 9 and 10 make that
statically true and testable. Choosing R1 or R2 is human; picking one silently is stop condition 2.
**Note the 2026-08-05 ratification does not touch this conflict:** R2 (HC-4) answered the *tripwire*
question (may we declare monetization rows at all — yes), not the *metrics-only* question (may a
ledger-derived value ride an event — still open, ADR-gated).

---

### Acceptance criteria (13)

Each is independently checkable by the named command; a criterion is met only when the named check
exits 0 (or fails exactly as specified). "Typed failure" everywhere means a returned discriminated
result, never a thrown exception (CM-C2a criterion 7 precedent, `state/backlog.md:369-376`).

1. **The table is the CSV — 45 rows, parsed, drift-tested, nothing invented.**
   `CatMetro.Application.EventTaxonomy.Taxonomy.Rows` exposes exactly **45** rows; each row carries
   `Name · Area · RequiredParams[] · OptionalParams[] · ValueDomains{param → allowed[]} ·
   UserProperties[] · Destinations[] · PrivacyClass · QaProcedure`, equal field-for-field to
   `docs/plan/data/analytics_event_taxonomy.csv:2-46` read from disk at test time via
   `Fixtures.RepoRoot()` (`unity/Assets/Tests/EditMode/Pure/Domain/Fixtures.cs:154-170`; the
   `SFixtures.RepoBounds()` pattern, `.../Pure/Save/SaveFixtures.cs:69-77`). The CSV reader handles
   **quoted fields containing commas** (rows `:2,:6,:9,:10` and 20 others) and **inline value domains**
   (`source(free|purchased|rewarded)`, `csv:12`) — the param **name** is the token before `(`.
   Every row's `PrivacyClass` is one of the four at `docs/prd/PRD.md:715` (CM-R45.1's build-time half,
   free here; the rest of CM-R45 is a non-goal).
   *Check:* one case asserting `Rows.Count == 45`; one `[TestCaseSource]` case **per row** (45)
   asserting all nine fields equal the parsed CSV field; one asserting the CSV has 46 non-empty lines
   (header + 45); one asserting every `PrivacyClass` ∈ the four. **Fails if** a row is added, removed
   or edited on either side. *(Retained at 45 under R2 — the dark rows are declared, not dropped.)*

2. **Exactly 45 emittable event types, in bijection with the table (CM-R43.1).**
   `EventTaxonomy.Events` exposes exactly 45 public static factory methods returning
   `CatMetro.Services.AnalyticsEvent`; the snake_case form of each method name maps 1:1 onto a table
   row, **both directions**, and the failure message names the offending side (extra factory / row
   with no factory).
   *Check:* one reflection case asserting the factory count is 45; one asserting the bijection over
   the live sets; one **negative** case feeding a synthetic 46-name set through the *same* comparison
   helper and asserting it reports the extra name (this is the testable form of "adding a 46th without
   a taxonomy row fails the build" — a real 46th factory cannot be added inside a test).

3. **Required params: constructing without one is a typed failure, per row (CM-R43.3).**
   `Taxonomy.TryBuild(string name, JObject parameters, out AnalyticsEvent e, out string error)`
   returns `false` and names the **missing key** when any of the row's `required_params` is absent,
   and never throws. `restore_started` (`csv:33`) has **zero** required params and builds from an
   empty object — the empty case is asserted, not skipped.
   *Check:* one `[TestCaseSource]` case per `(row, requiredKey)` pair — **124 cases**, the CSV's
   `required_params` entries counted across `:2-46` — asserting `false` + the key named +
   `Assert.DoesNotThrow`; one case asserting the generated pair count is exactly 124 (so a silently
   shrinking case source cannot pass); one case per row asserting the complete required set builds
   `true`; one case asserting `restore_started` builds from `{}`.

4. **Closed param set — an undeclared key is rejected (the value-level wall, A-C8-11).**
   A key outside `required ∪ optional` for that row is a typed failure naming the key. This is the
   enforcement `state/handoffs/CM-C8.md:52-55` says only the typed wrapper can add.
   *Check:* one case per row (45) adding `zz_unknown` and asserting `false`; one case asserting
   `level_started` rejects `balance_after` and `entitlement_id` (ledger-shaped keys the row does not
   declare) — the CM-R43.4(d) regression guard in its testable form.

5. **Value domains are enforced exactly where the CSV declares them, and nowhere else.**
   The seven inline domains — `rewind_used.source(free|purchased|rewarded)` (`csv:12`),
   `streak_changed.change(inc|reset|saved)` (`:16`), `ticket_earned.source(level|daily|gift|event|ad)`
   (`:19`), `ticket_spent.sink(cosmetic|saver)` (`:20`),
   `cosmetic_unlocked.method(tickets|iap|event|default)` (`:21`),
   `entitlement_changed.change(granted|revoked|expired)` (`:35`),
   `challenge_opened.source(link|code)` (`:41`) — accept exactly their listed values and typed-fail
   on anything else. A param with **no** declared domain accepts any JSON scalar; **no domain is
   invented** (`device_tier`, `rank_bucket`, `fail_reason` stay open — NEW-Q20, `docs/prd/PRD.md:696`).
   *Check:* 7 accept cases + 7 reject cases + one case asserting the stored param **name** is the
   bare token (`source`, not `source(free|purchased|rewarded)`) + one case asserting a
   domain-less param (`level_id`) accepts an arbitrary string.

6. **Unknown event names never become events (roadmap `:15`, "unknown event names assert in dev
   builds").** `TryBuild("not_an_event", …)` returns `false` naming the unknown name and yields no
   `AnalyticsEvent`; the dev-build *assert* form is a Bootstrap/composition concern and is **not**
   built here (non-goal 3).
   *Check:* one case asserting `false` + no event produced; one asserting a near-miss
   (`level_start` vs `level_started`) is also rejected.

7. **One construction site in shipped code (CM-R43.2).** Across `unity/Assets/Scripts/**`, the token
   `new AnalyticsEvent(` appears in **exactly one** file — the taxonomy builder — and zero SDK
   namespaces (`Firebase|OneSignalSDK|GoogleMobileAds|RevenueCat`) appear anywhere under
   `unity/Assets/Scripts/**` (`Integrations.*` does not exist; `docs/architecture/overview.md:213,245`).
   The scan root is **`Scripts` only**: `unity/Assets/Tests/**` is excluded by design, because CM-C8's
   shipped tests construct `AnalyticsEvent` directly and this contract may neither edit nor break them
   (AGENTS.md hard rule 5; `state/backlog.md:123` ownership). All scan roots are **explicit and
   repo-root-relative — never `.`** (`.claude/worktrees/ux-lane/` is a second full checkout and would
   double every match).
   *Check:* two greps in `tests/taxonomy/taxonomy.test.sh` (exact-count = 1; SDK count = 0), each
   **proven live** against `tests/fixtures/taxonomy-bad/Banned.cs` (the `analytics-bad/Banned.cs`
   pattern, `tests/fixtures/analytics-bad/Banned.cs:1-11`) — a dead pattern fails the wrapper.

8. **The metrics-only wall is inherited over the new root, not dodged.** The taxonomy sources live
   outside `unity/Assets/Scripts/Application/Analytics/**` (ownership, below), so CM-C8's wrapper greps
   (`tests/analytics/queue.test.sh:37,41-43,46-48,55-57`) do not scan them. This contract therefore
   **re-applies the same guards over its own root**: zero
   `ConsumableLedger|SaveStore|SaveState|SaveDefaults|MigrationTable|LoadResult|RealSaveFileSystem|SaveEventRecord`
   and zero SDK tokens under `unity/Assets/Scripts/Application/EventTaxonomy/**`, plus a reflection
   case asserting **no factory parameter type** is declared in `CatMetro.Application.Save`.
   *Check:* two greps + the same negative fixture + one reflection case over all 45 factories'
   parameter types.

9. **Staged dark: the monetization/ad rows are declared and unreferenced.**
   `docs/plan/data/roadmap_56_days.csv:15` stages "economy/ads/monetization rows … dark until their
   surfaces exist" and `docs/prd/PRD.md:208` forbids the attempt-1 paywall/ad event path.
   **R2 (2026-08-05) ratified that declaring these 15 factories does not trip the AGENTS.md
   monetization tripwire; this criterion is what keeps that ratification honest.** A grep asserts that
   **no file under `unity/Assets/Scripts/**` outside `Application/EventTaxonomy/**` references any of
   the 15 factory names whose `area` column is `economy`, `ads` or `monetization`** (rows
   `csv:19,20,23-35` — 2 + 5 + 8, counted), and that **no path this contract creates matches** the
   AGENTS.md risky-path globs `**/billing/**`, `**/iap/**`, `**/ads/**` — the taxonomy sources are one
   flat folder; no `Ads/`, `Billing/` or `Iap/` subfolder is created (the string value `iap` inside
   `cosmetic_unlocked.method(...)`, `csv:21`, is data, not a path).
   *Check:* one wrapper grep over the 15 names (with the fixture proving it fires) + **one NUnit case
   asserting the wrapper's hard-coded 15-name list equals the table's `area ∈ {economy, ads,
   monetization}` set** (so the shell list cannot drift from the data) + one `git diff --name-only`
   review pasted in the PR showing zero risky-path matches. **`state/mode` is not touched.**

10. **CM-R43.4(d) conflict note, written (criterion fails if absent).** The PR carries a written
    deviation/conflict note naming CM-R43.4(d) (`docs/prd/PRD.md:691`), ADR-0006:268-272, the five
    conflicting rows (`csv:12,19,20,34,35`), readings R1/R2 above, and A-C8-11
    (`state/handoffs/CM-C8.md:52-55`) — and states plainly that this diff emits nothing, so no ledger
    value reaches the queue today. It also states that the 2026-08-05 tripwire ratification (R2) did
    **not** resolve this conflict. *Check:* the note exists in the PR body and names all five rows
    (CM-C8 criterion 9 precedent, `state/handoffs/CM-C8-frozen-contract.md:70-80`: **the criterion
    fails if the note is absent**, not if the conflict is unresolved).

11. **Every canonical event fits `QUEUE_EVENT_MAX_BYTES`, read from config and never re-declared.**
    For each of the 45 rows a canonical fixture (all required + all optional params populated with
    representative values) builds an event whose **persisted record length** — computed the way
    CM-C8 computes it (`AnalyticsQueue.cs:247-248`, A-C8-9) — is `< QUEUE_EVENT_MAX_BYTES` **read from
    `config/runtime_bounds.json`** (ADR-0006:244-246: an oversize event "is a bug… no free text is
    permitted in the taxonomy"). No queue bound literal appears in any source this contract writes.
    *Check:* 45 cases asserting `record.Bytes < bounds.QueueEventMaxBytes`; one grep asserting the
    literals `2000|1048576|512|64` appear in no file under
    `unity/Assets/Scripts/Application/EventTaxonomy/**` (CM-C8 criterion 2's discipline, applied to
    this root by this contract's own wrapper).

12. **The queue is used and not touched.** One integration case builds all 45 canonical events through
    the factories, `Log`s them into a real `AnalyticsQueue` over a temp storage root, and asserts they
    persist and reload in order with stable ids; **zero files under
    `unity/Assets/Scripts/Application/Analytics/**`, `unity/Assets/Scripts/Services/**` or
    `tests/analytics/**` are modified**, and `bash tests/analytics/queue.test.sh` still exits 0
    unchanged.
    *Check:* one integration case + `git diff --name-only` review + the CM-C8 wrapper run pasted in
    the PR. **Fails if** the diff touches a CM-C8-owned path.

13. **Harness discovery and dual-host parity.** `tests/taxonomy/taxonomy.test.sh` exits 0 iff
    `dotnet test dotnet/CatMetro.sln -c Release` is green **and** every `[CI]` grep above holds
    (fail-closed on a missing scan root); `bash scripts/test.sh` prints
    `PASS tests/taxonomy/taxonomy.test.sh` and a summary line matching `^test: [0-9]+/[0-9]+ passed`
    **whose two numbers the wrapper compares equal** (`scripts/test.sh:24`; the backreference form
    `\1` is not POSIX ERE — CM-C2a criterion 13), and the wrapper total is **N+1**, where **N** is the
    count captured on this branch's rebase baseline (`git merge-base HEAD main`) and pasted in the
    handoff note before the wrapper was written — **never a literal** (cross-check C6 rule above). The
    same sources run in the Unity EditMode host with **zero csproj and zero asmdef edits** (the link
    glob at `dotnet/CatMetro.Application/CatMetro.Application.csproj:19` and the existing
    `CatMetro.Application.asmdef` / `CatMetro.Tests.EditMode.asmdef` pick the new folders up), and the
    EditMode case count rises by the number of new cases.
    *Check:* `bash scripts/test.sh` exit 0 with both lines and the baseline/after counts pasted; the
    `tests/unity/editmode.test.sh` editor half (`:79-107`) pasted showing the new EditMode total, or
    its "editor half DEFERRED" line if the pinned editor is absent on the runner (`:109`).

### Scope boundary

**In scope:** exactly the files in the table below, plus registration-only appends
(`state/backlog.md:125-131`). **No `Compile Include` append is permitted in any csproj**
(`state/backlog.md:146-155`) and none is needed.

#### Complete file table

| Path | Action | Ownership note |
|---|---|---|
| `unity/Assets/Scripts/Application/EventTaxonomy/TaxonomyRow.cs` | NEW | carve-out from CM-C2b's `Application/**` (`state/backlog.md:117`) — **drafted in the delegated batch, `t3-backlog-amendment.md` EDIT-1/EDIT-2**; the PR names the amendment |
| `unity/Assets/Scripts/Application/EventTaxonomy/Taxonomy.cs` (table + CSV-shaped row data + `TryBuild`) | NEW | same carve-out |
| `unity/Assets/Scripts/Application/EventTaxonomy/Events.cs` (the 45 factories; **the one** `new AnalyticsEvent(` site) | NEW | same carve-out |
| `unity/Assets/Scripts/Application/EventTaxonomy/*.cs.meta` (one per source) | NEW | generated by the pinned editor 6000.3.16f1, committed alongside (CM-C8 precedent: `AnalyticsQueue.cs.meta`). **Editor-generated, not stager output** — CM-C10's rules cover only `content/levels/*.json` and `config/runtime_bounds.json` |
| `unity/Assets/Tests/EditMode/Pure/EventTaxonomy/TaxonomyFixtures.cs` | NEW | unowned path — `Pure/<Area>/**` is per-contract (`state/backlog.md:115-123`) |
| `unity/Assets/Tests/EditMode/Pure/EventTaxonomy/TaxonomyTableTests.cs` (criteria 1, 2) | NEW | unowned |
| `unity/Assets/Tests/EditMode/Pure/EventTaxonomy/TaxonomyBuildTests.cs` (criteria 3, 4, 5, 6) | NEW | unowned |
| `unity/Assets/Tests/EditMode/Pure/EventTaxonomy/TaxonomyWallTests.cs` (criteria 8-reflection, 11, 12) | NEW | unowned |
| `unity/Assets/Tests/EditMode/Pure/EventTaxonomy/*.cs.meta` (one per test source) | NEW | as above |
| `tests/taxonomy/taxonomy.test.sh` | NEW | unowned — deliberately **not** `tests/analytics/**` (CM-C8's, `state/backlog.md:123`) |
| `tests/fixtures/taxonomy-bad/Banned.cs` (grep bait; never compiled) | NEW | unowned — mirrors `tests/fixtures/analytics-bad/Banned.cs` |
| `state/handoffs/CM-C9.md` + `state/handoffs/CM-C9-frozen-contract.md` | NEW | build-loop notes (session convention) |
| `state/PROJECT_STATE.md` | APPEND 1 line on merge | four lanes append in parallel; ~150-line cap |

**Touched by nothing in this contract (assert, do not edit):** `unity/Assets/Scripts/Application/Analytics/**`,
`unity/Assets/Scripts/Services/**`, `tests/analytics/**`, `unity/Assets/Scripts/Bootstrap/**`,
`unity/Assets/Scripts/Presentation/**`, `unity/Assets/Resources/Strings/ui.csv`,
`unity/Assets/Scripts/Domain/**`, `unity/Assets/Scripts/Content/**`, `unity/Assets/StreamingAssets/**`,
`config/**`, `scripts/**`, `docs/plan/**`, `.github/**`, `unity/ProjectSettings/**`.

**Parallel-lane safety (checked against every live lane at freeze time):**
- The **UX lane** (`state/handoffs/SESSION-HANDOFF-ux.md:27-39`) owns `Presentation/**`, append-only
  `ui.csv` rows, its own tests and — after DEVCAP/DEVFIX merge — `tests/unity/editmode.test.sh`. This
  contract touches **none** of those. No `ui.csv` row is added (no UI string exists here).
- **CM-C3-DEVCAP** and **CM-C2b-DEVFIX** (Bootstrap-owned, in flight; DEVFIX's 7 Presentation lines
  precede the UX lane per R4) own `Bootstrap/**`, `ProjectSettings/**` and `tests/unity/devcap.test.sh`.
  This contract touches none of those and adds **no** composition-root wiring (non-goal 3), so it can
  land before, between or after them in any order.
- **CM-C10** (stager) and **CM-C5.1** (dead-mechanic gate), the two other Wave-1 lanes: no shared
  writable path (cross-check §1 matrix).
- The only shared file is `state/PROJECT_STATE.md` (one appended line on merge).

**Explicit non-goals:**
1. **No call sites, no instrumentation, no emission.** Nothing in `Presentation/**`, `Bootstrap/**`,
   `Session/**` or `Retry/**` learns to log an event here. Wiring the choke point into the game is a
   later contract (and, for the ad/monetization rows, gated on `state/mode=production`).
2. **No SDK, no `Integrations.Analytics`, no Firebase/Crashlytics/OneSignal/RevenueCat**, no
   destination routing. The `destinations` column is **data in the table**, not behaviour.
3. **No dev-build assert, no `#if DEVELOPMENT_BUILD`, no composition root.** Roadmap `:15`'s "assert
   in dev builds" needs an engine-side seam that does not exist; criterion 6's typed failure is the
   engine-free half, and the assert half is recorded as deferred in the PR.
4. **No sampling (CM-R43.5), no session/`app_open`/`first_open` semantics (CM-R43.6/.7), no
   `daily_started` seed QA (CM-R43.8).** Those are separate requirements with separate contracts.
5. **No `UserPropertyKey` members.** The enum lives in CM-C8-owned
   `unity/Assets/Scripts/Services/Analytics/IAnalytics.cs:21-25` ("Empty by design"); populating it
   from the CSV's `user_properties_updated` column is a cross-contract edit and an RK-30 question —
   RIDES-WITH-PR row **H-6**. The column is still **carried in the table** (criterion 1), so the data
   is not lost.
6. **No change to the queue, its file format, its notes or its ids.** A-C8-12's durable dropped-total
   is **not** taken here: it would change ADR-0006 §5's bare-array payload (`:283`), and emitting
   `queue_dropped` as an event is impossible without breaking CM-R43.1's count (it is **not** a
   taxonomy row) and risks `Log → drop → Log` re-entry. Recorded as inherited debt, RIDES-WITH-PR row
   **H-5**.
7. **No edit to `docs/plan/data/analytics_event_taxonomy.csv`** — it is the source of truth, TD-01 is
   NEW-Q36's, and `docs/plan/**` diffs are human-merge paths (Constitution Amendment 1,
   `state/PROJECT_STATE.md:43`).
8. **No new dependency.** The table is C# + the already-pinned Newtonsoft (`JObject`) reached through
   `CatMetro.Content.ContentJson`'s settings factory if any serialization is needed (CM-C2a criterion
   4; `scripts/check.sh:65-90` fails on a second `TypeNameHandling` site). **If any criterion appears
   to need a CSV-parsing package, that is stop condition 6** — it would need an ADR under AGENTS.md
   hard rule 2, and a ~60-line RFC4180 reader is the boring alternative.
9. **No writes to immutable paths** (AGENTS.md hard rule 1: `tests/contract/`, `docs/constitution.md`,
   `.claude/hooks/`, `scripts/git-hooks/`, `state/mode`, `evals/` except `evals/results/`).

### Assumptions

- **A-C9-1 (placement).** The taxonomy ships at `unity/Assets/Scripts/Application/EventTaxonomy/**`, a
  **sibling** of `Application/Analytics/**`, inside the existing `CatMetro.Application` assembly —
  **no new assembly** (ADR-0003 declares assembly names irreversible; adding a row is a human ADR
  gate, Q-M/Q-X precedent at `state/backlog.md:54,65`). Why a sibling rather than
  `Application/Analytics/Taxonomy/**`: CM-C8's wrapper greps are **recursive** over
  `Application/Analytics/**` (`tests/analytics/queue.test.sh:37,41-43,46-48,55-57`), so a table living
  there would be scanned by gates written for the queue — the bound-literal grep
  `\b(2000|1048576|512|64)\b` and the `save\.dat` grep fire **on comments and prose too**, and this
  contract may not edit CM-C8's wrapper to relax them (ownership + AGENTS.md hard rule 5). Cost,
  stated plainly: analytics code sits in two folders. Mitigation, so this is not gate-avoidance:
  **criterion 8 re-applies the same guards over the new root, and criterion 11 re-applies the
  bound-literal grep.** If a reviewer prefers the nested folder, the remedy is a folder move plus
  wrapper-scope edits — no behaviour change, no test meaning changes — **and both landmines above
  become live and must be added as stop conditions** (taxonomy notes G-1).
- **A-C9-2 (ownership carve-out, delegated batch).** CM-C2b's `unity/Assets/Scripts/Application/**`
  gains `EXCEPT Application/EventTaxonomy/**` (`state/backlog.md:117`), exactly the shape CM-C3
  already has against CM-C2b for `tests/unity/failure.test.sh` (`:118`) and CM-C2b's own
  `EXCEPT Retry/Save/Analytics`. Under **R3** the exact amendment text is drafted at
  `t3-backlog-amendment.md` (EDIT-1, EDIT-2) and applied to `state/backlog.md` by the queue owner
  before or with this PR; **this contract never edits `state/backlog.md` itself**. All other paths in
  the file table are unowned by the ordered-longest-prefix rule (`state/backlog.md:108-111`).
- **A-C9-3 (the table is compiled C#, drift-tested against the CSV; no new config file).** The 45 rows
  ship as C# data with a test that re-reads `docs/plan/data/analytics_event_taxonomy.csv` and asserts
  equality (criterion 1). **Rejected alternative:** a `config/analytics_taxonomy.json` read at runtime
  — it would need a `unity/Assets/StreamingAssets/config/**` copy (CM-C2b's path, and now a CM-C10
  stager rule) and a byte-identity `ci` clause (`tests/unity/editmode.test.sh:25-26`, ADR-0009:33),
  i.e. a cross-contract edit, a third staging rule and a boot-path file read, to buy runtime
  editability that nothing asks for. `config/runtime_bounds.json` stays the only config file this
  contract reads (criterion 11) and it is **read, never written**.
- **A-C9-4 (typed failure, not exception).** `TryBuild` returns a bool + error string; the 45 factories
  are the typed façade over it. Rationale: the CM-C8 queue's `Log` path must never throw
  (`AnalyticsQueue.cs:211-226`, review B1), so the layer above it must not either.
- **A-C9-5 (param values are JSON scalars in a `JObject`).** `AnalyticsEvent.Params` is a `JObject`
  (`IAnalytics.cs:10-19`); factories take C# arguments and build it. Numeric params are JSON numbers,
  enumerated params are strings. No `float`/`double` is required by any row; if one appears necessary
  it is fine here (`Application/**` is **not** under a `scripts/check.sh:41` banned-symbol root) but
  must be recorded. Note also `CatMetro.Application` does **not** reference `CatMetro.Domain`
  (notes G-8), so `level_failed.fail_reason` may not be typed as `Domain.FailReason` without a
  cross-contract csproj edit — it stays a string.
- **A-C9-6 (undefined domains stay undefined).** `device_tier`, `rank_bucket`, `fail_reason`,
  `install_referrer` and every param the CSV does not constrain accept any scalar. NEW-Q20
  (`docs/prd/PRD.md:1047,696`) is not answered by this contract; inventing a domain is stop condition 3.
- **A-C9-7 (the 45-count is the CSV's count today).** TD-01/NEW-Q36 may move it to 46
  (`docs/prd/PRD.md:705,1068`). Criterion 2 asserts **45 because the CSV has 45 rows** and criterion 1
  binds the two together, so if the human lands the 46th row the count assertion follows the file with
  a one-line change and no redesign.
- **A-C9-8 (`.meta` files).** New `.cs` files under `unity/Assets/**` need committed `.meta` files;
  they are produced by opening the project in the pinned 6000.3.16f1 editor (CM-C8's shipped
  `*.cs.meta` pairs are the precedent). No asmdef is created or edited — asmdefs cover subfolders
  recursively. **These are editor guids, not CM-C10's derived staging guids** (R1 scope).
- **A-C9-9 (fixtures are test data, not product decisions).** Criterion 11's canonical per-row
  fixtures use representative values; they pin no product number and enter no shipped artifact.
- **A-C9-10 (sprint pricing).** `state/mode` is sprint: ceremony is priced, the enforcement floor is
  not. TDD per criterion, immutable paths, `[CI]` criteria and the independent fresh-context review
  round stand at full strength (AGENTS.md hard rules 1, 5, 7).

### Stop conditions

Defaults apply (AGENTS.md hard rule 3: ambiguous or missing requirement → STOP and ask). Plus:

1. **TD-01/NEW-Q36 looks like it needs answering** to make the count test pass (i.e. the temptation to
   add `previously_completed`/`completions_before` to `level_started`) → **stop**; that edits a
   published analytics contract and `docs/plan/data/analytics_event_taxonomy.csv`
   (`docs/prd/PRD.md:705,998,1068`).
2. **Any urge to resolve the CM-R43.4(d) conflict** — to drop `balance_after`/`entitlement_id` from a
   row, or to add a value-level ledger check that changes what an event may carry → **stop** and cite
   the CONFLICT block (H-2). Equally: any entitlement, ledger or cap **value** appearing in a call
   this contract writes → stop (CM-C8 stop condition 2, `state/handoffs/CM-C8-frozen-contract.md:135-136`).
3. **Any value domain, param, destination, privacy class or event name not present in
   `analytics_event_taxonomy.csv`** looks necessary → **stop**; copy or stop (CM-C7 stop condition 2's
   shape). NEW-Q20's undefined derivations are not agent choices.
4. **Any need to edit a CM-C8-owned path** (`Application/Analytics/**`, `Services/Analytics/**`,
   `tests/analytics/**`) — including making `AnalyticsEvent`'s constructor `internal`, or adding
   `UserPropertyKey` members → **stop**; that is a CM-C8 amendment and needs the grant in H-6.
5. **Any need to edit, relax or re-scope another contract's wrapper or gate** —
   `tests/analytics/queue.test.sh`, `tests/unity/editmode.test.sh` (the UX lane's carve-out),
   `scripts/check.sh`'s existing blocks → **stop** (AGENTS.md hard rule 5; ownership table).
6. **Any new dependency** (a CSV/serialization package, a source generator) → **stop**; name the ADR it
   would need (AGENTS.md hard rule 2). A hand-rolled RFC4180 reader is in scope.
7. **Any need to touch `Bootstrap/**`, `Presentation/**`, a scene, `ProjectSettings/**` or `ui.csv`**
   → **stop**; those belong to the two in-flight lanes and the contract's value does not depend on them.
8. **Any change to the queue file format, the id derivation, the note list, or the `Log` path**
   (A-C8-6/-8/-12, `state/handoffs/CM-C8.md:29-59`) → **stop**; the taxonomy rides the queue as-is.
9. **Anything requires `state/mode=production`, adds a *call site* for a monetization/ad factory, or
   creates a path matching `**/billing/**`, `**/iap/**`, `**/ads/**`** → **stop** (AGENTS.md risky
   paths; `state/PROJECT_STATE.md:10`). **R2 legalised declaring the rows, not emitting them or
   building a surface** — criterion 9 is the line.
10. **`docs/plan/**`, `.github/**`, `config/**`, `state/backlog.md` or any immutable path** appears to
    need an edit → **stop** (Amendment 1 / hard rule 1). The backlog amendment is the human's to apply
    from `t3-backlog-amendment.md`.

---

### RIDES-WITH-PR human calls (default recorded; ratify at review/merge)

None of these blocks starting; each is a ratification the PR must surface, not a missing input.

| # | Call | Default this contract ships | ADR gate? |
|---|---|---|---|
| **H-1 / HC-6** | **NEW-Q36 / TD-01** (`docs/prd/PRD.md:705,998,1068`) — does `level_started` gain `previously_completed`/`completions_before`, or does a separate event take the count to 46? | **45**, exactly the CSV's rows; criterion 1 binds count to file, so the answer lands later as a CSV row + a one-line count change. **CM-R44 metric (ii) stays uncomputable until then** — a product consequence the human should see now, not at D7. | **YES — published analytics contract.** |
| **H-2 / HC-7** | **CM-R43.4(d) metrics-only vs the five ledger-param rows** — reading R1 (system-of-record) or R2 (value-level)? | Table shipped verbatim, **zero call sites**; conflict note is criterion 10 and the criterion **fails if the note is absent**. | **YES under R2** (edits the published taxonomy + amends ADR-0006 §4). |
| **H-5 / HC-8** | **`queue_dropped` disposition** — (a) counter/note forever, (b) rides `error_caught` (`csv:44`), (c) becomes row 46? | **(a) by omission** (non-goal 6); the queue's loss visibility stays non-durable (A-C8-12) — a real gap once a device is in a tester's hands. | (b)/(c) are **published-contract** changes → ADR gate. |
| **H-6 / HC-9** | **`UserPropertyKey` population (RK-30)** — populate the CM-C8-owned enum from the CSV's `user_properties_updated` column, or defer? | **Defer** (non-goal 5); the column ships as data in the table, so nothing is lost. User properties stay uninstrumentable until answered. | External-contract-adjacent (OneSignal tags) → worth an ADR line. |
| **HC-25** | **Merge-delegation re-confirmation for this lane this session** (`state/handoffs/SESSION-HANDOFF-device-testing.md:9-10`; Constitution Amendment 1). | Assume **not** delegated until the human re-confirms in-session. | Blocks **merge**, not work. |

**Closed since the draft:** H-3 (tripwire) by **R2**; H-4 (placement + ownership grant) by **R3**
(text drafted, human applies); H-7 (id + queue position) by **R4**.
