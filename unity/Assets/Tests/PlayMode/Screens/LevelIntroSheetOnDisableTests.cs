using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-07 criterion 5 (R2-3, audit M-3), W-2: LevelIntroSheet.OnDisable mirrors OnDestroy.
    // Direct construction (the CM-UX-06 LevelIntroSheetTests.cs P-3 pattern) — the law is
    // component-local and needs no Bootstrap object.
    public sealed class LevelIntroSheetOnDisableTests
    {
        private GameObject _canvasGo;
        private LevelIntroSheet _sheet;
        private ChromeRegions _regions;

        private LevelIntroSheet CreateShown(string name = "First Switch", int deliveries = 3)
        {
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _sheet = LevelIntroSheet.Create(canvas.transform);
            _sheet.Attach(_regions);
            _sheet.Show(name, deliveries);
            return _sheet;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            _canvasGo = null;
            _sheet = null;
        }

        [UnityTest]
        public IEnumerator HostDeactivated_UnregistersThePlayChip_ReactivateAndReShow_NoDuplicateIdThrow()
        {
            CreateShown();
            yield return null;
            // #44 review F-1 (D-2): the precondition message format, applied here too.
            Assert.That(_regions.Count, Is.EqualTo(1),
                "precondition: the Play chip is registered — otherwise this test proves nothing");

            // SetActive(false) directly on the host — NOT Hide() — the W-2 law under audit
            _sheet.gameObject.SetActive(false);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "SetActive(false) unregisters — mirrors OnDestroy (CM-UX-07 W-2)");
            Assert.That(_regions.TryResolve(_sheet.PlayChipRectPx.center, out _), Is.False,
                "the chip resolves nothing while disabled");

            _sheet.gameObject.SetActive(true);
            _sheet.Show("First Switch", 3);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1),
                "re-Show re-registers — no duplicate-id throw crossed this line");
            Assert.That(_regions.TryResolve(_sheet.PlayChipRectPx.center, out var onTap), Is.True);
            Assert.That(onTap, Is.Not.Null);
        }
    }
}
