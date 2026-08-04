namespace CatMetro.Services
{
    // ADR-0003:78-99: the write-side twin of IContentSource — the only seam that keeps
    // CatMetro.Application engine-free. Exactly two properties (CM-C7 criterion 13); Bootstrap
    // supplies the engine's persistent/cache paths, tests supply a temp directory, and the save
    // store cannot tell the difference.
    public interface IStorageRoot
    {
        string SaveDirectory { get; }
        string CacheDirectory { get; }
    }
}
