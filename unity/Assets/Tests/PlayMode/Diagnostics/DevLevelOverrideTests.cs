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
            // PR #52 F4: this fixture's SceneBoot tests reach InitializeFromSeam, which now
            // ALSO reads DevBootOverride's file (CM-DEVCAP3) — isolate it to the same empty temp
            // dir so a real devcap/boot.json on the developer's machine can never leak an
            // unexpected DEVCAP_BOOT_OVERRIDE log into Override_Honored_Announced_
            // SeamLineSuppressed's LogAssert.NoUnexpectedReceived() below.
            DevBootOverride.DirectoryOverride = _tmpDir;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            DevLevelOverride.DirectoryOverride = null;
            DevBootOverride.DirectoryOverride = null; // PR #52 F4
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

        // CM-SEAMS: a minimal but SCHEMA-VALID level whose one wave carries a deliberately
        // malicious "color" value — invalid enough that LevelImporter's pin pre-check
        // (ColorCode) rejects it, but only AFTER referential integrity passes, so the rejection
        // detail is a controlled echo of attacker-supplied content: `wave color '<value>'
        // unknown`. The `\n` here is a real JSON escape sequence — Newtonsoft decodes it to an
        // ACTUAL embedded newline character in the resulting C# string — the exact log-injection
        // shape sanitize's strip-newline defends against. The 260-char run then blows the
        // resulting detail past the 200-char cap.
        private static string MaliciousColorLevelJson()
        {
            string zeros = new string('Z', 260);
            string colorLiteral = "\"x\\n" + zeros + "\"";
            return "{"
                + "\"schemaVersion\":2,"
                + "\"id\":\"SANITIZE-PIN\","
                + "\"name\":\"sanitize pin fixture\","
                + "\"seed\":1,"
                + "\"meta\":{"
                    + "\"band\":\"demo\","
                    + "\"difficultyTarget\":0.1,"
                    + "\"mechanics\":[],"
                    + "\"newMechanic\":null,"
                    + "\"teachingGoal\":\"t\","
                    + "\"minActionWindowTicks\":3,"
                    + "\"authoredBy\":\"test\""
                + "},"
                + "\"board\":{"
                    + "\"nodes\":[{\"id\":\"SRC\",\"x\":0,\"y\":0},{\"id\":\"DST\",\"x\":1,\"y\":0}],"
                    + "\"edges\":[{\"id\":\"E1\",\"from\":\"SRC\",\"to\":\"DST\",\"travelTicks\":1}]"
                + "},"
                + "\"sources\":[{\"nodeId\":\"SRC\",\"allowedColors\":[\"red\"]}],"
                + "\"stations\":[{\"nodeId\":\"DST\",\"accepts\":[\"red\"],\"capacity\":1}],"
                + "\"switches\":[],"
                + "\"waves\":[{\"tick\":1,\"sourceNode\":\"SRC\",\"color\":" + colorLiteral
                    + ",\"count\":1,\"spacingTicks\":1}],"
                + "\"win\":{\"deliveries\":1,\"timeLimitTicks\":20,\"perfectMaxSwitches\":0,"
                    + "\"stars\":{\"two\":1,\"three\":1}},"
                + "\"economy\":{\"baseTickets\":1,\"perfectBonus\":0}"
                + "}";
        }

        private string _capturedInvalidLog;
        private void CaptureInvalidLog(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith("DEVCAP_LEVEL_OVERRIDE_INVALID")) _capturedInvalidLog = condition;
        }

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
            // Contract amendment (recorded in the status log): QueueOverflow is provably
            // input-independent in single-source topologies (Simulation.cs:12-24 — one mouth
            // release per tick, and only SRC feeds E1), so the player-skill failure is the
            // CLOCK: the slow default route misses the limit. TimeOut is the designed loss.
            // (Review F1: burst waves add pressure but never arm the overload state here, and
            // no overload ring is rendered anywhere yet — CM-R02.5 is a follow-up.)
            Assert.That(_root.Session.State.Outcome.Reason,
                Is.EqualTo(CatMetro.Domain.FailReason.TimeOut),
                "via TimeOut — never the pinned misroute boundary");
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

        // --- CM-SEAMS (security-S1): pre-read size cap. LevelImporter.Import() ALSO rejects an
        // oversize payload (ContentBounds.CONTENT_MAX_FILE_BYTES) — but only after
        // File.ReadAllBytes has already read the whole thing into memory, and its rejection
        // message is routed through ContentError.ToString() ("FileTooLarge: payload N bytes
        // exceeds..."). This seam's own pre-read guard fires FIRST with its own directly-authored
        // wording (no "FileTooLarge:" kind prefix) — the regex below pins that exact shape, so a
        // mutation that deletes the pre-read guard (falling back to relying on the importer's own
        // cap) is caught: the log line would then read "...— FileTooLarge: payload..." instead. ---
        [UnityTest]
        public IEnumerator OversizeFile_PreReadSizeCap_LoudFallback_DistinctFromImporterCap()
        {
            int padLen = ContentBounds.CONTENT_MAX_FILE_BYTES + 1000;
            File.WriteAllText(Path.Combine(_tmpDir, "level.json"), new string('z', padLen));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                @"^DEVCAP_LEVEL_OVERRIDE_INVALID .+ — payload \d+ bytes exceeds CONTENT_MAX_FILE_BYTES — booting the shipped path$"));
            _root = SceneBoot();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null, "still boots — never a crash or hang");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"),
                "the NORMAL shipped level booted");
        }

        // --- CM-SEAMS (pins the security-S2-mirrored sanitization added in this batch —
        // code-R1: DevBootOverride's own S2 fix was landed unpinned, and this seam had none at
        // all): a malicious wave color that JSON-decodes to an embedded newline plus a 260-char
        // run must never spoof an extra log line or blow the log line's length open. Deleting
        // Sanitize() (or either half of it) turns this RED: the raw ContentError.ToString()
        // both contains a literal '\n' and the full 260-Z run verbatim. ---
        [UnityTest]
        public IEnumerator Sanitization_MaliciousImporterErrorDetail_LogLineSingleLineAndCapped()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "level.json"), MaliciousColorLevelJson());
            _capturedInvalidLog = null;
            UnityEngine.Application.logMessageReceived += CaptureInvalidLog;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^DEVCAP_LEVEL_OVERRIDE_INVALID "));
            try
            {
                _root = SceneBoot();
                yield return null;
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= CaptureInvalidLog;
            }
            Assert.That(_capturedInvalidLog, Is.Not.Null, "the INVALID line must have fired");
            Assert.That(_root.Session, Is.Not.Null, "still boots — never a crash or hang");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"),
                "the NORMAL shipped level booted");
            Assert.That(_capturedInvalidLog, Does.Not.Contain("\n"),
                "a color value that JSON-decodes to an embedded newline must never spoof an "
                + "extra log line");
            Assert.That(_capturedInvalidLog, Does.Not.Contain("\r"));
            Assert.That(_capturedInvalidLog, Does.Contain("…"),
                "an oversize importer error detail must be TRUNCATED, not logged whole");
            Assert.That(_capturedInvalidLog, Does.Not.Contain(new string('Z', 260)),
                "the full untruncated malicious payload must never reach the log");
        }
    }
}
