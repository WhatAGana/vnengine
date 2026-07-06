using System.Collections.Generic;
using UnityEngine;

namespace VNEngine.Unity
{
    [CreateAssetMenu(fileName = "Command", menuName = "VNEngine/Sim/Command Definition")]
    public sealed class CommandDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct Effect
        {
            public string resourceId;
            public int amount;
        }

        public string id;
        public string displayName;
        public List<Effect> effects = new List<Effect>();

        public CommandDef ToDef()
        {
            var deltas = new List<ResourceDelta>(effects.Count);
            foreach (var e in effects)
                deltas.Add(new ResourceDelta(e.resourceId, e.amount));
            return new CommandDef(id, displayName, deltas);
        }
    }
}
