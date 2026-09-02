using System.Collections;
using System.IO;
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
            Assert.That(previewTokens, Does.Contain("O!"), "the stray is visible before emission");
            Assert.That(previewTokens, Does.Contain("OE"), "the express flag is visible before emission");

            var labels = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            Assert.That(labels.Any(label => label.name == "tunnel-entry:E_RED_TUNNEL"), Is.True);
            Assert.That(labels.Any(label => label.name == "tunnel-exit:E_RED_TUNNEL"), Is.True);
            Assert.That(labels.Single(label => label.name == "hold:E_HOLD_OUT").text, Is.EqualTo("H"));
            Assert.That(labels.Any(label => label.name == "match-shape" && label.text == "T"), Is.True,
                "the triangle station has an independent non-colour match signal");

            _root.Session.AdvanceMs(4 * TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.View.TrainBadge(0), Is.EqualTo("O!"),
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
            Assert.That(gate.text, Is.EqualTo("X"));
            Assert.That(cooldown.text, Is.Empty);

            Assert.That(_root.Session.EnqueueToggle(0), Is.True);
            _root.Session.AdvanceMs(2 * TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(cooldown.text, Is.EqualTo("4"));
            Assert.That(_root.Preview.FlipSummary, Is.EqualTo("Flips 1/2"));

            _root.Session.AdvanceMs(7 * TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(gate.text, Is.EqualTo("X 7"),
                "previewTicks exposes the next transition instead of remaining decorative data");
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_CapstonePhoneFrame_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_LADDER_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_LADDER_CAPTURE_DIR to emit L060");
                yield break;
            }

            _root = GameRoot.LaunchWith(ReadLevel("L060"));
            yield return null;
            _root.Session.AdvanceMs(4 * TickInterpolator.TICK_MS);
            yield return null;

            const int width = 917;
            const int height = 2048;
            var rt = new RenderTexture(width, height, 24);
            _root.Cam.targetTexture = rt;
            yield return null; // camera aspect must settle before screen-space layout
            _root.Preview.Refresh();
            Canvas.ForceUpdateCanvases();
            _root.Cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            _root.Cam.targetTexture = null;
            RenderTexture.active = null;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "ladder-L060-tick004.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
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
