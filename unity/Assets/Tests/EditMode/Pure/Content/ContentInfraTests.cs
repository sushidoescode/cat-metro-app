using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Content
{
    [TestFixture]
    public class ContentInfraTests
    {
        private static XDocument Load(string rel) =>
            XDocument.Load(Path.Combine(Fixtures.RepoRoot(), rel.Replace('/', Path.DirectorySeparatorChar)));

        [Test] // criterion 2
        public void ContentCsproj_LinksExactlyTheContentGlob()
        {
            var doc = Load("dotnet/CatMetro.Content/CatMetro.Content.csproj");
            Assert.That(doc.Descendants("TargetFramework").Single().Value, Is.EqualTo("netstandard2.1"));
            var includes = doc.Descendants("Compile").Select(c => c.Attribute("Include")?.Value).Where(v => v != null).ToArray();
            Assert.That(includes, Is.EqualTo(new[] { "../../unity/Assets/Scripts/Content/**/*.cs" }));
        }

        [Test] // criterion 2
        public void ServicesCsproj_LinksExactlyTheServicesGlob()
        {
            var doc = Load("dotnet/CatMetro.Services/CatMetro.Services.csproj");
            Assert.That(doc.Descendants("TargetFramework").Single().Value, Is.EqualTo("netstandard2.1"));
            var includes = doc.Descendants("Compile").Select(c => c.Attribute("Include")?.Value).Where(v => v != null).ToArray();
            Assert.That(includes, Is.EqualTo(new[] { "../../unity/Assets/Scripts/Services/**/*.cs" }));
        }

        [Test] // criterion 4
        public void SingleSettingsSite_TypeNameHandlingNone_MaxDepthConstant()
        {
            Assert.That(ContentJson.Settings.TypeNameHandling, Is.EqualTo(TypeNameHandling.None),
                "permanent rule — ADR-0008 MUST 1");
            Assert.That(ContentJson.Settings.MaxDepth, Is.EqualTo(ContentBounds.CONTENT_MAX_JSON_DEPTH),
                "criterion 6: the configured parser depth equals the constant");
        }

        [Test] // criterion 5 — every constant, every value
        public void ContentBounds_EveryConstantMatchesItsCitation()
        {
            Assert.That(ContentBounds.CONTENT_MAX_FILE_BYTES, Is.EqualTo(262144));
            Assert.That(ContentBounds.CONTENT_MAX_JSON_DEPTH, Is.EqualTo(16));
            Assert.That(ContentBounds.MAX_NODES, Is.EqualTo(40));
            Assert.That(ContentBounds.MAX_EDGES, Is.EqualTo(70));
            Assert.That(ContentBounds.MAX_WAVES, Is.EqualTo(30));
            Assert.That(ContentBounds.MAX_SWITCHES, Is.EqualTo(10));
            Assert.That(ContentBounds.MAX_SOURCES, Is.EqualTo(6));
            Assert.That(ContentBounds.MAX_STATIONS, Is.EqualTo(6));
            Assert.That(ContentBounds.TRAVEL_TICKS_MIN, Is.EqualTo(1));
            Assert.That(ContentBounds.TRAVEL_TICKS_MAX, Is.EqualTo(40));
            Assert.That(ContentBounds.TIME_LIMIT_TICKS_MIN, Is.EqualTo(20));
            Assert.That(ContentBounds.TIME_LIMIT_TICKS_MAX, Is.EqualTo(4000));
            Assert.That(ContentBounds.QUEUE_CAPACITY_MIN, Is.EqualTo(1));
            Assert.That(ContentBounds.QUEUE_CAPACITY_MAX, Is.EqualTo(8));
            Assert.That(ContentBounds.STATION_CAPACITY_MIN, Is.EqualTo(1));
            Assert.That(ContentBounds.STATION_CAPACITY_MAX, Is.EqualTo(12));
            Assert.That(ContentBounds.INITIAL_ROUTE_MIN, Is.EqualTo(0));
            Assert.That(ContentBounds.INITIAL_ROUTE_MAX, Is.EqualTo(2));
            Assert.That(ContentBounds.ROUTES_MIN, Is.EqualTo(2));
            Assert.That(ContentBounds.ROUTES_MAX, Is.EqualTo(3));
            Assert.That(ContentBounds.WAVE_COUNT_MIN, Is.EqualTo(1));
            Assert.That(ContentBounds.WAVE_COUNT_MAX, Is.EqualTo(8));
            Assert.That(ContentBounds.SPACING_TICKS_MIN, Is.EqualTo(1));
            Assert.That(ContentBounds.SPACING_TICKS_MAX, Is.EqualTo(40));
            Assert.That(ContentBounds.MIN_ACTION_WINDOW_TICKS_FLOOR, Is.EqualTo(3));
        }
    }

    // Criterion 3: reflection over every DTO type in the Content assembly.
    [TestFixture]
    public class ContentImmutabilityTests
    {
        [Test]
        public void EveryDto_IsSealedReadonlyAndArrayFree()
        {
            var dtoTypes = typeof(LevelDto).Assembly.GetTypes()
                .Where(t => t.Namespace == "CatMetro.Content" && t.Name.EndsWith("Dto"))
                .ToArray();
            Assert.That(dtoTypes.Length, Is.GreaterThanOrEqualTo(10), "the DTO family must be discovered");
            foreach (var t in dtoTypes)
            {
                Assert.That(t.IsSealed, Is.True, $"{t.Name} must be sealed");
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Public |
                                             System.Reflection.BindingFlags.NonPublic))
                    Assert.That(f.IsInitOnly, Is.True, $"{t.Name}.{f.Name} must be readonly");
                foreach (var p in t.GetProperties())
                {
                    Assert.That(p.SetMethod, Is.Null, $"{t.Name}.{p.Name} must have no setter");
                    Assert.That(p.PropertyType.IsArray, Is.False, $"{t.Name}.{p.Name} must not expose an array");
                }
                foreach (var f in t.GetFields()) // public only
                    Assert.That(f.FieldType.IsArray, Is.False, $"{t.Name}.{f.Name} must not expose an array");
            }
        }
    }
}
