namespace CatMetro.Application.Save
{
    public sealed class SaveHeader
    {
        public const int SIZE = 16;
        public readonly string Magic;
        public readonly ushort FormatVersion;
        public readonly ushort SaveVersion;
        public readonly uint PayloadLength;
        public readonly uint PayloadCrc32;
        public SaveHeader(string magic, ushort formatVersion, ushort saveVersion, uint payloadLength, uint payloadCrc32)
        { Magic = magic; FormatVersion = formatVersion; SaveVersion = saveVersion; PayloadLength = payloadLength; PayloadCrc32 = payloadCrc32; }
        public static byte[] Write(string magic, ushort formatVersion, ushort saveVersion, byte[] payload) => throw new System.NotImplementedException();
        public static SaveHeader TryParse(byte[] file, string expectedMagic, out byte[] payload) => throw new System.NotImplementedException();
    }
}
