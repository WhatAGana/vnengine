namespace VNEngine
{
    public sealed class MetaState
    {
        public int LoopCount { get; }
        public HeroStats Heroes { get; }

        public MetaState(int loopCount) : this(loopCount, HeroStats.Empty) { }

        public MetaState(int loopCount, HeroStats heroes)
        {
            LoopCount = loopCount;
            Heroes = heroes ?? HeroStats.Empty;
        }
    }
}
