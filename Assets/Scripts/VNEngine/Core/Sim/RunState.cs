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
            Resources = resources;
        }
    }
}
