using CatMetro.Services;

namespace CatMetro.Bootstrap
{
    // ADR-0003's 10th row: the composition root is the ONLY assembly that may name the engine's
    // storage paths (CM-C2b criterion 9; the wrapper grep enforces the "only"). Tests supply a
    // temp directory to the same SaveStore; on device these two properties are the difference.
    public sealed class EngineStorageRoot : IStorageRoot
    {
        public string SaveDirectory => UnityEngine.Application.persistentDataPath;
        public string CacheDirectory => UnityEngine.Application.temporaryCachePath;
    }
}
