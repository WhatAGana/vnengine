namespace VNEngine
{
    public interface IRandom
    {
        // Inclusive on both ends.
        int Range(int minInclusive, int maxInclusive);
    }

    public sealed class SeededRandom : IRandom
    {
        private readonly System.Random _r;
        public SeededRandom(int seed) { _r = new System.Random(seed); }
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) return minInclusive;
            return _r.Next(minInclusive, maxInclusive + 1);
        }
    }
}
