using System;
using NUnit.Framework;
using CatMetro.Content.Validation;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.Validation
{
    internal static class DirectedValidationFixtures
    {
        public static byte[] Level(Action<JObject> mutate = null) => VFixtures.Level(o =>
        {
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 3),
                VFixtures.Node("J1", 0, 1),
                VFixtures.Node("RED", 0, -1));
            o["board"]["edges"] = new JArray(
                VFixtures.Edge("E_IN", "SRC", "J1", 5),
                VFixtures.Edge("E_RED", "J1", "RED", 7));
            o["sources"] = new JArray(new JObject
            {
                ["nodeId"] = "SRC", ["allowedColors"] = new JArray("red")
            });
            o["stations"] = new JArray(VFixtures.Station("RED", 6, "red"));
            o["switches"] = new JArray();
            o["waves"] = new JArray(VFixtures.Wave(0, "red", 1, 8));
            o["win"]["deliveries"] = 1;
            o["win"]["timeLimitTicks"] = 100;
            mutate?.Invoke(o);
        });

        public static byte[] ReverseOnlyPath(bool oneWay, bool reversible) => Level(o =>
        {
            var edge = o["board"]["edges"][1];
            edge["from"] = "RED";
            edge["to"] = "J1";
            edge["oneWay"] = oneWay;
            edge["reversible"] = reversible;
        });

        public static byte[] ReverseSwitchRoute(bool oneWay, bool reversible) => Level(o =>
        {
            var reverse = o["board"]["edges"][1];
            reverse["from"] = "RED";
            reverse["to"] = "J1";
            reverse["oneWay"] = oneWay;
            reverse["reversible"] = reversible;
            ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E_ALT", "J1", "RED", 8));
            o["switches"] = new JArray(
                VFixtures.Switch("S1", "J1", 1, "E_RED", "E_ALT"));
        });
    }

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
            // The decoy carve-out is audible, not silent — L001's BLUE station warns.
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

        [TestCase(true, false, StageVerdictCode.Fail)]
        [TestCase(false, false, StageVerdictCode.Pass)]
        [TestCase(true, true, StageVerdictCode.Pass)]
        public void ReverseOnlyStationReachability_RequiresReversePermission(
            bool oneWay, bool reversible, StageVerdictCode expected)
        {
            var v = Check(DirectedValidationFixtures.ReverseOnlyPath(oneWay, reversible));
            Assert.That(v.Code, Is.EqualTo(expected), v.Detail);
            Assert.That(v.Blocks, Is.EqualTo(expected == StageVerdictCode.Fail));
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
            var level = DirectedValidationFixtures.Level(o =>
            {
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("J2", 5, 1));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E8", "J2", "RED", 5));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E9", "J2", "RED", 5));
                ((JArray)o["switches"]).Add(VFixtures.Switch("S2", "J2", 0, "E8", "E9"));
            });
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("orphan").IgnoreCase.And.Contain("S2"));
        }

        [Test]
        public void SwitchRoute_TrulyNonincidentWithItsNode_Fails()
        {
            var level = DirectedValidationFixtures.Level(o =>
            {
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("SIDE_A", 4, 1));
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("SIDE_B", 5, 1));
                ((JArray)o["board"]["edges"]).Add(
                    VFixtures.Edge("E_SIDE", "SIDE_A", "SIDE_B", 4));
                ((JArray)o["board"]["edges"]).Add(
                    VFixtures.Edge("E_ALT", "J1", "RED", 8));
                o["switches"] = new JArray(
                    VFixtures.Switch("S1", "J1", 1, "E_SIDE", "E_ALT"));
            });
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("orphan").IgnoreCase.And.Contain("E_SIDE"));
        }

        [TestCase(true, false, StageVerdictCode.Fail)]
        [TestCase(false, false, StageVerdictCode.Pass)]
        [TestCase(true, true, StageVerdictCode.Pass)]
        public void ReverseSwitchRoute_IsValidOnlyWhenReverseTraversalIsAllowed(
            bool oneWay, bool reversible, StageVerdictCode expected)
        {
            var v = Check(DirectedValidationFixtures.ReverseSwitchRoute(oneWay, reversible));
            Assert.That(v.Code, Is.EqualTo(expected), v.Detail);
            if (expected == StageVerdictCode.Fail)
                Assert.That(v.Detail, Does.Contain("E_RED").And.Contain("traversed"));
        }

        [Test]
        public void AcyclicHoldEdge_Fails()
        {
            var v = Check(DirectedValidationFixtures.Level(o =>
                o["board"]["edges"][1]["hold"] = true));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True);
            Assert.That(v.Detail, Does.Contain("hold edge E_RED").And.Contain("cycle"));
        }

        [Test]
        public void ReversibleHoldEdge_SuppliesItsOwnDirectedReturn()
        {
            var v = Check(DirectedValidationFixtures.Level(o =>
            {
                o["board"]["edges"][1]["hold"] = true;
                o["board"]["edges"][1]["reversible"] = true;
            }));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pass), v.Detail);
        }

        [Test]
        public void HoldEdge_WithSeparateDirectedReturn_Passes()
        {
            var v = Check(DirectedValidationFixtures.Level(o =>
            {
                o["board"]["edges"][1]["hold"] = true;
                ((JArray)o["board"]["edges"]).Add(
                    VFixtures.Edge("E_RETURN", "RED", "J1", 9));
            }));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pass), v.Detail);
        }

        [Test]
        public void ExactNonStraySupply_Passes()
        {
            var v = Check(DirectedValidationFixtures.Level());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pass), v.Detail);
        }

        [TestCase(false, StageVerdictCode.Fail)]
        [TestCase(true, StageVerdictCode.Pass)]
        public void ExtraWave_CountsOnlyWhenItIsNotStray(
            bool stray, StageVerdictCode expected)
        {
            var v = Check(DirectedValidationFixtures.Level(o =>
            {
                var extra = (JObject)o["waves"][0].DeepClone();
                extra["tick"] = 20;
                extra["stray"] = stray;
                ((JArray)o["waves"]).Add(extra);
            }));
            Assert.That(v.Code, Is.EqualTo(expected), v.Detail);
            if (expected == StageVerdictCode.Fail)
                Assert.That(v.Detail, Does.Contain("deliverable wave supply 2")
                    .And.Contain("win.deliveries 1").And.Contain("stray waves excluded"));
        }

        [Test]
        public void StrayOnlySupply_CannotSatisfyDeliveries()
        {
            var v = Check(DirectedValidationFixtures.Level(o =>
                o["waves"][0]["stray"] = true));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("deliverable wave supply 0")
                .And.Contain("win.deliveries 1"));
        }

        [Test]
        public void JunctionSpacing_Under1Point2_Fails()
        {
            // Two controlled junctions are 1 grid unit apart (< 1.2; CM-R07.6).
            var level = DirectedValidationFixtures.Level(o =>
            {
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("J2", 0, 0));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E4", "J1", "J2", 2));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E5", "J2", "RED", 5));
                ((JArray)o["board"]["edges"]).Add(VFixtures.Edge("E6", "J2", "RED", 5));
                o["switches"] = new JArray(
                    VFixtures.Switch("S1", "J1", 0, "E_RED", "E4"),
                    VFixtures.Switch("S2", "J2", 0, "E5", "E6"));
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
        public void ControlledBoard_ComputedBound_IsHandDerived12()
        {
            // (E_IN 5 + E_RED 7) x 1 delivery = 12 — explicit fixture arithmetic, independent
            // of campaign authoring changes; assert the complete equation, not a substring.
            var dto = VFixtures.Import(DirectedValidationFixtures.Level()).Dto;
            var v = LowerBoundStage.Check(dto, VFixtures.BareConfig());
            Assert.That(v.Value, Is.EqualTo("lowerBound=12 (minTravelTicks=12 x deliveries=1)"));
        }

        [Test]
        public void NoSlackRow_ReportsUnconfiguredAndDoesNotBlock()
        {
            var dto = VFixtures.Import(DirectedValidationFixtures.Level()).Dto;
            var v = LowerBoundStage.Check(dto, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Unconfigured));
            Assert.That(v.Detail, Is.EqualTo("UNCONFIGURED(lowerBoundSlack)"));
            Assert.That(v.Blocks, Is.False);
        }

        [Test]
        public void SlackRowPresent_PassesControlledBoard_AndBlocksAViolatingBoard()
        {
            var config = VFixtures.FullConfig(); // slack 2
            var ok = LowerBoundStage.Check(
                VFixtures.Import(DirectedValidationFixtures.Level()).Dto, config);
            Assert.That(ok.Code, Is.EqualTo(StageVerdictCode.Pass), "12 <= 100 + 2");

            var tight = DirectedValidationFixtures.Level(o =>
            {
                o["board"]["edges"][0]["travelTicks"] = 15;
                o["board"]["edges"][1]["travelTicks"] = 15;
                o["win"]["timeLimitTicks"] = 20;
            });
            var bad = LowerBoundStage.Check(VFixtures.Import(tight).Dto, config);
            Assert.That(bad.Code, Is.EqualTo(StageVerdictCode.Fail),
                "30 > 20 + 2 blocks deterministically");
            Assert.That(bad.Blocks, Is.True);
        }

        [TestCase(true, false, StageVerdictCode.Fail)]
        [TestCase(false, false, StageVerdictCode.Unconfigured)]
        [TestCase(true, true, StageVerdictCode.Unconfigured)]
        public void ReverseOnlyPath_RequiresTheSameTraversalPermissionAsStaticAnalysis(
            bool oneWay, bool reversible, StageVerdictCode expected)
        {
            var dto = VFixtures.Import(
                DirectedValidationFixtures.ReverseOnlyPath(oneWay, reversible)).Dto;
            var v = LowerBoundStage.Check(dto, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(expected), v.Detail);
            if (expected == StageVerdictCode.Unconfigured)
                Assert.That(v.Value,
                    Is.EqualTo("lowerBound=12 (minTravelTicks=12 x deliveries=1)"));
            else
                Assert.That(v.Detail, Does.Contain("no colour-compatible").IgnoreCase);
        }
    }
}
