using NUnit.Framework;
using CatMetro.Content.Validation;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.Validation
{
    // Criterion 3: stage 2 — three failing fixtures + one warning fixture (verdict WARN, zero
    // exit contribution). Freezes per handoff A-C5-8.
    [TestFixture]
    public class StaticAnalysisTests
    {
        private static StageVerdict Check(byte[] level) =>
            StaticAnalysisStage.Check(VFixtures.Import(level).Dto);

        [Test]
        public void L001_WarnsOnItsDecoyOnly_NeverBlocks()
        {
            // Review F6: the decoy carve-out is audible, not silent — L001's BLU warns.
            var v = Check(VFixtures.L001Bytes());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Warn), v.Detail);
            Assert.That(v.Blocks, Is.False);
            Assert.That(v.Detail, Does.Contain("decoy").And.Contain("BLU"));
        }

        [Test]
        public void UnreachableStation_Fails()
        {
            // The source CAN emit green and GRN accepts green — but no path leads to GRN.
            var level = VFixtures.Level(o =>
            {
                o["sources"][0]["allowedColors"] = new JArray("red", "green");
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("GRN", 5, 5));
                ((JArray)o["stations"]).Add(VFixtures.Station("GRN", 6, "green"));
            });
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True);
            Assert.That(v.Detail, Does.Contain("GRN").And.Contain("reach").IgnoreCase);
        }

        [Test]
        public void DecoyStation_CannotFail_ButIsNamedInTheWarn()
        {
            // A decoy is not a reachability defect (A-C5-8) — and not silence either (F6): the
            // accepts-typo class ("yellow" for "green") surfaces here for a human to judge.
            var v = Check(VFixtures.Level(o =>
            {
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("BLU2", 5, 4));
                ((JArray)o["stations"]).Add(VFixtures.Station("BLU2", 6, "yellow"));
            }));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Warn));
            Assert.That(v.Blocks, Is.False);
            Assert.That(v.Detail, Does.Contain("BLU2"));
        }

        [Test]
        public void OrphanSwitch_NoInboundEdge_Fails()
        {
            // J2 hosts a switch but nothing flows into J2.
            var level = VFixtures.Level(o =>
            {
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("J2", 5, 6));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E8", "J2", "BLU", 5));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E9", "J2", "RED", 5));
                ((JArray)o["switches"]).Add(VFixtures.Switch("S2", "J2", 0, "E8", "E9"));
            });
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("orphan").IgnoreCase.And.Contain("S2"));
        }

        [Test]
        public void SwitchRoute_NotOutboundOfItsNode_Fails()
        {
            // S1 at J1 claims E1 (SRC->J1) as a route: E1 is not outbound of J1.
            var level = VFixtures.Level(o => o["switches"][0]["routes"] = new JArray("E2", "E1"));
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("orphan").IgnoreCase.And.Contain("E1"));
        }

        [Test]
        public void JunctionSpacing_Under1Point2_Fails()
        {
            // Second junction 1 grid unit from J1 (< 1.2; CM-R07.6).
            var level = VFixtures.Level(o =>
            {
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("J2", 3, 7));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E4", "J1", "J2", 2));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E5", "J2", "BLU", 5));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E6", "J2", "RED", 5));
                ((JArray)o["switches"]).Add(VFixtures.Switch("S2", "J2", 0, "E5", "E6"));
            });
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("1.2").Or.Contain("spacing"));
        }

        [Test]
        public void SwitchInTopFifteenPercent_WarnsWithoutBlocking()
        {
            // Move the switch node into the top 15% of the board's vertical extent.
            var level = VFixtures.Level(o => o["board"]["nodes"][1]["y"] = 9);
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Warn));
            Assert.That(v.Blocks, Is.False, "the top-15% rule warns, never fails");
            Assert.That(v.Detail, Does.Contain("top").IgnoreCase);
        }
    }

    // Criterion 4: stage 3 — hand-derived L001 bound, UNCONFIGURED with no row (also one half of
    // criterion 13's stage-3 pair), FAIL with the fixture row present and a violating level.
    [TestFixture]
    public class LowerBoundTests
    {
        [Test]
        public void L001_ComputedBound_IsHandDerived44()
        {
            // (E1 10 + E2 12) x 2 deliveries = 44 (handoff A-C5-9) — the full equation asserted,
            // not a substring (review's criterion-4 note).
            var v = LowerBoundStage.Check(VFixtures.Import(VFixtures.L001Bytes()).Dto, VFixtures.BareConfig());
            Assert.That(v.Value, Is.EqualTo("lowerBound=44 (minTravelTicks=22 x deliveries=2)"));
        }

        [Test]
        public void NoSlackRow_ReportsUnconfiguredAndDoesNotBlock()
        {
            var v = LowerBoundStage.Check(VFixtures.Import(VFixtures.L001Bytes()).Dto, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Unconfigured));
            Assert.That(v.Detail, Is.EqualTo("UNCONFIGURED(lowerBoundSlack)"));
            Assert.That(v.Blocks, Is.False);
        }

        [Test]
        public void SlackRowPresent_PassesL001_AndBlocksAViolatingLevel()
        {
            var config = VFixtures.FullConfig(); // slack 2
            var ok = LowerBoundStage.Check(VFixtures.Import(VFixtures.L001Bytes()).Dto, config);
            Assert.That(ok.Code, Is.EqualTo(StageVerdictCode.Pass), "44 <= 160 + 2");

            var tight = VFixtures.Level(o => o["win"]["timeLimitTicks"] = 40);
            var bad = LowerBoundStage.Check(VFixtures.Import(tight).Dto, config);
            Assert.That(bad.Code, Is.EqualTo(StageVerdictCode.Fail), "44 > 40 + 2 blocks normally");
            Assert.That(bad.Blocks, Is.True);
        }
    }
}
