using System.Collections.Generic;

namespace VNEngine
{
    public sealed class CommandDef
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ResourceDelta> Effects { get; }

        public CommandDef(string id, string displayName, IReadOnlyList<ResourceDelta> effects)
        {
            Id = id;
            DisplayName = displayName;
            Effects = effects ?? new List<ResourceDelta>();
        }
    }
}
