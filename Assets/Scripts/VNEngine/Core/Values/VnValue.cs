namespace VNEngine
{
    public enum VnKind { Int, Bool }

    public readonly struct VnValue : System.IEquatable<VnValue>
    {
        public VnKind Kind { get; }
        private readonly int _i; // Int payload, or Bool as 0/1

        private VnValue(VnKind kind, int i) { Kind = kind; _i = i; }

        public static VnValue Int(int n) => new VnValue(VnKind.Int, n);
        public static VnValue Bool(bool b) => new VnValue(VnKind.Bool, b ? 1 : 0);

        public int AsInt => _i;
        public bool AsBool => _i != 0;
        public bool Truthy => _i != 0;

        public override string ToString() =>
            Kind == VnKind.Bool ? (_i != 0 ? "true" : "false") : _i.ToString();

        public bool Equals(VnValue other) => other.Kind == Kind && other._i == _i;
        public override bool Equals(object obj) => obj is VnValue v && Equals(v);
        public override int GetHashCode() => (_i * 397) ^ (int)Kind;
    }
}
