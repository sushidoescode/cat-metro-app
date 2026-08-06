using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Services;
using CatMetro.Tests.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.EventTaxonomy
{
    // CM-C9 criteria 8 (reflection half of the metrics-only wall), 11 (every canonical event
    // fits QUEUE_EVENT_MAX_BYTES, read from config, never re-declared) and 12 (the queue is
    // used and not touched: 45 factory-built events persist and reload in order, stable ids).
    public sealed class TaxonomyWallTests
    {
        // --- criterion 8, reflection half ---
        [Test]
        public void NoFactoryParameter_IsDeclaredInTheSaveNamespace()
        {
            foreach (var m in TaxonomyTableTests.Factories())
            foreach (var p in m.GetParameters())
                Assert.That(p.ParameterType.Namespace,
                    Is.Not.EqualTo("CatMetro.Application.Save"),
                    m.Name + "(" + p.Name + ") reaches into the save/ledger types — " +
                    "the metrics-only wall (A-C8-11)");
        }

        // --- criterion 11 ---
        private static int QueueEventMaxBytes()
        {
            // read from config/runtime_bounds.json (the SFixtures bytes are that file);
            // never re-declared as a literal in taxonomy sources (the wrapper greps for that)
            var o = JObject.Parse(System.Text.Encoding.UTF8.GetString(SFixtures.RepoBoundsBytes()));
            return (int)o["QUEUE_EVENT_MAX_BYTES"];
        }

        private static int RecordBytesTheWayCmC8ComputesThem(AnalyticsEvent e, long ord)
        {
            // AnalyticsQueue.cs:247-248 (A-C8-9): UTF8 byte count of the compact record object
            var record = new JObject
            {
                ["ord"] = ord,
                ["name"] = e.Name ?? "",
                ["params"] = e.Params ?? new JObject(),
            };
            return System.Text.Encoding.UTF8.GetByteCount(
                record.ToString(Newtonsoft.Json.Formatting.None));
        }

        [Test, TestCaseSource(typeof(TaxonomyFixtures), nameof(TaxonomyFixtures.RowNames))]
        public void CanonicalEvent_FitsTheQueueByteBound(string name)
        {
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == name);
            bool ok = Taxonomy.TryBuild(name,
                TaxonomyFixtures.CanonicalParams(row, includeOptional: true),
                out var e, out var error);
            Assert.That(ok, Is.True, name + ": " + error);
            int bytes = RecordBytesTheWayCmC8ComputesThem(e, ord: 9_999_999);
            Assert.That(bytes, Is.LessThan(QueueEventMaxBytes()),
                name + " canonical record is " + bytes + " bytes — ADR-0006:244-246 says an " +
                "oversize event is a bug; no free text is permitted in the taxonomy");
        }

        // --- criterion 9's lockstep case: the wrapper's hard-coded dark list cannot drift
        // from the table's area ∈ {economy, ads, monetization} set ---
        [Test]
        public void WrapperDarkList_EqualsTheTablesDarkSet()
        {
            string wrapper = System.IO.File.ReadAllText(System.IO.Path.Combine(
                TaxonomyFixtures.RepoRoot(), "tests", "taxonomy", "taxonomy.test.sh"));
            var m = System.Text.RegularExpressions.Regex.Match(wrapper, "\nDARK='([^']+)'");
            Assert.That(m.Success, Is.True, "the wrapper's DARK= line is missing");
            var wrapperNames = m.Groups[1].Value.Split('|').OrderBy(x => x).ToList();
            var darkFromCsv = TaxonomyFixtures.DarkRowNames()
                .Select(n => string.Concat(n.Split('_')
                    .Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1))))
                .OrderBy(x => x).ToList();
            Assert.That(darkFromCsv.Count, Is.EqualTo(15), "2 economy + 5 ads + 8 monetization");
            Assert.That(wrapperNames, Is.EqualTo(darkFromCsv),
                "edit the shell list and the CSV together or not at all");
        }

        // --- criterion 12 ---
        private static object[] CanonicalArgsFor(MethodInfo factory)
        {
            var args = factory.GetParameters().Select(p =>
            {
                var t = p.ParameterType;
                if (t == typeof(bool)) return (object)true;
                if (t == typeof(int)) return 3;
                if (t == typeof(long)) return 3L;
                if (t == typeof(double)) return 3.5;
                // enumerated params must use a listed value: match by normalized name
                string bare = p.Name.Replace("_", "").ToLowerInvariant();
                foreach (var row in TaxonomyFixtures.ParseRows())
                foreach (var kv in row.ValueDomains)
                    if (kv.Key.Replace("_", "").ToLowerInvariant() == bare)
                        return kv.Value[0];
                return "v";
            }).ToArray();
            return args;
        }

        [Test]
        public void AllFortyFive_FactoryEvents_PersistAndReload_InOrder_StableIds()
        {
            using (var root = new SFixtures.TempRoot())
            {
                var (q, _, _) = QFixtures.Queue(root);
                var factories = TaxonomyTableTests.Factories()
                    .OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
                Assert.That(factories.Count, Is.EqualTo(45), "criterion 2 first");
                foreach (var f in factories)
                {
                    var e = (AnalyticsEvent)f.Invoke(null, CanonicalArgsFor(f));
                    q.Log(e);
                }
                Assert.That(q.QueuedEventCount, Is.EqualTo(45));
                var idsBefore = q.Snapshot().Select(r => r.Id).ToList();
                var namesBefore = q.Snapshot().Select(r => r.Name).ToList();

                var (q2, _, _) = QFixtures.Queue(root); // fresh instance, same storage root
                Assert.That(q2.QueuedEventCount, Is.EqualTo(45), "persisted and reloaded");
                Assert.That(q2.Snapshot().Select(r => r.Name), Is.EqualTo(namesBefore),
                    "order is stable across reload");
                Assert.That(q2.Snapshot().Select(r => r.Id), Is.EqualTo(idsBefore),
                    "ids are stable across reload (A-C8-6 untouched)");
            }
        }
    }
}
