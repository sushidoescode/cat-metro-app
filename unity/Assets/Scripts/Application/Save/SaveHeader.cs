namespace CatMetro.Application.Save
{
    // CM-C7 criterion 1: the 16-byte header of ADR-0006:32-40, byte-exact —
    //   0  4  magic          ASCII (save: "CMSV"; the CM-C8 queue reuses this helper with "CMQU")
    //   4  2  formatVersion  uint16 LE
    //   6  2  saveVersion    uint16 LE
    //   8  4  payloadLength  uint32 LE
    //  12  4  payloadCrc32   uint32 LE (CRC-32/IEEE over the payload bytes)
    //  16  …  payload        UTF-8 JSON, no BOM
    // magic/length/CRC make SI-1 checkable BEFORE the payload reaches a JSON parser (RK-34:
    // parsing is the likeliest boot-path crash source).
    public sealed class SaveHeader
    {
        public const int SIZE = 16;

        public readonly string Magic;        // exactly 4 ASCII chars
        public readonly ushort FormatVersion;
        public readonly ushort SaveVersion;
        public readonly uint PayloadLength;
        public readonly uint PayloadCrc32;

        public SaveHeader(string magic, ushort formatVersion, ushort saveVersion,
            uint payloadLength, uint payloadCrc32)
        {
            if (magic == null || magic.Length != 4)
                throw new System.ArgumentException("magic must be exactly 4 ASCII characters");
            Magic = magic; FormatVersion = formatVersion; SaveVersion = saveVersion;
            PayloadLength = payloadLength; PayloadCrc32 = payloadCrc32;
        }

        public static byte[] Write(string magic, ushort formatVersion, ushort saveVersion,
            byte[] payload)
        {
            var header = new SaveHeader(magic, formatVersion, saveVersion,
                (uint)payload.Length, Crc32.Compute(payload));
            var file = new byte[SIZE + payload.Length];
            for (int i = 0; i < 4; i++) file[i] = (byte)header.Magic[i];
            WriteU16(file, 4, header.FormatVersion);
            WriteU16(file, 6, header.SaveVersion);
            WriteU32(file, 8, header.PayloadLength);
            WriteU32(file, 12, header.PayloadCrc32);
            System.Array.Copy(payload, 0, file, SIZE, payload.Length);
            return file;
        }

        // Total parse: null on ANY failure (too short, wrong magic, length mismatch, CRC
        // mismatch) — the load fallback chain never throws (criterion 6).
        public static SaveHeader TryParse(byte[] file, string expectedMagic, out byte[] payload)
        {
            payload = null;
            if (file == null || file.Length < SIZE) return null;
            var magic = new string(new[] { (char)file[0], (char)file[1], (char)file[2], (char)file[3] });
            if (magic != expectedMagic) return null;
            ushort formatVersion = ReadU16(file, 4);
            ushort saveVersion = ReadU16(file, 6);
            uint length = ReadU32(file, 8);
            uint crc = ReadU32(file, 12);
            if ((long)file.Length - SIZE != length) return null;
            var body = new byte[length];
            System.Array.Copy(file, SIZE, body, 0, (int)length);
            if (Crc32.Compute(body) != crc) return null;
            payload = body;
            return new SaveHeader(magic, formatVersion, saveVersion, length, crc);
        }

        private static void WriteU16(byte[] b, int at, ushort v)
        {
            b[at] = (byte)(v & 0xFF); b[at + 1] = (byte)(v >> 8);
        }

        private static void WriteU32(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v & 0xFF); b[at + 1] = (byte)((v >> 8) & 0xFF);
            b[at + 2] = (byte)((v >> 16) & 0xFF); b[at + 3] = (byte)(v >> 24);
        }

        private static ushort ReadU16(byte[] b, int at) =>
            (ushort)(b[at] | (b[at + 1] << 8));

        private static uint ReadU32(byte[] b, int at) =>
            (uint)b[at] | ((uint)b[at + 1] << 8) | ((uint)b[at + 2] << 16) | ((uint)b[at + 3] << 24);
    }
}
