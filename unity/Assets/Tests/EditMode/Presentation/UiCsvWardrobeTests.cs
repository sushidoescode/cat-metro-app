using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Presentation.Strings;
using NUnit.Framework;

namespace CatMetro.Tests.Presentation
{
    // Pins the filmable Wardrobe copy this feature owns without forbidding unrelated slices
    // before or after it.
    public sealed class UiCsvWardrobeTests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";

        private static readonly string[] OwnedRows =
        {
            "wardrobe.entry,Wardrobe",
            "wardrobe.title,Profile Wardrobe",
            "wardrobe.profile,Your profile cat",
            "wardrobe.product,Conductor's Coat",
            "wardrobe.back,Back",
            "wardrobe.buy,Unlock · {price}",
            "wardrobe.equipped,Coat equipped",
            "wardrobe.restore,Restore purchases",
            "wardrobe.restore.running,Restoring…",
            "wardrobe.store.checking,Checking store…",
            "wardrobe.store.opening,Opening store…",
            "wardrobe.store.unavailable,Store unavailable",
            "wardrobe.status.checking,Checking the railway shop…",
            "wardrobe.status.locked,A station uniform for your profile cat",
            "wardrobe.status.equipped,Conductor's Coat equipped!",
            "wardrobe.status.opening,Opening the store…",
            "wardrobe.status.cancelled,Purchase cancelled",
            "wardrobe.status.pending,Purchase pending — the coat unlocks after approval",
            "wardrobe.status.failed,Purchase unavailable — please try again",
            "wardrobe.status.unconfirmed,Purchase received — checking the entitlement",
            "wardrobe.status.restoring,Restoring purchases…",
            "wardrobe.status.restored,Purchase restored — coat equipped!",
            "wardrobe.status.none,No purchases found",
            "wardrobe.status.restore.failed,Restore unavailable — please try again",
            "wardrobe.status.unavailable,The shop is offline — the game is still ready to play",
            "wardrobe.tryon.heading,Today's try-ons",
            "wardrobe.tryon.conductor,Conductor",
            "wardrobe.tryon.engineer,Engineer",
            "wardrobe.tryon.scarf,Scarf",
            "wardrobe.tryon.goggles,Goggles",
            "wardrobe.tryon.locked,Locked",
            "wardrobe.tryon.borrowed,Borrowed today",
            "wardrobe.tryon.watch,Watch to borrow today",
            "wardrobe.tryon.unavailable,Try-on unavailable",
            "wardrobe.tryon.success,Borrowed for today!",
        };

        private static string[] Rows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8)
                .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        }

        [Test]
        public void WardrobeRows_StayExactAndContiguousAfterUtf8CsvNormalization()
        {
            var rows = Rows();
            var firstWardrobeRow = System.Array.IndexOf(rows, OwnedRows[0]);
            Assert.That(firstWardrobeRow, Is.GreaterThanOrEqualTo(0),
                "the Wardrobe slice must retain its literal anchor row");
            Assert.That(rows.Length,
                Is.GreaterThanOrEqualTo(firstWardrobeRow + OwnedRows.Length),
                "all Wardrobe keys remain present as one contiguous owned slice");
            for (int i = 0; i < OwnedRows.Length; i++)
                Assert.That(rows[firstWardrobeRow + i], Is.EqualTo(OwnedRows[i]),
                    "Wardrobe row " + i + " changed or was interrupted");
        }

        [Test]
        public void EveryCsvKey_IsUnique()
        {
            var keys = Rows().Select(row => row.Substring(0, row.IndexOf(','))).ToArray();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Length),
                "a duplicate key would silently overwrite earlier UI copy at runtime");
        }

        [Test]
        public void WardrobeValues_RoundTripThroughUiStrings()
        {
            foreach (var row in OwnedRows)
            {
                int comma = row.IndexOf(',');
                string key = row.Substring(0, comma);
                string value = row.Substring(comma + 1);
                Assert.That(UiStrings.Get(key), Is.EqualTo(value),
                    key + " must resolve from the CSV rather than a missing-key sentinel");
            }
        }
    }
}
