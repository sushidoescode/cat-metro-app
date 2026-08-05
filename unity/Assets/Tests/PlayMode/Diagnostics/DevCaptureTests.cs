using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Content;
using CatMetro.Presentation.Diagnostics;

namespace CatMetro.Tests.PlayMode
{
    // CM-C3-DEVCAP: the dev-only FrameLog capture — criteria 1 (verbatim CSV, no second clock),
    // 2 (one causeVisible mark per failure, on the exact frame the editor protocol measures,
    // FailureTests.cs:247-251), 3 (single pause-hook write to a fixed, injectable location,
    // announced in logcat). The capture observes; it may never perturb the run (stop condition 7)
    // or read any clock beyond FrameRecord copies (A-C3-6 / A-DEVCAP-1).
    public sealed class DevCaptureTests
    {
        private GameRoot _root;
        private string _tmpRoot;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            if (_tmpRoot != null && Directory.Exists(_tmpRoot)) Directory.Delete(_tmpRoot, true);
            _tmpRoot = null;
        }

        // --- criterion 1: pure verbatim serialisation, exact text pinned ---
        [Test]
        public void ToCsv_VerbatimColumns_MarksOnlyTheGivenFrames_QuotesOnlyWhenNeeded()
        {
            var records = new List<FrameRecord>
            {
                new FrameRecord(101, 16, 0, "Playing"),
                new FrameRecord(102, 33, 1, "Playing"),
                new FrameRecord(103, 50, 1, "FailureReview"),
                // pathological state string: the ONLY case that may be quoted, quotes doubled
                new FrameRecord(104, 67, 2, "Fail,ure\"Review"),
            };
            var marks = new HashSet<int> { 103 };

            string csv = FrameLogCsv.ToCsv(records, marks);

            Assert.That(csv, Is.EqualTo(
                "frameIndex,monotonicMs,simTick,screenState,causeVisible\n" +
                "101,16,0,Playing,0\n" +
                "102,33,1,Playing,0\n" +
                "103,50,1,FailureReview,1\n" +
                "104,67,2,\"Fail,ure\"\"Review\",0\n"),
                "columns 1-4 are FrameRecord fields copied verbatim in record order; " +
                "column 5 is the mark; quoting only for , or \"");
        }

