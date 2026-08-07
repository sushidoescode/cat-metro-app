using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-07 criterion 5 (R2-3, audit M-3), W-3: ResultsPanel.OnDisable mirrors OnDestroy —
    // the component-local half only (the panel stays UNATTACHED to GameRoot per Q-3). Direct
    // construction on a bare host (no Camera needed — EnsureViews falls back to
    // ScreenSpaceOverlay), state-controlled exactly like the CM-UX-04 ResultsPanelTests.cs
    // AttachControlled pattern, isolated from GameRoot so SetActive(false) here disables ONLY
    // the panel's own host, never a shared object.
    public sealed class ResultsPanelOnDisableTests
    {
        private GameObject _hostGo;
        private ResultsPanel _panel;
        private ChromeRegions _regions;
        private string _state;

        private ResultsPanel AttachControlled(string initial)
        {
            _hostGo = new GameObject("ResultsHost");
            _regions = new ChromeRegions();
            _state = initial;
            _panel = _hostGo.AddComponent<ResultsPanel>();
            _panel.Attach(() => _state, _regions);
            return _panel;
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostGo != null) Object.Destroy(_hostGo);
            _hostGo = null;
            _panel = null;
        }

        [UnityTest]
        public IEnumerator HostDeactivated_UnregistersTheCta_ReactivateAndReApply_NoDuplicateIdThrow()
        {
            var panel = AttachControlled("Won");
            yield return null;
            // #44 review F-1 (D-2): the precondition message format, applied here too.
            Assert.That(panel.IsVisible, Is.True,
                "precondition: Won shows the panel — otherwise this test proves nothing");
            Assert.That(_regions.Count, Is.EqualTo(1),
                "precondition: the CTA region registers with the panel — otherwise this test "
                + "proves nothing");

            // SetActive(false) directly on the host — NOT a state flip — the W-3 law under audit
            _hostGo.SetActive(false);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "SetActive(false) unregisters — mirrors OnDestroy (CM-UX-07 W-3, audit M-3)");
            Assert.That(_regions.TryResolve(panel.ChipPaintedRectPx.center, out _), Is.False,
                "the CTA resolves nothing while disabled");

            _hostGo.SetActive(true);
            yield return null; // Update()'s Apply() re-registers — _state is still "Won"
            Assert.That(_regions.Count, Is.EqualTo(1),
                "reactivating re-registers via the normal Apply() poll — no duplicate-id throw "
                + "crossed this line");
            Assert.That(_regions.TryResolve(panel.ChipPaintedRectPx.center, out var onTap), Is.True);
            Assert.That(onTap, Is.Not.Null);
        }
    }
}
