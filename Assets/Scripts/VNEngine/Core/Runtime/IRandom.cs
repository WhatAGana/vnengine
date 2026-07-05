namespace VNEngine
{
    public interface IRandom
    {
        // Inclusive on both ends.
        int Range(int minInclusive, int maxInclusive);

        // Full internal PRNG state, for save/restore. uint so the whole
        // generator round-trips through a single serialized value.
        uint State { get; set; }
    }

    // Deterministic, serializable xorshift32. Its entire state is one uint,
    // so a save just stores State and a load sets it back — the subsequent
    // sequence is then identical.
    public sealed class SeededRandom : IRandom
    {
        private uint _state;

        public SeededRandom(int seed)
        {
            // xorshift must never sit at 0; map a 0 seed to a fixed nonzero.
            _state = unchecked((uint)seed);
            if (_state == 0) _state = 0x9E3779B9u;
        }

        public uint State { get => _state; set => _state = value == 0 ? 0x9E3779B9u : value; }

        private uint Next()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) return minInclusive;
            // 64-bit span so the full int range (int.MinValue..int.MaxValue) can't
            // overflow to 0 and divide-by-zero. For all normal ranges Next() % span
            // is identical to the previous uint computation, so sequences are unchanged.
            ulong span = (ulong)((long)maxInclusive - minInclusive) + 1UL; // span>=1
            return (int)((long)minInclusive + (long)(Next() % span));
        }
    }
}
