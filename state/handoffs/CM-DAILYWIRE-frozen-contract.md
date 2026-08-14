# CONTRACT CM-DAILYWIRE — wire the Daily Line into the playable funnel (position 6)

**Branch:** `task/CM-DAILYWIRE`
**Anchor:** `fac3e35` (main, 2026-08-13)
**Ground truth read before freezing:** `state/handoffs/DAILY-LINE-frozen-contract.md` (the
merged #73 engine contract — what it ships and explicitly defers, esp. A-DL-6); the merged
`unity/Assets/Scripts/Content/Daily/**` runtime API (`DailyPipeline`, `DailyRunRequest`,
`DailyLineSeed`, `DailyBoardFactory`, `DailyPipelineConfig`, `DailyLineSeedScheme`);
`docs/plan/specs/product_spec.md` §18 (Daily Line product shape) plus §8/§17; ADR-0010
(rewardless PGS leaderboards — confirms leaderboard work is a separate, later, unbuilt
integration); `unity/Assets/Scripts/Bootstrap/GameRoot.cs` (the funnel, `LevelBand`,
`WrapAtEndOfBand`, the dev-fenced Home/Intro/ScreenStack composition); the
`Presentation/Screens/**` sources (`HomeScreenView`, `HomeLayout`, `LevelIntroSheet`,
`ScreenStack`); `unity/Assets/Resources/Strings/ui.csv` + `UiCsvDisciplineTests`/
`UiCsvUx06Tests`; `unity/Assets/Scripts/Services/Save/ISave.cs` +
`unity/Assets/Scripts/Application/Save/**` (CM-C7's save API); `scripts/stage-content.sh`
(CM-C10's stager, the sole StreamingAssets author) and `docs/architecture/overview.md` §7.

**Authority:** the human's in-session 2026-08-13 directive to drive without per-item asks
(agent-relayed, H-1-class, recorded by the coordinator on the PR). Funnel position 6 is this
contract's; positions 8 (LEVEL-SELECT + BACK, still QUEUED/unopened on
`state/handoffs/PARALLEL-PUSH-2026-08-09.md`) and 5 (MONETIZATION-CODE) are untouched —
their surfaces (`Presentation/Input/**` beyond the existing chrome-region seam, ScreenStack
Back wiring, level-select browser, any Integrations/RevenueCat path) are not modified here.

## Contract restated (implementer's own words)

Give the merged-but-dormant Daily Line engine a way to reach a player: a Home entry point
that triggers it, GameRoot wiring that runs the real `DailyPipeline` for today's UTC date
and plays the admitted board through the same import seam campaign levels use, a session
marker so a Daily win never touches campaign progression, and a surfaced (not yet
persisted-and-enforced) ticket reward. Determinism: the wall clock is read exactly once, at
the Bootstrap boundary, and everything downstream of that single read stays pure/deterministic
(the same posture `Update()` already uses for `Time.deltaTime`).

## FLAGGED assumptions (proceeding per the "pick a reading, record it, keep going" directive)

- **FA-1 (Home-entry ownership).** `PARALLEL-PUSH-2026-08-09.md`'s Lane 6 row named "the Home
  chip/surface" as Lane 8's (LEVEL-SELECT + BACK), not Lane 6's — and Lane 8 is still QUEUED,
  gated on both art PRs merging, unopened. This contract's own task brief explicitly assigns
  "a Home entry point for the Daily Line (a chrome region + ui.csv rows, following the
  Play-CTA pattern)" to funnel position 6. Reading: that brief is a later, more specific
  human-relayed directive superseding the original lane brief for exactly this narrow slice
  (that file's own precedence rule: a later human directive in a lane's chat overrides it).
  I read "Home entry point" NARROWLY — one additional pin/chrome-region + its ui.csv label,
  structurally identical to the existing L001 pin — and explicitly do NOT build a level-select
  browser, a Back button, or any `Presentation/Input/**` seam beyond the one that already
  exists (`ChromeRegions`/`TapInput`). That leaves Lane 8's actual charter (browsing multiple
  levels, general Back navigation) completely untouched.
