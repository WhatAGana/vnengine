using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 함정 종류 id(값 타입 — UnitClassId 동형). 소스는 TrapCatalog 데이터 테이블.
    public readonly struct TrapId : IEquatable<TrapId>
    {
        public string Value { get; }
        public TrapId(string value) { Value = value; }
        public bool Equals(TrapId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TrapId o && Equals(o);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value;
    }

    public static class TrapIds
    {
        public static readonly TrapId Spike = new TrapId("Spike");
    }

    // 1편 함정 데이터. 지금은 1종(가시함정). Base/PerLevel는 구조검증용 초기 추정 — 플레이테스트 실측 튜닝.
    public static class TrapCatalog
    {
        public static IReadOnlyList<TrapDef> Default() => new List<TrapDef>
        {
            new TrapDef(TrapIds.Spike, "가시함정", baseDamage: 10, perLevel: 5),
        };
    }
}
