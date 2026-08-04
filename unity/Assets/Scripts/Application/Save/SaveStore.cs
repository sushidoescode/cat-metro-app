using System.Collections.Generic;
using CatMetro.Services;
namespace CatMetro.Application.Save
{
    public sealed class SaveEventRecord
    {
        public readonly string Name; public readonly string Detail;
        public SaveEventRecord(string name, string detail) { Name = name; Detail = detail ?? ""; }
    }
    public sealed class SaveStore : ISave
    {
        public SaveStore(IStorageRoot root, ISaveFileSystem fs, RuntimeBounds bounds, MigrationTable migrations)
            => throw new System.NotImplementedException();
        public SaveState State { get; private set; }
        public int LastCommittedBytes { get; private set; }
        public bool ReadOnlyMode { get; private set; }
        public IReadOnlyList<SaveEventRecord> ReportedEvents => throw new System.NotImplementedException();
        public int PauseBudgetMs => throw new System.NotImplementedException();
        public string SavePath => throw new System.NotImplementedException();
        public string TmpPath => throw new System.NotImplementedException();
        public string BakPath => throw new System.NotImplementedException();
        public void Report(string name, string detail) => throw new System.NotImplementedException();
        public LoadResult Load() => throw new System.NotImplementedException();
        public void CommitAtomic() => throw new System.NotImplementedException();
        public bool TryCommitWithin(int budgetMs) => throw new System.NotImplementedException();
        public bool TryCommitOnPause() => throw new System.NotImplementedException();
    }
}
