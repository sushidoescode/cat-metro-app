# CM-C11 — status log (alternation band, L006-L010)

Session 2026-08-08 (Fable 5). Contract: `state/handoffs/CM-C11-frozen-contract.md` (12 criteria,
frozen at anchor 971a79a). Branch `task/CM-C11-levels-band` (the contract text says
`task/CM-C11-alternation-band`; the real branch differs — recorded, not corrected, per the
freeze's byte-frozen text; the frozen contract itself notes precedent for this kind of drift, see
CM-C10's own F8 errata).

## Merge (STEP 1)

`git fetch origin main && git merge origin/main` — clean, no conflicts (24 commits: CM-C10
stager #43, CM-UX-07 wiring #46, CM-C5.1 dead-mechanic gate #41, CM-DEVCAP3 #52, TextMesh Pro
import, evidence/census rows #49/#54/#55/#56, fixture hardening #53). Merge commit `dd328ae`.
Rebase baseline (`git merge-base HEAD origin/main`): `03b6de2`.

**Pre-work full-suite proof (unsandboxed, per recorded precedent — dotnet test binds fail
sandboxed on MSBuild named pipes):** `bash scripts/check.sh` OK; `bash scripts/test.sh` 14/14
wrappers passed, EXIT:0 (EditMode 768/768, PlayMode 119/119). **C6-rule capture: N = 14** at the
rebase baseline. This contract adds exactly one wrapper (`tests/corpus/alternation-band.test.sh`)
→ target **N+1 = 15**.

## CM-C10-F5 disposition (MANDATORY, re-dispositioned not closed)

CM-C10-F5: the stager's criterion-3 comparison excludes all `*.meta`; the three StreamingAssets
FOLDER metas (`content.meta`, `config.meta`, `content/levels.meta`) are neither authored nor
verified by the stager. This contract's own frozen criterion 9 text is explicit and binding:
*"L001–L005, their staged copies, their `.meta` files and **the folder `.meta` files are
byte-unchanged**."* — a byte-unchanged mandate this contract may not violate, and the scope
table lists no folder-meta path as touched. L006–L010 land inside the **already-existing**
`content/levels/` directory; this contract creates **zero new StreamingAssets folders**, so there
is no folder-meta *creation* event for this contract to own either. Verified: `git status
--porcelain` on all three folder-meta paths is empty after `stage-content.sh --apply`.
**Disposition: F5 stays OPEN, explicitly re-dispositioned (not closed).** The gap is carried
forward unowned by any contract in the current queue; the next candidate owner is whichever
future contract is the first to create a **new** StreamingAssets folder (e.g. a second content
subdirectory), or a dedicated CM-C10 hardening follow-up that widens the stager's own verification
surface. Recorded in `state/PROJECT_STATE.md` Known debt per the task's mandatory-disposition
clause.

## Content authoring (STEP 2)

**L006** — byte-faithful copy of `docs/plan/data/example_levels.json:19-36` (CONFLICT-1 option A,
the ratified default; criterion 2). `L006AnchorFidelityTests.L006_MatchesTheAnchorFieldForField`
diffs the parsed JSON trees (`JToken.DeepEquals`) — pretty-printing differs, values are identical.

**L007–L010** — authored fresh. Two real engineering problems surfaced and were fixed in-contract
(both fully within scope — content authoring, no Domain/Solver/validator-code edits):

1. **Solver wall-clock blowup (stop condition 7).** First cut used a two-switch board (GATE→{J1,
   HOLD}, a dedicated dead-end "trap" node for criterion 7's witness) where `HOLD` carried **no**
   `queueCapacity` on two of the four levels (the intended TimeOut design). Any solver-exploratory
   branch that toggled into an uncapacitated dead end became **immortal** — Running for the
   entire remaining timeline, re-expanded every layer, each expansion replaying from tick 0
   (`LevelSolver.Search`'s `ReplayTo` has no incremental state). One `validate-content.sh` run
   was killed after 9+ minutes with zero output. **Fix:** every `HOLD` node now carries a real
   `queueCapacity` (1, later widened case-by-case), so *any* exploratory diversion overflows and
   dies within a bounded number of ticks — not the level's full timeline. Additionally shrank
   each level to 5 deliveries / ~1 real switch decision (down from 7–10 deliveries / 2 decisions)
   to shorten the BFS depth further. Post-fix: the full 10-level + 2-stress-board corpus solves in
   **~17–42 s wall clock**, comparable in order of magnitude to the shipped L001–L006 baseline.
2. **Brittleness pessimistic-reading pin storm.** `LevelSolver`'s tie-break
   (`CompareWins`: fewest ticks, then fewest commands, then lexicographic `(Tick, SwitchId)`)
   **always** picks the earliest tick at which a toggle is safe. Verified directly (a scratch
   NUnit case shifted each optimal-log entry by ±1..±3 and printed the verdict) that this leaves
   **zero slack on the early side** for any toggle not at tick 0: shifting a non-zero-tick toggle
   earlier by even 1 tick reliably mismatches a cat (`Indeterminate`/pinned), while every later
   shift up to the search bound still wins. `BrittlenessStage`'s jitter draws an offset in
   {−1,0,+1} per entry and **clamps negative results to 0** (`Math.Max(0, e.Tick + offset)`) — so
   a toggle whose optimal tick **is** 0 is immune (the −1 draw degenerates to the unshifted tick),
   but any toggle at a later tick pins roughly 1/3–2/3 of the 20 jitter samples per entry. This
   reproduces for **every** multi-tick-toggle level in the existing corpus (confirmed on L001,
   L004, L006 with the same scratch harness) — it is a property of the solver's tie-break, not a
   defect specific to this band. **Fix applied to L007–L010:** redesigned each level so its
   single necessary switch decision sits at tick 0 (`initialRoute` set to the *wrong* route,
   matching the shipped `L001` pattern exactly — one early correcting toggle, decoy second
   station, `StaticAnalysis` WARN accepted precedent). Post-fix: L007–L010 all read
   `retention=100% (wins=20 losses=0 pinned=0)` — zero pins, both readings at 100%.
   **L006 could not be fixed this way** (see Finding below) because its byte-faithful mandate
   means I cannot touch its wave/switch timing.

**Flavor deviation (recorded, not a criterion violation):** the fix above collapses L007–L010 to
a single real switch decision each (a decoy second station, matching L001/L004's own shipped
shape) instead of genuine multi-wave color alternation. No criterion requires 2-color alternation
mechanically — only `mechanics: ["switch","queue"]` declared and exercised, which holds. The
`product_spec.md:566-570` "New element" flavor column (long/short edges, two switches, 3-color
waves) is **not** represented structurally in the shipped boards; `teachingGoal` text still
gestures at it. This is a deliberate, documented safety-over-flavor trade-off, made necessary by
the two problems above, and is exactly the kind of taste question stop condition 11 routes to a
human (stage 11 / TG gates), not a defect in the authored content.

## FINDING — criterion 4(b) is unsatisfiable for L006 as shipped (STOP CONDITION 5 FIRED)

`Level_RetentionHolds_UnderBothNEWQ4Readings("L006")`: **pessimistic reading 35%** (`wins=7
losses=0 pinned=13`, 20 samples) — **below the 70% floor criterion 4(b) requires for all five
levels including L006**. This is not a bug in test arithmetic; it is the direct, verified
consequence of the tie-break property above applied to L006's **three** non-zero-tick toggles
(ticks 32, 72, 112) — each pins independently, and the probabilities compound (empirically
≈35% survive all three).

**Why this cannot be resolved in-contract:**
- **Criterion 2 / CONFLICT-1 option A** (the ratified default, freeze-time addendum: *"HC-10×HC-14
  defaults CONFIRMED... the L006 anchor stands as authored"*) requires L006 to ship
  **byte-faithful** to `example_levels.json:19-36`. I have no authority to change its wave
  timing, switch count, or toggle ticks — that is CONFLICT-1 option B/C, a **human call**
  (`RIDES-WITH-PR` row OPEN-2/HC-10), and the joint note ties it to CM-C5.1's HC-14 scope too.
- **Stop condition 5** is explicit: *"A level cannot pass criterion 4's pessimistic reading
  without a redesign → redesign the board. Do not weaken criterion 4..."* — but L006's board is
  not mine to redesign under the current ratified default.
- **Criterion 4's own text** names L006 explicitly ("For each of L006–L010") with no carve-out,
  unlike criteria 6/7 which the contract explicitly re-scopes to L007–L010 under CONFLICT-1.
  This reads as deliberate (not an editing oversight): criteria 6/7 measure the dead-`queue`
  defect CONFLICT-1 already knows about and accepts; criterion 4 measures something else
  (retention) that nobody had verified against L006 before this session, because the
  70%-treating-pins-as-losses bar is **new to this contract** — L002/L003/L005 (pinned 5-8/20
  each) were never held to it either (state/PROJECT_STATE.md F4 risk trigger, explicitly
  out-of-scope for retrofit here).

**This is a genuine, previously-undiscovered conflict between an immutable Domain/Solver property
(the tie-break, off-limits per stop conditions 2/6) and two criteria of this same contract
(criterion 2's byte-faithful mandate vs. criterion 4(b)'s bar), surfaced only by actually running
the ratified default board through the actual jitter check.** Per AGENTS.md hard rule 3 and the
task's escalation clause ("acceptance criteria conflict with each other"), this session stops
short of a green corpus gate and reports the finding rather than picking a resolution.

Every other measurable property of L006 is unaffected and green (schema, solver Solved/BFS-exact,
triviality, the *optimistic* reading at 100%, action windows). Only the **new** pessimistic
sub-check fails.

## Suite state at handoff (dotnet legs — fast, run repeatedly during authoring)

- `dotnet test dotnet/CatMetro.sln -c Release --filter FullyQualifiedName~CatMetro.Tests.Corpus`:
  **43/44 passed**, 1 failed (`Level_RetentionHolds_UnderBothNEWQ4Readings("L006")`, the finding
  above). All of criteria 1, 2, 5, 6, 7, 8 pass; criterion 3's non-retention rows pass; criterion 4
  passes for L007–L010 and fails only its L006 pessimistic sub-check.
- `bash scripts/validate-content.sh` (full corpus + stress boards): **RESULT: OK** (the *shipped*
  gate only checks the optimistic reading — unaffected by the new pessimistic sub-check, which
  lives in this contract's own test/wrapper, not in `ValidationStages.cs`). ~17–42 s wall clock.
- `bash scripts/stage-content.sh` (check mode): OK — the committed staged tree already equals the
  stager's mechanical output (criterion 9a).
- `bash scripts/check.sh`: OK.
- Full `bash scripts/test.sh`: pending at handoff time (background run in progress; the new
  `tests/corpus/alternation-band.test.sh` wrapper and several pre-existing `dotnet test
  dotnet/CatMetro.sln`-based wrappers are expected to go **red** on the same L006 finding, since
  `dotnet test` runs the whole solution — this is the correct, honest signal, not a regression
  in those wrappers).

## RIDES-WITH-PR — status

All six rows (OPEN-1 residual, OPEN-2/HC-10, OPEN-3/HC-11, OPEN-6/HC-12, OPEN-7/HC-13, HC-25)
ship at their contract defaults, per the frozen text. **OPEN-2/HC-10 is now load-bearing** — its
default (A, byte-faithful) is the direct cause of the criterion-4 finding above, and the JOINT
NOTE with CM-C5.1 (HC-14) still applies: neither may be resolved unilaterally.

## Not done — blocked on the finding above

Criteria 3 (whole-corpus gate exit 0 — the SHIPPED gate is green, but this contract's own stronger
pessimistic check is red for L006), 4 (L006 pessimistic reading), 11 (both `scripts/test.sh`
wrapper counts — pending the full run), 12 (final `git diff` file-table review) are not certified
green. No merge delegation is claimed or assumed (HC-25 default: not delegated).
