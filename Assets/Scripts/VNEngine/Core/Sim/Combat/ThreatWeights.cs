namespace VNEngine
{
    // 침입자 강도 기준선(ThreatBase) 가중치 테이블. ⚠️ StatCombatWeights(스탯->전투역할 파생)와는
    // 완전히 별도의 파일·테이블이다 — 이 클래스는 오직 "침입자가 얼마나 강한가"의 5개 계수만 다룬다.
    // 스탯->역할 가중치를 여기 섞지 말 것(그 반대도 마찬가지).
    public sealed class ThreatWeights
    {
        public int WHero { get; }
        public int WLoop { get; }
        public int WPlaced { get; }
        public int WDungeon { get; }
        public int BaseOffset { get; }

        public ThreatWeights(int wHero, int wLoop, int wPlaced, int wDungeon, int baseOffset)
        {
            WHero = wHero;
            WLoop = wLoop;
            WPlaced = wPlaced;
            WDungeon = wDungeon;
            BaseOffset = baseOffset;
        }

        // 1편 초기추정(튜닝대상) ThreatBase 가중치.
        public static ThreatWeights Default() => new ThreatWeights(wHero: 2, wLoop: 8, wPlaced: 1, wDungeon: 3, baseOffset: 20);
    }
}
