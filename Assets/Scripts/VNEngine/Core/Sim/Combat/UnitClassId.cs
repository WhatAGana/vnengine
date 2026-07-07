using System;

namespace VNEngine
{
    // 병종 식별자. string 을 감싼 값 타입 — StatId 와 동일 패턴(값동등성, Dictionary 키로 사용 가능).
    // enum·bare string 금지: 새 병종은 데이터(UnitClassCatalog)로만 추가.
    public readonly struct UnitClassId : IEquatable<UnitClassId>
    {
        public string Value { get; }

        public UnitClassId(string value)
        {
            Value = value;
        }

        public bool Equals(UnitClassId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is UnitClassId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(UnitClassId left, UnitClassId right) => left.Equals(right);
        public static bool operator !=(UnitClassId left, UnitClassId right) => !left.Equals(right);
    }
}
