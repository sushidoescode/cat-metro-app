using System.Threading;
using System.Threading.Tasks;
using CatMetro.Services;
using UnityEngine.Networking;

namespace CatMetro.Bootstrap
{
    // CM-C2b criterion 9: the IContentSource over StreamingAssets, through the web-request route
    // UNCONDITIONALLY — on Android the APK keeps StreamingAssets where plain file reads cannot
    // reach (ADR-0008:53-56), and an editor-only file read would pass every editor test and fail
    // on device (recut evaluator D4). Reads block on the transfer (greybox boot loads are a few
    // KB); the async seam shape is preserved for the real boot flow.
    public sealed class StreamingAssetsContentSource : IContentSource
    {
        public Task<byte[]> ReadAsync(string relativePath, CancellationToken ct)
        {
            byte[] bytes = ReadBlocking(relativePath, ct);
            if (bytes == null)
                return Task.FromException<byte[]>(
                    new System.IO.FileNotFoundException("StreamingAssets: " + relativePath));
            return Task.FromResult(bytes);
        }

        public bool Exists(string relativePath) =>
            ReadBlocking(relativePath, CancellationToken.None) != null;

        private static byte[] ReadBlocking(string relativePath, CancellationToken ct)
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath, relativePath ?? "");
            // Android's streamingAssetsPath already carries a scheme (jar:file://...); the
            // editor's is a bare filesystem path, which the web-request API refuses — add the
            // scheme exactly when absent.
            string url = path.Contains("://") ? path : "file://" + path;
            using (var request = UnityWebRequest.Get(url))
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    if (ct.IsCancellationRequested) { request.Abort(); return null; }
                    Thread.Sleep(1);
                }
                if (request.result != UnityWebRequest.Result.Success) return null;
                var data = request.downloadHandler.data;
                return data != null && data.Length > 0 ? data : null;
            }
        }
    }
}
