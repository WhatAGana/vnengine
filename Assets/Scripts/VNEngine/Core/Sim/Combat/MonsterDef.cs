using System;

namespace VNEngine
{
    // 방어측 몹 정의(데이터). Cost=배치 예산제 코스트, Base*=배치시 방어 Attacker 기본 능력치.
    public sealed class MonsterDef
    {
        public UnitClassId Id { get; }
        public string DisplayName { get; }
        public int Cost { get; }
        public int BaseHp { get; }
        public int BaseAtk { get; }
        public int BaseDef { get; }
        public bool IsCapturingMonster { get; }

        public MonsterDef(UnitClassId id, string displayName, int cost, int baseHp, int baseAtk, int baseDef, bool isCapturingMonster)
        {
            if (string.IsNullOrEmpty(id.Value)) throw new ArgumentException("UnitClassId.Value must not be null or empty", nameof(id));
            if (cost < 1) throw new ArgumentException("cost must be at least 1", nameof(cost));
            if (baseHp < 1) throw new ArgumentException("baseHp must be at least 1", nameof(baseHp));
            if (baseAtk < 1) throw new ArgumentException("baseAtk must be at least 1", nameof(baseAtk));
            if (baseDef < 1) throw new ArgumentException("baseDef must be at least 1", nameof(baseDef));

            Id = id;
            DisplayName = displayName;
            Cost = cost;
            BaseHp = baseHp;
            BaseAtk = baseAtk;
            BaseDef = baseDef;
            IsCapturingMonster = isCapturingMonster;
        }
    }
}
