namespace VNEngine
{
    public sealed class MetaState
    {
        public int LoopCount { get; }
        public HeroStats Heroes { get; }
        public InnState Inn { get; }

        public MetaState(int loopCount) : this(loopCount, HeroStats.Empty, InnState.Empty) { }

        public MetaState(int loopCount, HeroStats heroes) : this(loopCount, heroes, InnState.Empty) { }

        public MetaState(int loopCount, HeroStats heroes, InnState inn)
        {
            LoopCount = loopCount;
            Heroes = heroes ?? HeroStats.Empty;
            Inn = inn ?? InnState.Empty;
        }
    }
}
