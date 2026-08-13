# CM-RESID13 — recorded-residuals micro-PR (frozen contract)

Frozen 2026-08-13 by the coordination (takeover) session at main=`23a23e0` (SHA verified
post-fetch). Authority for the task list: the owed-follow-ups records on PR #76's round-2
verdict (Minor-10 + the L015 wording nit) and PR #77's round-2 verdict + merge record
("Follow-ups owed (one micro-PR): Minor-10 lemma comment + L015 'hold' wording + NEW-1 +
Residuals 2/3/5 + the two Recently-done nits").

**Scope: documentation/prose only.** No behavior, no test semantics, no assert changes, no
thresholds, no gate edits. Every edit corrects a committed record to match its primary
source (the review records cited per criterion). One branch, one PR (AGENTS.md rule 4).

## Criteria

1. **Minor-10 (#76 round 2):** `unity/Assets/Tests/EditMode/Pure/Corpus/QueueReadingBandTests.cs`
   comment block (≈:344-366): keep the single-incoming-edge limb verbatim; replace the
   falsified ≥3-converging-edge arity claim with the review record's stronger optimality
   argument — on a single-source board inflow is mouth-capped at 1 cat/tick upstream, so a
   parallel converging route can only add delay, never throughput; since the liveness
   observables sample the solver-optimal log, downstream buffering can never appear there.
   The reviewer's two-edge depth-2 counter-example is acknowledged in place. Comment-only.
2. **L015 wording (#76 round 2 residual nit):** `content/levels/L015.json` `teachingGoal`
   no longer opens with "hold" as a taught behavior on a board where nothing holds (HOLD is
   a dead-end decoy; `deliveries` equals total spawned cats, so any HOLD routing loses).
   New text ≤160 chars and pairwise-distinct across L001–L017 (both pinned by existing
   corpus tests). Staged copy re-synced via `scripts/stage-content.sh` ONLY (N1: the stager
   stays the single StreamingAssets author).
3. **NEW-1 (#77 round 2):** PROJECT_STATE CM-C12 row — collapse the "4 of 7" / "4–5 of 7"
   duplication into one corrected statement carrying the heading-said-four /
   enumeration-names-five parenthetical; delete the appended duplicate sentence.
4. **Residual-2 (#77 round 2):** PROJECT_STATE Now line — the "Chat-8 `.github` commit →
   art chain lands → fresh APK" causal arrow corrected to match #65's own governing blocker
   list (whose correction already sits ~600 chars earlier in the same line).
5. **Residual-3 (#77 round 2):** restore the #74-owed staleness-fix discharge record — the
   sentence present at #77's round-1 tip `8aeee1f` ("Cross-file staleness flagged by the
   #74 review is fixed in THIS append's PR (the line-47 clause + this bullet's forward
   language).") was DELETED rather than updated by the round-2 fix commit `3d594c4`.
   Restore it into the census fifth append in completed-past form with a bracketed
   restoration note naming this criterion as the sanction.
6. **Residual-5 (#77 round 2):** PROJECT_STATE Recently-done burst line — qualify "the
   coordination session's first merge under the v2.3 regime" as contingent on the census's
   #69/#66 classifications (the reviewer's own proposed closing form).
7. **Recently-done nit A (#77 round 2 — the two nits are UNENUMERATED on any record;
   identified by inspection, basis disclosed in the PR body):** the burst header
   "2026-08-10/11/12" excludes the 08-09 landings the same line lists (#63 landed 03:53:27
   08-09; #64 landed 23:43:26 08-09, both per the fifth append, local −0700) → date range
   corrected to 2026-08-09→12.
8. **Recently-done nit B (same basis):** "#63/#67 agent-armed/merged under words"
   contradicts the fifth append's #67 classification (DEVIATION entry — an Amendment-1
   self-authorization comment 9 s pre-merge, not a human word) → split so #63 (conditional
   word, honored-as-conditioned) and #67 (session-side authority, deviation entry) each
   carry the append's own classification.

## Census-discipline note

Criterion 5 edits fifth-append text; that exact restoration is sanctioned by the #77
round-2 record (Residual-3). Criteria 6–8 edit the Recently-done line and its rollup
claims, not any census append. No other pre-existing census text is altered; the
append-only discipline otherwise stands.

## Merge clause

Amendment 1 + Addendum v2.3: this session authors, owns, and merges this PR itself —
required checks green at the exact tip; the forge-risk-priced review completed with every
finding dispositioned ON the PR; no excluded path; no new dependency. Census merge-record
comment posted on the PR at merge.

## Evidence plan

`scripts/check.sh` · `scripts/stage-content.sh` (sync proof) · `scripts/validate-content.sh` ·
`dotnet test dotnet/CatMetro.sln` (compiles the edited test file; runs the teachingGoal
length/distinctness pins) · CI at the exact tip · fresh-context review round(s) posted on
the PR. forge-risk classification captured BEFORE any dotnet run in this worktree (the
recorded lock-file caveat).

## STOP conditions

Any edit that would change test semantics or assertions, any threshold, another lane's
PROJECT_STATE row, census text beyond the criterion-5 restoration, or anything outside the
eight criteria. If a criterion turns out to require a semantic change: stop and surface.
