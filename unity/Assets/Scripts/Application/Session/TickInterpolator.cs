namespace CatMetro.Application.Session
{
    // CM-C2b criterion 6's law object: 8 tps sim under any render rate. Accumulates real
    // milliseconds; the driver runs Steps for each whole 125 ms consumed; Alpha is the fraction
    // of the CURRENT tick elapsed — always in [0,1), strictly increasing between snapshots,
    // resetting exactly once per consumed tick (ADR-0002 §1; overview.md:147). Pure arithmetic:
    // engine-free, dotnet-leg tested.
    public sealed class TickInterpolator
    {
        public const double TICK_MS = 125.0; // 8 tps (ADR-0002)

        private double _accumulatedMs;

        // Consumes dt; returns the number of whole ticks the caller must Step now.
        public int AdvanceMs(double dtMs)
        {
            if (dtMs < 0) dtMs = 0;
            _accumulatedMs += dtMs;
            int steps = 0;
            while (_accumulatedMs >= TICK_MS)
            {
                _accumulatedMs -= TICK_MS;
                steps++;
            }
            return steps;
        }

        public double Alpha => _accumulatedMs / TICK_MS;
    }
}
