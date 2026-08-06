using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Content;

namespace CatMetro.Tests.PlayMode
{
    // CM-C3-DEVCAP2: the dev-only boot-level override — criteria 1 (honored + announced,
    // mutually exclusive with SEAM_LOADED), 2 (invalid falls back LOUDLY to the shipped path),
    // 3 (absent changes nothing), 5 (the demo level: no-input play FAILS into the retry loop,
    // solver-witnessed active play WINS). The scene-boot path is exercised by adding the
    // component OUTSIDE the factory paths, so Awake self-initializes exactly as on device.
    public sealed class DevLevelOverrideTests
    {
        private GameRoot _root;
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "cm-devcap2-test", "devcap");
            Directory.CreateDirectory(_tmpDir);
            DevLevelOverride.DirectoryOverride = _tmpDir;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            DevLevelOverride.DirectoryOverride = null;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            var parent = Path.GetDirectoryName(_tmpDir);
            if (parent != null && Directory.Exists(parent)) Directory.Delete(parent, true);
        }

        private GameRoot SceneBoot()
        {
            // device path: a bare AddComponent outside the factories → Awake self-initializes
            var go = new GameObject("GameRoot-scene-boot");
            return go.AddComponent<GameRoot>();
        }

        private static string DemoJson() =>
            File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory().Replace("/unity", ""),
                "tests/fixtures/devcap/demo-level.json"));

        // --- criterion 1: override honored, announced, seam line suppressed ---
        [UnityTest]
        public IEnumerator Override_Honored_Announced_SeamLineSuppressed()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "level.json"), DemoJson());
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                @"^DEVCAP_LEVEL_OVERRIDE .+[/\\]devcap[/\\]level\.json$"));
            _root = SceneBoot();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null, "booted");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("D001"),
                "the OVERRIDE level booted, not the shipped path");
            // mutual exclusivity: no seam line may have fired (an artifact must never mistake
            // an override run for a seam run)
            LogAssert.NoUnexpectedReceived();
        }

        // --- criterion 2: invalid override falls back LOUDLY to the shipped path ---
        [UnityTest]
        public IEnumerator InvalidOverride_LoudFallback_ToShippedPath()
        {
            File.WriteAllBytes(Path.Combine(_tmpDir, "level.json"),
                Encoding.UTF8.GetBytes("{ not a level"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^DEVCAP_LEVEL_OVERRIDE_INVALID "));
            _root = SceneBoot();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null, "still boots — never a crash or hang");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"),
                "the NORMAL shipped level booted");
        }

        // --- criterion 3: absent override changes nothing ---
        [UnityTest]
        public IEnumerator AbsentOverride_NormalSeamBoot()
        {
            _root = SceneBoot();
            yield return null;
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"));
        }

        // --- criterion 5(a): the demo fails into the RETRY LOOP under no input ---
        [UnityTest]
        public IEnumerator Demo_NoInput_FailsIntoRetryLoop()
        {
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(DemoJson()));
            Assert.That(imported.Ok, Is.True, "demo must import: " + imported.Error);
            _root = GameRoot.LaunchWith(imported.Value);
            _root.MotionOffToggle = true;
            yield return null;
            Time.timeScale = 12f;
            float dl = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < dl)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"),
                "inactive play must reach the fail/retry loop — the demo's whole point");
            Assert.That(_root.Session.State.Outcome.Reason,
                Is.EqualTo(CatMetro.Domain.FailReason.QueueOverflow),
                "via QueueOverflow — never the pinned misroute boundary");
            // and the loop is live: one tap returns to Playing
            int r = _root.Input.HandleTapAtScreen(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.1f));
            Assert.That(r, Is.EqualTo(-2));
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
        }

        // --- criterion 5(b): active switching can WIN, witnessed by the solver ---
        [UnityTest]
        public IEnumerator Demo_SolverWitnessed_ActivePlayWins()
        {
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(DemoJson()));
            Assert.That(imported.Ok, Is.True);
            var solve = CatMetro.Domain.Solver.LevelSolver.Solve(
                imported.Value.Graph, (ulong)imported.Value.Dto.Seed);
            Assert.That(solve.Verdict,
                Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.Solved),
                "the demo must be WINNABLE by active play — retune the level, never this test");

            _root = GameRoot.LaunchWith(imported.Value);
            yield return null;
            // replay the solver's optimal command log through the real session (no yields
            // inside the loop — GameRoot.Update must not advance ticks between entries)
            foreach (var e in solve.OptimalLog.Entries)
            {
                while (_root.Session.State.Tick < e.Tick &&
                       _root.Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running)
                    _root.Session.AdvanceMs(
                        CatMetro.Application.Session.TickInterpolator.TICK_MS);
                _root.Session.EnqueueToggle(e.SwitchId);
            }
            _root.Session.AdvanceMs(400 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.Session.State.Outcome.Kind,
                Is.EqualTo(CatMetro.Domain.OutcomeKind.Won),
                "the solver's own log wins through the real session");
        }
    }
}
