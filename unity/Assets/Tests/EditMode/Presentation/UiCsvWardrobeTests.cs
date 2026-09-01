using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Presentation.Strings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Presentation
{
    public sealed class UiCsvWardrobeTests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";
        private const string CatalogPath = "Assets/Resources/Cosmetics/cosmetic_catalog.json";

        private static readonly string[] RequiredKeys =
        {
            "wardrobe.entry", "wardrobe.title", "wardrobe.profile", "wardrobe.back",
            "wardrobe.buy", "wardrobe.restore", "wardrobe.restore.running",
            "wardrobe.cat.red_tabby", "wardrobe.cat.blue_siamese",
            "wardrobe.cat.yellow_longhair", "wardrobe.tab.outfit",
            "wardrobe.tab.accessory", "wardrobe.tab.frame", "wardrobe.state.equipped",
            "wardrobe.state.owned", "wardrobe.action.equip", "wardrobe.action.unequip",
            "wardrobe.action.rewarded", "wardrobe.time.remaining", "wardrobe.empty",
            "wardrobe.status.checking", "wardrobe.status.opening",
            "wardrobe.status.cancelled", "wardrobe.status.pending",
            "wardrobe.status.unconfirmed", "wardrobe.status.unavailable",
            "wardrobe.status.save.failed", "wardrobe.status.restoring",
            "wardrobe.status.restored", "wardrobe.status.none",
            "wardrobe.status.restore.failed", "wardrobe.product", "wardrobe.equipped",
            "wardrobe.status.locked", "wardrobe.status.equipped", "wardrobe.status.failed",
        };

        [Test]
        public void EveryRealCatalogueDisplayAndEarnKey_ResolvesExactlyOnce()
        {
            var rows = ParseRows();
            var root = JObject.Parse(File.ReadAllText(CatalogPath, Encoding.UTF8));
            var catalogueKeys = root["cats"].Children<JObject>()
                .Select(row => (string)row["displayNameKey"])
                .Concat(root["items"].Children<JObject>().SelectMany(row => new[]
                {
                    (string)row["displayNameKey"],
                    (string)row["earnInstructionKey"],
                }))
                .Where(key => !string.IsNullOrEmpty(key))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var key in catalogueKeys)
            {
                Assert.That(rows.Count(row => row.Key == key), Is.EqualTo(1),
                    key + " must occur exactly once in the real CSV");
                AssertResolved(key);
            }
        }

        [Test]
        public void RequiredWardrobeActionsAndStatuses_ArePresentAndNonSentinel()
        {
            var rows = ParseRows();
            foreach (var key in RequiredKeys)
            {
                Assert.That(rows.Count(row => row.Key == key), Is.EqualTo(1),
                    key + " must occur exactly once in the actual CSV");
                AssertResolved(key);
            }
        }

        [Test]
        public void UnlockTemplate_ContainsExactlyOnePriceToken_AndNoAuthoredCurrencyOrNumber()
        {
            string value = SingleValue("wardrobe.buy");
            Assert.That(Count(value, "{price}"), Is.EqualTo(1));
            Assert.That(value.Replace("{price}", string.Empty),
                Does.Not.Match(@"[0-9$\u00a2\u00a3\u00a5\u20ac]"),
                "the store is the only price/currency author");
        }

        [Test]
        public void EmptyStateCopy_IsNeutralAcrossEveryZeroCandidateSlot()
        {
            string value = SingleValue("wardrobe.empty");
            Assert.That(value, Does.Not.Match("(?i)accessor|outfit|frame"),
                "the same empty label is reused for every slot");
        }

        [Test]
        public void EveryCsvKey_IsUniqueBeforeUiStringsCouldOverwriteIt()
        {
            var rows = ParseRows();
            var duplicate = rows.GroupBy(row => row.Key, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() != 1);
            Assert.That(duplicate, Is.Null,
                duplicate == null ? string.Empty : "duplicate key: " + duplicate.Key);
        }

        private static void AssertResolved(string key)
        {
            string value = UiStrings.Get(key);
            Assert.That(value, Is.Not.Empty);
            Assert.That(value, Is.Not.EqualTo("??" + key + "??"));
        }

        private static string SingleValue(string key)
        {
            var values = ParseRows().Where(row => row.Key == key)
                .Select(row => row.Value).ToArray();
            Assert.That(values.Length, Is.EqualTo(1));
            return values[0];
        }

        private static int Count(string value, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }
            return count;
        }

        private static IReadOnlyList<CsvRow> ParseRows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8).Split('\n')
                .Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)
                .Select(line =>
                {
                    int comma = line.IndexOf(',');
                    Assert.That(comma, Is.GreaterThan(0), "invalid CSV row: " + line);
                    return new CsvRow(line.Substring(0, comma), line.Substring(comma + 1));
                }).ToArray();
        }

        private readonly struct CsvRow
        {
            public string Key { get; }
            public string Value { get; }

            public CsvRow(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}