- **FA-2 (on-device config supply, the load-bearing one).** `DailyPipeline.Run` needs
  `SchemaBytes` (`docs/plan/data/level_schema.json`), a `ValidatorConfig`
  (`config/validator_thresholds.json`), and a `DailyPipelineConfig` (`config/daily_pipeline.json`
  → only `SaltMaxK` matters for a single-date run). None of these three files are staged into
  `unity/Assets/StreamingAssets/**` today, and `scripts/stage-content.sh`'s own header is
  explicit: "config/ holds non-shipping tool config (pins, validator thresholds, daily
  pipeline) — **none of that may ever ship**" — and `docs/architecture/overview.md` §7 lists
  only `content/{levels,daily_overrides.json,daily_backup_pool.json,catalog.json,
  content.sha256}` and `config/runtime_bounds.json` as the shipped StreamingAssets shape.
  Shipping those three files (via StreamingAssets, `Resources`, or any new path) would be an
  undeclared architecture change (constitution principle 5 requires an ADR first) — out of my
  authority. Resolution, following the DAILY-LINE contract's OWN precedent for exactly this
  situation (the weekday-difficulty table is hardcoded in `DailyDifficulty.cs` rather than
  shipping `config/daily_weekday_curve.json`): the three inputs are reproduced as **compiled
  C# constants** in a new Bootstrap-layer file (`DailyRuntimeInputs.cs`, NOT under
  `Content/Daily/**` — that root's charter is closed/merged; this is Bootstrap composition,
  the same layer that already owns `EngineStorageRoot`/`StreamingAssetsContentSource`).
  `validator_thresholds.json` is one line (`jitterSampleCount: 20`, the four Q-R rows
  deliberately absent) — reproduced via `ValidatorConfig`'s public constructor, no parsing
  needed. `SaltMaxK` (10) is reproduced as a literal, documented against the source row. The
  schema (7.4 KB) is reproduced as a verbatim string constant; a NEW EditMode test (Unity-only,
  not linked into the dotnet Pure mirror, so it may use `File.IO`) reads the real
  `docs/plan/data/level_schema.json` off disk and asserts byte-for-byte equality against the
  embedded copy, so a future schema edit fails this contract's test loudly instead of drifting
  silently. **This means "runs the REAL DailyPipeline" is true in the strongest sense — the
  actual `CorpusValidator`, the actual eleven `ValidationStages`, the actual solver all run —
  while the three source files themselves never enter the shipped asset tree.**
- **FA-3 (the product-spec's 30-board backup pool is NOT built here).** product_spec §18's
  own fallback design is two-layered: the pinned generator+validator runs on-device
  (`DailyPipeline`'s own `k=0..SaltMaxK` candidate loop + its code-template fallback — proven
  to admit a board on every one of the 90 dates in #73's shipped horizon proof, fallback usage
  = 0), and IF that fails, "the dated backup pool (30 hand-validated dailies shipped in
  StreamingAssets, indexed by date hash)" is the second line of defense. Building that pool is
  a separate, larger content-generation + stager-extension deliverable (30 authored/validated
  boards, a new StreamingAssets rule, date-hash indexing) — not "wiring," and not named in this
  contract's scope list. If the on-device pipeline ever fails to admit a board for today (not
  observed across the shipped 90-date proof), this wiring fails LOUDLY (a logged error; the
  Daily entry point declines to load anything and Home stays exactly as it was — never a
  silent blank board, never a crash) rather than silently substituting unvalidated content.
  Building the actual pool is recorded as follow-up.