        // --- criterion 2: exactly one mark per failure, on the frame the predicate first held ---
        [UnityTest]
        public IEnumerator Mark_OncePerFailure_OnTheExactPredicateFrame_LatchResets(
            [Values(true, false)] bool motionOff)
        {
            _root = GameRoot.LaunchWith(Import(OverflowFixtureJson()));
            _root.MotionOffToggle = motionOff;
            yield return null;

            var cap = _root.GetComponent<DevFrameCapture>();
            Assert.That(cap, Is.Not.Null,
                "the composition root wires the capture in editor/dev builds (guarded lines)");
            Assert.That(cap.CauseVisibleFrames.Count, Is.Zero, "no mark before any failure");

            for (int cycle = 1; cycle <= 2; cycle++)
            {
                // run to failure (FailureTests.cs RunToFail pattern)
                Time.timeScale = 12f;
                float dl = Time.realtimeSinceStartup + 40f;
                while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < dl)
                    yield return null;
                Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"), "cycle " + cycle);

                // observe the FIRST frame on which the editor-protocol predicate holds —
                // the identical predicate to FailureTests.cs:247-251
                int expectedFrame = -1;
                float fdl = Time.realtimeSinceStartup + 10f;
                while (expectedFrame < 0 && Time.realtimeSinceStartup < fdl)
                {
                    if (_root.CauseCam.IsFramed && _root.Banner.Visible)
                        expectedFrame = Time.frameCount;
                    else
                        yield return null;
                }
                Time.timeScale = 1f;
                Assert.That(expectedFrame, Is.GreaterThan(0), "predicate held, cycle " + cycle);
                // the capture marks in LateUpdate of that same frame; let it run
                yield return null;

                Assert.That(cap.CauseVisibleFrames.Count, Is.EqualTo(cycle),
                    "exactly one mark per failure (latch), cycle " + cycle);
                Assert.That(cap.CauseVisibleFrames, Does.Contain(expectedFrame),
                    "the mark is the frame the predicate FIRST held — not before, not after");

                // dwell in FailureReview: the latch must not re-mark
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(cap.CauseVisibleFrames.Count, Is.EqualTo(cycle),
                    "no re-mark while framed in FailureReview");

                // retry (one tap on the thumb band), then dwell in Playing: zero marks there
                int r = _root.Input.HandleTapAtScreen(
                    new Vector2(Screen.width * 0.5f, Screen.height * 0.1f));
                Assert.That(r, Is.EqualTo(-2), "retry consumed the tap");
                Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(cap.CauseVisibleFrames.Count, Is.EqualTo(cycle),
                    "zero marks outside FailureReview");
            }
        }

        // --- criterion 3: one fixed location, one write, only on pause(true), announced ---
        [UnityTest]
        public IEnumerator Write_OnlyOnPauseTrue_ToInjectedDirectory_Announced()
        {
            _root = GameRoot.LaunchWith(Import(OverflowFixtureJson()));
            _root.MotionOffToggle = true;
            yield return null;

            var cap = _root.GetComponent<DevFrameCapture>();
            Assert.That(cap, Is.Not.Null);
            _tmpRoot = Path.Combine(Path.GetTempPath(), "cm-devcap-test");
            string outDir = Path.Combine(_tmpRoot, "devcap"); // tail matches the runbook path shape
            cap.OutputDirectory = outDir;

            // (a) a full fail -> retry cycle with NO pause writes nothing
            Time.timeScale = 12f;
            float dl = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < dl)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
            _root.Input.HandleTapAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.1f));
            yield return null;
            Assert.That(
                !Directory.Exists(outDir) || Directory.GetFiles(outDir).Length == 0, Is.True,
                "no capture I/O outside the pause hook — never inside a measured interval");

            // (b)+(c) the pause hook performs the single write, announced with the runbook line
            int rowsAtPause = _root.Log.Records.Count;
            int marksAtPause = cap.CauseVisibleFrames.Count;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                @"^DEVCAP_WRITTEN .+[/\\]devcap[/\\]framelog\.csv rows=[0-9]+ marks=[0-9]+$"));
            cap.OnApplicationPause(true);

            string file = Path.Combine(outDir, "framelog.csv");
            Assert.That(File.Exists(file), Is.True, "the fixed, documented filename");
            string[] lines = File.ReadAllLines(file);
            Assert.That(lines[0], Is.EqualTo("frameIndex,monotonicMs,simTick,screenState,causeVisible"));
            Assert.That(lines.Length - 1, Is.EqualTo(rowsAtPause),
                "one row per FrameLog record at the moment of the pause");
            int markRows = 0;
            for (int i = 1; i < lines.Length; i++)
                if (lines[i].EndsWith(",1")) markRows++;
            Assert.That(markRows, Is.EqualTo(marksAtPause), "marks serialize 1:1");

            // pause(false) must not write again (delete, unpause, still absent)
            File.Delete(file);
            cap.OnApplicationPause(false);
            yield return null;
            Assert.That(File.Exists(file), Is.False, "pause(false) never writes");
        }

        // --- criterion 3(d): the default location is the documented one ---
        [Test]
        public void DefaultOutputDirectory_IsPersistentDataPathDevcap()
        {
            var go = new GameObject("cap-default-probe");
            try
            {
                var cap = go.AddComponent<DevFrameCapture>();
                Assert.That(cap.OutputDirectory,
                    Is.EqualTo(Path.Combine(UnityEngine.Application.persistentDataPath, "devcap")),
                    "the runbook's adb-pull path derives from this exact default");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static ImportedLevel Import(string json)
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, $"fixture must import: {r.Error}");
            return r.Value;
        }

        // The CM-C2b/C3 scripted-overflow fixture shape (FailureTests.cs): queueCapacity 4 with a
        // flood then sustain holds the source queue inside the digest envelope and fails ~t27.
        private static string OverflowFixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T905"", ""name"": ""Devcap Fixture"", ""seed"": 905,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch"", ""queue""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9, ""queueCapacity"": 4 },
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
  ""waves"": [
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 14, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 8, ""spacingTicks"": 1 },
    { ""tick"": 22, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 4, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
