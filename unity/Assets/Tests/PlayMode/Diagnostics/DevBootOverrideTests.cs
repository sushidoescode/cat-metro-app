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
    // CM-DEVCAP3: the dev-only boot-to-home FILE seam — mirrors DevLevelOverrideTests' own
    // injection pattern (DirectoryOverride, an isolated temp dir per test). PR #52 round-1
    // review F1 correction: a real device boots via Awake self-init (a GameRoot placed in the
    // scene) — `GameRoot.Launch()` explicitly suppresses that self-init and calls
    // InitializeFromSeam directly instead (review N1), so it is NOT the same code path as the
    // device boot even though both currently reach the same InitializeFromSeam method. Tests
    // that must prove device fidelity (criterion 1, criterion 4/Q-5, the F3 combined-seam test)
    // use the SceneBoot() helper below — a bare AddComponent<GameRoot> outside the factories,
    // identical in shape to DevLevelOverrideTests.SceneBoot() — so a mutation that moves the
    // seam-read into Launch() itself (rather than InitializeFromSeam, which both Awake() and
    // Launch() reach) is caught RED instead of passing silently. Criteria 1 (honored, announced,
    // real screen-flow composition), 3 (four malformed classes: bad JSON syntax, a
    // well-formed-but-wrong key, an explicit false value, a present-but-non-boolean value — all
    // fall back to shipped boot with no throw), 4 (Q-5 law reasserted: the file NEVER changes
    // which level boots), 5 (the flag/file relationship, re-baselined below). Static-field
    // hygiene: GameRoot.BootToHome, DevBootOverride's DirectoryOverride, AND DevLevelOverride's
    // DirectoryOverride (F5: this fixture's symmetric isolation gap, since the F3 test needs
    // both seams pointed at the same temp dir anyway) are all reset in SetUp AND TearDown so a
    // failed test never bleeds state into an unrelated fixture, and no test here is exposed to
    // a real file on the developer's machine.
    //
    // CM-BOOT-HOME declared pin inversion (this file is one of the three named in the frozen
    // contract): criterion 3's "shipped boot with no partial composition" used to mean
    // "ScreensVisible stays False / Home stays Null" — CM-BOOT-HOME retires the file's return
    // value as ComposeScreenFlow's gate entirely (criterion 3), so Home now composes
    // UNCONDITIONALLY on every real boot regardless of the file's presence/validity/content.
    // Every malformed-file test below is inverted to its positive counterpart (ScreensVisible
    // True / Home Not.Null) while its actual file-parsing/logging assertions — this file's real
    // subject — stay byte-for-byte unchanged; each inversion is commented in place with why.
    public sealed class DevBootOverrideTests
    {
        private GameRoot _root;
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            CatMetro.Services.Purchases.PurchaseRuntime.ResetForTests();
            CatMetro.Services.Cosmetics.CosmeticRuntime.ResetForTests();
            _tmpDir = Path.Combine(Path.GetTempPath(), "cm-devcap3-test", "devcap");
            Directory.CreateDirectory(_tmpDir);
            DevBootOverride.DirectoryOverride = _tmpDir;
            DevLevelOverride.DirectoryOverride = _tmpDir; // F5: symmetric isolation
            GameRoot.BootToHome = false;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.BootToHome = false;
            DevBootOverride.DirectoryOverride = null;
            DevLevelOverride.DirectoryOverride = null; // F5
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            CatMetro.Services.Cosmetics.CosmeticRuntime.ResetForTests();
            CatMetro.Services.Purchases.PurchaseRuntime.ResetForTests();
            var parent = Path.GetDirectoryName(_tmpDir);
            if (parent != null && Directory.Exists(parent)) Directory.Delete(parent, true);
        }

        private GameRoot SceneBoot()
        {
            // device path: a bare AddComponent outside the factories → Awake self-initializes
            // (identical shape to DevLevelOverrideTests.SceneBoot() — PR #52 F1)
            var go = new GameObject("GameRoot-scene-boot");
            return go.AddComponent<GameRoot>();
        }

        // CM-SEAMS: N nested `{"<key>": ...}` wraps around an innermost `true` — depth is N
        // regardless of key content, so key length is a free knob for controlling how long the
        // resulting JsonReaderException's Path string gets once MaxDepth throws.
        private static string DeepNestedJson(int levels, string key)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < levels; i++) sb.Append('{').Append('"').Append(key).Append("\":");
            sb.Append("true");
            for (int i = 0; i < levels; i++) sb.Append('}');
            return sb.ToString();
        }

        private string _capturedInvalidLog;
        private void CaptureInvalidLog(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith("DEVCAP_BOOT_OVERRIDE_INVALID")) _capturedInvalidLog = condition;
        }

        private static string DemoJson() =>
            File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory().Replace("/unity", ""),
                "tests/fixtures/devcap/demo-level.json"));

        // --- criterion 1: file present + bootToHome:true -> honored, announced, real device
        // boot (SceneBoot -> Awake -> InitializeFromSeam) composes Home visible / board input
        // gated / stack ["home"]. CM-BOOT-HOME note: composition itself is unconditional on
        // real boot now (criterion 1) — this pin's remaining discriminating power is the
        // "announced" half (the LogAssert.Expect below), proving the file is still genuinely
        // read/parsed/logged even though its result no longer gates anything. Left as a
        // positive assertion (never inverted, nothing to invert — it was already True). ---
        [UnityTest]
        public IEnumerator FilePresent_BootToHomeTrue_ComposesDevScreenFlow_ViaSceneBoot()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": true}");
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
                @"^DEVCAP_BOOT_OVERRIDE .+[/\\]devcap[/\\]boot\.json$"));
            _root = SceneBoot();
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

        // --- CM-BOOT-HOME declared pin inversion (criterion 3(a)): bad JSON syntax -> the
        // FILE's own fallback is still loud and the LEVEL is still the normal shipped one
        // (Q-5, unchanged); "ScreensVisible False / Home Null" is INVERTED here — CM-BOOT-HOME
        // criterion 3 retired the file's return value as a compose gate, so Home now composes
        // unconditionally on the real Launch() seam regardless of the file's validity. This is
        // a re-baseline, not a loosening: the file-parsing/logging assertions below (the actual
        // point of this test) are byte-for-byte unchanged. ---
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
            Assert.That(_root.ScreensVisible, Is.True,
                "CM-BOOT-HOME criterion 1: Home composes unconditionally on real boot — the "
                + "malformed file's fallback affects ONLY whether the file's own request is "
                + "honored, never whether the shipped default composes");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Stack, Is.Not.Null);
        }

        // --- CM-BOOT-HOME declared pin inversion (criterion 3(b)): well-formed JSON, WRONG key
        // -> the file-parsing/no-throw/no-log assertions are unchanged; ScreensVisible/Home
        // inverted for the same reason as the pin above (compose is unconditional now). ---
        [UnityTest]
        public IEnumerator MalformedFile_WrongKey_ShippedBoot_NoThrow_NoLog()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"boot_to_home\": true}");
            // the SEAM_LOADED line is the NORMAL shipped-boot log (GameRoot.cs) — expected
            // here, never an "unexpected" log; only DEVCAP_BOOT_OVERRIDE_INVALID may not fire
            LogAssert.Expect(LogType.Log, "SEAM_LOADED content/levels/L001.json");
            // Every real boot also emits the exact independent numeric catalogue read-back.
            LogAssert.Expect(LogType.Log, CosmeticBootWiringTests.ExpectedDiagnostic);
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null);
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"));
            Assert.That(_root.ScreensVisible, Is.True,
                "CM-BOOT-HOME criterion 1: Home composes unconditionally on real boot");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Stack, Is.Not.Null);
            // a well-formed file that simply doesn't request the override is NOT an error case
            // (same shape as absence) — no DEVCAP_BOOT_OVERRIDE_INVALID line may fire
            LogAssert.NoUnexpectedReceived();
        }

        // --- CM-BOOT-HOME declared pin inversion (criterion 3(c)): well-formed JSON, explicit
        // FALSE value -> the same inversion, same rationale as the pin above. ---
        [UnityTest]
        public IEnumerator MalformedFile_ExplicitFalseValue_ShippedBoot_NoThrow_NoLog()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": false}");
            LogAssert.Expect(LogType.Log, "SEAM_LOADED content/levels/L001.json");
            LogAssert.Expect(LogType.Log, CosmeticBootWiringTests.ExpectedDiagnostic);
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null);
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"));
            Assert.That(_root.ScreensVisible, Is.True,
                "CM-BOOT-HOME criterion 1: Home composes unconditionally on real boot — even an "
                + "explicit bootToHome:false in the file no longer suppresses it (the file's "
                + "return value is retired as a compose gate, criterion 3)");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Stack, Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        // --- CM-BOOT-HOME declared pin inversion (criterion 3(d), PR #52 F2): well-formed
        // JSON, PRESENT key, NON-BOOLEAN value -> loud fallback (same discipline as bad JSON
        // syntax) — this is the diagnostic vacuum a human hits writing the file via a quoted
        // shell one-liner ("bootToHome": "true"). Same inversion as the pins above. ---
        [UnityTest]
        public IEnumerator MalformedFile_NonBooleanValue_ShippedBoot_LoudFallback_NoThrow()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": \"true\"}");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^DEVCAP_BOOT_OVERRIDE_INVALID "));
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null, "still boots — never a crash or hang");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"), "the NORMAL shipped level booted");
            Assert.That(_root.ScreensVisible, Is.True,
                "CM-BOOT-HOME criterion 1: Home composes unconditionally on real boot");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Stack, Is.Not.Null);
        }

        // --- criterion 4: Q-5 law reasserted — the boot-to-home file changes ONLY the screen
        // flow, never which level boots (shipped boot stays L001 even when the file composes
        // the dev screen flow on top of it). SceneBoot (PR #52 F1): must prove this on the REAL
        // device path, not just via Launch(). CM-BOOT-HOME note: ScreensVisible is True here
        // unconditionally now (Home composes on every real boot, criterion 1) rather than
        // specifically BECAUSE the file was honored — the precondition comment below is kept
        // (still literally true) but no longer discriminates the file read from its absence;
        // the level-id assertion (this test's actual point) is untouched either way. ---
        [UnityTest]
        public IEnumerator Q5Law_ShippedBootStaysL001_EvenWithBootOverrideFilePresentAndHonored()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": true}");
            _root = SceneBoot();
            yield return null;
            Assert.That(_root.ScreensVisible, Is.True,
                "precondition: Home composes on every real boot now (CM-BOOT-HOME criterion 1) "
                + "— kept as a sanity precondition, not a file-specific discriminator anymore");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"),
                "Q-5 law: the boot-to-home file changes ONLY the screen flow, never which level "
                + "boots — that stays DevLevelOverride's job, never this file's");
        }

        // --- PR #52 F3: the COMBINED seam — devcap/level.json AND devcap/boot.json both
        // present (the real demo workflow: adb push D001 + boot.json into the SAME devcap dir).
        // CM-BOOT-HOME note: Home now composes over EVERY real boot, dev-level-overridden or
        // not (InitializeFromSeam composes on both the dev-level branch and the shipped branch,
        // criterion 1) — so "composes over the demo level" is no longer specifically the boot-
        // to-home file's doing, but the level-id + composed-Home assertions below still pin the
        // real point: the dev-level seam and the screen-flow compose call cooperate correctly
        // (a mutation reordering InitializeFromSeam's two branches, or dropping the dev-level
        // branch's own compose call, is still caught here). ---
        [UnityTest]
        public IEnumerator CombinedSeams_LevelOverrideAndBootOverride_BothPresent_ComposesHomeOverDemoLevel()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "level.json"), DemoJson());
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), "{\"bootToHome\": true}");
            _root = SceneBoot();
            yield return null;

            Assert.That(_root.Session, Is.Not.Null, "booted");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("D001"),
                "precondition: the injected DEV LEVEL booted, not the shipped L001 — otherwise "
                + "this test cannot tell apart 'both seams composed together' from 'only one did'");
            Assert.That(_root.ScreensVisible, Is.True,
                "Home composes over the dev-level-overridden boot too (CM-BOOT-HOME criterion 1 "
                + "reaches BOTH InitializeFromSeam branches, not just the shipped one)");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True, "Home shown on boot, over the demo level");
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
        }

        // --- CM-BOOT-HOME re-baseline (criterion 5's precedence question is now moot): the
        // static flag and the file used to be two INDEPENDENT, OR'd triggers for composing —
        // criterion 3 retired BOTH as compose gates (compose is unconditional on real boot now,
        // gated only by the dev skip-hatch, DevSkipShippedHome). This test's assertion is
        // UNCHANGED (True) and still a valid regression pin — a mutation that reintroduces a
        // gate condition on the file's return value, or that makes a malformed file suppress
        // composition, would turn it RED — but the "precedence" framing is retired: there is no
        // longer a second gate for the flag to take precedence OVER. ---
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
                "CM-BOOT-HOME: compose is unconditional on real boot regardless of BootToHome's "
                + "value or the file's validity — the malformed file's own INVALID log (asserted "
                + "above) still fires independently, proving the file is still read/parsed even "
                + "though its result no longer gates anything");
        }

        // --- CM-SEAMS (security-S1): pre-read size cap. An oversize but otherwise WELL-FORMED
        // file (bootToHome:true, padded past CONTENT_MAX_FILE_BYTES) must be rejected BEFORE
        // parsing — the log regex below (unchanged) is the mutation-provable proof: a size
        // check that fires too late (or not at all) changes the log wording, or lets the file's
        // content actually get parsed instead of short-circuited. CM-BOOT-HOME declared pin
        // inversion: "never honored" used to mean "Home never composes" (ScreensVisible False);
        // that meaning is retired — CM-BOOT-HOME criterion 3 makes Home compose unconditionally
        // on real boot regardless of this file, so "never honored" now means exactly what the
        // log regex proves (the oversize content is rejected pre-parse, never read as JSON, and
        // no DEVCAP_BOOT_OVERRIDE success line can fire) — not whether Home appears. ---
        [UnityTest]
        public IEnumerator OversizeFile_ExceedsContentMaxFileBytes_LoudFallback_NeverHonored()
        {
            string prefix = "{\"bootToHome\": true, \"pad\": \"";
            string suffix = "\"}";
            int padLen = ContentBounds.CONTENT_MAX_FILE_BYTES + 1000 - prefix.Length - suffix.Length;
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"),
                prefix + new string('x', padLen) + suffix);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                @"^DEVCAP_BOOT_OVERRIDE_INVALID .+ — payload \d+ bytes exceeds CONTENT_MAX_FILE_BYTES — booting the shipped path$"));
            _root = SceneBoot();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null, "still boots — never a crash or hang");
            Assert.That(_root.ScreensVisible, Is.True,
                "CM-BOOT-HOME criterion 1: Home composes unconditionally on real boot — the "
                + "oversize file's own content is proven UNPARSED by the log regex above (the "
                + "success line 'DEVCAP_BOOT_OVERRIDE <path>' never fires), never by whether "
                + "Home appears");
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Stack, Is.Not.Null);
        }

        // --- CM-SEAMS (security-S1): the parse itself is depth-bounded at
        // ContentBounds.CONTENT_MAX_JSON_DEPTH (16) — Newtonsoft's own default is 64, so a
        // 20-level-deep file is accepted by the OLD unguarded JObject.Parse (it would then read
        // the nested JObject as the "bootToHome" value and reject it as non-boolean — a
        // DIFFERENT, MaxDepth-unrelated message) and rejected by the NEW depth-bounded parse
        // with a message naming MaxDepth. The regex pins the CAUSE, not just "some error".
        // CM-BOOT-HOME declared pin inversion: ScreensVisible is True now (Home composes
        // unconditionally on real boot, criterion 1) — this test's own proving power lives
        // entirely in the MaxDepth-naming log regex above, unchanged. ---
        [UnityTest]
        public IEnumerator DeeplyNestedFile_ExceedsContentMaxJsonDepth_LoudFallback_NamesMaxDepth()
        {
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), DeepNestedJson(20, "bootToHome"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                $@"^DEVCAP_BOOT_OVERRIDE_INVALID .+MaxDepth of {ContentBounds.CONTENT_MAX_JSON_DEPTH} has been exceeded"));
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Session, Is.Not.Null, "still boots — never a crash or hang");
            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"), "the NORMAL shipped level booted");
            Assert.That(_root.ScreensVisible, Is.True,
                "CM-BOOT-HOME criterion 1: Home composes unconditionally on real boot");
        }

        // --- CM-SEAMS (pins the security-S2 sanitization landed in PR #52 — code-R1: it was
        // unpinned): a deeply-nested file under a deliberately long key name blows the resulting
        // JsonReaderException's Path string well past 200 characters. Deleting Sanitize's
        // truncation (or the ellipsis append) makes `sanitized.Length` diverge from exactly 201
        // (200 chars + the ellipsis marker) — this assertion is RED the moment either half of
        // Sanitize's truncation is removed. ---
        [UnityTest]
        public IEnumerator Sanitization_OversizeExceptionMessage_LogLineTruncatedTo200PlusEllipsis()
        {
            string longKey = new string('k', 60);
            File.WriteAllText(Path.Combine(_tmpDir, "boot.json"), DeepNestedJson(20, longKey));
            _capturedInvalidLog = null;
            UnityEngine.Application.logMessageReceived += CaptureInvalidLog;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "^DEVCAP_BOOT_OVERRIDE_INVALID "));
            try
            {
                _root = GameRoot.Launch();
                yield return null;
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= CaptureInvalidLog;
            }
            Assert.That(_capturedInvalidLog, Is.Not.Null, "the INVALID line must have fired");
            string path = Path.Combine(_tmpDir, "boot.json");
            string prefix = "DEVCAP_BOOT_OVERRIDE_INVALID " + path + " — ";
            string suffix = " — booting the shipped path";
            Assert.That(_capturedInvalidLog.StartsWith(prefix), Is.True,
                "unexpected log shape: " + _capturedInvalidLog);
            Assert.That(_capturedInvalidLog.EndsWith(suffix), Is.True,
                "unexpected log shape: " + _capturedInvalidLog);
            string sanitized = _capturedInvalidLog.Substring(
                prefix.Length, _capturedInvalidLog.Length - prefix.Length - suffix.Length);
            Assert.That(sanitized, Does.Contain("…"),
                "security-S2 pin: an oversize exception message must be TRUNCATED, not logged whole");
            Assert.That(sanitized.Length, Is.EqualTo(201),
                "200-char cap + the ellipsis marker — the exact length a mutation that deletes "
                + "or shortens the truncation would diverge from");
        }
    }
}
