using System;

namespace VNEngine
{
    // 함정 종류 정의(데이터). Damage = Base + Level*PerLevel (TrapRule). 종류 확장은 TrapCatalog에 추가.
    // 지금은 1종(가시함정)·레벨1 고정 — 레벨업/다종류는 후속 슬라이스(골격만 확장형).
    public sealed class TrapDef
    {
        public TrapId Id { get; }
        public string DisplayName { get; }
        public int Base { get; }        // 레벨0 기준 데미지
        public int PerLevel { get; }    // 레벨당 증가량

        public TrapDef(TrapId id, string displayName, int baseDamage, int perLevel)
        {
            if (string.IsNullOrEmpty(id.Value)) throw new ArgumentException("TrapId.Value must not be null or empty", nameof(id));
            if (baseDamage < 1) throw new ArgumentException("baseDamage must be at least 1", nameof(baseDamage));
            if (perLevel < 0) throw new ArgumentException("perLevel must be non-negative", nameof(perLevel));
            Id = id;
            DisplayName = displayName;
            Base = baseDamage;
            PerLevel = perLevel;
        }
    }
}
