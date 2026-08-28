using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Application.Save;
using CatMetro.Content;
using CatMetro.Content.Daily;
using CatMetro.Presentation.Hud;
using CatMetro.Services;

namespace CatMetro.Tests.PlayMode
{
    // CM-DAILYWIRE: funnel position 6, end-to-end. Home's Daily pin -> the admitted precomputed
    // catalog when available, otherwise the real deterministic DailyPipeline fallback -> the
    // board enters the same imported-level seam as campaign play -> a Daily win never reaches
    // LevelBand/NextLevelId/WrapAtEndOfBand and returns Home. Every test drives real composition
    // through GameRoot.Wire/SelectDaily (the CM-UX-01/02/04/05/07/LOADNEXT live-wiring +
    // anti-vacuity rule) — no test hand-sets a delegate GameRoot binds itself.
    public sealed class DailyWireTests
    {
        private GameRoot _root;

        // #73's own pinned vectors (DAILY-LINE-frozen-contract.md) — reused here, never a
        // second source of truth for "what the correct seed is." (dateKey 2026-08-24 and
        // 2026-08-10 themselves are never re-typed here — only their Unix-second instants and
        // expected seeds, so a transcription slip in the date string cannot silently pass.)
        private const uint PinnedSeed = 1449106418u;
        private const long PinnedUnixSeconds = 1787572800L; // 2026-08-24T12:00:00Z
        private const uint OtherSeed = 252386339u;
        private const long OtherUnixSeconds = 1786363200L; // 2026-08-10T12:00:00Z

        // CM-BOOT-HOME: the boot-helper re-seam's temp dir. Home now composes on the REAL boot
        // path (InitializeFromSeam), which LaunchWith bypasses entirely — so this suite injects
        // its campaign fixture through DevLevelOverride's file seam and boots via Launch(),
        // exactly the DevBootOverrideTests/DevLevelOverrideTests SceneBoot pattern. Assertion
        // bodies are untouched: only HOW the fixture reaches GameRoot changed.
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            GameRoot.BootToHome = false;
            _tmpDir = Path.Combine(Path.GetTempPath(),
                "cm-dailywire-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpDir);
            DevLevelOverride.DirectoryOverride = _tmpDir;
            GameRoot.DailyStorageRootOverride = () =>
                new TestStorageRoot(Path.Combine(_tmpDir, "save"));
            // A fresh save is below the shipped configurable threshold. This suite explicitly
            // opts into the dev/test-only seam to exercise the
            // REAL wiring end-to-end; every test resets it in TearDown so it never bleeds into
            // an unrelated fixture (the BootToHome hygiene precedent).
            GameRoot.DailyEntryUnlocked = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.BootToHome = false;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.DailyStorageRootOverride = null;
            DevLevelOverride.DirectoryOverride = null; // CM-BOOT-HOME re-seam hygiene
            if (!string.IsNullOrEmpty(_tmpDir) && Directory.Exists(_tmpDir))
                Directory.Delete(_tmpDir, true);
            _tmpDir = null;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = 1f;
        }

        private sealed class TestStorageRoot : IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public TestStorageRoot(string path)
            {
                SaveDirectory = path;
                Directory.CreateDirectory(path);
            }
        }

        // CM-BOOT-HOME: writes the campaign fixture where DevLevelOverride reads it, then boots
        // the REAL seam — InitializeFromSeam picks the fixture up and composes Home over it.
        private GameRoot LaunchWithCampaignFixture(string campaignId = "L001")
        {
            File.WriteAllText(Path.Combine(_tmpDir, "level.json"), CampaignFixtureJson(campaignId));
            return GameRoot.Launch();
        }

        // CM-BOOT-HOME: the old in-memory Import() helper is retired — the fixture now reaches
        // GameRoot as BYTES through DevLevelOverride's real file seam (which runs the identical
        // LevelImporter.Import internally and logs DEVCAP_LEVEL_OVERRIDE_INVALID on a bad
        // payload), so a separate parse-and-assert helper would be a second source of truth.

