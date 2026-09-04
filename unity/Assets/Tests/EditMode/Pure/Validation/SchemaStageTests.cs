using NUnit.Framework;
using CatMetro.Content.Validation;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.Validation
{
    // Criterion 2: stage 1 validates against the REAL docs/plan/data/level_schema.json bytes —
    // one malformed fixture per named rule class (>= 6) plus the L001 pass.
    [TestFixture]
    public class SchemaStageTests
    {
        private static StageVerdict Check(byte[] level) =>
            SchemaStage.Check(VFixtures.SchemaBytes(), level);

        private static void AssertFails(byte[] level, string detailFragment)
        {
            var v = Check(level);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True, "schema failures block");
            Assert.That(v.Detail, Does.Contain(detailFragment).IgnoreCase,
                "the failing rule is named");
        }

        [Test]
        public void L001_Passes()
        {
            var v = Check(VFixtures.L001Bytes());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pass), v.Detail);
        }

        [Test]
        public void IdPattern_Violation_Fails() =>
            AssertFails(VFixtures.Level(o => o["id"] = "X1"), "pattern");

        [Test]
        public void SchemaVersionConst_Violation_Fails() =>
            AssertFails(VFixtures.Level(o => o["schemaVersion"] = 3), "const");

        [Test]
        public void BandEnum_Violation_Fails() =>
            AssertFails(VFixtures.Level(o => o["meta"]["band"] = "impossible-band"), "enum");

        [Test]
        public void MechanicsEnum_Violation_Fails() =>
            AssertFails(VFixtures.Level(o => o["meta"]["mechanics"] = new JArray("jetpack")), "enum");

        [Test]
        public void AdditionalProperty_AtRoot_Fails() =>
            AssertFails(VFixtures.Level(o => o["surprise"] = 1), "additional");

        [Test]
        public void AdditionalProperty_Nested_Fails() =>
            AssertFails(VFixtures.Level(o => o["win"]["cheatMode"] = true), "additional");

        [Test]
        public void MissingRequired_Fails() =>
            AssertFails(VFixtures.Level(o => o.Remove("win")), "required");

        [Test]
        public void NumericMinimum_Violation_Fails() =>
            AssertFails(VFixtures.Level(o => o["win"]["stars"]["two"] = 0), "minimum");

        [TestCase("round")]
        [TestCase("square")]
        [TestCase("triangle")]
        public void RuntimeMechanicFields_AcceptExplicitValues(string shape)
        {
            var level = VFixtures.Level(o =>
            {
                o["stations"][0]["shape"] = shape;
                o["waves"][0]["shape"] = shape;
                o["board"]["edges"][0]["tunnel"] = true;
                o["board"]["edges"][0]["hold"] = false;
                o["waves"][0]["stray"] = true;
            });
            var verdict = Check(level);
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Pass), verdict.Detail);
        }

        [TestCase("station-shape")]
        [TestCase("wave-shape")]
        [TestCase("tunnel")]
        [TestCase("hold")]
        [TestCase("stray")]
        public void RuntimeMechanicFields_RejectWrongTypes(string field)
        {
            AssertFails(VFixtures.Level(o =>
            {
                switch (field)
                {
                    case "station-shape": o["stations"][0]["shape"] = 7; break;
                    case "wave-shape": o["waves"][0]["shape"] = 7; break;
                    case "tunnel": o["board"]["edges"][0]["tunnel"] = "yes"; break;
                    case "hold": o["board"]["edges"][0]["hold"] = "yes"; break;
                    case "stray": o["waves"][0]["stray"] = "yes"; break;
                }
            }), "type");
        }

        [TestCase("station")]
        [TestCase("wave")]
        public void ShapeFields_RejectUnknownValues(string owner)
        {
            AssertFails(VFixtures.Level(o =>
            {
                if (owner == "station") o["stations"][0]["shape"] = "hexagon";
                else o["waves"][0]["shape"] = "hexagon";
            }), "enum");
        }

        [TestCase("station")]
        [TestCase("wave")]
        [TestCase("edge")]
        public void RuntimeMechanicObjects_RemainStrict(string owner)
        {
            AssertFails(VFixtures.Level(o =>
            {
                if (owner == "station") o["stations"][0]["surprise"] = true;
                else if (owner == "wave") o["waves"][0]["surprise"] = true;
                else o["board"]["edges"][0]["surprise"] = true;
            }), "additional");
        }

        [Test]
        public void RuntimeMechanicFields_DeclareLockedDefaults()
        {
            var schema = JObject.Parse(System.Text.Encoding.UTF8.GetString(VFixtures.SchemaBytes()));
            var properties = schema["properties"];
            var station = properties["stations"]["items"]["properties"];
            var wave = properties["waves"]["items"]["properties"];
            var edge = properties["board"]["properties"]["edges"]["items"]["properties"];

            Assert.That(station["shape"]["enum"].Values<string>(),
                Is.EqualTo(new[] { "round", "square", "triangle" }));
            Assert.That((string)station["shape"]["default"], Is.EqualTo("round"));
            Assert.That(wave["shape"]["enum"].Values<string>(),
                Is.EqualTo(new[] { "round", "square", "triangle" }));
            Assert.That((string)wave["shape"]["default"], Is.EqualTo("round"));
            Assert.That((bool)wave["stray"]["default"], Is.False);
            Assert.That((bool)edge["tunnel"]["default"], Is.False);
            Assert.That((bool)edge["hold"]["default"], Is.False);
        }

        [Test]
        public void UnparseableBytes_Fail()
        {
            var v = Check(System.Text.Encoding.UTF8.GetBytes("{nope"));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True);
        }

        [Test]
        public void UnsupportedSchemaKeyword_FailsClosed()
        {
            // A schema using a keyword outside the implemented subset must fail the stage, never
            // silently skip the rule (handoff §Stage-1).
            var schema = System.Text.Encoding.UTF8.GetBytes(
                "{ \"type\": \"object\", \"propertyNames\": { \"pattern\": \"^x\" } }");
            var v = SchemaStage.Check(schema, VFixtures.L001Bytes());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("unsupported schema keyword"));
        }
    }
}
