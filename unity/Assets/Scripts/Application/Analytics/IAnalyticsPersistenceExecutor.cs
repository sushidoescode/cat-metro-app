namespace CatMetro.Application.Analytics
{
    // Scheduling seam only. AnalyticsQueue owns serialization, coalescing, failure handling,
    // and the single-writer invariant; production dispatches off the gameplay thread.
    public interface IAnalyticsPersistenceExecutor
    {
        void Dispatch(System.Action work);
        bool TryDrain(int budgetMilliseconds);
    }
}
