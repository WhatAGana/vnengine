using System.Collections.Generic;

namespace VNEngine.Tests
{
    // CampaignWaveRuleTests의 결정론적 웨이브 픽스처를 시간구조 테스트(CampaignDayRuleTests, Task4)가
    // 공유하기 위한 헬퍼. 값은 CampaignWaveRuleTests와 동일하게 유지해 웨이브 해결이 결정론적이도록 한다.
    internal static class SimTimeFixtures
    {
        // CampaignWaveRuleTests와 동일한 결정론 IRandom.
        public static IRandom Rng(int seed = 1) => new SeededRandom(seed);

        private static readonly UnitClassId GruntId = new UnitClassId("Grunt");

        // 사살 경로용 잡졸(포획불가). 강한 주인공이 코어앞1칸에서 전원 즉사 → 약탈골드 발생.
        private static UnitClassDef Grunt()
            => new UnitClassDef(GruntId, "Grunt", 100, 100, 100, canBeCaptured: false);

        private static IReadOnlyList<UnitClassDef> ClassCatalog()
            => new List<UnitClassDef> { Grunt() };

        // 코어앞1칸(r2)에 주인공만 배치, 방어몹 없음 → 침입자 전원이 코어앞까지 도달.
        private static PlacementPlan ValidHeroOnlyPlan()
            => new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r2") };

        // r0,r1,r2 3방 선형(방어몹/함정 없음). CampaignWaveRuleTests.ThreeEmptyRooms와 동일.
        private static RoomGraph ThreeEmptyRooms()
            => RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
            });

        // 모든 항 0으로 눌러 baseOffset만 남긴 테스트 전용 위협가중치(threatBase 완전 통제).
        private static ThreatWeights FixedThreat(int baseOffset)
            => new ThreatWeights(wHero: 0, wLoop: 0, wPlaced: 0, wDungeon: 0, baseOffset: baseOffset);

        private static WaveDef OneWave(UnitClassId classId, int count)
            => new WaveDef(new List<WaveDef.Entry> { new WaveDef.Entry { ClassId = classId, Count = count } });

        // 유효 DayContext(웨이브 자원 id="gold"). Waves: 단일 WaveDef(잡졸5)를 9주기(10·20…90일)만큼 9회 반복.
        public static DayContext DayContext()
        {
            var wave = OneWave(GruntId, count: 5);
            var waves = new List<WaveDef>();
            for (int i = 0; i < TimeQuery.Cycles; i++) waves.Add(wave); // 9주기 채우기

            return new DayContext(
                ValidHeroOnlyPlan(),
                waves,
                ThreeEmptyRooms(),
                MonsterCatalog.Default(),
                StatCombatWeights.Default(),
                FixedThreat(50), // threatBase=50 고정 → 사살당 약탈골드 > 0
                ClassCatalog(),
                new ClassMatchup(new List<ClassMatchup.Entry>()),
                CaptureRule.Default(),
                "gold");
        }

        // 지정 일자의 유효 캠페인. Meta.Heroes를 강한 주인공(STR1000/DEX100)으로 시드 →
        // 웨이브날 전원 즉사로 약탈골드 발생. 여관 Decor>0 → 정비날 수급. gold 자원 존재. Run.Day=day.
        public static CampaignState CampaignAtDay(int day, int gold = 1000, int karmaBank = 0)
        {
            var heroes = new HeroStats(new Dictionary<StatId, int>
            {
                { StatIds.STR, 1000 },
                { StatIds.DEX, 100 },
            });
            var inn = new InnState(staff: 3, decor: 5, menuLevel: 2); // Decor>0 → 정비 수급
            var meta = new MetaState(loopCount: 1, heroes: heroes, inn: inn, karmaBank: karmaBank);
            var run = new RunState(day, new Dictionary<string, int> { { "gold", gold } });
            return new CampaignState(meta, run);
        }
    }
}
