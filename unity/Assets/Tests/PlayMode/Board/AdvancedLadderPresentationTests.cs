using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
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
            Time.timeScale = 1f;
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
        public IEnumerator AcceptedTap_StaysCommittedAcrossAnInterveningStrayCooldown()
        {
            Time.timeScale = 0f; // keep GameRoot.Update from racing the three manual boundaries
            _root = GameRoot.LaunchWith(StrayCooldownPriorityLevel());
            yield return null;

            _root.Session.AdvanceMs(TickInterpolator.TICK_MS); // process tick 0
            Assert.That(_root.Session.EnqueueToggle(0), Is.True);
            Assert.That(_root.View.CommittedRoute(0), Is.EqualTo(1));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(_root.Session.FlipStatus.Used, Is.EqualTo(1));
            Assert.That(_root.Session.FlipStatus.RemainingToPerfect, Is.Zero);

            _root.Session.AdvanceMs(TickInterpolator.TICK_MS); // stray presses during tick 1
            yield return null;
            Assert.That(CatMetro.Domain.SwitchState.Route(
                _root.Session.State.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(CatMetro.Domain.SwitchState.Cooldown(
                _root.Session.State.SwitchRoutes[0]), Is.EqualTo(2));
            Assert.That(_root.Session.PendingToggleCount(0), Is.EqualTo(1));
            int committedAfterStray = _root.View.CommittedRoute(0);
            Assert.That(committedAfterStray, Is.Zero,
                "the live lever composes the automatic press and pending player tap");

            _root.Session.AdvanceMs(TickInterpolator.TICK_MS); // accepted tap applies at tick 2
            yield return null;
            Assert.That(CatMetro.Domain.SwitchState.Route(
                _root.Session.State.SwitchRoutes[0]), Is.Zero,
                "the accepted tap produces its route change instead of disappearing");
            Assert.That(CatMetro.Domain.SwitchState.Cooldown(
                _root.Session.State.SwitchRoutes[0]), Is.EqualTo(2));
            Assert.That(_root.Session.State.SwitchesUsed, Is.EqualTo(1));
            Assert.That(_root.Session.State.FreshAutomaticCooldowns, Is.Zero);
            Assert.That(_root.Session.PendingToggleCount(0), Is.Zero);
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(_root.View.CommittedRoute(0), Is.EqualTo(committedAfterStray),
                "the rendered committed lever does not snap back at application");
            Assert.That(_root.Session.EnqueueToggle(0), Is.False,
                "the accepted tap still owns the one-flip cap");
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

        private static ImportedLevel StrayCooldownPriorityLevel()
        {
            const string json = @"{
  ""schemaVersion"": 2,
  ""id"": ""L901"",
  ""name"": ""Stray cooldown priority fixture"",
  ""seed"": 7001,
  ""meta"": {
    ""band"": ""combo"",
    ""difficultyTarget"": 0.5,
    ""mechanics"": [""switch"", ""cooldown"", ""stray"", ""second-train""],
    ""newMechanic"": null,
    ""teachingGoal"": ""Accepted tap survives automatic cooldown"",
    ""minActionWindowTicks"": 4,
    ""authoredBy"": ""llm+validator""
  },
  ""board"": {
    ""nodes"": [
      { ""id"": ""SRC"", ""x"": 0, ""y"": 4 },
      { ""id"": ""J1"", ""x"": 0, ""y"": 1, ""queueCapacity"": 4 },
      { ""id"": ""RED"", ""x"": -4, ""y"": -2 },
      { ""id"": ""BLUE"", ""x"": 4, ""y"": -2 },
      { ""id"": ""AUX"", ""x"": 7, ""y"": 2 }
    ],
    ""edges"": [
      { ""id"": ""E_IN"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 1 },
      { ""id"": ""E_RED"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 6 },
      { ""id"": ""E_BLUE"", ""from"": ""J1"", ""to"": ""BLUE"", ""travelTicks"": 4 },
      { ""id"": ""E_HELP"", ""from"": ""AUX"", ""to"": ""BLUE"", ""travelTicks"": 4 }
    ]
  },
  ""sources"": [
    { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] },
    { ""nodeId"": ""AUX"", ""allowedColors"": [""blue""] }
  ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 3 },
    { ""nodeId"": ""BLUE"", ""accepts"": [""blue""], ""capacity"": 3 }
  ],
  ""switches"": [
    { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E_RED"", ""E_BLUE""],
      ""initialRoute"": 0, ""cooldownTicks"": 2 }
  ],
  ""waves"": [
    { ""tick"": 0, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1,
      ""spacingTicks"": 1, ""stray"": true },
    { ""tick"": 1, ""sourceNode"": ""AUX"", ""color"": ""blue"", ""count"": 1,
      ""spacingTicks"": 1 },
    { ""tick"": 2, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1,
      ""spacingTicks"": 1 }
  ],
  ""win"": {
    ""deliveries"": 2,
    ""timeLimitTicks"": 20,
    ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 100, ""three"": 200 }
  },
  ""economy"": { ""baseTickets"": 1, ""perfectBonus"": 1 }
}";
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(imported.Ok, Is.True, imported.Ok ? "" : imported.Error.ToString());
            return imported.Value;
        }
    }
}