- **FA-4 (L007 unlock gate and first-clear ticket enforcement share one root cause).**
  product_spec §18 says Daily unlocks "after L007 win," and A-DL-6 defers first-clear-vs-
  practice ticket enforcement to this contract, conditioned on "if the save layer supports it
  cheaply." Reading CM-C7's save API (`ISave`/`SaveStore`/`SaveDefaults` — the `daily` block
  already has `lastDateKey`/`streakDays`/`playedKeys` fields ready for exactly this) found the
  save layer **fully built but never instantiated anywhere in Bootstrap or GameRoot** — no
  `EngineStorageRoot`/`SaveStore` composition exists on the real device boot path today, for
  any level, campaign or Daily. Wiring persistence into GameRoot for the first time (load on
  boot, commit on pause/win, choose an `ISaveFileSystem`) is a genuine new integration surface,
  not a cheap read — it is NOT built by this contract. Both the L007-unlock gate and A-DL-6's
  first-clear enforcement are recorded as follow-up work sharing that one blocker. **[The
  next sentence is SUPERSEDED by the correction below — recorded, not silently deleted, per
  the "never route around a finding" rule.]** ~~The Daily entry point ships unconditionally
  visible on Home (not gated on campaign progress)~~; the ticket reward is SURFACED (see
  criterion 9) but not persisted or capped.

## Correction discovered mid-implementation (revises FA-4; recorded, not silently applied)

While implementing the Home pin, `unity/Assets/Tests/PlayMode/Screens/HomeScreenTests.cs`
(an EXISTING, previously-frozen CM-UX-06 test, untouched by this contract) was found to
assert `Home_SessionOneStructuralLaw_NoCommerceNodes_DecoyControlled`: a tree walk over
Home's GameObjects that fails if ANY node name contains `"daily"` (among other banned
commerce/monetization words), with the comment "S-01: 'No shop entry, no daily entry, no
badges rendered in session 1'" — plus `Pin_RegistersExactlyOneRegion_...` asserting Home
registers **exactly one** chrome region. An unconditional Daily pin breaks BOTH. This is not
an implementation accident to route around (e.g. by renaming nodes to dodge the string
scan) — it is CM-UX-06's own deliberate, tested product law, and it independently agrees
with product_spec §18's "Unlock: after L007 win": Daily must not appear on the (today, only)
Home surface until that gate is satisfied. FA-4 already found the save layer — the only
thing that could compute "has L007 been won" — fully built but never wired into Bootstrap
for any level. Revised resolution, honoring both the existing test and the spec's gate
exactly, per the "pick the reading most consistent with product_spec + proceed" instruction:

