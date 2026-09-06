using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.Presentation
{
    public sealed class SettingsSheetTests
    {
        private GameObject _host;
        private SettingsSheet _sheet;
        private ChromeRegions _regions;
        [SetUp] public void SetUp()
        {
            _host = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas));
            _sheet = SettingsSheet.Create(_host.transform);
            _sheet.Attach(_regions = new ChromeRegions());
            _sheet.Configure(true, true, true, false, false);
        }
        [TearDown] public void TearDown() { _sheet.Hide(); Object.DestroyImmediate(_host); }

        [TestCase(480, 1072, 240)]
        [TestCase(917, 2048, 384)]
        [TestCase(320, 568, 160)]
        public void FourTouchRowsAndActionsFitTheSafeAreaWithoutOverlap(int width, int height, int dpi)
        {
            var safe = new Rect(0, 24, width, height - 48);
            _sheet.Show();
            _sheet.LayoutForViewport(safe, dpi);
            Rect previous = _sheet.CloseRectPx;
            foreach (SettingsChannel channel in System.Enum.GetValues(typeof(SettingsChannel)))
            {
                Rect row = _sheet.RowRectPx(channel);
                Assert.That(row.width, Is.GreaterThan(100));
                Assert.That(row.height, Is.GreaterThanOrEqualTo(44 * Mathf.Max(1, dpi / 160f)));
                Assert.That(safe.Contains(row.min) && safe.Contains(row.max - Vector2.one), Is.True);
                Assert.That(previous.Overlaps(row), Is.False);
                previous = row;
            }
            Assert.That(previous.Overlaps(_sheet.RestoreRectPx), Is.False);
            Assert.That(safe.Contains(_sheet.CardRectPx.min), Is.True);
        }

        [Test]
        public void ModalConsumesUnderlyingTapsAndOnlyAuthoritativeValuesChange()
        {
            int underlying = 0, changes = 0;
            _regions.Register("home", () => new Rect(0, 0, 480, 1072), () => underlying++, ChromeRegions.HomeScreenPriority);
            _sheet.Changed = (channel, value) =>
            {
                Assert.That(channel, Is.EqualTo(SettingsChannel.Music));
                Assert.That(value, Is.False); changes++;
            };
            _sheet.Show();
            _sheet.LayoutForViewport(new Rect(0, 0, 480, 1072), 240);
            Assert.That(_regions.TryResolve(_sheet.RowRectPx(SettingsChannel.Music).center, out var tap), Is.True);
            tap();
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(_sheet.Value(SettingsChannel.Music), Is.True, "save failure cannot paint an optimistic opt-out");
            Assert.That(_regions.TryResolve(new Vector2(1, 1), out tap, out var feedback), Is.True);
            Assert.That(feedback, Is.EqualTo(ChromeFeedback.None));
            tap();
            Assert.That(underlying, Is.Zero);
            _sheet.Hide();
            Assert.That(_regions.Count, Is.EqualTo(1));
        }

        [Test]
        public void DailyRowIsGatedAndRepeatedShowsDoNotDuplicateRegions()
        {
            int reminders = 0, restores = 0;
            _sheet.ReminderRequested = () => reminders++;
            _sheet.RestoreRequested = () => restores++;
            _sheet.Changed = (_, __) => _sheet.SetStatus("");
            _sheet.Show();
            Assert.That(_sheet.ReminderVisible, Is.False);
            Assert.That(_regions.IsRegistered("settings.reminder"), Is.False);
            _sheet.Configure(true, false, false, true, true);
            _sheet.Show();
            _sheet.LayoutForViewport(new Rect(0, 0, 480, 1072), 240);
            Assert.That(_sheet.ReminderVisible, Is.True);
            Assert.That(_regions.TryResolve(_sheet.ReminderRectPx.center, out var tap), Is.True);
            tap(); Assert.That(reminders, Is.EqualTo(1));
            Assert.That(_regions.TryResolve(_sheet.RestoreRectPx.center, out tap), Is.True);
            tap();
            Assert.That(_regions.TryResolve(_sheet.RowRectPx(SettingsChannel.Sound).center, out var change), Is.True);
            change();
            tap(); Assert.That(restores, Is.EqualTo(1), "a channel update cannot release the in-flight restore guard");
            int count = _regions.Count;
            _sheet.Hide(); _sheet.Show(); _sheet.Show();
            Assert.That(_regions.Count, Is.EqualTo(count));
            _sheet.Hide(); Assert.That(_regions.Count, Is.Zero);
        }
    }
}
