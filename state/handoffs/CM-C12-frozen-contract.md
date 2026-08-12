# CONTRACT CM-C12 — queue-reading band L011–L017

**Branch:** `task/CM-C12-queue-reading-band`
**Frozen:** 2026-08-12 against `origin/main` `92e767c` (contains the #66 tie-break-fixed
solver), as the FIRST commit on the branch.
**Lane stewardship:** coordination-session-ADOPTED (the original Lane 3 chat closed
before publication; the human directed the coordination session to take the lane).
Authorship: this session's implementer agents. Review: fresh-context independent legs
ON the PR — the author never reviews its own diff. Merge: the human's fresh HC-25 word
in the coordination chat; the venue is recorded in the census.
**Ground truth:** `state/handoffs/PARALLEL-PUSH-2026-08-09.md` Lane 3 row (v3.0, on
main) + the coordination count-block amendment (Addendum v2.1, in flight on the
addendum PR at freeze time; this contract cites it and executes it at merge).

## Rulings and surfaced conflicts

1. SPEC CONFLICT — SURFACED, NOT RESOLVED (per the v3.0 instruction): product_spec.md
   line ~350 says "Market Cross (L011–L015)"; the LOCKED band table (~:523), the
   per-level ladder (~:571–577), and the validator code (`CorpusValidator.cs:276`) all
   say L011–L017. This band is AUTHORED per the LOCKED table + code. The human's
   disposition of the :350 prose line is pending and may attach to the merge word; the
   `docs/plan/**` errata is human-authored only.
2. The #61-ratified WRAP behavior is preserved: the last wired band level wraps to
   L001.
3. Design findings recorded as reusable lessons (full detail in the PR body): (a)
   shared-work-meter exhaustion — 4+ decision windows stacked on one multi-route
   switch produces NotFound(Budget); (b) a single burst wave with spacingTicks:1 never
   produces externally observable queueing — burst waves use L004's same-tick
   multi-wave precedent.

## Acceptance criteria

1. `content/levels/L011.json`–`L017.json` authored, schema v2; every BLOCKING
   ValidationStages stage passes corpus-wide (`scripts/validate-content.sh` → OK).
2. Band mechanics covered across the seven boards: queue-as-buffer, chained queues,
   burst waves (4+ cats, same-tick wave objects), shared mid-node, symmetric-board
   misdirection, min-spacing waves — with a mechanics coverage map in the PR.
3. Difficulty: §22 per-level ladder values are the authored targets; band envelope
   0.28–0.36 with 68–78% FA targets; computed-vs-authored within ±0.05 asserted
   against the ladder (the shipped CI comparison is inert corpus-wide — a
   pre-existing, human-decided gap; directional evidence goes in the PR, no CI claim).
4. Real multi-decision boards: per-level solver metrics (status/frontier/windows/
   retention/nodes, solved BFS-exact) and the full novelty-distance matrix in the PR;
   minimum pairwise novelty ≥1.5 across the band and vs L001–L010.
5. Staged copies under `unity/Assets/StreamingAssets/content/levels/` produced by
   `scripts/stage-content.sh --apply`, byte-identical, with metas.
6. `unity/Assets/Tests/EditMode/Pure/Corpus/QueueReadingBandTests.cs` (NEW file, never
   touching AlternationBandTests.cs/BandFixtures.cs) — two-sided pins per level — and
   `tests/corpus/queue-reading-band.test.sh` (NEW wrapper, alternation-wrapper shape)
   both green. Unity's bundled NUnit predates Is.AnyOf — Is.EqualTo(..).Or.EqualTo(..).
7. Declared exceptions EXECUTED (the v3.0 row's two + the v2.1 count amendment):
   (a) GameRoot band wiring extended to the full authored campaign L001–L017 (the
   v3.0 sanction: "runtime wiring of bands beyond L001–L005"), WRAP-to-L001 intact;
   (b) `LoadNextBandTests` re-pinned to the extended band set (red-by-design → re-pin);
   (c) the CM-R09.1 count expectation BLOCK — every "10/30"-style literal, condition
   AND fail message — in `tests/corpus/alternation-band.test.sh` re-recorded to 17/30,
   touching nothing else in that file.
8. Full `bash scripts/check.sh` + `bash scripts/test.sh` green; full dotnet suite
   green; Unity EditMode batch compile+run attempted and recorded (the Is.AnyOf
   masking lesson — the dotnet leg alone does not prove Unity compilation).
9. Fresh-context review legs posted ON the PR (risk gate priced per
   scripts/forge-risk.sh; expect RISKY); census facts (arm/land local −0700 times,
   the word verbatim, evidentiary class) on the PR at merge.

## State writes (exhaustive)

(1) this file; (2) ONE PROJECT_STATE row at merge (second-lander takes the
update-branch merge; >140-line append → STOP and ping the human rotation ask);
(3) the Known-debt bullets this contract names: none.

## Must not touch

Everything else — GameRoot beyond the criterion-7a lines, `Domain/**`,
`AlternationBandTests.cs`/`BandFixtures.cs` beyond criterion-7c's wrapper block,
ValidationStages thresholds, `config/validator_thresholds.json`, `docs/plan/**`,
`unity/Packages/**` (revert packages-lock.json after any editor session),
Scene/ProjectSettings/URP, `Presentation/**`.
