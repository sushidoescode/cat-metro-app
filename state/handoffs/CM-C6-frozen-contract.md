# CONTRACT CM-C6 — Daily-seed pre-validation pipeline (pure-C# subset of CM-R46)

**Roadmap:** D12 (`docs/plan/data/roadmap_56_days.csv:14` — "Daily Line seeded mode behind a feature
flag"; acceptance "the same daily seed produces an identical level on 2 devices").
**DEPENDS-ON:** CM-C4 merged **and** CM-C5 merged.

### Goal

A deterministic, clock-free, engine-free pipeline that, given a list of date keys, derives each date's
seed, runs the bounded salt loop, validates each resulting board through CM-C5's blocking stages, and
emits a run artifact that prints the resolved seed per date key — the truth source CM-R43.8 compares a
device against.

### Spec reference

`docs/prd/PRD.md` CM-R46.1–.3, .5 (`:727-735`) · CM-R11.1 (fixed seed vectors, `:254`) · CM-R11.7
(the `"CM-DAILY-1"` constant asserted unchanged through Sep 30, `:260`) ·
`docs/plan/specs/liveops_spec.md:22-27` (seed = lower 32 bits of `SHA-256("CM-DAILY-1|" + local ISO
dateKey + "|" + k)`), `:29-31` (local-midnight rollover; UTC explicitly rejected), `:51-56`
(the `validate-dailies` job and the salt loop), `:57` (generator version frozen) ·
`docs/adr/0009-ci-topology-and-secret-custody.md:35` (`validate-dailies` over the next 90 dates,
printing the resolved seed per dateKey) · `docs/adr/0008-...:9-15` (the three distinct quantities:
90 pre-validated dates ≠ 30-board backup pool ≠ 40 shipped levels).
> **[CONFLICT carried, not resolved]** `docs/prd/PRD.md:252` records **NEW-Q8** — `product_spec.md:447`
> gives UTC + `"CM-DAILY-"`, `liveops_spec.md:22-31` gives local dateKey + `"CM-DAILY-1|"`. CM-R11
> **adopts liveops** pending human confirmation; CM-C6 implements the adopted reading and pins the
> constant (criterion 1).

### Acceptance criteria (11)

1. **Seed derivation with fixed vectors, and the constant is pinned.** `DailySeed.Derive(dateKey, k)`
   returns the **lower 32 bits of `SHA-256("CM-DAILY-1|" + dateKey + "|" + k)`**
   (`liveops_spec.md:22-27`). Three known dateKeys produce three seed values recorded in the test
   source (CM-R11.1). A contract test asserts the literal generator constant is exactly `"CM-DAILY-1"`
   and fails if it changes (CM-R11.7, `liveops_spec.md:57`).
   *Check:* three NUnit vector cases + one constant test.
2. **No clock, anywhere.** Date keys are **inputs** (a `IReadOnlyList<string>` of `yyyy-MM-dd`), never
   read from a clock. `IClock` is not referenced; `DateTime`/`DateTimeOffset` do not appear under
   `unity/Assets/Scripts/Content/Daily/**`. This is what keeps the local-midnight/DST question
   (`liveops_spec.md:29-31`) out of a pure-C# contract entirely.
   *Check:* one appended `scripts/check.sh` grep block over the Daily root with a negative fixture +
   one NUnit case asserting the pipeline signature takes the date list.
3. **The horizon is a constant, not a literal, and its value is 90 (Q-Q).**
   `DAILY_PREVALIDATION_DAYS = 90` is declared in `config/daily_pipeline.json`, **copied from the
   corpus with the citation on the row** — CM-R46's heading says "90 dates pre-validated in CI"
   (`docs/prd/PRD.md:727`) and ADR-0009:35 says `validate-dailies` runs "over the next 90 dates" — and
   read by both the job and the tests (PRD constant convention, `docs/prd/PRD.md:88`).
   **This is a corpus number with exactly the status of the beam widths, not an agent choice**, so the
   criterion does not need Q-Q resolved to pass; what Q-Q still guards is only ADR-0008:9-14's warning
   that the **30-board dated backup pool is a different quantity**, which this pipeline never touches.
   **The criterion instance runs the configured 90; the 30-date run is the smoke instance** — the same
   shape ADR-0006:224-227 uses for `QUEUE_MAX_EVENTS`/500.
   *Check:* one case asserting the pipeline processes exactly `DAILY_PREVALIDATION_DAYS` (= 90) dates;
   one 30-date smoke case; one asserting the value is read from the file and not hard-coded (grep);
   one asserting the file's row equals 90.
4. **Bounded, deterministic salt loop.** If `k = 0` produces a board failing any blocking CM-C5 stage,
   `k` increments deterministically until a board passes or `SALT_MAX_K` (declared in
   `config/daily_pipeline.json`) is reached; the resolved `k` is reported per date
   (`liveops_spec.md:55-56`; CM-R46.3). Two runs over the same date list produce the identical `k` for
   every date. *Check:* one case with a stub factory failing at `k=0` and passing at `k=1` asserting
   `k == 1`; one asserting `SALT_MAX_K` exhaustion yields a reported failure, not an infinite loop; one
   asserting `k` equality across two runs.
5. **Each date's board runs CM-C5's blocking stages.** A date whose board fails a blocking stage fails
   the job with the date key, the stage and the reason printed (CM-R46.1: "a failing date blocks
   merge"). Non-blocking verdicts (`UNCONFIGURED`, `PINNED`, `Indeterminate`, `STALE`) print and do not
   fail — the same semantics CM-C5 criterion 13 establishes, so the two jobs cannot disagree.
   *Check:* two cases (blocking fail → non-zero; non-blocking verdict → zero) + one asserting the
   printed reason names the stage.
6. **The artifact is the truth source, and it prints — written by the contract's own exe.**
   `scripts/validate-dailies.sh --out <path>` invokes
   **`dotnet run --project dotnet/CatMetro.DailyTools -- --out <path>`** — the console host this
   contract owns, which is where **all** file I/O lives — and that host writes JSON with one record per
   date (`{dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}`) **and** prints one stdout
   line per date matching `^DAILY_SEED <dateKey> <k> <seed>$` (ADR-0009:35 "printing the resolved seed
   per dateKey"; the source CM-R43.8 compares a device against, `docs/prd/PRD.md:695`).
   **The pipeline logic under `unity/Assets/Scripts/Content/Daily/**` opens and writes nothing**:
   `System.IO` is banned there by CM-C2a criterion 2's appended `check.sh` block, so config arrives as
   bytes through `IContentSource` and the artifact is serialised in-memory and handed to the host.
   *Check:* one case asserting the JSON record shape for every date; one asserting exactly one
   `DAILY_SEED` line per date and no other line starting with `DAILY_SEED`; one `[CI]` grep asserting
   zero `System\.IO` matches under `unity/Assets/Scripts/Content/Daily/**`.
7. **Byte-identical across runs.** Two invocations over the same date list and the same config produce
   **byte-identical** artifacts (this is the pure-C# half of roadmap D12's "the same daily seed
   produces an identical level on 2 devices"; the two-device half is CM-C2b/device work and is **not**
   claimed here). *Check:* one wrapper-level `diff` of two runs.
8. **The board generator is out of scope and stop-gated (Q-S).** The pipeline consumes an
   `IBoardFactory { LevelDto Build(uint seed, string dateKey, int k); }`. **No shipped implementation
   is written** — the corpus specifies no board-shaping rule anywhere, and NEW-Q21's weekday curve file
   does not exist. Tests supply a stub factory. *Check:* one case asserting the pipeline is fully
   exercised through a stub; one asserting no type under `Content/Daily/**` implements `IBoardFactory`
   (grep), so the gap cannot be silently filled.
9. **Weekday ramp reads a file that does not exist yet.** The ramp check (CM-R46.5, **`[PIN NEW-Q21]`**)
   reads `config/daily_weekday_curve.json`; **absent → the check prints `UNCONFIGURED(NEW-Q21)` and
   does not block**, exactly as CM-C5 criterion 13. Neither candidate curve
   (`liveops_spec.md` 0.35…0.75 vs `product_spec.md:452` 0.30…0.55) may be committed by an agent.
   *Check:* one case asserting `UNCONFIGURED(NEW-Q21)` with the file absent; one asserting the ±0.05
   comparison runs when a fixture curve is supplied.
10. **Harness discovery.** `tests/daily/daily-pipeline.test.sh` exits 0 iff `dotnet test` is green and
    performs criterion 7's two-run diff; `bash scripts/test.sh` prints
    `PASS tests/daily/daily-pipeline.test.sh` and a summary line matching `^test: [0-9]+/[0-9]+ passed`
    **whose two numbers the wrapper compares equal** (the backreference form `\1` is not POSIX ERE —
    see CM-C2a criterion 13).
    *Check:* `bash scripts/test.sh` exits 0 with both lines and the numeric comparison passing.
11. **Scope guard, asserted.** No backend, no store, no push, no analytics, no clock, no `UnityEngine`,
    no network: a `[CI]` grep over `unity/Assets/Scripts/Content/Daily/**` and
    `scripts/validate-dailies.sh` finds zero occurrences of `Http|WebRequest|UnityEngine|DateTime|
    IClock|OneSignal|RevenueCat|Firebase`. The **device-side** limbs of CM-R46 — the 250 ms bounded
    salt loop (CM-R46.3) and the ≤200 ms boot validation with backup-pool fallback (CM-R46.4) — are
    **explicitly not claimed here**; they are device work and are recorded as deferred in the PR.
    *Check:* one grep assertion with a negative fixture + the PR's deferral note.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C6, plus registration-only appends.

**Explicit non-goals:** no `.github/**` workflow (**Q-V**); no board generator (**Q-S**); no weekday
curve values (**NEW-Q21**); no seed-scheme choice (**NEW-Q8** — implement the adopted reading, pin the
constant); no clock, no timezone, no DST logic; no `daily_overrides.json`, no `daily_backup_pool.json`,
no `catalog.json`/`content.sha256` (the shipping pipeline is a later contract); no device budget
claims; no Unity; **no `System.IO` under `unity/Assets/Scripts/Content/Daily/**`** — CM-C2a criterion
2's `check.sh` block bans it and CM-C6 may not edit that block, so **all reads go through
`IContentSource`** and the only filesystem code this contract writes lives in
`dotnet/CatMetro.DailyTools/**` (criterion 6, Q-X); no writes to immutable paths.

### Assumptions

- **A-C6-1** The horizon is **90, copied from `docs/prd/PRD.md:727` and ADR-0009:35** — a corpus
  number, not an agent choice, with the same status as the beam widths (**Q-Q**). It is declared as a
  configured row so the tests and the job read one source, not so an agent may pick it.
- **A-C6-2** `SALT_MAX_K` is the **one** unpinned number here and is analyst-authored —
  `liveops_spec.md:55-56` describes the loop but names no ceiling. Declared with its derivation in the
  config file and flagged in the PR. Stop condition 3 deliberately does **not** cover it: forbidding
  the commit that criterion 4 requires would make the criterion unpassable.
- **A-C6-5** The console host `dotnet/CatMetro.DailyTools/**` is **analyst-assigned** (**Q-X**): the
  pipeline library may not do file I/O, so an executable must. Same status and same remedy as A-C5-6.
- **A-C6-3** The seed's `k` component is serialised as its invariant decimal representation; nothing in
  the corpus states the encoding, so it is fixed here and recorded — **changing it changes every daily
  seed**, so it is treated as golden-adjacent and named in the PR.
- **A-C6-4** Date keys are supplied by the caller in `yyyy-MM-dd` form (`overview.md:221`
  `IClock.LocalDateKey`). CM-C6 validates the form and rejects anything else.

### Stop conditions

Defaults apply. Plus:
1. Any need to read a clock, a timezone database or a network → stop.
2. **Any temptation to write a board generator** because the pipeline "needs one to be useful" → stop
   and cite Q-S; a stub factory is the deliverable.
3. **Any temptation to commit a weekday curve value** that is not human-ratified → stop (NEW-Q21;
   criterion 9 ships `UNCONFIGURED`). **Narrowed deliberately:** this condition no longer covers the
   horizon or the salt ceiling, because it previously forbade the very commits criteria 3 and 4
   *require*. The horizon is **90, copied from `docs/prd/PRD.md:727` / ADR-0009:35** (a corpus number,
   A-C6-1); `SALT_MAX_K` rides A-C6-2's declare-with-derivation route and is flagged in the PR.
   A number that is neither in the corpus nor derivable **is** still a stop.
4. NEW-Q8 appears to need answering to pick a seed scheme → stop; the adopted reading plus the pinned
   constant is the sanctioned path.
5. A daily board cannot be validated without changing a CM-C5 stage → stop; that is a CM-C5 amendment.
6. Anything requires `state/mode=production` or touches a monetization path → stop.

---
