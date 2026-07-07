using System;

namespace VNEngine
{
    // 스탯 식별자. string 을 감싼 값 타입 — 새 스탯은 StatDef 데이터로만 추가(enum 편집 불필요),
    // 동시에 타입 안전(자원 id 등 다른 string 과 컴파일타임 구분). Dictionary 키로 값 동등성 보장.
    public readonly struct StatId : IEquatable<StatId>
    {
        public string Value { get; }

        public StatId(string value)
        {
            Value = value;
        }

        public bool Equals(StatId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is StatId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(StatId left, StatId right) => left.Equals(right);
        public static bool operator !=(StatId left, StatId right) => !left.Equals(right);
    }
}
