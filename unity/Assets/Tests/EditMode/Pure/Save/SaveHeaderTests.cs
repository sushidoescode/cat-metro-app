using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    // CM-C7 criterion 1: the 16-byte header, byte-exact per ADR-0006:32-40, and a BOM-free
    // UTF-8 JSON payload from byte 16 on.
    public sealed class SaveHeaderTests
    {
        // The canonical CRC-32/IEEE check value — pinned against an independent implementation
        // (python binascii.crc32(b"123456789") == 0xCBF43926).
        [Test]
        public void Crc32_MatchesTheIeeeCheckValue() =>
            Assert.That(Crc32.Compute(Encoding.ASCII.GetBytes("123456789")),
                Is.EqualTo(0xCBF43926u));

        [Test]
        public void Header_FieldOffsets_Widths_Endianness()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Tickets = 5;
            store.CommitAtomic();
            byte[] file = SFixtures.RawFile(store.SavePath);

            // offset 0, 4 bytes: magic "CMSV" in ASCII
            Assert.That((char)file[0], Is.EqualTo('C'));
            Assert.That((char)file[1], Is.EqualTo('M'));
            Assert.That((char)file[2], Is.EqualTo('S'));
            Assert.That((char)file[3], Is.EqualTo('V'));
            // offset 4, uint16 LE: formatVersion = 1
            Assert.That(file[4] | (file[5] << 8), Is.EqualTo(1));
            // offset 6, uint16 LE: saveVersion = 1
            Assert.That(file[6] | (file[7] << 8), Is.EqualTo(1));
            // offset 8, uint32 LE: payloadLength = file length - 16
            uint length = (uint)(file[8] | (file[9] << 8) | (file[10] << 16) | ((uint)file[11] << 24));
            Assert.That(length, Is.EqualTo((uint)(file.Length - SaveHeader.SIZE)));
            // offset 12, uint32 LE: CRC-32/IEEE over the payload bytes
            var payload = new byte[length];
            System.Array.Copy(file, SaveHeader.SIZE, payload, 0, (int)length);
            uint crc = (uint)(file[12] | (file[13] << 8) | (file[14] << 16) | ((uint)file[15] << 24));
            Assert.That(crc, Is.EqualTo(Crc32.Compute(payload)));
        }

        [Test]
        public void Payload_IsBomFreeUtf8Json_FromByte16()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.CommitAtomic();
            byte[] file = SFixtures.RawFile(store.SavePath);

            Assert.That(file.Length, Is.GreaterThan(SaveHeader.SIZE + 3));
            Assert.That(file[SaveHeader.SIZE], Is.Not.EqualTo(0xEF), "payload must carry no BOM");
            var payload = new byte[file.Length - SaveHeader.SIZE];
            System.Array.Copy(file, SaveHeader.SIZE, payload, 0, payload.Length);
            // strict decoder + parse: throws on invalid UTF-8 or invalid JSON
            var text = new UTF8Encoding(false, true).GetString(payload);
            Assert.That(JObject.Parse(text), Is.Not.Null);
        }

        [Test]
        public void TryParse_RejectsTamperedLengthAndCrc()
        {
            var good = SaveHeader.Write(SaveDefaults.MAGIC, 1, 1,
                Encoding.UTF8.GetBytes("{\"a\":1}"));
            Assert.That(SaveHeader.TryParse(good, SaveDefaults.MAGIC, out _), Is.Not.Null);

            var badMagic = (byte[])good.Clone();
            badMagic[0] = (byte)'X';
            Assert.That(SaveHeader.TryParse(badMagic, SaveDefaults.MAGIC, out _), Is.Null);

            var badLen = (byte[])good.Clone();
            badLen[8] ^= 0xFF;
            Assert.That(SaveHeader.TryParse(badLen, SaveDefaults.MAGIC, out _), Is.Null);

            var badCrc = (byte[])good.Clone();
            badCrc[12] ^= 0xFF;
            Assert.That(SaveHeader.TryParse(badCrc, SaveDefaults.MAGIC, out _), Is.Null);

            var badBody = (byte[])good.Clone();
            badBody[SaveHeader.SIZE] ^= 0xFF;
            Assert.That(SaveHeader.TryParse(badBody, SaveDefaults.MAGIC, out _), Is.Null,
                "a payload flip must fail the CRC");
        }
    }
}
