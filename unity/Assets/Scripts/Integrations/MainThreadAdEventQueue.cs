using System;
using System.Collections.Generic;

namespace CatMetro.Integrations
{
    // Worker callbacks may enqueue only copied CLR work. A Unity main-thread owner explicitly
    // drains snapshots; callbacks added during a drain remain queued for the next frame.
    public sealed class MainThreadAdEventQueue : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<Action> _pending = new Queue<Action>();
        private bool _disposed;
        private int _generation;

        public bool Enqueue(Action consumer)
        {
            if (consumer == null) return false;
            lock (_gate)
            {
                if (_disposed) return false;
                _pending.Enqueue(consumer);
                return true;
            }
        }

        public int Drain()
        {
            Action[] snapshot;
            int generation;
            lock (_gate)
            {
                if (_disposed || _pending.Count == 0) return 0;
                snapshot = _pending.ToArray();
                _pending.Clear();
                generation = _generation;
            }

            int delivered = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                lock (_gate)
                {
                    if (_disposed || generation != _generation) return delivered;
                }
                try { snapshot[i](); }
                catch
                {
                    // One optional analytics consumer cannot strand or duplicate later entries.
                }
                delivered++;
            }
            return delivered;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                unchecked { _generation++; }
                _pending.Clear();
            }
        }
    }
}
