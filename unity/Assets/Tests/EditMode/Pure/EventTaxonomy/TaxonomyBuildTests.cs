using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Services;

namespace CatMetro.Tests.EventTaxonomy
{
    // CM-C9 criteria 3-6: required params fail typed per (row, key) — 124 pairs; the param set
    // is CLOSED (A-C8-11's value-level wall); the seven CSV value domains are enforced and no
    // domain is invented; unknown names never become events. TryBuild never throws.
    public sealed class TaxonomyBuildTests
    {
        // --- criterion 3 ---
        [Test, TestCaseSource(typeof(TaxonomyFixtures), nameof(TaxonomyFixtures.RequiredPairs))]
        public void MissingRequiredKey_IsTypedFailure(string name, string requiredKey)
        {
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == name);
            var p = TaxonomyFixtures.CanonicalParams(row, includeOptional: false);
            p.Remove(requiredKey);
            AnalyticsEvent e = default;
            string error = null;
            Assert.DoesNotThrow(() => Taxonomy.TryBuild(name, p, out e, out error),
                "typed failure, never a throw (A-C9-4)");
            bool ok = Taxonomy.TryBuild(name, p, out e, out error);
            Assert.That(ok, Is.False, name + " built without required " + requiredKey);
            Assert.That(error, Does.Contain(requiredKey), "the missing key is named");
        }

        [Test]
        public void RequiredPair_Count_Is124() =>
            Assert.That(TaxonomyFixtures.RequiredPairs().Count(), Is.EqualTo(124),
                "the CSV's required_params entries across rows 2-46 — a silently shrinking " +
                "case source cannot pass");

        [Test, TestCaseSource(typeof(TaxonomyFixtures), nameof(TaxonomyFixtures.RowNames))]
        public void CompleteRequiredSet_Builds(string name)
        {
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == name);
            bool ok = Taxonomy.TryBuild(name,
                TaxonomyFixtures.CanonicalParams(row, includeOptional: false),
                out var e, out var error);
            Assert.That(ok, Is.True, name + ": " + error);
            Assert.That(e.Name, Is.EqualTo(name));
        }

        [Test]
        public void RestoreStarted_BuildsFromEmptyObject()
        {
            bool ok = Taxonomy.TryBuild("restore_started", new JObject(), out var e, out var error);
            Assert.That(ok, Is.True, "the zero-required-params case is asserted, not skipped: " + error);
        }

        // --- criterion 4: closed param set ---
        [Test, TestCaseSource(typeof(TaxonomyFixtures), nameof(TaxonomyFixtures.RowNames))]
        public void UndeclaredKey_IsRejected(string name)
        {
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == name);
            var p = TaxonomyFixtures.CanonicalParams(row, includeOptional: false);
            p["zz_unknown"] = 1;
            bool ok = Taxonomy.TryBuild(name, p, out _, out var error);
            Assert.That(ok, Is.False, name + " accepted an undeclared key");
            Assert.That(error, Does.Contain("zz_unknown"));
        }

        [Test]
        public void LevelStarted_RejectsLedgerShapedKeys()
        {
            // CM-R43.4(d)'s regression guard in its testable form
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == "level_started");
            foreach (var ledgerKey in new[] { "balance_after", "entitlement_id" })
            {
                var p = TaxonomyFixtures.CanonicalParams(row, includeOptional: false);
                p[ledgerKey] = 7;
                bool ok = Taxonomy.TryBuild("level_started", p, out _, out var error);
                Assert.That(ok, Is.False, "level_started accepted " + ledgerKey);
                Assert.That(error, Does.Contain(ledgerKey));
            }
        }

        // --- criterion 5: the seven declared domains, and nothing else ---
        [Test, TestCaseSource(typeof(TaxonomyFixtures), nameof(TaxonomyFixtures.DomainCases))]
        public void DeclaredDomain_AcceptsListed_RejectsOther(string name, string param, string[] allowed)
        {
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == name);
            foreach (var v in allowed)
            {
                var p = TaxonomyFixtures.CanonicalParams(row, includeOptional: false);
                p[param] = v;
                Assert.That(Taxonomy.TryBuild(name, p, out _, out var err), Is.True,
                    name + "." + param + "=" + v + " must be accepted: " + err);
            }
            var bad = TaxonomyFixtures.CanonicalParams(row, includeOptional: false);
            bad[param] = "zz_not_in_domain";
            Assert.That(Taxonomy.TryBuild(name, bad, out _, out var error), Is.False,
                name + "." + param + " accepted an out-of-domain value");
            Assert.That(error, Does.Contain(param));
        }

        [Test]
        public void DomainCase_Count_Is7() =>
            Assert.That(TaxonomyFixtures.DomainCases().Count(), Is.EqualTo(7),
                "exactly the CSV's seven inline domains — none invented (NEW-Q20)");

        [Test]
        public void StoredParamName_IsTheBareToken()
        {
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == "rewind_used");
            bool ok = Taxonomy.TryBuild("rewind_used",
                TaxonomyFixtures.CanonicalParams(row, includeOptional: false),
                out var e, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(e.Params.Property("source"), Is.Not.Null,
                "the stored key is `source`, not `source(free|purchased|rewarded)`");
        }

        [Test]
        public void DomainlessParam_AcceptsArbitraryScalar()
        {
            var row = TaxonomyFixtures.ParseRows().Single(r => r.Name == "level_started");
            var p = TaxonomyFixtures.CanonicalParams(row, includeOptional: false);
            p["level_id"] = "any_weird_string_the_csv_never_constrained";
            Assert.That(Taxonomy.TryBuild("level_started", p, out _, out var err), Is.True, err);
        }

        // --- criterion 6 ---
        [Test]
        public void UnknownEventName_NeverBecomesAnEvent()
        {
            bool ok = Taxonomy.TryBuild("not_an_event", new JObject(), out var e, out var error);
            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("not_an_event"));
            Assert.That(e.Name, Is.Null.Or.Empty, "no event is produced");
        }

        [Test]
        public void NearMissName_IsAlsoRejected()
        {
            bool ok = Taxonomy.TryBuild("level_start", new JObject(), out _, out var error);
            Assert.That(ok, Is.False, "level_start is not level_started");
            Assert.That(error, Does.Contain("level_start"));
        }
    }
}
