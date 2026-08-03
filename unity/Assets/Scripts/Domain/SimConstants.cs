namespace CatMetro.Domain
{
    // ADR-0002 §1: fixed tick, no frame concept. These two constants are the game's read rhythm.
    public static class SimConstants
    {
        public const int TicksPerSecond = 8;
        public const int TickMilliseconds = 125;
    }
}
