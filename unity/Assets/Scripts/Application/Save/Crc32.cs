namespace CatMetro.Application.Save
{
    // CRC-32/IEEE (reflected, poly 0xEDB88320) over the payload bytes — ADR-0006:39 names the
    // algorithm; netstandard2.1 ships no CRC type and a NuGet add would need an ADR (hard rule
    // 2), so the 20-line table implementation lives here. Pinned in tests against the canonical
    // check value: CRC32("123456789") == 0xCBF43926.
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int bit = 0; bit < 8; bit++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte[] bytes)
        {
            if (bytes == null) throw new System.ArgumentException("bytes must not be null");
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < bytes.Length; i++)
                crc = Table[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
