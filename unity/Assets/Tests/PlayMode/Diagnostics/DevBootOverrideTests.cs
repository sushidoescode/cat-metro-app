using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;

namespace CatMetro.Tests.PlayMode
{
    // CM-DEVCAP3: the dev-only boot-to-home FILE seam — mirrors DevLevelOverrideTests' own
    // injection pattern (DirectoryOverride, an isolated temp dir per test, a scene/Launch()
    // boot so Awake -> InitializeFromSeam self-initializes exactly as on device). Criteria 1
    // (honored, announced, real screen-flow composition through the REAL GameRoot.Launch()
    // seam — never a test-side override of Wire's own binding), 3 (three malformed classes:
    // bad JSON syntax, a well-formed-but-wrong key, an explicit false value — all fall back to
    // shipped boot with no throw and no partial composition), 4 (Q-5 law reasserted: the file
    // NEVER changes which level boots), 5 (precedence between the static test flag and the
    // file — see the Precedence_* test below for the disposition). Static-field hygiene:
    // GameRoot.BootToHome is reset in SetUp AND TearDown so a failed test never bleeds the
    // static flag into an unrelated fixture.
    public sealed class DevBootOverrideTests
    {
        private GameRoot _root;
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "cm-devcap3-test", "devcap");
            Directory.CreateDirectory(_tmpDir);
            DevBootOverride.DirectoryOverride = _tmpDir;
            GameRoot.BootToHome = false;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.BootToHome = false;
            DevBootOverride.DirectoryOverride = null;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            var parent = Path.GetDirectoryName(_tmpDir);
            if (parent != null && Directory.Exists(parent)) Directory.Delete(parent, true);
        }

        // --- criterion 1: file present + bootToHome:true -> honored, announced, real
        // GameRoot.Launch() composes Home visible / board input gated / stack ["home"] ---
        [UnityTest]
        public IEnumerator FilePresent_BootToHomeTrue_ComposesDevScreenFlow_ViaRealLaunch()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": true}");
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                @"^DEVCAP_BOOT_OVERRIDE .+[/\\]devcap[/\\]boot\.json$"));
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Session, Is.Not.Null, "booted");
            Assert.That(_root.ScreensVisible, Is.True, "Home visible");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True, "Home shown on boot");
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Intro.IsVisible, Is.False, "Intro not shown yet");
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());

            // board input gated: the disc scan misses while Home is shown (the S2 conflict pin,
            // GameRootWiringTests.BoardInputGate_HomeShownUnderDevFlag_DiscScanMisses shape)
            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Home.PinPaintedRectPx.Contains(discPos), Is.False,
                "precondition: the disc sits outside the Home pin's rect — otherwise this test "
                + "cannot tell the board gate apart from the pin's chrome region claiming the tap");
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(-1),
                "criterion 1: board input is gated while the file-driven dev screen flow is up");
        }

        // --- criterion 3(a): bad JSON syntax -> shipped boot, loud fallback, no throw ---
        [UnityTest]
        public IEnumerator MalformedFile_BadJsonSyntax_ShippedBoot_LoudFallback_NoThrow()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{ not json");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^DEVCAP_BOOT_OVERRIDE_INVALID "));
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null, "still boots — never a crash or hang");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"), "the NORMAL shipped level booted");
            Assert.That(_root.ScreensVisible, Is.False, "no partial composition");
            Assert.That(_root.Home, Is.Null);
            Assert.That(_root.Intro, Is.Null);
            Assert.That(_root.Stack, Is.Null);
        }

        // --- criterion 3(b): well-formed JSON, WRONG key -> shipped boot, no throw, no log ---
        [UnityTest]
        public IEnumerator MalformedFile_WrongKey_ShippedBoot_NoThrow_NoLog()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"boot_to_home\": true}");
            // the SEAM_LOADED line is the NORMAL shipped-boot log (GameRoot.cs:131) — expected
            // here, never an "unexpected" log; only DEVCAP_BOOT_OVERRIDE_INVALID may not fire
            LogAssert.Expect(LogType.Log, "SEAM_LOADED content/levels/L001.json");
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null);
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"));
            Assert.That(_root.ScreensVisible, Is.False, "no partial composition");
            Assert.That(_root.Home, Is.Null);
            Assert.That(_root.Intro, Is.Null);
            Assert.That(_root.Stack, Is.Null);
            // a well-formed file that simply doesn't request the override is NOT an error case
            // (same shape as absence) — no DEVCAP_BOOT_OVERRIDE_INVALID line may fire
            LogAssert.NoUnexpectedReceived();
        }

        // --- criterion 3(c): well-formed JSON, explicit FALSE value -> shipped boot, no throw ---
        [UnityTest]
        public IEnumerator MalformedFile_ExplicitFalseValue_ShippedBoot_NoThrow_NoLog()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": false}");
            LogAssert.Expect(LogType.Log, "SEAM_LOADED content/levels/L001.json");
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null);
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"));
            Assert.That(_root.ScreensVisible, Is.False, "no partial composition");
            Assert.That(_root.Home, Is.Null);
            Assert.That(_root.Intro, Is.Null);
            Assert.That(_root.Stack, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        // --- criterion 4: Q-5 law reasserted — the boot-to-home file changes ONLY the screen
        // flow, never which level boots (shipped boot stays L001 even when the file composes
        // the dev screen flow on top of it) ---
        [UnityTest]
        public IEnumerator Q5Law_ShippedBootStaysL001_EvenWithBootOverrideFilePresentAndHonored()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": true}");
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.ScreensVisible, Is.True,
                "precondition: the file was honored — otherwise this test cannot tell apart "
                + "'the file never changes the level' from 'the file was never read at all'");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"),
                "Q-5 law: the boot-to-home file changes ONLY the screen flow, never which level "
                + "boots — that stays DevLevelOverride's job, never this file's");
        }

        // --- criterion 5: precedence — the static test flag's own power to compose is never
        // gated by the file (they are OR'd, independent checks, never AND'd): with the flag
        // true and the file malformed, GameRoot still composes AND the file's own INVALID log
        // still fires — proving neither check suppresses the other. See the report for the
        // full disposition (there is no genuine "force off" scenario for either seam, since
        // both are pure additive "turn on" triggers). ---
        [UnityTest]
        public IEnumerator Precedence_StaticFlagTrue_FileMalformed_StaticFlagStillComposes()
        {
            GameRoot.BootToHome = true;
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{ not json");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^DEVCAP_BOOT_OVERRIDE_INVALID "));
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.ScreensVisible, Is.True,
                "precedence: the static flag composes regardless of file validity — the file "
                + "check is independent and never gates the flag's own composition path");
        }
    }
}
