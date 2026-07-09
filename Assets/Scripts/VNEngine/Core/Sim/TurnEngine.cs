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

        public RunState CreateInitialState()
        {
            var res = new Dictionary<string, int>();
            foreach (var r in _resources)
                res[r.Id] = r.StartValue;
            return new RunState(1, res);
        }

        public RunState ExecuteCommand(RunState state, string commandId)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (!_commands.TryGetValue(commandId, out var cmd))
                throw new VnRuntimeException($"Unknown command: {commandId}");

            var res = new Dictionary<string, int>(state.Resources.Count);
            foreach (var kv in state.Resources)
                res[kv.Key] = kv.Value;

            foreach (var e in cmd.Effects)
                res[e.ResourceId] = (res.TryGetValue(e.ResourceId, out var cur) ? cur : 0) + e.Amount;

            return new RunState(state.Day + 1, res, state.Captives, state.PullsThisLoop);
        }
    }
}
