using System;
using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Tests.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Content
{
    [TestFixture]
    public sealed class SchemaPlumbingTests
    {
        [Test]
        public void AuthoredSchemaFieldsReachImmutableDtosAndRuntimeGraphData()
        {
            var json = L001Json();
            var firstEdge = (JObject)json["board"]["edges"][0];
            string gatedEdgeId = (string)json["board"]["edges"][1]["id"];
            firstEdge["oneWay"] = false;
            firstEdge["reversible"] = true;
            json["switches"][0]["cooldownTicks"] = 7;
            json["gates"] = new JArray(new JObject
            {
                ["edgeId"] = gatedEdgeId,
                ["openWindows"] = new JArray(
                    new JArray(2, 9),
                    new JArray(12, 20)),
                ["previewTicks"] = 11,
            });
            json["waves"][0]["express"] = true;
            json["tags"] = new JArray("teaching", "night");

            var imported = Import(json);

            var edge = imported.Dto.Edges.Span[0];
            Assert.That(edge.OneWay, Is.False);
            Assert.That(edge.Reversible, Is.True);
            Assert.That(imported.Dto.Switches.Span[0].CooldownTicks, Is.EqualTo(7));
            Assert.That(imported.Dto.Waves.Span[0].Express, Is.True);
            Assert.That(imported.Dto.Tags.ToArray(), Is.EqualTo(new[] { "teaching", "night" }));

            Assert.That(imported.Dto.Gates.Length, Is.EqualTo(1));
            var gate = imported.Dto.Gates.Span[0];
            Assert.That(gate.EdgeId, Is.EqualTo(gatedEdgeId));
            Assert.That(gate.PreviewTicks, Is.EqualTo(11));
            Assert.That(gate.OpenWindows.Length, Is.EqualTo(2));
            Assert.That(gate.OpenWindows.Span[0].StartTick, Is.EqualTo(2));
            Assert.That(gate.OpenWindows.Span[0].EndTick, Is.EqualTo(9));
            Assert.That(gate.OpenWindows.Span[1].StartTick, Is.EqualTo(12));
            Assert.That(gate.OpenWindows.Span[1].EndTick, Is.EqualTo(20));

            var graph = imported.Graph;
            Assert.That(graph.EdgeOneWay, Is.EqualTo(new[] { false, true, true }));
            Assert.That(graph.EdgeReversible, Is.EqualTo(new[] { true, false, false }));
            Assert.That(graph.SwitchCooldownTicks, Is.EqualTo(new[] { 7 }));
            Assert.That(graph.GateEdge, Is.EqualTo(new[] { 1 }),
                $"{gatedEdgeId} maps to dense edge index 1");
            Assert.That(graph.GatePreviewTicks, Is.EqualTo(new[] { 11 }));
            Assert.That(graph.GateOpenWindows[0].Length, Is.EqualTo(2));
            Assert.That(graph.GateOpenWindows[0][0].StartTick, Is.EqualTo(2));
            Assert.That(graph.GateOpenWindows[0][0].EndTick, Is.EqualTo(9));
            Assert.That(graph.GateOpenWindows[0][1].StartTick, Is.EqualTo(12));
            Assert.That(graph.GateOpenWindows[0][1].EndTick, Is.EqualTo(20));
            Assert.That(graph.WaveExpress, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void MissingOptionalFieldsMaterializeSchemaDefaultsInDtoAndGraph()
        {
            var imported = Import(L001Json());

            Assert.That(imported.Dto.Edges.ToArray().All(edge => edge.OneWay), Is.True);
            Assert.That(imported.Dto.Edges.ToArray().Any(edge => edge.Reversible), Is.False);
            Assert.That(imported.Dto.Switches.Span[0].CooldownTicks,
                Is.EqualTo(ContentBounds.COOLDOWN_TICKS_DEFAULT));
            Assert.That(imported.Dto.Gates.Length, Is.Zero);
            Assert.That(imported.Dto.Waves.Span[0].Express, Is.False);
            Assert.That(imported.Dto.Tags.Length, Is.Zero);

            Assert.That(imported.Graph.EdgeOneWay, Is.All.True);
            Assert.That(imported.Graph.EdgeReversible, Is.All.False);
            Assert.That(imported.Graph.SwitchCooldownTicks, Is.EqualTo(new[] { 0 }));
            Assert.That(imported.Graph.GateEdge, Is.Empty);
            Assert.That(imported.Graph.GateOpenWindows, Is.Empty);
            Assert.That(imported.Graph.GatePreviewTicks, Is.Empty);
            Assert.That(imported.Graph.WaveExpress, Is.EqualTo(new[] { false }));

            var gateJson = L001Json();
            string gatedEdgeId = (string)gateJson["board"]["edges"][1]["id"];
            gateJson["gates"] = new JArray(new JObject
            {
                ["edgeId"] = gatedEdgeId,
                ["openWindows"] = new JArray(
                    new JArray(2, 9),
                    new JArray(12, 20)),
            });
            var withDefaultedGate = Import(gateJson);
            Assert.That(withDefaultedGate.Dto.Gates.Span[0].PreviewTicks,
                Is.EqualTo(ContentBounds.GATE_PREVIEW_TICKS_DEFAULT));
            Assert.That(withDefaultedGate.Graph.GatePreviewTicks,
                Is.EqualTo(new[] { ContentBounds.GATE_PREVIEW_TICKS_DEFAULT }));
        }

        [Test]
        public void ExistingDirectGraphConstructorMaterializesNewDataDefaults()
        {
            var graph = Fixtures.L001Shape();

            Assert.That(graph.EdgeOneWay, Has.Length.EqualTo(graph.EdgeFrom.Length));
            Assert.That(graph.EdgeOneWay, Is.All.EqualTo(true));
            Assert.That(graph.EdgeReversible, Has.Length.EqualTo(graph.EdgeFrom.Length));
            Assert.That(graph.EdgeReversible, Is.All.EqualTo(false));
            Assert.That(graph.SwitchCooldownTicks, Has.Length.EqualTo(graph.SwitchRoutes.Length));
            Assert.That(graph.SwitchCooldownTicks, Is.All.EqualTo(0));
            Assert.That(graph.GateEdge, Is.Empty);
            Assert.That(graph.GateOpenWindows, Is.Empty);
            Assert.That(graph.GatePreviewTicks, Is.Empty);
            Assert.That(graph.WaveExpress, Has.Length.EqualTo(graph.WaveTick.Length));
            Assert.That(graph.WaveExpress, Is.All.EqualTo(false));
            Assert.That(graph.WaveSourceNode, Has.Length.EqualTo(graph.WaveTick.Length));
            Assert.That(graph.PerfectMaxSwitches, Is.EqualTo(FlipBudget.Unbudgeted));
        }

        [TestCase("oneWay-type", ContentErrorKind.MalformedJson)]
        [TestCase("reversible-type", ContentErrorKind.MalformedJson)]
        [TestCase("cooldown-bound", ContentErrorKind.BoundViolation)]
        [TestCase("preview-bound", ContentErrorKind.BoundViolation)]
        [TestCase("express-type", ContentErrorKind.MalformedJson)]
        [TestCase("tags-type", ContentErrorKind.MalformedJson)]
        [TestCase("gate-dangling", ContentErrorKind.DanglingReference)]
        [TestCase("gate-window-reversed", ContentErrorKind.BoundViolation)]
        [TestCase("gate-window-overlap", ContentErrorKind.BoundViolation)]
        [TestCase("gate-window-float", ContentErrorKind.IntegerExpected)]
        public void MalformedBoundedAndDanglingMutationsReturnTypedFailures(
            string mutation, ContentErrorKind expectedKind)
        {
            var json = L001Json();
            string gatedEdgeId = (string)json["board"]["edges"][1]["id"];
            switch (mutation)
            {
                case "oneWay-type":
                    json["board"]["edges"][0]["oneWay"] = "yes";
                    break;
                case "reversible-type":
                    json["board"]["edges"][0]["reversible"] = 1;
                    break;
                case "cooldown-bound":
                    json["switches"][0]["cooldownTicks"] = ContentBounds.COOLDOWN_TICKS_MAX + 1;
                    break;
                case "preview-bound":
                    AddGate(json, gatedEdgeId,
                        new JArray(new JArray(2, 9), new JArray(12, 20)),
                        ContentBounds.GATE_PREVIEW_TICKS_MIN - 1);
                    break;
                case "express-type":
                    json["waves"][0]["express"] = "fast";
                    break;
                case "tags-type":
                    json["tags"] = new JArray(4);
                    break;
                case "gate-dangling":
                    AddGate(json, "MISSING",
                        new JArray(new JArray(2, 9), new JArray(12, 20)), 16);
                    break;
                case "gate-window-reversed":
                    AddGate(json, gatedEdgeId,
                        new JArray(new JArray(9, 2), new JArray(12, 20)), 16);
                    break;
                case "gate-window-overlap":
                    AddGate(json, gatedEdgeId,
                        new JArray(new JArray(2, 9), new JArray(8, 12)), 16);
                    break;
                case "gate-window-float":
                    AddGate(json, gatedEdgeId,
                        new JArray(new JArray(2.5, 9), new JArray(12, 20)), 16);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            ContentResult<ImportedLevel> result = default;
            Assert.DoesNotThrow(() => result = LevelImporter.Import(Bytes(json)));
            Assert.That(result.Ok, Is.False, mutation);
            Assert.That(result.Error.Kind, Is.EqualTo(expectedKind),
                mutation + ": " + result.Error);
        }

        private static void AddGate(JObject json, string edgeId, JArray windows, int previewTicks)
        {
            json["gates"] = new JArray(new JObject
            {
                ["edgeId"] = edgeId,
                ["openWindows"] = windows,
                ["previewTicks"] = previewTicks,
            });
        }

        private static ImportedLevel Import(JObject json)
        {
            var result = LevelImporter.Import(Bytes(json));
            Assert.That(result.Ok, Is.True, result.Error?.ToString());
            return result.Value;
        }

        private static byte[] Bytes(JObject json) =>
            Encoding.UTF8.GetBytes(json.ToString(Formatting.None));

        private static JObject L001Json()
        {
            string path = Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json");
            return JObject.Parse(File.ReadAllText(path));
        }
    }
}
