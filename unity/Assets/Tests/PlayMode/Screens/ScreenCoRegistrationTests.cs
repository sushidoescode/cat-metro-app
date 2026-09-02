using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-06 follow-up (#42 review F3): the Home pin and the LevelIntro Play chip were the
    // first two SHIPPED registrars — and no test ever put them in ONE registry. At equal
    // priority the tie-break resolves to the EARLIEST registration (the pin), so a Play tap
    // dead-center on the modal sheet re-fired LevelSelected and the game could not be started
    // from the sheet. The modal-over-parent law: a sheet shown over a screen registers at a
    // STRICTLY higher priority. Red-first against the equal-priority tree.
    public sealed class ScreenCoRegistrationTests
    {
        private GameObject _homeHost;
        private GameObject _sheetHost;

        [TearDown]
        public void TearDown()
        {
            if (_homeHost != null) Object.Destroy(_homeHost);
            if (_sheetHost != null) Object.Destroy(_sheetHost);
        }

        // #42 review F1: the painted-rect asserts elsewhere compare a cached input to the
        // expression that assigned it — a tautology that misses any ApplyPx defect. These
        // read the RectTransform's WORLD CORNERS back (overlay canvas: world == screen px)
        // and assert them against the registered claim. Motion-off fixture: the pulse must
        // not skew the corners.
        [UnityTest]
        public IEnumerator PaintedRects_ReadBackFromWorldCorners_MatchTheRegisteredClaims()
        {
            var regions = new ChromeRegions();
            _homeHost = new GameObject("home-canvas");
            var canvas = _homeHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var home = HomeScreenView.Create(canvas.transform);
            home.Attach(regions, () => true); // motion OFF: static geometry
            home.Show();
            yield return null;

            var pin = home.PinTransform;
            Assert.That(pin, Is.Not.Null, "the read-back needs the real transform");
            var corners = new Vector3[4];
            pin.GetWorldCorners(corners);
            var measured = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            var claimed = home.PinPaintedRectPx;
            Assert.That(measured.x, Is.EqualTo(claimed.x).Within(0.5f),
                "painted-where-claimed: a drifting ApplyPx must go red here, not in a frame eyeball");
            Assert.That(measured.y, Is.EqualTo(claimed.y).Within(0.5f));
            Assert.That(measured.width, Is.EqualTo(claimed.width).Within(0.5f));
            Assert.That(measured.height, Is.EqualTo(claimed.height).Within(0.5f));

            _sheetHost = new GameObject("sheet-canvas");
            var sheetCanvas = _sheetHost.AddComponent<Canvas>();
            sheetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sheet = LevelIntroSheet.Create(sheetCanvas.transform);
            sheet.Attach(regions);
            sheet.Show("First Switch", 3);
            yield return null;

            var chip = sheet.ChipTransform;
            Assert.That(chip, Is.Not.Null);
            chip.GetWorldCorners(corners);
            var chipMeasured = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            var chipClaimed = sheet.PlayChipRectPx;
            Assert.That(chipMeasured.x, Is.EqualTo(chipClaimed.x).Within(0.5f));
            Assert.That(chipMeasured.y, Is.EqualTo(chipClaimed.y).Within(0.5f));
            Assert.That(chipMeasured.width, Is.EqualTo(chipClaimed.width).Within(0.5f));
            Assert.That(chipMeasured.height, Is.EqualTo(chipClaimed.height).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator PlayTapOnTheSheet_OverHome_FiresPlayNotLevelSelected()
        {
            var regions = new ChromeRegions();

            _homeHost = new GameObject("home-canvas");
            var homeCanvas = _homeHost.AddComponent<Canvas>();
            homeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var home = HomeScreenView.Create(homeCanvas.transform);
            int levelSelected = 0;
            home.LevelSelected = () => levelSelected++;
            home.Attach(regions, () => false);
            home.Show();
            yield return null;

            // Unity's batch window can pair a small 640x480 viewport with a high reported DPI,
            // pushing the stacked Home routes above the sheet's lower-quarter chip. This test
            // owns modal priority, not live DPI binding, so use the project's injected reference
            // scale to create a deterministic overlap; layout tests cover the runtime binding.
            home.LayoutForViewport(Screen.safeArea, HudBands.FallbackDpi);

            // the modal opens OVER Home — a sheet naturally does not hide its parent
            _sheetHost = new GameObject("sheet-canvas");
            var sheetCanvas = _sheetHost.AddComponent<Canvas>();
            sheetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sheet = LevelIntroSheet.Create(sheetCanvas.transform);
            int playRequested = 0;
            sheet.PlayRequested = () => playRequested++;
            sheet.Attach(regions);
            sheet.Show("First Switch", 3);
            yield return null;
            Assert.That(regions.Count, Is.EqualTo(2), "both views registered into ONE registry");

            // Use the actual intersection rather than assuming either region's center lies in
            // the other. That keeps this test about priority when CTA geometry evolves.
            var homeRect = home.PinPaintedRectPx;
            var sheetRect = sheet.PlayChipRectPx;
            var overlap = Rect.MinMaxRect(
                Mathf.Max(homeRect.xMin, sheetRect.xMin),
                Mathf.Max(homeRect.yMin, sheetRect.yMin),
                Mathf.Min(homeRect.xMax, sheetRect.xMax),
                Mathf.Min(homeRect.yMax, sheetRect.yMax));
            Assert.That(overlap.width, Is.GreaterThan(0f),
                "precondition: Home and sheet regions overlap horizontally");
            Assert.That(overlap.height, Is.GreaterThan(0f),
                "precondition: Home and sheet regions overlap vertically");
            var overlapPoint = overlap.center;
            Assert.That(regions.TryResolve(overlapPoint, out var onTap), Is.True);
            onTap();
            Assert.That(playRequested, Is.EqualTo(1),
                "the MODAL wins the overlap — the game must be startable from the sheet");
            Assert.That(levelSelected, Is.EqualTo(0),
                "the parent screen's pin must not steal the modal's tap");

            // Positive control: with the sheet hidden, the SAME point reaches the pin,
            // proving the pin region is alive and the modal's win was priority, not pin death.
            sheet.Hide();
            yield return null;
            Assert.That(regions.Count, Is.EqualTo(1), "sheet unregistered on hide");
            Assert.That(home.PinPaintedRectPx.Contains(overlapPoint), Is.True);
            Assert.That(regions.TryResolve(overlapPoint, out var pinTap), Is.True);
            pinTap();
            Assert.That(levelSelected, Is.EqualTo(1), "the pin fires once the modal is gone");
        }
    }
}
