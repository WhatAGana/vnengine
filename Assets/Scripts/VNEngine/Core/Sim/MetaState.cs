namespace VNEngine
{
    public sealed class MetaState
    {
        public int LoopCount { get; }
        public HeroStats Heroes { get; }
        public InnState Inn { get; }
        public int KarmaBank { get; }

        public MetaState(int loopCount) : this(loopCount, HeroStats.Empty, InnState.Empty, 0) { }

        public MetaState(int loopCount, HeroStats heroes) : this(loopCount, heroes, InnState.Empty, 0) { }

        public MetaState(int loopCount, HeroStats heroes, InnState inn) : this(loopCount, heroes, inn, 0) { }

        public MetaState(int loopCount, HeroStats heroes, InnState inn, int karmaBank)
        {
            LoopCount = loopCount;
            Heroes = heroes ?? HeroStats.Empty;
            Inn = inn ?? InnState.Empty;
            KarmaBank = karmaBank;
        }
    }
}
