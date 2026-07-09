using System.Collections.Generic;

namespace VNEngine
{
    // AdvanceDay가 웨이브날 ResolveWave를 호출하기 위한 회차 설정 번들.
    // 불변·런타임 config(세이브 안 함). Waves[0]=1차 웨이브(10일) … Waves[8]=9차(90일).
    public sealed class DayContext
    {
        public PlacementPlan Plan { get; }
        public IReadOnlyList<WaveDef> Waves { get; }
        public RoomGraph BaseGraph { get; }
        public IReadOnlyList<MonsterDef> MonsterCatalog { get; }
        public StatCombatWeights StatWeights { get; }
        public ThreatWeights ThreatWeights { get; }
        public IReadOnlyList<UnitClassDef> ClassCatalog { get; }
        public ClassMatchup Matchup { get; }
        public CaptureRule CaptureRule { get; }
        public string GoldResourceId { get; }

        // PlacementPlan은 값타입(struct)이라 null 불가 → null-check 없이 대입. 나머지 참조타입은 null-check.
        public DayContext(PlacementPlan plan, IReadOnlyList<WaveDef> waves, RoomGraph baseGraph,
            IReadOnlyList<MonsterDef> monsterCatalog, StatCombatWeights statWeights, ThreatWeights threatWeights,
            IReadOnlyList<UnitClassDef> classCatalog, ClassMatchup matchup, CaptureRule captureRule, string goldResourceId)
        {
            Plan = plan;
            if (waves == null) throw new System.ArgumentNullException(nameof(waves));
            Waves = new List<WaveDef>(waves); // 방어적 복사
            BaseGraph = baseGraph ?? throw new System.ArgumentNullException(nameof(baseGraph));
            if (monsterCatalog == null) throw new System.ArgumentNullException(nameof(monsterCatalog));
            MonsterCatalog = new List<MonsterDef>(monsterCatalog); // 방어적 복사
            StatWeights = statWeights ?? throw new System.ArgumentNullException(nameof(statWeights));
            ThreatWeights = threatWeights ?? throw new System.ArgumentNullException(nameof(threatWeights));
            if (classCatalog == null) throw new System.ArgumentNullException(nameof(classCatalog));
            ClassCatalog = new List<UnitClassDef>(classCatalog); // 방어적 복사
            Matchup = matchup ?? throw new System.ArgumentNullException(nameof(matchup));
            CaptureRule = captureRule ?? throw new System.ArgumentNullException(nameof(captureRule));
            GoldResourceId = goldResourceId ?? throw new System.ArgumentNullException(nameof(goldResourceId));
        }

        // 해당 웨이브날(10·20…90)의 WaveDef. 인덱스 = day/WaveInterval - 1.
        public WaveDef WaveForDay(int day)
        {
            int idx = day / TimeQuery.WaveInterval - 1;
            if (idx < 0 || idx >= Waves.Count)
                throw new VnRuntimeException($"no wave defined for day {day}");
            return Waves[idx];
        }
    }
}
