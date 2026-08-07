using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-07 criterion 5 (R2-3, audit M-3), W-1: HomeScreenView.OnDisable mirrors OnDestroy.
    // Direct construction (the CM-UX-06 HomeScreenTests.cs P-3 pattern) — the law is
    // component-local and needs no Bootstrap object.
    public sealed class HomeScreenViewOnDisableTests
    {
        private GameObject _canvasGo;
        private HomeScreenView _home;
        private ChromeRegions _regions;

        private HomeScreenView CreateShown()
        {
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _home = HomeScreenView.Create(canvas.transform);
            _home.Attach(_regions, () => false);
            _home.Show();
            return _home;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
            _canvasGo = null;
            _home = null;
        }

        [UnityTest]
        public IEnumerator HostDeactivated_UnregistersThePin_ReactivateAndReShow_NoDuplicateIdThrow()
        {
            CreateShown();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1), "pre-condition: the pin is registered");

            // SetActive(false) directly on the host — NOT Hide() — the W-1 law under audit
            _home.gameObject.SetActive(false);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "SetActive(false) unregisters — mirrors OnDestroy (CM-UX-07 W-1)");
            Assert.That(_regions.TryResolve(_home.PinPaintedRectPx.center, out _), Is.False,
                "the pin resolves nothing while disabled");

            _home.gameObject.SetActive(true);
            _home.Show();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1),
                "re-Show re-registers — no duplicate-id throw crossed this line");
            Assert.That(_regions.TryResolve(_home.PinPaintedRectPx.center, out var onTap), Is.True);
            Assert.That(onTap, Is.Not.Null);
        }
    }
}
