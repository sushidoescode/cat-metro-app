# Handoff — CM-C5: 11-stage level validator + licence-free `validate-content` leg

**Session:** 2026-08-04 · **Branch:** `task/cm-c5-validator` (off main @ 03b0f5d — CM-C4 merged) · **Status:** ANCHOR — planner phase
**Lane:** frontier in-session (sprint pricing, trust level 0). `state/backlog.md:238` rules CM-C5 **not hybrid-eligible** ("C5 and C6 leave `Domain`"), so the local-executor question does not arise.

## Contract restatement (mine; the frozen copy is below)

Build a pure-C# validation library under `unity/Assets/Scripts/Content/Validation/**` (no
`System.IO`, no `UnityEngine` — CM-C2a's check block already scans this root) implementing the 11
CM-R12 stages, plus a console host `dotnet/CatMetro.Validator/**` owning ALL file I/O (the on-disk
`IContentSource`, corpus discovery, the `--out` JSON report, the `--stamp` writer, the stage-10 git
reference probe), a `scripts/validate-content.sh` entry point (no credentials, no Unity, no
network), a `tests/validation/validator.test.sh` fast-leg wrapper, and
`config/validator_thresholds.json` **without** the four Q-R rows. Blocking stages gate the exit
code; `UNCONFIGURED`/`PINNED`/`SKIPPED`/stage-10-`STALE`(while Q-O open)/stage-11 rows never block.
Corpus = `content/levels/**` + `docs/plan/data/stress_boards.json` (stages 1–8 + 10 + a stage-11
checklist row for stress boards; stage 9 and campaign-order assertions print
`SKIPPED(non-campaign)` for them).

## Planner decisions (frozen at test-authoring; the tests are the executable definitions)

**Verdict model.** `StageVerdictCode { Pass, Fail, Warn, Unconfigured, Skipped, Pinned, Stale,
Fresh, Pending }` + a detail string + a computed-value string. Exit contribution: `Fail` on a
blocking stage only. Stage 4 blocks **only** on `Unsolvable` (`NotFound(Beam|Budget)` and
`Indeterminate` print counts, non-blocking — Q-N; budget exhaustion is treated like a beam miss
because neither is a proof). Stage 10 computes `STALE/FRESH` but contributes 0 while Q-O is open.
Stage 11 always `Pending`, contributes 0.

**Stage-1 schema engine.** No new dependency is legal (hard rule 2), and `Newtonsoft.Json.Schema`
is a separate, licence-encumbered package → stage 1 is a **hand-rolled interpreter over the actual
`docs/plan/data/level_schema.json` bytes** (supplied through `IContentSource`), implementing the
subset the schema document uses: `type`, `const`, `enum`, `pattern`, `required`,
`additionalProperties:false`, `minimum/maximum`, `minItems/maxItems`, `minLength/maxLength`,
`prefixItems`, `items`, `properties`, `default` (ignored), `description` (ignored). A schema
keyword outside that subset → the stage reports `Fail` with "unsupported schema keyword" (fail
closed, never silently skip). The schema file itself is an input, so a schema edit changes
behaviour without a code edit — the one-truth property the criterion wants.

**Stage-2 freezes (A-C5-8).** Junction = a node hosting a switch. Spacing = Euclidean distance
over the authored `x,y` between two junction centres; fail iff `< 1.2` (doubles are legal in
Content). Reachability: station S is reachable iff a directed path exists from some source node
whose `allowedColors ∩ S.accepts ≠ ∅`, over all edges (switches route dynamically, so every
outbound edge is potentially takeable). **Refinement (forced by L001 itself, narrowed by review F6):** a station whose
accept set intersects NO source's colours is a deliberate decoy — L001's BLU teaches the wrong
route — and cannot FAIL, but it WARNS (non-blocking, named) rather than pass silently: the
accepts-typo class stays audible. The fail rule fires only where a colour-compatible source
exists but no path does. Orphan switch: its node has no inbound edge, OR some route
is not an outbound edge of its node. Top-15% warning: board vertical extent from node `y` values;
a switch node with `y ≥ maxY − 0.15·(maxY − minY)` warns (top of screen = high y, per the corpus
convention SRC at y=9 above stations at y=1..2).

**Stage-3 freeze (A-C5-9).** `minTravelTicks` = the minimum over colour-compatible
(source, station) pairs of the shortest directed path length in `travelTicks` (Dijkstra, integer).
Printed value = `minTravelTicks × win.deliveries`. Hand-derived L001: (10+12) × 2 = **44**.
Comparison `44 ≤ timeLimitTicks + slack` with `slack` from the `lowerBoundSlack` row → absent →
`UNCONFIGURED(lowerBoundSlack)`.

**Stage-4/5 wiring.** Stage 4 = `LevelSolver.Solve(graph, seed)` (authored defaults). Stage 5 =
`LevelSolver.EvaluateLog(graph, seed, empty)` — `Solved` ⇒ the LEVEL fails triviality; `NotFound` /
`Indeterminate` ⇒ stage passes (a pinned zero-input run did not win; that is the L001 baseline
shape from CM-C4 criterion 10).

