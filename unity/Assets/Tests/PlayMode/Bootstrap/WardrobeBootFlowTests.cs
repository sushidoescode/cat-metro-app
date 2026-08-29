using System.Collections;
using CatMetro.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class WardrobeBootFlowTests
    {
        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            GameRoot.DevSkipShippedHome = false;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false;
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator ShippedHome_WardrobeRoundTrip_IsVisibleTappableAndKeepsSimulationPaused()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Wardrobe, Is.Not.Null,
                "the real shipped composition owns the monetization surface");
            Assert.That(_root.Wardrobe.EntryVisible, Is.True,
                "the wardrobe capsule is visible on Home, not hidden behind a debug path");
            Assert.That(_root.Wardrobe.PanelVisible, Is.False);
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0));

            int entryTap = _root.Input.HandleTapAtScreen(_root.Wardrobe.EntryRectPx.center);
            Assert.That(entryTap, Is.EqualTo(-3), "the painted capsule is reachable by real input");
            yield return null;

            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.Wardrobe.PanelVisible, Is.True);
            CollectionAssert.AreEqual(new[] { "home", "wardrobe" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                "opening the wardrobe never advances the puzzle behind it");

            int backTap = _root.Input.HandleTapAtScreen(_root.Wardrobe.BackRectPx.center);
            Assert.That(backTap, Is.EqualTo(-3));
            yield return null;

            Assert.That(_root.Wardrobe.PanelVisible, Is.False);
            Assert.That(_root.Wardrobe.EntryVisible, Is.True);
            Assert.That(_root.Home.IsVisible, Is.True);
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0));
        }
    }
}
