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
    public sealed class EarlyLadderDataPlumbingTests
    {
        [Test]
        public void ExplicitShapeAndTunnelFieldsReachDtoAndGraphData()
        {
            var json = L001Json();
            json["board"]["edges"][0]["tunnel"] = true;
            json["stations"][0]["shape"] = "square";
            json["stations"][1]["shape"] = "triangle";
            json["waves"][0]["shape"] = "triangle";

            var imported = Import(json);

            Assert.That(imported.Dto.Edges.Span[0].Tunnel, Is.True);
            Assert.That(imported.Dto.Stations.Span[0].Shape, Is.EqualTo("square"));
            Assert.That(imported.Dto.Stations.Span[1].Shape, Is.EqualTo("triangle"));
            Assert.That(imported.Dto.Waves.Span[0].Shape, Is.EqualTo("triangle"));

            Assert.That(imported.Graph.EdgeTunnel, Is.EqualTo(new[] { true, false, false }));
            Assert.That(imported.Graph.StationShape,
                Is.EqualTo(new[] { CatShape.Square, CatShape.Triangle }));
            Assert.That(imported.Graph.WaveShape, Is.EqualTo(new[] { CatShape.Triangle }));
        }

        [Test]
        public void MissingShapeAndTunnelFieldsMaterializeRuntimeDefaults()
        {
            var imported = Import(L001Json());

            Assert.That(imported.Dto.Edges.ToArray().All(edge => !edge.Tunnel), Is.True);
            Assert.That(imported.Dto.Stations.ToArray().All(station => station.Shape == "round"), Is.True);
            Assert.That(imported.Dto.Waves.ToArray().All(wave => wave.Shape == "round"), Is.True);
            Assert.That(imported.Graph.EdgeTunnel, Is.All.EqualTo(false));
            Assert.That(imported.Graph.StationShape, Is.All.EqualTo(CatShape.Round));
            Assert.That(imported.Graph.WaveShape, Is.All.EqualTo(CatShape.Round));
        }

        [TestCase("station-unknown", ContentErrorKind.BoundViolation)]
        [TestCase("wave-unknown", ContentErrorKind.BoundViolation)]
        [TestCase("station-malformed", ContentErrorKind.MalformedJson)]
        [TestCase("wave-malformed", ContentErrorKind.MalformedJson)]
        [TestCase("tunnel-malformed", ContentErrorKind.MalformedJson)]
        public void MalformedAndUnknownShapeOrTunnelMutationsFailTyped(
            string mutation, ContentErrorKind expectedKind)
        {
            var json = L001Json();
            switch (mutation)
            {
                case "station-unknown":
                    json["stations"][0]["shape"] = "hexagon";
                    break;
                case "wave-unknown":
                    json["waves"][0]["shape"] = "oval";
                    break;
                case "station-malformed":
                    json["stations"][0]["shape"] = 3;
                    break;
                case "wave-malformed":
                    json["waves"][0]["shape"] = JValue.CreateNull();
                    break;
                case "tunnel-malformed":
                    json["board"]["edges"][0]["tunnel"] = "yes";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            ContentResult<ImportedLevel> result = default;
            Assert.DoesNotThrow(() => result = LevelImporter.Import(Bytes(json)));
            Assert.That(result.Ok, Is.False, mutation);
            Assert.That(result.Error.Kind, Is.EqualTo(expectedKind), mutation + ": " + result.Error);
        }

        [Test]
        public void ExistingGraphCallersDefaultNewArraysAndRejectWrongLengthsOrCodes()
        {
            var graph = Fixtures.L001Shape();

            Assert.That(graph.EdgeTunnel, Has.Length.EqualTo(graph.EdgeFrom.Length));
            Assert.That(graph.EdgeTunnel, Is.All.EqualTo(false));
            Assert.That(graph.StationShape, Has.Length.EqualTo(graph.StationNode.Length));
            Assert.That(graph.StationShape, Is.All.EqualTo(CatShape.Round));
            Assert.That(graph.WaveShape, Has.Length.EqualTo(graph.WaveTick.Length));
            Assert.That(graph.WaveShape, Is.All.EqualTo(CatShape.Round));

            Assert.Throws<ArgumentException>(() => CopyGraph(
                graph, edgeTunnel: new bool[graph.EdgeFrom.Length + 1]));
            Assert.Throws<ArgumentException>(() => CopyGraph(
                graph, stationShape: new byte[graph.StationNode.Length + 1]));
            Assert.Throws<ArgumentException>(() => CopyGraph(
                graph, waveShape: new byte[graph.WaveTick.Length + 1]));

            var unknownStationShape = graph.StationShape.ToArray();
            unknownStationShape[0] = byte.MaxValue;
            Assert.Throws<ArgumentException>(() => CopyGraph(
                graph, stationShape: unknownStationShape));
        }

        private static LevelGraph CopyGraph(
            LevelGraph graph,
            bool[] edgeTunnel = null,
            byte[] stationShape = null,
            byte[] waveShape = null) =>
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
                edgeTunnel: edgeTunnel ?? graph.EdgeTunnel,
                stationShape: stationShape ?? graph.StationShape,
                waveShape: waveShape ?? graph.WaveShape);

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
