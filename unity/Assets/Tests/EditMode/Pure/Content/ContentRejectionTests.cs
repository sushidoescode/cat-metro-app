using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Content
{
    [TestFixture]
    public class ContentRejectionTests
    {
        private static byte[] Fixture(string name) =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "tests", "fixtures", "content-bad", name));

        private static ContentResult<ImportedLevel> ImportNoThrow(byte[] bytes)
        {
            ContentResult<ImportedLevel> r = default;
            Assert.DoesNotThrow(() => r = LevelImporter.Import(bytes), "no exception may escape the importer");
            return r;
        }

        [Test] // criterion 6a — rejected BEFORE deserialization
        public void OversizePayload_TypedFailure()
        {
            var r = ImportNoThrow(new byte[ContentBounds.CONTENT_MAX_FILE_BYTES + 1]);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.FileTooLarge));
        }

        [Test] // criterion 6b — depth pre-scan + configured parser depth equals the constant
        public void DepthBomb_TypedFailure()
        {
            var deep = new string('[', ContentBounds.CONTENT_MAX_JSON_DEPTH + 4)
                     + new string(']', ContentBounds.CONTENT_MAX_JSON_DEPTH + 4);
            var r = ImportNoThrow(System.Text.Encoding.UTF8.GetBytes(deep));
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.TooDeep));
            Assert.That(ContentJson.Settings.MaxDepth, Is.EqualTo(ContentBounds.CONTENT_MAX_JSON_DEPTH));
        }

        [Test] // review F1a — comments cannot smuggle brackets past the depth scan
        public void CommentPrefixedDocument_Rejected()
        {
            var l001 = File.ReadAllText(Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json"));
            foreach (var payload in new[]
            {
                "/*" + new string('}', 60) + "*/" + l001,          // closers hidden in a block comment
                "// header comment\n" + l001,                       // line comment
                "/* \" */" + l001,                                  // quote inside a comment
                "/* not JSON at all */",
            })
            {
                var r = ImportNoThrow(System.Text.Encoding.UTF8.GetBytes(payload));
                Assert.That(r.Ok, Is.False, "comments are never valid content JSON");
                Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.MalformedJson), payload.Substring(0, 12));
            }
        }

        [Test] // review F1 — the boundary itself: exactly MAX depth is not TooDeep
        public void ExactlyMaxDepth_IsNotRejectedForDepth()
        {
            var atLimit = new string('[', ContentBounds.CONTENT_MAX_JSON_DEPTH)
                        + new string(']', ContentBounds.CONTENT_MAX_JSON_DEPTH);
            var r = ImportNoThrow(System.Text.Encoding.UTF8.GetBytes(atLimit));
            Assert.That(r.Ok, Is.False, "an array root is still not a level");
            Assert.That(r.Error.Kind, Is.Not.EqualTo(ContentErrorKind.TooDeep), "depth at the limit passes the depth gate");
        }

        // criterion 7 — one fixture per rule, typed discriminant, nothing escapes
        private static IEnumerable<TestCaseData> RuleCases()
        {
            yield return new TestCaseData("bad-travel.json", ContentErrorKind.BoundViolation, "travelTicks");
            yield return new TestCaseData("bad-timelimit.json", ContentErrorKind.BoundViolation, "timeLimitTicks");
            yield return new TestCaseData("bad-queuecap.json", ContentErrorKind.BoundViolation, "queueCapacity");
            yield return new TestCaseData("bad-initialroute.json", ContentErrorKind.BoundViolation, "initialRoute");
            yield return new TestCaseData("dangling-ref.json", ContentErrorKind.DanglingReference, "NOPE");
            yield return new TestCaseData("over-cap.json", ContentErrorKind.CollectionOverCap, "nodes");
            yield return new TestCaseData("bad-schemaversion.json", ContentErrorKind.SchemaVersionMismatch, "3");
        }

        [TestCaseSource(nameof(RuleCases))]
        public void MalformedLevel_TypedFailurePerRule(string fixture, ContentErrorKind kind, string detailToken)
        {
            var r = ImportNoThrow(Fixture(fixture));
            Assert.That(r.Ok, Is.False, fixture);
            Assert.That(r.Error.Kind, Is.EqualTo(kind), $"{fixture}: {r.Error}");
            Assert.That(r.Error.Detail, Does.Contain(detailToken), $"{fixture} detail must name the offender");
        }
    }

    // Criterion 8: the fuzz corpus — >=3 files per class, >=24 total; every case typed-failure,
    // none throws (RK-34).
    [TestFixture]
    public class ContentFuzzTests
    {
        private static string FuzzDir =>
            Path.Combine(Fixtures.RepoRoot(), "tests", "fixtures", "content-bad", "fuzz");

        public static IEnumerable<TestCaseData> Corpus() =>
            Directory.EnumerateFiles(FuzzDir, "*.json").OrderBy(p => p)
                .Select(p => new TestCaseData(p).SetName("Fuzz_" + Path.GetFileNameWithoutExtension(p)));

        [Test]
        public void Corpus_CoversEveryClassWithAtLeastThree()
        {
            var names = Directory.EnumerateFiles(FuzzDir, "*.json").Select(Path.GetFileName).ToArray();
            Assert.That(names.Length, Is.GreaterThanOrEqualTo(24));
            foreach (var cls in new[] { "trunc", "depth", "huge", "dupid", "dangle", "nan", "dupkey", "bom" })
                Assert.That(names.Count(n => n.StartsWith(cls + "-")), Is.GreaterThanOrEqualTo(3), cls);
        }

        // Review F3: every class pins its DISCRIMINANT, not just "some error" — the weak
        // assertion is what hid the dead DuplicateKey branch.
        private static readonly Dictionary<string, ContentErrorKind[]> ExpectedByClass =
            new Dictionary<string, ContentErrorKind[]>
            {
                ["trunc"] = new[] { ContentErrorKind.MalformedJson },
                ["depth"] = new[] { ContentErrorKind.TooDeep },
                ["huge"] = new[] { ContentErrorKind.BoundViolation, ContentErrorKind.CollectionOverCap },
                ["dupid"] = new[] { ContentErrorKind.DuplicateId },
                ["dangle"] = new[] { ContentErrorKind.DanglingReference },
                ["nan"] = new[] { ContentErrorKind.IntegerExpected, ContentErrorKind.MalformedJson },
                ["dupkey"] = new[] { ContentErrorKind.DuplicateKey },
                ["bom"] = new[] { ContentErrorKind.EncodingError, ContentErrorKind.MalformedJson },
            };

        [TestCaseSource(nameof(Corpus))]
        public void FuzzCase_TypedFailureNeverThrows(string path)
        {
            ContentResult<ImportedLevel> r = default;
            Assert.DoesNotThrow(() => r = LevelImporter.Import(File.ReadAllBytes(path)));
            var name = Path.GetFileName(path);
            Assert.That(r.Ok, Is.False, $"{name} must be rejected");
            Assert.That(r.Error, Is.Not.Null);
            var cls = name.Substring(0, name.IndexOf('-'));
            Assert.That(ExpectedByClass.ContainsKey(cls), Is.True, $"unmapped fuzz class '{cls}'");
            Assert.That(ExpectedByClass[cls], Does.Contain(r.Error.Kind),
                $"{name}: expected one of [{string.Join(",", ExpectedByClass[cls])}], got {r.Error}");
        }
    }
}
