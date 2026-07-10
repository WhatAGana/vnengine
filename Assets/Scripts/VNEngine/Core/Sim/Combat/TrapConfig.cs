using System;

namespace VNEngine
{
    // 한 웨이브(=한 던전 그래프)에 적용될 함정 인스턴스: 종류(Def) × 레벨. Damage는 TrapRule로 산출.
    // 함정은 그래프의 속성 → RoomGraph가 이 값을 들고, CombatResolver가 함정방 데미지로 읽는다.
    // Level은 진행/캠페인 축(지금은 1 고정). None()은 함정 데미지 0(함정 boolean만, 데미지 없는 방/테스트용).
    public sealed class TrapConfig
    {
        public TrapDef Def { get; }
        public int Level { get; }
        public int Damage { get; }

        public TrapConfig(TrapDef def, int level)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            if (level < 0) throw new ArgumentException("level must be non-negative", nameof(level));
            Level = level;
            Damage = TrapRule.Damage(def, level);
        }

        // Damage를 명시적으로 0으로 두는 내부 생성자(None 전용). Def/Level은 기본 종·0으로 채우되 데미지만 0.
        private TrapConfig()
        {
            Def = TrapCatalog.Default()[0];
            Level = 0;
            Damage = 0;
        }

        // 기본: 가시함정 레벨1. 함정방이 실제로 데미지를 주는 표준 설정.
        public static TrapConfig Default() => new TrapConfig(TrapCatalog.Default()[0], 1);

        // 함정 데미지 0(함정 boolean만). 데미지를 배제하고 포획/배치 로직만 검사할 때.
        public static TrapConfig None() => new TrapConfig();
    }
}
