# CM-C7 — build-loop handoff note (session 2026-08-04, phases 6–10 run)

**Frozen contract:** `state/handoffs/CM-C7-frozen-contract.md` — verbatim copy of
`state/backlog.md:1305-1509` taken at anchor time on `task/CM-C7-save-v1`. Review verifies
against that copy.

## Restatement

The engine-free half of the save system: the byte-exact 16-byte header (magic "CMSV",
format/save versions u16 LE, payload length + CRC-32/IEEE u32 LE, BOM-free UTF-8 JSON payload) ·
the payload v1 key set EXACTLY per ADR-0006 §2 with the three OPEN sub-shapes absent ·
the three-call atomic write behind an injected filesystem seam · the SI-1..SI-7 kill-during-write
battery at both interruption points · the never-throwing load fallback chain (main → .bak →
fresh + error_caught) · the ordered migration table with downgrade refused read-only ·
the domain-separated consumable ledger as a DATA STRUCTURE (Q-T) · the authored
`config/runtime_bounds.json` (15 keys, values copied from ADR-0006 §4 verbatim) with the four
drift tests · `tests/save/save.test.sh` harness discovery.

## Assumption freezes (contract A-C7-1..5 plus session freezes)

- **A-C7-1..A-C7-5** — honoured verbatim. A-C7-3's enforcement: the payload serialises through
  `ContentJson.CreateSerializer()`; this assembly constructs no serializer settings (the CM-C2a
  check.sh block would fail the merge otherwise).
- **A-C7-6 (session): SaveState and LoadResult live in CatMetro.Services.** ADR-0003:105 pins
  "ISave declared in Services" and overview.md:224 pins `SaveState State { get; }` in ISave's
  signature — the type must therefore be visible to Services, and Services references nothing
  below it. SaveState is a boundary data-holder (the payload JObject + two thin balance
  accessors the ledger needs); no implementation logic. This adds the tree-wide Newtonsoft
  13.0.2 pin to the Services csproj — a registration-only append of an existing pinned package,
  not a new dependency. Named in the PR for the reviewer/human.
- **A-C7-7 (session): the unknown-key guarantee is structural.** State IS the loaded JObject —
  the store never re-projects through typed DTOs, so unknown keys and the opaque
  `breadcrumbs.purchase.state` string survive by construction, not by copying code.
- **A-C7-8 (session): TryCommitWithin's budget enforcement** = the two contract-checkable
  refusals (non-positive budget → false without writing; over-cap → false without writing).
  Wall-clock estimation against SAVE_PAUSE_BUDGET_MS is device-tier work (CM-R05's device rows)
  and is NOT claimed; the default-budget entry point `TryCommitOnPause()` reads the config row.
  Named in the PR.
- **A-C7-9 (session): TryGrant's quantity and grantedAtUtc are caller parameters.** Mapping a
  productId to a grant amount is an economy value (`config/economy_defaults.json`, CM-R04.1 —
  human) and timestamps come from the clock seam this contract may not touch. The ledger
  persists what it is handed; it invents neither.
- **A-C7-10 (session): read-only-after-downgrade commit refusal is recorded, not thrown.**
  CommitAtomic in ReadOnlyMode records `error_caught(domain=save_readonly)` and writes nothing;
  TryCommitWithin returns false. The profile is usable in memory (ADR-0006's "app starts with an
  in-memory default profile in a read-only mode"), and the refusal is visible in the event
  record.
- **A-C7-11 (session): ledger key vectors cross-verified.** The pinned dedupe keys were computed
  with python hashlib (`73cf1677ec4787a88391ca6dbf3ed6ab` for
  rewind_pack_small|GPA.1234-5678-9012-34567; `8d077a4c...` for the RK-19 pair) and the CRC-32
  check value 0xCBF43926 against python binascii — the C# implementations must reproduce them.
- **A-C7-12 (session): first-ever commit uses File.Move.** File.Replace requires an existing
  destination; when save.dat does not yet exist the real seam moves the temp into place (no
  .bak can exist to preserve). From the second commit on, the path is exactly the ADR's
  three-call shape.

## Deviations the PR must carry (contract-mandated disclosures)

- **Q-Y:** the `config/runtime_bounds.json` ↔ StreamingAssets byte-identity clause of
  ADR-0009:33 is UNSATISFIABLE until the contract that owns `unity/Assets/StreamingAssets/**`
  lands (CM-C2b, post-Q-G). CM-C7 authors only `config/runtime_bounds.json`. Follow-up named:
  the content-pipeline contract.
- **RK-17:** the auto-backup decision (manifest/backup-rules XML) is a human decision that must
  land WITH the save format; CM-C7 records it as unresolved and ships neither posture.
- **Q-T human ack:** the ledger is a data structure, not a monetization surface; `state/mode`
  untouched; no monetization path created. The PR asks for the ack Q-T names.
- **ADR gate (A-C7-5):** the payload v1 shape is IRREVERSIBLE the moment a tester device holds
  a v1 file — the PR carries the explicit human ADR-0006 §2 gate.

## Status log

- anchor: branch cut off main (CM-C6 #14 / L002-L005 #15 both awaiting human merge — independent).
- red: 56 Save NUnit cases (55 failing on skeleton; 1 declaration-level pin, the IStorageRoot
  shape). Services gained the SaveState/ISave/LoadResult boundary types (A-C7-6).
- green: implementation landed whole from the staged drafts; 56/56, suite 225/225 on this base
  (169 prior + 56; the 67 Daily cases live on unmerged #14). CRC-32 check value and both ledger
  key vectors reproduced the python pins exactly (A-C7-11).
- harness: tests/save/save.test.sh + tests/fixtures/save-bad negative fixture. The wrapper's
  criterion-13 grep caught its own first draft naming the engine path token in a comment —
  reworded (comments are in scope BY DESIGN; the CM-C1 landmine class).
- gates: check OK · test 6/6 · save.test.sh OK (4-shape, 13, 14, 15).
