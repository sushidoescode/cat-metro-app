using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    [TestFixture]
    public class CommandLogTests
    {
        [Test] // criterion 9
        public void FormatVersion_Is_1()
        {
            Assert.That(new CommandLog().FormatVersion, Is.EqualTo(1));
            Assert.That(CommandLog.CurrentFormatVersion, Is.EqualTo(1));
        }

        [Test] // criterion 9: single command applies at step 1 of the next tick boundary
        public void SingleCommand_AppliesAtNextTickBoundary()
        {
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 5));
            var atTick5 = Fixtures.RunThroughTick(Fixtures.L001Shape(), Fixtures.L001Seed, log, 5);
            Assert.That(atTick5.SwitchRoutes[0], Is.EqualTo(1), "not yet applied after processing tick 5");
            var atTick6 = Fixtures.RunThroughTick(Fixtures.L001Shape(), Fixtures.L001Seed, log, 6);
            Assert.That(atTick6.SwitchRoutes[0], Is.EqualTo(0), "applied at step 1 of tick 6");
        }

        [Test] // criterion 9: append-only receipt order; both same-tick commands apply.
        // Review F6 honesty note: APPLICATION order is unobservable under CM-C1's command
        // vocabulary (switch toggles commute), so this test pins (a) log order, (b) the ordered
        // span handed to Step, and (c) that both commands applied. Application-order becomes
        // directly testable the day a non-commutative command lands — that test is owed then.
        public void SameTick_Commands_AppearAndApplyInReceiptOrder()
        {
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(3, 5)); // deliberately not id-sorted:
            log.Append(new ToggleSwitchCommand(1, 5)); // sorting or reversing would fail below
            Assert.That(log.Entries.Select(e => (int)e.SwitchId), Is.EqualTo(new[] { 3, 1 }), "receipt order preserved in the log");
            var due = Fixtures.DueCommands(log, 6).ToArray();
            Assert.That(due.Select(e => (int)e.SwitchId), Is.EqualTo(new[] { 3, 1 }), "runner hands Step the span in receipt order");

            var applyLog = new CommandLog();
            applyLog.Append(new ToggleSwitchCommand(0, 5));
            applyLog.Append(new ToggleSwitchCommand(1, 5));
            var state = Fixtures.RunThroughTick(Fixtures.TwoSwitchShape(), 2, applyLog, 6);
            Assert.That(state.SwitchRoutes[0], Is.EqualTo(1), "S0 toggled");
            Assert.That(state.SwitchRoutes[1], Is.EqualTo(1), "S1 toggled");
            Assert.That(state.SwitchesUsed, Is.EqualTo(2), "both applied at the same boundary");
        }

        [Test] // criterion 10: the envelope is outside the hash
        public void FormatVersion_DoesNotAffectReplayHash()
        {
            var a = Fixtures.GoldenLog(); // FormatVersion 1
            var b = new CommandLog(formatVersion: 2);
            foreach (var e in a.Entries) b.Append(e);
            var ha = ReplayHasher.ComputeReplayHash(Fixtures.L001Shape(), Fixtures.L001Seed, a);
            var hb = ReplayHasher.ComputeReplayHash(Fixtures.L001Shape(), Fixtures.L001Seed, b);
            Assert.That(hb, Is.EqualTo(ha), "hash is over per-tick state digests only (ADR-0002 §7)");
        }
    }

    [TestFixture]
    public class ReplayHashTests
    {
        [Test] // criterion 11a
        public void TwoInProcessReplays_IdenticalHash_64LowercaseHex()
        {
            var h1 = ReplayHasher.ComputeReplayHash(Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog());
            var h2 = ReplayHasher.ComputeReplayHash(Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog());
            Assert.That(h1, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(h2, Is.EqualTo(h1));
        }

        [Test] // criterion 11c
        public void CommandLogDifferingByOneEntry_DifferentHash()
        {
            var other = new CommandLog();
            other.Append(new ToggleSwitchCommand(0, 11)); // same toggle, one tick earlier
            var h1 = ReplayHasher.ComputeReplayHash(Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog());
            var h2 = ReplayHasher.ComputeReplayHash(Fixtures.L001Shape(), Fixtures.L001Seed, other);
            Assert.That(h2, Is.Not.EqualTo(h1));
        }

        // Byte-for-byte golden comparison plus a machine-readable REPLAY_HASH line. The committed
        // fixture remains independent from the implementation, so a simulation change is loud.
        [Test]
        public void ReplayHash_MatchesCommittedGolden()
        {
            var hash = ReplayHasher.ComputeReplayHash(Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog());
            Console.Out.WriteLine($"REPLAY_HASH={hash}");

            var goldenPath = Path.Combine(
                Fixtures.RepoRoot(), "tests", "fixtures", "replay", "replay-hash-golden.json");
            string expected = null;
            if (File.Exists(goldenPath))
            {
                // Q-G scaffold port: Newtonsoft (the tree-wide pin, present in BOTH hosts) —
                // Unity's scripting profile ships no System.Text JSON stack. Assertions
                // untouched; the golden file is untouched.
                expected = (string)JObject.Parse(File.ReadAllText(goldenPath))["replayHash"];
            }

            if (expected == hash)
            {
                Assert.Pass("replay hash matches the human-committed golden byte-for-byte");
                return;
            }

            var goldenJson = new JObject
            {
                ["levelId"] = "L001",
                ["seed"] = Fixtures.L001Seed,
                ["commandLog"] = new JObject
                {
                    ["formatVersion"] = 1,
                    ["entries"] = new JArray(new JObject { ["switchId"] = 0, ["tick"] = 12 }),
                },
                ["fixture"] = "in-code L001 shape and command log pinned by the committed test fixture",
                ["digestBytes"] = 143,
                ["replayHash"] = hash,
            }.ToString(Newtonsoft.Json.Formatting.Indented);

            Console.Out.WriteLine("GOLDEN_JSON_BEGIN");
            Console.Out.WriteLine(goldenJson);
            Console.Out.WriteLine("GOLDEN_JSON_END");
            Assert.Fail(File.Exists(goldenPath)
                ? $"golden mismatch: expected {expected}, computed {hash}. Review any intentional simulation change before updating the committed fixture."
                : $"replay golden fixture is absent at {goldenPath}");
        }
    }

    // Criterion 2 (glob parity) + criterion 3 (no floating versions) as in-suite assertions.
    [TestFixture]
    public class CsprojParityTests
    {
        private static XDocument Load(string rel) =>
            XDocument.Load(Path.Combine(Fixtures.RepoRoot(), rel.Replace('/', Path.DirectorySeparatorChar)));

        [Test]
        public void DomainCsproj_LinksExactlyTheDomainGlob()
        {
            var doc = Load("dotnet/CatMetro.Domain/CatMetro.Domain.csproj");
            Assert.That(doc.Root.Attribute("Sdk"), Is.Not.Null);
            Assert.That(doc.Descendants("TargetFramework").Single().Value, Is.EqualTo("netstandard2.1"));
            var includes = doc.Descendants("Compile").Select(c => c.Attribute("Include")?.Value).Where(v => v != null).ToArray();
            Assert.That(includes, Is.EqualTo(new[] { "../../unity/Assets/Scripts/Domain/**/*.cs" }));
        }

        [Test]
        public void TestsCsproj_LinksOnlyThePureGlob()
        {
            var doc = Load("dotnet/CatMetro.Tests/CatMetro.Tests.csproj");
            Assert.That(doc.Descendants("TargetFramework").Single().Value, Is.EqualTo("net8.0"));
            var includes = doc.Descendants("Compile").Select(c => c.Attribute("Include")?.Value).Where(v => v != null).ToArray();
            Assert.That(includes, Is.EqualTo(new[] { "../../unity/Assets/Tests/EditMode/Pure/**/*.cs" }),
                "ADR-0005:169-172 test-split parity: Pure/** and ONLY that");
        }

        [Test]
        public void NoFloatingPackageVersions()
        {
            foreach (var rel in new[] { "dotnet/CatMetro.Domain/CatMetro.Domain.csproj", "dotnet/CatMetro.Tests/CatMetro.Tests.csproj" })
            {
                foreach (var p in Load(rel).Descendants("PackageReference"))
                {
                    var v = p.Attribute("Version")?.Value;
                    Assert.That(v, Is.Not.Null.And.Not.Empty, $"{rel}: {p.Attribute("Include")?.Value} must pin a Version");
                    Assert.That(Regex.IsMatch(v, @"^\d+\.\d+\.\d+$"), Is.True, $"{rel}: {v} is not an exact pin");
                }
            }
        }
    }
}
