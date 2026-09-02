using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace CatMetro.Tests.Presentation
{
    public sealed class UiCsvReminderRowsTests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";
        private const int FirstReminderRow = 18;

        private static readonly string[] FrozenReminderRows =
        {
            "reminder.prompt.title,Would you like tomorrow’s Daily Line delivered?",
            "reminder.prompt.body,One gentle reminder around the time you choose. Nothing expires.",
            "reminder.action.accept,Remind me",
            "reminder.action.dismiss,Not now",
            "reminder.settings.title,Daily reminder",
            "reminder.settings.on,On",
            "reminder.settings.off,Off",
            "reminder.slot.morning,Morning · around 10:00",
            "reminder.slot.afternoon,Afternoon · around 15:00",
            "reminder.slot.evening,Evening · around 18:00",
            "reminder.status.authorized,Notifications allowed.",
            "reminder.status.denied,Notifications are off in device settings.",
            "reminder.status.unknown,Notification permission not decided.",
            "reminder.status.unavailable,Notifications unavailable on this device.",
            "reminder.settings.open,Open notification settings",
            "reminder.settings.close,Close",
        };

        private static string[] Rows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8)
                .Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)
                .ToArray();
        }

        [Test]
        public void ReminderRows_AreBytePinnedAtIndices18Through33()
        {
            var rows = Rows();
            Assert.That(FrozenReminderRows, Has.Length.EqualTo(16),
                "the reminder slice owns exactly indices 18 through 33");
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(34),
                "all 16 reminder rows must remain present; later rows may append");

            for (var offset = 0; offset < FrozenReminderRows.Length; offset++)
            {
                var rowIndex = FirstReminderRow + offset;
                Assert.That(rows[rowIndex], Is.EqualTo(FrozenReminderRows[offset]),
                    "reminder row " + rowIndex + " must remain byte-exact");
            }
        }
    }
}
