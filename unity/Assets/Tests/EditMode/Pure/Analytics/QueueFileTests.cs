using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Application.Save;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    // CM-C8 criterion 1: analytics_queue.dat sits beside save.dat, wears the SAME 16-byte
    // header with magic "CMQU", and a corrupt file restarts empty with a visible loss note.
    public sealed class QueueFileTests
    {
        [Test]
        public void File_SitsBesideSaveDat_WithTheCmquHeader()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, _) = QFixtures.Queue(root);
            q.Log(QFixtures.Ev("level_started"));

            Assert.That(Path.GetDirectoryName(q.QueuePath),
                Is.EqualTo(root.SaveDirectory), "sibling of save.dat under SaveDirectory");
            byte[] file = File.ReadAllBytes(q.QueuePath);
            Assert.That((char)file[0], Is.EqualTo('C'));
            Assert.That((char)file[1], Is.EqualTo('M'));
            Assert.That((char)file[2], Is.EqualTo('Q'));
            Assert.That((char)file[3], Is.EqualTo('U'));
            // same layout: formatVersion u16 LE @4, queueVersion u16 LE @6, length @8, crc @12
            Assert.That(file[4] | (file[5] << 8), Is.EqualTo(1));
            Assert.That(file[6] | (file[7] << 8), Is.EqualTo((int)AnalyticsQueue.QUEUE_VERSION));
            var parsed = SaveHeader.TryParse(file, AnalyticsQueue.MAGIC, out var payload);
            Assert.That(parsed, Is.Not.Null, "CM-C7's own TryParse accepts the file — one helper");
            Assert.That(payload[0], Is.EqualTo((byte)'['), "payload is the ADR §5 JSON array");
        }

        [Test]
        public void CorruptFile_RestartsEmpty_AndReportsQueueDropped()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, _) = QFixtures.Queue(root);
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));
            var bytes = File.ReadAllBytes(q.QueuePath);
            bytes[bytes.Length - 1] ^= 0xFF; // CRC now fails
            File.WriteAllBytes(q.QueuePath, bytes);

            var (fresh, _, _) = QFixtures.Queue(root);
            Assert.That(fresh.QueuedEventCount, Is.Zero, "reject-and-restart-empty");
            Assert.That(fresh.Notes.Any(n =>
                n.Name == "queue_dropped" && n.Detail.Contains("unknown(corrupt)")), Is.True,
                "the loss is visible (A-C8-7: an exact count is unknowable from corrupt bytes)");
        }

        [TestCase(2, AnalyticsQueue.QUEUE_VERSION)]
        [TestCase(1, AnalyticsQueue.QUEUE_VERSION + 1)]
        public void UnsupportedHeaderVersion_RestartsEmpty(int formatVersion, int queueVersion)
        {
            using var root = new SFixtures.TempRoot();
            var (queue, _, _) = QFixtures.Queue(root);
            queue.Log(QFixtures.Ev("must-not-load"));
            var parsed = SaveHeader.TryParse(SFixtures.RawFile(queue.QueuePath),
                AnalyticsQueue.MAGIC, out var payload);
            Assert.That(parsed, Is.Not.Null);
            SFixtures.WriteRaw(queue.QueuePath, SaveHeader.Write(AnalyticsQueue.MAGIC,
                (ushort)formatVersion, (ushort)queueVersion, payload));

            var (restored, _, _) = QFixtures.Queue(root);

            Assert.That(restored.QueuedEventCount, Is.Zero);
            Assert.That(restored.Notes.Any(x => x.Detail.Contains("unsupported version")),
                Is.True);
        }

        // Criterion 1's "no second implementation" NUnit half: the write path is the CM-C7 seam
        // (temp+replace on the queue file), never an in-place write.
        [Test]
        public void WritePath_IsTheSharedHelper_TempThenReplace()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, fs) = QFixtures.Queue(root);
            fs.Calls.Clear();
            q.Log(QFixtures.Ev("a"));

            Assert.That(fs.Calls, Is.EqualTo(new[]
            {
                "WriteTempDurable:analytics_queue.dat.tmp",
                "Replace:analytics_queue.dat",
            }));
        }

        // Criterion 8's field half: every persisted record carries its idempotency id, so a
        // future backup-aware design has the field it needs (criterion 9's forward hook).
        [Test]
        public void PersistedRecords_CarryTheIdempotencyId()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, _) = QFixtures.Queue(root);
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));

            SaveHeader.TryParse(File.ReadAllBytes(q.QueuePath), AnalyticsQueue.MAGIC, out var payload);
            var arr = JArray.Parse(System.Text.Encoding.UTF8.GetString(payload));
            Assert.That(arr.Count, Is.EqualTo(2));
            foreach (var rec in arr)
            {
                Assert.That((string)rec["id"], Does.Match("^[0-9a-f]{16}$"));
                Assert.That(rec["ord"], Is.Not.Null);
                Assert.That(rec["name"], Is.Not.Null);
                Assert.That(rec["params"], Is.Not.Null);
            }
        }
    }
}
