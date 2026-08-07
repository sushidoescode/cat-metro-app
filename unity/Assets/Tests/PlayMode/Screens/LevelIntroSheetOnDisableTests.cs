using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-07 criterion 5 (R2-3, audit M-3), W-2: LevelIntroSheet.OnDisable mirrors OnDestroy.
    // #46 review F4 follow-up: OnEnable mirrors OnDisable too — a host reactivated directly
    // (SetActive(true), never through Show()) must re-register, or a visible Play chip sits
    // inert over an unstartable game (the ghost-affordance asymmetry F4 names). Direct
    // construction (the CM-UX-06 LevelIntroSheetTests.cs P-3 pattern) — the law is
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

        private LevelIntroSheet CreateAttachedButNotShown()
        {
            _canvasGo = new GameObject("TestCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _sheet = LevelIntroSheet.Create(canvas.transform);
            _sheet.Attach(_regions);
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
        public IEnumerator HostDeactivated_ReactivatedWithNoShowCall_ReregistersThePlayChip()
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

            // #46 review F4: NO Show() call on this leg — strengthened from the earlier version
            // of this test, which papered over the missing OnEnable path with a manual re-Show.
            // Show()'s own registration is already exercised by the precondition assert above.
            _sheet.gameObject.SetActive(true);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1),
                "SetActive(true) alone re-registers — OnEnable mirrors OnDisable (F4); no "
                + "duplicate-id throw crossed this line either");
            Assert.That(_regions.TryResolve(_sheet.PlayChipRectPx.center, out var onTap), Is.True,
                "the chip resolves again with no Show() call — closes the ghost-affordance "
                + "asymmetry F4 names (a visible chip over an unstartable game)");
            Assert.That(onTap, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Hidden_ThenHostReactivatedWithNoShowCall_StaysUnregistered()
        {
            CreateShown();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1),
                "precondition: the Play chip is registered — otherwise this test proves nothing");

            // Hide() carries explicit "not shown" intent — unlike the bare SetActive(false) leg
            // above, a later bare re-activation must NOT resurrect the registration.
            _sheet.Hide();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "precondition: Hide() unregistered — otherwise the reactivation check below "
                + "proves nothing about Hide()'s intent surviving OnEnable");

            _sheet.gameObject.SetActive(true);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "a bare re-activation after Hide() must NOT resurrect the registration — OnEnable "
                + "re-registers only what Show() left shown, never what Hide() explicitly closed");
        }

        [UnityTest]
        public IEnumerator ComposedButNeverShown_ActivatingDirectly_RegistersNothing()
        {
            CreateAttachedButNotShown();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "precondition: Create()+Attach() alone registers nothing — otherwise the direct "
                + "activation below proves nothing about boot semantics");

            // A bare activation with Show() never called (the Wire/compose ordering) must
            // register nothing — the OnEnable re-register law is gated on "has been shown",
            // never on activation alone (F4's boot-semantics caution).
            _sheet.gameObject.SetActive(true);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0),
                "a composed-but-never-shown component registers nothing on activation");
        }
    }
}
