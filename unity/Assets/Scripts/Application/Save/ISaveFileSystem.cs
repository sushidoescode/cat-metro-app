using System.IO;

namespace CatMetro.Application.Save
{
    // The injected filesystem seam (CM-C7 criteria 4/5): the store's write path is expressed as
    // exactly two seam calls — WriteTempDurable then Replace — so a recording fake can assert
    // the sequence and a faulting fake can interrupt at either point. Tests drive both
    // interruption points; the real implementation below is the ADR-0006:48-51 three-call shape.
    public interface ISaveFileSystem
    {
        bool Exists(string path);
        byte[] ReadAllBytes(string path);
        void Delete(string path);
        // write path + flush-to-disk + close, as ONE durable step (the ADR's calls 1+2)
        void WriteTempDurable(string path, byte[] bytes);
        // the ADR's call 3: rename tmp over dst, previous dst preserved as backup
        void Replace(string tmpPath, string dstPath, string backupPath);
    }

    // The real one. ALL durability claims live in these two methods and nowhere else.
    public sealed class RealSaveFileSystem : ISaveFileSystem
    {
        public bool Exists(string path) => File.Exists(path);
        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
        public void Delete(string path) => File.Delete(path);

        public void WriteTempDurable(string path, byte[] bytes)
        {
            // ADR-0006:48-50: FileStream + Flush(flushToDisk: true) — the one managed call that
            // forces the file's own data to stable storage — then close. No directory fsync and
            // no JNI helper, deliberately (ADR-0006:54-60): the loss ceiling is one commit,
            // covered by the .bak fallback.
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
        }

        public void Replace(string tmpPath, string dstPath, string backupPath)
        {
            // File.Replace requires an existing destination; first-ever commit is a plain move.
            if (File.Exists(dstPath)) File.Replace(tmpPath, dstPath, backupPath);
            else File.Move(tmpPath, dstPath);
        }
    }
}
