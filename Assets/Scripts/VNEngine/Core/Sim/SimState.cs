using System.Collections.Generic;

namespace VNEngine
{
    public sealed class SimState
    {
        public int Week { get; }
        public IReadOnlyDictionary<string, int> Resources { get; }

        public SimState(int week, IReadOnlyDictionary<string, int> resources)
        {
            Week = week;
            Resources = resources;
        }
    }
}
