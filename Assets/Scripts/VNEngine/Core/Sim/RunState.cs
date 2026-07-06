using System.Collections.Generic;

namespace VNEngine
{
    public sealed class RunState
    {
        public int Day { get; }
        public IReadOnlyDictionary<string, int> Resources { get; }

        public RunState(int day, IReadOnlyDictionary<string, int> resources)
        {
            Day = day;
            var copy = new Dictionary<string, int>(resources.Count);
            foreach (var kv in resources) copy[kv.Key] = kv.Value; // 방어적 복사
            Resources = copy;
        }
    }
}
