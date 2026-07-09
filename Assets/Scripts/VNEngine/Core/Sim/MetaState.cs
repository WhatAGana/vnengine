namespace VNEngine
{
    public sealed class MetaState
    {
        public int LoopCount { get; }
        public HeroStats Heroes { get; }
        public InnState Inn { get; }
        public int KarmaBank { get; }
        public int DungeonLevel { get; }

        public MetaState(int loopCount) : this(loopCount, HeroStats.Empty, InnState.Empty, 0) { }

        public MetaState(int loopCount, HeroStats heroes) : this(loopCount, heroes, InnState.Empty, 0) { }

        public MetaState(int loopCount, HeroStats heroes, InnState inn) : this(loopCount, heroes, inn, 0) { }

        public MetaState(int loopCount, HeroStats heroes, InnState inn, int karmaBank)
            : this(loopCount, heroes, inn, karmaBank, 1) { }

        // DungeonLevel: 07-C task9 추가(additive) — 던전 레벨업(LevelUpDungeon)의 저장소. 구세이브는 누락→1(0이면
        // DungeonLevelRule이 예외를 던진다. 짧은 ctor들은 전부 기본값 1로 위임).
        public MetaState(int loopCount, HeroStats heroes, InnState inn, int karmaBank, int dungeonLevel)
        {
            LoopCount = loopCount;
            Heroes = heroes ?? HeroStats.Empty;
            Inn = inn ?? InnState.Empty;
            KarmaBank = karmaBank;
            DungeonLevel = dungeonLevel;
        }
    }
}
