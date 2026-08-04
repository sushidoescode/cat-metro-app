# CM-C8 — build-loop handoff note (session 2026-08-04, phases 6–10 run)

**Frozen contract:** `state/handoffs/CM-C8-frozen-contract.md` — verbatim copy of
`state/backlog.md:1511-1655` taken at anchor time on `task/CM-C8-analytics-queue`.
**STACKED on `task/CM-C7-save-v1` (PR #16)** — the contract's DEPENDS-ON is CM-C7's header/write
helper and the QUEUE_* bounds rows; base main lacks both until #16 merges. Before this branch's
PR opens, it is REBASED onto main post-#16-merge (squash), dropping the inherited commits. Never
delete the base branch while this one points at it.

## Restatement

The Domain/Application half of the offline analytics queue, pure logic, no SDK, no engine:
`analytics_queue.dat` beside `save.dat` with the SAME 16-byte header (magic "CMQU") and the SAME
temp+replace write helper — no second implementation · every bound read from
`config/runtime_bounds.json` at construction, five literals hard-coded nowhere · no-loss /
in-order / no-duplicate at exactly QUEUE_MAX_EVENTS (500-event smoke variant per ADR-0006:224) ·
drop-oldest on BOTH overflow limbs with a visible `queue_dropped` count · oversize single events
dropped, never queued · flush on exactly the four triggers + high-water, NO timer (decidable
negative) · metrics-only statically (no ledger/save/entitlement type reachable from the queue) ·
deterministic idempotency ids that survive restart and dedupe retried flushes · M-21's backup
limb recorded as satisfied-by-exclusion (Q-U deviation note, NOT met by the id) ·
non-transactional with the save BY DESIGN (grant durable, event lost — never inverted) ·
`tests/analytics/queue.test.sh` harness discovery.

## Assumption freezes (contract A-C8-1..5 plus session freezes)

- **A-C8-1..A-C8-5** — honoured verbatim. A-C8-5's enforcement: records serialise through
  `ContentJson`'s factory; this contract constructs no serializer settings.
- **A-C8-6 (session): the idempotency id derivation (A-C8-2 instantiation)** =
  lowercase-hex(first 8 bytes of SHA-256("cm-queue-v1|" + ordinal + "|" + canonical event
  JSON)), ordinal = a monotonic per-queue counter persisted WITH each record; on load,
  nextOrdinal = max(persisted ord) + 1. Recorded limitation: after the file persists EMPTY
  (all flushed, then persisted), the ordinal resets — an identical payload at an identical
  reset ordinal reproduces an old id. Bounded and accepted: the taxonomy contract's session
  fields will break payload identity; noted here so it is a decision, not an accident.
- **A-C8-7 (session): corrupt-file loss count is UNKNOWABLE** — a failed CRC tells us bytes,
  not events. The reject-and-restart-empty path reports `queue_dropped` with
  `count=unknown(corrupt)`. The ADR's "lost count" is satisfiable only for counted drops
  (overflow, oversize); recorded as a deviation the PR carries.
- **A-C8-8 (session): `queue_dropped` and `SetUserProperty` surface as RECORDED notes** (the
  CM-C7 SaveEventRecord pattern) — the typed wrapper/taxonomy contract (CM-R43.1-.3, separate)
  is where they become real analytics events. Nothing is silently dropped.
- **A-C8-9 (session): byte accounting** = the sum of each persisted record's UTF-8 length
  (id+ord+name+params); JSON array framing overhead is not counted. QUEUE_EVENT_MAX_BYTES
  applies to the full persisted record.
- **A-C8-10 (session): the queue file persists on every enqueue and every acked flush** — the
  crash-safety posture that makes criterion 8's restart test meaningful; no timer exists
  anywhere (criterion 6's negative is structural).

## Status log

- anchor: branch cut STACKED on task/CM-C7-save-v1 @ dc02d5e; contract frozen; this note committed.
