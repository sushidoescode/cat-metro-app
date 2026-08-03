namespace CatMetro.Domain
{
    // PCG-XSH-RR 32-bit generator, implemented from the published construction at
    // https://www.pcg-random.org (Melissa O'Neill, "PCG: A Family of Simple Fast
    // Space-Efficient Statistically Good Algorithms for RNG"), public-domain algorithm.
    // No licensed code is copied (contract A-C1-6; ADR-0002 §4, §Spend).
    // The struct's two ulongs are fields of SimulationState and therefore inside the
    // replay hash (ADR-0002 §4).
    public struct Pcg32
    {
        public ulong State;
        public ulong Inc;

        public Pcg32(ulong seed, ulong sequence)
        {
            State = 0;
            Inc = (sequence << 1) | 1UL;
            Next();
            State += seed;
            Next();
        }

        public uint Next()
        {
            ulong old = State;
            State = old * 6364136223846793005UL + Inc;
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }
    }
}