**Stage-6 freezes (A-C5-2 + A-C5-7).** Jitter: `jitterSampleCount` (an ordinary configured row —
test-design number, set to 20) samples; per sample, each entry of the stage-4 winning log gets an
independent offset drawn from {−1, 0, +1} via `Pcg32(level seed, 3)` (`Next() % 3 − 1`, entries
in log order, samples sequentially — one generator stream, fully deterministic), ticks clamped to
≥ 0. Sample outcomes: `Solved` = win, `NotFound` = loss, `Indeterminate` (the NEW-Q4 guard fired)
= **pinned — neither** (stop condition 7). Retention = wins/(wins+losses) whenever ANY unpinned
sample exists — review F2 narrowed the original pin-majority hatch: wins and losses among the
unpinned samples measure real brittleness and the ≥70% guard stays LIVE with pins present. Only a
sample set with zero unpinned members reports **`PINNED(NEW-Q4)`** — a near-unreachable backstop,
since a one-entry-changed jitter sample is also a window shift, so all-pinned jitters imply
width-1 windows and the window limb fires first. Measured anatomy that shaped the rule: on L701
every misroute ends at a wrong-colour station, 18/20 samples pin, and BOTH unpinned samples win —
retention 100% over the unpinned set, pin counts printed; a pinned=lost rule would flunk the
shipped corpus on an open question, and the original discard-the-ratio rule would have let a
board with `pinned=11, wins=0, losses=9` ship (the reviewer's false-negative). The window and
onboarding limbs are unaffected (a pinned shift is still not-a-win there, which is what makes the
brittle fixture fail on its 2-tick window regardless); the retention rule itself is executed by
the `JitterLossLevel` red-only loop board, whose jitter losses are timeouts, never pins.
Action-window limb: per entry of the winning log, the window = the size of the maximal contiguous
run of single-entry tick shifts (scanned over `±(minActionWindowTicks + 2)`, clamped at 0) that
still win, **measured on the solver-optimal log** — a level fails when any entry's window <
`meta.minActionWindowTicks`. This is conservative (another solution might have wider windows); the
report prints the measured windows so a human can overrule — recorded limitation, not silent
narrowing. Onboarding limb: `band == "onboarding"` ⇒ `minActionWindowTicks ∈ [12,16]`, blocking.
No winning log (stage 4 not `Solved`) ⇒ stage 6 prints `SKIPPED(no winning log)`, non-blocking —
stage 4 already carries the verdict that matters.

**Stage-7.** `two < three` and both ≥ 1: blocking today. Reachability limb: `PINNED(NEW-Q5)`
(star scores need the pinned scoring model); with a `starBandSlack` row also absent it would be
`UNCONFIGURED(starBandSlack)` — the pin is checked first (a pin is the stronger fact).

**Stage-8 freezes (A-C5-10).** Raw axis values, all printed:
- **B** = `nodes + edges + switches` (L001: 4+3+1 = **8**). Normalisation basis = `axisBBandCaps`
  row → absent → stage verdict `UNCONFIGURED(axisBBandCaps)`.
- **E** = two components: `peakTrains80` = max spawns in any 80-tick sliding window (10 s at
  8 tps; L001: **2**), and `interleaveEntropy` = Shannon entropy (bits) over the adjacent-pair
  colour-transition frequencies of the chronological spawn sequence (L001: [red,red] → one
  transition red→red → **0.0**). Normalised E (when configured) = `min(1, peakTrains80/cap) ×
  0.5 + entropy/2 × 0.5` — but the cap is part of the absent band-caps row, so normalised E is
  `UNCONFIGURED` today; raw components always print.
- **C/T/H/R** consume CM-C4's `DifficultyProxy`. T = `SolverOptimalTicks / TimeLimitTicks`
  (L001: 50/160 = **0.3125**). R = `1 − winnable/tried`; **`tried == 0` ⇒ R-fraction = 1
  (maximally robust) ⇒ axis R = 0** — the CM-C4 review-L2 ruling, applied here as promised.
  H raw = `MinQueueSlackAtPeak`, printed as **`PARTIAL(Q-J)`** verbatim (queue term only).
- Weighted sum `Σ wᵢ·axisᵢ` with weights .20/.25/.20/.15/.15/.05 is computed **only when every
  normalisation input exists** (i.e., in tests via a fixture config carrying the band-caps row);
  the shipped config omits the row, so the stage prints raw axes + `UNCONFIGURED(axisBBandCaps)`
  and the ±0.05 comparison does not run. No agent picks the caps (stop condition 3).

**Stage-9 freeze (A-C5-11).** Feature vector (doubles): [nodes, edges, switches, stations,
sources, waves, totalSpawns, peakTrains80, distinctColours, timeLimitTicks/100, deliveries,
meanTravelTicks, interleaveEntropy]. Distance = Euclidean. Compared against prior levels in play
order (campaign only); threshold row `noveltyMinDistance` absent → `UNCONFIGURED`. Values always
print.

**Stage-10 (Q-O per the analyst default).** The HOST computes the reference instant as the newest
git commit timestamp (`%cI`) touching `unity/Assets/Scripts/Domain/**` or
`docs/plan/data/level_schema.json` (A-C5-4) and passes it INTO the library as a string; the
library compares ISO-8601 strings ordinally (both are ISO timestamps — no clock, no DateTime under
Content; ordinal comparison of ISO-8601 with offsets is analyst-accepted for same-repo commit
stamps, recorded limitation). Absent key ⇒ `STALE`. Reference unavailable (no git) ⇒ `STALE
(reference unavailable)`. Never blocks while Q-O is open, and the report says so verbatim.

**Stage-11.** Row per corpus member: id, band, capstone = (`band == "capstone"`), testers = 3 for
capstones else 1, verdict `HUMAN-VERIFIED (pending)` — depends on D-6, cited not resolved.

**Host & wrapper surface (A-C5-12).** `scripts/validate-content.sh [--corpus <path> ...]
[--out <path>] [--stamp]` → `dotnet run --project dotnet/CatMetro.Validator --` with the same
args; default corpus = `content/levels` + `docs/plan/data/stress_boards.json`. Stress-board wrapper
handling: the host parses the wrapper `{comment, levels:[…]}`, re-serialises each level object and
feeds those bytes through the same pipeline (recorded: the duplicate-key/depth belts run on the
re-serialised member, not the wrapper file). `--stamp` performs byte-surgical replacement/insertion
of `meta.validatedAt` ONLY under `content/levels/**` (never `docs/plan/**`), preserving every other
byte. Gate mode opens everything read-only. The dual-form report: stdout table + `--out` JSON
(shape asserted by test), including per level the `SolveResult` summary, `CompletionTicks`,
`seconds = CompletionTicks ÷ 8` printed with `PINNED(NEW-Q1)` for the 40–75 s range comparison.

**Config file.** `config/validator_thresholds.json` ships with: `jitterSampleCount: 20` (A-C5-2,
test-design number) and a `_comment` naming the four Q-R rows that are DELIBERATELY absent
(`lowerBoundSlack`, `starBandSlack`, `noveltyMinDistance`, `axisBBandCaps`) with the stop-condition
citation. Tests exercise the configured path via fixture configs under
`tests/validation/fixtures/` — values there are test fixtures, not authored thresholds.

**Campaign-order assertions (criterion 14).** One-new-mechanic (CM-R06.2) + the 30-level count
(CM-R09.1) + band table (CM-R09.3) run over `content/levels/**` only, in id order = play order.
With a 1-level corpus they pass trivially today; the count assertion prints the current count and
is non-blocking until the corpus approaches launch (recorded: a 30-level HARD count assertion on a
1-level corpus would block every merge for weeks — it prints `PENDING(corpus 1/30)` instead;
becomes blocking at ≥30). Mechanic-order IS blocking (violations are authoring defects now).

## Assumption ledger (all recorded above): A-C5-1..6 from the contract; new analyst freezes
A-C5-7 (action window), A-C5-8 (stage-2 metrics), A-C5-9 (lower-bound path), A-C5-10 (axis
formulas incl. E), A-C5-11 (novelty vector), A-C5-12 (host arg surface + wrapper handling +
PENDING corpus count). None is load-bearing-and-open under the objective test: every verdict that
could flip on a freeze either (a) has its threshold Q-R-absent (`UNCONFIGURED`, non-blocking), or
(b) is asserted against the freeze by its own test with the raw value printed for human overrule.
The one real risk — stage 6's blocking limbs on the live corpus (L701/L702 retention/windows) —
is empirical: measured during build; if a stress board fails the frozen rule, that is a surfaced
finding about the board (stop-and-report), not a rule to soften silently.

## CM-C2a errata surfaced by this build (cross-contract fixes, disclosed for review)

Criterion 14 (stress boards are corpus members) is unsatisfiable against the importer as merged,
and the defects are objective importer-vs-frozen-schema conflicts, so the minimal fixes ride this
branch in their own labelled commit rather than blocking the contract on a round-trip:

- **E-C2a-1** `LevelImporter` required `meta.newMechanic` to be a string; `level_schema.json:21`
  types it `["string","null"]` and BOTH shipped stress boards author `null`. Fix: null maps to a
  null `MetaDto.NewMechanic`; non-string-non-null still rejected.
- **E-C2a-2** `ContentJson.LoadToken` returned pass 2's token (`JToken.Parse`), which does not
  honour `Settings.DateParseHandling = None` — an ISO-dated string like `meta.validatedAt` came
  back as a Date token and its string-typed import then failed, i.e. every `--stamp`ed level
  would have been rejected on re-import. Fix: pass 1 (which honours Settings) yields the token;
  pass 2 remains solely the duplicate-key belt.

No CM-C2a test pinned either behaviour (verified before touching); the full CM-C2a suite is green
after both fixes. The stress boards import cleanly and L701/L702 SOLVE under the shipped budget
(146,942 / 16,839 expansions; completion ticks 182 / 126).

## Sub-plan (sprint, in-session, TDD)
T1 Stage enum + verdict/report model tests → types. T2 schema-stage tests (≥6 malformed + L001
passes) → mini interpreter. T3 stage-2/3 tests → static analysis + lower bound. T4 stage-4/5/6/7
tests (solver wiring, triviality, brittleness incl. determinism, stars). T5 stage-8/9 tests (axes
hand-derived on L001, weighted sum under fixture config, novelty ordering). T6 stage-10/11 +
UNCONFIGURED-semantics tests (criterion 13's with/without-row pairs). T7 corpus/report tests
(criterion 14 set, JSON shape, NEW-Q1 seconds). T8 host + wrapper + fixtures on disk + `--stamp`
surgery + shell-side checks (criterion 15/17). T9 full gates, evidence, PR.

## Evidence (2026-08-04, criterion → check)

1. `StageModelTests.StageEnum_IsExactlyTheElevenAuthoredStagesInOrder` — names AND ordinals.
2. 9 malformed fixtures (pattern/const/enum×2/additionalProperties×2/required/minimum/unparseable)
   + L001 passes + `UnsupportedSchemaKeyword_FailsClosed` — all against the REAL schema bytes.
3. Unreachable-station, orphan×2, spacing<1.2 fail; decoy passes vacuously; top-15% → `Warn`,
   `Blocks == false`.
4. L001 bound printed = 44 (hand-derived); no row → `UNCONFIGURED(lowerBoundSlack)`; fixture row
   2 → L001 passes, timeLimit-40 fixture blocks.
5. Unsolvable blocks; Indeterminate prints its count non-blocking; NotFound non-blocking
   (ADR-0008:117); Solved passes. All four on authored-JSON fixtures through the real importer.
6. L001 zero-input → stage passes (Indeterminate via pin); initialRoute-0 board → stage FAILS.
7. L001 robust (windows [18], retention 100% — review F10 corrected the stale [17]); brittle
   2-tick-window fixture blocks on the WINDOW limb specifically; the ≥70% RETENTION limb is
   executed by `JitterLossLevel` (crafted edge-log, losses are timeouts, `pinned=0` asserted);
   onboarding 12–16 limb blocks; byte-identical verdict across two runs (Pcg32 stream); no-log →
   SKIPPED; L701's stage-6 row asserted non-blocking with its pin counts printed (review F2 rule).
8. `two >= three` blocks; row present → `PINNED(NEW-Q5)`; row absent → `UNCONFIGURED(starBandSlack)`.
9. All six raw axes hand-derived on L001 (B=8, peak=2, entropy=0, C=1, T=0.3125, H=8, R=1/2);
   weighted sum equals the frozen formula under fixture caps; no caps → `UNCONFIGURED` with raw
   axes printed; H prints `PARTIAL(Q-J)`.
10. near-identical < dissimilar distance; no row → `UNCONFIGURED(noveltyMinDistance)` with
    distances printed; row 5.0 → recycled level blocks, no-priors passes.
11. Absent key → `STALE` + "Q-O" verbatim + `Blocks false`; older → `STALE`; newer → `FRESH`;
    null reference → `STALE (unavailable)`.
12. L001 row (band, capstone false, 1 tester, `HUMAN-VERIFIED (pending)`); capstone → 3 testers.
13. The 8 with/without-row cases across stages 3/7/8/9 (tests in criteria 4/8/9/10 above);
    shipped config has jitterSampleCount only — `Config_ShippedFile_HasJitterRowAndNoQRRows`.
14. Corpus run: L001+L701+L702 all reported; stage 9 = `SKIPPED(non-campaign)` for L701 while
    stage 8 is not; stage-11 rows for both stress boards; campaign count `1/30` over campaign
    only; two-new-mechanics fixture blocks the corpus.
15. Wrapper: (a) full-corpus run exit 0 (L701 solves at 146,942 expansions, ticks 182; L702 at
    16,839, ticks 126); (b) `broken-level.json` (schema-valid, Unsolvable) exits non-zero naming
    L999 + Solver; (c) the entry-point grep is clean (it caught its own first comment); (d) zero
    file-API refs under Validation (wrapper belt + the existing check.sh Content block).
16. JSON: 11 stage rows/level, solve summary, `seconds == CompletionTicks/8`, `secondsVerdict ==
    PINNED(NEW-Q1)`, `exitFailure false`; table carries `PARTIAL(Q-J)` + `UNCONFIGURED`.
17. (a) SHA-256 of every input unchanged by the gate run; (b) `--stamp` on a corpus copy passes
    `--assert-stamp-diff` (exactly the one key; ≤1 changed + ≤1 inserted line) AND the stamped
    copy re-validates (regression belt on E-C2a-2); (c) `git diff --name-only` clean.

**Suite: 167/167** (59 new) · `check.sh` 0 · `test.sh` **5/5** (`PASS tests/validation/validator.test.sh` discovered) · CM-C1 golden hash unchanged (`d4818af8…`).

## Review round 1 (2026-08-04) — REQUEST CHANGES, all 14 findings applied

Fresh-context reviewer re-ran everything (167/167, gates, live corpus + broken-fixture runs) and
found: **F1** the ≥70% retention rule had no test that could fail (the "brittle" fixture failed
on windows alone; the `Or` assertion couldn't tell) — fixed with `JitterLossLevel` + a
limb-specific test; **F2** the pin-majority hatch discarded measured wins/losses — narrowed to
retention-over-unpinned-samples (see the stage-6 freeze above); **F3** the wrapper's 15b greps
were tautological (`Solver` appears in every table) — now greps the exact `BLOCKING: L999 stage 4
Solver` line; **F4** any `--corpus` path was classified campaign AND stampable — campaign status
now derives from the `content/levels/**` path, `StampFile` refuses `docs/plan/**` outright, and
the wrapper asserts no campaign noise in the broken-fixture run; **F5** the schema keyword audit
was data-dependent — now a schema-driven pre-pass (`AuditKeywords`) covering subschemas of absent
optional properties, non-object `items`, and `items`+`prefixItems`; **F6** decoy stations warn
instead of passing silently; **F7** the stress-board stage-8 assertion flipped to the positive
form (Unconfigured + axes present); **F8** the `IContentSource` seam is now the host's actual
read path; **F9** ISO instants compare as instants (mixed-offset test added; host normalises git
output to Z); **F10** stale evidence figure corrected; **F11** three culture-sensitive format
sites fixed; **F12** the stamp proof runs over a two-file corpus; **F13** the sln BOM reverted;
**F14** the wrapper refuses an empty corpus glob.

---

## FROZEN CONTRACT (verbatim copy from state/backlog.md @ main 03b0f5d — review verifies against THIS)
# CONTRACT CM-C5 — 11-stage level validator + the licence-free `validate-content` leg

**Roadmap:** D9 (`docs/plan/data/roadmap_56_days.csv:11` — "batch validator CLI; GitHub Action runs
level validation on every content PR"; acceptance "CI fails a deliberately broken level and passes
L1-L12").
**DEPENDS-ON:** **CM-C2a merged** (parsed DTOs + `LevelGraph` mapping) **and CM-C4 merged** (stages 3,
4, 5, 6, 7, 8 all consume solver output).

### Goal

A pure-C# batch validator implementing all 11 CM-R12 stages over `content/levels/**` and
`docs/plan/data/stress_boards.json`, runnable as a **credential-free, licence-free** job
(`scripts/validate-content.sh`) plus a fast-leg wrapper, which prints a per-level per-stage verdict
table and exits non-zero iff a **blocking** stage fails.

### Spec reference

`docs/prd/PRD.md` CM-R12.1–.6 (`:264-274`, including the AMD-09 note at `:274`) · CM-R06.1/.2
(mechanic set + one-new-mechanic ordering, `:184-185`) · CM-R07.6 (junction spacing ≥1.2, `:200`) ·
CM-R09.2 (difficulty ±0.05, `:225`) · CM-R04.2 (3★ solver-reachable, `:151`) · CM-R19.1
(solver-optimal time, **`[PIN NEW-Q1]`**, `:355-358`) ·
`docs/plan/specs/product_spec.md:637-647` (**the 11 stages verbatim**) and `:504-515` (the B/E/C/T/H/R
axes and weights) · `docs/adr/0008-content-pipeline-and-level-schema.md:109-123` (validation as a merge
gate; `meta.validatedAt` handling) · `docs/adr/0009-ci-topology-and-secret-custody.md:35`
(`validate-content` job: **no credentials**, 10 automated stages) · `docs/adr/0005-...:112,128-129`
(why the validator must not be a licensed Unity job).

### Acceptance criteria (17)

1. **The stage inventory is exactly 11 and matches the source.** A `Stage` enumeration declares
   exactly the 11 stages of `product_spec.md:637-647`, in order and with those names:
   `Schema, StaticAnalysis, LowerBoundFeasibility, Solver, TrivialityReject, BrittlenessAccessibility,
   StarCheck, DifficultyCheck, NoveltyCheck, Staleness, HumanPlaytest`. A contract test fails if a
   member is added, removed or reordered (the same shape as CM-C1's `FailReason` enum test).
   *Check:* one NUnit case asserting the member list and ordinals.
2. **Stage 1 — Schema.** Every level validates against `docs/plan/data/level_schema.json` including the
   `^L[0-9]{3}$` id pattern (`:10`), `schemaVersion const 2` (`:9`), the `band` enum (`:18`), the
   `mechanics` enum (`:20`), `additionalProperties: false` at every level, and every `required` list.
   *Check:* one NUnit case per rule over a matching malformed fixture (≥6 cases) + one asserting L001
   passes.
3. **Stage 2 — Static analysis.** Fails a level when: a station is unreachable from a source able to
   emit its colours; an orphan switch exists (a switch whose node has no inbound edge, or whose routes
   are not all outbound edges of its node); two junction centres are <1.2 grid units apart
   (`product_spec.md:638`; CM-R07.6). **Warns** (does not fail) when a switch sits in the top 15% of
   the board (`product_spec.md:638`). *Check:* three failing fixtures + one warning fixture asserting
   verdict `WARN` and a zero exit contribution.
4. **Stage 3 — Lower-bound feasibility.** Computes `minTravelTicks × requiredDeliveries` and compares
   against `win.timeLimitTicks` **with slack read from `config/validator_thresholds.json`**. The
   computed value is always printed. **The slack number is absent from the corpus (Q-R)** → with no
   threshold row the verdict is `UNCONFIGURED` (criterion 13). *Check:* one case asserting the computed
   lower bound for L001 equals a hand-derived value; one asserting `UNCONFIGURED` with no row; one
   asserting a fail with a row present and a violating fixture.
5. **Stage 4 — Solver.** Calls CM-C4 and fails a level that is `Unsolvable`. `NotFound(beam, 5000)` and
   `Indeterminate(pinned, …)` are **non-blocking** verdicts printed with their counts — the first
   because ADR-0008:117 admits a human witness replay, the second because of **Q-N**.
   *Check:* three cases, one per verdict, asserting blocking/non-blocking behaviour.
6. **Stage 5 — Triviality reject.** A zero-input run must **not** win, on any level including L001
   (CM-R12.2, `docs/prd/PRD.md:269`; `product_spec.md:641`). *Check:* one case asserting L001 fails a
   zero-input run (so the stage passes) + one asserting a contrived always-winning board **fails the
   stage**.
7. **Stage 6 — Brittleness / accessibility.** Applies **±1-tick jitter** to a winning command log over
   a fixed, seeded perturbation set and requires **≥70%** win retention; fails any level whose only
   solutions require action windows below `meta.minActionWindowTicks`; asserts onboarding-band levels
   use **12–16** ticks (CM-R12.3, `docs/prd/PRD.md:270`; `product_spec.md:642`; `level_schema.json:23`).
   The jitter set is derived from `Pcg32` seeded by the level `seed`, so the stage is deterministic.
   *Check:* three cases — a robust fixture ≥70%; a brittle fixture <70% failing; an onboarding level
   outside 12–16 failing.
8. **Stage 7 — Star check.** Fails any level whose `win.stars.three` is not reachable by the solver
   **within band slack**; also asserts the schema rule `stars.two < stars.three`, both ≥1
   (CM-R04.2/.3, `docs/prd/PRD.md:151-152`; `level_schema.json:127-131`).
   **Band slack is absent from the corpus (Q-R)** → `UNCONFIGURED` with no threshold row. Note that
   star *scores* depend on the pinned scoring model (Q-C), so the reachability limb reports
   `PINNED(NEW-Q5)` until scoring lands; the `two < three` limb blocks today. *Check:* one case per
   limb (3).
9. **Stage 8 — Difficulty check.** Computes all six axes **B, E, C, T, H, R** with the weights
   `0.20, 0.25, 0.20, 0.15, 0.15, 0.05` (`product_spec.md:504-511`), consuming CM-C4's integer
   `DifficultyProxy` for C/T/H/R, and fails a level whose computed `difficultyTarget` deviates from the
   authored value by **>0.05** (CM-R09.2). **Axis B's "normalized to band caps" names no caps (Q-R)** →
   axis B is computed and printed but its normalisation basis is `UNCONFIGURED`, which makes the whole
   stage `UNCONFIGURED` until the row exists. **Axis H prints as `PARTIAL(Q-J)`**: `product_spec.md:510`
   defines H as `min(queue+platform slack)`, but the platform term is unobservable while
   `PlatformOverflow` is never raised (`Outcomes.cs:40-42`), so CM-C4's `MinQueueSlackAtPeak` is the
   **queue term only** (CM-C4 criterion 6). The stage therefore never claims to have computed
   `product_spec.md`'s H. *Check:* one case per axis asserting the computed raw value on L001 against a
   hand-derived number (6), one asserting the weighted sum, one asserting the `UNCONFIGURED`
   propagation, one asserting axis H's printed verdict is `PARTIAL(Q-J)`.
10. **Stage 9 — Novelty check.** Computes a feature vector (board topology + wave signature) per level
    and the pairwise distance against all prior levels in play order; fails a level below the
    **threshold**. **The threshold is absent from the corpus (Q-R)** → `UNCONFIGURED`. The distance
    values are always printed. *Check:* one case asserting the distance between two deliberately
    near-identical fixtures is smaller than between two dissimilar ones; one asserting `UNCONFIGURED`.
11. **Stage 10 — Staleness.** Compares `meta.validatedAt` against the last sim/schema change; **an
    absent key is treated as stale** (ADR-0008:119-123). Because nothing stamps the key today, this
    stage would fail every level — so it **computes and prints its verdict and does not block, pending
    Q-O**, and the report says so verbatim. *Check:* three cases — absent key → `STALE`; a key older
    than the reference → `STALE`; a key newer → `FRESH`; plus one asserting a `STALE` verdict
    contributes **0** to the exit code while Q-O is open.
12. **Stage 11 — Human playtest.** Not runnable by CI (`docs/adr/0009-...:35` says 10 of 11 are
    automated). The stage emits a **checklist artifact row per level** (level id, band, capstone
    yes/no, required tester count — 3 for capstones per `product_spec.md:647`) and reports
    `HUMAN-VERIFIED (pending)`. It never blocks and never claims to have run. **Depends on D-6**
    (tester roster) — cited, not resolved. *Check:* one case asserting the artifact row set equals the
    corpus level set and that the stage's exit contribution is 0.
13. **`UNCONFIGURED` semantics are themselves tested.** A stage whose threshold row is absent from
    `config/validator_thresholds.json` prints `UNCONFIGURED(<row name>)` and contributes **0** to the
    exit code; the same stage with the row present blocks normally. *Check:* two NUnit cases per
    affected stage (3, 7, 8, 9) = 8 cases, run against a fixture config with and without each row.
    **No agent may add a value for the four Q-R rows** (stop condition 3).
14. **Corpus selection and the non-campaign carve-out (Q-P).** The validator's inputs are
    `content/levels/**` **and** `docs/plan/data/stress_boards.json` (`:3-75`, boards L701/L702).
    **Stress boards run stages 1–8 and 10, and stage 11 emits a checklist row for them.** Stage 8 is
    **included** because the boards carry an authored `difficultyTarget` (0.30 / 0.35,
    `stress_boards.json:6`) worth checking — it will report `UNCONFIGURED` while axis B's band caps are
    a Q-R row, which is a printed verdict, not a skip. Stage 11 emits a row because
    `stress_boards.json:2` requires the boards to pass the validator "**plus human playtest**".
    **Stage 9** (novelty-vs-prior-order) and the campaign-order assertions (CM-R06.2 one-new-mechanic;
    CM-R09.1's 30-level count; CM-R09.3's band table) print `SKIPPED(non-campaign)` for them, per
    `stress_boards.json:2` ("NOT campaign content: never enter the L001-L030 progression"). **The Q-P
    row states this same set; the two must not diverge** — the difference between "1–7" and "1–8" is
    load-bearing for both the exit code and the printed report. *Check:* one case asserting L701/L702
    are validated; one asserting stage 8 runs for them while stage 9 reports `SKIPPED(non-campaign)`;
    one asserting stage 11's checklist contains a row for each of L701/L702; one asserting the campaign
    count assertion is computed over `content/levels/**` only.
15. **CI entry point, credential-free, hosted by the contract's own exe.**
    `scripts/validate-content.sh` runs the batch validator by invoking
    **`dotnet run --project dotnet/CatMetro.Validator`** — the console host this contract owns, which is
    where **all** file I/O and the only on-disk `IContentSource` implementation live. It needs **no**
    Unity, no licence, no network and no secret (ADR-0009:35), and exits 0 iff every **blocking** stage
    passes on every level. **The validation logic under `unity/Assets/Scripts/Content/Validation/**`
    opens nothing**: `System.IO` is banned there by CM-C2a criterion 2's appended `check.sh` block, so
    every read arrives as bytes through `IContentSource` (ADR-0008:53-56). *Check:*
    (a) `bash scripts/validate-content.sh` exits 0 on the current corpus;
    (b) the same command against **`tests/validation/fixtures/broken-level.json`** — a CM-C5-owned
    path, because `tests/fixtures/content-bad/**` belongs to CM-C2a and writing there would be an
    out-of-scope diff under the longest-prefix resolution rule — exits non-zero naming the level and
    the failing stage; this is roadmap D9's acceptance verbatim ("CI fails a deliberately broken
    level");
    (c) `grep -rn 'secrets\.\|UnityEngine\|Unity ' scripts/validate-content.sh` returns nothing;
    (d) a `[CI]` grep asserting zero `System\.IO` matches under
    `unity/Assets/Scripts/Content/Validation/**`.
16. **Two output forms, one truth.** The validator emits (a) a human-readable per-level × per-stage
    table to stdout and (b) a machine-readable JSON report to a caller-supplied `--out <path>`
    containing, per level: id, per-stage verdict, per-stage computed value, and the CM-C4 `SolveResult`
    summary including `CompletionTicks`. **`CM-R19.1`'s 40–75 s check consumes `CompletionTicks ÷ 8`
    and is `[PIN NEW-Q1]`** — the seconds figure is computed and printed; the range comparison reports
    `PINNED(NEW-Q1)` and does not block. *Check:* one case asserting the JSON shape; one asserting the
    seconds figure for L001 equals `CompletionTicks / 8`; one asserting `PINNED(NEW-Q1)` is emitted.
17. **The gate run never writes to its own inputs (Q-O).** In gate mode the validator opens
    `content/levels/**` and `docs/plan/data/**` **read-only**; a separate, explicitly opt-in
    `--stamp` invocation is the only path that may write `meta.validatedAt`, and it writes only that
    key, preserving byte-for-byte everything else. *Check:* (a) a test asserting the gate run leaves
    every input file's SHA-256 unchanged; (b) a test asserting `--stamp` changes exactly one key and no
    other byte; (c) `git diff --name-only` on a CI-mode run shows zero content paths.
    **Both the gate read and the `--stamp` write happen in `dotnet/CatMetro.Validator`**, never in the
    `Content/Validation/**` library (criterion 15).
    Plus the fast-leg wrapper `tests/validation/validator.test.sh` discovered by `scripts/test.sh`:
    `bash scripts/test.sh` prints `PASS tests/validation/validator.test.sh` and a summary line matching
    `^test: [0-9]+/[0-9]+ passed` **whose two numbers the wrapper compares equal** (the backreference
    form `\1` is not POSIX ERE — see CM-C2a criterion 13).

### Scope boundary

**In scope:** the paths in the ownership table for CM-C5, plus registration-only appends.

**Explicit non-goals:**
- **No `.github/**` workflow file** — that is a risky path needing independent security review
  (**Q-V**; AGENTS.md; RK-37). CM-C5 delivers the script and the wrapper; wiring is a human PR.
- **No solver implementation** — CM-C4 owns it; CM-C5 calls it.
- **No level authoring** beyond the malformed fixtures it owns. **No schema change.**
- **No daily generation, no seed derivation, no salt loop** — CM-C6.
- **No value for any Q-R threshold**, no invented band caps, no invented novelty distance.
- **No `meta.validatedAt` write in gate mode**; no write anywhere under `docs/plan/**`.
- **No Unity, no `CatMetro.Editor`, no editor menu item.**
- **No `System.IO` under `unity/Assets/Scripts/Content/**`** — CM-C2a criterion 2's appended `check.sh`
  block bans it and CM-C5 may not edit that block. **All reads go through `IContentSource`**; the only
  filesystem code this contract writes lives in `dotnet/CatMetro.Validator/**` (criterion 15, Q-X).
- **No writes under `tests/fixtures/content-bad/**`** — that tree is CM-C2a's; CM-C5's own malformed
  fixtures live at `tests/validation/fixtures/**`.
- **No path matching `**/billing/**`, `**/iap/**` or `**/ads/**`**; any such need is a **stop
  condition** requiring `state/mode=production` first (AGENTS.md §Risky paths;
  `state/PROJECT_STATE.md:10`).
- **No writes to immutable paths** (AGENTS.md hard rule 1).

### Assumptions

- **A-C5-1** The validator lives in `CatMetro.Content` under `Validation/**` because ADR-0003:35 gives
  Content "schema+bounds validation" and Content may reference Domain (so it reaches CM-C4's solver);
  Content is **not** under a banned-symbol root, so the difficulty model's real arithmetic is legal
  there. If the architect places it elsewhere, Q-M's ruling applies to this contract too.
- **A-C5-2** Stage 6's jitter set is **seeded from the level `seed` via `Pcg32`**, so "≥70% win rate"
  is a deterministic figure and not a flaky one. The set size is declared in
  `config/validator_thresholds.json` as an ordinary configured row (it is a test-design number, not a
  product number).
- **A-C5-3** Stress boards run **stages 1–8 and 10** plus a stage-11 checklist row, and are excluded
  from stage 9 and the campaign-order assertions (**Q-P**, criterion 14 — the two statements are
  identical by construction).
- **A-C5-6** The console host `dotnet/CatMetro.Validator/**` is **analyst-assigned** (**Q-X**): the
  validation library may not do file I/O, so an executable must. It is a `dotnet`-leg tool exe with no
  Unity asmdef counterpart and therefore no row in ADR-0003's 13-assembly list; if the architect rules
  otherwise the remedy is a rename, and **no criterion changes meaning**.
- **A-C5-4** "the last sim/schema change" (stage 10's reference point) is taken as the most recent
  commit timestamp touching `unity/Assets/Scripts/Domain/**` or `docs/plan/data/level_schema.json`.
  **Analyst-authored** — no source defines it. Recorded in the report so a human can overrule.
- **A-C5-5** `[CI]` criteria CM-R12.1's "all 11 stages run in CI on every content PR" is satisfied by
  the script existing and being green; the *trigger* is Q-V's human PR.

### Stop conditions

Defaults apply. Plus:
1. A stage cannot be implemented without a Domain change → stop (golden-invalidating).
2. A stage needs a level-schema field that does not exist → stop (Q-F, schema frozen).
3. **Any temptation to pick a number for a Q-R row** (lower-bound slack, band slack, novelty threshold,
   axis-B band caps) → **stop and ask**; ship `UNCONFIGURED`.
4. Stage 10 appears to require stamping inside the gate run → stop and cite Q-O.
5. The corpus contains a level that cannot be classified as campaign or non-campaign → stop (Q-P).
6. A stage requires a Unity licence, a network call or a secret → stop; that breaks ADR-0009:35 and the
   entire economics of ADR-0008.
7. A stage requires resolving NEW-Q1, NEW-Q4, NEW-Q5, NEW-Q9, NEW-Q21 or D-6 → stop; report `PINNED`.

---
