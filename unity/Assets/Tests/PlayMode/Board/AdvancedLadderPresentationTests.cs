using System.Collections;
using System.Linq;
using System.Threading;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Strings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class AdvancedLadderPresentationTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        [Test]
        public void CollisionFailure_HasDedicatedCsvBackedCopy()
        {
            var mapping = GameRoot.FailKey(CatMetro.Domain.FailReason.Collision);
            Assert.That(mapping.key, Is.EqualTo("fail.collision"));
            Assert.That(mapping.token, Is.Null);
            Assert.That(UiStrings.Get(mapping.key), Is.EqualTo("Two trains bumped!"));
        }

        [UnityTest]
        public IEnumerator Capstone_RendersTokenFlags_HardBudget_Tunnel_Hold_AndShape()
        {
            _root = GameRoot.LaunchWith(ReadLevel("L060"));
            yield return null;

            Assert.That(_root.Preview.FlipSummary, Is.EqualTo("Flips 0/1"));
            var previewTokens = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)
                .Where(label => label.name == "cat-token" && label.transform.IsChildOf(_root.Preview.transform))
                .Select(label => label.text).ToArray();
            Assert.That(previewTokens, Does.Contain("●↯"), "the stray is visible before emission");
            Assert.That(previewTokens, Does.Contain("●⚡"), "the express flag is visible before emission");

            var labels = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            Assert.That(labels.Any(label => label.name == "tunnel-entry:E_RED_TUNNEL"), Is.True);
            Assert.That(labels.Any(label => label.name == "tunnel-exit:E_RED_TUNNEL"), Is.True);
            Assert.That(labels.Single(label => label.name == "hold:E_HOLD_OUT").text, Is.EqualTo("H"));
            Assert.That(labels.Any(label => label.name == "match-shape" && label.text == "▲"), Is.True,
                "the triangle station has an independent non-colour match signal");

            _root.Session.AdvanceMs(4 * TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.View.TrainBadge(0), Is.EqualTo("●↯"),
                "the emitted train carries the same token badge as its preview");
        }

        [UnityTest]
        public IEnumerator GateAndCooldown_ShowAuthoritativeStateAndPreviewCountdown()
        {
            _root = GameRoot.LaunchWith(ReadLevel("L057"));
            yield return null;

            var labels = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            var gate = labels.Single(label => label.name == "gate:E_GATE_TUNNEL");
            var cooldown = labels.Single(label => label.name == "cooldown:S1");
            Assert.That(gate.text, Is.EqualTo("╫"));
            Assert.That(cooldown.text, Is.Empty);

            Assert.That(_root.Session.EnqueueToggle(0), Is.True);
            _root.Session.AdvanceMs(2 * TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(cooldown.text, Is.EqualTo("4"));
            Assert.That(_root.Preview.FlipSummary, Is.EqualTo("Flips 1/2"));

            _root.Session.AdvanceMs(7 * TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(gate.text, Is.EqualTo("╫ 7"),
                "previewTicks exposes the next transition instead of remaining decorative data");
        }

        private static ImportedLevel ReadLevel(string id)
        {
            var source = new StreamingAssetsContentSource();
            var bytes = source.ReadAsync(
                "content/levels/" + id + ".json", CancellationToken.None)
                .GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            Assert.That(imported.Ok, Is.True, imported.Ok ? "" : imported.Error.ToString());
            return imported.Value;
        }
    }
}
