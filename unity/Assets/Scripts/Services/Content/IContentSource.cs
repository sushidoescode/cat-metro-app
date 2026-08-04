using System.Threading;
using System.Threading.Tasks;

namespace CatMetro.Services
{
    // ADR-0003: interfaces only. Android keeps StreamingAssets inside the compressed APK where
    // System.IO cannot reach it, so all content reads cross this seam and CatMetro.Content
    // receives bytes (ADR-0008:53-56). Signature per docs/architecture/overview.md:240-243.
    // Implemented engine-side by CatMetro.Bootstrap (CM-C2b); tests supply in-memory fakes.
    public interface IContentSource
    {
        Task<byte[]> ReadAsync(string relativePath, CancellationToken ct);
        bool Exists(string relativePath);
    }
}