- `HomeScreenView.Create(Transform, bool dailyEntryUnlocked = false)` — an added OPTIONAL
  parameter. Every EXISTING caller/test (including all of `HomeScreenTests.cs`) uses the
  original one-argument form and is therefore completely unaffected: false means the Daily
  pin/label GameObjects are **never constructed** (the CM-UX-07 "zero screen objects
  constructed" precedent — not merely hidden), so the S-01 tree walk and the exactly-one-
  region count both keep passing exactly as CM-UX-06 pinned them.
- `GameRoot.DailyEntryUnlocked` (new `static bool`, default false, dev-fenced — mirrors
  `BootToHome`'s exact shape, needed because `ComposeDevScreenFlow` reads it synchronously at
  Wire-time) gates the one call site that matters: `ComposeDevScreenFlow`'s
  `HomeScreenView.Create` call. Shipped/default behavior is unchanged from before this
  contract touched anything: zero Daily surface anywhere, session 1 or otherwise.
- The REAL wiring underneath (SelectDaily, ResolveDailyBoard, the real DailyPipeline call,
  ReturnHomeFromDaily, the ticket surfacing) is unaffected by this correction — it is real,
  tested, functioning production code; only the Home pin's CONSTRUCTION is now conditional.
  This contract's own tests exercise it by setting `GameRoot.DailyEntryUnlocked = true`
  before booting (mirroring the `BootToHome` test-hygiene precedent), proving the feature
  works end to end without shipping it visible before its product-spec'd unlock condition can
  actually be computed.
- **Known debt, sharpened by this discovery (supersedes the earlier, softer FA-4 wording):**
  the L007-unlock gate is not a "nice to have deferred enhancement" — it is the ONLY thing
  standing between this contract's real, tested Daily pipeline and an actual player-visible
  Home entry. It blocks on the identical save-layer-not-wired root cause FA-4 already named.
  Whoever wires `ISave`/`SaveStore` into Bootstrap next should flip `DailyEntryUnlocked`
  (or replace it with a real progress-derived value) as close to a one-line follow-up as this
  codebase gets.

## Review fix round (PR #85, round 1) — every finding, named and closed

- **F1 (HIGH):** `DailyRuntimeInputsTests`'s `ValidatorConfig`/`PipelineConfig` "drift guards"
  never read the source files — they compared compiled constants to a second set of
  hand-typed literals. Rewritten to the schema test's own pattern: read the real
  `config/validator_thresholds.json`/`config/daily_pipeline.json`, parse through the public
  `ValidatorConfig.Parse`/`DailyPipelineConfig.Parse`, assert every field DailyRuntimeInputs
  actually claims to mirror against the PARSED value.
- **F2 (HIGH):** the win-path test's campaign fixture was `"L001"` (`LevelBand[0]`), which
  cannot discriminate the mutation it exists to catch (`NextLevelId`'s own out-of-band wrap
  rule coincidentally lands on `LevelBand[0]` too). Switched to `"L004"`; mutation RED proof
  recorded verbatim below. The region-count assertion also tightened from `Is.LessThan` to
  the exact `regionBaseline - 2`.
- **F3 (MEDIUM):** `ReturnHomeFromDaily` now defers `Home.Show()`/`Stack.Push` by a one-frame
  input lockout (`Update()`-driven, `_pendingHomeShowFrame`) so a repeat tap at the results
  CTA's coordinates — which the L001/Daily pins occupy once shown — cannot land on them,
  whether the repeat tap is same-frame or one yield later. Found and closed a related latent
  bug while implementing this: `LoadLevel()` now cancels any stale pending show on every level
  load, so a rapid return-home-then-reselect-Daily sequence can't leave an old lockout armed
  to fire mid-way through a NEW Daily session.
- **F4 (MEDIUM):** `SelectDaily()` now no-ops if `_dailySession` is already true, preventing a
  second call from corrupting `_preDailyLevel` with the Daily board itself.
- **F5 (MEDIUM):** a new forcing-function test (`WeekdayCurveBytes_IfTheFileExists_
  MustBeEmbedded`) proves the fourth `DailyRunRequest` input (the weekday curve) is honestly
  absent today and will loudly fail the day `config/daily_weekday_curve.json` exists, naming
  the fix. Known-debt entry added below.
- **F6:** two new Known-debt entries — (a) current-date admissibility is proven only for the
  two pinned test dates, not exhaustively; the failure mode stays bounded and loud regardless.
  (b) a lost (failed) Daily session has no wired exit except Retry — no Home escape from
  `FailureReview` for a daily session; follow-up candidate named.
- **F7:** FA-4's now-superseded "ships unconditionally visible on Home" sentence marked
  `[SUPERSEDED by the correction below]` and struck through rather than deleted.
- **F9:** the "sanctioned mechanism" comment this contract cited was misattributed to
  `UiCsvUx06Tests.cs` — it actually lives in `UiCsvDisciplineTests.cs`. Corrected here and in
  the evidence file.

Full re-verification after this round: `scripts/check.sh` OK; `dotnet test` unaffected (no
`dotnet/**` files touched by this round); Unity EditMode/PlayMode filtered + full re-runs
green (exact counts in the evidence file, updated for this round).

## Design

### Clock boundary
`GameRoot` gains `public System.Func<long> DailyClockUnixSeconds = () =>
System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();` — the one ambient-clock read, injectable for
tests (mirrors the existing `MotionOffToggle`/`AnimatorDurationScale` injection style). The
Unix-seconds value converts to a `yyyy-MM-dd` date key via the ENGINE'S OWN
`DailyLineSeed.DateKeyFromUnixSeconds` (never a second, competing date-math implementation) —
downstream of that one call, seed derivation/generation/validation stay exactly as pinned by
#73, byte-for-byte reusable in tests.

### GameRoot: `ResolveDailyBoard()` / `SelectDaily()`
Builds one `DailyRunRequest` (embedded schema/validator/pipeline inputs from
`DailyRuntimeInputs`, `dateKeys = [today]`, `factory = new DailyBoardFactory()`,
`seedScheme = DailyLineSeedScheme.Instance`, default `MaxNodesExpanded` — unchanged from the
engine's own default, no weakening of the admission bar), runs `DailyPipeline.Run`, and on a
non-blocking admitted record serializes+reimports the board through
`LevelImporter.Import(Encoding.UTF8.GetBytes(record.BoardJson))` — **the identical import
function `InitializeFromSeam`/`LoadNext` already use for campaign levels** ("the same seam"
literally, not by analogy). On any failure (request-level or a blocked date record) nothing
loads; a loud `Debug.LogError` fires and Home stays exactly as it was.

### Session marking
`_dailySession` (private bool) is set true immediately after `SelectDaily()`'s successful
`LoadLevel(dailyBoard)`, and explicitly cleared (`false`) inside `LoadNext()` (campaign-only)
and when returning Home. `ResultsPanel.NextRequested` now binds to a small router
(`OnResultsCtaRequested`) instead of `LoadNext` directly: Daily → `ReturnHomeFromDaily()`
(reloads the level that was active before Daily was selected — so nothing behind Home is a
stale `Won` panel; sets `_dailySession = false`; re-shows Home), campaign → `LoadNext()`
unchanged. `LevelBand`/`WrapAtEndOfBand`/`NextLevelId` are untouched — a Daily win never calls
`LoadNext`, so campaign progression is structurally unreachable from a Daily session, not just
behaviorally avoided.

### Ticket reward surfacing (A-DL-6)
`GameRoot.DailyTicketsEarned` (`int?`, public, read-only) is set to
`_level.Dto.Economy.BaseTickets` the moment a Daily session's `Update()` transitions to `Won`,
and cleared to `null` at the top of every `LoadLevel()` call. No new pixel UI is added: **no
level, campaign or Daily, currently renders a ticket amount anywhere** (grepped — zero hits),
so building ticket-reward UI is a separate UX deliverable, not part of wiring a value through.
This still satisfies "surfaced" in the literal sense the contract asks for — a public, tested,
non-DTO-cold-storage property — without touching `ResultsPanel`'s two protected monetization
invariants (exactly one CTA region, structurally-empty footer).

### Results CTA text
`ResultsPanel` gets one new method, `SetCtaTextKey(string key)`, purely a `Strings.UiStrings.Get`
lookup + text assignment — it changes no existing behavior (`EnsureViews()`'s default text is
untouched, so the LOCKED `results.next`/"Next" pin for campaign wins is unaffected). GameRoot
calls it with a new key (`results.daily.done`, "Home") when a Daily session starts and restores
`results.next` when returning Home.

### Home entry
`HomeScreenView` gets a second registered pin (`home.pin.daily`), positioned via new pure math
in `HomeLayout.DailyPinRect` (56dp square, to the right of the existing L001 pin inside the
same thumb band, clearing the 48dp floor by construction like the existing pin) — no live
`Screen` reads inside the math itself (A-UX1-5 law preserved). Labeled via a new ui.csv key
(`home.daily.label`). `Home.DailySelected` (a new public `Action`, mirroring
`Home.LevelSelected`) is wired in `GameRoot.ComposeDevScreenFlow` to `SelectDaily()`. Same
dev-fence as the rest of the screen flow (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`,
`BootToHome`) — Daily is reachable exactly where campaign level-select already is today
(nowhere in shipped boot yet; that is pre-existing architecture, not something this contract
changes).

## Acceptance criteria

1. **Home carries a Daily entry, gated by `DailyEntryUnlocked` (see the mid-implementation
   correction above).** When unlocked, a second chrome region (`home.pin.daily`) registers
   when Home shows and unregisters when it hides/destroys, following the exact `RegisterPin`/
   `UnregisterPin`/`OnDisable`/`OnEnable` lifetime law the L001 pin already obeys, and its
   label resolves through a new ui.csv key, never a literal. When NOT unlocked (the shipped
   default), zero Daily objects are constructed and `HomeScreenTests.cs`'s existing S-01 tree
   walk / exactly-one-region assertions pass completely untouched.
2. **Selecting Daily runs the real pipeline for today.** `SelectDaily()` derives today's UTC
   date key from exactly one clock read, builds a `DailyRunRequest` with the embedded
   schema/validator/pipeline inputs, and calls the real `DailyPipeline.Run` — proven by a fixed
   test date whose independently-known seed/board is asserted, not mocked.
3. **The admitted board loads through the campaign seam.** The resolved board reaches
   `Session`/`View` via `LevelImporter.Import`, the identical function `LoadNext`/
   `InitializeFromSeam` call — proven by a shared-code-path test, not a parallel loader.
4. **Determinism.** Two resolutions for the same injected clock value produce byte-identical
   admitted board JSON; two different injected dates (that both resolve within the horizon)
   produce different ids/content — mirroring #73's own determinism criteria at the wiring
   layer.
5. **Daily session never advances the campaign band.** A Daily win's `ResultsPanel` CTA does
   not call `LoadNext`; `_level`/`Session` after a Daily win-then-return-home cycle is the SAME
   campaign level (by id) that was active immediately before Daily was selected —
   `GameRoot.LevelBand`/`NextLevelId`/`WrapAtEndOfBand` are provably never invoked on that path
   (delete-the-router-branch mutation proof turns this red).
6. **Daily win returns Home, not the next level.** `ReturnHomeFromDaily()` re-shows Home and
   the `ScreenStack` breadcrumb returns to `["home"]`; the CTA that triggers it reads a
   DIFFERENT ui.csv label from the campaign "Next" CTA (regression pin on the existing LOCKED
   `results.next`="Next" test, untouched).
7. **Board-admission failure is loud, never silent.** An injected factory that cannot admit any
   candidate (mirroring #73's own bounded-rejection test shape) leaves Home untouched and logs
   an error — no partial/garbage level ever reaches `Session`.
8. **The clock enters exactly once.** `DailyClockUnixSeconds` is the sole read; everything
   downstream (`DailyLineSeed`, `DailyBoardFactory`, `CorpusValidator`, the solver) stays the
   same pure functions #73 already tests — proven by injecting a fixed clock value and getting
   the exact vectors #73's own frozen contract pins for that date.
9. **Ticket reward surfaced.** `DailyTicketsEarned` is null before a Daily win, equals the
   admitted board's `Economy.BaseTickets` immediately after, and returns to null on the next
   `LoadLevel`. No first-clear/persistence claim is made or tested (FA-4).
10. **Schema/validator/pipeline inputs never ship as loose files.** `git diff` against this
    contract's tree shows no new file under `unity/Assets/StreamingAssets/**`, no new rule in
    `scripts/stage-content.sh`, and the drift-guard test (FA-2) passes, proving the embedded
    schema copy matches the real source file byte-for-byte.
11. **Gates and mutation evidence.** Focused RED→GREEN cycles for criteria 2, 5, 7, 10, then
    `scripts/check.sh`, `dotnet test dotnet/CatMetro.sln`, and the Unity EditMode+PlayMode legs
    via `tests/unity/editmode.test.sh` all pass from the final tree.

## Files in scope

- `unity/Assets/Scripts/Bootstrap/GameRoot.cs` (the funnel edit itself)
- `unity/Assets/Scripts/Bootstrap/DailyRuntimeInputs.cs` (new — embedded schema/validator/
  pipeline-config constants, Bootstrap layer, never `Content/Daily/**`)
- `unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs`,
  `unity/Assets/Scripts/Presentation/Screens/HomeLayout.cs` (the second pin)
- `unity/Assets/Scripts/Presentation/Hud/ResultsPanel.cs` (the `SetCtaTextKey` method only)
- `unity/Assets/Resources/Strings/ui.csv` (append-only: `home.daily.label`,
  `results.daily.done`)
- New tests under `unity/Assets/Tests/EditMode/**` and `unity/Assets/Tests/PlayMode/**`
  (mirroring `LoadNextBandTests.cs`/`DevScreenFlowTests.cs`/`LoadNextTests.cs` patterns), plus
  one new `UiCsvDailyWireTests.cs` mirroring `UiCsvUx06Tests.cs`'s append-declaration pattern.
  **Correction (discovered via the first full EditMode run):** the existing
  `UiCsvDisciplineTests.cs`/`UiCsvUx06Tests.cs` DO need a one-line edit each — both pin an
  EXACT total row count (`NewRows_ExactlyTheSevenPinned_Appended` /
  `ThisSlice_AppendsExactlyThreeRows_BytePinned`), and **`UiCsvDisciplineTests.cs`'s** own
  comment (`NewRows_ExactlyTheSevenPinned_Appended`'s R1-L6 note — F9, review fix round:
  corrected attribution, this sentence previously named the wrong file) names the sanctioned
  mechanism verbatim: "amended only by declared contract evolution (raise the count + pin
  your own rows...)". Only the numeric bound and its comment change (12→14 in each file);
  every existing byte-pinned row assertion (rows 0-11) is untouched.
- this frozen contract; the one `state/PROJECT_STATE.md` row at merge

Out of scope, refused here (per the coordinator's brief): leaderboards/PGS (ADR-0010's own
unbuilt integration), any monetization surface, funnel positions 8/5 and their files
(level-select browser, Back wiring, `Integrations/RevenueCat/**`), Scene/ProjectSettings
edits, `content/levels/**` changes, the solver, `docs/plan/**`. Also out of scope, per FA-3/
FA-4 above: the 30-board dated backup pool, save-layer wiring into Bootstrap, L007-unlock
gating, first-clear ticket persistence/enforcement — each recorded as named follow-up, not
silently dropped.

## Stop conditions

Stop and ask before proceeding if: (1) the embedded schema/validator/config reproduction
cannot keep `DailyPipeline.Run`'s real admission semantics byte-identical to #73's own tests;
(2) Home's second pin cannot be added without touching `Presentation/Input/**` beyond the
existing `ChromeRegions`/`TapInput` seam; (3) returning Home from a Daily win requires editing
`LevelBand`/`WrapAtEndOfBand`/`NextLevelId`; (4) any requirement reaches
`Integrations/RevenueCat/**`, a level-select browser, or `docs/plan/**`.

## Known debt / follow-ups recorded here (not fixed by this contract)

- **[#85 round-2 D-1 — and a completeness correction owned by the coordination session: the
  fix round's "all nine findings applied" claim was 8-for-9; the reviewer's F6 was silently
  dropped in the coordinator's renumbered dispatch and is dispositioned HERE, 9th]** The
  CM-UX-05 hint-attempt run intentionally SURVIVES Daily transitions in this contract:
  `Hints.ResetForNewLevel()` is called only in `LoadNext()`; `SelectDaily`→`LoadLevel` and
  `ReturnHomeFromDaily`→`LoadLevel` change the level id without resetting, so a Daily failure
  counts toward the restored campaign level's hint threshold (and vice versa). Recorded as
  intentional-for-now rather than silently fixed at the review cap; the follow-up (reset on
  Daily-boundary transitions + a test pinning the boundary semantics) rides the SAVE-WIRE /
  Daily-unlock contract, which owns the session-boundary semantics anyway.
- **[#85 round-2 D-2]** The F3 input lockout closes the SAME-FRAME and one-yield double-tap
  windows only (frame-count gate, Home shows at F+2). The >1-frame human-cadence double-tap
  (~6 frames) REMAINS OPEN and is inherited by the pre-existing CM-LOADNEXT tap-collision
  debt (`ChromeRegions.cs:24-29`); the standard close is a time-based or first-tap-consuming
  lockout. Dev-fenced-only in every branch of the residual; two benign dev-only consequences
  during the lockout window recorded by the reviewer (bare board visible ~2 frames; a stray
  tap in the window routes to the board as a miss).

- **Highest priority, sharpened by the mid-implementation correction above:** `GameRoot.
  DailyEntryUnlocked` is a dev/test-only static seam, not a real progression gate. Wiring
  `ISave`/`SaveStore` into Bootstrap (unwired for ANY level today, campaign or Daily — CM-C7's
  save layer is fully built and fully unused at runtime) is the ONLY remaining step between
  this contract's real, tested Daily pipeline and an actual player-visible Home entry —
  replace the static flag with a real "has L007 been won" read once that layer exists. This
  single gap now blocks THREE things at once: the L007-unlock visibility gate itself,
  first-clear-vs-practice ticket enforcement (A-DL-6), and the actual player-facing launch of
  everything this contract built.
- The 30-board dated backup pool (product_spec §18's second fallback layer) is unbuilt (FA-3).
- No level anywhere shows a ticket-reward amount in pixels yet; `DailyTicketsEarned` is a
  tested data seam, not UI.
- On-device wall-clock timing for a full `k=0..SaltMaxK` candidate loop (up to 11 solves) plus
  a possible fallback solve is unmeasured on real hardware in this contract — the templates are
  small (1-2 switches) so this is expected to be fast, but it is asserted, not measured, here.
- **F5 (review fix round): the weekday-curve truth-fork.** `DailyRuntimeInputs` supplies
  exactly three embedded inputs; `GameRoot.ResolveDailyBoard` passes the fourth
  `DailyRunRequest` field, `weekdayCurveBytes`, as a literal `null` (#73's own NEW-Q21
  "absent file" behavior). `config/daily_weekday_curve.json` cannot exist today (the daily
  wrapper's own criterion-9 gate forbids it), so this is currently correct by construction —
  but the day NEW-Q21 answers and the file lands, this wiring silently keeps passing `null`
  past a now-real curve unless someone updates it. `DailyRuntimeInputsTests.
  WeekdayCurveBytes_IfTheFileExists_MustBeEmbedded` is the forcing function: it passes
  trivially while the file is absent and fails loudly the day it exists, naming the exact fix
  (a fourth embedded `WeekdayCurveBytes` input mirroring the other three, plus a
  `GameRoot.ResolveDailyBoard` edit to stop passing `null`).
- **F6(a) (review fix round): current-date admissibility is unproven before 2026-08-24.** Every
  test in this contract pins one of two specific dates (2026-08-24, seed `1449106418`;
  2026-08-10, seed `252386339`) via the injectable `DailyClockUnixSeconds` seam — #73's own
  90-date horizon proof covers a much wider range, but THIS wiring layer has only exercised
  those two dates end-to-end through the embedded-inputs path. The production default
  (`DateTimeOffset.UtcNow`) will resolve whatever the device's real calendar date is, which
  could fall outside anything directly proven here. The failure mode is bounded and loud by
  design (criterion 7 — `ResolveDailyBoard` returns `null` and logs an error on any admission
  failure, never a silent or partial board), so this is an accepted, recorded risk rather than
  a blocking defect, but it is not the same claim as "every reachable date is proven."
- **F6(b) (review fix round): a lost Daily has no exit except winning.** `ReturnHomeFromDaily`
  is reachable ONLY from `ResultsPanel.NextRequested`, which only fires on a real `Won`
  transition. A Daily session that reaches `FailureReview` has exactly the same recourse every
  campaign level has — the Retry verb (`Input.RetryRegionActive`/`RetryTapped`), which
  replays the SAME Daily board from tick 0 — and no wired way back to Home at all. For a
  campaign level this is arguably fine (progression doesn't need a Home escape mid-band); for
  a Daily session, which is entered FROM Home as a discrete side mode, a player who keeps
  losing is stuck retry-looping the same board with no exit. Follow-up candidate: a Home verb
  on the fail chrome, gated to daily sessions only (`_dailySession`), reusing
  `ReturnHomeFromDaily`'s own restore-and-show machinery (including its F3 one-frame lockout,
  which is chrome-region-and-frame-generic, not Won-specific). Not built here — out of this
  contract's declared scope (fail-chrome edits were never in the Files-in-scope list) and
  discovered only during the review fix round, so it is recorded rather than silently added.
