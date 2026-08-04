using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Validation
{
    // CM-C5 test fixtures. Levels are built by mutating the REAL L001 bytes (one truth), configs
    // are in-code JSON. Tests may use file APIs; the shipped Content assembly may not.
    public static class VFixtures
    {
        public static byte[] SchemaBytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "docs", "plan", "data", "level_schema.json"));

        public static byte[] L001Bytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json"));

        public static byte[] StressBoardsBytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "docs", "plan", "data", "stress_boards.json"));

        public static byte[] ShippedConfigBytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "config", "validator_thresholds.json"));

        // A mutated copy of the real L001 document.
        public static byte[] Level(Action<JObject> mutate)
        {
            var o = JObject.Parse(Encoding.UTF8.GetString(L001Bytes()));
            mutate(o);
            return Encoding.UTF8.GetBytes(o.ToString());
        }

        public static byte[] StressLevelBytes(int index)
        {
            var wrapper = JObject.Parse(Encoding.UTF8.GetString(StressBoardsBytes()));
            return Encoding.UTF8.GetBytes(((JArray)wrapper["levels"])[index].ToString());
        }

        public static ImportedLevel Import(byte[] bytes)
        {
            var r = LevelImporter.Import(bytes);
            Assert.That(r.Ok, Is.True, $"fixture must import: {r.Error}");
            return r.Value;
        }

        // Configs. The four Q-R rows appear ONLY in test fixtures (criterion 13's configured leg);
        // the shipped config omits them (stop condition 3).
        public static ValidatorConfig Config(string json)
        {
            var r = ValidatorConfig.Parse(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, $"fixture config must parse: {r.Error}");
            return r.Value;
        }

        public static ValidatorConfig BareConfig() =>
            Config("{\"jitterSampleCount\": 20}");

        public static ValidatorConfig FullConfig() =>
            Config(@"{
                ""jitterSampleCount"": 20,
                ""lowerBoundSlack"": 2,
                ""starBandSlack"": 50,
                ""noveltyMinDistance"": 5.0,
                ""axisBBandCaps"": {
                  ""onboarding"": { ""maxComplexity"": 20, ""peakTrainsCap"": 4, ""maxConcurrency"": 4, ""maxHeadroom"": 8 }
                }
            }");

        // Zero-input-winnable board: L001 with the switch already pointing at RED.
        public static byte[] TrivialWinLevel() =>
            Level(o => o["switches"][0]["initialRoute"] = 0);

        // Unsolvable, no pins: both routes reach the one red station, 1 red cat, win needs 2.
        public static byte[] UnsolvableLevel() =>
            Level(o =>
            {
                o["board"]["nodes"] = new JArray(
                    Node("SRC", 3, 9), Node("J1", 3, 6), Node("RED", 1, 2));
                o["board"]["edges"] = new JArray(
                    Edge("E1", "SRC", "J1", 4), Edge("E2", "J1", "RED", 5), Edge("E3", "J1", "RED", 9));
                o["stations"] = new JArray(Station("RED", 6, "red"));
                o["switches"][0]["initialRoute"] = 0;
                o["waves"][0]["count"] = 1;
                o["waves"][0]["tick"] = 0;
                o["win"]["deliveries"] = 2;
                o["win"]["timeLimitTicks"] = 40;
            });

        // Every line pins: both routes end at blue-only stations, one red cat.
        public static byte[] AllPinnedLevel() =>
            Level(o =>
            {
                o["stations"] = new JArray(Station("RED", 6, "blue"), Station("BLU", 6, "blue"));
                o["waves"][0]["count"] = 1;
                o["win"]["deliveries"] = 1;
            });

        // 3 switches in series, everything blue-deliverable, win 2 with 1 cat: beam misses, no pins
        // (the CM-C4 BeamMissNoPins shape as authored JSON).
        public static byte[] BeamMissLevel() =>
            Level(o =>
            {
                o["board"]["nodes"] = new JArray(
                    Node("SRC", 3, 9), Node("J1", 2, 8), Node("J2", 2, 6), Node("J3", 2, 4),
                    Node("BLU", 3, 1), Node("D1", 5, 8), Node("D2", 5, 6), Node("D3", 5, 4));
                o["board"]["edges"] = new JArray(
                    Edge("E0", "SRC", "J1", 3), Edge("E1", "J1", "D1", 3), Edge("E2", "J1", "J2", 3),
                    Edge("E3", "J2", "D2", 3), Edge("E4", "J2", "J3", 3), Edge("E5", "J3", "D3", 3),
                    Edge("E6", "J3", "BLU", 3));
                o["sources"][0]["allowedColors"] = new JArray("blue");
                o["stations"] = new JArray(
                    Station("BLU", 6, "blue"), Station("D1", 6, "blue"),
                    Station("D2", 6, "blue"), Station("D3", 6, "blue"));
                o["switches"] = new JArray(
                    Switch("S1", "J1", 0, "E1", "E2"), Switch("S2", "J2", 0, "E3", "E4"),
                    Switch("S3", "J3", 0, "E5", "E6"));
                o["waves"][0]["color"] = "blue";
                o["waves"][0]["count"] = 1;
                o["win"]["deliveries"] = 2;
                o["win"]["timeLimitTicks"] = 40;
            });

        // Brittle: init points at RED; a red then a blue cat arrive at J1 two ticks apart, so the
        // toggle window is ~2 ticks < minActionWindowTicks 6. Band moved off onboarding so the
        // 12-16 limb does not also fire.
        public static byte[] BrittleLevel() =>
            Level(o =>
            {
                o["meta"]["band"] = "alternation";
                o["meta"]["minActionWindowTicks"] = 6;
                o["meta"]["mechanics"] = new JArray("switch", "queue");
                o["meta"]["newMechanic"] = null;
                o["sources"][0]["allowedColors"] = new JArray("red", "blue");
                o["switches"][0]["initialRoute"] = 0;
                o["waves"] = new JArray(
                    Wave(0, "red", 1, 1), Wave(2, "blue", 1, 1));
                o["win"]["deliveries"] = 2;
                o["win"]["timeLimitTicks"] = 60;
            });

        public static JObject Node(string id, int x, int y) =>
            new JObject { ["id"] = id, ["x"] = x, ["y"] = y };
        public static JObject Edge(string id, string from, string to, int travel) =>
            new JObject { ["id"] = id, ["from"] = from, ["to"] = to, ["travelTicks"] = travel };
        public static JObject Station(string nodeId, int capacity, params string[] accepts) =>
            new JObject { ["nodeId"] = nodeId, ["accepts"] = new JArray(accepts), ["capacity"] = capacity };
        public static JObject Switch(string id, string nodeId, int init, params string[] routes) =>
            new JObject { ["id"] = id, ["nodeId"] = nodeId, ["routes"] = new JArray(routes), ["initialRoute"] = init };
        public static JObject Wave(int tick, string color, int count, int spacing) =>
            new JObject { ["tick"] = tick, ["sourceNode"] = "SRC", ["color"] = color, ["count"] = count, ["spacingTicks"] = spacing };
    }
}
