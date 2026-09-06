using System;
using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class TransitionVeilTests
    {
        private GameObject _owner;
        private ChromeRegions _regions;
        private TransitionVeil _veil;

        [TearDown]
        public void TearDown()
        {
            if (_owner != null) Object.DestroyImmediate(_owner);
        }

        private void Create()
        {
            _owner = new GameObject("TransitionTestOwner");
            _regions = new ChromeRegions();
            _veil = TransitionVeil.Create(_owner.transform, null, _regions);
        }

        private bool Begin(Action action, bool motionOff = false) =>
            _veil.Begin(action, motionOff);
        private void Advance(float seconds, bool motionOff = false) =>
            _veil.Advance(seconds, motionOff);
        private float Alpha => _veil.Alpha;
        private bool InFlight => _veil.IsInFlight;

        [Test]
        public void LoadRunsOnce_AtTheOpaqueMidpoint_ThenTheVeilClears()
        {
            Create();
            int loads = 0;
            Assert.That(Begin(() => { Assert.That(Alpha, Is.EqualTo(1f)); loads++; }), Is.True);
            Assert.That(InFlight, Is.True);
            Assert.That(_regions.Count, Is.EqualTo(1));
            Advance(.1f);
            Assert.That(loads, Is.Zero);
            Assert.That(Alpha, Is.GreaterThan(0f).And.LessThan(1f));
            Advance(.12f);
            Assert.That(loads, Is.EqualTo(1));
            Assert.That(Alpha, Is.EqualTo(1f));
            Advance(.14f);
            Assert.That(Alpha, Is.EqualTo(.5f).Within(.01f));
            Advance(.14f);
            Assert.That(loads, Is.EqualTo(1));
            Assert.That(Alpha, Is.Zero);
            Assert.That(InFlight, Is.False);
            Assert.That(_regions.Count, Is.Zero);
        }

        [Test]
        public void RepeatedRequestsAndCoveredTaps_CannotAdvanceAgain()
        {
            Create();
            int loads = 0, taps = 0;
            _regions.Register("covered.next", () => new Rect(0, 0, Screen.width, Screen.height),
                () => taps++, ChromeRegions.ModalPriority);
            Begin(() => loads++);
            Assert.That(Begin(() => loads++), Is.False);
            Assert.That(_regions.TryResolve(new Vector2(20, 20), out var tap, out var feedback), Is.True);
            Assert.That(feedback, Is.EqualTo(ChromeFeedback.None), "the cover is inert");
            tap();
            Assert.That(taps, Is.Zero);
            Advance(.22f);
            Assert.That(loads, Is.EqualTo(1));
        }

        [Test]
        public void MotionOff_IsImmediate_AndCanFinishAnInflightVeil()
        {
            Create();
            int loads = 0;
            Begin(() => loads++, true);
            Assert.That(loads, Is.EqualTo(1));
            Assert.That(InFlight, Is.False);
            Assert.That(Alpha, Is.Zero);
            Begin(() => loads++);
            Advance(.05f);
            Advance(0f, true);
            Advance(1f, true);
            Assert.That(loads, Is.EqualTo(2));
            Assert.That(InFlight, Is.False);
            Assert.That(_regions.Count, Is.Zero);
        }
    }
}
