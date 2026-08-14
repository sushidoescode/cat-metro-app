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
    // CM-UX-07 criterion 6 / CM-BOOT-HOME: the screen flow (Home/Intro/ScreenStack) GameRoot
    // composes. CM-BOOT-HOME retired GameRoot.BootToHome as the compose gate — Home now
    // composes by default on every real boot (Launch()/Awake()) and never at all through
    // LaunchWith (the gameplay-fixture seam), so this file's tests below assert the NEW
    // topology directly rather than toggling a flag. BootToHome itself is still reset in
    // SetUp/TearDown as harmless static-field hygiene (nothing reads it for composition
    // anymore, but out-of-scope fixtures elsewhere still touch it).
    //
    // CM-DEVCAP3 addendum: DevBootOverride.DirectoryOverride is ALSO reset in SetUp/TearDown to
    // an isolated empty temp dir (PR #52 F7 correction: most tests below boot via LaunchWith — a
    // path the file seam never touches by design, see DevBootOverrideTests — but the
    // criterion-2 extension test boots via the REAL GameRoot.Launch() seam, which DOES read the
    // file; the isolation keeps this whole file immune to a stray real devcap/boot.json on the
    // developer's machine either way, and gives that one test a guaranteed-absent file to boot
    // against).
    public sealed class DevScreenFlowTests
    {
        private GameRoot _root;
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            GameRoot.BootToHome = false;
            _tmpDir = Path.Combine(Path.GetTempPath(), "cm-devcap3-devscreenflow-test", "devcap");
            Directory.CreateDirectory(_tmpDir);
            DevBootOverride.DirectoryOverride = _tmpDir;
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

        private static ImportedLevel Fixture()
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(FixtureJson()));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        private static GameObject FindByName(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        // --- CM-BOOT-HOME declared pin inversion: LaunchWith itself never composes a screen
        // flow (retitled from "the flag OFF pin: ... (shipped boot)" — LaunchWith is the
        // ~12-gameplay-fixture seam, NOT shipped boot anymore; shipped boot's positive Home
        // proof now lives in ShippedBootHomeTests.cs, which drives the REAL Launch() seam).
        // The assertions below are UNCHANGED (still true under the new topology, for a
        // different reason): CM-BOOT-HOME EDIT 2 moved ComposeScreenFlow's call site from Wire
        // to InitializeFromSeam, and LaunchWith calls Wire directly, bypassing
        // InitializeFromSeam entirely — so it is now structurally impossible for LaunchWith to
        // compose Home, independent of any flag's value (BootToHome is no longer even read for
        // this purpose). This re-baselines the pin from "the flag is off" to "this seam never
        // reaches the composer at all" — a strictly stronger guarantee. ---

        [UnityTest]
        public IEnumerator LaunchWith_NeverComposesAScreenFlow_GameplayFixtureSeamPin()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            Assert.That(_root.Home, Is.Null);
            Assert.That(_root.Intro, Is.Null);
            Assert.That(_root.Stack, Is.Null);
            Assert.That(_root.ScreensVisible, Is.False);
            Assert.That(FindByName(_root.gameObject, "ScreensCanvas"), Is.Null,
                "no ScreensCanvas exists on the LaunchWith seam");
            Assert.That(
                _root.gameObject.GetComponentInChildren<CatMetro.Presentation.Screens.HomeScreenView>(true),
                Is.Null, "zero Home objects constructed");
            Assert.That(
                _root.gameObject.GetComponentInChildren<CatMetro.Presentation.Screens.LevelIntroSheet>(true),
                Is.Null, "zero Intro objects constructed");

            // board input is unaffected (positive control: the merged behavior still holds)
            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(0));
        }

        // --- CM-BOOT-HOME declared pin inversion (the headline one): the REAL Launch() seam —
        // formerly "BootOverrideFileAbsent_ZeroScreenObjectsConstructed_ShippedBootPin_
        // ViaRealLaunch", asserting Home/Intro/Stack ARE null on shipped boot (the OLD Q-5
        // design this contract's frozen contract explicitly supersedes). CM-BOOT-HOME criterion
        // 1 makes Home the shipped default: the REAL Launch() seam, with no boot.json present
        // (so DevBootOverride's file seam contributes nothing) and the dev skip-hatch at its
        // default false, now composes Home unconditionally. Inverted to its positive
        // counterpart — not deleted, not loosened — proving the NEW topology at the exact same
        // seam the old pin proved the OLD one at. ---

        [UnityTest]
        public IEnumerator RealLaunchSeam_NoBootOverrideFile_StillComposesHome_ShippedDefaultPin()
        {
            Assert.That(File.Exists(Path.Combine(_tmpDir, "boot.json")), Is.False,
                "precondition: no boot.json exists in the injected devcap dir — otherwise this "
                + "test proves nothing about the file-absent case");
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Session.Level.Dto.Id, Is.EqualTo("L001"), "shipped L001 board");
            Assert.That(_root.Home, Is.Not.Null,
                "CM-BOOT-HOME criterion 1: Home composes on the real seam, file-absent or not");
            Assert.That(_root.Home.IsVisible, Is.True, "Home shown on boot");
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Stack, Is.Not.Null);
            Assert.That(_root.ScreensVisible, Is.True);
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
            Assert.That(FindByName(_root.gameObject, "ScreensCanvas"), Is.Not.Null,
                "the ScreensCanvas exists on the real boot seam");

            // CM-BOOT-HOME criterion 2 (the tick-0 hold): board input is now gated while Home
            // is up — the opposite of this pin's old "board input is unaffected" control, which
            // proved the pre-CM-BOOT-HOME design where a dev screen flow never mounted here.
            var discPos2 = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Home.PinPaintedRectPx.Contains(discPos2), Is.False,
                "precondition: the disc sits outside the Home pin's rect — otherwise this test "
                + "cannot tell the board gate apart from the pin's chrome region claiming the tap");
            Assert.That(_root.Input.HandleTapAtScreen(discPos2), Is.EqualTo(-1),
                "board input is gated while Home is up (criterion 2c)");
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                "criterion 2: the sim is held at tick 0 while Home is shown");
        }

        // --- CM-BOOT-HOME declared pin inversion: the round-trip flow, re-seamed onto the REAL
        // Launch() seam (formerly "BootToHome_ComposesHomeIntroFlow_..." via
        // `GameRoot.BootToHome = true; LaunchWith(...)`) — that seam is retired, not merely
        // re-gated (see the pin above): LaunchWith can no longer reach the composer under ANY
        // flag value. This is the shipped-default topology now (criterion 1), so the round trip
        // is proven through GameRoot.Launch() with the dev skip-hatch left at its default false
        // — no test-side override needed. Every assertion below is UNCHANGED in intent (Home ->
        // pin tap -> Intro -> Play tap -> Playing, region-count/visibility discriminators all
        // preserved); only the boot seam and the loaded level (L001 instead of the local
        // fixture — the level's own DTO fields feed every assertion, so this substitution is
        // load-bearing-free) changed. ---

        [UnityTest]
        public IEnumerator
            RealLaunchSeam_ComposesHomeIntroFlow_PinTapToIntro_PlayTapToPlaying_StackRoundTrips()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.ScreensVisible, Is.True);
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True, "Home shown on boot");
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Intro.IsVisible, Is.False, "Intro not shown yet");
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                "criterion 2: the sim is held at tick 0 while Home is shown");
            // F1/F2 fix (round-1 review): baseline region count while only Home's pin is
            // registered — used below to prove the pin's region is genuinely gone (not just
            // visually hidden) once LevelSelected fires, without hardcoding a literal count.
            int regionBaseline = _root.Input.Regions.Count;

            var canvasGo = FindByName(_root.gameObject, "ScreensCanvas");
            Assert.That(canvasGo, Is.Not.Null);
            var canvas = canvasGo.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(canvas.sortingOrder, Is.EqualTo(120), "above the CM-UX-04 results canvas (110)");

            // board input is gated while Home is shown (criterion 2c re-confirmed through the
            // composed flow, not a test-side override)
            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            // #44 review F-1 (D-2): if the disc drifted UNDER Home's pin, the tap would resolve
            // as -3 (a chrome region claiming it) rather than -1 (the gate closing it).
            Assert.That(_root.Home.PinPaintedRectPx.Contains(discPos), Is.False,
                "precondition: the disc sits outside the Home pin's rect — otherwise this test "
                + "cannot tell the board gate apart from the pin's chrome region claiming the tap");
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(-1));

            // tap the pin: Intro shows with the substituted goal count from the loaded level
            int tapResult = _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            Assert.That(tapResult, Is.EqualTo(-3), "the pin is a chrome region");
            Assert.That(_root.Intro.IsVisible, Is.True);
            Assert.That(_root.Intro.NameText, Is.EqualTo(_root.Session.Level.Dto.Name));
            Assert.That(_root.Intro.GoalText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("intro.goal")
                    .Replace("{count}", _root.Session.Level.Dto.Win.Deliveries.ToString())),
                "no new I/O — the count is substituted from the already-loaded level");
            CollectionAssert.AreEqual(new[] { "home", "intro" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.ScreensVisible, Is.True, "still up — the stack is non-empty");

            // F1/F2 fix (round-1 review): the old precondition here compared the SAME rect
            // computation to itself — LevelIntroSheet.LayoutChip sets the chip rect to
            // HudBands.ThumbBand(Screen.safeArea) and HomeLayout.PinRect centers the pin in
            // that IDENTICAL band, so "chip.Contains(pin.center)" reduces to
            // "band.Contains(band.center)": true by construction for any dpi/safe-area/device,
            // and its rationale was false besides — Home.Hide() (GameRoot.ComposeScreenFlow)
            // has already unregistered the pin by this point, so no tie-break ever occurs here.
            // Replaced with asserts that read SUT state and can fail under the plausible
            // mutation of deleting `Home.Hide();` (the PR's headline in-slice fix).
            Assert.That(_root.Home.IsVisible, Is.False,
                "F2 discriminator: LevelSelected must call Home.Hide() — otherwise Home (and "
                + "its title) is still visible bleeding through the Intro sheet right now");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(regionBaseline),
                "F1 discriminator: the pin's region is genuinely gone, not just visually "
                + "hidden — Home's pin unregisters (-1) while Intro's chip registers (+1), "
                + "netting back to the boot baseline; under the delete-Home.Hide mutation this "
                + "reads baseline + 1 (both regions live — the tie-break geometry this test "
                + "must actually exercise)");

            // tap Play: both hide, stack empties, board input returns, and CM-BOOT-HOME
            // criterion 2's tick-0 hold lifts — the sim now advances on subsequent frames.
            int playResult = _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            Assert.That(playResult, Is.EqualTo(-3), "the Play chip is a chrome region");
            Assert.That(_root.Intro.IsVisible, Is.False);
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.ScreensVisible, Is.False, "the stack popped to empty");
            CollectionAssert.AreEqual(new string[0], _root.Stack.ToBreadcrumb());

            var discAgain = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(discAgain), Is.EqualTo(0),
                "input is live once the screens clear");
        }

        private static string FixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T914"", ""name"": ""Dev Screen Flow Fixture"", ""seed"": 914,
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
}
