using System.Collections.Generic;

namespace VNEngine
{
    public sealed class GameState
    {
        private readonly Dictionary<string, VnValue> _vars = new Dictionary<string, VnValue>();

        public IRandom Random { get; }

        public GameState(IRandom random)
        {
            Random = random ?? throw new System.ArgumentNullException(nameof(random));
        }

        public VnValue Get(string name) =>
            _vars.TryGetValue(name, out var v) ? v : VnValue.Int(0);

        public void Set(string name, VnValue value) => _vars[name] = value;

        public bool Has(string name) => _vars.ContainsKey(name);

        public IReadOnlyDictionary<string, VnValue> Snapshot => _vars;
    }
}
