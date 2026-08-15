using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Content;
using CatMetro.Content.Daily;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.PlayMode
{
    // CM-DAILYWIRE: funnel position 6, end-to-end. Home's Daily pin -> the REAL DailyPipeline
    // for an injected UTC date -> the admitted board plays through LevelImporter.Import (the
    // identical function LoadNext/InitializeFromSeam use) -> a Daily win never reaches
    // LevelBand/NextLevelId/WrapAtEndOfBand and returns Home. Every test drives REAL composition
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
            // CM-DAILYWIRE: the shipped default is false (S-01 — HomeScreenTests.cs's own tree
            // walk forbids a Daily node on session-1 Home; product_spec §18 agrees, "after L007
            // win"). This suite explicitly opts into the dev/test-only seam to exercise the
            // REAL wiring end-to-end; every test resets it in TearDown so it never bleeds into
            // an unrelated fixture (the BootToHome hygiene precedent).
            GameRoot.DailyEntryUnlocked = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.BootToHome = false;
            GameRoot.DailyEntryUnlocked = false;
            DevLevelOverride.DirectoryOverride = null; // CM-BOOT-HOME re-seam hygiene
            if (!string.IsNullOrEmpty(_tmpDir) && Directory.Exists(_tmpDir))
                Directory.Delete(_tmpDir, true);
            _tmpDir = null;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = 1f;
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

        // --- the S-01 default: DailyEntryUnlocked false builds ZERO Daily objects (discovered
        // mid-implementation via HomeScreenTests.cs's own tree walk — product_spec §18's
        // "after L007 win" gate and S-01's "no daily entry rendered in session 1" law agree,
        // and this contract does not relax either; see the frozen contract's revised FA-4) ---

        [UnityTest]
        public IEnumerator ShippedDefault_DailyEntryUnlocked_False_BuildsZeroDailyObjects()
        {
            GameRoot.DailyEntryUnlocked = false; // the actual shipped default, overriding SetUp
            _root = LaunchWithCampaignFixture("L001");
            yield return null;

            Assert.That(_root.Home, Is.Not.Null, "precondition: the screen flow composed");
            Assert.That(_root.Home.DailyPinTransform, Is.Null,
                "zero Daily objects constructed — not merely hidden (the CM-UX-07 shipped-boot "
                + "precedent) — S-01 and product_spec §18 (\"after L007 win\") both forbid it "
                + "here");
            Assert.That(_root.Home.DailyLabelText, Is.EqualTo(""),
                "no label component exists to read");

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

        // --- criteria 2/3/8: the real pipeline runs for the injected date, loads through the
        // campaign seam, and the clock enters exactly once ---

        [UnityTest]
        public IEnumerator TapDailyPin_RunsTheRealPipeline_ForTheInjectedDate_LoadsThroughTheSeam()
        {
            yield return BootToHomeWithCampaignFixture();
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;

            LogAssert.Expect(LogType.Log, new Regex(@"SEAM_LOADED daily:2026-08-24"));
            int tapResult = _root.Input.HandleTapAtScreen(_root.Home.DailyPinPaintedRectPx.center);
            Assert.That(tapResult, Is.EqualTo(-3), "the Daily pin is a registered chrome region");

            Assert.That(_root.IsDailySession, Is.True);
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L800"),
                "the shipped Daily runtime id (DAILY-LINE A-DL-3)");
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(PinnedSeed),
                "#73's own pinned vector for 2026-08-24 — the REAL pipeline ran, not a stub");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.Home.IsVisible, Is.False);
            CollectionAssert.AreEqual(new string[0], _root.Stack.ToBreadcrumb(),
                "no Intro sheet for Daily — straight into gameplay");
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
            _root.DailyClockUnixSeconds = () => PinnedUnixSeconds;
            _root.SelectDaily();
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(PinnedSeed));
            uint firstSeed = (uint)_root.Session.Level.Dto.Seed;
            string firstJson = DailyBoardJson.Serialize(_root.Session.Level.Dto);

            // Return home and select Daily again with the SAME injected date.
            var panel = _root.GetComponent<ResultsPanel>();
            panel.NextRequested?.Invoke(); // the LoadNextTests direct-call precedent: isolates
                                            // the routing law from the tap-routing law
            Assert.That(_root.IsDailySession, Is.False, "precondition: returned home");
            _root.SelectDaily();
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(firstSeed),
                "same injected date -> byte-identical resolution");
            Assert.That(DailyBoardJson.Serialize(_root.Session.Level.Dto), Is.EqualTo(firstJson));

            // A different injected date resolves the OTHER pinned vector.
            panel.NextRequested?.Invoke();
            _root.DailyClockUnixSeconds = () => OtherUnixSeconds;
            _root.SelectDaily();
            Assert.That((uint)_root.Session.Level.Dto.Seed, Is.EqualTo(OtherSeed),
                "#73's own pinned vector for 2026-08-10");

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
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
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
            _root.DailyFactory = () => new NeverAdmitsFactory();

            LogAssert.Expect(LogType.Error,
                new Regex(@"daily pipeline could not admit a board"));
            _root.SelectDaily();

            Assert.That(_root.IsDailySession, Is.False);
            Assert.That(_root.CurrentLevelId, Is.EqualTo(campaignIdBefore),
                "nothing loaded — the campaign level stays exactly as it was");
            Assert.That(_root.Home.IsVisible, Is.True, "Home is untouched, never half-hidden");
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
