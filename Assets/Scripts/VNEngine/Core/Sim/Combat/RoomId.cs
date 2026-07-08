using System;

namespace VNEngine
{
    // 방 식별자. string 을 감싼 값 타입 — UnitClassId 와 동일 패턴(값동등성, Dictionary 키로 사용 가능).
    public readonly struct RoomId : IEquatable<RoomId>
    {
        public string Value { get; }

        public RoomId(string value)
        {
            Value = value;
        }

        public bool Equals(RoomId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RoomId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(RoomId left, RoomId right) => left.Equals(right);
        public static bool operator !=(RoomId left, RoomId right) => !left.Equals(right);
    }
}
