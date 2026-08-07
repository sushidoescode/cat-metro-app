using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Services;

namespace CatMetro.Tests.EventTaxonomy
{
    // CM-C9 criteria 1 + 2: the table IS the CSV (drift-tested field-for-field), and the 45
    // factories are in bijection with it, both directions, with the offending side named.
    public sealed class TaxonomyTableTests
    {
        // --- criterion 1 ---
        [Test]
        public void Rows_Count_Is45() =>
            Assert.That(Taxonomy.Rows.Count, Is.EqualTo(45),
                "exactly the CSV's rows — nothing invented, nothing dropped");

        [Test]
        public void Csv_Has46NonEmptyLines() =>
            Assert.That(TaxonomyFixtures.NonEmptyCsvLines().Count, Is.EqualTo(46),
                "header + 45 rows; a 46th row is TD-01/NEW-Q36's to add (stop condition 1)");

        [Test, TestCaseSource(typeof(TaxonomyFixtures), nameof(TaxonomyFixtures.RowNames))]
        public void Row_MatchesCsv_FieldForField(string name)
        {
            var parsed = TaxonomyFixtures.ParseRows().Single(r => r.Name == name);
            var row = Taxonomy.Rows.SingleOrDefault(r => r.Name == name);
            Assert.That(row, Is.Not.Null, "table row missing for CSV row: " + name);
            Assert.That(row.Area, Is.EqualTo(parsed.Area), name + ".Area");
            Assert.That(row.RequiredParams, Is.EqualTo(parsed.RequiredParams), name + ".RequiredParams");
            Assert.That(row.OptionalParams, Is.EqualTo(parsed.OptionalParams), name + ".OptionalParams");
            Assert.That(row.UserProperties, Is.EqualTo(parsed.UserProperties), name + ".UserProperties");
            Assert.That(row.Destinations, Is.EqualTo(parsed.Destinations), name + ".Destinations");
            Assert.That(row.PrivacyClass, Is.EqualTo(parsed.PrivacyClass), name + ".PrivacyClass");
            Assert.That(row.QaProcedure, Is.EqualTo(parsed.QaProcedure), name + ".QaProcedure");
            Assert.That(row.ValueDomains.Keys.OrderBy(k => k),
                Is.EqualTo(parsed.ValueDomains.Keys.OrderBy(k => k)), name + ".ValueDomains keys");
            foreach (var kv in parsed.ValueDomains)
                Assert.That(row.ValueDomains[kv.Key], Is.EqualTo(kv.Value),
                    name + ".ValueDomains[" + kv.Key + "]");
        }

        [Test]
        public void Every_PrivacyClass_IsOneOfTheFour()
        {
            var four = new[] { "behavioral_no_pii", "behavioral_ad", "transactional", "diagnostic" };
            foreach (var row in Taxonomy.Rows)
                Assert.That(four, Does.Contain(row.PrivacyClass),
                    row.Name + " carries an unknown privacy class (CM-R45.1, PRD:715)");
        }

        // --- criterion 2 ---
        public static IReadOnlyList<MethodInfo> Factories() =>
            typeof(Events).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == typeof(AnalyticsEvent)).ToList();

        public static string SnakeCase(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                if (char.IsUpper(pascal[i]) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(pascal[i]));
            }
            return sb.ToString();
        }

        // The comparison helper the negative case reuses — reports each offending side by name.
        public static List<string> BijectionDiff(IEnumerable<string> factoryNames,
            IEnumerable<string> rowNames)
        {
            var f = new HashSet<string>(factoryNames);
            var r = new HashSet<string>(rowNames);
            var diffs = new List<string>();
            foreach (var extra in f.Except(r).OrderBy(x => x))
                diffs.Add("factory with no taxonomy row: " + extra);
            foreach (var missing in r.Except(f).OrderBy(x => x))
                diffs.Add("taxonomy row with no factory: " + missing);
            return diffs;
        }

        [Test]
        public void Factory_Count_Is45() =>
            Assert.That(Factories().Count, Is.EqualTo(45),
                "CM-R43.1: exactly 45 emittable event types");

        [Test]
        public void Factories_And_Rows_AreInBijection()
        {
            var diffs = BijectionDiff(
                Factories().Select(m => SnakeCase(m.Name)),
                Taxonomy.Rows.Select(r => r.Name));
            Assert.That(diffs, Is.Empty, string.Join("; ", diffs));
        }

        [Test]
        public void BijectionHelper_ReportsA46th()
        {
            // the testable form of "adding a 46th without a taxonomy row fails the build"
            var withExtra = Taxonomy.Rows.Select(r => r.Name).Append("zz_not_a_row");
            var diffs = BijectionDiff(withExtra, Taxonomy.Rows.Select(r => r.Name));
            Assert.That(diffs, Has.Count.EqualTo(1));
            Assert.That(diffs[0], Does.Contain("zz_not_a_row"));
        }
    }
}
