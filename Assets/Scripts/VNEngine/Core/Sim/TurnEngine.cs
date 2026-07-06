using System.Collections.Generic;

namespace VNEngine
{
    public sealed class TurnEngine
    {
        private readonly List<ResourceDef> _resources = new List<ResourceDef>();
        private readonly List<CommandDef> _commandList = new List<CommandDef>();
        private readonly Dictionary<string, CommandDef> _commands = new Dictionary<string, CommandDef>();

        public IReadOnlyList<ResourceDef> Resources => _resources;
        public IReadOnlyList<CommandDef> Commands => _commandList;

        public TurnEngine(IReadOnlyList<ResourceDef> resources, IReadOnlyList<CommandDef> commands)
        {
            if (resources == null) throw new System.ArgumentNullException(nameof(resources));
            if (commands == null) throw new System.ArgumentNullException(nameof(commands));

            var resourceIds = new HashSet<string>();
            foreach (var r in resources)
            {
                if (!resourceIds.Add(r.Id))
                    throw new VnRuntimeException($"Duplicate resource id: {r.Id}");
                _resources.Add(r);
            }

            foreach (var c in commands)
            {
                if (_commands.ContainsKey(c.Id))
                    throw new VnRuntimeException($"Duplicate command id: {c.Id}");
                foreach (var e in c.Effects)
                {
                    if (!resourceIds.Contains(e.ResourceId))
                        throw new VnRuntimeException(
                            $"Command '{c.Id}' references undefined resource: {e.ResourceId}");
                }
                _commands.Add(c.Id, c);
                _commandList.Add(c);
            }
        }

        public SimState CreateInitialState()
        {
            var res = new Dictionary<string, int>();
            foreach (var r in _resources)
                res[r.Id] = r.StartValue;
            return new SimState(1, res);
        }
    }
}