        // F2 (review fix round): optional campaignId, default "L001" for the tests that don't
        // care which campaign level is active. The win-path test below passes "L004"
        // specifically — see its own comment for why L001 (== LevelBand[0]) cannot
        // discriminate the mutation it exists to catch.
        private IEnumerator BootToHomeWithCampaignFixture(string campaignId = "L001")
        {
            _root = LaunchWithCampaignFixture(campaignId);
            yield return null;
            Assert.That(_root.Home, Is.Not.Null, "precondition: the screen flow composed");
            Assert.That(_root.Home.IsVisible, Is.True, "precondition: Home shown on boot");
        }

        private static IEnumerator WaitForDailySession(GameRoot root, float timeoutSeconds = 90f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!root.IsDailySession && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(root.IsDailySession, Is.True,
                "Daily fallback did not finish before the real-time deadline");
        }

        // --- a fresh save below the configured threshold builds zero Daily objects ---

        [UnityTest]
        public IEnumerator FreshSave_BelowConfiguredThreshold_BuildsZeroDailyObjects()
        {
            GameRoot.DailyEntryUnlocked = false; // exercise save/config rather than force-on
            _root = LaunchWithCampaignFixture("L001");
            yield return null;

            Assert.That(_root.Home, Is.Not.Null, "precondition: the screen flow composed");
            Assert.That(_root.Home.DailyPinTransform, Is.Null,
                "zero Daily objects constructed — not merely hidden (the CM-UX-07 shipped-boot "
                + "precedent) — S-01 and product_spec §18 (\"after L007 win\") both forbid it "
                + "here");
            Assert.That(_root.Home.DailyLabelText, Is.EqualTo(""),
                "no label component exists to read");
            Assert.That(_root.DailyUnlockAfterCampaignCompletions, Is.EqualTo(7),
                "the shipped config defaults to seven unique campaign clears");

            // positive control: the SAME boot with the flag on DOES build it (the pair proves
            // the guard is real, not a tautology).
            Object.Destroy(_root.gameObject);
            yield return null;
            GameRoot.DailyEntryUnlocked = true;
            _root = LaunchWithCampaignFixture("L001");
            yield return null;
            Assert.That(_root.Home.DailyPinTransform, Is.Not.Null,
                "control: the flag genuinely gates construction");
        }

