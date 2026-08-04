# CM-C6 — build-loop handoff note (session 2026-08-04, phases 6–10 run)

**Frozen contract:** `state/handoffs/CM-C6-frozen-contract.md` — verbatim copy of
`state/backlog.md:1145-1303` taken at anchor time on `task/CM-C6-daily-seed-pipeline`
(main = 6c2867e). Review verifies against that copy.

## Restatement

Build a deterministic, clock-free, engine-free daily-seed pre-validation pipeline:

- `DailySeed.Derive(dateKey, k)` = lower 32 bits of SHA-256("CM-DAILY-1|" + dateKey + "|" + k);
  the constant `"CM-DAILY-1"` is contract-tested verbatim (NEW-Q8 adopted-liveops reading carried,
  not resolved).
- Date keys are **inputs** (`IReadOnlyList<string>`, `yyyy-MM-dd`); no clock type appears under
  `unity/Assets/Scripts/Content/Daily/**` — enforced by a new appended `scripts/check.sh` block
  scoped to that root only (CM-C5's StalenessStage legally uses a clock type OUTSIDE it), with a
  negative fixture.
- Horizon and salt ceiling are config rows in `config/daily_pipeline.json`
  (`DAILY_PREVALIDATION_DAYS` = 90, cited to PRD:727 / ADR-0009:35; `SALT_MAX_K` analyst-authored
  with derivation, A-C6-2).
- Bounded deterministic salt loop k = 0..SALT_MAX_K over CM-C5's blocking stages; resolved k
  reported per date; exhaustion is a reported failure.
- Each candidate board runs **CM-C5's actual stages** — the pipeline serialises the factory's
  `LevelDto` to schema-shaped JSON in-memory and feeds `CorpusValidator.Validate` as a
  single-member non-campaign corpus, so the two jobs cannot disagree by construction.
- Artifact: `scripts/validate-dailies.sh --out <path>` → `dotnet run --project
  dotnet/CatMetro.DailyTools -- --out <path>`; the host owns ALL file I/O; JSON one record per
  date `{dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}` + one stdout line per
  date `^DAILY_SEED <dateKey> <k> <seed>$`; two runs byte-identical.
- `IBoardFactory { LevelDto Build(uint seed, string dateKey, int k); }` — NO shipped
  implementation under the Daily root (Q-S, grep-asserted); tests supply stubs.
- Weekday ramp reads `config/daily_weekday_curve.json`; absent → `UNCONFIGURED(NEW-Q21)`,
  non-blocking; fixture-supplied → ±0.05 comparison runs. Neither candidate curve is committed.
- Harness: `tests/daily/daily-pipeline.test.sh` (dotnet-test green + two-run diff + [CI] greps);
  scope-guard grep over the Daily root + `scripts/validate-dailies.sh`.
- Device-side limbs of CM-R46 (250 ms bounded loop, ≤200 ms boot validation, backup pool) are
  explicitly NOT claimed — deferred, recorded in the PR.

## Assumption freezes (contract A-C6-1..5 plus session freezes)

- **A-C6-1..A-C6-5** — as written in the frozen contract; honoured verbatim.
- **A-C6-2 instantiation: `SALT_MAX_K = 10`.** Derivation (declared on the config row, flagged in
  the PR): the device-side limb of CM-R46.3 bounds the same loop at 250 ms with the solver at
  beam width 1k; a solver-lite attempt is budgeted ≥ ~25 ms, so a device can guarantee at most
  ~10 attempts inside its budget. CI adopting a HIGHER ceiling could resolve a k a device can
  never reach, breaking "same algorithm ⇒ same k" (liveops_spec.md:55-56). Ceiling = 10.
- **A-C6-6 (session): "lower 32 bits"** = the last 4 bytes of the SHA-256 digest read big-endian —
  i.e. the digest interpreted as the canonical big-endian 256-bit integer, taken mod 2^32. The
  corpus states no byte order; fixed here per the A-C6-3 pattern (golden-adjacent: changing it
  changes every daily seed; named in the PR). Test vectors were computed with an INDEPENDENT tool
  (python hashlib) and pinned in the test source; the C# implementation must reproduce them.
- **A-C6-7 (session): the host's default `--from`** is the config row `PIPELINE_ANCHOR_DATE`
  ("2026-08-24" — the `--from 2026-08-24` example at liveops_spec.md:53, the public-1.0 window
  start). Criterion 6's literal invocation (`--out` only) therefore works with zero clock reads;
  the future CI workflow (human-authored, Q-V) passes a real `--from` computed CI-side. Date keys
  stay inputs everywhere.
- **A-C6-8 (session): weekday-curve fixture format** = `{"mon": <num>, ..., "sun": <num>}`
  (keys = the liveops table's weekday rows, lower-case three-letter). Only test fixtures author
  values, and fixture values are synthetic (not either candidate curve). The committed file stays
  absent (NEW-Q21).
- **A-C6-9 (session): the host ships a fixed-board harness stub, not a generator.** The wrapper
  criteria (6, 7, 10) require `validate-dailies.sh` to run end-to-end, which requires SOME
  `IBoardFactory`. The host's `FixedBoardFactory` imports an EXISTING corpus level
  (default `content/levels/L001.json`) through the byte seam and returns that DTO for every date
  — zero board-shaping rules, loudly labelled Q-S in source, artifact records the stub provenance.
  It lives in `dotnet/CatMetro.DailyTools/**`, outside the grep-banned Daily root, so criterion
  8's "the gap cannot be silently filled" assertion stays meaningful. Stop condition 2 read
  honestly: this is the stub the criteria themselves demand, not a generator.
- **A-C6-10 (session): weekday computation** is pure civil-calendar arithmetic (Sakamoto's
  congruence) on the parsed y/m/d — no clock, no timezone, no locale. Spot-checked in tests
  against known dates (2026-08-24 is a Monday).

## Status log

- anchor: branch cut, contract frozen, this note committed.
- red: 67 Daily NUnit cases (65 failing on skeleton; 2 declaration-level pins pass by
  construction — the frozen constant and the reflection signature).
- green: library implemented; 236/236 across the suite. The python-pinned seed vectors
  reproduced exactly by the C# implementation (cross-tool verification of A-C6-6).
- host+harness: DailyTools exe (sln member, lock file committed), validate-dailies.sh,
  check.sh Daily clock-ban block (+ tests/fixtures/daily-bad negative fixture),
  tests/daily/daily-pipeline.test.sh. Gates: check OK, test 6/6.
- Salt-loop note: the weekday ramp check runs POST-resolution, outside the salt loop — a
  configured-curve mismatch fails the date without salt retries (the loop iterates on CM-C5
  blocking stages only, per liveops_spec.md:55-56's description). Today the curve is absent, so
  the path is UNCONFIGURED(NEW-Q21) everywhere.
- Stub-era note: the artifact's `seed` is the derived daily seed (the CM-R43.8 truth source);
  the stub board's internal sim seed stays the authored corpus seed. These cohere only when a
  real generator (Q-S) embeds the derived seed — recorded so nobody reads stub artifacts as
  generated dailies (boardProvenance names the stub).
