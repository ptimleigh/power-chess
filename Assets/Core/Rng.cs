namespace PowerChess.Core
{
    /// <summary>Small deterministic xorshift generator; a struct so game states clone cheaply.</summary>
    public struct Rng
    {
        public ulong State;

        public Rng(ulong seed)
        {
            State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public uint Next()
        {
            State ^= State >> 12;
            State ^= State << 25;
            State ^= State >> 27;
            return (uint)((State * 0x2545F4914F6CDD1DUL) >> 32);
        }

        /// <summary>Uniform-ish integer in [0, n).</summary>
        public int Range(int n)
        {
            return (int)(Next() % (uint)n);
        }
    }
}
