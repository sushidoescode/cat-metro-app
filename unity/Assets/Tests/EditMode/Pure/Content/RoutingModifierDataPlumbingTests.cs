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
    public sealed class RoutingModifierDataPlumbingTests
    {
        [Test]
        public void ExplicitHoldStrayAndSecondTrainDataReachDtoAndGraph()
        {
            var json = L001Json();
            json["board"]["edges"][0]["hold"] = true;
            json["waves"][0]["stray"] = true;
            ((JArray)json["meta"]["mechanics"]).Add("second-train");

            var imported = Import(json);

            Assert.That(imported.Dto.Edges.Span[0].Hold, Is.True);
            Assert.That(imported.Dto.Waves.Span[0].Stray, Is.True);
            Assert.That(imported.Graph.EdgeHold, Is.EqualTo(new[] { true, false, false }));
            Assert.That(imported.Graph.WaveStray, Is.EqualTo(new[] { true }));
            Assert.That(imported.Graph.CollisionsEnabled, Is.True);
        }

        [Test]
        public void MissingFieldsDefaultFalseAndCollisionDerivationRequiresExactMechanic()
        {
            var imported = Import(L001Json());

            Assert.That(imported.Dto.Edges.ToArray().All(edge => !edge.Hold), Is.True);
            Assert.That(imported.Dto.Waves.ToArray().All(wave => !wave.Stray), Is.True);
            Assert.That(imported.Graph.EdgeHold, Is.All.EqualTo(false));
            Assert.That(imported.Graph.WaveStray, Is.All.EqualTo(false));
            Assert.That(imported.Graph.CollisionsEnabled, Is.False);

            var nearMatch = L001Json();
            ((JArray)nearMatch["meta"]["mechanics"]).Add("second-train-preview");
            Assert.That(Import(nearMatch).Graph.CollisionsEnabled, Is.False);
        }

        [TestCase("hold", ContentErrorKind.MalformedJson)]
        [TestCase("stray", ContentErrorKind.MalformedJson)]
        public void NonBooleanFlagsFailAsTypedMalformedContent(
            string mutation, ContentErrorKind expectedKind)
        {
            var json = L001Json();
            if (mutation == "hold") json["board"]["edges"][0]["hold"] = "loop";
            else json["waves"][0]["stray"] = 1;

            ContentResult<ImportedLevel> result = default;
            Assert.DoesNotThrow(() => result = LevelImporter.Import(Bytes(json)));
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Kind, Is.EqualTo(expectedKind), result.Error.ToString());
        }

        [Test]
        public void ExistingGraphCallersDefaultArraysAndWrongLengthsAreRejected()
        {
            var graph = Fixtures.L001Shape();

            Assert.That(graph.EdgeHold, Has.Length.EqualTo(graph.EdgeFrom.Length));
            Assert.That(graph.EdgeHold, Is.All.EqualTo(false));
            Assert.That(graph.WaveStray, Has.Length.EqualTo(graph.WaveTick.Length));
            Assert.That(graph.WaveStray, Is.All.EqualTo(false));
            Assert.That(graph.CollisionsEnabled, Is.False);

            Assert.Throws<ArgumentException>(() => CopyGraph(
                graph, edgeHold: new bool[graph.EdgeFrom.Length + 1]));
            Assert.Throws<ArgumentException>(() => CopyGraph(
                graph, waveStray: new bool[graph.WaveTick.Length + 1]));

            Assert.That(CopyGraph(graph, collisionsEnabled: true).CollisionsEnabled, Is.True);
        }

        private static LevelGraph CopyGraph(
            LevelGraph graph,
            bool[] edgeHold = null,
            bool[] waveStray = null,
            bool collisionsEnabled = false) =>
            new LevelGraph(
                graph.LevelId,
                graph.NodeCount,
                graph.NodeQueueCapacity,
                graph.EdgeFrom,
                graph.EdgeTo,
                graph.EdgeTravelTicks,
                graph.SourceNodes,
                graph.SwitchRoutes,
                graph.SwitchNode,
                graph.SwitchInitialRoute,
                graph.StationNode,
                graph.StationAccepts,
                graph.StationCapacity,
                graph.WaveTick,
                graph.WaveColor,
                graph.WaveCount,
                graph.WaveSpacingTicks,
                graph.WinDeliveries,
                graph.TimeLimitTicks,
                graph.QCapBound,
                graph.TrainsMax,
                waveSourceNode: graph.WaveSourceNode,
                perfectMaxSwitches: graph.PerfectMaxSwitches,
                edgeOneWay: graph.EdgeOneWay,
                edgeReversible: graph.EdgeReversible,
                switchCooldownTicks: graph.SwitchCooldownTicks,
                gateEdge: graph.GateEdge,
                gateOpenWindows: graph.GateOpenWindows,
                gatePreviewTicks: graph.GatePreviewTicks,
                waveExpress: graph.WaveExpress,
                edgeTunnel: graph.EdgeTunnel,
                stationShape: graph.StationShape,
                waveShape: graph.WaveShape,
                edgeHold: edgeHold ?? graph.EdgeHold,
                waveStray: waveStray ?? graph.WaveStray,
                collisionsEnabled: collisionsEnabled);

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
