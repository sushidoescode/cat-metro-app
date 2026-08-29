using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Analytics
{
    // Narrow profile seam: analytics code cannot reach the game's save implementation or files.
    public interface IAnalyticsProfileStore
    {
        JObject Profile { get; }
        bool CommitDurable();
        void RequestCommit();
    }

    // Keeps routine session bookkeeping off the caller thread. The caller-owned profile is
    // mutated only on the composition/main thread; the worker receives immutable deep clones.
    // Identity creation deliberately uses CommitDurable before any network transport exists.
    public sealed class BufferedAnalyticsProfileStore : IAnalyticsProfileStore
    {
        private readonly System.Func<JObject, bool> _writeSnapshot;
        private readonly IAnalyticsPersistenceExecutor _executor;
        private readonly object _gate = new object();
        private JObject _pending;
        private bool _workerRunning;

        public BufferedAnalyticsProfileStore(JObject initialProfile,
            System.Func<JObject, bool> writeSnapshot,
            IAnalyticsPersistenceExecutor executor)
        {
            if (writeSnapshot == null || executor == null)
                throw new System.ArgumentException("writer and executor are required");
            Profile = initialProfile == null
                ? new JObject()
                : (JObject)initialProfile.DeepClone();
            _writeSnapshot = writeSnapshot;
            _executor = executor;
        }

        public JObject Profile { get; }

        public bool CommitDurable()
        {
            try { return _writeSnapshot((JObject)Profile.DeepClone()); }
            catch { return false; }
        }

        public void RequestCommit()
        {
            JObject snapshot;
            try { snapshot = (JObject)Profile.DeepClone(); }
            catch { return; }
            bool dispatch = false;
            lock (_gate)
            {
                _pending = snapshot;
                if (!_workerRunning)
                {
                    _workerRunning = true;
                    dispatch = true;
                }
            }
            if (!dispatch) return;
            try { _executor.Dispatch(WriteLoop); }
            catch
            {
                lock (_gate) _workerRunning = false;
            }
        }

        private void WriteLoop()
        {
            while (true)
            {
                JObject snapshot;
                lock (_gate)
                {
                    snapshot = _pending;
                    _pending = null;
                }
                bool written;
                try { written = snapshot != null && _writeSnapshot(snapshot); }
                catch { written = false; }
                lock (_gate)
                {
                    if (!written && _pending == null) _pending = snapshot;
                    if (!written || _pending == null)
                    {
                        _workerRunning = false;
                        return;
                    }
                }
            }
        }

        public bool TryDrain(int budgetMilliseconds)
        {
            bool drained;
            try { drained = _executor.TryDrain(System.Math.Max(0, budgetMilliseconds)); }
            catch { return false; }
            lock (_gate) return drained && !_workerRunning && _pending == null;
        }
    }
}
