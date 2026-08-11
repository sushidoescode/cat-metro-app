# CONTRACT DAILY-LINE — runtime UTC daily board generation (Phase 1, pure Content)

**Branch:** `feat/daily-line`
**Anchor:** freshly re-fetched `origin/main` at `04b9173` (2026-08-10)
**Ground truth:** `state/handoffs/PARALLEL-PUSH-2026-08-09.md` plus its Wave 2
Addendum read from `origin/session/wave2-addendum` at `c1d78cc`; the human's Q3 ruling
relayed in this lane on 2026-08-10; `docs/plan/specs/product_spec.md` §§18 and 24;
CM-C6 (`7220dfa`, merged PR #14) is the substrate.

## Human rulings executed

1. Runtime NEW-Q8 uses the product-spec reading: the UTC civil date and the exact
   preimage `"CM-DAILY-" + yyyy-MM-dd`. The older `CM-DAILY-1|date|k` CI scheme remains
   historical substrate and is not silently rewritten. This reading is recorded in the
   PR body as requested.
2. Q3: no shaping rule exists elsewhere. Lane 6 is authorized to design and implement a
   deterministic template-parametric generator using PCG32 and validator-approved
   topology templates.
3. Production code stays under `unity/Assets/Scripts/Content/Daily/**`; tests stay in the
   lane's Daily test surface and exercise the existing validation stages. The public
   `CatMetro.Domain` API does not change.
4. GameRoot/Bootstrap wiring is deferred to a separate contract at funnel position 6,
   ordered after Lane 3's GameRoot band wiring. This contract designs the returned seam
   but does not touch Bootstrap, GameRoot, Home, Presentation, scenes, project settings,
   or UI.
5. Every accepted board runs the same eleven `ValidationStages` as authored content and
   the post-#66 solver/brittleness behavior once that change is on main. Rejection is
   bounded and ends in a deterministic validator-approved fallback.

The Q3 answer is **not** HC-25. HC-25 is asked fresh only after the implementation,
review, CI, and PR evidence converge.

## Goal

Deliver the engine-free runtime Daily Line board resolver: a UTC instant produces the
product-spec seed, a schema-v2 `LevelDto` is generated deterministically from a finite
template catalogue, the real CM-C5 validator admits or rejects each candidate, and a
fixed fallback makes seed-path exhaustion total. The resolved DTO and canonical JSON
remain available for the later GameRoot wiring contract.

## Design

### Seed and UTC boundary

`DailyLineSeed` is additive; the historical `DailySeed` symbol and its fixed vectors stay
unchanged. `DailyLineSeed` accepts a validated UTC date key or a Unix-second instant. The
instant-to-civil conversion is integer Gregorian arithmetic under the existing Daily
clock ban: no clock, locale, timezone database, engine API, or floating point.

The base seed is the final four SHA-256 digest bytes read big-endian, matching CM-C6's
recorded meaning of “lower 32 bits,” over the UTF-8 bytes of exactly:

```text
CM-DAILY-yyyy-MM-dd
```

Independent Python `hashlib` vectors fixed at contract freeze:

| UTC date | expected `uint` |
|---|---:|
| 2026-08-10 | 252386339 |
| 2026-08-24 | 1449106418 |
| 2026-12-31 | 1117928761 |
| 2028-02-29 | 3895508439 |

The base seed stays constant through rejection attempts. Attempt `k` is a generator
variation index, not an unrecorded change to the product-spec seed.

### One pipeline, two explicit schemes

CM-C6's salt/serialization/validation loop remains the single implementation. An
`IDailySeedScheme` seam supplies `{ artifact label, Derive(dateKey, k) }`:

- the existing request constructor defaults to the historical wrapper and therefore
  preserves #14 artifacts and vectors byte-for-byte;
- runtime requests explicitly use `DailyLineSeedScheme`, whose derivation ignores `k`
  and returns the product-spec base seed;
- the report prints the selected scheme's label instead of hard-coding the historical
  label.

This is an additive Content API. It changes no Domain type or member.

### Template-parametric factory

`DailyBoardFactory` is the one shipped `IBoardFactory` implementation. It owns exactly
three finite candidate topology families and one fallback family:

1. **Alternating fork:** one two-route switch, two color stations, separated alternating
   waves.
2. **Queued fork:** a holding branch followed by a two-route delivery switch; queue
   pressure is intentional but bounded.
3. **Cascade:** two two-route switches and three color stations; decisions remain
   separated by at least the authored action-window floor.
4. **Fallback fork:** the smallest non-trivial one-switch/two-station board, with generous
   station capacity, wave spacing, and time headroom.

All arrays are constructed as fresh immutable DTO inputs. For candidates, variation uses
`new Pcg32(baseSeed, (ulong)k)` and consumes draws in this frozen order:

1. candidate family (`Next() % 3`);
2. horizontal mirror bit;
3. one route-order bit per two-route switch, with `initialRoute` remapped so the board
   remains semantically equivalent;
4. Fisher-Yates permutation of `[red, blue, yellow, green]`, descending index, with
   `Next() % (i + 1)`.

The color bijection is applied consistently to sources, stations, and waves. Mirroring
changes integer `x` coordinates only. Route reversal changes the route array and its
initial index together. No topology branch reads a float, dictionary iteration order,
locale, clock, platform API, or validator measurement.

The fallback performs no topology selection, mirroring, or route reordering. It may use
only the same deterministic color bijection; its topology, timings, capacities, and
switch defaults are fixed and independently admitted by the validator.

Every generated DTO uses:

- schema version `2`, id `L800`, band `daily`, `authoredBy = generator+validator`;
- the product-spec weekday difficulty target;
- launch mechanics only (`switch`, and `queue` where the selected template uses it);
- no `newMechanic`, no post-launch mechanic, and no new Domain capability;
- the product-spec base seed in `LevelDto.Seed`;
- the Daily first-completion reward row (`baseTickets = 100`, `perfectBonus = 0`); practice
  replay reward suppression remains Application/wiring scope and is not claimed here.

### Difficulty table

The generator copies the product-spec Daily envelope exactly:

| UTC weekday | target |
|---|---:|
| Monday | 0.30 |
| Tuesday | 0.35 |
| Wednesday | 0.38 |
| Thursday | 0.42 |
| Friday | 0.45 |
| Saturday | 0.50 |
| Sunday | 0.55 |

These values live in the runtime Daily code because this human-authorized contract makes
the product-spec curve the runtime rule. It does **not** create
`config/daily_weekday_curve.json`; the historical CM-C6 absent-file behavior and artifact
remain intact.

### Admission, rejection, and fallback

For each date, CM-C6 serializes the DTO with `DailyBoardJson`, validates it as a
non-campaign member through `CorpusValidator.Validate`, and retains the accepted DTO and
canonical JSON in the date record for later wiring. Admission requires:

- exactly the eleven ordered stage rows;
- zero blocking verdicts;
- a real `SolveVerdict.Solved` result (a non-blocking `NotFound` warning is not enough for
  a playable Daily);
- stage 5 does not report a zero-input win;
- stage 6 is non-blocking under the post-#66 solver result.

Candidate attempts are exactly `k = 0..SALT_MAX_K`, using the existing configured ceiling
(`10` today). After those attempts fail, the fallback is built once and run through the
same admission function. A passing fallback yields a normal resolved record with
`UsedFallback = true`; it never emits a rejected candidate as playable. A fallback
validation failure remains a loud blocking report because concealing a validator/schema
regression would be worse than claiming an unvalidated board. The build gate proves the
fallback across the dated test horizon, so a pathological **seed** cannot reach that path.

Factory exceptions, null DTOs, serialization failures, and malformed date inputs become
typed/reportable failures; none escape the pure pipeline.

## Acceptance criteria

1. **Exact runtime seed.** The four independent vectors above pass; changing the prefix,
   separator count, UTF-8 input, digest end, or byte order turns at least one test red.
2. **UTC and calendar edges.** Integer civil-date tests cover the second before/at/after a
   UTC midnight, month end, year end, leap day, non-leap February, and two offset-labelled
   representations of the same instant. Equal instants always yield the same date, seed,
   and board bytes worldwide.
3. **Historical compatibility.** All existing CM-C6 seed, artifact, and wrapper tests pass
   unchanged in behavior. The legacy default still emits `CM-DAILY-1`; a runtime request
   emits `CM-DAILY-` and the new vectors. A mutation that routes runtime through the legacy
   source fails.
4. **One shipped factory, no Domain delta.** Exactly one production type under
   `Content/Daily` implements `IBoardFactory`; the old Q-S “zero implementations” wrapper
   assertion is deliberately re-authored to this exact-one ownership assertion under the
   human Q3 ruling. `git diff origin/main -- unity/Assets/Scripts/Domain` is empty.
5. **Frozen PCG draw order.** Fixed `{date, k}` cases pin family, mirror, route ordering,
   color map, and canonical-board hash. Removing/reordering a draw or replacing PCG32
   fails these tests.
6. **Weekday envelope.** Seven literal weekday cases produce exactly
   `0.30/0.35/0.38/0.42/0.45/0.50/0.55`; UTC Sunday→Monday rollover selects the new row.
   No topology choice branches on those floating values.
7. **Canonical byte identity.** Repeated runs, reversed process culture settings, and
   boundary-equivalent instants produce byte-identical UTF-8 JSON. Different date vectors
   demonstrate non-constant generated output without requiring every pair to differ.
8. **The real eleven stages.** Every accepted candidate and fallback record contains the
   eleven existing `Stage` rows in ordinal order and a solved trace. Tests call the real
   `CorpusValidator`, not a mock or a parallel stage implementation.
9. **Bounded rejection.** A controlled factory that fails candidates proves the exact
   attempt set `0..SALT_MAX_K`, no attempt beyond the ceiling, and one fallback call. Two
   runs resolve the same `k`, seed, DTO, stage rows, and bytes.
10. **Fallback totality.** Injected candidate failure/exhaustion resolves the admitted
    fallback with `UsedFallback = true`; deleting the fallback call, returning the last bad
    candidate, or skipping fallback validation turns a named test red.
11. **Dated horizon.** The shipped horizon of 90 consecutive UTC dates resolves with no
    blocking board and byte-identical second-run artifacts. The set crosses at least one
    month boundary; separate vectors cover year and leap boundaries.
12. **Post-#66 brittleness gate.** Before final certification, re-fetch/rebase after Lane
    2's solver change lands on main. All generated horizon boards and the fallback remain
    solved and non-blocking at stage 6. This is an ordering gate, not permission to copy or
    edit Lane 2 files.
13. **Offline and engine-free.** Production Daily code contains no Unity, file API,
    network, clock, locale-dependent parsing/formatting, external RNG, or new dependency.
    Existing purity/scope guards remain green.
14. **Deferred wiring is mechanically visible.** The diff contains no Bootstrap,
    GameRoot, Presentation, Home, scene, prefab, project-setting, Domain, or staged-content
    edit. The PR names funnel position 6 and Lane 3 as the wiring prerequisite.
15. **Gates and mutation evidence.** Focused RED→GREEN cycles are recorded for seed,
    timezone boundary, PCG draw order, real-validator admission, rejection bound, and
    fallback. Then `scripts/check.sh`, the focused Daily wrapper, the full test suite, and
    `scripts/build.sh` pass from the final tree.

## Files in scope

Expected production work:

- modify `unity/Assets/Scripts/Content/Daily/DailyPipeline.cs`
- modify `unity/Assets/Scripts/Content/Daily/DailySeed.cs` only if a non-breaking legacy
  wrapper hook is required; its constant and behavior are frozen
- add focused files under `unity/Assets/Scripts/Content/Daily/**` for the seed scheme, UTC
  civil conversion, difficulty table, template catalogue, and concrete board factory
- modify/add tests under `unity/Assets/Tests/EditMode/Pure/Daily/**`
- modify `tests/daily/daily-pipeline.test.sh` only to replace CM-C6 criterion 8b's now-
  superseded zero-factory assertion with the exact-one Q3 ownership assertion
- this frozen contract; later, exactly the Lane 6 state row and contract-named debt at the
  merge bookkeeping point required by the Wave 2 addendum

No `content/daily/**` file is required by this Phase 1 design: templates are immutable
Content DTO constructors, avoiding a premature StreamingAssets/stager change. If that
proves impossible, stop and request a declared scope amendment instead of silently adding
one.

## Assumptions and explicit non-goals

- **A-DL-1:** “lower 32 bits” keeps CM-C6's digest-tail, big-endian convention. It is
  golden-adjacent and pinned by independent vectors.
- **A-DL-2:** The configured CM-C6 `SALT_MAX_K` is reused; no second attempt ceiling is
  invented.
- **A-DL-3:** `L800` is the schema-valid canonical Daily runtime level id. The UTC date and
  product seed distinguish daily instances; no schema change is introduced.
- **A-DL-4:** Candidate transformations are deliberately semantics-preserving where
  possible; variety comes from finite topology selection, mirroring, route representation,
  and color permutation. Infinite procedural topology is not required by Q3.
- **A-DL-5:** Stage 11 remains the existing pending human checklist row. CI cannot perform
  a device playtest, and this contract does not claim otherwise.
- **A-DL-6:** The product-spec 100-ticket reward is represented in the DTO, but first-clear
  versus practice enforcement belongs to later Application/runtime wiring.
- No streak, score submission, practice ledger, analytics call, Home chip, share card,
  notification, save migration, server, remote override, backup-pool asset, GameRoot
  wiring, or monetization work.
- No new package, ADR, Domain seam, validator-threshold change, scene/prefab asset, or
  `config/daily_weekday_curve.json`.

## Stop conditions

Stop and ask before proceeding if:

1. a generated board requires any `CatMetro.Domain` public API change;
2. the real validator cannot admit the catalogue/fallback without changing a shared
   ValidationStage or threshold;
3. stage 6 cannot pass after the #66 solver change without editing Lane 2's work;
4. runtime usability requires GameRoot/Bootstrap before Lane 3's band wiring lands;
5. a content/staging asset becomes necessary despite the code-template design;
6. exact cross-platform bytes require a new dependency or serialized schema change;
7. any requirement reaches Home/Presentation, persistence, analytics, monetization,
   networking, or human-only paths.

## Evidence record to carry into the PR

- Restate the runtime NEW-Q8 reading and preserve the historical CM-C6 distinction.
- List every assumption A-DL-1..6.
- Map each criterion to its focused tests and exact mutation proof.
- Record the Lane 2 rebase commit and post-#66 stage-6 result.
- Record the deferred funnel position `3 → 6 → 8 → 5` and zero GameRoot diff.
- Ask HC-25 fresh only after review and CI converge; do not treat Q3 as merge authority.
