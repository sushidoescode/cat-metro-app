using System;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Application.Analytics;

namespace CatMetro.Bootstrap
{
    public sealed class BackgroundAnalyticsPersistenceExecutor : IAnalyticsPersistenceExecutor
    {
        private readonly object _gate = new object();
        private Task _active = Task.CompletedTask;

        public void Dispatch(Action work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            lock (_gate)
            {
                _active = _active.ContinueWith(_ => work(), CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default);
            }
        }

        public bool TryDrain(int budgetMilliseconds)
        {
            Task active;
            lock (_gate) active = _active;
            if (active.IsCompleted) return true;
            try { return active.Wait(Math.Max(0, budgetMilliseconds)); }
            catch { return false; }
        }
    }
}