        [UnityTest]
        public IEnumerator SevenSavedCampaignClears_UnlockDailyWithoutTheDevFlag()
        {
            GameRoot.DailyEntryUnlocked = false;
            var storage = new TestStorageRoot(Path.Combine(_tmpDir, "save"));
            var boundsBytes = File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "config", "runtime_bounds.json"));
            var bounds = RuntimeBounds.Parse(boundsBytes);
            Assert.That(bounds.Ok, Is.True, bounds.Error?.ToString());
            var store = new SaveStore(storage, new RealSaveFileSystem(),
                bounds.Value, new MigrationTable());
            store.Load();
            var progress = new DailyProgressTracker(store);
            for (int i = 1; i <= 7; i++)
                progress.RecordCampaignCompletion("L" + i.ToString("000"));

            _root = LaunchWithCampaignFixture("L008");
            yield return null;

            Assert.That(_root.Home.DailyPinTransform, Is.Not.Null,
                "the real saved-progress/config path constructs the shipped Daily entry");
            Assert.That(_root.DailyUnlockAfterCampaignCompletions, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator SeventhCampaignClear_UnlocksDailyInTheSameRun_AndReturnsHome()
        {
            GameRoot.DailyEntryUnlocked = false;
            var storage = new TestStorageRoot(Path.Combine(_tmpDir, "save"));
            var boundsBytes = File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "config", "runtime_bounds.json"));
            var bounds = RuntimeBounds.Parse(boundsBytes);
            Assert.That(bounds.Ok, Is.True, bounds.Error?.ToString());
            var store = new SaveStore(storage, new RealSaveFileSystem(),
                bounds.Value, new MigrationTable());
            store.Load();
            var progress = new DailyProgressTracker(store);
            for (int i = 1; i <= 6; i++)
                progress.RecordCampaignCompletion("L" + i.ToString("000"));

            File.Copy(Path.Combine(UnityEngine.Application.streamingAssetsPath,
                    GameRoot.LevelPath("L007")),
                Path.Combine(_tmpDir, "level.json"), overwrite: true);
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Home.DailyPinTransform, Is.Null,
                "six unique clears are still below the shipped threshold");
            Assert.That(_root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center),
                Is.EqualTo(-3));
            Assert.That(_root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center),
                Is.EqualTo(-3));
            Assert.That(_root.ScreensVisible, Is.False, "campaign play has started");

            var level = _root.Session.Level;
            var solve = CatMetro.Domain.Solver.LevelSolver.Solve(level.Graph, (ulong)level.Dto.Seed);
            Assert.That(solve.Verdict, Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.Solved),
                "the real shipped L007 supplies the campaign-win witness");
            foreach (var e in solve.OptimalLog.Entries)
            {
                while (_root.Session.State.Tick < e.Tick
                    && _root.Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running)
                    _root.Session.AdvanceMs(
                        CatMetro.Application.Session.TickInterpolator.TICK_MS);
                _root.Session.EnqueueToggle(e.SwitchId);
            }
            while (_root.Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running
                && _root.Session.State.Tick <= level.Dto.Win.TimeLimitTicks)
                _root.Session.AdvanceMs(CatMetro.Application.Session.TickInterpolator.TICK_MS);
            Assert.That(_root.Session.State.Outcome.Kind,
                Is.EqualTo(CatMetro.Domain.OutcomeKind.Won), "solver witness wins L007 for real");

            yield return null; // GameRoot observes the win and persists clear seven.
            yield return null; // ResultsPanel observes ScreenState == Won.
            Assert.That(_root.Home.DailyPinTransform, Is.Not.Null,
                "the threshold transition constructs Daily without a cold relaunch");
            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.CtaText, Is.EqualTo("Home"),
                "the threshold-crossing result routes to the newly unlocked Home surface");

            panel.NextRequested?.Invoke();
            yield return null;
            yield return null;
            yield return null;
            Assert.That(_root.Home.IsVisible, Is.True,
                "the player can reach the unlocked entry in the same running app");
            Assert.That(_root.Home.DailyPinTransform, Is.Not.Null);
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L008"),
                "the next campaign level is prepared behind Home");

            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            Assert.That(_root.Input.HandleTapAtScreen(
                _root.Home.DailyPinPaintedRectPx.center), Is.EqualTo(-3));
            Assert.That(_root.IsDailySession, Is.True,
                "the newly constructed region is live, not only visible");
        }

        // --- criterion 1: Home carries a Daily entry, same lifetime law as the L001 pin ---

        [UnityTest]
        public IEnumerator HomeCarriesADailyPin_RegisteredWhileShown_UnregisteredOnceSelected()
        {
            yield return BootToHomeWithCampaignFixture();

            Assert.That(_root.Home.DailyLabelText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("home.daily.label")),
                "the label resolves through the csv key, never a literal");
            int regionBaseline = _root.Input.Regions.Count;

            // The tap IS the trigger (HomeScreenView.DailySelected -> GameRoot.SelectDaily) —
            // no test hand-invokes SelectDaily here, so this proves the real registered region
            // routes to the real production handler, not merely that the handler works in
            // isolation.
            int tapResult = _root.Input.HandleTapAtScreen(_root.Home.DailyPinPaintedRectPx.center);
            Assert.That(tapResult, Is.EqualTo(-3), "the Daily pin is a registered chrome region");
            Assert.That(_root.Home.IsVisible, Is.False, "Home hides once Daily is selected");
            Assert.That(_root.IsDailySession, Is.True,
                "the real tap reached SelectDaily(), not a no-op");
            // F2 (review fix round): EXACTLY two regions gone (the L001 pin + the Daily pin),
            // not merely "fewer than before" — Is.LessThan would also pass if only one
            // unregistered (a real ghost-region bug) or if some unrelated third region
            // vanished too, proving nothing precise.
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(regionBaseline - 2),
                "both Home pins (L001 + Daily), and only those two, unregister once Home "
                + "hides — a ghost region would leave this count higher");
        }

        // --- criteria 2/3/8: the precomputed artifact wins for an in-horizon injected date,
        // loads through the gameplay seam, and the clock enters exactly once ---

        [UnityTest]
        public IEnumerator TapDailyPin_UsesPrecomputedBoard_ForInjectedDate_LoadsThroughTheSeam()
        {
            yield return BootToHomeWithCampaignFixture();
            int clockReads = 0;
            _root.DailyClockUnixSeconds = () =>
            {
                clockReads++;
                return PinnedUnixSeconds;
            };

            LogAssert.Expect(LogType.Log, new Regex(@"SEAM_LOADED daily:2026-08-24"));
            int tapResult = _root.Input.HandleTapAtScreen(_root.Home.DailyPinPaintedRectPx.center);
            Assert.That(tapResult, Is.EqualTo(-3), "the Daily pin is a registered chrome region");

            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L800"),
                "the shipped Daily runtime id (DAILY-LINE A-DL-3)");
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(PinnedSeed),
                "#73's pinned vector proves the catalog serves the deterministic board");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.ActiveDailyDateKey, Is.EqualTo("2026-08-24"));
            Assert.That(clockReads, Is.EqualTo(1),
                "selection captures today's UTC date once; midnight cannot split one run");
            Assert.That(_root.DailyRunIsPractice, Is.False);
            Assert.That(_root.LastDailyBoardSource, Is.EqualTo("precomputed"),
                "an in-horizon date must use the shipped, prevalidated board before regeneration");
            CollectionAssert.AreEqual(new string[0], _root.Stack.ToBreadcrumb(),
                "no Intro sheet for Daily — straight into gameplay");
        }

        private sealed class ThrowingFactory : IBoardFactory
        {
            public LevelDto Build(uint seed, string dateKey, int k) =>
                throw new System.InvalidOperationException("runtime regeneration should not run");
        }

        private sealed class DelayedFactory : IBoardFactory
        {
            private readonly DailyBoardFactory _inner = new DailyBoardFactory();
            private int _first = 1;

            public LevelDto Build(uint seed, string dateKey, int k)
            {
                if (System.Threading.Interlocked.Exchange(ref _first, 0) == 1)
                    System.Threading.Thread.Sleep(750);
                return _inner.Build(seed, dateKey, k);
            }
        }

        private sealed class CountingFactory : IBoardFactory
        {
            private readonly DailyBoardFactory _inner = new DailyBoardFactory();
            private int _buildCalls;

            public int BuildCalls => System.Threading.Volatile.Read(ref _buildCalls);

            public LevelDto Build(uint seed, string dateKey, int k)
            {
                System.Threading.Interlocked.Increment(ref _buildCalls);
                return _inner.Build(seed, dateKey, k);
            }
        }

        private sealed class DelayedThrowingFactory : IBoardFactory
        {
            public LevelDto Build(uint seed, string dateKey, int k)
            {
                System.Threading.Thread.Sleep(250);
                throw new System.InvalidOperationException("cancelled fallback sentinel");
            }
        }

        [UnityTest]
        public IEnumerator InHorizonCatalog_RemainsPlayableWhenRuntimeGeneratorThrows()
        {
            yield return BootToHomeWithCampaignFixture();
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.DailyFactory = () => new ThrowingFactory();

            Assert.DoesNotThrow(() => _root.SelectDaily());

            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.LastDailyBoardSource, Is.EqualTo("precomputed"));
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(PinnedSeed));
        }

        [UnityTest]
        public IEnumerator OutsideCatalogFallback_ReturnsTheFrame_ShowsLoading_ThenLoads()
        {
            yield return BootToHomeWithCampaignFixture();
            _root.DailyClockUnixSeconds = () => OtherUnixSeconds;
            _root.DailyFactory = () => new DelayedFactory();

            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            _root.SelectDaily();
            elapsed.Stop();

            Assert.That(elapsed.ElapsedMilliseconds, Is.LessThan(250),
                "fallback generation must not run the solver on the Home tap frame");
            Assert.That(_root.IsDailySession, Is.False,
                "the generated board is not installed before background work completes");
            Assert.That(_root.Home.IsVisible, Is.True,
                "Home remains visible while the fallback is being prepared");
            Assert.That(_root.Home.DailyStatusText, Is.EqualTo("Preparing today's Line…"),
                "the returned frame explains that deterministic fallback work is pending");

            float deadline = Time.realtimeSinceStartup + 90f;
            while (!_root.IsDailySession && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(_root.IsDailySession, Is.True,
                "the admitted fallback is installed on a later main-thread frame");
            Assert.That(_root.LastDailyBoardSource, Is.EqualTo("generated"));
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(OtherSeed));
            Assert.That(_root.Home.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator NavigatingToCampaign_CancelsPendingFallback_AndStaleWorkCannotInstall()
        {
            yield return BootToHomeWithCampaignFixture();
            _root.DailyClockUnixSeconds = () => OtherUnixSeconds;
            _root.DailyFactory = () => new DelayedThrowingFactory();

            _root.SelectDaily();
            Assert.That(_root.Home.DailyStatusText, Is.EqualTo("Preparing today's Line…"));
            Assert.That(_root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center),
                Is.EqualTo(-3), "campaign navigation consumes the tap while fallback is pending");
            Assert.That(_root.Intro.IsVisible, Is.True);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline) yield return null;

            Assert.That(_root.IsDailySession, Is.False,
                "a detached worker cannot install over the newer campaign navigation state");
            Assert.That(_root.Intro.IsVisible, Is.True);
            Assert.That(_root.Home.DailyStatusText, Is.Empty,
                "logical cancellation clears the transient loading state");
        }

        // --- F4 (review fix round): a second SelectDaily() call while already in a Daily
        // session must be a no-op and must not corrupt the pre-daily restore target ---

        [UnityTest]
        public IEnumerator SelectDaily_CalledTwiceInARow_IsANoOp_PreDailyRestoreTargetSurvives()
        {
            yield return BootToHomeWithCampaignFixture("L004");
            string campaignId = _root.CurrentLevelId;
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;

            _root.SelectDaily();
            Assert.That(_root.IsDailySession, Is.True, "precondition: the first call succeeded");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L800"));

            // The reviewer's scenario: call it again, directly, no yield — before the guard,
            // this would set _preDailyLevel = _level, and _level by now IS the Daily board
            // itself, corrupting the restore target.
            _root.SelectDaily();
            Assert.That(_root.IsDailySession, Is.True, "still in the same daily session");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L800"),
                "the second call did nothing — no reload, no second board resolution");

            var panel = _root.GetComponent<ResultsPanel>();
            panel.NextRequested?.Invoke(); // the LoadNextTests direct-call precedent
            Assert.That(_root.IsDailySession, Is.False);
            Assert.That(_root.CurrentLevelId, Is.EqualTo(campaignId),
                "the pre-daily restore target must be the ORIGINAL campaign level — a "
                + "corrupted _preDailyLevel would restore to the Daily board (\"L800\") "
                + "instead");
        }

        // --- determinism at the wiring layer (criterion 4/8): one clock read, pure downstream ---

        [UnityTest]
        public IEnumerator SelectDaily_IsDeterministic_ForTheSameDate_AndDiffers_ForADifferentDate()
        {
            yield return BootToHomeWithCampaignFixture();
            _root.DailyClockUnixSeconds = () => OtherUnixSeconds;
            var countingFactory = new CountingFactory();
            _root.DailyFactory = () => countingFactory;
            _root.SelectDaily();
            yield return WaitForDailySession(_root);
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(OtherSeed));
            Assert.That(_root.LastDailyBoardSource, Is.EqualTo("generated"),
                "outside the baked horizon, deterministic runtime generation is the fallback");
            uint firstSeed = (uint)_root.Session.Level.Dto.Seed;
            string firstJson = DailyBoardJson.Serialize(_root.Session.Level.Dto);
            Assert.That(countingFactory.BuildCalls, Is.EqualTo(1),
                "the admitted k=0 fallback invokes the generator exactly once");

            // Return home and select Daily again with the SAME injected date.
            var panel = _root.GetComponent<ResultsPanel>();
            panel.NextRequested?.Invoke(); // the LoadNextTests direct-call precedent: isolates
                                            // the routing law from the tap-routing law
            Assert.That(_root.IsDailySession, Is.False, "precondition: returned home");
            _root.SelectDaily();
            yield return WaitForDailySession(_root);
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(firstSeed),
                "same injected date -> byte-identical resolution");
            Assert.That(DailyBoardJson.Serialize(_root.Session.Level.Dto), Is.EqualTo(firstJson));
            Assert.That(countingFactory.BuildCalls, Is.EqualTo(1),
                "same-date replay must use the in-memory admitted board, not regenerate it");

            // A later injected date resolves the other pinned vector. This ordering is
            // deliberate: forward jumps are accepted because an offline client cannot tell a
            // clock jump from a long absence; the reverse ordering is the rollback-practice
            // case covered separately below.
            panel.NextRequested?.Invoke();
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.SelectDaily();
            yield return WaitForDailySession(_root);
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(PinnedSeed),
                "#73's own pinned vector for 2026-08-24");
            Assert.That(_root.LastDailyBoardSource, Is.EqualTo("precomputed"));

            // F3 (review fix round, found while implementing the one-frame lockout): the
            // return-home -> immediate-reselect sequence just above (no yield between
            // NextRequested?.Invoke() and the next SelectDaily()) used to leave the FIRST
            // return-home's deferred Home-show armed while a SECOND Daily session is now in
            // progress — LoadLevel's own pending-show clear (inside SelectDaily's call)
            // closes that. Pump several frames now (well past the lockout window) and prove
            // Home never appears uninvited mid-session.
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_root.IsDailySession, Is.True,
                "still in the SECOND daily session — nothing here should have returned home");
            Assert.That(_root.Home.IsVisible, Is.False,
                "a stale pending Home-show from the FIRST return-home cycle must not fire "
                + "while a different Daily session is active");
        }

        // --- criteria 5/6/9: a REAL Daily win (solver-witnessed, the DevLevelOverrideTests
        // precedent) surfaces the ticket reward, never advances the campaign band, and returns
        // Home through a CTA that reads differently from the campaign "Next" ---

        [UnityTest]
        public IEnumerator DailyWin_SolverWitnessed_SurfacesTickets_ReturnsHome_NeverAdvancesCampaign()
        {
            // F2 (review fix round): "L004", deliberately NOT LevelBand[0]. The daily board
            // that loads carries id "L800", which is outside LevelBand entirely — under the
            // MUTATION this test exists to catch (deleting OnResultsCtaRequested's
            // _dailySession branch, routing every CTA tap straight to LoadNext), LoadNext
            // would compute NextLevelId("L800"), and NextLevelId's own A-LN-2 "unknown id
            // restarts the band" rule maps ANY out-of-band id to LevelBand[0] == "L001". With
            // the ORIGINAL "L001" fixture, that wrap target coincidentally equals the
            // pre-Daily campaign id, so the mutated code silently produced the "right"
            // answer for the wrong reason and the assertion below could never go red. "L004"
            // has no such coincidence: the mutated path lands on "L001", not "L004", so the
            // restored-id assertion now genuinely discriminates the real router branch from
            // both the wrap path and the advance path.
            yield return BootToHomeWithCampaignFixture("L004");
            string campaignIdBeforeDaily = _root.CurrentLevelId;
            Assert.That(campaignIdBeforeDaily, Is.EqualTo("L004"));

            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.SelectDaily();
            Assert.That(_root.IsDailySession, Is.True, "precondition");
            Assert.That(_root.DailyTicketsEarned, Is.Null, "not yet won");
            Assert.That(_root.LifetimeDailyCompletions, Is.Zero,
                "the batch test boot starts with an isolated fresh save");

            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.CtaText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("results.daily.done")),
                "the Daily CTA never reads the campaign LOCKED \"Next\" copy");
            Assert.That(panel.CtaText, Is.Not.EqualTo("Next"));

            var level = _root.Session.Level;
            var solve = CatMetro.Domain.Solver.LevelSolver.Solve(level.Graph, (ulong)level.Dto.Seed);
            Assert.That(solve.Verdict, Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.Solved),
                "DailyPipeline's own admission already proved this — re-solving here just gets "
                + "the command log to replay");

            foreach (var e in solve.OptimalLog.Entries)
            {
                while (_root.Session.State.Tick < e.Tick
                    && _root.Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running)
                    _root.Session.AdvanceMs(
                        CatMetro.Application.Session.TickInterpolator.TICK_MS);
                _root.Session.EnqueueToggle(e.SwitchId);
            }
            _root.Session.AdvanceMs(400 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null; // GameRoot.Update picks up the outcome
            Assert.That(_root.ScreenState, Is.EqualTo("Won"), "the solver's own log wins for real");
            Assert.That(_root.DailyTicketsEarned, Is.EqualTo(level.Dto.Economy.BaseTickets),
                "criterion 9 — surfaced from the admitted board's own DTO reward");
            Assert.That(_root.LifetimeDailyCompletions, Is.EqualTo(1),
                "the first eligible Daily win increments the cumulative lifetime tally once");
            yield return null; // within one pumped frame the panel is up

            Assert.That(panel.IsVisible, Is.True, "precondition: the panel is showing");
            int tapResult = _root.Input.HandleTapAtScreen(panel.ChipPaintedRectPx.center);
            Assert.That(tapResult, Is.EqualTo(-3), "the chrome region consumed the tap");

            Assert.That(_root.IsDailySession, Is.False);
            Assert.That(_root.DailyTicketsEarned, Is.Null, "cleared on the return-home reload");
            Assert.That(_root.CurrentLevelId, Is.EqualTo(campaignIdBeforeDaily),
                "restored the SAME campaign level — never GameRoot.NextLevelId/LevelBand");

            // F3 (review fix round): Home.Show()/Stack.Push are now deferred by the one-frame
            // input lockout (GameRoot.Update) — immediately after the tap, Home is NOT yet
            // visible and the breadcrumb is momentarily empty by design (this is exactly the
            // window DailyWin_DoubleTapAcrossOneYield... below proves is safe from ghost
            // navigation). Two yields let the deferred show settle before asserting the final,
            // steady-state screen.
            Assert.That(_root.Home.IsVisible, Is.False,
                "precondition: still within the one-frame lockout window immediately after "
                + "the tap — Home has not been shown yet");
            yield return null;
            yield return null;
            Assert.That(_root.Home.IsVisible, Is.True, "Home re-shown once the lockout clears");
            Assert.That(_root.Home.DailyTallyText, Is.EqualTo("Dailies completed: 1"));
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
        }

        [UnityTest]
        public IEnumerator ClockRollback_LoadsTrustedPuzzleAsPractice_AndExplainsWhy()
        {
            yield return BootToHomeWithCampaignFixture();
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.SelectDaily();
            Assert.That(_root.ActiveDailyDateKey, Is.EqualTo("2026-08-24"));

            _root.GetComponent<ResultsPanel>().NextRequested?.Invoke();
            _root.DailyClockUnixSeconds = () => OtherUnixSeconds;
            _root.SelectDaily();

            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.ActiveDailyDateKey, Is.EqualTo("2026-08-24"),
                "rollback serves the trusted high-water puzzle, not the older clock date");
            Assert.That(_root.DailyRunIsPractice, Is.True);
            Assert.That(_root.Banner.CurrentKey, Is.EqualTo("daily.practice"));
            Assert.That(_root.Banner.CurrentText,
                Is.EqualTo("Clock changed — practice run"));
        }

        // --- F3 (review fix round): a repeat tap at the SAME coordinates, one yield after the
        // first, must not land on the freshly-shown Home pin and push Intro ---

        [UnityTest]
        public IEnumerator DailyWin_DoubleTapAcrossOneYield_DoesNotLandOnHomePin_NoIntroPushed()
        {
            yield return BootToHomeWithCampaignFixture("L004");
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.SelectDaily();

            var level = _root.Session.Level;
            var solve = CatMetro.Domain.Solver.LevelSolver.Solve(level.Graph, (ulong)level.Dto.Seed);
            Assert.That(solve.Verdict, Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.Solved));
            foreach (var e in solve.OptimalLog.Entries)
            {
                while (_root.Session.State.Tick < e.Tick
                    && _root.Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running)
                    _root.Session.AdvanceMs(
                        CatMetro.Application.Session.TickInterpolator.TICK_MS);
                _root.Session.EnqueueToggle(e.SwitchId);
            }
            _root.Session.AdvanceMs(400 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;
            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.IsVisible, Is.True, "precondition: the panel is showing");
            var center = panel.ChipPaintedRectPx.center;

            int tap1 = _root.Input.HandleTapAtScreen(center);
            Assert.That(tap1, Is.EqualTo(-3), "precondition: the first tap returned home");
            Assert.That(_root.IsDailySession, Is.False, "precondition: the router fired once");

            yield return null; // the ONE-yield gap this test exists to exercise

            // The repeat tap, at the EXACT same screen coordinates: the L001/Daily pins
            // (once shown) center in the same thumb band the results CTA occupied, so this
            // is the coordinate a real accidental double-tap would land on.
            int tap2 = _root.Input.HandleTapAtScreen(center);
            Assert.That(tap2, Is.Not.EqualTo(-3),
                "the lockout window must still be active one yield after the transition — "
                + "the repeat tap must not resolve to a chrome region at all");

            // Let the deferred Home.Show() actually settle before checking the final state.
            yield return null;
            yield return null;
            Assert.That(_root.Home.IsVisible, Is.True, "Home eventually settles visible");
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb(),
                "the double-tap across one yield must not have pushed intro — exactly one "
                + "\"home\" entry, never [\"home\", \"intro\"]");
            Assert.That(_root.Intro.IsVisible, Is.False, "no Intro sheet was ever shown");
        }

        // --- criterion 7: an unadmittable factory fails loud, Home stays exactly as it was ---

        private sealed class NeverAdmitsFactory : IBoardFactory
        {
            public LevelDto Build(uint seed, string dateKey, int k) => null;
        }

        [UnityTest]
        public IEnumerator AdmissionFailure_LeavesHomeUntouched_NeverLoadsAnything_LogsLoudly()
        {
            yield return BootToHomeWithCampaignFixture();
            string campaignIdBefore = _root.CurrentLevelId;
            _root.DailyClockUnixSeconds = () => OtherUnixSeconds;
            _root.DailyFactory = () => new NeverAdmitsFactory();

            LogAssert.Expect(LogType.Error,
                new Regex(@"daily fallback failed.*pipeline could not admit a board"));
            _root.SelectDaily();

            Assert.That(_root.Home.DailyStatusText, Is.EqualTo("Preparing today's Line…"),
                "the failure path returns a loading frame before reporting the terminal error");
            float deadline = Time.realtimeSinceStartup + 90f;
            while (_root.Home.DailyStatusText != "Daily unavailable — try again"
                && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(_root.IsDailySession, Is.False);
            Assert.That(_root.CurrentLevelId, Is.EqualTo(campaignIdBefore),
                "nothing loaded — the campaign level stays exactly as it was");
            Assert.That(_root.Home.IsVisible, Is.True, "Home is untouched, never half-hidden");
            Assert.That(_root.Home.DailyStatusText,
                Is.EqualTo("Daily unavailable — try again"),
                "the failed tap must be visible, not only an error log");
        }

        // --- fixtures ---

        // A minimal, real, importable campaign-shaped fixture — only used to seed GameRoot's
        // boot level before Daily is selected; this contract never needs it to WIN (the Daily
        // win tests solve+replay the REAL admitted Daily board instead, the
        // DevLevelOverrideTests precedent).
        private static string CampaignFixtureJson(string id)
        {
            return CampaignFixtureTemplate.Replace("__ID__", id);
        }

        private const string CampaignFixtureTemplate = @"{
  ""schemaVersion"": 2, ""id"": ""__ID__"", ""name"": ""DailyWire Campaign Fixture"", ""seed"": 960,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 12 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": 12 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": [ { ""tick"": 3999, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 5, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
    }
}
